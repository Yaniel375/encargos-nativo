using System.Text.RegularExpressions;

namespace Encargos.Core;

/// <summary>Centro de notificaciones: detecta eventos comparando el estado anterior y el nuevo (igual que la app web).</summary>
public sealed class Notifications
{
    readonly EncargosApp _app;
    const string K_LIST = "encargos_notifs", K_CFG = "encargos_notif_cfg", K_SEEN_SEC = "encargos_notif_secseen", K_SEEN_BK = "encargos_notif_bkseen";
    const int MAX = 1000;
    public static readonly (string Key, string Label)[] CAT = { ("pay", "Pagos"), ("order", "Pedidos"), ("client", "Clientes"), ("system", "Sistema") };
    public static string CatLabel(string k) => CAT.FirstOrDefault(c => c.Key == k).Label ?? "";
    /// <summary>Aviso breve dentro de la app.</summary>
    public Action<JObj>? OnImportant;
    public long SuppressUntil;

    public Notifications(EncargosApp app) { _app = app; }

    public JObj Cfg()
    {
        var c = JObj.Of(("inapp", true), ("pay", true), ("order", true), ("client", true), ("system", true), ("important", true));
        if (_app.Ls.JsonOf(K_CFG) is JObj o) foreach (var kv in o) c[kv.Key] = kv.Value;
        return c;
    }
    public void ToggleCfg(string k) { var c = Cfg(); c[k] = !Js.Truthy(c[k]); _app.Ls.Put(K_CFG, c); _app.Changed(false); }
    public List<JObj> All() => _app.Ls.Arr(K_LIST).Objs();
    void Store(IEnumerable<JObj> a) => _app.Ls.Put(K_LIST, new JArr(a.Take(MAX)));
    public List<string> VisibleCats() { var c = Cfg(); return new[] { "pay", "order", "client", "system" }.Where(k => !Js.Eq(c[k], false)).ToList(); }
    public int UnreadCount() { var cats = VisibleCats(); return All().Count(n => !Js.Truthy(n["read"]) && cats.Contains(n.Str("cat"))); }

    public void Add(JObj n)
    {
        var a = All();
        n["id"] = "n" + Js.Base36(Js.NowMs()) + Crypto.Hex(Crypto.Random(2));
        if (n["t"] == null) n["t"] = (double)Js.NowMs();
        n["read"] = false;
        a.Insert(0, n); Store(a);
        var c = Cfg();
        if (Js.Eq(c[n.Str("cat")], false)) return;
        if (Js.Truthy(n["imp"]) && Js.Truthy(c["important"]) && Js.Truthy(c["inapp"])) OnImportant?.Invoke(n);
    }
    public void Read(string id) { var a = All(); var n = a.FirstOrDefault(x => Js.Eq(x["id"], id)); if (n != null) { n["read"] = true; Store(a); } _app.Changed(false); }
    public void ReadAll() { var a = All(); foreach (var n in a) n["read"] = true; Store(a); _app.Changed(false); }
    public void Clear() { Store(new List<JObj>()); _app.Changed(false); }

    public List<JObj> List(string filter, string q)
    {
        var cats = VisibleCats(); var a = All().Where(n => cats.Contains(n.Str("cat")));
        if (filter == "unread") a = a.Where(n => !Js.Truthy(n["read"]));
        else if (CAT.Any(c => c.Key == filter)) a = a.Where(n => n.Str("cat") == filter);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var nq = Js.NormSearch(q);
            a = a.Where(n => Js.NormSearch(string.Join(" ", n.Str("title"), n.Str("text"), CatLabel(n.Str("cat")), (n["data"] as JObj)?.Str("method") ?? "",
                (n["data"] as JObj)?.Str("status") ?? "", (n["ref"] as JObj)?.Str("client") ?? "")).Contains(nq));
        }
        return a.ToList();
    }

    /* ---------- detección por diferencias ---------- */
    sealed record OSnap(string Id, string Name, string Status, double Paid, double Bal, double Lb, int Ph, double Cost);
    sealed record CSnap(string Name, double Pending, double Funds);
    (Dictionary<string, OSnap> o, List<string> oOrder, Dictionary<string, CSnap> c, List<string> cOrder)? _snap;

    (Dictionary<string, OSnap>, List<string>, Dictionary<string, CSnap>, List<string>) TakeSnap()
    {
        var o = new Dictionary<string, OSnap>(); var oo = new List<string>();
        foreach (var x in _app.Orders())
        {
            var id = Js.Str(x["id"]);
            if (!o.ContainsKey(id)) oo.Add(id);
            o[id] = new OSnap(id, x.Str("name"), x.Str("status").IfEmpty("Pedido"), Js.Num(x["paid"]), Js.Num(x["balance"]), Js.Num(x["pounds"]), (x["paymentHistory"] as JArr)?.Count ?? 0, Js.Num(x["cost"]));
        }
        var cl = new Dictionary<string, CSnap>(); var co = new List<string>();
        foreach (var c in _app.Clients()) { if (!cl.ContainsKey(c.Key)) co.Add(c.Key); cl[c.Key] = new CSnap(c.Name, c.Pending, c.Funds); }
        return (o, oo, cl, co);
    }
    public void ResetSnapshot() { try { _snap = TakeSnap(); } catch { _snap = null; } }

    static JObj Ref(string? order, string client) { var r = new JObj(); if (order != null) r["order"] = order; r["client"] = client; return r; }

    public void Diff()
    {
        var now = TakeSnap(); var prev = _snap; _snap = now;
        if (prev == null) return;
        if (Js.NowMs() < SuppressUntil) return;
        var (po, _, pcl, _) = prev.Value; var (no, noOrder, ncl, nclOrder) = now;
        if (po.Count == 0 && no.Count > 3) { Add(JObj.Of(("cat", "system"), ("type", "bulk"), ("title", "Datos recuperados"), ("text", $"Se cargaron {no.Count} encargos guardados."), ("short", "Datos recuperados"))); return; }
        var ev = new List<JObj>();
        foreach (var k in nclOrder) { var c = ncl[k]; if (!pcl.ContainsKey(k)) ev.Add(JObj.Of(("cat", "client"), ("type", "client_new"), ("title", "Cliente agregado"), ("text", $"{c.Name} fue agregado a tus clientes."), ("short", c.Name), ("ref", JObj.Of(("client", c.Name))))); }
        foreach (var id in noOrder)
        {
            var x = no[id]; po.TryGetValue(x.Id, out var p); var ord = _app.ById(x.Id);
            if (p == null)
            {
                ev.Add(JObj.Of(("cat", "order"), ("type", "order_new"), ("title", "Encargo creado"), ("text", $"{x.Name} · {Js.Money(ord?["total"])}"), ("short", x.Name), ("ref", Ref(x.Id, x.Name)),
                    ("data", JObj.Of(("total", Js.Num(ord?["total"])), ("status", x.Status)))));
                if (x.Paid > 0.005)
                {
                    var h = ((ord?["paymentHistory"] as JArr)?.LastOrDefault() as JObj) ?? new JObj();
                    ev.Add(JObj.Of(("cat", "pay"), ("type", "pay_new"), ("title", "Pago registrado"), ("text", $"{x.Name} realizó un pago de {Js.Money(x.Paid)}."), ("short", $"{x.Name} · {Js.Money(x.Paid)}"),
                        ("ref", Ref(x.Id, x.Name)), ("data", JObj.Of(("amount", x.Paid), ("method", h.Str("method").IfEmpty("Efectivo"))))));
                }
                continue;
            }
            if (p.Status != x.Status)
            {
                (string t, string tx, string icon, bool imp) m = x.Status switch
                {
                    "En tránsito" => ("Pedido en tránsito", $"El pedido de {x.Name} va en camino.", "truck", false),
                    "Recibido" => ("Pedido disponible", $"El pedido de {x.Name} ya llegó y está listo para entregar.", "box", true),
                    "Entregado" => ("Pedido entregado", $"El pedido de {x.Name} fue entregado.", "check", false),
                    "Cancelado" => ("Pedido archivado", $"El pedido de {x.Name} fue cancelado.", "box", false),
                    "Pedido" => ("Pedido actualizado", $"El pedido de {x.Name} volvió a \"Pedido\".", "box", false),
                    _ => ("Pedido actualizado", $"{x.Name} → {x.Status}", "box", false),
                };
                ev.Add(JObj.Of(("cat", "order"), ("type", "status"), ("title", m.t), ("text", m.tx), ("short", x.Name), ("icon", m.icon), ("imp", m.imp), ("ref", Ref(x.Id, x.Name)),
                    ("data", JObj.Of(("status", x.Status), ("from", p.Status)))));
            }
            if (x.Ph > p.Ph && x.Paid > p.Paid + 0.005)
            {
                var h = ((ord?["paymentHistory"] as JArr)?.LastOrDefault() as JObj) ?? new JObj(); var amt = x.Paid - p.Paid;
                ev.Add(JObj.Of(("cat", "pay"), ("type", "pay_new"), ("title", "Pago registrado"), ("text", $"{x.Name} realizó un pago de {Js.Money(amt)}."), ("short", $"{x.Name} · {Js.Money(amt)}"),
                    ("ref", Ref(x.Id, x.Name)), ("data", JObj.Of(("amount", amt), ("method", h.Str("method").IfEmpty("Efectivo"))))));
            }
            else if (Math.Abs(x.Paid - p.Paid) > 0.005)
            {
                var del = x.Ph < p.Ph;
                ev.Add(JObj.Of(("cat", "pay"), ("type", del ? "pay_del" : "pay_edit"), ("title", del ? "Pago eliminado" : "Pago modificado"), ("text", $"{x.Name}: lo pagado pasó de {Js.Money(p.Paid)} a {Js.Money(x.Paid)}."),
                    ("short", x.Name), ("ref", Ref(x.Id, x.Name)), ("data", JObj.Of(("amount", x.Paid - p.Paid)))));
            }
            else if (p.Status == x.Status && (Math.Abs(x.Lb - p.Lb) > 0.004 || Math.Abs(x.Cost - p.Cost) > 0.004))
            {
                ev.Add(JObj.Of(("cat", "order"), ("type", "order_edit"), ("title", "Encargo actualizado"),
                    ("text", Math.Abs(x.Lb - p.Lb) > 0.004 ? $"{x.Name}: {Js.ToFixed(p.Lb, 2)} → {Js.ToFixed(x.Lb, 2)} lb." : $"{x.Name}: costo actualizado a {Js.Money(x.Cost)}."),
                    ("short", x.Name), ("ref", Ref(x.Id, x.Name))));
            }
        }
        foreach (var p in po.Values) if (!no.ContainsKey(p.Id)) ev.Add(JObj.Of(("cat", "order"), ("type", "order_del"), ("title", "Encargo eliminado"), ("text", $"Se eliminó un encargo de {p.Name}."), ("short", p.Name), ("ref", JObj.Of(("client", p.Name)))));
        foreach (var k in nclOrder)
        {
            var c = ncl[k]; if (!pcl.TryGetValue(k, out var p)) continue;
            if (p.Pending > 0.005 && c.Pending <= 0.005) ev.Add(JObj.Of(("cat", "pay"), ("type", "settled"), ("title", "Saldo pendiente liquidado"), ("text", $"{c.Name} ya no debe nada."), ("short", c.Name), ("imp", true), ("ref", JObj.Of(("client", c.Name)))));
            if (p.Funds <= 0.005 && c.Funds > 0.005) ev.Add(JObj.Of(("cat", "pay"), ("type", "funds"), ("title", "Fondo a favor generado"), ("text", $"{c.Name} ahora tiene {Js.Money(c.Funds)} a favor."), ("short", $"{c.Name} · {Js.Money(c.Funds)}"),
                ("ref", JObj.Of(("client", c.Name))), ("data", JObj.Of(("amount", c.Funds)))));
        }
        if (ev.Count > 12) { Add(JObj.Of(("cat", "system"), ("type", "bulk"), ("title", "Datos actualizados"), ("text", $"Se actualizaron {no.Count} encargos de una vez."), ("short", "Actualización de datos"))); return; }
        foreach (var e in ev) Add(e);
    }

    static double D(string? s) => double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0.0;

    /// <summary>Eventos del sistema: respaldos y cambios de seguridad.</summary>
    public void SysEvents()
    {
        var lb = D(_app.Ls.Get("encargos_last_backup", "0")); var seen = D(_app.Ls.Get(K_SEEN_BK, "0"));
        if (lb > 0 && lb > seen)
        {
            _app.Ls.Set(K_SEEN_BK, Js.NumberToString(lb));
            if (seen > 0) Add(JObj.Of(("cat", "system"), ("type", "backup"), ("title", "Respaldo completado"),
                ("text", D(_app.Ls.Get("encargos_last_backup_enc", "0")) == lb ? "Se creó un respaldo cifrado de tus datos." : "Tus datos fueron respaldados correctamente."),
                ("short", "Respaldo guardado"), ("ref", JObj.Of(("panel", "backup")))));
        }
        var sl = _app.Ls.Arr("encargos_sec_log").Objs(); var ss = D(_app.Ls.Get(K_SEEN_SEC, "0"));
        var rel = new Regex("^(PIN creado|PIN cambiado|PIN desactivado|Huella activada|Huella desactivada|Windows Hello activado|Windows Hello desactivado|Protección de acciones sensibles|Privacidad en recientes|Privacidad de pantalla|Todos los datos eliminados|App restablecida|Bloqueo automático:)");
        if (sl.Count > 0)
        {
            if (ss == 0.0) _app.Ls.Set(K_SEEN_SEC, Js.NumberToString(Js.Num(sl[0]["t"])));
            else
            {
                foreach (var l in sl.Where(l => Js.Num(l["t"]) > ss && rel.IsMatch(l.Str("text"))).Reverse())
                    Add(JObj.Of(("cat", "system"), ("type", "security"), ("title", "Seguridad"), ("text", l.Str("text") + "."), ("short", l.Str("text")), ("t", Js.Num(l["t"])), ("ref", JObj.Of(("panel", "security")))));
                _app.Ls.Set(K_SEEN_SEC, Js.NumberToString(Js.Num(sl[0]["t"])));
            }
        }
    }
    public void ImportDone(int n)
    {
        SuppressUntil = Js.NowMs() + 1500; ResetSnapshot();
        Add(JObj.Of(("cat", "system"), ("type", "import"), ("title", "Importación completada"), ("text", $"Se importaron {n} encargos desde un respaldo."), ("short", "Respaldo importado"), ("ref", JObj.Of(("panel", "orders")))));
    }
}
