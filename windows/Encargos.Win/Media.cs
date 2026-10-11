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
    public static Border ReceiptView(EncargosApp core, JObj x)
    {
        var dark = Br(0xFF0F172Au); var mut = Br(0xFF64748Bu); var green = Br(0xFF059669u); var rose = Br(0xFFE11D48u);
        TextBlock Tx(string s, double size, bool bold, Brush c) { var t = T(s, size, bold, c); return t; }
        Grid L(string a, string b, Brush? c = null, bool bold = false) => Row(Tx(a, 13, false, mut), Tx(b, 14, bold, c ?? dark));
        var bal = Js.Num(x["balance"]);
        var s = V(8,
            Tx(core.BusinessName, 22, true, dark), Tx("Recibo de compra · " + core.OrderLabel(x), 13, false, mut),
            new Border { Height = 1, Background = Br(0xFFE2E8F0u) },
            L("Cliente", x.Str("name")), L("Teléfono", x.Str("phone").IfEmpty("—")), L("Fecha", Js.Fmt(x["date"])), L("Estado", x.Str("status").IfEmpty("Pedido")),
            new Border { Height = 1, Background = Br(0xFFE2E8F0u) },
            L("Costo del artículo", Js.Money(x["cost"])),
            L($"Envío ({core.ShippingName(core.TypeOf(x))}) {Js.ToFixed(Js.Num(x["pounds"]), 2)} lb × {Js.Money(x["lbCost"])}", Js.Money(x["shipping"])),
            L("Total", Js.Money(x["total"]), dark, true), L("Pagado", Js.Money(x["paid"]), green, true),
            L(bal < -0.005 ? "A favor" : "Saldo pendiente", bal > 0.005 ? Js.Money(bal) : bal < -0.005 ? "+" + Js.Money(-bal) : "Pagado completo", bal > 0.005 ? rose : green, true),
            new Border { Height = 1, Background = Br(0xFFE2E8F0u) },
            Tx("¡Gracias por tu compra!", 13, true, dark), Tx("Código de cliente: " + x.Str("accessCode"), 11, false, mut));
        return new Border { Background = Br(0xFFFFFFFFu), Padding = new Thickness(26), CornerRadius = new CornerRadius(12), Width = 420, Child = s };
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
        var copy = Btn("Copiar imagen y abrir WhatsApp", async () =>
        {
            var png = await Png(); if (png == null) return;
            var ms = new InMemoryRandomAccessStream(); await ms.WriteAsync(png.AsBuffer()); ms.Seek(0);
            var dp = new DataPackage(); dp.SetBitmap(RandomAccessStreamReference.CreateFromStream(ms)); dp.SetText(core.ReceiptCaption(x));
            Clipboard.SetContent(dp); try { Clipboard.Flush(); } catch { }
            MainWindow.I.Toast("Recibo copiado. En WhatsApp pulsa Ctrl+V para pegarlo.");
            MainWindow.I.WhatsApp(x.Str("phone"), core.ReceiptCaption(x), x.Str("name"));
        }, true, Green);
        var save = Btn("Guardar imagen (PNG)", async () => { var png = await Png(); if (png != null && await SaveFile(FileBase(x) + ".png", ".png", "Imagen PNG", png)) MainWindow.I.Toast("✅ Recibo guardado."); });
        var print = Btn("Abrir para imprimir", async () =>
        {
            var png = await Png(); if (png == null) return;
            var path = Path.Combine(Path.GetTempPath(), FileBase(x) + ".png"); await File.WriteAllBytesAsync(path, png);
            await Windows.System.Launcher.LaunchFileAsync(await StorageFile.GetFileFromPathAsync(path));
        });
        await MainWindow.I.Dialog("Enviar recibo", V(10, view, copy, Cols(save, print)), "Cerrar", null, null);
    }
}
