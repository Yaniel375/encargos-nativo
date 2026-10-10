using Encargos.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using static Encargos.Win.K;

namespace Encargos.Win;

public sealed class ClientsPage : Screen
{
    public override string Title => "Clientes";
    public override string Nav => "clients";
    string _q = ""; int _limit = 30; StackPanel _list = new();

    public override void Build(StackPanel p)
    {
        _list = new StackPanel { Spacing = 12 };
        p.Children.Add(Search(_q, "Buscar cliente, teléfono o código", q => { _q = q; _limit = 30; Fill(); }));
        p.Children.Add(Row(Muted($"{Core.ActiveClients().Count} clientes activos", 13), Link("Completados ›", () => S.Go(new CompletedPage()))));
        p.Children.Add(_list); Fill();
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
        var grid = new Grid { ColumnSpacing = 12, RowSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition());
        var shown = list.Take(_limit).ToList();
        for (var i = 0; i < shown.Count; i++)
        {
            var c = shown[i]; if (i % 2 == 0) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var card = ClickCard(() => S.Go(new ClientPage(c.Name)),
                Lead(Avatar(c.Name, c.Pending > 0.005 ? Rose : Blue), V(0, T(c.Name, 15, true), Muted($"{c.Phone.IfEmpty("Sin teléfono")} · {c.OrderList.Count} encargo(s)")),
                    c.Pending > 0.005 ? T("- " + Money(c.Pending), 15, true, Rose) : null),
                Cols(Kpi("Libras", Js.ToFixed(c.Pounds, 2)), Kpi("Cobrado", Money(c.Paid), Green), Kpi("Pendiente", c.Pending > 0 ? "- " + Money(c.Pending) : "$0.00", Rose)),
                c.Funds > 0 ? T("🟢 Fondo a favor + " + Money(c.Funds), 14, true, Green) : null);
            Grid.SetRow(card, i / 2); Grid.SetColumn(card, i % 2); grid.Children.Add(card);
        }
        _list.Children.Add(grid);
        if (list.Count > _limit) _list.Children.Add(Btn($"Ver más ({list.Count - _limit} restantes)", () => { _limit += 30; Fill(); }));
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
        p.Children.Add(Card(Lead(Avatar(c.Name, Blue, 56), V(2, H(c.Name), Muted(c.Phone.IfEmpty("Sin teléfono"), 13))),
            Cols(Kpi("Encargos", c.OrderList.Count.ToString()), Kpi("Libras", Js.ToFixed(c.Pounds, 2)), Kpi("Cobrado", Money(c.Paid), Green),
                Kpi("Pendiente", c.Pending > 0 ? "- " + Money(c.Pending) : "$0.00", Rose), Kpi("Fondo a favor", c.Funds > 0 ? "+ " + Money(c.Funds) : "$0.00", Green))));
        p.Children.Add(Cols(Btn("Nuevo encargo", () => S.Go(new NewOrderPage(c.Name, c.Phone)), true, null, ""),
            c.Pending > 0.005 ? Btn("Cobrar", () => S.WhatsApp(c.Phone, Core.ReminderClient(c), c.Name), true, Green, "") : Btn("WhatsApp", () => S.WhatsApp(c.Phone, "", c.Name), true, Green, ""),
            Btn("Avisar que llegó", () => S.WhatsApp(c.Phone, Core.ArrivalMessage(c), c.Name), false, null, ""),
            c.Pending <= 0.005 ? Btn("Agradecer compra", () => S.WhatsApp(c.Phone, Core.ThanksClient(c), c.Name)) : null));
        var empty = inf.Str("address").Length == 0 && inf.Str("note").Length == 0;
        p.Children.Add(Card("Datos del cliente", Link(empty ? "Agregar" : "Editar", () => EditInfo(c.Name, inf.Str("address"), inf.Str("note"))),
            empty ? Muted("Guarda su dirección de entrega y detalles importantes (tallas, preferencias, horarios…).", 13) : null,
            inf.Str("address").Length > 0 ? V(0, Muted("Dirección", 11), T(inf.Str("address"))) : null,
            inf.Str("note").Length > 0 ? V(0, Muted("Nota fija", 11), T(inf.Str("note"))) : null));
        p.Children.Add(T("Compras", 16, true));
        foreach (var x in c.OrderList.OrderByDescending(o => Js.ParseDate(o["date"])))
        {
            var b = Js.Num(x["balance"]); var id = Js.Str(x["id"]);
            p.Children.Add(ClickCard(() => S.Go(new OrderPage(id)),
                Row(V(0, T("Compra · " + Js.Fmt(x["date"]), 14, true), Muted($"{Core.OrderLabel(x)} · {Core.ShippingLabel(Core.TypeOf(x))} · {Money(x["lbCost"])}/lb")), Chip(x.Str("status").IfEmpty("Pedido"), Status(x.Str("status")))),
                Line("Libras", Lb(Js.Num(x["pounds"]))), Line("Costo del artículo", Money(x["cost"])), Line("Envío", Money(x["shipping"])), Line("Total", Money(x["total"]), null, true),
                Line("Pagado", Money(x["paid"]), Green), Line("Saldo", b > 0 ? "- " + Money(b) : b < 0 ? "+ " + Money(-b) : "Pagado", b > 0 ? Rose : Green, true)));
        }
    }
    static async void EditInfo(string name, string a0, string n0)
    {
        var a = a0; var n = n0;
        if (await MainWindow.I.Dialog("📇 Datos de " + name, V(10, Box("Dirección de entrega", a, v => a = v, true), Box("Nota fija del cliente (talla, horarios…)", n, v => n = v, true)), "Guardar")
            && Services.Core.SaveClientInfo(name, a, n)) MainWindow.I.Toast("✅ Datos del cliente guardados.");
    }
}
