using System.Globalization;
using Encargos.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using static Encargos.Win.K;

namespace Encargos.Win;

public sealed class HomePage : Screen
{
    public override string Title => "Inicio";
    public override string Nav => "home";
    string _q = ""; bool _week = true;
    StackPanel _results = new() { Spacing = 8 };

    public override bool ShowTitle => false;

    /// <summary>Misma composición que la app web en PC: saludo, buscador, "Por cobrar" con anillo, 4 accesos, estado de los pedidos y últimos pagos.</summary>
    public override void Build(StackPanel p)
    {
        _results = new StackPanel { Spacing = 8 };
        var d = Services.Stats.GetDashboard(); var ps = Services.Stats.Payments(); var active = Core.ActiveOrders();
        var hi = H("¡Hola, " + Core.GreetName + "!", 24); hi.Margin = new Thickness(2, 4, 0, 0);
        p.Children.Add(hi);
        p.Children.Add(Search(_q, "Buscar clientes, encargos o notas", q => { _q = q; FillResults(); }));
        p.Children.Add(_results); FillResults();

        // .aHero: anillo + "Por cobrar"
        var total = d.Paid + d.Pending; var pct = total > 0 ? d.Paid / total * 100 : 0;
        Grid Leg(Brush c, string l, string v, Brush? vc = null) { var g = Lead(Dot(c, 8), T(l, 13.5, false, Mut), T(v, 13.5, true, vc, false)); g.ColumnSpacing = 8; return g; }
        var hero = Card(Lead(Ring(pct, "cobrado", 100), V(2, T("Por cobrar", 12.5, true, Mut), Counted(Big("", null, 32), d.Pending, Money), V(3, Leg(Green, "Cobrado", Money(d.Paid), Green), Leg(Rose, "Pendiente", Money(d.Pending), Rose)))));
        hero.Padding = new Thickness(20);
        UIElement Stat(string glyph, string label, string value, Brush? vc, Action go, double? num = null, Func<double, string>? fmt = null)
        {
            var lab = T(label, 12.5, true, Mut, false, 1); var val = NoWrap(Big(value, vc, 18.5));
            if (num != null && fmt != null) Counted(val, num.Value, fmt);
            var c = ClickCard(go, Lead(IconTile(glyph), V(1, lab, val), new FontIcon { Glyph = "", FontSize = 11, Foreground = Mut }));
            c.Padding = new Thickness(0); ((Button)c.Child).Padding = new Thickness(12); return c;
        }
        var stats = new Grid { RowSpacing = 12, ColumnSpacing = 12 };
        stats.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); stats.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        stats.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); stats.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var cells = new[] { Stat("", "Encargos", active.Count.ToString(), null, () => S.Root(new OrdersPage()), active.Count, v => Math.Round(v).ToString()), Stat("", "Clientes", d.Clients.ToString(), null, () => S.Root(new ClientsPage()), d.Clients, v => Math.Round(v).ToString()),
            Stat("", "Libras", Js.ToFixed(d.Pounds, 2) + " lb", null, () => S.Root(new StatsPage()), d.Pounds, v => Js.ToFixed(v, 2) + " lb"), Stat("", "Fondos a favor", "+ " + Money(d.Funds), Green, () => S.Root(new PendingPage()), d.Funds, v => "+ " + Money(v)) };
        for (var i = 0; i < 4; i++) { var fe = (FrameworkElement)cells[i]; Grid.SetRow(fe, i / 2); Grid.SetColumn(fe, i % 2); stats.Children.Add(fe); }
        var narrow = S.ContentWidth < 900;   // ventana estrecha: las tarjetas se apilan
        if (narrow) { p.Children.Add(hero); p.Children.Add(stats); }
        else
        {
            var top = new Grid { ColumnSpacing = 12 };
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            top.Children.Add(hero); Grid.SetColumn(stats, 1); top.Children.Add(stats);
            p.Children.Add(top);
        }

        // Estado de los pedidos + Últimos pagos
        var ped = d.ByStatus.Count > 0 ? d.ByStatus[0].N : 0; var tra = (d.ByStatus.Count > 1 ? d.ByStatus[1].N : 0) + d.Received; var ent = d.ByStatus.Count > 2 ? d.ByStatus[2].N : 0;
        var seg = new Grid { ColumnSpacing = 5, Height = 10 };
        var parts = new[] { (ped, Blue), (tra, Status("En tránsito")), (ent, Green) }.Where(x => x.Item1 > 0).ToList(); var n = 0;
        foreach (var (v, c) in parts) { seg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(v, GridUnitType.Star) }); var bb = new Border { CornerRadius = new CornerRadius(5), Background = c }; Grid.SetColumn(bb, n++); seg.Children.Add(bb); }
        if (parts.Count == 0) seg.Children.Add(new Border { CornerRadius = new CornerRadius(5), Background = Track });
        UIElement Col(string l, int v, Brush c, string? sub) { var s = V(2, Hs(6, Dot(c, 7), T(l, 12.5, true, Mut, false)), Big(v.ToString(), null, 22), sub != null ? Muted(sub, 10.5) : null); s.HorizontalAlignment = HorizontalAlignment.Center; foreach (var ch in s.Children) ((FrameworkElement)ch).HorizontalAlignment = HorizontalAlignment.Center; return s; }
        var estado = IconCard("", "Estado de los pedidos", Link($"{active.Count} activos ›", () => S.Root(new OrdersPage())), seg,
            Cols(Col("Pedido", ped, Blue, null), Col("En tránsito", tra, Status("En tránsito"), d.Received > 0 ? $"incluye {d.Received} recibido{(d.Received == 1 ? "" : "s")}" : null), Col("Entregado", ent, Green, null)));
        var rows = new List<UIElement>();
        var list = ps.List.Take(4).ToList();
        if (list.Count == 0) rows.Add(Muted("Aún no hay pagos registrados.", 13));
        var orders = Core.Orders();
        for (var i = 0; i < list.Count; i++)
        {
            var it = list[i]; var x = orders.FirstOrDefault(o => Js.Str(o["id"]) == it.OrderId);
            var done = x != null && Js.Num(x["total"]) > 0 && Js.Num(x["balance"]) <= 0.005;
            var id = it.OrderId;
            if (i > 0) rows.Add(new Border { Height = 1, Background = LineBr });
            rows.Add(Click(Row(V(0, T(PayDate(it.P["date"]), 12.5, true), T(it.Client, 13.5, true, null, false, 1)), T(it.P.Str("method").IfEmpty("Efectivo"), 12, false, Mut, false), T(Money(it.P["amount"]), 15, true, null, false), done ? Chip("Completado", Green) : null), () => S.Go(new OrderPage(id))));
        }
        var pagos = IconCard("", "Últimos pagos", Link("Ver todos ›", () => S.Go(new PaymentsPage())), rows.ToArray());
        if (narrow) { p.Children.Add(estado); p.Children.Add(pagos); } else p.Children.Add(Cols(estado, pagos));

        // Actividad del mes (datos reales de Estadísticas) + ingresos recientes
        var st = Services.Stats; var (ma, mb) = st.Range(Stats.Period.Month, Js.Today()); var m = st.Calc(ma, mb);
        var kpis = Cols(Kpi("Cobrado", Money(m.Cobrado), Green), Kpi("Ventas", Money(m.Ventas)), Kpi("Encargos", m.Ord.Count.ToString()), Kpi("Ganancia neta", Signed(m.Neta), m.Neta >= 0 ? Green : Rose));
        var rev = st.Revenue(_week);
        var actividad = IconCard("", "Actividad de " + st.Label(Stats.Period.Month, ma, mb).Split(" de ")[0].ToLowerInvariant(), Link("Estadísticas ›", () => S.Root(new StatsPage())), kpis,
            Choice(new[] { ("w", "Últimos 7 días"), ("m", "Últimos 6 meses") }, _week ? "w" : "m", k => { _week = k == "w"; Redraw(); }),
            rev.Any(i => i.Value > 0) ? Bars(rev, null, 110) : Muted("Aún no hay cobros en este periodo.", 13));
        p.Children.Add(actividad);
    }

    static TextBlock Counted(TextBlock t, double v, Func<double, string> fmt) { Fx.Count(t, v, fmt); return t; }
    static TextBlock NoWrap(TextBlock t) { t.TextWrapping = TextWrapping.NoWrap; t.TextTrimming = TextTrimming.CharacterEllipsis; return t; }
    static string PayDate(object? v)
    {
        var t = Js.ParseDate(v); if (double.IsNaN(t)) return "";
        var s = Js.Local(t).ToString("dd MMM yyyy", new CultureInfo("es-ES")).Replace(".", "");
        return string.Join(" ", s.Split(' ').Select(w => w.Length > 0 ? char.ToUpper(w[0]) + w[1..] : w));
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
