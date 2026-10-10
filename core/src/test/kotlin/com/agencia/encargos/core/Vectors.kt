package com.agencia.encargos.core

import java.io.File

/** Pruebas de conformidad: vectores calculados con el código de la app web. */
object Vectors {
    var fails = 0
    fun check(name: String, ok: Boolean, info: Any? = null) { println((if (ok) "✅ " else "❌ ") + name + (if (info != null) " · $info" else "")); if (!ok) fails++ }

    @JvmStatic fun main(args: Array<String>) {
        check("sha256hex('abc')", Crypto.sha256hex("abc") == "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad")
        check("detUuid order", Crypto.detUuid("order|1696000000000|2026-10-01T12:00:00.000Z|Juan Pérez") == "fb2e151c-cd34-5037-9cbd-ca8e3952a188")
        check("detUuid client", Crypto.detUuid("client|juan pérez") == "08e04d0e-2d06-5d30-88a2-0d9e5f97cf85")
        check("detUuid setting", Crypto.detUuid("setting|templates") == "aeb4a149-b04f-5d9a-8a10-0e17a363b4df")
        check("dbTag", Crypto.dbTag("2d8c3068-a188-40fd-845d-c18d4ed0599e") == "43bb1b1aae3e13e0")
        check("detUuid pay (Number(10.5))", Crypto.detUuid("pay|abc|1696|2026-10-01T12:00:00.000Z|" + Js.numberToString(10.5) + "|0") == "632accfb-bbc3-5111-8ecf-7a599c119b20")
        // Números como JavaScript
        val nums = mapOf(10.0 to "10", 10.5 to "10.5", 0.1 + 0.2 to "0.30000000000000004", 1e21 to "1e+21", 1.5e-7 to "1.5e-7", -3.0 to "-3", 1791554830000.0 to "1791554830000", 123456.789 to "123456.789")
        nums.forEach { (d, s) -> check("numberToString($d)", Js.numberToString(d) == s, Js.numberToString(d)) }
        check("r2(1.005)", Js.r2(1.005) == 1.0 || Js.r2(1.005) == 1.01, Js.r2(1.005))
        check("r2(-0.001) sin -0", Js.r2(-0.001).toString() == "0.0")
        check("toFixed(2.345,2)", Js.toFixed(2.345, 2) == "2.35" || Js.toFixed(2.345, 2) == "2.34", Js.toFixed(2.345, 2))
        check("money", Js.money(3.5) == "$3.50")
        // JSON ida y vuelta conservando campos desconocidos y orden
        val src = """{"b":1,"a":{"x":[1,2.5,"ñ"],"y":null},"campoNuevo":true}"""
        check("JSON ida y vuelta", Json.stringify(Json.parse(src)) == src, Json.stringify(Json.parse(src)))
        // ENCv1: archivo de muestra hecho con WebCrypto + CompressionStream
        val f = File(args.getOrElse(0) { "src/test/resources/fixtures/sample.enc" }.let { if (it == "none") "src/test/resources/fixtures/sample.enc" else it })
        if (f.exists()) {
            val key = Crypto.B64U.dec(File(f.path.replace(".enc", ".key")).readText())
            val o = Crypto.unseal(f.readBytes(), key, "2d8c3068-a188-40fd-845d-c18d4ed0599e") as JObj
            check("Descifra ENCv1 creado por la app web", ((o["operations"] as JArr)[0] as JObj).obj("data")?.str("name") == "Juan Pérez")
            check("AAD distinto se rechaza", try { Crypto.unseal(f.readBytes(), key, "otra"); false } catch (e: Exception) { true })
        }
        val key = Crypto.random(32)
        val sealed = Crypto.seal(jobj("hola" to "mundo ñ"), key, "db1")
        check("ENCv1 propio: cabecera ENCv1Z", String(sealed, 0, 6) == "ENCv1Z")
        check("ENCv1 propio ida y vuelta", (Crypto.unseal(sealed, key, "db1") as JObj)["hola"] == "mundo ñ")
        File("build").mkdirs(); File("build/kotlin_sample.enc").writeBytes(sealed); File("build/kotlin_sample.key").writeText(Crypto.B64U.enc(key))
        // ENC1
        val lc = LinkCode("123456789:AAEncargosTestTokenXXXXXXXXXXXXXXXX", 777000111.0, Crypto.B64U.enc(key), "db-1")
        val back = LinkCode.parse(lc.encode())
        check("ENC1 ida y vuelta", back != null && back.token == lc.token && Js.num(back.chatId) == 777000111.0 && back.db == "db-1")
        check("ENC1 inválido", LinkCode.parse("ENC1.xxx") == null && LinkCode.parse("hola") == null)
        // PBKDF2 = WebCrypto (vector RFC 7914 / conocido para SHA-256)
        check("PBKDF2-SHA256", Crypto.hex(Crypto.pbkdf2("password", "salt".toByteArray(), 1, 32)) == "120fb6cffcf8b32c43e7225256c4f837a86548c92ccc35480805987cb70be17b")
        // Texto del mensaje fijado
        val sync = TelegramSync(Encargos(MemoryStorage()), MemorySecrets(), "http://test")
        val man = jobj("type" to "ENCARGOS_BACKUP_LATEST", "version" to 7.0, "packageId" to "p", "deviceId" to "PC-1", "timestamp" to "2026-10-09T14:00:00.000Z", "checksum" to "c", "telegramFileId" to "F", "messageId" to 5.0, "size" to 10.0, "schemaVersion" to 1.0, "db" to "x")
        val txt = sync.manText(man)
        check("Mensaje fijado: formato", txt.startsWith("📦 Última sincronización\nVersión 7\n") && txt.contains("\n#ENCARGOS_BACKUP_LATEST\n{\"type\":\"ENCARGOS_BACKUP_LATEST\",\"version\":7,"), txt.lines().take(2))
        check("Mensaje fijado: lectura", sync.parseMan(txt)?.get("packageId") == "p")
        // Cálculos
        val app = Encargos(MemoryStorage())
        val x = app.createOrder(Encargos.NewOrder("Ana", "55551234", 45.0, 7.0, 2.0, 20.0)).getOrThrow()
        check("Encargo: envío 14, total 59, saldo 39", Js.num(x["shipping"]) == 14.0 && Js.num(x["total"]) == 59.0 && Js.num(x["balance"]) == 39.0)
        app.saveEdit(x["id"], 1.5, 10.0, "USD", "abono", "En tránsito")
        check("Editar: libras 3.5, pagado 30, saldo 39.5", Js.num(x["pounds"]) == 3.5 && Js.num(x["paid"]) == 30.0 && Js.num(x["balance"]) == 39.5, Json.stringify(jarr(x["pounds"], x["paid"], x["balance"])))
        check("statusDates registra En tránsito", (x["statusDates"] as JObj).containsKey("En tránsito"))
        check("Cliente pendiente 39.5", app.clients()[0].pending == 39.5)
        check("waNumber agrega código de país", app.waNumber("5555 1234") == "5355551234")
        check("Notificaciones generadas", app.notifs.all().any { it["type"] == "pay_new" })
        check("Atención: alerta de pago", app.attention.compute().any { it.cat == "pay" })
        val bk = Backup(app); val enc = bk.exportEncrypted("secreto1")
        val dec = bk.decrypt(Json.parse(enc) as JObj, "secreto1") as JObj
        check("Respaldo cifrado ida y vuelta", ((dec["data"] as JArr)[0] as JObj)["name"] == "Ana")
        println(if (fails == 0) "\nTODO CORRECTO" else "\nFALLOS: $fails")
        if (fails > 0) System.exit(1)
    }
}
fun JObj.obj(k: String): JObj? = this[k] as? JObj
