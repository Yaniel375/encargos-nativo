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

    /// <summary>Miniatura (o foto grande) a partir de un data URL.</summary>
    public static Border Photo(string dataUrl, double w, double h, Action? onClick = null, int decode = 400)
    {
        var img = new Image { Stretch = Stretch.UniformToFill };
        var b = new Border { Width = w, Height = h, CornerRadius = new CornerRadius(10), Background = Alpha(Mut, 0.15), Child = img };
        if (double.IsNaN(w)) { b.Width = double.NaN; img.Stretch = Stretch.Uniform; }
        _ = SetAsync(img, dataUrl, decode);
        if (onClick != null) b.Tapped += (_, e) => { e.Handled = true; onClick(); };
        return b;
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

    /// <summary>Visor de fotos con anterior/siguiente y eliminar.</summary>
    public static async void Viewer(List<string> list, int start, Action<int>? onDelete)
    {
        if (list.Count == 0) return;
        var i = Math.Clamp(start, 0, list.Count - 1);
        var host = new Grid { Width = 760, Height = 520 };
        void Show() { host.Children.Clear(); host.Children.Add(Photo(list[i], 760, 520, null, 1600)); }
        Show();
        var nav = Hs(8, list.Count > 1 ? Btn("‹ Anterior", () => { i = (i - 1 + list.Count) % list.Count; Show(); }, false, null, null, false) : null,
            list.Count > 1 ? Btn("Siguiente ›", () => { i = (i + 1) % list.Count; Show(); }, false, null, null, false) : null);
        var del = false;
        await MainWindow.I.Dialog("Foto", V(10, host, nav), "Cerrar", null, null, onDelete != null ? "Eliminar foto" : null, () => del = true);
        if (del && onDelete != null) onDelete(i);
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
