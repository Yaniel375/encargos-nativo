using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;
using Windows.System;
using Windows.UI.Core;
using static Encargos.Win.K;

namespace Encargos.Win;

/// <summary>Pantalla y resolución (solo PC): tamaño de la ventana, resolución a medida y tamaño del contenido.
/// Se guarda solo en este equipo (no viaja en respaldos ni en la sincronización).</summary>
public sealed partial class MainWindow
{
    const string KFit = "encargos_win_fit", KSize = "encargos_win_size", KLast = "encargos_win_last";
    /// <summary>Tamaño de diseño de la ventana (en puntos de pantalla, sin la escala de Windows).</summary>
    const int DesignW = 1320, DesignH = 860, MinW = 800, MinH = 560;
    public static readonly double[] Zooms = { .75, .85, .9, 1, 1.1, 1.25, 1.5, 1.75, 2 };
    public static readonly (int W, int H)[] Presets = { (1024, 768), (1280, 720), (1280, 800), (1366, 768), (1440, 900), (1600, 900), (1680, 1050), (1920, 1080), (2560, 1440), (3840, 2160) };

    static string LsGet(string k) => Services.Core.Ls.Get(k, "");
    /// <summary>Tamaño del contenido elegido a mano (null = automático). "off" era el antiguo "Adaptar a la pantalla" apagado = 100 %.</summary>
    public static double? FitManual()
    {
        var v = LsGet(KFit);
        if (v == "off") return 1;
        return double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && d > 0 ? Math.Clamp(d, .5, 2.5) : null;
    }
    public static bool FitAuto() => FitManual() == null;
    /// <summary>Automático: más ancha → todo crece; más estrecha → se reduce un poco. A mano: el tamaño elegido.</summary>
    public static double FitFor(double w, double h) => FitManual() ?? Math.Clamp(Math.Min(w / (double)DesignW, h / 640.0), 0.85, 1.6);
    /// <summary>"" = recordar el último tamaño · "max" · "full" · "1600x900".</summary>
    public static string SizeMode() => LsGet(KSize);

    public void SetZoom(double? z)
    {
        Services.Core.Ls.Set(KFit, z == null ? "auto" : z.Value.ToString("0.##", CultureInfo.InvariantCulture));
        RefreshFit();
    }
    /// <summary>Ctrl + / Ctrl −: pasa al siguiente tamaño de la lista partiendo del actual.</summary>
    public void StepZoom(int dir)
    {
        var cur = FitManual() ?? Fit;
        var next = dir > 0 ? Zooms.Where(z => z > cur + .001).DefaultIfEmpty(Zooms[^1]).First() : Zooms.Where(z => z < cur - .001).DefaultIfEmpty(Zooms[0]).Last();
        SetZoom(next); Toast($"Tamaño del contenido: {Math.Round(next * 100)} %");
    }
    public static double NearestZoom(double f) => Zooms.OrderBy(z => Math.Abs(z - f)).First();

    [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr hwnd);
    /// <summary>Escala de Windows de la pantalla donde está la ventana (1 = 100 %, 1.25 = 125 %…).</summary>
    public double DpiScale() { try { var d = GetDpiForWindow(Hwnd); return d > 0 ? d / 96.0 : 1; } catch { return 1; } }
    DisplayArea Display() => DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest);
    /// <summary>Resolución de la pantalla donde está la ventana (píxeles reales).</summary>
    public (int W, int H) ScreenSize() { try { var b = Display().OuterBounds; return (b.Width, b.Height); } catch { return (0, 0); } }
    public (int W, int H) WindowSize() { try { return (AppWindow.Size.Width, AppWindow.Size.Height); } catch { return (0, 0); } }
    public bool IsFullScreen => AppWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen;
    public bool IsMaximized => AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Maximized };

    public static bool TryParseSize(string s, out int w, out int h)
    {
        w = h = 0; var p = s.Split('x');
        return p.Length == 2 && int.TryParse(p[0], out w) && int.TryParse(p[1], out h) && w > 0 && h > 0;
    }

    /// <summary>Último tamaño "normal" (sin maximizar) para recordarlo al cerrar.</summary>
    RectInt32? _normal;

    /// <summary>Al abrir: aplica el modo guardado. La autoprueba siempre abre a 1320×860 para que las capturas sean comparables.</summary>
    void InitWindowSize()
    {
        AppWindow.Changed += (_, e) =>
        {
            if ((e.DidSizeChange || e.DidPositionChange) && AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Restored })
                _normal = new RectInt32(AppWindow.Position.X, AppWindow.Position.Y, AppWindow.Size.Width, AppWindow.Size.Height);
        };
        AppWindow.Closing += (_, _) => SaveLast();
        _root.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(OnShortcut), true);
        if (Services.SelfTestDir != null) { try { AppWindow.Resize(new SizeInt32(DesignW, DesignH)); } catch { } return; }
        ApplyWindowMode(SizeMode(), false);
    }

    void SaveLast()
    {
        try
        {
            if (_normal is not { } r) return;
            Services.Core.Ls.Set(KLast, $"{r.X},{r.Y},{r.Width},{r.Height},{(IsMaximized ? 1 : 0)}");
        }
        catch { }
    }

    /// <summary>Aplica un tamaño de ventana. save=false: solo lo aplica (al abrir, o F11).</summary>
    public void ApplyWindowMode(string mode, bool save = true)
    {
        if (save) Services.Core.Ls.Set(KSize, mode);
        try
        {
            if (mode == "full") { AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen); return; }
            if (AppWindow.Presenter.Kind != AppWindowPresenterKind.Overlapped) AppWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
            var op = AppWindow.Presenter as OverlappedPresenter;
            if (mode == "max") { op?.Maximize(); return; }
            if (op?.State == OverlappedPresenterState.Maximized) op.Restore();
            if (TryParseSize(mode, out var w, out var h)) { Place(w, h); return; }
            RestoreLast(op);
        }
        catch (Exception e) { Services.Report(e); }
    }

    /// <summary>Recordar: como se cerró la última vez; la primera vez, el tamaño de diseño con la escala de Windows
    /// (si no cabe en la pantalla, se abre maximizada en lugar de salirse por abajo).</summary>
    void RestoreLast(OverlappedPresenter? op)
    {
        var p = LsGet(KLast).Split(',');
        if (p.Length == 5 && p.Take(4).All(x => int.TryParse(x, out _)))
        {
            var r = new RectInt32(int.Parse(p[0]), int.Parse(p[1]), int.Parse(p[2]), int.Parse(p[3]));
            Place(r.Width, r.Height, r.X, r.Y, DisplayArea.GetFromRect(r, DisplayAreaFallback.Nearest));
            if (p[4] == "1") op?.Maximize();
            return;
        }
        var s = DpiScale(); var wa = Display().WorkArea;
        int dw = (int)Math.Round(DesignW * s), dh = (int)Math.Round(DesignH * s);
        if (dw > wa.Width || dh > wa.Height) { Place(dw, dh); op?.Maximize(); } else Place(dw, dh);
    }

    /// <summary>Pone la ventana a w×h píxeles reales dentro del espacio libre de la pantalla (sin taparse con la barra de tareas).
    /// Sin posición: centrada.</summary>
    void Place(int w, int h, int? x = null, int? y = null, DisplayArea? area = null)
    {
        var wa = (area ?? Display()).WorkArea; var s = DpiScale();
        int minW = Math.Min((int)(MinW * s), wa.Width), minH = Math.Min((int)(MinH * s), wa.Height);
        w = Math.Clamp(w, minW, wa.Width); h = Math.Clamp(h, minH, wa.Height);
        var px = Math.Clamp(x ?? wa.X + (wa.Width - w) / 2, wa.X, wa.X + wa.Width - w);
        var py = Math.Clamp(y ?? wa.Y + (wa.Height - h) / 2, wa.Y, wa.Y + wa.Height - h);
        AppWindow.MoveAndResize(new RectInt32(px, py, w, h));
    }

    /// <summary>F11 pantalla completa (Esc para salir) · Ctrl + / Ctrl − tamaño del contenido · Ctrl 0 vuelve a 100 %.</summary>
    void OnShortcut(object sender, KeyRoutedEventArgs e)
    {
        try
        {
            if (e.Key == VirtualKey.F11) { ToggleFull(); e.Handled = true; return; }
            if (e.Key == VirtualKey.Escape && IsFullScreen && _overlay.Visibility != Visibility.Visible) { ExitFull(); e.Handled = true; return; }
            if (!InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down)) return;
            switch ((int)e.Key)
            {
                case (int)VirtualKey.Add: case 187: StepZoom(1); e.Handled = true; break;          // + (teclado numérico y tecla =/+)
                case (int)VirtualKey.Subtract: case 189: StepZoom(-1); e.Handled = true; break;    // −
                case (int)VirtualKey.Number0: case (int)VirtualKey.NumberPad0: SetZoom(1); Toast("Tamaño del contenido: 100 %"); e.Handled = true; break;
            }
        }
        catch (Exception ex) { Services.Report(ex); }
    }
    public void ToggleFull()
    {
        if (IsFullScreen) { ExitFull(); return; }
        ApplyWindowMode("full", false); Toast("Pantalla completa · pulsa F11 o Esc para salir");
    }
    void ExitFull()
    {
        var mode = SizeMode();
        if (mode == "full") { Services.Core.Ls.Set(KSize, ""); mode = ""; }   // salir de la pantalla completa guardada vuelve a "recordar"
        ApplyWindowMode(mode, false);
    }

    /* ---------------- autoprueba de los atajos (solo con ENCARGOS_SELFTEST) ---------------- */
    [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    static async Task Press(params byte[] keys)
    {
        foreach (var k in keys) keybd_event(k, 0, 0, UIntPtr.Zero);
        for (var i = keys.Length - 1; i >= 0; i--) keybd_event(keys[i], 0, 2, UIntPtr.Zero);
        await Task.Delay(900);
    }
    /// <summary>Pulsa F11, Esc, Ctrl + y Ctrl 0 como lo haría una persona. Primero sin preparar el foco (como al usar la app)
    /// y, si no responde, con el foco en el menú lateral para distinguir un fallo de la app de un fallo del equipo de prueba.</summary>
    async Task KeyTest(Action<string> L, Action fail)
    {
        SetZoom(null); Root(new HomePage()); Activate(); SetForegroundWindow(Hwnd); await Task.Delay(1200);
        async Task<bool> Round(string label)
        {
            var ok = true;
            await Press(0x7A); var full = IsFullScreen; L($"{label} F11 → pantalla completa: {(full ? "sí" : "no")}"); ok &= full;
            await Press(0x1B); var outF = !IsFullScreen; L($"{label} Esc → sale de pantalla completa: {(outF ? "sí" : "no")}"); ok &= outF;
            if (IsFullScreen) ExitFull();
            var before = Fit; await Press(0x11, 0xBB); var up = Fit > before + .001 && !FitAuto(); L($"{label} Ctrl + → contenido {before * 100:0} % → {Fit * 100:0} %"); ok &= up;
            await Press(0x11, 0x30); var one = Math.Abs(Fit - 1) < .001; L($"{label} Ctrl 0 → contenido {Fit * 100:0} %"); ok &= one;
            SetZoom(null); await Task.Delay(400);
            return ok;
        }
        if (await Round("teclado:")) { L("atajos de teclado: OK"); return; }
        if (_items.Count > 0) { _items.Values.First().B.Focus(FocusState.Programmatic); await Task.Delay(500); }
        if (await Round("teclado (con foco):")) { fail(); L("ERROR: los atajos solo responden si un botón tiene el foco"); }
        else L("(aviso) el equipo de prueba no acepta teclas simuladas; los atajos no se pudieron comprobar aquí");
    }

    /* ---------------- ventana de Ajustes: Pantalla y resolución ---------------- */
    /// <summary>Resumen para la fila de Ajustes.</summary>
    public string DisplaySummary()
    {
        var m = SizeMode();
        var win = m switch { "max" => "Maximizada", "full" => "Pantalla completa", _ when TryParseSize(m, out var w, out var h) => $"Ventana {w} × {h}", _ => "Recuerda el último tamaño" };
        var z = FitManual() is { } f ? $"contenido {Math.Round(f * 100)} %" : $"contenido automático ({Math.Round(Fit * 100)} %)";
        return win + " · " + z;
    }

    public async void DisplaySheet(Action? after = null)
    {
        var holder = new Border();
        string cw = "", ch = ""; var err = T("", 12.5, false, Rose);
        void Fill()
        {
            var (sw, sh) = ScreenSize(); var (ww, wh) = WindowSize(); var mode = SizeMode();
            var info = InfoBox($"Tu pantalla: {sw} × {sh} píxeles · escala de Windows {Math.Round(DpiScale() * 100)} % · la ventana mide ahora {ww} × {wh}.");
            UIElement Pick(string key, string glyph, string title, string sub) => PickRow(glyph, title, sub, mode == key, () => { ApplyWindowMode(key); Later(); });
            var modes = SettingsGroup(
                Pick("", "", "Recordar el último tamaño", "Se abre como la cerraste"),
                Pick("max", "\uE922", "Maximizada", "Ocupa la pantalla, con la barra de tareas visible"),
                Pick("full", "\uE740", "Pantalla completa", "Sin bordes ni barra de tareas · F11 o Esc para salir"));
            var fits = Presets.Where(p => sw == 0 || (p.W <= sw && p.H <= sh)).Select(p => ($"{p.W}x{p.H}", $"{p.W} × {p.H}"));
            var presets = Choice(fits, mode, k => { ApplyWindowMode(k); Later(); });
            if (cw.Length == 0 && TryParseSize(mode, out var mw, out var mh)) { cw = mw.ToString(); ch = mh.ToString(); }
            var custom = Cols(Box("Ancho (píxeles)", cw, v => cw = v, false, "1500", 5), Box("Alto (píxeles)", ch, v => ch = v, false, "850", 5),
                Btn("Aplicar", () =>
                {
                    int.TryParse(cw.Trim(), out var w); int.TryParse(ch.Trim(), out var h);
                    if (w < MinW || h < MinH) { err.Text = $"La medida mínima es {MinW} × {MinH} píxeles."; return; }
                    if (sw > 0 && (w > sw || h > sh)) { err.Text = $"Tu pantalla mide {sw} × {sh}: escribe una medida igual o menor."; return; }
                    err.Text = ""; ApplyWindowMode($"{w}x{h}"); Later();
                }, false, null, ""));
            var manual = FitManual();
            var auto = SettingsGroup(Toggle("Automático", manual == null ? $"Crece o se reduce con la ventana · ahora {Math.Round(Fit * 100)} %" : "Desactivado: usa el tamaño que elijas abajo", manual == null,
                v => { SetZoom(v ? null : NearestZoom(Fit)); Later(); }, ""));
            var zooms = Choice(Zooms.Select(z => (z.ToString("0.##", CultureInfo.InvariantCulture), $"{Math.Round(z * 100)} %")), manual?.ToString("0.##", CultureInfo.InvariantCulture) ?? "",
                k => { SetZoom(double.Parse(k, CultureInfo.InvariantCulture)); Later(); });
            holder.Child = V(10, info,
                T("Tamaño de la ventana", 12.5, true, Mut), modes,
                T("Resolución de la ventana (píxeles)", 12.5, true, Mut), presets, custom, err,
                T("Tamaño del contenido", 12.5, true, Mut), auto, zooms,
                Muted("Atajos: Ctrl + agranda, Ctrl − reduce, Ctrl 0 vuelve a 100 % y F11 pone la pantalla completa.", 12),
                Btn("Restablecer", () => { Services.Core.Ls.Set(KLast, ""); SetZoom(null); ApplyWindowMode(""); cw = ch = ""; err.Text = ""; Later(); }, false, null, ""));
        }
        // los cambios de tamaño llegan un instante después: se vuelve a dibujar para mostrar las medidas nuevas
        async void Later() { Fill(); await Task.Delay(400); Fill(); }
        Fill();
        await Dialog("Pantalla y resolución", holder, "Listo", null, null, glyph: "", sub: "Tamaño de la ventana y del contenido");
        after?.Invoke();
    }
}
