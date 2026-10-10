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
import androidx.compose.material.icons.outlined.Insights
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
    var q by rememberSaveable { mutableStateOf("") }; var week by rememberSaveable { mutableStateOf(true) }
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
                    Text(money(countUp(d.pending)), fontSize = 32.sp, fontWeight = FontWeight.ExtraBold, letterSpacing = (-0.64).sp, maxLines = 1)
                    Spacer(Modifier.size(4.dp))
                    Legend(W.green, "Cobrado", money(countUp(d.paid)), W.green); Legend(W.rose, "Pendiente", money(countUp(d.pending)), W.rose)
                }
            }
        }
        item {
            Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
                Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                    StatButton(Icons.Outlined.Inventory2, "Encargos", "${countUp(active.size.toDouble()).toInt()}", Modifier.weight(1f)) { ui.root(Screen.Orders) }
                    StatButton(Icons.Outlined.People, "Clientes", "${countUp(d.clients.toDouble()).toInt()}", Modifier.weight(1f)) { ui.root(Screen.Clients) }
                }
                Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                    StatButton(Icons.Outlined.Scale, "Libras", Js.toFixed(countUp(d.pounds), 2) + " lb", Modifier.weight(1f)) { ui.go(Screen.Stats) }
                    StatButton(Icons.Outlined.AccountBalanceWallet, "Fondos a favor", "+ " + money(countUp(d.funds)), Modifier.weight(1f), W.green) { ui.go(Screen.Pending) }
                }
            }
        }
        item {
            val ped = d.byStatus.getOrNull(0)?.second ?: 0; val tra = (d.byStatus.getOrNull(1)?.second ?: 0) + d.received; val ent = d.byStatus.getOrNull(2)?.second ?: 0
            SectionCard("Estado de los pedidos", icon = Icons.Outlined.LocalShipping, action = { LinkText("${active.size} activos ›") { ui.root(Screen.Orders) } }) {
                SegBar(listOf(ped.toDouble() to W.blue, tra.toDouble() to C.status("En tránsito"), ent.toDouble() to W.green))
                Row(Modifier.fillMaxWidth().padding(top = 4.dp)) {
                    StateCol("Pedido", ped, W.blue, null, Modifier.weight(1f))
                    StateCol("En tránsito", tra, C.status("En tránsito"), if (d.received > 0) "incluye ${d.received} recibido${if (d.received == 1) "" else "s"}" else null, Modifier.weight(1f))
                    StateCol("Entregado", ent, W.green, null, Modifier.weight(1f))
                }
            }
        }
        item {
            // Actividad del periodo (datos reales de Estadísticas): mes en curso + ingresos recientes
            val st = app.stats; val today = java.time.LocalDate.now()
            val (ma, mb) = st.range(com.agencia.encargos.core.Stats.Period.MONTH, today); val m = st.calc(ma, mb)
            SectionCard("Actividad de " + st.label(com.agencia.encargos.core.Stats.Period.MONTH, ma, mb).substringBefore(" de ").lowercase(), icon = Icons.Outlined.Insights, action = { LinkText("Estadísticas ›") { ui.go(Screen.Stats) } }) {
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    Kpi("Cobrado", money(countUp(m.cobrado)), W.green, modifier = Modifier.weight(1f)); Kpi("Ventas", money(countUp(m.ventas)), modifier = Modifier.weight(1f))
                }
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    Kpi("Encargos", "${countUp(m.ord.size.toDouble()).toInt()}", modifier = Modifier.weight(1f))
                    Kpi("Ganancia neta", (if (m.neta < 0) "-" else "") + money(kotlin.math.abs(countUp(m.neta))), if (m.neta >= 0) W.green else W.rose, modifier = Modifier.weight(1f))
                }
                Choice(listOf("w" to "Últimos 7 días", "m" to "Últimos 6 meses"), if (week) "w" else "m") { week = it == "w" }
                val items = st.revenue(week)
                if (items.any { it.second > 0 }) Bars(items, height = 120) else Text("Aún no hay cobros en este periodo.", fontSize = 13.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
        }
        item {
            SectionCard("Últimos pagos", icon = Icons.Outlined.CreditCard, action = { LinkText("Ver todos ›") { ui.go(Screen.Payments) } }) {
                val list = ps.list.take(4)
                if (list.isEmpty()) Text("Aún no hay pagos registrados.", Modifier.fillMaxWidth().padding(vertical = 6.dp), color = MaterialTheme.colorScheme.onSurfaceVariant, fontSize = 13.sp, textAlign = TextAlign.Center)
                list.forEachIndexed { i, p ->
                    if (i > 0) HorizontalDivider(color = W.line)
                    val x = core.orders().firstOrNull { Js.str(it["id"]) == p.orderId }
                    val done = x != null && Js.num(x["total"]) > 0 && Js.num(x["balance"]) <= 0.005
                    // fila compacta: inicial · cliente y fecha/método · importe (y "Saldado" si ya no debe nada)
                    Row(Modifier.fillMaxWidth().clickable { ui.go(Screen.Order(p.orderId)) }.padding(vertical = 7.dp), verticalAlignment = Alignment.CenterVertically) {
                        Avatar(p.client, C.green, 32); Spacer(Modifier.width(10.dp))
                        Column(Modifier.weight(1f)) {
                            Text(p.client, fontSize = 13.5.sp, fontWeight = FontWeight.Bold, maxLines = 1, overflow = TextOverflow.Ellipsis)
                            Text(payDate(p.p["date"]) + " · " + p.p.str("method").ifEmpty { "Efectivo" }, fontSize = 11.5.sp, color = MaterialTheme.colorScheme.onSurfaceVariant, maxLines = 1)
                        }
                        Column(horizontalAlignment = Alignment.End) {
                            Text("+ " + money(p.p["amount"]), fontSize = 14.sp, fontWeight = FontWeight.ExtraBold, color = C.green)
                            if (done) Text("Saldado", fontSize = 10.5.sp, fontWeight = FontWeight.Bold, color = MaterialTheme.colorScheme.onSurfaceVariant)
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

@Composable private fun Legend(c: Color, label: String, value: String, vc: Color = MaterialTheme.colorScheme.onSurface) {
    Row(Modifier.fillMaxWidth().padding(vertical = 2.dp), verticalAlignment = Alignment.CenterVertically) {
        Box(Modifier.size(8.dp).clip(CircleShape).background(c)); Spacer(Modifier.width(7.dp))
        Text(label, Modifier.weight(1f), fontSize = 13.5.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
        Text(value, fontSize = 13.5.sp, fontWeight = FontWeight.Bold, color = vc)
    }
}

@Composable fun LinkText(text: String, onClick: () -> Unit) =
    Text(text, Modifier.clip(RoundedCornerShape(10.dp)).clickable { tapFeedback(); onClick() }.padding(horizontal = 4.dp, vertical = 4.dp), color = W.blue, fontSize = 13.sp, fontWeight = FontWeight.Bold)

/** Acceso del inicio (.aStat): icono en recuadro, dato y flecha; rebota al tocarlo. */
@Composable fun StatButton(icon: ImageVector, label: String, value: String, modifier: Modifier = Modifier, valueColor: Color = MaterialTheme.colorScheme.onSurface, onClick: () -> Unit) {
    val src = remember { MutableInteractionSource() }
    Row(modifier.pressBounce(src).glass(day = isDay()).clickable(src, null) { tapFeedback(); onClick() }.padding(12.dp), verticalAlignment = Alignment.CenterVertically) {
        IconTile(icon, size = 36); Spacer(Modifier.width(9.dp))
        Column(Modifier.weight(1f)) {
            Text(label, fontSize = 12.sp, fontWeight = FontWeight.Bold, color = MaterialTheme.colorScheme.onSurfaceVariant, maxLines = 2, overflow = TextOverflow.Ellipsis, lineHeight = 14.sp, letterSpacing = (-0.1).sp)
            Text(value, fontSize = 18.5.sp, fontWeight = FontWeight.ExtraBold, letterSpacing = (-0.19).sp, color = valueColor, maxLines = 1, overflow = TextOverflow.Ellipsis)
        }
        Icon(Icons.AutoMirrored.Filled.KeyboardArrowRight, null, Modifier.size(16.dp), tint = W.mut)
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
