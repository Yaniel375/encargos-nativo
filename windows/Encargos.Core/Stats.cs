namespace Encargos.Core;

/// <summary>Paneles numéricos: Inicio, Pendientes, Registro de pagos y Estadísticas (mismos cálculos que la app web).</summary>
public sealed class Stats
{
    readonly EncargosApp _app;
    public Stats(EncargosApp app) { _app = app; }
    static double T(object? v) => Js.ParseDate(v);
    static double Ms(DateOnly d) => Js.MsOf(d);
    static double Z(double v) => double.IsNaN(v) ? 0.0 : v;

    static readonly string[] MonthFull = { "enero", "febrero", "marzo", "abril", "mayo", "junio", "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre" };
    static readonly string[] MonthShort = { "ene", "feb", "mar", "abr", "may", "jun", "jul", "ago", "sept", "oct", "nov", "dic" };
    static readonly string[] MonthNarrow = { "E", "F", "M", "A", "M", "J", "J", "A", "S", "O", "N", "D" };
    static readonly string[] DowNarrow = { "D", "L", "M", "X", "J", "V", "S" }; // índice = DayOfWeek (domingo = 0)

    public sealed record Dashboard(int Clients, int Count, double Pounds, double Paid, double Pending, double Funds, int PendingOrders, int Transit, int Received,
        List<JObj> Recent, List<(string Label, int N, double Total)> ByStatus, List<(string Name, double Debt, int N)> TopDebts, int Debtors);

    public Dashboard GetDashboard()
    {
        var ad = _app.ActiveOrders();
        var pend = ad.Where(o => Js.Num(o["balance"]) > 0).ToList();
        var recent = ad.OrderByDescending(o => Z(T(o["updated"] ?? o["date"]))).Take(5).ToList();
        var states = new[] { ("Pedido", "Pedido"), ("En tránsito", "En tránsito"), ("Entregados", "Entregado") };
        var byStatus = states.Select(s => { var g = ad.Where(o => Js.Eq(o["status"], s.Item2)).ToList(); return (s.Item1, g.Count, g.Sum(o => Js.Num(o["total"]))); }).ToList();
        var debt = new Dictionary<string, (string, double, int)>(); var order = new List<string>();
        foreach (var x in pend)
        {
            var k = x.Str("name").Trim().ToLowerInvariant();
            if (!debt.TryGetValue(k, out var d)) { d = (x.Str("name"), 0.0, 0); order.Add(k); }
            debt[k] = (d.Item1 == "" ? x.Str("name") : d.Item1, d.Item2 + Js.Num(x["balance"]), d.Item3 + 1);
        }
        var debts = order.Select(k => debt[k]).OrderByDescending(d => d.Item2).ToList();
        return new Dashboard(_app.ActiveClients().Count, _app.Data.Count, ad.Sum(o => Js.Num(o["pounds"])), ad.Sum(o => Js.Num(o["paid"])),
            ad.Sum(o => Math.Max(0.0, Js.Num(o["balance"]))), ad.Sum(o => Math.Max(0.0, -Js.Num(o["balance"]))), pend.Count,
            ad.Count(o => Js.Eq(o["status"], "En tránsito")), ad.Count(o => Js.Eq(o["status"], "Recibido")), recent, byStatus, debts.Take(5).ToList(), debts.Count);
    }

    List<JObj> AllPays() => _app.Orders().SelectMany(o => (o["paymentHistory"] as JArr)?.Objs() ?? new List<JObj>()).ToList();

    /// <summary>Gráfica de ingresos: últimos 7 días (hora local) o últimos 6 meses.</summary>
    public List<(string Label, double Value)> Revenue(bool week)
    {
        var pays = AllPays();
        if (week)
        {
            var names = new[] { "Dom", "Lun", "Mar", "Mié", "Jue", "Vie", "Sáb" };
            var totals = new Dictionary<DateOnly, double>();
            foreach (var p in pays) { var m = T(p["date"]); if (!double.IsNaN(m)) { var d = DateOnly.FromDateTime(Js.Local(m)); totals[d] = totals.GetValueOrDefault(d) + Js.Num(p["amount"]); } }
            return Enumerable.Range(0, 7).Reverse().Select(i => { var d = Js.Today().AddDays(-i); return (names[(int)d.DayOfWeek] + " " + d.Day, totals.GetValueOrDefault(d)); }).ToList();
        }
        var months = new[] { "Ene", "Feb", "Mar", "Abr", "May", "Jun", "Jul", "Ago", "Sep", "Oct", "Nov", "Dic" };
        var t = Js.Today(); var b = new DateOnly(t.Year, t.Month, 1);
        return Enumerable.Range(0, 6).Reverse().Select(i =>
        {
            var m = b.AddMonths(-i);
            // monthsBack usa toISOString().slice(0,7) de la medianoche local del día 1 (igual que la web)
            var k2 = Js.IsoOf((long)Ms(m))[..7];
            var parts = k2.Split('-'); var lbl = months[int.Parse(parts[1]) - 1] + " " + parts[0][^2..];
            return (lbl, pays.Where(p => Js.Str(p["date"]).Take(7) == k2).Sum(p => Js.Num(p["amount"])));
        }).ToList();
    }

    public sealed record PendingR(List<JObj> Pend, List<JObj> Funds, double PendTotal, double FundsTotal);
    public PendingR Pending()
    {
        var ad = _app.ActiveOrders();
        var pend = ad.Where(o => Js.Num(o["balance"]) > 0).OrderByDescending(o => Js.Num(o["balance"])).ToList();
        var fund = ad.Where(o => Js.Num(o["balance"]) < 0).OrderByDescending(o => Js.Num(o["balance"])).ToList();
        return new PendingR(pend, fund, pend.Sum(o => Js.Num(o["balance"])), fund.Sum(o => -Js.Num(o["balance"])));
    }

    /* ---------- Registro de pagos ---------- */
    public sealed record PayItem(string OrderId, int Idx, string Client, JObj P);
    public sealed record PaySummary(string GoalMode, double GoalAmount, List<PayItem> List, double Collected, double Target, double Pct, double Remaining);
    public JObj Goal() { var g = _app.Ls.JsonOf("encargos_pay_goal") as JObj; return g != null && (Js.Eq(g["mode"], "auto") || Js.Eq(g["mode"], "monthly")) ? g : JObj.Of(("mode", "auto"), ("amount", 0.0)); }
    public void SaveGoal(string mode, double amount) { _app.Ls.Put("encargos_pay_goal", JObj.Of(("mode", mode), ("amount", amount))); _app.Changed(false); }
    public PaySummary Payments()
    {
        var g = Goal(); var monthly = Js.Eq(g["mode"], "monthly");
        var src = monthly ? _app.Orders() : _app.ActiveOrders();
        var now = Js.Today(); var first = new DateOnly(now.Year, now.Month, 1); var from = Ms(first); var to = Ms(first.AddMonths(1));
        var list = new List<PayItem>();
        foreach (var x in src)
        {
            if (x["paymentHistory"] is not JArr ph) continue;
            for (var idx = 0; idx < ph.Count; idx++)
            {
                if (ph[idx] is not JObj pp) continue;
                if (monthly) { var tt = T(pp["date"]); if (!(tt >= from && tt < to)) continue; }
                list.Add(new PayItem(Js.Str(x["id"]), idx, x.Str("name"), pp));
            }
        }
        list = list.OrderByDescending(i => Z(T(i.P["date"]))).ToList();
        var collected = list.Sum(i => Js.Num(i.P["amount"]));
        var target = monthly ? Js.Num(g["amount"]) : _app.ActiveOrders().Sum(o => Math.Max(0.0, Js.Num(o["total"])));
        return new PaySummary(Js.Str(g["mode"]), Js.Num(g["amount"]), list, collected, target, target > 0 ? collected / target * 100 : 0.0, Math.Max(0.0, target - collected));
    }

    /* ---------- Estadísticas por periodo ---------- */
    public enum Period { Week, Month, Year }
    public (DateOnly A, DateOnly B) Range(Period p, DateOnly r) => p switch
    {
        Period.Week => r.AddDays(-(((int)r.DayOfWeek + 6) % 7)) is var a ? (a, a.AddDays(7)) : default,
        Period.Year => (new DateOnly(r.Year, 1, 1), new DateOnly(r.Year + 1, 1, 1)),
        _ => (new DateOnly(r.Year, r.Month, 1), new DateOnly(r.Year, r.Month, 1).AddMonths(1)),
    };
    public DateOnly Shift(Period p, DateOnly r, int k) => p switch { Period.Week => r.AddDays(7 * k), Period.Year => r.AddYears(k), _ => r.AddMonths(k) };
    static bool InR(object? v, DateOnly a, DateOnly b) { var x = T(v); return x >= Ms(a) && x < Ms(b); }

    public sealed record R(List<JObj> Ord, double Cobrado, double Ventas, double Lb, double Gan, double Gastos) { public double Neta => Gan - Gastos; }
    public R Calc(DateOnly a, DateOnly b)
    {
        var ord = _app.Orders().Where(o => !Js.Eq(o["status"], "Cancelado") && InR(o["date"], a, b)).ToList();
        var cobrado = AllPays().Where(p => InR(p["date"], a, b)).Sum(p => Js.Num(p["amount"]));
        var gastos = _app.Expenses().Where(e => InR(e["date"], a, b)).Sum(e => Js.Num(e["amount"]));
        return new R(ord, cobrado, ord.Sum(o => Js.Num(o["total"])), ord.Sum(o => Js.Num(o["pounds"])), ord.Sum(o => _app.Profit(o)), gastos);
    }
    /// <summary>pct(n,o): null = "nuevo"</summary>
    public double? Pct(double n, double o) => o == 0.0 ? (n > 0 ? (double?)null : 0.0) : (n - o) / Math.Abs(o) * 100;

    public string Label(Period p, DateOnly a, DateOnly b)
    {
        var e = b.AddDays(-1);
        return p switch
        {
            Period.Week => $"{a.Day} {MonthShort[a.Month - 1]} – {e.Day} {MonthShort[e.Month - 1]}",
            Period.Year => a.Year.ToString(),
            _ => char.ToUpper(MonthFull[a.Month - 1][0]) + MonthFull[a.Month - 1][1..] + " de " + a.Year,
        };
    }
    public List<(string Label, double Value)> Buckets(Period p, DateOnly a, DateOnly b)
    {
        var ks = new List<(string, DateOnly, DateOnly)>();
        switch (p)
        {
            case Period.Year: for (var m = 1; m <= 12; m++) { var s = new DateOnly(a.Year, m, 1); ks.Add((MonthNarrow[m - 1], s, s.AddMonths(1))); } break;
            case Period.Week: for (var i = 0; i < 7; i++) { var s = a.AddDays(i); ks.Add((DowNarrow[(int)s.DayOfWeek], s, s.AddDays(1))); } break;
            default: { var s = a; var w = 1; while (s < b) { var e = s.AddDays(7); ks.Add(("S" + w++, s, e < b ? e : b)); s = e; } break; }
        }
        var pays = AllPays();
        return ks.Select(k => (k.Item1, pays.Where(x => InR(x["date"], k.Item2, k.Item3)).Sum(x => Js.Num(x["amount"])))).ToList();
    }
    public List<(string Name, double Total, int N)> TopClients(R r)
    {
        var by = new Dictionary<string, (string, double, int)>(); var order = new List<string>();
        foreach (var x in r.Ord) { var k = x.Str("name").Trim(); if (!by.TryGetValue(k, out var o)) { o = (k, 0, 0); order.Add(k); } by[k] = (k, o.Item2 + Js.Num(x["total"]), o.Item3 + 1); }
        return order.Select(k => by[k]).OrderByDescending(v => v.Item2).Take(5).ToList();
    }
    public List<(EncargosApp.Client Client, double Last)> InactiveClients()
    {
        var now = Js.NowMs();
        return _app.Clients().Select(c => (c, c.OrderList.Count == 0 ? double.NegativeInfinity : c.OrderList.Max(o => { var v = T(o["date"]); return double.IsNaN(v) ? double.NegativeInfinity : v; })))
            .Where(t => now - t.Item2 > 30 * 86400000.0).OrderBy(t => t.Item2).Take(5).ToList();
    }
    public List<JObj> ExpensesIn(DateOnly a, DateOnly b) => _app.Expenses().Where(e => InR(e["date"], a, b)).OrderByDescending(e => T(e["date"])).ToList();

    /// <summary>Panel mensual antiguo (agrupa por la fecha UTC, como la app web).</summary>
    public (List<JObj> Orders, List<PayItem> Pays, double Total) Monthly(string ym)
    {
        var orders = _app.ActiveOrders().Where(o => Js.Str(o["date"]).Take(7) == ym).ToList();
        var pays = new List<PayItem>();
        foreach (var x in _app.Orders()) if (x["paymentHistory"] is JArr ph) for (var i = 0; i < ph.Count; i++) if (ph[i] is JObj pp && Js.Str(pp["date"]).Take(7) == ym) pays.Add(new PayItem(Js.Str(x["id"]), i, x.Str("name"), pp));
        return (orders, pays, pays.Sum(p => Js.Num(p.P["amount"])));
    }
}
