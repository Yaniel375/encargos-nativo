package com.agencia.encargos.nativo.ui

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.material3.AlertDialog
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
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.agencia.encargos.core.JArr
import com.agencia.encargos.core.JObj
import com.agencia.encargos.core.Js
import com.agencia.encargos.core.str
import java.time.LocalDate

/** Pago que se está editando (pedido, índice en paymentHistory). */
val editPaymentState = mutableStateOf<Pair<String, Int>?>(null)

/** Editar o eliminar un pago (mantiene lo pagado y el saldo coherentes). */
@Composable
fun PaymentEditHost() {
    val ui = LocalUi.current; val core = ui.core
    val sel = editPaymentState.value ?: return
    val x = core.byId(sel.first) ?: run { editPaymentState.value = null; return }
    val p = (x["paymentHistory"] as? JArr)?.getOrNull(sel.second) as? JObj ?: run { editPaymentState.value = null; return }
    var amt by remember(sel) { mutableStateOf(Js.numberToString(Js.num(p["amount"]))) }
    var method by remember(sel) { mutableStateOf(p.str("method").ifEmpty { "Efectivo" }) }
    var note by remember(sel) { mutableStateOf(p.str("note")) }
    AlertDialog(onDismissRequest = { editPaymentState.value = null }, title = { Text("💵 Editar pago") },
        text = { Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
            Text("Pago de ${x.str("name")} · ${Js.fmt(p["date"])}", color = MaterialTheme.colorScheme.onSurfaceVariant)
            Field(amt, { amt = it }, "Cantidad", number = true, prefix = "$")
            Choice(listOf("Efectivo" to "Efectivo", "USD" to "USD"), method) { method = it }
            Field(note, { note = it }, "Nota")
            TextButton(onClick = {
                editPaymentState.value = null
                ui.ask("🗑️ Eliminar pago", "¿Eliminar el pago de ${money(p["amount"])} de ${x.str("name")}? El saldo del encargo se ajustará.", "Eliminar", true) {
                    val snap = core.payDelete(x["id"], sel.second)
                    if (snap != null) ui.toast("Pago eliminado.") { core.restoreOrder(snap) }
                }
            }) { Text("Eliminar pago", color = C.rose) }
        } },
        confirmButton = { TextButton(onClick = { core.payEdit(x["id"], sel.second, Js.num(amt), method, note).onSuccess { editPaymentState.value = null; ui.toast("✅ Pago actualizado.") }.onFailure { ui.toast(it.message ?: "") } }) { Text("Guardar") } },
        dismissButton = { TextButton(onClick = { editPaymentState.value = null }) { Text("Cancelar") } })
}

/* ======================= Registro de pagos ======================= */
@Composable
fun PaymentsScreen() {
    val ui = LocalUi.current; val app = ui.app; val v = LocalVersion.current
    val s = app.stats.payments()
    var mf by rememberSaveable { mutableStateOf("") }; var shown by rememberSaveable { mutableStateOf(25) }; var goal by remember { mutableStateOf(false) }
    val list = s.list.filter { mf.isEmpty() || it.p.str("method").ifEmpty { "Efectivo" }.uppercase() == mf.uppercase() }
    Page {
        item {
            SectionCard(action = { Text("🎯 Meta", color = C.blue, modifier = Modifier.clickable { goal = true }) }) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Gauge(s.pct, size = 140); Spacer(Modifier.width(14.dp))
                    Column {
                        Text("Total cobrado", fontSize = 13.sp, color = MaterialTheme.colorScheme.onSurfaceVariant); Text(money(s.collected), fontSize = 24.sp, fontWeight = FontWeight.ExtraBold, color = C.green)
                        Text("de ${money(s.target)} " + if (s.goalMode == "monthly") "(meta del mes)" else "esperado", fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                        if (s.pct > 100.5) Text("+${Math.round(s.pct - 100)}% sobre la meta", color = C.green, fontSize = 12.sp)
                    }
                }
            }
        }
        item {
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                Kpi(if (s.goalMode == "monthly") "Meta del mes" else "Meta (encargos)", if (s.target > 0) money(s.target) else "Sin meta", modifier = Modifier.weight(1f))
                val over = s.collected > s.target && s.target > 0
                Kpi(if (over) "Superado" else "Restante", if (over) "+" + money(s.collected - s.target) else money(s.remaining), if (over) C.green else MaterialTheme.colorScheme.onSurface, modifier = Modifier.weight(1f))
                Kpi("Pagos", "${s.list.size}", modifier = Modifier.weight(1f))
            }
        }
        item { SectionCard("Movimientos recientes", action = { Text("${list.size} pago(s)", color = MaterialTheme.colorScheme.onSurfaceVariant, fontSize = 12.sp) }) {
            Choice(listOf("" to "Todos", "Efectivo" to "Efectivo", "USD" to "USD"), mf) { mf = it; shown = 25 }
            if (list.isEmpty()) Text("Todavía no hay pagos registrados.", color = MaterialTheme.colorScheme.onSurfaceVariant)
            list.take(shown).forEach { it2 ->
                Row(Modifier.fillMaxWidth().clickable { editPaymentState.value = Pair(it2.orderId, it2.idx) }.padding(vertical = 6.dp), verticalAlignment = Alignment.CenterVertically) {
                    Column(Modifier.weight(1f)) { Text("Pago recibido · ${it2.client}", fontWeight = FontWeight.Bold); Text("${dayLabel(it2.p["date"])} · ${it2.p.str("method").ifEmpty { "Efectivo" }}" + (if (it2.p.str("note").isNotEmpty()) " · " + it2.p.str("note") else ""), fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant) }
                    Text("+" + money(it2.p["amount"]), color = C.green, fontWeight = FontWeight.ExtraBold)
                }
            }
            if (list.size > shown) Secondary("Ver más pagos (${list.size - shown})", Modifier.fillMaxWidth()) { shown += 25 }
            if (list.isNotEmpty()) Text("Toca un pago para corregirlo o eliminarlo.", fontSize = 11.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
        } }
    }
    if (goal) GoalDialog { goal = false }
    PaymentEditHost()
    @Suppress("UNUSED_VARIABLE") val u = v
}

fun dayLabel(d: Any?): String {
    val t = Js.parseDate(d); if (t.isNaN()) return ""
    val ld = Js.local(t).toLocalDate(); val today = LocalDate.now()
    if (ld == today) return "Hoy"; if (ld == today.minusDays(1)) return "Ayer"
    val m = listOf("ene", "feb", "mar", "abr", "may", "jun", "jul", "ago", "sept", "oct", "nov", "dic")[ld.monthValue - 1]
    return "${ld.dayOfMonth.toString().padStart(2, '0')} $m" + if (ld.year != today.year) " ${ld.year}" else ""
}

@Composable
fun GoalDialog(dismiss: () -> Unit) {
    val ui = LocalUi.current; val g = ui.app.stats.goal()
    var mode by remember { mutableStateOf(g.str("mode")) }; var amt by remember { mutableStateOf(if (Js.num(g["amount"]) > 0) Js.numberToString(Js.num(g["amount"])) else "") }
    AlertDialog(onDismissRequest = dismiss, title = { Text("🎯 Meta de cobro") },
        text = { Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
            Text("El círculo se llena según lo cobrado frente a esta meta.", color = MaterialTheme.colorScheme.onSurfaceVariant)
            Choice(listOf("auto" to "Total de encargos activos", "monthly" to "Meta del mes"), mode) { mode = it }
            if (mode == "monthly") Field(amt, { amt = it }, "¿Cuánto quieres cobrar este mes?", number = true, prefix = "$")
            Text(if (mode == "monthly") "Cuenta los pagos recibidos desde el día 1 del mes." else "Compara lo cobrado con el total de todos los encargos activos.", fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
        } },
        confirmButton = { TextButton(onClick = { if (mode == "monthly" && !(Js.num(amt) > 0)) { ui.toast("Escribe una meta mayor que 0."); return@TextButton }; ui.app.stats.saveGoal(mode, Js.num(amt)); ui.toast("🎯 Meta actualizada."); dismiss() }) { Text("Guardar meta") } },
        dismissButton = { TextButton(onClick = dismiss) { Text("Cancelar") } })
}

/* ======================= Pendientes y fondos a favor ======================= */
@Composable
fun PendingScreen() {
    val ui = LocalUi.current; val app = ui.app; val core = ui.core; val v = LocalVersion.current
    val p = app.stats.pending(); var age by rememberSaveable { mutableStateOf(0) }
    val days = { x: JObj -> core.attention.daysSince(x["date"]) }
    val shown = p.pend.filter { age == 0 || days(it) >= age }
    Page {
        item { Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) { Kpi("Pendiente", "- " + money(p.pendTotal), C.rose, modifier = Modifier.weight(1f)); Kpi("A favor", "+ " + money(p.fundsTotal), C.green, modifier = Modifier.weight(1f)); Kpi("Pedidos", "${p.pend.size}", modifier = Modifier.weight(1f)) } }
        item { Text("Saldos pendientes", fontWeight = FontWeight.Bold, fontSize = 16.sp) }
        item { Choice(listOf("0" to "Todas", "15" to "+15 días (${p.pend.count { days(it) >= 15 }})", "30" to "+30 días (${p.pend.count { days(it) >= 30 }})"), age.toString()) { age = it.toInt() } }
        if (shown.isEmpty()) item { Empty("🎉 No hay saldos pendientes.") }
        shown.forEach { x -> item {
            val n = days(x)
            SectionCard(modifier = Modifier.clickable { ui.go(Screen.Order(Js.str(x["id"]))) }) {
                Row(verticalAlignment = Alignment.CenterVertically) { Avatar(x.str("name"), C.rose); Spacer(Modifier.width(10.dp))
                    Column(Modifier.weight(1f)) { Text(x.str("name"), fontWeight = FontWeight.Bold); Text("${x.str("phone").ifEmpty { "Sin teléfono" }} • ${x.str("status")}", fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant) }
                    Column(horizontalAlignment = Alignment.End) { Text("Pendiente", fontSize = 11.sp, color = MaterialTheme.colorScheme.onSurfaceVariant); Text("- " + money(x["balance"]), color = C.rose, fontWeight = FontWeight.ExtraBold) } }
                Row(horizontalArrangement = Arrangement.spacedBy(6.dp)) { Chip(core.attention.ago(x["date"]), if (n >= 30) C.rose else C.mut); Chip("Total " + money(x["total"]), C.blue) }
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    Primary("Cobrar", Modifier.weight(1f), color = C.green) { ui.whatsapp(x.str("phone"), core.reminderOrder(x), x.str("name")) }
                    Secondary("Registrar pago", Modifier.weight(1f)) { ui.go(Screen.EditOrder(Js.str(x["id"]))) }
                }
            }
        } }
        item { Text("Fondos a favor", fontWeight = FontWeight.Bold, fontSize = 16.sp) }
        if (p.funds.isEmpty()) item { Empty("No hay fondos a favor.") }
        p.funds.forEach { x -> item {
            SectionCard(modifier = Modifier.clickable { ui.go(Screen.Order(Js.str(x["id"]))) }) {
                Row(verticalAlignment = Alignment.CenterVertically) { Avatar(x.str("name"), C.green); Spacer(Modifier.width(10.dp)); Column(Modifier.weight(1f)) { Text(x.str("name"), fontWeight = FontWeight.Bold); Text(x.str("phone").ifEmpty { "Sin teléfono" }, fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant) }
                    Text("+ " + money(-Js.num(x["balance"])), color = C.green, fontWeight = FontWeight.ExtraBold) }
                Text("🟢 Este importe queda a favor del cliente.", fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
        } }
    }
    @Suppress("UNUSED_VARIABLE") val u = v
}
