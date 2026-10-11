using Encargos.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using static Encargos.Win.K;

namespace Encargos.Win;

/* ======================= Centro de atención ======================= */
public sealed class AttentionPage : Screen
{
    public override string Title => "Centro de atención";
    public override string Nav => "attention";
    string _filter = "all"; bool _onlyPending; readonly HashSet<string> _open = new();

    public override void Build(StackPanel p)
    {
        var att = Core.Attention; var all = att.Compute();
        att.DetectResolved(all);
        var urgent = all.Count(a => a.Pr == "urgent"); var important = all.Count(a => a.Pr == "important"); var ready = all.Count(a => a.Cat == "ready");
        var due = all.Where(a => a.Cat == "pay").Sum(a => a.Amount ?? 0);
        Func<Attention.Alert, bool> fm = _filter switch
        {
            "urgent" => a => a.Pr == "urgent", "pay" => a => a.Cat == "pay", "orders" => a => a.Cat is "ready" or "transit",
            "clients" => a => a.Cat is "pay" or "funds", "other" => a => a.Cat is "incomplete" or "backup", _ => _ => true,
        };
        var list = all.Where(fm).Where(a => !_onlyPending || !att.IsReviewed(a)).ToList();
        SolidColorBrush Pc(string pr) => pr == "urgent" ? Rose : pr == "important" ? Amber : Blue;

        p.Children.Add(Card(Hs(10, Big((urgent + important).ToString(), null, 34), Muted(urgent + important == 1 ? "elemento requiere tu atención" : "elementos requieren tu atención", 14)),
            Cols(Kpi("Urgentes", urgent.ToString(), Rose), Kpi("Atención", important.ToString(), Amber), Kpi("Pedidos listos", ready.ToString(), Green), Kpi("Por cobrar", Money(due), Rose)),
            all.Count > 0 ? Btn("Marcar todo revisado", () => { att.ReviewAll(); S.Toast("Todo marcado como revisado. Los problemas siguen activos hasta resolverse."); }, false, null, null, false) : null));
        if (att.RecentResolved.Count > 0) p.Children.Add(Card(att.RecentResolved.Select(r => (UIElement)T("✓ " + r.Text, 14, false, Green)).ToArray()));
        p.Children.Add(Choice(new[] { ("all", "Todos"), ("urgent", "Urgentes"), ("pay", "Pagos"), ("orders", "Pedidos"), ("clients", "Clientes"), ("other", "Otros") }, _filter, k => { _filter = k; Redraw(); }));
        p.Children.Add(Toggle("Solo pendientes de revisar", "", _onlyPending, v => { _onlyPending = v; Redraw(); }));

        foreach (var (cat, title) in Attention.GROUPS)
        {
            var items = list.Where(a => a.Cat == cat).ToList(); if (items.Count == 0) continue;
            var pr = items.Any(a => a.Pr == "urgent") ? "urgent" : items.Any(a => a.Pr == "important") ? "important" : "info";
            var col = cat is "funds" or "ready" ? Green : Pc(pr); var tot = items.Sum(a => a.Amount ?? 0);
            var sub = cat switch
            {
                "pay" => $"{items.Count} cliente{(items.Count == 1 ? " debe" : "s deben")} dinero · total {Money(tot)}",
                "ready" => $"{items.Count} pedido{(items.Count == 1 ? " llegó" : "s llegaron")}",
                "transit" => $"{items.Count} pedido(s) con mucho tiempo en tránsito",
                "funds" => $"{items.Count} cliente(s) · total {Money(tot)}",
                "incomplete" => $"{items.Count} encargo(s) con datos faltantes", _ => "",
            };
            var rows = new List<UIElement>(); if (sub.Length > 0) rows.Add(Muted(sub));
            var open = _open.Contains(cat);
            foreach (var a in items.Take(open ? items.Count : 3)) rows.Add(AlertRow(a, Pc(a.Pr)));
            if (items.Count > 3) rows.Add(Link(open ? "Ver menos" : $"Ver los {items.Count}", () => { if (!_open.Remove(cat)) _open.Add(cat); Redraw(); }));
            if (cat is "pay" or "funds") rows.Add(Link("Lista de pendientes y fondos ›", () => S.Go(new PendingPage())));
            var ic = cat switch { "pay" => "\uE8C7", "ready" => "\uE7B8", "transit" => "\uE804", "funds" => "\uE8C7", "incomplete" => "\uE783", _ => "\uE946" };
            p.Children.Add(IconCard(ic, title, T(items.Count.ToString(), 18, true, col), rows.ToArray()));
        }
        if (list.Count == 0) p.Children.Add(Empty(all.Count > 0 ? "Nada en este filtro" : "Todo está en orden", all.Count > 0 ? "Prueba con otro filtro o desactiva \"Solo pendientes\"." : "En este momento no tienes elementos que requieran atención."));
        var lb = att.LastBackup();
        if (lb > 0 && Js.NowMs() - lb <= 7 * 86400000.0) p.Children.Add(Muted("Todo respaldado · último respaldo " + Js.Fmt(lb)));
        var acts = att.Activity();
        var actRows = new List<UIElement>(); if (acts.Count == 0) actRows.Add(Muted("Aquí verás tus pagos, entregas, avisos y respaldos.", 14));
        foreach (var a in acts.Take(5)) actRows.Add(ActRow(a));
        p.Children.Add(IconCard("\uE81C", "Actividad reciente", acts.Count > 5 ? Link("Ver todo", () => S.Info("Historial de actividad", V(6, att.Activity().Select(a => (UIElement)ActRow(a)).ToArray()))) : null, actRows.ToArray()));
    }
    static StackPanel ActRow(JObj a) => V(0, T(a.Str("text") + (Js.Num(a["amount"]) != 0 ? " — " + Money(a["amount"]) : ""), 14, true), Muted(Js.Fmt(a["t"]), 11));

    UIElement AlertRow(Attention.Alert a, SolidColorBrush col)
    {
        var att = Core.Attention; var rev = att.IsReviewed(a);
        var chips = new List<UIElement>();
        foreach (var ac in a.Actions) { var a2 = ac; chips.Add(Chip(ac.Label, ac.Kind is "deliver" or "remind" or "arrival" ? Green : Blue, () => Run(a2))); }
        chips.Add(Chip(rev ? "Revisada" : "Revisar", rev ? Mut : Vio, () => att.ToggleReview(a.Key)));
        return V(4, Lead(Dot(col), V(0, T(a.Title, 14, true, rev ? Mut : null), Muted(a.Sub)), a.Amount != null ? T((a.AmountGreen ? "+" : "") + Money(a.Amount.Value), 14, true, a.AmountGreen ? Green : Rose) : null),
            Chips(chips.ToArray()));
    }

    static void Run(Attention.Act ac)
    {
        var S = MainWindow.I; var core = Services.Core;
        switch (ac.Kind)
        {
            case "pay": case "edit": S.Go(new EditOrderPage(ac.OrderId!)); break;
            case "client": S.Go(new ClientPage(ac.Client!)); break;
            case "order": S.Go(new OrderPage(ac.OrderId!)); break;
            case "remind": { var c = core.FindClient(ac.Client!); if (c == null) break; if (c.Pending > 0.005) S.WhatsApp(c.Phone, core.ReminderClient(c), c.Name); else S.Toast("Este cliente no tiene saldo pendiente."); break; }
            case "arrival": { var c = core.FindClient(ac.Client!); if (c != null) S.WhatsApp(c.Phone, core.ArrivalMessage(c), c.Name); break; }
            case "deliver": { var x = core.ById(ac.OrderId); if (x == null) break; S.Ask("Pedido entregado", $"¿Marcar el pedido de {x.Str("name")} como entregado?", "Sí, entregado", false, () => { if (core.Deliver(x["id"])) S.Toast($"Pedido de {x.Str("name")} marcado como entregado"); }); break; }
            case "advance": { var x = core.ById(ac.OrderId); if (x != null) OrderActions.Advance(x); break; }
            case "backup": S.Root(new BackupPage()); break;
        }
    }
}

/* ======================= Notificaciones ======================= */
public sealed class NotifsPage : Screen
{
    public override string Title => "Notificaciones";
    public override string Nav => "notifs";
    string _filter = "all", _q = ""; int _limit = 40; StackPanel _list = new();

    public override void Build(StackPanel p)
    {
        _list = new StackPanel { Spacing = 6 };
        var nf = Core.Notifs; var cats = nf.VisibleCats(); var unread = nf.All().Where(n => !Js.Truthy(n["read"]) && cats.Contains(n.Str("cat"))).ToList();
        if (unread.Count > 0)
            p.Children.Add(Card(Hs(10, Big(unread.Count.ToString(), null, 30), Muted("notificaci" + (unread.Count == 1 ? "ón nueva" : "ones nuevas"), 14)),
                Muted(string.Join(" · ", new[] { ("pay", "pagos"), ("order", "pedidos"), ("client", "clientes"), ("system", "sistema") }.Where(c => cats.Contains(c.Item1)).Select(c => $"{unread.Count(n => n.Str("cat") == c.Item1)} {c.Item2}"))),
                Btn("Marcar todo leído", () => { nf.ReadAll(); S.Toast("Todas las notificaciones están leídas"); }, false, null, null, false)));
        p.Children.Add(Search(_q, "Buscar actividad: cliente, evento, fecha…", q => { _q = q; _limit = 40; Fill(); }));
        var opts = new[] { ("all", "Todas"), ("unread", "No leídas" + (unread.Count > 0 ? $" ({unread.Count})" : "")), ("pay", "Pagos"), ("order", "Pedidos"), ("client", "Clientes"), ("system", "Sistema") }
            .Where(o => o.Item1 is "all" or "unread" || cats.Contains(o.Item1));
        p.Children.Add(Choice(opts, _filter, k => { _filter = k; _limit = 40; Redraw(); }));
        p.Children.Add(_list); Fill();
        p.Children.Add(Cols(Btn("Ajustes de notificaciones", Settings),
            nf.All().Count > 0 ? Btn("Eliminar historial", () => S.Ask("¿Eliminar todas las notificaciones?", "Solo se borra el historial de notificaciones. Tus clientes, encargos y pagos no se tocan.", "Eliminar", true, () => { nf.Clear(); S.Toast("Historial de notificaciones eliminado"); }), false, Rose) : null));
    }
    static string Group(object? t)
    {
        var d = DateOnly.FromDateTime(Js.Local(Js.Num(t))); var diff = Js.Today().DayNumber - d.DayNumber;
        return diff <= 0 ? "Hoy" : diff == 1 ? "Ayer" : diff < 7 ? "Esta semana" : diff < 31 ? "Este mes" : "Anteriores";
    }
    void Fill()
    {
        _list.Children.Clear(); var nf = Core.Notifs;
        var items = nf.List(_filter, _q);
        if (items.Count == 0) _list.Children.Add(Empty(_q.Length > 0 || _filter != "all" ? "Sin resultados" : "Todo está al día", _q.Length > 0 || _filter != "all" ? "Prueba con otra búsqueda o filtro." : "No tienes nuevas notificaciones."));
        var g = "";
        foreach (var n in items.Take(_limit))
        {
            var gg = Group(n["t"]); if (gg != g) { g = gg; _list.Children.Add(T(gg, 13, true, Mut)); }
            var read = Js.Truthy(n["read"]); var col = n.Str("cat") == "pay" && n.Str("type") != "pay_del" ? Green : Js.Truthy(n["imp"]) ? Amber : Blue;
            var n2 = n;
            _list.Children.Add(Click(Lead(Dot(read ? LineBr : col, 10), V(0, T(n.Str("title"), 14, !read), Muted(n.Str("text"), 13), Muted(Notifications.CatLabel(n.Str("cat")) + " · " + Js.Fmt(n["t"]), 11))),
                () => { nf.Read(n2.Str("id")); Detail(n2); }));
        }
        if (items.Count > _limit) _list.Children.Add(Btn($"Ver más ({items.Count - _limit})", () => { _limit += 40; Fill(); }));
    }
    static async void Detail(JObj n)
    {
        var S = MainWindow.I; var core = Services.Core;
        var rf = n["ref"] as JObj; var ord = rf?["order"] != null ? core.ById(rf["order"]) : null; var client = rf?.Str("client") ?? ""; var panel = rf?.Str("panel") ?? "";
        var rows = new List<UIElement> { T(n.Str("text")) };
        if (client.Length > 0) rows.Add(Line("Cliente", client));
        if (n["data"] is JObj d)
        {
            if (d["amount"] != null) rows.Add(Line("Monto", Money(Math.Abs(Js.Num(d["amount"])))));
            if (d.Str("method").Length > 0) rows.Add(Line("Método", d.Str("method")));
            if (d.Str("status").Length > 0) rows.Add(Line("Estado", (d.Str("from").Length > 0 ? d.Str("from") + " → " : "") + d.Str("status")));
        }
        rows.Add(Line("Fecha", Js.Fmt(n["t"])));
        string? primary = ord != null ? (n.Str("cat") == "pay" ? "Ver pago" : "Ver pedido") : panel.Length > 0 ? "Abrir" : null;
        string? second = client.Length > 0 && core.FindClient(client) != null ? "Ver cliente" : null;
        var goClient = false;
        var ok = await S.Dialog(n.Str("title"), V(6, rows.ToArray()), primary ?? "Cerrar", null, primary != null ? "Cerrar" : null, second, () => goClient = true);
        if (goClient) { S.Go(new ClientPage(client)); return; }
        if (ok && primary != null)
        {
            if (ord != null) S.Go(new OrderPage(Js.Str(ord["id"])));
            else S.Root(panel == "backup" ? (Screen)new BackupPage() : panel == "security" ? new SecurityPage() : new OrdersPage());
        }
    }
    static async void Settings()
    {
        var nf = Services.Core.Notifs; var c = nf.Cfg();
        var rows = new[] { ("inapp", "Avisos dentro de la app"), ("important", "Notificaciones importantes"), ("pay", "Pagos"), ("order", "Pedidos"), ("client", "Clientes"), ("system", "Sistema (respaldos, importaciones y seguridad)") }
            .Select(o => (UIElement)Toggle(o.Item2, "", Js.Truthy(c[o.Item1]), _ => nf.ToggleCfg(o.Item1))).ToArray();
        await MainWindow.I.Dialog("Notificaciones", V(6, rows), "Listo", null, null);
    }
}
