using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace TermoDom;

internal sealed class HouseZoomState
{
    public double Scale { get; set; } = 1;
    public double Horizontal { get; set; }
    public double Vertical { get; set; }
}

/// <summary>Fits the diagram to the page by default and offers a separate pannable viewport when enlarged.</summary>
internal sealed class HouseDiagramViewport : DockPanel
{
    const double MaximumScale = 2;
    readonly HouseZoomState state;
    readonly FrameworkElement diagram;
    readonly Grid surface = new();
    readonly Border fit = new();
    readonly ScrollViewer pan = new()
    {
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        Focusable = true,
        Visibility = Visibility.Collapsed
    };
    readonly Button smaller;
    readonly Button larger;
    readonly Button reset;
    bool restoring;
    Point? dragStart;
    Point scrollStart;

    public HouseDiagramViewport(FrameworkElement diagram, HouseZoomState state, Brush quiet)
    {
        this.diagram = diagram;
        this.state = state;
        Margin = new Thickness(0, 11, 0, 14);

        var controls = new DockPanel { Margin = new Thickness(0, 0, 0, 7) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        smaller = ZoomButton("−", "Smanji presjek", "house-zoom-out", () => Change(state.Scale - .25));
        reset = ZoomButton("100%", "Vrati prikaz na 100 %", "house-zoom-reset", () => Change(1));
        larger = ZoomButton("+", "Povećaj presjek", "house-zoom-in", () => Change(state.Scale + .25));
        buttons.Children.Add(smaller);
        buttons.Children.Add(reset);
        buttons.Children.Add(larger);
        DockPanel.SetDock(buttons, Dock.Right);
        controls.Children.Add(buttons);
        controls.Children.Add(new TextBlock { Text = "Prikaz kuće", FontSize = 12, Foreground = quiet, VerticalAlignment = VerticalAlignment.Center });
        DockPanel.SetDock(controls, Dock.Top);
        Children.Add(controls);
        Children.Add(surface);
        surface.Children.Add(fit);
        surface.Children.Add(pan);
        AutomationProperties.SetName(pan, "Zumirani presjek kuće");
        AutomationProperties.SetHelpText(pan, "Pomičite presjek povlačenjem, klizačima ili tipkama sa strelicama.");
        surface.ToolTip = "Ctrl + kotačić za zumiranje. Povucite u zumiranom prikazu za pomicanje.";

        fit.Child = diagram;
        UpdateButtons();
        Loaded += (_, _) => { if (state.Scale > 1) Display(); };
        surface.SizeChanged += (_, e) => { if (e.WidthChanged && state.Scale > 1) SizeMagnifiedDiagram(); };
        surface.PreviewMouseWheel += (_, e) =>
        {
            if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) return;
            Change(state.Scale + (e.Delta > 0 ? .25 : -.25), e.GetPosition(surface));
            e.Handled = true;
        };
        pan.ScrollChanged += (_, _) =>
        {
            if (restoring || state.Scale <= 1) return;
            state.Horizontal = pan.HorizontalOffset;
            state.Vertical = pan.VerticalOffset;
        };
        diagram.PreviewMouseLeftButtonDown += StartDrag;
        diagram.PreviewMouseMove += MoveDrag;
        diagram.PreviewMouseLeftButtonUp += (_, e) =>
        {
            if (!dragStart.HasValue) return;
            EndDrag();
            e.Handled = true;
        };
        diagram.LostMouseCapture += (_, _) => EndDrag();
    }

    void Change(double requested, Point? pivot = null)
    {
        var scale = Math.Clamp(requested, 1, MaximumScale);
        if (scale == state.Scale) return;
        var anchor = pivot ?? new Point(surface.ActualWidth / 2, Math.Min(surface.ActualHeight / 2, 200));
        var ratio = scale / state.Scale;
        state.Horizontal = scale <= 1 ? 0 : Math.Max(0, (state.Horizontal + anchor.X) * ratio - anchor.X);
        state.Vertical = scale <= 1 ? 0 : Math.Max(0, (state.Vertical + anchor.Y) * ratio - anchor.Y);
        state.Scale = scale;
        Display();
    }

    void Display()
    {
        UpdateButtons();
        if (state.Scale <= 1)
        {
            if (pan.Content == diagram) pan.Content = null;
            pan.Visibility = Visibility.Collapsed;
            diagram.LayoutTransform = Transform.Identity;
            diagram.Width = double.NaN;
            fit.Child = diagram;
            fit.Visibility = Visibility.Visible;
            return;
        }

        restoring = true;
        fit.Child = null;
        fit.Visibility = Visibility.Collapsed;
        SizeMagnifiedDiagram();
        diagram.LayoutTransform = new ScaleTransform(state.Scale, state.Scale);
        pan.MaxHeight = Math.Clamp(SystemParameters.WorkArea.Height - 255, 260, 680);
        pan.Content = diagram;
        pan.Visibility = Visibility.Visible;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            if (state.Scale <= 1 || !IsLoaded) { restoring = false; return; }
            pan.UpdateLayout();
            pan.ScrollToHorizontalOffset(state.Horizontal);
            pan.ScrollToVerticalOffset(state.Vertical);
            pan.UpdateLayout();
            restoring = false;
        }));
    }

    void SizeMagnifiedDiagram()
    {
        if (surface.ActualWidth > 0) diagram.Width = Math.Max(100, surface.ActualWidth - SystemParameters.VerticalScrollBarWidth);
    }

    void UpdateButtons()
    {
        smaller.IsEnabled = reset.IsEnabled = state.Scale > 1;
        larger.IsEnabled = state.Scale < MaximumScale;
        reset.Content = (state.Scale * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
    }

    void StartDrag(object sender, MouseButtonEventArgs e)
    {
        if (state.Scale <= 1 || e.ChangedButton != MouseButton.Left || e.OriginalSource is not DependencyObject source || IsInsideButton(source)) return;
        pan.Focus();
        dragStart = e.GetPosition(pan);
        scrollStart = new Point(pan.HorizontalOffset, pan.VerticalOffset);
        if (!diagram.CaptureMouse()) { dragStart = null; return; }
        diagram.Cursor = Cursors.SizeAll;
        e.Handled = true;
    }

    void MoveDrag(object sender, MouseEventArgs e)
    {
        if (dragStart is not Point start) return;
        if (e.LeftButton != MouseButtonState.Pressed) { EndDrag(); return; }
        var movement = start - e.GetPosition(pan);
        pan.ScrollToHorizontalOffset(scrollStart.X + movement.X);
        pan.ScrollToVerticalOffset(scrollStart.Y + movement.Y);
        e.Handled = true;
    }

    void EndDrag()
    {
        dragStart = null;
        diagram.Cursor = null;
        if (diagram.IsMouseCaptured) diagram.ReleaseMouseCapture();
    }

    static bool IsInsideButton(DependencyObject origin)
    {
        for (DependencyObject? current = origin; current != null; current = current is Visual ? VisualTreeHelper.GetParent(current) : LogicalTreeHelper.GetParent(current))
            if (current is Button) return true;
        return false;
    }

    static Button ZoomButton(string text, string title, string id, Action action)
    {
        var button = new Button
        {
            Content = text, ToolTip = title, FontSize = 12, MinHeight = 29, MinWidth = 34,
            Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(4, 0, 0, 0)
        };
        AutomationProperties.SetName(button, title);
        AutomationProperties.SetAutomationId(button, id);
        button.Click += (_, _) => action();
        return button;
    }
}
