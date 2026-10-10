using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;

namespace Encargos.Core;

/// <summary>Reglas numéricas y de texto idénticas a las de la app web (JavaScript) y al núcleo Kotlin.</summary>
public static class Js
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static long NowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// <summary>Igualdad de valores JSON (nunca por referencia).</summary>
    public static bool Eq(object? a, object? b)
    {
        if (a == null || b == null) return a == null && b == null;
        if (IsNumber(a) && IsNumber(b)) return Convert.ToDouble(a, Inv) == Convert.ToDouble(b, Inv);
        return a.Equals(b);
    }
    public static bool IsNumber(object? v) => v is double or float or int or long or decimal or short or byte or uint or ulong;

    /// <summary>num(v) = Number(v || 0)</summary>
    public static double Num(object? v) => v switch
    {
        null => 0.0,
        double d => d,
        bool b => b ? 1.0 : 0.0,
        string s => ParseNumStr(s),
        _ when IsNumber(v) => Convert.ToDouble(v, Inv),
        _ => double.NaN,
    };
    static double ParseNumStr(string s)
    {
        var t = s.Trim(); if (t.Length == 0) return 0.0;
        if (t == "Infinity" || t == "+Infinity") return double.PositiveInfinity;
        if (t == "-Infinity") return double.NegativeInfinity;
        if (!Regex.IsMatch(t, @"^[+-]?([0-9]+\.?[0-9]*|\.[0-9]+)([eE][+-]?[0-9]+)?$")) return double.NaN;
        return double.TryParse(t, NumberStyles.Float, Inv, out var d) ? d : double.NaN;
    }

    /// <summary>String(v ?? '')</summary>
    public static string Str(object? v) => v switch
    {
        null => "",
        string s => s,
        bool b => b ? "true" : "false",
        _ when IsNumber(v) => NumberToString(Convert.ToDouble(v, Inv)),
        _ => v.ToString() ?? "",
    };

    public static bool Truthy(object? v) => v switch
    {
        null => false,
        bool b => b,
        string s => s.Length > 0,
        _ when IsNumber(v) => Convert.ToDouble(v, Inv) is var d && d != 0.0 && !double.IsNaN(d),
        _ => true,
    };

    /// <summary>Math.round de JavaScript (los .5 van hacia +infinito).</summary>
    public static double Round(double x) => double.IsNaN(x) || double.IsInfinity(x) ? x : Math.Floor(x + 0.5);

    /// <summary>r2 de la app: Math.round(num(v)*100)/100, sin -0.</summary>
    public static double R2(object? v) { var n = Round(Num(v) * 100) / 100; return n == 0.0 ? 0.0 : n; }

    public static double ToFixed2(double x) => double.Parse(ToFixed(x, 2), Inv);

    /// <summary>Number.prototype.toFixed, con el valor binario exacto (igual que BigDecimal(double) HALF_UP).</summary>
    public static string ToFixed(double x, int digits)
    {
        if (double.IsNaN(x)) return "NaN";
        if (Math.Abs(x) >= 1e21) return NumberToString(x);
        var neg = x < 0; var a = Math.Abs(x);
        // a = m * 2^e exactamente
        long bits = BitConverter.DoubleToInt64Bits(a);
        int exp = (int)((bits >> 52) & 0x7FF); long frac = bits & 0xFFFFFFFFFFFFFL;
        BigInteger m; int e;
        if (exp == 0) { m = frac; e = -1074; } else { m = frac | (1L << 52); e = exp - 1075; }
        BigInteger scaled; var pow10 = BigInteger.Pow(10, digits);
        if (e >= 0) scaled = (m << e) * pow10;
        else
        {
            var den = BigInteger.One << (-e); var num = m * pow10;
            scaled = BigInteger.DivRem(num, den, out var rem);
            if (rem * 2 >= den) scaled += 1;
        }
        var s = scaled.ToString(Inv);
        if (digits > 0)
        {
            if (s.Length <= digits) s = new string('0', digits - s.Length + 1) + s;
            s = s[..^digits] + "." + s[^digits..];
        }
        return neg && scaled != 0 ? "-" + s : s;
    }

    /// <summary>money(v) = '$' + num(v).toFixed(2)</summary>
    public static string Money(object? v) => "$" + ToFixed(Num(v), 2);

    /// <summary>Number.prototype.toString() de JavaScript.</summary>
    public static string NumberToString(double d)
    {
        if (double.IsNaN(d)) return "NaN";
        if (double.IsInfinity(d)) return d > 0 ? "Infinity" : "-Infinity";
        if (d == 0.0) return "0";
        var a = Math.Abs(d);
        if (d == Math.Floor(d) && a < 9.007199254740992E15) return ((long)d).ToString(Inv);
        // dígitos más cortos que vuelven al mismo double
        var r = a.ToString("R", Inv); // p.ej. "1.5E-07", "123456.789", "1E+21"
        string mant = r; int e10 = 0;
        var ei = r.IndexOfAny(new[] { 'E', 'e' });
        if (ei >= 0) { mant = r[..ei]; e10 = int.Parse(r[(ei + 1)..], NumberStyles.AllowLeadingSign, Inv); }
        var dot = mant.IndexOf('.');
        string digits = dot >= 0 ? mant.Remove(dot, 1) : mant;
        int intLen = (dot >= 0 ? dot : mant.Length) + e10; // posición del punto decimal
        // quitar ceros a la izquierda
        int lz = 0; while (lz < digits.Length - 1 && digits[lz] == '0') { lz++; intLen--; }
        digits = digits[lz..].TrimEnd('0'); if (digits.Length == 0) digits = "0";
        var sign = d < 0 ? "-" : "";
        if (a >= 1e-6 && a < 1e21)
        {
            string body;
            if (intLen <= 0) body = "0." + new string('0', -intLen) + digits;
            else if (intLen >= digits.Length) body = digits + new string('0', intLen - digits.Length);
            else body = digits[..intLen] + "." + digits[intLen..];
            return sign + body;
        }
        var exp = intLen - 1;
        var m2 = digits.Length > 1 ? digits[0] + "." + digits[1..] : digits;
        return sign + m2 + "e" + (exp >= 0 ? "+" : "-") + Math.Abs(exp);
    }

    /// <summary>new Date().toISOString()</summary>
    public static string NowIso() => IsoOf(NowMs());
    public static string IsoOf(long ms) => DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", Inv);

    static readonly Regex IsoRe = new(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2}(\.\d+)?)?(Z|[+-]\d{2}:\d{2})$", RegexOptions.Compiled);
    /// <summary>new Date(s).getTime() para cadenas ISO (o milisegundos); NaN si no se entiende.</summary>
    public static double ParseDate(object? v)
    {
        if (v == null) return double.NaN;
        if (IsNumber(v)) return Convert.ToDouble(v, Inv);
        var s = v.ToString()!.Trim();
        if (s.Length == 0) return double.NaN;
        if (IsoRe.IsMatch(s) && DateTimeOffset.TryParse(s, Inv, DateTimeStyles.AssumeUniversal, out var dto)) return dto.ToUnixTimeMilliseconds();
        if (s.Length >= 10 && DateTime.TryParseExact(s[..10], "yyyy-MM-dd", Inv, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt))
            return new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
        return double.NaN;
    }

    /// <summary>Fecha y hora local del equipo.</summary>
    public static DateTime Local(double ms) => DateTimeOffset.FromUnixTimeMilliseconds((long)ms).ToLocalTime().DateTime;

    /// <summary>Medianoche local de un día → milisegundos (si esa hora no existe por cambio de horario, la primera hora válida).</summary>
    public static double MsOf(DateOnly d)
    {
        var dt = d.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified); var tz = TimeZoneInfo.Local; var n = 0;
        while (tz.IsInvalidTime(dt) && n++ < 24 * 4) dt = dt.AddMinutes(15);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(dt, tz)).ToUnixTimeMilliseconds();
    }
    public static DateOnly Today() => DateOnly.FromDateTime(DateTime.Now);

    /// <summary>fmt(s) de la app: fecha corta y hora corta.</summary>
    public static string Fmt(object? v)
    {
        var t = ParseDate(v); if (double.IsNaN(t)) return "Invalid Date";
        return Local(t).ToString("d/M/yy, H:mm", Inv);
    }

    public static string NormSearch(string s)
    {
        var n = s.ToLowerInvariant().Normalize(NormalizationForm.FormD); var sb = new StringBuilder(n.Length);
        foreach (var c in n) if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString();
    }

    public static string Base36(long v)
    {
        const string D = "0123456789abcdefghijklmnopqrstuvwxyz"; if (v == 0) return "0";
        var neg = v < 0; var u = (ulong)Math.Abs(v); var sb = new StringBuilder();
        while (u > 0) { sb.Insert(0, D[(int)(u % 36)]); u /= 36; }
        return neg ? "-" + sb : sb.ToString();
    }

    /// <summary>Comparación tipo localeCompare('es') de la app (sin distinguir mayúsculas).</summary>
    public static readonly StringComparer Collator = StringComparer.Create(new CultureInfo("es-ES"), CompareOptions.IgnoreCase);
    public static int Ord(string a, string b) => string.CompareOrdinal(a, b);
    public static string Take(this string s, int n) => s.Length <= n ? s : s[..n];
    public static string TakeLast(this string s, int n) => s.Length <= n ? s : s[^n..];
    public static string IfEmpty(this string s, string d) => s.Length == 0 ? d : s;
}
