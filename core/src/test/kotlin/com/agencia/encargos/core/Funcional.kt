package com.agencia.encargos.core

import java.io.File
import kotlin.math.abs

/**
 * Pruebas funcionales del núcleo con datos ficticios aislados (nunca datos reales):
 * clientes con varios encargos, pagos parciales, edición y eliminación de pagos, estados,
 * persistencia al reabrir, eliminación con deshacer, WhatsApp, centro de atención y panel.
 */
object Funcional {
    var fails = 0; var total = 0
    fun check(name: String, ok: Boolean, info: Any? = null) { total++; println((if (ok) "✅ " else "❌ ") + name + (if (info != null) " · $info" else "")); if (!ok) fails++ }
    fun eq(a: Double, b: Double) = abs(a - b) < 0.005
    fun n(x: JObj?, k: String) = Js.num(x?.get(k))

    @JvmStatic fun main(args: Array<String>) {
        val app = Encargos(MemoryStorage(), "PRUEBA")
        app.phoneCC = "53"

        // ---------- Encargos: cálculos ----------
        val a = app.createOrder(Encargos.NewOrder("Ana Prueba", "55512345", 40.0, 7.0, 3.0, 10.0)).getOrThrow()
        check("Encargo A: envío 3 lb × 7 = 21", eq(n(a, "shipping"), 21.0), a["shipping"])
        check("Encargo A: total 40 + 21 = 61", eq(n(a, "total"), 61.0), a["total"])
        check("Encargo A: saldo 61 − 10 = 51", eq(n(a, "balance"), 51.0), a["balance"])
        check("Encargo A: pago inicial en el historial", (a["paymentHistory"] as JArr).size == 1)
        val b = app.createOrder(Encargos.NewOrder("ana prueba ", "55512345", 20.0, 7.0, 1.0, 50.0, "USD")).getOrThrow()
        check("Encargo B: total 27, saldo −23 (fondo a favor)", eq(n(b, "total"), 27.0) && eq(n(b, "balance"), -23.0), "${b["total"]} / ${b["balance"]}")
        check("Mismo cliente aunque cambien mayúsculas/espacios: comparte código", b["accessCode"] == a["accessCode"])
        check("Número de encargo correlativo", n(b, "orderNo") == n(a, "orderNo") + 1)

        // ---------- Validaciones ----------
        check("Rechaza nombre vacío", app.createOrder(Encargos.NewOrder("  ", "", 1.0, 1.0, 1.0, 0.0)).isFailure)
        check("Rechaza valores negativos", app.createOrder(Encargos.NewOrder("X", "", -1.0, 1.0, 1.0, 0.0)).isFailure)
        check("Rechaza pago negativo al editar", app.saveEdit(a["id"], 0.0, -5.0, "Efectivo", "", "Pedido").isFailure)
        check("No se crearon encargos de más", app.orders().size == 2, app.orders().size)

        // ---------- Clientes: saldo consolidado ----------
        val c = app.findClient("ANA PRUEBA")
        check("Ficha del cliente encontrada sin importar mayúsculas", c != null)
        check("Cliente con 2 encargos", c?.orders?.size == 2, c?.orders?.size)
        check("Pendiente consolidado = 51 (solo saldos positivos)", eq(c!!.pending, 51.0), c.pending)
        check("Fondos a favor consolidados = 23", eq(c.funds, 23.0), c.funds)
        check("Cobrado total del cliente = 60", eq(c.paid, 60.0), c.paid)
        check("Libras del cliente = 4", eq(c.pounds, 4.0), c.pounds)
        check("Cliente activo (tiene saldo)", app.activeClients().any { it.key == "ana prueba" })

        // ---------- Pagos parciales, edición y eliminación ----------
        app.saveEdit(a["id"], 0.0, 21.0, "Efectivo", "abono", "Pedido").getOrThrow()
        var x = app.byId(a["id"])
        check("Pago parcial 21: pagado 31, saldo 30", eq(n(x, "paid"), 31.0) && eq(n(x, "balance"), 30.0), "${x?.get("paid")} / ${x?.get("balance")}")
        check("Historial con 2 pagos", (x!!["paymentHistory"] as JArr).size == 2)
        app.payEdit(a["id"], 1, 25.0, "USD", "corregido").getOrThrow()
        x = app.byId(a["id"])
        check("Editar pago 21→25: pagado 35, saldo 26", eq(n(x, "paid"), 35.0) && eq(n(x, "balance"), 26.0), "${x?.get("paid")} / ${x?.get("balance")}")
        check("Editar a 0 se rechaza", app.payEdit(a["id"], 1, 0.0, "USD", "").isFailure)
        val snap = app.payDelete(a["id"], 1)
        x = app.byId(a["id"])
        check("Eliminar pago: vuelve a pagado 10, saldo 51", eq(n(x, "paid"), 10.0) && eq(n(x, "balance"), 51.0), "${x?.get("paid")} / ${x?.get("balance")}")
        app.restoreOrder(snap!!)
        x = app.byId(a["id"])
        check("Deshacer eliminación de pago: pagado 35", eq(n(x, "paid"), 35.0), x?.get("paid"))
        check("La suma del historial coincide con lo pagado", eq((x!!["paymentHistory"] as JArr).objs().sumOf { Js.num(it["amount"]) }, n(x, "paid")))

        // ---------- Libras añadidas ----------
        app.saveEdit(a["id"], 2.0, 0.0, "Efectivo", "", "Pedido").getOrThrow()
        x = app.byId(a["id"])
        check("Añadir 2 lb: 5 lb, envío 35, total 75, saldo 40", eq(n(x, "pounds"), 5.0) && eq(n(x, "shipping"), 35.0) && eq(n(x, "total"), 75.0) && eq(n(x, "balance"), 40.0),
            "${x?.get("pounds")} lb · ${x?.get("total")} · ${x?.get("balance")}")

        // ---------- Estados ----------
        val flow = ArrayList<String>(); var prev: String?
        do { prev = app.advance(a["id"]); if (prev != null) flow.add(app.byId(a["id"])!!.str("status")) } while (prev != null)
        check("Flujo de estados Pedido → En tránsito → Recibido → Entregado", flow == listOf("En tránsito", "Recibido", "Entregado"), flow)
        check("statusDates registra cada estado", (app.byId(a["id"])!!["statusDates"] as JObj).keys.containsAll(listOf("Pedido", "En tránsito", "Recibido", "Entregado")))

        // ---------- Saldar y completar cliente ----------
        app.saveEdit(a["id"], 0.0, 40.0, "Efectivo", "", "Entregado").getOrThrow()
        check("Encargo A saldado (saldo 0)", eq(n(app.byId(a["id"]), "balance"), 0.0))
        check("Cliente sigue activo mientras B tenga fondo a favor (regla de la web)", app.activeClients().any { it.key == "ana prueba" })
        app.saveEdit(b["id"], 23.0 / 7.0, 0.0, "Efectivo", "", "Entregado").getOrThrow()
        check("B saldado al añadir libras equivalentes al fondo", abs(n(app.byId(b["id"]), "balance")) < 0.01, app.byId(b["id"])?.get("balance"))
        check("Cliente completado cuando todo está saldado", app.completedClients().any { it.key == "ana prueba" })

        // ---------- Centro de atención consolidado ----------
        val p1 = app.createOrder(Encargos.NewOrder("Luis Prueba", "5355500001", 10.0, 7.0, 1.0, 0.0)).getOrThrow()
        app.createOrder(Encargos.NewOrder("Luis Prueba", "5355500001", 30.0, 7.0, 2.0, 5.0)).getOrThrow()
        val pay = app.attention.compute().firstOrNull { it.cat == "pay" && it.refClient == "Luis Prueba" }
        check("Atención: cliente con saldo aparece", pay != null)
        check("Atención: importe consolidado 17 + 39 = 56", pay != null && eq(pay.amount ?: 0.0, 56.0), pay?.amount)
        check("Atención: el cliente saldado no aparece en pagos", app.attention.compute().none { it.cat == "pay" && it.refClient?.lowercase() == "ana prueba" })

        // ---------- Panel principal ----------
        val st = Stats(app); val d = st.dashboard()
        check("Panel: pendiente = 56 (solo clientes activos)", eq(d.pending, 56.0), d.pending)
        check("Panel: clientes activos = 1", d.clients == 1, d.clients)
        check("Panel: cobrado de activos = 5", eq(d.paid, 5.0), d.paid)
        check("Panel: estados por pedido suman los activos", d.byStatus.sumOf { it.second } + d.received == app.activeOrders().size)

        // ---------- WhatsApp ----------
        check("waNumber agrega 53 a números de 8 cifras", app.waNumber("5551 2345") == "5355512345", app.waNumber("5551 2345"))
        check("waNumber respeta números con código", app.waNumber("+53 5551-2345") == "5355512345")
        check("waLink sin teléfono → null", app.waLink("", "hola") == null)
        val link = app.waLink("55512345", "Hola Ana, saldo $51.00")
        check("waLink codifica el mensaje", link != null && link.startsWith("https://wa.me/5355512345?text=") && !link.contains(" "), link)
        val luis = app.findClient("Luis Prueba")!!
        val rem = app.reminderClient(luis)
        check("Recordatorio menciona al cliente y el saldo real", rem.contains("Luis") && rem.contains("56"), rem.take(120))

        // ---------- Eliminar con deshacer ----------
        val before = app.orders().size
        val r = app.removeOrder(p1["id"])
        check("Eliminar encargo", r != null && app.orders().size == before - 1)
        check("Deshacer eliminación", app.undoRemove(r!!) && app.orders().size == before)
        check("Deshacer dos veces no duplica", !app.undoRemove(r) && app.orders().size == before)

        // ---------- Vista reutilizada: se actualiza en cuanto cambian los datos ----------
        val n0 = app.activeOrders().size; val c0 = app.clients().size
        val nv = app.createOrder(Encargos.NewOrder("Vista Prueba", "5355500009", 5.0, 7.0, 1.0, 0.0)).getOrThrow()
        check("Vista: el encargo nuevo aparece al momento en activos", app.activeOrders().size == n0 + 1 && app.activeOrders().any { it["id"] == nv["id"] })
        check("Vista: el cliente nuevo aparece al momento", app.clients().size == c0 + 1 && app.findClient("vista prueba") != null)
        app.saveEdit(nv["id"], 0.0, 12.0, "Efectivo", "", "Entregado").getOrThrow()
        check("Vista: al saldarlo pasa a completados sin reiniciar", app.completedClients().any { it.key == "vista prueba" } && app.activeOrders().none { it["id"] == nv["id"] })
        val rv = app.removeOrder(nv["id"]); check("Vista: al eliminarlo desaparece de las listas", app.findClient("Vista Prueba") == null)
        app.undoRemove(rv!!); check("Vista: deshacer lo devuelve", app.findClient("Vista Prueba") != null)

        // ---------- Persistencia al reabrir ----------
        val dir = File(System.getProperty("java.io.tmpdir"), "encargos-prueba-" + System.nanoTime()).apply { mkdirs() }
        try {
            val app1 = Encargos(FileStorage(File(dir, "datos.json")), "PRUEBA")
            val o = app1.createOrder(Encargos.NewOrder("Persistencia Prueba", "5355500002", 12.5, 7.0, 2.0, 3.0)).getOrThrow()
            app1.saveEdit(o["id"], 0.0, 4.0, "Efectivo", "", "En tránsito").getOrThrow()
            app1.saveNote(null, "Nota de prueba", "texto", 0.0, "blue", false, emptyList())
            val app2 = Encargos(FileStorage(File(dir, "datos.json")), "PRUEBA")
            val o2 = app2.byId(o["id"])
            check("Reabrir: el encargo sigue ahí", o2 != null)
            check("Reabrir: pagos y estado conservados", o2 != null && eq(n(o2, "paid"), 7.0) && o2["status"] == "En tránsito" && eq(n(o2, "balance"), 19.5), "${o2?.get("paid")} / ${o2?.get("balance")}")
            check("Reabrir: notas conservadas", app2.notes().any { it.str("title") == "Nota de prueba" })
        } finally { dir.deleteRecursively() }

        // ---------- Respaldo JSON ida y vuelta (sin perder campos) ----------
        val bk = Backup(app); val json = bk.exportJson()
        val app3 = Encargos(MemoryStorage(), "PRUEBA"); val n3 = Backup(app3).applyImport((Backup(app3).parse(json) as Backup.Parsed.Plain).o)
        check("Respaldo: importa todos los encargos", n3 == app.orders().size, "$n3 de ${app.orders().size}")
        check("Respaldo: saldos idénticos tras importar", app3.orders().sumOf { Js.num(it["balance"]) }.let { eq(it, app.orders().sumOf { o -> Js.num(o["balance"]) }) })

        // ---------- Artículos en venta (se guardan como notas con kind = "sale") ----------
        val sapp = Encargos(MemoryStorage(), "PRUEBA")
        check("Venta: rechaza artículo sin nombre", sapp.saveSaleItem(null, " ", "", 10.0, 5.0, 1, emptyList()).isFailure)
        val s1 = sapp.saveSaleItem(null, "Tenis Prueba", "Talla 40", 25.0, 15.0, 4, listOf("data:image/jpeg;base64,AAAA", "data:image/jpeg;base64,BBBB")).getOrThrow()
        sapp.saveSaleItem(null, "Gorra Prueba", "", 8.0, 3.0, 2, emptyList()).getOrThrow()
        var sm = sapp.saleSummary()
        check("Venta: 2 artículos y 6 unidades", sm.items == 2 && sm.units == 6, "${sm.items} / ${sm.units}")
        check("Venta: valor en venta 4×25 + 2×8 = 116", eq(sm.value, 116.0), sm.value)
        check("Venta: invertido 4×15 + 2×3 = 66", eq(sm.invested, 66.0), sm.invested)
        check("Venta: ganancia posible 116 − 66 = 50", eq(sm.potential, 50.0), sm.potential)
        check("Venta: guarda varias fotos", (s1["photos"] as JArr).size == 2)
        sapp.sellSaleItem(s1.str("id"), 2, 22.0).getOrThrow()
        sm = sapp.saleSummary()
        check("Venta: vender 2 deja 2 disponibles", n(sapp.saleItems().first { it.str("id") == s1.str("id") }, "stock") == 2.0)
        check("Venta: cobrado 2×22 = 44 y ganancia real 2×(22−15) = 14", eq(sm.income, 44.0) && eq(sm.realized, 14.0) && sm.soldUnits == 2, "${sm.income} / ${sm.realized}")
        check("Venta: no deja vender más de lo que queda", sapp.sellSaleItem(s1.str("id"), 3, 25.0).isFailure)
        check("Venta: deshacer devuelve las unidades", sapp.undoLastSale(s1.str("id")) && sapp.saleSummary().units == 6 && eq(sapp.saleSummary().income, 0.0))
        check("Venta: no cuenta como dinero que te deben en notas", sapp.notes().sumOf { Js.num(it["amount"]) } == 0.0)
        // peso y envío: compra 10 + 2 lb × $5/lb + 1 de otros = 21 por unidad
        val s3 = sapp.saveSaleItem(null, "Perfume Prueba", "", 40.0, 10.0, 3, emptyList(), 2.0, "avion", 5.0, 1.0).getOrThrow()
        check("Venta: costo por unidad = compra + peso × costo/lb + otros = 21", eq(sapp.saleUnitCost(s3), 21.0), sapp.saleUnitCost(s3))
        sm = sapp.saleSummary()
        check("Venta: inversión incluye el envío por peso (66 + 3×21 = 129)", eq(sm.invested, 129.0), sm.invested)
        check("Venta: libras de lo que tienes = 3 × 2 = 6", eq(sm.lbs, 6.0), sm.lbs)
        sapp.sellSaleItem(s3.str("id"), 1, 40.0).getOrThrow()
        sm = sapp.saleSummary()
        check("Venta: ganancia neta real = 40 − 21 = 19", eq(sm.realized, 19.0), sm.realized)
        check("Venta: inversión total incluye lo vendido (66 + 2×21 + 21 = 129)", eq(sm.totalInvested, 129.0) && eq(sm.invested, 108.0), "${sm.totalInvested} / ${sm.invested}")
        sapp.undoLastSale(s3.str("id"))
        val sjson = Backup(sapp).exportJson(); val sapp2 = Encargos(MemoryStorage(), "PRUEBA"); Backup(sapp2).applyImport((Backup(sapp2).parse(sjson) as Backup.Parsed.Plain).o)
        check("Venta: los artículos viajan en el respaldo (con peso y costos)", sapp2.saleItems().size == 3 && eq(sapp2.saleSummary().value, 236.0) && eq(sapp2.saleSummary().invested, 129.0))

        println("\n$total pruebas · ${total - fails} correctas · $fails fallidas")
        if (fails > 0) { println("HAY FALLOS"); kotlin.system.exitProcess(1) } else println("TODO CORRECTO")
    }
}
