package se.automatiziraj.termodom;

import org.json.JSONArray;
import org.json.JSONObject;

import java.time.Duration;
import java.time.Instant;
import java.time.LocalDateTime;
import java.time.format.DateTimeFormatter;
import java.util.Locale;

/** Converts the original, timestamped read API into a schematic snapshot. */
final class HouseTelemetry {
    static final int MUTED = 0xffa1b6c1;
    private static final Locale HR = Locale.forLanguageTag("hr-HR");
    private static final DateTimeFormatter STAMP = DateTimeFormatter.ofPattern("dd.MM.yyyy. HH:mm:ss", HR);
    enum Pump { ON, OFF, UNKNOWN }
    enum Age { FRESH, MUTED, OLD, UNKNOWN }

    static final class Packet {
        final JSONObject data;
        final Instant observed;
        final LocalDateTime server;

        Packet(JSONObject data, Instant observed) {
            this.data = data;
            this.observed = observed;
            this.server = parseTime(data.optString("server_time"));
        }

        Reading find(String array, String key, String id, String value, String at) {
            JSONArray rows = data.optJSONArray(array);
            JSONObject newest = null;
            LocalDateTime time = null;
            for (int i = 0; rows != null && i < rows.length(); i++) {
                JSONObject row = rows.optJSONObject(i);
                if (row == null || !id.equals(row.optString(key))) continue;
                LocalDateTime candidate = parseTime(row.optString(at));
                if (newest == null || candidate != null && (time == null || candidate.isAfter(time))) {
                    newest = row;
                    time = candidate;
                }
            }
            return newest == null ? Reading.EMPTY : new Reading(newest.optDouble(value, Double.NaN), server, time, observed);
        }
    }

    static final class Reading {
        static final Reading EMPTY = new Reading(Double.NaN, null, null, null);
        final double value;
        final String degrees;
        final LocalDateTime at, server;
        final Instant observed;

        private Reading(double value, LocalDateTime server, LocalDateTime at, Instant observed) {
            this.value = Double.isFinite(value) ? value : Double.NaN;
            this.degrees = Double.isFinite(value) ? String.format(HR, "%.1f°", value) : "—";
            this.at = at;
            this.server = server;
            this.observed = observed;
        }

        Age age(Instant now) {
            if (!Double.isFinite(value) || at == null || observed == null || server == null) return Age.UNKNOWN;
            Duration base = Duration.between(at, server);
            if (base.isNegative()) return Age.UNKNOWN;
            Duration elapsed = Duration.between(observed, now);
            Duration age = elapsed.isNegative() ? base : base.plus(elapsed);
            if (age.compareTo(Duration.ofMinutes(10)) > 0) return Age.OLD;
            if (age.compareTo(Duration.ofMinutes(3)) > 0) return Age.MUTED;
            return Age.FRESH;
        }

        String tag(Instant now) {
            switch (age(now)) {
                case MUTED: return "zastarjelo";
                case OLD: return "zadnje " + at.format(STAMP);
                case UNKNOWN: return Double.isFinite(value) ? "vrijeme nije dostupno" : "nije dostupno";
                default: return "";
            }
        }
    }

    final Reading kat, katTarget, prizemlje, prizemljeTarget, outside, boiler, sanitary, heatPump, collector;
    final Reading katSupply, prizemljeSupply, extraSupply, extraSensor;
    final Reading[] buffer, bufferTwo, pumpReadings;

    HouseTelemetry(Packet data, Packet pumps) { this(data, pumps, false); }

    HouseTelemetry(Packet data, Packet pumps, boolean demo) {
        kat = room(data, "kat", "temperature");
        katTarget = room(data, "kat", "setpoint");
        prizemlje = room(data, "prizemlje", "temperature");
        prizemljeTarget = room(data, "prizemlje", "setpoint");
        outside = sensor(data, "house/outdoor/temp");
        boiler = sensor(data, "house/kotal/temp");
        sanitary = sensor(data, "house/dhw/temp");
        collector = sensor(data, "house/solar/temp");
        katSupply = sensor(data, "house/polaz_kat/temp");
        prizemljeSupply = sensor(data, "house/polaz_priz/temp");
        extraSupply = sensor(data, "house/polaz_priz2/temp");
        extraSensor = sensor(data, "house/sensor1/temp");
        heatPump = demo ? sensor(data, "demo/heat-pump") : Reading.EMPTY;
        // The owner confirmed that these three source topics belong to the 2,000 L tank.
        buffer = new Reading[]{sensor(data, "house/puffer/top"), sensor(data, "house/puffer/midd"), sensor(data, "house/puffer/bottom")};
        bufferTwo = demo ? new Reading[]{sensor(data, "demo/buffer2/top"), sensor(data, "demo/buffer2/midd"), sensor(data, "demo/buffer2/bottom")}
                : new Reading[]{Reading.EMPTY, Reading.EMPTY, Reading.EMPTY};
        pumpReadings = new Reading[]{pump(pumps, "kat"), pump(pumps, "prizemlje"), pump(pumps, "kotao"),
                pump(pumps, "sanitarna"), pump(pumps, "solar")};
    }

    private static Reading room(Packet data, String id, String value) {
        return data == null ? Reading.EMPTY : data.find("room_latest", "room", id, value, "received_at");
    }

    private static Reading sensor(Packet data, String topic) {
        return data == null ? Reading.EMPTY : data.find("latest", "topic", topic, "temperature", "received_at");
    }

    private static Reading pump(Packet data, String id) {
        return data == null ? Reading.EMPTY : data.find("pumps", "pump", id, "state", "updated_at");
    }

    static LocalDateTime parseTime(String raw) {
        try { return LocalDateTime.parse(raw.replace(' ', 'T')); }
        catch (Exception invalid) { return null; }
    }

    Pump pumpState(int index, Instant now) {
        Reading pump = pumpReadings[index];
        if (pump.age(now) == Age.UNKNOWN) return Pump.UNKNOWN;
        return pump.value == 1 ? Pump.ON : pump.value == 0 ? Pump.OFF : Pump.UNKNOWN;
    }

    boolean hasRunning(Instant now) {
        for (int i = 0; i < pumpReadings.length; i++) if (pumpState(i, now) == Pump.ON && pumpReadings[i].age(now) == Age.FRESH) return true;
        return false;
    }

    static int valueColor(Reading reading, int color, Instant now) {
        return reading.age(now) == Age.FRESH ? color : MUTED;
    }

    static String degrees(Reading reading) {
        return reading.degrees;
    }

    static String degrees(double value) {
        return Double.isFinite(value) ? String.format(HR, "%.1f °C", value) : "—";
    }

    static final String[] PUMP_NAMES = {"Kat", "Prizemlje", "Peć", "Sanitarna voda", "Solarni kolektori"};

    String description() {
        Instant now = Instant.now();
        return "Presjek kuće. Kat: " + spoken(kat, now) + ", zadano " + spoken(katTarget, now)
                + ". Prizemlje: " + spoken(prizemlje, now) + ", zadano " + spoken(prizemljeTarget, now)
                + ". Krovni solarni kolektori: " + spoken(collector, now)
                + ". Kotlovnica. Pufer 2.000 L: " + spokenBuffer(buffer, now)
                + ". Pufer 1.000 L: " + spokenBuffer(bufferTwo, now) + ". Peć: " + spoken(boiler, now)
                + ". Sanitarna voda: " + spoken(sanitary, now) + ". Dizalica topline: " + spoken(heatPump, now) + ".";
    }

    private static String spokenBuffer(Reading[] levels, Instant now) {
        return "vrh " + spoken(levels[0], now) + ", sredina " + spoken(levels[1], now) + ", dno " + spoken(levels[2], now);
    }

    private static String spoken(Reading reading, Instant now) {
        if (!Double.isFinite(reading.value)) return "nije dostupno";
        String tag = reading.tag(now);
        return reading.degrees + (tag.isEmpty() ? "" : ", " + tag);
    }
}
