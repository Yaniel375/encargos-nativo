package com.agencia.encargos.core

import java.io.ByteArrayInputStream
import java.io.ByteArrayOutputStream
import java.security.MessageDigest
import java.security.SecureRandom
import java.util.Base64
import java.util.zip.GZIPInputStream
import java.util.zip.GZIPOutputStream
import javax.crypto.Cipher
import javax.crypto.Mac
import javax.crypto.spec.GCMParameterSpec
import javax.crypto.spec.SecretKeySpec

/** Primitivas criptográficas compatibles byte a byte con la app web. */
object Crypto {
    private val rnd = SecureRandom()
    private val HEX = "0123456789abcdef".toCharArray()

    fun hex(b: ByteArray): String { val c = CharArray(b.size * 2); b.forEachIndexed { i, x -> c[2 * i] = HEX[(x.toInt() shr 4) and 15]; c[2 * i + 1] = HEX[x.toInt() and 15] }; return String(c) }
    fun sha256(b: ByteArray): ByteArray = MessageDigest.getInstance("SHA-256").digest(b)
    /** sha256hex(str) de la app: SHA-256 del texto en UTF-8, en hexadecimal. */
    fun sha256hex(s: String): String = hex(sha256(s.toByteArray(Charsets.UTF_8)))
    fun shaHex(b: ByteArray): String = hex(sha256(b))
    fun random(n: Int): ByteArray = ByteArray(n).also { rnd.nextBytes(it) }

    /** crypto.randomUUID() */
    fun uuid(): String {
        val b = random(16); b[6] = ((b[6].toInt() and 15) or 64).toByte(); b[8] = ((b[8].toInt() and 63) or 128).toByte()
        val h = hex(b); return "${h.substring(0, 8)}-${h.substring(8, 12)}-${h.substring(12, 16)}-${h.substring(16, 20)}-${h.substring(20)}"
    }

    /** detUuid(s) de la app (identificadores deterministas para datos antiguos). */
    fun detUuid(s: String): String {
        val h = sha256hex("encargos-sync|$s")
        val v = (8 + (h[16].digitToInt(16) and 3)).toString(16)
        return "${h.substring(0, 8)}-${h.substring(8, 12)}-5${h.substring(13, 16)}-$v${h.substring(17, 20)}-${h.substring(20, 32)}"
    }

    fun dbTag(db: String): String = sha256hex("encargos-db|$db").substring(0, 16)

    object B64U {
        fun enc(b: ByteArray): String = Base64.getUrlEncoder().withoutPadding().encodeToString(b)
        fun dec(s: String): ByteArray = Base64.getUrlDecoder().decode(s.trim().trimEnd('='))
    }
    fun b64(b: ByteArray): String = Base64.getEncoder().encodeToString(b)
    fun unb64(s: String): ByteArray = Base64.getDecoder().decode(s.trim())

    fun gzip(b: ByteArray): ByteArray { val bo = ByteArrayOutputStream(); GZIPOutputStream(bo).use { it.write(b) }; return bo.toByteArray() }
    fun gunzip(b: ByteArray): ByteArray = GZIPInputStream(ByteArrayInputStream(b)).use { it.readBytes() }

    /* ---------- ENCv1: "ENCv1" + Z|N + IV(12) + AES-256-GCM(ciphertext+tag), AAD = "ENCv1|" + db ---------- */
    fun seal(obj: Any?, key: ByteArray, db: String): ByteArray {
        val raw = Json.stringify(obj).toByteArray(Charsets.UTF_8)
        val z = gzip(raw)
        val iv = random(12)
        val c = Cipher.getInstance("AES/GCM/NoPadding")
        c.init(Cipher.ENCRYPT_MODE, SecretKeySpec(key, "AES"), GCMParameterSpec(128, iv))
        c.updateAAD("ENCv1|$db".toByteArray(Charsets.UTF_8))
        val ct = c.doFinal(z)
        return "ENCv1Z".toByteArray(Charsets.US_ASCII) + iv + ct
    }

    fun unseal(bytes: ByteArray, key: ByteArray, db: String): Any? {
        if (bytes.size < 18 + 16) throw IllegalArgumentException("Formato desconocido")
        val h = String(bytes, 0, 6, Charsets.US_ASCII)
        if (!h.startsWith("ENCv1")) throw IllegalArgumentException("Formato desconocido")
        val c = Cipher.getInstance("AES/GCM/NoPadding")
        c.init(Cipher.DECRYPT_MODE, SecretKeySpec(key, "AES"), GCMParameterSpec(128, bytes, 6, 12))
        c.updateAAD("ENCv1|$db".toByteArray(Charsets.UTF_8))
        var pt = c.doFinal(bytes, 18, bytes.size - 18)
        if (h[5] == 'Z') pt = gunzip(pt)
        return Json.parse(String(pt, Charsets.UTF_8))
    }

    /* ---------- PBKDF2-HMAC-SHA256 (respaldo cifrado con contraseña, igual que WebCrypto) ---------- */
    fun pbkdf2(password: String, salt: ByteArray, iterations: Int, len: Int = 32): ByteArray {
        val mac = Mac.getInstance("HmacSHA256"); mac.init(SecretKeySpec(password.toByteArray(Charsets.UTF_8), "HmacSHA256"))
        val out = ByteArray(len); var block = 1; var off = 0
        while (off < len) {
            mac.update(salt); mac.update(byteArrayOf((block ushr 24).toByte(), (block ushr 16).toByte(), (block ushr 8).toByte(), block.toByte()))
            var u = mac.doFinal(); val t = u.copyOf()
            for (i in 1 until iterations) { u = mac.doFinal(u); for (j in t.indices) t[j] = (t[j].toInt() xor u[j].toInt()).toByte() }
            val n = minOf(t.size, len - off); System.arraycopy(t, 0, out, off, n); off += n; block++
        }
        return out
    }

    fun aesGcmEncrypt(key: ByteArray, iv: ByteArray, pt: ByteArray): ByteArray {
        val c = Cipher.getInstance("AES/GCM/NoPadding"); c.init(Cipher.ENCRYPT_MODE, SecretKeySpec(key, "AES"), GCMParameterSpec(128, iv)); return c.doFinal(pt)
    }
    fun aesGcmDecrypt(key: ByteArray, iv: ByteArray, ct: ByteArray): ByteArray {
        val c = Cipher.getInstance("AES/GCM/NoPadding"); c.init(Cipher.DECRYPT_MODE, SecretKeySpec(key, "AES"), GCMParameterSpec(128, iv)); return c.doFinal(ct)
    }

    /** Comparación en tiempo constante. */
    fun ctEq(a: String, b: String): Boolean { if (a.length != b.length) return false; var r = 0; for (i in a.indices) r = r or (a[i].code xor b[i].code); return r == 0 }
}

/* ---------- Código de enlace ENC1 ---------- */
data class LinkCode(val token: String, val chatId: Any, val key: String, val db: String) {
    fun encode(): String = "ENC1." + Crypto.B64U.enc(Json.stringify(jobj("t" to token, "c" to chatId, "k" to key, "d" to db)).toByteArray(Charsets.UTF_8))
    companion object {
        fun parse(s: String): LinkCode? = try {
            val t = s.trim()
            if (!t.startsWith("ENC1.")) null else {
                val o = Json.parse(String(Crypto.B64U.dec(t.substring(5)), Charsets.UTF_8)) as JObj
                val token = o["t"] as? String; val c = o["c"]; val k = o["k"] as? String; val d = o["d"] as? String
                if (token.isNullOrEmpty() || c == null || k.isNullOrEmpty() || d.isNullOrEmpty()) null else LinkCode(token, c, k, d)
            }
        } catch (e: Exception) { null }
    }
}
