using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Color = Windows.UI.Color;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace Encargos.Win;

/// <summary>
/// Fondo "aurora" de la app web: imagen con deriva lenta (48 s), haces de luz que recorren
/// las líneas, pulso azul (10 s) y chispas que suben y se apagan.
/// </summary>
public sealed class AuroraBg : Grid
{
    static readonly (string D, int Dur, int Delay)[] Runs =
    {
        ("M380 0 L622 248 C682 318 724 360 841 381", 19000, 0), ("M652 292 C672 342 662 418 700 488 L841 640", 23000, -6000),
        ("M688 556 L841 712", 13000, -3000), ("M0 192 L174 0", 15000, -9000), ("M0 1160 L242 1398", 17000, -4000),
        ("M0 1394 C108 1428 186 1448 236 1494 C336 1592 430 1704 604 1748 C702 1768 772 1772 841 1812", 26000, -12000),
        ("M604 1748 L841 1514", 18000, -14000),
    };
    static readonly (double X, double Y, int Dur, int Delay, double S)[] Dots =
    {
        (.45, .25, 15000, -10000, 2), (.72, .18, 14000, -9000, 2.5), (.31, .10, 10000, -6000, 2), (.34, .17, 15000, 0, 2.5), (.32, .86, 9000, -9000, 2.5),
        (.10, .34, 9000, -8000, 2), (.57, .24, 10000, -9000, 2.5), (.91, .29, 10000, -9000, 3), (.28, .53, 10000, -8000, 2.5), (.76, .13, 12000, -7000, 3),
        (.58, .46, 16000, -9000, 2), (.42, .37, 11000, -11000, 2), (.77, .44, 16000, -14000, 2.5), (.61, .42, 10000, -1000, 2.5),
    };
    readonly Canvas _dots = new() { IsHitTestVisible = false };
    readonly List<(Ellipse E, double X, double Y)> _dotEls = new();

    public AuroraBg()
    {
        IsHitTestVisible = false;
        Background = new SolidColorBrush(Color.FromArgb(255, 2, 6, 17));
        // ----- escena: imagen + líneas, con la misma deriva -----
        var scene = new Grid { RenderTransformOrigin = new Point(.5, .5) };
        var tr = new CompositeTransform { ScaleX = 1.04, ScaleY = 1.04 }; scene.RenderTransform = tr;
        try { scene.Children.Add(new Image { Source = new BitmapImage(new Uri(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "aurora_bg.jpg"))), Stretch = Stretch.UniformToFill, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }); } catch { }
        var cv = new Canvas { Width = 841, Height = 1870 };
        var sb = new Storyboard();
        foreach (var (d, dur, delay) in Runs)
        {
            var geo = Geo(d, out var len);
            foreach (var (th, a) in new[] { (12.0, 0x22), (6.0, 0x50), (3.0, 0xD9) })
            {
                var seg = len * 0.06; var p = new Path { Data = Geo(d, out _), Stroke = new SolidColorBrush(Color.FromArgb((byte)a, 0x8F, 0xD0, 0xFF)), StrokeThickness = th,
                    StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeDashCap = PenLineCap.Round,
                    StrokeDashArray = new DoubleCollection { seg / th, (len - seg) / th } };
                // recorrido continuo: offset len→0 a velocidad constante, empezando en la fase de la web (retardo negativo)
                var full = len / th; var phase = ((-delay) % dur) / (double)dur; var v0 = full * (1 - phase); var tA = (1 - phase) * dur;
                var k = new DoubleAnimationUsingKeyFrames { RepeatBehavior = RepeatBehavior.Forever, EnableDependentAnimation = true };
                k.KeyFrames.Add(new LinearDoubleKeyFrame { KeyTime = TimeSpan.Zero, Value = v0 });
                k.KeyFrames.Add(new LinearDoubleKeyFrame { KeyTime = TimeSpan.FromMilliseconds(tA), Value = 0 });
                k.KeyFrames.Add(new DiscreteDoubleKeyFrame { KeyTime = TimeSpan.FromMilliseconds(Math.Min(dur, tA + 1)), Value = full });
                k.KeyFrames.Add(new LinearDoubleKeyFrame { KeyTime = TimeSpan.FromMilliseconds(dur), Value = v0 });
                Storyboard.SetTarget(k, p); Storyboard.SetTargetProperty(k, "StrokeDashOffset"); sb.Children.Add(k);
                cv.Children.Add(p);
            }
            _ = geo;
        }
        scene.Children.Add(new Viewbox { Stretch = Stretch.UniformToFill, Child = cv, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
        Children.Add(scene);
        sb.Children.Add(Fx.Anim(tr, "ScaleX", 1.04, 1.075, 48000, 0, ease: new SineEase { EasingMode = EasingMode.EaseInOut }, autoReverse: true, forever: true));
        sb.Children.Add(Fx.Anim(tr, "ScaleY", 1.04, 1.075, 48000, 0, ease: new SineEase { EasingMode = EasingMode.EaseInOut }, autoReverse: true, forever: true));
        sb.Children.Add(Fx.Anim(tr, "TranslateX", 0, -16, 48000, 0, ease: new SineEase { EasingMode = EasingMode.EaseInOut }, autoReverse: true, forever: true));
        sb.Children.Add(Fx.Anim(tr, "TranslateY", 0, 9, 48000, 0, ease: new SineEase { EasingMode = EasingMode.EaseInOut }, autoReverse: true, forever: true));

        // ----- pulso azul -----
        var pulse = new Grid { Opacity = .3 };
        pulse.Children.Add(new Rectangle { Fill = Blob(.78, .14, .55, .35, 0.22) });
        pulse.Children.Add(new Rectangle { Fill = Blob(.18, .86, .60, .38, 0.20) });
        Children.Add(pulse);
        sb.Children.Add(Fx.Anim(pulse, "Opacity", .3, .85, 5000, 0, ease: new SineEase { EasingMode = EasingMode.EaseInOut }, autoReverse: true, forever: true));

        // ----- chispas -----
        foreach (var (x, y, dur, delay, s) in Dots)
        {
            var e = new Ellipse { Width = s * 3, Height = s * 3, Opacity = 0, RenderTransformOrigin = new Point(.5, .5),
                Fill = new RadialGradientBrush { GradientStops = { new GradientStop { Color = Color.FromArgb(255, 0x9C, 0xCB, 0xFF), Offset = 0 }, new GradientStop { Color = Color.FromArgb(255, 0x9C, 0xCB, 0xFF), Offset = .32 }, new GradientStop { Color = Color.FromArgb(0, 96, 165, 250), Offset = 1 } } } };
            var t = new CompositeTransform(); e.RenderTransform = t;
            _dotEls.Add((e, x, y)); _dots.Children.Add(e);
            var begin = TimeSpan.FromMilliseconds(((dur + delay) % dur + dur) % dur);
            var op = new DoubleAnimationUsingKeyFrames { RepeatBehavior = RepeatBehavior.Forever, BeginTime = begin };
            op.KeyFrames.Add(new LinearDoubleKeyFrame { KeyTime = TimeSpan.Zero, Value = 0 }); op.KeyFrames.Add(new LinearDoubleKeyFrame { KeyTime = TimeSpan.FromMilliseconds(dur * .3), Value = .75 });
            op.KeyFrames.Add(new LinearDoubleKeyFrame { KeyTime = TimeSpan.FromMilliseconds(dur * .6), Value = .35 }); op.KeyFrames.Add(new LinearDoubleKeyFrame { KeyTime = TimeSpan.FromMilliseconds(dur), Value = 0 });
            Storyboard.SetTarget(op, e); Storyboard.SetTargetProperty(op, "Opacity"); sb.Children.Add(op);
            var ty = Fx.Anim(t, "TranslateY", 0, -26, dur, 0, forever: true); ty.BeginTime = begin; sb.Children.Add(ty);
            var sx = Fx.Anim(t, "ScaleX", .6, 1, dur, 0, forever: true); sx.BeginTime = begin; sb.Children.Add(sx);
            var sy = Fx.Anim(t, "ScaleY", .6, 1, dur, 0, forever: true); sy.BeginTime = begin; sb.Children.Add(sy);
        }
        Children.Add(_dots);
        SizeChanged += (_, e) => { foreach (var (el, x, y) in _dotEls) { Canvas.SetLeft(el, e.NewSize.Width * x); Canvas.SetTop(el, e.NewSize.Height * y); } };
        Loaded += (_, _) => { if (!Fx.Enabled) return; try { sb.Begin(); } catch { } };  // "efectos de animación" desactivados en Windows: fondo quieto
    }

    static RadialGradientBrush Blob(double cx, double cy, double rx, double ry, double a) => new()
    {
        Center = new Point(cx, cy), GradientOrigin = new Point(cx, cy), RadiusX = rx, RadiusY = ry, MappingMode = BrushMappingMode.RelativeToBoundingBox,
        GradientStops = { new GradientStop { Color = Color.FromArgb((byte)(a * 255), 37, 99, 235), Offset = 0 }, new GradientStop { Color = Color.FromArgb(0, 37, 99, 235), Offset = .7 } },
    };

    /// <summary>Convierte el "d" del SVG (M, L, C) en una geometría y calcula su longitud.</summary>
    static PathGeometry Geo(string d, out double len)
    {
        var toks = System.Text.RegularExpressions.Regex.Matches(d, "[MLC]|-?[0-9.]+").Select(m => m.Value).ToList();
        var g = new PathGeometry(); PathFigure? f = null; var i = 0; var cmd = 'M'; len = 0; var cur = new Point(0, 0);
        double N() => double.Parse(toks[i++], System.Globalization.CultureInfo.InvariantCulture);
        while (i < toks.Count)
        {
            if (toks[i].Length == 1 && "MLC".Contains(toks[i][0])) { cmd = toks[i][0]; i++; }
            if (cmd == 'M') { cur = new Point(N(), N()); f = new PathFigure { StartPoint = cur, IsClosed = false, IsFilled = false }; g.Figures.Add(f); }
            else if (cmd == 'L') { var p = new Point(N(), N()); f!.Segments.Add(new LineSegment { Point = p }); len += Dist(cur, p); cur = p; }
            else
            {
                var a = new Point(N(), N()); var b = new Point(N(), N()); var c = new Point(N(), N());
                f!.Segments.Add(new BezierSegment { Point1 = a, Point2 = b, Point3 = c });
                var prev = cur; for (var s = 1; s <= 24; s++) { var t = s / 24.0; var q = Bez(cur, a, b, c, t); len += Dist(prev, q); prev = q; }
                cur = c;
            }
        }
        return g;
    }
    static double Dist(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    static Point Bez(Point p0, Point p1, Point p2, Point p3, double t)
    {
        var u = 1 - t;
        return new Point(u * u * u * p0.X + 3 * u * u * t * p1.X + 3 * u * t * t * p2.X + t * t * t * p3.X, u * u * u * p0.Y + 3 * u * u * t * p1.Y + 3 * u * t * t * p2.Y + t * t * t * p3.Y);
    }
}

/// <summary>Animaciones de la web: entrada escalonada (aUp), rebote al pulsar, aparición, sacudida.</summary>
public static class Fx
{
    public static readonly KeySpline Out = new() { ControlPoint1 = new Point(.2, .8), ControlPoint2 = new Point(.2, 1) };
    /// <summary>Windows → Accesibilidad → Efectos de animación (equivale a prefers-reduced-motion).</summary>
    public static bool Enabled { get; } = ReadEnabled();
    static bool ReadEnabled() { try { return new Windows.UI.ViewManagement.UISettings().AnimationsEnabled; } catch { return true; } }

    /// <summary>Cifra que "cuenta" hasta su valor en 0,7 s (transición numérica discreta).</summary>
    public static void Count(TextBlock tb, double to, Func<double, string> fmt, double ms = 700, double delayMs = 0)
    {
        tb.Text = fmt(Enabled ? 0 : to); if (!Enabled || !double.IsFinite(to)) { tb.Text = fmt(to); return; }
        var q = tb.DispatcherQueue; if (q == null) { tb.Text = fmt(to); return; }
        var t = q.CreateTimer(); t.Interval = TimeSpan.FromMilliseconds(16); var start = DateTime.UtcNow.AddMilliseconds(delayMs);
        t.Tick += (_, _) =>
        {
            var k = Math.Clamp((DateTime.UtcNow - start).TotalMilliseconds / ms, 0, 1); var e = 1 - Math.Pow(1 - k, 3);
            tb.Text = fmt(k >= 1 ? to : to * e); if (k >= 1) t.Stop();
        };
        t.Start();
    }

    public static CompositeTransform CT(UIElement e)
    {
        if (e.RenderTransform is CompositeTransform c) return c;
        c = new CompositeTransform(); e.RenderTransform = c; e.RenderTransformOrigin = new Point(.5, .5); return c;
    }

    public static Timeline Anim(DependencyObject target, string prop, double from, double to, double ms, double delayMs, EasingFunctionBase? ease = null, bool autoReverse = false, bool forever = false, bool dependent = false)
    {
        Timeline t;
        if (ease == null && !autoReverse)
        {
            var k = new DoubleAnimationUsingKeyFrames { EnableDependentAnimation = dependent };
            k.KeyFrames.Add(new DiscreteDoubleKeyFrame { KeyTime = TimeSpan.Zero, Value = from });
            k.KeyFrames.Add(new SplineDoubleKeyFrame { KeyTime = TimeSpan.FromMilliseconds(ms), Value = to, KeySpline = new KeySpline { ControlPoint1 = new Point(.2, .8), ControlPoint2 = new Point(.2, 1) } });
            t = k;
        }
        else t = new DoubleAnimation { From = from, To = to, Duration = TimeSpan.FromMilliseconds(ms), EasingFunction = ease, AutoReverse = autoReverse, EnableDependentAnimation = dependent };
        t.BeginTime = TimeSpan.FromMilliseconds(Enabled || forever ? delayMs : 0);
        if (!Enabled && !forever) t.Duration = new Duration(TimeSpan.FromMilliseconds(1));
        if (forever) t.RepeatBehavior = RepeatBehavior.Forever;
        Storyboard.SetTarget(t, target); Storyboard.SetTargetProperty(t, prop);
        return t;
    }

    static void Run(Action? done, params Timeline[] ts)
    {
        var sb = new Storyboard(); foreach (var t in ts) sb.Children.Add(t);
        if (done != null) sb.Completed += (_, _) => done();
        try { sb.Begin(); } catch { done?.Invoke(); }
    }

    /// <summary>aUp: sube 14 px y aparece en 0,55 s (curva .2,.8,.2,1), escalonado 70 ms.</summary>
    public static void EnterUp(UIElement e, int index, double dy = 14)
    {
        var c = CT(e); e.Opacity = 0; c.TranslateY = dy;
        var delay = Math.Min(index, 9) * 70;
        Run(() => { e.Opacity = 1; c.TranslateY = 0; }, Anim(e, "Opacity", 0, 1, 550, delay), Anim(c, "TranslateY", dy, 0, 550, delay));
    }
    /// <summary>Aparición con escala (aPop / lgLogoIn).</summary>
    public static void Pop(UIElement e, double delayMs, double from = .8, double ms = 520)
    {
        var c = CT(e); e.Opacity = 0; c.ScaleX = c.ScaleY = from;
        var back = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = .45 };
        Run(() => { e.Opacity = 1; c.ScaleX = c.ScaleY = 1; }, Anim(e, "Opacity", 0, 1, ms * .7, delayMs), Anim(c, "ScaleX", from, 1, ms, delayMs, back), Anim(c, "ScaleY", from, 1, ms, delayMs, back));
    }
    public static void Move(UIElement e, double fromY, double delayMs, double ms = 720, bool fade = false)
    {
        var c = CT(e); c.TranslateY = fromY; if (fade) e.Opacity = 0;
        var list = new List<Timeline> { Anim(c, "TranslateY", fromY, 0, ms, delayMs) }; if (fade) list.Add(Anim(e, "Opacity", 0, 1, ms, delayMs));
        Run(() => { c.TranslateY = 0; if (fade) e.Opacity = 1; }, list.ToArray());
    }
    public static void Shake(UIElement e)
    {
        var c = CT(e); var k = new DoubleAnimationUsingKeyFrames();
        var v = new[] { 0, -6, 6, -6, 6, 0 }; for (var i = 0; i < v.Length; i++) k.KeyFrames.Add(new LinearDoubleKeyFrame { KeyTime = TimeSpan.FromMilliseconds(i * 70), Value = v[i] });
        Storyboard.SetTarget(k, c); Storyboard.SetTargetProperty(k, "TranslateX"); Run(null, k);
    }
    /// <summary>Rebote al pulsar (transform .35s cubic-bezier(.34,1.56,.64,1)).</summary>
    public static T Bounce<T>(T e, double to = .97) where T : UIElement
    {
        if (!Enabled) return e;
        void S(double v, double ms, bool spring) { var c = CT(e); var ease = spring ? (EasingFunctionBase)new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = .5 } : new QuadraticEase(); Run(null, Anim(c, "ScaleX", c.ScaleX, v, ms, 0, ease), Anim(c, "ScaleY", c.ScaleY, v, ms, 0, ease)); }
        e.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler((_, _) => S(to, 110, false)), true);
        e.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler((_, _) => S(1, 380, true)), true);
        e.AddHandler(UIElement.PointerCaptureLostEvent, new PointerEventHandler((_, _) => S(1, 380, true)), true);
        e.AddHandler(UIElement.PointerExitedEvent, new PointerEventHandler((_, _) => S(1, 380, true)), true);
        return e;
    }
    /// <summary>Valor animado de 0 al destino (anillos y barras que "se dibujan").</summary>
    public static void Grow(DependencyObject target, string prop, double to, double ms = 1100, double delayMs = 250) =>
        Run(null, Anim(target, prop, 0, to, ms, delayMs, dependent: true));
}

/// <summary>Sonido de inicio de sesión de la web: Mi5, Si5 y Mi6 suaves con armónicos (solo al entrar correctamente).</summary>
public static class Sound
{
    [DllImport("winmm.dll", CharSet = CharSet.Unicode)] static extern bool PlaySound(byte[]? data, IntPtr hmod, uint flags);
    const uint SND_ASYNC = 0x1, SND_NODEFAULT = 0x2, SND_MEMORY = 0x4;
    static byte[]? _wav;
    const string KEY = "encargos_feedback";

    public static bool On()
    {
        try { var o = Encargos.Core.Json.Parse(Services.Storage.Get(KEY) ?? "{}") as Encargos.Core.JObj; return o == null || !o.ContainsKey("loginSoundEnabled") || Encargos.Core.Js.Truthy(o["loginSoundEnabled"]); }
        catch { return true; }
    }
    public static void Set(bool on)
    {
        try { var o = Encargos.Core.Json.Parse(Services.Storage.Get(KEY) ?? "{}") as Encargos.Core.JObj ?? new Encargos.Core.JObj(); o["loginSoundEnabled"] = on; Services.Storage.Set(KEY, Encargos.Core.Json.Stringify(o)); } catch { }
    }

    public static void Login()
    {
        if (!On()) return;
        try { _wav ??= Build(); PlaySound(_wav, IntPtr.Zero, SND_ASYNC | SND_MEMORY | SND_NODEFAULT); } catch { }
    }

    static byte[] Build()
    {
        const int rate = 44100; var total = (int)(rate * 0.78); var buf = new double[total];
        void Note(double f, double start, double dur, double g)
        {
            foreach (var (m, gg, k) in new[] { (1.0, g, 1.0), (2.01, g * .16, .6), (3.0, g * .05, .6) })
            {
                var d = dur * k; var s0 = (int)((start + 0.02) * rate); var n = (int)((d + 0.05) * rate);
                for (var i = 0; i < n; i++)
                {
                    var t = (double)i / rate; var idx = s0 + i; if (idx >= total) break;
                    var env = t < 0.012 ? 0.0001 * Math.Exp(Math.Log(gg / 0.0001) * t / 0.012) : t < d ? gg * Math.Exp(Math.Log(0.0001 / gg) * (t - 0.012) / (d - 0.012)) : 0;
                    buf[idx] += Math.Sin(2 * Math.PI * f * m * t) * env;
                }
            }
        }
        Note(659.25, 0, 0.32, 0.28); Note(987.77, 0.09, 0.42, 0.24); Note(1318.5, 0.17, 0.5, 0.10);
        using var ms = new MemoryStream(); using var w = new BinaryWriter(ms);
        var dataLen = total * 2;
        w.Write("RIFF"u8.ToArray()); w.Write(36 + dataLen); w.Write("WAVE"u8.ToArray()); w.Write("fmt "u8.ToArray()); w.Write(16); w.Write((short)1); w.Write((short)1);
        w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16); w.Write("data"u8.ToArray()); w.Write(dataLen);
        foreach (var v in buf) w.Write((short)Math.Clamp(v * 0.35 * 32767, -32767, 32767));
        w.Flush(); return ms.ToArray();
    }
}
