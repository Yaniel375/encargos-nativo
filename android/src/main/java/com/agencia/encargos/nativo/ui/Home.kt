package com.agencia.encargos.nativo.ui

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.interaction.MutableInteractionSource
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
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.KeyboardArrowRight
import androidx.compose.material.icons.outlined.AccountBalanceWallet
import androidx.compose.material.icons.outlined.CreditCard
import androidx.compose.material.icons.outlined.Inventory2
import androidx.compose.material.icons.outlined.LocalShipping
import androidx.compose.material.icons.outlined.People
import androidx.compose.material.icons.outlined.Scale
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.agencia.encargos.core.Js
import com.agencia.encargos.core.str
import java.time.Instant
import java.time.ZoneId
import java.time.format.DateTimeFormatter
import java.util.Locale

/** Inicio — misma composición que la app web: saludo, buscador, "Por cobrar", 4 accesos, estado de los pedidos y últimos pagos. */
@Composable
fun HomeScreen() {
    val ui = LocalUi.current; val app = ui.app; val core = app.core; val v = LocalVersion.current
    val d = app.stats.dashboard(); val ps = app.stats.payments()
    val active = core.activeOrders()
    var q by rememberSaveable { mutableStateOf("") }
    Page(title = null) {
        item {
            Text("¡Hola, ${core.greetName}!", Modifier.padding(top = 4.dp, start = 2.dp),
                style = TextStyle(brush = shineBrush(MaterialTheme.colorScheme.onSurface), fontFamily = Manrope, fontSize = 24.sp, fontWeight = FontWeight.ExtraBold, letterSpacing = (-0.24).sp))
        }
        item { SearchBox(q, { q = it }, "Buscar clientes, encargos o notas") }
        if (q.isNotBlank()) item { GlobalResults(q) }
        item {
            // .aHero: anillo de cobro + "Por cobrar"
            val total = d.paid + d.pending; val pct = if (total > 0) d.paid / total * 100 else 0.0
            Row(Modifier.fillMaxWidth().glass(day = isDay()).padding(20.dp), verticalAlignment = Alignment.CenterVertically) {
                RingGauge(pct, size = 100)
                Spacer(Modifier.width(14.dp))
                Column(Modifier.weight(1f)) {
                    Text("Por cobrar", fontSize = 12.5.sp, fontWeight = FontWeight.SemiBold, color = MaterialTheme.colorScheme.onSurfaceVariant)
                    Text(money(d.pending), fontSize = 32.sp, fontWeight = FontWeight.ExtraBold, letterSpacing = (-0.64).sp, maxLines = 1)
                    Spacer(Modifier.size(4.dp))
                    Legend(W.green, "Cobrado", money(d.paid)); Legend(Color(0xFF64748B), "Pendiente", money(d.pending))
                }
            }
        }
        item {
            Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
                Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                    StatButton(Icons.Outlined.Inventory2, "Encargos", "${active.size}", Modifier.weight(1f)) { ui.root(Screen.Orders) }
                    StatButton(Icons.Outlined.People, "Clientes", "${d.clients}", Modifier.weight(1f)) { ui.root(Screen.Clients) }
                }
                Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                    StatButton(Icons.Outlined.Scale, "Libras", Js.toFixed(d.pounds, 2) + " lb", Modifier.weight(1f)) { ui.go(Screen.Stats) }
                    StatButton(Icons.Outlined.AccountBalanceWallet, "Fondos a favor", "+ " + money(d.funds), Modifier.weight(1f), W.green) { ui.go(Screen.Pending) }
                }
            }
        }
        item {
            val ped = d.byStatus.getOrNull(0)?.second ?: 0; val tra = (d.byStatus.getOrNull(1)?.second ?: 0) + d.received; val ent = d.byStatus.getOrNull(2)?.second ?: 0
            SectionCard("Estado de los pedidos", icon = Icons.Outlined.LocalShipping, action = { LinkText("${active.size} activos ›") { ui.root(Screen.Orders) } }) {
                SegBar(listOf(ped.toDouble() to W.blue, tra.toDouble() to Color(0xFF64748B), ent.toDouble() to W.green))
                Row(Modifier.fillMaxWidth().padding(top = 4.dp)) {
                    StateCol("Pedido", ped, W.blue, null, Modifier.weight(1f))
                    StateCol("En tránsito", tra, Color(0xFF94A3B8), if (d.received > 0) "incluye ${d.received} recibido${if (d.received == 1) "" else "s"}" else null, Modifier.weight(1f))
                    StateCol("Entregado", ent, W.green, null, Modifier.weight(1f))
                }
            }
        }
        item {
            SectionCard("Últimos pagos", icon = Icons.Outlined.CreditCard, action = { LinkText("Ver todos ›") { ui.go(Screen.Payments) } }) {
                val list = ps.list.take(4)
                if (list.isEmpty()) Text("Aún no hay pagos registrados.", Modifier.fillMaxWidth().padding(vertical = 6.dp), color = MaterialTheme.colorScheme.onSurfaceVariant, fontSize = 13.sp, textAlign = TextAlign.Center)
                list.forEachIndexed { i, p ->
                    if (i > 0) HorizontalDivider(color = W.line)
                    val x = core.data.firstOrNull { Js.str(it["id"]) == p.orderId }
                    Row(Modifier.fillMaxWidth().clickable { ui.go(Screen.Order(p.orderId)) }.padding(vertical = 10.dp), verticalAlignment = Alignment.CenterVertically) {
                        Column(Modifier.weight(1f)) {
                            Text(payDate(p.p["date"]), fontSize = 12.5.sp, fontWeight = FontWeight.Bold)
                            Text(p.client, fontSize = 13.5.sp, fontWeight = FontWeight.Bold, maxLines = 1, overflow = TextOverflow.Ellipsis)
                            Text(p.p.str("method").ifEmpty { "Efectivo" }, fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                        }
                        Column(horizontalAlignment = Alignment.End, verticalArrangement = Arrangement.spacedBy(4.dp)) {
                            Text(money(p.p["amount"]), fontSize = 15.sp, fontWeight = FontWeight.ExtraBold)
                            if (x != null && Js.num(x["total"]) > 0 && Js.num(x["balance"]) <= 0.005) Chip("Completado", W.green)
                        }
                    }
                }
            }
        }
    }
    @Suppress("UNUSED_VARIABLE") val u = v
}

private fun payDate(v: Any?): String {
    val t = Js.parseDate(v); if (t.isNaN()) return ""
    return Instant.ofEpochMilli(t.toLong()).atZone(ZoneId.systemDefault()).format(DateTimeFormatter.ofPattern("dd MMM yyyy", Locale.forLanguageTag("es-ES")))
        .replace(".", "").split(" ").joinToString(" ") { it.replaceFirstChar { c -> c.uppercase() } }
}

@Composable private fun Legend(c: Color, label: String, value: String) {
    Row(Modifier.fillMaxWidth().padding(vertical = 2.dp), verticalAlignment = Alignment.CenterVertically) {
        Box(Modifier.size(8.dp).clip(CircleShape).background(c)); Spacer(Modifier.width(7.dp))
        Text(label, Modifier.weight(1f), fontSize = 13.5.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
        Text(value, fontSize = 13.5.sp, fontWeight = FontWeight.Bold)
    }
}

@Composable fun LinkText(text: String, onClick: () -> Unit) =
    Text(text, Modifier.clip(RoundedCornerShape(10.dp)).clickable { tapFeedback(); onClick() }.padding(horizontal = 4.dp, vertical = 4.dp), color = W.blue, fontSize = 13.sp, fontWeight = FontWeight.Bold)

/** Acceso del inicio (.aStat): icono en recuadro, dato y flecha; rebota al tocarlo. */
@Composable fun StatButton(icon: ImageVector, label: String, value: String, modifier: Modifier = Modifier, valueColor: Color = MaterialTheme.colorScheme.onSurface, onClick: () -> Unit) {
    val src = remember { MutableInteractionSource() }
    Row(modifier.pressBounce(src).glass(day = isDay()).clickable(src, null) { tapFeedback(); onClick() }.padding(12.dp), verticalAlignment = Alignment.CenterVertically) {
        IconTile(icon); Spacer(Modifier.width(10.dp))
        Column(Modifier.weight(1f)) {
            Text(label, fontSize = 12.5.sp, fontWeight = FontWeight.Bold, color = MaterialTheme.colorScheme.onSurfaceVariant, maxLines = 1, overflow = TextOverflow.Ellipsis)
            Text(value, fontSize = 18.5.sp, fontWeight = FontWeight.ExtraBold, letterSpacing = (-0.19).sp, color = valueColor, maxLines = 1, overflow = TextOverflow.Ellipsis)
        }
        Icon(Icons.AutoMirrored.Filled.KeyboardArrowRight, null, Modifier.size(18.dp), tint = W.mut)
    }
}

@Composable private fun StateCol(label: String, n: Int, c: Color, sub: String?, modifier: Modifier) {
    Column(modifier, horizontalAlignment = Alignment.CenterHorizontally) {
        Row(verticalAlignment = Alignment.CenterVertically) { Box(Modifier.size(7.dp).clip(CircleShape).background(c)); Spacer(Modifier.width(6.dp)); Text(label, fontSize = 12.5.sp, fontWeight = FontWeight.SemiBold, color = MaterialTheme.colorScheme.onSurfaceVariant) }
        Text("$n", fontSize = 22.sp, fontWeight = FontWeight.ExtraBold)
        if (sub != null) Text(sub, fontSize = 10.5.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
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
        if (cl.isNotEmpty()) Text("Clientes", fontWeight = FontWeight.ExtraBold, color = C.vio)
        cl.forEach { c -> Text("${c.name} · ${c.orders.size} encargo(s)" + if (c.pending > 0.005) " · debe ${money(c.pending)}" else "", Modifier.fillMaxWidth().clickable { ui.go(Screen.Client(c.name)) }.padding(vertical = 4.dp)) }
        if (od.isNotEmpty()) Text("Encargos", fontWeight = FontWeight.ExtraBold, color = C.blue)
        od.forEach { x -> Text("${x.str("name")} · ${money(x["total"])} · ${x.str("status").ifEmpty { "Pedido" }}", Modifier.fillMaxWidth().clickable { ui.go(Screen.Order(Js.str(x["id"]))) }.padding(vertical = 4.dp)) }
        if (nt.isNotEmpty()) Text("Notas", fontWeight = FontWeight.ExtraBold, color = C.amber)
        nt.forEach { n -> Text(n.str("title").ifEmpty { "Sin título" }, Modifier.fillMaxWidth().clickable { ui.go(Screen.Notes) }.padding(vertical = 4.dp)) }
    }
}
