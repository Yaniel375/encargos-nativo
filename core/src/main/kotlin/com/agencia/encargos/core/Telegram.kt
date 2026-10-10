package com.agencia.encargos.core

import java.io.ByteArrayOutputStream
import java.io.IOException
import java.net.HttpURLConnection
import java.net.URL

/** Error devuelto por Telegram (ok:false). Sin código = no hubo respuesta (red). */
class TgError(message: String, val code: Int? = null, val retryAfter: Int? = null) : Exception(message)

/** Acceso a la Bot API. Solo métodos reales; el token nunca se escribe en registros ni mensajes de error. */
interface TgApi {
    fun call(method: String, params: JObj): Any?
    fun sendFile(method: String, fields: Map<String, String>, fileField: String, fileName: String, bytes: ByteArray, mime: String): Any?
    fun download(filePath: String): ByteArray
}

class HttpTgApi(private val base: String, private val token: () -> String, private val timeoutMs: Int = 45000) : TgApi {
    private fun url(path: String) = URL("$base/$path")
    /** Oculta el token en cualquier texto (mensajes de error). */
    private fun clean(s: String?): String { val t = token(); return (s ?: "").replace(t, "bot<oculto>").replace(Regex("bot\\d{5,}:[\\w-]{20,}"), "bot<oculto>") }

    private fun parse(c: HttpURLConnection): Any? {
        val code = c.responseCode
        val body = (if (code >= 400) c.errorStream else c.inputStream)?.use { it.readBytes() } ?: ByteArray(0)
        val j = try { Json.parse(String(body, Charsets.UTF_8)) as? JObj } catch (e: Exception) { null }
            ?: throw TgError("Respuesta no válida", code)
        if (j["ok"] != true) {
            val p = j["parameters"] as? JObj
            throw TgError(clean(j.str("description").ifEmpty { "HTTP $code" }), Js.num(j["error_code"]).toInt().let { if (it == 0) code else it }, p?.get("retry_after")?.let { Js.num(it).toInt() })
        }
        return j["result"]
    }
    private fun open(path: String): HttpURLConnection {
        val c = url(path).openConnection() as HttpURLConnection
        c.connectTimeout = 20000; c.readTimeout = timeoutMs; c.useCaches = false; return c
    }
    override fun call(method: String, params: JObj): Any? {
        try {
            val c = open("bot${token()}/$method"); c.requestMethod = "POST"; c.doOutput = true
            c.setRequestProperty("Content-Type", "application/json")
            c.outputStream.use { it.write(Json.stringify(params).toByteArray(Charsets.UTF_8)) }
            return parse(c)
        } catch (e: TgError) { throw e } catch (e: IOException) { throw IOException(clean(e.message), null) }
    }
    override fun sendFile(method: String, fields: Map<String, String>, fileField: String, fileName: String, bytes: ByteArray, mime: String): Any? {
        try {
            val b = "----encargos" + Crypto.hex(Crypto.random(12))
            val out = ByteArrayOutputStream()
            fun w(s: String) = out.write(s.toByteArray(Charsets.UTF_8))
            fields.forEach { (k, v) -> w("--$b\r\nContent-Disposition: form-data; name=\"$k\"\r\n\r\n$v\r\n") }
            w("--$b\r\nContent-Disposition: form-data; name=\"$fileField\"; filename=\"$fileName\"\r\nContent-Type: $mime\r\n\r\n"); out.write(bytes); w("\r\n--$b--\r\n")
            val c = open("bot${token()}/$method"); c.requestMethod = "POST"; c.doOutput = true
            c.setRequestProperty("Content-Type", "multipart/form-data; boundary=$b"); c.setFixedLengthStreamingMode(out.size())
            c.outputStream.use { out.writeTo(it) }
            return parse(c)
        } catch (e: TgError) { throw e } catch (e: IOException) { throw IOException(clean(e.message), null) }
    }
    override fun download(filePath: String): ByteArray {
        try {
            val c = open("file/bot${token()}/$filePath")
            if (c.responseCode != 200) throw TgError("No se pudo descargar el paquete", c.responseCode)
            return c.inputStream.use { it.readBytes() }
        } catch (e: TgError) { throw e } catch (e: IOException) { throw IOException(clean(e.message), null) }
    }
}

/** Almacén de secretos de la plataforma (Android Keystore / DPAPI). Aquí viven el token y la clave. */
interface Secrets {
    fun get(name: String): String?
    fun set(name: String, value: String)
    fun remove(name: String)
}
class MemorySecrets : Secrets { private val m = HashMap<String, String>(); override fun get(name: String) = m[name]; override fun set(name: String, value: String) { m[name] = value }; override fun remove(name: String) { m.remove(name) } }
