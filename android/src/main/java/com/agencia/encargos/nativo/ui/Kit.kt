package com.agencia.encargos.nativo.ui

import android.graphics.Bitmap
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ColumnScope
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.RowScope
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Search
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Card
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.FilledTonalButton
import androidx.compose.material3.FilterChip
import androidx.compose.material3.FilterChipDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Button
import androidx.compose.material3.Text
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.geometry.CornerRadius
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.graphics.drawscope.clipPath
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.agencia.encargos.core.Js
import kotlin.math.max

/* ======================= Colores (mismo sistema que la app: azul, verde, rojo) ======================= */
object C {
    val bg = Color(0xFF070B14); val surface = Color(0xFF0F172A); val card = Color(0xFF131C2E); val line = Color(0xFF26324A)
    val tx = Color(0xFFF1F5F9); val mut = Color(0xFF94A3B8)
    val blue = Color(0xFF3B82F6); val green = Color(0xFF34D399); val rose = Color(0xFFFB7185); val amber = Color(0xFFFBBF24); val vio = Color(0xFFA78BFA); val teal = Color(0xFF2DD4BF)
    fun status(s: String?): Color = when (s) { "En tránsito" -> Color(0xFF94A3B8); "Recibido" -> Color(0xFF7DD3FC); "Entregado" -> green; "Cancelado" -> Color(0xFF64748B); else -> blue }
}
private val Dark = darkColorScheme(primary = C.blue, onPrimary = Color.White, secondary = C.green, background = C.bg, surface = C.surface, onSurface = C.tx, onBackground = C.tx,
    surfaceVariant = C.card, onSurfaceVariant = C.mut, outline = C.line, error = C.rose, surfaceContainer = C.card, surfaceContainerHigh = C.card, surfaceContainerHighest = Color(0xFF1B2539))
private val Light = lightColorScheme(primary = Color(0xFF2563EB), secondary = Color(0xFF059669), background = Color(0xFFF4F6FB), surface = Color.White, error = Color(0xFFE11D48))

@Composable fun EncargosTheme(day: Boolean = false, content: @Composable () -> Unit) = MaterialTheme(colorScheme = if (day) Light else Dark, content = content)

fun money(v: Any?) = Js.money(v)
fun lb(v: Double) = Js.toFixed(v, 2) + " lb"

/* ======================= Piezas reutilizables ======================= */
@Composable fun SectionCard(title: String? = null, modifier: Modifier = Modifier, action: (@Composable () -> Unit)? = null, content: @Composable ColumnScope.() -> Unit) {
    Card(modifier.fillMaxWidth(), shape = RoundedCornerShape(18.dp), colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceVariant), border = BorderStroke(1.dp, MaterialTheme.colorScheme.outline.copy(alpha = .6f))) {
        Column(Modifier.padding(14.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
            if (title != null || action != null) Row(verticalAlignment = Alignment.CenterVertically) {
                Text(title ?: "", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold, modifier = Modifier.weight(1f)); action?.invoke()
            }
            content()
        }
    }
}

@Composable fun Kpi(label: String, value: String, color: Color = MaterialTheme.colorScheme.onSurface, sub: String? = null, modifier: Modifier = Modifier) {
    Box(modifier.clip(RoundedCornerShape(14.dp)).background(color.copy(alpha = .10f)).border(1.dp, color.copy(alpha = .25f), RoundedCornerShape(14.dp)).padding(12.dp)) {
        Column { Text(label, fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant); Text(value, fontSize = 18.sp, fontWeight = FontWeight.ExtraBold, color = color, maxLines = 1, overflow = TextOverflow.Ellipsis)
            if (sub != null) Text(sub, fontSize = 11.sp, color = MaterialTheme.colorScheme.onSurfaceVariant) }
    }
}

@Composable fun KpiRow(vararg items: @Composable RowScope.() -> Unit) { Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) { items.forEach { it() } } }

@Composable fun Chip(text: String, color: Color, modifier: Modifier = Modifier, onClick: (() -> Unit)? = null) {
    Row(modifier.clip(RoundedCornerShape(50)).background(color.copy(alpha = .16f)).border(1.dp, color.copy(alpha = .34f), RoundedCornerShape(50))
        .let { if (onClick != null) it.clickable { onClick() } else it }.padding(horizontal = 10.dp, vertical = 4.dp), verticalAlignment = Alignment.CenterVertically) {
        Box(Modifier.size(7.dp).clip(CircleShape).background(color)); Spacer(Modifier.width(6.dp)); Text(text, color = color, fontSize = 12.sp, fontWeight = FontWeight.Bold)
    }
}
@Composable fun StatusChip(s: String?, onClick: (() -> Unit)? = null) = Chip(s?.ifEmpty { null } ?: "Pedido", C.status(s), onClick = onClick)

@OptIn(ExperimentalMaterial3Api::class)
@Composable fun Choice(options: List<Pair<String, String>>, selected: String, onSelect: (String) -> Unit) {
    Row(Modifier.horizontalScroll(rememberScrollState()), horizontalArrangement = Arrangement.spacedBy(6.dp)) {
        options.forEach { (v, l) -> FilterChip(selected = v == selected, onClick = { onSelect(v) }, label = { Text(l) }, colors = FilterChipDefaults.filterChipColors(selectedContainerColor = C.blue.copy(alpha = .25f))) }
    }
}

@Composable fun Field(value: String, onChange: (String) -> Unit, label: String, modifier: Modifier = Modifier, number: Boolean = false, singleLine: Boolean = true, minLines: Int = 1, prefix: String? = null, suffix: String? = null, password: Boolean = false) {
    OutlinedTextField(value, onChange, label = { Text(label) }, modifier = modifier.fillMaxWidth(), singleLine = singleLine, minLines = minLines,
        prefix = prefix?.let { p -> @Composable { Text(p) } }, suffix = suffix?.let { x -> @Composable { Text(x) } },
        visualTransformation = if (password) androidx.compose.ui.text.input.PasswordVisualTransformation() else androidx.compose.ui.text.input.VisualTransformation.None,
        keyboardOptions = KeyboardOptions(keyboardType = if (number) KeyboardType.Decimal else if (password) KeyboardType.Password else KeyboardType.Text), shape = RoundedCornerShape(12.dp))
}

@Composable fun SearchBox(q: String, onQ: (String) -> Unit, hint: String) {
    OutlinedTextField(q, onQ, placeholder = { Text(hint) }, leadingIcon = { Icon(Icons.Filled.Search, null) }, singleLine = true, modifier = Modifier.fillMaxWidth(), shape = RoundedCornerShape(14.dp))
}

@Composable fun Primary(text: String, modifier: Modifier = Modifier, icon: ImageVector? = null, enabled: Boolean = true, color: Color = C.blue, onClick: () -> Unit) {
    Button(onClick, modifier.height(48.dp), enabled = enabled, shape = RoundedCornerShape(14.dp), colors = ButtonDefaults.buttonColors(containerColor = color)) {
        if (icon != null) { Icon(icon, null, Modifier.size(18.dp)); Spacer(Modifier.width(6.dp)) }; Text(text, fontWeight = FontWeight.Bold, maxLines = 1)
    }
}
@Composable fun Secondary(text: String, modifier: Modifier = Modifier, icon: ImageVector? = null, color: Color? = null, onClick: () -> Unit) {
    OutlinedButton(onClick, modifier.height(46.dp), shape = RoundedCornerShape(14.dp), colors = if (color != null) ButtonDefaults.outlinedButtonColors(contentColor = color) else ButtonDefaults.outlinedButtonColors()) {
        if (icon != null) { Icon(icon, null, Modifier.size(18.dp)); Spacer(Modifier.width(6.dp)) }; Text(text, maxLines = 1, overflow = TextOverflow.Ellipsis)
    }
}
@Composable fun Tonal(text: String, modifier: Modifier = Modifier, icon: ImageVector? = null, onClick: () -> Unit) {
    FilledTonalButton(onClick, modifier.height(44.dp), shape = RoundedCornerShape(14.dp)) { if (icon != null) { Icon(icon, null, Modifier.size(18.dp)); Spacer(Modifier.width(6.dp)) }; Text(text, maxLines = 1) }
}

@Composable fun Line(label: String, value: String, valueColor: Color = MaterialTheme.colorScheme.onSurface, bold: Boolean = false) {
    Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
        Text(label, color = MaterialTheme.colorScheme.onSurfaceVariant, modifier = Modifier.weight(1f), fontSize = 14.sp)
        Text(value, color = valueColor, fontWeight = if (bold) FontWeight.ExtraBold else FontWeight.SemiBold, fontSize = 14.sp, textAlign = TextAlign.End)
    }
}

@Composable fun Empty(title: String, text: String = "", action: (@Composable () -> Unit)? = null) {
    Column(Modifier.fillMaxWidth().padding(vertical = 28.dp), horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.spacedBy(6.dp)) {
        Text(title, fontWeight = FontWeight.Bold, fontSize = 16.sp); if (text.isNotEmpty()) Text(text, color = MaterialTheme.colorScheme.onSurfaceVariant, textAlign = TextAlign.Center, fontSize = 13.sp); action?.invoke()
    }
}

@Composable fun Avatar(name: String, color: Color = C.blue, size: Int = 42) {
    Box(Modifier.size(size.dp).clip(RoundedCornerShape((size / 3).dp)).background(color.copy(alpha = .22f)), contentAlignment = Alignment.Center) {
        Text(name.trim().take(1).uppercase().ifEmpty { "?" }, fontWeight = FontWeight.ExtraBold, color = color, fontSize = (size / 2.3).sp)
    }
}

/** Indicador circular de cobro ("agua"). */
@Composable fun Gauge(pct: Double, label: String = "cobrado", size: Int = 150) {
    val fill = (pct / 100).coerceIn(0.0, 1.0).toFloat()
    Box(Modifier.size(size.dp), contentAlignment = Alignment.Center) {
        Canvas(Modifier.size(size.dp)) {
            val r = this.size.minDimension / 2
            drawCircle(C.blue.copy(alpha = .08f), r)
            val top = this.size.height * (1 - fill)
            val path = androidx.compose.ui.graphics.Path().apply { addOval(androidx.compose.ui.geometry.Rect(Offset.Zero, this@Canvas.size)) }
            clipPath(path) { drawRect(C.blue.copy(alpha = .75f), topLeft = Offset(0f, top), size = Size(this.size.width, this.size.height - top)) }
            drawCircle(C.blue.copy(alpha = .35f), r, style = Stroke(3f))
        }
        Column(horizontalAlignment = Alignment.CenterHorizontally) { Text("${Math.round(pct)}%", fontSize = 26.sp, fontWeight = FontWeight.ExtraBold); Text(label, fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant) }
    }
}

/** Gráfica de barras sencilla (cobros por día, semana o mes). */
@Composable fun Bars(items: List<Pair<String, Double>>, color: Color = C.blue, height: Int = 150) {
    val mx = max(1.0, items.maxOfOrNull { it.second } ?: 1.0)
    Row(Modifier.fillMaxWidth().height(height.dp), horizontalArrangement = Arrangement.spacedBy(4.dp), verticalAlignment = Alignment.Bottom) {
        items.forEach { (l, v) ->
            Column(Modifier.weight(1f), horizontalAlignment = Alignment.CenterHorizontally) {
                if (v > 0) Text("$" + Js.toFixed(v, 0), fontSize = 9.sp, color = MaterialTheme.colorScheme.onSurfaceVariant, maxLines = 1)
                Box(Modifier.fillMaxWidth().height(max(2.0, (v / mx) * (height - 40)).dp).clip(RoundedCornerShape(4.dp)).background(if (v > 0) color else MaterialTheme.colorScheme.outline))
                Text(l, fontSize = 9.sp, color = MaterialTheme.colorScheme.onSurfaceVariant, maxLines = 1)
            }
        }
    }
}

@Composable fun ProgressBar(k: Double, color: Color = C.green) {
    Canvas(Modifier.fillMaxWidth().height(8.dp)) {
        drawRoundRect(C.line, size = this.size, cornerRadius = CornerRadius(8f, 8f))
        drawRoundRect(color, size = Size(this.size.width * k.coerceIn(0.0, 1.0).toFloat(), this.size.height), cornerRadius = CornerRadius(8f, 8f))
    }
}

@Composable fun Photo(bm: Bitmap?, modifier: Modifier = Modifier, onClick: (() -> Unit)? = null) {
    if (bm == null) return
    Image(bm.asImageBitmap(), null, modifier.clip(RoundedCornerShape(12.dp)).let { if (onClick != null) it.clickable { onClick() } else it }, contentScale = ContentScale.Crop)
}
