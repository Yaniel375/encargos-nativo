using Encargos.Core;

/// <summary>Pruebas funcionales del núcleo C# (PC) con datos ficticios aislados — mismas comprobaciones que Funcional.kt.</summary>
static class Funcional
{
    static int fails, total;
    static void Check(string name, bool ok, object? info = null) { total++; Console.WriteLine((ok ? "✅ " : "❌ ") + name + (info != null ? " · " + info : "")); if (!ok) fails++; }
    static bool Eq(double a, double b) => Math.Abs(a - b) < 0.005;
    static double N(JObj? x, string k) => Js.Num(x?[k]);

    public static int Run()
    {
        var app = new EncargosApp(new MemoryStorage(), "PRUEBA"); app.PhoneCC = "53";
        var a = app.CreateOrder(new EncargosApp.NewOrder("Ana Prueba", "55512345", 40, 7, 3, 10)).GetOrThrow();
        Check("Encargo A: envío 21", Eq(N(a, "shipping"), 21), a["shipping"]);
        Check("Encargo A: total 61", Eq(N(a, "total"), 61), a["total"]);
        Check("Encargo A: saldo 51", Eq(N(a, "balance"), 51), a["balance"]);
        var b = app.CreateOrder(new EncargosApp.NewOrder("ana prueba ", "55512345", 20, 7, 1, 50, "USD")).GetOrThrow();
        Check("Encargo B: total 27, saldo −23", Eq(N(b, "total"), 27) && Eq(N(b, "balance"), -23), $"{b["total"]} / {b["balance"]}");
        Check("Mismo cliente: comparte código", Js.Str(b["accessCode"]) == Js.Str(a["accessCode"]));
        Check("Rechaza nombre vacío", !app.CreateOrder(new EncargosApp.NewOrder("  ", "", 1, 1, 1, 0)).Ok);
        Check("Rechaza negativos", !app.CreateOrder(new EncargosApp.NewOrder("X", "", -1, 1, 1, 0)).Ok);
        Check("Rechaza pago negativo al editar", !app.SaveEdit(a["id"], 0, -5, "Efectivo", "", "Pedido").Ok);
        Check("Sin encargos de más", app.Orders().Count == 2, app.Orders().Count);

        var c = app.FindClient("ANA PRUEBA");
        Check("Ficha del cliente encontrada", c != null);
        Check("Cliente con 2 encargos", c?.OrderList.Count == 2);
        Check("Pendiente consolidado 51", Eq(c!.Pending, 51), c.Pending);
        Check("Fondos a favor 23", Eq(c.Funds, 23), c.Funds);
        Check("Cobrado 60", Eq(c.Paid, 60), c.Paid);

        app.SaveEdit(a["id"], 0, 21, "Efectivo", "abono", "Pedido").GetOrThrow();
        var x = app.ById(a["id"]);
        Check("Pago parcial: pagado 31, saldo 30", Eq(N(x, "paid"), 31) && Eq(N(x, "balance"), 30), $"{x?["paid"]} / {x?["balance"]}");
        app.PayEdit(a["id"], 1, 25, "USD", "corregido").GetOrThrow(); x = app.ById(a["id"]);
        Check("Editar pago: pagado 35, saldo 26", Eq(N(x, "paid"), 35) && Eq(N(x, "balance"), 26), $"{x?["paid"]} / {x?["balance"]}");
        Check("Editar a 0 se rechaza", !app.PayEdit(a["id"], 1, 0, "USD", "").Ok);
        var snap = app.PayDelete(a["id"], 1); x = app.ById(a["id"]);
        Check("Eliminar pago: pagado 10, saldo 51", Eq(N(x, "paid"), 10) && Eq(N(x, "balance"), 51));
        app.RestoreOrder(snap!); x = app.ById(a["id"]);
        Check("Deshacer eliminación de pago", Eq(N(x, "paid"), 35));
        app.SaveEdit(a["id"], 2, 0, "Efectivo", "", "Pedido").GetOrThrow(); x = app.ById(a["id"]);
        Check("Añadir 2 lb: total 75, saldo 40", Eq(N(x, "total"), 75) && Eq(N(x, "balance"), 40), $"{x?["total"]} / {x?["balance"]}");
        var flow = new List<string>(); string? prev;
        do { prev = app.Advance(a["id"]); if (prev != null) flow.Add(app.ById(a["id"])!.Str("status")); } while (prev != null);
        Check("Flujo de estados", string.Join(">", flow) == "En tránsito>Recibido>Entregado", string.Join(">", flow));
        app.SaveEdit(a["id"], 0, 40, "Efectivo", "", "Entregado").GetOrThrow();
        app.SaveEdit(b["id"], 23.0 / 7.0, 0, "Efectivo", "", "Entregado").GetOrThrow();
        Check("Cliente completado cuando todo está saldado", app.CompletedClients().Any(k => k.Key == "ana prueba"));

        var p1 = app.CreateOrder(new EncargosApp.NewOrder("Luis Prueba", "5355500001", 10, 7, 1, 0)).GetOrThrow();
        app.CreateOrder(new EncargosApp.NewOrder("Luis Prueba", "5355500001", 30, 7, 2, 5)).GetOrThrow();
        var pay = app.Attention.Compute().FirstOrDefault(al => al.Cat == "pay" && al.RefClient == "Luis Prueba");
        Check("Atención: importe consolidado 56", pay != null && Eq(pay.Amount ?? 0, 56), pay?.Amount);
        var d = new Stats(app).GetDashboard();
        Check("Panel: pendiente 56", Eq(d.Pending, 56), d.Pending);
        Check("Panel: clientes activos 1", d.Clients == 1, d.Clients);

        Check("waNumber agrega 53", app.WaNumber("5551 2345") == "5355512345");
        var link = app.WaLink("55512345", "Hola Ana, saldo $51.00");
        Check("waLink codifica el mensaje", link != null && link.StartsWith("https://wa.me/5355512345?text=") && !link.Contains(' '), link);
        Check("Recordatorio con saldo real", app.ReminderClient(app.FindClient("Luis Prueba")!).Contains("56"));

        var before = app.Orders().Count; var r = app.RemoveOrder(p1["id"]);
        Check("Eliminar y deshacer", r != null && app.UndoRemove(r) && app.Orders().Count == before);
        Check("Deshacer dos veces no duplica", !app.UndoRemove(r!) && app.Orders().Count == before);

        // ---------- Vista reutilizada: se actualiza en cuanto cambian los datos ----------
        var n0 = app.ActiveOrders().Count; var c0 = app.Clients().Count;
        var nv = app.CreateOrder(new EncargosApp.NewOrder("Vista Prueba", "5355500009", 5, 7, 1, 0)).GetOrThrow();
        Check("Vista: el encargo nuevo aparece al momento en activos", app.ActiveOrders().Count == n0 + 1 && app.ActiveOrders().Any(o => Js.Eq(o["id"], nv["id"])));
        Check("Vista: el cliente nuevo aparece al momento", app.Clients().Count == c0 + 1 && app.FindClient("vista prueba") != null);
        app.SaveEdit(nv["id"], 0, 12, "Efectivo", "", "Entregado");
        Check("Vista: al saldarlo pasa a completados", app.CompletedClients().Any(c => c.Key == "vista prueba") && !app.ActiveOrders().Any(o => Js.Eq(o["id"], nv["id"])));
        var rv = app.RemoveOrder(nv["id"]); Check("Vista: al eliminarlo desaparece", app.FindClient("Vista Prueba") == null);
        app.UndoRemove(rv!); Check("Vista: deshacer lo devuelve", app.FindClient("Vista Prueba") != null);

        // ---------- Artículos en venta (se guardan como notas con kind = "sale") — mismas cuentas que Funcional.kt ----------
        var sapp = new EncargosApp(new MemoryStorage(), "PRUEBA");
        Check("Venta: rechaza artículo sin nombre", !sapp.SaveSaleItem(null, " ", "", 10, 5, 1, new List<string>()).Ok);
        var s1 = sapp.SaveSaleItem(null, "Tenis Prueba", "Talla 40", 25, 15, 4, new List<string> { "data:image/jpeg;base64,AAAA", "data:image/jpeg;base64,BBBB" }).GetOrThrow();
        sapp.SaveSaleItem(null, "Gorra Prueba", "", 8, 3, 2, new List<string>()).GetOrThrow();
        var sm = sapp.SaleSummaryOf();
        Check("Venta: 2 artículos y 6 unidades", sm.Items == 2 && sm.Units == 6, $"{sm.Items} / {sm.Units}");
        Check("Venta: valor en venta 4×25 + 2×8 = 116", Eq(sm.Value, 116), sm.Value);
        Check("Venta: invertido 4×15 + 2×3 = 66", Eq(sm.Invested, 66), sm.Invested);
        Check("Venta: ganancia posible 116 − 66 = 50", Eq(sm.Potential, 50), sm.Potential);
        Check("Venta: guarda varias fotos", s1["photos"] is JArr ph && ph.Count == 2);
        Check("Venta: la lista de notas no la cuenta como nota normal", sapp.Notes().Count(nn => !EncargosApp.IsSale(nn)) == 0);
        sapp.SellSaleItem(s1.Str("id"), 2, 22).GetOrThrow();
        sm = sapp.SaleSummaryOf();
        Check("Venta: vender 2 deja 2 disponibles", N(sapp.SaleItems().First(it => it.Str("id") == s1.Str("id")), "stock") == 2);
        Check("Venta: cobrado 2×22 = 44 y ganancia real 2×(22−15) = 14", Eq(sm.Income, 44) && Eq(sm.Realized, 14) && sm.SoldUnits == 2, $"{sm.Income} / {sm.Realized}");
        Check("Venta: no deja vender más de lo que queda", !sapp.SellSaleItem(s1.Str("id"), 3, 25).Ok);
        Check("Venta: deshacer devuelve las unidades", sapp.UndoLastSale(s1.Str("id")) && sapp.SaleSummaryOf().Units == 6 && Eq(sapp.SaleSummaryOf().Income, 0));
        Check("Venta: no cuenta como dinero que te deben en notas", sapp.Notes().Sum(nn => Js.Num(nn["amount"])) == 0);
        // peso y envío: compra 10 + 2 lb × $5/lb + 1 de otros = 21 por unidad
        var s3 = sapp.SaveSaleItem(null, "Perfume Prueba", "", 40, 10, 3, new List<string>(), 2, "avion", 5, 1).GetOrThrow();
        Check("Venta: costo por unidad = compra + peso × costo/lb + otros = 21", Eq(EncargosApp.SaleUnitCost(s3), 21), EncargosApp.SaleUnitCost(s3));
        sm = sapp.SaleSummaryOf();
        Check("Venta: inversión incluye el envío por peso (66 + 3×21 = 129)", Eq(sm.Invested, 129), sm.Invested);
        Check("Venta: libras de lo que tienes = 3 × 2 = 6", Eq(sm.Lbs, 6), sm.Lbs);
        sapp.SellSaleItem(s3.Str("id"), 1, 40).GetOrThrow();
        sm = sapp.SaleSummaryOf();
        Check("Venta: ganancia neta real = 40 − 21 = 19", Eq(sm.Realized, 19), sm.Realized);
        Check("Venta: inversión total incluye lo vendido (66 + 2×21 + 21 = 129)", Eq(sm.TotalInvested, 129) && Eq(sm.Invested, 108), $"{sm.TotalInvested} / {sm.Invested}");
        sapp.UndoLastSale(s3.Str("id"));
        var sjson = new Backup(sapp).ExportJson(); var sapp2 = new EncargosApp(new MemoryStorage(), "PRUEBA"); var sbk = new Backup(sapp2);
        if (sbk.Parse(sjson) is Backup.Parsed.Plain sp) sbk.ApplyImport(sp.O);
        Check("Venta: los artículos viajan en el respaldo (con peso y costos)", sapp2.SaleItems().Count == 3 && Eq(sapp2.SaleSummaryOf().Value, 236) && Eq(sapp2.SaleSummaryOf().Invested, 129),
            $"{sapp2.SaleItems().Count} / {sapp2.SaleSummaryOf().Value} / {sapp2.SaleSummaryOf().Invested}");

        var dir = Path.Combine(Path.GetTempPath(), "encargos-prueba-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try
        {
            var app1 = new EncargosApp(new FileStorage(Path.Combine(dir, "datos.json")), "PRUEBA");
            var o = app1.CreateOrder(new EncargosApp.NewOrder("Persistencia Prueba", "5355500002", 12.5, 7, 2, 3)).GetOrThrow();
            app1.SaveEdit(o["id"], 0, 4, "Efectivo", "", "En tránsito").GetOrThrow();
            var app2 = new EncargosApp(new FileStorage(Path.Combine(dir, "datos.json")), "PRUEBA");
            var o2 = app2.ById(o["id"]);
            Check("Reabrir: encargo, pagos y estado conservados", o2 != null && Eq(N(o2, "paid"), 7) && o2.Str("status") == "En tránsito" && Eq(N(o2, "balance"), 19.5));
        }
        finally { try { Directory.Delete(dir, true); } catch { } }

        Console.WriteLine($"\n{total} pruebas · {total - fails} correctas · {fails} fallidas");
        Console.WriteLine(fails > 0 ? "HAY FALLOS" : "TODO CORRECTO");
        return fails > 0 ? 1 : 0;
    }
}
