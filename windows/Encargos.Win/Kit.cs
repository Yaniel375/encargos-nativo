using Encargos.Core;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Foundation;
// Solo Color: Windows.UI también define Colors y ColorHelper, que chocarían con los de Microsoft.UI.
using Color = Windows.UI.Color;

namespace Encargos.Win;

/// <summary>Piezas de interfaz reutilizables (mismo aspecto y textos que la app Android).</summary>
public static class K
{
    static readonly Dictionary<uint, SolidColorBrush> _br = new();
    public static SolidColorBrush Br(uint argb) { if (!_br.TryGetValue(argb, out var b)) { b = new SolidColorBrush(ColorHelper.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb)); _br[argb] = b; } return b; }
    public static SolidColorBrush Alpha(SolidColorBrush b, double a) { var c = b.Color; return new SolidColorBrush(Color.FromArgb((byte)(a * 255), c.R, c.G, c.B)); }

    /// <summary>Tema vigente (día/noche). Lo cambia MainWindow.ApplyTheme sin reiniciar; todos los colores salen de aquí.</summary>
    public static bool Day { get; set; }
    public static SolidColorBrush Blue => Br(Day ? 0xFF2563EBu : 0xFF3B82F6u);
    public static SolidColorBrush Green => Br(Day ? 0xFF047857u : 0xFF34D399u);
    public static SolidColorBrush Rose => Br(Day ? 0xFFE11D48u : 0xFFFB7185u);
    public static SolidColorBrush Amber => Br(Day ? 0xFFD97706u : 0xFFFBBF24u);
    public static SolidColorBrush Vio => Br(Day ? 0xFF7C3AEDu : 0xFFA78BFAu);
    public static SolidColorBrush Teal => Br(Day ? 0xFF0F766Eu : 0xFF2DD4BFu);
    public static SolidColorBrush Mut => Br(Day ? 0xFF526079u : 0xFF9AA6B8u);
    public static SolidColorBrush Text => Br(Day ? 0xFF0F172Au : 0xFFF1F5F9u);
    public static SolidColorBrush Bg => Br(Day ? 0xFFEEF2F8u : 0xFF020611u);
    public static SolidColorBrush BtnBg => Br(Day ? 0x0A0F172Au : 0x0FFFFFFFu);
    public static SolidColorBrush BtnLine => Br(Day ? 0x1A0F172Au : 0x1AFFFFFFu);
    public static SolidColorBrush AvatarBg => Br(Day ? 0xFFE2E8F0u : 0xFF1E293Bu);
    public static SolidColorBrush BadgeRing => Br(Day ? 0xFFFFFFFFu : 0xFF090D18u);
    public static SolidColorBrush Track => Br(Day ? 0x1A0F172Au : 0x2894A3B8u);
    public static SolidColorBrush White => Br(0xFFFFFFFFu);
    public static SolidColorBrush CardBg => Br(Day ? 0xFFFFFFFFu : 0xFF0E1628u);
    public static SolidColorBrush LineBr => Br(Day ? 0x1F0F172Au : 0x2494A3B8u);
    public static SolidColorBrush Line2 => Br(Day ? 0x330F172Au : 0x3D94A3B8u);
    public static SolidColorBrush Tile => Br(Day ? 0x0A0F172Au : 0x1494A3B8u);
    public static SolidColorBrush BlueInk => Br(Day ? 0xFF1D4ED8u : 0xFF93C5FDu);
    public static SolidColorBrush NavOff => Br(Day ? 0xFF64748Bu : 0xFF7C8BA5u);
    public static SolidColorBrush Status(string? s) => s switch { "En tránsito" => Br(Day ? 0xFF475569u : 0xFF94A3B8u), "Recibido" => Br(Day ? 0xFF0369A1u : 0xFF7DD3FCu), "Entregado" => Green, "Cancelado" => Br(0xFF64748Bu), _ => Blue };

    /// <summary>Vidrio de la web (.aCard): linear-gradient(160deg, rgba(30,41,59,.62), rgba(15,23,42,.72)).</summary>
    public static Brush Glass => Day ? Br(0xFFFFFFFFu) : new LinearGradientBrush { StartPoint = new Point(0.2, 0), EndPoint = new Point(0.8, 1),
        GradientStops = { new GradientStop { Color = Color.FromArgb(158, 30, 41, 59), Offset = 0 }, new GradientStop { Color = Color.FromArgb(184, 15, 23, 42), Offset = 1 } } };
    public static Brush PrimaryBrush => new LinearGradientBrush { StartPoint = new Point(0.2, 0), EndPoint = new Point(0.8, 1),
        GradientStops = { new GradientStop { Color = Color.FromArgb(255, 0x4F, 0x8D, 0xF7), Offset = 0 }, new GradientStop { Color = Color.FromArgb(255, 0x25, 0x63, 0xEB), Offset = 1 } } };
    public static Brush Grad(SolidColorBrush c)
    {
        var k = c.Color; byte L(byte v) => (byte)(v + (255 - v) * .15);
        return new LinearGradientBrush { StartPoint = new Point(0.2, 0), EndPoint = new Point(0.8, 1), GradientStops = { new GradientStop { Color = Color.FromArgb(255, L(k.R), L(k.G), L(k.B)), Offset = 0 }, new GradientStop { Color = k, Offset = 1 } } };
    }

    static FontFamily? _font;
    /// <summary>Manrope (la fuente de la app web).</summary>
    public static FontFamily Font => _font ??= Application.Current.Resources.TryGetValue("ManropeFont", out var f) && f is FontFamily ff ? ff : new FontFamily("Segoe UI Variable Text");
    static Style? _btnStyle;
    public static Style? BtnStyle => _btnStyle ??= Application.Current.Resources.TryGetValue("AuroraButton", out var s) ? (Style)s : null;

    public static string Money(object? v) => Js.Money(v);
    public static string Lb(double v) => Js.ToFixed(v, 2) + " lb";
    public static string Signed(double v) => (v < 0 ? "-" : "") + Js.Money(Math.Abs(v));

    /* ---------- texto ---------- */
    public static TextBlock T(string text, double size = 14, bool bold = false, Brush? color = null, bool wrap = true, int maxLines = 0)
    {
        var t = new TextBlock { Text = text, FontSize = size, FontFamily = Font, TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap, Foreground = color ?? Text, IsTextSelectionEnabled = false };
        if (bold) t.FontWeight = FontWeights.Bold;
        if (maxLines > 0) { t.MaxLines = maxLines; t.TextTrimming = TextTrimming.CharacterEllipsis; }
        return t;
    }
    public static TextBlock H(string text, double size = 20) { var t = T(text, size, true); t.FontWeight = FontWeights.ExtraBold; t.CharacterSpacing = -10; return t; }
    public static TextBlock Muted(string text, double size = 12) => T(text, size, false, Mut);
    public static TextBlock Big(string text, Brush? color = null, double size = 26) { var t = T(text, size, true, color); t.FontWeight = FontWeights.ExtraBold; t.CharacterSpacing = -15; return t; }

    /* ---------- contenedores ---------- */
    public static StackPanel V(double spacing = 8, params UIElement?[] items) { var s = new StackPanel { Spacing = spacing }; foreach (var i in items) if (i != null) s.Children.Add(i); return s; }
    public static StackPanel Hs(double spacing = 8, params UIElement?[] items) { var s = new StackPanel { Orientation = Orientation.Horizontal, Spacing = spacing }; foreach (var i in items) if (i != null) { if (i is FrameworkElement fe) fe.VerticalAlignment = VerticalAlignment.Center; s.Children.Add(i); } return s; }
    /// <summary>Fila con columnas del mismo ancho.</summary>
    public static Grid Cols(params UIElement?[] items)
    {
        var g = new Grid { ColumnSpacing = 12 }; var n = 0;
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
        var g = new Grid { ColumnSpacing = 14 };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ((FrameworkElement)first).VerticalAlignment = VerticalAlignment.Center; ((FrameworkElement)main).VerticalAlignment = VerticalAlignment.Center;
        g.Children.Add(first); Grid.SetColumn((FrameworkElement)main, 1); g.Children.Add(main);
        var n = 2;
        foreach (var r in rest) { if (r == null) continue; g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); var fe = (FrameworkElement)r; fe.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(fe, n++); g.Children.Add(r); }
        return g;
    }
    /// <summary>Tarjeta de vidrio (.aCard): radio 18, borde fino, título 16 extra negrita.</summary>
    public static Border Card(string? title, UIElement? action, params UIElement?[] children)
    {
        var s = new StackPanel { Spacing = 10 };
        if (title != null || action != null) { var tt = T(title ?? "", 16, true); tt.FontWeight = FontWeights.ExtraBold; s.Children.Add(action != null ? (UIElement)Row(tt, action) : tt); }
        foreach (var c in children) if (c != null) s.Children.Add(c);
        return new Border { Child = s, Padding = new Thickness(18), CornerRadius = new CornerRadius(18), Background = Glass, BorderBrush = LineBr, BorderThickness = new Thickness(1) };
    }
    public static Border Card(params UIElement?[] children) => Card((string?)null, null, children);
    /// <summary>Tarjeta con icono en el título (como "Estado de los pedidos").</summary>
    public static Border IconCard(string glyph, string title, UIElement? action, params UIElement?[] children)
    {
        var head = Hs(9, new FontIcon { Glyph = glyph, FontSize = 17, Foreground = Text }, T(title, 16, true));
        ((TextBlock)head.Children[1]).FontWeight = FontWeights.ExtraBold;
        var c = Card((string?)null, null, children); ((StackPanel)c.Child).Children.Insert(0, action != null ? Row(head, action) : head); return c;
    }

    /// <summary>Hace que cualquier elemento sea pulsable (con brillo al pasar el ratón y rebote al pulsar).</summary>
    public static Button Click(UIElement content, Action onClick)
    {
        var b = new Button { Content = content, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch, Background = new SolidColorBrush(Colors.Transparent), BorderThickness = new Thickness(0), Padding = new Thickness(0), CornerRadius = new CornerRadius(12) };
        if (BtnStyle != null) b.Style = BtnStyle;
        b.Padding = new Thickness(0); b.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        b.Click += Once(onClick); return Fx.Bounce(b, .985);
    }
    public static Border ClickCard(Action onClick, params UIElement?[] children)
    {
        var c = Card(children); var inner = c.Child; c.Child = null;
        var pad = c.Padding; c.Padding = new Thickness(0);
        var b = Click(inner, onClick); b.Padding = pad; b.CornerRadius = new CornerRadius(18); c.Child = b; return c;
    }

    /* ---------- botones ---------- */
    /// <summary>Evita que un doble clic registre dos veces la misma operación (700 ms).</summary>
    public static RoutedEventHandler Once(Action a) { var last = DateTime.MinValue; return (_, _) => { var now = DateTime.UtcNow; if ((now - last).TotalMilliseconds < 700) return; last = now; a(); }; }
    /// <summary>Botón: principal (degradado azul, texto blanco) o secundario (vidrio gris con borde).</summary>
    public static Button Btn(string text, Action onClick, bool accent = false, Brush? color = null, string? glyph = null, bool stretch = true)
    {
        var fg = accent ? Br(0xFFFFFFFFu) : color ?? Text;
        var tb = T(text, accent ? 15 : 14, true, fg, false); if (accent) tb.FontWeight = FontWeights.ExtraBold; tb.TextTrimming = TextTrimming.CharacterEllipsis;
        object content = glyph != null ? Hs(8, new FontIcon { Glyph = glyph, FontSize = 15, Foreground = fg }, tb) : tb;
        var b = new Button { Content = content, HorizontalAlignment = stretch ? HorizontalAlignment.Stretch : HorizontalAlignment.Left, Padding = new Thickness(16, 10, 16, 10), CornerRadius = new CornerRadius(14), MinHeight = accent ? 48 : 44, Foreground = fg };
        if (BtnStyle != null) b.Style = BtnStyle;
        if (accent) { b.Background = color is SolidColorBrush sc ? Grad(sc) : PrimaryBrush; b.BorderThickness = new Thickness(0); }
        else if (color is SolidColorBrush c2) { b.Background = Alpha(c2, .08); b.BorderBrush = Alpha(c2, .35); b.BorderThickness = new Thickness(1); }
        else { b.Background = Tile; b.BorderBrush = Line2; b.BorderThickness = new Thickness(1); }
        b.HorizontalContentAlignment = HorizontalAlignment.Center;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(b, text); // lectores de pantalla y pruebas automáticas
        b.Click += Once(onClick); return Fx.Bounce(b);
    }
    public static HyperlinkButton Link(string text, Action onClick, Brush? color = null)
    {
        var h = new HyperlinkButton { Content = T(text, 13, true, color ?? Blue, false), Padding = new Thickness(4, 2, 4, 2), FontFamily = Font };
        h.Click += (_, _) => onClick(); return h;
    }

    /* ---------- campos ---------- */
    static TextBlock Header(string h) { var t = T(h, 12.5, true, Mut, false); t.FontWeight = FontWeights.SemiBold; t.Margin = new Thickness(0, 0, 0, 2); return t; }
    public static TextBox Box(string header, string value, Action<string> onChange, bool multiline = false, string? placeholder = null, int maxLen = 0)
    {
        var t = new TextBox { Header = Header(header), Text = value, PlaceholderText = placeholder ?? "", AcceptsReturn = multiline, TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap, MinHeight = multiline ? 90 : 44,
            CornerRadius = new CornerRadius(14), Padding = new Thickness(13, 11, 13, 10), FontFamily = Font, FontSize = 15, FontWeight = FontWeights.SemiBold };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(t, header);
        if (maxLen > 0) t.MaxLength = maxLen;
        t.TextChanged += (_, _) => onChange(t.Text); return t;
    }
    public static PasswordBox Pass(string header, Action<string> onChange)
    {
        var p = new PasswordBox { Header = Header(header), CornerRadius = new CornerRadius(14), Padding = new Thickness(13, 11, 13, 10), MinHeight = 44, FontFamily = Font, FontSize = 15 };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(p, header);
        p.PasswordChanged += (_, _) => onChange(p.Password); return p;
    }
    /// <summary>Número escrito por el usuario (acepta coma decimal).</summary>
    public static double Num(string s) => Js.Num(s.Replace(',', '.').Replace("$", "").Trim());

    public static ComboBox Combo(IEnumerable<(string Key, string Label)> options, string selected, Action<string> onSelect, string? header = null)
    {
        var opts = options.ToList();
        var c = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, MinWidth = 160, MinHeight = 44, CornerRadius = new CornerRadius(14), FontFamily = Font, FontWeight = FontWeights.SemiBold };
        if (header != null) c.Header = Header(header);
        foreach (var o in opts) c.Items.Add(o.Label);
        c.SelectedIndex = Math.Max(0, opts.FindIndex(o => o.Key == selected));
        c.SelectionChanged += (_, _) => { if (c.SelectedIndex >= 0) onSelect(opts[c.SelectedIndex].Key); };
        return c;
    }
    /// <summary>Filtros en pastilla (Todos · Pedido · En tránsito…).</summary>
    public static UIElement Choice(IEnumerable<(string Key, string Label)> options, string selected, Action<string> onSelect)
    {
        var s = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        foreach (var (k, l) in options)
        {
            var on = k == selected;
            var b = new Button { Content = T(l, 13, true, on ? Br(0xFFFFFFFFu) : Br(Day ? 0xFF334155u : 0xFFCBD5E1u), false), CornerRadius = new CornerRadius(99), Padding = new Thickness(15, 8, 15, 8), MinHeight = 0,
                Background = on ? Alpha(Blue, .2) : Br(Day ? 0x0A0F172Au : 0x0F94A3B8u), BorderBrush = on ? Alpha(Blue, .65) : LineBr, BorderThickness = new Thickness(1) };
            if (BtnStyle != null) b.Style = BtnStyle;
            var key = k; b.Click += (_, _) => onSelect(key);
            s.Children.Add(Fx.Bounce(b, .95));
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
    /// <summary>Recuadro de dato: neutro, o teñido del color del dato (Cobrado verde, Pendiente rojo).</summary>
    public static Border Kpi(string label, string value, Brush? color = null, string? sub = null)
    {
        var tint = color as SolidColorBrush;
        var lab = T(label, 11.5, true, Mut, false); lab.FontWeight = FontWeights.SemiBold;
        return new Border { Padding = new Thickness(12, 10, 12, 10), CornerRadius = new CornerRadius(12), Background = tint != null ? Alpha(tint, .08) : Tile, BorderBrush = tint != null ? Alpha(tint, .30) : LineBr, BorderThickness = new Thickness(1),
            Child = V(2, lab, Big(value, color, 17), sub != null ? Muted(sub, 11) : null) };
    }
    /// <summary>Etiqueta con punto (• Pedido, • Entregado).</summary>
    public static FrameworkElement Chip(string text, Brush color, Action? onClick = null)
    {
        var c = (SolidColorBrush)color; var k = c.Color; byte L(byte v) => (byte)(v + (255 - v) * .45);
        var ink = new SolidColorBrush(Color.FromArgb(255, L(k.R), L(k.G), L(k.B)));
        var tb = T(text, 11.5, true, Day ? c : ink, false);
        var inner = Hs(6, new Border { Width = 6, Height = 6, CornerRadius = new CornerRadius(3), Background = c }, tb);
        if (onClick == null) return new Border { CornerRadius = new CornerRadius(99), Background = Alpha(c, 0.14), BorderBrush = Alpha(c, .38), BorderThickness = new Thickness(1), Padding = new Thickness(10, 3, 10, 4), Child = inner, VerticalAlignment = VerticalAlignment.Center };
        var b = new Button { CornerRadius = new CornerRadius(99), Background = Alpha(c, 0.14), BorderBrush = Alpha(c, .38), BorderThickness = new Thickness(1), Padding = new Thickness(10, 3, 10, 4), MinHeight = 0, Content = inner };
        if (BtnStyle != null) b.Style = BtnStyle;
        b.Click += (_, _) => onClick(); return Fx.Bounce(b, .94);
    }
    public static StackPanel Chips(params UIElement?[] items) { var s = Hs(6, items); return s; }
    public static Grid Line(string label, string value, Brush? color = null, bool bold = false) { var v = T(value, 14, true, color, false); v.FontWeight = bold ? FontWeights.ExtraBold : FontWeights.SemiBold; return Row(T(label, 13.5, false, Mut), v); }
    /// <summary>Barra fina que crece al aparecer (0,9 s).</summary>
    public static FrameworkElement Progress(double k, Brush? color = null)
    {
        k = Math.Clamp(double.IsNaN(k) ? 0 : k, 0, 1);
        var g = new Grid { Height = 6, CornerRadius = new CornerRadius(3), Background = Br(Day ? 0x1A0F172Au : 0x2494A3B8u) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(k, 0.0001), GridUnitType.Star) }); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(1 - k, 0.0001), GridUnitType.Star) });
        var fill = new Border { CornerRadius = new CornerRadius(3), Background = color ?? Green, RenderTransformOrigin = new Point(0, .5) };
        var ct = new CompositeTransform { ScaleX = 0 }; fill.RenderTransform = ct; g.Children.Add(fill);
        fill.Loaded += (_, _) => { var sb = new Storyboard(); sb.Children.Add(Fx.Anim(ct, "ScaleX", 0, 1, 900, 300)); sb.Completed += (_, _) => ct.ScaleX = 1; sb.Begin(); };
        return g;
    }
    /// <summary>Indicador "agua": el círculo se llena de azul hasta el % cobrado.</summary>
    public static Grid Gauge(double pct, string label = "cobrado", double size = 130)
    {
        pct = double.IsNaN(pct) ? 0 : pct; var fill = Math.Clamp(pct / 100, 0, 1);
        var g = new Grid { Width = size, Height = size };
        var baseC = Day ? Color.FromArgb(255, 0xE2, 0xE8, 0xF0) : Color.FromArgb(204, 15, 23, 42);
        var s1 = new GradientStop { Color = baseC, Offset = 1 }; var s2 = new GradientStop { Color = Color.FromArgb(255, 0x60, 0xA5, 0xFA), Offset = 1 };
        var water = new LinearGradientBrush { StartPoint = new Point(.5, 0), EndPoint = new Point(.5, 1), GradientStops = { new GradientStop { Color = baseC, Offset = 0 }, s1, s2, new GradientStop { Color = Color.FromArgb(255, 0x25, 0x63, 0xEB), Offset = 1 } } };
        g.Children.Add(new Microsoft.UI.Xaml.Shapes.Ellipse { Fill = water, Stroke = Alpha(Blue, .45), StrokeThickness = 2.5 });
        g.Loaded += (_, _) => { var to = 1 - fill; var sb = new Storyboard(); sb.Children.Add(Fx.Anim(s1, "Offset", 1, to, 1300, 250, dependent: true)); sb.Children.Add(Fx.Anim(s2, "Offset", 1, to, 1300, 250, dependent: true)); sb.Completed += (_, _) => { s1.Offset = to; s2.Offset = to; }; sb.Begin(); };
        var onWater = !Day || fill > .55;
        var big = Big(Js.ToFixed(Math.Max(0, pct), 0) + "%", onWater ? White : Text, size / 6); var lab = T(label, 11, false, onWater ? Br(0xD9FFFFFFu) : Mut, false);
        var t = V(0, big, lab); t.HorizontalAlignment = HorizontalAlignment.Center; t.VerticalAlignment = VerticalAlignment.Center;
        foreach (var c in t.Children) ((FrameworkElement)c).HorizontalAlignment = HorizontalAlignment.Center;
        g.Children.Add(t); return g;
    }
    /// <summary>Anillo de cobro del inicio (se dibuja en 1,1 s).</summary>
    public static Grid Ring(double pct, string label = "cobrado", double size = 100)
    {
        pct = double.IsNaN(pct) ? 0 : Math.Clamp(pct, 0, 100);
        var g = new Grid { Width = size, Height = size };
        var ring = new ProgressRing { IsIndeterminate = false, Minimum = 0, Maximum = 100, Value = 0, Width = size, Height = size, Foreground = Blue, Background = Track };
        g.Children.Add(ring);
        ring.Loaded += (_, _) => { var sb = new Storyboard(); sb.Children.Add(Fx.Anim(ring, "Value", 0, pct, 1100, 250, dependent: true)); sb.Completed += (_, _) => ring.Value = pct; sb.Begin(); };
        var t = V(0, Big(Js.ToFixed(pct, 0) + "%", null, size / 4.6), Muted(label, 11)); t.HorizontalAlignment = HorizontalAlignment.Center; t.VerticalAlignment = VerticalAlignment.Center;
        foreach (var c in t.Children) ((FrameworkElement)c).HorizontalAlignment = HorizontalAlignment.Center;
        g.Children.Add(t); return g;
    }
    /// <summary>Gráfica de barras: crecen desde abajo (sGrow).</summary>
    public static Grid Bars(List<(string Label, double Value)> items, Brush? color = null, double height = 140)
    {
        var g = new Grid { ColumnSpacing = 6, Height = height + 36 };
        var mx = Math.Max(1e-9, items.Count == 0 ? 1 : items.Max(i => i.Value));
        for (var i = 0; i < items.Count; i++)
        {
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var (l, v) = items[i];
            var col = new Grid(); col.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); col.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var bar = new Border { Height = Math.Max(2, v / mx * height), VerticalAlignment = VerticalAlignment.Bottom, CornerRadius = new CornerRadius(6, 6, 2, 2),
                Background = v > 0 ? new LinearGradientBrush { StartPoint = new Point(.5, 0), EndPoint = new Point(.5, 1), GradientStops = { new GradientStop { Color = Color.FromArgb(255, 0x60, 0xA5, 0xFA), Offset = 0 }, new GradientStop { Color = ((SolidColorBrush)(color ?? Blue)).Color, Offset = 1 } } } : Br(0x2894A3B8u) };
            var ct = new CompositeTransform { ScaleY = 0 }; bar.RenderTransform = ct; bar.RenderTransformOrigin = new Point(.5, 1);
            var delay = 200 + i * 40; bar.Loaded += (_, _) => { var sb = new Storyboard(); sb.Children.Add(Fx.Anim(ct, "ScaleY", 0, 1, 800, delay)); sb.Completed += (_, _) => ct.ScaleY = 1; sb.Begin(); };
            ToolTipService.SetToolTip(bar, Js.Money(v));
            col.Children.Add(bar);
            var lab = Muted(l, 11); lab.HorizontalAlignment = HorizontalAlignment.Center; lab.TextWrapping = TextWrapping.NoWrap; Grid.SetRow(lab, 1); col.Children.Add(lab);
            Grid.SetColumn(col, i); g.Children.Add(col);
        }
        return g;
    }
    /// <summary>Inicial del cliente en recuadro (como en la web).</summary>
    public static Border Avatar(string name, Brush? color = null, double size = 40)
    {
        var ini = name.Trim().Length == 0 ? "?" : name.Trim()[..1].ToUpperInvariant();
        var t = T(ini, size * 0.42, true, Text, false); t.FontWeight = FontWeights.ExtraBold; t.HorizontalAlignment = HorizontalAlignment.Center; t.VerticalAlignment = VerticalAlignment.Center;
        var rose = color != null && ReferenceEquals(color, Rose);
        return new Border { Width = size, Height = size, CornerRadius = new CornerRadius(size * .3), Background = Br(Day ? 0x0F0F172Au : 0x1A94A3B8u), BorderBrush = rose ? Alpha(Rose, .35) : Line2, BorderThickness = new Thickness(1), Child = t };
    }
    /// <summary>Icono en recuadro (.ic) de las tarjetas de la web.</summary>
    public static Border IconTile(string glyph, double size = 40) =>
        new() { Width = size, Height = size, CornerRadius = new CornerRadius(12), Background = Br(Day ? 0x0F0F172Au : 0x1A94A3B8u), BorderBrush = LineBr, BorderThickness = new Thickness(1),
            Child = new FontIcon { Glyph = glyph, FontSize = size * .45, Foreground = Br(Day ? 0xFF334155u : 0xFFCBD5E1u) } };
    public static Border Dot(Brush color, double size = 9) => new() { Width = size, Height = size, CornerRadius = new CornerRadius(size / 2), Background = color, VerticalAlignment = VerticalAlignment.Center };
    public static FrameworkElement Empty(string title, string text = "", UIElement? action = null)
    {
        var tt = T(title, 16, true); tt.FontWeight = FontWeights.ExtraBold; tt.TextAlignment = TextAlignment.Center;
        var s = V(8, new FontIcon { Glyph = "", FontSize = 46, Foreground = Br(Day ? 0xFF334155u : 0xFFCBD5E1u) }, tt, text.Length > 0 ? Muted(text, 13) : null, action);
        s.HorizontalAlignment = HorizontalAlignment.Center;
        foreach (var c in s.Children) { ((FrameworkElement)c).HorizontalAlignment = HorizontalAlignment.Center; if (c is TextBlock tb) tb.TextAlignment = TextAlignment.Center; }
        return new Border { Child = s, Padding = new Thickness(18, 26, 18, 26), CornerRadius = new CornerRadius(18), Background = Glass, BorderBrush = LineBr, BorderThickness = new Thickness(1) };
    }
    /// <summary>Buscador (.aSearch): vidrio, radio 16, lupa.</summary>
    public static FrameworkElement Search(string q, string hint, Action<string> onQ)
    {
        var tb = new TextBox { Text = q, PlaceholderText = hint, BorderThickness = new Thickness(0), Background = new SolidColorBrush(Colors.Transparent), FontFamily = Font, FontSize = 15, FontWeight = FontWeights.Medium, Padding = new Thickness(4, 12, 8, 12), VerticalAlignment = VerticalAlignment.Center };
        tb.Resources["TextControlBackgroundFocused"] = new SolidColorBrush(Colors.Transparent); tb.Resources["TextControlBackgroundPointerOver"] = new SolidColorBrush(Colors.Transparent);
        tb.Resources["TextControlBorderBrushFocused"] = new SolidColorBrush(Colors.Transparent);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(tb, hint);
        tb.TextChanged += (_, _) => onQ(tb.Text);
        var g = Lead(new FontIcon { Glyph = "", FontSize = 16, Foreground = Mut, Margin = new Thickness(14, 0, 0, 0) }, tb);
        g.ColumnSpacing = 6;
        var border = new Border { Child = g, CornerRadius = new CornerRadius(16), Background = Glass, BorderBrush = LineBr, BorderThickness = new Thickness(1) };
        tb.GotFocus += (_, _) => border.BorderBrush = Alpha(Blue, .7); tb.LostFocus += (_, _) => border.BorderBrush = LineBr;
        return border;
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
