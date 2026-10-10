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

@Composable
fun ClientScreen(name: String) {
    val ui = LocalUi.current; val core = ui.core; val v = LocalVersion.current
    val c = core.findClient(name) ?: run { Empty("Este cliente ya no existe."); return }
    val inf = core.clientInfo(c.name); var edit by remember { mutableStateOf(false) }
    val total = c.orders.sumOf { Js.num(it["total"]) }
    Page {
        item {
            SectionCard {
                Row(verticalAlignment = Alignment.CenterVertically) { Avatar(c.name, C.blue, 56); Spacer(Modifier.width(12.dp)); Column { Text(c.name, fontSize = 20.sp, fontWeight = FontWeight.ExtraBold); Text("${c.phone.ifEmpty { "Sin teléfono" }} · ${c.orders.size} ${if (c.orders.size == 1) "encargo" else "encargos"}", color = MaterialTheme.colorScheme.onSurfaceVariant, fontSize = 13.sp) } }
                Row { Text("Pagado ${money(c.paid)} de ${money(total)}", Modifier.weight(1f), fontSize = 13.5.sp); Text(if (c.pending > 0.005) "Debe ${money(c.pending)}" else "Al día", color = if (c.pending > 0.005) C.rose else C.green, fontWeight = FontWeight.ExtraBold, fontSize = 13.5.sp) }
                ProgressBar(if (total > 0) c.paid / total else 1.0)
            }
        }
        item {
            SectionCard("Datos del cliente", action = { Text(if (inf.str("address").isEmpty() && inf.str("note").isEmpty()) "Agregar ✎" else "Editar ✎", color = C.blue, fontWeight = FontWeight.Bold, modifier = Modifier.clickable { edit = true }) }) {
                if (inf.str("address").isEmpty() && inf.str("note").isEmpty()) Text("Guarda su dirección de entrega y detalles importantes (tallas, preferencias, horarios…).", fontSize = 13.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                if (inf.str("address").isNotEmpty()) { Text("Dirección", fontSize = 11.sp, color = MaterialTheme.colorScheme.onSurfaceVariant); Text(inf.str("address")) }
                if (inf.str("note").isNotEmpty()) { Text("Nota fija", fontSize = 11.sp, color = MaterialTheme.colorScheme.onSurfaceVariant); Text(inf.str("note")) }
            }
        }
        item {
            Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) { Kpi("Encargos", "${c.orders.size}", modifier = Modifier.weight(1f)); Kpi("Libras", lb(c.pounds), modifier = Modifier.weight(1f)); Kpi("A favor", if (c.funds > 0) "+ " + money(c.funds) else "$0.00", C.green, modifier = Modifier.weight(1f)) }
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) { Kpi("Cobrado", money(c.paid), C.green, modifier = Modifier.weight(1f)); Kpi("Pendiente", if (c.pending > 0) "- " + money(c.pending) else "$0.00", C.rose, modifier = Modifier.weight(1f)) }
            }
        }
        item {
            Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    Primary("Nuevo encargo", Modifier.weight(1f), Icons.Filled.Add) { ui.go(Screen.NewFor(c.name, c.phone)) }
                    if (c.pending > 0.005) Secondary("Cobrar", Modifier.weight(1f).height(52.dp), Icons.Filled.Chat, color = C.green) { ui.whatsapp(c.phone, core.reminderClient(c), c.name) }
                    else Secondary("WhatsApp", Modifier.weight(1f).height(52.dp), Icons.Filled.Chat, color = C.green) { ui.whatsapp(c.phone, "", c.name) }
                }
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    Secondary("Avisar llegada", Modifier.weight(1f), Icons.Filled.Inventory2) { ui.whatsapp(c.phone, core.arrivalMessage(c), c.name) }
                    if (c.pending <= 0.005) Secondary("Agradecer compra", Modifier.weight(1f)) { ui.whatsapp(c.phone, core.thanksClient(c), c.name) }
                }
            }
        }
        item {
            SectionCard("Encargos") {
                c.orders.sortedByDescending { Js.parseDate(it["date"]) }.forEach { x ->
                    val b = Js.num(x["balance"]); val t = Js.num(x["total"]); val id = Js.str(x["id"])
                    val sh = androidx.compose.foundation.shape.RoundedCornerShape(14.dp)
                    Column(Modifier.fillMaxWidth().clip(sh).background(W.tile).border(1.dp, W.line, sh).clickable { ui.go(Screen.Order(id)) }.padding(12.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            Column(Modifier.weight(1f)) {
                                Text("${Js.fmt(x["date"])} · ${core.shippingName(core.typeOf(x))} ${lb(Js.num(x["pounds"]))}", fontWeight = FontWeight.ExtraBold, fontSize = 14.sp, maxLines = 1)
                                Row { Text("Total ${money(t)} · ", fontSize = 12.5.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                                    Text(if (b > 0.005) "Debe ${money(b)}" else if (b < -0.005) "A favor ${money(-b)}" else "Pagado", fontSize = 12.5.sp, color = if (b > 0.005) C.rose else C.green, fontWeight = FontWeight.Bold) }
                                Text("${core.orderLabel(x)} · ${money(x["lbCost"])}/lb", fontSize = 11.5.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                            }
                            StatusChip(x.str("status"))
                        }
                        ProgressBar(if (t > 0) Js.num(x["paid"]) / t else 1.0)
                        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                            CardBtn("Ficha", Icons.Outlined.Description, Modifier.weight(1f)) { ui.go(Screen.Order(id)) }
                            CardBtn("Editar", Icons.Filled.Edit, Modifier.weight(1f)) { ui.go(Screen.EditOrder(id)) }
                        }
                    }
                }
            }
        }
    }
    if (edit) ClientInfoDialog(c.name, inf.str("address"), inf.str("note")) { edit = false }
    @Suppress("UNUSED_VARIABLE") val u = v
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
