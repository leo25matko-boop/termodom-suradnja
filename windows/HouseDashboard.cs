using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace TermoDom;

/// <summary>Native illustrated section of the house with read-only equipment states.</summary>
internal sealed class HouseDashboard : StackPanel
{
    static readonly CultureInfo Hr = CultureInfo.GetCultureInfo("hr-HR");
    static readonly Brush Ink = Paint("#F3F5F1"), Quiet = Paint("#A8B9B9"), Dim = Paint("#91A1A6");
    static readonly Brush Mint = Paint("#90E3C2"), Gold = Paint("#F1BD7F"), Sky = Paint("#8DBCE2"), Red = Paint("#FF9795");
    static readonly Brush Edge = Paint("#334A4E"), Slate = Paint("#17252E"), Panel = Paint("#1A2A31");
    readonly DateTimeOffset now = DateTimeOffset.UtcNow;
    readonly HouseTelemetry? telemetry;
    readonly IReadOnlyDictionary<string, HouseReading<bool>> pumps;
    readonly Action<string> open;
    readonly DateTimeOffset? solarFetched;
    readonly bool demo;

    public HouseDashboard(HouseTelemetry? telemetry, IReadOnlyDictionary<string, HouseReading<bool>> pumps, SolarData? solar,
        DateTimeOffset? solarFetched, string solarEmpty, OzaljWeather? weather, string? weatherError, bool demo, HouseZoomState zoom, Action<string> open)
    {
        this.telemetry = telemetry;
        this.pumps = pumps;
        this.open = open;
        this.solarFetched = solarFetched;
        this.demo = demo;

        Children.Add(OutdoorStrip(weather, weatherError));
        var plan = Columns(1.14, 1);
        var house = new StackPanel { Margin = new Thickness(0, 0, 12, 0) };
        house.Children.Add(Roof(solar, solarEmpty));
        house.Children.Add(Floor("kat", "Kat", "polaz_kat", "Kat · termostat i polaz vode"));
        house.Children.Add(Floor("prizemlje", "Prizemlje", "polaz_priz", "Prizemlje · termostat i polaz vode"));
        house.Children.Add(Plinth());
        plan.Children.Add(house);
        var boiler = Equipment();
        Grid.SetColumn(boiler, 1);
        plan.Children.Add(boiler);
        var pipes = new Canvas { Width = 13, Height = 25, HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 91), IsHitTestVisible = false };
        pipes.Children.Add(new Line { X1 = 0, X2 = 13, Y1 = 7, Y2 = 7, Stroke = Paint("#A78667"), StrokeThickness = 3 });
        pipes.Children.Add(new Line { X1 = 0, X2 = 13, Y1 = 17, Y2 = 17, Stroke = Paint("#5C8794"), StrokeThickness = 3 });
        plan.Children.Add(pipes);
        Children.Add(new HouseDiagramViewport(plan, zoom, Quiet));
        if (weather?.Days.Count > 0) Children.Add(Forecast(weather));
        if (telemetry?.Temperature("house/polaz_priz2/temp") != null || telemetry?.Temperature("house/sensor1/temp") != null)
            Children.Add(AdditionalSensors());
    }

    FrameworkElement OutdoorStrip(OzaljWeather? weather, string? weatherError)
    {
        var grid = Columns(1, 1.5);
        var local = telemetry?.Temperature("house/outdoor/temp");
        var localReading = Stack(Label("VANJSKI SENZOR", 11, Mint, true), Reading(local, 33, Ink), Label("Mjereno kod kuće", 12, Quiet));
        var weatherContent = Stack(Label("VRIJEME · OZALJ", 11, Sky, true));
        var conditions = Columns(1, 1.18);
        var summary = new DockPanel();
        var icon = WeatherIcon(weather?.Code, weather?.IsDay ?? true, 36);
        icon.Margin = new Thickness(0, 3, 10, 2);
        DockPanel.SetDock(icon, Dock.Left);
        summary.Children.Add(icon);
        summary.Children.Add(Stack(Label(Format(weather?.Temperature), 30, weather == null ? Quiet : Ink, true),
            Label(weather == null ? weatherError ?? "Učitavam Open-Meteo…" : HouseTelemetrySource.WeatherDescription(weather.Code, weather.IsDay), 12, Quiet)));
        conditions.Children.Add(summary);
        if (weather != null)
        {
            var details = Stack(Label($"Osjećaj {Format(weather.FeelsLike)} · Vlažnost {Format(weather.Humidity, "%")}", 11, Quiet),
                Label($"Vjetar {Format(weather.Wind, "km/h")} · Tlak {Format(weather.Pressure, "hPa")}", 11, Quiet),
                Label($"Vjerojatnost kiše {Format(weather.RainChance, "%")}", 11, Quiet));
            details.Margin = new Thickness(9, 4, 0, 0);
            var attribution = demo ? "Primjer vremena" : "Open-Meteo · " + (weather.MeasuredAt?.ToString("HH:mm", Hr) ?? "—");
            if (weatherError != null) attribution += " · zadnja dostupna prognoza";
            details.Children.Add(Label(attribution, 10, Sky));
            Grid.SetColumn(details, 1);
            conditions.Children.Add(details);
        }
        else Grid.SetColumnSpan(summary, 2);
        weatherContent.Children.Add(conditions);
        var localCard = Frame(localReading, Paint("#1B3034"), 10);
        localCard.Margin = new Thickness(0, 0, 10, 0);
        grid.Children.Add(localCard);
        var weatherCard = Frame(weatherContent, Paint("#192C3C"), 10);
        Grid.SetColumn(weatherCard, 1);
        grid.Children.Add(weatherCard);
        return grid;
    }

    FrameworkElement Roof(SolarData? solar, string emptyText)
    {
        var stack = new StackPanel();
        stack.Children.Add(Illustration("roof", 460, 114, 120));
        var source = Columns(.91, 1.09);
        var collectors = Stack(Label("SOLARNI KOLEKTORI", 12, Gold, true), Label("Temperatura kolektora", 12, Quiet),
            Reading(telemetry?.Temperature("house/solar/temp"), 26, Gold));
        collectors.Margin = new Thickness(0, 0, 7, 0);
        source.Children.Add(collectors);
        var generation = Stack(Label("FUSIONSOLAR · ELEKTRANA", 12, Sky, true));
        var values = Stack(Label($"{Format(solar?.Power, "kW")}  ·  Danas {Format(solar?.Daily, "kWh")}", 16, Ink, true));
        generation.Children.Add(values);
        if (solarFetched.HasValue)
        {
            var elapsed = now - solarFetched.Value;
            if (elapsed > TimeSpan.FromMinutes(10)) generation.Children.Add(Label("Zadnje dohvaćeno " + solarFetched.Value.ToLocalTime().ToString("d.M. HH:mm", Hr), 10, Quiet));
            if (elapsed > TimeSpan.FromMinutes(3)) values.Opacity = .62;
        }
        if (solar == null || emptyText.Length > 0) generation.Children.Add(Label(emptyText, 11, Quiet));
        generation.Children.Add(InlineLink("Otvori elektranu →", "solar", "house-solar"));
        var electric = new Border { BorderBrush = Edge, BorderThickness = new Thickness(1, 0, 0, 0), Padding = new Thickness(8, 0, 0, 0), Child = generation };
        Grid.SetColumn(electric, 1);
        source.Children.Add(electric);
        var footer = Frame(source, Paint("#2C3034"), 12);
        footer.CornerRadius = new CornerRadius(0);
        stack.Children.Add(footer);
        AutomationProperties.SetName(stack, "Krov kuće sa solarnim kolektorima i pregledom elektrane");
        return stack;
    }

    FrameworkElement Floor(string id, string title, string supply, string accessibleName)
    {
        var room = telemetry?.Room(id);
        var inset = Columns(.72, 1.08, .78);
        var scene = Illustration(id, 128, 94, 91);
        scene.VerticalAlignment = VerticalAlignment.Center;
        scene.Margin = new Thickness(0, 0, 12, 0);
        inset.Children.Add(scene);

        var thermometer = Stack(Label(title, 21, Ink, true), Label("Termostat · izmjereno", 12, Quiet), Reading(room?.Measured, 35, Mint));
        Grid.SetColumn(thermometer, 1);
        inset.Children.Add(thermometer);
        var settings = Stack(Label("Zadano", 12, Quiet), Reading(room?.Target, 24, Mint),
            Label("Polaz vode", 12, Quiet), Reading(telemetry?.Temperature("house/" + supply + "/temp"), 18, Gold));
        settings.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(settings, 2);
        inset.Children.Add(settings);
        var floor = Frame(inset, id == "kat" ? Paint("#273E40") : Paint("#2C3737"), 14);
        floor.CornerRadius = new CornerRadius(0);
        floor.Margin = new Thickness(0, 4, 0, 0);
        floor.BorderThickness = new Thickness(2, 0, 2, 3);
        AutomationProperties.SetName(floor, accessibleName);
        return floor;
    }

    FrameworkElement Plinth()
    {
        var foundation = Frame(InlineLink("Rasporedi i regulator →", "heating", "house-heating"), Paint("#26383B"), 11);
        foundation.CornerRadius = new CornerRadius(0, 0, 12, 12);
        foundation.BorderThickness = new Thickness(2, 0, 2, 2);
        return foundation;
    }

    FrameworkElement Equipment()
    {
        var equipment = Stack(Label("Kotlovnica", 24, Ink, true));
        var generation = new UniformGrid { Columns = 2, Margin = new Thickness(0, 7, 0, 2) };
        generation.Children.Add(Device("Kotao", "boiler", telemetry?.Temperature("house/kotal/temp"), Gold));
        generation.Children.Add(Device("Dizalica topline", "heatpump", null, Sky));
        equipment.Children.Add(generation);

        var buffers = new UniformGrid { Columns = 2, Margin = new Thickness(0, 4, 0, 4) };
        buffers.Children.Add(Buffer("Pufer 2.000 L", Gold, true));
        buffers.Children.Add(Buffer("Pufer 1.000 L", Sky, false));
        equipment.Children.Add(buffers);

        var water = new StackPanel { Margin = new Thickness(0, 7, 0, 0) };
        water.Children.Add(Device("Sanitarna voda", "tank", telemetry?.Temperature("house/dhw/temp"), Sky));
        equipment.Children.Add(water);
        equipment.Children.Add(new Border { Height = 1, Background = Edge, Margin = new Thickness(0, 9, 0, 6) });
        equipment.Children.Add(Label("Cirkulacijske pumpe", 16, Ink, true));

        var motorGrid = new UniformGrid { Columns = 2 };
        foreach (var (id, title) in new[] { ("kotao", "Kotao"), ("prizemlje", "Prizemlje"), ("kat", "Kat"), ("sanitarna", "Sanitarna voda"), ("solar", "Solarni krug") })
            motorGrid.Children.Add(Pump(title, pumps.GetValueOrDefault(id)));
        equipment.Children.Add(motorGrid);
        return Frame(equipment, Paint("#1D2B31"), 14);
    }

    FrameworkElement Device(string name, string image, HouseReading<double>? sample, Brush accent)
    {
        var card = new DockPanel();
        var illustration = Illustration(image, 42, 52, 41);
        illustration.Width = 26;
        illustration.VerticalAlignment = VerticalAlignment.Center;
        illustration.Margin = new Thickness(0, 0, 6, 0);
        DockPanel.SetDock(illustration, Dock.Left);
        card.Children.Add(illustration);
        card.Children.Add(Stack(Label(name, 13, Quiet, true), Reading(sample, 22, accent)));
        var frame = Frame(card, Slate, 9);
        frame.Margin = new Thickness(0, 0, 5, 3);
        return frame;
    }

    FrameworkElement Buffer(string title, Brush accent, bool connected)
    {
        var data = new DockPanel();
        var glyph = Illustration("tank", 42, 52, 51);
        glyph.Width = 32;
        glyph.Margin = new Thickness(0, 1, 6, 0);
        DockPanel.SetDock(glyph, Dock.Left);
        data.Children.Add(glyph);
        var readings = new StackPanel();
        foreach (var (label, level, color) in new[] { ("Vrh", "top", Gold), ("Sredina", "midd", Mint), ("Dno", "bottom", Sky) })
        {
            var row = new DockPanel();
            var position = Label(label, 11, Quiet);
            position.Width = 47;
            position.Padding = new Thickness(0, 4, 0, 0);
            DockPanel.SetDock(position, Dock.Left);
            row.Children.Add(position);
            row.Children.Add(Reading(connected ? telemetry?.Temperature("house/puffer/" + level) : null, 17, color));
            readings.Children.Add(row);
        }
        data.Children.Add(readings);
        var panel = Frame(Stack(Label(title, 14, accent, true), data), Slate, 8);
        panel.Margin = new Thickness(0, 0, 5, 3);
        AutomationProperties.SetName(panel, connected ? title : title + ": mjesta za tri dodatna senzora");
        AutomationProperties.SetAutomationId(panel, connected ? "buffer-2000" : "buffer-1000");
        return panel;
    }

    FrameworkElement Pump(string name, HouseReading<bool>? sample)
    {
        var enabled = sample?.Value;
        var age = sample?.Age(now) ?? ReadingAge.Unknown;
        var accent = enabled is true ? Mint : enabled is false ? Red : Dim;
        var pump = new DockPanel { Margin = new Thickness(0, 6, 7, 2) };
        var icon = new Grid { Width = 30, Height = 30, Margin = new Thickness(0, 0, 7, 0), VerticalAlignment = VerticalAlignment.Top };
        icon.Children.Add(new Ellipse { Stroke = Edge, Fill = Slate, StrokeThickness = 1 });
        var bladeCanvas = new Canvas { Width = 26, Height = 26 };
        var glyph = enabled.HasValue ? "M 18,7 A 8,8 0 1 1 6,17 M 5,13 L 6,17 L 10,16" : "M 8,13 H 18";
        var blade = new Path { Data = Geometry.Parse(glyph), Stroke = accent,
            StrokeThickness = 1.5, Stretch = Stretch.None, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
        bladeCanvas.Children.Add(blade);
        icon.Children.Add(bladeCanvas);
        if (enabled == true && age == ReadingAge.Current && SystemParameters.ClientAreaAnimation)
        {
            var rotate = new RotateTransform(0, 13, 13);
            blade.RenderTransform = rotate;
            blade.Loaded += (_, _) => rotate.BeginAnimation(RotateTransform.AngleProperty,
                new DoubleAnimation(0, 360, TimeSpan.FromMilliseconds(1800)) { RepeatBehavior = RepeatBehavior.Forever });
            blade.Unloaded += (_, _) => rotate.BeginAnimation(RotateTransform.AngleProperty, null);
        }
        DockPanel.SetDock(icon, Dock.Left);
        pump.Children.Add(icon);
        var label = enabled is true ? "Radi" : enabled is false ? "Miruje" : "Nepoznato";
        if (enabled.HasValue && age != ReadingAge.Current) label = "Zadnje: " + label.ToLower(Hr);
        var state = Stack(Label(name, 13, Ink, true), Label(label, 12, accent));
        if (age == ReadingAge.Outdated && sample?.MeasuredAt is DateTime time) state.Children.Add(Label(TimeText(time), 10, Quiet));
        else if (enabled.HasValue && age == ReadingAge.Unknown) state.Children.Add(Label("Vrijeme nepoznato", 10, Quiet));
        pump.Children.Add(state);
        if (age is ReadingAge.Muted or ReadingAge.Outdated) { icon.Opacity = .58; state.Opacity = .74; }
        AutomationProperties.SetName(pump, name + ", " + label);
        return pump;
    }

    FrameworkElement Reading(HouseReading<double>? sample, double size, Brush accent)
    {
        var age = sample?.Age(now) ?? ReadingAge.Unknown;
        var value = Label(Format(sample?.Value), size, age == ReadingAge.Current ? accent : Quiet, true);
        var panel = Stack(value);
        if (age is ReadingAge.Muted or ReadingAge.Outdated) value.Opacity = .64;
        if (age == ReadingAge.Outdated && sample?.MeasuredAt is DateTime time)
            panel.Children.Add(Label("Zadnje " + TimeText(time), 10, Quiet));
        else if (sample?.Value.HasValue == true && age == ReadingAge.Unknown)
            panel.Children.Add(Label("Vrijeme nepoznato", 10, Quiet));
        return panel;
    }

    FrameworkElement Forecast(OzaljWeather weather)
    {
        var forecast = new UniformGrid { Columns = weather.Days.Count };
        foreach (var day in weather.Days)
        {
            var card = Stack(Label(day.Date.ToString("ddd d.M.", Hr), 11, Quiet, true), WeatherIcon(day.Code, true, 22),
                Label(Format(day.High) + " / " + Format(day.Low), 13, Ink, true),
                Label("Kiša " + Format(day.RainChance, "%"), 10, Quiet));
            card.Margin = new Thickness(0, 0, 5, 0);
            forecast.Children.Add(card);
        }
        return Frame(Stack(Label("Prognoza za Ozalj", 17, Ink, true), forecast,
            Label(demo ? "Primjer prognoze" : "Prognoza · Open-Meteo", 10, Sky)), Paint("#1B2D39"), 12);
    }

    FrameworkElement AdditionalSensors()
    {
        var tiles = new UniformGrid { Columns = 2 };
        tiles.Children.Add(Stack(Label("Polaz prizemlja 2", 11, Quiet, true), Reading(telemetry?.Temperature("house/polaz_priz2/temp"), 18, Gold)));
        tiles.Children.Add(Stack(Label("Senzor 1", 11, Quiet, true), Reading(telemetry?.Temperature("house/sensor1/temp"), 18, Mint)));
        var panel = Frame(Stack(Label("Dodatni senzori regulatora", 15, Ink, true), tiles), Panel, 11);
        panel.Margin = new Thickness(0, 10, 0, 0);
        return panel;
    }

    Button InlineLink(string label, string route, string id)
    {
        var button = new Button { Content = label, FontSize = 11, MinHeight = 28, Margin = new Thickness(0, 4, 0, 0),
            Padding = new Thickness(8, 3, 8, 3), Background = Paint("#32474B"), HorizontalAlignment = HorizontalAlignment.Left };
        AutomationProperties.SetAutomationId(button, id);
        button.Click += (_, _) => open(route);
        return button;
    }

    static string Format(double? value, string units = "°C") => value.HasValue ? value.Value.ToString("0.#", Hr) + " " + units : "—";
    static string TimeText(DateTime time) => time.ToString("d.M. HH:mm:ss", Hr);
    static Brush Paint(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }

    static TextBlock Label(string text, double size, Brush color, bool strong = false) => new()
    {
        Text = text, FontSize = size, Foreground = color, FontWeight = strong ? FontWeights.SemiBold : FontWeights.Normal,
        TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 2)
    };

    static Border Frame(UIElement child, Brush fill, int padding) => new()
    {
        Child = child, Background = fill, BorderBrush = Edge, BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(12), Padding = new Thickness(padding)
    };

    static StackPanel Stack(params UIElement[] items)
    {
        var panel = new StackPanel();
        foreach (var item in items) panel.Children.Add(item);
        return panel;
    }

    static Grid Columns(params double[] sizes)
    {
        var grid = new Grid();
        foreach (var size in sizes) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(size, GridUnitType.Star) });
        return grid;
    }

    static FrameworkElement Illustration(string kind, int width, int height, int displayHeight)
    {
        var canvas = new Canvas { Width = width, Height = height, IsHitTestVisible = false };
        void Draw(string geometry, string fill, string? stroke = null, double thickness = 1) => canvas.Children.Add(new Path
        {
            Data = Geometry.Parse(geometry), Fill = Paint(fill), Stroke = stroke == null ? null : Paint(stroke), StrokeThickness = thickness,
            StrokeLineJoin = PenLineJoin.Round, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round
        });
        const string none = "Transparent";
        switch (kind)
        {
            case "roof":
                Draw("M0,108 L219,11 L457,108 Z", "#2E4247", "#5B7275", 2);
                Draw("M2,108 L220,11 L319,108 Z", "#354D4F");
                Draw("M219,11 L457,108 L435,108 L217,23 L22,108 L1,108 Z", "#758C87");
                Draw("M90,69 L190,24 L285,70 L175,105 Z", "#173544", "#A1BDCB", 2);
                Draw("M106,76 L209,33 M123,83 L225,40 M142,91 L244,47 M157,98 L259,55 M126,53 L215,92 M157,40 L251,80", none, "#71A8C5", 1);
                Draw("M239,18 L405,86 L382,92 L219,27 Z", "#243337");
                Draw("M349,56 L349,35 L374,35 L374,67 Z", "#637470", "#879987", 1);
                Draw("M345,35 L378,35 L377,29 L347,29 Z", "#859A90");
                Draw("M0,108 L460,108 L460,114 L0,114 Z", "#536862");
                break;
            case "kat":
                Draw("M7,77 L122,77 M11,70 L11,18 L110,18 L110,77", none, "#587373", 1);
                Draw("M70,24 H104 V55 H70 Z M87,24 V55 M70,39 H104", "#293F4F", "#84A2A7", 1.2);
                Draw("M17,56 H68 V72 H17 Z", "#285051", "#82A9A2", 1.5);
                Draw("M17,69 L17,48 Q17,45 22,45 H29 Q33,45 33,51 H64 Q68,51 68,56", "#486663", "#82A9A2", 1.5);
                Draw("M16,73 V77 M66,73 V77 M35,57 H64 M81,67 H113 M84,68 V77 M107,68 V77", none, "#8EAC9D", 1.5);
                Draw("M80,65 L84,59 L88,65 Z", "#E3B886");
                break;
            case "prizemlje":
                Draw("M4,77 L122,77 M11,77 L11,17 L114,17 L114,77", none, "#587373", 1);
                Draw("M18,25 H48 V50 H18 Z M32,25 V50 M18,37 H48", "#315164", "#90ABAA", 1);
                Draw("M54,63 L57,46 Q59,42 64,42 H97 Q101,42 102,46 L105,63 Z", "#4C6764", "#9EB8A5", 1.4);
                Draw("M53,56 Q48,52 46,56 V67 Q46,72 52,72 H108 Q113,72 113,67 V56 Q110,52 106,56 L102,63 H58 Z", "#617F70", "#A1B9A0", 1.2);
                Draw("M53,72 V77 M106,72 V77 M82,51 V62 M13,64 H34 L31,75 H17 Z", "#B69370", "#D8BC92", 1);
                Draw("M24,64 V49 M24,59 Q12,60 16,53 Q20,51 24,59 M24,57 Q25,46 32,46 Q36,49 24,57", "#79AB81", "#99B891", 1);
                break;
            case "boiler":
                Draw("M9,14 Q9,10 13,10 H31 Q35,10 35,14 V45 H9 Z M15,45 V49 M29,45 V49 M23,10 V5 H33", "#59666A", "#A7ACA7", 1.3);
                Draw("M14,22 H30 V37 H14 Z", "#23333C", "#A7ACA7", 1);
                Draw("M17,34 Q15,29 21,24 Q20,29 24,29 Q25,27 25,26 Q30,29 27,34 Z", "#EDBA77");
                break;
            case "heatpump":
                Draw("M4,13 H38 V44 H4 Z M8,45 V48 M34,45 V48", "#456270", "#9CB7C4", 1.3);
                Draw("M10,28 A9,9 0 1 0 28,28 A9,9 0 1 0 10,28 M17,26 Q16,21 20,22 Q24,24 19,29 Q25,30 22,34 Q19,35 18,29 Q12,34 12,30 Z", "#203642", "#9CB7C4", 1);
                Draw("M31,22 H35 M31,28 H35 M31,35 H35", none, "#ABBEBF", 1);
                break;
            case "tank":
                Draw("M10,13 Q10,6 21,6 Q33,6 33,13 V43 Q33,48 21,48 Q10,48 10,43 Z", "#436475", "#99B7B9", 1.5);
                Draw("M10,14 Q21,19 33,14 M10,25 Q21,30 33,25 M10,36 Q21,41 33,36 M19,5 V2 M24,49 V52", none, "#ABC0B4", 1);
                break;
        }
        return new Viewbox { Child = canvas, Height = displayHeight, Stretch = kind == "roof" ? Stretch.Fill : Stretch.Uniform };
    }

    static FrameworkElement WeatherIcon(int? code, bool isDay, int size)
    {
        string geometry;
        Brush accent;
        if (code is 0 or 1)
        {
            geometry = isDay ? "M11,17 A6,6 0 1 0 23,17 A6,6 0 1 0 11,17 M17,4 V8 M17,26 V30 M4,17 H8 M26,17 H30 M8,8 L11,11 M23,23 L26,26 M8,26 L11,23 M23,11 L26,8"
                : "M28,21 C22,26 10,22 12,8 C2,13 4,30 18,30 C23,30 28,26 28,21 Z";
            accent = isDay ? Gold : Sky;
        }
        else if (code.HasValue)
        {
            geometry = "M10,24 C3,24 4,14 10,14 C11,7 23,5 25,14 C33,14 34,24 27,24 Z";
            if (code >= 51) geometry += " M11,27 L9,31 M19,27 L17,31 M27,27 L25,31";
            if (code is 45 or 48) geometry += " M6,28 H28 M9,31 H25";
            accent = Sky;
        }
        else { geometry = "M9,17 H25"; accent = Dim; }
        return new Path { Width = size, Height = size, Data = Geometry.Parse(geometry), Stroke = accent, StrokeThickness = 2,
            Stretch = Stretch.Uniform, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
            HorizontalAlignment = HorizontalAlignment.Left, IsHitTestVisible = false };
    }
}
