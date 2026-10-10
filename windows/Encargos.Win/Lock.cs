using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
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
        _onUnlocked = onUnlocked; Background = Bg;
        var sec = Services.Security; _setup = !sec.HasCredentials(); _usePass = !sec.PinOn(); _user = sec.UserName();
        Children.Add(new ScrollViewer { Content = _box, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        KeyDown += OnKey;
        Draw();
        Loaded += async (_, _) => { if (!_setup && sec.BioOn() && await Native.HelloAvailable()) TryHello(); };
    }

    void Done(string method) { Services.Security.Ok(method); _onUnlocked(); }
    async void TryHello() { if (await Native.Hello(MainWindow.I.Hwnd, "Desbloquear Encargos")) Done("bio"); }

    void Draw()
    {
        var sec = Services.Security; _box.Children.Clear();
        var img = new Image { Width = 84, Height = 84, HorizontalAlignment = HorizontalAlignment.Center };
        try { img.Source = new BitmapImage(new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", "logo.png"))); } catch { }
        _box.Children.Add(img);
        var t = Big("Encargos", null, 28); t.HorizontalAlignment = HorizontalAlignment.Center; _box.Children.Add(t);
        var s = Muted(_setup ? "Crea tu acceso" : "Hola, " + Services.Core.GreetName, 14); s.HorizontalAlignment = HorizontalAlignment.Center; _box.Children.Add(s);
        if (_setup)
        {
            _box.Children.Add(Muted("Elige un usuario y una contraseña para proteger tus datos. No hay contraseña por defecto.", 13));
            _box.Children.Add(Box("Usuario", _user, v => _user = v));
            _box.Children.Add(Pass("Contraseña (mínimo 6)", v => _pass = v));
            _box.Children.Add(Pass("Repite la contraseña", v => _pass2 = v));
            _box.Children.Add(Btn(_working ? "Guardando…" : "Crear acceso", Create, true));
        }
        else if (!_usePass)
        {
            _box.Children.Add(Center(Muted("Escribe tu PIN", 14)));
            var dots = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14, HorizontalAlignment = HorizontalAlignment.Center };
            for (var i = 0; i < sec.PinLen(); i++) dots.Children.Add(new Border { Width = 14, Height = 14, CornerRadius = new CornerRadius(7), BorderBrush = Blue, BorderThickness = new Thickness(1.5), Background = i < _pin.Length ? Blue : null });
            _box.Children.Add(dots);
            var pad = new Grid { RowSpacing = 10, ColumnSpacing = 18, HorizontalAlignment = HorizontalAlignment.Center };
            for (var c = 0; c < 3; c++) pad.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            for (var r = 0; r < 4; r++) pad.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            string[] keys = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "hello", "0", "del" };
            for (var i = 0; i < keys.Length; i++)
            {
                var k = keys[i]; Button b;
                if (k == "hello") { if (!sec.BioOn()) continue; b = KeyBtn("", true); b.Click += (_, _) => TryHello(); ToolTipService.SetToolTip(b, "Windows Hello"); }
                else if (k == "del") { b = KeyBtn("", true); b.Click += (_, _) => { if (_pin.Length > 0) { _pin = _pin[..^1]; Draw(); } }; }
                else { b = KeyBtn(k, false); b.Click += (_, _) => Digit(k); }
                Grid.SetRow(b, i / 3); Grid.SetColumn(b, i % 3); pad.Children.Add(b);
            }
            _box.Children.Add(pad);
            _box.Children.Add(Center(Link("Entrar con usuario y contraseña", () => { _usePass = true; _msg = ""; Draw(); })));
        }
        else
        {
            _box.Children.Add(Box("Usuario", _user, v => _user = v));
            var pb = Pass("Contraseña", v => _pass = v);
            pb.KeyDown += (_, e) => { if (e.Key == Windows.System.VirtualKey.Enter) Login(); };
            _box.Children.Add(pb);
            _box.Children.Add(Btn(_working ? "Verificando…" : "Iniciar sesión", Login, true));
            if (sec.PinOn()) _box.Children.Add(Center(Link("Entrar con PIN", () => { _usePass = false; _msg = ""; Draw(); })));
            if (sec.BioOn()) _box.Children.Add(Center(Link("Usar Windows Hello", TryHello)));
        }
        if (_msg.Length > 0) _box.Children.Add(Center(T(_msg, 13, false, Rose)));
        _box.Children.Add(Center(Muted("Encargos · Rápido · Seguro · Siempre contigo", 11)));
        if (!_usePass && !_setup) Focus(FocusState.Programmatic);
    }
    static FrameworkElement Center(FrameworkElement e) { e.HorizontalAlignment = HorizontalAlignment.Center; return e; }
    static Button KeyBtn(string text, bool glyph)
    {
        var b = new Button { Width = 68, Height = 68, CornerRadius = new CornerRadius(34), Content = glyph ? (object)new FontIcon { Glyph = text, FontSize = 20 } : T(text, 22, true) };
        return b;
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
        if (ok) Done("pin"); else { _msg = sec.Fail("pin"); _pin = ""; Draw(); }
    }
    async void Login()
    {
        var sec = Services.Security; if (_working) return;
        var w = sec.WaitMs(); if (w > 0) { _msg = "Demasiados intentos. Espera " + (w >= 60000 ? $"{w / 60000} min" : $"{w / 1000} s") + "."; Draw(); return; }
        if (_user.Trim().Length == 0 || _pass.Length == 0) { _msg = "Completa usuario y contraseña."; Draw(); return; }
        _working = true; Draw(); var u = _user; var p = _pass;
        var ok = await Task.Run(() => sec.CheckPassword(u, p)); _working = false; _pass = "";
        if (ok) Done("password"); else { _msg = sec.Fail("password"); Draw(); }
    }
    async void Create()
    {
        var sec = Services.Security; if (_working) return;
        if (_pass != _pass2) { _msg = "Las contraseñas no coinciden."; Draw(); return; }
        _working = true; Draw(); var u = _user; var p = _pass;
        var r = await Task.Run(() => sec.SetCredentials(u, p)); _working = false;
        if (r.Ok) { _setup = false; _usePass = true; Done("password"); } else { _msg = r.Error; Draw(); }
    }
}
