using System.Runtime.InteropServices.WindowsRuntime;
using Encargos.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using static Encargos.Win.K;

namespace Encargos.Win;

/// <summary>Fotos (data URL en base64, igual que la web), archivos y recibos.</summary>
public static class Media
{
    static readonly Dictionary<string, BitmapImage> _cache = new(); static readonly Queue<string> _order = new();

    static byte[]? Bytes(string? dataUrl)
    {
        if (string.IsNullOrEmpty(dataUrl)) return null;
        var i = dataUrl.IndexOf(','); try { return Convert.FromBase64String(i >= 0 ? dataUrl[(i + 1)..] : dataUrl); } catch { return null; }
    }
    static async Task<BitmapImage?> Load(string dataUrl, int width)
    {
        var key = width + ":" + dataUrl.Length + ":" + dataUrl.GetHashCode();
        if (_cache.TryGetValue(key, out var hit)) return hit;
        var b = Bytes(dataUrl); if (b == null) return null;
        var bmp = new BitmapImage { DecodePixelWidth = width };
        using var ms = new InMemoryRandomAccessStream(); await ms.WriteAsync(b.AsBuffer()); ms.Seek(0);
        try { await bmp.SetSourceAsync(ms); } catch { return null; }
        _cache[key] = bmp; _order.Enqueue(key); while (_order.Count > 120) _cache.Remove(_order.Dequeue());
        return bmp;
    }

    /* ---------- foto de perfil del administrador (solo en este equipo) ---------- */
    public const string ProfileKey = "encargos_profile_photo";
    public static string ProfilePhoto => Services.Core.Ls.Get(ProfileKey, "");
    /// <summary>Foto de perfil recortada al recuadro (o la inicial si no hay foto).</summary>
    public static FrameworkElement ProfilePic(string name, double size, Brush? border = null)
    {
        var p = ProfilePhoto; if (p.Length == 0) return Avatar(name, Blue, size);
        var b = new Border { Width = size, Height = size, CornerRadius = new CornerRadius(size * .3), Background = Alpha(Mut, 0.12), BorderBrush = border ?? Line2, BorderThickness = new Thickness(1) };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(b, "Foto de perfil");
        _ = FillProfile(b, p); return b;
    }
    /// <summary>Foto de perfil en círculo con aro (pantalla de acceso).</summary>
    public static Border ProfileCircle(double size, Brush ring)
    {
        var inner = new Border { CornerRadius = new CornerRadius((size - 10) / 2), Background = Alpha(Mut, 0.12) };
        _ = FillProfile(inner, ProfilePhoto);
        return new Border { Width = size, Height = size, CornerRadius = new CornerRadius(size / 2), BorderBrush = ring, BorderThickness = new Thickness(2), Padding = new Thickness(3), Child = inner, HorizontalAlignment = HorizontalAlignment.Center };
    }
    static async Task FillProfile(Border b, string p) { var bmp = await Load(p, 256); if (bmp != null) b.Background = new ImageBrush { ImageSource = bmp, Stretch = Stretch.UniformToFill }; }
    /// <summary>Elige una foto, la reduce a 400 px y la guarda como foto de perfil.</summary>
    public static async Task<bool> PickProfile()
    {
        var d = await PickPhoto(400, 0.8); if (d == null) return false;
        Services.Core.Ls.Set(ProfileKey, d); return true;
    }

    /// <summary>Foto a partir de un data URL. Se ve SIEMPRE entera (sin recortes ni deformaciones) dentro de su recuadro.</summary>
    public static Border Photo(string dataUrl, double w, double h, Action? onClick = null, int decode = 400)
    {
        var img = new Image { Stretch = Stretch.Uniform };
        var b = new Border { Width = w, Height = h, CornerRadius = new CornerRadius(10), Background = Alpha(Mut, 0.12), BorderBrush = LineBr, BorderThickness = new Thickness(1), Child = img };
        if (double.IsNaN(w)) b.Width = double.NaN;
        _ = SetAsync(img, dataUrl, decode);
        if (onClick != null) { b.Tapped += (_, e) => { e.Handled = true; onClick(); }; b.PointerEntered += (_, _) => b.BorderBrush = Alpha(Blue, .6); b.PointerExited += (_, _) => b.BorderBrush = LineBr; }
        return b;
    }

    /// <summary>Miniatura que muestra la foto entera: misma altura y ancho según su proporción real. Tocar abre el visor.</summary>
    public static Border Thumb(string dataUrl, double h, int index, int count, Action onClick)
    {
        var img = new Image { Stretch = Stretch.Uniform };
        var g = new Grid(); g.Children.Add(img);
        if (count > 1) g.Children.Add(new Border { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(5), CornerRadius = new CornerRadius(6), Padding = new Thickness(5, 1, 5, 2), Background = Br(0x8C000000u), Child = T($"{index + 1}/{count}", 10, true, White, false) });
        g.Children.Add(new Border { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(5), Width = 22, Height = 22, CornerRadius = new CornerRadius(11), Background = Br(0x8C000000u), Child = new FontIcon { Glyph = "\uE71E", FontSize = 11, Foreground = White } });
        var b = new Border { Width = h, Height = h, CornerRadius = new CornerRadius(12), Background = Alpha(Mut, 0.12), BorderBrush = LineBr, BorderThickness = new Thickness(1), Child = g };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(b, $"Foto {index + 1} de {count}");
        img.ImageOpened += (_, _) => { if (img.Source is BitmapImage bi && bi.PixelHeight > 0) b.Width = h * Math.Clamp((double)bi.PixelWidth / bi.PixelHeight, 0.35, 3); };
        _ = SetAsync(img, dataUrl, 500);
        b.Tapped += (_, e) => { e.Handled = true; onClick(); };
        b.PointerEntered += (_, _) => b.BorderBrush = Alpha(Blue, .6); b.PointerExited += (_, _) => b.BorderBrush = LineBr;
        return b;
    }
    /// <summary>Fila de miniaturas completas con desplazamiento horizontal.</summary>
    public static FrameworkElement Strip(List<string> photos, double h, Action<int> onOpen)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        for (var i = 0; i < photos.Count; i++) { var idx = i; row.Children.Add(Thumb(photos[i], h, i, photos.Count, () => onOpen(idx))); }
        return new ScrollViewer { Content = row, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, HorizontalScrollMode = ScrollMode.Enabled, VerticalScrollMode = ScrollMode.Disabled, Padding = new Thickness(0, 0, 0, 6) };
    }

    static async Task SetAsync(Image img, string dataUrl, int decode) { var s = await Load(dataUrl, decode); if (s != null) img.Source = s; }

    /// <summary>Elegir una foto y comprimirla (JPEG, lado mayor maxDim) como data URL.</summary>
    public static async Task<string?> PickPhoto(int maxDim = 1000, double quality = 0.6)
    {
        var p = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.PicturesLibrary, ViewMode = PickerViewMode.Thumbnail };
        foreach (var e in new[] { ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif", ".heic" }) p.FileTypeFilter.Add(e);
        WinRT.Interop.InitializeWithWindow.Initialize(p, MainWindow.I.Hwnd);
        var f = await p.PickSingleFileAsync(); if (f == null) return null;
        try
        {
            using var src = await f.OpenReadAsync();
            var dec = await BitmapDecoder.CreateAsync(src);
            var w = dec.OrientedPixelWidth; var h = dec.OrientedPixelHeight; var k = Math.Min(1.0, (double)maxDim / Math.Max(w, h));
            using var outS = new InMemoryRandomAccessStream();
            var props = new BitmapPropertySet { { "ImageQuality", new BitmapTypedValue((float)quality, Windows.Foundation.PropertyType.Single) } };
            var enc = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, outS, props);
            var px = await dec.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, new BitmapTransform { ScaledWidth = (uint)Math.Max(1, w * k), ScaledHeight = (uint)Math.Max(1, h * k), InterpolationMode = BitmapInterpolationMode.Fant },
                ExifOrientationMode.RespectExifOrientation, ColorManagementMode.ColorManageToSRgb);
            enc.SetSoftwareBitmap(px); await enc.FlushAsync();
            var bytes = await ReadAll(outS);
            return "data:image/jpeg;base64," + Convert.ToBase64String(bytes);
        }
        catch (Exception e) { Services.Report(e); MainWindow.I.Toast("No se pudo leer esa imagen."); return null; }
    }

    /// <summary>Visor a pantalla completa: cada foto entera; se amplía con Ctrl + rueda, pellizcando o con doble clic,
    /// y se pasa de una a otra con las flechas o deslizando. "Eliminar" solo aparece donde ya existía esa acción.</summary>
    public static void Viewer(List<string> list, int start, Action<int>? onDelete)
    {
        if (list.Count == 0) return;
        var S = MainWindow.I;
        var flip = new FlipView { Background = null, SelectedIndex = -1 };
        var counter = T("", 15, true, White, false); counter.FontWeight = Microsoft.UI.Text.FontWeights.ExtraBold;
        foreach (var src in list)
        {
            // la foto se centra dentro de un lienzo del tamaño exacto del área visible: se ve entera; al ampliar se recorre
            var img = new Image { Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var frame = new Grid { Children = { img } };
            var sv = new ScrollViewer { ZoomMode = ZoomMode.Enabled, MinZoomFactor = 1, MaxZoomFactor = 5, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
                HorizontalScrollMode = ScrollMode.Auto, VerticalScrollMode = ScrollMode.Auto, Content = frame, Padding = new Thickness(0) };
            sv.SizeChanged += (_, e) => { frame.Width = e.NewSize.Width; frame.Height = e.NewSize.Height; };
            sv.DoubleTapped += (_, e) => { var p = e.GetPosition(img); if (sv.ZoomFactor > 1.01f) sv.ChangeView(0, 0, 1); else sv.ChangeView(p.X * 1.5, p.Y * 1.5, 2.5f); };
            _ = SetAsync(img, src, 1800);
            flip.Items.Add(sv);
        }
        void Count() => counter.Text = list.Count > 1 ? $"Foto {flip.SelectedIndex + 1} de {list.Count}" : "Foto";
        flip.SelectionChanged += (_, _) => { Count(); foreach (var it in flip.Items) if (it is ScrollViewer z && z != flip.SelectedItem) z.ChangeView(0, 0, 1, true); };
        var close = Btn("Cerrar", () => S.HideOverlay(), false, White, "\uE711", false);
        var top = Row(counter, close); top.Margin = new Thickness(18, 14, 18, 8);
        var hint = Muted("Ctrl + rueda, pellizca o doble clic para ampliar · flechas para cambiar de foto", 12); hint.Foreground = Br(0xB3FFFFFFu); hint.TextAlignment = TextAlignment.Center;
        var del = onDelete != null ? Btn("Eliminar", () => { var i = flip.SelectedIndex; S.HideOverlay(); onDelete(i); }, false, Rose, "\uE74D", false) : null;
        var bottom = Row(hint, del); bottom.Margin = new Thickness(18, 8, 18, 14);
        var g = new Grid { Background = Br(0xF2020611u) };
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        g.Children.Add(top); Grid.SetRow(flip, 1); g.Children.Add(flip); Grid.SetRow(bottom, 2); g.Children.Add(bottom);
        g.KeyDown += (_, e) => { if (e.Key == Windows.System.VirtualKey.Escape) S.HideOverlay(); };
        S.ShowOverlay(g);
        flip.SelectedIndex = Math.Clamp(start, 0, list.Count - 1); Count();
        flip.Focus(FocusState.Programmatic);
    }

    /* ---------- archivos ---------- */
    public static async Task<bool> SaveFile(string suggestedName, string ext, string label, byte[] bytes)
    {
        var p = new FileSavePicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary, SuggestedFileName = Path.GetFileNameWithoutExtension(suggestedName) };
        p.FileTypeChoices.Add(label, new List<string> { ext });
        WinRT.Interop.InitializeWithWindow.Initialize(p, MainWindow.I.Hwnd);
        var f = await p.PickSaveFileAsync(); if (f == null) return false;
        try { await FileIO.WriteBytesAsync(f, bytes); return true; } catch (Exception e) { Services.Report(e); return false; }
    }
    public static async Task<string?> OpenText(params string[] exts)
    {
        var p = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
        foreach (var e in exts) p.FileTypeFilter.Add(e);
        WinRT.Interop.InitializeWithWindow.Initialize(p, MainWindow.I.Hwnd);
        var f = await p.PickSingleFileAsync(); if (f == null) return null;
        try { var b = (await FileIO.ReadBufferAsync(f)).ToArray(); return Crypto.FromUtf8(b); } catch { return null; }
    }

    /* ---------- recibo ---------- */
    /// <summary>Recibo con el mismo contenido que el de Android (fondo blanco, listo para enviar).</summary>
    /// <summary>Recibo con el mismo estilo que la app (modo noche aurora): fondo, logo, Manrope, tarjetas de cristal, ficha de saldo,
    /// etiqueta de estado y recuadros de cifras como en la ficha del encargo. Mismo diseño que el recibo de Android; las cifras no cambian.</summary>
    public static Border ReceiptView(EncargosApp core, JObj x)
    {
        // colores fijos del modo noche (el recibo se ve igual aunque la app esté en modo día)
        SolidColorBrush C(uint v) => new(Microsoft.UI.ColorHelper.FromArgb((byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v));
        var tx = C(0xFFF1F5F9u); var mut = C(0xFF9AA6B8u); var blue = C(0xFF3B82F6u); var blueInk = C(0xFF93C5FDu); var green = C(0xFF34D399u); var rose = C(0xFFFB7185u);
        var line = C(0x2494A3B8u); var line2 = C(0x3D94A3B8u); var tile = C(0x1494A3B8u); var track = C(0x2894A3B8u);
        SolidColorBrush A(SolidColorBrush c, double a) => new(Windows.UI.Color.FromArgb((byte)(a * 255), c.Color.R, c.Color.G, c.Color.B));
        Brush GlassB() => new LinearGradientBrush { StartPoint = new Windows.Foundation.Point(0, 0), EndPoint = new Windows.Foundation.Point(1, 1),
            GradientStops = { new GradientStop { Color = Windows.UI.Color.FromArgb(158, 30, 41, 59), Offset = 0 }, new GradientStop { Color = Windows.UI.Color.FromArgb(184, 15, 23, 42), Offset = 1 } } };
        TextBlock Tx(string s2, double size, Brush c, string w = "r", bool trim = false)
        {
            var t = new TextBlock { Text = s2, FontSize = size, Foreground = c, FontFamily = Font, TextWrapping = trim ? TextWrapping.NoWrap : TextWrapping.Wrap, TextTrimming = trim ? TextTrimming.CharacterEllipsis : TextTrimming.None,
                FontWeight = w switch { "xb" => Microsoft.UI.Text.FontWeights.ExtraBold, "b" => Microsoft.UI.Text.FontWeights.Bold, "s" => Microsoft.UI.Text.FontWeights.SemiBold, _ => Microsoft.UI.Text.FontWeights.Normal } };
            return t;
        }
        Border Glass(UIElement child, double pad = 18) => new() { Background = GlassB(), BorderBrush = line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(18), Padding = new Thickness(pad), Child = child };
        Grid Rw(UIElement a, UIElement b) { var g = new Grid(); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); g.Children.Add(a); Grid.SetColumn((FrameworkElement)b, 1); g.Children.Add(b); return g; }

        double b = Js.Num(x["balance"]), tot = Js.Num(x["total"]), paid = Js.Num(x["paid"]); var st = x.Str("status").IfEmpty("Pedido");
        var stc = st switch { "En tránsito" => C(0xFF94A3B8u), "Recibido" => C(0xFF7DD3FCu), "Entregado" => green, "Cancelado" => C(0xFF64748Bu), _ => blue };
        var owes = b > 0.005; var tone = owes ? rose : green;
        var biz = core.BusinessName.IfEmpty("Encargos");

        // ---- barra superior: logo, negocio y "Recibo" ----
        var logo = new Image { Width = 34, Height = 34, Stretch = Stretch.UniformToFill };
        try { logo.Source = new BitmapImage(new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", "logo.png"))); } catch { }
        var bar = new Grid { ColumnSpacing = 9 };
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bar.Children.Add(new Border { CornerRadius = new CornerRadius(10), Child = logo, VerticalAlignment = VerticalAlignment.Center });
        var bizT = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { Tx(biz, 17, tx, "xb", true), Tx("Tu negocio, en orden", 11, mut) } }; Grid.SetColumn(bizT, 1); bar.Children.Add(bizT);
        var chip = new Border { CornerRadius = new CornerRadius(14), Background = A(blue, .14), BorderBrush = A(blue, .38), BorderThickness = new Thickness(1), Padding = new Thickness(12, 4, 12, 5), VerticalAlignment = VerticalAlignment.Center, Child = Tx("Recibo", 12, blueInk, "b") };
        Grid.SetColumn(chip, 2); bar.Children.Add(chip);
        var barB = new Border { Background = C(0xC7090D18u), BorderBrush = line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(20), Padding = new Thickness(14, 12, 14, 12), Child = bar };

        // ---- ficha como la cabecera de la ficha del encargo ----
        var av = new Border { Width = 60, Height = 60, CornerRadius = new CornerRadius(18), Background = tile, BorderBrush = owes ? A(rose, .35) : line2, BorderThickness = new Thickness(1),
            Child = new TextBlock { Text = (x.Str("name").Trim() + "?")[..1].ToUpperInvariant(), FontFamily = Font, FontSize = 25, FontWeight = Microsoft.UI.Text.FontWeights.ExtraBold, Foreground = tx, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
        var stChip = new Border { CornerRadius = new CornerRadius(13), Background = A(stc, .14), BorderBrush = A(stc, .38), BorderThickness = new Thickness(1), Padding = new Thickness(10, 4, 11, 5), VerticalAlignment = VerticalAlignment.Top,
            Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new Border { Width = 6, Height = 6, CornerRadius = new CornerRadius(3), Background = stc, VerticalAlignment = VerticalAlignment.Center }, Tx(st, 11.5, stc, "b") } } };
        // el nombre usa todo el ancho; la etiqueta de estado va a la derecha de los datos de contacto
        var nameT = Tx(x.Str("name").IfEmpty("Cliente"), 21, tx, "xb", true); nameT.Margin = new Thickness(14, 0, 0, 1);
        var info = new StackPanel { Spacing = 1, Margin = new Thickness(14, 0, 8, 0), Children = {
            Tx("Tel. " + x.Str("phone").IfEmpty("sin teléfono"), 13, mut, "r", true),
            Tx(core.ShippingName(core.TypeOf(x)) + " · " + Js.ToFixed(Js.Num(x["pounds"]), 2) + " lb", 13, mut, "r", true), Tx("Encargo del " + Js.Fmt(x["date"]).Split(',')[0], 12, mut) } };
        var head = new Grid(); head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        head.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); head.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRowSpan(av, 2); head.Children.Add(av);
        Grid.SetColumn(nameT, 1); Grid.SetColumnSpan(nameT, 2); head.Children.Add(nameT);
        Grid.SetColumn(info, 1); Grid.SetRow(info, 1); head.Children.Add(info);
        Grid.SetColumn(stChip, 2); Grid.SetRow(stChip, 1); head.Children.Add(stChip);
        var k = tot > 0 ? Math.Clamp(paid / tot, 0, 1) : 1;
        var barTrack = new Grid { Height = 6 }; barTrack.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(k, 0.0001), GridUnitType.Star) }); barTrack.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(1 - k, 0.0001), GridUnitType.Star) });
        barTrack.Children.Add(new Border { CornerRadius = new CornerRadius(3), Background = track }); Grid.SetColumnSpan((FrameworkElement)barTrack.Children[0], 2);
        if (k > 0) barTrack.Children.Add(new Border { CornerRadius = new CornerRadius(3), Background = new LinearGradientBrush { StartPoint = new Windows.Foundation.Point(0, 0), EndPoint = new Windows.Foundation.Point(1, 0), GradientStops = { new GradientStop { Color = A(green, .75).Color, Offset = 0 }, new GradientStop { Color = green.Color, Offset = 1 } } } });
        var totCol = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right, Children = { Tx("Total", 12, mut), Tx(Js.Money(tot), 17, tx, "xb") } };
        foreach (var ch in totCol.Children) ((FrameworkElement)ch).HorizontalAlignment = HorizontalAlignment.Right;
        var hero = new Border { CornerRadius = new CornerRadius(16), Background = A(tone, .08), BorderBrush = A(tone, .30), BorderThickness = new Thickness(1), Padding = new Thickness(14), Margin = new Thickness(0, 12, 0, 0),
            Child = new StackPanel { Spacing = 8, Children = {
                Rw(new StackPanel { Children = { Tx(owes ? "Pendiente por cobrar" : b < -0.005 ? "Saldo a favor" : "Pagado completo", 12.5, mut), Tx(owes ? "- " + Js.Money(b) : b < -0.005 ? "+ " + Js.Money(-b) : Js.Money(0), 26, tone, "xb") } }, totCol),
                barTrack, Tx($"Pagó {Js.Money(paid)} de {Js.Money(tot)}" + (tot > 0 ? $" · {Math.Round(k * 100)} %" : ""), 12.5, mut) } } };
        Border Kp(string l, string v, SolidColorBrush? col, string? sub)
        {
            var sp = new StackPanel { Children = { Tx(l, 11.5, mut, "s"), Tx(v, 17, (Brush?)col ?? tx, "xb", true) } }; if (sub != null) sp.Children.Add(Tx(sub, 11, mut, "r", true));
            return new Border { CornerRadius = new CornerRadius(12), Background = col != null ? A(col, .08) : tile, BorderBrush = col != null ? A(col, .30) : line, BorderThickness = new Thickness(1), Padding = new Thickness(11, 9, 11, 9), Child = sp };
        }
        var kpis = new Grid { ColumnSpacing = 8, Margin = new Thickness(0, 10, 0, 0) };
        for (var i = 0; i < 3; i++) kpis.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var ks = new[] { Kp("Pedido", Js.Money(x["cost"]), null, null), Kp("Envío", Js.Money(x["shipping"]), null, Js.ToFixed(Js.Num(x["pounds"]), 2) + " × " + Js.Money(x["lbCost"])), Kp("Pagado", Js.Money(paid), green, null) };
        for (var i = 0; i < 3; i++) { Grid.SetColumn(ks[i], i); kpis.Children.Add(ks[i]); }
        var card = Glass(new StackPanel { Children = { head, hero, kpis } });

        // ---- secciones con título y filas separadas ----
        Border Section(string title, List<(string L, string V, Brush C)> rows)
        {
            var sp = new StackPanel();
            sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 0, 0, 6), Children = { new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), Background = blue, VerticalAlignment = VerticalAlignment.Center }, Tx(title, 16, tx, "xb") } });
            for (var i = 0; i < rows.Count; i++)
            {
                var last = i == rows.Count - 1;
                if (i > 0) sp.Children.Add(new Border { Height = 1, Background = line });
                var r = Rw(Tx(rows[i].L, 14, last ? tx : mut, last ? "b" : "r"), Tx(rows[i].V, 14, rows[i].C, last ? "xb" : "s")); r.Padding = new Thickness(0, 9, 0, 9); ((FrameworkElement)r.Children[0]).Margin = new Thickness(0, 0, 12, 0);
                sp.Children.Add(r);
            }
            return Glass(sp, 18);
        }
        var det = Section("Detalle del pedido", new() { ("Costo del pedido", Js.Money(x["cost"]), tx),
            ($"Envío {core.ShippingName(core.TypeOf(x))} · {Js.ToFixed(Js.Num(x["pounds"]), 2)} lb × {Js.Money(x["lbCost"])}", Js.Money(x["shipping"]), tx), ("Total", Js.Money(tot), tx) });
        var pays = ((x["paymentHistory"] as JArr)?.Objs() ?? new()).OrderBy(h => { var v = Js.ParseDate(h["date"]); return double.IsNaN(v) ? 0 : v; }).ToList();
        var prow = pays.Count > 0 ? pays.Select(h => (Js.Fmt(h["date"]) + " · " + h.Str("method").IfEmpty("Efectivo"), "+" + Js.Money(h["amount"]), (Brush)green)).ToList() : new() { ("Sin pagos registrados", "—", mut) };
        prow.Add(("Total pagado", Js.Money(paid), green));
        var pag = Section("Pagos", prow);
        var foot = new StackPanel { Spacing = 4, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 4), Children = {
            new Border { Width = 22, Height = 3, CornerRadius = new CornerRadius(2), Background = blue, HorizontalAlignment = HorizontalAlignment.Center },
            Tx("Gracias por tu confianza", 13, mut, "s"), Tx(biz + "  •  Tu negocio, en orden", 11, C(0xFF7C8BA5u)) } };
        foreach (var ch in foot.Children) ((FrameworkElement)ch).HorizontalAlignment = HorizontalAlignment.Center;
        var code = x.Str("accessCode"); if (code.Length > 0) foot.Children.Add(new TextBlock { Text = "Código de cliente: " + code, FontFamily = Font, FontSize = 11, Foreground = C(0xFF7C8BA5u), HorizontalAlignment = HorizontalAlignment.Center });

        var page = new StackPanel { Spacing = 12, Children = { barB,
            new StackPanel { Margin = new Thickness(2, 6, 0, 0), Children = { Tx("Recibo de compra", 24, tx, "xb"), Tx(Js.Fmt(x["date"]), 13, mut) } },
            card, det, pag, foot } };
        // fondo aurora de la app (con una capa oscura encima, como en la app)
        var bg = new Grid { Background = C(0xFF020611u) };
        try { bg.Children.Add(new Border { Background = new ImageBrush { ImageSource = new BitmapImage(new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", "aurora_bg.jpg"))), Stretch = Stretch.UniformToFill } }); } catch { }
        bg.Children.Add(new Border { Background = C(0x5A020611u) });
        bg.Children.Add(new Border { Padding = new Thickness(16), Child = page });
        return new Border { Width = 420, CornerRadius = new CornerRadius(12), Child = bg };
    }
    public static async Task<byte[]> RenderPng(UIElement el)
    {
        var rtb = new RenderTargetBitmap(); await rtb.RenderAsync(el);
        var px = await rtb.GetPixelsAsync();
        using var ms = new InMemoryRandomAccessStream();
        var enc = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, ms);
        var dpi = (float)(MainWindow.I.Content.XamlRoot?.RasterizationScale ?? 1.0) * 96;
        enc.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, (uint)rtb.PixelWidth, (uint)rtb.PixelHeight, dpi, dpi, px.ToArray());
        await enc.FlushAsync();
        return await ReadAll(ms);
    }
    /// <summary>Lee todo el contenido de un flujo en memoria (se usa el búfer que devuelve ReadAsync).</summary>
    static async Task<byte[]> ReadAll(IRandomAccessStream s)
    {
        var size = (uint)s.Size; if (size == 0) return Array.Empty<byte>();
        s.Seek(0);
        var buf = await s.ReadAsync(new Windows.Storage.Streams.Buffer(size), size, InputStreamOptions.None);
        return buf.ToArray();
    }
    public static string FileBase(JObj x) => "Recibo_" + System.Text.RegularExpressions.Regex.Replace(x.Str("name"), @"[^\p{L}\p{N}]+", "_").Trim('_').IfEmpty("cliente") + "_" + Js.Str(x["orderNo"]);

    /// <summary>Ventana del recibo: copiar imagen + abrir WhatsApp, guardar, imprimir.</summary>
    public static async void Receipt(JObj x)
    {
        var core = Services.Core; var view = ReceiptView(core, x);
        async Task<byte[]?> Png() { try { return await RenderPng(view); } catch (Exception e) { Services.Report(e); MainWindow.I.Toast("No se pudo preparar el recibo."); return null; } }
        var copy = OptionRow("\uE8BD", "Copiar y abrir WhatsApp", "Pega el recibo en el chat con Ctrl+V", async () =>
        {
            var png = await Png(); if (png == null) return;
            var ms = new InMemoryRandomAccessStream(); await ms.WriteAsync(png.AsBuffer()); ms.Seek(0);
            var dp = new DataPackage(); dp.SetBitmap(RandomAccessStreamReference.CreateFromStream(ms)); dp.SetText(core.ReceiptCaption(x));
            Clipboard.SetContent(dp); try { Clipboard.Flush(); } catch { }
            MainWindow.I.Toast("Recibo copiado. En WhatsApp pulsa Ctrl+V para pegarlo.");
            MainWindow.I.WhatsApp(x.Str("phone"), core.ReceiptCaption(x), x.Str("name"));
        }, true);
        var save = OptionRow("\uE896", "Guardar imagen (PNG)", "Elige la carpeta donde guardarlo", async () => { var png = await Png(); if (png != null && await SaveFile(FileBase(x) + ".png", ".png", "Imagen PNG", png)) MainWindow.I.Toast("✅ Recibo guardado."); });
        var print = OptionRow("\uE749", "Abrir para imprimir", "Se abre con el visor de imágenes", async () =>
        {
            var png = await Png(); if (png == null) return;
            var path = Path.Combine(Path.GetTempPath(), FileBase(x) + ".png"); await File.WriteAllBytesAsync(path, png);
            await Windows.System.Launcher.LaunchFileAsync(await StorageFile.GetFileFromPathAsync(path));
        });
        await MainWindow.I.Dialog("Enviar recibo", V(12, OptionGroup(copy, save, print), view), "Cerrar", null, null, glyph: "\uE8A5", sub: $"{x.Str("name")} · total {Money(x["total"])}");
    }
}
