package com.agencia.encargos.core

/** Respaldos locales: JSON versión 5 (compatible con la app web), respaldo cifrado con contraseña, importación y CSV. */
class Backup(private val app: Encargos) {
    private val ls get() = app.ls

    fun extras(): JObj = jobj("activity" to ls.arr("encargos_activity"), "expenses" to ls.arr("encargos_expenses"), "clientInfo" to ls.obj("encargos_client_info"),
        "templates" to ls.json("encargos_templates"), "buyRates" to ls.json("encargos_buy_rates"))

    private fun payloadObj(auto: Boolean): JObj {
        val o = jobj("version" to 5.0, "exportedAt" to Js.nowIso())
        if (auto) o["auto"] = true
        o["shippingRates"] = jobj("rates" to app.shippingRates, "names" to app.shippingNames)
        o["data"] = app.data; o["notes"] = ls.arr("encargos_notes"); o["extra"] = extras()
        return o
    }
    fun fileName(prefix: String = "respaldo_encargos_", ext: String = ".json") = prefix + Js.nowIso().take(10) + ext

    /** exportData(): texto del respaldo normal. */
    fun exportJson(auto: Boolean = false): String {
        val s = Json.stringify(payloadObj(auto), if (auto) 0 else 2)
        markBackup(false); return s
    }
    fun markBackup(encrypted: Boolean) {
        val now = Js.numberToString(System.currentTimeMillis().toDouble())
        ls.set("encargos_last_backup", now); if (encrypted) ls.set("encargos_last_backup_enc", now)
        app.attention.log("backup", if (encrypted) "Respaldo cifrado realizado" else "Respaldo realizado")
        app.notifs.sysEvents(); app.changed(false)
    }

    /** Respaldo cifrado: PBKDF2-SHA256 (150000) + AES-256-GCM, mismo formato que la app web. */
    fun exportEncrypted(password: String): String {
        require(password.length >= 6) { "La contraseña debe tener al menos 6 caracteres." }
        val payload = Json.stringify(payloadObj(false)).toByteArray(Charsets.UTF_8)
        val salt = Crypto.random(16); val iv = Crypto.random(12)
        val key = Crypto.pbkdf2(password, salt, 150000)
        val ct = Crypto.aesGcmEncrypt(key, iv, payload)
        val out = Json.stringify(jobj("encargosCifrado" to 1.0, "v" to 1.0, "alg" to "AES-GCM", "kdf" to "PBKDF2-SHA256", "iter" to 150000.0, "salt" to Crypto.b64(salt), "iv" to Crypto.b64(iv), "data" to Crypto.b64(ct)))
        markBackup(true); return out
    }

    sealed class Parsed { class Plain(val o: Any?) : Parsed(); class Encrypted(val o: JObj) : Parsed(); class Invalid(val why: String) : Parsed() }
    fun parse(text: String): Parsed = try {
        when (val o = Json.parse(text.trimStart('﻿'))) {
            is LinkedHashMap<*, *> -> { @Suppress("UNCHECKED_CAST") val m = o as JObj; if (Js.truthy(m["encargosCifrado"])) Parsed.Encrypted(m) else if (m["data"] is ArrayList<*>) Parsed.Plain(m) else Parsed.Invalid("El archivo de respaldo no es válido.") }
            is ArrayList<*> -> Parsed.Plain(o)
            else -> Parsed.Invalid("El archivo de respaldo no es válido.")
        }
    } catch (e: Exception) { Parsed.Invalid("El archivo de respaldo no es válido.") }

    fun decrypt(o: JObj, password: String): Any? {
        val iter = Js.num(o["iter"]).toInt().let { if (it > 0) it else 150000 }
        val key = Crypto.pbkdf2(password, Crypto.unb64(o.str("salt")), iter)
        val pt = Crypto.aesGcmDecrypt(key, Crypto.unb64(o.str("iv")), Crypto.unb64(o.str("data")))
        return Json.parse(String(pt, Charsets.UTF_8))
    }

    /** readImport(): reemplaza los datos actuales por los del respaldo. Devuelve cuántos encargos se importaron. */
    @Suppress("UNCHECKED_CAST")
    fun applyImport(o: Any?): Int {
        val arr: JArr = (if (o is ArrayList<*>) o as JArr else (o as? JObj)?.get("data") as? JArr) ?: throw IllegalArgumentException("El archivo de respaldo no es válido.")
        val m = o as? JObj
        val sr = m?.get("shippingRates") as? JObj
        if (sr != null) {
            if (sr["rates"] is LinkedHashMap<*, *>) {
                val r = JObj(); Encargos.DEFAULT_SHIPPING_RATES.forEach { (k, v) -> r[k] = v }; (sr["rates"] as JObj).forEach { (k, v) -> r[k] = v }
                val n = JObj(); Encargos.DEFAULT_SHIPPING_NAMES.forEach { (k, v) -> n[k] = v }; (sr["names"] as? JObj)?.forEach { (k, v) -> n[k] = v }
                app.shippingRates = r; app.shippingNames = n
            } else {
                app.shippingRates = jobj("avion" to (sr["avion"]?.let { Js.num(it) } ?: 7.0), "barco" to (sr["barco"]?.let { Js.num(it) } ?: 4.0))
                app.shippingNames = JObj().also { n -> Encargos.DEFAULT_SHIPPING_NAMES.forEach { (k, v) -> n[k] = v } }
            }
            app.saveShippingRates()
        }
        val data = JArr()
        arr.forEach { x -> val ox = x as? JObj ?: return@forEach; val c = Json.deepCopy(ox) as JObj
            val t = c.str("shippingType").ifEmpty { "avion" }; c["shippingType"] = t
            c["lbCost"] = if (ox["lbCost"] != null) Js.num(ox["lbCost"]) else (app.shippingRates[t]?.let { Js.num(it) } ?: 7.0)
            data.add(c) }
        app.data = data
        (m?.get("notes") as? JArr)?.let { ls.put("encargos_notes", it) }
        (m?.get("extra") as? JObj)?.let { x ->
            (x["activity"] as? JArr)?.let { ls.put("encargos_activity", it) }
            (x["expenses"] as? JArr)?.let { ls.put("encargos_expenses", it) }
            (x["clientInfo"] as? JObj)?.let { ls.put("encargos_client_info", it) }
            (x["templates"] as? JObj)?.let { ls.put("encargos_templates", it) }
            (x["buyRates"] as? JObj)?.let { ls.put("encargos_buy_rates", it) }
        }
        app.notifs.suppressUntil = System.currentTimeMillis() + 1500
        app.persist(); app.refresh()
        app.notifs.importDone(data.size)
        return data.size
    }

    fun csv(): String {
        val esc = { v: String -> if (Regex("[\",\n;]").containsMatchIn(v)) "\"" + v.replace("\"", "\"\"") + "\"" else v }
        val header = listOf("Cliente", "Teléfono", "Estado", "Envío", "Libras", "Tarifa/lb", "Costo", "Envío total", "Total", "Pagado", "Saldo", "Fecha", "Nota interna")
        val rows = app.orders().map { x -> listOf(x.str("name"), x.str("phone"), x.str("status"), app.shippingLabel(x.str("shippingType").ifEmpty { "avion" }),
            Js.toFixed(Js.num(x["pounds"]), 2), Js.toFixed(Js.num(x["lbCost"]), 2), Js.toFixed(Js.num(x["cost"]), 2), Js.toFixed(Js.num(x["shipping"]), 2),
            Js.toFixed(Js.num(x["total"]), 2), Js.toFixed(Js.num(x["paid"]), 2), Js.toFixed(Js.num(x["balance"]), 2), Js.fmt(x["date"]), x.str("note")) }
        return "﻿" + (listOf(header) + rows).joinToString("\r\n") { r -> r.joinToString(",") { esc(it) } }
    }

    /* ---------- Respaldo automático (diario / semanal / desactivado) ---------- */
    var autoMode: String
        get() = ls.get("encargos_auto_backup", "daily") ?: "daily"
        set(v) { ls.set("encargos_auto_backup", v); app.changed(false) }
    val lastAuto: Double get() = (ls.get("encargos_auto_backup_last", "0") ?: "0").toDoubleOrNull() ?: 0.0
    /** Devuelve el contenido a guardar si toca hacer el respaldo automático (la plataforma escribe el archivo y llama a [autoDone]). */
    fun autoDue(force: Boolean = false): String? {
        val mode = autoMode; if (mode == "off" && !force) return null
        val need = if (mode == "weekly") 7 * 86400000.0 else 86400000.0
        if (!force && System.currentTimeMillis() - lastAuto < need) return null
        if (app.data.isEmpty() && !force) return null
        return Json.stringify(payloadObj(true))
    }
    fun autoDone() { val now = Js.numberToString(System.currentTimeMillis().toDouble()); ls.set("encargos_auto_backup_last", now); ls.set("encargos_last_backup", now); app.notifs.sysEvents(); app.changed(false) }
}
