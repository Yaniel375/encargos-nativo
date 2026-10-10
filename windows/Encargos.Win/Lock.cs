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
        var ic = new Border { Width = 40, Height = 40, CornerRadius = new CornerRadius(20), Background = Br(0x0FFFFFFFu), BorderBrush = Br(0x1AFFFFFFu), BorderThickness = new Thickness(1), Child = new FontIcon { Glyph = glyph, FontSize = 15, Foreground = Br(0xFFCBD5E1u) } };
        var lab = T(label, 11.5, true, Mut, false); lab.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        var g = Lead(ic, V(0, lab, input)); g.ColumnSpacing = 12;
        var b = new Border { Child = g, CornerRadius = new CornerRadius(16), Background = Br(0x6E1E293Bu), BorderBrush = Line2, BorderThickness = new Thickness(1), Padding = new Thickness(10, 9, 10, 9) };
        input.GotFocus += (_, _) => b.BorderBrush = Alpha(Blue, .8); input.LostFocus += (_, _) => b.BorderBrush = Line2;
        return b;
    }

    void Draw()
    {
        var sec = Services.Security; _box.Children.Clear(); _box.Width = 420; _box.Spacing = 0;
        var img = new Image { Width = 64, Height = 64 };
        try { img.Source = new BitmapImage(new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", "logo.png"))); } catch { }
        var logo = new Border { CornerRadius = new CornerRadius(18), BorderBrush = Br(0x5A60A5FAu), BorderThickness = new Thickness(1), Child = img, HorizontalAlignment = HorizontalAlignment.Center };
        _box.Children.Add(logo);
        var t = Big("Encargos", Br(0xFFE8EEF8u), 28); t.HorizontalAlignment = HorizontalAlignment.Center; t.Margin = new Thickness(0, 12, 0, 0);
        var sub = Muted("Tu negocio, en orden.", 14); sub.HorizontalAlignment = HorizontalAlignment.Center;
        var brand = V(0, t, sub); _box.Children.Add(brand);
        var chipC = _granted ? GreenB : BlueInk;
        var chip = new Border { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 12, 0, 18), CornerRadius = new CornerRadius(99), Background = Alpha(chipC, .10), BorderBrush = Alpha(chipC, .35), BorderThickness = new Thickness(1), Padding = new Thickness(12, 4, 12, 5),
            Child = Hs(6, new FontIcon { Glyph = _granted ? "" : "", FontSize = 11, Foreground = chipC }, T(_granted ? "Acceso autorizado" : "Acceso seguro", 12, true, chipC, false)) };
        _box.Children.Add(chip);

        // ----- tarjeta -----
        var c = new StackPanel { Spacing = 12 };
        var ring = new Border { Width = 70, Height = 70, CornerRadius = new CornerRadius(35), Background = Br(0xA00F172Au), BorderBrush = _granted ? GreenB : Br(0x963B82F6u), BorderThickness = new Thickness(2),
            Child = new FontIcon { Glyph = _granted ? "" : "", FontSize = 24, Foreground = _granted ? GreenB : Br(0xFF60A5FAu) } };
        var hello = V(1, T(_setup ? "Bienvenido," : "Hola,", 16, false, Br(0xFFE2E8F0u)), Big(_setup ? "Crea tu acceso" : Services.Core.GreetName, null, 26),
            Muted(_setup ? "Elige un usuario y una contraseña para proteger tus datos. No hay contraseña por defecto." : !_usePass ? "Escribe tu PIN para entrar." : "Escribe tu usuario y contraseña para entrar.", 13));
        c.Children.Add(Lead(ring, hello));
        if (_setup || _usePass)
        {
            var ub = new TextBox { Text = _user, PlaceholderText = "admin" }; ub.TextChanged += (_, _) => _user = ub.Text; Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(ub, "Usuario");
            c.Children.Add(LgField("", "Usuario", ub));
            var pb = new PasswordBox { PlaceholderText = "Contraseña", Password = _pass }; pb.PasswordChanged += (_, _) => _pass = pb.Password; Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(pb, _setup ? "Contraseña (mínimo 6)" : "Contraseña");
            pb.KeyDown += (_, e) => { if (e.Key == Windows.System.VirtualKey.Enter && !_setup) Login(); };
            c.Children.Add(LgField("", _setup ? "Contraseña (mínimo 6)" : "Contraseña", pb));
            if (_setup) { var pb2 = new PasswordBox { PlaceholderText = "Contraseña", Password = _pass2 }; pb2.PasswordChanged += (_, _) => _pass2 = pb2.Password; Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(pb2, "Repite la contraseña"); c.Children.Add(LgField("", "Repite la contraseña", pb2)); }
            var go = Btn(_granted ? "Acceso" : _working ? "Verificando…" : _setup ? "Crear acceso" : "Iniciar sesión", () => { if (_granted || _working) return; if (_setup) Create(); else Login(); }, true, _granted ? GreenB : null, _granted ? "" : "", false);
            c.Children.Add(go);
            if (!_setup && sec.PinOn()) c.Children.Add(Link("Entrar con PIN ›", () => { _usePass = false; _msg = ""; Draw(); }));
            if (!_setup && sec.BioOn()) c.Children.Add(Link("Usar Windows Hello", TryHello));
        }
        else
        {
            var dots = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16, HorizontalAlignment = HorizontalAlignment.Center };
            for (var i = 0; i < sec.PinLen(); i++) dots.Children.Add(new Border { Width = 14, Height = 14, CornerRadius = new CornerRadius(7), BorderBrush = _granted ? GreenB : Blue, BorderThickness = new Thickness(1.5), Background = i < _pin.Length ? (_granted ? GreenB : Blue) : null });
            c.Children.Add(dots);
            var pad = new Grid { RowSpacing = 10, ColumnSpacing = 18, HorizontalAlignment = HorizontalAlignment.Center };
            for (var i = 0; i < 3; i++) pad.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            for (var i = 0; i < 4; i++) pad.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var keys = new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "hello", "0", "del" };
            for (var i = 0; i < keys.Length; i++)
            {
                var k = keys[i]; Button b;
                if (k == "hello") { if (!sec.BioOn()) continue; b = KeyBtn("", true); b.Click += (_, _) => TryHello(); ToolTipService.SetToolTip(b, "Windows Hello"); }
                else if (k == "del") { b = KeyBtn("", true); b.Click += (_, _) => { if (_pin.Length > 0) { _pin = _pin[..^1]; Draw(); } }; }
                else { b = KeyBtn(k, false); b.Click += (_, _) => Digit(k); }
                Grid.SetRow(b, i / 3); Grid.SetColumn(b, i % 3); pad.Children.Add(b);
            }
            c.Children.Add(pad);
            c.Children.Add(Center(Link("Entrar con usuario y contraseña", () => { _usePass = true; _msg = ""; Draw(); })));
        }
        if (_msg.Length > 0) c.Children.Add(T(_msg, 13, true, Rose));
        c.Children.Add(Hs(7, new FontIcon { Glyph = "", FontSize = 13, Foreground = Mut }, Muted("Tus datos se guardan solo en este equipo", 12)));
        _card = new Border { Child = c, CornerRadius = new CornerRadius(26), Padding = new Thickness(18), BorderThickness = new Thickness(1), BorderBrush = _granted ? Alpha(GreenB, .75) : Line2,
            Background = new LinearGradientBrush { StartPoint = new Windows.Foundation.Point(.2, 0), EndPoint = new Windows.Foundation.Point(.8, 1), GradientStops = { new GradientStop { Color = Windows.UI.Color.FromArgb(214, 17, 24, 39), Offset = 0 }, new GradientStop { Color = Windows.UI.Color.FromArgb(224, 10, 15, 28), Offset = 1 } } } };
        _box.Children.Add(_card);
        var tag = V(10, new Border { Width = 22, Height = 3, CornerRadius = new CornerRadius(2), Background = Blue, HorizontalAlignment = HorizontalAlignment.Center }, Center(T("Encargos  •  Rápido  •  Seguro  •  Siempre contigo", 11.5, false, NavOff, false)));
        tag.Margin = new Thickness(0, 34, 0, 0); _box.Children.Add(tag);
        if (_first)
        {
            _first = false;
            Fx.Pop(logo, 250); Fx.Move(logo, 200, 890, 720); Fx.Move(brand, 190, 890, 720, true); Fx.Move(chip, 12, 1270, 420, true);
            Fx.Move(_card, 30, 1390, 620, true); Fx.Move(tag, 10, 1390, 620, true);
        }
        if (!_usePass && !_setup) Focus(FocusState.Programmatic);
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
        _pin += d; Draw();
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
