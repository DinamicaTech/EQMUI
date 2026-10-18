using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DinamicaBackupAgent;

/// <summary>Colours and small building blocks shared by every screen.</summary>
internal static class Ui
{
    public static readonly Brush PageBack = Hex("#E9ECF0"), CardBack = Hex("#FFFFFF"), Line = Hex("#D5DAE1"), SoftLine = Hex("#EEF0F3"),
        Ink = Hex("#1B1F24"), Muted = Hex("#5B6573"), Faint = Hex("#8A93A0"), Accent = Hex("#1D4ED8"), AccentSoft = Hex("#DCE6FB"), RowSelected = Hex("#EEF3FD"),
        NavBack = Hex("#F6F7F9"), FieldLine = Hex("#C3CAD3"), Disabled = Hex("#F2F4F7"),
        Red = Hex("#B42318"), RedInk = Hex("#8F1D14"), RedBack = Hex("#FDECEA"), RedLine = Hex("#F3B8B2"),
        Amber = Hex("#B45309"), AmberInk = Hex("#8A4307"), AmberBack = Hex("#FEF4E6"), AmberLine = Hex("#F1CF9D"),
        Green = Hex("#1F6B45"), GreenBack = Hex("#E3F2EA");
    public static readonly FontFamily Mono = new("Cascadia Mono, Consolas");

    private static SolidColorBrush Hex(string value)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(value)!;
        brush.Freeze(); return brush;
    }

    public static void Tip(FrameworkElement control, string text)
    {
        control.ToolTip = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, MaxWidth = 420 };
        ToolTipService.SetShowOnDisabled(control, true);
        ToolTipService.SetShowDuration(control, 30000);
    }

    public static Border Card(UIElement child, double padding = 16) => new()
    {
        Background = CardBack, BorderBrush = Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10),
        Padding = new Thickness(padding), Margin = new Thickness(0, 0, 0, 12), Child = child
    };

    public static TextBlock Title(string text) => new() { Text = text, FontSize = 22, FontWeight = FontWeights.SemiBold, Foreground = Ink };
    public static TextBlock Heading(string text) => new() { Text = text, FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = Ink, Margin = new Thickness(0, 0, 0, 8) };
    public static TextBlock Note(string text, Brush? color = null) => new() { Text = text, FontSize = 13, Foreground = color ?? Muted, TextWrapping = TextWrapping.Wrap };
    public static TextBlock Label(string text) => new() { Text = text, FontSize = 12, Foreground = Muted, Margin = new Thickness(0, 0, 0, 4) };
    public static TextBlock Text(string text, Brush? color = null, bool bold = false) => new()
    {
        Text = text, FontSize = 13, Foreground = color ?? Ink, TextWrapping = TextWrapping.Wrap, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, VerticalAlignment = VerticalAlignment.Center
    };

    public static StackPanel Stack(double gap = 8, params UIElement[] children)
    {
        var panel = new StackPanel();
        foreach (var child in children) Add(panel, child, gap);
        return panel;
    }
    public static void Add(StackPanel panel, UIElement child, double gap = 8)
    {
        if (panel.Children.Count > 0 && child is FrameworkElement element && element.Margin == default)
            element.Margin = panel.Orientation == Orientation.Vertical ? new Thickness(0, gap, 0, 0) : new Thickness(gap, 0, 0, 0);
        panel.Children.Add(child);
    }
    public static StackPanel Row(double gap, params UIElement[] children)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        foreach (var child in children) Add(panel, child, gap);
        return panel;
    }

    public static Ellipse Dot(Brush color, double size = 8) => new() { Width = size, Height = size, Fill = color, VerticalAlignment = VerticalAlignment.Center };

    public static Border Pill(string text, Brush back, Brush ink) => new()
    {
        Background = back, CornerRadius = new CornerRadius(10), Padding = new Thickness(9, 2, 9, 2), VerticalAlignment = VerticalAlignment.Center,
        Child = new TextBlock { Text = text, FontSize = 12, Foreground = ink }
    };

    public static ControlTemplate FlatTemplate()
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetBinding(Border.BackgroundProperty, new Binding(nameof(Control.Background)) { RelativeSource = RelativeSource.TemplatedParent });
        border.SetBinding(Border.BorderBrushProperty, new Binding(nameof(Control.BorderBrush)) { RelativeSource = RelativeSource.TemplatedParent });
        border.SetBinding(Border.BorderThicknessProperty, new Binding(nameof(Control.BorderThickness)) { RelativeSource = RelativeSource.TemplatedParent });
        border.SetBinding(Border.PaddingProperty, new Binding(nameof(Control.Padding)) { RelativeSource = RelativeSource.TemplatedParent });
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetBinding(FrameworkElement.HorizontalAlignmentProperty, new Binding(nameof(Control.HorizontalContentAlignment)) { RelativeSource = RelativeSource.TemplatedParent });
        content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(content);
        return new ControlTemplate(typeof(ButtonBase)) { VisualTree = border };
    }
    // Templates belong to the thread that created them; each UI thread gets its own.
    [ThreadStatic] private static ControlTemplate? _flat;
    private static ControlTemplate Flat => _flat ??= FlatTemplate();

    public enum Kind { Primary, Secondary, Dashed }
    public static Button Button(string text, string tip, Kind kind = Kind.Secondary)
    {
        var button = new Button { Content = text, Template = Flat, Padding = new Thickness(12, 6, 12, 6), FontSize = 13, Cursor = System.Windows.Input.Cursors.Hand,
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, BorderThickness = new Thickness(1) };
        void Paint()
        {
            if (!button.IsEnabled) { button.Background = Disabled; button.BorderBrush = Line; button.Foreground = Faint; return; }
            button.Background = kind == Kind.Primary ? Accent : CardBack;
            button.BorderBrush = kind == Kind.Primary ? Accent : kind == Kind.Dashed ? Faint : FieldLine;
            button.Foreground = kind == Kind.Primary ? Brushes.White : Ink;
        }
        Paint(); button.IsEnabledChanged += (_, _) => Paint();
        Tip(button, tip); return button;
    }

    /// <summary>A text button that looks like a link.</summary>
    public static Button Link(string text, string tip)
    {
        var button = new Button { Template = Flat, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand,
            Content = new TextBlock { Text = text, Foreground = Accent, TextDecorations = TextDecorations.Underline, FontSize = 13 }, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left };
        Tip(button, tip); return button;
    }

    public static TextBox Box(double width, string tip, bool mono = false)
    {
        var box = new TextBox { Width = width, FontSize = mono ? 12 : 13, Padding = new Thickness(6, 4, 6, 4), BorderBrush = FieldLine, VerticalAlignment = VerticalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
        if (mono) box.FontFamily = Mono;
        if (double.IsNaN(width)) box.HorizontalAlignment = HorizontalAlignment.Stretch;
        Tip(box, tip); return box;
    }

    /// <summary>Coloured strip with a dot, a title, the cause and an optional action.</summary>
    public static Border Banner(Severity severity, string title, string cause, Button? action)
    {
        var (back, line, dot, ink) = severity == Severity.Error ? (RedBack, RedLine, Red, RedInk) : (AmberBack, AmberLine, Amber, AmberInk);
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var marker = Dot(dot, 10); marker.Margin = new Thickness(0, 4, 12, 0); marker.VerticalAlignment = VerticalAlignment.Top;
        grid.Children.Add(marker);
        var text = Stack(2, Text(title, Ink, true), Text(cause, ink));
        Grid.SetColumn(text, 1); grid.Children.Add(text);
        if (action is not null) { action.Margin = new Thickness(14, 0, 0, 0); Grid.SetColumn(action, 2); grid.Children.Add(action); }
        return new Border { Background = back, BorderBrush = line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(14, 12, 14, 12), Margin = new Thickness(0, 0, 0, 10), Child = grid };
    }

    /// <summary>Grid whose columns have the given widths; 0 means "the rest".</summary>
    public static Grid Columns(params double[] widths)
    {
        var grid = new Grid();
        foreach (var width in widths)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = width == 0 ? new GridLength(1, GridUnitType.Star) : width < 0 ? GridLength.Auto : new GridLength(width) });
        return grid;
    }
    public static void Place(Grid grid, UIElement child, int row, int column, int span = 1)
    {
        while (grid.RowDefinitions.Count <= row) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(child, row); Grid.SetColumn(child, column); Grid.SetColumnSpan(child, span); grid.Children.Add(child);
    }
}
