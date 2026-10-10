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
import androidx.compose.material.icons.filled.Edit
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.ui.draw.clip
import androidx.compose.foundation.layout.height
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.Chat
import androidx.compose.material.icons.filled.Inventory2
import androidx.compose.material.icons.outlined.Description
import androidx.compose.material.icons.outlined.Phone
import androidx.compose.material.icons.outlined.Favorite
import androidx.compose.material.icons.outlined.ContactPage
import androidx.compose.material.icons.outlined.Place
import androidx.compose.material.icons.outlined.StickyNote2
import androidx.compose.material.icons.outlined.Inventory2
import androidx.compose.material.icons.outlined.Payments
import androidx.compose.material.icons.outlined.PhotoLibrary
import com.agencia.encargos.core.objs
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
import com.agencia.encargos.core.Js
import com.agencia.encargos.core.str

@Composable
fun ClientsScreen() {
    val ui = LocalUi.current; val core = ui.core; val v = LocalVersion.current
    var q by rememberSaveable { mutableStateOf("") }; var limit by rememberSaveable { mutableStateOf(30) }
    val all = core.activeClients()
    val words = Js.normSearch(q).split(Regex("\\s+")).filter { it.isNotEmpty() }
    val list = all.filter { c -> val hay = Js.normSearch(listOf(c.name, c.phone, c.orders.joinToString(" ") { listOf(it.str("accessCode"), Js.str(it["orderNo"]), it.str("status")).joinToString(" ") }).joinToString(" ")); words.all { hay.contains(it) } }
    Page(right = "${all.size} activos") {
        item { SearchBox(q, { q = it; limit = 30 }, "Buscar cliente, teléfono o código") }
        item { TopTiles(Triple("Clientes", "${all.size}", null), Triple("Por cobrar", money(all.sumOf { it.pending }), C.rose), Triple("A favor", money(all.sumOf { it.funds }), C.green)) }
        if (all.isEmpty()) item { Empty("Aún no tienes clientes", "Los clientes aparecen automáticamente al crear encargos.") { Primary("+ Nuevo encargo") { ui.root(Screen.NewOrder) } } }
        else if (list.isEmpty()) item { Empty("Sin resultados", "Ningún cliente coincide con tu búsqueda.") { Secondary("Limpiar búsqueda") { q = "" } } }
        list.take(limit).forEach { c -> item { ClientCard(c) } }
        if (list.size > limit) item { Secondary("Ver más (${list.size - limit} restantes)", Modifier.fillMaxWidth()) { limit += 30 } }
        item { CompletedLink() }
    }
    @Suppress("UNUSED_VARIABLE") val u = v
}

@Composable
fun ClientCard(c: com.agencia.encargos.core.Encargos.Client) {
    val ui = LocalUi.current; val core = ui.core
    val total = c.orders.sumOf { Js.num(it["total"]) }
    val last = c.orders.maxByOrNull { Js.parseDate(it["date"]).let { d -> if (d.isNaN()) 0.0 else d } }
    SectionCard(modifier = Modifier.clickable { ui.go(Screen.Client(c.name)) }) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Avatar(c.name, if (c.pending > 0.005) C.rose else C.blue); Spacer(Modifier.width(10.dp))
            Column(Modifier.weight(1f)) {
                Text(c.name, fontWeight = FontWeight.ExtraBold, fontSize = 16.sp, maxLines = 1)
                Text("${c.phone.ifEmpty { "Sin teléfono" }} · ${c.orders.size} ${if (c.orders.size == 1) "encargo" else "encargos"}${last?.let { " · " + it.str("status").ifEmpty { "Pedido" } } ?: ""}", fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant, maxLines = 1)
            }
        }
        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) { Kpi("Libras", lb(c.pounds), modifier = Modifier.weight(1f)); Kpi("Cobrado", money(c.paid), C.green, modifier = Modifier.weight(1f)); Kpi("Pendiente", if (c.pending > 0.005) "- " + money(c.pending) else "$0.00", if (c.pending > 0.005) C.rose else C.green, modifier = Modifier.weight(1f)) }
        Text("Pagó ${money(c.paid)} de ${money(total)}", fontSize = 12.5.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
        ProgressBar(if (total > 0) c.paid / total else 0.0)
        if (c.funds > 0) Text("🟢 Fondo a favor + ${money(c.funds)}", color = C.green, fontWeight = FontWeight.Bold, fontSize = 13.sp)
        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            CardBtn("Ficha", Icons.Outlined.Description, Modifier.weight(1f)) { ui.go(Screen.Client(c.name)) }
            if (c.pending > 0.005) CardBtn("Cobrar", Icons.Filled.Chat, Modifier.weight(1f), tint = C.green) { ui.whatsapp(c.phone, core.reminderClient(c), c.name) }
            else CardBtn("WhatsApp", Icons.Filled.Chat, Modifier.weight(1f), tint = C.green) { ui.whatsapp(c.phone, "", c.name) }
            CardBtn("Encargo", Icons.Filled.Add, Modifier.weight(1f)) { ui.go(Screen.NewFor(c.name, c.phone)) }
        }
    }
}

/* ======================= Ficha del cliente ======================= */
@Composable
fun ClientScreen(name: String) {
    val ui = LocalUi.current; val core = ui.core; val v = LocalVersion.current
    val c = core.findClient(name) ?: run { Empty("Este cliente ya no existe."); return }
    val inf = core.clientInfo(c.name); var edit by remember { mutableStateOf(false) }
    var viewer by remember { mutableStateOf<Pair<List<String>, Int>?>(null) }
    val orders = c.orders.sortedByDescending { Js.parseDate(it["date"]).let { d -> if (d.isNaN()) 0.0 else d } }
    val total = c.orders.sumOf { Js.num(it["total"]) }
    val first = c.orders.minOfOrNull { Js.parseDate(it["date"]).let { d -> if (d.isNaN()) Double.MAX_VALUE else d } }
    val pays = orders.flatMap { x -> core.payments(x).objs().mapIndexed { i, h -> Triple(x, i, h) } }.sortedByDescending { Js.parseDate(it.third["date"]).let { d -> if (d.isNaN()) 0.0 else d } }
    val shots = orders.flatMap { core.orderPhotos(it) }
    val owes = c.pending > 0.005
    Page {
        // ---- cabecera: quién es y cómo está su cuenta ----
        item {
            SectionCard {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Avatar(c.name, if (owes) C.rose else C.blue, 60); Spacer(Modifier.width(14.dp))
                    Column(Modifier.weight(1f)) {
                        Text(c.name, fontSize = 21.sp, fontWeight = FontWeight.ExtraBold, maxLines = 2)
                        IconLabel(Icons.Outlined.Phone, c.phone.ifEmpty { "Sin teléfono" })
                        if (first != null && first < Double.MAX_VALUE) Text("Cliente desde ${Js.fmt(first).substringBefore(",")}", fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                    }
                    Chip(if (owes) "Debe" else "Al día", if (owes) C.rose else C.green)
                }
                // saldo: rojo si debe, verde si está al día o tiene fondo a favor
                val shape = androidx.compose.foundation.shape.RoundedCornerShape(16.dp)
                Column(Modifier.fillMaxWidth().clip(shape).background((if (owes) C.rose else C.green).copy(alpha = .08f)).border(1.dp, (if (owes) C.rose else C.green).copy(alpha = .30f), shape).padding(14.dp),
                    verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    Row(verticalAlignment = Alignment.Bottom) {
                        Column(Modifier.weight(1f)) {
                            Text(if (owes) "Pendiente por cobrar" else "Cuenta al día", fontSize = 12.5.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                            Text(if (owes) "- " + money(countUp(c.pending)) else money(0), fontSize = 26.sp, fontWeight = FontWeight.ExtraBold, color = if (owes) C.rose else C.green)
                        }
                        if (c.funds > 0.005) Column(horizontalAlignment = Alignment.End) {
                            Text("Fondo a favor", fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                            Text("+ " + money(c.funds), fontSize = 17.sp, fontWeight = FontWeight.ExtraBold, color = C.green)
                        }
                    }
                    ProgressBar(if (total > 0) c.paid / total else 1.0)
                    Text("Pagó ${money(c.paid)} de ${money(total)}" + if (total > 0) " · ${Math.round((c.paid / total).coerceIn(0.0, 1.0) * 100)} %" else "", fontSize = 12.5.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    Kpi("Encargos", "${c.orders.size}", modifier = Modifier.weight(1f)); Kpi("Libras", lb(c.pounds), modifier = Modifier.weight(1f)); Kpi("Cobrado", money(c.paid), C.green, modifier = Modifier.weight(1f))
                }
            }
        }
        // ---- acciones ----
        item {
            Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    Primary("Nuevo encargo", Modifier.weight(1f), Icons.Filled.Add) { ui.go(Screen.NewFor(c.name, c.phone)) }
                    if (owes) Secondary("Cobrar", Modifier.weight(1f).height(52.dp), Icons.Filled.Chat, color = C.green) { ui.whatsapp(c.phone, core.reminderClient(c), c.name) }
                    else Secondary("WhatsApp", Modifier.weight(1f).height(52.dp), Icons.Filled.Chat, color = C.green) { ui.whatsapp(c.phone, "", c.name) }
                }
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    Secondary("Avisar llegada", Modifier.weight(1f), Icons.Filled.Inventory2) { ui.whatsapp(c.phone, core.arrivalMessage(c), c.name) }
                    if (!owes) Secondary("Agradecer compra", Modifier.weight(1f), Icons.Outlined.Favorite) { ui.whatsapp(c.phone, core.thanksClient(c), c.name) }
                }
            }
        }
        // ---- datos fijos del cliente ----
        item {
            SectionCard("Datos del cliente", icon = Icons.Outlined.ContactPage, action = { Text(if (inf.str("address").isEmpty() && inf.str("note").isEmpty()) "Agregar ✎" else "Editar ✎", color = C.blue, fontWeight = FontWeight.Bold, modifier = Modifier.clickable { edit = true }) }) {
                if (inf.str("address").isEmpty() && inf.str("note").isEmpty()) Text("Guarda su dirección de entrega y detalles importantes (tallas, preferencias, horarios…).", fontSize = 13.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                if (inf.str("address").isNotEmpty()) InfoLine(Icons.Outlined.Place, "Dirección", inf.str("address"))
                if (inf.str("note").isNotEmpty()) InfoLine(Icons.Outlined.StickyNote2, "Nota fija", inf.str("note"))
            }
        }
        // ---- encargos ----
        item {
            SectionCard("Encargos (${orders.size})", icon = Icons.Outlined.Inventory2) {
                orders.forEach { x ->
                    val b = Js.num(x["balance"]); val t = Js.num(x["total"]); val id = Js.str(x["id"]); val ph = core.orderPhotos(x)
                    val sh = androidx.compose.foundation.shape.RoundedCornerShape(14.dp)
                    Column(Modifier.fillMaxWidth().clip(sh).background(W.tile).border(1.dp, W.line, sh).clickable { ui.go(Screen.Order(id)) }.padding(12.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            Column(Modifier.weight(1f)) {
                                Text("${Js.fmt(x["date"]).substringBefore(",")} · ${core.shippingName(core.typeOf(x))} ${lb(Js.num(x["pounds"]))}", fontWeight = FontWeight.ExtraBold, fontSize = 14.sp, maxLines = 1)
                                Text("${core.orderLabel(x)} · ${money(x["lbCost"])}/lb", fontSize = 11.5.sp, color = MaterialTheme.colorScheme.onSurfaceVariant, maxLines = 1)
                            }
                            StatusChip(x.str("status"))
                        }
                        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                            Kpi("Total", money(t), modifier = Modifier.weight(1f)); Kpi("Pagado", money(x["paid"]), C.green, modifier = Modifier.weight(1f))
                            Kpi(if (b < -0.005) "A favor" else "Saldo", if (b > 0.005) "- " + money(b) else if (b < -0.005) "+ " + money(-b) else "$0.00", if (b > 0.005) C.rose else C.green, modifier = Modifier.weight(1f))
                        }
                        ProgressBar(if (t > 0) Js.num(x["paid"]) / t else 1.0)
                        if (ph.isNotEmpty()) PhotoStrip(ph, 84) { viewer = ph to it }
                        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                            CardBtn("Ficha", Icons.Outlined.Description, Modifier.weight(1f)) { ui.go(Screen.Order(id)) }
                            CardBtn("Editar", Icons.Filled.Edit, Modifier.weight(1f)) { ui.go(Screen.EditOrder(id)) }
                        }
                    }
                }
            }
        }
        // ---- pagos de todos sus encargos ----
        item {
            SectionCard("Pagos (${pays.size})", icon = Icons.Outlined.Payments) {
                if (pays.isEmpty()) Text("Todavía no hay pagos registrados.", fontSize = 13.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                pays.take(30).forEachIndexed { k, (x, i, h) ->
                    if (k > 0) RowSep()
                    Row(Modifier.fillMaxWidth().clickable { editPaymentState.value = Pair(Js.str(x["id"]), i) }.padding(vertical = 6.dp), verticalAlignment = Alignment.CenterVertically) {
                        Column(Modifier.weight(1f)) {
                            Text("${dayLabel(h["date"])} · ${h.str("method").ifEmpty { "Efectivo" }}", fontWeight = FontWeight.Bold, fontSize = 14.sp)
                            Text(core.orderLabel(x) + if (h.str("note").isNotEmpty()) " · " + h.str("note") else "", fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant, maxLines = 1)
                        }
                        Text("+ " + money(h["amount"]), color = C.green, fontWeight = FontWeight.ExtraBold)
                    }
                }
                if (pays.isNotEmpty()) Text("Toca un pago para corregirlo o eliminarlo.", fontSize = 11.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
        }
        // ---- todas las capturas de sus pedidos ----
        if (shots.isNotEmpty()) item {
            SectionCard("Capturas de pedidos (${shots.size})", icon = Icons.Outlined.PhotoLibrary) {
                PhotoStrip(shots, 120) { viewer = shots to it }
                Text("Toca una captura para verla completa y ampliarla.", fontSize = 11.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
        }
    }
    if (edit) ClientInfoDialog(c.name, inf.str("address"), inf.str("note")) { edit = false }
    viewer?.let { (l, i) -> PhotoViewer(l, i, null) { viewer = null } }
    PaymentEditHost()
    @Suppress("UNUSED_VARIABLE") val u = v
}

/** Dato del cliente con icono, etiqueta pequeña y valor. */
@Composable private fun InfoLine(icon: androidx.compose.ui.graphics.vector.ImageVector, label: String, value: String) {
    Row(verticalAlignment = Alignment.Top) {
        IconTile(icon, size = 32); Spacer(Modifier.width(10.dp))
        Column(Modifier.weight(1f)) { Text(label, fontSize = 11.sp, color = MaterialTheme.colorScheme.onSurfaceVariant); Text(value, fontSize = 14.sp) }
    }
}

@Composable
fun ClientInfoDialog(name: String, a0: String, n0: String, dismiss: () -> Unit) {
    val ui = LocalUi.current
    var a by remember { mutableStateOf(a0) }; var n by remember { mutableStateOf(n0) }
    AlertDialog(onDismissRequest = dismiss, title = { Text("📇 Datos de $name") },
        text = { Column(verticalArrangement = Arrangement.spacedBy(8.dp)) { Field(a, { a = it }, "Dirección de entrega", singleLine = false, minLines = 2); Field(n, { n = it }, "Nota fija del cliente (talla, horarios…)", singleLine = false, minLines = 2) } },
        confirmButton = { TextButton(onClick = { if (ui.core.saveClientInfo(name, a, n)) ui.toast("✅ Datos del cliente guardados."); dismiss() }) { Text("Guardar") } },
        dismissButton = { TextButton(onClick = dismiss) { Text("Cancelar") } })
}
@Suppress("unused") private val pad = Modifier.padding(0.dp)
