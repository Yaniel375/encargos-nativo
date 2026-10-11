package com.agencia.encargos.core

import java.text.Collator
import java.util.Locale
import kotlin.math.abs
import kotlin.math.max

/**
 * Núcleo de Encargos: los mismos datos, cálculos y reglas que la app web.
 * Todo se guarda con las mismas claves (encargos_v3_data, encargos_notes, …) en el Storage de cada plataforma.
 * La interfaz (Compose o WinUI) llama a estas funciones y vuelve a dibujar cuando cambia [version].
 */
class Encargos(val storage: Storage, val platform: String = "ANDROID") {
    val ls = LS(storage)
    @Volatile var version = 0L; private set
    private val listeners = mutableListOf<() -> Unit>()
    fun onChange(l: () -> Unit) { synchronized(listeners) { listeners.add(l) } }
    /** Se llama cada vez que se guardan datos del negocio (la sincronización se engancha aquí). */
    var onPersist: (() -> Unit)? = null
    /** true mientras se aplican cambios que llegaron de otro dispositivo. */
    var applyingRemote = false

    var data: JArr = JArr()
    var shippingRates: JObj = JObj(); var shippingNames: JObj = JObj()

    val notifs = Notifications(this)
    val attention = Attention(this)

    companion object {
        const val KEY = "encargos_v3_data"
        const val SHIPPING_KEY = "encargos_shipping_rates_v2"
        val DEFAULT_SHIPPING_RATES = linkedMapOf("avion" to 7.0, "barco" to 4.0)
        val DEFAULT_SHIPPING_NAMES = linkedMapOf("avion" to "✈️ Avión", "barco" to "🚢 Barco")
        val FLOW = listOf("Pedido", "En tránsito", "Recibido", "Entregado")
        val STATES = listOf("Pedido", "En tránsito", "Recibido", "Entregado", "Cancelado")
        val DEF_BUY = linkedMapOf("avion" to 5.0, "barco" to 2.8)
        val DEF_TPL = linkedMapOf(
            "arrival" to "Hola {nombre}, te informamos que tu pedido ya llegó. 📦✅\n\nPor favor, ponte en contacto conmigo para coordinar la recogida.",
            "reminder" to "Hola {nombre}, ¿cómo estás? 😊 Te escribo para recordarte que tienes un saldo pendiente de {saldo} por tu pedido (total {total}, pagado {pagado}).\n\nCuando puedas, me avisas. ¡Gracias!",
            "thanks" to "Hola {nombre}, ¡muchas gracias por tu compra! 🙌 Fue un placer atenderte. Aquí estoy para tu próximo encargo.")
        val EXP_CATS = listOf("transporte" to "Transporte", "aduana" to "Aduana", "empaque" to "Empaques", "otro" to "Otros")
        private val CODE_CHARS = "23456789ABCDEFGHJKMNPQRSTUVWXYZ"
        val collator: Collator = Collator.getInstance(Locale("es")).apply { strength = Collator.SECONDARY }
    }

    init { reload() }

    /** Vuelve a leer todo desde el almacenamiento (también tras importar o restaurar). */
    fun reload() {
        data = load()
        loadShippingConfig()
        notifs.resetSnapshot()
        changed(false)
    }

    fun changed(fromPersist: Boolean = true) {
        version++
        if (fromPersist) { try { notifs.diff() } catch (e: Exception) {} }
        val ls = synchronized(listeners) { listeners.toList() }
        ls.forEach { try { it() } catch (e: Exception) {} }
    }

    /* ---------------- Carga y guardado (load / persist de la app web) ---------------- */
    private fun randCode(): String { val r = java.security.SecureRandom(); return (1..6).map { CODE_CHARS[r.nextInt(CODE_CHARS.length)] }.joinToString("") }
    fun genAccessCode(existing: List<Any?>): String { var c: String; do { c = randCode() } while (existing.any { (it as? JObj)?.get("accessCode") == c }); return c }

    fun normalizeClientAccessCodes(list: JArr): Boolean {
        val byName = HashMap<String, String>(); val used = HashSet<String>(); var changed = false
        list.forEach { a ->
            val x = a as? JObj ?: return@forEach
            val k = x.str("name").trim().lowercase(); if (k.isEmpty()) return@forEach
            if (byName[k] == null) {
                val cand = x.str("accessCode").trim()
                if (cand.isNotEmpty() && !used.contains(cand)) { byName[k] = cand; used.add(cand) }
                else { var code: String; do { code = randCode() } while (used.contains(code)); byName[k] = code; used.add(code) }
            }
            if (x["accessCode"] != byName[k]) { x["accessCode"] = byName[k]; changed = true }
        }
        return changed
    }

    private fun load(): JArr {
        return try {
            val a = Json.arr(storage.get(KEY)) ?: return JArr()
            val arr = JArr(a.filter { it is LinkedHashMap<*, *> })
            var next = 0.0; var changed = false
            arr.reversed().forEach { o -> val x = o as JObj
                val n = Js.num(x["orderNo"])
                if (n == 0.0 || n.isNaN()) { next += 1; x["orderNo"] = next; changed = true } else next = max(next, n)
                if (!Js.truthy(x["accessCode"])) { x["accessCode"] = genAccessCode(arr); changed = true }
            }
            if (normalizeClientAccessCodes(arr)) changed = true
            if (changed) storage.set(KEY, Json.stringify(arr))
            arr
        } catch (e: Exception) { JArr() }
    }

    /** persist(): guarda los encargos y avisa a la sincronización. */
    fun persist(): Boolean {
        return try {
            storage.set(KEY, Json.stringify(data))
            if (!applyingRemote) onPersist?.invoke()
            changed(); true
        } catch (e: Exception) { false }
    }
    /** Guarda otra clave del negocio (notas, gastos, ajustes) y avisa a la sincronización. */
    fun putBusiness(key: String, v: Any?): Boolean { val ok = ls.put(key, v); if (ok) { if (!applyingRemote) onPersist?.invoke(); changed() }; return ok }

    /* ---------------- Tarifas de envío ---------------- */
    fun loadShippingConfig() {
        val rates = JObj(); DEFAULT_SHIPPING_RATES.forEach { (k, v) -> rates[k] = v }
        val names = JObj(); DEFAULT_SHIPPING_NAMES.forEach { (k, v) -> names[k] = v }
        try {
            val legacy = ls.json("encargos_shipping_rates_v1") as? JObj
            if (legacy != null) {
                Js.num(legacy["avion"]).let { if (it.isFinite() && legacy["avion"] != null) rates["avion"] = it }
                Js.num(legacy["barco"]).let { if (it.isFinite() && legacy["barco"] != null) rates["barco"] = it }
            }
            val raw = ls.json(SHIPPING_KEY) as? JObj
            (raw?.get("rates") as? JObj)?.forEach { (k, v) -> val n = Js.num(v); if (n.isFinite() && n >= 0) rates[k] = n }
            (raw?.get("names") as? JObj)?.forEach { (k, v) -> val s = Js.str(v).trim(); if (s.isNotEmpty()) names[k] = s }
        } catch (e: Exception) {}
        shippingRates = rates; shippingNames = names
    }
    fun saveShippingRates() = putBusiness(SHIPPING_KEY, jobj("rates" to shippingRates, "names" to shippingNames))
    fun shippingLabel(type: String?): String { val t = if (type.isNullOrEmpty()) "avion" else type; return (shippingNames[t] as? String) ?: ("📦 " + t.replace(Regex("[-_]+"), " ")) }
    fun shippingName(type: String?): String = shippingLabel(type).replace(Regex("^\\S+\\s"), "")
    fun rateOf(type: String?): Double { val t = if (type.isNullOrEmpty()) "avion" else type; return shippingRates[t]?.let { Js.num(it) } ?: DEFAULT_SHIPPING_RATES[t] ?: 0.0 }
    fun addShippingRate(name: String, price: Double): String? {
        if (name.isBlank() || !price.isFinite() || price < 0) return null
        var key = java.text.Normalizer.normalize(name.lowercase(), java.text.Normalizer.Form.NFD).replace(Regex("[\\u0300-\\u036f]"), "")
            .replace(Regex("[^a-z0-9]+"), "_").trim('_').take(28).ifEmpty { "envio" }
        if (shippingRates.containsKey(key)) key = key + "_" + System.currentTimeMillis().toString().takeLast(5)
        shippingRates[key] = price; shippingNames[key] = "📦 " + name.trim(); saveShippingRates(); return key
    }
    fun removeShippingRate(key: String) { if (key == "avion" || key == "barco") return; shippingRates.remove(key); shippingNames.remove(key); saveShippingRates() }
    fun setShippingRates(values: Map<String, Double>): Boolean {
        if (values.values.any { !it.isFinite() || it < 0 }) return false
        val r = JObj(); shippingRates.keys.forEach { k -> r[k] = values[k] ?: Js.num(shippingRates[k]) }; shippingRates = r; saveShippingRates(); return true
    }

    /* ---------------- Ajustes simples ---------------- */
    var phoneCC: String
        get() = (ls.get("encargos_phone_cc_v1", "53") ?: "53").replace(Regex("\\D"), "").ifEmpty { "53" }
        set(v) { ls.set("encargos_phone_cc_v1", v.replace(Regex("\\D"), "")); changed(false) }
    var businessName: String
        get() = ls.get("encargos_business_name", null)?.ifEmpty { null } ?: "Encargos"
        set(v) { ls.set("encargos_business_name", v.trim().ifEmpty { "Encargos" }); if (!applyingRemote) onPersist?.invoke(); changed(false) }
    var greetName: String
        get() = ls.get("encargos_greet_name", null)?.ifEmpty { null } ?: "Yaniel"
        set(v) { ls.set("encargos_greet_name", v.trim()); changed(false) }
    var transitWarn: Int
        get() = max(1, (ls.get("encargos_transit_warn", "7") ?: "7").toDoubleOrNull()?.toInt() ?: 7)
        set(v) { ls.set("encargos_transit_warn", v.toString()) }
    var transitLate: Int
        get() = max(2, (ls.get("encargos_transit_late", "15") ?: "15").toDoubleOrNull()?.toInt() ?: 15)
        set(v) { ls.set("encargos_transit_late", v.toString()) }
    fun saveTransitAlerts(w: Int, l: Int) { val ww = max(1, w); transitWarn = ww; transitLate = max(ww + 1, l); changed(false) }

    fun buyRates(): JObj { val r = JObj(); DEF_BUY.forEach { (k, v) -> r[k] = v }; (ls.json("encargos_buy_rates") as? JObj)?.forEach { (k, v) -> r[k] = v }; return r }
    fun saveBuyRates(values: Map<String, Double>, applyToActive: Boolean) {
        val r = JObj(); values.forEach { (k, v) -> r[k] = v }
        putBusiness("encargos_buy_rates", r)
        if (applyToActive) {
            val done = completedClients().map { it.name.trim().lowercase() }.toSet()
            orders().forEach { x -> if (!done.contains(x.str("name").trim().lowercase())) { val t = typeOf(x); if (r[t] != null) x["lbBuy"] = r[t] } }
            persist()
        }
    }
    fun buyOf(x: JObj): Double = if (x["lbBuy"] != null && x["lbBuy"] != "") Js.num(x["lbBuy"]) else Js.num(buyRates()[typeOf(x)])
    fun marginOf(x: JObj) = Js.num(x["lbCost"]) - buyOf(x)
    fun profit(x: JObj): Double = if (x["status"] == "Cancelado") 0.0 else Js.num(x["pounds"]) * marginOf(x)
    fun typeOf(x: JObj): String = x.str("shippingType").ifEmpty { "avion" }

    fun templates(): JObj = ls.json("encargos_templates") as? JObj ?: JObj()
    fun tpl(k: String): String { val t = templates()[k]; return if (t != null && Js.str(t).trim().isNotEmpty()) Js.str(t) else DEF_TPL[k] ?: "" }
    fun saveTemplates(arrival: String, reminder: String, thanks: String) = putBusiness("encargos_templates", jobj("arrival" to arrival, "reminder" to reminder, "thanks" to thanks))
    fun resetTemplates() = putBusiness("encargos_templates", JObj())

    /* ---------------- Encargos ---------------- */
    @Suppress("UNCHECKED_CAST")
    fun orders(): List<JObj> = data.objs()
    fun byId(id: Any?): JObj? { val s = Js.str(id); return orders().firstOrNull { Js.str(it["id"]) == s } }
    fun idOf(x: JObj): Long = Js.num(x["id"]).toLong()
    fun orderLabel(x: JObj): String { val n = Js.num(x["orderNo"]); val v = if (n.isFinite() && n > 0) n.toLong() else max(1, data.indexOf(x) + 1).toLong(); return "ENC-" + v.toString().padStart(4, '0') }

    private fun r2(v: Any?) = Js.r2(v)
    /** recalcOrder de la app (redondeo a centavos). */
    fun recalcOrder(x: JObj): Boolean {
        val before = Json.stringify(jarr(x["name"], x["phone"], x["paid"], x["shipping"], x["total"], x["balance"]))
        x["name"] = (if (x["name"] == null) "" else Js.str(x["name"])).trim().ifEmpty { "Sin nombre" }
        x["phone"] = if (x["phone"] == null) "" else Js.str(x["phone"])
        x["paid"] = r2(x["paid"])
        val sh = x["shipping"]
        x["shipping"] = if (sh == null || sh == "") r2(Js.num(x["lbCost"]) * Js.num(x["pounds"])) else r2(sh)
        x["total"] = r2(Js.num(x["cost"]) + Js.num(x["shipping"]))
        x["balance"] = r2(Js.num(x["total"]) - Js.num(x["paid"]))
        return before != Json.stringify(jarr(x["name"], x["phone"], x["paid"], x["shipping"], x["total"], x["balance"]))
    }
    fun recalcAll(): Boolean { var ch = false; orders().forEach { if (recalcOrder(it)) ch = true }; return ch }
    /** renderAll de la app recalcula y guarda si algo cambió. */
    fun refresh() { if (recalcAll()) { try { storage.set(KEY, Json.stringify(data)) } catch (e: Exception) {} }; changed() }

    fun stamp(x: JObj, st: String) { val sd = x["statusDates"] as? JObj ?: JObj().also { x["statusDates"] = it }; if (sd[st] == null) sd[st] = Js.nowIso() }

    data class NewOrder(val name: String, val phone: String, val cost: Double, val lbCost: Double, val pounds: Double, val fund: Double,
                        val fundMethod: String = "Efectivo", val status: String = "Pedido", val shippingType: String = "avion", val photo: String = "")

    /** save() de la app: crea un encargo. Devuelve el encargo o un mensaje de error. */
    fun createOrder(o: NewOrder): Result<JObj> {
        val name = o.name.trim(); if (name.isEmpty()) return Result.failure(Exception("Escribe el nombre del cliente."))
        if (listOf(o.cost, o.lbCost, o.pounds, o.fund).any { !it.isFinite() || it < 0 }) return Result.failure(Exception("Los valores no pueden ser negativos."))
        val now = Js.nowIso(); val nowMs = System.currentTimeMillis()
        var id = (nowMs + (Math.random() * 999).toLong()).toDouble(); while (byId(id) != null) id += 1
        val maxNo = orders().fold(0.0) { m, a -> max(m, Js.num(a["orderNo"]).let { if (it.isNaN()) 0.0 else it }) }
        val payment = if (o.fund != 0.0) jarr(jobj("id" to nowMs.toDouble(), "date" to now, "amount" to o.fund, "method" to o.fundMethod, "note" to "Pago inicial")) else JArr()
        val existing = orders().firstOrNull { it.str("name").trim().lowercase() == name.lowercase() }
        val code = existing?.get("accessCode")?.let { Js.str(it) }?.ifEmpty { null } ?: genAccessCode(data)
        val ship = o.lbCost * o.pounds
        val x = jobj("id" to id, "orderNo" to maxNo + 1, "accessCode" to code, "name" to name, "phone" to o.phone.trim(), "cost" to o.cost, "lbCost" to o.lbCost,
            "pounds" to o.pounds, "shippingType" to o.shippingType, "shipping" to ship, "total" to o.cost + ship, "paid" to o.fund, "balance" to o.cost + ship - o.fund,
            "status" to o.status, "date" to now, "updated" to now, "paymentHistory" to payment,
            "lbHistory" to (if (o.pounds != 0.0) jarr(jobj("date" to now, "from" to 0.0, "to" to o.pounds, "change" to o.pounds)) else JArr()), "photo" to o.photo)
        // capas de la app web: statusDates, costo por libra fijado y actividad
        stamp(x, o.status.ifEmpty { "Pedido" })
        x["lbBuy"] = Js.num(buyRates()[o.shippingType.ifEmpty { "avion" }])
        data.add(0, x)
        if (!persist()) { data.removeAt(0); return Result.failure(Exception("No se pudo guardar.")) }
        attention.log("created", "Encargo creado · $name")
        if (o.fund > 0) attention.log("payment", "Pago registrado · $name", o.fund)
        return Result.success(x)
    }

    /** saveEdit() de la app: estado, libras añadidas y un pago. */
    fun saveEdit(id: Any?, addLb: Double, pay: Double, method: String, payNote: String, status: String): Result<JObj> {
        val x = byId(id) ?: return Result.failure(Exception("No existe el encargo"))
        if (!addLb.isFinite() || !pay.isFinite() || addLb < 0 || pay < 0) return Result.failure(Exception("Los valores no pueden ser negativos."))
        val snapshot = Json.deepCopy(x) as JObj
        val prevSt = Js.str(x["status"]); val prevPaid = Js.num(x["paid"])
        val now = Js.nowIso(); val old = Js.num(x["pounds"]); val newP = old + addLb
        if (addLb != 0.0) { x.arr("lbHistory").add(jobj("date" to now, "from" to old, "to" to newP, "change" to addLb)); x["pounds"] = newP }
        if (pay != 0.0) { x.arr("paymentHistory").add(jobj("id" to System.currentTimeMillis().toDouble(), "date" to now, "amount" to pay, "method" to method, "note" to payNote.trim())); x["paid"] = Js.num(x["paid"]) + pay }
        x["status"] = status
        x["shipping"] = Js.num(x["lbCost"]) * Js.num(x["pounds"]); x["total"] = Js.num(x["cost"]) + Js.num(x["shipping"]); x["balance"] = Js.num(x["total"]) - Js.num(x["paid"]); x["updated"] = now
        if (status != prevSt) stamp(x, status)
        if (!persist()) { x.clear(); x.putAll(snapshot); return Result.failure(Exception("No se pudo guardar.")) }
        val nm = x.str("name")
        if (status != prevSt) attention.log(if (status == "Entregado") "delivered" else "status", if (status == "Entregado") "Pedido entregado · $nm" else "Estado actualizado · $nm: $status")
        val d = Js.num(x["paid"]) - prevPaid; if (d > 0.005) attention.log("payment", "Pago registrado · $nm", d)
        refresh(); return Result.success(x)
    }

    fun nextStatus(s: String): String? { val i = FLOW.indexOf(s); return if (i >= 0 && i < FLOW.size - 1) FLOW[i + 1] else null }
    /** advance(): pasa al siguiente estado. Devuelve el estado anterior (para deshacer). */
    fun advance(id: Any?): String? {
        val x = byId(id) ?: return null
        val prev = x.str("status").ifEmpty { "Pedido" }; val nx = nextStatus(prev) ?: return null
        x["status"] = nx; x["updated"] = Js.nowIso(); stamp(x, nx)
        if (!persist()) { x["status"] = prev; return null }
        attention.log(if (nx == "Entregado") "delivered" else "status", if (nx == "Entregado") "Pedido entregado · ${x.str("name")}" else "Estado actualizado · ${x.str("name")}: $nx")
        return prev
    }
    fun setStatus(id: Any?, st: String) { val x = byId(id) ?: return; x["status"] = st; persist() }
    fun deliver(id: Any?): Boolean {
        val x = byId(id) ?: return false; val prev = x["status"]
        x["status"] = "Entregado"; stamp(x, "Entregado"); x["updated"] = Js.nowIso()
        if (!persist()) { x["status"] = prev; return false }
        attention.log("delivered", "Pedido entregado · ${x.str("name")}"); return true
    }
    fun setDate(id: Any?, iso: String): Boolean { val x = byId(id) ?: return false; if (Js.parseDate(iso).isNaN()) return false; x["date"] = iso; x["updated"] = Js.nowIso(); return persist() }
    fun setNote(id: Any?, note: String): Boolean { val x = byId(id) ?: return false; x["note"] = note.trim(); x["updated"] = Js.nowIso(); return persist() }
    fun addOrderPhoto(id: Any?, dataUrl: String): Boolean { val x = byId(id) ?: return false; x.arr("photos").add(dataUrl); x["updated"] = Js.nowIso(); return persist() }
    fun deleteOrderPhoto(id: Any?, idx: Int): Boolean { val x = byId(id) ?: return false; val p = x["photos"] as? JArr ?: return false; if (idx !in p.indices) return false; p.removeAt(idx); return persist() }
    fun orderPhotos(x: JObj): List<String> { val l = ArrayList<String>(); x.str("photo").let { if (it.isNotEmpty()) l.add(it) }; (x["photos"] as? JArr)?.forEach { if (it is String && it.isNotEmpty()) l.add(it) }; return l }

    data class Removed(val item: JObj, val index: Int)
    fun removeOrder(id: Any?): Removed? {
        val idx = data.indexOfFirst { Js.str((it as? JObj)?.get("id")) == Js.str(id) }; if (idx < 0) return null
        val item = data.removeAt(idx) as JObj
        if (!persist()) { data.add(idx, item); return null }
        return Removed(item, idx)
    }
    fun undoRemove(r: Removed): Boolean {
        if (byId(r.item["id"]) != null) return false
        data.add(r.index.coerceIn(0, data.size), r.item); return persist()
    }

    /* ---------------- Pagos ---------------- */
    fun payments(x: JObj): JArr = x["paymentHistory"] as? JArr ?: JArr().also { x["paymentHistory"] = it }
    /** paySave(): edita un pago y mantiene lo pagado coherente. */
    fun payEdit(orderId: Any?, idx: Int, amount: Double, method: String, note: String): Result<Unit> {
        val x = byId(orderId) ?: return Result.failure(Exception("No existe"))
        val p = payments(x).getOrNull(idx) as? JObj ?: return Result.failure(Exception("No existe el pago"))
        if (!(amount > 0)) return Result.failure(Exception("La cantidad debe ser mayor que 0. Para quitarlo usa \"Eliminar pago\"."))
        val snap = Json.deepCopy(x) as JObj
        val before = Js.num(x["paid"]) - payments(x).sumOf { Js.num((it as? JObj)?.get("amount")) }
        p["amount"] = amount; p["method"] = method.ifEmpty { p.str("method").ifEmpty { "Efectivo" } }; p["note"] = note.trim(); p["edited"] = Js.nowIso()
        x["paid"] = payments(x).sumOf { Js.num((it as? JObj)?.get("amount")) }; x["balance"] = Js.num(x["total"]) - Js.num(x["paid"]); x["updated"] = Js.nowIso()
        x["paid"] = Js.num(x["paid"]) + before; x["balance"] = Js.num(x["total"]) - Js.num(x["paid"])
        if (!persist()) { x.clear(); x.putAll(snap); return Result.failure(Exception("No se pudo guardar.")) }
        refresh(); return Result.success(Unit)
    }
    fun payDelete(orderId: Any?, idx: Int): JObj? {
        val x = byId(orderId) ?: return null
        val p = payments(x).getOrNull(idx) as? JObj ?: return null
        val snap = Json.deepCopy(x) as JObj
        payments(x).removeAt(idx); x["paid"] = Js.num(x["paid"]) - Js.num(p["amount"]); x["balance"] = Js.num(x["total"]) - Js.num(x["paid"]); x["updated"] = Js.nowIso()
        if (!persist()) { x.clear(); x.putAll(snap); return null }
        refresh(); return snap
    }
    fun restoreOrder(snapshot: JObj) { val x = byId(snapshot["id"]) ?: return; x.clear(); x.putAll(Json.deepCopy(snapshot) as JObj); persist(); refresh() }

    /* ---------------- Clientes ---------------- */
    class Client(val name: String, val phone: String, val orders: MutableList<JObj>, var paid: Double = 0.0, var pending: Double = 0.0, var funds: Double = 0.0, var pounds: Double = 0.0) {
        val key get() = name.trim().lowercase()
    }
    /* Vista calculada una sola vez por versión de los datos: las pantallas, el panel, las estadísticas y el centro de atención
     * piden estas listas muchas veces; se recalculan solo cuando cambia [version] (es decir, tras guardar un cambio).
     * Los resultados son idénticos a calcularlas cada vez. */
    private var cacheVer = -1L
    private var cClients: List<Client> = emptyList(); private var cActive: List<Client> = emptyList(); private var cDone: List<Client> = emptyList()
    private var cActiveOrders: List<JObj> = emptyList(); private var cByKey: Map<String, Client> = emptyMap()
    @Synchronized private fun view() {
        if (cacheVer == version) return
        val all = computeClients(); val done = all.filter { isCompletedClient(it) }; val doneKeys = done.map { it.key }.toSet()
        cClients = all; cDone = done; cActive = all.filter { !isCompletedClient(it) }
        cActiveOrders = orders().filter { !doneKeys.contains(it.str("name").trim().lowercase()) }
        cByKey = all.associateBy { it.name.lowercase() }
        cacheVer = version
    }
    fun clients(): List<Client> { view(); return cClients }
    private fun computeClients(): List<Client> {
        val map = LinkedHashMap<String, Client>()
        orders().forEach { x ->
            val k = x.str("name").trim().lowercase()
            val c = map.getOrPut(k) { Client(x.str("name"), x.str("phone"), ArrayList()) }
            c.orders.add(x); c.paid += Js.num(x["paid"]); c.pounds += Js.num(x["pounds"])
            val b = Js.num(x["balance"]); if (b > 0) c.pending += b; if (b < 0) c.funds += -b
        }
        return map.values.sortedWith { a, b -> collator.compare(a.name, b.name) }
    }
    /** Versión vigente de la app: cliente completado = todos sus encargos cancelados o pagados (total > 0 y saldo 0). */
    private fun settled(o: JObj) = o["status"] == "Cancelado" || (Js.num(o["total"]) > 0 && abs(Js.num(o["balance"])) < 0.005)
    fun isCompletedClient(c: Client?) = c != null && c.orders.isNotEmpty() && c.orders.all { settled(it) }
    fun activeClients(): List<Client> { view(); return cActive }
    fun completedClients(): List<Client> { view(); return cDone }
    fun activeOrders(): List<JObj> { view(); return cActiveOrders }
    fun findClient(name: String): Client? { view(); return cByKey[name.lowercase()] ?: cClients.firstOrNull { it.name.lowercase() == name.lowercase() } }

    fun clientInfo(name: String): JObj = (ls.json("encargos_client_info") as? JObj)?.get(name.trim().lowercase()) as? JObj ?: JObj()
    fun saveClientInfo(name: String, address: String, note: String): Boolean {
        val all = ls.json("encargos_client_info") as? JObj ?: JObj()
        all[name.trim().lowercase()] = jobj("address" to address.trim(), "note" to note.trim())
        val ok = putBusiness("encargos_client_info", all)
        if (ok) notifs.add(jobj("cat" to "client", "type" to "client_edit", "title" to "Cliente actualizado", "text" to "Se actualizaron los datos de $name.", "short" to name, "ref" to jobj("client" to name)))
        return ok
    }

    /* ---------------- WhatsApp ---------------- */
    fun waNumber(phone: String?): String { var p = (phone ?: "").replace(Regex("\\D"), ""); if (p.isNotEmpty() && p.length <= 8) p = phoneCC + p; return p }
    fun waLink(phone: String?, msg: String?): String? { val p = waNumber(phone); if (p.isEmpty()) return null; return "https://wa.me/$p" + (if (!msg.isNullOrEmpty()) "?text=" + java.net.URLEncoder.encode(msg, "UTF-8").replace("+", "%20") else "") }
    fun fill(t: String, v: Map<String, String>) = Regex("\\{(nombre|saldo|total|pagado|libras|negocio)\\}").replace(t) { m -> v[m.groupValues[1]] ?: m.value }
    fun ctxClient(c: Client): Map<String, String> = mapOf("nombre" to c.name, "saldo" to Js.money(c.pending), "total" to Js.money(c.orders.sumOf { Js.num(it["total"]) }), "pagado" to Js.money(c.paid), "libras" to Js.toFixed(c.pounds, 2) + " lb", "negocio" to businessName)
    fun ctxOrder(x: JObj): Map<String, String> = mapOf("nombre" to x.str("name"), "saldo" to Js.money(max(0.0, Js.num(x["balance"]))), "total" to Js.money(x["total"]), "pagado" to Js.money(x["paid"]), "libras" to Js.toFixed(Js.num(x["pounds"]), 2) + " lb", "negocio" to businessName)
    fun arrivalMessage(c: Client) = fill(tpl("arrival"), ctxClient(c))
    fun reminderClient(c: Client) = fill(tpl("reminder"), ctxClient(c))
    fun reminderOrder(x: JObj) = fill(tpl("reminder"), ctxOrder(x))
    fun thanksOrder(x: JObj) = fill(tpl("thanks"), ctxOrder(x))
    fun thanksClient(c: Client) = fill(tpl("thanks"), ctxClient(c))
    fun receiptCaption(x: JObj): String { val b = Js.num(x["balance"]); return "Hola ${x.str("name")}, aquí tienes el recibo de tu compra.\nTotal: ${Js.money(x["total"])} · Pagado: ${Js.money(x["paid"])}" + (if (b > 0.005) " · Pendiente: ${Js.money(b)}" else " · ✅ Pagado completo") }
    fun notified(who: String) = attention.log("notified", "Cliente notificado · $who")

    /* ---------------- Notas y gastos ---------------- */
    fun notes(): List<JObj> = ls.arr("encargos_notes").objs()
    fun notesSorted(): List<JObj> = notes().sortedWith(compareByDescending<JObj> { if (Js.truthy(it["pinned"])) 1 else 0 }.thenByDescending { Js.num(it["updated"]) })
    fun saveNote(id: String?, title: String, body: String, amount: Double, color: String, pinned: Boolean, photos: List<String>): Result<JObj> {
        if (title.isBlank() && body.isBlank() && photos.isEmpty()) return Result.failure(Exception("Escribe un título, el contenido o agrega una foto."))
        val all = ls.arr("encargos_notes"); val now = System.currentTimeMillis().toDouble()
        val n: JObj
        if (!id.isNullOrEmpty()) {
            n = all.firstOrNull { (it as? JObj)?.get("id") == id } as? JObj ?: return Result.failure(Exception("No existe la nota"))
            n["title"] = title.trim(); n["body"] = body.trim(); n["color"] = color; n["pinned"] = pinned; n["updated"] = now; n["photos"] = JArr(photos)
            if (amount > 0) n["amount"] = amount else n.remove("amount")
        } else {
            n = jobj("id" to "n" + java.lang.Long.toString(now.toLong(), 36) + java.lang.Long.toString((Math.random() * 1.6e6).toLong(), 36).take(4), "title" to title.trim(), "body" to body.trim(), "color" to color, "pinned" to pinned, "created" to now, "updated" to now, "photos" to JArr(photos))
            if (amount > 0) n["amount"] = max(0.0, amount)
            all.add(n)
        }
        return if (putBusiness("encargos_notes", all)) Result.success(n) else Result.failure(Exception("No se pudo guardar la nota (memoria llena)."))
    }
    fun deleteNote(id: String) = putBusiness("encargos_notes", JArr(ls.arr("encargos_notes").filter { (it as? JObj)?.get("id") != id }))

    /* ---------------- Artículos en venta ----------------
     * Se guardan junto a las notas (kind = "sale"): así entran en los respaldos y viajan cifrados por Telegram con sus fotos,
     * sin cambiar el formato de sincronización. Las apps que no conocen los artículos los ven como una nota normal (sin monto). */
    data class SaleSummary(val items: Int, val available: Int, val units: Int, val value: Double, val invested: Double, val potential: Double,
                           val soldUnits: Int, val income: Double, val realized: Double)
    fun isSale(n: JObj) = n.str("kind") == "sale"
    fun saleItems(): List<JObj> = notes().filter { isSale(it) }.sortedByDescending { Js.num(it["updated"]) }
    fun saleSales(n: JObj): List<JObj> = (n["sales"] as? JArr)?.objs() ?: emptyList()
    /** Cálculos de todos los artículos: lo que vale lo que tienes, lo invertido, lo que ganarías y lo ya vendido. */
    fun saleSummary(list: List<JObj> = saleItems()): SaleSummary {
        var units = 0; var value = 0.0; var invested = 0.0; var soldU = 0; var income = 0.0; var realized = 0.0
        list.forEach { n -> val st = Js.num(n["stock"]).toInt().coerceAtLeast(0); val pr = Js.num(n["price"]); val co = Js.num(n["cost"])
            units += st; value += st * pr; invested += st * co
            saleSales(n).forEach { v -> val q = Js.num(v["qty"]).toInt(); soldU += q; income += q * Js.num(v["price"]); realized += q * (Js.num(v["price"]) - Js.num(v["cost"])) } }
        return SaleSummary(list.size, list.count { Js.num(it["stock"]) > 0 }, units, value, invested, value - invested, soldU, income, realized)
    }
    private fun saleBody(name: String, desc: String, price: Double, stock: Int): String =
        "Artículo en venta · ${Js.money(price)} · ${if (stock == 1) "1 disponible" else "$stock disponibles"}" + if (desc.isNotBlank()) "\n" + desc.trim() else ""
    fun saveSaleItem(id: String?, name: String, desc: String, price: Double, cost: Double, stock: Int, photos: List<String>): Result<JObj> {
        if (name.isBlank()) return Result.failure(Exception("Escribe el nombre del artículo."))
        if (price.isNaN() || price <= 0) return Result.failure(Exception("Escribe el precio de venta."))
        if (cost.isNaN() || cost < 0) return Result.failure(Exception("Revisa lo que te costó."))
        val st = stock.coerceAtLeast(0); val all = ls.arr("encargos_notes"); val now = System.currentTimeMillis().toDouble()
        val n: JObj
        if (!id.isNullOrEmpty()) {
            n = all.firstOrNull { (it as? JObj)?.get("id") == id } as? JObj ?: return Result.failure(Exception("No existe el artículo"))
        } else {
            n = jobj("id" to "s" + java.lang.Long.toString(now.toLong(), 36) + java.lang.Long.toString((Math.random() * 1.6e6).toLong(), 36).take(4), "kind" to "sale", "created" to now, "sales" to JArr(), "color" to "green", "pinned" to false)
            all.add(n)
        }
        n["title"] = name.trim(); n["desc"] = desc.trim(); n["price"] = Js.toFixed2(price); n["cost"] = Js.toFixed2(cost); n["stock"] = st.toDouble()
        n["photos"] = JArr(photos); n["updated"] = now; n["body"] = saleBody(name, desc, price, st); n.remove("amount")
        return if (putBusiness("encargos_notes", all)) Result.success(n) else Result.failure(Exception("No se pudo guardar el artículo (memoria llena)."))
    }
    /** Registra una venta: baja las unidades disponibles y guarda precio y costo de ese momento (para la ganancia real). */
    fun sellSaleItem(id: String, qty: Int, price: Double): Result<JObj> {
        val all = ls.arr("encargos_notes")
        val n = all.firstOrNull { (it as? JObj)?.get("id") == id } as? JObj ?: return Result.failure(Exception("No existe el artículo"))
        val st = Js.num(n["stock"]).toInt()
        if (qty <= 0) return Result.failure(Exception("La cantidad debe ser 1 o más."))
        if (qty > st) return Result.failure(Exception(if (st <= 0) "No quedan unidades de este artículo." else "Solo quedan $st."))
        if (price.isNaN() || price < 0) return Result.failure(Exception("Revisa el precio de venta."))
        val now = System.currentTimeMillis().toDouble()
        val sales = (n["sales"] as? JArr) ?: JArr().also { n["sales"] = it }
        sales.add(jobj("t" to now, "qty" to qty.toDouble(), "price" to Js.toFixed2(price), "cost" to Js.num(n["cost"])))
        n["stock"] = (st - qty).toDouble(); n["updated"] = now; n["body"] = saleBody(n.str("title"), n.str("desc"), Js.num(n["price"]), st - qty)
        return if (putBusiness("encargos_notes", all)) Result.success(n) else Result.failure(Exception("No se pudo guardar la venta."))
    }
    /** Deshace la última venta registrada del artículo (devuelve las unidades). */
    fun undoLastSale(id: String): Boolean {
        val all = ls.arr("encargos_notes")
        val n = all.firstOrNull { (it as? JObj)?.get("id") == id } as? JObj ?: return false
        val sales = n["sales"] as? JArr ?: return false; if (sales.isEmpty()) return false
        val last = sales.removeAt(sales.size - 1) as? JObj ?: return false
        val st = Js.num(n["stock"]).toInt() + Js.num(last["qty"]).toInt()
        n["stock"] = st.toDouble(); n["updated"] = System.currentTimeMillis().toDouble(); n["body"] = saleBody(n.str("title"), n.str("desc"), Js.num(n["price"]), st)
        return putBusiness("encargos_notes", all)
    }

    fun expenses(): List<JObj> = ls.arr("encargos_expenses").objs()
    fun saveExpense(id: String?, concept: String, amount: Double, dateYmd: String, cat: String): Result<Unit> {
        if (concept.isBlank()) return Result.failure(Exception("Escribe el concepto del gasto."))
        if (!(amount > 0)) return Result.failure(Exception("Escribe un monto mayor que 0."))
        val all = ls.arr("encargos_expenses")
        // new Date(d+'T12:00:00') es hora local del dispositivo
        val iso = try { Js.isoOf(java.time.LocalDateTime.parse(dateYmd + "T12:00:00").atZone(java.time.ZoneId.systemDefault()).toInstant().toEpochMilli()) } catch (e: Exception) { Js.nowIso() }
        if (!id.isNullOrEmpty()) { val e = all.firstOrNull { (it as? JObj)?.get("id") == id } as? JObj; if (e != null) { e["concept"] = concept.trim(); e["amount"] = amount; e["date"] = iso; e["cat"] = cat } }
        else all.add(jobj("id" to "g" + java.lang.Long.toString(System.currentTimeMillis(), 36), "concept" to concept.trim(), "amount" to amount, "date" to iso, "cat" to cat))
        return if (putBusiness("encargos_expenses", all)) Result.success(Unit) else Result.failure(Exception("No hay espacio suficiente para guardar."))
    }
    fun deleteExpense(id: String) = putBusiness("encargos_expenses", JArr(ls.arr("encargos_expenses").filter { (it as? JObj)?.get("id") != id }))
}
