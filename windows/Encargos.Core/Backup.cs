using System.Text.RegularExpressions;

namespace Encargos.Core;

/// <summary>Respaldos locales: JSON versión 5 (compatible con la app web), respaldo cifrado con contraseña, importación y CSV.</summary>
public sealed class Backup
{
    readonly EncargosApp _app;
    LS Ls => _app.Ls;
    public Backup(EncargosApp app) { _app = app; }

    public JObj Extras() => JObj.Of(("activity", Ls.Arr("encargos_activity")), ("expenses", Ls.Arr("encargos_expenses")), ("clientInfo", Ls.Obj("encargos_client_info")),
        ("templates", Ls.JsonOf("encargos_templates")), ("buyRates", Ls.JsonOf("encargos_buy_rates")));

    JObj PayloadObj(bool auto)
    {
        var o = JObj.Of(("version", 5.0), ("exportedAt", Js.NowIso()));
        if (auto) o["auto"] = true;
        o["shippingRates"] = JObj.Of(("rates", _app.ShippingRates), ("names", _app.ShippingNames));
        o["data"] = _app.Data; o["notes"] = Ls.Arr("encargos_notes"); o["extra"] = Extras();
        return o;
    }
    public static string FileName(string prefix = "respaldo_encargos_", string ext = ".json") => prefix + Js.NowIso()[..10] + ext;

    /// <summary>exportData(): texto del respaldo normal.</summary>
    public string ExportJson(bool auto = false) { var s = Json.Stringify(PayloadObj(auto), auto ? 0 : 2); MarkBackup(false); return s; }
    public void MarkBackup(bool encrypted)
    {
        var now = Js.NumberToString(Js.NowMs());
        Ls.Set("encargos_last_backup", now); if (encrypted) Ls.Set("encargos_last_backup_enc", now);
        _app.Attention.Log("backup", encrypted ? "Respaldo cifrado realizado" : "Respaldo realizado");
        _app.Notifs.SysEvents(); _app.Changed(false);
    }

    /// <summary>Respaldo cifrado: PBKDF2-SHA256 (150000) + AES-256-GCM, mismo formato que la app web.</summary>
    public string ExportEncrypted(string password)
    {
        if (password.Length < 6) throw new ArgumentException("La contraseña debe tener al menos 6 caracteres.");
        var payload = Crypto.Utf8(Json.Stringify(PayloadObj(false)));
        var salt = Crypto.Random(16); var iv = Crypto.Random(12);
        var key = Crypto.Pbkdf2(password, salt, 150000);
        var ct = Crypto.AesGcmEncrypt(key, iv, payload);
        var outp = Json.Stringify(JObj.Of(("encargosCifrado", 1.0), ("v", 1.0), ("alg", "AES-GCM"), ("kdf", "PBKDF2-SHA256"), ("iter", 150000.0), ("salt", Crypto.B64(salt)), ("iv", Crypto.B64(iv)), ("data", Crypto.B64(ct))));
        MarkBackup(true); return outp;
    }

    public abstract record Parsed { public sealed record Plain(object? O) : Parsed; public sealed record Encrypted(JObj O) : Parsed; public sealed record Invalid(string Why) : Parsed; }
    public Parsed Parse(string text)
    {
        try
        {
            return Json.Parse(text.TrimStart('﻿')) switch
            {
                JObj m when Js.Truthy(m["encargosCifrado"]) => new Parsed.Encrypted(m),
                JObj m when m["data"] is JArr => new Parsed.Plain(m),
                JArr a => new Parsed.Plain(a),
                _ => new Parsed.Invalid("El archivo de respaldo no es válido."),
            };
        }
        catch { return new Parsed.Invalid("El archivo de respaldo no es válido."); }
    }

    public object? Decrypt(JObj o, string password)
    {
        var iter = (int)Js.Num(o["iter"]); if (iter <= 0) iter = 150000;
        var key = Crypto.Pbkdf2(password, Crypto.UnB64(o.Str("salt")), iter);
        var pt = Crypto.AesGcmDecrypt(key, Crypto.UnB64(o.Str("iv")), Crypto.UnB64(o.Str("data")));
        return Json.Parse(Crypto.FromUtf8(pt));
    }

    /// <summary>readImport(): reemplaza los datos actuales por los del respaldo. Devuelve cuántos encargos se importaron.</summary>
    public int ApplyImport(object? o)
    {
        var arr = (o as JArr) ?? ((o as JObj)?["data"] as JArr) ?? throw new ArgumentException("El archivo de respaldo no es válido.");
        var m = o as JObj;
        if (m?["shippingRates"] is JObj sr)
        {
            if (sr["rates"] is JObj rates)
            {
                var r = new JObj(); foreach (var (k, v) in EncargosApp.DEFAULT_SHIPPING_RATES) r[k] = v; foreach (var kv in rates) r[kv.Key] = kv.Value;
                var n = new JObj(); foreach (var (k, v) in EncargosApp.DEFAULT_SHIPPING_NAMES) n[k] = v; if (sr["names"] is JObj names) foreach (var kv in names) n[kv.Key] = kv.Value;
                _app.ShippingRates = r; _app.ShippingNames = n;
            }
            else
            {
                _app.ShippingRates = JObj.Of(("avion", sr["avion"] != null ? Js.Num(sr["avion"]) : 7.0), ("barco", sr["barco"] != null ? Js.Num(sr["barco"]) : 4.0));
                var n = new JObj(); foreach (var (k, v) in EncargosApp.DEFAULT_SHIPPING_NAMES) n[k] = v; _app.ShippingNames = n;
            }
            _app.SaveShippingRates();
        }
        var data = new JArr();
        foreach (var x in arr)
        {
            if (x is not JObj ox) continue; var c = (JObj)Json.DeepCopy(ox)!;
            var t = c.Str("shippingType").IfEmpty("avion"); c["shippingType"] = t;
            c["lbCost"] = ox["lbCost"] != null ? Js.Num(ox["lbCost"]) : (_app.ShippingRates[t] is { } rv ? Js.Num(rv) : 7.0);
            data.Add(c);
        }
        _app.Data = data;
        if (m?["notes"] is JArr notes) Ls.Put("encargos_notes", notes);
        if (m?["extra"] is JObj ex)
        {
            if (ex["activity"] is JArr a1) Ls.Put("encargos_activity", a1);
            if (ex["expenses"] is JArr a2) Ls.Put("encargos_expenses", a2);
            if (ex["clientInfo"] is JObj a3) Ls.Put("encargos_client_info", a3);
            if (ex["templates"] is JObj a4) Ls.Put("encargos_templates", a4);
            if (ex["buyRates"] is JObj a5) Ls.Put("encargos_buy_rates", a5);
        }
        _app.Notifs.SuppressUntil = Js.NowMs() + 1500;
        _app.Persist(); _app.Refresh();
        _app.Notifs.ImportDone(data.Count);
        return data.Count;
    }

    public string Csv()
    {
        static string Esc(string v) => Regex.IsMatch(v, "[\",\n;]") ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;
        var header = new[] { "Cliente", "Teléfono", "Estado", "Envío", "Libras", "Tarifa/lb", "Costo", "Envío total", "Total", "Pagado", "Saldo", "Fecha", "Nota interna" };
        var rows = _app.Orders().Select(x => new[] { x.Str("name"), x.Str("phone"), x.Str("status"), _app.ShippingLabel(x.Str("shippingType").IfEmpty("avion")),
            Js.ToFixed(Js.Num(x["pounds"]), 2), Js.ToFixed(Js.Num(x["lbCost"]), 2), Js.ToFixed(Js.Num(x["cost"]), 2), Js.ToFixed(Js.Num(x["shipping"]), 2),
            Js.ToFixed(Js.Num(x["total"]), 2), Js.ToFixed(Js.Num(x["paid"]), 2), Js.ToFixed(Js.Num(x["balance"]), 2), Js.Fmt(x["date"]), x.Str("note") });
        return "﻿" + string.Join("\r\n", new[] { header }.Concat(rows).Select(r => string.Join(",", r.Select(Esc))));
    }

    /* ---------- Respaldo automático (diario / semanal / desactivado) ---------- */
    public string AutoMode { get => Ls.Get("encargos_auto_backup", "daily") ?? "daily"; set { Ls.Set("encargos_auto_backup", value); _app.Changed(false); } }
    public double LastAuto => double.TryParse(Ls.Get("encargos_auto_backup_last", "0"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0.0;
    /// <summary>Devuelve el contenido a guardar si toca hacer el respaldo automático (la plataforma escribe el archivo y llama a AutoDone).</summary>
    public string? AutoDue(bool force = false)
    {
        var mode = AutoMode; if (mode == "off" && !force) return null;
        var need = mode == "weekly" ? 7 * 86400000.0 : 86400000.0;
        if (!force && Js.NowMs() - LastAuto < need) return null;
        if (_app.Data.Count == 0 && !force) return null;
        return Json.Stringify(PayloadObj(true));
    }
    public void AutoDone() { var now = Js.NumberToString(Js.NowMs()); Ls.Set("encargos_auto_backup_last", now); Ls.Set("encargos_last_backup", now); _app.Notifs.SysEvents(); _app.Changed(false); }
}
