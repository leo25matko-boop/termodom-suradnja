package se.automatiziraj.termodom;

import org.json.JSONArray;
import org.json.JSONObject;
import org.junit.Test;

import java.time.Instant;
import java.time.LocalDateTime;

import static org.junit.Assert.*;

public final class HouseTelemetryTest {
    private static final Instant FETCHED = Instant.parse("2026-11-16T14:00:00Z");
    private static final LocalDateTime SERVER = LocalDateTime.parse("2026-11-16T15:00:00");

    @Test public void ageUsesSourceClockAndExpiresWhileCached() throws Exception {
        JSONObject data = new JSONObject().put("room_latest", new JSONArray()
                .put(room("kat", 19, 21, SERVER.minusMinutes(7)))
                .put(room("kat", 22.35, 23, SERVER.minusMinutes(3)))
                .put(room("prizemlje", 18.3, 22, SERVER.minusMinutes(10))));
        HouseTelemetry model = new HouseTelemetry(packet(data), null);
        assertEquals(22.35, model.kat.value, 0);
        assertEquals(23, model.katTarget.value, 0);
        assertEquals(HouseTelemetry.Age.FRESH, model.kat.age(FETCHED));
        assertEquals(HouseTelemetry.Age.MUTED, model.kat.age(FETCHED.plusSeconds(1)));
        assertEquals(HouseTelemetry.Age.MUTED, model.prizemlje.age(FETCHED));
        assertEquals(HouseTelemetry.Age.OLD, model.prizemlje.age(FETCHED.plusSeconds(1)));
        assertEquals("zadnje 16.11.2026. 14:50:00", model.prizemlje.tag(FETCHED.plusSeconds(1)));
    }

    @Test public void staleKnownPumpKeepsLastStateWithoutAnimation() throws Exception {
        JSONObject data = new JSONObject().put("pumps", new JSONArray()
                .put(pump("kat", 1, SERVER.minusMinutes(3)))
                .put(pump("prizemlje", 0, SERVER.minusMinutes(2)))
                .put(pump("kotao", 7, SERVER.minusMinutes(1)))
                .put(pump("solar", 1, SERVER.plusSeconds(2))));
        HouseTelemetry model = new HouseTelemetry(null, packet(data));
        assertTrue(model.hasRunning(FETCHED));
        assertFalse(model.hasRunning(FETCHED.plusSeconds(1)));
        assertEquals(HouseTelemetry.Pump.ON, model.pumpState(0, FETCHED.plusSeconds(1)));
        assertEquals(HouseTelemetry.Pump.OFF, model.pumpState(1, FETCHED));
        assertEquals(HouseTelemetry.Pump.UNKNOWN, model.pumpState(2, FETCHED));
        assertEquals(HouseTelemetry.Pump.UNKNOWN, model.pumpState(3, FETCHED));
        assertEquals(HouseTelemetry.Pump.UNKNOWN, model.pumpState(4, FETCHED));
    }

    @Test public void unverifiedDeviceIsNotFilledWithAnotherSensorsTemperature() throws Exception {
        JSONObject data = new JSONObject().put("latest", new JSONArray()
                .put(sensor("house/puffer/top", 58)).put(sensor("house/puffer/midd", 50))
                .put(sensor("house/puffer/bottom", 39)).put(sensor("house/kotal/temp", 65))
                .put(sensor("house/outdoor/temp", 0)).put(sensor("house/sensor1/temp", 40)));
        HouseTelemetry model = new HouseTelemetry(packet(data), null);
        assertEquals(58, model.buffer[0].value, 0);
        assertEquals(50, model.buffer[1].value, 0);
        assertEquals(39, model.buffer[2].value, 0);
        assertEquals(0, model.outside.value, 0);
        assertEquals(65, model.boiler.value, 0);
        assertEquals(40, model.extraSensor.value, 0);
        assertTrue(Double.isNaN(model.bufferTwo[0].value));
        assertTrue(Double.isNaN(model.heatPump.value));
    }

    @Test public void readOnlyTelemetryDoesNotAllowOtherQueryActions() throws Exception {
        int[] callCount = {0};
        HeatingRepository repository = new HeatingRepository((uri, headers) -> {
            callCount[0]++;
            assertEquals("/centralno/php/m.php", uri.getPath());
            assertEquals("application/json", headers.get("Accept"));
            assertEquals("no-cache", headers.get("Cache-Control"));
            String key = "action=pumps".equals(uri.getRawQuery()) ? "pumps" : "latest";
            return new HeatingRepository.HttpResponse(200, packet(new JSONObject().put("success", true).put(key, new JSONArray())).data.toString());
        });
        repository.telemetry(false);
        repository.telemetry(true);
        assertEquals(2, callCount[0]);
        for (String path : new String[]{"m.php", "m.php?action=set", "m.php?action=pumps&extra=true", "m.php?action=data&action=pumps"}) {
            try { HeatingRepository.safeUrl(path); fail("Query outside the read allowlist accepted: " + path); }
            catch (HeatingRepository.AppException expected) { assertEquals(400, expected.status); }
        }
    }

    @Test public void forecastAndMeasuredOutsideAreNeverInterchanged() throws Exception {
        JSONObject data = new JSONObject().put("current", new JSONObject().put("temperature_2m", 10.9)
                .put("time", SERVER.toString()).put("weather_code", 61).put("is_day", 0));
        OzaljWeather.Reading reading = new OzaljWeather.Reading(data);
        assertEquals("Kiša", reading.conditions());
        assertEquals(10.9, reading.outside, 0);
        assertFalse(reading.isDay);
        assertTrue(Double.isNaN(reading.humidity));
        HouseTelemetry model = new HouseTelemetry(null, null);
        assertTrue(Double.isNaN(model.outside.value));
    }

    private static HouseTelemetry.Packet packet(JSONObject data) throws Exception {
        return new HouseTelemetry.Packet(data.put("server_time", SERVER.toString()), FETCHED);
    }

    private static JSONObject room(String id, double measured, double target, LocalDateTime time) throws Exception {
        return new JSONObject().put("room", id).put("temperature", measured).put("setpoint", target).put("received_at", time.toString());
    }

    private static JSONObject pump(String id, int state, LocalDateTime time) throws Exception {
        return new JSONObject().put("pump", id).put("state", state).put("updated_at", time.toString());
    }

    private static JSONObject sensor(String id, double value) throws Exception {
        return new JSONObject().put("topic", id).put("temperature", value).put("received_at", SERVER.minusSeconds(4).toString());
    }
}
