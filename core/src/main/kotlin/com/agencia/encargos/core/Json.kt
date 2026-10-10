package com.agencia.encargos.core

/**
 * JSON con el mismo modelo que JavaScript: objetos = LinkedHashMap (conserva el orden y los
 * campos desconocidos), listas = ArrayList, números = Double, más String, Boolean y null.
 * stringify() imprime los números como JavaScript (10 y no 10.0), para que lo que publique la app
 * nativa sea idéntico a lo que publicaría la app web.
 */
typealias JObj = LinkedHashMap<String, Any?>
typealias JArr = ArrayList<Any?>

object Json {
    fun parse(text: String): Any? = Parser(text).parseTop()

    fun obj(text: String?): JObj? = try { if (text == null) null else parse(text) as? JObj } catch (e: Exception) { null }
    fun arr(text: String?): JArr? = try { if (text == null) null else parse(text) as? JArr } catch (e: Exception) { null }

    fun stringify(v: Any?, indent: Int = 0): String { val sb = StringBuilder(); write(sb, v, indent, 0); return sb.toString() }

    private fun write(sb: StringBuilder, v: Any?, indent: Int, level: Int) {
        when (v) {
            null -> sb.append("null")
            is String -> quote(sb, v)
            is Boolean -> sb.append(if (v) "true" else "false")
            is Number -> { val d = v.toDouble(); sb.append(if (d.isNaN() || d.isInfinite()) "null" else Js.numberToString(d)) }
            is Map<*, *> -> {
                if (v.isEmpty()) { sb.append("{}"); return }
                sb.append('{'); var first = true
                for ((k, value) in v) {
                    // igual que JSON.stringify: las propiedades undefined no se escriben; aquí no existen
                    if (!first) sb.append(',')
                    first = false
                    newline(sb, indent, level + 1)
                    quote(sb, k.toString()); sb.append(':'); if (indent > 0) sb.append(' ')
                    write(sb, value, indent, level + 1)
                }
                newline(sb, indent, level); sb.append('}')
            }
            is List<*> -> {
                if (v.isEmpty()) { sb.append("[]"); return }
                sb.append('['); var first = true
                for (value in v) {
                    if (!first) sb.append(',')
                    first = false
                    newline(sb, indent, level + 1)
                    write(sb, value, indent, level + 1)
                }
                newline(sb, indent, level); sb.append(']')
            }
            else -> quote(sb, v.toString())
        }
    }

    private fun newline(sb: StringBuilder, indent: Int, level: Int) {
        if (indent <= 0) return
        sb.append('\n'); repeat(indent * level) { sb.append(' ') }
    }

    fun quote(sb: StringBuilder, s: String) {
        sb.append('"')
        for (c in s) {
            when (c) {
                '"' -> sb.append("\\\"")
                '\\' -> sb.append("\\\\")
                '\n' -> sb.append("\\n")
                '\r' -> sb.append("\\r")
                '\t' -> sb.append("\\t")
                '\b' -> sb.append("\\b")
                '\u000C' -> sb.append("\\f")
                else -> if (c < ' ' || c == ' ' || c == ' ') {
                    // JSON.stringify escapa los controles; U+2028/9 los deja tal cual, pero escaparlos es válido
                    if (c < ' ') sb.append(String.format("\\u%04x", c.code)) else sb.append(c)
                } else sb.append(c)
            }
        }
        sb.append('"')
    }

    /** Copia profunda (para "snapshots" antes de cambios, igual que JSON.parse(JSON.stringify(x))). */
    @Suppress("UNCHECKED_CAST")
    fun deepCopy(v: Any?): Any? = when (v) {
        is Map<*, *> -> JObj().also { m -> v.forEach { (k, x) -> m[k.toString()] = deepCopy(x) } }
        is List<*> -> JArr().also { a -> v.forEach { a.add(deepCopy(it)) } }
        else -> v
    }

    private class Parser(val s: String) {
        var i = 0
        fun parseTop(): Any? { ws(); val v = value(); ws(); if (i != s.length) err("datos sobrantes"); return v }
        fun err(m: String): Nothing = throw IllegalArgumentException("JSON inválido ($m) en $i")
        fun ws() { while (i < s.length && (s[i] == ' ' || s[i] == '\n' || s[i] == '\r' || s[i] == '\t')) i++ }
        fun value(): Any? {
            if (i >= s.length) err("fin inesperado")
            return when (val c = s[i]) {
                '{' -> obj()
                '[' -> arr()
                '"' -> str()
                't' -> lit("true", true)
                'f' -> lit("false", false)
                'n' -> lit("null", null)
                else -> if (c == '-' || c in '0'..'9') num() else err("carácter '$c'")
            }
        }
        fun lit(w: String, v: Any?): Any? { if (!s.startsWith(w, i)) err("literal"); i += w.length; return v }
        fun obj(): JObj {
            val m = JObj(); i++; ws()
            if (i < s.length && s[i] == '}') { i++; return m }
            while (true) {
                ws(); if (i >= s.length || s[i] != '"') err("clave")
                val k = str(); ws(); if (i >= s.length || s[i] != ':') err(":"); i++; ws()
                m[k] = value(); ws()
                if (i >= s.length) err("fin")
                if (s[i] == ',') { i++; continue }
                if (s[i] == '}') { i++; return m }
                err("objeto")
            }
        }
        fun arr(): JArr {
            val a = JArr(); i++; ws()
            if (i < s.length && s[i] == ']') { i++; return a }
            while (true) {
                ws(); a.add(value()); ws()
                if (i >= s.length) err("fin")
                if (s[i] == ',') { i++; continue }
                if (s[i] == ']') { i++; return a }
                err("lista")
            }
        }
        fun str(): String {
            i++; val sb = StringBuilder()
            while (true) {
                if (i >= s.length) err("cadena sin cerrar")
                val c = s[i++]
                when (c) {
                    '"' -> return sb.toString()
                    '\\' -> {
                        if (i >= s.length) err("escape")
                        when (val e = s[i++]) {
                            '"' -> sb.append('"'); '\\' -> sb.append('\\'); '/' -> sb.append('/')
                            'b' -> sb.append('\b'); 'f' -> sb.append('\u000C'); 'n' -> sb.append('\n')
                            'r' -> sb.append('\r'); 't' -> sb.append('\t')
                            'u' -> { if (i + 4 > s.length) err("\\u"); sb.append(s.substring(i, i + 4).toInt(16).toChar()); i += 4 }
                            else -> err("escape \\$e")
                        }
                    }
                    else -> sb.append(c)
                }
            }
        }
        fun num(): Double {
            val st = i
            if (s[i] == '-') i++
            while (i < s.length && (s[i].isDigit() || s[i] == '.' || s[i] == 'e' || s[i] == 'E' || s[i] == '+' || s[i] == '-')) i++
            return s.substring(st, i).toDoubleOrNull() ?: err("número")
        }
    }
}

/* ---------- Accesos cómodos al modelo tipo JavaScript ---------- */
@Suppress("UNCHECKED_CAST")
fun Any?.asObj(): JObj? = this as? JObj
@Suppress("UNCHECKED_CAST")
fun Any?.asArr(): JArr? = this as? JArr
fun JObj.str(k: String): String = Js.str(this[k])
fun JObj.num(k: String): Double = Js.num(this[k])
fun JObj.objOrNull(k: String): JObj? = this[k] as? JObj
fun JObj.arr(k: String): JArr = (this[k] as? JArr) ?: JArr().also { this[k] = it }
fun jobj(vararg pairs: Pair<String, Any?>): JObj = JObj().also { m -> pairs.forEach { (k, v) -> m[k] = v } }
fun jarr(vararg items: Any?): JArr = JArr().also { a -> items.forEach { a.add(it) } }
@Suppress("UNCHECKED_CAST")
fun List<Any?>.objs(): List<JObj> = mapNotNull { it as? LinkedHashMap<String, Any?> }
