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

public sealed class MainWindow : Window
{
    public static MainWindow I { get; private set; } = null!;
    public IntPtr Hwnd { get; }
    readonly Grid _root = new();
    readonly Grid _host = new();
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
        try { AppWindow.Resize(new Windows.Graphics.SizeInt32(1320, 860)); } catch { }
        try { if (AppWindow.TitleBar is { } tb && AppWindowTitleBar.IsCustomizationSupported()) { tb.BackgroundColor = Windows.UI.Color.FromArgb(255, 6, 10, 20); tb.ButtonBackgroundColor = Windows.UI.Color.FromArgb(255, 6, 10, 20); tb.ForegroundColor = Windows.UI.Color.FromArgb(255, 226, 232, 240); tb.ButtonForegroundColor = Windows.UI.Color.FromArgb(255, 226, 232, 240); tb.InactiveBackgroundColor = Windows.UI.Color.FromArgb(255, 6, 10, 20); tb.ButtonInactiveBackgroundColor = Windows.UI.Color.FromArgb(255, 6, 10, 20); } } catch { }
        Services.Start(DispatcherQueue.GetForCurrentThread());
        _renderTimer = DispatcherQueue.CreateTimer(); _renderTimer.Interval = TimeSpan.FromMilliseconds(150); _renderTimer.IsRepeating = false; _renderTimer.Tick += (_, _) => Render(false);
        _tick = DispatcherQueue.CreateTimer(); _tick.Interval = TimeSpan.FromSeconds(45); _tick.IsRepeating = true; _tick.Tick += (_, _) => { if (_unlocked && Services.Sync.Enabled()) Services.Sync.SyncNow(); UpdateStatus(); };
        _toastTimer = DispatcherQueue.CreateTimer(); _toastTimer.IsRepeating = false; _toastTimer.Tick += (_, _) => HideToast();

        K.Day = false; _root.RequestedTheme = ElementTheme.Dark; _root.Background = Bg;
        _root.Children.Add(MakeBg(false));
        _root.Children.Add(_host);
        _stack.Add(new HomePage());
        BuildMain();
        _busy.Child = new Border { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Padding = new Thickness(24), CornerRadius = new CornerRadius(18), Background = CardBg, BorderBrush = Line2, BorderThickness = new Thickness(1), Child = Hs(12, new ProgressRing { IsActive = true, Width = 28, Height = 28 }, _busyText) };
        _toast.Background = CardBg; _toast.BorderBrush = Line2; _toast.BorderThickness = new Thickness(1);
        _root.Children.Add(_busy); _root.Children.Add(_toast);
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
        _main.Children.Clear(); _main.ColumnDefinitions.Clear(); _side.Children.Clear(); _items.Clear();
        _title = H("Inicio", 18); _syncText = Muted("", 11.5);
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
        var titleRow = Hs(10, back, V(0, _title, _syncText)); bar.Children.Add(titleRow);
        var attn = HeadBtn("", "Centro de atención", 0, 0xFFF43F5Eu, () => Go(new AttentionPage())); Grid.SetColumn(attn, 1); bar.Children.Add(attn);
        var bell = HeadBtn("", "Notificaciones", 1, 0xFF3B82F6u, () => Go(new NotifsPage())); Grid.SetColumn(bell, 2); bar.Children.Add(bell);
        // chip de perfil con menú
        var av = new Border { Width = 30, Height = 30, CornerRadius = new CornerRadius(10), Background = AvatarBg, BorderBrush = Alpha(Blue, .45), BorderThickness = new Thickness(1), Child = new TextBlock { Text = (Services.Core.GreetName.Trim() + "E")[..1].ToUpperInvariant(), FontFamily = Font, FontWeight = FontWeights.ExtraBold, FontSize = 14, Foreground = Text, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
        var who = V(0, T("Administrador", 12.5, true, null, false), Muted(Services.Security.UserName().IfEmpty("—"), 10.5));
        var chip = new Button { Content = Hs(8, av, who, new FontIcon { Glyph = "", FontSize = 11, Foreground = Mut }), Padding = new Thickness(4, 4, 10, 4), CornerRadius = new CornerRadius(14), Background = BtnBg, BorderBrush = BtnLine, BorderThickness = new Thickness(1) };
        if (BtnStyle != null) chip.Style = BtnStyle;
        var menu = new MenuFlyout();
        foreach (var (k, l) in new[] { ("payments", "Registro de pagos"), ("pending", "Pendientes"), ("stats", "Estadísticas"), ("completed", "Encargos completados"), ("backup", "Respaldo"), ("security", "Seguridad"), ("sync", "Sincronización"), ("settings", "Ajustes") })
        { var mi = new MenuFlyoutItem { Text = l, FontFamily = Font }; var key = k; mi.Click += (_, _) => Go(PageFor(key)); menu.Items.Add(mi); }
        menu.Items.Add(new MenuFlyoutSeparator());
        var lockItem = new MenuFlyoutItem { Text = "Bloquear ahora", Foreground = Rose, FontFamily = Font }; lockItem.Click += (_, _) => { Services.Security.Log(true, "Bloqueo manual"); ShowLock(); }; menu.Items.Add(lockItem);
        chip.Flyout = menu; Grid.SetColumn(chip, 3); bar.Children.Add(Fx.Bounce(chip, .96));
        var header = new Border { Child = bar, Margin = new Thickness(16, 14, 16, 6), Padding = new Thickness(16, 9, 10, 9), CornerRadius = new CornerRadius(20), Background = Br(Day ? 0xE6FFFFFFu : 0xC7090D18u), BorderBrush = LineBr, BorderThickness = new Thickness(1) };

        var body = new Grid();
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        body.Children.Add(header); Grid.SetRow(_scroll, 1); body.Children.Add(_scroll);
        Grid.SetColumn(body, 1); _main.Children.Add(body);
    }
    Button? _back;
    Button HeadBtn(string glyph, string desc, int badgeIdx, uint badgeColor, Action onClick)
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
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, -6, -6, 0), Visibility = Visibility.Collapsed };
            g.Children.Add(bd); _topBadges[badgeIdx] = (bd, t);
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
    public void Go(Screen p) { _stack.Add(p); Render(true, true); }
    public void Root(Screen p) { _stack.Clear(); _stack.Add(p); Render(true, true); }
    public void Replace(Screen p) { _stack.RemoveAt(_stack.Count - 1); _stack.Add(p); Render(true, true); }
    public void Back() { if (_stack.Count > 1) { _stack.RemoveAt(_stack.Count - 1); Render(true, true); } else if (Top is not HomePage) Root(new HomePage()); }
    public bool IsCurrent(Screen p) => _stack.Count > 0 && ReferenceEquals(Top, p);

    /// <summary>Dibuja la pantalla actual. force=false solo redibuja si la pantalla es "viva". Al navegar, las tarjetas entran escalonadas (aUp).</summary>
    public void Render(bool force, bool top = false)
    {
        if (!_unlocked) return;
        var p = Top; if (!force && !p.Live) { UpdateBadges(); return; }
        var off = _scroll.VerticalOffset;
        var panel = new StackPanel { Spacing = 12, Padding = new Thickness(28, 10, 28, 48), MaxWidth = 1080, HorizontalAlignment = HorizontalAlignment.Stretch };
        try
        {
            if (p.ShowTitle)
            {
                var big = H(p.Title, 24); big.CharacterSpacing = -10;
                var left = V(2, big, p.Sub != null ? Muted(p.Sub, 13) : null);
                panel.Children.Add(p.Right != null ? Row(left, T(p.Right, 12.5, true, Mut, false)) : left);
            }
            p.Build(panel);
        }
        catch (Exception e) { Services.Report(e); LastRenderError = e.GetType().Name + ": " + Services.Redact(e.Message); panel.Children.Clear(); panel.Children.Add(Card(T("No se pudo mostrar esta pantalla.", 15, true), Muted(Services.Redact(e.Message)), Btn("Ir a Inicio", () => Root(new HomePage()), true))); }
        _scroll.Content = panel;
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
    void UpdateStatus()
    {
        var s = Services.Sync.GetStatus();
        _syncText.Text = s.Kind == "off" && Services.Sync.Cfg() == null ? "Tu negocio, en orden" : "☁️ " + s.Text;
        _syncText.Foreground = s.Kind switch { "ok" => Green, "error" => Rose, "offline" or "pending" => Amber, _ => Mut };
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
        var tx = T(ok ? msg[1..].Trim() : msg, 13.5, true); tx.FontWeight = FontWeights.SemiBold; tx.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(tx, 1); row.Children.Add(tx);
        if (undo != null) { var l = Link("Deshacer", () => { HideToast(); undo(); }); Grid.SetColumn(l, 2); row.Children.Add(l); }
        _toast.Child = row; _toast.Visibility = Visibility.Visible;
        Fx.Move(_toast, 18, 0, 260, true);
        _toastTimer.Stop(); _toastTimer.Interval = TimeSpan.FromSeconds(undo != null ? 7 : 4); _toastTimer.Start();
    }
    void HideToast() => _toast.Visibility = Visibility.Collapsed;

    /* ---------------- avisos, diálogos, identidad ---------------- */
    /// <summary>Diálogo genérico. onPrimary devuelve false para dejarlo abierto (por ejemplo si falta un dato).</summary>
    public async Task<bool> Dialog(string title, UIElement content, string primary, Func<bool>? onPrimary = null, string? close = "Cancelar", string? secondary = null, Action? onSecondary = null)
    {
        await _dlg.WaitAsync();
        try
        {
            var d = new ContentDialog { Title = H(title, 19), Content = new ScrollViewer { Content = content, MaxHeight = 560 }, PrimaryButtonText = primary, XamlRoot = _root.XamlRoot, DefaultButton = ContentDialogButton.Primary, RequestedTheme = Day ? ElementTheme.Light : ElementTheme.Dark, CornerRadius = new CornerRadius(22), FontFamily = Font };
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
