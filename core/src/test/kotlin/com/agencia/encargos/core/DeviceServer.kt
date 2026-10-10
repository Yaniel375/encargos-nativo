package com.agencia.encargos.core

import com.sun.net.httpserver.HttpServer
import java.io.File
import java.net.InetSocketAddress

/** Un "dispositivo nativo" controlable por HTTP para las pruebas cruzadas con la app web. */
object DeviceServer {
    lateinit var app: Encargos; lateinit var sync: TelegramSync
    lateinit var dir: File; lateinit var apiBase: String; var platform = "ANDROID"

    class FileSecrets(private val f: File) : Secrets {
        private fun all() = Json.obj(if (f.exists()) f.readText() else null) ?: JObj()
        override fun get(name: String) = all()[name] as? String
        override fun set(name: String, value: String) { val a = all(); a[name] = value; f.writeText(Json.stringify(a)) }
        override fun remove(name: String) { val a = all(); a.remove(name); f.writeText(Json.stringify(a)) }
    }
    fun boot() {
        app = Encargos(FileStorage(File(dir, "storage.json")), platform)
        sync = TelegramSync(app, FileSecrets(File(dir, "secrets.json")), apiBase)
        app.onPersist = { /* las pruebas llaman a /sync explícitamente */ }
    }

    @JvmStatic fun main(args: Array<String>) {
        val port = args[0].toInt(); apiBase = args[1]; dir = File(args[2]).apply { mkdirs() }; platform = args.getOrElse(3) { "ANDROID" }
        boot()
        val srv = HttpServer.create(InetSocketAddress("127.0.0.1", port), 0)
        srv.createContext("/") { ex ->
            val path = ex.requestURI.path
            val body = ex.requestBody.readBytes().toString(Charsets.UTF_8)
            val p = if (body.isNotBlank()) Json.parse(body) as JObj else JObj()
            fun byName(n: String) = app.orders().first { it.str("name") == n }
            val res: Any? = try {
                when (path) {
                    "/connect" -> { val r = sync.connect(p.str("token")); r::class.simpleName + ((r as? TelegramSync.ConnectResult.Error)?.msg ?: "") }
                    "/fresh" -> sync.freshStart()
                    "/link" -> { val r = sync.link(p.str("code")); r::class.simpleName + ((r as? TelegramSync.ConnectResult.Error)?.msg ?: "") }
                    "/code" -> sync.linkCode()
                    "/order" -> { app.createOrder(Encargos.NewOrder(p.str("name"), p.str("phone").ifEmpty { "5511" }, Js.num(p["cost"]), Js.num(p["lbCost"] ?: 7.0), Js.num(p["lb"] ?: 1.0), Js.num(p["fund"]), status = p.str("status").ifEmpty { "Pedido" })).getOrThrow(); "ok" }
                    "/pay" -> { val x = byName(p.str("name")); app.saveEdit(x["id"], 0.0, Js.num(p["amt"]), p.str("method").ifEmpty { "Efectivo" }, "", x.str("status")).getOrThrow(); "ok" }
                    "/lb" -> { val x = byName(p.str("name")); app.saveEdit(x["id"], Js.num(p["lb"]), 0.0, "Efectivo", "", x.str("status")).getOrThrow(); "ok" }
                    "/advance" -> app.advance(byName(p.str("name"))["id"])
                    "/deliver" -> app.deliver(byName(p.str("name"))["id"])
                    "/delete" -> app.removeOrder(byName(p.str("name"))["id"]) != null
                    "/paydel" -> app.payDelete(byName(p.str("name"))["id"], Js.num(p["idx"]).toInt()) != null
                    "/field" -> { val x = byName(p.str("name")); x[p.str("field")] = p["value"]; x["updated"] = Js.nowIso(); app.persist() }
                    "/note" -> { app.saveNote(null, p.str("title"), p.str("body"), Js.num(p["amount"]), "blue", false, emptyList()).getOrThrow(); "ok" }
                    "/clientinfo" -> app.saveClientInfo(p.str("name"), p.str("address"), p.str("note"))
                    "/templates" -> app.saveTemplates(p.str("arrival"), p.str("reminder"), p.str("thanks"))
                    "/collect" -> sync.collect()
                    "/sync" -> { sync.collect(); val ok = sync.syncNowBlocking(); jobj("ok" to ok, "err" to sync.lastErr, "state" to sync.state) }
                    "/backup" -> sync.backupNow()
                    "/restore" -> sync.restore().exceptionOrNull()?.message ?: "ok"
                    "/restart" -> { boot(); "ok" }
                    "/state" -> jobj("orders" to JArr(app.orders().map { x -> jarr(x["uid"], x["name"], x["status"], x["pounds"], x["total"], x["paid"], x["balance"]) }.sortedBy { Json.stringify(it) }),
                        "full" to app.data, "notes" to JArr(app.notes()), "clientInfo" to app.ls.obj("encargos_client_info"), "templates" to app.templates(),
                        "pending" to sync.pending(), "meta" to sync.metaInfo(), "status" to sync.status().text, "conflicts" to sync.conflicts().size, "notifs" to JArr(app.notifs.all().map { it["title"] }))
                    else -> "?"
                }
            } catch (e: Throwable) { jobj("error" to (e.message ?: e.toString())) }
            val out = Json.stringify(jobj("r" to res)).toByteArray(Charsets.UTF_8)
            ex.responseHeaders.add("Content-Type", "application/json"); ex.sendResponseHeaders(200, out.size.toLong()); ex.responseBody.use { it.write(out) }
        }
        srv.start(); println("device listo en $port")
    }
}
