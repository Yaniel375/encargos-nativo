package com.agencia.encargos.nativo.ui

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.agencia.encargos.core.Js
import com.agencia.encargos.core.str
import java.time.LocalDate
import java.time.format.DateTimeFormatter
import java.util.Locale

@Composable
fun HomeScreen() {
    val ui = LocalUi.current; val app = ui.app; val core = app.core; val v = LocalVersion.current
    val d = app.stats.dashboard(); val ps = app.stats.payments()
    var week by rememberSaveable { mutableStateOf(true) }
    var q by rememberSaveable { mutableStateOf("") }
    Page {
        item {
            Column {
                Text("¡Hola, ${core.greetName}!", fontSize = 24.sp, fontWeight = FontWeight.ExtraBold)
                Text(LocalDate.now().format(DateTimeFormatter.ofPattern("EEEE, d 'de' MMMM 'de' yyyy", Locale.forLanguageTag("es-ES"))).replaceFirstChar { it.uppercase() }, color = MaterialTheme.colorScheme.onSurfaceVariant, fontSize = 13.sp)
            }
        }
        item { SearchBox(q, { q = it }, "Buscar clientes, encargos o notas") }
        if (q.isNotBlank()) item { GlobalResults(q) }
        item {
            SectionCard {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Gauge(ps.pct, size = 128)
                    Spacer(Modifier.width(14.dp))
                    Column(verticalArrangement = Arrangement.spacedBy(4.dp)) {
                        Text("Total cobrado", color = MaterialTheme.colorScheme.onSurfaceVariant, fontSize = 13.sp)
                        Text(money(ps.collected), fontSize = 24.sp, fontWeight = FontWeight.ExtraBold, color = C.green)
                        Text("de ${money(ps.target)} ${if (ps.goalMode == "monthly") "(meta del mes)" else "esperado"}", fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                        Text("Ver registro de pagos ›", color = C.blue, fontSize = 13.sp, modifier = Modifier.clickable { ui.go(Screen.Payments) })
                    }
                }
            }
        }
        item {
            Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) { Kpi("Clientes", "${d.clients}", modifier = Modifier.weight(1f)); Kpi("Encargos", "${d.count}", modifier = Modifier.weight(1f)); Kpi("Libras", Js.toFixed(d.pounds, 2), modifier = Modifier.weight(1f)) }
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) { Kpi("Cobrado", money(d.paid), C.green, modifier = Modifier.weight(1f)); Kpi("Pendiente", "- " + money(d.pending), C.rose, modifier = Modifier.weight(1f)); Kpi("A favor", "+ " + money(d.funds), C.teal, modifier = Modifier.weight(1f)) }
            }
        }
        item {
            val all = core.attention.compute(); val n = core.attention.badge(all)
            SectionCard("Requiere tu atención", action = { Text("Abrir ›", color = C.blue, modifier = Modifier.clickable { ui.go(Screen.Attention) }) }) {
                if (d.pendingOrders > 0) Text("🔴 ${d.pendingOrders} pedido(s) con saldo pendiente.")
                if (d.transit > 0) Text("🚚 ${d.transit} pedido(s) en tránsito.")
                if (d.received > 0) Text("📦 ${d.received} pedido(s) recibidos.")
                if (d.pendingOrders == 0 && d.transit == 0 && d.received == 0) Text("Todo está al día.", color = MaterialTheme.colorScheme.onSurfaceVariant)
                if (n > 0) Text("$n aviso(s) sin revisar en el Centro de atención", color = C.amber, fontSize = 13.sp)
            }
        }
        item {
            val items = app.stats.revenue(week); val total = items.sumOf { it.second }
            SectionCard("Ingresos") {
                Choice(listOf("w" to "Últimos 7 días", "m" to "Últimos 6 meses"), if (week) "w" else "m") { week = it == "w" }
                Text(money(total), fontSize = 22.sp, fontWeight = FontWeight.ExtraBold)
                Text("cobrado en " + if (week) "los últimos 7 días" else "los últimos 6 meses", fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                if (items.any { it.second > 0 }) Bars(items) else Text("Aún no hay pagos en este periodo.", fontSize = 13.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
        }
        item {
            SectionCard("Por estado") {
                val n = maxOf(1, core.data.size)
                d.byStatus.forEach { (s, c, t) ->
                    Row(verticalAlignment = Alignment.CenterVertically) { Text(s, Modifier.weight(1f)); Text("$c", fontWeight = FontWeight.Bold) }
                    ProgressBar(c / n.toDouble(), if (s == "Pedido") C.amber else if (s == "En tránsito") C.blue else C.green)
                    Text(money(t) + " en total", fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
            }
        }
        item {
            SectionCard("Quién me debe", action = { Text("Ver todos ›", color = C.blue, modifier = Modifier.clickable { ui.go(Screen.Pending) }) }) {
                if (d.topDebts.isEmpty()) Text("Nadie tiene saldo pendiente.", color = MaterialTheme.colorScheme.onSurfaceVariant)
                d.topDebts.forEach { (name, bal, n) ->
                    Row(Modifier.fillMaxWidth().clickable { ui.go(Screen.Client(name)) }.padding(vertical = 4.dp), verticalAlignment = Alignment.CenterVertically) {
                        Avatar(name, C.rose, 34); Spacer(Modifier.width(10.dp))
                        Column(Modifier.weight(1f)) { Text(name, fontWeight = FontWeight.Bold); Text("$n encargo(s)", fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant) }
                        Text(money(bal), color = C.rose, fontWeight = FontWeight.ExtraBold)
                    }
                }
                if (d.debtors > 5) Text("y ${d.debtors - 5} más", fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
        }
        item {
            SectionCard("Actividad reciente") {
                if (d.recent.isEmpty()) Text("Sin actividad.", color = MaterialTheme.colorScheme.onSurfaceVariant)
                d.recent.forEach { x ->
                    Column(Modifier.fillMaxWidth().clickable { ui.go(Screen.Order(Js.str(x["id"]))) }.padding(vertical = 4.dp)) {
                        Text(x.str("name"), fontWeight = FontWeight.Bold)
                        Text("${x.str("status")} · ${Js.fmt(x["updated"] ?: x["date"])} · ${money(x["total"])}", fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                    }
                }
            }
        }
    }
}

/** Buscador general: clientes, encargos y notas. */
@Composable
fun GlobalResults(q: String) {
    val ui = LocalUi.current; val core = ui.core
    val words = Js.normSearch(q).split(Regex("\\s+")).filter { it.isNotEmpty() }
    fun m(t: String): Boolean { val h = Js.normSearch(t); return words.all { h.contains(it) } }
    val cl = core.clients().filter { m(it.name + " " + it.phone) }.take(5)
    val od = core.orders().filter { m(listOf(it.str("name"), it.str("phone"), it.str("status"), it.str("note")).joinToString(" ")) }.sortedByDescending { Js.parseDate(it["date"]) }.take(5)
    val nt = core.notes().filter { m(it.str("title") + " " + it.str("body")) }.take(5)
    SectionCard {
        if (cl.isEmpty() && od.isEmpty() && nt.isEmpty()) Text("Sin resultados para \"$q\".", color = MaterialTheme.colorScheme.onSurfaceVariant)
        if (cl.isNotEmpty()) Text("Clientes", fontWeight = FontWeight.Bold, color = C.vio)
        cl.forEach { c -> Text("${c.name} · ${c.orders.size} encargo(s)" + if (c.pending > 0.005) " · debe ${money(c.pending)}" else "", Modifier.fillMaxWidth().clickable { ui.go(Screen.Client(c.name)) }.padding(vertical = 4.dp)) }
        if (od.isNotEmpty()) Text("Encargos", fontWeight = FontWeight.Bold, color = C.blue)
        od.forEach { x -> Text("${x.str("name")} · ${money(x["total"])} · ${x.str("status").ifEmpty { "Pedido" }}", Modifier.fillMaxWidth().clickable { ui.go(Screen.Order(Js.str(x["id"]))) }.padding(vertical = 4.dp)) }
        if (nt.isNotEmpty()) Text("Notas", fontWeight = FontWeight.Bold, color = C.amber)
        nt.forEach { n -> Text(n.str("title").ifEmpty { "Sin título" }, Modifier.fillMaxWidth().clickable { ui.go(Screen.Notes) }.padding(vertical = 4.dp)) }
    }
}
