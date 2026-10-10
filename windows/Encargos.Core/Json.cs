using System.Globalization;
using System.Text;

namespace Encargos.Core;

/// <summary>Objeto JSON con el mismo comportamiento que un objeto de JavaScript: conserva el orden de las claves y los campos desconocidos.</summary>
public sealed class JObj : IEnumerable<KeyValuePair<string, object?>>
{
    private readonly Dictionary<string, object?> _d = new();
    private readonly List<string> _keys = new();
    public JObj() { }
    public JObj(IEnumerable<KeyValuePair<string, object?>> src) { foreach (var kv in src) this[kv.Key] = kv.Value; }

    /// <summary>Lectura: null si no existe (como undefined). Escritura: conserva la posición si la clave ya existía.</summary>
    public object? this[string k]
    {
        get => _d.TryGetValue(k, out var v) ? v : null;
        set { if (!_d.ContainsKey(k)) _keys.Add(k); _d[k] = value; }
    }
    public bool ContainsKey(string k) => _d.ContainsKey(k);
    public bool Remove(string k) { if (!_d.Remove(k)) return false; _keys.Remove(k); return true; }
    public int Count => _keys.Count;
    public bool IsEmpty => _keys.Count == 0;
    public IReadOnlyList<string> Keys => _keys.ToList();
    public void Clear() { _d.Clear(); _keys.Clear(); }
    public void PutAll(JObj o) { foreach (var kv in o) this[kv.Key] = kv.Value; }
    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() { foreach (var k in _keys.ToList()) yield return new(k, _d[k]); }
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    public string Str(string k) => Js.Str(this[k]);
    public double Num(string k) => Js.Num(this[k]);
    public JObj? Obj(string k) => this[k] as JObj;
    /// <summary>Lista en la clave k; la crea si no existe.</summary>
    public JArr Arr(string k) { if (this[k] is JArr a) return a; var n = new JArr(); this[k] = n; return n; }
    public static JObj Of(params (string, object?)[] pairs) { var o = new JObj(); foreach (var (k, v) in pairs) o[k] = v; return o; }
}

/// <summary>Lista JSON.</summary>
public sealed class JArr : List<object?>
{
    public JArr() { }
    public JArr(IEnumerable<object?> items) : base(items) { }
    public static JArr Of(params object?[] items) => new(items);
    public List<JObj> Objs() => this.OfType<JObj>().ToList();
}

public static class JsonX
{
    public static List<JObj> Objs(this IEnumerable<object?> l) => l.OfType<JObj>().ToList();
}

public static class Json
{
    public static object? Parse(string text) => new Parser(text).ParseTop();
    public static JObj? Obj(string? text) { try { return text == null ? null : Parse(text) as JObj; } catch { return null; } }
    public static JArr? Arr(string? text) { try { return text == null ? null : Parse(text) as JArr; } catch { return null; } }

    public static string Stringify(object? v, int indent = 0) { var sb = new StringBuilder(); Write(sb, v, indent, 0); return sb.ToString(); }

    static void Write(StringBuilder sb, object? v, int indent, int level)
    {
        switch (v)
        {
            case null: sb.Append("null"); return;
            case string s: Quote(sb, s); return;
            case bool b: sb.Append(b ? "true" : "false"); return;
            case double or float or int or long or decimal or short or byte or uint or ulong:
                {
                    var d = Convert.ToDouble(v, CultureInfo.InvariantCulture);
                    sb.Append(double.IsNaN(d) || double.IsInfinity(d) ? "null" : Js.NumberToString(d)); return;
                }
            case JObj o:
                {
                    if (o.IsEmpty) { sb.Append("{}"); return; }
                    sb.Append('{'); var first = true;
                    foreach (var kv in o)
                    {
                        if (!first) sb.Append(','); first = false;
                        NewLine(sb, indent, level + 1);
                        Quote(sb, kv.Key); sb.Append(':'); if (indent > 0) sb.Append(' ');
                        Write(sb, kv.Value, indent, level + 1);
                    }
                    NewLine(sb, indent, level); sb.Append('}'); return;
                }
            case System.Collections.IDictionary dict:
                {
                    var o = new JObj(); foreach (System.Collections.DictionaryEntry e in dict) o[e.Key.ToString()!] = e.Value; Write(sb, o, indent, level); return;
                }
            case System.Collections.IEnumerable list:
                {
                    var items = list.Cast<object?>().ToList();
                    if (items.Count == 0) { sb.Append("[]"); return; }
                    sb.Append('['); var first = true;
                    foreach (var x in items)
                    {
                        if (!first) sb.Append(','); first = false;
                        NewLine(sb, indent, level + 1); Write(sb, x, indent, level + 1);
                    }
                    NewLine(sb, indent, level); sb.Append(']'); return;
                }
            default: Quote(sb, v.ToString() ?? ""); return;
        }
    }
    static void NewLine(StringBuilder sb, int indent, int level) { if (indent <= 0) return; sb.Append('\n'); sb.Append(' ', indent * level); }

    public static void Quote(StringBuilder sb, string s)
    {
        sb.Append('"');
        foreach (var c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                default:
                    if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4")); else sb.Append(c);
                    break;
            }
        }
        sb.Append('"');
    }

    /// <summary>Copia profunda (igual que JSON.parse(JSON.stringify(x))).</summary>
    public static object? DeepCopy(object? v) => v switch
    {
        JObj o => new JObj(o.Select(kv => new KeyValuePair<string, object?>(kv.Key, DeepCopy(kv.Value)))),
        JArr a => new JArr(a.Select(DeepCopy)),
        _ => v,
    };

    sealed class Parser
    {
        readonly string s; int i;
        public Parser(string s) { this.s = s; }
        public object? ParseTop() { Ws(); var v = Value(); Ws(); if (i != s.Length) Err("datos sobrantes"); return v; }
        Exception Err(string m) => throw new FormatException($"JSON inválido ({m}) en {i}");
        void Ws() { while (i < s.Length && (s[i] == ' ' || s[i] == '\n' || s[i] == '\r' || s[i] == '\t')) i++; }
        object? Value()
        {
            if (i >= s.Length) throw Err("fin inesperado");
            var c = s[i];
            switch (c)
            {
                case '{': return ObjV();
                case '[': return ArrV();
                case '"': return StrV();
                case 't': return Lit("true", true);
                case 'f': return Lit("false", false);
                case 'n': return Lit("null", null);
                default: if (c == '-' || (c >= '0' && c <= '9')) return NumV(); throw Err($"carácter '{c}'");
            }
        }
        object? Lit(string w, object? v) { if (string.CompareOrdinal(s, i, w, 0, w.Length) != 0) throw Err("literal"); i += w.Length; return v; }
        JObj ObjV()
        {
            var m = new JObj(); i++; Ws();
            if (i < s.Length && s[i] == '}') { i++; return m; }
            while (true)
            {
                Ws(); if (i >= s.Length || s[i] != '"') throw Err("clave");
                var k = StrV(); Ws(); if (i >= s.Length || s[i] != ':') throw Err(":"); i++; Ws();
                m[k] = Value(); Ws();
                if (i >= s.Length) throw Err("fin");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; return m; }
                throw Err("objeto");
            }
        }
        JArr ArrV()
        {
            var a = new JArr(); i++; Ws();
            if (i < s.Length && s[i] == ']') { i++; return a; }
            while (true)
            {
                Ws(); a.Add(Value()); Ws();
                if (i >= s.Length) throw Err("fin");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; return a; }
                throw Err("lista");
            }
        }
        string StrV()
        {
            i++; var sb = new StringBuilder();
            while (true)
            {
                if (i >= s.Length) throw Err("cadena sin cerrar");
                var c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) throw Err("escape");
                var e = s[i++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (i + 4 > s.Length) throw Err("\\u");
                        sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16)); i += 4; break;
                    default: throw Err("escape \\" + e);
                }
            }
        }
        double NumV()
        {
            var st = i;
            if (s[i] == '-') i++;
            while (i < s.Length && (char.IsAsciiDigit(s[i]) || s[i] == '.' || s[i] == 'e' || s[i] == 'E' || s[i] == '+' || s[i] == '-')) i++;
            if (double.TryParse(s.AsSpan(st, i - st), NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return d;
            throw Err("número");
        }
    }
}
