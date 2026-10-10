package com.agencia.encargos.core

import kotlin.math.floor

/** Centro de atención: alertas calculadas desde los datos, "revisada" ≠ "resuelta", actividad reciente. */
class Attention(private val app: Encargos) {
    data class Action(val kind: String, val label: String, val orderId: String? = null, val client: String? = null)
    data class Alert(val key: String, val cat: String, val pr: String, val sig: String, val title: String, val sub: String,
                     val amount: Double?, val amountGreen: Boolean, val actions: List<Action>, val refOrder: String?, val refClient: String?)

    private val K_REV = "encargos_attn_reviewed"; private val K_LAST = "encargos_attn_last"; private val K_ACT = "encargos_activity"
    val GROUPS = listOf("pay" to "Pagos pendientes", "ready" to "Pedidos listos para entregar", "transit" to "Pedidos en tránsito", "incomplete" to "Información incompleta", "funds" to "Fondos a favor", "backup" to "Respaldo")
    var recentResolved = ArrayList<Pair<Long, String>>()

    fun daysSince(t: Any?): Int { val ms = Js.parseDate(t); return if (ms.isNaN()) 0 else floor((System.currentTimeMillis() - ms) / 86400000.0).toInt() }
    fun ago(t: Any?): String { val d = daysSince(t); return if (d <= 0) "hoy" else if (d == 1) "hace 1 día" else "hace $d días" }

    fun log(type: String, text: String, amount: Double? = null) {
        val a = app.ls.arr(K_ACT); val e = jobj("t" to System.currentTimeMillis().toDouble(), "type" to type, "text" to text); if (amount != null) e["amount"] = amount
        a.add(0, e); app.ls.put(K_ACT, JArr(a.take(120)))
    }
    fun activity(): List<JObj> = app.ls.arr(K_ACT).objs()

    fun compute(): List<Alert> {
        val out = ArrayList<Alert>(); val ad = app.activeOrders(); val now = System.currentTimeMillis()
        app.activeClients().filter { it.pending > 0.005 }.forEach { c ->
            val pend = c.orders.filter { Js.num(it["balance"]) > 0.005 }.sortedBy { Js.parseDate(it["date"]) }
            val oldest = pend.firstOrNull()?.let { daysSince(it["date"]) } ?: 0
            val act = if (pend.size == 1) Action("pay", "Registrar pago", orderId = Js.str(pend[0]["id"])) else Action("client", "Registrar pago", client = c.name)
            out.add(Alert("pay:" + c.name.trim().lowercase(), "pay", if (oldest >= 30) "urgent" else "important", Js.toFixed(c.pending, 2), c.name,
                "$${Js.toFixed(c.pending, 2)} pendientes en ${pend.size} encargo${if (pend.size == 1) "" else "s"} · el más antiguo ${ago(pend.firstOrNull()?.get("date") ?: now.toDouble())}",
                c.pending, false, listOf(act, Action("remind", "WhatsApp", client = c.name)), null, c.name))
        }
        app.orders().filter { it["status"] == "Recibido" }.forEach { x ->
            val since = (x["statusDates"] as? JObj)?.get("Recibido")
            out.add(Alert("ready:" + Js.str(x["id"]), "ready", "important", "1", x.str("name"), (if (since != null) "Llegó ${ago(since)} · " else "") + "${Js.toFixed(Js.num(x["pounds"]), 2)} lb · listo para recoger",
                null, false, listOf(Action("order", "Ver pedido", orderId = Js.str(x["id"])), Action("arrival", "WhatsApp", client = x.str("name")), Action("deliver", "Entregado", orderId = Js.str(x["id"]))), Js.str(x["id"]), null))
        }
        val w = app.transitWarn; val l = app.transitLate
        app.orders().filter { it["status"] == "En tránsito" }.forEach { x ->
            val real = (x["statusDates"] as? JObj)?.get("En tránsito"); val d = daysSince(real ?: x["date"]); if (d < w) return@forEach
            out.add(Alert("transit:" + Js.str(x["id"]), "transit", if (d >= l) "urgent" else "important", (d >= l).toString(), x.str("name"),
                (if (real != null) "Lleva $d días en tránsito" else "Registrado hace $d días y sigue en tránsito") + (if (d >= l) " · atrasado" else ""),
                null, false, listOf(Action("order", "Ver pedido", orderId = Js.str(x["id"])), Action("advance", "Llegó", orderId = Js.str(x["id"]))), Js.str(x["id"]), null))
        }
        app.orders().filter { x -> (x["status"] != "Entregado" && x["status"] != "Cancelado") || (!(Js.num(x["cost"]) > 0) && x["status"] != "Cancelado" && ad.contains(x)) }.forEach { x ->
            val miss = ArrayList<String>()
            if (x.str("name").trim().isEmpty()) miss.add("nombre del cliente")
            if (!(Js.num(x["cost"]) > 0)) miss.add("costo del pedido")
            if ((x["status"] == "Recibido" || x["status"] == "Entregado") && !(Js.num(x["pounds"]) > 0)) miss.add("peso (libras)")
            if (!Encargos.STATES.contains(x.str("status").ifEmpty { "Pedido" })) miss.add("estado válido")
            if (x.str("phone").replace(Regex("\\D"), "").isEmpty()) miss.add("teléfono (para WhatsApp)")
            if (miss.isNotEmpty()) out.add(Alert("inc:" + Js.str(x["id"]), "incomplete", if (miss.size == 1 && miss[0].startsWith("teléfono")) "info" else "important", miss.joinToString(","),
                x.str("name").ifEmpty { "Encargo sin nombre" }, "Falta: " + miss.joinToString(", "), null, false, listOf(Action("edit", "Editar", orderId = Js.str(x["id"]))), Js.str(x["id"]), null))
        }
        app.activeClients().filter { it.funds > 0.005 }.forEach { c ->
            out.add(Alert("funds:" + c.name.trim().lowercase(), "funds", "info", Js.toFixed(c.funds, 2), c.name, "Tiene $${Js.toFixed(c.funds, 2)} a favor", c.funds, true, listOf(Action("client", "Ver cliente", client = c.name)), null, c.name))
        }
        val lb = lastBackup()
        if (app.data.isNotEmpty() && (lb == 0.0 || now - lb > 7 * 86400000.0))
            out.add(Alert("backup", "backup", "info", "1", "Respaldo pendiente", if (lb > 0) "Último respaldo ${ago(lb)}" else "Aún no has hecho ningún respaldo", null, false, listOf(Action("backup", "Crear respaldo")), null, null))
        return out
    }
    fun lastBackup(): Double = (app.ls.get("encargos_last_backup", "0") ?: "0").toDoubleOrNull() ?: 0.0

    fun reviewed(): JObj = app.ls.json(K_REV) as? JObj ?: JObj()
    fun isReviewed(a: Alert) = reviewed()[a.key] == a.sig
    fun toggleReview(key: String) { val a = compute().firstOrNull { it.key == key } ?: return; val r = reviewed(); if (r[key] == a.sig) r.remove(key) else r[key] = a.sig; app.ls.put(K_REV, r); app.changed(false) }
    fun reviewAll() { val r = JObj(); compute().forEach { r[it.key] = it.sig }; app.ls.put(K_REV, r); app.changed(false) }
    /** Insignia: urgentes + importantes no revisadas. */
    fun badge(all: List<Alert> = compute()): Int = all.count { it.pr != "info" && !isReviewed(it) }

    /** detectResolved: registra en la actividad lo que dejó de ser un problema. */
    fun detectResolved(alerts: List<Alert>) {
        val last = app.ls.json(K_LAST) as? JObj
        val nowKeys = JObj(); alerts.forEach { a -> nowKeys[a.key] = jobj("t" to a.title, "cat" to a.cat, "ref" to jobj("order" to a.refOrder, "client" to a.refClient)) }
        last?.forEach { (k, v) ->
            if (nowKeys[k] != null) return@forEach
            val o = v as? JObj ?: return@forEach; val cat = o.str("cat"); if (cat !in listOf("pay", "ready", "transit", "incomplete")) return@forEach
            val ref = o["ref"] as? JObj ?: JObj()
            if (ref["order"] != null && app.byId(ref["order"]) == null) return@forEach
            if (ref["client"] != null && app.clients().none { it.key == Js.str(ref["client"]).trim().lowercase() }) return@forEach
            val t = o.str("t")
            val txt = when (cat) { "pay" -> "$t: saldo pendiente resuelto"; "ready" -> "$t: pedido entregado"; "transit" -> "$t: pedido ya no está atrasado"; else -> "$t: información completada" }
            if (cat != "ready") log("resolved", txt); recentResolved.add(0, System.currentTimeMillis() to txt)
        }
        recentResolved = ArrayList(recentResolved.filter { System.currentTimeMillis() - it.first < 60000 }.take(4))
        app.ls.put(K_LAST, nowKeys)
        val r = reviewed(); var ch = false; r.keys.toList().forEach { if (nowKeys[it] == null) { r.remove(it); ch = true } }; if (ch) app.ls.put(K_REV, r)
    }
}
