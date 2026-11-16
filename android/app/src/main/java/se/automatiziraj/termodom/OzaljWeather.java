package se.automatiziraj.termodom;

import org.json.JSONArray;
import org.json.JSONObject;

import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.nio.charset.StandardCharsets;
import java.time.LocalDateTime;
import java.time.format.DateTimeFormatter;
import java.util.concurrent.TimeUnit;

import okhttp3.OkHttpClient;
import okhttp3.Request;
import okhttp3.Response;
import okhttp3.ResponseBody;

/** Fixed location weather estimate; no location permission, credentials or heating data. */
final class OzaljWeather {
    private static final String URL = "https://api.open-meteo.com/v1/forecast?latitude=45.61293&longitude=15.47771"
            + "&current=temperature_2m,apparent_temperature,relative_humidity_2m,weather_code,wind_speed_10m,pressure_msl,is_day"
            + "&daily=temperature_2m_max,temperature_2m_min,precipitation_probability_max,sunrise,sunset"
            + "&timezone=Europe%2FZagreb&forecast_days=1";
    private static final DateTimeFormatter CLOCK = DateTimeFormatter.ofPattern("HH:mm");
    private static final DateTimeFormatter STAMP = DateTimeFormatter.ofPattern("dd.MM. HH:mm");
    private final OkHttpClient client = new OkHttpClient.Builder().retryOnConnectionFailure(false)
            .followRedirects(false).followSslRedirects(false).callTimeout(12, TimeUnit.SECONDS).build();

    static final class Reading {
        final double outside, feelsLike, humidity, wind, pressure, low, high, rainChance;
        final int code;
        final boolean isDay;
        final LocalDateTime time;
        final String sunrise, sunset;

        Reading(JSONObject data) throws Exception {
            JSONObject current = data.getJSONObject("current"), daily = data.optJSONObject("daily");
            outside = current.optDouble("temperature_2m", Double.NaN);
            time = HouseTelemetry.parseTime(current.optString("time"));
            if (!Double.isFinite(outside) || time == null || data.optBoolean("error")) {
                throw new IOException("Vremenski izvor nije vratio valjane podatke.");
            }
            feelsLike = current.optDouble("apparent_temperature", Double.NaN);
            humidity = current.optDouble("relative_humidity_2m", Double.NaN);
            wind = current.optDouble("wind_speed_10m", Double.NaN);
            pressure = current.optDouble("pressure_msl", Double.NaN);
            code = current.optInt("weather_code", -1);
            isDay = current.optInt("is_day", 1) == 1;
            low = dailyNumber(daily, "temperature_2m_min");
            high = dailyNumber(daily, "temperature_2m_max");
            rainChance = dailyNumber(daily, "precipitation_probability_max");
            sunrise = dailyClock(daily, "sunrise");
            sunset = dailyClock(daily, "sunset");
        }

        String timestamp() { return time.format(STAMP); }
        private static double dailyNumber(JSONObject daily, String key) {
            JSONArray array = daily == null ? null : daily.optJSONArray(key);
            return array == null ? Double.NaN : array.optDouble(0, Double.NaN);
        }
        private static String dailyClock(JSONObject daily, String key) {
            JSONArray array = daily == null ? null : daily.optJSONArray(key);
            LocalDateTime time = array == null ? null : HouseTelemetry.parseTime(array.optString(0));
            return time == null ? "—" : time.format(CLOCK);
        }

        String conditions() {
            switch (code) {
                case 0: return "Vedro";
                case 1: return "Pretežno vedro";
                case 2: return "Djelomično oblačno";
                case 3: return "Oblačno";
                case 45: case 48: return "Magla";
                case 51: case 53: case 55: return "Rosulja";
                case 56: case 57: return "Ledena rosulja";
                case 61: case 63: case 65: return "Kiša";
                case 66: case 67: return "Ledena kiša";
                case 71: case 73: case 75: case 77: return "Snijeg";
                case 80: case 81: case 82: return "Pljuskovi";
                case 85: case 86: return "Snježni pljuskovi";
                case 95: case 97: return "Grmljavina";
                case 96: case 99: return "Grmljavina i tuča";
                default: return "Uvjeti nisu dostupni";
            }
        }
    }

    Reading read() throws Exception {
        Request request = new Request.Builder().url(URL).get().header("Accept", "application/json").build();
        try (Response response = client.newCall(request).execute()) {
            ResponseBody body = response.body();
            if (!response.isSuccessful() || body == null || body.contentLength() > 65_536) {
                throw new IOException("Vremenski izvor nije dostupan.");
            }
            try (InputStream input = body.byteStream(); ByteArrayOutputStream output = new ByteArrayOutputStream()) {
                byte[] buffer = new byte[4096];
                for (int n; (n = input.read(buffer)) != -1; ) {
                    if (output.size() + n > 65_536) throw new IOException("Vremenski odgovor je prevelik.");
                    output.write(buffer, 0, n);
                }
                return new Reading(new JSONObject(output.toString(StandardCharsets.UTF_8.name())));
            }
        }
    }
}
