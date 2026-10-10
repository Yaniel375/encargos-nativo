package com.agencia.encargos.nativo.ui

import android.app.DatePickerDialog
import android.app.TimePickerDialog
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.clickable
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.AddAPhoto
import androidx.compose.material.icons.filled.Chat
import androidx.compose.material.icons.filled.Edit
import androidx.compose.material.icons.filled.MoreVert
import androidx.compose.material.icons.filled.Person
import androidx.compose.material.icons.filled.Save
import androidx.compose.material.icons.filled.Send
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
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
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.agencia.encargos.core.Encargos
import com.agencia.encargos.core.JArr
import com.agencia.encargos.core.JObj
import com.agencia.encargos.core.Js
import com.agencia.encargos.core.objs
import com.agencia.encargos.core.str
import com.agencia.encargos.nativo.util.Files
import com.agencia.encargos.nativo.util.Images
import com.agencia.encargos.nativo.util.Receipt
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import java.time.LocalDateTime
import java.time.ZoneId

/* ======================= Lista de encargos activos ======================= */
@Composable
fun OrdersScreen() {
    val ui = LocalUi.current; val core = ui.core; val v = LocalVersion.current
    var q by rememberSaveable { mutableStateOf("") }; var f by rememberSaveable { mutableStateOf("") }; var limit by rememberSaveable { mutableStateOf(30) }
    val ad = core.activeOrders()
    val words = Js.normSearch(q).split(Regex("\\s+")).filter { it.isNotEmpty() }
    val list = ad.filter { x -> val hay = Js.normSearch(listOf(x.str("name"), x.str("phone"), x.str("accessCode"), Js.str(x["orderNo"]), core.orderLabel(x), x.str("status"), x.str("shippingType"), x.str("note")).joinToString(" "))
        words.all { hay.contains(it) } && (f.isEmpty() || x["status"] == f) }
    Page(right = "${ad.size} activos") {
        item { SearchBox(q, { q = it; limit = 30 }, "Buscar por cliente, teléfono, código o estado") }
        item { Choice(listOf("" to "Todos") + Encargos.STATES.map { it to it }, f) { f = it; limit = 30 } }
        item { Row { Text("${ad.size} activos", color = MaterialTheme.colorScheme.onSurfaceVariant, modifier = Modifier.weight(1f)); Text("Completados ›", color = C.blue, modifier = Modifier.clickable { ui.go(Screen.Completed) }) } }
        if (ad.isEmpty()) item { Empty("No hay encargos activos", "Los encargos culminados se guardan en Encargos completados.") { Primary("+ Nuevo encargo") { ui.root(Screen.NewOrder) } } }
        else if (list.isEmpty()) item { Empty("Sin resultados", "No hay encargos que coincidan con tu búsqueda o filtro.") { Secondary("Limpiar búsqueda") { q = ""; f = "" } } }
        list.take(limit).forEach { x -> item { OrderCard(x) } }
        if (list.size > limit) item { Secondary("Ver más (${list.size - limit} restantes)", Modifier.fillMaxWidth()) { limit += 30 } }
    }
    @Suppress("UNUSED_VARIABLE") val u = v
}

@Composable
fun OrderCard(x: JObj) {
    val ui = LocalUi.current; val core = ui.core
    val b = Js.num(x["balance"]); val id = Js.str(x["id"]); val g = core.profit(x)
    var menu by remember { mutableStateOf(false) }
    SectionCard(modifier = Modifier.clickable { ui.go(Screen.Order(id)) }) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Avatar(x.str("name"), C.status(x.str("status")))
            Spacer(Modifier.width(10.dp))
            Column(Modifier.weight(1f)) {
                Text(x.str("name"), fontWeight = FontWeight.Bold, maxLines = 1, overflow = TextOverflow.Ellipsis)
                Text("${core.orderLabel(x)} · ${x.str("phone").ifEmpty { "Sin número" }} · ${Js.fmt(x["date"])}", fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant, maxLines = 1)
            }
            Box { IconButton(onClick = { menu = true }) { Icon(Icons.Filled.MoreVert, "Más") }; OrderMenu(x, menu) { menu = false } }
        }
        Row(horizontalArrangement = Arrangement.spacedBy(6.dp)) {
            val nx = core.nextStatus(x.str("status").ifEmpty { "Pedido" })
            StatusChip(x.str("status")) { if (nx != null) advanceWithUndo(ui, x) }
            Chip(core.shippingName(core.typeOf(x)) + " · " + lb(Js.num(x["pounds"])), C.teal)
            if (Js.num(x["pounds"]) > 0) Chip("Ganancia " + (if (g < 0) "-" else "") + money(kotlin.math.abs(g)), if (g >= 0) C.green else C.rose)
        }
        Line("Costo del pedido", money(x["cost"])); Line("${core.shippingLabel(core.typeOf(x))} · ${Js.toFixed(Js.num(x["pounds"]), 2)} lb × ${money(x["lbCost"])}", money(x["shipping"]))
        Line("Total", money(x["total"]), bold = true); Line("Pagado", money(x["paid"]), C.green)
        Text(if (b > 0) "🔴 Pendiente - ${money(b)}" else if (b < 0) "🟢 Fondo a favor + ${money(-b)}" else "✓ Pagado completo", color = if (b > 0) C.rose else C.green, fontWeight = FontWeight.Bold)
        if (x.str("note").isNotEmpty()) Text("📝 " + x.str("note"), fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            Tonal("Editar", Modifier.weight(1f), Icons.Filled.Edit) { ui.go(Screen.EditOrder(Js.str(x["id"]))) }
            Tonal("WhatsApp", Modifier.weight(1f), Icons.Filled.Chat) { ui.whatsapp(x.str("phone"), "", x.str("name")) }
        }
    }
}

fun advanceWithUndo(ui: Ui, x: JObj) {
    val prev = ui.core.advance(x["id"]) ?: run { ui.toast("Este encargo ya está entregado."); return }
    ui.toast("📦 ${x.str("name")}: $prev → ${x.str("status")}") { ui.core.setStatus(x["id"], prev) }
}

/** Menú ⋯ de un encargo: fecha, nota, recibo, recordar, agradecer, eliminar. */
@Composable
fun OrderMenu(x: JObj, open: Boolean, close: () -> Unit) {
    val ui = LocalUi.current; val core = ui.core
    var note by remember { mutableStateOf<String?>(null) }; var receipt by remember { mutableStateOf(false) }
    DropdownMenu(open, close) {
        DropdownMenuItem(text = { Text("📅 Cambiar fecha") }, onClick = { close(); pickDate(ui, x) })
        DropdownMenuItem(text = { Text("📝 Nota interna") }, onClick = { close(); note = x.str("note") })
        DropdownMenuItem(text = { Text("📤 Enviar recibo al cliente") }, onClick = { close(); receipt = true })
        if (Js.num(x["balance"]) > 0.005) DropdownMenuItem(text = { Text("🔔 Recordar pago") }, onClick = { close(); ui.whatsapp(x.str("phone"), core.reminderOrder(x), x.str("name")) })
        DropdownMenuItem(text = { Text("🙏 Enviar agradecimiento") }, onClick = { close(); ui.whatsapp(x.str("phone"), core.thanksOrder(x), x.str("name")) })
        DropdownMenuItem(text = { Text("🗑️ Eliminar", color = C.rose) }, onClick = { close(); deleteOrder(ui, x) })
    }
    note?.let { n -> NoteDialog(n, { note = null }) { t -> if (core.setNote(x["id"], t)) ui.toast("✅ Nota guardada."); note = null } }
    if (receipt) ReceiptDialog(x) { receipt = false }
}

fun deleteOrder(ui: Ui, x: JObj) {
    ui.requireAuth("Eliminar un encargo.") {
        ui.ask("🗑️ Eliminar encargo", "¿Eliminar el encargo de ${x.str("name")}? Esta acción no se puede deshacer.", "Eliminar", true) {
            val r = ui.core.removeOrder(x["id"]) ?: return@ask
            if (ui.current is Screen.Order || ui.current is Screen.EditOrder) ui.back()
            ui.toast("🗑️ Encargo de ${x.str("name")} eliminado.") { if (ui.core.undoRemove(r)) ui.toast("↩️ Encargo de ${x.str("name")} restaurado.") }
        }
    }
}

fun pickDate(ui: Ui, x: JObj) {
    val t = Js.parseDate(x["date"]).let { if (it.isNaN()) System.currentTimeMillis().toDouble() else it }
    val cur = Js.local(t)
    DatePickerDialog(ui.act, { _, y, m, d ->
        TimePickerDialog(ui.act, { _, hh, mm ->
            val ms = LocalDateTime.of(y, m + 1, d, hh, mm).atZone(ZoneId.systemDefault()).toInstant().toEpochMilli()
            if (ui.core.setDate(x["id"], Js.isoOf(ms))) ui.toast("✅ Fecha actualizada.")
        }, cur.hour, cur.minute, true).show()
    }, cur.year, cur.monthValue - 1, cur.dayOfMonth).show()
}

@Composable
fun NoteDialog(initial: String, dismiss: () -> Unit, save: (String) -> Unit) {
    var t by remember { mutableStateOf(initial) }
    AlertDialog(onDismissRequest = dismiss, title = { Text("📝 Nota interna") },
        text = { Column { Text("Solo para ti: el cliente nunca la ve.", fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant); Field(t, { t = it }, "Nota del encargo", singleLine = false, minLines = 3) } },
        confirmButton = { TextButton(onClick = { save(t) }) { Text("Guardar") } }, dismissButton = { TextButton(onClick = dismiss) { Text("Cancelar") } })
}

@Composable
fun ReceiptDialog(x: JObj, dismiss: () -> Unit) {
    val ui = LocalUi.current; val core = ui.core
    fun make(kind: String) {
        ui.io("Preparando el recibo") {
            val bm = Receipt.bitmap(core, x)
            val bytes = if (kind == "pdf") Receipt.pdf(bm) else Receipt.png(bm)
            withContext(Dispatchers.Main) {
                when (kind) {
                    "print" -> Receipt.print(ui.act, x, bm)
                    "save" -> { val ok = Files.saveToDownloads(ui.act, Receipt.fileBase(x) + ".png", bytes, "image/png", "Encargos/Recibos"); ui.toast(if (ok) "Recibo guardado en Descargas/Encargos/Recibos." else "No se pudo guardar el recibo.") }
                    else -> { Files.shareFile(ui.act, Receipt.fileBase(x) + if (kind == "pdf") ".pdf" else ".png", bytes, if (kind == "pdf") "application/pdf" else "image/png", core.receiptCaption(x), core.waNumber(x.str("phone")).ifEmpty { null }); core.notified(x.str("name")) }
                }
            }
        }
        dismiss()
    }
    AlertDialog(onDismissRequest = dismiss, title = { Text("Enviar recibo") },
        text = { Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
            Text("Recibo de ${x.str("name")} · ${money(x["total"])}", color = MaterialTheme.colorScheme.onSurfaceVariant)
            Primary("Enviar como imagen", Modifier.fillMaxWidth()) { make("png") }
            Secondary("Enviar como PDF", Modifier.fillMaxWidth()) { make("pdf") }
            Secondary("Imprimir o guardar PDF", Modifier.fillMaxWidth()) { make("print") }
            Secondary("Guardar imagen en Descargas", Modifier.fillMaxWidth()) { make("save") }
        } }, confirmButton = { TextButton(onClick = dismiss) { Text("Cerrar") } })
}

/* ======================= Ficha del encargo ======================= */
@Composable
fun OrderScreen(id: String) {
    val ui = LocalUi.current; val core = ui.core; val v = LocalVersion.current
    val x = core.byId(id) ?: run { Empty("Este encargo ya no existe."); return }
    val t = Js.num(x["total"]); val p = Js.num(x["paid"]); val b = Js.num(x["balance"]); val st = x.str("status").ifEmpty { "Pedido" }
    val g = core.profit(x); val nx = core.nextStatus(st)
    var viewer by remember { mutableStateOf<Int?>(null) }; var receipt by remember { mutableStateOf(false) }; var note by remember { mutableStateOf<String?>(null) }; var menu by remember { mutableStateOf(false) }
    val photos = core.orderPhotos(x)
    Page {
        item {
            SectionCard {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Avatar(x.str("name"), C.status(st), 52); Spacer(Modifier.width(12.dp))
                    Column(Modifier.weight(1f)) { Text(x.str("name"), fontSize = 20.sp, fontWeight = FontWeight.ExtraBold); Text("${core.orderLabel(x)} · ${x.str("phone").ifEmpty { "Sin teléfono" }} · ${Js.fmt(x["date"])}", fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant) }
                    Box { IconButton(onClick = { menu = true }) { Icon(Icons.Filled.MoreVert, "Más") }; OrderMenu(x, menu) { menu = false } }
                }
                Row(horizontalArrangement = Arrangement.spacedBy(6.dp)) { StatusChip(st); Chip(core.shippingName(core.typeOf(x)) + " · " + lb(Js.num(x["pounds"])), C.teal) }
                Row { Text("Pagado ${money(p)} de ${money(t)}", Modifier.weight(1f)); Text(if (b > 0.005) "Debe ${money(b)}" else if (b < -0.005) "A favor ${money(-b)}" else "Pagado completo", color = if (b > 0.005) C.rose else C.green, fontWeight = FontWeight.Bold) }
                ProgressBar(if (t > 0) p / t else 1.0)
            }
        }
        item {
            if (st == "Cancelado") SectionCard { Text("Este encargo está cancelado.", color = MaterialTheme.colorScheme.onSurfaceVariant) }
            else SectionCard("Estado del pedido", action = { if (nx != null) Text("Pasar a $nx ›", color = C.blue, modifier = Modifier.clickable { advanceWithUndo(ui, x) }) }) {
                val idx = Encargos.FLOW.indexOf(st)
                Row(Modifier.horizontalScroll(rememberScrollState()), horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                    Encargos.FLOW.forEachIndexed { i, s -> Chip((if (i < idx) "✓ " else "${i + 1}. ") + s, if (i <= idx) C.status(s) else MaterialTheme.colorScheme.outline) }
                }
            }
        }
        item {
            Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) { Kpi("Pedido", money(x["cost"]), modifier = Modifier.weight(1f)); Kpi("Envío", money(x["shipping"]), sub = "${Js.toFixed(Js.num(x["pounds"]), 2)} × ${money(x["lbCost"])}", modifier = Modifier.weight(1f)); Kpi("Total", money(t), C.blue, modifier = Modifier.weight(1f)) }
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    Kpi("Pagado", money(p), C.green, modifier = Modifier.weight(1f))
                    Kpi(if (b < -0.005) "A favor" else "Saldo", if (b > 0.005) "-" + money(b) else if (b < -0.005) "+" + money(-b) else "$0.00", if (b > 0.005) C.rose else C.green, modifier = Modifier.weight(1f))
                    Kpi("Ganancia", (if (g < 0) "-" else "") + money(kotlin.math.abs(g)), if (g >= 0) C.green else C.rose, sub = money(core.marginOf(x)) + "/lb", modifier = Modifier.weight(1f))
                }
            }
        }
        item {
            Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) { Primary("Editar", Modifier.weight(1f), Icons.Filled.Edit) { ui.go(Screen.EditOrder(id)) }; Primary("WhatsApp", Modifier.weight(1f), Icons.Filled.Chat, color = C.green) { ui.whatsapp(x.str("phone"), "", x.str("name")) } }
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    Secondary("Enviar recibo", Modifier.weight(1f), Icons.Filled.Send) { receipt = true }
                    if (b > 0.005) Secondary("Recordar pago", Modifier.weight(1f), color = C.rose) { ui.whatsapp(x.str("phone"), core.reminderOrder(x), x.str("name")) }
                    else Secondary("Agradecer", Modifier.weight(1f)) { ui.whatsapp(x.str("phone"), core.thanksOrder(x), x.str("name")) }
                }
            }
        }
        item { SectionCard("Nota interna", action = { Text(if (x.str("note").isEmpty()) "Agregar" else "Editar", color = C.blue, modifier = Modifier.clickable { note = x.str("note") }) }) {
            Text(x.str("note").ifEmpty { "Solo para ti: el cliente nunca la ve." }, color = if (x.str("note").isEmpty()) MaterialTheme.colorScheme.onSurfaceVariant else MaterialTheme.colorScheme.onSurface) } }
        item {
            SectionCard("Fotos (${photos.size})") {
                if (photos.isEmpty()) Text("Puedes agregar fotos desde Editar.", color = MaterialTheme.colorScheme.onSurfaceVariant)
                Row(Modifier.horizontalScroll(rememberScrollState()), horizontalArrangement = Arrangement.spacedBy(8.dp)) { photos.forEachIndexed { i, s -> Photo(Images.decode(s, 400), Modifier.size(96.dp)) { viewer = i } } }
            }
        }
        item {
            SectionCard("Historial") {
                val ev = ArrayList<Triple<Any?, String, Pair<String, androidx.compose.ui.graphics.Color>>>()
                ev.add(Triple(x["date"], "Encargo creado · ${core.shippingName(core.typeOf(x))} · ${money(x["lbCost"])}/lb", money(x["cost"]) to C.blue))
                (x["paymentHistory"] as? JArr)?.objs()?.forEach { h -> ev.add(Triple(h["date"], "Pago · ${h.str("method").ifEmpty { "Efectivo" }}" + (if (h.str("note").isNotEmpty()) " · " + h.str("note") else ""), "+" + money(h["amount"]) to C.green)) }
                (x["lbHistory"] as? JArr)?.objs()?.forEach { h -> ev.add(Triple(h["date"], "Libras: ${Js.toFixed(Js.num(h["from"]), 2)} → ${Js.toFixed(Js.num(h["to"]), 2)}", (if (Js.num(h["change"]) >= 0) "+" else "") + Js.toFixed(Js.num(h["change"]), 2) + " lb" to C.mut)) }
                ev.sortedByDescending { Js.parseDate(it.first).let { d -> if (d.isNaN()) 0.0 else d } }.forEach { (d, txt, vc) ->
                    Row(verticalAlignment = Alignment.CenterVertically) { Column(Modifier.weight(1f)) { Text(txt, fontSize = 14.sp); Text(Js.fmt(d), fontSize = 11.sp, color = MaterialTheme.colorScheme.onSurfaceVariant) }; Text(vc.first, color = vc.second, fontWeight = FontWeight.Bold) }
                }
            }
        }
        item { Secondary("Ver ficha del cliente", Modifier.fillMaxWidth(), Icons.Filled.Person) { ui.go(Screen.Client(x.str("name"))) } }
    }
    viewer?.let { i -> PhotoViewer(photos, i, onDelete = if (i > 0 || x.str("photo").isEmpty()) ({ idx -> ui.ask("📷 Eliminar foto", "¿Eliminar esta foto del encargo?", "Eliminar", true) { core.deleteOrderPhoto(x["id"], if (x.str("photo").isEmpty()) idx else idx - 1); viewer = null } }) else null) { viewer = null } }
    if (receipt) ReceiptDialog(x) { receipt = false }
    note?.let { n -> NoteDialog(n, { note = null }) { t2 -> if (core.setNote(x["id"], t2)) ui.toast("✅ Nota guardada."); note = null } }
    @Suppress("UNUSED_VARIABLE") val u = v
}

@Composable
fun PhotoViewer(list: List<String>, start: Int, onDelete: ((Int) -> Unit)?, dismiss: () -> Unit) {
    var i by remember { mutableStateOf(start) }
    AlertDialog(onDismissRequest = dismiss, confirmButton = { TextButton(onClick = dismiss) { Text("Cerrar") } },
        dismissButton = { Row { if (list.size > 1) { TextButton(onClick = { i = (i - 1 + list.size) % list.size }) { Text("‹") }; TextButton(onClick = { i = (i + 1) % list.size }) { Text("›") } }
            if (onDelete != null) TextButton(onClick = { onDelete(i) }) { Text("Eliminar foto", color = C.rose) } } },
        text = { Photo(Images.decode(list.getOrNull(i), 1400), Modifier.fillMaxWidth().height(380.dp)) })
}

/* ======================= Editar encargo (estado, libras, pago, fotos) ======================= */
@Composable
fun EditOrderScreen(id: String) {
    val ui = LocalUi.current; val core = ui.core; val v = LocalVersion.current
    val x = core.byId(id) ?: run { Empty("Este encargo ya no existe."); return }
    var st by rememberSaveable { mutableStateOf(x.str("status").ifEmpty { "Pedido" }) }
    var addLb by rememberSaveable { mutableStateOf("") }; var pay by rememberSaveable { mutableStateOf("") }
    var method by rememberSaveable { mutableStateOf("Efectivo") }; var payNote by rememberSaveable { mutableStateOf("") }
    var viewer by remember { mutableStateOf<Int?>(null) }
    val pick = rememberLauncherForActivityResult(ActivityResultContracts.GetContent()) { uri ->
        if (uri != null) ui.scope.launch { val d = withContext(Dispatchers.IO) { Images.fromUri(ui.act, uri, 900, 55) }; if (d != null && core.addOrderPhoto(x["id"], d)) ui.toast("✅ Foto guardada.") } }
    val lbv = Js.num(addLb); val pv = Js.num(pay); val pp = Js.num(x["pounds"]) + lbv
    val tot = Js.num(x["cost"]) + Js.num(x["lbCost"]) * pp; val pd = Js.num(x["paid"]) + pv; val bal = tot - pd
    val photos = core.orderPhotos(x)
    Page {
        item {
            SectionCard {
                Row(verticalAlignment = Alignment.CenterVertically) { Avatar(x.str("name"), C.status(st), 48); Spacer(Modifier.width(10.dp)); Column { Text(x.str("name"), fontSize = 18.sp, fontWeight = FontWeight.ExtraBold); Text(x.str("phone").ifEmpty { "Sin teléfono" }, fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant) } }
                Chip("🔒 " + core.shippingName(core.typeOf(x)) + " · " + money(x["lbCost"]) + "/lb", C.teal)
                Text("El tipo de envío y la tarifa quedan fijos para conservar el precio original.", fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
        }
        item {
            SectionCard("Resumen", action = { Text("${Math.round((if (tot > 0) (pd / tot).coerceIn(0.0, 1.0) else 1.0) * 100)}% pagado", color = MaterialTheme.colorScheme.onSurfaceVariant) }) {
                Line("Total", money(tot), bold = true); Line("Pagado", money(pd), C.green); Line("Saldo", if (bal > 0) "- " + money(bal) else if (bal < 0) "+ " + money(-bal) else "Pagado completo", if (bal > 0) C.rose else C.green, bold = true)
                ProgressBar(if (tot > 0) pd / tot else 1.0)
            }
        }
        item { SectionCard("Estado") { Choice(Encargos.STATES.map { it to it }, st) { st = it } } }
        item {
            SectionCard("Agregar libras", action = { Text("Ahora: ${lb(Js.num(x["pounds"]))}", color = MaterialTheme.colorScheme.onSurfaceVariant, fontSize = 12.sp) }) {
                Field(addLb, { addLb = it }, "Libras a agregar", number = true, suffix = "lb")
                Row(horizontalArrangement = Arrangement.spacedBy(6.dp)) { listOf(0.5, 1.0, 2.0, 5.0).forEach { q -> Tonal("+" + Js.numberToString(q), Modifier.weight(1f)) { addLb = Js.toFixed(Js.num(addLb) + q, 2) } } }
                val g = pp * core.marginOf(x)
                Text("${Js.toFixed(pp, 2)} lb × (${money(x["lbCost"])} − ${money(core.buyOf(x))}) = ${if (g < 0) "-" else ""}${money(kotlin.math.abs(g))}", fontSize = 12.sp, color = if (g >= 0) C.green else C.rose)
            }
        }
        item {
            SectionCard("Registrar pago", action = { Text(if (bal > 0.005) "Debe ${money(bal)}" else "Sin saldo pendiente", fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant) }) {
                Field(pay, { pay = it }, "Cantidad", number = true, prefix = "$")
                Row(horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                    val due = kotlin.math.max(0.0, Js.num(x["cost"]) + Js.num(x["lbCost"]) * pp - Js.num(x["paid"]))
                    Tonal("Todo el saldo", Modifier.weight(1f)) { pay = Js.toFixed(due, 2) }; Tonal("La mitad", Modifier.weight(1f)) { pay = Js.toFixed(due / 2, 2) }; Tonal("Sin pago", Modifier.weight(1f)) { pay = "" }
                }
                Choice(listOf("Efectivo" to "Efectivo", "USD" to "USD"), method) { method = it }
                Field(payNote, { payNote = it }, "Nota del pago (ej. Abono)")
            }
        }
        item {
            Primary("Guardar cambios", Modifier.fillMaxWidth(), Icons.Filled.Save) {
                val before = core.completedClients().map { it.key }.toSet()
                core.saveEdit(x["id"], lbv, pv, method, payNote, st).onSuccess {
                    val moved = core.completedClients().filter { !before.contains(it.key) }
                    ui.toast(if (moved.isNotEmpty()) "✅ ${moved.joinToString { it.name }} completó todos sus pagos. Ahora está en Encargos completados." else "✅ Cambios guardados.")
                    ui.back()
                }.onFailure { ui.toast(it.message ?: "No se pudo guardar.") }
            }
        }
        item {
            SectionCard("Fotos del encargo") {
                Row(Modifier.horizontalScroll(rememberScrollState()), horizontalArrangement = Arrangement.spacedBy(8.dp)) { photos.forEachIndexed { i, s -> Photo(Images.decode(s, 400), Modifier.size(90.dp)) { viewer = i } } }
                Secondary("Agregar foto", Modifier.fillMaxWidth(), Icons.Filled.AddAPhoto) { pick.launch("image/*") }
            }
        }
        item {
            SectionCard("Historial de pagos") {
                val ph = (x["paymentHistory"] as? JArr)?.objs() ?: emptyList()
                if (ph.isEmpty()) Text("Sin pagos", color = MaterialTheme.colorScheme.onSurfaceVariant)
                ph.withIndex().reversed().forEach { (i, h) -> Row(Modifier.fillMaxWidth().clickable { editPaymentState.value = Pair(Js.str(x["id"]), i) }.padding(vertical = 4.dp)) {
                    Column(Modifier.weight(1f)) { Text(Js.fmt(h["date"])); Text(h.str("method").ifEmpty { "Pago" } + (if (h.str("note").isNotEmpty()) " · " + h.str("note") else ""), fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant) }
                    Text("+ " + money(h["amount"]), color = C.green, fontWeight = FontWeight.Bold) } }
                Text("Toca un pago para corregirlo o eliminarlo.", fontSize = 11.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
        }
        item {
            SectionCard("Historial de libras") {
                val lh = (x["lbHistory"] as? JArr)?.objs() ?: emptyList()
                if (lh.isEmpty()) Text("Sin registros", color = MaterialTheme.colorScheme.onSurfaceVariant)
                lh.reversed().forEach { h -> Line(Js.fmt(h["date"]), "${Js.toFixed(Js.num(h["from"]), 2)} → ${Js.toFixed(Js.num(h["to"]), 2)} lb") }
            }
        }
    }
    viewer?.let { i -> PhotoViewer(photos, i, onDelete = if (i > 0 || x.str("photo").isEmpty()) ({ idx -> ui.ask("📷 Eliminar foto", "¿Eliminar esta foto del encargo?", "Eliminar", true) { core.deleteOrderPhoto(x["id"], if (x.str("photo").isEmpty()) idx else idx - 1); viewer = null } }) else null) { viewer = null } }
    PaymentEditHost()
    @Suppress("UNUSED_VARIABLE") val u = v
}

/* ======================= Encargos completados ======================= */
@Composable
fun CompletedScreen() {
    val ui = LocalUi.current; val core = ui.core; val v = LocalVersion.current
    var q by rememberSaveable { mutableStateOf("") }
    val all = core.completedClients().filter { c -> q.isBlank() || Js.normSearch(c.name + " " + c.phone).contains(Js.normSearch(q)) }
    Page {
        item { Text("Clientes con todos sus pagos completados (${core.completedClients().size}).", color = MaterialTheme.colorScheme.onSurfaceVariant) }
        item { SearchBox(q, { q = it }, "Buscar cliente") }
        if (all.isEmpty()) item { Empty("Aún no hay clientes con todos sus pagos completados.") }
        all.forEach { c -> item {
            SectionCard(modifier = Modifier.clickable { ui.go(Screen.Client(c.name)) }) {
                Row(verticalAlignment = Alignment.CenterVertically) { Avatar(c.name, C.green); Spacer(Modifier.width(10.dp)); Column { Text(c.name, fontWeight = FontWeight.Bold); Text("${c.phone.ifEmpty { "Sin teléfono" }} · ${c.orders.size} encargo(s)", fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant) } }
                c.orders.sortedByDescending { Js.parseDate(it["date"]) }.forEach { o -> Line("${Js.fmt(o["date"])} · ${o.str("status")}", money(o["total"])) }
                Line("Libras", lb(c.pounds)); Line("Total cobrado", money(c.paid), C.green, true)
                Text("✓ Todos los pagos completados", color = C.green, fontWeight = FontWeight.Bold)
            }
        } }
    }
    @Suppress("UNUSED_VARIABLE") val u = v
}
