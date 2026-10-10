using Encargos.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using static Encargos.Win.K;

namespace Encargos.Win;

public sealed class NewOrderPage : Screen
{
    readonly string? _forName, _forPhone;
    string _name = "", _phone = "", _type = "avion", _rate = "", _pounds = "", _cost = "", _fund = "", _method = "Efectivo", _status = "Pedido", _photo = "";
    bool _loaded;
    StackPanel _sum = new(), _sugg = new(), _photoBox = new(), _preview = new();
    TextBlock _totalT = new();
    TextBox _nameBox = null!, _phoneBox = null!, _rateBox = null!, _lbBox = null!;

    public NewOrderPage(string? forName = null, string? forPhone = null) { _forName = forName; _forPhone = forPhone; }
    public override string Title => "Nuevo encargo";
    public override string Nav => "new";
    public override bool Live => false;

    void LoadDraft()
    {
        if (_loaded) return; _loaded = true;
        var d = _forName == null ? Core.Ls.Obj("encargos_v4_draft") : new JObj();
        _name = _forName ?? d.Str("name"); _phone = _forPhone ?? d.Str("phone"); _type = d.Str("shippingType").IfEmpty("avion");
        _rate = d.Str("lbCost").IfEmpty(Js.ToFixed(Core.RateOf(_type), 2)); _pounds = d.Str("pounds"); _cost = d.Str("cost"); _fund = d.Str("fund");
        _method = d.Str("fundMethod").IfEmpty("Efectivo"); _status = d.Str("status").IfEmpty("Pedido");
    }
    /// <summary>Borrador automático (como la app web).</summary>
    void SaveDraft()
    {
        if (_forName == null) Core.Ls.Put("encargos_v4_draft", JObj.Of(("name", _name), ("phone", _phone), ("cost", _cost), ("lbCost", _rate), ("pounds", _pounds), ("fund", _fund), ("fundMethod", _method), ("status", _status), ("shippingType", _type)));
        Calc();
    }

    public override void Build(StackPanel p)
    {
        LoadDraft();
        _sum = new StackPanel { Spacing = 6 }; _sugg = new StackPanel { Spacing = 2 }; _photoBox = new StackPanel { Spacing = 8 };
        _nameBox = Box("Nombre del cliente *", _name, v => { _name = v; Suggest(); SaveDraft(); });
        _phoneBox = Box("Teléfono (WhatsApp)", _phone, v => { _phone = v; SaveDraft(); });
        _rateBox = Box("Tarifa por libra ($)", _rate, v => { _rate = v; SaveDraft(); });
        _lbBox = Box("Libras", _pounds, v => { _pounds = v; SaveDraft(); });
        TextBox fundBox = null!;
        fundBox = Box("Pago inicial ($)", _fund, v => { _fund = v; SaveDraft(); });
        double Total() => Num(_cost) + Num(_rate) * Num(_pounds);
        _preview = new StackPanel { Spacing = 10 };
        p.Children.Add(Card(T("VISTA PREVIA DEL ENCARGO", 10.5, true, Mut, false).Also(t => t.CharacterSpacing = 80), _preview));
        var step1 = StepCard(1, "\uE77B", "Cliente", "Escribe un nombre nuevo o elige uno guardado", _name.Trim().Length > 0, _nameBox, _sugg, _phoneBox);
        var step2 = StepCard(2, "\uE7B8", "Pedido y envío", "Costo de la compra, tipo de envío y libras", Num(_cost) > 0,
            Box("Costo del pedido ($)", _cost, v => { _cost = v; SaveDraft(); }),
            Muted("Tipo de envío", 12.5),
            Choice(Core.ShippingRates.Keys.Select(k => (k, Core.ShippingLabel(k))), _type, k => { _type = k; _rate = Js.ToFixed(Core.RateOf(k), 2); SaveDraft(); Redraw(); }),
            Cols(_rateBox, _lbBox),
            Cols(new[] { 0.5, 1, 2, 5 }.Select(q => (UIElement)Btn("+" + Js.NumberToString(q), () => _lbBox.Text = Js.ToFixed(Num(_lbBox.Text) + q, 2))).ToArray()));
        var step3 = StepCard(3, "\uE8C7", "Pago y estado", "Lo que pagó hoy y en qué etapa va", true,
            Cols(fundBox, V(6, Muted("Método de pago", 12.5), Choice(new[] { ("Efectivo", "Efectivo"), ("USD", "USD") }, _method, k => { _method = k; SaveDraft(); Redraw(); }))),
            Choice(new[] { ("all", "Pagó todo"), ("half", "Pagó la mitad"), ("none", "Sin pago") }, "", k => fundBox.Text = k switch { "all" => Js.ToFixed(Total(), 2), "half" => Js.ToFixed(Total() / 2, 2), _ => "" }),
            Muted("Estado", 12.5),
            Choice(EncargosApp.STATES.Select(s => (s, s)), _status, k => { _status = k; SaveDraft(); Redraw(); }));
        var step4 = StepCard(4, "\uE722", "Captura de la compra", "Opcional · foto o captura de pantalla", _photo.Length > 0, _photoBox);
        _totalT = Big("$0.00", null, 30);
        var total = TotalCard("$0.00", _sum); ((StackPanel)total.Child).Children[1] = _totalT;
        var wide = S.ContentWidth >= 760;
        if (wide) { p.Children.Add(Cols(step1, step3)); p.Children.Add(step2); p.Children.Add(Cols(step4, total)); }
        else { p.Children.Add(step1); p.Children.Add(step2); p.Children.Add(step3); p.Children.Add(step4); p.Children.Add(total); }
        p.Children.Add(Cols(Btn("Guardar encargo", Save, true, null, "\uE73E"),
            Btn("Limpiar", () => { _name = _phone = _cost = _pounds = _fund = _photo = ""; _status = "Pedido"; _type = "avion"; _rate = Js.ToFixed(Core.RateOf("avion"), 2); Core.Ls.Del("encargos_v4_draft"); Redraw(); })));
        DrawPhoto(); Suggest(); Calc();
    }
    void DrawPhoto()
    {
        _photoBox.Children.Clear();
        if (_photo.Length > 0) _photoBox.Children.Add(Media.Photo(_photo, double.NaN, 180, () => S.Ask("Quitar foto", "¿Quitar la foto de este encargo?", "Quitar", true, () => { _photo = ""; DrawPhoto(); })));
        _photoBox.Children.Add(Btn(_photo.Length == 0 ? "Agregar foto" : "Cambiar foto", async () => { var d = await Media.PickPhoto(); if (d != null) { _photo = d; DrawPhoto(); } }, false, null, ""));
    }
    void Suggest()
    {
        _sugg.Children.Clear();
        if (_name.Length < 2 || _forName != null) return;
        foreach (var s in Core.Clients().Where(c => c.Name.ToLowerInvariant().Contains(_name.Trim().ToLowerInvariant()) && c.Name != _name).Take(4))
            _sugg.Children.Add(Link($"↳ {s.Name} · {s.Phone}", () => { _nameBox.Text = s.Name; if (_phone.Trim().Length == 0) _phoneBox.Text = s.Phone; }, Blue));
    }
    void Calc()
    {
        if (_sum == null) return;
        double c = Num(_cost), lc = Num(_rate), pp = Num(_pounds), f = Num(_fund);
        var ship = lc * pp; var total = c + ship; var bal = total - f;
        var buy = Js.Num(Core.BuyRates()[_type]); var gain = pp * (lc - buy);
        _sum.Children.Clear();
        _totalT.Text = Money(total);
        _sum.Children.Add(Line($"Envío por libras ({Js.ToFixed(pp, 2)} lb × {Money(lc)})", Money(ship)));
        _sum.Children.Add(Line("Pagado", Money(f), Green));
        _sum.Children.Add(Line("Saldo / fondo", bal > 0.005 ? "- " + Money(bal) : bal < -0.005 ? "+ " + Money(-bal) + " a favor" : "Pagado completo", bal > 0.005 ? Rose : Green, true));
        if (pp > 0) _sum.Children.Add(Line($"Ganancia ({Js.ToFixed(pp, 2)} lb × ({Money(lc)} − {Money(buy)}))", Signed(gain), gain >= 0 ? Green : Rose));
        // vista previa del encargo (se actualiza mientras escribes)
        _preview.Children.Clear();
        var nm = T(_name.Trim().IfEmpty("Nuevo cliente"), 17, true, null, false, 1); nm.FontWeight = Microsoft.UI.Text.FontWeights.ExtraBold;
        _preview.Children.Add(Lead(Avatar(_name.Trim().IfEmpty("?"), Blue, 44), V(1, nm, Muted($"{_phone.Trim().IfEmpty("Sin teléfono")} · {Js.ToFixed(pp, 1)} lb", 12)), Chip(_status, Status(_status))));
        _preview.Children.Add(Cols(Kpi("Total", Money(total)), Kpi("Pagado", Money(f), Green), Kpi("Saldo", bal < 0 ? "+ " + Money(-bal) : Money(bal), bal > 0 ? Rose : Green)));
        _preview.Children.Add(Progress(total > 0 ? Math.Clamp(f / total, 0, 1) : 0));
        _preview.Children.Add(Row(Muted($"Pedido {Money(c)} + envío {Money(ship)}", 12.5), T(c <= 0 ? "Escribe el costo" : "Total " + Money(total), 12.5, true)));
    }
    void Save()
    {
        var r = Core.CreateOrder(new EncargosApp.NewOrder(_name, _phone, Num(_cost), Num(_rate), Num(_pounds), Num(_fund), _method, _status, _type, _photo));
        if (!r.Ok) { S.Toast(r.Error.IfEmpty("No se pudo guardar.")); return; }
        Core.Ls.Del("encargos_v4_draft");
        S.Toast("✅ Encargo guardado correctamente.");
        S.Root(new OrdersPage()); S.Go(new OrderPage(Js.Str(r.Value!["id"])));
    }
}
