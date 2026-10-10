package com.agencia.encargos.core

import java.util.concurrent.ConcurrentHashMap

/**
 * Almacenamiento clave/valor con las MISMAS claves que la app web (localStorage).
 * Android lo implementa con Room y Windows con SQLite; aquí hay uno en memoria y otro en archivo para pruebas.
 */
interface Storage {
    fun get(key: String): String?
    fun set(key: String, value: String)
    fun remove(key: String)
    fun keys(): List<String>
    /** Agrupa varias escrituras en una transacción (si el almacenamiento lo permite). */
    fun <T> transaction(block: () -> T): T = block()
}

class MemoryStorage : Storage {
    val map = ConcurrentHashMap<String, String>()
    override fun get(key: String) = map[key]
    override fun set(key: String, value: String) { map[key] = value }
    override fun remove(key: String) { map.remove(key) }
    override fun keys() = map.keys.toList()
}

/** Almacenamiento en un archivo JSON (pruebas y herramientas de línea de comandos). */
class FileStorage(private val file: java.io.File) : Storage {
    private val map: LinkedHashMap<String, String> = LinkedHashMap()
    init {
        if (file.exists()) (Json.obj(file.readText()) ?: JObj()).forEach { (k, v) -> if (v is String) map[k] = v }
    }
    @Synchronized private fun flush() { val tmp = java.io.File(file.path + ".tmp"); tmp.writeText(Json.stringify(map)); tmp.renameTo(file) }
    @Synchronized override fun get(key: String) = map[key]
    @Synchronized override fun set(key: String, value: String) { map[key] = value; flush() }
    @Synchronized override fun remove(key: String) { map.remove(key); flush() }
    @Synchronized override fun keys() = map.keys.toList()
}

/** Ayudas iguales al objeto LS de la app web. */
class LS(val s: Storage) {
    fun get(k: String, d: String? = null): String? = try { s.get(k) ?: d } catch (e: Exception) { d }
    fun set(k: String, v: String): Boolean = try { s.set(k, v); true } catch (e: Exception) { false }
    fun del(k: String) { try { s.remove(k) } catch (e: Exception) {} }
    fun json(k: String): Any? = try { s.get(k)?.let { Json.parse(it) } } catch (e: Exception) { null }
    fun obj(k: String): JObj = json(k) as? JObj ?: JObj()
    fun arr(k: String): JArr = json(k) as? JArr ?: JArr()
    fun put(k: String, v: Any?): Boolean = set(k, Json.stringify(v))
}
