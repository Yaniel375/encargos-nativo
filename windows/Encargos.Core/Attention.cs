namespace Encargos.Core;

/// <summary>Centro de atención: alertas calculadas desde los datos, "revisada" ≠ "resuelta", actividad reciente.</summary>
public sealed class Attention
{
    public sealed record Act(string Kind, string Label, string? OrderId = null, string? Client = null);
    public sealed record Alert(string Key, string Cat, string Pr, string Sig, string Title, string Sub, double? Amount, bool AmountGreen, List<Act> Actions, string? RefOrder, string? RefClient);

    readonly EncargosApp _app;
    const string K_REV = "encargos_attn_reviewed", K_LAST = "encargos_attn_last", K_ACT = "encargos_activity";
    public static readonly (string Key, string Label)[] GROUPS = { ("pay", "Pagos pendientes"), ("ready", "Pedidos listos para entregar"), ("transit", "Pedidos en tránsito"), ("incomplete", "Información incompleta"), ("funds", "Fondos a favor"), ("backup", "Respaldo") };
    public List<(long T, string Text)> RecentResolved = new();

    public Attention(EncargosApp app) { _app = app; }

    public int DaysSince(object? t) { var ms = Js.ParseDate(t); return double.IsNaN(ms) ? 0 : (int)Math.Floor((Js.NowMs() - ms) / 86400000.0); }
    public string Ago(object? t) { var d = DaysSince(t); return d <= 0 ? "hoy" : d == 1 ? "hace 1 día" : $"hace {d} días"; }

    public void Log(string type, string text, double? amount = null)
    {
        var a = _app.Ls.Arr(K_ACT); var e = JObj.Of(("t", (double)Js.NowMs()), ("type", type), ("text", text)); if (amount != null) e["amount"] = amount.Value;
        a.Insert(0, e); _app.Ls.Put(K_ACT, new JArr(a.Take(120)));
    }
    public List<JObj> Activity() => _app.Ls.Arr(K_ACT).Objs();

    public List<Alert> Compute()
    {
        var outp = new List<Alert>(); var ad = _app.ActiveOrders(); var now = Js.NowMs();
        foreach (var c in _app.ActiveClients().Where(c => c.Pending > 0.005))
        {
            var pend = c.OrderList.Where(o => Js.Num(o["balance"]) > 0.005).OrderBy(o => { var v = Js.ParseDate(o["date"]); return double.IsNaN(v) ? double.PositiveInfinity : v; }).ToList(); // NaN al final, como sortedBy de Kotlin
            var oldest = pend.Count > 0 ? DaysSince(pend[0]["date"]) : 0;
            var act = pend.Count == 1 ? new Act("pay", "Registrar pago", OrderId: Js.Str(pend[0]["id"])) : new Act("client", "Registrar pago", Client: c.Name);
            outp.Add(new Alert("pay:" + c.Name.Trim().ToLowerInvariant(), "pay", oldest >= 30 ? "urgent" : "important", Js.ToFixed(c.Pending, 2), c.Name,
                $"${Js.ToFixed(c.Pending, 2)} pendientes en {pend.Count} encargo{(pend.Count == 1 ? "" : "s")} · el más antiguo {Ago(pend.Count > 0 ? pend[0]["date"] : (double)now)}",
                c.Pending, false, new() { act, new Act("remind", "WhatsApp", Client: c.Name) }, null, c.Name));
        }
        foreach (var x in _app.Orders().Where(o => Js.Eq(o["status"], "Recibido")))
        {
            var since = (x["statusDates"] as JObj)?["Recibido"];
            outp.Add(new Alert("ready:" + Js.Str(x["id"]), "ready", "important", "1", x.Str("name"), (since != null ? $"Llegó {Ago(since)} · " : "") + $"{Js.ToFixed(Js.Num(x["pounds"]), 2)} lb · listo para recoger",
                null, false, new() { new Act("order", "Ver pedido", OrderId: Js.Str(x["id"])), new Act("arrival", "WhatsApp", Client: x.Str("name")), new Act("deliver", "Entregado", OrderId: Js.Str(x["id"])) }, Js.Str(x["id"]), null));
        }
        var w = _app.TransitWarn; var l = _app.TransitLate;
        foreach (var x in _app.Orders().Where(o => Js.Eq(o["status"], "En tránsito")))
        {
            var real = (x["statusDates"] as JObj)?["En tránsito"]; var d = DaysSince(real ?? x["date"]); if (d < w) continue;
            outp.Add(new Alert("transit:" + Js.Str(x["id"]), "transit", d >= l ? "urgent" : "important", d >= l ? "true" : "false", x.Str("name"),
                (real != null ? $"Lleva {d} días en tránsito" : $"Registrado hace {d} días y sigue en tránsito") + (d >= l ? " · atrasado" : ""),
                null, false, new() { new Act("order", "Ver pedido", OrderId: Js.Str(x["id"])), new Act("advance", "Llegó", OrderId: Js.Str(x["id"])) }, Js.Str(x["id"]), null));
        }
        foreach (var x in _app.Orders().Where(x => (!Js.Eq(x["status"], "Entregado") && !Js.Eq(x["status"], "Cancelado")) || (!(Js.Num(x["cost"]) > 0) && !Js.Eq(x["status"], "Cancelado") && ad.Contains(x))))
        {
            var miss = new List<string>();
            if (x.Str("name").Trim().Length == 0) miss.Add("nombre del cliente");
            if (!(Js.Num(x["cost"]) > 0)) miss.Add("costo del pedido");
            if ((Js.Eq(x["status"], "Recibido") || Js.Eq(x["status"], "Entregado")) && !(Js.Num(x["pounds"]) > 0)) miss.Add("peso (libras)");
            if (!EncargosApp.STATES.Contains(x.Str("status").IfEmpty("Pedido"))) miss.Add("estado válido");
            if (System.Text.RegularExpressions.Regex.Replace(x.Str("phone"), @"\D", "").Length == 0) miss.Add("teléfono (para WhatsApp)");
            if (miss.Count > 0) outp.Add(new Alert("inc:" + Js.Str(x["id"]), "incomplete", miss.Count == 1 && miss[0].StartsWith("teléfono") ? "info" : "important", string.Join(",", miss),
                x.Str("name").IfEmpty("Encargo sin nombre"), "Falta: " + string.Join(", ", miss), null, false, new() { new Act("edit", "Editar", OrderId: Js.Str(x["id"])) }, Js.Str(x["id"]), null));
        }
        foreach (var c in _app.ActiveClients().Where(c => c.Funds > 0.005))
            outp.Add(new Alert("funds:" + c.Name.Trim().ToLowerInvariant(), "funds", "info", Js.ToFixed(c.Funds, 2), c.Name, $"Tiene ${Js.ToFixed(c.Funds, 2)} a favor", c.Funds, true, new() { new Act("client", "Ver cliente", Client: c.Name) }, null, c.Name));
        var lb = LastBackup();
        if (_app.Data.Count > 0 && (lb == 0.0 || now - lb > 7 * 86400000.0))
            outp.Add(new Alert("backup", "backup", "info", "1", "Respaldo pendiente", lb > 0 ? "Último respaldo " + Ago(lb) : "Aún no has hecho ningún respaldo", null, false, new() { new Act("backup", "Crear respaldo") }, null, null));
        return outp;
    }
    public double LastBackup() => double.TryParse(_app.Ls.Get("encargos_last_backup", "0"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0.0;

    public JObj Reviewed() => _app.Ls.JsonOf(K_REV) as JObj ?? new JObj();
    public bool IsReviewed(Alert a) => Js.Eq(Reviewed()[a.Key], a.Sig);
    public void ToggleReview(string key)
    {
        var a = Compute().FirstOrDefault(x => x.Key == key); if (a == null) return;
        var r = Reviewed(); if (Js.Eq(r[key], a.Sig)) r.Remove(key); else r[key] = a.Sig; _app.Ls.Put(K_REV, r); _app.Changed(false);
    }
    public void ReviewAll() { var r = new JObj(); foreach (var a in Compute()) r[a.Key] = a.Sig; _app.Ls.Put(K_REV, r); _app.Changed(false); }
    /// <summary>Insignia: urgentes + importantes no revisadas.</summary>
    public int Badge(List<Alert>? all = null) { var rv = Reviewed(); return (all ?? Compute()).Count(a => a.Pr != "info" && !Js.Eq(rv[a.Key], a.Sig)); }

    /// <summary>detectResolved: registra en la actividad lo que dejó de ser un problema.</summary>
    public void DetectResolved(List<Alert> alerts)
    {
        var last = _app.Ls.JsonOf(K_LAST) as JObj;
        var nowKeys = new JObj();
        foreach (var a in alerts) nowKeys[a.Key] = JObj.Of(("t", a.Title), ("cat", a.Cat), ("ref", JObj.Of(("order", a.RefOrder), ("client", a.RefClient))));
        if (last != null)
            foreach (var kv in last)
            {
                if (nowKeys[kv.Key] != null) continue;
                if (kv.Value is not JObj o) continue; var cat = o.Str("cat"); if (!new[] { "pay", "ready", "transit", "incomplete" }.Contains(cat)) continue;
                var rf = o["ref"] as JObj ?? new JObj();
                if (rf["order"] != null && _app.ById(rf["order"]) == null) continue;
                if (rf["client"] != null && !_app.Clients().Any(c => c.Key == Js.Str(rf["client"]).Trim().ToLowerInvariant())) continue;
                var t = o.Str("t");
                var txt = cat switch { "pay" => $"{t}: saldo pendiente resuelto", "ready" => $"{t}: pedido entregado", "transit" => $"{t}: pedido ya no está atrasado", _ => $"{t}: información completada" };
                if (cat != "ready") Log("resolved", txt); RecentResolved.Insert(0, (Js.NowMs(), txt));
            }
        RecentResolved = RecentResolved.Where(r => Js.NowMs() - r.T < 60000).Take(4).ToList();
        _app.Ls.Put(K_LAST, nowKeys);
        var rv = Reviewed(); var ch = false; foreach (var k in rv.Keys) if (nowKeys[k] == null) { rv.Remove(k); ch = true; }
        if (ch) _app.Ls.Put(K_REV, rv);
    }
}
