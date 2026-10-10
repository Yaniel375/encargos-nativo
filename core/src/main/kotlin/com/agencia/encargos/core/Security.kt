package com.agencia.encargos.core

/**
 * Seguridad de las apps nativas.
 * - Sin contraseña por defecto: se crea en el primer inicio.
 * - Contraseña y PIN con PBKDF2-SHA256 + sal + un secreto del dispositivo (Android Keystore / DPAPI),
 *   así que el PIN no se puede probar fuera de este equipo.
 * - Esperas progresivas tras intentos fallidos (mismas que la app web), registro de seguridad,
 *   bloqueo automático, privacidad en recientes, confirmación para acciones sensibles.
 * @param deviceSecret secreto de 32 bytes protegido por el sistema operativo.
 */
class Security(private val app: Encargos, private val deviceSecret: ByteArray) {
    private val ls get() = app.ls
    private val K_AUTH = "encargos_native_auth"; private val K_PIN = "encargos_native_pin"
    private val K_ON = "encargos_pin_on"; private val K_BIO = "encargos_bio_on"; private val K_FAILS = "encargos_sec_fails"; private val K_LOG = "encargos_sec_log"
    private val K_SENS = "encargos_sec_sensitive"; private val K_PRIV = "encargos_privacy"
    private val ITER_PW = 120000; private val ITER_PIN = 60000
    @Volatile var recentAuth = 0L; private set

    private fun derive(secret: String, salt: ByteArray, it: Int): String = Crypto.hex(Crypto.pbkdf2(secret, salt + deviceSecret, it))

    /* ---------- usuario y contraseña ---------- */
    fun hasCredentials() = ls.json(K_AUTH) is LinkedHashMap<*, *>
    fun userName(): String = ls.obj(K_AUTH).str("user")
    fun setCredentials(user: String, password: String): Result<Unit> {
        if (user.trim().length < 3) return Result.failure(Exception("El usuario debe tener al menos 3 caracteres."))
        if (password.length < 6) return Result.failure(Exception("La contraseña debe tener al menos 6 caracteres."))
        val salt = Crypto.random(16)
        ls.put(K_AUTH, jobj("user" to user.trim(), "salt" to Crypto.b64(salt), "it" to ITER_PW.toDouble(), "h" to derive(password, salt, ITER_PW)))
        log(true, "Usuario y contraseña actualizados"); return Result.success(Unit)
    }
    fun checkPassword(user: String, password: String): Boolean {
        val a = ls.obj(K_AUTH); if (a.isEmpty()) return false
        val okUser = user.trim().lowercase() == a.str("user").lowercase()
        val h = derive(password, Crypto.unb64(a.str("salt")), Js.num(a["it"]).toInt())
        return okUser && Crypto.ctEq(h, a.str("h"))
    }

    /* ---------- PIN ---------- */
    fun hasPin() = ls.json(K_PIN) is LinkedHashMap<*, *>
    fun pinOn() = hasPin() && ls.get(K_ON, "") == "on"
    fun pinLen(): Int = Js.num(ls.obj(K_PIN)["len"]).toInt().let { if (it in 4..6) it else 4 }
    fun setPin(pin: String): Result<Unit> {
        if (!Regex("^\\d{4,6}$").matches(pin)) return Result.failure(Exception("El PIN debe tener entre 4 y 6 números."))
        val salt = Crypto.random(16)
        val had = hasPin()
        ls.put(K_PIN, jobj("salt" to Crypto.b64(salt), "it" to ITER_PIN.toDouble(), "h" to derive(pin, salt, ITER_PIN), "len" to pin.length.toDouble())); ls.set(K_ON, "on")
        log(true, if (had) "PIN cambiado" else "PIN creado"); app.notifs.sysEvents(); return Result.success(Unit)
    }
    fun verifyPin(pin: String): Boolean { val v = ls.obj(K_PIN); if (v.isEmpty()) return false; return Crypto.ctEq(derive(pin, Crypto.unb64(v.str("salt")), Js.num(v["it"]).toInt()), v.str("h")) }
    fun disablePin() { ls.del(K_PIN); ls.set(K_ON, "off"); ls.set(K_BIO, "off"); log(true, "PIN desactivado"); app.notifs.sysEvents() }

    /* ---------- biometría ---------- */
    fun bioOn() = ls.get(K_BIO, "") == "on" && pinOn()
    fun setBio(on: Boolean) { ls.set(K_BIO, if (on) "on" else "off"); log(true, if (on) "Huella activada" else "Huella desactivada"); app.notifs.sysEvents() }

    /* ---------- intentos fallidos: espera progresiva, nunca bloqueo permanente ---------- */
    fun waitMs(): Long { val s = ls.obj(K_FAILS); return maxOf(0L, Js.num(s["until"]).toLong() - System.currentTimeMillis()) }
    /** Devuelve el mensaje a mostrar. */
    fun fail(kind: String): String {
        val s = ls.obj(K_FAILS); val n = Js.num(s["n"]).toInt() + 1
        val w = if (n >= 12) 15 * 60000L else if (n >= 10) 5 * 60000L else if (n >= 8) 2 * 60000L else if (n >= 5) 30000L else 0L
        s["n"] = n.toDouble(); if (w > 0) s["until"] = (System.currentTimeMillis() + w).toDouble(); ls.put(K_FAILS, s)
        log(false, "Intento de acceso incorrecto" + when (kind) { "pin" -> " (PIN)"; "password" -> " (contraseña)"; else -> "" })
        return if (w > 0) "Demasiados intentos. Espera " + (if (w >= 60000) "${w / 60000} min" else "${w / 1000} s") + "." else if (kind == "pin") "PIN incorrecto." else "Usuario o contraseña incorrectos."
    }
    fun ok(method: String) {
        ls.put(K_FAILS, jobj("n" to 0.0, "until" to 0.0)); recentAuth = System.currentTimeMillis(); touch()
        log(true, "Aplicación desbloqueada" + when (method) { "pin" -> " con PIN"; "bio" -> " con huella"; "password" -> " con contraseña"; else -> "" })
    }

    /* ---------- bloqueo automático ---------- */
    data class LockCfg(val on: Boolean, val minutes: Double, val onOpen: Boolean)
    fun lockCfg(): LockCfg {
        val on = ls.get("encargos_lock_enabled", null) != "off"
        val raw = ls.get("encargos_lock_minutes", null); var min = raw?.toDoubleOrNull() ?: 5.0; if (!(min >= 0)) min = 5.0
        return LockCfg(on, min, ls.get("encargos_lock_on_open", null) != "off")
    }
    fun setLock(minutes: Double?) {
        if (minutes == null) { ls.set("encargos_lock_enabled", "off"); log(true, "Bloqueo automático: nunca") }
        else { ls.set("encargos_lock_enabled", "on"); ls.set("encargos_lock_minutes", Js.numberToString(minutes)); log(true, "Bloqueo automático: " + lockLabel(minutes)) }
        app.notifs.sysEvents(); app.changed(false)
    }
    fun lockLabel(m: Double) = if (m == 0.0) "Inmediatamente" else if (m < 1) "${Math.round(m * 60)} segundos" else if (m == 1.0) "1 minuto" else "${Js.numberToString(m)} minutos"
    fun setLockOnOpen(on: Boolean) { ls.set("encargos_lock_on_open", if (on) "on" else "off"); app.changed(false) }
    /** ¿Hay que pedir identidad al volver tras [hiddenMs] ms fuera de la app? */
    fun shouldLockAfter(hiddenMs: Long): Boolean { val c = lockCfg(); return c.on && hiddenMs > c.minutes * 60000 }
    fun touch() { ls.set("encargos_sec_last", Js.numberToString(System.currentTimeMillis().toDouble())) }

    /* ---------- privacidad y acciones sensibles ---------- */
    fun privacyOn() = ls.get(K_PRIV, "on") != "off"
    fun setPrivacy(on: Boolean) { ls.set(K_PRIV, if (on) "on" else "off"); log(true, if (on) "Privacidad en recientes activada" else "Privacidad en recientes desactivada"); app.notifs.sysEvents(); app.changed(false) }
    fun sensitiveOn() = ls.get(K_SENS, "on") != "off"
    fun setSensitive(on: Boolean) { ls.set(K_SENS, if (on) "on" else "off"); log(true, "Protección de acciones sensibles " + if (on) "activada" else "desactivada"); app.notifs.sysEvents(); app.changed(false) }
    /** true si hay que pedir PIN/contraseña antes de una acción sensible. */
    fun needsAuth(force: Boolean = false): Boolean = force || (sensitiveOn() && System.currentTimeMillis() - recentAuth >= 120000)
    fun forget() { recentAuth = 0 }

    /* ---------- registro ---------- */
    fun log(ok: Boolean, text: String) { val a = ls.arr(K_LOG); a.add(0, jobj("t" to System.currentTimeMillis().toDouble(), "ok" to ok, "text" to text)); ls.put(K_LOG, JArr(a.take(80))) }
    fun logEntries(): List<JObj> = ls.arr(K_LOG).objs()

    /** Borrar todo: requiere escribir ELIMINAR en la interfaz. */
    fun wipe(resetAccess: Boolean) {
        app.data.clear(); app.persist()
        listOf("encargos_notes", "encargos_expenses", "encargos_client_info", "encargos_activity", "encargos_attn_reviewed", "encargos_attn_last").forEach { ls.del(it) }
        if (resetAccess) { ls.del(K_AUTH); ls.del(K_PIN); ls.set(K_ON, "off"); ls.set(K_BIO, "off"); ls.put(K_FAILS, jobj("n" to 0.0, "until" to 0.0)) }
        log(true, if (resetAccess) "App restablecida (datos y acceso)" else "Todos los datos eliminados"); app.notifs.sysEvents(); app.reload()
    }
}
