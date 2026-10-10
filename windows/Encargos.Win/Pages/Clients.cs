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
    public override string? Right => $"{Core.ActiveClients().Count} activos";
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
            c.Funds > 0 ? T("🟢 Fondo a favor + " + Money(c.Funds), 13, true, Green) : null,
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
        var total = c.OrderList.Sum(o => Js.Num(o["total"]));
        p.Children.Add(Card(Lead(Avatar(c.Name, Blue, 56), V(2, H(c.Name), Muted($"{c.Phone.IfEmpty("Sin teléfono")} · {c.OrderList.Count} {(c.OrderList.Count == 1 ? "encargo" : "encargos")}", 13))),
            Row(T($"Pagado {Money(c.Paid)} de {Money(total)}", 13.5), T(c.Pending > 0.005 ? $"Debe {Money(c.Pending)}" : "Al día", 13.5, true, c.Pending > 0.005 ? Rose : Green)),
            Progress(total > 0 ? c.Paid / total : 1)));
        var empty = inf.Str("address").Length == 0 && inf.Str("note").Length == 0;
        p.Children.Add(Card("Datos del cliente", Link(empty ? "Agregar ✎" : "Editar ✎", () => EditInfo(c.Name, inf.Str("address"), inf.Str("note"))),
            empty ? Muted("Guarda su dirección de entrega y detalles importantes (tallas, preferencias, horarios…).", 13) : null,
            inf.Str("address").Length > 0 ? V(0, Muted("Dirección", 11), T(inf.Str("address"))) : null,
            inf.Str("note").Length > 0 ? V(0, Muted("Nota fija", 11), T(inf.Str("note"))) : null));
        p.Children.Add(Cols(Kpi("Encargos", c.OrderList.Count.ToString()), Kpi("Libras", Lb(c.Pounds)), Kpi("A favor", c.Funds > 0 ? "+ " + Money(c.Funds) : "$0.00", Green),
            Kpi("Cobrado", Money(c.Paid), Green), Kpi("Pendiente", c.Pending > 0 ? "- " + Money(c.Pending) : "$0.00", Rose)));
        p.Children.Add(Cols(Btn("Nuevo encargo", () => S.Go(new NewOrderPage(c.Name, c.Phone)), true, null, ""),
            c.Pending > 0.005 ? Btn("Cobrar", () => S.WhatsApp(c.Phone, Core.ReminderClient(c), c.Name), false, Green, "") : Btn("WhatsApp", () => S.WhatsApp(c.Phone, "", c.Name), false, Green, ""),
            Btn("Avisar llegada", () => S.WhatsApp(c.Phone, Core.ArrivalMessage(c), c.Name), false, null, ""),
            c.Pending <= 0.005 ? Btn("Agradecer compra", () => S.WhatsApp(c.Phone, Core.ThanksClient(c), c.Name)) : null));
        var rows = new List<UIElement>();
        foreach (var x in c.OrderList.OrderByDescending(o => Js.ParseDate(o["date"])))
        {
            var b = Js.Num(x["balance"]); var t = Js.Num(x["total"]); var id = Js.Str(x["id"]);
            var title = T($"{Js.Fmt(x["date"])} · {Core.ShippingName(Core.TypeOf(x))} {Lb(Js.Num(x["pounds"]))}", 14, true, null, false, 1); title.FontWeight = Microsoft.UI.Text.FontWeights.ExtraBold;
            var inner = V(8,
                Row(V(1, title, Hs(0, Muted($"Total {Money(t)} · ", 12.5), T(b > 0.005 ? $"Debe {Money(b)}" : b < -0.005 ? $"A favor {Money(-b)}" : "Pagado", 12.5, true, b > 0.005 ? Rose : Green)),
                    Muted($"{Core.OrderLabel(x)} · {Money(x["lbCost"])}/lb", 11.5)), Chip(x.Str("status").IfEmpty("Pedido"), Status(x.Str("status")))),
                Progress(t > 0 ? Js.Num(x["paid"]) / t : 1),
                Cols(CardBtn("Ficha", "\uE8A5", () => S.Go(new OrderPage(id))), CardBtn("Editar", "\uE70F", () => S.Go(new EditOrderPage(id)))));
            var bt = Click(inner, () => S.Go(new OrderPage(id))); bt.Padding = new Thickness(12); bt.CornerRadius = new CornerRadius(14);
            rows.Add(new Border { Child = bt, CornerRadius = new CornerRadius(14), Background = Tile, BorderBrush = LineBr, BorderThickness = new Thickness(1) });
        }
        p.Children.Add(Card("Encargos", null, S.ContentWidth < 760 ? V(10, rows.ToArray()) : Grid2(rows, 2)));
    }
    static async void EditInfo(string name, string a0, string n0)
    {
        var a = a0; var n = n0;
        if (await MainWindow.I.Dialog("📇 Datos de " + name, V(10, Box("Dirección de entrega", a, v => a = v, true), Box("Nota fija del cliente (talla, horarios…)", n, v => n = v, true)), "Guardar")
            && Services.Core.SaveClientInfo(name, a, n)) MainWindow.I.Toast("✅ Datos del cliente guardados.");
    }
}
