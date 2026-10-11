using Encargos.Core;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using static Encargos.Win.K;

namespace Encargos.Win;

/// <summary>Una pantalla de la app. Build() dibuja su contenido; se vuelve a dibujar cuando cambian los datos (si Live).</summary>
public abstract class Screen
{
    public abstract string Title { get; }
    /// <summary>Elemento del menú lateral que queda marcado.</summary>
    public virtual string Nav => "";
    /// <summary>false en formularios: no se redibujan mientras escribes.</summary>
    public virtual bool Live => true;
    /// <summary>Texto pequeño a la derecha del título (por ejemplo "5 activos").</summary>
    public virtual string? Right => null;
    /// <summary>Subtítulo bajo el título grande.</summary>
    public virtual string? Sub => null;
    /// <summary>false si la pantalla dibuja su propio encabezado (Inicio).</summary>
    public virtual bool ShowTitle => true;
    public abstract void Build(StackPanel p);
    protected static MainWindow S => MainWindow.I;
    protected static EncargosApp Core => Services.Core;
    protected void Redraw() => S.Render(true);
}

public sealed partial class MainWindow : Window
{
    public static MainWindow I { get; private set; } = null!;
    public IntPtr Hwnd { get; }
    readonly Grid _root = new();
    readonly Grid _host = new();
    readonly Canvas _stage = new();
    /// <summary>Escala actual de "Adaptar a la pantalla" (1 = tamaño normal).</summary>
    public double Fit { get; private set; } = 1;
    // FitAuto, FitFor y el tamaño de la ventana están en Display.cs (Pantalla y resolución)
    public void ApplyFit(double w, double h)
    {
        if (w <= 0 || h <= 0) return;
        var s = FitFor(w, h); Fit = s;
        _host.Width = w / s; _host.Height = h / s;
        _host.RenderTransform = Math.Abs(s - 1) < 0.001 ? null : new ScaleTransform { ScaleX = s, ScaleY = s };
    }
    /// <summary>Capa a pantalla completa (visor de fotos) por encima de todo.</summary>
    readonly Grid _overlay = new() { Visibility = Visibility.Collapsed };
    public void ShowOverlay(UIElement content) { _overlay.Children.Clear(); _overlay.Children.Add(content); _overlay.Visibility = Visibility.Visible; Fx.EnterUp(content, 0); }
    public void HideOverlay() { _overlay.Visibility = Visibility.Collapsed; _overlay.Children.Clear(); }
    public void RefreshFit() => ApplyFit(_root.ActualWidth, _root.ActualHeight);
    readonly Grid _main = new();
    readonly StackPanel _side = new() { Spacing = 4 };
    readonly ScrollViewer _scroll = new() { HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    TextBlock _title = H("Inicio", 18);
    TextBlock _syncText = Muted("", 11.5);
    AuroraBg? _aurora;
    bool _narrow;
    readonly Border _toast = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(250, 16, 16, 26), MaxWidth = 560, Visibility = Visibility.Collapsed, CornerRadius = new CornerRadius(16), Padding = new Thickness(14, 11, 12, 11) };
    readonly Border _busy = new() { Visibility = Visibility.Collapsed, Background = Alpha(Br(0xFF000000u), 0.35) };
    readonly TextBlock _busyText = T("", 14, true);
    readonly List<Screen> _stack = new();
    readonly Dictionary<string, (Button B, FontIcon I, TextBlock T, Border? Badge, TextBlock? BadgeT)> _items = new();
    readonly (Border B, TextBlock T)[] _topBadges = new (Border, TextBlock)[2];
    readonly DispatcherQueueTimer _renderTimer, _tick, _toastTimer;
    readonly SemaphoreSlim _dlg = new(1, 1);
    long _hiddenAt;
    bool _unlocked;
    public Screen Top => _stack[^1];
    /// <summary>Ancho útil del contenido (para distribuir las tarjetas en ventanas estrechas).</summary>
    public double ContentWidth => _scroll.ActualWidth > 0 ? _scroll.ActualWidth : 1100;

    public MainWindow()
    {
        I = this;
        Hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        Title = "Encargos";
        try { AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "icon.ico")); } catch { }
        try { if (AppWindow.TitleBar is { } tb && AppWindowTitleBar.IsCustomizationSupported()) { tb.BackgroundColor = Windows.UI.Color.FromArgb(255, 6, 10, 20); tb.ButtonBackgroundColor = Windows.UI.Color.FromArgb(255, 6, 10, 20); tb.ForegroundColor = Windows.UI.Color.FromArgb(255, 226, 232, 240); tb.ButtonForegroundColor = Windows.UI.Color.FromArgb(255, 226, 232, 240); tb.InactiveBackgroundColor = Windows.UI.Color.FromArgb(255, 6, 10, 20); tb.ButtonInactiveBackgroundColor = Windows.UI.Color.FromArgb(255, 6, 10, 20); } } catch { }
        Services.Start(DispatcherQueue.GetForCurrentThread());
        // tamaño de la ventana según Ajustes → Pantalla y resolución (cabe siempre en la pantalla)
        try { InitWindowSize(); } catch (Exception ex) { Services.Report(ex); }
        _renderTimer = DispatcherQueue.CreateTimer(); _renderTimer.Interval = TimeSpan.FromMilliseconds(150); _renderTimer.IsRepeating = false; _renderTimer.Tick += (_, _) => Render(false);
        _tick = DispatcherQueue.CreateTimer(); _tick.Interval = TimeSpan.FromSeconds(45); _tick.IsRepeating = true; _tick.Tick += (_, _) => { if (_unlocked && Services.Sync.Enabled()) Services.Sync.SyncNow(); UpdateStatus(); };
        _toastTimer = DispatcherQueue.CreateTimer(); _toastTimer.IsRepeating = false; _toastTimer.Tick += (_, _) => HideToast();

        K.Day = false; _root.RequestedTheme = ElementTheme.Dark; _root.Background = Bg;
        _root.Children.Add(MakeBg(false));
        // "Adaptar a la pantalla": la ventana se compone a su tamaño de diseño y se escala en proporción a la resolución,
        // así al maximizar todo crece en su sitio en lugar de separarse (las ventanas emergentes y avisos no se escalan)
        _stage.Children.Add(_host);
        _root.Children.Add(_stage);
        _root.SizeChanged += (_, e) => ApplyFit(e.NewSize.Width, e.NewSize.Height);
        _stack.Add(new HomePage());
        BuildMain();
        _busy.Child = new Border { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Padding = new Thickness(24), CornerRadius = new CornerRadius(18), Background = CardBg, BorderBrush = Line2, BorderThickness = new Thickness(1), Child = Hs(12, new ProgressRing { IsActive = true, Width = 28, Height = 28 }, _busyText) };
        _toast.Background = CardBg; _toast.BorderBrush = Line2; _toast.BorderThickness = new Thickness(1);
        _root.Children.Add(_busy); _root.Children.Add(_toast); _root.Children.Add(_overlay);
        Content = _root;

        // al cambiar el ancho de la ventana, el Inicio redistribuye sus tarjetas
        _scroll.SizeChanged += (_, e) => { var n = e.NewSize.Width < 900; if (n != _narrow) { _narrow = n; if (_unlocked && Top is HomePage) Render(true); } };
        Services.Core.OnChange(() => Services.Post(() => _renderTimer.Start()));
        Services.Core.Notifs.OnImportant = n => Services.Post(() => Toast("🔔 " + n.Str("title") + " · " + n.Str("text")));
        Services.Sync.OnState = () => Services.Post(() => { UpdateStatus(); if (Top is SyncPage or SyncDiagPage) _renderTimer.Start(); });
        Activated += OnActivated;
        ApplyPrivacy();

        var sec = Services.Security;
        if (sec.HasCredentials() && !sec.GetLockCfg().OnOpen) { Unlock(false); }
        else ShowLock();
        _tick.Start();
        if (Services.SelfTestDir != null) _ = SelfTest(Services.SelfTestDir);
    }

    /* ---------------- autoprueba (solo con ENCARGOS_SELFTEST, carpeta de datos aparte) ---------------- */
    public string? LastRenderError;
    async Task SelfTest(string dir)
    {
        var log = new System.Text.StringBuilder(); var fails = 0;
        void L(string s) { log.AppendLine(s); try { File.WriteAllText(Path.Combine(dir, "autoprueba.txt"), log.ToString()); } catch { } }
        try
        {
            Directory.CreateDirectory(dir);
            await Task.Delay(3000);
            await Snap(dir, "00_acceso", L);
            if (!Services.Security.HasCredentials()) Services.Security.SetCredentials("prueba", "clave123");
            Unlock(true); await Task.Delay(1500);
            var r = Services.Core.CreateOrder(new EncargosApp.NewOrder("Cliente Prueba", "55512345", 40, 7, 3, 10));
            var x = r.Value;
            L(r.Ok ? $"encargo de prueba creado: total {Js.Money(x!["total"])} · pagado {Js.Money(x["paid"])} · saldo {Js.Money(x["balance"])}" : "no se pudo crear el encargo: " + r.Error);
            if (r.Ok && Js.Num(x!["total"]) != 61) { fails++; L("ERROR: el total debería ser $61.00"); }
            var keys = new[] { "home", "clients", "new", "orders", "notes", "payments", "pending", "stats", "attention", "notifs", "completed", "backup", "security", "sync", "settings" };
            var i = 1;
            foreach (var k in keys)
            {
                LastRenderError = null; Root(PageFor(k)); await Task.Delay(900);
                if (LastRenderError != null) { fails++; L($"ERROR en {k}: {LastRenderError}"); }
                await Snap(dir, $"{i++:00}_{k}", L);
            }
            if (r.Ok)
            {
                foreach (var (name, page) in new (string, Screen)[] { ("ficha_encargo", new OrderPage(Js.Str(x!["id"]))), ("editar_encargo", new EditOrderPage(Js.Str(x["id"]))), ("ficha_cliente", new ClientPage("Cliente Prueba")), ("diagnostico_sync", new SyncDiagPage()) })
                {
                    LastRenderError = null; Go(page); await Task.Delay(900);
                    if (LastRenderError != null) { fails++; L($"ERROR en {name}: {LastRenderError}"); }
                    await Snap(dir, $"{i++:00}_{name}", L);
                }
            }
            // fotos de muestra (marco rojo y esquinas marcadas: si se ven las 4 esquinas, la foto está completa) — solo en la carpeta de prueba
            if (r.Ok)
            {
                var v = await TestPhoto(600, 1200, "VERTICAL"); var hz = await TestPhoto(1200, 600, "HORIZONTAL");
                Services.Core.AddOrderPhoto(x!["id"], v); Services.Core.AddOrderPhoto(x["id"], hz);
                foreach (var (name, page) in new (string, Screen)[] { ("ficha_encargo_fotos", new OrderPage(Js.Str(x["id"]))), ("ficha_cliente_fotos", new ClientPage("Cliente Prueba")) })
                { LastRenderError = null; Go(page); await Task.Delay(1200); if (LastRenderError != null) { fails++; L($"ERROR en {name}: {LastRenderError}"); } await Snap(dir, $"{i++:00}_{name}", L); }
                Media.Viewer(Services.Core.OrderPhotos(Services.Core.ById(x["id"])!), 0, null); await Task.Delay(1500); await Snap(dir, $"{i++:00}_visor_vertical", L);
                HideOverlay(); Media.Viewer(Services.Core.OrderPhotos(Services.Core.ById(x["id"])!), 1, null); await Task.Delay(1500); await Snap(dir, $"{i++:00}_visor_horizontal", L); HideOverlay();
            }
            // foto de perfil de prueba y ventanas con el estilo de la app (solo en la carpeta de prueba)
            Services.Core.Ls.Set(Media.ProfileKey, await TestPhoto(400, 400, "PERFIL")); RefreshProfile();
            LastRenderError = null; Root(new SettingsPage()); await Task.Delay(1200); if (LastRenderError != null) { fails++; L($"ERROR en perfil: {LastRenderError}"); }
            await Snap(dir, $"{i++:00}_perfil_con_foto", L);
            await SnapDialog(dir, $"{i++:00}_ventana_meta", L, () => Payments.Goal());
            await SnapDialog(dir, $"{i++:00}_ventana_negocio", L, () => _ = Dialog("Nombre del negocio", V(10, InfoBox("Aparece en los recibos y en los mensajes de WhatsApp."), Box("Nombre del negocio (aparece en recibos)", "Encargos", _ => { })), "Guardar", sub: "Aparece en recibos y mensajes"));
            await SnapDialog(dir, $"{i++:00}_ventana_confirmar", L, () => Ask("🧾 Eliminar gasto", "¿Eliminar este gasto?", "Eliminar", true, () => { }));
            // acceso con PIN (PIN de prueba solo en la carpeta de prueba)
            if (!Services.Security.PinOn()) Services.Security.SetPin("1234");
            ShowLock(); await Task.Delay(2600); await Snap(dir, $"{i++:00}_acceso_pin", L);
            if (_host.Children.Count > 0 && _host.Children[0] is LockView lv) { lv.UsePassword(); await Task.Delay(900); await Snap(dir, $"{i++:00}_acceso_contrasena", L); }
            Unlock(false); await Task.Delay(800);
            // ficha del encargo como la del cliente y ventana de recibo con opciones
            if (r.Ok) { LastRenderError = null; Go(new OrderPage(Js.Str(x!["id"]))); await Task.Delay(1000); if (LastRenderError != null) { fails++; L($"ERROR en ficha nueva: {LastRenderError}"); } await Snap(dir, $"{i++:00}_ficha_como_cliente", L);
                await SnapDialog(dir, $"{i++:00}_ventana_recibo", L, () => Media.Receipt(Services.Core.ById(x["id"])!)); }
            // anillos: fase 1 (llenándose) y fase 2 (lleno, con brillo recorriéndolo)
            Root(new HomePage()); await Task.Delay(800); await Snap(dir, $"{i++:00}_anillo_llenandose", L);
            await Task.Delay(3200); await Snap(dir, $"{i++:00}_anillo_lleno", L);
            Root(new PaymentsPage()); await Task.Delay(900); await Snap(dir, $"{i++:00}_agua_llenandose", L);
            await Task.Delay(3200); await Snap(dir, $"{i++:00}_agua_llena", L);
            // ventana grande (como maximizada en 1920×1080): con "Adaptar a la pantalla" todo crece en su sitio
            try { AppWindow.Resize(new Windows.Graphics.SizeInt32(1920, 1080)); } catch { }
            await Task.Delay(1500); L($"ventana {_root.ActualWidth:0}×{_root.ActualHeight:0} · escala {Fit:0.00}");
            foreach (var k in new[] { "home", "orders", "settings" }) { LastRenderError = null; Root(PageFor(k)); await Task.Delay(1200); if (LastRenderError != null) { fails++; L($"ERROR (grande) en {k}: {LastRenderError}"); } await Snap(dir, $"{i++:00}_grande_{k}", L); }
            try { AppWindow.Resize(new Windows.Graphics.SizeInt32(1320, 860)); } catch { }
            await Task.Delay(1200);
            // Pantalla y resolución: ventana de Ajustes, medidas fijas, maximizada, pantalla completa y tamaño del contenido
            {
                var (sw, sh) = ScreenSize(); L($"pantalla {sw}×{sh} · escala de Windows {DpiScale() * 100:0} %");
                Root(new SettingsPage()); await Task.Delay(900); await Snap(dir, $"{i++:00}_ajustes_pantalla", L);
                await SnapDialog(dir, $"{i++:00}_ventana_pantalla", L, () => DisplaySheet());
                foreach (var m in new[] { "1024x768", "1366x768", "1280x720" })
                {
                    ApplyWindowMode(m, false); await Task.Delay(1200); var (ww, wh) = WindowSize();
                    L($"medida {m}: ventana {ww}×{wh} · contenido {Fit * 100:0} %");
                    if (sw > 0 && (ww > sw || wh > sh)) { fails++; L($"ERROR: la ventana {ww}×{wh} no cabe en la pantalla {sw}×{sh}"); }
                    LastRenderError = null; Root(PageFor("home")); await Task.Delay(900); if (LastRenderError != null) { fails++; L($"ERROR ({m}): {LastRenderError}"); }
                    await Snap(dir, $"{i++:00}_medida_{m}", L);
                }
                foreach (var z in new[] { 1.25, 0.85 })
                {
                    SetZoom(z); await Task.Delay(900);
                    if (Math.Abs(Fit - z) > 0.001) { fails++; L($"ERROR: el contenido debería estar al {z * 100:0} % y está al {Fit * 100:0} %"); } else L($"contenido al {Fit * 100:0} %");
                    await Snap(dir, $"{i++:00}_contenido_{z * 100:0}", L);
                }
                // pantalla muy ancha (contenido al 50 %): las tarjetas deben empezar junto al menú lateral, sin hueco a la izquierda
                SetZoom(0.5); foreach (var k in new[] { "home", "orders", "settings" }) { LastRenderError = null; Root(PageFor(k)); await Task.Delay(1100); if (LastRenderError != null) { fails++; L($"ERROR (ancha) en {k}: {LastRenderError}"); } await Snap(dir, $"{i++:00}_ancha_{k}", L); }
                SetZoom(null);
                // medida exacta que cabe en la pantalla
                ApplyWindowMode("900x600", false); await Task.Delay(1200);
                { var (ew, eh) = WindowSize(); if (ew != 900 || eh != 600) { fails++; L($"ERROR: con 900x600 la ventana mide {ew}×{eh}"); } else L("medida 900x600: exacta"); }
                // recordar el último tamaño: se guarda, se cambia la ventana y se recupera
                SaveLast(); ApplyWindowMode("1000x700", false); await Task.Delay(1000); ApplyWindowMode("", false); await Task.Delay(1200);
                { var (rw, rh) = WindowSize(); if (rw != 900 || rh != 600) { fails++; L($"ERROR: al recordar, la ventana mide {rw}×{rh} (debía 900×600)"); } else L("recordar el último tamaño: vuelve a 900×600"); }
                // atajos con el teclado de verdad
                await KeyTest(L, () => fails++);
                ApplyWindowMode("max", false); await Task.Delay(1300); L($"maximizada: {(IsMaximized ? "sí" : "NO")} · ventana {WindowSize().W}×{WindowSize().H} · contenido {Fit * 100:0} %"); await Snap(dir, $"{i++:00}_maximizada", L);
                ApplyWindowMode("full", false); await Task.Delay(1300); L($"pantalla completa: {(IsFullScreen ? "sí" : "NO")} · ventana {WindowSize().W}×{WindowSize().H}");
                if (!IsFullScreen) { fails++; L("ERROR: no se puso en pantalla completa"); }
                await Snap(dir, $"{i++:00}_pantalla_completa", L);
                ExitFull(); await Task.Delay(1300); if (IsFullScreen) { fails++; L("ERROR: no salió de la pantalla completa"); }
                ApplyWindowMode("1320x860", false); try { AppWindow.Resize(new Windows.Graphics.SizeInt32(1320, 860)); } catch { }
                await Task.Delay(1200);
            }
            // segunda vuelta: MODO DÍA (cambio inmediato, sin reiniciar)
            ApplyTheme(true); await Task.Delay(800);
            foreach (var k in new[] { "home", "orders", "clients", "new", "payments", "pending", "stats", "attention", "notifs", "settings" })
            {
                LastRenderError = null; Root(PageFor(k)); await Task.Delay(900);
                if (LastRenderError != null) { fails++; L($"ERROR (día) en {k}: {LastRenderError}"); }
                await Snap(dir, $"dia_{i++:00}_{k}", L);
            }
            if (r.Ok) { LastRenderError = null; Go(new OrderPage(Js.Str(x!["id"]))); await Task.Delay(900); await Snap(dir, $"dia_{i++:00}_ficha", L); }
            ApplyTheme(false); await Task.Delay(300);
            L(fails == 0 ? "RESULTADO: OK" : $"RESULTADO: {fails} error(es)");
        }
        catch (Exception e) { L("RESULTADO: ERROR " + e.GetType().Name + ": " + Services.Redact(e.Message) + "\n" + e.StackTrace); }
        await Task.Delay(500);
        Application.Current.Exit();
    }
    /// <summary>Foto de prueba para la autoprueba: fondo claro, recuadro azul, marco rojo y esquinas rojas.</summary>
    static async Task<string> TestPhoto(int w, int h, string label)
    {
        var px = new byte[w * h * 4]; var m = Math.Min(w, h) / 8; var fr = 14;
        for (var y = 0; y < h; y++) for (var xx = 0; xx < w; xx++)
        {
            var o = (y * w + xx) * 4; byte r = 226, g = 232, b = 240;
            if (xx > w * .12 && xx < w * .88 && y > h * .12 && y < h * .88) { r = 37; g = 99; b = 235; }
            var edge = xx < fr || y < fr || xx >= w - fr || y >= h - fr; var corner = (xx < m || xx >= w - m) && (y < m || y >= h - m);
            if (edge || corner) { r = 225; g = 29; b = 72; }
            px[o] = b; px[o + 1] = g; px[o + 2] = r; px[o + 3] = 255;
        }
        using var ms = new Windows.Storage.Streams.InMemoryRandomAccessStream();
        var enc = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(Windows.Graphics.Imaging.BitmapEncoder.JpegEncoderId, ms);
        enc.SetPixelData(Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8, Windows.Graphics.Imaging.BitmapAlphaMode.Ignore, (uint)w, (uint)h, 96, 96, px);
        await enc.FlushAsync(); ms.Seek(0);
        var bytes = new byte[ms.Size]; using (var rs = ms.AsStreamForRead()) { var n = 0; while (n < bytes.Length) { var k = await rs.ReadAsync(bytes.AsMemory(n)); if (k == 0) break; n += k; } }
        _ = label; return "data:image/jpeg;base64," + Convert.ToBase64String(bytes);
    }
    Task Snap(string dir, string name, Action<string> L) => SnapEl(_root, dir, name, L);
    /// <summary>Abre una ventana, la captura (las ventanas viven en una capa aparte) y la cierra.</summary>
    async Task SnapDialog(string dir, string name, Action<string> L, Action open)
    {
        open(); await Task.Delay(1300);
        ContentDialog? cd = null;
        foreach (var p in VisualTreeHelper.GetOpenPopupsForXamlRoot(_root.XamlRoot)) if (p.Child is ContentDialog c) cd = c;
        if (cd == null) { L($"(no se encontró la ventana {name})"); return; }
        await SnapEl(cd, dir, name, L); cd.Hide(); await Task.Delay(700);
    }
    async Task SnapEl(UIElement el, string dir, string name, Action<string> L)
    {
        try
        {
            var rtb = new Microsoft.UI.Xaml.Media.Imaging.RenderTargetBitmap();
            await rtb.RenderAsync(el);
            var px = await rtb.GetPixelsAsync();
            var bytes = System.Runtime.InteropServices.WindowsRuntime.WindowsRuntimeBufferExtensions.ToArray(px);
            using var ms = new Windows.Storage.Streams.InMemoryRandomAccessStream();
            var enc = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId, ms);
            enc.SetPixelData(Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8, Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied, (uint)rtb.PixelWidth, (uint)rtb.PixelHeight, 96, 96, bytes);
            await enc.FlushAsync();
            ms.Seek(0);
            using var outS = File.Create(Path.Combine(dir, name + ".png"));
            await ms.AsStreamForRead().CopyToAsync(outS);
            L($"captura {name} ({rtb.PixelWidth}x{rtb.PixelHeight})");
        }
        catch (Exception e) { L($"captura {name} falló: {e.GetType().Name} {e.Message}"); }
    }

    /* ---------------- tema día / noche (inmediato, sin reiniciar) ---------------- */
    UIElement MakeBg(bool day)
    {
        if (!day) return _aurora ??= new AuroraBg();
        var g = new Grid { Background = new LinearGradientBrush { StartPoint = new Windows.Foundation.Point(0, 0), EndPoint = new Windows.Foundation.Point(0, 1), GradientStops = { new GradientStop { Color = Windows.UI.Color.FromArgb(255, 0xF4, 0xF6, 0xFB), Offset = 0 }, new GradientStop { Color = Windows.UI.Color.FromArgb(255, 0xE9, 0xEE, 0xF8), Offset = 1 } } } };
        g.Children.Add(new Microsoft.UI.Xaml.Shapes.Rectangle { Fill = new RadialGradientBrush { Center = new Windows.Foundation.Point(.85, .08), GradientOrigin = new Windows.Foundation.Point(.85, .08), RadiusX = .6, RadiusY = .5, GradientStops = { new GradientStop { Color = Windows.UI.Color.FromArgb(26, 37, 99, 235), Offset = 0 }, new GradientStop { Color = Windows.UI.Color.FromArgb(0, 37, 99, 235), Offset = 1 } } } });
        return g;
    }
    /// <summary>Aplica el tema a toda la ventana al instante: fondo, barra lateral, barra superior y pantalla actual.</summary>
    public void ApplyTheme(bool day)
    {
        K.Day = day; _root.RequestedTheme = day ? ElementTheme.Light : ElementTheme.Dark; _root.Background = Bg;
        _root.Children[0] = MakeBg(day);
        _toast.Background = CardBg; _toast.BorderBrush = Line2;
        if (_busy.Child is Border bb) { bb.Background = CardBg; bb.BorderBrush = Line2; }
        BuildMain();
        if (_unlocked) Render(true, true);
    }

    /* ---------------- estructura: barra lateral + barra superior + contenido ---------------- */
    void BuildMain()
    {
        // al reconstruir (cambio de tema) hay que soltar los elementos que se reutilizan de su contenedor anterior
        if (_side.Parent is ScrollViewer oldSv) oldSv.Content = null;
        if (_scroll.Parent is Panel oldP) oldP.Children.Remove(_scroll);
        _main.Children.Clear(); _main.ColumnDefinitions.Clear(); _side.Children.Clear(); _items.Clear();
        _title = H("Inicio", 18); _syncText = Muted("Tu negocio, en orden", 11.5); _syncDot = SyncDotBtn();
        _main.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });
        _main.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // ----- barra lateral (.nav de la web en PC) -----
        var logo = new Image { Width = 34, Height = 34 };
        try { logo.Source = new BitmapImage(new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", "logo.png"))); } catch { }
        var brandT = T("Encargos", 17, true); brandT.FontWeight = FontWeights.ExtraBold;
        _side.Children.Add(Click(Hs(10, new Border { CornerRadius = new CornerRadius(10), Child = logo }, V(0, brandT, Muted("Tu negocio, en orden", 11))), () => Root(new HomePage())));
        var nb = Btn("Nuevo encargo", () => Root(new NewOrderPage()), true, null, ""); nb.Margin = new Thickness(0, 12, 0, 12); _side.Children.Add(nb);
        void Item(string key, string text, string glyph, uint? badge = null)
        {
            var ic = new FontIcon { Glyph = glyph, FontSize = 15, Foreground = Mut };
            var tx = T(text, 13.5, true, Br(Day ? 0xFF334155u : 0xFFCBD5E1u), false); tx.FontWeight = FontWeights.SemiBold;
            Border? bd = null; TextBlock? bt = null;
            var g = new Grid { ColumnSpacing = 11 };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.Children.Add(ic); Grid.SetColumn(tx, 1); g.Children.Add(tx);
            if (badge != null) { bt = T("", 10.5, true, Br(0xFFFFFFFFu), false); bt.FontWeight = FontWeights.ExtraBold; bt.HorizontalAlignment = HorizontalAlignment.Center; bd = new Border { MinWidth = 20, Height = 20, CornerRadius = new CornerRadius(10), Background = Br(badge.Value), Padding = new Thickness(5, 0, 5, 0), Child = bt, Visibility = Visibility.Collapsed }; Grid.SetColumn(bd, 2); g.Children.Add(bd); }
            var b = Click(g, () => Root(PageFor(key))); b.Padding = new Thickness(12, 9, 12, 9); b.BorderThickness = new Thickness(1); b.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(b, text);
            _items[key] = (b, ic, tx, bd, bt); _side.Children.Add(b);
        }
        void Sep() => _side.Children.Add(new Border { Height = 1, Background = LineBr, Margin = new Thickness(6, 8, 6, 8) });
        Item("home", "Inicio", ""); Item("orders", "Encargos", ""); Item("clients", "Clientes", ""); Item("payments", "Pagos", "");
        Item("pending", "Pendientes", ""); Item("stats", "Estadísticas", ""); Item("notes", "Notas", "");
        Sep(); Item("attention", "Centro de atención", "", 0xFFF43F5Eu); Item("notifs", "Notificaciones", "", 0xFF3B82F6u); Item("completed", "Completados", "");
        Sep(); Item("backup", "Respaldo", ""); Item("security", "Seguridad", ""); Item("sync", "Sincronización", ""); Item("settings", "Ajustes", "");
        var side = new ScrollViewer { Content = _side, Padding = new Thickness(12, 14, 12, 14), VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var sideB = new Border { Child = side, Background = Br(Day ? 0xE6FFFFFFu : 0xC7090D18u), BorderBrush = LineBr, BorderThickness = new Thickness(0, 0, 1, 0) };
        _main.Children.Add(sideB);

        // ----- barra superior flotante (.header) -----
        var bar = new Grid { ColumnSpacing = 8 };
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (var i = 0; i < 3; i++) bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var back = HeadBtn("", "Volver", -1, 0, () => Back()); back.Visibility = Visibility.Collapsed; _back = back;
        var titleRow = Hs(10, back, V(0, Hs(8, _title, _syncDot), _syncText)); bar.Children.Add(titleRow);
        var attn = HeadBtn("", "Centro de atención", 0, 0xFFF43F5Eu, () => Go(new AttentionPage())); Grid.SetColumn(attn, 1); bar.Children.Add(attn);
        var bell = HeadBtn("", "Notificaciones", 1, 0xFF3B82F6u, () => Go(new NotifsPage())); Grid.SetColumn(bell, 2); bar.Children.Add(bell);
        // chip de perfil con menú
        _avSlot = new Grid(); _avSlot.Children.Add(ChipAvatar()); var av = _avSlot;
        var who = V(0, T("Administrador", 12.5, true, null, false), Muted(Services.Security.UserName().IfEmpty("—"), 10.5));
        var chip = new Button { Content = Hs(8, av, who, new FontIcon { Glyph = "", FontSize = 11, Foreground = Mut }), Padding = new Thickness(4, 4, 10, 4), CornerRadius = new CornerRadius(14), Background = BtnBg, BorderBrush = BtnLine, BorderThickness = new Thickness(1) };
        if (BtnStyle != null) chip.Style = BtnStyle;
        // menú del perfil como en la referencia: cabecera con el usuario, accesos en cuadrícula, modo día/noche y bloquear
        var menu = new Flyout();
        menu.FlyoutPresenterStyle = new Style(typeof(FlyoutPresenter)) { Setters = { new Setter(Control.PaddingProperty, new Thickness(8)), new Setter(Control.CornerRadiusProperty, new CornerRadius(16)), new Setter(Control.BackgroundProperty, CardBg), new Setter(Control.BorderBrushProperty, Line2), new Setter(Control.BorderThicknessProperty, new Thickness(1)) } };
        // usuario y rol → Pagos · Estadísticas · Ajustes → separador → Modo día/noche → Bloquear app (compacto, como en Android)
        // (Pendientes, Completados, Respaldo, Seguridad y Sincronización siguen en la barra lateral)
        var mp = new StackPanel { Spacing = 5, Width = 232 };
        var whoName = T(Services.Core.GreetName.IfEmpty("Mi cuenta"), 13.5, true, null, false, 1); whoName.FontWeight = FontWeights.ExtraBold;
        _menuAvSlot = new Grid(); _menuAvSlot.Children.Add(Media.ProfilePic(Services.Core.GreetName.IfEmpty("E"), 30));
        var headBtn = Click(Lead(_menuAvSlot, V(0, whoName, Muted(Services.Security.UserName().IfEmpty("admin"), 11)), new FontIcon { Glyph = "\uE76C", FontSize = 11, Foreground = Mut }), () => { menu.Hide(); Go(new SettingsPage()); });
        headBtn.Padding = new Thickness(4); ((Grid)headBtn.Content).ColumnSpacing = 9; Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(headBtn, "Ver perfil y ajustes");
        mp.Children.Add(headBtn);
        var tiles = new List<UIElement>();
        foreach (var (k, l, gl, full) in new[] { ("payments", "Pagos", "\uE8C7", "Registro de pagos"), ("stats", "Estadísticas", "\uE9D2", "Estadísticas"), ("settings", "Ajustes", "\uE713", "Ajustes") })
        {
            var key = k; var lab = T(l, 10.5, true, null, false); lab.HorizontalAlignment = HorizontalAlignment.Center;
            var ic = new FontIcon { Glyph = gl, FontSize = 16, Foreground = BlueInk, HorizontalAlignment = HorizontalAlignment.Center };
            var tb = Click(V(3, ic, lab), () => { menu.Hide(); if (Top.Nav != key) Go(PageFor(key)); }); tb.Padding = new Thickness(2, 8, 2, 8);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(tb, full);
            tiles.Add(new Border { Child = tb, CornerRadius = new CornerRadius(10), Background = Tile, BorderBrush = LineBr, BorderThickness = new Thickness(1) });
        }
        var tg = Grid2(tiles, 3); tg.ColumnSpacing = 5; mp.Children.Add(tg);
        mp.Children.Add(new Border { Height = 1, Background = LineBr, Margin = new Thickness(4, 2, 4, 2) });
        Button MenuRow(string glyph, string text, Brush color, Brush tint, Brush bg, Action a)
        {
            var ic = new Border { Width = 26, Height = 26, CornerRadius = new CornerRadius(8), Background = bg, BorderBrush = Alpha((SolidColorBrush)tint, .25), BorderThickness = new Thickness(1), Child = new FontIcon { Glyph = glyph, FontSize = 12, Foreground = tint } };
            var lab = T(text, 12.5, true, color, false); lab.FontWeight = FontWeights.SemiBold;
            var g = Lead(ic, lab); g.ColumnSpacing = 9;
            var b = Click(g, () => { menu.Hide(); a(); }); b.Padding = new Thickness(4); Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(b, text); return b;
        }
        mp.Children.Add(MenuRow(Day ? "\uE708" : "\uE706", Day ? "Modo noche" : "Modo día", Text, Text, Tile, () => { var d = !K.Day; Services.Core.Ls.Set("encargos_aurora_mode", d ? "day" : "night"); Services.Post(() => ApplyTheme(d)); }));
        mp.Children.Add(MenuRow("\uE72E", "Bloquear app", Rose, Rose, Alpha(Rose, .10), () => { Services.Security.Log(true, "Bloqueo manual"); ShowLock(); }));
        // el menú crece con "Adaptar a la pantalla" para no verse diminuto en ventanas grandes
        menu.Opening += (_, _) => { if (menu.Content is Viewbox vb) { vb.Width = 232 * Fit; } };
        mp.Width = 232;
        menu.Content = new Viewbox { Child = mp, Width = 232, Stretch = Stretch.Uniform };
        chip.Flyout = menu; Grid.SetColumn(chip, 3); bar.Children.Add(Fx.Bounce(chip, .96));
        var header = new Border { Child = bar, Margin = new Thickness(16, 14, 16, 6), Padding = new Thickness(16, 9, 10, 9), CornerRadius = new CornerRadius(20), Background = Br(Day ? 0xE6FFFFFFu : 0xC7090D18u), BorderBrush = LineBr, BorderThickness = new Thickness(1) };

        var body = new Grid();
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        body.Children.Add(header); Grid.SetRow(_scroll, 1); body.Children.Add(_scroll);
        Grid.SetColumn(body, 1); _main.Children.Add(body);
    }
    FrameworkElement? _back;
    Grid _avSlot = new(), _menuAvSlot = new();
    /// <summary>Avatar del chip de la barra: foto de perfil o la inicial.</summary>
    FrameworkElement ChipAvatar() => Media.ProfilePhoto.Length > 0 ? Media.ProfilePic(Services.Core.GreetName.IfEmpty("E"), 30, Alpha(Blue, .45)) :
        new Border { Width = 30, Height = 30, CornerRadius = new CornerRadius(10), Background = AvatarBg, BorderBrush = Alpha(Blue, .45), BorderThickness = new Thickness(1), Child = new TextBlock { Text = (Services.Core.GreetName.Trim() + "E")[..1].ToUpperInvariant(), FontFamily = Font, FontWeight = FontWeights.ExtraBold, FontSize = 14, Foreground = Text, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
    /// <summary>Actualiza la foto (o inicial) del perfil en la barra y en su menú.</summary>
    public void RefreshProfile()
    {
        _avSlot.Children.Clear(); _avSlot.Children.Add(ChipAvatar());
        _menuAvSlot.Children.Clear(); _menuAvSlot.Children.Add(Media.ProfilePic(Services.Core.GreetName.IfEmpty("E"), 30));
    }
    /// <summary>Botón redondo de la barra superior. El aviso con el número va por fuera del botón (encima de su esquina):
    /// dentro del botón quedaba recortado por el borde redondeado y solo se veía un trocito de color.</summary>
    FrameworkElement HeadBtn(string glyph, string desc, int badgeIdx, uint badgeColor, Action onClick)
    {
        var g = new Grid { Width = 40, Height = 40 };
        g.Children.Add(new FontIcon { Glyph = glyph, FontSize = 16, Foreground = Text });
        var b = new Button { Content = g, Width = 40, Height = 40, Padding = new Thickness(0), CornerRadius = new CornerRadius(14), Background = BtnBg, BorderBrush = BtnLine, BorderThickness = new Thickness(1) };
        if (BtnStyle != null) b.Style = BtnStyle;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(b, desc);
        b.Click += (_, _) => onClick();
        if (badgeIdx >= 0)
        {
            var t = T("", 10, true, Br(0xFFFFFFFFu), false); t.FontWeight = FontWeights.ExtraBold; t.HorizontalAlignment = HorizontalAlignment.Center; t.VerticalAlignment = VerticalAlignment.Center;
            var bd = new Border { MinWidth = 18, Height = 18, CornerRadius = new CornerRadius(9), Background = Br(badgeColor), BorderBrush = BadgeRing, BorderThickness = new Thickness(2), Padding = new Thickness(3, 0, 3, 0), Child = t,
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, -7, -7, 0), Visibility = Visibility.Collapsed, IsHitTestVisible = false };
            _topBadges[badgeIdx] = (bd, t);
            var wrap = new Grid { Width = 40, Height = 40, VerticalAlignment = VerticalAlignment.Center };
            wrap.Children.Add(Fx.Bounce(b, .92)); wrap.Children.Add(bd);
            return wrap;
        }
        return Fx.Bounce(b, .92);
    }

    public static Screen PageFor(string k) => k switch
    {
        "clients" => (Screen)new ClientsPage(), "new" => new NewOrderPage(), "orders" => new OrdersPage(), "notes" => new NotesPage(), "payments" => new PaymentsPage(),
        "pending" => new PendingPage(), "stats" => new StatsPage(), "attention" => new AttentionPage(), "notifs" => new NotifsPage(), "completed" => new CompletedPage(),
        "backup" => new BackupPage(), "security" => new SecurityPage(), "sync" => new SyncPage(), "settings" => new SettingsPage(), _ => new HomePage(),
    };

    /* ---------------- navegación ---------------- */
    /// <summary>Posición de desplazamiento de cada pantalla que sigue en el historial: al volver atrás aparece donde la dejaste.</summary>
    readonly Dictionary<Screen, double> _scrollMemo = new();
    public void Go(Screen p) { if (_stack.Count > 0) _scrollMemo[Top] = _scroll.VerticalOffset; _stack.Add(p); Render(true, true); }
    public void Root(Screen p) { _stack.Clear(); _scrollMemo.Clear(); _stack.Add(p); Render(true, true); }
    public void Replace(Screen p) { _scrollMemo.Remove(Top); _stack.RemoveAt(_stack.Count - 1); _stack.Add(p); Render(true, true); }
    public void Back()
    {
        if (_stack.Count > 1)
        {
            _scrollMemo.Remove(Top); _stack.RemoveAt(_stack.Count - 1); Render(true, true);
            if (_scrollMemo.TryGetValue(Top, out var off) && off > 0)
                // tras dibujar la pantalla (y su entrada animada), se vuelve a la posición guardada
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () => { _scroll.UpdateLayout(); _scroll.ChangeView(null, off, null, true); });
        }
        else if (Top is not HomePage) Root(new HomePage());
    }
    public bool IsCurrent(Screen p) => _stack.Count > 0 && ReferenceEquals(Top, p);

    /// <summary>Dibuja la pantalla actual. force=false solo redibuja si la pantalla es "viva". Al navegar, las tarjetas entran escalonadas (aUp).</summary>
    public void Render(bool force, bool top = false)
    {
        if (!_unlocked) return;
        var p = Top; if (!force && !p.Live) { UpdateBadges(); return; }
        var off = _scroll.VerticalOffset;
        var panel = new StackPanel { Spacing = 12, Padding = new Thickness(28, 10, 28, 48) };
        try
        {
            if (p.ShowTitle)
            {
                var big = H(p.Title, 24); big.CharacterSpacing = -10;
                var left = V(2, big, p.Sub != null ? Muted(p.Sub, 13) : null);
                // las fichas se presentan como en la referencia: título y botón "Cerrar" a la derecha
                var close = p is OrderPage or EditOrderPage or ClientPage ? Btn("Cerrar", () => Back(), false, null, null, false) : null;
                panel.Children.Add(p.Right != null || close != null ? Row(left, p.Right != null ? T(p.Right, 12.5, true, Mut, false) : null, close) : left);
            }
            p.Build(panel);
        }
        catch (Exception e) { Services.Report(e); LastRenderError = e.GetType().Name + ": " + Services.Redact(e.Message); panel.Children.Clear(); panel.Children.Add(Card(T("No se pudo mostrar esta pantalla.", 15, true), Muted(Services.Redact(e.Message)), Btn("Ir a Inicio", () => Root(new HomePage()), true))); }
        // pegado al menú lateral (alineado con el título de la barra de arriba) en lugar de centrado: en pantallas anchas
        // no queda un hueco a la izquierda; ocupa el ancho disponible hasta 1400 y lo que sobra queda a la derecha
        var holder = new Grid();
        holder.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MaxWidth = 1400 });
        holder.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        holder.Children.Add(panel);
        _scroll.Content = holder;
        if (!top) { panel.UpdateLayout(); _scroll.ChangeView(null, off, null, true); }
        else { _scroll.ChangeView(null, 0, null, true); var i = 0; foreach (var c in panel.Children) Fx.EnterUp(c, i++); }
        _title.Text = p.Title;
        if (_back != null) _back.Visibility = _stack.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        foreach (var (k, it) in _items)
        {
            var on = k == p.Nav;
            it.B.Background = on ? Alpha(Blue, .14) : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            it.B.BorderBrush = on ? Alpha(Blue, .45) : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            it.I.Foreground = on ? Blue : Mut; it.T.Foreground = on ? (Day ? Br(0xFF0F172Au) : Br(0xFFFFFFFFu)) : Br(Day ? 0xFF334155u : 0xFFCBD5E1u);
        }
        UpdateBadges(); UpdateStatus();
    }
    void UpdateBadges()
    {
        try
        {
            var a = Services.Core.Attention.Badge(); var n = Services.Core.Notifs.UnreadCount();
            void Set(Border? b, TextBlock? t, int v) { if (b == null || t == null) return; b.Visibility = v > 0 ? Visibility.Visible : Visibility.Collapsed; var s = v > 99 ? "99+" : v.ToString(); if (t.Text != s) { t.Text = s; if (v > 0) Fx.Pop(b, 0, .8, 300); } }
            Set(_items["attention"].Badge, _items["attention"].BadgeT, a); Set(_items["notifs"].Badge, _items["notifs"].BadgeT, n);
            Set(_topBadges[0].B, _topBadges[0].T, a); Set(_topBadges[1].B, _topBadges[1].T, n);
        }
        catch { }
    }
    /* ---- punto de sincronización: verde = sincronizado · azul latiendo = sincronizando · ámbar = pendiente o sin conexión · rojo = error · gris = en pausa ---- */
    Button _syncDot = new();
    Border _dotCore = new(), _dotHalo = new();
    Microsoft.UI.Xaml.Media.Animation.Storyboard? _pulse;
    Button SyncDotBtn()
    {
        _dotCore = new Border { Width = 9, Height = 9, CornerRadius = new CornerRadius(4.5), Background = Mut, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        _dotHalo = new Border { Width = 16, Height = 16, CornerRadius = new CornerRadius(8), Background = Alpha(Mut, .25), Child = _dotCore };
        var b = new Button { Content = _dotHalo, Padding = new Thickness(3), MinWidth = 0, MinHeight = 0, CornerRadius = new CornerRadius(11), Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent), BorderThickness = new Thickness(0), Visibility = Visibility.Collapsed, VerticalAlignment = VerticalAlignment.Center };
        if (BtnStyle != null) b.Style = BtnStyle;
        b.Padding = new Thickness(3); b.Click += (_, _) => Go(new SyncPage());
        _pulse = null; return b;
    }
    void UpdateStatus()
    {
        var s = Services.Sync.GetStatus();
        _syncText.Text = "Tu negocio, en orden"; _syncText.Foreground = Mut;
        var on = Services.Sync.Cfg() != null;
        _syncDot.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        if (!on) return;
        var c = s.Kind switch { "ok" => Green, "run" => Blue, "error" => Rose, "offline" or "pending" => Amber, _ => Mut };
        _dotCore.Background = c; _dotHalo.Background = Alpha(c, .25);
        ToolTipService.SetToolTip(_syncDot, s.Text); Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_syncDot, s.Text);
        if (s.Kind == "run" && Fx.Enabled) { if (_pulse == null) { _pulse = new Microsoft.UI.Xaml.Media.Animation.Storyboard(); _pulse.Children.Add(Fx.Anim(_dotHalo, "Opacity", 1, .35, 700, 0, new Microsoft.UI.Xaml.Media.Animation.SineEase(), true, true)); try { _pulse.Begin(); } catch { } } }
        else if (_pulse != null) { _pulse.Stop(); _pulse = null; _dotHalo.Opacity = 1; }
    }

    /* ---------------- bloqueo ---------------- */
    void ShowLock()
    {
        _unlocked = false; Services.Unlocked = false; Services.Security.Forget();
        if (K.Day) ApplyTheme(false);   // el acceso va siempre sobre el fondo aurora (como la app web)
        _host.Children.Clear();
        var lv = new LockView(() => Unlock(true));
        _host.Children.Add(lv);
    }
    void Unlock(bool fromLock)
    {
        _unlocked = true; Services.Unlocked = true;
        if (Services.DayMode() != K.Day) ApplyTheme(Services.DayMode());
        _host.Children.Clear(); _host.Children.Add(_main);
        if (fromLock) { _main.Opacity = 0; var sb = new Microsoft.UI.Xaml.Media.Animation.Storyboard(); sb.Children.Add(Fx.Anim(_main, "Opacity", 0, 1, 420, 0)); sb.Completed += (_, _) => _main.Opacity = 1; sb.Begin(); }
        Render(true, true);
        AfterUnlock();
    }
    void AfterUnlock()
    {
        try
        {
            var payload = Services.Backup.AutoDue();
            if (payload != null) { File.WriteAllText(Path.Combine(Services.DocsDir(), "respaldo_auto_" + Js.NowIso()[..10] + ".json"), payload); Services.Backup.AutoDone(); }
        }
        catch (Exception e) { Services.Report(e); }
        Services.Core.Notifs.SysEvents();
        if (Services.Sync.Enabled()) Services.Sync.SyncNow();
    }
    void OnActivated(object sender, WindowActivatedEventArgs e)
    {
        if (e.WindowActivationState == WindowActivationState.Deactivated) { if (_hiddenAt == 0) _hiddenAt = Js.NowMs(); return; }
        var hidden = _hiddenAt > 0 ? Js.NowMs() - _hiddenAt : 0; _hiddenAt = 0;
        var sec = Services.Security;
        if (_unlocked && sec.HasCredentials() && hidden > 0 && sec.ShouldLockAfter(hidden)) { sec.Log(true, "Bloqueo al volver a la app"); ShowLock(); return; }
        if (_unlocked && hidden > 0 && Services.Sync.Enabled()) Services.Sync.SyncNow();
    }
    public void ApplyPrivacy() => Native.Privacy(Hwnd, Services.Security.PrivacyOn());

    /// <summary>Aviso (.toast): vidrio oscuro con icono; entra desde abajo.</summary>
    public void Toast(string msg, Action? undo = null)
    {
        var ok = msg.StartsWith("✅"); var bad = !ok && System.Text.RegularExpressions.Regex.IsMatch(msg, "^(No se pudo|Error|⚠️)|no es válid|incorrect", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var row = new Grid { ColumnSpacing = 10 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        if (ok || bad) row.Children.Add(new Border { Width = 24, Height = 24, CornerRadius = new CornerRadius(12), Background = Alpha(ok ? Green : Rose, .18), Child = new FontIcon { Glyph = ok ? "" : "", FontSize = 12, Foreground = ok ? Green : Rose } });
        var tx = T(NoEmoji(msg), 13.5, true); tx.FontWeight = FontWeights.SemiBold; tx.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(tx, 1); row.Children.Add(tx);
        if (undo != null) { var l = Link("Deshacer", () => { HideToast(); undo(); }); Grid.SetColumn(l, 2); row.Children.Add(l); }
        _toast.Child = row; _toast.Visibility = Visibility.Visible;
        Fx.Move(_toast, 18, 0, 260, true);
        _toastTimer.Stop(); _toastTimer.Interval = TimeSpan.FromSeconds(undo != null ? 7 : 4); _toastTimer.Start();
    }
    void HideToast() => _toast.Visibility = Visibility.Collapsed;

    /* ---------------- avisos, diálogos, identidad ---------------- */
    /// <summary>Diálogo genérico. onPrimary devuelve false para dejarlo abierto (por ejemplo si falta un dato).</summary>
    public async Task<bool> Dialog(string title, UIElement content, string primary, Func<bool>? onPrimary = null, string? close = "Cancelar", string? secondary = null, Action? onSecondary = null, bool danger = false, string? glyph = null, string? sub = null)
    {
        await _dlg.WaitAsync();
        try
        {
            // mismo marco que el resto de la app: vidrio, borde fino, icono en recuadro junto al título y botones de la app
            var clean = CleanTitle(title);
            // título con icono en recuadro y, en Ajustes, una línea que explica para qué sirve
            var head = Hs(12, IconTile(glyph ?? DialogGlyph(clean), sub != null ? 32 : 36), V(1, H(clean, sub != null ? 17 : 19), sub != null ? Muted(sub, 12) : null));
            var d = new ContentDialog { Title = head, Content = new ScrollViewer { Content = content, MaxHeight = 560 }, PrimaryButtonText = primary, XamlRoot = _root.XamlRoot, DefaultButton = ContentDialogButton.None, RequestedTheme = Day ? ElementTheme.Light : ElementTheme.Dark, CornerRadius = new CornerRadius(22), FontFamily = Font,
                Background = CardBg, BorderBrush = Line2, BorderThickness = new Thickness(1),
                PrimaryButtonStyle = DialogBtn(true, danger), SecondaryButtonStyle = DialogBtn(false, false), CloseButtonStyle = DialogBtn(false, false) };
            // un solo fondo en toda la ventana (sin franja distinta en el pie)
            d.Resources["ContentDialogTopOverlay"] = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            d.Resources["ContentDialogBackground"] = CardBg;
            if (sub != null) d.Padding = new Thickness(20, 18, 20, 16);   // ventanas de Ajustes: más compactas
            if (close != null) d.CloseButtonText = close;
            if (secondary != null) d.SecondaryButtonText = secondary;
            d.PrimaryButtonClick += (_, a) => { if (onPrimary != null && !onPrimary()) a.Cancel = true; };
            var r = await d.ShowAsync();
            if (r == ContentDialogResult.Secondary) onSecondary?.Invoke();
            return r == ContentDialogResult.Primary;
        }
        finally { _dlg.Release(); }
    }
    public async void Ask(string title, string text, string ok, bool danger, Action onOk)
    {
        if (await Dialog(title, T(text, 14, false, Mut), ok, null, "Cancelar", null, null, danger)) onOk();
    }
    /// <summary>Botones del pie de las ventanas: principal en degradado azul (rojo si la acción es destructiva) y secundarios en vidrio.</summary>
    static Style DialogBtn(bool primary, bool danger)
    {
        var s = new Style(typeof(Button));
        if (BtnStyle != null) s.BasedOn = BtnStyle;
        s.Setters.Add(new Setter(Control.CornerRadiusProperty, new CornerRadius(14)));
        s.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, 46.0));
        s.Setters.Add(new Setter(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch));
        s.Setters.Add(new Setter(Control.FontFamilyProperty, Font));
        s.Setters.Add(new Setter(Control.FontWeightProperty, primary ? FontWeights.ExtraBold : FontWeights.Bold));
        s.Setters.Add(new Setter(Control.FontSizeProperty, primary ? 15.0 : 14.0));
        s.Setters.Add(new Setter(Control.BackgroundProperty, primary ? (danger ? Grad(Rose) : PrimaryBrush) : Tile));
        s.Setters.Add(new Setter(Control.ForegroundProperty, primary ? White : Text));
        s.Setters.Add(new Setter(Control.BorderBrushProperty, primary ? new SolidColorBrush(Microsoft.UI.Colors.Transparent) : Line2));
        s.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(primary ? 0 : 1)));
        return s;
    }
    /// <summary>Quita el emoji del principio del título (el icono va en su recuadro).</summary>
    static string CleanTitle(string t) { var i = 0; while (i < t.Length && !char.IsLetterOrDigit(t[i]) && t[i] != '¿' && t[i] != '¡') i++; return t[i..].Trim(); }
    static readonly (string K, string G)[] _dlgIcons = {
        ("eliminar", "\uE74D"), ("borrar", "\uE74D"), ("quitar", "\uE74D"), ("identidad", "\uE72E"), ("pin", "\uE72E"), ("contraseña", "\uE72E"), ("bloqueo", "\uE72E"),
        ("meta", "\uE7C1"), ("gasto", "\uE8EF"), ("pago", "\uE8C7"), ("nota", "\uE70B"), ("código", "\uE8D7"), ("recibo", "\uE8A5"), ("foto", "\uE722"),
        ("precio", "\uE804"), ("libra", "\uE804"), ("tránsito", "\uE804"), ("whatsapp", "\uE8BD"), ("mensaje", "\uE8BD"), ("negocio", "\uE719"), ("saludo", "\uE76E"),
        ("país", "\uE717"), ("notific", "\uEA8F"), ("tamaño", "\uE8D3"), ("sonido", "\uE767"), ("importar", "\uE896"), ("respaldo", "\uE74E"), ("restaurar", "\uE777"),
        ("sincron", "\uE895"), ("bot", "\uE895"), ("desconectar", "\uE895"), ("historial", "\uE81C"), ("actividad", "\uE81C"), ("fecha", "\uE787"), ("cliente", "\uE77B"),
        ("usuario", "\uE77B"), ("datos", "\uE77B"), ("estado", "\uE804") };
    static string DialogGlyph(string title) { var t = title.ToLowerInvariant(); foreach (var (k, g) in _dlgIcons) if (t.Contains(k)) return g; return "\uE946"; }
    public async void Info(string title, UIElement content) => await Dialog(title, content, "Cerrar", null, null);

    /// <summary>Pide PIN, contraseña o Windows Hello antes de una acción sensible.</summary>
    public async void RequireAuth(string reason, Action onOk, bool force = false)
    {
        var sec = Services.Security;
        if (!sec.NeedsAuth(force)) { onOk(); return; }
        if (sec.BioOn() && await Native.Hello(Hwnd, reason)) { sec.Ok("bio"); onOk(); return; }
        var usePin = sec.PinOn(); string val = ""; var msg = T("", 13, false, Rose);
        UIElement input = usePin ? new PasswordBox { Header = "PIN", MaxLength = 6 } : new PasswordBox { Header = "Contraseña" };
        ((PasswordBox)input).PasswordChanged += (s, _) => val = ((PasswordBox)s).Password;
        var ok = false;
        await Dialog("🔒 Confirma tu identidad", V(10, Muted(reason, 13), input, msg), "Confirmar", () =>
        {
            var w = sec.WaitMs(); if (w > 0) { msg.Text = "Demasiados intentos. Espera " + (w >= 60000 ? $"{w / 60000} min" : $"{w / 1000} s") + "."; return false; }
            var good = usePin ? sec.VerifyPin(val) : sec.CheckPassword(sec.UserName(), val);
            if (!good) { msg.Text = sec.Fail(usePin ? "pin" : "password"); return false; }
            sec.Ok(usePin ? "pin" : "password"); ok = true; return true;
        });
        if (ok) onOk();
    }

    public async void WhatsApp(string? phone, string? msg, string? who = null)
    {
        var link = Services.Core.WaLink(phone, msg);
        if (link == null) { Toast("Este cliente no tiene teléfono registrado."); return; }
        if (await Windows.System.Launcher.LaunchUriAsync(new Uri(link)) && who != null) Services.Core.Notified(who);
    }
    public async void OpenUrl(string url) { try { await Windows.System.Launcher.LaunchUriAsync(new Uri(url)); } catch { } }

    /// <summary>Corre un trabajo fuera del hilo de la interfaz mostrando "Cargando…"; luego vuelve con el resultado.</summary>
    public async Task<T> Busy<T>(string label, Func<T> work)
    {
        _busyText.Text = label; _busy.Visibility = Visibility.Visible;
        try { return await Task.Run(work); }
        finally { _busy.Visibility = Visibility.Collapsed; }
    }
    /// <summary>Igual que Busy pero en el hilo de sincronización (para operaciones de Telegram).</summary>
    public async Task<T> BusySync<T>(string label, Func<T> work)
    {
        _busyText.Text = label; _busy.Visibility = Visibility.Visible;
        try { return await Services.Sync.Run(work); }
        finally { _busy.Visibility = Visibility.Collapsed; }
    }
}
