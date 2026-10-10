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
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.verticalScroll
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
import androidx.compose.material.icons.filled.Check
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
import androidx.compose.ui.draw.drawBehind
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
import androidx.compose.animation.animateColorAsState
import androidx.compose.animation.core.Animatable
import androidx.compose.animation.core.tween
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.interaction.collectIsFocusedAsState
import androidx.compose.foundation.text.BasicTextField
import androidx.compose.material.icons.outlined.Description
import androidx.compose.material.icons.outlined.Visibility
import androidx.compose.material.icons.outlined.VisibilityOff
import androidx.compose.material3.Typography
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.SolidColor
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.animation.togetherWith
import androidx.compose.animation.fadeOut
import androidx.compose.animation.fadeIn
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.text.input.VisualTransformation
import androidx.compose.ui.unit.TextUnit
import kotlinx.coroutines.delay
import com.agencia.encargos.core.Js
import kotlin.math.max

/* ======================= Colores (los de la app web, tema aurora) ======================= */
object C {
    val bg get() = W.base; val surface get() = if (W.day) Color.White else Color(0xFF0B1220); val card get() = if (W.day) Color.White else Color(0xFF111A2C); val line get() = if (W.day) Color(0xFFE2E8F0) else Color(0xFF243049)
    val tx get() = W.tx; val mut get() = W.mut
    val blue get() = W.blue; val green get() = W.green; val rose get() = W.rose; val amber get() = W.amber; val vio get() = W.vio; val teal get() = W.teal
    /** Colores de estado (mismos en ambos temas, más oscuros de día para que se lean). */
    fun status(s: String?): Color = when (s) {
        "En tránsito" -> if (W.day) Color(0xFF475569) else Color(0xFF94A3B8); "Recibido" -> if (W.day) Color(0xFF0369A1) else Color(0xFF7DD3FC)
        "Entregado" -> green; "Cancelado" -> Color(0xFF64748B); else -> blue }
}
private fun darkScheme() = darkColorScheme(primary = NIGHT.blue, onPrimary = Color.White, secondary = NIGHT.green, background = NIGHT.base, surface = Color(0xFF0B1220), onSurface = NIGHT.tx, onBackground = NIGHT.tx,
    surfaceVariant = Color(0xFF111A2C), onSurfaceVariant = NIGHT.mut, outline = Color(0xFF243049), outlineVariant = NIGHT.line, error = NIGHT.rose, surfaceContainer = NIGHT.menu, surfaceContainerHigh = NIGHT.menu,
    surfaceContainerHighest = Color(0xFF16213A), surfaceContainerLow = Color(0xFF0B1220), primaryContainer = NIGHT.blueSoft, onPrimaryContainer = NIGHT.blueInk, secondaryContainer = Color(0xFF16213A), onSecondaryContainer = NIGHT.tx)
private fun lightScheme() = lightColorScheme(primary = DAY.blue, onPrimary = Color.White, secondary = DAY.green, background = DAY.base, surface = Color.White, onSurface = DAY.tx, onBackground = DAY.tx,
    surfaceVariant = Color(0xFFF1F5F9), onSurfaceVariant = DAY.mut, outline = Color(0xFFCBD5E1), outlineVariant = DAY.line, error = DAY.rose, surfaceContainer = Color.White, surfaceContainerHigh = Color.White,
    surfaceContainerHighest = Color(0xFFF1F5F9), surfaceContainerLow = Color(0xFFF8FAFC), primaryContainer = DAY.blueSoft, onPrimaryContainer = DAY.blueInk, secondaryContainer = Color(0xFFE2E8F0), onSecondaryContainer = DAY.tx)

private fun manropeTypography(): Typography {
    val t = Typography()
    fun f(s: TextStyle) = s.copy(fontFamily = Manrope)
    return Typography(displayLarge = f(t.displayLarge), displayMedium = f(t.displayMedium), displaySmall = f(t.displaySmall), headlineLarge = f(t.headlineLarge),
        headlineMedium = f(t.headlineMedium), headlineSmall = f(t.headlineSmall), titleLarge = f(t.titleLarge).copy(fontWeight = FontWeight.ExtraBold), titleMedium = f(t.titleMedium).copy(fontWeight = FontWeight.ExtraBold),
        titleSmall = f(t.titleSmall), bodyLarge = f(t.bodyLarge).copy(fontSize = 15.sp), bodyMedium = f(t.bodyMedium), bodySmall = f(t.bodySmall),
        labelLarge = f(t.labelLarge).copy(fontWeight = FontWeight.Bold), labelMedium = f(t.labelMedium), labelSmall = f(t.labelSmall))
}

/** Radios comunes: los diálogos y menús del sistema usan las mismas esquinas que las tarjetas. */
private fun appShapes() = androidx.compose.material3.Shapes(extraSmall = RoundedCornerShape(10.dp), small = RoundedCornerShape(12.dp), medium = RoundedCornerShape(16.dp),
    large = RoundedCornerShape(18.dp), extraLarge = RoundedCornerShape(22.dp))

@Composable fun EncargosTheme(day: Boolean = false, content: @Composable () -> Unit) {
    W.p = if (day) DAY else NIGHT   // tema central: lo leen todos los componentes
    // tamaño de la interfaz: se escala solo la densidad; como sp = dp × fontScale, textos y medidas crecen en la misma proporción
    // (y se respeta el tamaño de letra que el usuario eligió en Android)
    val base = androidx.compose.ui.platform.LocalDensity.current; val s = UiScale.value
    androidx.compose.runtime.CompositionLocalProvider(androidx.compose.ui.platform.LocalDensity provides androidx.compose.ui.unit.Density(base.density * s, base.fontScale)) {
        MaterialTheme(colorScheme = if (day) lightScheme() else darkScheme(), typography = manropeTypography(), shapes = appShapes(), content = content)
    }
}

fun money(v: Any?) = Js.money(v)
fun lb(v: Double) = Js.toFixed(v, 2) + " lb"

/** ¿Modo día? (los componentes de vidrio cambian a tarjetas blancas). */
@Composable fun isDay() = W.day

/* ======================= Piezas reutilizables ======================= */

/** Tarjeta de sección (.aCard): vidrio, radio 18, título 16 extra negrita, icono opcional. */
@Composable fun SectionCard(title: String? = null, modifier: Modifier = Modifier, action: (@Composable () -> Unit)? = null, icon: ImageVector? = null, content: @Composable ColumnScope.() -> Unit) {
    Column(modifier.fillMaxWidth().glass(day = isDay()).padding(18.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
        if (title != null || action != null) Row(verticalAlignment = Alignment.CenterVertically) {
            if (icon != null) { Icon(icon, null, Modifier.size(19.dp), tint = MaterialTheme.colorScheme.onSurface); Spacer(Modifier.width(9.dp)) }
            Text(title ?: "", fontSize = 16.sp, fontWeight = FontWeight.ExtraBold, letterSpacing = (-0.16).sp, modifier = Modifier.weight(1f)); action?.invoke()
        }
        content()
    }
}

/** Recuadro de dato (Libras / Cobrado / Pendiente): neutro, o teñido del color del dato. */
@Composable fun Kpi(label: String, value: String, color: Color = MaterialTheme.colorScheme.onSurface, sub: String? = null, modifier: Modifier = Modifier) {
    val neutral = color == MaterialTheme.colorScheme.onSurface
    val shape = RoundedCornerShape(12.dp)
    Box(modifier.clip(shape).background(if (neutral) W.tile else color.copy(alpha = .08f)).border(1.dp, if (neutral) W.line else color.copy(alpha = .30f), shape).padding(horizontal = 11.dp, vertical = 9.dp)) {
        Column { Text(label, fontSize = 11.5.sp, fontWeight = FontWeight.SemiBold, color = MaterialTheme.colorScheme.onSurfaceVariant, maxLines = 1)
            Text(value, fontSize = 17.sp, fontWeight = FontWeight.ExtraBold, letterSpacing = (-0.2).sp, color = color, maxLines = 1, overflow = TextOverflow.Ellipsis)
            if (sub != null) Text(sub, fontSize = 11.sp, color = MaterialTheme.colorScheme.onSurfaceVariant) }
    }
}

@Composable fun KpiRow(vararg items: @Composable RowScope.() -> Unit) { Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) { items.forEach { it() } } }

/** Etiqueta con punto (• Pedido, • Entregado). */
@Composable fun Chip(text: String, color: Color, modifier: Modifier = Modifier, trail: String? = null, onClick: (() -> Unit)? = null) {
    val shape = RoundedCornerShape(50)
    Row(modifier.clip(shape).background(color.copy(alpha = .14f)).border(1.dp, color.copy(alpha = .38f), shape)
        .let { if (onClick != null) it.clickable { tapFeedback(); onClick() } else it }.padding(horizontal = 10.dp, vertical = 4.dp), verticalAlignment = Alignment.CenterVertically) {
        Box(Modifier.size(6.dp).clip(CircleShape).background(color)); Spacer(Modifier.width(6.dp)); Text(text, color = lerpInk(color), fontSize = 11.5.sp, fontWeight = FontWeight.Bold, maxLines = 1)
        if (trail != null) { Spacer(Modifier.width(5.dp)); Text(trail, color = lerpInk(color), fontSize = 11.5.sp, fontWeight = FontWeight.Bold) }
    }
}
private fun lerpInk(c: Color) = if (W.day) Color(c.red * .8f, c.green * .8f, c.blue * .8f, 1f) else Color(c.red + (1 - c.red) * .45f, c.green + (1 - c.green) * .45f, c.blue + (1 - c.blue) * .45f, 1f)
@Composable fun StatusChip(s: String?, arrow: Boolean = false, onClick: (() -> Unit)? = null) = Chip(s?.ifEmpty { null } ?: "Pedido", C.status(s), trail = if (arrow) "→" else null, onClick = onClick)

/** Filtros en pastilla (Todos · Pedido · En tránsito…). */
@Composable fun Choice(options: List<Pair<String, String>>, selected: String, onSelect: (String) -> Unit) {
    Row(Modifier.horizontalScroll(rememberScrollState()), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
        options.forEach { (v, l) ->
            val on = v == selected; val shape = RoundedCornerShape(50)
            val bg by animateColorAsState(if (on) W.blue.copy(alpha = if (W.day) .12f else .20f) else W.tile, tween(240), label = "chip")
            val bd by animateColorAsState(if (on) W.blue.copy(alpha = .65f) else W.line, tween(240), label = "chipB")
            val src = remember { MutableInteractionSource() }
            Box(Modifier.pressBounce(src, .95f).clip(shape).background(bg).border(1.dp, bd, shape)
                .clickable(src, null) { tapFeedback(); onSelect(v) }.padding(horizontal = 15.dp, vertical = 9.dp)) {
                Text(l, fontSize = 13.sp, fontWeight = FontWeight.Bold, color = if (on) (if (W.day) W.blueDeep else Color.White) else W.chipTx, maxLines = 1)
            }
        }
    }
}

/** Campo de la web: etiqueta arriba y caja oscura redondeada que se ilumina en azul al escribir. */
@Composable fun Field(value: String, onChange: (String) -> Unit, label: String, modifier: Modifier = Modifier, number: Boolean = false, singleLine: Boolean = true, minLines: Int = 1, prefix: String? = null, suffix: String? = null, password: Boolean = false) {
    val src = remember { MutableInteractionSource() }; val focused by src.collectIsFocusedAsState()
    var show by remember { mutableStateOf(false) }
    val bd by animateColorAsState(if (focused) W.blue.copy(alpha = .85f) else W.line2, tween(200), label = "fb")
    val day = isDay(); val shape = RoundedCornerShape(14.dp)
    Column(modifier.fillMaxWidth(), verticalArrangement = Arrangement.spacedBy(6.dp)) {
        Text(label, fontSize = 12.5.sp, fontWeight = FontWeight.SemiBold, color = MaterialTheme.colorScheme.onSurfaceVariant)
        BasicTextField(value, onChange, Modifier.fillMaxWidth().let { if (focused) it.glow(W.blue, 6.dp, 0.dp, .22f) else it }, singleLine = singleLine, minLines = minLines,
            textStyle = TextStyle(color = MaterialTheme.colorScheme.onSurface, fontFamily = Manrope, fontSize = 15.sp, fontWeight = FontWeight.SemiBold),
            cursorBrush = SolidColor(W.blue), interactionSource = src,
            visualTransformation = if (password && !show) PasswordVisualTransformation() else VisualTransformation.None,
            keyboardOptions = KeyboardOptions(keyboardType = if (number) KeyboardType.Decimal else if (password) KeyboardType.Password else KeyboardType.Text),
            decorationBox = { inner ->
                Row(Modifier.fillMaxWidth().clip(shape).background(W.inputBg).border(1.dp, bd, shape).padding(horizontal = 14.dp, vertical = if (singleLine) 14.dp else 12.dp), verticalAlignment = Alignment.CenterVertically) {
                    if (prefix != null) Text(prefix + " ", color = MaterialTheme.colorScheme.onSurfaceVariant, fontSize = 15.sp, fontWeight = FontWeight.SemiBold)
                    Box(Modifier.weight(1f)) { inner() }
                    if (suffix != null) Text(" $suffix", color = MaterialTheme.colorScheme.onSurfaceVariant, fontSize = 14.sp)
                    if (password) Icon(if (show) Icons.Outlined.VisibilityOff else Icons.Outlined.Visibility, if (show) "Ocultar" else "Mostrar", Modifier.size(20.dp).clickable { show = !show }, tint = MaterialTheme.colorScheme.onSurfaceVariant)
                }
            })
    }
}

/** Buscador (.aSearch): vidrio, radio 16, lupa. */
@Composable fun SearchBox(q: String, onQ: (String) -> Unit, hint: String) {
    val src = remember { MutableInteractionSource() }; val focused by src.collectIsFocusedAsState()
    val bd by animateColorAsState(if (focused) W.blue.copy(alpha = .7f) else W.line, tween(200), label = "sb")
    val shape = RoundedCornerShape(16.dp)
    BasicTextField(q, onQ, Modifier.fillMaxWidth(), singleLine = true, interactionSource = src, cursorBrush = SolidColor(W.blue),
        textStyle = TextStyle(color = MaterialTheme.colorScheme.onSurface, fontFamily = Manrope, fontSize = 15.sp, fontWeight = FontWeight.Medium),
        decorationBox = { inner ->
            Row(Modifier.fillMaxWidth().glass(shape, bd, isDay()).padding(horizontal = 14.dp, vertical = 15.dp), verticalAlignment = Alignment.CenterVertically) {
                Icon(Icons.Filled.Search, null, Modifier.size(19.dp), tint = W.mut); Spacer(Modifier.width(10.dp))
                Box(Modifier.weight(1f)) { if (q.isEmpty()) Text(hint, color = W.mut, fontSize = 14.5.sp, fontWeight = FontWeight.Medium, maxLines = 1); inner() }
            }
        })
}

/** Botón principal: degradado azul, brillo, rebote al tocar y vibración breve. */
/** Evita que un doble toque registre dos veces la misma operación (700 ms). */
@Composable fun rememberOnce(onClick: () -> Unit): () -> Unit {
    val last = remember { longArrayOf(0L) }
    return { val now = android.os.SystemClock.elapsedRealtime(); if (now - last[0] > 700) { last[0] = now; onClick() } }
}

@Composable fun Primary(text: String, modifier: Modifier = Modifier, icon: ImageVector? = null, enabled: Boolean = true, color: Color = C.blue, onClick: () -> Unit) {
    val src = remember { MutableInteractionSource() }; val shape = RoundedCornerShape(14.dp); val once = rememberOnce(onClick)
    val brush = if (color == C.blue) W.primary else Brush.linearGradient(listOf(Color(color.red + (1 - color.red) * .15f, color.green + (1 - color.green) * .15f, color.blue + (1 - color.blue) * .15f), color))
    Row(modifier.height(52.dp).pressBounce(src).graphicsLayer { alpha = if (enabled) 1f else .5f }.glow(color, 14.dp, 8.dp, if (enabled) .45f else 0f)
        .clip(shape).background(brush).clickable(src, null, enabled = enabled) { tapFeedback(); once() }.padding(horizontal = 16.dp),
        horizontalArrangement = Arrangement.Center, verticalAlignment = Alignment.CenterVertically) {
        if (icon != null) { Icon(icon, null, Modifier.size(18.dp), tint = Color.White); Spacer(Modifier.width(8.dp)) }
        Text(text, fontWeight = FontWeight.ExtraBold, fontSize = 15.sp, color = Color.White, maxLines = 1, overflow = TextOverflow.Ellipsis)
    }
}
/** Botón secundario (.secondary): vidrio gris con borde. */
@Composable fun Secondary(text: String, modifier: Modifier = Modifier, icon: ImageVector? = null, color: Color? = null, onClick: () -> Unit) {
    val src = remember { MutableInteractionSource() }; val shape = RoundedCornerShape(14.dp); val once = rememberOnce(onClick)
    val fg = color ?: MaterialTheme.colorScheme.onSurface
    Row(modifier.height(48.dp).pressBounce(src).clip(shape).background(if (color != null) color.copy(alpha = .08f) else W.tile).border(1.dp, if (color != null) color.copy(alpha = .35f) else W.line2, shape)
        .clickable(src, null) { tapFeedback(); once() }.padding(horizontal = 14.dp), horizontalArrangement = Arrangement.Center, verticalAlignment = Alignment.CenterVertically) {
        if (icon != null) { Icon(icon, null, Modifier.size(17.dp), tint = fg); Spacer(Modifier.width(7.dp)) }
        Text(text, fontWeight = FontWeight.Bold, fontSize = 14.sp, color = fg, maxLines = 1, overflow = TextOverflow.Ellipsis)
    }
}
@Composable fun Tonal(text: String, modifier: Modifier = Modifier, icon: ImageVector? = null, onClick: () -> Unit) = Secondary(text, modifier, icon, W.blueInk, onClick)

@Composable fun Line(label: String, value: String, valueColor: Color = MaterialTheme.colorScheme.onSurface, bold: Boolean = false) {
    Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
        Text(label, color = MaterialTheme.colorScheme.onSurfaceVariant, modifier = Modifier.weight(1f), fontSize = 14.sp)
        Text(value, color = valueColor, fontWeight = if (bold) FontWeight.ExtraBold else FontWeight.SemiBold, fontSize = 14.sp, textAlign = TextAlign.End)
    }
}

/** Estado vacío: icono, título y texto centrados (como "Aún no tienes notas"). */
@Composable fun Empty(title: String, text: String = "", icon: ImageVector = Icons.Outlined.Description, action: (@Composable () -> Unit)? = null) {
    Column(Modifier.fillMaxWidth().glass(day = isDay()).padding(vertical = 26.dp, horizontal = 18.dp), horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.spacedBy(6.dp)) {
        Icon(icon, null, Modifier.size(52.dp), tint = W.icon)
        Text(title, fontWeight = FontWeight.ExtraBold, fontSize = 16.sp, textAlign = TextAlign.Center); if (text.isNotEmpty()) Text(text, color = MaterialTheme.colorScheme.onSurfaceVariant, textAlign = TextAlign.Center, fontSize = 13.sp); action?.invoke()
    }
}

/** Inicial del cliente en recuadro (como en la web). */
@Composable fun Avatar(name: String, color: Color = C.blue, size: Int = 42) {
    val shape = RoundedCornerShape((size * .3).dp)
    Box(Modifier.size(size.dp).clip(shape).background(W.tile).border(1.dp, if (color == C.rose) C.rose.copy(alpha = .35f) else W.line2, shape), contentAlignment = Alignment.Center) {
        Text(name.trim().take(1).uppercase().ifEmpty { "?" }, fontWeight = FontWeight.ExtraBold, color = MaterialTheme.colorScheme.onSurface, fontSize = (size / 2.4).sp)
    }
}

/** Icono en recuadro (.ic) de las tarjetas de la web. */
@Composable fun IconTile(icon: ImageVector, tint: Color = W.icon, size: Int = 40) {
    val shape = RoundedCornerShape(12.dp)
    Box(Modifier.size(size.dp).clip(shape).background(W.tile).border(1.dp, W.line, shape), contentAlignment = Alignment.Center) { Icon(icon, null, Modifier.size((size * .48).dp), tint = tint) }
}

/** Llenado tipo agua: sube desde la base al aparecer y, si cambian los datos, pasa del nivel anterior al nuevo.
 *  Devuelve (nivel actual, oleaje 0..1). El oleaje solo existe mientras el nivel se mueve y se calma al llegar. */
@Composable fun waterLevel(target: Float, durationMs: Int = 1300, delayMs: Int = 250, key: Any? = null): Pair<Float, Float> {
    val k = remember { Animatable(if (Motion.enabled) 0f else target) }
    val from = remember { floatArrayOf(0f) }
    LaunchedEffect(target, key) {
        if (!Motion.enabled) { k.snapTo(target); return@LaunchedEffect }
        if (key != null) k.snapTo(0f)
        from[0] = k.value
        delay(delayMs.toLong()); k.animateTo(target, tween(durationMs, easing = EaseOut))
    }
    val dist = kotlin.math.abs(target - from[0]).coerceAtLeast(1e-4f)
    val wave = if (!Motion.enabled) 0f else (kotlin.math.abs(target - k.value) / dist).coerceIn(0f, 1f)
    return k.value to wave
}

/** Superficie de agua: línea ondulada suave cuya amplitud depende del oleaje (0 = superficie plana). */
private fun waterPath(w: Float, h: Float, top: Float, amp: Float, phase: Float, left: Float = 0f): androidx.compose.ui.graphics.Path =
    androidx.compose.ui.graphics.Path().apply {
        moveTo(left, h); lineTo(left, top)
        val steps = 24
        for (i in 0..steps) { val x = left + w * i / steps; lineTo(x, top + amp * kotlin.math.sin(phase + i / steps.toFloat() * 2f * Math.PI.toFloat() * 1.3f)) }
        lineTo(left + w, h); close()
    }

/** Indicador "agua" de Pagos y Estadísticas: el círculo se llena hasta el % cobrado con una ola discreta que se calma
 *  al llegar; el porcentaje sube al mismo ritmo y termina en el valor real. */
@Composable fun Gauge(pct: Double, label: String = "cobrado", size: Int = 150) {
    val target = (pct / 100).coerceIn(0.0, 1.0).toFloat()
    val (fill, wave) = waterLevel(target)
    val shown = if (target > 0f && Motion.enabled && fill != target) pct * (fill / target) else pct
    Box(Modifier.size(size.dp).semantics(mergeDescendants = true) { contentDescription = "${Math.round(pct)}% $label" }, contentAlignment = Alignment.Center) {
        Canvas(Modifier.size(size.dp)) {
            val r = this.size.minDimension / 2
            drawCircle(if (W.day) Color(0xFFE2E8F0) else Color(15, 23, 42, 200), r)
            val h = this.size.height; val w = this.size.width
            val top = h * (1 - fill)
            val amp = h * .035f * wave; val phase = fill * 14f
            val circle = androidx.compose.ui.graphics.Path().apply { addOval(androidx.compose.ui.geometry.Rect(Offset.Zero, this@Canvas.size)) }
            clipPath(circle) {
                if (fill > 0f) {
                    // capa trasera (más clara y desfasada) y capa delantera: dan profundidad sin mover la lectura
                    drawPath(waterPath(w, h, top, amp * .8f, phase + 2.2f), Color(0xFF60A5FA).copy(alpha = .35f))
                    drawPath(waterPath(w, h, top, amp, phase), Brush.verticalGradient(listOf(Color(0xFF60A5FA), Color(0xFF2563EB)), top, h))
                    drawRect(Color.White.copy(alpha = .30f), topLeft = Offset(0f, top - 1f), size = Size(w, 2.5f))
                }
            }
            drawCircle(W.blue.copy(alpha = .45f), r - 1.5f, style = Stroke(3f))
        }
        Column(horizontalAlignment = Alignment.CenterHorizontally) { val onWater = !W.day || fill > .55f
            Text("${Math.round(shown)}%", fontSize = (size / 6).sp, fontWeight = FontWeight.ExtraBold, color = if (onWater) Color.White else W.tx)
            Text(label, fontSize = 11.sp, color = if (onWater) Color.White.copy(alpha = .85f) else W.mut) }
    }
}

/** Anillo de cobro del inicio: el arco se dibuja y el % sube a la vez (pasa del valor anterior al nuevo si cambian los datos). */
@Composable fun RingGauge(pct: Double, label: String = "cobrado", size: Int = 100) {
    val target = (pct / 100).coerceIn(0.0, 1.0).toFloat()
    val (k, _) = waterLevel(target, 1100)
    val shown = if (target > 0f && Motion.enabled && k != target) pct * (k / target) else pct
    Box(Modifier.size(size.dp).semantics(mergeDescendants = true) { contentDescription = "${Math.round(pct)}% $label" }, contentAlignment = Alignment.Center) {
        Canvas(Modifier.size(size.dp)) {
            val sw = this.size.minDimension * .085f; val inset = sw / 2 + 2f
            val sz = Size(this.size.width - inset * 2, this.size.height - inset * 2)
            drawArc(W.track, 0f, 360f, false, Offset(inset, inset), sz, style = Stroke(sw))
            if (k > 0f) drawArc(Brush.sweepGradient(listOf(W.cyan, W.blue, Color(0xFF60A5FA), W.cyan)), -90f, 360f * k, false, Offset(inset, inset), sz, style = Stroke(sw, cap = StrokeCap.Round))
        }
        Column(horizontalAlignment = Alignment.CenterHorizontally) { Text("${Math.round(shown)}%", fontSize = (size / 4.6).sp, fontWeight = FontWeight.ExtraBold); Text(label, fontSize = 11.sp, color = MaterialTheme.colorScheme.onSurfaceVariant) }
    }
}

/** Barras que se llenan de agua desde la base, una tras otra; al tocar una barra se resalta y muestra su importe.
 *  Si cambia el periodo, las barras se vacían y se vuelven a llenar con los datos nuevos (nunca se mezclan). */
@Composable fun Bars(items: List<Pair<String, Double>>, color: Color = C.blue, height: Int = 150) {
    val mx = max(1.0, items.maxOfOrNull { it.second } ?: 1.0)
    var sel by remember(items) { mutableStateOf(-1) }
    Column(Modifier.fillMaxWidth()) {
        // información del dato seleccionado (o pista para tocar)
        val info = items.getOrNull(sel)
        Box(Modifier.fillMaxWidth().height(22.dp), contentAlignment = Alignment.CenterStart) {
            androidx.compose.animation.AnimatedContent(info, transitionSpec = { fadeIn(tween(180)) togetherWith fadeOut(tween(120)) }, label = "barInfo") { it2 ->
                if (it2 != null) Text("${it2.first} · ${money(it2.second)}", fontSize = 12.5.sp, fontWeight = FontWeight.ExtraBold, color = W.blueInk)
                else Text("Toca una barra para ver su importe", fontSize = 11.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
        }
        Row(Modifier.fillMaxWidth().height(height.dp), horizontalArrangement = Arrangement.spacedBy(5.dp), verticalAlignment = Alignment.Bottom) {
            items.forEachIndexed { i, (l, v) ->
                androidx.compose.runtime.key(i) {
                    val (k, wave) = waterLevel((v / mx).toFloat(), 900, 200 + i * 45, key = items)
                    val dim = sel >= 0 && sel != i
                    Column(Modifier.weight(1f).clickable(remember { MutableInteractionSource() }, null) { tapFeedback(); sel = if (sel == i) -1 else i }
                        .semantics(mergeDescendants = true) { contentDescription = "$l: ${money(v)}" }, horizontalAlignment = Alignment.CenterHorizontally) {
                        Text(if (v > 0) "$" + Js.toFixed(v, 0) else "", fontSize = 10.sp, color = MaterialTheme.colorScheme.onSurfaceVariant, maxLines = 1,
                            modifier = Modifier.graphicsLayer { alpha = if (v <= 0) 0f else ((k / (v / mx).toFloat()).coerceIn(0f, 1f)) * (if (dim) .5f else 1f) })
                        Canvas(Modifier.fillMaxWidth().weight(1f)) {
                            val w = this.size.width; val h = this.size.height
                            val shape = CornerRadius(6.dp.toPx(), 6.dp.toPx())
                            if (v <= 0) { drawRoundRect(W.track, topLeft = Offset(0f, h - 2.dp.toPx()), size = Size(w, 2.dp.toPx()), cornerRadius = CornerRadius(2f, 2f)); return@Canvas }
                            val top = h * (1 - k)
                            val body = Brush.verticalGradient(listOf(Color(0xFF60A5FA), color), top, h)
                            val alphaMul = if (dim) .45f else 1f
                            val clip = androidx.compose.ui.graphics.Path().apply { addRoundRect(androidx.compose.ui.geometry.RoundRect(0f, 0f, w, h, shape, shape, CornerRadius(2f, 2f), CornerRadius(2f, 2f))) }
                            clipPath(clip) {
                                drawPath(waterPath(w, h, top, 3.dp.toPx() * wave, k * 12f + i), body, alpha = alphaMul)
                                drawRect(Color.White.copy(alpha = .35f * alphaMul), topLeft = Offset(0f, top - 1f), size = Size(w, 2f))
                            }
                            if (sel == i) drawRoundRect(W.blueInk, topLeft = Offset(0f, top), size = Size(w, h - top), cornerRadius = shape, style = Stroke(1.5.dp.toPx()))
                        }
                        Text(l, fontSize = 10.sp, color = if (sel == i) W.blueInk else MaterialTheme.colorScheme.onSurfaceVariant, fontWeight = if (sel == i) FontWeight.ExtraBold else FontWeight.Normal, maxLines = 1)
                    }
                }
            }
        }
    }
}

/** Barra de progreso fina: se llena al aparecer (0,9 s) y pasa suavemente al valor nuevo si cambian los datos. */
@Composable fun ProgressBar(k: Double, color: Color = C.green) {
    val (v, _) = waterLevel(k.coerceIn(0.0, 1.0).toFloat(), 900, 300)
    Canvas(Modifier.fillMaxWidth().height(6.dp)) {
        drawRoundRect(W.track, size = this.size, cornerRadius = CornerRadius(this.size.height, this.size.height))
        if (v > 0f) {
            val w = this.size.width * v
            drawRoundRect(Brush.horizontalGradient(listOf(color.copy(alpha = .75f), color), 0f, w), size = Size(w, this.size.height), cornerRadius = CornerRadius(this.size.height, this.size.height))
        }
    }
}

/** Barra segmentada (Estado de los pedidos). */
@Composable fun SegBar(parts: List<Pair<Double, Color>>) {
    val total = parts.sumOf { it.first }.let { if (it <= 0) 1.0 else it }
    val k = drawIn(1f, 900, 300, key = parts.map { it.first })
    Row(Modifier.fillMaxWidth().height(10.dp), horizontalArrangement = Arrangement.spacedBy(5.dp)) {
        parts.filter { it.first > 0 }.forEach { (v, c) -> Box(Modifier.weight((v / total).toFloat()).fillMaxWidth().height(10.dp).graphicsLayer { scaleX = k; transformOrigin = androidx.compose.ui.graphics.TransformOrigin(0f, .5f) }.clip(RoundedCornerShape(99.dp)).background(c)) }
        if (parts.all { it.first <= 0 }) Box(Modifier.weight(1f).height(10.dp).clip(RoundedCornerShape(99.dp)).background(W.track))
    }
}

@Composable fun Photo(bm: Bitmap?, modifier: Modifier = Modifier, onClick: (() -> Unit)? = null) {
    if (bm == null) return
    Image(bm.asImageBitmap(), null, modifier.clip(RoundedCornerShape(12.dp)).let { if (onClick != null) it.clickable { onClick() } else it }, contentScale = ContentScale.Crop)
}


/* ======================= Piezas de la referencia (Encargos-9) ======================= */

/** Botón compacto de las tarjetas (Ficha · Editar · WhatsApp · ⋯): vidrio, radio 12, alto 40. */
@Composable fun CardBtn(text: String?, icon: ImageVector, modifier: Modifier = Modifier, tint: Color? = null, desc: String? = null, onClick: () -> Unit) {
    val src = remember { MutableInteractionSource() }; val shape = RoundedCornerShape(12.dp); val once = rememberOnce(onClick)
    val fg = MaterialTheme.colorScheme.onSurface
    Row(modifier.height(40.dp).pressBounce(src, .95f).clip(shape).background(W.tile).border(1.dp, W.line2, shape)
        .clickable(src, null) { tapFeedback(); once() }.padding(horizontal = 4.dp), horizontalArrangement = Arrangement.Center, verticalAlignment = Alignment.CenterVertically) {
        Icon(icon, desc ?: text, Modifier.size(15.dp), tint = tint ?: fg)
        if (text != null) { Spacer(Modifier.width(5.dp)); Text(text, fontSize = 13.sp, softWrap = false, fontWeight = FontWeight.Bold, color = fg, maxLines = 1, overflow = TextOverflow.Ellipsis) }
    }
}

/** Tarjeta de fila con icono, título, subtítulo, valor a la derecha y flecha (como en Ajustes y "Encargos completados"). */
@Composable fun NavRow(icon: ImageVector, title: String, sub: String? = null, value: String? = null, tint: Color = W.icon, right: (@Composable () -> Unit)? = null, titleColor: Color? = null, onClick: (() -> Unit)?) {
    val src = remember { MutableInteractionSource() }
    Row(Modifier.fillMaxWidth().let { if (onClick != null) it.clickable(src, null) { tapFeedback(); onClick() } else it }.padding(horizontal = 14.dp, vertical = 12.dp), verticalAlignment = Alignment.CenterVertically) {
        IconTile(icon, tint, 34); Spacer(Modifier.width(12.dp))
        Column(Modifier.weight(1f)) {
            Text(title, fontWeight = FontWeight.ExtraBold, fontSize = 14.5.sp, maxLines = 2, color = titleColor ?: MaterialTheme.colorScheme.onSurface)
            if (!sub.isNullOrEmpty()) Text(sub, fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant, maxLines = 2, overflow = TextOverflow.Ellipsis)
        }
        if (value != null) { Spacer(Modifier.width(8.dp)); Text(value, fontSize = 13.sp, fontWeight = FontWeight.Bold, color = MaterialTheme.colorScheme.onSurfaceVariant) }
        if (right != null) { Spacer(Modifier.width(8.dp)); right() }
        else if (onClick != null) { Spacer(Modifier.width(6.dp)); Text("›", fontSize = 20.sp, color = MaterialTheme.colorScheme.onSurfaceVariant) }
    }
}

/** Grupo de filas con título pequeño encima (Negocio y tarifas, WhatsApp…). */
@Composable fun RowGroup(title: String?, content: @Composable ColumnScope.() -> Unit) {
    Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
        if (title != null) Text(title, fontSize = 12.5.sp, fontWeight = FontWeight.Bold, color = MaterialTheme.colorScheme.onSurfaceVariant, modifier = Modifier.padding(start = 4.dp))
        Column(Modifier.fillMaxWidth().glass(day = isDay())) { content() }
    }
}
@Composable fun RowSep() = Box(Modifier.fillMaxWidth().padding(horizontal = 14.dp).height(1.dp).background(W.line))

/** Recuadro de dato compacto para la fila de totales de arriba. */
@Composable fun TopTiles(vararg t: Triple<String, String, Color?>) {
    Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
        t.forEach { (l, v, c) -> Kpi(l, v, c ?: MaterialTheme.colorScheme.onSurface, modifier = Modifier.weight(1f)) }
    }
}

/** Hoja emergente de la referencia: tarjeta de vidrio arriba, título y botón "Cerrar"; se desplaza y respeta el teclado. */
@Composable fun Sheet(title: String, dismiss: () -> Unit, content: @Composable ColumnScope.() -> Unit) {
    androidx.compose.ui.window.Dialog(onDismissRequest = dismiss, properties = androidx.compose.ui.window.DialogProperties(usePlatformDefaultWidth = false)) {
        val none = remember { MutableInteractionSource() }; val none2 = remember { MutableInteractionSource() }
        val shape = RoundedCornerShape(22.dp)
        Box(Modifier.fillMaxWidth().fillMaxHeight().clickable(none, null) { dismiss() }.imePadding().padding(12.dp), contentAlignment = Alignment.TopCenter) {
            Column(Modifier.fillMaxWidth().widthIn(max = 560.dp).enterUp(0).clip(shape).background(W.menu).border(1.dp, W.line2, shape).clickable(none2, null) {}
                .verticalScroll(rememberScrollState()).padding(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Text(title, fontSize = 18.sp, fontWeight = FontWeight.ExtraBold, modifier = Modifier.weight(1f))
                    Secondary("Cerrar", Modifier.height(38.dp)) { dismiss() }
                }
                content()
            }
        }
    }
}

/** Recuadro informativo azul de las hojas. */
@Composable fun InfoBox(text: String) {
    val shape = RoundedCornerShape(14.dp)
    Text(text, fontSize = 13.sp, color = MaterialTheme.colorScheme.onSurface, modifier = Modifier.fillMaxWidth().clip(shape).background(W.blue.copy(alpha = .10f)).border(1.dp, W.blue.copy(alpha = .45f), shape).padding(12.dp))
}

/** Tarjeta de acción grande (Respaldar datos, Respaldo cifrado, Importar…): la principal va en degradado azul. */
@Composable fun ActionCard(icon: ImageVector, title: String, sub: String, primary: Boolean = false, onClick: () -> Unit) {
    val src = remember { MutableInteractionSource() }; val shape = RoundedCornerShape(18.dp); val once = rememberOnce(onClick)
    Row(Modifier.fillMaxWidth().pressBounce(src).let { if (primary) it.glow(W.blue, 14.dp, 8.dp, .45f).clip(shape).background(W.primary) else it.glass(shape) }
        .clickable(src, null) { tapFeedback(); once() }.padding(16.dp), verticalAlignment = Alignment.CenterVertically) {
        val ts = RoundedCornerShape(12.dp)
        Box(Modifier.size(40.dp).clip(ts).background(if (primary) Color.White.copy(alpha = .18f) else W.tile).border(1.dp, if (primary) Color.White.copy(alpha = .25f) else W.line, ts), contentAlignment = Alignment.Center) {
            Icon(icon, null, Modifier.size(20.dp), tint = if (primary) Color.White else W.icon) }
        Spacer(Modifier.width(14.dp))
        Column(Modifier.weight(1f)) {
            Text(title, fontWeight = FontWeight.ExtraBold, fontSize = 15.5.sp, color = if (primary) Color.White else MaterialTheme.colorScheme.onSurface)
            Text(sub, fontSize = 12.5.sp, color = if (primary) Color.White.copy(alpha = .85f) else MaterialTheme.colorScheme.onSurfaceVariant)
        }
    }
}

/** Fila "dato — valor" con separador fino (tarjeta de estado de Seguridad). */
@Composable fun StatRow(label: String, value: String, color: Color = MaterialTheme.colorScheme.onSurface) {
    Box(Modifier.fillMaxWidth().height(1.dp).background(W.line))
    Row(Modifier.fillMaxWidth().padding(vertical = 2.dp), verticalAlignment = Alignment.CenterVertically) {
        Text(label, fontSize = 13.5.sp, color = MaterialTheme.colorScheme.onSurfaceVariant, modifier = Modifier.weight(1f))
        Text(value, fontSize = 13.5.sp, fontWeight = FontWeight.ExtraBold, color = color)
    }
}

/** Tarjeta de paso numerado (Nuevo encargo): icono, título, ayuda y número o ✓ cuando el paso está completo. */
@Composable fun StepCard(n: Int, icon: ImageVector, title: String, sub: String, done: Boolean, content: @Composable ColumnScope.() -> Unit) {
    Column(Modifier.fillMaxWidth().glass(day = isDay()).padding(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            IconTile(icon, W.icon, 36); Spacer(Modifier.width(12.dp))
            Column(Modifier.weight(1f)) { Text(title, fontSize = 15.5.sp, fontWeight = FontWeight.ExtraBold); Text(sub, fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant) }
            val bg by animateColorAsState(if (done) W.green else W.tile, tween(260), label = "step")
            Box(Modifier.size(22.dp).clip(CircleShape).background(bg).border(1.dp, if (done) W.green else W.line2, CircleShape), contentAlignment = Alignment.Center) {
                if (done) Icon(Icons.Filled.Check, "Completo", Modifier.size(14.dp), tint = Color.White)
                else Text("$n", fontSize = 11.sp, fontWeight = FontWeight.ExtraBold, color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
        }
        content()
    }
}

/** Etapas del pedido en círculos unidos por una línea (Pedido → En tránsito → Recibido → Entregado). */
@Composable fun StatusStepper(flow: List<String>, current: String) {
    val idx = flow.indexOf(current)
    Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.Top) {
        flow.forEachIndexed { i, s ->
            Column(Modifier.weight(1f), horizontalAlignment = Alignment.CenterHorizontally) {
                Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
                    Box(Modifier.weight(1f).height(2.dp).background(if (i == 0) Color.Transparent else if (i <= idx) C.status(s).copy(alpha = .6f) else W.line))
                    val c = C.status(s); val on = i <= idx
                    Box(Modifier.size(26.dp).clip(CircleShape).background(if (i < idx) c else if (i == idx) c.copy(alpha = .22f) else W.tile).border(if (i == idx) 2.dp else 1.dp, if (on) c else W.line2, CircleShape), contentAlignment = Alignment.Center) {
                        if (i < idx) Icon(Icons.Filled.Check, null, Modifier.size(14.dp), tint = Color.White)
                        else Text("${i + 1}", fontSize = 11.sp, fontWeight = FontWeight.ExtraBold, color = if (on) c else MaterialTheme.colorScheme.onSurfaceVariant)
                    }
                    Box(Modifier.weight(1f).height(2.dp).background(if (i == flow.size - 1) Color.Transparent else if (i < idx) C.status(flow[i + 1]).copy(alpha = .6f) else W.line))
                }
                Spacer(Modifier.height(5.dp))
                Text(s, fontSize = 11.sp, fontWeight = FontWeight.Bold, maxLines = 1, color = if (i <= idx) MaterialTheme.colorScheme.onSurface else MaterialTheme.colorScheme.onSurfaceVariant)
            }
        }
    }
}

/** Tarjeta del total con degradado violeta-azul (final de Nuevo encargo). */
@Composable fun TotalCard(total: String, content: @Composable ColumnScope.() -> Unit) {
    val shape = RoundedCornerShape(20.dp)
    Column(Modifier.fillMaxWidth().glow(W.blue, 18.dp, 10.dp, .35f).clip(shape)
        .background(Brush.linearGradient(listOf(W.blue.copy(alpha = if (W.day) .10f else .22f), W.vio.copy(alpha = if (W.day) .10f else .22f)))).border(1.dp, W.blue.copy(alpha = .45f), shape).padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(8.dp)) {
        Text("Total del encargo", fontSize = 12.5.sp, fontWeight = FontWeight.SemiBold, color = MaterialTheme.colorScheme.onSurfaceVariant)
        Text(total, fontSize = 30.sp, fontWeight = FontWeight.ExtraBold, letterSpacing = (-0.5).sp)
        Box(Modifier.fillMaxWidth().height(1.dp).background(W.line))
        content()
    }
}

/** Borde discontinuo (zona para agregar captura). */
fun Modifier.dashedBorder(color: Color, radius: androidx.compose.ui.unit.Dp): Modifier = this.drawBehind {
    val r = radius.toPx()
    drawRoundRect(color, cornerRadius = CornerRadius(r, r), style = Stroke(width = 1.5f * density, pathEffect = androidx.compose.ui.graphics.PathEffect.dashPathEffect(floatArrayOf(8f * density, 6f * density))))
}

/** Encabezado de sección con punto de color y total a la derecha (• Saldo pendiente · $265.00). */
@Composable fun DotHeader(title: String, color: Color, right: String? = null) {
    Row(Modifier.fillMaxWidth().padding(top = 4.dp), verticalAlignment = Alignment.CenterVertically) {
        Box(Modifier.size(8.dp).clip(CircleShape).background(color)); Spacer(Modifier.width(8.dp))
        Text(title, fontWeight = FontWeight.ExtraBold, fontSize = 15.sp, modifier = Modifier.weight(1f))
        if (right != null) Text(right, fontSize = 12.5.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
    }
}

/** Botón cuadrado de icono con el mismo vidrio que "Volver" (flechas de periodo, acciones de cabecera). */
@Composable fun GlassIconBtn(icon: ImageVector, desc: String, enabled: Boolean = true, onClick: () -> Unit) {
    val src = remember { MutableInteractionSource() }; val sh = RoundedCornerShape(12.dp)
    Box(Modifier.pressBounce(src, .9f).size(40.dp).graphicsLayer { alpha = if (enabled) 1f else .4f }.clip(sh).background(W.btnBg).border(1.dp, W.btnLine, sh)
        .clickable(src, null, enabled = enabled) { tapFeedback(); onClick() }, contentAlignment = Alignment.Center) {
        Icon(icon, desc, Modifier.size(20.dp), tint = MaterialTheme.colorScheme.onSurface)
    }
}
