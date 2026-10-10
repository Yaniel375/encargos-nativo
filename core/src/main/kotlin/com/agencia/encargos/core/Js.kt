package com.agencia.encargos.core

import java.math.BigDecimal
import java.math.RoundingMode
import java.time.Instant
import java.time.ZoneId
import java.time.ZonedDateTime
import java.time.format.DateTimeFormatter
import kotlin.math.abs
import kotlin.math.floor

/** Reglas numéricas y de texto idénticas a las de la app web (JavaScript). */
object Js {
    /** num(v) = Number(v || 0) */
    fun num(v: Any?): Double = when (v) {
        null -> 0.0
        is Double -> v
        is Number -> v.toDouble()
        is Boolean -> if (v) 1.0 else 0.0
        is String -> { val t = v.trim(); if (t.isEmpty()) 0.0 else t.toDoubleOrNull() ?: Double.NaN }
        else -> Double.NaN
    }

    /** String(v ?? '') */
    fun str(v: Any?): String = when (v) {
        null -> ""
        is String -> v
        is Number -> numberToString(v.toDouble())
        is Boolean -> v.toString()
        else -> v.toString()
    }

    fun truthy(v: Any?): Boolean = when (v) {
        null -> false
        is Boolean -> v
        is Number -> { val d = v.toDouble(); d != 0.0 && !d.isNaN() }
        is String -> v.isNotEmpty()
        else -> true
    }

    /** Math.round de JavaScript (los .5 van hacia +infinito). */
    fun round(x: Double): Double = if (x.isNaN() || x.isInfinite()) x else floor(x + 0.5)

    /** r2 de la app: Math.round(num(v)*100)/100, sin -0. */
    fun r2(v: Any?): Double { val n = round(num(v) * 100) / 100; return if (n == 0.0) 0.0 else n }

    /** +(x).toFixed(2) */
    fun toFixed2(x: Double): Double = toFixed(x, 2).toDouble()

    /** Number.prototype.toFixed */
    fun toFixed(x: Double, digits: Int): String {
        if (x.isNaN()) return "NaN"
        if (abs(x) >= 1e21) return numberToString(x)
        val neg = x < 0
        val bd = BigDecimal(abs(x)).setScale(digits, RoundingMode.HALF_UP)
        val s = bd.toPlainString()
        return if (neg && bd.signum() != 0) "-$s" else s
    }

    /** money(v) = '$' + num(v).toFixed(2) */
    fun money(v: Any?): String = "$" + toFixed(num(v), 2)

    /** Number.prototype.toString() de JavaScript. */
    fun numberToString(d: Double): String {
        if (d.isNaN()) return "NaN"
        if (d.isInfinite()) return if (d > 0) "Infinity" else "-Infinity"
        if (d == 0.0) return "0"
        val a = abs(d)
        if (a >= 1e-6 && a < 1e21) {
            if (d == floor(d) && a < 9.007199254740992E15) return d.toLong().toString()
            return BigDecimal(d.toString()).stripTrailingZeros().toPlainString()
        }
        // notación exponencial estilo JS: 1e+21, 1.5e-7
        val bd = BigDecimal(d.toString()).stripTrailingZeros()
        val unscaled = bd.unscaledValue().abs().toString()
        val exp = unscaled.length - 1 - bd.scale()
        val mant = if (unscaled.length > 1) unscaled[0] + "." + unscaled.substring(1) else unscaled
        return (if (d < 0) "-" else "") + mant + "e" + (if (exp >= 0) "+" else "-") + abs(exp)
    }

    /** new Date().toISOString() */
    fun nowIso(): String = isoOf(System.currentTimeMillis())
    fun isoOf(ms: Long): String = DateTimeFormatter.ofPattern("yyyy-MM-dd'T'HH:mm:ss.SSS'Z'").withZone(ZoneId.of("UTC")).format(Instant.ofEpochMilli(ms))

    /** new Date(s).getTime() para cadenas ISO (o milisegundos); NaN si no se entiende. */
    fun parseDate(v: Any?): Double {
        if (v == null) return Double.NaN
        if (v is Number) return v.toDouble()
        val s = v.toString().trim()
        if (s.isEmpty()) return Double.NaN
        return try { Instant.parse(s).toEpochMilli().toDouble() } catch (e: Exception) {
            try { java.time.LocalDate.parse(s.take(10)).atStartOfDay(ZoneId.of("UTC")).toInstant().toEpochMilli().toDouble() } catch (e2: Exception) { Double.NaN }
        }
    }

    fun local(ms: Double, zone: ZoneId = ZoneId.systemDefault()): ZonedDateTime = Instant.ofEpochMilli(ms.toLong()).atZone(zone)

    /** fmt(s) de la app: fecha corta y hora corta en español. */
    fun fmt(v: Any?): String {
        val t = parseDate(v); if (t.isNaN()) return "Invalid Date"
        return DateTimeFormatter.ofPattern("d/M/yy, H:mm").format(local(t))
    }

    fun normSearch(s: String): String = java.text.Normalizer.normalize(s.lowercase(), java.text.Normalizer.Form.NFD).replace(Regex("\\p{Mn}+"), "")
}
