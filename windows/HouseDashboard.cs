using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
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

        var plan = Columns(1.19, 1);
        var house = new HouseScene(CollectorBadge(),
            Floor("kat", "1. kat", "polaz_kat"),
            Floor("prizemlje", "Prizemlje", "polaz_priz"));
        var sceneFrame = new Border { Child = house,
            Background = Paint("#101C24"), BorderBrush = Edge, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12), VerticalAlignment = VerticalAlignment.Top };
        var regulation = InlineLink("Rasporedi i regulator →", "heating", "house-heating");
        regulation.Margin = new Thickness(0, 8, 0, 0);
        var electric = SolarSummary(solar, solarEmpty);
        electric.Margin = new Thickness(0, 8, 0, 0);
        var houseColumn = Stack(sceneFrame, regulation, electric);
        houseColumn.Margin = new Thickness(0, 0, 14, 0);
        plan.Children.Add(houseColumn);

        var outdoor = OutdoorStrip(weather, weatherError);
        outdoor.Margin = new Thickness(0, 0, 0, 9);
        var annex = Stack(outdoor, Equipment());
        Grid.SetColumn(annex, 1);
        plan.Children.Add(annex);
        Children.Add(new HouseDiagramViewport(plan, zoom, Quiet));

        if (weather != null) Children.Add(Forecast(weather, weatherError));
        if (telemetry?.Temperature("house/polaz_priz2/temp") != null || telemetry?.Temperature("house/sensor1/temp") != null)
            Children.Add(AdditionalSensors());
    }

    FrameworkElement OutdoorStrip(OzaljWeather? weather, string? weatherError)
    {
        var grid = Columns(1.12, 1);
        var summary = new DockPanel();
        var icon = WeatherIcon(weather?.Code, weather?.IsDay ?? true, 28);
        icon.Margin = new Thickness(0, 1, 7, 0);
        icon.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(icon, Dock.Left);
        summary.Children.Add(icon);
        var conditions = weather == null ? weatherError ?? "Učitavanje prognoze…" :
            $"{HouseTelemetrySource.WeatherDescription(weather.Code, weather.IsDay)} · {Format(weather.Temperature)}";
        summary.Children.Add(Stack(Label("Vrijeme · Ozalj", 14, Ink, true), Label(conditions, 12, Quiet)));
        grid.Children.Add(summary);

        var sensor = Stack(Label("Vanjski senzor", 11, Quiet, true), Reading(telemetry?.Temperature("house/outdoor/temp"), 19, Mint));
        var separation = new Border { Child = sensor, Padding = new Thickness(10, 0, 0, 0),
            Margin = new Thickness(8, 0, 0, 0), BorderBrush = Edge, BorderThickness = new Thickness(1, 0, 0, 0) };
        Grid.SetColumn(separation, 1);
        grid.Children.Add(separation);
        var panel = Frame(grid, Paint("#192C37"), 9);
        AutomationProperties.SetAutomationId(panel, "house-outside");
        return panel;
    }

    FrameworkElement CollectorBadge()
    {
        var collectors = new DockPanel();
        var caption = Label("Solarni kolektori", 12, Ink, true);
        caption.Margin = new Thickness(0, 2, 9, 0);
        DockPanel.SetDock(caption, Dock.Left);
        collectors.Children.Add(caption);
        collectors.Children.Add(Reading(telemetry?.Temperature("house/solar/temp"), 15, Gold));
        var badge = Frame(collectors, Paint("#F01A2D36"), 6);
        badge.CornerRadius = new CornerRadius(8);
        AutomationProperties.SetAutomationId(badge, "house-roof");
        return badge;
    }

    FrameworkElement SolarSummary(SolarData? solar, string emptyText)
    {
        var row = new DockPanel();
        var button = InlineLink("Otvori elektranu →", "solar", "house-solar");
        button.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(button, Dock.Right);
        row.Children.Add(button);
        var generation = Stack(Label("FUSIONSOLAR · ELEKTRANA", 12, Sky, true));
        if (solar != null) generation.Children.Add(Label($"{Format(solar.Power, "kW")}  ·  Danas {Format(solar.Daily, "kWh")}",
            14, solarFetched.HasValue && now - solarFetched.Value > TimeSpan.FromMinutes(3) ? Quiet : Ink, true));
        if (solarFetched.HasValue)
        {
            var elapsed = now - solarFetched.Value;
            if (elapsed > TimeSpan.FromMinutes(10)) generation.Children.Add(Label("Zadnje dohvaćeno " + solarFetched.Value.ToLocalTime().ToString("d.M. HH:mm", Hr), 10, Quiet));
        }
        if (emptyText.Length > 0) generation.Children.Add(Label(emptyText, 11, Quiet));
        row.Children.Add(generation);
        return Frame(row, Panel, 9);
    }

    FrameworkElement Floor(string id, string title, string supply)
    {
        var room = telemetry?.Room(id);
        var values = Columns(1, 1);
        values.Children.Add(Stack(Label("Sobna", 11, Quiet), Reading(room?.Measured, 20, Mint)));
        var target = Stack(Label("Zadana", 11, Quiet), Reading(room?.Target, 20, Mint));
        Grid.SetColumn(target, 1);
        values.Children.Add(target);
        var water = new DockPanel();
        var waterLabel = Label("Polaz vode", 11, Quiet);
        waterLabel.Margin = new Thickness(0, 2, 7, 0);
        DockPanel.SetDock(waterLabel, Dock.Left);
        water.Children.Add(waterLabel);
        water.Children.Add(Reading(telemetry?.Temperature("house/" + supply + "/temp"), 13, Gold));
        var floor = Frame(Stack(Label(title, 15, Ink, true), values, water), Paint("#EC14232C"), 8);
        floor.CornerRadius = new CornerRadius(9);
        floor.BorderBrush = Paint("#788D8E");
        AutomationProperties.SetName(floor, title + " · sobna temperatura, zadana temperatura i polaz vode");
        AutomationProperties.SetAutomationId(floor, "house-floor-" + id);
        return floor;
    }

    // The bitmap is a crop of the original web illustration. Overlays follow the
    // actual storeys as it resizes; the same WPF elements remain legible in zoom.
    sealed class HouseScene : Panel
    {
        static readonly BitmapImage Cutaway = LoadCutaway();
        readonly UIElement roof, upstairs, downstairs;
        readonly Image artwork = new() { Source = Cutaway, Stretch = Stretch.Fill, IsHitTestVisible = false };

        public HouseScene(UIElement roof, UIElement upstairs, UIElement downstairs)
        {
            this.roof = roof;
            this.upstairs = upstairs;
            this.downstairs = downstairs;
            AutomationProperties.SetName(artwork, "Izvorna ilustracija presjeka kuće s prizemljem, 1. katom i solarnim kolektorima");
            AutomationProperties.SetAutomationId(artwork, "house-original-artwork");
            ClipToBounds = true;
            Children.Add(artwork);
            Children.Add(roof);
            Children.Add(upstairs);
            Children.Add(downstairs);
        }

        static BitmapImage LoadCutaway()
        {
            var image = new BitmapImage(new Uri("pack://application:,,,/Assets/house-cutaway.png"));
            image.Freeze();
            return image;
        }

        static double FloorWidth(double width) => Math.Min(278, width * .58);

        protected override Size MeasureOverride(Size availableSize)
        {
            var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 500;
            var height = Math.Clamp(width * (896d / 1006), 352, 515);
            artwork.Measure(new Size(width, height));
            roof.Measure(new Size(width * .66, double.PositiveInfinity));
            foreach (var floor in new[] { upstairs, downstairs })
                floor.Measure(new Size(FloorWidth(width), double.PositiveInfinity));
            // Leave enough room for old readings whose timestamp adds a line.
            var tallest = Math.Max(upstairs.DesiredSize.Height, downstairs.DesiredSize.Height);
            return new Size(width, Math.Max(height, (tallest + 8) / .315));
        }

        protected override Size ArrangeOverride(Size size)
        {
            var (width, height) = (size.Width, size.Height);
            artwork.Arrange(new Rect(size));
            roof.Arrange(new Rect(width * .975 - roof.DesiredSize.Width, height * .014, roof.DesiredSize.Width, roof.DesiredSize.Height));
            upstairs.Arrange(new Rect(width * .09, height * .335, FloorWidth(width), upstairs.DesiredSize.Height));
            downstairs.Arrange(new Rect(width * .09, height * .665, FloorWidth(width), downstairs.DesiredSize.Height));
            return size;
        }
    }

    FrameworkElement Equipment()
    {
        var equipment = Stack(Label("Kotlovnica", 20, Ink, true));
        var generation = new UniformGrid { Columns = 2, Margin = new Thickness(0, 7, 0, 2) };
        generation.Children.Add(Device("Kotao", "boiler", telemetry?.Temperature("house/kotal/temp"), Gold));
        generation.Children.Add(Device("Toplinska pumpa", "heatpump", null, Sky));
        equipment.Children.Add(generation);

        var buffers = new UniformGrid { Columns = 2, Margin = new Thickness(0, 4, 0, 4) };
        buffers.Children.Add(Buffer("Pufer 2000 L", Gold, true));
        buffers.Children.Add(Buffer("Pufer 1000 L", Sky, false));
        equipment.Children.Add(buffers);

        var water = new StackPanel { Margin = new Thickness(0, 7, 0, 0) };
        water.Children.Add(Device("Sanitarna voda", "tank", telemetry?.Temperature("house/dhw/temp"), Sky));
        equipment.Children.Add(water);
        equipment.Children.Add(new Border { Height = 1, Background = Edge, Margin = new Thickness(0, 9, 0, 6) });
        equipment.Children.Add(Label("Cirkulacijske pumpe", 16, Ink, true));

        var motorGrid = new UniformGrid { Columns = 2, Margin = new Thickness(0, 5, -5, -5) };
        motorGrid.SizeChanged += (_, args) =>
        {
            if (args.WidthChanged) motorGrid.Columns = args.NewSize.Width >= 400 ? 3 : 2;
        };
        foreach (var (id, title) in new[] { ("kotao", "Kotao"), ("prizemlje", "Prizemlje"), ("kat", "1. kat"), ("sanitarna", "Sanitarna voda"), ("solar", "Solarni kolektori") })
            motorGrid.Children.Add(Pump(title, pumps.GetValueOrDefault(id)));
        equipment.Children.Add(motorGrid);
        var room = Frame(equipment, Paint("#1D2B31"), 12);
        AutomationProperties.SetAutomationId(room, "house-boiler-room");
        return room;
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
        var pump = new DockPanel();
        var icon = new Grid { Width = 34, Height = 30, Margin = new Thickness(0, 0, 7, 0), VerticalAlignment = VerticalAlignment.Top };
        icon.Children.Add(new Path { Data = Geometry.Parse("M 0,9 H 5 V 21 H 0 Z M 29,9 H 34 V 21 H 29 Z"),
            Fill = Paint("#4B686F"), Stroke = Paint("#729198"), StrokeThickness = 1 });
        icon.Children.Add(new Ellipse { Width = 26, Height = 26, Stroke = Paint("#5B767B"), Fill = Slate, StrokeThickness = 1.4 });
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
        var frame = Frame(pump, Slate, 5);
        frame.Margin = new Thickness(0, 0, 5, 5);
        frame.CornerRadius = new CornerRadius(7);
        AutomationProperties.SetName(frame, name + ", " + label);
        return frame;
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

    FrameworkElement Forecast(OzaljWeather weather, string? weatherError)
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
        var conditions = Label($"Osjećaj {Format(weather.FeelsLike)} · Vlažnost {Format(weather.Humidity, "%")} · " +
            $"Vjetar {Format(weather.Wind, "km/h")} · Tlak {Format(weather.Pressure, "hPa")} · Kiša {Format(weather.RainChance, "%")}", 12, Quiet);
        conditions.Margin = new Thickness(0, 4, 0, 8);
        var attribution = demo ? "Primjer prognoze" : "Prognoza · Open-Meteo · " + (weather.MeasuredAt?.ToString("HH:mm", Hr) ?? "—");
        if (weatherError != null) attribution += " · zadnja dostupna prognoza";
        return Frame(Stack(Label("Prognoza za Ozalj", 17, Ink, true), conditions, forecast,
            Label(attribution, 10, Sky)), Paint("#1B2D39"), 12);
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
        return new Viewbox { Child = canvas, Height = displayHeight, Stretch = Stretch.Uniform };
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
