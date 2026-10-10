package com.agencia.encargos.core

import java.io.IOException
import java.util.concurrent.Executors
import java.util.concurrent.ScheduledFuture
import java.util.concurrent.TimeUnit
import kotlin.math.max

/**
 * Sincronización por Telegram, compatible byte a byte con la app web (respaldo único ENCARGOS_BACKUP_LATEST).
 * Es una traducción directa del motor JavaScript: mismas claves locales, mismos formatos, mismo orden de pasos.
 *
 * @param onApp ejecuta un bloque en el hilo de la interfaz y espera (los datos solo se tocan desde ahí).
 */
class TelegramSync(
    private val app: Encargos,
    private val secrets: Secrets,
    private val apiBase: String = "https://api.telegram.org",
    private val onApp: (() -> Unit) -> Unit = { it() },
) {
    companion object {
        const val SCHEMA = 1
        val ENT = listOf("order", "payment", "order_media", "client", "note", "expense", "setting")
        const val LATEST = "ENCARGOS_BACKUP_LATEST"
        const val BK_NAME = "encargos-latest.enc"
        const val MAX_BYTES = 19.5 * 1048576
        const val REFRESH_MS = 40 * 3600000L
        val BACKOFF = longArrayOf(5000, 15000, 30000, 60000, 300000)
        const val RESET_MSG = "La sincronización se reinició en otro dispositivo. Ve a Ajustes → Sincronización → Desconectar y pega el código de enlace nuevo."
        val OMIT = setOf("id", "uid", "paid", "balance", "paymentHistory", "photo", "photos", "total", "_u")
        object K { const val cfg = "encargos_tg_cfg"; const val dev = "encargos_device_id"; const val q = "encargos_tg_queue"; const val h = "encargos_tg_hashes"; const val st = "encargos_tg_stamps"
            const val pp = "encargos_tg_processed_pkgs"; const val po = "encargos_tg_processed_ops"; const val meta = "encargos_tg_meta"; const val log = "encargos_tg_log"; const val conf = "encargos_tg_conflicts"; const val seq = "encargos_tg_seq" }
    }
    private val ls get() = app.ls
    val isTest = apiBase != "https://api.telegram.org"
    private fun minGap() = if (isTest) 0L else 15000L
    private fun <T> onUi(block: () -> T): T { var r: Result<T>? = null; onApp { r = runCatching(block) }; return r!!.getOrThrow() }

    /* ---------- configuración (en el almacén seguro de la plataforma) ---------- */
    fun cfg(): JObj? = Json.obj(secrets.get(K.cfg))
    private fun putCfg(c: JObj?) { if (c == null) secrets.remove(K.cfg) else secrets.set(K.cfg, Json.stringify(c)) }
    fun enabled(): Boolean { val c = cfg() ?: return false; return c.str("token").isNotEmpty() && c["chatId"] != null && c.str("key").isNotEmpty() && c.str("db").isNotEmpty() && !Js.truthy(c["paused"]) }
    private fun token() = cfg()?.str("token") ?: ""
    private fun chatId(): Any? = cfg()?.get("chatId")
    private fun keyBytes() = Crypto.B64U.dec(cfg()!!.str("key"))
    private fun db() = cfg()!!.str("db")
    var api: TgApi = HttpTgApi(apiBase, { token() })

    fun deviceId(): String {
        var d = ls.get(K.dev, "") ?: ""
        if (d.isEmpty() || !Regex("^(ANDROID|PC|WEB|PHONE)-").containsMatchIn(d)) { d = app.platform + "-" + Crypto.uuid(); ls.set(K.dev, d) }
        return d
    }
    private fun nextSeq(): Double { val n = ((ls.get(K.seq, "0") ?: "0").toDoubleOrNull() ?: 0.0) + 1; ls.set(K.seq, Js.numberToString(n)); return n }
    private fun meta() = ls.obj(K.meta)
    private fun putMeta(m: JObj) = ls.put(K.meta, m)
    private fun updMeta(f: (JObj) -> Unit) { val m = meta(); f(m); putMeta(m) }

    fun slog(kind: String, text: String) { val a = ls.arr(K.log); a.add(0, jobj("t" to System.currentTimeMillis().toDouble(), "dev" to app.platform, "kind" to kind, "text" to text)); ls.put(K.log, JArr(a.take(200))) }
    fun logEntries() = ls.arr(K.log).objs()
    fun conflicts() = ls.arr(K.conf).objs()

    /* ---------- identificadores deterministas para datos existentes ---------- */
    fun ensureIds() {
        var changed = false
        app.orders().forEach { x ->
            if (x["uid"] == null || x["uid"] == "") { x["uid"] = Crypto.detUuid("order|" + Js.str(x["id"]) + "|" + Js.str(x["date"]) + "|" + Js.str(x["name"])); changed = true }
            if (x["paymentHistory"] !is ArrayList<*>) { x["paymentHistory"] = JArr(); changed = true }
            val ph = x["paymentHistory"] as JArr
            ph.forEachIndexed { i, hh -> val h = hh as? JObj ?: return@forEachIndexed
                if (h["sid"] == null || h["sid"] == "") { h["sid"] = Crypto.detUuid("pay|" + Js.str(x["uid"]) + "|" + Js.str(h["id"]) + "|" + Js.str(h["date"]) + "|" + Js.numberToString(Js.num(h["amount"])) + "|" + i); changed = true } }
            val sum = ph.objs().sumOf { Js.num(it["amount"]) }
            if (kotlin.math.abs(Js.num(x["paid"]) - sum) > 0.005 && ph.objs().none { Js.truthy(it["adj"]) }) {
                ph.add(0, jobj("id" to "adj", "sid" to Crypto.detUuid("payadj|" + Js.str(x["uid"])), "adj" to true, "date" to (x["date"]?.let { Js.str(it) }?.ifEmpty { null } ?: Js.nowIso()),
                    "amount" to Js.toFixed2(Js.num(x["paid"]) - sum), "method" to "Efectivo", "note" to "Pagos anteriores")); changed = true
            }
        }
        val notes = ls.arr("encargos_notes"); var nch = false
        notes.objs().forEach { n -> if (n["uid"] == null || n["uid"] == "") { n["uid"] = Crypto.detUuid("note|" + Js.str(n["id"]) + "|" + Js.str(n["created"])); nch = true } }
        if (nch) ls.put("encargos_notes", notes)
        val ex = ls.arr("encargos_expenses"); var ech = false
        ex.objs().forEach { e -> if (e["uid"] == null || e["uid"] == "") { e["uid"] = Crypto.detUuid("exp|" + Js.str(e["id"]) + "|" + Js.str(e["date"])); ech = true } }
        if (ech) ls.put("encargos_expenses", ex)
        if (changed) { app.applyingRemote = true; try { app.storage.set(Encargos.KEY, Json.stringify(app.data)) } finally { app.applyingRemote = false } }
    }

    /** Modelo local → entidades (mismo formato que la app web). */
    fun snapshot(): LinkedHashMap<String, JObj> {
        ensureIds(); val r = LinkedHashMap<String, JObj>(); ENT.forEach { r[it] = JObj() }
        app.orders().forEach { x ->
            val p = JObj(); x.forEach { (k, v) -> if (!OMIT.contains(k)) p[k] = Json.deepCopy(v) }; p["localId"] = x["id"]; r["order"]!![Js.str(x["uid"])] = p
            (x["paymentHistory"] as JArr).objs().forEach { h -> val q = Json.deepCopy(h) as JObj; q.remove("sid"); q["order"] = x["uid"]; r["payment"]!![Js.str(h["sid"])] = q }
            val photos = x["photos"] as? JArr
            if (Js.truthy(x["photo"]) || (photos != null && photos.isNotEmpty())) r["order_media"]!![Js.str(x["uid"])] = jobj("order" to x["uid"], "photo" to (if (Js.truthy(x["photo"])) x["photo"] else null), "photos" to (photos?.let { Json.deepCopy(it) } ?: JArr()))
        }
        ls.obj("encargos_client_info").forEach { (k, v) -> val o = jobj("key" to k); (v as? JObj)?.forEach { (a, b) -> o[a] = b }; r["client"]!![Crypto.detUuid("client|$k")] = o }
        ls.arr("encargos_notes").objs().forEach { n -> r["note"]!![Js.str(n["uid"])] = n }
        ls.arr("encargos_expenses").objs().forEach { e -> r["expense"]!![Js.str(e["uid"])] = e }
        val s = linkedMapOf<String, Any?>("templates" to ls.json("encargos_templates"), "buy_rates" to ls.json("encargos_buy_rates"),
            "shipping" to jobj("rates" to app.shippingRates, "names" to app.shippingNames), "business" to ls.get("encargos_business_name", null))
        s.forEach { (k, v) -> if (v != null) r["setting"]!![Crypto.detUuid("setting|$k")] = jobj("key" to k, "value" to v) }
        return r
    }
    private fun hashOf(p: Any?) = Crypto.sha256hex(Json.stringify(p))
    private fun stampOf(o: Any?): JObj? = o as? JObj
    /** newer(a,b): timestamp y, si empatan, deviceId como desempate. */
    fun newer(a: JObj, b: JObj?): Boolean = b == null || Js.num(a["ts"]) > Js.num(b["ts"]) || (Js.num(a["ts"]) == Js.num(b["ts"]) && Js.str(a["dev"]) > Js.str(b["dev"]))

    /* ---------- cola de operaciones ---------- */
    fun queue(): JArr = ls.arr(K.q)
    private fun saveQ(q: List<Any?>) = ls.put(K.q, JArr(q))
    fun pending(): Int = queue().size
    private val lastPayloads = HashMap<String, Any?>()
    private fun opName(ent: String, prev: Any?, isNew: Boolean, deleted: Boolean, payload: Any?): String {
        if (ent == "payment") return if (deleted) "PAYMENT_VOID" else if (isNew) "PAYMENT_ADD" else "UPDATE"
        if (deleted) return "DELETE"; if (isNew) return "CREATE"
        if (ent == "order" && prev is LinkedHashMap<*, *> && prev["status"] != (payload as? JObj)?.get("status")) return "ORDER_STATUS_CHANGE"
        if (ent == "client") return "CLIENT_UPDATE"; if (ent == "setting") return "SETTINGS_UPDATE"; return "UPDATE"
    }
    fun collect(): Int {
        if (!enabled()) return 0
        val snap = snapshot(); val known = ls.obj(K.h); val stamps = ls.obj(K.st); val q = queue(); val meta = meta(); val dev = deviceId(); var n = 0; val now = System.currentTimeMillis().toDouble()
        val removed = ArrayList<String>()
        ENT.forEach { t -> val cur = snap[t]!!
            cur.forEach { (id, payload) -> val k = "$t:$id"; val h = hashOf(payload); if (known[k] == h) return@forEach
                val isNew = known[k] == null; known[k] = h; stamps[k] = jobj("ts" to now, "dev" to dev)
                q.add(jobj("opId" to Crypto.uuid(), "deviceId" to dev, "sequence" to nextSeq(), "timestamp" to now, "entity" to t, "entityId" to id, "operation" to opName(t, lastPayloads[k], isNew, false, payload), "data" to payload, "baseVersion" to Js.num(meta["localVersion"])))
                lastPayloads[k] = payload; n++ }
            known.keys.forEach { k -> if (k.startsWith("$t:") && !cur.containsKey(k.substring(t.length + 1))) removed.add(k) }
        }
        val ko = known.keys.count { it.startsWith("order:") }; val ro = removed.count { it.startsWith("order:") }
        val massive = ro > 3 && ro > ko * 0.3
        removed.forEach { k -> known.remove(k); if (massive) return@forEach; val i = k.indexOf(':'); val t = k.substring(0, i); val id = k.substring(i + 1)
            stamps[k] = jobj("ts" to now, "dev" to dev, "deleted" to true)
            q.add(jobj("opId" to Crypto.uuid(), "deviceId" to dev, "sequence" to nextSeq(), "timestamp" to now, "entity" to t, "entityId" to id, "operation" to opName(t, null, false, true, null), "data" to null, "deleted" to true, "baseVersion" to Js.num(meta["localVersion"]))); n++ }
        if (massive) slog("warn", "Importación o borrado masivo detectado: no se borrará nada en tus otros dispositivos.")
        ls.put(K.h, known); ls.put(K.st, stamps); saveQ(q); return n
    }

    /* ---------- aplicar operaciones (merge) ---------- */
    data class Applied(val n: Int, val late: List<JObj>)
    fun applyOps(ops: List<JObj>, source: String, track: Boolean = true): Applied {
        val processed = LinkedHashSet(ls.arr(K.po).map { Js.str(it) }); val stamps = ls.obj(K.st); val known = ls.obj(K.h); val conflicts = ls.arr(K.conf)
        ensureIds(); var n = 0; var pay = 0; var ord = 0
        val M = HashMap<String, JObj>(); app.orders().forEach { M[Js.str(it["uid"])] = it }
        val notes = ls.arr("encargos_notes"); val exps = ls.arr("encargos_expenses"); val ci = ls.obj("encargos_client_info")
        var nT = false; var eT = false; var cT = false
        val pendLocal = queue().objs().map { it.str("entity") + ":" + it.str("entityId") }.toSet()
        val sorted = ops.sortedWith(Comparator { a, b ->
            val c1 = Js.num(a["timestamp"]).compareTo(Js.num(b["timestamp"])); if (c1 != 0) return@Comparator c1
            val c2 = Encargos.collator.compare(Js.str(a["deviceId"]), Js.str(b["deviceId"])); if (c2 != 0) return@Comparator c2
            Js.num(a["sequence"]).compareTo(Js.num(b["sequence"])) })
        val late = ArrayList<JObj>(); val touched = HashSet<String>()
        for (op in sorted) {
            val opId = Js.str(op["opId"])
            if (processed.contains(opId)) continue
            val ent = op.str("entity"); val eid = op.str("entityId"); val k = "$ent:$eid"; val deleted = Js.truthy(op["deleted"])
            val st = jobj("ts" to Js.num(op["timestamp"]), "dev" to op["deviceId"], "deleted" to deleted)
            if (!newer(st, stampOf(stamps[k]))) {
                if (track) processed.add(opId)
                if (track && ent != "payment" && pendLocal.contains(k) && !deleted) conflicts.add(0, jobj("t" to System.currentTimeMillis().toDouble(), "entity" to ent, "id" to eid, "kept" to "local", "remoteDevice" to op["deviceId"]))
                continue
            }
            if (ent != "payment" && pendLocal.contains(k)) conflicts.add(0, jobj("t" to System.currentTimeMillis().toDouble(), "entity" to ent, "id" to eid, "kept" to "remote", "remoteDevice" to op["deviceId"]))
            val d = op["data"] as? JObj ?: JObj()
            when (ent) {
                "order" -> { val x = M[eid]
                    if (deleted) { if (x != null) { app.data.remove(x); M.remove(eid) } }
                    else { val p = Json.deepCopy(d) as JObj; p.remove("localId")
                        if (x != null) { x.keys.toList().forEach { kk -> if (!OMIT.contains(kk) && !p.containsKey(kk)) x.remove(kk) }; x.putAll(p) }
                        else { var id = Js.num(d["localId"]).let { if (it == 0.0 || it.isNaN()) System.currentTimeMillis().toDouble() else it }; while (app.orders().any { Js.num(it["id"]) == id }) id += 1
                            val nx = jobj("id" to id, "uid" to eid, "paid" to 0.0, "balance" to 0.0, "paymentHistory" to JArr()); nx.putAll(p); app.data.add(0, nx); M[eid] = nx } }
                    ord++ }
                "payment" -> { val ox = M[Js.str(d["order"])] ?: app.orders().firstOrNull { o -> (o["paymentHistory"] as? JArr)?.objs()?.any { it["sid"] == eid } == true }
                    if (ox == null) { if (!deleted) { late.add(op); continue }; }
                    else {
                        val ph = ox["paymentHistory"] as? JArr ?: JArr().also { ox["paymentHistory"] = it }
                        val i = ph.indexOfFirst { (it as? JObj)?.get("sid") == eid }
                        if (deleted) { if (i >= 0) ph.removeAt(i) } else { val h = Json.deepCopy(d) as JObj; h["sid"] = eid; h.remove("order"); if (i >= 0) ph[i] = h else ph.add(h) }
                    }
                    pay++ }
                "order_media" -> { val x = M[eid]; if (x != null) { if (deleted) { x.remove("photo"); x.remove("photos") } else { if (Js.truthy(d["photo"])) x["photo"] = d["photo"] else x.remove("photo"); x["photos"] = (d["photos"] as? JArr)?.let { Json.deepCopy(it) } ?: JArr() } } }
                "client" -> { if (d["key"] != null || deleted) { val key = d["key"]?.let { Js.str(it) } ?: ci.keys.firstOrNull { Crypto.detUuid("client|$it") == eid }
                    if (key != null) { if (deleted) ci.remove(key) else { val v = Json.deepCopy(d) as JObj; v.remove("key"); ci[key] = v }; cT = true } } }
                "note" -> { val i = notes.indexOfFirst { (it as? JObj)?.get("uid") == eid }
                    if (deleted) { if (i >= 0) notes.removeAt(i) } else { val v = Json.deepCopy(d) as JObj; v["uid"] = eid; if (i >= 0) notes[i] = v else notes.add(v) }; nT = true }
                "expense" -> { val i = exps.indexOfFirst { (it as? JObj)?.get("uid") == eid }
                    if (deleted) { if (i >= 0) exps.removeAt(i) } else { val v = Json.deepCopy(d) as JObj; v["uid"] = eid; if (i >= 0) exps[i] = v else exps.add(v) }; eT = true }
                "setting" -> { if (!deleted && op["data"] != null) when (d.str("key")) {
                    "templates" -> ls.put("encargos_templates", d["value"])
                    "buy_rates" -> ls.put("encargos_buy_rates", d["value"])
                    "business" -> ls.set("encargos_business_name", Js.str(d["value"]))
                    "shipping" -> { val v = d["value"] as? JObj; if (v != null) { (v["rates"] as? JObj)?.forEach { (a, b) -> app.shippingRates[a] = b }; (v["names"] as? JObj)?.forEach { (a, b) -> app.shippingNames[a] = b }
                        ls.put(Encargos.SHIPPING_KEY, jobj("rates" to app.shippingRates, "names" to app.shippingNames)) } }
                } }
            }
            stamps[k] = st; if (track) processed.add(opId); n++; touched.add(k)
        }
        // lo pagado se deriva siempre de los pagos
        app.orders().forEach { x -> val ph = x["paymentHistory"] as? JArr ?: return@forEach
            val sortedPh = ph.objs().sortedWith(compareBy { Js.parseDate(it["date"]).let { v -> if (v.isNaN()) 0.0 else v } })
            ph.clear(); ph.addAll(sortedPh); x["paid"] = Js.toFixed2(sortedPh.sumOf { Js.num(it["amount"]) }) }
        if (nT) ls.put("encargos_notes", notes); if (eT) ls.put("encargos_expenses", exps); if (cT) ls.put("encargos_client_info", ci)
        if (n > 0) {
            app.applyingRemote = true
            try {
                app.recalcAll(); app.storage.set(Encargos.KEY, Json.stringify(app.data))
                val snap = snapshot()
                touched.forEach { kk -> if (pendLocal.contains(kk)) return@forEach; val i = kk.indexOf(':'); val t = kk.substring(0, i); val id = kk.substring(i + 1)
                    val p = snap[t]?.get(id); if (p != null) known[kk] = hashOf(p) else known.remove(kk) }
                if (touched.any { it.startsWith("payment:") }) app.orders().forEach { x -> val kk = "order:" + Js.str(x["uid"]); if (known[kk] != null && !pendLocal.contains(kk)) snap["order"]!![Js.str(x["uid"])]?.let { known[kk] = hashOf(it) } }
                ls.put(K.h, known)
            } finally { app.applyingRemote = false }
            app.changed()
        }
        ls.put(K.st, stamps); ls.put(K.po, JArr(processed.toList().takeLast(20000))); ls.put(K.conf, JArr(conflicts.take(100)))
        if (n > 0) slog("in", "Recibido: " + listOfNotNull(if (pay > 0) "$pay pago" + (if (pay == 1) "" else "s") else null, if (ord > 0) "$ord encargo" + (if (ord == 1) "" else "s") else null,
            if (n - pay - ord > 0) "${n - pay - ord} dato" + (if (n - pay - ord == 1) "" else "s") + " más" else null).joinToString(", ") + " ($source)")
        return Applied(n, late)
    }

    /* ---------- Telegram ---------- */
    private fun tg(method: String, params: JObj) = api.call(method, params)
    private fun sendFile(bytes: ByteArray, name: String, caption: String): Pair<Double, String> {
        val m = api.sendFile("sendDocument", mapOf("chat_id" to Js.str(chatId()), "caption" to caption, "disable_notification" to "true"), "document", name, bytes, "application/octet-stream") as JObj
        return Js.num(m["message_id"]) to ((m["document"] as JObj).str("file_id"))
    }
    private fun fetchFile(fileId: String): ByteArray { val f = tg("getFile", jobj("file_id" to fileId)) as JObj; return api.download(f.str("file_path")) }
    private fun sha(b: ByteArray) = Crypto.shaHex(b)
    private fun seal(o: Any?) = Crypto.seal(o, keyBytes(), db())
    private fun unseal(b: ByteArray) = Crypto.unseal(b, keyBytes(), db())

    /* ---------- coordinador: mensaje fijado ---------- */
    private fun dbTag() = Crypto.dbTag(db())
    private fun isLatestPin(pm: JObj?) = pm != null && pm["text"] is String && pm.str("text").contains("#$LATEST")
    private fun isLegacyPin(pm: JObj?) = pm != null && (pm["document"] as? JObj)?.str("file_name")?.startsWith("encargos-manifest") == true
    fun parseMan(t: String): JObj? { val i = t.indexOf("#$LATEST"); val a = t.indexOf('{', i); val b = t.lastIndexOf('}'); if (i < 0 || a < 0 || b < a) return null
        return try { (Json.parse(t.substring(a, b + 1)) as? JObj)?.takeIf { it["type"] == LATEST } } catch (e: Exception) { null } }
    fun manText(m: JObj): String {
        val d = Js.local(Js.parseDate(m["timestamp"])); fun p(n: Int) = n.toString().padStart(2, '0')
        val dev = m.str("deviceId"); val from = if (dev.startsWith("ANDROID")) "📱 Android" else if (dev.startsWith("PC")) "💻 PC" else "🌐 Web"
        return "📦 Última sincronización\nVersión ${Js.numberToString(Js.num(m["version"]))}\n${p(d.dayOfMonth)}/${p(d.monthValue)}/${d.year} ${p(d.hour)}:${p(d.minute)}\n✓ Correctamente sincronizado\nDesde: $from\n\nNo borres ni desfijes este mensaje ni el archivo $BK_NAME.\n\n#$LATEST\n${Json.stringify(m)}"
    }
    data class Remote(val kind: String, val pinId: Double, val m: JObj?, val foreign: Boolean = false)
    fun readRemote(): Remote {
        val ch = tg("getChat", jobj("chat_id" to chatId())) as JObj; val pm = ch["pinned_message"] as? JObj
        val fresh = Js.truthy(cfg()?.get("fresh"))
        if (isLatestPin(pm)) { val m = parseMan(pm!!.str("text")) ?: throw TgError("El mensaje fijado de Encargos en Telegram está dañado; se volverá a intentar")
            if (m["db"] != dbTag()) { if (fresh) return Remote("none", Js.num(pm["message_id"]), null, true); throw TgError(RESET_MSG) }
            if (Js.num(m["schemaVersion"]) > SCHEMA) throw TgError("Este dispositivo necesita actualizar Encargos para leer los datos")
            return Remote("latest", Js.num(pm["message_id"]), m) }
        if (isLegacyPin(pm)) {
            val bytes = fetchFile((pm!!["document"] as JObj).str("file_id"))
            val m = try { unseal(bytes) as JObj } catch (e: Exception) { if (fresh) return Remote("none", Js.num(pm["message_id"]), null, true); throw TgError(RESET_MSG) }
            if (m["databaseId"] != db()) { if (fresh) return Remote("none", Js.num(pm["message_id"]), null, true); throw TgError(RESET_MSG) }
            if (Js.num(m["schemaVersion"]) > SCHEMA) throw TgError("Este dispositivo necesita actualizar Encargos para leer los datos")
            if (m["packages"] !is ArrayList<*>) m["packages"] = JArr()
            return Remote("legacy", Js.num(pm["message_id"]), m)
        }
        return Remote("none", pm?.let { Js.num(it["message_id"]) } ?: 0.0, null)
    }
    private fun sameHead(a: Remote, b: Remote) = a.kind == b.kind && when (a.kind) { "latest" -> a.m!!["packageId"] == b.m!!["packageId"]; "legacy" -> a.pinId == b.pinId; else -> true }
    private fun writeManifest(man: JObj, cur: Remote): Double {
        val text = manText(man)
        if (cur.kind == "latest") { tg("editMessageText", jobj("chat_id" to chatId(), "message_id" to cur.pinId, "text" to text)); return cur.pinId }
        val t = tg("sendMessage", jobj("chat_id" to chatId(), "text" to text, "disable_notification" to true)) as JObj; val mid = Js.num(t["message_id"])
        try { tg("pinChatMessage", jobj("chat_id" to chatId(), "message_id" to mid, "disable_notification" to true)) } catch (e: Exception) { delMsg(mid); throw e }
        return mid
    }
    /** 'ok' (borrado o ya no existe) · 'old' (no se puede: más de 48 h) · 'net' (sin respuesta) */
    private fun delMsg(id: Double?): String {
        if (id == null || id == 0.0) return "ok"
        return try { tg("deleteMessage", jobj("chat_id" to chatId(), "message_id" to id)); "ok" } catch (e: TgError) {
            if ((e.message ?: "").contains("not found", true)) "ok" else if (e.code == 400 || e.code == 403) "old" else "net" } catch (e: Exception) { "net" }
    }
    private fun addCleanup(id: Double?) { if (id == null || id == 0.0) return; updMeta { m -> val l = LinkedHashSet((m["cleanup"] as? JArr ?: JArr()).map { Js.num(it) }); l.add(id); m["cleanup"] = JArr(l.toList().takeLast(50)) } }
    private fun leftover(n: Int) = updMeta { it["leftover"] = Js.num(it["leftover"]) + n }
    private fun runCleanup() {
        val m = meta(); val list = (m["cleanup"] as? JArr ?: JArr()).map { Js.num(it) }; if (list.isEmpty()) return
        val keep = ArrayList<Double>()
        for (id in list) { if (id == Js.num(m["backupMsg"]) || id == Js.num(m["manifestMsg"])) continue; when (delMsg(id)) { "net" -> keep.add(id); "old" -> leftover(1) } }
        updMeta { it["cleanup"] = JArr(keep) }
    }
    private fun neutralize(id: Double) {
        try {
            val media = Json.stringify(jobj("type" to "document", "media" to "attach://f", "caption" to "Respaldo antiguo vaciado por Encargos. Puedes borrar este mensaje."))
            api.sendFile("editMessageMedia", mapOf("chat_id" to Js.str(chatId()), "message_id" to Js.numberToString(id), "media" to media), "f", "encargos-antiguo-vacio.txt", "Encargos: respaldo antiguo sin datos.".toByteArray(), "text/plain")
        } catch (e: Exception) {}
        leftover(1)
    }

    /* ---------- formato anterior (solo para migrar) ---------- */
    private fun downloadMissing(m: JObj): Int {
        val meta = meta(); val done = LinkedHashSet(ls.arr(K.pp).map { Js.str(it) }); var applied = 0; var late: List<JObj> = emptyList()
        val list = ArrayList<JObj>(); val snap = m["snapshot"] as? JObj
        if (snap != null && !done.contains(snap.str("packageId")) && Js.num(meta["localVersion"]) < Js.num(snap["version"])) list.add(snap)
        (m["packages"] as? JArr)?.objs()?.forEach { if (!done.contains(it.str("packageId"))) list.add(it) }
        for (p in list) {
            val bytes = fetchFile(p.str("fileId"))
            if (p["checksum"] != null && sha(bytes) != p.str("checksum")) throw TgError("Un paquete llegó dañado (checksum); se volverá a intentar")
            val pkg = unseal(bytes) as JObj; if (pkg["databaseId"] != db() || Js.num(pkg["schemaVersion"]) > SCHEMA) throw TgError("Paquete no compatible")
            val r = onUi { applyOps(late + ((pkg["operations"] as? JArr)?.objs() ?: emptyList()), p.str("packageId")) }; late = r.late; applied += r.n
            done.add(p.str("packageId")); ls.put(K.pp, JArr(done.toList().takeLast(5000)))
        }
        if (late.isNotEmpty()) onUi { applyOps(late, "late") }
        updMeta { mm -> mm["remoteVersion"] = m["latestVersion"]; if (Js.num(mm["localVersion"]) < Js.num(m["latestVersion"])) mm["localVersion"] = m["latestVersion"] }
        return applied
    }

    /* ---------- remoto distinto: descargar → checksum → descifrar → validar → MERGE ---------- */
    private fun pullLatest(r: Remote): Int {
        val m = r.m!!
        val bytes = try { fetchFile(m.str("telegramFileId")) } catch (e: TgError) { throw TgError("No se encontró el respaldo en Telegram; se volverá a intentar") }
        if (sha(bytes) != m.str("checksum")) throw TgError("El respaldo de Telegram llegó dañado (checksum); no se aplicó nada y se volverá a intentar")
        val pkg = unseal(bytes) as? JObj
        if (pkg == null || pkg["type"] != LATEST || pkg["databaseId"] != db() || pkg["packageId"] != m["packageId"] || pkg["operations"] !is ArrayList<*>) throw TgError("El respaldo de Telegram no tiene un formato válido; no se aplicó nada")
        if (Js.num(pkg["schemaVersion"]) > SCHEMA) throw TgError("Este dispositivo necesita actualizar Encargos para leer los datos")
        val ops = (pkg["operations"] as JArr).objs()
        return onUi {
            val r1 = applyOps(ops, "respaldo v" + Js.numberToString(Js.num(m["version"])), false); var n = r1.n; if (r1.late.isNotEmpty()) n += applyOps(r1.late, "late", false).n
            val rs = HashMap<String, JObj>(); ops.forEach { o -> rs[o.str("entity") + ":" + o.str("entityId")] = jobj("ts" to Js.num(o["timestamp"]), "dev" to o["deviceId"]) }
            val stamps = ls.obj(K.st); val behind = stamps.any { (k, v) -> rs[k] == null || newer(v as JObj, rs[k]) }
            updMeta { mm -> mm["appliedPackage"] = m["packageId"]; mm["remoteVersion"] = m["version"]; mm["localVersion"] = max(Js.num(mm["localVersion"]), Js.num(m["version"])); mm["backupMsg"] = m["messageId"]; mm["manifestMsg"] = r.pinId
                mm["lastPull"] = System.currentTimeMillis().toDouble(); mm["latest"] = jobj("version" to m["version"], "size" to (m["size"] ?: bytes.size.toDouble()), "checksum" to m["checksum"], "timestamp" to m["timestamp"], "deviceId" to m["deviceId"]); if (behind) mm["dirty"] = true }
            if (n == 0) slog("in", "Respaldo versión ${Js.numberToString(Js.num(m["version"]))} revisado: sin cambios nuevos")
            n
        }
    }
    private fun snapOps(packageId: String, dev: String, base: Double): JArr {
        val s = snapshot(); val stamps = ls.obj(K.st); val ops = JArr(); var ch = false
        ENT.forEach { t -> s[t]!!.forEach { (id, payload) -> val k = "$t:$id"; var st = stamps[k] as? JObj
            if (st == null || Js.truthy(st["deleted"])) { st = jobj("ts" to System.currentTimeMillis().toDouble(), "dev" to dev); stamps[k] = st; ch = true }
            ops.add(jobj("opId" to "L-$packageId-$k", "deviceId" to st["dev"], "sequence" to 0.0, "timestamp" to st["ts"], "entity" to t, "entityId" to id, "operation" to "UPDATE", "data" to payload, "baseVersion" to base)) } }
        stamps.forEach { (k, v) -> val st = v as? JObj ?: return@forEach; if (!Js.truthy(st["deleted"])) return@forEach; val i = k.indexOf(':'); val t = k.substring(0, i); val id = k.substring(i + 1)
            if (s[t]?.containsKey(id) == true) return@forEach
            ops.add(jobj("opId" to "L-$packageId-del-$k", "deviceId" to st["dev"], "sequence" to 0.0, "timestamp" to st["ts"], "entity" to t, "entityId" to id, "operation" to "DELETE", "data" to null, "deleted" to true, "baseVersion" to base)) }
        if (ch) ls.put(K.st, stamps); return ops
    }
    data class Published(val ok: Boolean, val ops: Int = 0, val version: Double = 0.0)
    private fun publish(r: Remote): Published {
        val dev = deviceId(); val meta = meta()
        val base = max(if (r.kind == "latest") Js.num(r.m!!["version"]) else if (r.kind == "legacy") Js.num(r.m!!["latestVersion"]) else 0.0, Js.num(meta["localVersion"]))
        val version = base + 1; val packageId = Crypto.uuid()
        // 1) paquete (con la cola local ya integrada) → 2) gzip → 3) cifrar
        val (pkg, qIds) = onUi { val q = queue().objs().map { it.str("opId") }
            jobj("type" to LATEST, "schemaVersion" to SCHEMA.toDouble(), "databaseId" to db(), "deviceId" to dev, "packageId" to packageId, "version" to version, "baseVersion" to base, "createdAt" to Js.nowIso(), "operations" to snapOps(packageId, dev, base)) to q }
        val bytes = seal(pkg); if (bytes.size > MAX_BYTES) throw TgError("El respaldo pesa más de 20 MB (límite de Telegram para bots). Elimina fotos que ya no necesites.")
        val checksum = sha(bytes)
        // 4) subir el nuevo (el anterior no se toca)
        val (msgId, fileId) = sendFile(bytes, BK_NAME, "$LATEST\nVersión ${Js.numberToString(version)}")
        // 5) verificar: se descarga otra vez y se compara el checksum
        try { val back = fetchFile(fileId); if (sha(back) != checksum) throw TgError("El archivo subido a Telegram no coincide (checksum). El respaldo anterior se conserva; se volverá a intentar.") }
        catch (e: Exception) { if (delMsg(msgId) == "net") addCleanup(msgId); throw e }
        // 6) registrar en el coordinador, solo si nadie publicó mientras tanto
        val cur = try { readRemote() } catch (e: Exception) { addCleanup(msgId); throw e }
        if (!sameHead(cur, r)) { if (delMsg(msgId) == "net") addCleanup(msgId); return Published(false) }
        val man = jobj("type" to LATEST, "version" to version, "packageId" to packageId, "deviceId" to dev, "timestamp" to Js.nowIso(), "checksum" to checksum, "telegramFileId" to fileId,
            "messageId" to msgId, "size" to bytes.size.toDouble(), "schemaVersion" to SCHEMA.toDouble(), "db" to dbTag())
        val prev = JArr()
        fun addPrev(id: Any?, backup: Boolean = false) { val v = Js.num(id); if (v != 0.0 && prev.objs().none { Js.num(it["id"]) == v }) prev.add(jobj("id" to v, "backup" to backup)) }
        if (cur.kind == "latest") { if (cur.m!!["messageId"] != null) addPrev(cur.m["messageId"], true) }
        else if (cur.kind == "legacy") { addPrev(cur.pinId); (cur.m!!["snapshot"] as? JObj)?.let { addPrev(it["messageId"]) }; (cur.m["packages"] as? JArr)?.objs()?.forEach { addPrev(it["messageId"]) } }
        if (!cur.foreign) { if (meta["backupMsg"] != null) addPrev(meta["backupMsg"], true); if (cur.kind != "latest" && meta["manifestMsg"] != null) addPrev(meta["manifestMsg"]) }
        val f = jobj("packageId" to packageId, "messageId" to msgId, "version" to version, "qIds" to JArr(qIds), "prev" to prev, "man" to man)
        updMeta { it["inflight"] = f }
        try { writeManifest(man, cur) }
        catch (e: TgError) { if (e.code != null) { updMeta { it.remove("inflight") }; if (delMsg(msgId) == "net") addCleanup(msgId) }; throw e }
        Thread.sleep(if (isTest) 150 else 1200)
        val chk = readRemote()
        if (chk.kind != "latest" || chk.m!!["packageId"] != packageId) { updMeta { it.remove("inflight") }; if (delMsg(msgId) == "net") addCleanup(msgId); return Published(false) }
        finalize(f, chk.pinId)
        return Published(true, (pkg["operations"] as JArr).size, version)
    }
    /** 7) versión registrada → 8) recién ahora se borra lo anterior → 9) queda solo el respaldo nuevo */
    private fun finalize(f: JObj, pinId: Double) {
        val man = f["man"] as JObj
        onUi {
            val q = (f["qIds"] as? JArr ?: JArr()).map { Js.str(it) }.toSet(); saveQ(queue().objs().filter { !q.contains(it.str("opId")) })
            val po = LinkedHashSet(ls.arr(K.po).map { Js.str(it) }); po.addAll(q); ls.put(K.po, JArr(po.toList().takeLast(20000)))
            updMeta { mm -> mm["localVersion"] = f["version"]; mm["remoteVersion"] = f["version"]; mm["lastSyncedVersion"] = f["version"]; mm["appliedPackage"] = f["packageId"]; mm["backupMsg"] = f["messageId"]; mm["manifestMsg"] = pinId
                mm["lastSync"] = System.currentTimeMillis().toDouble(); mm["lastPublish"] = System.currentTimeMillis().toDouble(); mm["dirty"] = false; mm["linked"] = true
                mm["latest"] = jobj("version" to f["version"], "size" to man["size"], "checksum" to man["checksum"], "timestamp" to man["timestamp"], "deviceId" to man["deviceId"]); mm.remove("inflight"); mm.remove("manifestCache") }
            cfg()?.let { c -> if (c.containsKey("fresh")) { c.remove("fresh"); putCfg(c) } }
            forceSnap = false
            slog("out", "Respaldo único actualizado · versión ${Js.numberToString(Js.num(f["version"]))}")
        }
        var old = 0
        (f["prev"] as? JArr)?.objs()?.forEach { p -> val id = Js.num(p["id"]); if (id == Js.num(f["messageId"]) || id == pinId) return@forEach
            when (delMsg(id)) { "net" -> addCleanup(id); "old" -> if (Js.truthy(p["backup"])) neutralize(id) else { old++; leftover(1) } } }
        if (old > 0) slog("warn", "$old archivo${if (old == 1) "" else "s"} del sistema anterior no se pudo${if (old == 1) "" else "ieron"} borrar (Telegram no deja al bot borrar mensajes de más de 48 h). Puedes borrarlos a mano en el chat.")
        runCleanup()
    }
    private fun resolveInflight(r: Remote) {
        val f = meta()["inflight"] as? JObj ?: return
        if (r.kind == "latest" && r.m!!["packageId"] == f["packageId"]) { finalize(f, r.pinId); return }
        updMeta { it.remove("inflight") }
        if (!(r.kind == "latest" && Js.num(r.m!!["messageId"]) == Js.num(f["messageId"]))) addCleanup(Js.num(f["messageId"]))
    }

    @Volatile var forceSnap = false
    private fun cycle(): Int {
        if (Js.truthy(meta()["linked"])) onUi { collect() }
        repeat(5) {
            val r = readRemote()
            resolveInflight(r)
            var meta = meta()
            if (r.kind == "latest" && r.m!!["packageId"] != meta["appliedPackage"]) pullLatest(r)
            else if (r.kind == "latest") updMeta { it["remoteVersion"] = r.m!!["version"] }
            else if (r.kind == "legacy") { downloadMissing(r.m!!); updMeta { it["dirty"] = true } }
            onUi {
                if (!Js.truthy(meta()["linked"])) { val stamps = ls.obj(K.st); val known = ls.obj(K.h); val snap = snapshot()
                    ENT.forEach { t -> snap[t]!!.forEach { (id, p) -> val k = "$t:$id"; if (stamps[k] != null) known[k] = hashOf(p) } }; ls.put(K.h, known); updMeta { it["linked"] = true } }
                collect()
            }
            meta = meta()
            val force = forceSnap; val q = queue(); val hasData = app.data.isNotEmpty() || ls.obj(K.st).isNotEmpty()
            val stale = r.kind == "latest" && System.currentTimeMillis() - Js.parseDate(r.m!!["timestamp"]) > REFRESH_MS
            if (!(force || q.isNotEmpty() || Js.truthy(meta["dirty"]) || (r.kind != "latest" && hasData) || stale)) { runCleanup(); return 0 }
            if (!force && r.kind == "latest" && meta["lastPublish"] != null && System.currentTimeMillis() - Js.num(meta["lastPublish"]) < minGap()) {
                schedule(minGap() - (System.currentTimeMillis() - Js.num(meta["lastPublish"]).toLong()) + 300); return 0 }
            val res = publish(r)
            if (res.ok) return res.ops
            // otro dispositivo publicó a la vez: se combina lo suyo y se vuelve a intentar
        }
        throw TgError("Otro dispositivo está sincronizando al mismo tiempo; se reintentará")
    }

    /* ---------- ejecución: un ciclo a la vez, reintentos con espera ---------- */
    private val exec = Executors.newSingleThreadScheduledExecutor { r -> Thread(r, "encargos-sync").apply { isDaemon = true } }
    @Volatile var state = "idle"; private set
    @Volatile var lastErr = ""; private set
    @Volatile private var running = false
    @Volatile private var again = false
    private var backoff = 0; private var retry: ScheduledFuture<*>? = null; private var later: ScheduledFuture<*>? = null
    var onState: (() -> Unit)? = null
    private fun paint() { try { onState?.invoke() } catch (e: Exception) {} }
    private fun schedule(ms: Long) { later?.cancel(false); later = exec.schedule({ syncNowBlocking() }, max(0, ms), TimeUnit.MILLISECONDS) }

    /** Lanza una sincronización en segundo plano. */
    fun syncNow(done: ((Boolean, String) -> Unit)? = null) { exec.execute { val ok = syncNowBlocking(); done?.invoke(ok, lastErr) } }
    /** Sincroniza en el hilo actual (pruebas y WorkManager). */
    fun syncNowBlocking(): Boolean {
        if (!enabled()) return false
        synchronized(this) { if (running) { again = true; return false }; running = true }
        state = "syncing"; paint()
        try {
            cycle(); state = "ok"; lastErr = ""; backoff = 0; updMeta { it["lastSync"] = System.currentTimeMillis().toDouble() }; return true
        } catch (e: Exception) {
            val off = e is IOException && e !is TgError
            val code = (e as? TgError)?.code
            state = if (off) "offline" else "error"
            lastErr = if (off) "Sin conexión. Tus cambios se guardaron en este dispositivo." else if (code == 401) "El token del bot no es válido." else if (code == 400 && (e.message ?: "").contains("chat not found", true)) "No se encuentra el chat de Telegram." else (e.message ?: e.toString())
            slog("err", lastErr)
            val wait = (e as? TgError)?.retryAfter?.let { it * 1000L } ?: BACKOFF[minOf(backoff, BACKOFF.size - 1)]; backoff++
            retry?.cancel(false); retry = exec.schedule({ syncNowBlocking() }, wait, TimeUnit.MILLISECONDS)
            return false
        } finally {
            synchronized(this) { running = false }; paint()
            if (again) { again = false; schedule(600) }
        }
    }

    /** Después de cada guardado: agrupa 2 s y sincroniza. */
    private var deb: ScheduledFuture<*>? = null
    fun changedLocally() { if (!enabled()) return; deb?.cancel(false); deb = exec.schedule({ onUi { collect() }; syncNowBlocking() }, 2000, TimeUnit.MILLISECONDS) }

    data class Status(val kind: String, val text: String)
    fun status(): Status {
        val c = cfg() ?: return Status("off", "Tu negocio, en orden")
        if (Js.truthy(c["paused"])) return Status("off", "Sincronización en pausa")
        val p = pending(); val m = meta()
        if (state == "syncing") return Status("run", "Sincronizando…")
        if (state == "offline") return Status("offline", if (p > 0) "Sin conexión · $p pendiente${if (p == 1) "" else "s"}" else "Sin conexión")
        if (state == "error") return Status("error", "Error de sincronización")
        if (p > 0) return Status("pending", "Pendiente de sincronizar ($p)")
        val ls2 = Js.num(m["lastSync"]); if (ls2 > 0) { val s = max(0L, (System.currentTimeMillis() - ls2.toLong()) / 1000)
            return Status("ok", "Sincronizado " + if (s < 60) "hace $s s" else if (s < 3600) "hace ${Math.round(s / 60.0)} min" else "hace ${Math.round(s / 3600.0)} h") }
        return Status("ok", "Sincronizado")
    }
    fun metaInfo(): JObj = meta()

    /* ---------- conectar, enlazar, restaurar ---------- */
    sealed class ConnectResult { object Ok : ConnectResult(); object NeedStart : ConnectResult(); object Existing : ConnectResult(); class Error(val msg: String) : ConnectResult() }
    private var pendingTok: String? = null; private var pendingChat: Any? = null
    private val SYNC_KEYS = listOf(K.h, K.st, K.pp, K.po, K.meta, K.q)

    /** "Este es mi primer dispositivo": conecta el bot con el token. Bloqueante (llamar fuera del hilo de la interfaz). */
    fun connect(tok: String): ConnectResult {
        val t = tok.trim(); if (!Regex("^\\d{5,}:[\\w-]{20,}$").matches(t)) return ConnectResult.Error("Ese token no parece válido. Cópialo completo desde @BotFather.")
        try {
            putCfg(jobj("token" to t, "chatId" to null, "key" to null, "db" to null, "paused" to true))
            tg("getMe", JObj())
            val ups = (tg("getUpdates", jobj("limit" to 100.0, "timeout" to 0.0)) as? JArr)?.objs() ?: emptyList()
            val priv = ups.mapNotNull { (it["message"] ?: it["edited_message"]) as? JObj }.filter { (it["chat"] as? JObj)?.get("type") == "private" }
            if (priv.isEmpty()) { putCfg(null); return ConnectResult.NeedStart }
            val chat = (priv.last()["chat"] as JObj)["id"]
            putCfg(jobj("token" to t, "chatId" to chat, "key" to Crypto.B64U.enc(Crypto.random(32)), "db" to Crypto.uuid(), "paused" to false))
            val ch = tg("getChat", jobj("chat_id" to chat)) as JObj; val pm = ch["pinned_message"] as? JObj
            if (isLatestPin(pm) || isLegacyPin(pm)) { putCfg(null); pendingTok = t; pendingChat = chat; return ConnectResult.Existing }
            onUi { SYNC_KEYS.forEach { ls.del(it) } }
            slog("ok", "Bot conectado en ${app.platform}"); forceSnap = true; onUi { collect() }
            return if (syncNowBlocking()) ConnectResult.Ok else ConnectResult.Error(lastErr)
        } catch (e: Exception) { putCfg(null); return ConnectResult.Error(if ((e as? TgError)?.code == 401) "El token no es válido." else "No se pudo conectar con Telegram: " + (e.message ?: "")) }
    }
    /** "Empezar de nuevo con este bot" (cuando no se tiene el código). */
    fun freshStart(): Boolean {
        val t = pendingTok ?: return false; val c = pendingChat ?: return false
        onUi { SYNC_KEYS.forEach { ls.del(it) } }
        putCfg(jobj("token" to t, "chatId" to c, "key" to Crypto.B64U.enc(Crypto.random(32)), "db" to Crypto.uuid(), "paused" to false, "fresh" to true))
        slog("ok", "Sincronización nueva creada en ${app.platform}"); forceSnap = true; onUi { collect() }
        return syncNowBlocking()
    }
    fun linkCode(): String? { val c = cfg() ?: return null; return LinkCode(c.str("token"), c["chatId"]!!, c.str("key"), c.str("db")).encode() }
    /** "Ya lo tengo en otro dispositivo": pega el código ENC1. */
    fun link(code: String): ConnectResult {
        val o = LinkCode.parse(code) ?: return ConnectResult.Error("El código no es válido. Cópialo completo.")
        onUi { SYNC_KEYS.forEach { ls.del(it) } }
        putCfg(jobj("token" to o.token, "chatId" to o.chatId, "key" to o.key, "db" to o.db, "paused" to false))
        slog("ok", "Dispositivo enlazado: ${app.platform}")
        return if (syncNowBlocking()) ConnectResult.Ok else ConnectResult.Error(lastErr)
    }
    fun backupNow(): Boolean { forceSnap = true; onUi { collect() }; return syncNowBlocking() }
    fun pause(p: Boolean) { val c = cfg() ?: return; c["paused"] = p; putCfg(c); paint(); if (!p) syncNow() }
    fun disconnect() { putCfg(null); onUi { SYNC_KEYS.forEach { ls.del(it) } }; state = "idle"; lastErr = ""; paint() }

    /** Restaurar: comprueba el archivo ANTES de tocar los datos locales; luego reconstruye este dispositivo. */
    fun restore(): Result<Unit> = try {
        val r = readRemote()
        if (r.kind == "none") Result.failure(Exception("Todavía no hay un respaldo en Telegram."))
        else {
            if (r.kind == "latest") { val b = fetchFile(r.m!!.str("telegramFileId")); if (sha(b) != r.m.str("checksum")) throw TgError("el respaldo llegó dañado (checksum)"); unseal(b) }
            val keep = meta()
            onUi {
                app.data.clear(); listOf("encargos_notes", "encargos_expenses", "encargos_client_info").forEach { ls.del(it) }
                listOf(K.h, K.st, K.pp, K.po, K.q).forEach { ls.del(it) }
                putMeta(jobj("cleanup" to (keep["cleanup"] ?: JArr()), "backupMsg" to keep["backupMsg"], "manifestMsg" to keep["manifestMsg"], "leftover" to Js.num(keep["leftover"])))
                app.storage.set(Encargos.KEY, "[]")
            }
            if (r.kind == "latest") pullLatest(r) else downloadMissing(r.m!!)
            onUi {
                val snap = snapshot(); val known = JObj(); val stamps = ls.obj(K.st)
                ENT.forEach { t -> snap[t]!!.forEach { (id, p) -> val k = "$t:$id"; if (stamps[k] != null) known[k] = hashOf(p) } }; ls.put(K.h, known)
                updMeta { it["linked"] = true; it["dirty"] = r.kind != "latest"; it["lastSync"] = System.currentTimeMillis().toDouble() }
                slog("ok", "Respaldo restaurado"); app.loadShippingConfig(); app.refresh()
            }
            Result.success(Unit)
        }
    } catch (e: Exception) { Result.failure(e) }
}
