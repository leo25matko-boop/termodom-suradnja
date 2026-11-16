using System.IO;
using System.Net;
using System.Text.Json;

namespace TermoDom;

/// <summary>Explicit verification mode uses synthetic fixtures. Production never substitutes sample data.</summary>
public static class NativeVerification
{
    public static void TestOriginalSource(string directory)
    {
        var status = HeatingParser.Status(File.ReadAllText(Path.Combine(directory, "https-original.html")));
        if (status.Zones.Count != 2) throw new Exception("Original source zones not parsed.");
        foreach (var id in new[] { "kat", "prizemlje" })
        {
            var schedule = HeatingParser.Schedule(File.ReadAllText(Path.Combine(directory, id + ".php.html")), id);
            HeatingParser.ValidateSchedule(schedule.Fields.Select(f => f.Value).ToArray(), schedule);
        }
        var settings = HeatingParser.Settings(File.ReadAllText(Path.Combine(directory, "podesavanje.php.html")));
        HeatingParser.ValidateSettings(settings.Fields.Select(f => f.Value).ToArray());
        var valid = 0; var unavailable = 0;
        foreach (var meta in ChartParser.Catalog)
        {
            var html = File.ReadAllText(Path.Combine(directory, meta.Path + ".html"));
            try
            {
                var chart = ChartParser.Parse(html, meta.Id);
                if (chart.Series.Count == 0) throw new Exception("Original chart not parsed: " + meta.Id);
                valid++;
            }
            catch (ControllerException) when (!html.Contains("datasets", StringComparison.Ordinal)) { unavailable++; }
        }
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "original-source-tests.txt"), $"TERMODOM_WINDOWS_ORIGINAL_SOURCE_OK\nStatus, both 24h schedules, six settings and {valid} original chart pages parsed (empty datasets preserved); {unavailable} upstream non-chart pages rejected; no network/control commands.\n");
    }
    public static string ScheduleHtml(string id, double[]? values = null)
    {
        values ??= Enumerable.Range(0, 24).Select(i => i < 6 || i > 21 ? 28d : 35d).ToArray();
        return "<form action='dodajPODESAVANJA.php' method='get'>" + string.Join("", values.Select((v, i) => $"<input name='n{i}' type='range' min='0' max='60' step='0.1' value='{v.ToString(System.Globalization.CultureInfo.InvariantCulture)}'>")) + "<input type='hidden' name='kat' value='" + (id == "kat" ? "2" : "1") + "'></form>";
    }
    public static string SettingsHtml(double[]? values = null)
    {
        values ??= [40, 35, 21.75, 0.15, 6.8, 0];
        string[] labels = ["Temperatura peći", "Temperatura vode", "Ciljana sobna temperatura", "Korekcija regulatora", "Temperaturni prag", "Stanje klimatizacije"];
        return "<form action='zapisi_postavke.php' method='get'>" + string.Join("", values.Select((v, i) => $"<label>{labels[i]}</label><input name='n{i}' value='{v.ToString(System.Globalization.CultureInfo.InvariantCulture)}'>")) + "</form>";
    }
    public const string StatusHtml = """
        <body><p>2026-10-22 20:12:00 vrijeme sistema 20:12:00 Resetirano: 2 Vanjska temperatura 12.4 Posto 78%</p>
        <p>SYNC Kat u 20:11</p><p>SYNC Prizemlje u 20:11</p>
        <a href='graf_kat_temp.php'><span>22.3</span></a><p>Trenutno je 35.5 <img src='/img/ok.png'><span id='ztk'>36</span></p>
        <a href='graf_prizemlje_temp.php'><span>21.7</span></a><p>Trenutno je 34.2 <img src='/img/no.png'><span id='ztp'>34</span></p>
        <a href='graf_puffer_temp.php'>Puffer</a><div class='tekst-puffer'><p>vrh</p><h2>58.7</h2><p>sredina</p><h2>49.2</h2><p>dno</p><h2>36.8</h2></div>
        <a href='graf_sanitarna_temp.php'><img src='/img/no.png'></a><a href='graf_pec_temp2.php'>Peć</a>
        <div class='tekst-pec'><h2>46.3</h2><h2>65.8</h2></div><div class='pump_pec'><img src='/img/ok.png'></div><div class='vent'><img src='/img/no.png'></div></body>
        """;
    public const string ChartHtml = """
        <script>const labels = ['00:00','02:00','04:00','06:00','08:00','10:00','12:00','14:00','16:00','18:00','20:00','22:00'];
        const actual = [20.7,20.5,20.3,20.8,21.7,22.3,22.6,null,22.2,22.4,22.3,22.1];
        new Chart(document.getElementById('c'), {data:{labels:labels,datasets:[{label:'Temperatura',data:actual,borderColor:'#79D8C4'},
        {label:'Ciljana temperatura',data:Array(labels.length).fill(22),borderColor:'#E7B469',steppedLine:true}]},options:{}});</script>
        """;
    public static HeatingRepository Repository() => new((uri, _, mutation) =>
    {
        if (mutation || uri.Query.Length != 0) throw new InvalidOperationException("UI fixtures never send control commands.");
        var name = System.IO.Path.GetFileName(uri.AbsolutePath);
        var html = name switch { "novo.php" => StatusHtml, "kat.php" => ScheduleHtml("kat"), "prizemlje.php" => ScheduleHtml("prizemlje"), "podesavanje.php" => SettingsHtml(), _ when ChartParser.Catalog.Any(x => x.Path == name) => ChartHtml, _ => throw new InvalidOperationException("Unknown fixture") };
        return Task.FromResult(new SourceResponse(200, html));
    });
    public static SolarData SolarFixture()
    {
        var inner = JsonSerializer.Serialize(new { stationOverview = new { stationName = "Demo elektrana · provjera izgleda" }, realKpi = new { realTimePower = 3.24, dailyEnergy = 18.7, monthEnergy = 436.2, yearEnergy = 5032.8, cumulativeEnergy = 12784.6 }, socialContribution = new { co2Reduction = 5420.3, standardCoalSavings = 2140.6, equivalentTreePlanting = 312 }, powerCurve = new { xAxis = Enumerable.Range(0, 24).Select(i => i.ToString("00") + ":00"), activePower = Enumerable.Range(0, 24).Select(i => i < 6 || i > 18 ? "-" : (Math.Sin((i - 6) / 12d * Math.PI) * 4.2).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)) } });
        return SolarRepository.Parse(JsonSerializer.Serialize(new { success = true, data = WebUtility.HtmlEncode(inner) }));
    }

    internal static (HouseTelemetry Telemetry, IReadOnlyDictionary<string, HouseReading<bool>> Pumps, OzaljWeather Weather) HouseFixture()
    {
        var now = DateTimeOffset.UtcNow;
        var clock = new DateTime(2026, 11, 16, 15, 39, 0);
        string Time(int minutes) => clock.AddMinutes(-minutes).ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        using var readings = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            success = true,
            server_time = Time(0),
            latest = new[]
            {
                ("house/outdoor/temp", "9.4", 1), ("house/kotal/temp", "66.2", 1), ("house/dhw/temp", "51.6", 1),
                ("house/solar/temp", "32.5", 15), ("house/puffer/top", "62.3", 1), ("house/puffer/midd", "48.7", 1),
                ("house/puffer/bottom", "36.4", 4), ("house/polaz_kat/temp", "35.8", 1), ("house/polaz_priz/temp", "34.5", 1),
                ("house/polaz_priz2/temp", "32.9", 16), ("house/sensor1/temp", "19.4", 18)
            }.Select(value => new { topic = value.Item1, temperature = value.Item2, received_at = Time(value.Item3) }),
            room_latest = new[]
            {
                new { room = "kat", temperature = "22.3", setpoint = "22", received_at = Time(1) },
                new { room = "prizemlje", temperature = "21.7", setpoint = "22.5", received_at = Time(1) }
            }
        }));
        using var pumps = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            success = true,
            server_time = Time(0),
            pumps = new[] { ("kotao", 1, 1), ("prizemlje", 1, 4), ("kat", 1, 1), ("sanitarna", 0, 1), ("solar", 0, 13) }
                .Select(x => new { pump = x.Item1, state = x.Item2, updated_at = Time(x.Item3) })
        }));
        using var weather = JsonDocument.Parse("""
        {"current":{"time":"2026-11-16T15:30","temperature_2m":10.2,"apparent_temperature":8.1,"relative_humidity_2m":71,
        "weather_code":2,"is_day":1,"wind_speed_10m":8.6,"pressure_msl":1018.4},
        "hourly":{"time":["2026-11-16T14:00","2026-11-16T15:00"],"precipitation_probability":[12,24]},
        "daily":{"time":["2026-11-16","2026-11-17","2026-11-18","2026-11-19","2026-11-20"],"weather_code":[2,61,3,0,45],
        "temperature_2m_max":[11.8,9.2,10.3,12.5,8.3],"temperature_2m_min":[6.1,5.3,3.6,4.2,2.8],"precipitation_probability_max":[24,84,28,3,17]}}
        """);
        return (HouseTelemetrySource.ParseTemperatures(readings.RootElement, now), HouseTelemetrySource.ParsePumps(pumps.RootElement, now), HouseTelemetrySource.ParseWeather(weather.RootElement));
    }

    public static async Task TestCore()
    {
        var checks = 0;
        void Check(bool value, string name) { if (!value) throw new Exception("Verification failed: " + name); checks++; }
        void Reject(Action action, string name) { try { action(); } catch { checks++; return; } throw new Exception("Expected rejection: " + name); }
        var status = HeatingParser.Status(StatusHtml);
        Check(status.Zones.Count == 2 && status.Zones[0].Temperature == 22.3 && status.BufferMiddle == 49.2 && status.BoilerPump == true, "all heating status");
        var (house, pumps, ozalj) = HouseFixture();
        Check(house.Room("kat")?.Measured.Value == 22.3 && house.Room("kat")?.Target.Value == 22 && house.Room("prizemlje")?.Target.Value == 22.5,
            "house separates the two thermostat targets from heating water values");
        Check(house.Temperature("house/outdoor/temp")?.Value == 9.4 && house.Temperature("house/puffer/midd")?.Value == 48.7 && house.Temperature("house/heatpump/temp") == null,
            "house preserves measured outdoor and 2000 L puffer readings and does not infer the heat pump");
        Check(pumps.Count == 5 && pumps["kotao"].Value == true && pumps["sanitarna"].Value == false,
            "actual JSON on and off pump states remain distinct");
        var checkedAt = pumps["kat"].FetchedAt;
        Check(pumps["kat"].Age(checkedAt.AddMinutes(2)) == ReadingAge.Current && pumps["kat"].Age(checkedAt.AddMinutes(2).AddSeconds(1)) == ReadingAge.Muted &&
            pumps["kat"].Age(checkedAt.AddMinutes(9)) == ReadingAge.Muted && pumps["kat"].Age(checkedAt.AddMinutes(9).AddSeconds(1)) == ReadingAge.Outdated,
            "source sample age plus elapsed time uses strict three- and ten-minute thresholds");
        using var invalidPump = JsonDocument.Parse("""{"success":true,"server_time":"2026-11-16 15:39:00","pumps":[{"pump":"kat","state":2,"updated_at":"2026-11-16 15:38:00"}]}""");
        var unknownPump = HouseTelemetrySource.ParsePumps(invalidPump.RootElement, checkedAt);
        Check(unknownPump["kat"].Value == null && unknownPump["kat"].Age(checkedAt) == ReadingAge.Unknown && !unknownPump.ContainsKey("kotao"),
            "unknown and missing pumps are never rendered as off");
        Check(ozalj.Temperature == 10.2 && ozalj.RainChance == 24 && ozalj.Days.Count == 5 && ozalj.Days[1].Code == 61,
            "Ozalj weather selects matching hour and forecast slices");
        Reject(() => HeatingParser.Status("<html>unrelated page</html>"), "no invented status");
        Reject(() => HeatingParser.Status("<input type='password'>"), "login detected");
        var schedule = HeatingParser.Schedule(ScheduleHtml("kat"), "kat");
        Check(schedule.Fields.Count == 24 && schedule.Hidden["kat"] == "2" && schedule.Fields[3].Step == 0.1, "24 source fields and zone");
        Reject(() => HeatingParser.Schedule(ScheduleHtml("kat").Replace("name='n12'", "name='missing'"), "kat"), "missing hour");
        Reject(() => HeatingParser.Schedule(ScheduleHtml("prizemlje"), "kat"), "wrong zone");
        var values = schedule.Fields.Select(f => f.Value).ToArray(); values[0] = 60.1;
        Reject(() => HeatingParser.ValidateSchedule(values, schedule), "source bounds"); values[0] = 28.05;
        Reject(() => HeatingParser.ValidateSchedule(values, schedule), "source step");
        var settings = HeatingParser.Settings(SettingsHtml());
        Check(settings.Fields.Count == 6 && settings.Fields[2].Value == 21.75 && settings.Fields[3].Value == 0.15, "precise settings");
        Reject(() => HeatingParser.ValidateSettings([1, 2, 3, 4, 5, 2]), "invalid climate state");
        var chart = ChartParser.Parse(ChartHtml, "kat");
        Check(chart.Labels.Count == 12 && chart.Series.Count == 2 && chart.Series[0].Values[7] == null && chart.Series[1].Values.All(v => v == 22) && chart.Series[1].Stepped, "native chart literal bindings/null/array fill");
        Reject(() => ChartParser.Parse(ChartHtml.Replace("data:actual", "data:fetch('/secret')"), "kat"), "script expressions rejected");
        Check(ChartParser.Catalog.Length == 10, "all ten chart sources");
        Reject(() => HeatingRepository.SafeUrl("https://example.com/novo.php"), "foreign source");
        Reject(() => HeatingRepository.SafeUrl("../../anything.php"), "unsafe path");
        Reject(() => SolarRepository.Endpoint("https://example.com/pvmswebsite/nologin/assets/build/cloud.html#/kiosk?kk=fixtureTokenABC"), "foreign solar host");
        var fixtureSolar = SolarFixture();
        Check(fixtureSolar.Power == 3.24 && fixtureSolar.Daily == 18.7 && fixtureSolar.Curve?.Series[0].Values[0] == null, "encoded solar values and missing points");
        Reject(() => SolarRepository.Parse("{\"success\":true,\"data\":\"{}\"}"), "empty solar schema");
        Check(CameraEngine.ParseLoginResponse(HttpStatusCode.OK, "").Status == "success", "native Xiaomi login success");
        Check(CameraEngine.ParseLoginResponse(HttpStatusCode.Unauthorized, "{\"captcha\":\"AAE=\"}").CaptchaBytes?.Length == 2, "native Xiaomi captcha bytes");
        Check(CameraEngine.ParseLoginResponse(HttpStatusCode.Unauthorized, "{\"verify_phone\":\"***12\"}").Status == "verify", "native Xiaomi 2FA challenge");
        foreach (var raw in new[] { "malformed provider secret", "{\"error\":\"private provider diagnostic\"}" })
            try { CameraEngine.ParseLoginResponse(HttpStatusCode.Unauthorized, raw); throw new Exception("Expected authentication failure"); }
            catch (CameraEngineException e) { Check(!e.Message.Contains(raw, StringComparison.Ordinal), "raw provider errors not exposed"); }
        Reject(() => CameraEngine.ParseLoginResponse(HttpStatusCode.InternalServerError, "private provider body"), "Xiaomi server failure");
        var installed = GitHubUpdateService.CurrentVersion;
        var nextVersion = new Version(installed.Major, installed.Minor, installed.Build + 1);
        var updateTag = "v" + nextVersion.ToString(3);
        var release = JsonSerializer.Serialize(new { tag_name = updateTag, draft = false, prerelease = false, assets = new[] { new { name = "TermoDom-Windows.zip", size = 100, digest = "sha256:" + new string('a', 64), browser_download_url = $"https://github.com/leo25matko-boop/termo/releases/download/{updateTag}/TermoDom-Windows.zip" } } });
        var package = GitHubUpdateService.SelectPackage(release, GitHubUpdateService.CurrentVersion);
        Check(package?.Version == nextVersion && package.Size == 100, "native GitHub update metadata");
        Check(GitHubUpdateService.SelectPackage(release, nextVersion) == null, "same version not redownloaded");
        Reject(() => GitHubUpdateService.SelectPackage(release.Replace("sha256:", "unknown:"), GitHubUpdateService.CurrentVersion), "update must have published SHA256");
        Reject(() => GitHubUpdateService.SelectPackage(release.Replace("https://github.com/", "https://example.com/"), GitHubUpdateService.CurrentVersion), "foreign update download rejected");
        Reject(() => GitHubUpdateService.ValidateHash(new byte[32], new string('a', 64)), "corrupt update hash rejected");
        var saved = Enumerable.Repeat(31.2, 24).ToArray(); var writes = 0; var reads = 0;
        var repo = new HeatingRepository((uri, _, mutation) =>
        {
            if (!mutation) { reads++; return Task.FromResult(new SourceResponse(200, ScheduleHtml("kat", writes == 0 ? null : saved))); }
            writes++;
            Check(uri.AbsolutePath.EndsWith("dodajPODESAVANJA.php") && uri.Query.Contains("kat=2") && Enumerable.Range(0, 24).All(i => uri.Query.Contains("n" + i + "=")), "write correct source all fields");
            return Task.FromResult(new SourceResponse(302, "", new Dictionary<string, string[]> { ["Location"] = ["kat.php"] }));
        });
        var verified = await repo.SaveSchedule("kat", saved);
        Check(writes == 1 && reads == 3 && verified.Fields.All(f => f.Value == 31.2), "single mutation and independent verification");
        var failedRepo = new HeatingRepository((_, _, mutation) => Task.FromResult(new SourceResponse(200, mutation ? "ok" : ScheduleHtml("kat"))));
        try { await failedRepo.SaveSchedule("kat", saved); throw new Exception("Mismatch was accepted"); } catch (ControllerException e) { Check(e.MutationUnconfirmed, "unconfirmed write surfaced"); }
        var unsafeRepo = new HeatingRepository((_, _, mutation) => { if (mutation) throw new Exception("Unsafe mutation attempted"); return Task.FromResult(new SourceResponse(200, ScheduleHtml("kat").Replace("dodajPODESAVANJA.php", "https://example.com/bad.php"))); });
        try { await unsafeRepo.SaveSchedule("kat", saved); throw new Exception("Unsafe form accepted"); } catch (ControllerException) { checks++; }
        File.WriteAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "core-tests.txt"), $"TERMODOM_WINDOWS_CORE_TESTS_OK\n{checks} meaningful checks passed; source writes mocked; no real heating commands sent.\n");
    }
}
