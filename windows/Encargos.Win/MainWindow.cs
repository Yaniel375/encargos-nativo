using Encargos.Core;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
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
    public abstract void Build(StackPanel p);
    protected static MainWindow S => MainWindow.I;
    protected static EncargosApp Core => Services.Core;
    protected void Redraw() => S.Render(true);
}

public sealed class MainWindow : Window
{
    public static MainWindow I { get; private set; } = null!;
    public IntPtr Hwnd { get; }
    readonly Grid _root = new();
    readonly Grid _main = new();
    readonly NavigationView _nav = new();
    readonly ScrollViewer _scroll = new() { HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    readonly TextBlock _title = H("Inicio", 24);
    readonly TextBlock _syncText = Muted("");
    readonly InfoBar _toast = new() { IsClosable = true, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(16), MaxWidth = 640 };
    readonly Border _busy = new() { Visibility = Visibility.Collapsed, Background = Alpha(Br(0xFF000000u), 0.35) };
    readonly TextBlock _busyText = T("", 14, true);
    readonly List<Screen> _stack = new();
    readonly Dictionary<string, NavigationViewItem> _items = new();
    readonly DispatcherQueueTimer _renderTimer, _tick, _toastTimer;
    readonly SemaphoreSlim _dlg = new(1, 1);
    long _hiddenAt;
    bool _unlocked;
    public Screen Top => _stack[^1];

    public MainWindow()
    {
        I = this;
        Hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        Title = "Encargos";
        try { AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "icon.ico")); } catch { }
        try { AppWindow.Resize(new Windows.Graphics.SizeInt32(1240, 840)); } catch { }
        Services.Start(DispatcherQueue.GetForCurrentThread());
        _renderTimer = DispatcherQueue.CreateTimer(); _renderTimer.Interval = TimeSpan.FromMilliseconds(150); _renderTimer.IsRepeating = false; _renderTimer.Tick += (_, _) => Render(false);
        _tick = DispatcherQueue.CreateTimer(); _tick.Interval = TimeSpan.FromSeconds(45); _tick.IsRepeating = true; _tick.Tick += (_, _) => { if (_unlocked && Services.Sync.Enabled()) Services.Sync.SyncNow(); UpdateStatus(); };
        _toastTimer = DispatcherQueue.CreateTimer(); _toastTimer.IsRepeating = false; _toastTimer.Tick += (_, _) => _toast.IsOpen = false;

        _root.Background = Bg;
        BuildMain();
        _busy.Child = new Border { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Padding = new Thickness(24), CornerRadius = new CornerRadius(14), Background = CardBg, Child = Hs(12, new ProgressRing { IsActive = true, Width = 28, Height = 28 }, _busyText) };
        Content = _root;

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
            L(fails == 0 ? "RESULTADO: OK" : $"RESULTADO: {fails} error(es)");
        }
        catch (Exception e) { L("RESULTADO: ERROR " + e.GetType().Name + ": " + Services.Redact(e.Message) + "\n" + e.StackTrace); }
        await Task.Delay(500);
        Application.Current.Exit();
    }
    async Task Snap(string dir, string name, Action<string> L)
    {
        try
        {
            var rtb = new Microsoft.UI.Xaml.Media.Imaging.RenderTargetBitmap();
            await rtb.RenderAsync(_root);
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

    /* ---------------- estructura ---------------- */
    void BuildMain()
    {
        _nav.IsSettingsVisible = false; _nav.IsBackButtonVisible = NavigationViewBackButtonVisible.Visible; _nav.PaneDisplayMode = NavigationViewPaneDisplayMode.Left; _nav.OpenPaneLength = 240;
        _nav.PaneTitle = "Encargos";
        void Item(string key, string text, string glyph, bool footer = false)
        {
            var it = new NavigationViewItem { Content = text, Tag = key, Icon = new FontIcon { Glyph = glyph } };
            _items[key] = it; (footer ? _nav.FooterMenuItems : _nav.MenuItems).Add(it);
        }
        Item("home", "Inicio", ""); Item("clients", "Clientes", ""); Item("new", "Nuevo encargo", ""); Item("orders", "Encargos", "");
        Item("notes", "Notas", ""); Item("payments", "Pagos", ""); Item("pending", "Pendientes", ""); Item("stats", "Estadísticas", "");
        Item("attention", "Centro de atención", ""); Item("notifs", "Notificaciones", ""); Item("completed", "Completados", "");
        Item("backup", "Respaldo", "", true); Item("security", "Seguridad", "", true); Item("sync", "Sincronización", "", true); Item("settings", "Ajustes", "", true);
        _nav.ItemInvoked += (_, e) => { if (e.InvokedItemContainer?.Tag is string k) Root(PageFor(k)); };
        _nav.BackRequested += (_, _) => Back();

        var header = new Grid { Padding = new Thickness(28, 18, 28, 6) };
        header.Children.Add(V(2, _title, _syncText));
        var lockBtn = Btn("Bloquear", () => { Services.Security.Log(true, "Bloqueo manual"); ShowLock(); }, false, null, "", false);
        lockBtn.HorizontalAlignment = HorizontalAlignment.Right; lockBtn.VerticalAlignment = VerticalAlignment.Top; header.Children.Add(lockBtn);

        var body = new Grid();
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        body.Children.Add(header); Grid.SetRow(_scroll, 1); body.Children.Add(_scroll);
        _nav.Content = body;
        _main.Children.Add(_nav);
        _stack.Add(new HomePage());
    }

    public static Screen PageFor(string k) => k switch
    {
        "clients" => (Screen)new ClientsPage(), "new" => new NewOrderPage(), "orders" => new OrdersPage(), "notes" => new NotesPage(), "payments" => new PaymentsPage(),
        "pending" => new PendingPage(), "stats" => new StatsPage(), "attention" => new AttentionPage(), "notifs" => new NotifsPage(), "completed" => new CompletedPage(),
        "backup" => new BackupPage(), "security" => new SecurityPage(), "sync" => new SyncPage(), "settings" => new SettingsPage(), _ => new HomePage(),
    };

    /* ---------------- navegación ---------------- */
    public void Go(Screen p) { _stack.Add(p); Render(true, true); }
    public void Root(Screen p) { _stack.Clear(); _stack.Add(p); Render(true, true); }
    public void Replace(Screen p) { _stack.RemoveAt(_stack.Count - 1); _stack.Add(p); Render(true, true); }
    public void Back() { if (_stack.Count > 1) { _stack.RemoveAt(_stack.Count - 1); Render(true, true); } else if (Top is not HomePage) Root(new HomePage()); }
    public bool IsCurrent(Screen p) => _stack.Count > 0 && ReferenceEquals(Top, p);

    /// <summary>Dibuja la pantalla actual. force=false solo redibuja si la pantalla es "viva".</summary>
    public void Render(bool force, bool top = false)
    {
        if (!_unlocked) return;
        var p = Top; if (!force && !p.Live) { UpdateBadges(); return; }
        var off = _scroll.VerticalOffset;
        var panel = new StackPanel { Spacing = 12, Padding = new Thickness(28, 8, 28, 40), MaxWidth = 1000, HorizontalAlignment = HorizontalAlignment.Stretch };
        try { p.Build(panel); }
        catch (Exception e) { Services.Report(e); LastRenderError = e.GetType().Name + ": " + Services.Redact(e.Message); panel.Children.Clear(); panel.Children.Add(Card(T("No se pudo mostrar esta pantalla.", 15, true), Muted(Services.Redact(e.Message)), Btn("Ir a Inicio", () => Root(new HomePage()), true))); }
        _scroll.Content = panel;
        if (!top) { panel.UpdateLayout(); _scroll.ChangeView(null, off, null, true); } else _scroll.ChangeView(null, 0, null, true);
        _title.Text = p.Title;
        _nav.IsBackEnabled = _stack.Count > 1 || p is not HomePage;
        var key = p.Nav; _nav.SelectedItem = key.Length > 0 && _items.TryGetValue(key, out var it) ? it : null;
        UpdateBadges(); UpdateStatus();
    }
    void UpdateBadges()
    {
        try
        {
            var a = Services.Core.Attention.Badge(); var n = Services.Core.Notifs.UnreadCount();
            _items["attention"].InfoBadge = a > 0 ? new InfoBadge { Value = a } : null;
            _items["notifs"].InfoBadge = n > 0 ? new InfoBadge { Value = n } : null;
        }
        catch { }
    }
    void UpdateStatus()
    {
        var s = Services.Sync.GetStatus();
        _syncText.Text = s.Kind == "off" && Services.Sync.Cfg() == null ? "" : "☁️ " + s.Text;
        _syncText.Foreground = s.Kind switch { "ok" => Green, "error" => Rose, "offline" or "pending" => Amber, _ => Mut };
    }

    /* ---------------- bloqueo ---------------- */
    void ShowLock()
    {
        _unlocked = false; Services.Unlocked = false; Services.Security.Forget();
        _root.Children.Clear();
        _root.Children.Add(new LockView(() => Unlock(true)));
        AddOverlays();
    }
    void Unlock(bool fromLock)
    {
        _unlocked = true; Services.Unlocked = true;
        _root.Children.Clear(); _root.Children.Add(_main); AddOverlays();
        Render(true, true);
        AfterUnlock();
    }
    void AddOverlays() { _root.Children.Add(_busy); _root.Children.Add(_toast); }
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

    /* ---------------- avisos, diálogos, identidad ---------------- */
    public void Toast(string msg, Action? undo = null)
    {
        _toast.Message = msg; _toast.Severity = InfoBarSeverity.Informational;
        if (undo != null) { var b = new Button { Content = "Deshacer" }; b.Click += (_, _) => { _toast.IsOpen = false; undo(); }; _toast.ActionButton = b; } else _toast.ActionButton = null;
        _toast.IsOpen = true; _toastTimer.Stop(); _toastTimer.Interval = TimeSpan.FromSeconds(undo != null ? 7 : 4); _toastTimer.Start();
    }

    /// <summary>Diálogo genérico. onPrimary devuelve false para dejarlo abierto (por ejemplo si falta un dato).</summary>
    public async Task<bool> Dialog(string title, UIElement content, string primary, Func<bool>? onPrimary = null, string? close = "Cancelar", string? secondary = null, Action? onSecondary = null)
    {
        await _dlg.WaitAsync();
        try
        {
            var d = new ContentDialog { Title = title, Content = new ScrollViewer { Content = content, MaxHeight = 560 }, PrimaryButtonText = primary, XamlRoot = _root.XamlRoot, DefaultButton = ContentDialogButton.Primary, RequestedTheme = Day ? ElementTheme.Light : ElementTheme.Dark };
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
        if (await Dialog(title, T(text, 14), ok)) onOk();
    }
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
