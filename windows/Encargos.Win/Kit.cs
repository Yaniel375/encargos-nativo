using Encargos.Core;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
// Solo Color: Windows.UI también define Colors y ColorHelper, que chocarían con los de Microsoft.UI.
using Color = Windows.UI.Color;

namespace Encargos.Win;

/// <summary>Piezas de interfaz reutilizables (mismo aspecto y textos que la app Android).</summary>
public static class K
{
    static readonly Dictionary<uint, SolidColorBrush> _br = new();
    public static SolidColorBrush Br(uint argb) { if (!_br.TryGetValue(argb, out var b)) { b = new SolidColorBrush(ColorHelper.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb)); _br[argb] = b; } return b; }
    public static SolidColorBrush Alpha(SolidColorBrush b, double a) { var c = b.Color; return new SolidColorBrush(Color.FromArgb((byte)(a * 255), c.R, c.G, c.B)); }

    public static bool Day => Application.Current.RequestedTheme == ApplicationTheme.Light;
    public static SolidColorBrush Blue => Br(Day ? 0xFF2563EBu : 0xFF3B82F6u);
    public static SolidColorBrush Green => Br(Day ? 0xFF059669u : 0xFF34D399u);
    public static SolidColorBrush Rose => Br(Day ? 0xFFE11D48u : 0xFFFB7185u);
    public static SolidColorBrush Amber => Br(Day ? 0xFFD97706u : 0xFFFBBF24u);
    public static SolidColorBrush Vio => Br(Day ? 0xFF7C3AEDu : 0xFFA78BFAu);
    public static SolidColorBrush Teal => Br(Day ? 0xFF0D9488u : 0xFF2DD4BFu);
    public static SolidColorBrush Mut => Br(Day ? 0xFF64748Bu : 0xFF94A3B8u);
    public static SolidColorBrush Text => Br(Day ? 0xFF0F172Au : 0xFFF1F5F9u);
    public static SolidColorBrush Bg => Br(Day ? 0xFFF4F6FBu : 0xFF070B14u);
    public static SolidColorBrush CardBg => Br(Day ? 0xFFFFFFFFu : 0xFF131C2Eu);
    public static SolidColorBrush LineBr => Br(Day ? 0xFFE2E8F0u : 0xFF26324Au);
    public static SolidColorBrush Status(string? s) => s switch { "En tránsito" => Br(0xFF94A3B8u), "Recibido" => Br(0xFF7DD3FCu), "Entregado" => Green, "Cancelado" => Br(0xFF64748Bu), _ => Blue };

    public static string Money(object? v) => Js.Money(v);
    public static string Lb(double v) => Js.ToFixed(v, 2) + " lb";
    public static string Signed(double v) => (v < 0 ? "-" : "") + Js.Money(Math.Abs(v));

    /* ---------- texto ---------- */
    public static TextBlock T(string text, double size = 14, bool bold = false, Brush? color = null, bool wrap = true, int maxLines = 0)
    {
        var t = new TextBlock { Text = text, FontSize = size, TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap, Foreground = color ?? Text, IsTextSelectionEnabled = false };
        if (bold) t.FontWeight = FontWeights.SemiBold;
        if (maxLines > 0) { t.MaxLines = maxLines; t.TextTrimming = TextTrimming.CharacterEllipsis; }
        return t;
    }
    public static TextBlock H(string text, double size = 20) { var t = T(text, size, true); t.FontWeight = FontWeights.Bold; return t; }
    public static TextBlock Muted(string text, double size = 12) => T(text, size, false, Mut);
    public static TextBlock Big(string text, Brush? color = null, double size = 26) { var t = T(text, size, true, color); t.FontWeight = FontWeights.Bold; return t; }

    /* ---------- contenedores ---------- */
    public static StackPanel V(double spacing = 8, params UIElement?[] items) { var s = new StackPanel { Spacing = spacing }; foreach (var i in items) if (i != null) s.Children.Add(i); return s; }
    public static StackPanel Hs(double spacing = 8, params UIElement?[] items) { var s = new StackPanel { Orientation = Orientation.Horizontal, Spacing = spacing }; foreach (var i in items) if (i != null) { if (i is FrameworkElement fe) fe.VerticalAlignment = VerticalAlignment.Center; s.Children.Add(i); } return s; }
    /// <summary>Fila con columnas del mismo ancho.</summary>
    public static Grid Cols(params UIElement?[] items)
    {
        var g = new Grid { ColumnSpacing = 8 }; var n = 0;
        foreach (var i in items) { if (i == null) continue; g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); Grid.SetColumn((FrameworkElement)i, n++); g.Children.Add(i); }
        return g;
    }
    /// <summary>Fila: el primer elemento ocupa el espacio libre, el resto su tamaño.</summary>
    public static Grid Row(UIElement main, params UIElement?[] rest)
    {
        var g = new Grid { ColumnSpacing = 10 };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); g.Children.Add(main);
        if (main is FrameworkElement m) m.VerticalAlignment = VerticalAlignment.Center;
        var n = 1;
        foreach (var r in rest) { if (r == null) continue; g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); var fe = (FrameworkElement)r; fe.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(fe, n++); g.Children.Add(r); }
        return g;
    }
    /// <summary>Fila: elementos de tamaño propio a la izquierda, el último ocupa el resto.</summary>
    public static Grid Lead(UIElement first, UIElement main, params UIElement?[] rest)
    {
        var g = new Grid { ColumnSpacing = 12 };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ((FrameworkElement)first).VerticalAlignment = VerticalAlignment.Center; ((FrameworkElement)main).VerticalAlignment = VerticalAlignment.Center;
        g.Children.Add(first); Grid.SetColumn((FrameworkElement)main, 1); g.Children.Add(main);
        var n = 2;
        foreach (var r in rest) { if (r == null) continue; g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); var fe = (FrameworkElement)r; fe.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(fe, n++); g.Children.Add(r); }
        return g;
    }
    public static Border Card(string? title, UIElement? action, params UIElement?[] children)
    {
        var s = new StackPanel { Spacing = 8 };
        if (title != null || action != null) s.Children.Add(action != null ? (UIElement)Row(T(title ?? "", 15, true), action) : T(title!, 15, true));
        foreach (var c in children) if (c != null) s.Children.Add(c);
        return new Border { Child = s, Padding = new Thickness(16), CornerRadius = new CornerRadius(14), Background = CardBg, BorderBrush = LineBr, BorderThickness = new Thickness(1) };
    }
    public static Border Card(params UIElement?[] children) => Card((string?)null, null, children);

    /// <summary>Hace que cualquier elemento sea pulsable.</summary>
    public static Button Click(UIElement content, Action onClick)
    {
        var b = new Button { Content = content, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Background = new SolidColorBrush(Colors.Transparent), BorderThickness = new Thickness(0), Padding = new Thickness(0) };
        b.Click += (_, _) => onClick(); return b;
    }
    public static Border ClickCard(Action onClick, params UIElement?[] children)
    {
        var c = Card(children); var inner = c.Child; c.Child = null;
        var b = Click(inner, onClick); b.Padding = new Thickness(0); c.Child = b; return c;
    }

    /* ---------- botones ---------- */
    public static Button Btn(string text, Action onClick, bool accent = false, Brush? color = null, string? glyph = null, bool stretch = true)
    {
        object content = text;
        if (glyph != null) content = Hs(8, new FontIcon { Glyph = glyph, FontSize = 14 }, new TextBlock { Text = text });
        var b = new Button { Content = content, HorizontalAlignment = stretch ? HorizontalAlignment.Stretch : HorizontalAlignment.Left, Padding = new Thickness(14, 8, 14, 8), CornerRadius = new CornerRadius(10) };
        if (accent) { if (Application.Current.Resources.TryGetValue("AccentButtonStyle", out var st)) b.Style = (Style)st; if (color != null) { b.Background = color; b.Foreground = Br(0xFFFFFFFFu); } }
        else if (color != null) b.Foreground = color;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(b, text); // lectores de pantalla y pruebas automáticas
        b.Click += (_, _) => onClick(); return b;
    }
    public static HyperlinkButton Link(string text, Action onClick, Brush? color = null) { var h = new HyperlinkButton { Content = text, Padding = new Thickness(4, 2, 4, 2) }; if (color != null) h.Foreground = color; h.Click += (_, _) => onClick(); return h; }

    /* ---------- campos ---------- */
    public static TextBox Box(string header, string value, Action<string> onChange, bool multiline = false, string? placeholder = null, int maxLen = 0)
    {
        var t = new TextBox { Header = header, Text = value, PlaceholderText = placeholder ?? "", AcceptsReturn = multiline, TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap, MinHeight = multiline ? 90 : 0 };
        if (maxLen > 0) t.MaxLength = maxLen;
        t.TextChanged += (_, _) => onChange(t.Text); return t;
    }
    public static PasswordBox Pass(string header, Action<string> onChange) { var p = new PasswordBox { Header = header }; p.PasswordChanged += (_, _) => onChange(p.Password); return p; }
    /// <summary>Número escrito por el usuario (acepta coma decimal).</summary>
    public static double Num(string s) => Js.Num(s.Replace(',', '.').Replace("$", "").Trim());

    public static ComboBox Combo(IEnumerable<(string Key, string Label)> options, string selected, Action<string> onSelect, string? header = null)
    {
        var opts = options.ToList();
        var c = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, MinWidth = 160 };
        if (header != null) c.Header = header;
        foreach (var o in opts) c.Items.Add(o.Label);
        c.SelectedIndex = Math.Max(0, opts.FindIndex(o => o.Key == selected));
        c.SelectionChanged += (_, _) => { if (c.SelectedIndex >= 0) onSelect(opts[c.SelectedIndex].Key); };
        return c;
    }
    /// <summary>Opciones en fila (como los "chips" de Android).</summary>
    public static UIElement Choice(IEnumerable<(string Key, string Label)> options, string selected, Action<string> onSelect)
    {
        var s = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        foreach (var (k, l) in options)
        {
            var on = k == selected;
            var b = new ToggleButton { Content = l, IsChecked = on, CornerRadius = new CornerRadius(16), Padding = new Thickness(12, 5, 12, 5) };
            var key = k; b.Click += (_, _) => onSelect(key);
            s.Children.Add(b);
        }
        return new ScrollViewer { Content = s, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, HorizontalScrollMode = ScrollMode.Enabled, VerticalScrollMode = ScrollMode.Disabled, Padding = new Thickness(0, 0, 0, 4) };
    }
    public static Grid Toggle(string title, string sub, bool value, Action<bool> onChange)
    {
        var sw = new ToggleSwitch { IsOn = value, OnContent = "", OffContent = "", MinWidth = 0 };
        sw.Toggled += (_, _) => { if (sw.IsOn != value) { value = sw.IsOn; onChange(sw.IsOn); } };
        return Row(V(2, T(title, 14, true), sub.Length > 0 ? Muted(sub) : null), sw);
    }

    /* ---------- datos ---------- */
    public static Border Kpi(string label, string value, Brush? color = null, string? sub = null) =>
        new() { Padding = new Thickness(12), CornerRadius = new CornerRadius(12), Background = Alpha(Mut, 0.08), Child = V(2, Muted(label), Big(value, color, 18), sub != null ? Muted(sub, 11) : null) };
    public static FrameworkElement Chip(string text, Brush color, Action? onClick = null)
    {
        var bg = Alpha((SolidColorBrush)color, 0.16);
        if (onClick == null) return new Border { CornerRadius = new CornerRadius(12), Background = bg, Padding = new Thickness(10, 3, 10, 3), Child = T(text, 12, true, color, false) };
        var b = new Button { CornerRadius = new CornerRadius(12), Background = bg, BorderThickness = new Thickness(0), Padding = new Thickness(10, 3, 10, 3), MinHeight = 0, Content = T(text, 12, true, color, false) };
        b.Click += (_, _) => onClick(); return b;
    }
    public static StackPanel Chips(params UIElement?[] items) { var s = Hs(6, items); return s; }
    public static Grid Line(string label, string value, Brush? color = null, bool bold = false) => Row(T(label, 13, false, Mut), T(value, 14, bold, color, false));
    public static ProgressBar Progress(double k, Brush? color = null) => new() { Minimum = 0, Maximum = 1, Value = Math.Clamp(double.IsNaN(k) ? 0 : k, 0, 1), Foreground = color ?? Green, Height = 6 };
    public static Grid Gauge(double pct, string label = "cobrado", double size = 130)
    {
        var g = new Grid { Width = size, Height = size };
        g.Children.Add(new ProgressRing { IsIndeterminate = false, Minimum = 0, Maximum = 100, Value = Math.Clamp(double.IsNaN(pct) ? 0 : pct, 0, 100), Width = size, Height = size, Foreground = Green, Background = Alpha(Mut, 0.2) });
        var t = V(0, Big(Js.ToFixed(Math.Max(0, pct), 0) + "%", null, 22), Muted(label)); t.HorizontalAlignment = HorizontalAlignment.Center; t.VerticalAlignment = VerticalAlignment.Center;
        foreach (var c in t.Children) ((FrameworkElement)c).HorizontalAlignment = HorizontalAlignment.Center;
        g.Children.Add(t); return g;
    }
    public static Grid Bars(List<(string Label, double Value)> items, Brush? color = null, double height = 140)
    {
        var g = new Grid { ColumnSpacing = 6, Height = height + 36 };
        var mx = Math.Max(1e-9, items.Count == 0 ? 1 : items.Max(i => i.Value));
        for (var i = 0; i < items.Count; i++)
        {
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var (l, v) = items[i];
            var col = new Grid(); col.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); col.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var bar = new Border { Height = Math.Max(2, v / mx * height), VerticalAlignment = VerticalAlignment.Bottom, CornerRadius = new CornerRadius(6, 6, 2, 2), Background = color ?? Blue };
            ToolTipService.SetToolTip(bar, Js.Money(v));
            col.Children.Add(bar);
            var lab = Muted(l, 11); lab.HorizontalAlignment = HorizontalAlignment.Center; lab.TextWrapping = TextWrapping.NoWrap; Grid.SetRow(lab, 1); col.Children.Add(lab);
            Grid.SetColumn(col, i); g.Children.Add(col);
        }
        return g;
    }
    public static Border Avatar(string name, Brush? color = null, double size = 40)
    {
        var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var ini = parts.Length == 0 ? "?" : (parts[0][..1] + (parts.Length > 1 ? parts[1][..1] : "")).ToUpperInvariant();
        var c = (SolidColorBrush)(color ?? Blue);
        var t = T(ini, size * 0.38, true, c, false); t.HorizontalAlignment = HorizontalAlignment.Center; t.VerticalAlignment = VerticalAlignment.Center;
        return new Border { Width = size, Height = size, CornerRadius = new CornerRadius(size / 2), Background = Alpha(c, 0.18), Child = t };
    }
    public static Border Dot(Brush color, double size = 9) => new() { Width = size, Height = size, CornerRadius = new CornerRadius(size / 2), Background = color, VerticalAlignment = VerticalAlignment.Center };
    public static StackPanel Empty(string title, string text = "", UIElement? action = null)
    {
        var s = V(8, T(title, 16, true), text.Length > 0 ? Muted(text, 13) : null, action);
        s.Padding = new Thickness(0, 30, 0, 30); s.HorizontalAlignment = HorizontalAlignment.Center;
        foreach (var c in s.Children) ((FrameworkElement)c).HorizontalAlignment = HorizontalAlignment.Center;
        return s;
    }
    public static AutoSuggestBox Search(string q, string hint, Action<string> onQ)
    {
        var b = new AutoSuggestBox { Text = q, PlaceholderText = hint, QueryIcon = new SymbolIcon(Symbol.Find) };
        b.TextChanged += (s, e) => { if (e.Reason == AutoSuggestionBoxTextChangeReason.UserInput) onQ(b.Text); };
        return b;
    }

    /// <summary>Etiqueta corta de fecha: Hoy, Ayer, 05 sept.</summary>
    public static string DayLabel(object? d)
    {
        var t = Js.ParseDate(d); if (double.IsNaN(t)) return "";
        var ld = DateOnly.FromDateTime(Js.Local(t)); var today = Js.Today();
        if (ld == today) return "Hoy"; if (ld == today.AddDays(-1)) return "Ayer";
        var m = new[] { "ene", "feb", "mar", "abr", "may", "jun", "jul", "ago", "sept", "oct", "nov", "dic" }[ld.Month - 1];
        return $"{ld.Day:00} {m}" + (ld.Year != today.Year ? " " + ld.Year : "");
    }
}
