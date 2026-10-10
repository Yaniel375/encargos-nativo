using System.Globalization;
using Encargos.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using static Encargos.Win.K;

namespace Encargos.Win;

public sealed class HomePage : Screen
{
    public override string Title => "Inicio";
    public override string Nav => "home";
    bool _week = true; string _q = "";
    StackPanel _results = new() { Spacing = 8 };

    public override void Build(StackPanel p)
    {
        _results = new StackPanel { Spacing = 8 };
        var d = Services.Stats.GetDashboard(); var ps = Services.Stats.Payments();
        var date = DateTime.Now.ToString("dddd, d 'de' MMMM 'de' yyyy", new CultureInfo("es-ES"));
        p.Children.Add(V(2, H("¡Hola, " + Core.GreetName + "!", 26), Muted(char.ToUpper(date[0]) + date[1..], 13)));
        p.Children.Add(Search(_q, "Buscar clientes, encargos o notas", q => { _q = q; FillResults(); }));
        p.Children.Add(_results); FillResults();

        p.Children.Add(Card(Lead(Gauge(ps.Pct, "cobrado", 128), V(4, Muted("Total cobrado", 13), Big(Money(ps.Collected), Green),
            Muted("de " + Money(ps.Target) + (ps.GoalMode == "monthly" ? " (meta del mes)" : " esperado")), Link("Ver registro de pagos ›", () => S.Go(new PaymentsPage()))))));
        p.Children.Add(Cols(Kpi("Clientes", d.Clients.ToString()), Kpi("Encargos", d.Count.ToString()), Kpi("Libras", Js.ToFixed(d.Pounds, 2)),
            Kpi("Cobrado", Money(d.Paid), Green), Kpi("Pendiente", "- " + Money(d.Pending), Rose), Kpi("A favor", "+ " + Money(d.Funds), Teal)));

        var all = Core.Attention.Compute(); var n = Core.Attention.Badge(all);
        p.Children.Add(Card("Requiere tu atención", Link("Abrir ›", () => S.Go(new AttentionPage())),
            d.PendingOrders > 0 ? T($"🔴 {d.PendingOrders} pedido(s) con saldo pendiente.") : null,
            d.Transit > 0 ? T($"🚚 {d.Transit} pedido(s) en tránsito.") : null,
            d.Received > 0 ? T($"📦 {d.Received} pedido(s) recibidos.") : null,
            d.PendingOrders == 0 && d.Transit == 0 && d.Received == 0 ? Muted("Todo está al día.", 14) : null,
            n > 0 ? T($"{n} aviso(s) sin revisar en el Centro de atención", 13, false, Amber) : null));

        var items = Services.Stats.Revenue(_week); var total = items.Sum(i => i.Value);
        var revenue = Card("Ingresos", null,
            Choice(new[] { ("w", "Últimos 7 días"), ("m", "Últimos 6 meses") }, _week ? "w" : "m", k => { _week = k == "w"; Redraw(); }),
            Big(Money(total), null, 22), Muted("cobrado en " + (_week ? "los últimos 7 días" : "los últimos 6 meses")),
            items.Any(i => i.Value > 0) ? (UIElement)Bars(items) : Muted("Aún no hay pagos en este periodo.", 13));
        var nn = Math.Max(1, Core.Data.Count);
        var byStatus = Card("Por estado", null, d.ByStatus.Select(s => (UIElement)V(4, Row(T(s.Label), T(s.N.ToString(), 14, true)),
            Progress(s.N / (double)nn, s.Label == "Pedido" ? Amber : s.Label == "En tránsito" ? Blue : Green), Muted(Money(s.Total) + " en total"))).ToArray());
        p.Children.Add(Cols(revenue, byStatus));

        var debts = new List<UIElement>();
        if (d.TopDebts.Count == 0) debts.Add(Muted("Nadie tiene saldo pendiente.", 14));
        foreach (var (name, bal, cnt) in d.TopDebts)
            debts.Add(Click(Lead(Avatar(name, Rose, 34), V(0, T(name, 14, true), Muted($"{cnt} encargo(s)")), T(Money(bal), 15, true, Rose)), () => S.Go(new ClientPage(name))));
        if (d.Debtors > 5) debts.Add(Muted($"y {d.Debtors - 5} más"));
        var recent = new List<UIElement>();
        if (d.Recent.Count == 0) recent.Add(Muted("Sin actividad.", 14));
        foreach (var x in d.Recent)
            recent.Add(Click(V(0, T(x.Str("name"), 14, true), Muted($"{x.Str("status")} · {Js.Fmt(x["updated"] ?? x["date"])} · {Money(x["total"])}")), () => S.Go(new OrderPage(Js.Str(x["id"])))));
        p.Children.Add(Cols(Card("Quién me debe", Link("Ver todos ›", () => S.Go(new PendingPage())), debts.ToArray()), Card("Actividad reciente", null, recent.ToArray())));
    }

    void FillResults()
    {
        _results.Children.Clear();
        if (string.IsNullOrWhiteSpace(_q)) return;
        var words = Js.NormSearch(_q).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        bool M(string t) { var h = Js.NormSearch(t); return words.All(h.Contains); }
        var cl = Core.Clients().Where(c => M(c.Name + " " + c.Phone)).Take(5).ToList();
        var od = Core.Orders().Where(x => M(string.Join(" ", x.Str("name"), x.Str("phone"), x.Str("status"), x.Str("note")))).OrderByDescending(x => Js.ParseDate(x["date"])).Take(5).ToList();
        var nt = Core.Notes().Where(n => M(n.Str("title") + " " + n.Str("body"))).Take(5).ToList();
        var items = new List<UIElement>();
        if (cl.Count == 0 && od.Count == 0 && nt.Count == 0) items.Add(Muted($"Sin resultados para \"{_q}\".", 14));
        if (cl.Count > 0) items.Add(T("Clientes", 14, true, Vio));
        foreach (var c in cl) items.Add(Click(T($"{c.Name} · {c.OrderList.Count} encargo(s)" + (c.Pending > 0.005 ? $" · debe {Money(c.Pending)}" : "")), () => S.Go(new ClientPage(c.Name))));
        if (od.Count > 0) items.Add(T("Encargos", 14, true, Blue));
        foreach (var x in od) items.Add(Click(T($"{x.Str("name")} · {Money(x["total"])} · {x.Str("status").IfEmpty("Pedido")}"), () => S.Go(new OrderPage(Js.Str(x["id"])))));
        if (nt.Count > 0) items.Add(T("Notas", 14, true, Amber));
        foreach (var n in nt) items.Add(Click(T(n.Str("title").IfEmpty("Sin título")), () => S.Root(new NotesPage())));
        _results.Children.Add(Card(items.ToArray()));
    }
}
