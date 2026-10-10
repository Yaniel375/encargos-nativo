package com.agencia.encargos.core

import kotlin.math.abs

/** Centro de notificaciones: detecta eventos comparando el estado anterior y el nuevo (igual que la app web). */
class Notifications(private val app: Encargos) {
    private val K_LIST = "encargos_notifs"; private val K_CFG = "encargos_notif_cfg"
    private val K_SEEN_SEC = "encargos_notif_secseen"; private val K_SEEN_BK = "encargos_notif_bkseen"
    private val MAX = 1000
    val CAT = linkedMapOf("pay" to "Pagos", "order" to "Pedidos", "client" to "Clientes", "system" to "Sistema")
    /** Aviso breve dentro de la app (la interfaz lo muestra como toast). */
    var onImportant: ((JObj) -> Unit)? = null
    var suppressUntil = 0L

    fun cfg(): JObj { val c = jobj("inapp" to true, "pay" to true, "order" to true, "client" to true, "system" to true, "important" to true); (app.ls.json(K_CFG) as? JObj)?.forEach { (k, v) -> c[k] = v }; return c }
    fun toggleCfg(k: String) { val c = cfg(); c[k] = !Js.truthy(c[k]); app.ls.put(K_CFG, c); app.changed(false) }
    fun all(): List<JObj> = app.ls.arr(K_LIST).objs()
    private fun store(a: List<JObj>) = app.ls.put(K_LIST, JArr(a.take(MAX)))
    fun visibleCats(): List<String> { val c = cfg(); return listOf("pay", "order", "client", "system").filter { c[it] != false } }
    fun unreadCount(): Int { val cats = visibleCats(); return all().count { !Js.truthy(it["read"]) && cats.contains(it.str("cat")) } }

    fun add(n: JObj) {
        val a = ArrayList(all())
        n["id"] = "n" + java.lang.Long.toString(System.currentTimeMillis(), 36) + Crypto.hex(Crypto.random(2))
        if (n["t"] == null) n["t"] = System.currentTimeMillis().toDouble()
        n["read"] = false
        a.add(0, n); store(a)
        val c = cfg()
        if (c[n.str("cat")] == false) return
        if (Js.truthy(n["imp"]) && Js.truthy(c["important"]) && Js.truthy(c["inapp"])) onImportant?.invoke(n)
    }
    fun read(id: String) { val a = all(); a.firstOrNull { it["id"] == id }?.let { it["read"] = true; store(a) }; app.changed(false) }
    fun readAll() { val a = all(); a.forEach { it["read"] = true }; store(a); app.changed(false) }
    fun clear() { store(emptyList()); app.changed(false) }

    fun list(filter: String, q: String): List<JObj> {
        val cats = visibleCats(); var a = all().filter { cats.contains(it.str("cat")) }
        if (filter == "unread") a = a.filter { !Js.truthy(it["read"]) } else if (CAT.containsKey(filter)) a = a.filter { it.str("cat") == filter }
        if (q.isNotBlank()) { val nq = Js.normSearch(q); a = a.filter { n -> Js.normSearch(listOf(n.str("title"), n.str("text"), CAT[n.str("cat")] ?: "", (n["data"] as? JObj)?.str("method") ?: "", (n["data"] as? JObj)?.str("status") ?: "", (n["ref"] as? JObj)?.str("client") ?: "").joinToString(" ")).contains(nq) } }
        return a
    }

    /* ---------- detección por diferencias ---------- */
    private data class OSnap(val id: String, val name: String, val status: String, val paid: Double, val bal: Double, val lb: Double, val ph: Int, val cost: Double)
    private data class CSnap(val name: String, val pending: Double, val funds: Double)
    private var snap: Pair<Map<String, OSnap>, Map<String, CSnap>>? = null

    private fun takeSnap(): Pair<Map<String, OSnap>, Map<String, CSnap>> {
        val o = LinkedHashMap<String, OSnap>(); app.orders().forEach { x -> val id = Js.str(x["id"]); o[id] = OSnap(id, x.str("name"), x.str("status").ifEmpty { "Pedido" }, Js.num(x["paid"]), Js.num(x["balance"]), Js.num(x["pounds"]), (x["paymentHistory"] as? JArr)?.size ?: 0, Js.num(x["cost"])) }
        val cl = LinkedHashMap<String, CSnap>(); app.clients().forEach { c -> cl[c.key] = CSnap(c.name, c.pending, c.funds) }
        return o to cl
    }
    fun resetSnapshot() { snap = try { takeSnap() } catch (e: Exception) { null } }

    fun diff() {
        val now = takeSnap(); val prev = snap; snap = now
        if (prev == null) return
        if (System.currentTimeMillis() < suppressUntil) return
        val (po, pcl) = prev; val (no, ncl) = now
        if (po.isEmpty() && no.size > 3) { add(jobj("cat" to "system", "type" to "bulk", "title" to "Datos recuperados", "text" to "Se cargaron ${no.size} encargos guardados.", "short" to "Datos recuperados")); return }
        val ev = ArrayList<JObj>()
        ncl.forEach { (k, c) -> if (pcl[k] == null) ev.add(jobj("cat" to "client", "type" to "client_new", "title" to "Cliente agregado", "text" to "${c.name} fue agregado a tus clientes.", "short" to c.name, "ref" to jobj("client" to c.name))) }
        no.values.forEach { x ->
            val p = po[x.id]; val ord = app.byId(x.id)
            if (p == null) {
                ev.add(jobj("cat" to "order", "type" to "order_new", "title" to "Encargo creado", "text" to "${x.name} · ${Js.money(ord?.get("total"))}", "short" to x.name, "ref" to jobj("order" to x.id, "client" to x.name), "data" to jobj("total" to Js.num(ord?.get("total")), "status" to x.status)))
                if (x.paid > 0.005) { val h = (ord?.get("paymentHistory") as? JArr)?.lastOrNull() as? JObj ?: JObj(); ev.add(jobj("cat" to "pay", "type" to "pay_new", "title" to "Pago registrado", "text" to "${x.name} realizó un pago de ${Js.money(x.paid)}.", "short" to "${x.name} · ${Js.money(x.paid)}", "ref" to jobj("order" to x.id, "client" to x.name), "data" to jobj("amount" to x.paid, "method" to h.str("method").ifEmpty { "Efectivo" }))) }
                return@forEach
            }
            if (p.status != x.status) {
                val m = when (x.status) {
                    "En tránsito" -> listOf("Pedido en tránsito", "El pedido de ${x.name} va en camino.", "truck", false)
                    "Recibido" -> listOf("Pedido disponible", "El pedido de ${x.name} ya llegó y está listo para entregar.", "box", true)
                    "Entregado" -> listOf("Pedido entregado", "El pedido de ${x.name} fue entregado.", "check", false)
                    "Cancelado" -> listOf("Pedido archivado", "El pedido de ${x.name} fue cancelado.", "box", false)
                    "Pedido" -> listOf("Pedido actualizado", "El pedido de ${x.name} volvió a \"Pedido\".", "box", false)
                    else -> listOf("Pedido actualizado", "${x.name} → ${x.status}", "box", false)
                }
                ev.add(jobj("cat" to "order", "type" to "status", "title" to m[0], "text" to m[1], "short" to x.name, "icon" to m[2], "imp" to m[3], "ref" to jobj("order" to x.id, "client" to x.name), "data" to jobj("status" to x.status, "from" to p.status)))
            }
            if (x.ph > p.ph && x.paid > p.paid + 0.005) {
                val h = (ord?.get("paymentHistory") as? JArr)?.lastOrNull() as? JObj ?: JObj(); val amt = x.paid - p.paid
                ev.add(jobj("cat" to "pay", "type" to "pay_new", "title" to "Pago registrado", "text" to "${x.name} realizó un pago de ${Js.money(amt)}.", "short" to "${x.name} · ${Js.money(amt)}", "ref" to jobj("order" to x.id, "client" to x.name), "data" to jobj("amount" to amt, "method" to h.str("method").ifEmpty { "Efectivo" })))
            } else if (abs(x.paid - p.paid) > 0.005) {
                val del = x.ph < p.ph
                ev.add(jobj("cat" to "pay", "type" to if (del) "pay_del" else "pay_edit", "title" to if (del) "Pago eliminado" else "Pago modificado", "text" to "${x.name}: lo pagado pasó de ${Js.money(p.paid)} a ${Js.money(x.paid)}.", "short" to x.name, "ref" to jobj("order" to x.id, "client" to x.name), "data" to jobj("amount" to x.paid - p.paid)))
            } else if (p.status == x.status && (abs(x.lb - p.lb) > 0.004 || abs(x.cost - p.cost) > 0.004)) {
                ev.add(jobj("cat" to "order", "type" to "order_edit", "title" to "Encargo actualizado", "text" to if (abs(x.lb - p.lb) > 0.004) "${x.name}: ${Js.toFixed(p.lb, 2)} → ${Js.toFixed(x.lb, 2)} lb." else "${x.name}: costo actualizado a ${Js.money(x.cost)}.", "short" to x.name, "ref" to jobj("order" to x.id, "client" to x.name)))
            }
        }
        po.values.forEach { p -> if (no[p.id] == null) ev.add(jobj("cat" to "order", "type" to "order_del", "title" to "Encargo eliminado", "text" to "Se eliminó un encargo de ${p.name}.", "short" to p.name, "ref" to jobj("client" to p.name))) }
        ncl.forEach { (k, c) -> val p = pcl[k] ?: return@forEach
            if (p.pending > 0.005 && c.pending <= 0.005) ev.add(jobj("cat" to "pay", "type" to "settled", "title" to "Saldo pendiente liquidado", "text" to "${c.name} ya no debe nada.", "short" to c.name, "imp" to true, "ref" to jobj("client" to c.name)))
            if (p.funds <= 0.005 && c.funds > 0.005) ev.add(jobj("cat" to "pay", "type" to "funds", "title" to "Fondo a favor generado", "text" to "${c.name} ahora tiene ${Js.money(c.funds)} a favor.", "short" to "${c.name} · ${Js.money(c.funds)}", "ref" to jobj("client" to c.name), "data" to jobj("amount" to c.funds)))
        }
        if (ev.size > 12) { add(jobj("cat" to "system", "type" to "bulk", "title" to "Datos actualizados", "text" to "Se actualizaron ${no.size} encargos de una vez.", "short" to "Actualización de datos")); return }
        ev.forEach { add(it) }
    }

    /** Eventos del sistema: respaldos y cambios de seguridad. */
    fun sysEvents() {
        val lb = (app.ls.get("encargos_last_backup", "0") ?: "0").toDoubleOrNull() ?: 0.0
        val seen = (app.ls.get(K_SEEN_BK, "0") ?: "0").toDoubleOrNull() ?: 0.0
        if (lb > 0 && lb > seen) {
            app.ls.set(K_SEEN_BK, Js.numberToString(lb))
            if (seen > 0) add(jobj("cat" to "system", "type" to "backup", "title" to "Respaldo completado", "text" to if ((app.ls.get("encargos_last_backup_enc", "0") ?: "0").toDoubleOrNull() == lb) "Se creó un respaldo cifrado de tus datos." else "Tus datos fueron respaldados correctamente.", "short" to "Respaldo guardado", "ref" to jobj("panel" to "backup")))
        }
        val sl = app.ls.arr("encargos_sec_log").objs(); val ss = (app.ls.get(K_SEEN_SEC, "0") ?: "0").toDoubleOrNull() ?: 0.0
        val rel = Regex("^(PIN creado|PIN cambiado|PIN desactivado|Huella activada|Huella desactivada|Protección de acciones sensibles|Privacidad en recientes|Todos los datos eliminados|App restablecida|Bloqueo automático:)")
        if (sl.isNotEmpty()) {
            if (ss == 0.0) app.ls.set(K_SEEN_SEC, Js.numberToString(Js.num(sl[0]["t"])))
            else { sl.filter { Js.num(it["t"]) > ss && rel.containsMatchIn(it.str("text")) }.reversed().forEach { l -> add(jobj("cat" to "system", "type" to "security", "title" to "Seguridad", "text" to l.str("text") + ".", "short" to l.str("text"), "t" to Js.num(l["t"]), "ref" to jobj("panel" to "security"))) }; app.ls.set(K_SEEN_SEC, Js.numberToString(Js.num(sl[0]["t"]))) }
        }
    }
    fun importDone(n: Int) { suppressUntil = System.currentTimeMillis() + 1500; resetSnapshot(); add(jobj("cat" to "system", "type" to "import", "title" to "Importación completada", "text" to "Se importaron $n encargos desde un respaldo.", "short" to "Respaldo importado", "ref" to jobj("panel" to "orders"))) }
}
