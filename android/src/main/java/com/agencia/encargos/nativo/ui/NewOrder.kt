package com.agencia.encargos.nativo.ui

import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.AddAPhoto
import androidx.compose.material.icons.filled.Save
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.Alignment
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.Spacer
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.agencia.encargos.core.Encargos
import com.agencia.encargos.core.Js
import com.agencia.encargos.core.jobj
import com.agencia.encargos.core.str
import com.agencia.encargos.nativo.util.Images
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

@Composable
fun NewOrderScreen(forName: String?, forPhone: String?) {
    val ui = LocalUi.current; val core = ui.core; val v = LocalVersion.current
    val draft = remember { if (forName == null) core.ls.obj("encargos_v4_draft") else jobj() }
    var name by rememberSaveable { mutableStateOf(forName ?: draft.str("name")) }
    var phone by rememberSaveable { mutableStateOf(forPhone ?: draft.str("phone")) }
    var type by rememberSaveable { mutableStateOf(draft.str("shippingType").ifEmpty { "avion" }) }
    var rate by rememberSaveable { mutableStateOf(draft.str("lbCost").ifEmpty { Js.toFixed(core.rateOf(type), 2) }) }
    var pounds by rememberSaveable { mutableStateOf(draft.str("pounds")) }
    var cost by rememberSaveable { mutableStateOf(draft.str("cost")) }
    var fund by rememberSaveable { mutableStateOf(draft.str("fund")) }
    var method by rememberSaveable { mutableStateOf(draft.str("fundMethod").ifEmpty { "Efectivo" }) }
    var status by rememberSaveable { mutableStateOf(draft.str("status").ifEmpty { "Pedido" }) }
    var photo by remember { mutableStateOf("") }
    val pick = rememberLauncherForActivityResult(ActivityResultContracts.GetContent()) { uri -> if (uri != null) ui.scope.launch { photo = withContext(Dispatchers.IO) { Images.fromUri(ui.act, uri) } ?: "" } }

    // borrador automático (como la app web)
    LaunchedEffect(name, phone, type, rate, pounds, cost, fund, method, status) {
        if (forName == null) core.ls.put("encargos_v4_draft", jobj("name" to name, "phone" to phone, "cost" to cost, "lbCost" to rate, "pounds" to pounds, "fund" to fund, "fundMethod" to method, "status" to status, "shippingType" to type))
    }
    val c = Js.num(cost); val lc = Js.num(rate); val p = Js.num(pounds); val f = Js.num(fund)
    val ship = lc * p; val total = c + ship; val bal = total - f
    val buy = Js.num(core.buyRates()[type]); val gain = p * (lc - buy)
    val suggestions = if (name.length >= 2 && forName == null) core.clients().filter { it.name.lowercase().contains(name.trim().lowercase()) && it.name != name }.take(4) else emptyList()

    Page(title = if (forName != null) "Nuevo encargo para $forName" else "Nuevo encargo", sub = "Completa los datos y guarda") {
        item {
            // Vista previa del encargo (como en la web): se actualiza mientras escribes
            SectionCard {
                Text("VISTA PREVIA DEL ENCARGO", fontSize = 10.5.sp, fontWeight = FontWeight.ExtraBold, letterSpacing = 0.8.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Avatar(name.ifBlank { "?" }, C.blue, 44); Spacer(Modifier.width(10.dp))
                    Column(Modifier.weight(1f)) {
                        Text(name.ifBlank { "Nuevo cliente" }, fontSize = 17.sp, fontWeight = FontWeight.ExtraBold, maxLines = 1)
                        Text((phone.ifBlank { "Sin teléfono" }) + " · " + Js.toFixed(p, 1) + " lb", fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                    }
                    StatusChip(status)
                }
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    Kpi("Total", money(total), modifier = Modifier.weight(1f)); Kpi("Pagado", money(f), C.green, modifier = Modifier.weight(1f))
                    Kpi("Saldo", if (bal < 0) "+ " + money(-bal) else money(bal), if (bal > 0) C.rose else C.green, modifier = Modifier.weight(1f))
                }
                ProgressBar(if (total > 0) (f / total).coerceIn(0.0, 1.0) else 0.0, C.green)
                Row { Text("Pedido ${money(c)} + envío ${money(ship)}", Modifier.weight(1f), fontSize = 12.5.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                    Text(if (c <= 0) "Escribe el costo" else "Total ${money(total)}", fontSize = 12.5.sp, fontWeight = FontWeight.Bold) }
            }
        }
        item {
            SectionCard("Cliente") {
                Field(name, { name = it }, "Nombre del cliente")
                suggestions.forEach { s -> Text("↳ ${s.name} · ${s.phone}", color = C.blue, modifier = Modifier.fillMaxWidth().clickable { name = s.name; if (phone.isBlank()) phone = s.phone }.padding(vertical = 4.dp)) }
                Field(phone, { phone = it }, "Teléfono (WhatsApp)", number = false)
            }
        }
        item {
            SectionCard("Pedido y envío") {
                Field(cost, { cost = it }, "Costo del pedido", number = true, prefix = "$")
                Text("Tipo de envío", fontSize = 13.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                Choice(core.shippingRates.keys.map { it to core.shippingLabel(it) }, type) { type = it; rate = Js.toFixed(core.rateOf(it), 2) }
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    Field(rate, { rate = it }, "Tarifa por libra", Modifier.weight(1f), number = true, prefix = "$")
                    Field(pounds, { pounds = it }, "Libras", Modifier.weight(1f), number = true, suffix = "lb")
                }
                Row(horizontalArrangement = Arrangement.spacedBy(6.dp)) { listOf(0.5, 1.0, 2.0, 5.0).forEach { q -> Tonal("+" + Js.numberToString(q), Modifier.weight(1f)) { pounds = Js.toFixed(Js.num(pounds) + q, 2) } } }
            }
        }
        item {
            SectionCard("Pago inicial y estado") {
                Field(fund, { fund = it }, "Pago inicial (opcional)", number = true, prefix = "$")
                Choice(listOf("Efectivo" to "Efectivo", "USD" to "USD"), method) { method = it }
                Text("Estado", fontSize = 13.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                Choice(Encargos.STATES.map { it to it }, status) { status = it }
            }
        }
        item {
            SectionCard("Foto de la compra") {
                if (photo.isNotEmpty()) Photo(Images.decode(photo), Modifier.fillMaxWidth().height(180.dp)) { ui.ask("Quitar foto", "¿Quitar la foto de este encargo?", "Quitar", true) { photo = "" } }
                Secondary(if (photo.isEmpty()) "Agregar foto" else "Cambiar foto", Modifier.fillMaxWidth(), Icons.Filled.AddAPhoto) { pick.launch("image/*") }
            }
        }
        item {
            SectionCard("Resumen") {
                Line("Envío (${Js.toFixed(p, 2)} lb × ${money(lc)})", money(ship))
                Line("Total", money(total), bold = true)
                Line("Pagado", money(f), C.green)
                Line(if (bal < 0) "A favor" else "Saldo", if (bal > 0) "- " + money(bal) else if (bal < 0) "+ " + money(-bal) + " a favor" else "Pagado completo", if (bal > 0) C.rose else C.green, bold = true)
                if (p > 0) Line("Ganancia (${Js.toFixed(p, 2)} lb × (${money(lc)} − ${money(buy)}))", (if (gain < 0) "-" else "") + money(kotlin.math.abs(gain)), if (gain >= 0) C.green else C.rose)
            }
        }
        item {
            Primary("Guardar encargo", Modifier.fillMaxWidth(), Icons.Filled.Save) {
                core.createOrder(Encargos.NewOrder(name, phone, c, lc, p, f, method, status, type, photo))
                    .onSuccess { x -> core.ls.del("encargos_v4_draft"); ui.toast("✅ Encargo guardado correctamente."); ui.root(Screen.Orders); ui.go(Screen.Order(Js.str(x["id"]))) }
                    .onFailure { ui.toast(it.message ?: "No se pudo guardar.") }
            }
        }
        item { Secondary("Limpiar formulario", Modifier.fillMaxWidth()) { name = ""; phone = ""; cost = ""; pounds = ""; fund = ""; photo = ""; status = "Pedido"; type = "avion"; rate = Js.toFixed(core.rateOf("avion"), 2); core.ls.del("encargos_v4_draft") } }
    }
}
