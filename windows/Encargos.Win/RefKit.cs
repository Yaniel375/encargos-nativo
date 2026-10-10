using Encargos.Core;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Color = Windows.UI.Color;

namespace Encargos.Win;

/// <summary>Piezas de la referencia Encargos-9 (las mismas que en Android: Kit.kt), adaptadas a PC.</summary>
public static partial class K
{
    /// <summary>Botón compacto de las tarjetas (Ficha · Editar · WhatsApp · ⋯).</summary>
    public static Button CardBtn(string? text, string glyph, Action onClick, Brush? tint = null, string? desc = null)
    {
        var ic = new FontIcon { Glyph = glyph, FontSize = 13, Foreground = tint ?? Text };
        object content = text != null ? Hs(7, ic, T(text, 13, true, Text, false)) : ic;
        var b = new Button { Content = content, MinHeight = 38, Padding = new Thickness(10, 6, 10, 6), CornerRadius = new CornerRadius(12), Background = Tile, BorderBrush = Line2, BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center };
        if (BtnStyle != null) b.Style = BtnStyle;
        b.HorizontalContentAlignment = HorizontalAlignment.Center;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(b, desc ?? text ?? "");
        b.Click += Once(onClick); return Fx.Bounce(b, .95);
    }

    /// <summary>Fila con icono, título, subtítulo, valor y flecha (Ajustes, Seguridad, "Encargos completados").</summary>
    public static FrameworkElement NavRow(string glyph, string title, string? sub, Action? onClick, string? value = null, UIElement? right = null, Brush? tint = null, Brush? titleColor = null)
    {
        var tile = IconTile(glyph, 34); if (tint != null) ((FontIcon)tile.Child).Foreground = tint;
        var tt = T(title, 14.5, true, titleColor ?? Text, true, 2); tt.FontWeight = FontWeights.ExtraBold;
        var rest = new List<UIElement?>();
        if (value != null) rest.Add(T(value, 13, true, Mut, false));
        if (right != null) rest.Add(right); else if (onClick != null) rest.Add(new FontIcon { Glyph = "", FontSize = 12, Foreground = Mut });
        var g = Lead(tile, V(1, tt, string.IsNullOrEmpty(sub) ? null : Muted(sub, 12)), rest.ToArray());
        g.Padding = new Thickness(14, 11, 14, 11);
        if (onClick == null) return g;
        var b = Click(g, onClick); b.CornerRadius = new CornerRadius(0);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(b, title);
        return b;
    }
    public static Border RowSep() => new() { Height = 1, Background = LineBr, Margin = new Thickness(14, 0, 14, 0) };
    /// <summary>Grupo de filas con título pequeño encima (Negocio y tarifas, WhatsApp…).</summary>
    public static StackPanel Group(string? title, params UIElement?[] rows)
    {
        var inner = new StackPanel();
        var first = true;
        foreach (var r in rows) { if (r == null) continue; if (!first) inner.Children.Add(RowSep()); inner.Children.Add(r); first = false; }
        var box = new Border { Child = inner, CornerRadius = new CornerRadius(18), Background = Glass, BorderBrush = LineBr, BorderThickness = new Thickness(1) };
        var lab = title != null ? T(title, 12.5, true, Mut, false) : null; if (lab != null) lab.Margin = new Thickness(4, 0, 0, 0);
        return V(8, lab, box);
    }
    /// <summary>Fila de totales de arriba (Total · Cobrado · Pendiente).</summary>
    public static Grid TopTiles(params (string L, string V, Brush? C)[] t) => Cols(t.Select(x => (UIElement)Kpi(x.L, x.V, x.C)).ToArray());
    /// <summary>Tarjeta de acción grande; la principal va en degradado azul.</summary>
    public static Border ActionCard(string glyph, string title, string sub, Action onClick, bool primary = false)
    {
        var ic = new Border { Width = 40, Height = 40, CornerRadius = new CornerRadius(12), Background = primary ? Br(0x2EFFFFFFu) : Tile, BorderBrush = primary ? Br(0x40FFFFFFu) : LineBr, BorderThickness = new Thickness(1),
            Child = new FontIcon { Glyph = glyph, FontSize = 18, Foreground = primary ? White : Br(Day ? 0xFF334155u : 0xFFCBD5E1u) } };
        var tt = T(title, 15.5, true, primary ? White : Text); tt.FontWeight = FontWeights.ExtraBold;
        var g = Lead(ic, V(2, tt, T(sub, 12.5, false, primary ? Br(0xD9FFFFFFu) : Mut)));
        var b = Click(g, onClick); b.Padding = new Thickness(16); b.CornerRadius = new CornerRadius(18);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(b, title);
        return new Border { Child = b, CornerRadius = new CornerRadius(18), Background = primary ? PrimaryBrush : Glass, BorderBrush = primary ? null : LineBr, BorderThickness = new Thickness(primary ? 0 : 1) };
    }
    /// <summary>Fila "dato — valor" con separador fino (estado de Seguridad).</summary>
    public static StackPanel StatRow(string label, string value, Brush? color = null) =>
        V(8, new Border { Height = 1, Background = LineBr }, Row(T(label, 13.5, false, Mut), T(value, 13.5, true, color ?? Text, false)));
    /// <summary>Encabezado con punto de color y total a la derecha.</summary>
    public static Grid DotHeader(string title, Brush color, string? right = null)
    {
        var t = T(title, 15, true); t.FontWeight = FontWeights.ExtraBold;
        return Lead(Dot(color, 8), t, right != null ? T(right, 12.5, false, Mut, false) : null);
    }
    /// <summary>Tarjeta de paso numerado (Nuevo encargo).</summary>
    public static Border StepCard(int n, string glyph, string title, string sub, bool done, params UIElement?[] children)
    {
        var badgeT = done ? (UIElement)new FontIcon { Glyph = "", FontSize = 11, Foreground = White } : T(n.ToString(), 11, true, Mut, false);
        ((FrameworkElement)badgeT).HorizontalAlignment = HorizontalAlignment.Center; ((FrameworkElement)badgeT).VerticalAlignment = VerticalAlignment.Center;
        var badge = new Border { Width = 22, Height = 22, CornerRadius = new CornerRadius(11), Background = done ? Green : Tile, BorderBrush = done ? Green : Line2, BorderThickness = new Thickness(1), Child = badgeT };
        var tt = T(title, 15.5, true); tt.FontWeight = FontWeights.ExtraBold;
        var head = Lead(IconTile(glyph, 36), V(1, tt, Muted(sub, 12)), badge);
        var all = new List<UIElement?> { head }; all.AddRange(children);
        return Card(all.ToArray());
    }
    /// <summary>Etapas del pedido en círculos unidos por una línea.</summary>
    public static Grid Stepper(string[] flow, string current)
    {
        var idx = Array.IndexOf(flow, current);
        var g = new Grid();
        for (var i = 0; i < flow.Length; i++)
        {
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var s = flow[i]; var c = (SolidColorBrush)Status(s); var on = i <= idx;
            var lineRow = new Grid { Height = 28 };
            lineRow.ColumnDefinitions.Add(new ColumnDefinition()); lineRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); lineRow.ColumnDefinitions.Add(new ColumnDefinition());
            var l1 = new Border { Height = 2, Background = i == 0 ? null : (on ? Alpha(c, .6) : LineBr) }; lineRow.Children.Add(l1);
            var l2 = new Border { Height = 2, Background = i == flow.Length - 1 ? null : (i < idx ? Alpha((SolidColorBrush)Status(flow[i + 1]), .6) : LineBr) }; Grid.SetColumn(l2, 2); lineRow.Children.Add(l2);
            UIElement inner = i < idx ? new FontIcon { Glyph = "", FontSize = 12, Foreground = White } : T((i + 1).ToString(), 11, true, on ? c : Mut, false);
            ((FrameworkElement)inner).HorizontalAlignment = HorizontalAlignment.Center; ((FrameworkElement)inner).VerticalAlignment = VerticalAlignment.Center;
            var circle = new Border { Width = 26, Height = 26, CornerRadius = new CornerRadius(13), Background = i < idx ? c : i == idx ? Alpha(c, .22) : Tile, BorderBrush = on ? c : Line2, BorderThickness = new Thickness(i == idx ? 2 : 1), Child = inner };
            Grid.SetColumn(circle, 1); lineRow.Children.Add(circle);
            var lab = T(s, 11.5, true, on ? Text : Mut, false); lab.HorizontalAlignment = HorizontalAlignment.Center;
            var col = V(5, lineRow, lab); Grid.SetColumn(col, i); g.Children.Add(col);
        }
        return g;
    }
    /// <summary>Tarjeta del total con degradado azul-violeta (final de Nuevo encargo).</summary>
    public static Border TotalCard(string total, params UIElement?[] rows)
    {
        var a = Day ? (byte)26 : (byte)56;
        var bg = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1), GradientStops = {
            new GradientStop { Color = Color.FromArgb(a, Blue.Color.R, Blue.Color.G, Blue.Color.B), Offset = 0 }, new GradientStop { Color = Color.FromArgb(a, Vio.Color.R, Vio.Color.G, Vio.Color.B), Offset = 1 } } };
        var all = new List<UIElement?> { Muted("Total del encargo", 12.5), Big(total, null, 30), new Border { Height = 1, Background = LineBr } }; all.AddRange(rows);
        return new Border { Child = V(8, all.ToArray()), Padding = new Thickness(18), CornerRadius = new CornerRadius(20), Background = bg, BorderBrush = Alpha(Blue, .45), BorderThickness = new Thickness(1) };
    }
    /// <summary>Recuadro informativo azul de las hojas de ajustes.</summary>
    public static Border InfoBox(string text) => new() { Child = T(text, 13), Padding = new Thickness(12), CornerRadius = new CornerRadius(14), Background = Alpha(Blue, .10), BorderBrush = Alpha(Blue, .45), BorderThickness = new Thickness(1) };
    /// <summary>Interruptor suelto para el lado derecho de una fila.</summary>
    public static ToggleSwitch Switch(bool value, Action<bool> onChange)
    {
        var sw = new ToggleSwitch { IsOn = value, OnContent = "", OffContent = "", MinWidth = 0 };
        sw.Toggled += (_, _) => { if (sw.IsOn != value) { value = sw.IsOn; onChange(sw.IsOn); } };
        return sw;
    }
    /// <summary>Tarjeta "Encargos completados" al final de las listas.</summary>
    public static Border CompletedLink(int n, Action onClick)
    {
        var r = NavRow("", "Encargos completados", $"{n} {(n == 1 ? "cliente" : "clientes")} con todo pagado", onClick, tint: Green);
        return new Border { Child = r, CornerRadius = new CornerRadius(18), Background = Glass, BorderBrush = LineBr, BorderThickness = new Thickness(1) };
    }
    /// <summary>Reparte tarjetas en columnas según el ancho (2 en PC normal, 1 en ventana estrecha).</summary>
    public static Grid Grid2(IList<UIElement> items, int cols = 2)
    {
        var grid = new Grid { ColumnSpacing = 12, RowSpacing = 12 };
        for (var c = 0; c < cols; c++) grid.ColumnDefinitions.Add(new ColumnDefinition());
        for (var i = 0; i < items.Count; i++)
        {
            if (i % cols == 0) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var e = (FrameworkElement)items[i]; e.VerticalAlignment = VerticalAlignment.Top; Grid.SetRow(e, i / cols); Grid.SetColumn(e, i % cols); grid.Children.Add(e);
        }
        return grid;
    }
}
