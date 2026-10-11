package com.agencia.encargos.nativo.ui

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateMapOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.agencia.encargos.core.Attention
import com.agencia.encargos.core.JObj
import com.agencia.encargos.core.Js
import com.agencia.encargos.core.str
import java.time.LocalDate
import androidx.compose.material.icons.outlined.AccountBalanceWallet
import androidx.compose.material.icons.outlined.ErrorOutline
import androidx.compose.material.icons.outlined.History
import androidx.compose.material.icons.outlined.Info
import androidx.compose.material.icons.outlined.Inventory2
import androidx.compose.material.icons.outlined.LocalShipping
import androidx.compose.material.icons.outlined.Payments
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Notifications
import androidx.compose.material.icons.outlined.FilterList

/* ======================= Centro de atención ======================= */
@Composable
fun AttentionScreen() {
    val ui = LocalUi.current; val core = ui.core; val att = core.attention; val v = LocalVersion.current
    val all = att.compute()
    LaunchedEffect(v) { att.detectResolved(all) }
    var filter by rememberSaveable { mutableStateOf("all") }; var onlyPending by rememberSaveable { mutableStateOf(false) }
    val open = remember { mutableStateMapOf<String, Boolean>() }; var hist by remember { mutableStateOf(false) }
    val urgent = all.count { it.pr == "urgent" }; val important = all.count { it.pr == "important" }; val ready = all.count { it.cat == "ready" }
    val due = all.filter { it.cat == "pay" }.sumOf { it.amount ?: 0.0 }
    val fm: (Attention.Alert) -> Boolean = when (filter) { "urgent" -> { a -> a.pr == "urgent" }; "pay" -> { a -> a.cat == "pay" }; "orders" -> { a -> a.cat in listOf("ready", "transit") }; "clients" -> { a -> a.cat in listOf("pay", "funds") }; "other" -> { a -> a.cat in listOf("incomplete", "backup") }; else -> { _ -> true } }
    val list = all.filter(fm).filter { !onlyPending || !att.isReviewed(it) }
    val pc = mapOf("urgent" to C.rose, "important" to C.amber, "info" to C.blue)
    Page {
        item {
            SectionCard {
                Row(verticalAlignment = Alignment.CenterVertically) { Text("${urgent + important}", fontSize = 34.sp, fontWeight = FontWeight.ExtraBold); Spacer(Modifier.width(10.dp)); Text(if (urgent + important == 1) "elemento requiere tu atención" else "elementos requieren tu atención", color = MaterialTheme.colorScheme.onSurfaceVariant) }
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) { Kpi("Urgentes", "$urgent", C.rose, modifier = Modifier.weight(1f)); Kpi("Atención", "$important", C.amber, modifier = Modifier.weight(1f)) }
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) { Kpi("Pedidos listos", "$ready", C.green, modifier = Modifier.weight(1f)); Kpi("Por cobrar", money(due), C.rose, modifier = Modifier.weight(1f)) }
                if (all.isNotEmpty()) Secondary("Marcar todo revisado", Modifier.fillMaxWidth()) { att.reviewAll(); ui.toast("Todo marcado como revisado. Los problemas siguen activos hasta resolverse.") }
            }
        }
        if (att.recentResolved.isNotEmpty()) item { SectionCard { att.recentResolved.forEach { Text("✓ " + it.second, color = C.green) } } }
        item { Choice(listOf("all" to "Todos", "urgent" to "Urgentes", "pay" to "Pagos", "orders" to "Pedidos", "clients" to "Clientes", "other" to "Otros"), filter) { filter = it } }
        item { SettingsGroup { ToggleRow("Solo pendientes de revisar", "Oculta lo que ya marcaste como revisado", onlyPending, Icons.Outlined.FilterList) { onlyPending = it } } }
        att.GROUPS.forEach { (cat, title) ->
            val items = list.filter { it.cat == cat }
            if (items.isNotEmpty()) item {
                val pr = if (items.any { it.pr == "urgent" }) "urgent" else if (items.any { it.pr == "important" }) "important" else "info"
                val col = if (cat == "funds" || cat == "ready") C.green else pc[pr]!!
                val tot = items.sumOf { it.amount ?: 0.0 }
                val sub = when (cat) { "pay" -> "${items.size} cliente${if (items.size == 1) " debe" else "s deben"} dinero · total ${money(tot)}"; "ready" -> "${items.size} pedido${if (items.size == 1) " llegó" else "s llegaron"}"
                    "transit" -> "${items.size} pedido(s) con mucho tiempo en tránsito"; "funds" -> "${items.size} cliente(s) · total ${money(tot)}"; "incomplete" -> "${items.size} encargo(s) con datos faltantes"; else -> "" }
                val ic = when (cat) { "pay" -> Icons.Outlined.Payments; "ready" -> Icons.Outlined.Inventory2; "transit" -> Icons.Outlined.LocalShipping; "funds" -> Icons.Outlined.AccountBalanceWallet; "incomplete" -> Icons.Outlined.ErrorOutline; else -> Icons.Outlined.Info }
                SectionCard(title, icon = ic, action = { Text("${items.size}", color = col, fontWeight = FontWeight.ExtraBold, fontSize = 18.sp) }) {
                    if (sub.isNotEmpty()) Text(sub, fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                    items.take(if (open[cat] == true) items.size else 3).forEach { a -> AlertRow(a, pc[a.pr]!!) }
                    if (items.size > 3) LinkText(if (open[cat] == true) "Ver menos" else "Ver los ${items.size}") { open[cat] = open[cat] != true }
                    if (cat == "pay" || cat == "funds") LinkText("Lista de pendientes y fondos ›") { ui.go(Screen.Pending) }
                }
            }
        }
        if (list.isEmpty()) item { Empty(if (all.isNotEmpty()) "Nada en este filtro" else "Todo está en orden", if (all.isNotEmpty()) "Prueba con otro filtro o desactiva \"Solo pendientes\"." else "En este momento no tienes elementos que requieran atención.") }
        item {
            val lb = att.lastBackup()
            if (lb > 0 && System.currentTimeMillis() - lb <= 7 * 86400000.0) Text("Todo respaldado · último respaldo ${Js.fmt(lb)}", fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
        item {
            val acts = att.activity()
            SectionCard("Actividad reciente", icon = Icons.Outlined.History, action = { if (acts.size > 5) LinkText("Ver todo") { hist = true } }) {
                if (acts.isEmpty()) Text("Aquí verás tus pagos, entregas, avisos y respaldos.", color = MaterialTheme.colorScheme.onSurfaceVariant)
                acts.take(5).forEach { a -> ActivityRow(a) }
            }
        }
    }
    if (hist) AlertDialog(onDismissRequest = { hist = false }, confirmButton = { TextButton(onClick = { hist = false }) { Text("Cerrar") } }, icon = Icons.Outlined.History, title = { Text("Historial de actividad") },
        text = { Column { att.activity().forEach { a -> ActivityRow(a) } } })
}

@Composable private fun ActivityRow(a: JObj) {
    Column(Modifier.padding(vertical = 3.dp)) { Text(a.str("text") + if (Js.num(a["amount"]) != 0.0) " — " + money(a["amount"]) else "", fontWeight = FontWeight.SemiBold, fontSize = 14.sp); Text(Js.fmt(a["t"]), fontSize = 11.sp, color = MaterialTheme.colorScheme.onSurfaceVariant) }
}

@Composable
private fun AlertRow(a: Attention.Alert, col: androidx.compose.ui.graphics.Color) {
    val ui = LocalUi.current; val core = ui.core; val att = core.attention; val rev = att.isReviewed(a)
    Column(Modifier.fillMaxWidth().padding(vertical = 4.dp), verticalArrangement = Arrangement.spacedBy(4.dp)) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Box(Modifier.size(9.dp).clip(CircleShape).background(col)); Spacer(Modifier.width(8.dp))
            Column(Modifier.weight(1f)) { Text(a.title, fontWeight = FontWeight.Bold, color = if (rev) MaterialTheme.colorScheme.onSurfaceVariant else MaterialTheme.colorScheme.onSurface); Text(a.sub, fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant) }
            if (a.amount != null) Text((if (a.amountGreen) "+" else "") + money(a.amount), color = if (a.amountGreen) C.green else C.rose, fontWeight = FontWeight.Bold)
        }
        Row(horizontalArrangement = Arrangement.spacedBy(6.dp)) {
            a.actions.forEach { ac -> Chip(ac.label, if (ac.kind == "deliver") C.green else if (ac.kind == "remind" || ac.kind == "arrival") C.green else C.blue) { runAction(ui, ac) } }
            Chip(if (rev) "Revisada" else "Revisar", if (rev) C.mut else C.vio) { att.toggleReview(a.key) }
        }
    }
}

private fun runAction(ui: Ui, ac: Attention.Action) {
    val core = ui.core
    when (ac.kind) {
        "pay", "edit" -> ui.go(Screen.EditOrder(ac.orderId!!))
        "client" -> ui.go(Screen.Client(ac.client!!))
        "order" -> ui.go(Screen.Order(ac.orderId!!))
        "remind" -> core.findClient(ac.client!!)?.let { c -> if (c.pending > 0.005) ui.whatsapp(c.phone, core.reminderClient(c), c.name) else ui.toast("Este cliente no tiene saldo pendiente.") }
        "arrival" -> core.findClient(ac.client!!)?.let { c -> ui.whatsapp(c.phone, core.arrivalMessage(c), c.name) }
        "deliver" -> { val x = core.byId(ac.orderId) ?: return; ui.ask("Pedido entregado", "¿Marcar el pedido de ${x.str("name")} como entregado?", "Sí, entregado") { if (core.deliver(x["id"])) ui.toast("Pedido de ${x.str("name")} marcado como entregado") } }
        "advance" -> { val x = core.byId(ac.orderId) ?: return; advanceWithUndo(ui, x) }
        "backup" -> ui.go(Screen.Backup)
    }
}

/* ======================= Notificaciones ======================= */
@Composable
fun NotifsScreen() {
    val ui = LocalUi.current; val nf = ui.core.notifs; val v = LocalVersion.current
    var filter by rememberSaveable { mutableStateOf("all") }; var q by rememberSaveable { mutableStateOf("") }; var limit by rememberSaveable { mutableStateOf(40) }
    var detail by remember { mutableStateOf<JObj?>(null) }; var cfg by remember { mutableStateOf(false) }
    val cats = nf.visibleCats(); val unread = nf.all().filter { !Js.truthy(it["read"]) && cats.contains(it.str("cat")) }
    val items = nf.list(filter, q)
    fun group(t: Any?): String { val d = Js.local(Js.num(t)).toLocalDate(); val n = LocalDate.now(); val diff = java.time.temporal.ChronoUnit.DAYS.between(d, n)
        return if (diff <= 0) "Hoy" else if (diff == 1L) "Ayer" else if (diff < 7) "Esta semana" else if (diff < 31) "Este mes" else "Anteriores" }
    Page {
        if (unread.isNotEmpty()) item { SectionCard {
            Row(verticalAlignment = Alignment.CenterVertically) { Text("${unread.size}", fontSize = 30.sp, fontWeight = FontWeight.ExtraBold); Text("  notificaci" + if (unread.size == 1) "ón nueva" else "ones nuevas", color = MaterialTheme.colorScheme.onSurfaceVariant, modifier = Modifier.weight(1f)) }
            Text(listOf("pay" to "pagos", "order" to "pedidos", "client" to "clientes", "system" to "sistema").filter { cats.contains(it.first) }.joinToString(" · ") { (c, l) -> "${unread.count { it.str("cat") == c }} $l" }, fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
            Secondary("Marcar todo leído", Modifier.fillMaxWidth()) { nf.readAll(); ui.toast("Todas las notificaciones están leídas") }
        } }
        item { SearchBox(q, { q = it; limit = 40 }, "Buscar actividad: cliente, evento, fecha…") }
        item { Choice(listOf("all" to "Todas", "unread" to "No leídas" + if (unread.isNotEmpty()) " (${unread.size})" else "", "pay" to "Pagos", "order" to "Pedidos", "client" to "Clientes", "system" to "Sistema").filter { it.first in listOf("all", "unread") || cats.contains(it.first) }, filter) { filter = it; limit = 40 } }
        if (items.isEmpty()) item { Empty(if (q.isNotEmpty() || filter != "all") "Sin resultados" else "Todo está al día", if (q.isNotEmpty() || filter != "all") "Prueba con otra búsqueda o filtro." else "No tienes nuevas notificaciones.") }
        var g = ""
        items.take(limit).forEach { n -> val gg = group(n["t"]); if (gg != g) { g = gg; item { Text(gg, fontWeight = FontWeight.Bold, color = MaterialTheme.colorScheme.onSurfaceVariant) } }
            item {
                val read = Js.truthy(n["read"]); val col = if (n.str("cat") == "pay" && n.str("type") != "pay_del") C.green else if (Js.truthy(n["imp"])) C.amber else C.blue
                Row(Modifier.fillMaxWidth().clickable { nf.read(n.str("id")); detail = n }.padding(vertical = 4.dp), verticalAlignment = Alignment.CenterVertically) {
                    Box(Modifier.size(10.dp).clip(CircleShape).background(if (read) MaterialTheme.colorScheme.outline else col)); Spacer(Modifier.width(10.dp))
                    Column(Modifier.weight(1f)) { Text(n.str("title"), fontWeight = if (read) FontWeight.Normal else FontWeight.Bold); Text(n.str("text"), fontSize = 13.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                        Text((nf.CAT[n.str("cat")] ?: "") + " · " + Js.fmt(n["t"]), fontSize = 11.sp, color = MaterialTheme.colorScheme.onSurfaceVariant) }
                }
            } }
        if (items.size > limit) item { Secondary("Ver más (${items.size - limit})", Modifier.fillMaxWidth()) { limit += 40 } }
        item { Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            Secondary("Ajustes", Modifier.weight(1f)) { cfg = true }
            if (nf.all().isNotEmpty()) Secondary("Eliminar historial", Modifier.weight(1f), color = C.rose) { ui.ask("¿Eliminar todas las notificaciones?", "Solo se borra el historial de notificaciones. Tus clientes, encargos y pagos no se tocan.", "Eliminar", true) { nf.clear(); ui.toast("Historial de notificaciones eliminado") } }
        } }
    }
    detail?.let { n ->
        val ord = (n["ref"] as? JObj)?.get("order")?.let { ui.core.byId(it) }; val client = (n["ref"] as? JObj)?.str("client").orEmpty(); val panel = (n["ref"] as? JObj)?.str("panel").orEmpty()
        AlertDialog(onDismissRequest = { detail = null }, icon = Icons.Outlined.Notifications, title = { Text(n.str("title")) },
            text = { Column(verticalArrangement = Arrangement.spacedBy(6.dp)) { Text(n.str("text"))
                if (client.isNotEmpty()) Line("Cliente", client); (n["data"] as? JObj)?.let { d -> if (d["amount"] != null) Line("Monto", money(kotlin.math.abs(Js.num(d["amount"])))); if (d.str("method").isNotEmpty()) Line("Método", d.str("method")); if (d.str("status").isNotEmpty()) Line("Estado", (if (d.str("from").isNotEmpty()) d.str("from") + " → " else "") + d.str("status")) }
                Line("Fecha", Js.fmt(n["t"]))
                // accesos directos: botones secundarios a lo ancho, como en el resto de la app
                if (ord != null) Secondary(if (n.str("cat") == "pay") "Ver pago" else "Ver pedido", Modifier.fillMaxWidth()) { detail = null; ui.go(Screen.Order(Js.str(ord["id"]))) }
                if (client.isNotEmpty() && ui.core.findClient(client) != null) Secondary("Ver cliente", Modifier.fillMaxWidth()) { detail = null; ui.go(Screen.Client(client)) }
                if (panel.isNotEmpty()) Secondary("Abrir", Modifier.fillMaxWidth()) { detail = null; ui.go(if (panel == "backup") Screen.Backup else if (panel == "security") Screen.Security else Screen.Orders) }
            } },
            confirmButton = { TextButton(onClick = { detail = null }) { Text("Cerrar") } })
    }
    if (cfg) {
        val c = nf.cfg()
        // la misma ventana que Ajustes → Notificaciones
        var tick by remember { mutableStateOf(0) }
        Sheet("Notificaciones", { cfg = false }, Icons.Outlined.Notifications, "Qué avisos quieres recibir") {
            @Suppress("UNUSED_VARIABLE") val t = tick
            NotifToggles(nf.cfg()) { k -> nf.toggleCfg(k); tick++ }
        }
    }
    @Suppress("UNUSED_VARIABLE") val u = v
}
