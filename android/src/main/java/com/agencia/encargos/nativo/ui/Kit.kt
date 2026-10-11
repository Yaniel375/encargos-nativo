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
import androidx.compose.material.icons.filled.Info
import androidx.compose.material.icons.outlined.LocalShipping
import androidx.compose.material.icons.outlined.Flight
import androidx.compose.material.icons.outlined.DirectionsBoat
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
import androidx.compose.animation.core.animateFloat
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
import kotlinx.coroutines.launch
import androidx.compose.material.icons.filled.ZoomIn
import androidx.compose.material.icons.automirrored.filled.KeyboardArrowLeft
import androidx.compose.material.icons.automirrored.filled.KeyboardArrowRight
import androidx.compose.ui.input.pointer.positionChanged
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.foundation.gestures.calculatePan
import androidx.compose.foundation.gestures.calculateZoom
import androidx.compose.foundation.gestures.awaitFirstDown
import androidx.compose.foundation.gestures.awaitEachGesture
import androidx.compose.foundation.gestures.detectTapGestures
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.layout.aspectRatio
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.fillMaxSize
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
    // al cambiar el tamaño, toda la interfaz crece o se reduce suavemente hasta el nuevo valor
    val base = androidx.compose.ui.platform.LocalDensity.current
    val s by androidx.compose.animation.core.animateFloatAsState(UiScale.value, if (Motion.enabled) tween(450, easing = EaseOut) else androidx.compose.animation.core.snap(), label = "escala")
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
    val day = isDay(); val compact = LocalCompact.current; val shape = RoundedCornerShape(if (compact) 12.dp else 14.dp)
    Column(modifier.fillMaxWidth(), verticalArrangement = Arrangement.spacedBy(if (compact) 4.dp else 6.dp)) {
        Text(label, fontSize = if (compact) 11.5.sp else 12.5.sp, fontWeight = FontWeight.SemiBold, color = MaterialTheme.colorScheme.onSurfaceVariant)
        BasicTextField(value, onChange, Modifier.fillMaxWidth().let { if (focused) it.glow(W.blue, 6.dp, 0.dp, .22f) else it }, singleLine = singleLine, minLines = minLines,
            textStyle = TextStyle(color = MaterialTheme.colorScheme.onSurface, fontFamily = Manrope, fontSize = if (compact) 14.sp else 15.sp, fontWeight = FontWeight.SemiBold),
            cursorBrush = SolidColor(W.blue), interactionSource = src,
            visualTransformation = if (password && !show) PasswordVisualTransformation() else VisualTransformation.None,
            keyboardOptions = KeyboardOptions(keyboardType = if (number) KeyboardType.Decimal else if (password) KeyboardType.Password else KeyboardType.Text),
            decorationBox = { inner ->
                Row(Modifier.fillMaxWidth().clip(shape).background(W.inputBg).border(1.dp, bd, shape).padding(horizontal = if (compact) 12.dp else 14.dp, vertical = if (compact) 11.dp else if (singleLine) 14.dp else 12.dp), verticalAlignment = Alignment.CenterVertically) {
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
    val compact = LocalCompact.current
    Row(modifier.height(if (compact) 44.dp else 50.dp).pressBounce(src).graphicsLayer { alpha = if (enabled) 1f else .5f }.glow(color, 14.dp, 8.dp, if (enabled) .45f else 0f)
        .clip(shape).background(brush).clickable(src, null, enabled = enabled) { tapFeedback(); once() }.padding(horizontal = 16.dp),
        horizontalArrangement = Arrangement.Center, verticalAlignment = Alignment.CenterVertically) {
        if (icon != null) { Icon(icon, null, Modifier.size(18.dp), tint = Color.White); Spacer(Modifier.width(8.dp)) }
        Text(text, fontWeight = FontWeight.ExtraBold, fontSize = if (compact) 14.sp else 15.sp, color = Color.White, maxLines = 1, overflow = TextOverflow.Ellipsis)
    }
}
/** Botón secundario (.secondary): vidrio gris con borde. */
@Composable fun Secondary(text: String, modifier: Modifier = Modifier, icon: ImageVector? = null, color: Color? = null, onClick: () -> Unit) {
    val src = remember { MutableInteractionSource() }; val shape = RoundedCornerShape(14.dp); val once = rememberOnce(onClick)
    val fg = color ?: MaterialTheme.colorScheme.onSurface
    Row(modifier.height(if (LocalCompact.current) 44.dp else 50.dp).pressBounce(src).clip(shape).background(if (color != null) color.copy(alpha = .08f) else W.tile).border(1.dp, if (color != null) color.copy(alpha = .35f) else W.line2, shape)
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

/** Reloj de las olas: avanza de forma continua mientras la pantalla está abierta (se detiene sola al salir de ella,
 *  y Android la pausa con la app en segundo plano). Sin animaciones del sistema, las olas quedan quietas. */
@Composable fun waveClock(): androidx.compose.runtime.State<Float> {
    if (!Motion.enabled) return remember { androidx.compose.runtime.mutableFloatStateOf(0f) }
    val tr = androidx.compose.animation.core.rememberInfiniteTransition(label = "olas")
    // se lee solo dentro del dibujo: cada fotograma redibuja el círculo, sin recomponer la pantalla
    return tr.animateFloat(0f, (2 * Math.PI).toFloat(), androidx.compose.animation.core.infiniteRepeatable(tween(2400, easing = androidx.compose.animation.core.LinearEasing)), label = "fase")
}
/** Superficie ondulada (olas que avanzan de izquierda a derecha) cerrada hasta el fondo. */
private fun wavePath(left: Float, w: Float, surf: Float, amp: Float, phase: Float, bottom: Float, cycles: Float = 1.25f): androidx.compose.ui.graphics.Path =
    androidx.compose.ui.graphics.Path().apply {
        moveTo(left, bottom); lineTo(left, surf)
        val steps = 36
        for (i in 0..steps) { val f = i / steps.toFloat(); lineTo(left + w * f, surf + amp * kotlin.math.sin(f * cycles * 2f * Math.PI.toFloat() - phase)) }
        lineTo(left + w, bottom); close()
    }
/** Agua dentro de un círculo: dos olas del mismo azul (una más clara detrás) recortadas exactamente al contorno.
 *  El nivel (0..1) es la altura del agua respecto al diámetro: 30 % ocupa el 30 % inferior. */
private fun androidx.compose.ui.graphics.drawscope.DrawScope.waterIn(c: Offset, r: Float, level: Float, phase: Float, amp: Float) {
    if (level <= 0f) return
    val d = r * 2; val left = c.x - r; val bottom = c.y + r
    val surf = (c.y - r) + d * (1 - level)
    val circle = androidx.compose.ui.graphics.Path().apply { addOval(androidx.compose.ui.geometry.Rect(c, r)) }
    clipPath(circle) {
        drawPath(wavePath(left, d, surf, amp * .75f, -phase * 1.15f + 1.9f, bottom, 1.05f), Color(0xFF60A5FA).copy(alpha = .40f))
        drawPath(wavePath(left, d, surf, amp, phase, bottom), Brush.verticalGradient(listOf(Color(0xFF60A5FA), Color(0xFF2563EB)), surf - amp, bottom))
    }
}

/** Indicador "agua" de Pagos y Estadísticas: el círculo se llena hasta el % cobrado con una ola discreta que se calma
 *  al llegar; el porcentaje sube al mismo ritmo y termina en el valor real. */
@Composable fun Gauge(pct: Double, label: String = "cobrado", size: Int = 100) = RingGauge(pct, label, size)

/** Anillo de cobro del inicio: el arco se dibuja y el % sube a la vez (pasa del valor anterior al nuevo si cambian los datos). */
@Composable fun RingGauge(pct: Double, label: String = "cobrado", size: Int = 100) {
    val target = (pct / 100).coerceIn(0.0, 1.0).toFloat()
    val (k, wave) = waterLevel(target, 1500)
    val shown = if (target > 0f && Motion.enabled && k != target) pct * (k / target) else pct
    val phase = waveClock()
    Box(Modifier.size(size.dp).semantics(mergeDescendants = true) { contentDescription = "${Math.round(pct)}% $label" }, contentAlignment = Alignment.Center) {
        Canvas(Modifier.size(size.dp)) {
            val sw = this.size.minDimension * .085f; val inset = sw / 2 + 2f
            val sz = Size(this.size.width - inset * 2, this.size.height - inset * 2)
            // agua dentro del anillo (recortada al hueco interior)
            val inner = this.size.minDimension / 2 - inset - sw / 2 - 3f
            waterIn(Offset(this.size.width / 2, this.size.height / 2), inner, k, phase.value + k * 6f, inner * 2 * (.03f + .02f * wave))
            drawArc(W.track, 0f, 360f, false, Offset(inset, inset), sz, style = Stroke(sw))
            if (k > 0f) drawArc(Brush.sweepGradient(listOf(W.cyan, W.blue, Color(0xFF60A5FA), W.cyan)), -90f, 360f * k, false, Offset(inset, inset), sz, style = Stroke(sw, cap = StrokeCap.Round))
        }
        Column(horizontalAlignment = Alignment.CenterHorizontally) { val onWater = !W.day || k > .45f
            Text("${Math.round(shown)}%", fontSize = (size / 4.6).sp, fontWeight = FontWeight.ExtraBold, color = if (onWater) Color.White else W.tx)
            Text(label, fontSize = 11.sp, color = if (onWater) Color.White.copy(alpha = .85f) else MaterialTheme.colorScheme.onSurfaceVariant) }
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
    // al aplicar un ajuste, la fila destella en azul y el texto nuevo entra deslizándose
    val flash = remember { Animatable(0f) }; val first = remember { booleanArrayOf(true) }
    LaunchedEffect(sub, value) {
        if (first[0]) { first[0] = false; return@LaunchedEffect }
        if (Motion.enabled) { flash.snapTo(1f); flash.animateTo(0f, tween(1100, easing = EaseOut)) }
    }
    Row(Modifier.fillMaxWidth().drawBehind { if (flash.value > 0f) drawRect(W.blue.copy(alpha = .16f * flash.value)) }
        .let { if (onClick != null) it.clickable(src, null) { tapFeedback(); onClick() } else it }.padding(horizontal = 14.dp, vertical = 12.dp), verticalAlignment = Alignment.CenterVertically) {
        IconTile(icon, tint, 34); Spacer(Modifier.width(12.dp))
        Column(Modifier.weight(1f)) {
            Text(title, fontWeight = FontWeight.ExtraBold, fontSize = 14.5.sp, maxLines = 2, color = titleColor ?: MaterialTheme.colorScheme.onSurface)
            if (!sub.isNullOrEmpty()) androidx.compose.animation.AnimatedContent(sub, transitionSpec = { (fadeIn(tween(260)) + androidx.compose.animation.slideInVertically(tween(260)) { it / 2 }) togetherWith fadeOut(tween(160)) }, label = "sub") { s2 ->
                Text(s2, fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant, maxLines = 2, overflow = TextOverflow.Ellipsis) }
        }
        if (value != null) { Spacer(Modifier.width(8.dp)); androidx.compose.animation.AnimatedContent(value, transitionSpec = { (fadeIn(tween(260)) + androidx.compose.animation.scaleIn(tween(260), .85f)) togetherWith fadeOut(tween(160)) }, label = "val") { v2 ->
            Text(v2, fontSize = 13.sp, fontWeight = FontWeight.Bold, color = if (flash.value > .05f) W.blueInk else MaterialTheme.colorScheme.onSurfaceVariant) } }
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

/** Marco común de todas las ventanas (hojas de Ajustes, Meta, confirmaciones…): tarjeta de vidrio centrada, radio 22,
 *  cabecera con icono en recuadro y título como las tarjetas de Inicio; se desplaza y respeta el teclado. */
@Composable private fun WindowFrame(dismiss: () -> Unit, compact: Boolean = false, content: @Composable ColumnScope.() -> Unit) {
    androidx.compose.ui.window.Dialog(onDismissRequest = dismiss, properties = androidx.compose.ui.window.DialogProperties(usePlatformDefaultWidth = false)) {
        val none = remember { MutableInteractionSource() }; val none2 = remember { MutableInteractionSource() }
        val shape = RoundedCornerShape(22.dp)
        Box(Modifier.fillMaxSize().clickable(none, null) { dismiss() }.imePadding().padding(horizontal = 14.dp, vertical = 18.dp), contentAlignment = Alignment.Center) {
            Column(Modifier.fillMaxWidth().widthIn(max = 560.dp).enterUp(0).clip(shape).background(W.menu).border(1.dp, W.line2, shape).clickable(none2, null) {}
                .padding(if (compact) 15.dp else 18.dp), verticalArrangement = Arrangement.spacedBy(if (compact) 11.dp else 14.dp), content = content)
        }
    }
}
@Composable private fun WindowHead(title: @Composable () -> Unit, icon: ImageVector?, close: (() -> Unit)?, sub: String? = null, compact: Boolean = false) {
    Row(verticalAlignment = Alignment.CenterVertically) {
        if (icon != null) { IconTile(icon, W.icon, if (compact) 32 else 36); Spacer(Modifier.width(if (compact) 10.dp else 12.dp)) }
        Column(Modifier.weight(1f)) {
            androidx.compose.material3.ProvideTextStyle(TextStyle(fontFamily = Manrope, fontSize = if (compact) 16.sp else 18.sp, fontWeight = FontWeight.ExtraBold, letterSpacing = (-0.16).sp)) { title() }
            if (sub != null) Text(sub, fontSize = 11.5.sp, lineHeight = 15.sp, color = MaterialTheme.colorScheme.onSurfaceVariant, maxLines = 2, overflow = TextOverflow.Ellipsis)
        }
        if (close != null) { Spacer(Modifier.width(8.dp)); Secondary("Cerrar", Modifier.height(if (compact) 34.dp else 38.dp)) { close() } }
    }
}

/** ¿Estamos dentro de una hoja compacta de Ajustes? (campos, botones y avisos se dibujan más pequeños). */
val LocalCompact = androidx.compose.runtime.compositionLocalOf { false }

/** Hoja de Ajustes: marco común compacto, icono, título, una línea de detalle y botón "Cerrar". */
@Composable fun Sheet(title: String, dismiss: () -> Unit, icon: ImageVector? = null, sub: String? = null, content: @Composable ColumnScope.() -> Unit) {
    WindowFrame(dismiss, compact = true) {
        WindowHead({ Text(title) }, icon, dismiss, sub, compact = true)
        Box(Modifier.fillMaxWidth().height(1.dp).background(W.line))
        androidx.compose.runtime.CompositionLocalProvider(LocalCompact provides true) {
            Column(Modifier.weight(1f, fill = false).verticalScroll(rememberScrollState()), verticalArrangement = Arrangement.spacedBy(10.dp), content = content)
        }
    }
}

/** ¿Este botón está en el pie de una ventana? (el de confirmar se pinta como botón principal). */
private val LocalDlgSlot = androidx.compose.runtime.compositionLocalOf { 0 }   // 0 = fuera, 1 = cancelar, 2 = confirmar

/** Ventana de aviso o formulario con el estilo de la app (sustituye al diálogo del sistema en todas las pantallas):
 *  mismo marco que las hojas; pie con "Cancelar" a la izquierda y la acción principal a la derecha. */
@Composable fun AlertDialog(onDismissRequest: () -> Unit, confirmButton: (@Composable () -> Unit)? = null, modifier: Modifier = Modifier,
                            dismissButton: (@Composable () -> Unit)? = null, icon: ImageVector? = null, title: (@Composable () -> Unit)? = null,
                            text: (@Composable () -> Unit)? = null, @Suppress("UNUSED_PARAMETER") shape: androidx.compose.ui.graphics.Shape? = null) {
    WindowFrame(onDismissRequest) {
        if (title != null) WindowHead(title, icon, null)
        if (text != null) Column(modifier.fillMaxWidth().weight(1f, fill = false).verticalScroll(rememberScrollState())) {
            androidx.compose.runtime.CompositionLocalProvider(androidx.compose.material3.LocalContentColor provides MaterialTheme.colorScheme.onSurface) {
                androidx.compose.material3.ProvideTextStyle(TextStyle(fontFamily = Manrope, fontSize = 14.sp, lineHeight = 20.sp)) { text() }
            }
        }
        if (confirmButton != null || dismissButton != null) Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(10.dp), verticalAlignment = Alignment.CenterVertically) {
            if (dismissButton != null) Box(Modifier.weight(1f)) { androidx.compose.runtime.CompositionLocalProvider(LocalDlgSlot provides 1) { dismissButton() } }
            if (confirmButton != null) Box(Modifier.weight(1f)) { androidx.compose.runtime.CompositionLocalProvider(LocalDlgSlot provides 2) { confirmButton() } }
        }
    }
}

/** Botón de texto con el estilo de la app: en el pie de una ventana ocupa su mitad (confirmar = principal azul, o rojo si
 *  [tone] es rojo); fuera de las ventanas es un botón secundario compacto. */
@Composable fun TextButton(onClick: () -> Unit, modifier: Modifier = Modifier, enabled: Boolean = true, tone: Color? = null, content: @Composable RowScope.() -> Unit) {
    val slot = LocalDlgSlot.current; val primary = slot == 2
    val src = remember { MutableInteractionSource() }; val shape = RoundedCornerShape(if (slot > 0) 14.dp else 12.dp); val once = rememberOnce(onClick)
    val base = tone ?: C.blue
    val brush: Brush = if (primary) (if (tone == null) W.primary else Brush.linearGradient(listOf(base.copy(alpha = .85f), base))) else SolidColor(W.tile)
    val fg = if (primary) Color.White else MaterialTheme.colorScheme.onSurface
    Row(modifier.let { if (slot > 0) it.fillMaxWidth().height(48.dp) else it.heightIn(min = 38.dp) }.pressBounce(src, .96f).graphicsLayer { alpha = if (enabled) 1f else .45f }
        .clip(shape).background(brush).let { if (primary) it else it.border(1.dp, W.line2, shape) }
        .clickable(src, null, enabled = enabled) { tapFeedback(); once() }.padding(horizontal = 14.dp),
        horizontalArrangement = Arrangement.Center, verticalAlignment = Alignment.CenterVertically) {
        androidx.compose.runtime.CompositionLocalProvider(androidx.compose.material3.LocalContentColor provides fg) {
            androidx.compose.material3.ProvideTextStyle(TextStyle(fontFamily = Manrope, fontSize = 14.sp, fontWeight = if (primary) FontWeight.ExtraBold else FontWeight.Bold)) { content() }
        }
    }
}

/* ======================= Foto de perfil del administrador ======================= */
/** Se guarda solo en este dispositivo (ajustes locales), reducida a 400 px. */
object Profile {
    const val KEY = "encargos_profile_photo"
    var photo by mutableStateOf("")
}
/** Foto de perfil (o la inicial si no hay foto) en recuadro redondeado. */
@Composable fun ProfilePic(name: String, size: Int, border: Color = W.line2) {
    val p = Profile.photo
    val bm = remember(p) { if (p.isEmpty()) null else com.agencia.encargos.nativo.util.Images.decode(p, 400) }
    if (bm == null) { Avatar(name, C.blue, size); return }
    val shape = RoundedCornerShape((size * .3).dp)
    Image(bm.asImageBitmap(), "Foto de perfil", Modifier.size(size.dp).clip(shape).border(1.dp, border, shape), contentScale = ContentScale.Crop)
}

/** Recuadro informativo azul de las hojas. */
@Composable fun InfoBox(text: String) {
    val shape = RoundedCornerShape(14.dp)
    val compact = LocalCompact.current
    Row(Modifier.fillMaxWidth().clip(shape).background(W.blue.copy(alpha = .08f)).border(1.dp, W.blue.copy(alpha = .35f), shape).padding(horizontal = if (compact) 10.dp else 12.dp, vertical = if (compact) 9.dp else 12.dp), verticalAlignment = Alignment.Top) {
        Icon(Icons.Filled.Info, null, Modifier.padding(top = 1.dp).size(if (compact) 15.dp else 17.dp), tint = W.blueInk); Spacer(Modifier.width(8.dp))
        Text(text, fontSize = if (compact) 12.sp else 13.sp, lineHeight = if (compact) 16.sp else 18.sp, color = MaterialTheme.colorScheme.onSurface)
    }
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

/* ======================= Fotos completas (sin recortes ni deformaciones) ======================= */

/** Proporción real de una foto (ancho / alto), limitada para que una foto muy alargada no rompa el diseño. */
private fun ratioOf(bm: Bitmap) = (bm.width.toFloat() / bm.height.coerceAtLeast(1)).coerceIn(0.35f, 3f)

/** Foto completa a todo el ancho: respeta su proporción ("contain"); si es muy alta, se limita la altura y se ve entera dentro. */
@Composable fun FitPhoto(bm: Bitmap?, modifier: Modifier = Modifier, maxHeight: Int = 320, onClick: (() -> Unit)? = null) {
    if (bm == null) return
    val shape = RoundedCornerShape(14.dp)
    Box(modifier.fillMaxWidth().heightIn(max = maxHeight.dp).aspectRatio(ratioOf(bm), matchHeightConstraintsFirst = false).clip(shape).background(W.tile).border(1.dp, W.line, shape)
        .let { if (onClick != null) it.clickable { tapFeedback(); onClick() } else it }, contentAlignment = Alignment.Center) {
        Image(bm.asImageBitmap(), "Foto", Modifier.fillMaxSize(), contentScale = ContentScale.Fit)
        if (onClick != null) ZoomBadge(Modifier.align(Alignment.BottomEnd))
    }
}

/** Fila de miniaturas que muestran cada foto ENTERA (misma altura, ancho según su proporción). Tocar abre el visor. */
@Composable fun PhotoStrip(photos: List<String>, height: Int = 110, onOpen: (Int) -> Unit) {
    Row(Modifier.horizontalScroll(rememberScrollState()), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
        photos.forEachIndexed { i, s ->
            val bm = remember(s) { com.agencia.encargos.nativo.util.Images.decode(s, 500) } ?: return@forEachIndexed
            val shape = RoundedCornerShape(12.dp)
            Box(Modifier.height(height.dp).width((height * ratioOf(bm)).dp).clip(shape).background(W.tile).border(1.dp, W.line, shape)
                .clickable { tapFeedback(); onOpen(i) }.semantics { contentDescription = "Foto ${i + 1} de ${photos.size}" }, contentAlignment = Alignment.Center) {
                Image(bm.asImageBitmap(), null, Modifier.fillMaxSize(), contentScale = ContentScale.Fit)
                if (photos.size > 1) Text("${i + 1}/${photos.size}", fontSize = 10.sp, fontWeight = FontWeight.Bold, color = Color.White,
                    modifier = Modifier.align(Alignment.TopStart).padding(5.dp).clip(RoundedCornerShape(6.dp)).background(Color.Black.copy(alpha = .55f)).padding(horizontal = 5.dp, vertical = 1.dp))
                ZoomBadge(Modifier.align(Alignment.BottomEnd))
            }
        }
    }
}

@Composable private fun ZoomBadge(modifier: Modifier) {
    Box(modifier.padding(5.dp).size(22.dp).clip(CircleShape).background(Color.Black.copy(alpha = .55f)), contentAlignment = Alignment.Center) {
        Icon(androidx.compose.material.icons.Icons.Filled.ZoomIn, null, Modifier.size(14.dp), tint = Color.White)
    }
}

/** Visor a pantalla completa: cada foto se ve entera; se amplía con dos dedos o con doble toque y, si hay varias,
 *  se pasa de una a otra deslizando (o con las flechas). "Eliminar foto" solo aparece donde ya existía esa acción. */
@Composable fun PhotoViewer(list: List<String>, start: Int, onDelete: ((Int) -> Unit)?, dismiss: () -> Unit) {
    if (list.isEmpty()) { dismiss(); return }
    androidx.compose.ui.window.Dialog(onDismissRequest = dismiss, properties = androidx.compose.ui.window.DialogProperties(usePlatformDefaultWidth = false)) {
        val pager = androidx.compose.foundation.pager.rememberPagerState(initialPage = start.coerceIn(0, list.size - 1)) { list.size }
        val scope = androidx.compose.runtime.rememberCoroutineScope()
        var zoomed by remember { mutableStateOf(false) }
        Box(Modifier.fillMaxSize().background(Color(0xF2020611))) {
            androidx.compose.foundation.pager.HorizontalPager(pager, Modifier.fillMaxSize(), userScrollEnabled = !zoomed, key = { it }) { page ->
                ZoomableImage(list[page], active = pager.currentPage == page) { zoomed = it }
            }
            // barra superior: posición y Cerrar
            Row(Modifier.fillMaxWidth().statusBarsPadding().padding(12.dp), verticalAlignment = Alignment.CenterVertically) {
                Text(if (list.size > 1) "Foto ${pager.currentPage + 1} de ${list.size}" else "Foto", color = Color.White, fontWeight = FontWeight.ExtraBold, fontSize = 15.sp, modifier = Modifier.weight(1f))
                Secondary("Cerrar", Modifier.height(40.dp), color = Color.White) { dismiss() }
            }
            // barra inferior: anterior / siguiente / eliminar
            Row(Modifier.align(Alignment.BottomCenter).fillMaxWidth().navigationBarsPadding().padding(12.dp), horizontalArrangement = Arrangement.spacedBy(8.dp), verticalAlignment = Alignment.CenterVertically) {
                if (list.size > 1) {
                    GlassIconBtn(Icons.AutoMirrored.Filled.KeyboardArrowLeft, "Foto anterior", enabled = pager.currentPage > 0) { scope.launch { pager.animateScrollToPage(pager.currentPage - 1) } }
                    GlassIconBtn(Icons.AutoMirrored.Filled.KeyboardArrowRight, "Foto siguiente", enabled = pager.currentPage < list.size - 1) { scope.launch { pager.animateScrollToPage(pager.currentPage + 1) } }
                }
                Text("Pellizca o toca dos veces para ampliar", fontSize = 11.5.sp, color = Color.White.copy(alpha = .7f), modifier = Modifier.weight(1f), textAlign = TextAlign.Center)
                if (onDelete != null) Secondary("Eliminar", Modifier.height(40.dp), color = C.rose) { onDelete(pager.currentPage) }
            }
        }
    }
}

/** Una foto entera que se puede ampliar (1× a 5×) y mover cuando está ampliada; vuelve a 1× al cambiar de foto. */
@Composable private fun ZoomableImage(src: String, active: Boolean, onZoom: (Boolean) -> Unit) {
    val bm = remember(src) { com.agencia.encargos.nativo.util.Images.decode(src, 1600) }
    var scale by remember { mutableStateOf(1f) }; var off by remember { mutableStateOf(Offset.Zero) }
    LaunchedEffect(active) { if (!active) { scale = 1f; off = Offset.Zero } }
    LaunchedEffect(scale, active) { if (active) onZoom(scale > 1.01f) }
    Box(Modifier.fillMaxSize().padding(top = 64.dp, bottom = 72.dp)
        .pointerInput(src) { detectTapGestures(onDoubleTap = { p -> if (scale > 1.01f) { scale = 1f; off = Offset.Zero } else { scale = 2.5f; off = (Offset(size.width / 2f, size.height / 2f) - p) * 1.5f } }) }
        .pointerInput(src) {
            awaitEachGesture {
                awaitFirstDown(requireUnconsumed = false)
                do {
                    val ev = awaitPointerEvent()
                    val zoom = ev.calculateZoom(); val pan = ev.calculatePan()
                    val pinching = ev.changes.count { it.pressed } >= 2
                    if (pinching || scale > 1.01f) {
                        val ns = (scale * zoom).coerceIn(1f, 5f)
                        val maxX = size.width * (ns - 1) / 2f; val maxY = size.height * (ns - 1) / 2f
                        off = if (ns <= 1.01f) Offset.Zero else Offset((off.x + pan.x).coerceIn(-maxX, maxX), (off.y + pan.y).coerceIn(-maxY, maxY))
                        scale = ns
                        ev.changes.forEach { if (it.positionChanged()) it.consume() }
                    }
                } while (ev.changes.any { it.pressed })
            }
        }, contentAlignment = Alignment.Center) {
        if (bm == null) Text("No se pudo abrir esta foto.", color = Color.White)
        else Image(bm.asImageBitmap(), "Foto ampliable", Modifier.fillMaxSize().graphicsLayer { scaleX = scale; scaleY = scale; translationX = off.x; translationY = off.y }, contentScale = ContentScale.Fit)
    }
}


/* ======================= Menús y opciones con el estilo de la app ======================= */
/** Quita el emoji del principio de un texto (los iconos van en su recuadro). */
fun noEmoji(t: String): String { var i = 0; while (i < t.length && !t[i].isLetterOrDigit() && t[i] != '¿' && t[i] != '¡' && t[i] != '$' && t[i] != '+' && t[i] != '-') i++; return t.substring(i).trim() }

/** Icono del tipo de envío (avión, barco u otro). */
fun shipIcon(type: String?): ImageVector = when (type) { "barco" -> androidx.compose.material.icons.Icons.Outlined.DirectionsBoat; null, "", "avion" -> androidx.compose.material.icons.Icons.Outlined.Flight; else -> androidx.compose.material.icons.Icons.Outlined.LocalShipping }

/** Menú desplegable de la app: mismo marco, ancho y filas que el menú del perfil (vidrio, radio 16, borde fino). */
@Composable fun AppMenu(open: Boolean, close: () -> Unit, content: @Composable ColumnScope.() -> Unit) {
    androidx.compose.material3.DropdownMenu(open, close, shape = RoundedCornerShape(16.dp), containerColor = W.menu,
        modifier = Modifier.width(226.dp).border(1.dp, W.line2, RoundedCornerShape(16.dp))) {
        Column(Modifier.padding(horizontal = 6.dp), verticalArrangement = Arrangement.spacedBy(5.dp), content = content)
    }
}
/** Fila de menú (idéntica a las del menú del perfil): icono en recuadro de 26, texto de 12,5 y misma altura en todas;
 *  las acciones destructivas en rojo. */
@Composable fun MenuItem(icon: ImageVector, text: String, @Suppress("UNUSED_PARAMETER") sub: String? = null, danger: Boolean = false, onClick: () -> Unit) {
    val src = remember { MutableInteractionSource() }; val c = if (danger) C.rose else W.tx
    Row(Modifier.fillMaxWidth().height(34.dp).pressBounce(src, .97f).clip(RoundedCornerShape(12.dp)).clickable(src, null) { tapFeedback(); onClick() }.padding(horizontal = 4.dp),
        verticalAlignment = Alignment.CenterVertically) {
        val ts = RoundedCornerShape(8.dp)
        Box(Modifier.size(26.dp).clip(ts).background(if (danger) C.rose.copy(alpha = .10f) else W.tile).border(1.dp, c.copy(alpha = .25f), ts), contentAlignment = Alignment.Center) {
            Icon(icon, null, Modifier.size(14.dp), tint = c) }
        Spacer(Modifier.width(9.dp))
        Text(text, Modifier.weight(1f), fontSize = 12.5.sp, fontWeight = FontWeight.SemiBold, color = c, maxLines = 1, overflow = TextOverflow.Ellipsis)
    }
}
@Composable fun MenuSep() = Box(Modifier.fillMaxWidth().padding(horizontal = 4.dp).height(1.dp).background(W.line))

/** Opción de una ventana (Enviar como imagen, PDF…): fila con icono, título, detalle y flecha; la principal con icono azul. */
@Composable fun OptionRow(icon: ImageVector, title: String, sub: String, primary: Boolean = false, onClick: () -> Unit) {
    val src = remember { MutableInteractionSource() }; val once = rememberOnce(onClick)
    Row(Modifier.fillMaxWidth().pressBounce(src, .98f).clickable(src, null) { tapFeedback(); once() }.padding(horizontal = 12.dp, vertical = 11.dp), verticalAlignment = Alignment.CenterVertically) {
        val ts = RoundedCornerShape(11.dp)
        Box(Modifier.size(36.dp).clip(ts).let { if (primary) it.background(W.primary) else it.background(W.tile).border(1.dp, W.line, ts) }, contentAlignment = Alignment.Center) {
            Icon(icon, null, Modifier.size(18.dp), tint = if (primary) Color.White else W.icon) }
        Spacer(Modifier.width(12.dp))
        Column(Modifier.weight(1f)) {
            Text(title, fontSize = 14.5.sp, fontWeight = FontWeight.ExtraBold, color = MaterialTheme.colorScheme.onSurface)
            Text(sub, fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant, maxLines = 2)
        }
        Icon(Icons.AutoMirrored.Filled.KeyboardArrowRight, null, Modifier.size(18.dp), tint = W.mut)
    }
}
/** Grupo de opciones con separadores finos, dentro de un recuadro de vidrio. */
@Composable fun OptionGroup(content: @Composable ColumnScope.() -> Unit) {
    val sh = RoundedCornerShape(16.dp)
    Column(Modifier.fillMaxWidth().clip(sh).background(W.tile).border(1.dp, W.line, sh), content = content)
}
