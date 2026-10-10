using Encargos.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using static Encargos.Win.K;

namespace Encargos.Win;

public static class Payments
{
    /// <summary>Editar o eliminar un pago (mantiene lo pagado y el saldo coherentes).</summary>
    public static async void Edit(string orderId, int idx, Action? after = null)
    {
        var S = MainWindow.I; var core = Services.Core;
        var x = core.ById(orderId); if (x?["paymentHistory"] is not JArr ph || idx < 0 || idx >= ph.Count || ph[idx] is not JObj p) return;
        var amt = Js.NumberToString(Js.Num(p["amount"])); var method = p.Str("method").IfEmpty("Efectivo"); var note = p.Str("note");
        var del = false; var err = T("", 13, false, Rose);
        var ok = await S.Dialog("💵 Editar pago", V(10, Muted($"Pago de {x.Str("name")} · {Js.Fmt(p["date"])}", 13),
                Box("Cantidad ($)", amt, v => amt = v), Combo(new[] { ("Efectivo", "Efectivo"), ("USD", "USD") }, method, k => method = k, "Método"), Box("Nota", note, v => note = v), err),
            "Guardar", () =>
            {
                var r = core.PayEdit(x["id"], idx, Num(amt), method, note);
                if (!r.Ok) { err.Text = r.Error; return false; }
                return true;
            }, "Cancelar", "Eliminar pago", () => del = true);
        if (ok) { S.Toast("✅ Pago actualizado."); after?.Invoke(); }
        if (del)
            S.Ask("🗑️ Eliminar pago", $"¿Eliminar el pago de {Js.Money(p["amount"])} de {x.Str("name")}? El saldo del encargo se ajustará.", "Eliminar", true, () =>
            {
                var snap = core.PayDelete(x["id"], idx);
                if (snap != null) { S.Toast("Pago eliminado.", () => core.RestoreOrder(snap)); after?.Invoke(); }
            });
    }

    public static async void Goal()
    {
        var S = MainWindow.I; var st = Services.Stats; var g = st.Goal();
        var mode = g.Str("mode"); var amt = Js.Num(g["amount"]) > 0 ? Js.NumberToString(Js.Num(g["amount"])) : "";
        var err = T("", 13, false, Rose);
        var ok = await S.Dialog("🎯 Meta de cobro", V(10, Muted("El círculo se llena según lo cobrado frente a esta meta.", 13),
                Combo(new[] { ("auto", "Total de encargos activos"), ("monthly", "Meta del mes") }, mode, k => mode = k, "Tipo de meta"),
                Box("¿Cuánto quieres cobrar este mes? (solo para meta del mes)", amt, v => amt = v),
                Muted("Meta del mes: cuenta los pagos recibidos desde el día 1. Encargos activos: compara lo cobrado con el total de todos los encargos activos."), err),
            "Guardar meta", () =>
            {
                if (mode == "monthly" && !(Num(amt) > 0)) { err.Text = "Escribe una meta mayor que 0."; return false; }
                st.SaveGoal(mode, Num(amt)); return true;
            });
        if (ok) S.Toast("🎯 Meta actualizada.");
    }
}

/* ======================= Registro de pagos ======================= */
public sealed class PaymentsPage : Screen
{
    public override string Title => "Registro de pagos";
    public override string Nav => "payments";
    string _mf = ""; int _shown = 25;

    public override void Build(StackPanel p)
    {
        var s = Services.Stats.Payments();
        var list = s.List.Where(i => _mf.Length == 0 || i.P.Str("method").IfEmpty("Efectivo").ToUpperInvariant() == _mf.ToUpperInvariant()).ToList();
        var over = s.Collected > s.Target && s.Target > 0;
        p.Children.Add(Card(Lead(Gauge(s.Pct, "cobrado", 100), V(2, Row(T("Total cobrado", 12.5, true, Mut), Link("Meta ›", Payments.Goal)), Big(Money(s.Collected), Green, 30),
                Muted("de " + Money(s.Target) + (s.GoalMode == "monthly" ? " (meta del mes)" : " esperado"), 12.5),
                s.Pct > 100.5 ? T($"+{Math.Round(s.Pct - 100)}% sobre la meta", 12.5, true, Green) : null))));
        p.Children.Add(Cols(Kpi(s.GoalMode == "monthly" ? "Meta del mes" : "Meta (encargos)", s.Target > 0 ? Money(s.Target) : "Sin meta"),
            Kpi(over ? "Superado" : "Restante", over ? "+" + Money(s.Collected - s.Target) : Money(s.Remaining), over ? Green : null), Kpi("Pagos", s.List.Count.ToString())));
        var rows = new List<UIElement> { Choice(new[] { ("", "Todos"), ("Efectivo", "Efectivo"), ("USD", "USD") }, _mf, k => { _mf = k; _shown = 25; Redraw(); }) };
        if (list.Count == 0) rows.Add(Muted("Todavía no hay pagos registrados.", 14));
        foreach (var it in list.Take(_shown))
        {
            var i2 = it;
            rows.Add(Click(Row(V(0, T("Pago recibido · " + it.Client, 14, true), Muted($"{DayLabel(it.P["date"])} · {it.P.Str("method").IfEmpty("Efectivo")}" + (it.P.Str("note").Length > 0 ? " · " + it.P.Str("note") : ""))),
                T("+" + Money(it.P["amount"]), 15, true, Green)), () => Payments.Edit(i2.OrderId, i2.Idx)));
        }
        if (list.Count > _shown) rows.Add(Btn($"Ver más pagos ({list.Count - _shown})", () => { _shown += 25; Redraw(); }));
        if (list.Count > 0) rows.Add(Muted("Pulsa un pago para corregirlo o eliminarlo.", 11));
        p.Children.Add(IconCard("\uE8C7", "Movimientos recientes", Muted($"{list.Count} pago(s)"), rows.ToArray()));
    }
}

/* ======================= Pendientes y fondos a favor ======================= */
public sealed class PendingPage : Screen
{
    public override string Title => "Pendientes";
    public override string Nav => "pending";
    public override string? Right => $"{Services.Stats.Pending().Pend.Count} por cobrar";
    int _age;

    public override void Build(StackPanel p)
    {
        var pd = Services.Stats.Pending(); int Days(JObj x) => Core.Attention.DaysSince(x["date"]);
        var shown = pd.Pend.Where(x => _age == 0 || Days(x) >= _age).ToList();
        var nCli = pd.Pend.Select(x => x.Str("name").Trim().ToLowerInvariant()).Distinct().Count();
        p.Children.Add(Card(Cols(V(2, Muted("Por cobrar", 13), Big(Money(pd.PendTotal), Rose, 28), Muted($"{pd.Pend.Count} pedido{(pd.Pend.Count == 1 ? "" : "s")} · {nCli} cliente(s)")),
                V(2, Muted("Fondos a favor", 13), Big(Money(pd.FundsTotal), Green, 28), Muted($"{pd.Funds.Count} pedido{(pd.Funds.Count == 1 ? "" : "s")}"))),
            Progress(pd.PendTotal + pd.FundsTotal > 0 ? pd.PendTotal / (pd.PendTotal + pd.FundsTotal) : 0, Rose)));
        p.Children.Add(DotHeader("Saldo pendiente", Rose, Money(pd.PendTotal)));
        p.Children.Add(Choice(new[] { ("0", "Todas"), ("15", $"+15 días ({pd.Pend.Count(x => Days(x) >= 15)})"), ("30", $"+30 días ({pd.Pend.Count(x => Days(x) >= 30)})") }, _age.ToString(), k => { _age = int.Parse(k); Redraw(); }));
        if (shown.Count == 0) p.Children.Add(Empty("🎉 No hay saldos pendientes."));
        var cards = new List<UIElement>();
        foreach (var x in shown)
        {
            var n = Days(x); var id = Js.Str(x["id"]); double t = Js.Num(x["total"]), pdd = Js.Num(x["paid"]);
            var name = T(x.Str("name"), 15.5, true, null, false, 1); name.FontWeight = Microsoft.UI.Text.FontWeights.ExtraBold;
            var owe = V(0, Muted("Debe", 11), Big("- " + Money(x["balance"]), Rose, 20)); foreach (var c in owe.Children) ((FrameworkElement)c).HorizontalAlignment = HorizontalAlignment.Right;
            cards.Add(ClickCard(() => S.Go(new OrderPage(id)),
                Lead(Avatar(x.Str("name"), Rose), V(0, name, Muted(x.Str("phone").IfEmpty("Sin teléfono"))), owe),
                Row(Chips(Chip(x.Str("status").IfEmpty("Pedido"), Status(x.Str("status"))), Chip(Core.Attention.Ago(x["date"]), n >= 30 ? Rose : Mut)), Muted($"Total {Money(t)} · Pagó {Money(pdd)}", 12)),
                Progress(t > 0 ? pdd / t : 0),
                Cols(CardBtn("Cobrar", "\uE8BD", () => S.WhatsApp(x.Str("phone"), Core.ReminderOrder(x), x.Str("name")), Green), CardBtn("Ficha", "\uE8A5", () => S.Go(new OrderPage(id))),
                    CardBtn("Registrar pago", "\uE70F", () => S.Go(new EditOrderPage(id))))));
        }
        if (cards.Count > 0) p.Children.Add(Grid2(cards, S.ContentWidth < 760 ? 1 : 2));
        p.Children.Add(DotHeader("Fondos a favor", Green, Money(pd.FundsTotal)));
        if (pd.Funds.Count == 0) p.Children.Add(Empty("Ningún cliente tiene saldo a favor."));
        var fcards = new List<UIElement>();
        foreach (var x in pd.Funds)
        {
            var id = Js.Str(x["id"]);
            fcards.Add(ClickCard(() => S.Go(new OrderPage(id)),
                Lead(Avatar(x.Str("name"), Green), V(0, T(x.Str("name"), 15, true), Muted(x.Str("phone").IfEmpty("Sin teléfono"))), T("+ " + Money(-Js.Num(x["balance"])), 15, true, Green)),
                Muted("🟢 Este importe queda a favor del cliente.")));
        }
        if (fcards.Count > 0) p.Children.Add(Grid2(fcards, S.ContentWidth < 760 ? 1 : 2));
    }
}

/* ======================= Estadísticas ======================= */
public sealed class StatsPage : Screen
{
    public override string Title => "Estadísticas";
    public override string Nav => "stats";
    Stats.Period _period = Stats.Period.Month; DateOnly _ref = Js.Today();

    public override void Build(StackPanel p)
    {
        var st = Services.Stats;
        var (a, b) = st.Range(_period, _ref); var (pa, pb) = st.Range(_period, st.Shift(_period, _ref, -1));
        var cur = st.Calc(a, b); var prev = st.Calc(pa, pb);
        var today = Js.Today(); var isNow = today >= a && today < b;
        UIElement Delta(double n, double o)
        {
            var d = st.Pct(n, o);
            var txt = d == null ? "▲ nuevo" : Math.Abs(d.Value) < 0.5 ? "= igual que antes" : (d > 0 ? "▲ " : "▼ ") + Js.ToFixed(Math.Abs(d.Value), 0) + "% vs anterior";
            return T(txt, 11, false, d == null ? BlueInk : Math.Abs(d.Value) < 0.5 ? Mut : d > 0 ? Green : Rose);
        }
        UIElement KpiD(string label, string value, Microsoft.UI.Xaml.Media.Brush? color, double n, double o) =>
            V(4, Kpi(label, value, color), Delta(n, o));
        p.Children.Add(Choice(new[] { ("Week", "Semana"), ("Month", "Mes"), ("Year", "Año") }, _period.ToString(), k => { _period = Enum.Parse<Stats.Period>(k); _ref = Js.Today(); Redraw(); }));
        var prevB = Btn("‹", () => { _ref = st.Shift(_period, _ref, -1); Redraw(); }, false, null, null, false);
        var nextB = Btn("›", () => { _ref = st.Shift(_period, _ref, 1); Redraw(); }, false, null, null, false); nextB.IsEnabled = !isNow;
        var lbl = T(st.Label(_period, a, b) + (isNow ? " · actual" : ""), 16, true); lbl.HorizontalAlignment = HorizontalAlignment.Center;
        p.Children.Add(Lead(prevB, lbl, nextB));
        p.Children.Add(Card(Lead(Gauge(cur.Ventas > 0 ? cur.Cobrado / cur.Ventas * 100 : cur.Cobrado > 0 ? 100 : 0, "cobrado", 100),
            V(2, T("Dinero cobrado", 12.5, true, Mut), Big(Money(cur.Cobrado), Green, 30), Delta(cur.Cobrado, prev.Cobrado), Muted($"de {Money(cur.Ventas)} vendido"))), Bars(st.Buckets(_period, a, b), null, 120)));
        p.Children.Add(Cols(KpiD("Ventas", Money(cur.Ventas), null, cur.Ventas, prev.Ventas), KpiD("Encargos", cur.Ord.Count.ToString(), null, cur.Ord.Count, prev.Ord.Count),
            KpiD("Libras", Js.ToFixed(cur.Lb, 1) + " lb", null, cur.Lb, prev.Lb)));
        p.Children.Add(Cols(KpiD("Ganancia", Signed(cur.Gan), cur.Gan >= 0 ? Green : Rose, cur.Gan, prev.Gan), KpiD("Gastos", Money(cur.Gastos), null, cur.Gastos, prev.Gastos),
            KpiD("Ganancia neta", Signed(cur.Neta), cur.Neta >= 0 ? Green : Rose, cur.Neta, prev.Neta)));
        var top = st.TopClients(cur); var mx = top.Count == 0 ? 1 : top.Max(t => t.Total);
        var topRows = new List<UIElement>(); if (top.Count == 0) topRows.Add(Muted("Sin encargos en este periodo.", 14));
        foreach (var (n, t, c) in top) { topRows.Add(T(n, 14, true)); topRows.Add(Muted($"{Money(t)} · {c} encargo{(c == 1 ? "" : "s")}")); topRows.Add(Progress(t / mx, Blue)); }
        var ex = st.ExpensesIn(a, b); var exRows = new List<UIElement>();
        if (ex.Count == 0) exRows.Add(Muted("Anota transporte, aduana, empaques y otros gastos para ver tu ganancia neta real.", 13));
        foreach (var e in ex)
        {
            var e2 = e;
            exRows.Add(Click(Row(V(0, T(e.Str("concept"), 14, true), Muted((EncargosApp.EXP_CATS.FirstOrDefault(c => c.Item1 == e.Str("cat")).Item2 ?? "Otros") + " · " + DayLabel(e["date"]))),
                T("-" + Money(e["amount"]), 14, true)), () => ExpenseDialog(e2)));
        }
        p.Children.Add(Cols(IconCard("\uE734", "Mejores clientes", null, topRows.ToArray()), IconCard("\uE8EF", "Gastos del periodo", Link("+ Agregar", () => ExpenseDialog(null)), exRows.ToArray())));
        var ina = st.InactiveClients(); var inRows = new List<UIElement>();
        if (ina.Count == 0) inRows.Add(Muted("Todos tus clientes compraron en los últimos 30 días. 👏", 14));
        foreach (var (c, last) in ina) { var cc = c; inRows.Add(Lead(Avatar(c.Name, Mut, 34), V(0, T(c.Name, 14, true), Muted("Última compra " + Core.Attention.Ago(last))), Btn("Escribir", () => S.WhatsApp(cc.Phone, "", cc.Name), false, null, null, false))); }
        p.Children.Add(IconCard("\uE716", "Clientes inactivos", null, inRows.ToArray()));
    }

    static async void ExpenseDialog(JObj? e)
    {
        var S = MainWindow.I; var core = Services.Core;
        var concept = e?.Str("concept") ?? ""; var amount = e != null ? Js.NumberToString(Js.Num(e["amount"])) : "";
        var date = e != null ? DateOnly.FromDateTime(Js.Local(Js.ParseDate(e["date"]))) : Js.Today(); var cat = e?.Str("cat").IfEmpty("transporte") ?? "transporte";
        var dp = new CalendarDatePicker { Header = "Fecha", Date = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue)) };
        var err = T("", 13, false, Rose); var del = false;
        var ok = await S.Dialog(e == null ? "🧾 Nuevo gasto" : "🧾 Editar gasto",
            V(10, Box("Concepto (ej. Taxi al aeropuerto)", concept, v => concept = v), Box("Monto ($)", amount, v => amount = v), dp,
                Combo(EncargosApp.EXP_CATS.Select(c => (c.Item1, c.Item2)), cat, k => cat = k, "Categoría"), err),
            "Guardar gasto", () =>
            {
                var d = dp.Date?.DateTime ?? DateTime.Now;
                var r = core.SaveExpense(e?.Str("id"), concept, Num(amount), d.ToString("yyyy-MM-dd"), cat);
                if (!r.Ok) { err.Text = r.Error; return false; }
                return true;
            }, "Cancelar", e != null ? "Eliminar" : null, () => del = true);
        if (ok) S.Toast("✅ Gasto guardado.");
        if (del && e != null) S.Ask("🧾 Eliminar gasto", "¿Eliminar este gasto?", "Eliminar", true, () => { core.DeleteExpense(e.Str("id")); S.Toast("Gasto eliminado."); });
    }
}
