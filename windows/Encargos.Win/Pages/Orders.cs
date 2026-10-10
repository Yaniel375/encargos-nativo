using Encargos.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using static Encargos.Win.K;

namespace Encargos.Win;

/// <summary>Acciones comunes sobre un encargo.</summary>
public static class OrderActions
{
    static MainWindow S => MainWindow.I; static EncargosApp Core => Services.Core;

    public static void Advance(JObj x)
    {
        var prev = Core.Advance(x["id"]);
        if (prev == null) { S.Toast("Este encargo ya está entregado."); return; }
        S.Toast($"📦 {x.Str("name")}: {prev} → {x.Str("status")}", () => Core.SetStatus(x["id"], prev));
    }
    public static void Delete(JObj x)
    {
        S.RequireAuth("Eliminar un encargo.", () =>
            S.Ask("🗑️ Eliminar encargo", $"¿Eliminar el encargo de {x.Str("name")}? Esta acción no se puede deshacer.", "Eliminar", true, () =>
            {
                var r = Core.RemoveOrder(x["id"]); if (r == null) return;
                if (S.Top is OrderPage or EditOrderPage) S.Back();
                S.Toast($"🗑️ Encargo de {x.Str("name")} eliminado.", () => { if (Core.UndoRemove(r)) S.Toast($"↩️ Encargo de {x.Str("name")} restaurado."); });
            }));
    }
    public static async void PickDate(JObj x)
    {
        var t = Js.ParseDate(x["date"]); if (double.IsNaN(t)) t = Js.NowMs();
        var cur = Js.Local(t);
        var dp = new DatePicker { Header = "Fecha", Date = new DateTimeOffset(cur) };
        var tp = new TimePicker { Header = "Hora", ClockIdentifier = "24HourClock", Time = cur.TimeOfDay };
        if (await S.Dialog("📅 Cambiar fecha", V(10, dp, tp), "Guardar"))
        {
            var local = dp.Date.Date + tp.Time;
            var ms = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), TimeZoneInfo.Local)).ToUnixTimeMilliseconds();
            if (Core.SetDate(x["id"], Js.IsoOf(ms))) S.Toast("✅ Fecha actualizada.");
        }
    }
    public static async void EditNote(JObj x)
    {
        var t = x.Str("note");
        var box = Box("Nota del encargo", t, v => t = v, true);
        if (await S.Dialog("📝 Nota interna", V(8, Muted("Solo para ti: el cliente nunca la ve.", 13), box), "Guardar") && Core.SetNote(x["id"], t)) S.Toast("✅ Nota guardada.");
    }
    /// <summary>Menú ⋯ de un encargo: fecha, nota, recibo, recordar, agradecer, eliminar.</summary>
    public static Button Menu(JObj x)
    {
        var f = new MenuFlyout();
        void Add(string text, Action a) { var i = new MenuFlyoutItem { Text = text }; i.Click += (_, _) => a(); f.Items.Add(i); }
        Add("📅 Cambiar fecha", () => PickDate(x));
        Add("📝 Nota interna", () => EditNote(x));
        Add("📤 Enviar recibo al cliente", () => Media.Receipt(x));
        if (Js.Num(x["balance"]) > 0.005) Add("🔔 Recordar pago", () => S.WhatsApp(x.Str("phone"), Core.ReminderOrder(x), x.Str("name")));
        Add("🙏 Enviar agradecimiento", () => S.WhatsApp(x.Str("phone"), Core.ThanksOrder(x), x.Str("name")));
        f.Items.Add(new MenuFlyoutSeparator());
        Add("🗑️ Eliminar", () => Delete(x));
        var b = CardBtn(null, "\uE712", () => { }, null, "Opciones del encargo"); b.Flyout = f; b.MinWidth = 44; b.HorizontalAlignment = HorizontalAlignment.Left; return b;
    }
    public static void EditPhotoDelete(JObj x, int idx)
    {
        S.Ask("📷 Eliminar foto", "¿Eliminar esta foto del encargo?", "Eliminar", true, () => Core.DeleteOrderPhoto(x["id"], x.Str("photo").Length == 0 ? idx : idx - 1));
    }
    public static void ViewPhotos(JObj x, int i)
    {
        var photos = Core.OrderPhotos(x);
        Media.Viewer(photos, i, i > 0 || x.Str("photo").Length == 0 ? (Action<int>?)(idx => EditPhotoDelete(x, idx)) : null);
    }

    public static Border Card(JObj x)
    {
        var b = Js.Num(x["balance"]); var id = Js.Str(x["id"]); var g = Core.Profit(x); var st = x.Str("status").IfEmpty("Pedido"); var nx = Core.NextStatus(st);
        double t = Js.Num(x["total"]), pd = Js.Num(x["paid"]);
        var name = T(x.Str("name"), 16, true, null, false, 1); name.FontWeight = Microsoft.UI.Text.FontWeights.ExtraBold;
        var actions = new Grid { ColumnSpacing = 8 };
        foreach (var w in new[] { 1.0, 1.0, 1.25 }) actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w, GridUnitType.Star) });
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        UIElement[] btns = { CardBtn("Ficha", "\uE8A5", () => S.Go(new OrderPage(id))), CardBtn("Editar", "\uE70F", () => S.Go(new EditOrderPage(id))), CardBtn("WhatsApp", "\uE8BD", () => S.WhatsApp(x.Str("phone"), "", x.Str("name")), Green), Menu(x) };
        for (var i = 0; i < btns.Length; i++) { Grid.SetColumn((FrameworkElement)btns[i], i); actions.Children.Add(btns[i]); }
        return ClickCard(() => S.Go(new OrderPage(id)),
            Lead(Avatar(x.Str("name"), Status(st)), V(1, name, Muted($"{Core.ShippingLabel(Core.TypeOf(x))} · {Lb(Js.Num(x["pounds"]))} · {Js.Fmt(x["date"])}", 12), Muted($"{Core.OrderLabel(x)} · {x.Str("phone").IfEmpty("Sin número")}", 11.5)),
                Chip(st + (nx != null ? "  →" : ""), Status(st), nx != null ? () => Advance(x) : () => S.Toast("Este encargo ya está entregado."))),
            Cols(Kpi("Total", Money(t)), Kpi("Pagado", Money(pd), Green), Kpi("Saldo", b > 0.005 ? "- " + Money(b) : b < -0.005 ? "+ " + Money(-b) : "$0.00", b > 0.005 ? Rose : Green, b < -0.005 ? "a favor" : null)),
            Row(Js.Num(x["pounds"]) > 0 ? Chips(Chip("Ganancia " + Signed(g), g >= 0 ? Green : Rose)) : (UIElement)new Border(), Muted("Envío " + Money(x["shipping"]), 12)),
            Progress(t > 0 ? pd / t : 0),
            x.Str("note").Length > 0 ? Muted("📝 " + x.Str("note")) : null,
            actions);
    }
}

/* ======================= Lista de encargos activos ======================= */
public sealed class OrdersPage : Screen
{
    public override string Title => "Encargos";
    public override string Nav => "orders";
    public override string? Right => $"{Core.ActiveOrders().Count} activos";
    string _q = "", _f = ""; int _limit = 30; StackPanel _list = new();

    public override void Build(StackPanel p)
    {
        _list = new StackPanel { Spacing = 12 };
        var ad = Core.ActiveOrders();
        p.Children.Add(Search(_q, "Buscar por cliente, teléfono, código o estado", q => { _q = q; _limit = 30; Fill(); }));
        p.Children.Add(Choice(new[] { ("", "Todos") }.Concat(EncargosApp.STATES.Select(s => (s, s))), _f, k => { _f = k; _limit = 30; Redraw(); }));
        p.Children.Add(TopTiles(("Total", Money(ad.Sum(x => Js.Num(x["total"]))), null), ("Cobrado", Money(ad.Sum(x => Js.Num(x["paid"]))), Green), ("Pendiente", Money(ad.Sum(x => Math.Max(0, Js.Num(x["balance"])))), Rose)));
        if (ad.Count > 0) { var tip = Muted("Consejo: pulsa el estado de un encargo para pasarlo al siguiente.", 12.5); tip.HorizontalAlignment = HorizontalAlignment.Center; p.Children.Add(tip); }
        p.Children.Add(_list);
        p.Children.Add(CompletedLink(Core.CompletedClients().Count, () => S.Go(new CompletedPage())));
        Fill();
    }
    void Fill()
    {
        _list.Children.Clear();
        var ad = Core.ActiveOrders();
        var words = Js.NormSearch(_q).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var list = ad.Where(x =>
        {
            var hay = Js.NormSearch(string.Join(" ", x.Str("name"), x.Str("phone"), x.Str("accessCode"), Js.Str(x["orderNo"]), Core.OrderLabel(x), x.Str("status"), x.Str("shippingType"), x.Str("note")));
            return words.All(hay.Contains) && (_f.Length == 0 || Js.Eq(x["status"], _f));
        }).ToList();
        if (ad.Count == 0) _list.Children.Add(Empty("No hay encargos activos", "Los encargos culminados se guardan en Encargos completados.", Btn("+ Nuevo encargo", () => S.Root(new NewOrderPage()), true, null, null, false)));
        else if (list.Count == 0) _list.Children.Add(Empty("Sin resultados", "No hay encargos que coincidan con tu búsqueda o filtro.", Btn("Limpiar búsqueda", () => { _q = ""; _f = ""; Redraw(); }, false, null, null, false)));
        _list.Children.Add(Grid2(list.Take(_limit).Select(x => (UIElement)OrderActions.Card(x)).ToList(), S.ContentWidth < 760 ? 1 : 2));
        if (list.Count > _limit) _list.Children.Add(Btn($"Ver más ({list.Count - _limit} restantes)", () => { _limit += 30; Fill(); }));
    }
}

/* ======================= Ficha del encargo ======================= */
public sealed class OrderPage : Screen
{
    readonly string _id;
    public OrderPage(string id) { _id = id; }
    public override string Title => "Ficha del encargo";
    public override string Nav => "orders";

    public override void Build(StackPanel p)
    {
        var x = Core.ById(_id);
        if (x == null) { p.Children.Add(Empty("Este encargo ya no existe.")); return; }
        double t = Js.Num(x["total"]), pd = Js.Num(x["paid"]), b = Js.Num(x["balance"]); var st = x.Str("status").IfEmpty("Pedido");
        var g = Core.Profit(x); var nx = Core.NextStatus(st); var photos = Core.OrderPhotos(x);
        p.Children.Add(Card(
            Lead(Avatar(x.Str("name"), Status(st), 52), V(2, H(x.Str("name")), Muted($"{Core.OrderLabel(x)} · {x.Str("phone").IfEmpty("Sin teléfono")} · {Js.Fmt(x["date"])}")), OrderActions.Menu(x)),
            Chips(Chip(st, Status(st)), Chip(Core.ShippingName(Core.TypeOf(x)) + " · " + Lb(Js.Num(x["pounds"])), Teal)),
            Row(T($"Pagado {Money(pd)} de {Money(t)}"), T(b > 0.005 ? $"Debe {Money(b)}" : b < -0.005 ? $"A favor {Money(-b)}" : "Pagado completo", 14, true, b > 0.005 ? Rose : Green)),
            Progress(t > 0 ? pd / t : 1)));
        if (st == "Cancelado") p.Children.Add(Card(Muted("Este encargo está cancelado.", 14)));
        else
        {
            p.Children.Add(Card("Estado del pedido", nx != null ? Link($"Pasar a {nx} ›", () => OrderActions.Advance(x)) : null, Stepper(EncargosApp.FLOW, st)));
        }
        p.Children.Add(Cols(Kpi("Pedido", Money(x["cost"])), Kpi("Envío", Money(x["shipping"]), null, $"{Js.ToFixed(Js.Num(x["pounds"]), 2)} × {Money(x["lbCost"])}"), Kpi("Total", Money(t), Blue),
            Kpi("Pagado", Money(pd), Green), Kpi(b < -0.005 ? "A favor" : "Saldo", b > 0.005 ? "-" + Money(b) : b < -0.005 ? "+" + Money(-b) : "$0.00", b > 0.005 ? Rose : Green),
            Kpi("Ganancia", Signed(g), g >= 0 ? Green : Rose, Money(Core.MarginOf(x)) + "/lb")));
        p.Children.Add(Cols(Btn("Editar", () => S.Go(new EditOrderPage(_id)), true, null, ""), Btn("WhatsApp", () => S.WhatsApp(x.Str("phone"), "", x.Str("name")), false, Green, ""),
            Btn("Enviar recibo", () => Media.Receipt(x), false, null, ""),
            b > 0.005 ? Btn("Recordar pago", () => S.WhatsApp(x.Str("phone"), Core.ReminderOrder(x), x.Str("name")), false, Rose) : Btn("Agradecer", () => S.WhatsApp(x.Str("phone"), Core.ThanksOrder(x), x.Str("name")))));
        p.Children.Add(Card("Nota interna", Link(x.Str("note").Length == 0 ? "Agregar" : "Editar", () => OrderActions.EditNote(x)),
            x.Str("note").Length == 0 ? Muted("Solo para ti: el cliente nunca la ve.", 14) : T(x.Str("note"))));
        var ph = Hs(8, photos.Select((s, i) => (UIElement)Media.Photo(s, 110, 110, () => OrderActions.ViewPhotos(x, i))).ToArray());
        p.Children.Add(Card($"Fotos ({photos.Count})", Link("Agregar foto", async () => { var d = await Media.PickPhoto(900, 0.55); if (d != null && Core.AddOrderPhoto(x["id"], d)) S.Toast("✅ Foto guardada."); }),
            photos.Count == 0 ? (UIElement)Muted("Todavía no hay fotos de esta compra.", 14) : new ScrollViewer { Content = ph, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, HorizontalScrollMode = ScrollMode.Enabled }));
        var ev = new List<(object? d, string txt, string val, Microsoft.UI.Xaml.Media.Brush col)>();
        ev.Add((x["date"], $"Encargo creado · {Core.ShippingName(Core.TypeOf(x))} · {Money(x["lbCost"])}/lb", Money(x["cost"]), Blue));
        foreach (var h in (x["paymentHistory"] as JArr)?.Objs() ?? new()) ev.Add((h["date"], "Pago · " + h.Str("method").IfEmpty("Efectivo") + (h.Str("note").Length > 0 ? " · " + h.Str("note") : ""), "+" + Money(h["amount"]), Green));
        foreach (var h in (x["lbHistory"] as JArr)?.Objs() ?? new()) ev.Add((h["date"], $"Libras: {Js.ToFixed(Js.Num(h["from"]), 2)} → {Js.ToFixed(Js.Num(h["to"]), 2)}", (Js.Num(h["change"]) >= 0 ? "+" : "") + Js.ToFixed(Js.Num(h["change"]), 2) + " lb", Mut));
        p.Children.Add(Card("Historial", null, ev.OrderByDescending(e => { var v = Js.ParseDate(e.d); return double.IsNaN(v) ? 0 : v; })
            .Select(e => (UIElement)Row(V(0, T(e.txt), Muted(Js.Fmt(e.d), 11)), T(e.val, 14, true, e.col))).ToArray()));
        p.Children.Add(Btn("Ver ficha del cliente", () => S.Go(new ClientPage(x.Str("name"))), false, null, ""));
    }
}

/* ======================= Editar encargo (estado, libras, pago, fotos) ======================= */
public sealed class EditOrderPage : Screen
{
    readonly string _id; string? _st; string _addLb = "", _pay = "", _method = "Efectivo", _payNote = "";
    public EditOrderPage(string id) { _id = id; }
    public override string Title => "Editar encargo";
    public override string Nav => "orders";
    public override bool Live => false;
    StackPanel _sum = new(); TextBlock _gain = new();

    public override void Build(StackPanel p)
    {
        var x = Core.ById(_id);
        if (x == null) { p.Children.Add(Empty("Este encargo ya no existe.")); return; }
        _st ??= x.Str("status").IfEmpty("Pedido");
        _sum = new StackPanel { Spacing = 8 }; _gain = Muted("");
        p.Children.Add(Card(Lead(Avatar(x.Str("name"), Status(_st), 48), V(2, H(x.Str("name"), 18), Muted(x.Str("phone").IfEmpty("Sin teléfono")))),
            Chips(Chip("🔒 " + Core.ShippingName(Core.TypeOf(x)) + " · " + Money(x["lbCost"]) + "/lb", Teal)),
            Muted("El tipo de envío y la tarifa quedan fijos para conservar el precio original.")));
        p.Children.Add(Card("Resumen", null, _sum));
        p.Children.Add(Card("Estado", null, Choice(EncargosApp.STATES.Select(s => (s, s)), _st, k => { _st = k; Redraw(); })));
        TextBox lbBox = null!, payBox = null!;
        lbBox = Box("Libras a agregar", _addLb, v => { _addLb = v; Calc(x); });
        p.Children.Add(Card("Agregar libras", Muted("Ahora: " + Lb(Js.Num(x["pounds"]))), lbBox,
            Cols(new[] { 0.5, 1, 2, 5 }.Select(q => (UIElement)Btn("+" + Js.NumberToString(q), () => lbBox.Text = Js.ToFixed(Num(lbBox.Text) + q, 2))).ToArray()), _gain));
        payBox = Box("Cantidad del pago ($)", _pay, v => { _pay = v; Calc(x); });
        double Due() => Math.Max(0, Js.Num(x["cost"]) + Js.Num(x["lbCost"]) * (Js.Num(x["pounds"]) + Num(_addLb)) - Js.Num(x["paid"]));
        p.Children.Add(Card("Registrar pago", null, payBox,
            Cols(Btn("Todo el saldo", () => payBox.Text = Js.ToFixed(Due(), 2)), Btn("La mitad", () => payBox.Text = Js.ToFixed(Due() / 2, 2)), Btn("Sin pago", () => payBox.Text = "")),
            Combo(new[] { ("Efectivo", "Efectivo"), ("USD", "USD") }, _method, k => _method = k, "Método"),
            Box("Nota del pago (ej. Abono)", _payNote, v => _payNote = v)));
        p.Children.Add(Btn("Guardar cambios", () => Save(x), true, null, ""));
        var photos = Core.OrderPhotos(x);
        p.Children.Add(Card("Fotos del encargo", null,
            Hs(8, photos.Select((s, i) => (UIElement)Media.Photo(s, 96, 96, () => OrderActions.ViewPhotos(x, i))).ToArray()),
            Btn("Agregar foto", async () => { var d = await Media.PickPhoto(900, 0.55); if (d != null && Core.AddOrderPhoto(x["id"], d)) { S.Toast("✅ Foto guardada."); Redraw(); } }, false, null, "")));
        var ph = (x["paymentHistory"] as JArr)?.Objs() ?? new();
        var rows = new List<UIElement>();
        if (ph.Count == 0) rows.Add(Muted("Sin pagos", 14));
        for (var i = ph.Count - 1; i >= 0; i--)
        {
            var h = ph[i]; var idx = i;
            rows.Add(Click(Row(V(0, T(Js.Fmt(h["date"])), Muted(h.Str("method").IfEmpty("Pago") + (h.Str("note").Length > 0 ? " · " + h.Str("note") : ""))), T("+ " + Money(h["amount"]), 14, true, Green)),
                () => Payments.Edit(_id, idx, Redraw)));
        }
        if (ph.Count > 0) rows.Add(Muted("Pulsa un pago para corregirlo o eliminarlo.", 11));
        p.Children.Add(Card("Historial de pagos", null, rows.ToArray()));
        var lh = (x["lbHistory"] as JArr)?.Objs() ?? new();
        p.Children.Add(Card("Historial de libras", null, lh.Count == 0 ? new UIElement[] { Muted("Sin registros", 14) } :
            Enumerable.Reverse(lh).Select(h => (UIElement)Line(Js.Fmt(h["date"]), $"{Js.ToFixed(Js.Num(h["from"]), 2)} → {Js.ToFixed(Js.Num(h["to"]), 2)} lb")).ToArray()));
        Calc(x);
    }
    void Calc(JObj x)
    {
        var lbv = Num(_addLb); var pv = Num(_pay); var pp = Js.Num(x["pounds"]) + lbv;
        var tot = Js.Num(x["cost"]) + Js.Num(x["lbCost"]) * pp; var pd = Js.Num(x["paid"]) + pv; var bal = tot - pd;
        _sum.Children.Clear();
        _sum.Children.Add(Line("Total", Money(tot), null, true)); _sum.Children.Add(Line("Pagado", Money(pd), Green));
        _sum.Children.Add(Line("Saldo", bal > 0 ? "- " + Money(bal) : bal < 0 ? "+ " + Money(-bal) : "Pagado completo", bal > 0 ? Rose : Green, true));
        _sum.Children.Add(Progress(tot > 0 ? pd / tot : 1));
        _sum.Children.Add(Muted($"{Math.Round((tot > 0 ? Math.Clamp(pd / tot, 0, 1) : 1) * 100)}% pagado"));
        var g = pp * Core.MarginOf(x);
        _gain.Text = $"{Js.ToFixed(pp, 2)} lb × ({Money(x["lbCost"])} − {Money(Core.BuyOf(x))}) = {Signed(g)}"; _gain.Foreground = g >= 0 ? Green : Rose;
    }
    void Save(JObj x)
    {
        var before = Core.CompletedClients().Select(c => c.Key).ToHashSet();
        var r = Core.SaveEdit(x["id"], Num(_addLb), Num(_pay), _method, _payNote, _st ?? "Pedido");
        if (!r.Ok) { S.Toast(r.Error.IfEmpty("No se pudo guardar.")); return; }
        var moved = Core.CompletedClients().Where(c => !before.Contains(c.Key)).ToList();
        S.Toast(moved.Count > 0 ? $"✅ {string.Join(", ", moved.Select(c => c.Name))} completó todos sus pagos. Ahora está en Encargos completados." : "✅ Cambios guardados.");
        S.Back();
    }
}

/* ======================= Encargos completados ======================= */
public sealed class CompletedPage : Screen
{
    public override string Title => "Encargos completados";
    public override string Nav => "completed";
    string _q = ""; StackPanel _list = new();
    public override void Build(StackPanel p)
    {
        _list = new StackPanel { Spacing = 12 };
        p.Children.Add(Muted($"Clientes con todos sus pagos completados ({Core.CompletedClients().Count}).", 14));
        p.Children.Add(Search(_q, "Buscar cliente", q => { _q = q; Fill(); }));
        p.Children.Add(_list); Fill();
    }
    void Fill()
    {
        _list.Children.Clear();
        var all = Core.CompletedClients().Where(c => _q.Trim().Length == 0 || Js.NormSearch(c.Name + " " + c.Phone).Contains(Js.NormSearch(_q))).ToList();
        if (all.Count == 0) _list.Children.Add(Empty("Aún no hay clientes con todos sus pagos completados."));
        foreach (var c in all)
        {
            var items = new List<UIElement> { Lead(Avatar(c.Name, Green), V(0, T(c.Name, 15, true), Muted($"{c.Phone.IfEmpty("Sin teléfono")} · {c.OrderList.Count} encargo(s)"))) };
            foreach (var o in c.OrderList.OrderByDescending(o => Js.ParseDate(o["date"]))) items.Add(Line($"{Js.Fmt(o["date"])} · {o.Str("status")}", Money(o["total"])));
            items.Add(Line("Libras", Lb(c.Pounds))); items.Add(Line("Total cobrado", Money(c.Paid), Green, true)); items.Add(T("✓ Todos los pagos completados", 14, true, Green));
            var name = c.Name; _list.Children.Add(ClickCard(() => S.Go(new ClientPage(name)), items.ToArray()));
        }
    }
}
