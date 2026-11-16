using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace TermoDom;

internal enum ReadingAge { Unknown, Current, Muted, Outdated }

internal sealed record HouseReading<T>(T? Value, DateTime? MeasuredAt, DateTime? ServerTime, DateTimeOffset FetchedAt) where T : struct
{
    public ReadingAge Age(DateTimeOffset now)
    {
        if (!Value.HasValue || !MeasuredAt.HasValue || !ServerTime.HasValue) return ReadingAge.Unknown;
        var initialAge = ServerTime.Value - MeasuredAt.Value;
        if (initialAge < TimeSpan.FromSeconds(-30)) return ReadingAge.Unknown;
        var age = TimeSpan.FromTicks(Math.Max(initialAge.Ticks, 0) + Math.Max((now - FetchedAt).Ticks, 0));
        if (age > TimeSpan.FromMinutes(10)) return ReadingAge.Outdated;
        return age > TimeSpan.FromMinutes(3) ? ReadingAge.Muted : ReadingAge.Current;
    }
}

internal sealed record HouseRoom(HouseReading<double> Measured, HouseReading<double> Target);
internal sealed record HouseTelemetry(IReadOnlyDictionary<string, HouseReading<double>> Temperatures, IReadOnlyDictionary<string, HouseRoom> Rooms)
{
    public HouseReading<double>? Temperature(string id) => Temperatures.GetValueOrDefault(id);
    public HouseRoom? Room(string id) => Rooms.GetValueOrDefault(id);
}

internal sealed record WeatherDay(DateOnly Date, int? Code, double? High, double? Low, double? RainChance);
internal sealed record OzaljWeather(DateTime? MeasuredAt, double? Temperature, double? FeelsLike, double? Humidity,
    int? Code, bool IsDay, double? Wind, double? Pressure, double? RainChance, IReadOnlyList<WeatherDay> Days);

internal static class HouseTelemetrySource
{
    const string Controller = "https://automatiziraj.se/centralno/php/m.php";
    internal const string WeatherUrl = "https://api.open-meteo.com/v1/forecast?latitude=45.61293&longitude=15.47771"
        + "&current=temperature_2m,apparent_temperature,relative_humidity_2m,weather_code,is_day,wind_speed_10m,pressure_msl"
        + "&hourly=precipitation_probability&daily=weather_code,temperature_2m_max,temperature_2m_min,precipitation_probability_max"
        + "&timezone=Europe%2FZagreb&forecast_days=5";
    internal static readonly string[] PumpIds = ["kotao", "prizemlje", "kat", "sanitarna", "solar"];
    static readonly HashSet<string> Topics = new(StringComparer.Ordinal)
    {
        "house/puffer/top", "house/puffer/midd", "house/puffer/bottom", "house/kotal/temp", "house/dhw/temp",
        "house/solar/temp", "house/outdoor/temp", "house/polaz_priz/temp", "house/polaz_kat/temp",
        "house/polaz_priz2/temp", "house/sensor1/temp"
    };
    static readonly HttpClient Http = new(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

    public static async Task<HouseTelemetry> ReadTemperatures(CancellationToken cancellation)
    {
        using var document = await ReadJson(Controller + "?action=data", cancellation);
        return ParseTemperatures(document.RootElement, DateTimeOffset.UtcNow);
    }

    public static async Task<IReadOnlyDictionary<string, HouseReading<bool>>> ReadPumps(CancellationToken cancellation)
    {
        using var document = await ReadJson(Controller + "?action=pumps", cancellation);
        return ParsePumps(document.RootElement, DateTimeOffset.UtcNow);
    }

    public static async Task<OzaljWeather> ReadWeather(CancellationToken cancellation)
    {
        using var document = await ReadJson(WeatherUrl, cancellation);
        return ParseWeather(document.RootElement);
    }

    static async Task<JsonDocument> ReadJson(string url, CancellationToken cancellation)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("TermoDom/2.1-native-Windows");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
        using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        response.EnsureSuccessStatusCode();
        const int limit = 4_000_000;
        if (response.Content.Headers.ContentLength > limit) throw new ControllerException("Izvor je vratio prevelik odgovor.");
        await using var input = await response.Content.ReadAsStreamAsync(timeout.Token);
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        int count;
        while ((count = await input.ReadAsync(buffer, timeout.Token)) > 0)
        {
            if (output.Length + count > limit) throw new ControllerException("Izvor je vratio prevelik odgovor.");
            output.Write(buffer, 0, count);
        }
        return JsonDocument.Parse(output.ToArray());
    }

    internal static HouseTelemetry ParseTemperatures(JsonElement root, DateTimeOffset now)
    {
        var clock = ReadClock(root);
        var temperatures = new Dictionary<string, HouseReading<double>>(StringComparer.Ordinal);
        foreach (var item in RequiredArray(root, "latest"))
        {
            var topic = Text(item, "topic");
            if (topic != null && Topics.Contains(topic))
                AppendLatest(temperatures, topic, new(Number(Member(item, "temperature")), Date(item, "received_at"), clock, now));
        }

        var rooms = new Dictionary<string, HouseRoom>(StringComparer.Ordinal);
        foreach (var item in RequiredArray(root, "room_latest"))
        {
            var id = Text(item, "room");
            if (id is not ("kat" or "prizemlje")) continue;
            var received = Date(item, "received_at");
            if (rooms.TryGetValue(id, out var old) && (received ?? DateTime.MinValue) <= (old.Measured.MeasuredAt ?? DateTime.MinValue)) continue;
            rooms[id] = new(new(Number(Member(item, "temperature")), received, clock, now),
                new(Number(Member(item, "setpoint")), received, clock, now));
        }
        return new(temperatures, rooms);
    }

    internal static IReadOnlyDictionary<string, HouseReading<bool>> ParsePumps(JsonElement root, DateTimeOffset now)
    {
        var clock = ReadClock(root);
        var pumps = new Dictionary<string, HouseReading<bool>>(StringComparer.Ordinal);
        foreach (var item in RequiredArray(root, "pumps"))
        {
            var id = Text(item, "pump");
            if (id == null || !PumpIds.Contains(id, StringComparer.Ordinal)) continue;
            var state = Member(item, "state");
            bool? enabled = state.ValueKind == JsonValueKind.Number && state.TryGetInt32(out var flag) && flag is 0 or 1 ? flag == 1 : null;
            AppendLatest(pumps, id, new(enabled, Date(item, "updated_at"), clock, now));
        }
        return pumps;
    }

    internal static OzaljWeather ParseWeather(JsonElement root)
    {
        var current = Member(root, "current");
        var currentTime = Date(current, "time");
        if (currentTime == null || Number(Member(current, "temperature_2m")) is not double temperature)
            throw new ControllerException("Open-Meteo nije vratio aktualno vrijeme za Ozalj.");
        var hourly = Member(root, "hourly");
        var hours = Array(hourly, "time");
        var hourlyProbability = Array(hourly, "precipitation_probability");
        var currentHour = currentTime.Value.ToString("yyyy-MM-dd'T'HH:00", CultureInfo.InvariantCulture);
        int hourIndex = System.Array.FindIndex(hours, hour => hour.ValueKind == JsonValueKind.String && hour.GetString() == currentHour);
        var chance = hourIndex >= 0 ? Number(hourlyProbability.ElementAtOrDefault(hourIndex)) : null;

        var daily = Member(root, "daily");
        var dates = Array(daily, "time");
        var highs = Array(daily, "temperature_2m_max");
        var lows = Array(daily, "temperature_2m_min");
        var codes = Array(daily, "weather_code");
        var probabilities = Array(daily, "precipitation_probability_max");
        var days = new List<WeatherDay>();
        for (var i = 0; i < Math.Min(5, dates.Length); i++)
        {
            if (dates[i].ValueKind != JsonValueKind.String || !DateOnly.TryParseExact(dates[i].GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)) continue;
            days.Add(new(day, Integer(codes.ElementAtOrDefault(i)), Number(highs.ElementAtOrDefault(i)), Number(lows.ElementAtOrDefault(i)), Number(probabilities.ElementAtOrDefault(i))));
        }

        return new(currentTime, temperature, Number(Member(current, "apparent_temperature")), Number(Member(current, "relative_humidity_2m")),
            Integer(Member(current, "weather_code")), Integer(Member(current, "is_day")) != 0, Number(Member(current, "wind_speed_10m")),
            Number(Member(current, "pressure_msl")), chance, days);
    }

    internal static string WeatherDescription(int? code, bool isDay = true) => code switch
    {
        0 => isDay ? "Sunčano" : "Vedro", 1 => "Pretežno vedro", 2 => "Djelomice oblačno", 3 => "Oblačno",
        45 or 48 => "Magla", >= 51 and <= 57 => "Rosulja", >= 61 and <= 67 => "Kiša", >= 71 and <= 77 => "Snijeg",
        >= 80 and <= 82 => "Pljuskovi", 85 or 86 => "Snježni pljuskovi", >= 95 and <= 99 => "Grmljavina", _ => "Stanje nije dostupno"
    };

    static void AppendLatest<T>(Dictionary<string, HouseReading<T>> readings, string id, HouseReading<T> value) where T : struct
    {
        if (!readings.TryGetValue(id, out var old) || (value.MeasuredAt ?? DateTime.MinValue) > (old.MeasuredAt ?? DateTime.MinValue)) readings[id] = value;
    }

    static DateTime? ReadClock(JsonElement root)
    {
        if (Member(root, "success").ValueKind != JsonValueKind.True) throw new ControllerException("Regulator nije vratio podatke presjeka kuće.");
        return Date(root, "server_time");
    }

    static JsonElement.ArrayEnumerator RequiredArray(JsonElement root, string name)
    {
        var member = Member(root, name);
        if (member.ValueKind != JsonValueKind.Array) throw new ControllerException("Regulator nije vratio očekivani oblik podataka presjeka kuće.");
        return member.EnumerateArray();
    }

    static JsonElement[] Array(JsonElement root, string name) => Member(root, name) is { ValueKind: JsonValueKind.Array } value ? value.EnumerateArray().ToArray() : [];
    static JsonElement Member(JsonElement root, string name) => root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var value) ? value : default;
    static string? Text(JsonElement root, string name) => Member(root, name) is { ValueKind: JsonValueKind.String } value ? value.GetString() : null;
    static int? Integer(JsonElement value) => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : null;

    static double? Number(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) && double.IsFinite(number)) return number;
        if (value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number) && double.IsFinite(number)) return number;
        return null;
    }

    static DateTime? Date(JsonElement root, string name) => DateTime.TryParseExact(Text(root, name),
        ["yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd'T'HH:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var value) ? value : null;
}
