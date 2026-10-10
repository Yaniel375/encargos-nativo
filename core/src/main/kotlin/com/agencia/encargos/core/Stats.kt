package com.agencia.encargos.core

import java.time.DayOfWeek
import java.time.LocalDate
import java.time.ZoneId
import java.time.format.TextStyle
import java.util.Locale
import kotlin.math.abs
import kotlin.math.max

/** Paneles numéricos: Inicio, Pendientes, Registro de pagos y Estadísticas (mismos cálculos que la app web). */
class Stats(private val app: Encargos, private val zone: ZoneId = ZoneId.systemDefault()) {
    private val es = Locale("es", "ES")
    private fun t(v: Any?) = Js.parseDate(v)
    private fun ms(d: LocalDate) = d.atStartOfDay(zone).toInstant().toEpochMilli().toDouble()

    data class Dashboard(val clients: Int, val count: Int, val pounds: Double, val paid: Double, val pending: Double, val funds: Double,
                         val pendingOrders: Int, val transit: Int, val received: Int, val recent: List<JObj>,
                         val byStatus: List<Triple<String, Int, Double>>, val topDebts: List<Triple<String, Double, Int>>, val debtors: Int)
    fun dashboard(): Dashboard {
        val ad = app.activeOrders()
        val pend = ad.filter { Js.num(it["balance"]) > 0 }
        val recent = ad.sortedByDescending { t(it["updated"] ?: it["date"]).let { v -> if (v.isNaN()) 0.0 else v } }.take(5)
        val states = listOf("Pedido" to "Pedido", "En tránsito" to "En tránsito", "Entregados" to "Entregado")
        val byStatus = states.map { (label, st) -> val g = ad.filter { it["status"] == st }; Triple(label, g.size, g.sumOf { Js.num(it["total"]) }) }
        val debt = LinkedHashMap<String, Triple<String, Double, Int>>()
        pend.forEach { x -> val k = x.str("name").trim().lowercase(); val d = debt[k]; debt[k] = Triple(x.str("name"), (d?.second ?: 0.0) + Js.num(x["balance"]), (d?.third ?: 0) + 1) }
        val debts = debt.values.sortedByDescending { it.second }
        return Dashboard(app.activeClients().size, app.data.size, ad.sumOf { Js.num(it["pounds"]) }, ad.sumOf { Js.num(it["paid"]) },
            ad.sumOf { max(0.0, Js.num(it["balance"])) }, ad.sumOf { max(0.0, -Js.num(it["balance"])) }, pend.size,
            ad.count { it["status"] == "En tránsito" }, ad.count { it["status"] == "Recibido" }, recent, byStatus, debts.take(5), debts.size)
    }

    /** Gráfica de ingresos: últimos 7 días (hora local) o últimos 6 meses. */
    fun revenue(week: Boolean): List<Pair<String, Double>> {
        val pays = app.orders().flatMap { (it["paymentHistory"] as? JArr)?.objs() ?: emptyList() }
        return if (week) {
            val names = listOf("Dom", "Lun", "Mar", "Mié", "Jue", "Vie", "Sáb")
            val totals = HashMap<LocalDate, Double>()
            pays.forEach { p -> val m = t(p["date"]); if (!m.isNaN()) { val d = Js.local(m, zone).toLocalDate(); totals[d] = (totals[d] ?: 0.0) + Js.num(p["amount"]) } }
            (6 downTo 0).map { i -> val d = LocalDate.now(zone).minusDays(i.toLong()); (names[d.dayOfWeek.value % 7] + " " + d.dayOfMonth) to (totals[d] ?: 0.0) }
        } else {
            val months = listOf("Ene", "Feb", "Mar", "Abr", "May", "Jun", "Jul", "Ago", "Sep", "Oct", "Nov", "Dic")
            val base = LocalDate.now(zone).withDayOfMonth(1)
            (5 downTo 0).map { i -> val m = base.minusMonths(i.toLong())
                // monthsBack usa toISOString().slice(0,7) de la medianoche local del día 1 (igual que la web)
                val k2 = Js.isoOf(ms(m).toLong()).take(7)
                val lbl = k2.split("-").let { months[it[1].toInt() - 1] + " " + it[0].takeLast(2) }
                lbl to pays.filter { Js.str(it["date"]).take(7) == k2 }.sumOf { Js.num(it["amount"]) }
            }
        }
    }

    data class Pending(val pend: List<JObj>, val funds: List<JObj>, val pendTotal: Double, val fundsTotal: Double)
    fun pending(): Pending {
        val ad = app.activeOrders()
        val pend = ad.filter { Js.num(it["balance"]) > 0 }.sortedByDescending { Js.num(it["balance"]) }
        val fund = ad.filter { Js.num(it["balance"]) < 0 }.sortedByDescending { Js.num(it["balance"]) }
        return Pending(pend, fund, pend.sumOf { Js.num(it["balance"]) }, fund.sumOf { -Js.num(it["balance"]) })
    }

    /* ---------- Registro de pagos ---------- */
    data class PayItem(val orderId: String, val idx: Int, val client: String, val p: JObj)
    data class PaySummary(val goalMode: String, val goalAmount: Double, val list: List<PayItem>, val collected: Double, val target: Double, val pct: Double, val remaining: Double)
    fun goal(): JObj { val g = app.ls.json("encargos_pay_goal") as? JObj; return if (g != null && (g["mode"] == "auto" || g["mode"] == "monthly")) g else jobj("mode" to "auto", "amount" to 0.0) }
    fun saveGoal(mode: String, amount: Double) { app.ls.put("encargos_pay_goal", jobj("mode" to mode, "amount" to amount)); app.changed(false) }
    fun payments(): PaySummary {
        val g = goal(); val monthly = g["mode"] == "monthly"
        val src = if (monthly) app.orders() else app.activeOrders()
        val now = LocalDate.now(zone); val from = ms(now.withDayOfMonth(1)); val to = ms(now.withDayOfMonth(1).plusMonths(1))
        val list = ArrayList<PayItem>()
        src.forEach { x -> (x["paymentHistory"] as? JArr)?.forEachIndexed { idx, p -> val pp = p as? JObj ?: return@forEachIndexed
            if (monthly) { val tt = t(pp["date"]); if (!(tt >= from && tt < to)) return@forEachIndexed }
            list.add(PayItem(Js.str(x["id"]), idx, x.str("name"), pp)) } }
        list.sortByDescending { t(it.p["date"]).let { v -> if (v.isNaN()) 0.0 else v } }
        val collected = list.sumOf { Js.num(it.p["amount"]) }
        val target = if (monthly) Js.num(g["amount"]) else app.activeOrders().sumOf { max(0.0, Js.num(it["total"])) }
        return PaySummary(Js.str(g["mode"]), Js.num(g["amount"]), list, collected, target, if (target > 0) collected / target * 100 else 0.0, max(0.0, target - collected))
    }

    /* ---------- Estadísticas por periodo ---------- */
    enum class Period { WEEK, MONTH, YEAR }
    fun range(p: Period, ref: LocalDate): Pair<LocalDate, LocalDate> = when (p) {
        Period.WEEK -> { val a = ref.minusDays((ref.dayOfWeek.value - 1).toLong()); a to a.plusDays(7) }
        Period.YEAR -> LocalDate.of(ref.year, 1, 1).let { it to it.plusYears(1) }
        Period.MONTH -> ref.withDayOfMonth(1).let { it to it.plusMonths(1) }
    }
    fun shift(p: Period, ref: LocalDate, k: Int): LocalDate = when (p) { Period.WEEK -> ref.plusDays(7L * k); Period.YEAR -> ref.plusYears(k.toLong()); Period.MONTH -> ref.plusMonths(k.toLong()) }
    private fun inR(v: Any?, a: LocalDate, b: LocalDate): Boolean { val x = t(v); return x >= ms(a) && x < ms(b) }

    data class R(val ord: List<JObj>, val cobrado: Double, val ventas: Double, val lb: Double, val gan: Double, val gastos: Double) { val neta get() = gan - gastos }
    fun calc(a: LocalDate, b: LocalDate): R {
        val ord = app.orders().filter { it["status"] != "Cancelado" && inR(it["date"], a, b) }
        var cobrado = 0.0; app.orders().forEach { x -> (x["paymentHistory"] as? JArr)?.forEach { p -> val pp = p as? JObj ?: return@forEach; if (inR(pp["date"], a, b)) cobrado += Js.num(pp["amount"]) } }
        val gastos = app.expenses().filter { inR(it["date"], a, b) }.sumOf { Js.num(it["amount"]) }
        return R(ord, cobrado, ord.sumOf { Js.num(it["total"]) }, ord.sumOf { Js.num(it["pounds"]) }, ord.sumOf { app.profit(it) }, gastos)
    }
    /** pct(n,o): null = "nuevo" */
    fun pct(n: Double, o: Double): Double? = if (o == 0.0) (if (n > 0) null else 0.0) else (n - o) / abs(o) * 100

    fun label(p: Period, a: LocalDate, b: LocalDate): String = when (p) {
        Period.WEEK -> "${a.dayOfMonth} ${a.month.getDisplayName(TextStyle.SHORT, es).trimEnd('.')} – ${b.minusDays(1).dayOfMonth} ${b.minusDays(1).month.getDisplayName(TextStyle.SHORT, es).trimEnd('.')}"
        Period.YEAR -> a.year.toString()
        Period.MONTH -> a.month.getDisplayName(TextStyle.FULL_STANDALONE, es).replaceFirstChar { it.uppercase() } + " de " + a.year
    }
    fun buckets(p: Period, a: LocalDate, b: LocalDate): List<Pair<String, Double>> {
        val ks = ArrayList<Triple<String, LocalDate, LocalDate>>()
        when (p) {
            Period.YEAR -> for (m in 1..12) { val s = LocalDate.of(a.year, m, 1); ks.add(Triple(s.month.getDisplayName(TextStyle.NARROW_STANDALONE, es).uppercase(), s, s.plusMonths(1))) }
            Period.WEEK -> for (i in 0..6) { val s = a.plusDays(i.toLong()); ks.add(Triple(s.dayOfWeek.getDisplayName(TextStyle.NARROW_STANDALONE, es).uppercase(), s, s.plusDays(1))) }
            Period.MONTH -> { var s = a; var w = 1; while (s < b) { val e = s.plusDays(7); ks.add(Triple("S" + w++, s, if (e < b) e else b)); s = e } }
        }
        val pays = app.orders().flatMap { (it["paymentHistory"] as? JArr)?.objs() ?: emptyList() }
        return ks.map { (l, s, e) -> l to pays.filter { inR(it["date"], s, e) }.sumOf { Js.num(it["amount"]) } }
    }
    fun topClients(r: R): List<Triple<String, Double, Int>> {
        val by = LinkedHashMap<String, Triple<String, Double, Int>>()
        r.ord.forEach { x -> val k = x.str("name").trim(); val o = by[k]; by[k] = Triple(k, (o?.second ?: 0.0) + Js.num(x["total"]), (o?.third ?: 0) + 1) }
        return by.values.sortedByDescending { it.second }.take(5)
    }
    fun inactiveClients(): List<Pair<Encargos.Client, Double>> = app.clients().map { c -> c to (c.orders.maxOfOrNull { t(it["date"]).let { v -> if (v.isNaN()) Double.NEGATIVE_INFINITY else v } } ?: Double.NEGATIVE_INFINITY) }
        .filter { System.currentTimeMillis() - it.second > 30 * 86400000.0 }.sortedBy { it.second }.take(5)
    fun expensesIn(a: LocalDate, b: LocalDate) = app.expenses().filter { inR(it["date"], a, b) }.sortedByDescending { t(it["date"]) }

    /** Panel mensual antiguo (agrupa por la fecha UTC, como la app web). */
    fun monthly(ym: String): Triple<List<JObj>, List<PayItem>, Double> {
        val orders = app.activeOrders().filter { Js.str(it["date"]).take(7) == ym }
        val pays = ArrayList<PayItem>(); app.orders().forEach { x -> (x["paymentHistory"] as? JArr)?.forEachIndexed { i, p -> val pp = p as? JObj ?: return@forEachIndexed; if (Js.str(pp["date"]).take(7) == ym) pays.add(PayItem(Js.str(x["id"]), i, x.str("name"), pp)) } }
        return Triple(orders, pays, pays.sumOf { Js.num(it.p["amount"]) })
    }
    @Suppress("unused") private val dow = DayOfWeek.MONDAY
}
