using Encargos.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using static Encargos.Win.K;

namespace Encargos.Win;

/* ---------------- Notas → En venta: artículos que tienes a la venta (igual que en Android, ui/Sale.kt) ----------------
 * Cálculos automáticos de todos los artículos (valor en venta, inversión con envío por peso, ganancia neta posible y real),
 * varias fotos por artículo, registrar ventas (baja las unidades, con deshacer) y compartir. El dinero va en verde. */
public static class SaleUi
{
    public static int StockOf(JObj n) => Math.Max(0, (int)Js.Num(n["stock"]));
    public static List<string> PhotosOf(JObj n) => (n["photos"] as JArr)?.OfType<string>().ToList() ?? new();
    public static string Pct(double gain, double cost) => cost > 0.005 ? $" ({Math.Round(gain / cost * 100, MidpointRounding.AwayFromZero):0} %)" : "";

    /// <summary>Texto para compartir el artículo (WhatsApp, Facebook…), sin datos de clientes.</summary>
    public static string ShareText(EncargosApp core, JObj n)
    {
        var st = StockOf(n);
        return string.Join("\n", new[] { n.Str("title"), "Precio: " + Money(n["price"]), n.Str("desc").Trim(),
            st > 0 ? (st == 1 ? "Queda 1 disponible" : $"{st} disponibles") : "Agotado", core.BusinessName.Trim() }.Where(s => s.Length > 0));
    }

    /// <summary>Resumen con los cálculos automáticos de todos los artículos.</summary>
    public static Border Summary(EncargosApp.SaleSummary sm, double width)
    {
        var lab = T("Valor en venta", 12.5, true, Mut, false); lab.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        var kpis = new List<UIElement> {
            Kpi("Artículos", $"{sm.Items}"), Kpi("Unidades", $"{sm.Units}"), Kpi("Peso", Js.ToFixed(sm.Lbs, 2) + " lb"),
            Kpi("Cobrado en ventas", Money(sm.Income), Green, Plural(sm.SoldUnits, "unidad vendida", "unidades vendidas")),
            Kpi("Inversión", Money(sm.Invested), null, "en lo que tienes"), Kpi("Inversión total", Money(sm.TotalInvested), null, "con lo vendido"),
            Kpi("Por ganar", Money(sm.Potential), sm.Potential >= 0 ? Green : Rose, "neto, si vendes todo"),
            Kpi("Ganancia neta", Money(sm.Realized), sm.Realized >= 0 ? Green : Rose, "de lo vendido") };
        return IconCard("\uE719", "Tu mercancía", null, V(2, lab, Big(Money(sm.Value), Green, 30),
                Muted(sm.Units > 0 ? $"Lo que cobrarías si vendes las {Plural(sm.Units, "unidad", "unidades")} que tienes" : "Agrega artículos para ver cuánto vale tu mercancía", 12.5)),
            Grid2(kpis, width < 760 ? 2 : 4));
    }

    /// <summary>Artículo en tarjeta: foto, nombre, precio en verde, unidades y acciones rápidas.</summary>
    public static Border Card(JObj n)
    {
        var S = MainWindow.I; var core = Services.Core;
        var ph = PhotosOf(n); var st = StockOf(n); var price = Js.Num(n["price"]); var cost = EncargosApp.SaleUnitCost(n); var w = Js.Num(n["weight"]);
        var id = n.Str("id"); var nn = n;
        void Open() => S.Go(new SaleEditorPage(id));
        var items = new List<UIElement>();
        if (ph.Count > 0)
        {
            var g = new Grid(); g.Children.Add(Media.Photo(ph[0], double.NaN, 150, Open, 600));
            if (ph.Count > 1) g.Children.Add(new Border { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(6), CornerRadius = new CornerRadius(8),
                Padding = new Thickness(7, 2, 7, 3), Background = Br(0x99000000u), IsHitTestVisible = false, Child = T($"+{ph.Count - 1}", 11, true, White, false) });
            items.Add(g);
        }
        var tt = T(n.Str("title").IfEmpty("Sin nombre"), 15, true, null, true, 2); tt.FontWeight = Microsoft.UI.Text.FontWeights.ExtraBold;
        items.Add(tt);
        items.Add(Big(Money(price), Green, 19));
        items.Add(Chips(st > 0 ? Chip(st == 1 ? "1 disponible" : $"{st} disponibles", Blue) : Chip("Agotado", Rose)));
        if (cost > 0.005) items.Add(T($"Ganancia neta {Money(price - cost)} c/u" + Pct(price - cost, cost), 12, true, price >= cost ? Green : Rose));
        if (w > 0) items.Add(Muted($"{Js.ToFixed(w, 2)} lb c/u · costo {Money(cost)}", 11.5));
        var share = CardBtn(null, "\uE72D", () => Share(nn), null, "Compartir"); share.Width = 46; share.HorizontalAlignment = HorizontalAlignment.Right;
        items.Add(Row(CardBtn(st > 0 ? "Vendí" : "Agotado", "\uE8EC", () => { if (st > 0) Sell(nn); else S.Toast("No quedan unidades. Edita el artículo para reponer."); }, st > 0 ? Green : null), share));
        return ClickCard(Open, items.ToArray());
    }

    /// <summary>Botón − / + de la cantidad.</summary>
    public static Button StepBtn(string glyph, string name, Action onClick)
    {
        var b = new Button { Content = new FontIcon { Glyph = glyph, FontSize = 14, Foreground = Text }, Width = 44, Height = 44, Padding = new Thickness(0), CornerRadius = new CornerRadius(14), Background = Tile, BorderBrush = Line2, BorderThickness = new Thickness(1) };
        if (BtnStyle != null) b.Style = BtnStyle;
        b.Padding = new Thickness(0); b.HorizontalContentAlignment = HorizontalAlignment.Center;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(b, name);
        b.Click += (_, _) => onClick(); return b;
    }
    /// <summary>− cantidad + (no baja de min ni pasa de max).</summary>
    public static StackPanel Qty(Func<int> get, Action<int> set, int min, int max)
    {
        var val = Big($"{get()}", null, 22); val.Width = 64; val.TextAlignment = TextAlignment.Center;
        Button? minus = null, plus = null;
        void Sync() { val.Text = $"{get()}"; minus!.IsEnabled = get() > min; plus!.IsEnabled = get() < max; minus.Opacity = minus.IsEnabled ? 1 : .4; plus.Opacity = plus.IsEnabled ? 1 : .4; }
        minus = StepBtn("\uE738", "Menos", () => { set(Math.Max(min, get() - 1)); Sync(); });
        plus = StepBtn("\uE710", "Más", () => { set(Math.Min(max, get() + 1)); Sync(); });
        Sync(); return Hs(10, minus, val, plus);
    }

    /// <summary>Ventana para registrar una venta: cantidad y precio (por si hiciste rebaja).</summary>
    public static async void Sell(JObj n)
    {
        var S = MainWindow.I; var core = Services.Core; var st = StockOf(n); var id = n.Str("id");
        if (st <= 0) { S.Toast("No quedan unidades. Edita el artículo para reponer."); return; }
        int qty = 1; var price = Js.ToFixed(Js.Num(n["price"]), 2); var uc = EncargosApp.SaleUnitCost(n); double total = 0;
        var lines = new StackPanel { Spacing = 6 }; var msg = T("", 13, false, Rose);
        void Draw()
        {
            var p = Num(price); total = double.IsNaN(p) ? 0 : p * qty; var gain = double.IsNaN(p) ? 0 : (p - uc) * qty;
            lines.Children.Clear();
            lines.Children.Add(Line("Total de la venta", Money(total), Green, true));
            if (uc > 0.005) { lines.Children.Add(Line("Costo (compra + envío + otros)", Money(uc * qty))); lines.Children.Add(Line("Ganancia neta de esta venta", Money(gain), gain >= 0 ? Green : Rose, true)); }
            lines.Children.Add(Line("Quedarán", Plural(st - qty, "unidad", "unidades")));
        }
        Draw();
        var content = V(10, Muted("Cantidad vendida", 12.5), Qty(() => qty, v => { qty = v; Draw(); }, 1, st),
            Box("Precio por unidad ($)", price, v => { price = v; Draw(); }), K.Card(lines), msg);
        var ok = await S.Dialog("Registrar venta", content, "Guardar venta", () =>
        {
            var r = core.SellSaleItem(id, qty, Num(price)); if (!r.Ok) { msg.Text = r.Error; return false; }
            return true;
        }, glyph: "\uE8EC", sub: n.Str("title").IfEmpty("Sin nombre"));
        if (ok) S.Toast("✅ Venta registrada · " + Money(total), () => core.UndoLastSale(id));
    }

    /// <summary>Compartir: copiar texto y foto (para pegar en WhatsApp o Facebook) o guardar las fotos en una carpeta.</summary>
    public static async void Share(JObj n)
    {
        var S = MainWindow.I; var core = Services.Core; var ph = PhotosOf(n); var text = ShareText(core, n);
        var copyAll = OptionRow("\uE8C8", ph.Count > 0 ? "Copiar texto y foto" : "Copiar texto", "Pégalo en WhatsApp, Facebook o donde quieras con Ctrl+V", async () =>
        {
            var dp = new DataPackage(); dp.SetText(text);
            if (ph.Count > 0 && Bytes(ph[0]) is { } b)
            {
                var ms = new InMemoryRandomAccessStream(); await ms.WriteAsync(System.Runtime.InteropServices.WindowsRuntime.WindowsRuntimeBufferExtensions.AsBuffer(b)); ms.Seek(0);
                dp.SetBitmap(RandomAccessStreamReference.CreateFromStream(ms));
            }
            Clipboard.SetContent(dp); try { Clipboard.Flush(); } catch { }
            S.Toast(ph.Count > 0 ? "Copiado. Pulsa Ctrl+V en el chat para pegar la foto con el texto." : "Texto copiado.");
        }, true);
        var copyText = ph.Count > 0 ? OptionRow("\uE8A5", "Copiar solo el texto", "Nombre, precio, descripción y unidades", () =>
        {
            var dp = new DataPackage(); dp.SetText(text); Clipboard.SetContent(dp); S.Toast("Texto copiado.");
        }) : null;
        var save = ph.Count > 0 ? OptionRow("\uE896", ph.Count == 1 ? "Guardar la foto" : $"Guardar las {ph.Count} fotos", "Elige la carpeta donde guardarlas", async () =>
        {
            var p = new FolderPicker { SuggestedStartLocation = PickerLocationId.PicturesLibrary }; p.FileTypeFilter.Add("*");
            WinRT.Interop.InitializeWithWindow.Initialize(p, S.Hwnd);
            var dir = await p.PickSingleFolderAsync(); if (dir == null) return;
            var name = System.Text.RegularExpressions.Regex.Replace(n.Str("title"), @"[^\p{L}\p{N}]+", "_").Trim('_').IfEmpty("articulo"); var k = 0;
            try { for (var i = 0; i < ph.Count; i++) if (Bytes(ph[i]) is { } b) { var f = await dir.CreateFileAsync($"{name}_{i + 1}.jpg", CreationCollisionOption.GenerateUniqueName); await FileIO.WriteBytesAsync(f, b); k++; } }
            catch (Exception e) { Services.Report(e); }
            S.Toast(k > 0 ? "✅ " + Plural(k, "foto guardada", "fotos guardadas") + "." : "No se pudieron guardar las fotos.");
        }) : null;
        await S.Dialog("Compartir artículo", V(8, Muted(text.Replace("\n", " · "), 12.5), copyAll, copyText, save), "Cerrar", null, null, glyph: "\uE72D", sub: n.Str("title").IfEmpty("Sin nombre"));
    }
    static byte[]? Bytes(string dataUrl) { try { var i = dataUrl.IndexOf(','); return Convert.FromBase64String(i >= 0 ? dataUrl[(i + 1)..] : dataUrl); } catch { return null; } }
}

/// <summary>Pestaña «En venta» dentro de Notas: resumen, buscador, filtros y tarjetas.</summary>
public sealed class SaleList
{
    string _q = "", _filter = "all"; StackPanel _list = new();
    readonly Action _redraw;
    public SaleList(Action redraw) { _redraw = redraw; }
    static MainWindow S => MainWindow.I;
    static EncargosApp Core => Services.Core;

    public void Build(StackPanel p)
    {
        _list = new StackPanel { Spacing = 12 };
        var all = Core.SaleItems(); var sm = Core.SaleSummaryOf(all);
        p.Children.Add(SaleUi.Summary(sm, S.ContentWidth));
        p.Children.Add(Btn("Nuevo artículo", () => S.Go(new SaleEditorPage(null)), true, null, "\uE710"));
        if (all.Count > 0)
        {
            p.Children.Add(Search(_q, "Buscar artículo", q => { _q = q; Fill(); }));
            p.Children.Add(Choice(new[] { ("all", $"Todos ({all.Count})"), ("avail", $"Disponibles ({sm.Available})"), ("out", $"Agotados ({all.Count - sm.Available})") }, _filter, k => { _filter = k; _redraw(); }));
        }
        p.Children.Add(_list); Fill();
    }
    void Fill()
    {
        _list.Children.Clear();
        var all = Core.SaleItems(); var nq = Js.NormSearch(_q);
        var list = all.Where(n => (nq.Length == 0 || Js.NormSearch(n.Str("title") + " " + n.Str("desc")).Contains(nq)) &&
            _filter switch { "avail" => SaleUi.StockOf(n) > 0, "out" => SaleUi.StockOf(n) == 0, _ => true }).ToList();
        if (all.Count == 0) { _list.Children.Add(Empty("Aún no tienes artículos en venta", "Agrega lo que vendes con sus fotos, precio y cantidad: la app suma sola cuánto vale todo y cuánto vas ganando.")); return; }
        if (list.Count == 0) { _list.Children.Add(Empty("Ningún artículo coincide.")); return; }
        _list.Children.Add(Grid2(list.Select(n => (UIElement)SaleUi.Card(n)).ToList(), S.ContentWidth < 760 ? 2 : 3));
    }
}

public sealed class SaleEditorPage : Screen
{
    readonly string? _id; bool _loaded;
    string _name = "", _desc = "", _price = "", _cost = "", _shipType = "avion", _weight = "", _lbRate = "", _extra = ""; int _stock = 1;
    readonly List<string> _photos = new();
    StackPanel _photoRow = new(), _calc = new(); TextBlock _photoCount = new();
    public SaleEditorPage(string? id) { _id = id; }
    public override string Title => _id == null ? "Nuevo artículo" : "Editar artículo";
    public override string Nav => "notes";
    public override bool Live => false;

    public override void Build(StackPanel p)
    {
        var n = _id == null ? null : Core.SaleItems().FirstOrDefault(x => x.Str("id") == _id);
        // pesaje y costos: libras por unidad, tipo de envío, costo por libra (de Ajustes → Costo por libra) y otros gastos por unidad
        var buy = Core.BuyRates();
        if (!_loaded)
        {
            _loaded = true;
            if (n != null)
            {
                _name = n.Str("title"); _desc = n.Str("desc"); _price = Js.ToFixed(Js.Num(n["price"]), 2);
                _cost = Js.Num(n["cost"]) > 0 ? Js.ToFixed(Js.Num(n["cost"]), 2) : ""; _stock = SaleUi.StockOf(n);
                _shipType = n.Str("shipType").IfEmpty("avion");
                _weight = Js.Num(n["weight"]) > 0 ? Js.NumberToString(Js.Num(n["weight"])) : "";
                _lbRate = Js.ToFixed(Js.Num(n["lbRate"] ?? buy[_shipType]), 2);
                _extra = Js.Num(n["extra"]) > 0 ? Js.ToFixed(Js.Num(n["extra"]), 2) : "";
                _photos.AddRange(SaleUi.PhotosOf(n));
            }
            else _lbRate = Js.ToFixed(Js.Num(buy[_shipType]), 2);
        }
        _photoRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 }; _photoCount = Muted("", 12); _calc = new StackPanel { Spacing = 6 };
        p.Children.Add(IconCard("\uEB9F", "Fotos", _photoCount,
            new ScrollViewer { Content = _photoRow, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, HorizontalScrollMode = ScrollMode.Enabled },
            Btn("Agregar fotos", async () =>
            {
                var got = await Media.PickPhotos(10, 900, 0.55); if (got.Count == 0) return;
                _photos.AddRange(got); DrawPhotos(); S.Toast(Plural(got.Count, "foto agregada", "fotos agregadas") + ". Pulsa «Guardar artículo» para guardarlas.");
            }, false, null, "\uE722")));
        DrawPhotos();
        p.Children.Add(Box("Nombre del artículo", _name, v => _name = v));
        p.Children.Add(Box("Descripción (talla, color, estado…)", _desc, v => _desc = v, true));
        p.Children.Add(Box("Precio de venta (por unidad, $)", _price, v => { _price = v; DrawCalc(); }));
        p.Children.Add(IconCard("\uE804", "Costos y pesaje", null,
            Muted("Para saber tu inversión y tu ganancia neta: lo que pagaste, el envío por su peso y otros gastos. Todo por unidad.", 12.5),
            Box("Precio de compra ($)", _cost, v => { _cost = v; DrawCalc(); }),
            Cols(Box("Peso (lb)", _weight, v => { _weight = v; DrawCalc(); }), Box("Envío por libra ($)", _lbRate, v => { _lbRate = v; DrawCalc(); })),
            Choice(Core.ShippingRates.Select(kv => (kv.Key, Core.ShippingName(kv.Key))), _shipType, t => { _shipType = t; _lbRate = Js.ToFixed(Js.Num(buy[t]), 2); Redraw(); }),
            Box("Otros gastos (aduana, empaque…, $)", _extra, v => { _extra = v; DrawCalc(); })));
        p.Children.Add(V(6, T("Unidades disponibles", 12.5, true, Mut, false), SaleUi.Qty(() => _stock, v => { _stock = v; DrawCalc(); }, 0, 9999)));
        p.Children.Add(IconCard("\uE8EF", "Cálculo automático", null, _calc)); DrawCalc();
        var sales = n != null ? EncargosApp.SaleSales(n) : new List<JObj>();
        if (sales.Count > 0)
        {
            var hist = new List<UIElement?>();
            foreach (var s in Enumerable.Reverse(sales).Take(20))
            {
                var q = Js.Num(s["qty"]);
                hist.Add(Line($"{DayLabel(s["t"])} · {(int)q} × {Money(s["price"])} · neta {Money(q * (Js.Num(s["price"]) - Js.Num(s["cost"])))}", Money(q * Js.Num(s["price"])), Green));
            }
            hist.Add(Btn("Deshacer la última venta", () => S.Ask("Deshacer venta", "Se devuelven las unidades de la última venta registrada.", "Deshacer", false,
                () => { if (Core.UndoLastSale(_id!)) { S.Toast("✅ Venta deshecha."); S.Back(); } }), false, null, "\uE7A7"));
            p.Children.Add(IconCard("\uE81C", "Ventas", Big(Money(sales.Sum(s => Js.Num(s["qty"]) * Js.Num(s["price"]))), Green, 15), hist.ToArray()));
        }
        if (n != null) p.Children.Add(Muted("Creado: " + Js.Fmt(n["created"]) + (Js.Num(n["updated"]) != Js.Num(n["created"]) ? " · Editado: " + Js.Fmt(n["updated"]) : ""), 11));
        p.Children.Add(Cols(Btn("Guardar artículo", Save, true, null, ""),
            n != null ? Btn("Eliminar artículo", () => S.Ask("Eliminar artículo", "¿Eliminar este artículo y sus fotos? No se puede deshacer.", "Eliminar", true, () => { Core.DeleteNote(_id!); S.Back(); }), false, Rose)
                      : Btn("Cancelar", () => S.Back())));
    }
    static double Ok(string t) { var v = Num(t); return double.IsNaN(v) ? 0 : v; }
    static double Bad(string t) { var v = Num(t); return double.IsNaN(v) ? -1 : v; }
    void DrawPhotos()
    {
        _photoRow.Children.Clear(); _photoCount.Text = Plural(_photos.Count, "foto", "fotos");
        if (_photos.Count == 0) { _photoRow.Children.Add(Muted("Agrega varias fotos: la primera es la que se ve en la lista.", 12.5)); return; }
        for (var i = 0; i < _photos.Count; i++) { var idx = i; _photoRow.Children.Add(Media.Thumb(_photos[i], 110, i, _photos.Count, () => Media.Viewer(_photos.ToList(), idx, k => { _photos.RemoveAt(k); DrawPhotos(); }))); }
    }
    /// <summary>Cálculo automático mientras escribes (mismas cuentas que el núcleo: compra + peso × costo por libra + otros).</summary>
    void DrawCalc()
    {
        _calc.Children.Clear();
        double pr = Ok(_price), buyC = Ok(_cost), w = Ok(_weight), rate = Ok(_lbRate), ex = Ok(_extra);
        var ship = w * rate; var c = buyC + ship + ex; var st = _stock;
        if (c > 0.005)
        {
            _calc.Children.Add(Line("Compra", Money(buyC)));
            if (ship > 0.005) _calc.Children.Add(Line($"Envío · {Js.ToFixed(w, 2)} lb × {Money(rate)}", Money(ship)));
            if (ex > 0.005) _calc.Children.Add(Line("Otros gastos", Money(ex)));
            _calc.Children.Add(Line("Costo total por unidad", Money(c), null, true));
            _calc.Children.Add(Line("Ganancia neta por unidad", Money(pr - c) + SaleUi.Pct(pr - c, c), pr >= c ? Green : Rose, true));
        }
        _calc.Children.Add(Line($"Valor de las {Plural(st, "unidad", "unidades")}", Money(pr * st), Green, true));
        if (c > 0.005)
        {
            _calc.Children.Add(Line("Tu inversión en ellas", Money(c * st), null, true));
            _calc.Children.Add(Line("Ganancia neta si vendes todo", Money((pr - c) * st), pr >= c ? Green : Rose, true));
        }
        if (w > 0) _calc.Children.Add(Line("Peso total", Js.ToFixed(w * st, 2) + " lb"));
        if (c > 0.005 && pr < c) _calc.Children.Add(T("El precio es menor que lo que te cuesta cada unidad: perderías dinero en cada venta.", 12.5, false, Rose));
        if (c <= 0.005) _calc.Children.Add(Muted("Escribe el precio de compra y el peso para ver tu inversión y tu ganancia neta.", 12.5));
    }
    void Save()
    {
        var r = Core.SaveSaleItem(_id, _name, _desc, Bad(_price), Bad(_cost), _stock, _photos.ToList(), Bad(_weight), _shipType, Bad(_lbRate), Bad(_extra));
        if (!r.Ok) { S.Toast(r.Error); return; }
        S.Toast(_id != null ? "✅ Artículo actualizado." : "✅ Artículo guardado."); S.Back();
    }
}
