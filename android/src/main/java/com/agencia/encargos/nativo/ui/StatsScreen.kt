package com.agencia.encargos.nativo.ui

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.KeyboardArrowLeft
import androidx.compose.material.icons.automirrored.filled.KeyboardArrowRight
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.agencia.encargos.core.Encargos
import com.agencia.encargos.core.JObj
import com.agencia.encargos.core.Js
import com.agencia.encargos.core.Stats
import com.agencia.encargos.core.str
import java.time.LocalDate
import kotlin.math.abs

@Composable
fun StatsScreen() {
    val ui = LocalUi.current; val st = ui.app.stats; val core = ui.core; val v = LocalVersion.current
    var period by rememberSaveable { mutableStateOf("MONTH") }; var ref by rememberSaveable { mutableStateOf(LocalDate.now().toString()) }
    var exp by remember { mutableStateOf<JObj?>(null) }; var newExp by remember { mutableStateOf(false) }
    val p = Stats.Period.valueOf(period); val r0 = LocalDate.parse(ref)
    val (a, b) = st.range(p, r0); val (pa, pb) = st.range(p, st.shift(p, r0, -1))
    val cur = st.calc(a, b); val prev = st.calc(pa, pb)
    val isNow = LocalDate.now().let { !it.isBefore(a) && it.isBefore(b) }
    @Composable fun delta(n: Double, o: Double) { val d = st.pct(n, o)
        Text(if (d == null) "▲ nuevo" else if (abs(d) < 0.5) "= igual que antes" else (if (d > 0) "▲ " else "▼ ") + Js.toFixed(abs(d), 0) + "% vs anterior", fontSize = 11.sp, color = if (d == null || d > 0) C.green else if (abs(d) < 0.5) MaterialTheme.colorScheme.onSurfaceVariant else C.rose) }
    @Composable fun kpi(label: String, value: String, color: Color, n: Double, o: Double, m: Modifier) {
        Column(m) { Kpi(label, value, color, modifier = Modifier.fillMaxWidth()); delta(n, o) } }
    Page(right = "${cur.ord.size} encargo${if (cur.ord.size == 1) "" else "s"}") {
        item { Choice(listOf("WEEK" to "Semana", "MONTH" to "Mes", "YEAR" to "Año"), period) { period = it; ref = LocalDate.now().toString() } }
        item {
            Row(verticalAlignment = Alignment.CenterVertically) {
                IconButton(onClick = { ref = st.shift(p, r0, -1).toString() }) { Icon(Icons.AutoMirrored.Filled.KeyboardArrowLeft, "Anterior") }
                Text(st.label(p, a, b) + if (isNow) " · actual" else "", Modifier.weight(1f), textAlign = TextAlign.Center, fontWeight = FontWeight.Bold)
                IconButton(onClick = { ref = st.shift(p, r0, 1).toString() }, enabled = !isNow) { Icon(Icons.AutoMirrored.Filled.KeyboardArrowRight, "Siguiente") }
            }
        }
        item {
            SectionCard {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Gauge(if (cur.ventas > 0) cur.cobrado / cur.ventas * 100 else if (cur.cobrado > 0) 100.0 else 0.0, size = 120); Spacer(Modifier.width(14.dp))
                    Column { Text("Dinero cobrado", fontSize = 13.sp, color = MaterialTheme.colorScheme.onSurfaceVariant); Text(money(countUp(cur.cobrado)), fontSize = 22.sp, fontWeight = FontWeight.ExtraBold, color = C.green); delta(cur.cobrado, prev.cobrado); Text("de ${money(cur.ventas)} vendido", fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant) }
                }
                Bars(st.buckets(p, a, b), height = 120)
            }
        }
        item {
            Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) { kpi("Ventas", money(countUp(cur.ventas)), C.blue, cur.ventas, prev.ventas, Modifier.weight(1f)); kpi("Encargos", "${countUp(cur.ord.size.toDouble()).toInt()}", C.vio, cur.ord.size.toDouble(), prev.ord.size.toDouble(), Modifier.weight(1f)) }
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) { kpi("Libras", Js.toFixed(countUp(cur.lb), 1) + " lb", C.amber, cur.lb, prev.lb, Modifier.weight(1f)); kpi("Ganancia", (if (cur.gan < 0) "-" else "") + money(abs(cur.gan)), if (cur.gan >= 0) C.green else C.rose, cur.gan, prev.gan, Modifier.weight(1f)) }
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) { kpi("Gastos", money(countUp(cur.gastos)), C.rose, cur.gastos, prev.gastos, Modifier.weight(1f)); kpi("Ganancia neta", (if (cur.neta < 0) "-" else "") + money(abs(cur.neta)), if (cur.neta >= 0) C.green else C.rose, cur.neta, prev.neta, Modifier.weight(1f)) }
            }
        }
        item {
            SectionCard("Mejores clientes") {
                val top = st.topClients(cur); val mx = top.maxOfOrNull { it.second } ?: 1.0
                if (top.isEmpty()) Text("Sin encargos en este periodo.", color = MaterialTheme.colorScheme.onSurfaceVariant)
                top.forEach { (n, t, c) -> Text(n, fontWeight = FontWeight.Bold); Text("${money(t)} · $c encargo${if (c == 1) "" else "s"}", fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant); ProgressBar(t / mx, C.vio) }
            }
        }
        item {
            SectionCard("Gastos del periodo", action = { Text("+ Agregar", color = C.blue, modifier = Modifier.clickable { newExp = true }) }) {
                val ex = st.expensesIn(a, b)
                if (ex.isEmpty()) Text("Anota transporte, aduana, empaques y otros gastos para ver tu ganancia neta real.", fontSize = 13.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                ex.forEach { e -> Row(Modifier.fillMaxWidth().clickable { exp = e }.padding(vertical = 4.dp), verticalAlignment = Alignment.CenterVertically) {
                    Column(Modifier.weight(1f)) { Text(e.str("concept"), fontWeight = FontWeight.Bold); Text((Encargos.EXP_CATS.firstOrNull { it.first == e.str("cat") }?.second ?: "Otros") + " · " + dayLabel(e["date"]), fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant) }
                    Text("-" + money(e["amount"]), color = C.rose, fontWeight = FontWeight.Bold) } }
            }
        }
        item {
            SectionCard("Clientes inactivos") {
                val ina = st.inactiveClients()
                if (ina.isEmpty()) Text("Todos tus clientes compraron en los últimos 30 días. 👏", color = MaterialTheme.colorScheme.onSurfaceVariant)
                ina.forEach { (c, last) -> Row(verticalAlignment = Alignment.CenterVertically) {
                    Avatar(c.name, C.mut, 34); Spacer(Modifier.width(8.dp)); Column(Modifier.weight(1f)) { Text(c.name, fontWeight = FontWeight.Bold); Text("Última compra " + core.attention.ago(last), fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant) }
                    TextButton(onClick = { ui.whatsapp(c.phone, "", c.name) }) { Text("Escribir") } } }
            }
        }
    }
    if (newExp) ExpenseDialog(null) { newExp = false }
    exp?.let { e -> ExpenseDialog(e) { exp = null } }
    @Suppress("UNUSED_VARIABLE") val u = v
}

@Composable
fun ExpenseDialog(e: JObj?, dismiss: () -> Unit) {
    val ui = LocalUi.current; val core = ui.core
    var concept by remember { mutableStateOf(e?.str("concept") ?: "") }
    var amount by remember { mutableStateOf(e?.let { Js.numberToString(Js.num(it["amount"])) } ?: "") }
    var date by remember { mutableStateOf(e?.let { Js.local(Js.parseDate(it["date"])).toLocalDate().toString() } ?: LocalDate.now().toString()) }
    var cat by remember { mutableStateOf(e?.str("cat")?.ifEmpty { null } ?: "transporte") }
    AlertDialog(onDismissRequest = dismiss, title = { Text(if (e == null) "🧾 Nuevo gasto" else "🧾 Editar gasto") },
        text = { Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
            Field(concept, { concept = it }, "Concepto (ej. Taxi al aeropuerto)")
            Field(amount, { amount = it }, "Monto", number = true, prefix = "$")
            Field(date, { date = it }, "Fecha (AAAA-MM-DD)")
            Choice(Encargos.EXP_CATS, cat) { cat = it }
            if (e != null) TextButton(onClick = { ui.ask("🧾 Eliminar gasto", "¿Eliminar este gasto?", "Eliminar", true) { core.deleteExpense(e.str("id")); ui.toast("Gasto eliminado."); dismiss() } }) { Text("Eliminar", color = C.rose) }
        } },
        confirmButton = { TextButton(onClick = { core.saveExpense(e?.str("id"), concept, Js.num(amount), date, cat).onSuccess { ui.toast("✅ Gasto guardado."); dismiss() }.onFailure { ui.toast(it.message ?: "") } }) { Text("Guardar gasto") } },
        dismissButton = { TextButton(onClick = dismiss) { Text("Cancelar") } })
}
