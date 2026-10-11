using Encargos.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using static Encargos.Win.K;
using static Encargos.Win.ClientCards;

namespace Encargos.Win;

public sealed class ClientsPage : Screen
{
    public override string Title => "Clientes";
    public override string Nav => "clients";
    public override string? Right => Plural(Core.ActiveClients().Count, "activo", "activos");
    string _q = ""; int _limit = 30; StackPanel _list = new();

    public override void Build(StackPanel p)
    {
        _list = new StackPanel { Spacing = 12 };
        p.Children.Add(Search(_q, "Buscar cliente, teléfono o código", q => { _q = q; _limit = 30; Fill(); }));
        var all = Core.ActiveClients();
        p.Children.Add(TopTiles(("Clientes", all.Count.ToString(), null), ("Por cobrar", Money(all.Sum(c => c.Pending)), Rose), ("A favor", Money(all.Sum(c => c.Funds)), Green)));
        p.Children.Add(_list);
        p.Children.Add(CompletedLink(Core.CompletedClients().Count, () => S.Go(new CompletedPage())));
        Fill();
    }
    void Fill()
    {
        _list.Children.Clear();
        var all = Core.ActiveClients();
        var words = Js.NormSearch(_q).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var list = all.Where(c =>
        {
            var hay = Js.NormSearch(string.Join(" ", c.Name, c.Phone, string.Join(" ", c.OrderList.Select(o => string.Join(" ", o.Str("accessCode"), Js.Str(o["orderNo"]), o.Str("status"))))));
            return words.All(hay.Contains);
        }).ToList();
        if (all.Count == 0) _list.Children.Add(Empty("Aún no tienes clientes", "Los clientes aparecen automáticamente al crear encargos.", Btn("+ Nuevo encargo", () => S.Root(new NewOrderPage()), true, null, null, false)));
        else if (list.Count == 0) _list.Children.Add(Empty("Sin resultados", "Ningún cliente coincide con tu búsqueda."));
        _list.Children.Add(Grid2(list.Take(_limit).Select(c => (UIElement)ClientCard(c)).ToList(), S.ContentWidth < 760 ? 1 : 2));
        if (list.Count > _limit) _list.Children.Add(Btn($"Ver más ({list.Count - _limit} restantes)", () => { _limit += 30; Fill(); }));
    }
}

public static class ClientCards
{
    public static Border ClientCard(EncargosApp.Client c)
    {
        var S = MainWindow.I; var Core = Services.Core;
        var total = c.OrderList.Sum(o => Js.Num(o["total"]));
        var last = c.OrderList.OrderByDescending(o => { var d = Js.ParseDate(o["date"]); return double.IsNaN(d) ? 0 : d; }).FirstOrDefault();
        var name = T(c.Name, 16, true, null, false, 1); name.FontWeight = Microsoft.UI.Text.FontWeights.ExtraBold;
        var cname = c.Name; var cphone = c.Phone;
        return ClickCard(() => S.Go(new ClientPage(cname)),
            Lead(Avatar(c.Name, c.Pending > 0.005 ? Rose : Blue), V(1, name, Muted($"{c.Phone.IfEmpty("Sin teléfono")} · {c.OrderList.Count} {(c.OrderList.Count == 1 ? "encargo" : "encargos")}" + (last != null ? " · " + last.Str("status").IfEmpty("Pedido") : ""), 12))),
            Cols(Kpi("Libras", Lb(c.Pounds)), Kpi("Cobrado", Money(c.Paid), Green), Kpi("Pendiente", c.Pending > 0.005 ? "- " + Money(c.Pending) : "$0.00", c.Pending > 0.005 ? Rose : Green)),
            Muted($"Pagó {Money(c.Paid)} de {Money(total)}", 12.5),
            Progress(total > 0 ? c.Paid / total : 0),
            c.Funds > 0 ? T("Fondo a favor + " + Money(c.Funds), 13, true, Green) : null,
            Cols(CardBtn("Ficha", "\uE8A5", () => S.Go(new ClientPage(cname))),
                c.Pending > 0.005 ? CardBtn("Cobrar", "\uE8BD", () => S.WhatsApp(cphone, Core.ReminderClient(c), cname), Green) : CardBtn("WhatsApp", "\uE8BD", () => S.WhatsApp(cphone, "", cname), Green),
                CardBtn("Encargo", "\uE710", () => S.Go(new NewOrderPage(cname, cphone)))));
    }
}

public sealed class ClientPage : Screen
{
    readonly string _name;
    public ClientPage(string name) { _name = name; }
    public override string Title => "Ficha del cliente";
    public override string Nav => "clients";

    public override void Build(StackPanel p)
    {
        var c = Core.FindClient(_name);
        if (c == null) { p.Children.Add(Empty("Este cliente ya no existe.")); return; }
        var inf = Core.ClientInfo(c.Name);
        var orders = c.OrderList.OrderByDescending(o => { var d = Js.ParseDate(o["date"]); return double.IsNaN(d) ? 0 : d; }).ToList();
        var total = c.OrderList.Sum(o => Js.Num(o["total"]));
        var firsts = c.OrderList.Select(o => Js.ParseDate(o["date"])).Where(d => !double.IsNaN(d)).ToList();
        var pays = orders.SelectMany(x => Core.Payments(x).Objs().Select((h, i) => (x, i, h))).OrderByDescending(t => { var d = Js.ParseDate(t.h["date"]); return double.IsNaN(d) ? 0 : d; }).ToList();
        var shots = orders.SelectMany(o => Core.OrderPhotos(o)).ToList();
        var owes = c.Pending > 0.005; var tone = owes ? Rose : Green;

        // ---- cabecera y saldo ----
        var name = H(c.Name, 21);
        var headInfo = V(2, name, Hs(6, new FontIcon { Glyph = "\uE717", FontSize = 12, Foreground = Mut }, Muted(c.Phone.IfEmpty("Sin teléfono"), 13)),
            firsts.Count > 0 ? Muted("Cliente desde " + Js.Fmt(firsts.Min()).Split(',')[0], 12) : null);
        var balTitle = Muted(owes ? "Pendiente por cobrar" : "Cuenta al día", 12.5);
        var balBig = Big(owes ? "- " + Money(c.Pending) : Money(0), tone, 26);
        var funds = c.Funds > 0.005 ? V(0, Muted("Fondo a favor", 12), Big("+ " + Money(c.Funds), Green, 17)) : null;
        if (funds != null) foreach (var ch in funds.Children) ((FrameworkElement)ch).HorizontalAlignment = HorizontalAlignment.Right;
        var balance = new Border { CornerRadius = new CornerRadius(16), Padding = new Thickness(14), Background = Alpha(tone, .08), BorderBrush = Alpha(tone, .30), BorderThickness = new Thickness(1),
            Child = V(8, Row(V(0, balTitle, balBig), funds), Progress(total > 0 ? c.Paid / total : 1),
                Muted($"Pagó {Money(c.Paid)} de {Money(total)}" + (total > 0 ? $" · {Math.Round(Math.Clamp(c.Paid / total, 0, 1) * 100)} %" : ""), 12.5)) };
        var head = Card(Lead(Avatar(c.Name, owes ? Rose : Blue, 60), headInfo, Chip(owes ? "Debe" : "Al día", tone)), balance,
            Cols(Kpi("Encargos", c.OrderList.Count.ToString()), Kpi("Libras", Lb(c.Pounds)), Kpi("Cobrado", Money(c.Paid), Green)));
        // ---- acciones ----
        var actions = Grid2(new List<UIElement?> { Btn("Nuevo encargo", () => S.Go(new NewOrderPage(c.Name, c.Phone)), true, null, "\uE710"),
            owes ? Btn("Cobrar", () => S.WhatsApp(c.Phone, Core.ReminderClient(c), c.Name), false, Green, "\uE8BD") : Btn("WhatsApp", () => S.WhatsApp(c.Phone, "", c.Name), false, Green, "\uE8BD"),
            Btn("Avisar llegada", () => S.WhatsApp(c.Phone, Core.ArrivalMessage(c), c.Name), false, null, "\uE7B8"),
            !owes ? Btn("Agradecer compra", () => S.WhatsApp(c.Phone, Core.ThanksClient(c), c.Name), false, null, "\uEB51") : null }.Where(b => b != null).Cast<UIElement>().ToList(), 2, true);
        // ---- datos fijos ----
        var empty = inf.Str("address").Length == 0 && inf.Str("note").Length == 0;
        UIElement InfoLine(string glyph, string label, string value) => Lead(IconTile(glyph, 32), V(0, Muted(label, 11), T(value, 14)));
        var datos = IconCard("\uE779", "Datos del cliente", Link(empty ? "Agregar" : "Editar", () => EditInfo(c.Name, inf.Str("address"), inf.Str("note"))),
            empty ? Muted("Guarda su dirección de entrega y detalles importantes (tallas, preferencias, horarios…).", 13) : null,
            inf.Str("address").Length > 0 ? InfoLine("\uE707", "Dirección", inf.Str("address")) : null,
            inf.Str("note").Length > 0 ? InfoLine("\uE70B", "Nota fija", inf.Str("note")) : null);

        p.Children.Add(S.ContentWidth < 760 ? V(12, head, actions, datos) : Cols(V(12, head), V(12, actions, datos)));

        // ---- encargos ----
        var rows = new List<UIElement>();
        foreach (var x in orders)
        {
            var b = Js.Num(x["balance"]); var t = Js.Num(x["total"]); var id = Js.Str(x["id"]); var ph = Core.OrderPhotos(x);
            var title = T($"{Js.Fmt(x["date"]).Split(',')[0]} · {Core.ShippingName(Core.TypeOf(x))} {Lb(Js.Num(x["pounds"]))}", 14, true, null, false, 1); title.FontWeight = Microsoft.UI.Text.FontWeights.ExtraBold;
            var inner = V(8,
                Row(V(1, title, Muted($"{Money(x["lbCost"])}/lb · total {Money(t)}", 11.5)), Chip(x.Str("status").IfEmpty("Pedido"), Status(x.Str("status")))),
                Cols(Kpi("Total", Money(t)), Kpi("Pagado", Money(x["paid"]), Green), Kpi(b < -0.005 ? "A favor" : "Saldo", b > 0.005 ? "- " + Money(b) : b < -0.005 ? "+ " + Money(-b) : "$0.00", b > 0.005 ? Rose : Green)),
                Progress(t > 0 ? Js.Num(x["paid"]) / t : 1),
                ph.Count > 0 ? Media.Strip(ph, 90, i => OrderActions.ViewPhotos(x, i)) : null,
                Cols(CardBtn("Ficha", "\uE8A5", () => S.Go(new OrderPage(id))), CardBtn("Editar", "\uE70F", () => S.Go(new EditOrderPage(id)))));
            var bt = Click(inner, () => S.Go(new OrderPage(id))); bt.Padding = new Thickness(12); bt.CornerRadius = new CornerRadius(14);
            rows.Add(new Border { Child = bt, CornerRadius = new CornerRadius(14), Background = Tile, BorderBrush = LineBr, BorderThickness = new Thickness(1) });
        }
        p.Children.Add(IconCard("\uE7B8", $"Encargos ({orders.Count})", null, S.ContentWidth < 760 ? V(10, rows.ToArray()) : Grid2(rows, 2)));

        // ---- pagos y capturas ----
        var payRows = new List<UIElement>();
        if (pays.Count == 0) payRows.Add(Muted("Todavía no hay pagos registrados.", 13));
        var k = 0;
        foreach (var (x, i, h) in pays.Take(30))
        {
            if (k++ > 0) payRows.Add(new Border { Height = 1, Background = LineBr });
            var oid = Js.Str(x["id"]); var idx = i;
            var row = Row(V(0, T($"{DayLabel(h["date"])} · {h.Str("method").IfEmpty("Efectivo")}", 14, true), Muted("Encargo del " + Js.Fmt(x["date"]).Split(',')[0] + (h.Str("note").Length > 0 ? " · " + h.Str("note") : ""), 12)),
                T("+ " + Money(h["amount"]), 14, true, Green));
            var rb = Click(row, () => Payments.Edit(oid, idx, Redraw)); rb.Padding = new Thickness(4, 6, 4, 6); payRows.Add(rb);
        }
        if (pays.Count > 0) payRows.Add(Muted("Pulsa un pago para corregirlo o eliminarlo.", 11));
        var pagos = IconCard("\uE8C7", $"Pagos ({pays.Count})", null, payRows.ToArray());
        var capt = shots.Count > 0 ? IconCard("\uE91B", $"Capturas de pedidos ({shots.Count})", null, Media.Strip(shots, 130, i => Media.Viewer(shots, i, null)), Muted("Pulsa una captura para verla completa y ampliarla.", 11)) : null;
        p.Children.Add(S.ContentWidth < 760 || capt == null ? V(12, pagos, capt) : Cols(pagos, capt));
    }
    static async void EditInfo(string name, string a0, string n0)
    {
        var a = a0; var n = n0;
        if (await MainWindow.I.Dialog("📇 Datos de " + name, V(10, Box("Dirección de entrega", a, v => a = v, true), Box("Nota fija del cliente (talla, horarios…)", n, v => n = v, true)), "Guardar")
            && Services.Core.SaveClientInfo(name, a, n)) MainWindow.I.Toast("✅ Datos del cliente guardados.");
    }
}
