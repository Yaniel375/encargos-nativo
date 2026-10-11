package com.agencia.encargos.nativo.ui

import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
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
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.AddAPhoto
import androidx.compose.material.icons.filled.Remove
import androidx.compose.material.icons.outlined.History
import androidx.compose.material.icons.outlined.Inventory2
import androidx.compose.material.icons.outlined.PhotoLibrary
import androidx.compose.material.icons.outlined.Scale
import androidx.compose.material.icons.outlined.Sell
import androidx.compose.material.icons.outlined.Share
import androidx.compose.material.icons.outlined.Storefront
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateListOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.agencia.encargos.core.Encargos
import com.agencia.encargos.core.JArr
import com.agencia.encargos.core.JObj
import com.agencia.encargos.core.Js
import com.agencia.encargos.core.str
import com.agencia.encargos.nativo.util.Files
import com.agencia.encargos.nativo.util.Images
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import kotlin.math.roundToInt

/* ---------------- Notas → En venta: artículos que tienes a la venta ----------------
 * Cálculos automáticos de todos los artículos (valor en venta, invertido, ganancia posible, cobrado y ganancia real),
 * varias fotos por artículo, registrar ventas (baja las unidades, con deshacer) y compartir con fotos.
 * El dinero que entra o se gana va en verde. */

private fun stockOf(n: JObj) = Js.num(n["stock"]).toInt().coerceAtLeast(0)
private fun photosOf(n: JObj) = (n["photos"] as? JArr)?.filterIsInstance<String>() ?: emptyList()
/** Número escrito por el usuario (acepta coma decimal); vacío o inválido = NaN se trata aparte. */
private fun numIn(t: String) = Js.num(t.replace(',', '.').replace("$", ""))
private fun pct(gain: Double, cost: Double) = if (cost > 0.005) " (${(gain / cost * 100).roundToInt()} %)" else ""

/** Texto para compartir el artículo (WhatsApp, Facebook…), sin datos de clientes. */
private fun shareText(core: Encargos, n: JObj): String {
    val st = stockOf(n); val biz = core.businessName.ifBlank { "" }
    return listOfNotNull(n.str("title"), "Precio: ${money(n["price"])}", n.str("desc").ifBlank { null },
        if (st > 0) (if (st == 1) "Queda 1 disponible" else "$st disponibles") else "Agotado", biz.ifBlank { null }).joinToString("\n")
}

@Composable
fun SaleScreen(onTab: (String) -> Unit) {
    val ui = LocalUi.current; val core = ui.core; val v = LocalVersion.current
    var q by rememberSaveable { mutableStateOf("") }; var filter by rememberSaveable { mutableStateOf("all") }
    var editing by remember { mutableStateOf<JObj?>(null) }; var creating by remember { mutableStateOf(false) }; var selling by remember { mutableStateOf<JObj?>(null) }
    val all = core.saleItems(); val sm = core.saleSummary(all); val nq = Js.normSearch(q)
    val list = all.filter { (nq.isEmpty() || Js.normSearch(it.str("title") + " " + it.str("desc")).contains(nq)) &&
        when (filter) { "avail" -> stockOf(it) > 0; "out" -> stockOf(it) == 0; else -> true } }
    if (editing != null || creating) { SaleEditor(editing) { editing = null; creating = false }; return }
    selling?.let { n -> SellSheet(n) { selling = null } }
    Page(right = plural(sm.items, "artículo", "artículos")) {
        item { Choice(listOf("notes" to "Notas", "sale" to "En venta"), "sale") { onTab(it) } }
        item { SaleSummaryCard(sm) }
        item { Primary("Nuevo artículo", Modifier.fillMaxWidth(), Icons.Filled.Add) { creating = true } }
        if (all.isNotEmpty()) {
            item { SearchBox(q, { q = it }, "Buscar artículo") }
            item { Choice(listOf("all" to "Todos (${all.size})", "avail" to "Disponibles (${sm.available})", "out" to "Agotados (${all.size - sm.available})"), filter) { filter = it } }
        }
        if (all.isEmpty()) item { Empty("Aún no tienes artículos en venta", "Agrega lo que vendes con sus fotos, precio y cantidad: la app suma sola cuánto vale todo y cuánto vas ganando.", Icons.Outlined.Storefront) }
        else if (list.isEmpty()) item { Empty("Ningún artículo coincide.") }
        list.chunked(2).forEach { pair -> item {
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(10.dp), verticalAlignment = Alignment.Top) {
                pair.forEach { n -> SaleCard(n, Modifier.weight(1f), open = { editing = n }, sell = { selling = n }) }
                if (pair.size == 1) Spacer(Modifier.weight(1f))
            }
        } }
    }
    @Suppress("UNUSED_VARIABLE") val u = v
}

/** Resumen con los cálculos automáticos de todos los artículos (inversión con envío por peso y ganancia neta). */
@Composable
private fun SaleSummaryCard(sm: Encargos.SaleSummary) {
    SectionCard("Tu mercancía", icon = Icons.Outlined.Storefront) {
        Text("Valor en venta", fontSize = 12.5.sp, fontWeight = FontWeight.SemiBold, color = MaterialTheme.colorScheme.onSurfaceVariant)
        Text(money(countUp(sm.value)), fontSize = 30.sp, fontWeight = FontWeight.ExtraBold, letterSpacing = (-0.6).sp, color = C.green, maxLines = 1)
        Hint(if (sm.units > 0) "Lo que cobrarías si vendes las ${plural(sm.units, "unidad", "unidades")} que tienes" else "Agrega artículos para ver cuánto vale tu mercancía")
        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            Kpi("Artículos", "${sm.items}", modifier = Modifier.weight(1f)); Kpi("Unidades", "${sm.units}", modifier = Modifier.weight(1f))
            Kpi("Peso", "${Js.toFixed(sm.lbs, 2)} lb", modifier = Modifier.weight(1f))
        }
        // rótulos cortos (caben en el recuadro) y la explicación debajo
        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            Kpi("Inversión", money(sm.invested), sub = "en lo que tienes", modifier = Modifier.weight(1f))
            Kpi("Inversión total", money(sm.totalInvested), sub = "con lo vendido", modifier = Modifier.weight(1f))
        }
        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            Kpi("Por ganar", money(sm.potential), if (sm.potential >= 0) C.green else C.rose, sub = "neto, si vendes todo", modifier = Modifier.weight(1f))
            Kpi("Ganancia neta", money(sm.realized), if (sm.realized >= 0) C.green else C.rose, sub = "de lo vendido", modifier = Modifier.weight(1f))
        }
        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            Kpi("Cobrado en ventas", money(sm.income), C.green, sub = plural(sm.soldUnits, "unidad vendida", "unidades vendidas"), modifier = Modifier.weight(1f))
        }
    }
}

/** Artículo en tarjeta: foto, nombre, precio en verde, unidades y acciones rápidas. */
@Composable
private fun SaleCard(n: JObj, modifier: Modifier, open: () -> Unit, sell: () -> Unit) {
    val ui = LocalUi.current; val core = ui.core
    val ph = photosOf(n); val st = stockOf(n); val price = Js.num(n["price"]); val cost = core.saleUnitCost(n); val w = Js.num(n["weight"])
    Column(modifier.glass(RoundedCornerShape(18.dp)).clickable { open() }.padding(12.dp), verticalArrangement = Arrangement.spacedBy(7.dp)) {
        if (ph.isNotEmpty()) Box {
            FitPhoto(Images.decode(ph[0], 600), maxHeight = 150) { open() }
            if (ph.size > 1) Text("+${ph.size - 1}", fontSize = 11.sp, fontWeight = FontWeight.ExtraBold, color = Color.White,
                modifier = Modifier.align(Alignment.TopEnd).padding(6.dp).clip(RoundedCornerShape(8.dp)).background(Color(0x99000000)).padding(horizontal = 7.dp, vertical = 2.dp))
        }
        Text(n.str("title").ifEmpty { "Sin nombre" }, fontWeight = FontWeight.ExtraBold, maxLines = 2, overflow = TextOverflow.Ellipsis)
        Text(money(price), fontSize = 19.sp, fontWeight = FontWeight.ExtraBold, color = C.green, maxLines = 1)
        if (st > 0) Chip(if (st == 1) "1 disponible" else "$st disponibles", C.blue) else Chip("Agotado", C.rose)
        if (cost > 0.005) Text("Ganancia neta ${money(price - cost)} c/u" + pct(price - cost, cost), fontSize = 11.5.sp, fontWeight = FontWeight.SemiBold, color = if (price >= cost) C.green else C.rose)
        if (w > 0) Text("${Js.toFixed(w, 2)} lb c/u · costo ${money(cost)}", fontSize = 11.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
        Row(horizontalArrangement = Arrangement.spacedBy(6.dp)) {
            CardBtn(if (st > 0) "Vendí" else "Agotado", Icons.Outlined.Sell, Modifier.weight(1f), tint = if (st > 0) C.green else null) { if (st > 0) sell() else ui.toast("No quedan unidades. Edita el artículo para reponer.") }
            CardBtn(null, Icons.Outlined.Share, desc = "Compartir") { Files.shareImages(ui.act, ph, shareText(core, n)) }
        }
    }
}

/** Ventana para registrar una venta: cantidad y precio (por si hiciste rebaja). */
@Composable
private fun SellSheet(n: JObj, close: () -> Unit) {
    val ui = LocalUi.current; val core = ui.core; val st = stockOf(n)
    var qty by remember { mutableStateOf(1) }; var price by remember { mutableStateOf(Js.toFixed(Js.num(n["price"]), 2)) }
    val p = numIn(price); val total = if (p.isNaN()) 0.0 else p * qty; val uc = core.saleUnitCost(n); val gain = if (p.isNaN()) 0.0 else (p - uc) * qty
    Sheet("Registrar venta", close, Icons.Outlined.Sell, n.str("title")) {
        Text("Cantidad vendida", fontSize = 12.5.sp, fontWeight = FontWeight.SemiBold, color = MaterialTheme.colorScheme.onSurfaceVariant)
        QtyStepper(qty, 1, st) { qty = it }
        Field(price, { price = it }, "Precio por unidad", number = true, prefix = "$")
        SectionCard {
            Line("Total de la venta", money(total), C.green, true)
            if (uc > 0.005) { Line("Costo (compra + envío + otros)", money(uc * qty)); Line("Ganancia neta de esta venta", money(gain), if (gain >= 0) C.green else C.rose, true) }
            Line("Quedarán", plural(st - qty, "unidad", "unidades"))
        }
        Primary("Registrar venta", Modifier.fillMaxWidth(), Icons.Outlined.Sell, color = C.green) {
            core.sellSaleItem(n.str("id"), qty, numIn(price)).onSuccess {
                close(); ui.toast("✅ Venta registrada · ${money(total)}") { core.undoLastSale(n.str("id")) }
            }.onFailure { ui.toast(it.message ?: "") }
        }
    }
}

/** − cantidad + (no baja de min ni pasa de max). */
@Composable
private fun QtyStepper(value: Int, min: Int, max: Int, onChange: (Int) -> Unit) {
    Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(10.dp)) {
        GlassIconBtn(Icons.Filled.Remove, "Menos", enabled = value > min) { onChange((value - 1).coerceAtLeast(min)) }
        Text("$value", fontSize = 22.sp, fontWeight = FontWeight.ExtraBold, textAlign = TextAlign.Center, modifier = Modifier.width(56.dp))
        GlassIconBtn(Icons.Filled.Add, "Más", enabled = value < max) { onChange((value + 1).coerceAtMost(max)) }
    }
}

@Composable
fun SaleEditor(n: JObj?, close: () -> Unit) {
    val ui = LocalUi.current; val core = ui.core
    var name by remember { mutableStateOf(n?.str("title") ?: "") }; var desc by remember { mutableStateOf(n?.str("desc") ?: "") }
    var price by remember { mutableStateOf(if (n != null) Js.toFixed(Js.num(n["price"]), 2) else "") }
    var cost by remember { mutableStateOf(if (n != null && Js.num(n["cost"]) > 0) Js.toFixed(Js.num(n["cost"]), 2) else "") }
    var stock by remember { mutableStateOf(if (n != null) stockOf(n) else 1) }
    // pesaje y costos: libras por unidad, tipo de envío, costo por libra (de Ajustes → Costo por libra) y otros gastos por unidad
    val buy = core.buyRates()
    var shipType by remember { mutableStateOf(n?.str("shipType")?.ifEmpty { null } ?: "avion") }
    var weight by remember { mutableStateOf(if (n != null && Js.num(n["weight"]) > 0) Js.numberToString(Js.num(n["weight"])) else "") }
    var lbRate by remember { mutableStateOf(if (n != null && n["lbRate"] != null) Js.toFixed(Js.num(n["lbRate"]), 2) else Js.toFixed(Js.num(buy[shipType]), 2)) }
    var extra by remember { mutableStateOf(if (n != null && Js.num(n["extra"]) > 0) Js.toFixed(Js.num(n["extra"]), 2) else "") }
    val photos = remember { mutableStateListOf<String>().apply { if (n != null) addAll(photosOf(n)) } }
    var viewer by remember { mutableStateOf<Int?>(null) }
    androidx.activity.compose.BackHandler { close() }
    // se pueden elegir varias fotos a la vez de la galería
    val pick = rememberLauncherForActivityResult(ActivityResultContracts.GetMultipleContents()) { uris ->
        if (uris.isNotEmpty()) ui.scope.launch {
            val got = withContext(Dispatchers.IO) { uris.take(10).mapNotNull { Images.fromUri(ui.act, it, 900, 55) } }
            photos.addAll(got); if (got.isNotEmpty()) ui.toast("📷 ${plural(got.size, "foto agregada", "fotos agregadas")}. Toca \"Guardar artículo\" para guardarlas.")
        }
    }
    fun ok(t: String) = numIn(t).let { if (it.isNaN()) 0.0 else it }
    val p = ok(price); val buyC = ok(cost); val w = ok(weight); val rate = ok(lbRate); val ex = ok(extra)
    val ship = w * rate; val c = buyC + ship + ex   // costo completo de una unidad
    Page(title = if (n == null) "Nuevo artículo" else "Editar artículo") {
        item {
            SectionCard("Fotos", icon = Icons.Outlined.PhotoLibrary, action = { Text(plural(photos.size, "foto", "fotos"), fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant) }) {
                if (photos.isNotEmpty()) PhotoStrip(photos.toList(), 110) { viewer = it } else Hint("Agrega varias fotos: la primera es la que se ve en la lista.")
                Secondary("Agregar fotos", Modifier.fillMaxWidth(), Icons.Filled.AddAPhoto) { pick.launch("image/*") }
            }
        }
        item { Field(name, { name = it }, "Nombre del artículo") }
        item { Field(desc, { desc = it }, "Descripción (talla, color, estado…)", singleLine = false, minLines = 3) }
        item { Field(price, { price = it }, "Precio de venta (por unidad)", number = true, prefix = "$") }
        item {
            SectionCard("Costos y pesaje", icon = Icons.Outlined.Scale) {
                Hint("Para saber tu inversión y tu ganancia neta: lo que pagaste, el envío por su peso y otros gastos. Todo por unidad.")
                Field(cost, { cost = it }, "Precio de compra", number = true, prefix = "$")
                Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                    Field(weight, { weight = it }, "Peso", Modifier.weight(1f), number = true, suffix = "lb")
                    Field(lbRate, { lbRate = it }, "Envío por libra", Modifier.weight(1f), number = true, prefix = "$")
                }
                Choice(core.shippingRates.keys.map { it to core.shippingName(it) }, shipType) { t -> shipType = t; lbRate = Js.toFixed(Js.num(buy[t]), 2) }
                Field(extra, { extra = it }, "Otros gastos (aduana, empaque…)", number = true, prefix = "$")
            }
        }
        item { Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
            Text("Unidades disponibles", fontSize = 12.5.sp, fontWeight = FontWeight.SemiBold, color = MaterialTheme.colorScheme.onSurfaceVariant)
            QtyStepper(stock, 0, 9999) { stock = it }
        } }
        // cálculo automático mientras escribes
        item {
            SectionCard("Cálculo automático", icon = Icons.Outlined.Inventory2) {
                if (c > 0.005) {
                    Line("Compra", money(buyC))
                    if (ship > 0.005) Line("Envío · ${Js.toFixed(w, 2)} lb × ${money(rate)}", money(ship))
                    if (ex > 0.005) Line("Otros gastos", money(ex))
                    Line("Costo total por unidad", money(c), bold = true)
                    Line("Ganancia neta por unidad", money(p - c) + pct(p - c, c), if (p >= c) C.green else C.rose, true)
                }
                Line("Valor de las ${plural(stock, "unidad", "unidades")}", money(p * stock), C.green, true)
                if (c > 0.005) {
                    Line("Tu inversión en ellas", money(c * stock), bold = true)
                    Line("Ganancia neta si vendes todo", money((p - c) * stock), if (p >= c) C.green else C.rose, true)
                }
                if (w > 0) Line("Peso total", "${Js.toFixed(w * stock, 2)} lb")
                if (c > 0.005 && p < c) Text("El precio es menor que lo que te cuesta cada unidad: perderías dinero en cada venta.", fontSize = 12.sp, color = C.rose)
                if (c <= 0.005) Hint("Escribe el precio de compra y el peso para ver tu inversión y tu ganancia neta.")
            }
        }
        val sales = if (n != null) core.saleSales(n) else emptyList()
        if (sales.isNotEmpty()) item {
            SectionCard("Ventas", icon = Icons.Outlined.History, action = { Text(money(sales.sumOf { Js.num(it["qty"]) * Js.num(it["price"]) }), color = C.green, fontWeight = FontWeight.ExtraBold, fontSize = 13.sp) }) {
                sales.reversed().take(20).forEach { s -> val q = Js.num(s["qty"]); Line("${dayLabel(s["t"])} · ${q.toInt()} × ${money(s["price"])} · neta ${money(q * (Js.num(s["price"]) - Js.num(s["cost"])))}", money(q * Js.num(s["price"])), C.green) }
                Secondary("Deshacer la última venta", Modifier.fillMaxWidth(), Icons.Outlined.History) {
                    ui.ask("Deshacer venta", "Se devuelven las unidades de la última venta registrada.", "Deshacer", false) { if (core.undoLastSale(n!!.str("id"))) { ui.toast("✅ Venta deshecha."); close() } }
                }
            }
        }
        item {
            Primary("Guardar artículo", Modifier.fillMaxWidth()) {
                fun bad(t: String) = numIn(t).let { if (it.isNaN()) -1.0 else it }
                core.saveSaleItem(n?.str("id"), name, desc, bad(price), bad(cost), stock, photos.toList(), bad(weight), shipType, bad(lbRate), bad(extra))
                    .onSuccess { ui.toast(if (n != null) "✅ Artículo actualizado." else "✅ Artículo guardado."); close() }.onFailure { ui.toast(it.message ?: "") }
            }
        }
        item { if (n != null) Secondary("Eliminar artículo", Modifier.fillMaxWidth(), color = C.rose) { ui.ask("🗑️ Eliminar artículo", "¿Eliminar este artículo y sus fotos? No se puede deshacer.", "Eliminar", true) { core.deleteNote(n.str("id")); close() } }
               else Secondary("Cancelar", Modifier.fillMaxWidth()) { close() } }
        item { Spacer(Modifier.height(4.dp)) }
    }
    viewer?.let { i -> PhotoViewer(photos.toList(), i, onDelete = { idx -> photos.removeAt(idx); viewer = null }) { viewer = null } }
}
