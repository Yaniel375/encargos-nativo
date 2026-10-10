using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using static Encargos.Win.K;

namespace Encargos.Win;

/// <summary>Pantalla de entrada: crear acceso (primera vez), contraseña, PIN y Windows Hello.</summary>
public sealed class LockView : Grid
{
    readonly Action _onUnlocked;
    readonly StackPanel _box = new() { Spacing = 12, Width = 360, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    bool _setup, _usePass, _working;
    string _user = "", _pass = "", _pass2 = "", _pin = "", _msg = "";

    public LockView(Action onUnlocked)
    {
        _onUnlocked = onUnlocked; Background = new SolidColorBrush(Windows.UI.Color.FromArgb(70, 2, 6, 17));
        var sec = Services.Security; _setup = !sec.HasCredentials(); _usePass = !sec.PinOn(); _user = sec.UserName();
        Children.Add(new ScrollViewer { Content = _box, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        KeyDown += OnKey;
        Draw();
        Loaded += async (_, _) => { if (!_setup && sec.BioOn() && await Native.HelloAvailable()) TryHello(); };
    }

    bool _first = true, _granted;
    Border? _card;
    void Done(string method)
    {
        Services.Security.Ok(method); _granted = true; _msg = ""; Draw(); Sound.Login();
        var t = DispatcherQueue.CreateTimer(); t.Interval = TimeSpan.FromMilliseconds(750); t.IsRepeating = false; t.Tick += (_, _) => { t.Stop(); _onUnlocked(); }; t.Start();
    }
    void Fail(string m) { _msg = m; Draw(); if (_card != null) Fx.Shake(_card); }
    async void TryHello() { if (await Native.Hello(MainWindow.I.Hwnd, "Desbloquear Encargos")) Done("bio"); }

    static readonly SolidColorBrush GreenB = new(Windows.UI.Color.FromArgb(255, 0x34, 0xD3, 0x99));
    /// <summary>Campo del acceso (.lgF): icono redondo, etiqueta pequeña y entrada sin borde.</summary>
    static Border LgField(string glyph, string label, Control input)
    {
        input.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent); input.BorderThickness = new Thickness(0); input.Padding = new Thickness(0, 2, 0, 2); input.MinHeight = 0; input.FontFamily = Font; input.FontSize = 15; input.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        foreach (var k in new[] { "TextControlBackgroundFocused", "TextControlBackgroundPointerOver", "TextControlBorderBrushFocused", "TextControlBorderBrushPointerOver" }) input.Resources[k] = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        var ic = new Border { Width = 34, Height = 34, CornerRadius = new CornerRadius(17), Background = Br(0x0FFFFFFFu), BorderBrush = Br(0x1AFFFFFFu), BorderThickness = new Thickness(1), Child = new FontIcon { Glyph = glyph, FontSize = 13, Foreground = Br(0xFFCBD5E1u) } };
        var lab = T(label, 11, true, Mut, false); lab.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        var g = Lead(ic, V(0, lab, input)); g.ColumnSpacing = 10;
        var b = new Border { Child = g, CornerRadius = new CornerRadius(14), Background = Br(0x6E1E293Bu), BorderBrush = Line2, BorderThickness = new Thickness(1), Padding = new Thickness(10, 7, 10, 7) };
        input.GotFocus += (_, _) => b.BorderBrush = Alpha(Blue, .8); input.LostFocus += (_, _) => b.BorderBrush = Line2;
        return b;
    }

    void Draw()
    {
        var sec = Services.Security; _box.Children.Clear(); _box.Width = 360; _box.Spacing = 0;
        var pinMode = !_setup && !_usePass;
        var img = new Image { Width = 52, Height = 52 };
        try { img.Source = new BitmapImage(new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", "logo.png"))); } catch { }
        var logo = new Border { CornerRadius = new CornerRadius(15), BorderBrush = Br(0x5A60A5FAu), BorderThickness = new Thickness(1), Child = img, HorizontalAlignment = HorizontalAlignment.Center };
        _box.Children.Add(logo);
        var t = Big("Encargos", Br(0xFFE8EEF8u), 24); t.HorizontalAlignment = HorizontalAlignment.Center; t.Margin = new Thickness(0, 10, 0, 0);
        var sub = Muted("Tu negocio, en orden.", 13); sub.HorizontalAlignment = HorizontalAlignment.Center;
        var brand = V(0, t, sub); _box.Children.Add(brand);
        var chipC = _granted ? GreenB : BlueInk;
        var chip = new Border { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 12, 0, 14), CornerRadius = new CornerRadius(99), Background = Alpha(chipC, .10), BorderBrush = Alpha(chipC, .35), BorderThickness = new Thickness(1), Padding = new Thickness(12, 4, 12, 5),
            Child = Hs(6, new FontIcon { Glyph = _granted ? "\uE73E" : "\uE72E", FontSize = 11, Foreground = chipC }, T(_granted ? "Acceso autorizado" : "Acceso seguro", 12, true, chipC, false)) };
        _box.Children.Add(chip);

        // ----- tarjeta compacta y centrada -----
        var c = new StackPanel { Spacing = 10 };
        if (!pinMode)
        {
            var ring = new Border { Width = 46, Height = 46, CornerRadius = new CornerRadius(23), Background = Br(0xA00F172Au), BorderBrush = _granted ? GreenB : Br(0x963B82F6u), BorderThickness = new Thickness(1.5), HorizontalAlignment = HorizontalAlignment.Center,
                Child = new FontIcon { Glyph = _granted ? "\uE73E" : "\uE72E", FontSize = 18, Foreground = _granted ? GreenB : Br(0xFF60A5FAu) } };
            c.Children.Add(ring);
        }
        var title = T(_setup ? "Crea tu acceso" : "Hola, " + Services.Core.GreetName, 20, true, Br(0xFFF1F5F9u)); title.FontWeight = Microsoft.UI.Text.FontWeights.ExtraBold; title.TextAlignment = TextAlignment.Center; title.HorizontalAlignment = HorizontalAlignment.Center;
        // con PIN, el aviso de error ocupa el lugar de la instrucción (en rojo) para no alargar la tarjeta
        var pinErr = pinMode && _msg.Length > 0;
        var help = T(pinErr ? _msg : _setup ? "Elige un usuario y una contraseña para proteger tus datos. No hay contraseña por defecto." : pinMode ? "Escribe tu PIN para entrar." : "Escribe tu usuario y contraseña para entrar.", 12.5, pinErr, pinErr ? Rose : Mut);
        help.TextAlignment = TextAlignment.Center; help.HorizontalAlignment = HorizontalAlignment.Center;
        c.Children.Add(V(3, title, help));
        if (!pinMode)
        {
            var ub = new TextBox { Text = _user, PlaceholderText = "admin" }; ub.TextChanged += (_, _) => _user = ub.Text; Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(ub, "Usuario");
            c.Children.Add(LgField("\uE77B", "Usuario", ub));
            var pb = new PasswordBox { PlaceholderText = "Contraseña", Password = _pass }; pb.PasswordChanged += (_, _) => _pass = pb.Password; Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(pb, _setup ? "Contraseña (mínimo 6)" : "Contraseña");
            pb.KeyDown += (_, e) => { if (e.Key == Windows.System.VirtualKey.Enter && !_setup) Login(); };
            c.Children.Add(LgField("\uE72E", _setup ? "Contraseña (mínimo 6)" : "Contraseña", pb));
            if (_setup) { var pb2 = new PasswordBox { PlaceholderText = "Contraseña", Password = _pass2 }; pb2.PasswordChanged += (_, _) => _pass2 = pb2.Password; Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(pb2, "Repite la contraseña"); c.Children.Add(LgField("\uE72E", "Repite la contraseña", pb2)); }
            var go = Btn(_granted ? "Acceso" : _working ? "Verificando…" : _setup ? "Crear acceso" : "Iniciar sesión", () => { if (_granted || _working) return; if (_setup) Create(); else Login(); }, true, _granted ? GreenB : null, _granted ? "\uE73E" : "\uE8AD", true);
            go.MinHeight = 46; c.Children.Add(go);
            if (!_setup && sec.PinOn()) c.Children.Add(Center(Link("Entrar con PIN ›", () => { _usePass = false; _msg = ""; Draw(); })));
            if (!_setup && sec.BioOn()) c.Children.Add(Center(Link("Usar Windows Hello", TryHello)));
            if (_msg.Length > 0) { var m = T(_msg, 13, true, Rose); m.TextAlignment = TextAlignment.Center; m.HorizontalAlignment = HorizontalAlignment.Center; c.Children.Add(m); }
        }
        else
        {
            // cápsula con un punto por dígito (rojo si el PIN fue incorrecto, verde al entrar)
            var dots = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14 };
            for (var i = 0; i < sec.PinLen(); i++) dots.Children.Add(new Border { Width = 12, Height = 12, CornerRadius = new CornerRadius(6), BorderBrush = _granted ? GreenB : Alpha(Blue, .85), BorderThickness = new Thickness(1.5), Background = i < _pin.Length ? (_granted ? GreenB : Blue) : null });
            c.Children.Add(new Border { Child = dots, HorizontalAlignment = HorizontalAlignment.Center, CornerRadius = new CornerRadius(99), Padding = new Thickness(18, 12, 18, 12), Background = Br(0x960F172Au), BorderBrush = pinErr ? Alpha(Rose, .6) : Line2, BorderThickness = new Thickness(1) });
            var pad = new Grid { RowSpacing = 8, ColumnSpacing = 18, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 2, 0, 0) };
            for (var i = 0; i < 3; i++) pad.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            for (var i = 0; i < 4; i++) pad.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var keys = new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "left", "0", "del" };
            for (var i = 0; i < keys.Length; i++)
            {
                var k = keys[i]; Button b;
                if (k == "left")
                {
                    if (sec.BioOn()) { b = KeyBtn("\uE928", true); b.Click += (_, _) => TryHello(); ToolTipService.SetToolTip(b, "Windows Hello"); }
                    else { b = KeyBtn("\uE8D7", true); b.Background = null; b.BorderThickness = new Thickness(0); b.Click += (_, _) => { _usePass = true; _msg = ""; Draw(); }; ToolTipService.SetToolTip(b, "Entrar con contraseña"); }
                }
                else if (k == "del") { b = KeyBtn("\uE750", true); b.Background = null; b.BorderThickness = new Thickness(0); b.Click += (_, _) => { if (_pin.Length > 0) { _pin = _pin[..^1]; Draw(); } }; Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(b, "Borrar"); }
                else { b = KeyBtn(k, false); b.Click += (_, _) => Digit(k); Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(b, k); }
                Grid.SetRow(b, i / 3); Grid.SetColumn(b, i % 3); pad.Children.Add(b);
            }
            c.Children.Add(pad);
            c.Children.Add(new Border { Height = 1, Background = LineBr, Margin = new Thickness(8, 2, 8, 0) });
            c.Children.Add(Center(Link("Entrar con usuario y contraseña", () => { _usePass = true; _msg = ""; Draw(); })));
        }
        c.Children.Add(Center(Hs(6, new FontIcon { Glyph = "\uE83D", FontSize = 12, Foreground = Mut }, Muted("Tus datos se guardan solo en este equipo", 11.5))));
        _card = new Border { Child = c, CornerRadius = new CornerRadius(22), Padding = new Thickness(18, pinMode ? 16 : 20, 18, 16), BorderThickness = new Thickness(1), BorderBrush = _granted ? Alpha(GreenB, .75) : Line2,
            Background = new LinearGradientBrush { StartPoint = new Windows.Foundation.Point(.2, 0), EndPoint = new Windows.Foundation.Point(.8, 1), GradientStops = { new GradientStop { Color = Windows.UI.Color.FromArgb(214, 17, 24, 39), Offset = 0 }, new GradientStop { Color = Windows.UI.Color.FromArgb(224, 10, 15, 28), Offset = 1 } } } };
        _box.Children.Add(_card);
        var tag = V(10, new Border { Width = 22, Height = 3, CornerRadius = new CornerRadius(2), Background = Blue, HorizontalAlignment = HorizontalAlignment.Center }, Center(T("Encargos  •  Rápido  •  Seguro  •  Siempre contigo", 11, false, NavOff, false)));
        tag.Margin = new Thickness(0, 18, 0, 0); _box.Children.Add(tag);
        if (_first)
        {
            _first = false;
            Fx.Pop(logo, 250); Fx.Move(logo, 200, 890, 720); Fx.Move(brand, 190, 890, 720, true); Fx.Move(chip, 12, 1270, 420, true);
            Fx.Move(_card, 30, 1390, 620, true); Fx.Move(tag, 10, 1390, 620, true);
        }
        if (!_usePass && !_setup) Focus(FocusState.Programmatic);
    }
    static FrameworkElement Center(FrameworkElement e) { e.HorizontalAlignment = HorizontalAlignment.Center; return e; }
    static Button KeyBtn(string text, bool glyph)
    {
        var b = new Button { Width = 56, Height = 56, CornerRadius = new CornerRadius(28), Padding = new Thickness(0), Content = glyph ? (object)new FontIcon { Glyph = text, FontSize = 18 } : T(text, 20, true), Background = Glass, BorderBrush = Line2, BorderThickness = new Thickness(1) };
        if (BtnStyle != null) b.Style = BtnStyle;
        return Fx.Bounce(b, .9);
    }
    void OnKey(object sender, KeyRoutedEventArgs e)
    {
        if (_setup || _usePass) return;
        var k = (int)e.Key;
        if (k >= 48 && k <= 57) Digit(((char)k).ToString());
        else if (k >= 96 && k <= 105) Digit(((char)(k - 48)).ToString());
        else if (e.Key == Windows.System.VirtualKey.Back && _pin.Length > 0) { _pin = _pin[..^1]; Draw(); }
    }

    async void Digit(string d)
    {
        var sec = Services.Security;
        if (_working || _pin.Length >= sec.PinLen()) return;
        _pin += d; _msg = ""; Draw();
        if (_pin.Length < sec.PinLen()) return;
        var w = sec.WaitMs(); if (w > 0) { _msg = "Demasiados intentos. Espera " + (w >= 60000 ? $"{w / 60000} min" : $"{w / 1000} s") + "."; _pin = ""; Draw(); return; }
        _working = true; var pin = _pin; var ok = await Task.Run(() => sec.VerifyPin(pin)); _working = false;
        if (ok) Done("pin"); else { _pin = ""; Fail(sec.Fail("pin")); }
    }
    async void Login()
    {
        var sec = Services.Security; if (_working) return;
        var w = sec.WaitMs(); if (w > 0) { _msg = "Demasiados intentos. Espera " + (w >= 60000 ? $"{w / 60000} min" : $"{w / 1000} s") + "."; Draw(); return; }
        if (_user.Trim().Length == 0 || _pass.Length == 0) { Fail("Completa usuario y contraseña."); return; }
        _working = true; Draw(); var u = _user; var p = _pass;
        var ok = await Task.Run(() => sec.CheckPassword(u, p)); _working = false; _pass = "";
        if (ok) Done("password"); else Fail(sec.Fail("password"));
    }
    async void Create()
    {
        var sec = Services.Security; if (_working) return;
        if (_pass != _pass2) { Fail("Las contraseñas no coinciden."); return; }
        _working = true; Draw(); var u = _user; var p = _pass;
        var r = await Task.Run(() => sec.SetCredentials(u, p)); _working = false;
        if (r.Ok) { _setup = false; _usePass = true; Done("password"); } else Fail(r.Error);
    }
}
