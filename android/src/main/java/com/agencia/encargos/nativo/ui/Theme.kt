package com.agencia.encargos.nativo.ui

import androidx.compose.animation.core.Animatable
import androidx.compose.animation.core.CubicBezierEasing
import androidx.compose.animation.core.FastOutSlowInEasing
import androidx.compose.animation.core.LinearEasing
import androidx.compose.animation.core.RepeatMode
import androidx.compose.animation.core.Spring
import androidx.compose.animation.core.animateFloat
import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.core.infiniteRepeatable
import androidx.compose.animation.core.rememberInfiniteTransition
import androidx.compose.animation.core.spring
import androidx.compose.animation.core.tween
import androidx.compose.foundation.ExperimentalFoundationApi
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.interaction.collectIsPressedAsState
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.lazy.LazyItemScope
import androidx.compose.foundation.lazy.LazyListScope
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.compose.ui.composed
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.drawBehind
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.PathMeasure
import androidx.compose.ui.graphics.Shape
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.graphics.drawscope.scale
import androidx.compose.ui.graphics.drawscope.withTransform
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.font.Font
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import com.agencia.encargos.nativo.R
import com.agencia.encargos.nativo.util.Feedback
import kotlinx.coroutines.delay
import kotlin.math.max
import kotlin.math.min

/* =====================================================================
   Sistema visual "Aurora" — copiado de la app web (mismos colores, radios,
   fondos, tipografía Manrope y animaciones).
   ===================================================================== */

val Manrope = FontFamily(
    Font(R.font.manrope_regular, FontWeight.Normal), Font(R.font.manrope_medium, FontWeight.Medium),
    Font(R.font.manrope_semibold, FontWeight.SemiBold), Font(R.font.manrope_bold, FontWeight.Bold), Font(R.font.manrope_extrabold, FontWeight.ExtraBold),
    Font(R.font.manrope_extrabold, FontWeight.Black))

/** Colores de la app web (tema aurora, modo noche). */
object W {
    val base = Color(0xFF020611)
    val tx = Color(0xFFF1F5F9); val mut = Color(0xFF9AA6B8); val navOff = Color(0xFF7C8BA5)
    val glassA = Color(30, 41, 59, (0.62f * 255).toInt()); val glassB = Color(15, 23, 42, (0.72f * 255).toInt())
    val line = Color(148, 163, 184, (0.14f * 255).toInt()); val line2 = Color(148, 163, 184, (0.24f * 255).toInt())
    val tile = Color(148, 163, 184, (0.08f * 255).toInt())
    val bar = Color(9, 13, 24, (0.78f * 255).toInt()); val nav = Color(9, 13, 24, (0.94f * 255).toInt())
    val btnBg = Color(255, 255, 255, (0.06f * 255).toInt()); val btnLine = Color(255, 255, 255, (0.10f * 255).toInt())
    val blue = Color(0xFF3B82F6); val blueTop = Color(0xFF4F8DF7); val blueDeep = Color(0xFF2563EB); val blueInk = Color(0xFF93C5FD); val blueSoft = Color(0xFF0F2544)
    val green = Color(0xFF34D399); val greenInk = Color(0xFF6EE7B7); val rose = Color(0xFFFB7185); val amber = Color(0xFFFBBF24); val cyan = Color(0xFF22D3EE)
    val inputBg = Color(15, 23, 42, (0.55f * 255).toInt())
    val glass = Brush.linearGradient(listOf(glassA, glassB), start = Offset(0f, 0f), end = Offset(Float.POSITIVE_INFINITY, Float.POSITIVE_INFINITY))
    val primary = Brush.linearGradient(listOf(blueTop, blueDeep), start = Offset(0f, 0f), end = Offset(Float.POSITIVE_INFINITY, Float.POSITIVE_INFINITY))
    val success = Brush.linearGradient(listOf(Color(0xFF34D399), Color(0xFF059669)), start = Offset(0f, 0f), end = Offset(Float.POSITIVE_INFINITY, Float.POSITIVE_INFINITY))
}

/** Curvas de la web: cubic-bezier(.2,.8,.2,1) y el rebote (.34,1.56,.64,1). */
val EaseOut = CubicBezierEasing(.2f, .8f, .2f, 1f)
val EaseBounce = CubicBezierEasing(.34f, 1.56f, .64f, 1f)
val EaseInOut = CubicBezierEasing(.42f, 0f, .58f, 1f)

/** Tarjeta de vidrio (.aCard): degradado oscuro translúcido + borde fino. */
fun Modifier.glass(shape: Shape = RoundedCornerShape(18.dp), border: Color = W.line, day: Boolean = false): Modifier =
    if (day) this.clip(shape).background(Color.White).border(1.dp, Color(0x1F0F172A), shape)
    else this.clip(shape).background(W.glass).border(1.dp, border, shape)

/** Animación de entrada (aUp): sube 14 px y aparece en 0,55 s, escalonada 70 ms. */
fun Modifier.enterUp(index: Int, enabled: Boolean = true): Modifier = composed {
    if (!enabled) return@composed this
    val k = remember { Animatable(0f) }
    val dy = with(LocalDensity.current) { 14.dp.toPx() }
    LaunchedEffect(Unit) { delay(min(index, 9) * 70L); k.animateTo(1f, tween(550, easing = EaseOut)) }
    this.graphicsLayer { alpha = k.value; translationY = (1 - k.value) * dy }
}

/** Rebote al tocar (transition: transform .35s cubic-bezier(.34,1.56,.64,1)). */
fun Modifier.pressBounce(source: MutableInteractionSource, scaleTo: Float = 0.97f): Modifier = composed {
    val pressed by source.collectIsPressedAsState()
    val s by animateFloatAsState(if (pressed) scaleTo else 1f, if (pressed) tween(120) else spring(dampingRatio = Spring.DampingRatioMediumBouncy, stiffness = 500f), label = "press")
    this.graphicsLayer { scaleX = s; scaleY = s }
}

/** Página con entrada escalonada: envuelve cada item() en la animación aUp. */
@OptIn(ExperimentalFoundationApi::class)
class EnterScope(private val base: LazyListScope) : LazyListScope by base {
    private var i = 0
    override fun item(key: Any?, contentType: Any?, content: @Composable LazyItemScope.() -> Unit) {
        val idx = i++
        base.item(key, contentType) { Box(Modifier.enterUp(idx)) { content() } }
    }
    override fun items(count: Int, key: ((index: Int) -> Any)?, contentType: (index: Int) -> Any?, itemContent: @Composable LazyItemScope.(index: Int) -> Unit) {
        val start = i; i += count
        base.items(count, key, contentType) { n -> Box(Modifier.enterUp(start + n)) { itemContent(n) } }
    }
}

/* ============================ Fondo animado ============================ */

private class Run(val d: String, val dur: Int, val delay: Int)
private val RUNS = listOf(
    Run("M380 0 L622 248 C682 318 724 360 841 381", 19000, 0), Run("M652 292 C672 342 662 418 700 488 L841 640", 23000, -6000),
    Run("M688 556 L841 712", 13000, -3000), Run("M0 192 L174 0", 15000, -9000), Run("M0 1160 L242 1398", 17000, -4000),
    Run("M0 1394 C108 1428 186 1448 236 1494 C336 1592 430 1704 604 1748 C702 1768 772 1772 841 1812", 26000, -12000),
    Run("M604 1748 L841 1514", 18000, -14000))
private class Dot(val x: Float, val y: Float, val dur: Int, val delay: Int, val w: Float, val h: Float)
private val DOTS = listOf(Dot(.45f, .25f, 15000, -10000, 2f, 2f), Dot(.72f, .18f, 14000, -9000, 2f, 3f), Dot(.31f, .10f, 10000, -6000, 2f, 2f), Dot(.34f, .17f, 15000, 0, 3f, 2f),
    Dot(.32f, .86f, 9000, -9000, 3f, 2f), Dot(.10f, .34f, 9000, -8000, 2f, 2f), Dot(.57f, .24f, 10000, -9000, 2f, 3f), Dot(.91f, .29f, 10000, -9000, 3f, 3f),
    Dot(.28f, .53f, 10000, -8000, 3f, 2f), Dot(.76f, .13f, 12000, -7000, 3f, 3f), Dot(.58f, .46f, 16000, -9000, 2f, 2f), Dot(.42f, .37f, 11000, -11000, 2f, 2f),
    Dot(.77f, .44f, 16000, -14000, 2f, 3f), Dot(.61f, .42f, 10000, -1000, 3f, 2f))

/** Convierte el "d" de un SVG (M, L, C) en Path de Compose. */
private fun svgPath(d: String): Path {
    val p = Path(); val t = Regex("[MLC]|-?[0-9.]+").findAll(d).map { it.value }.toList(); var i = 0; var cmd = 'M'
    fun n() = t[i++].toFloat()
    while (i < t.size) {
        if (t[i].length == 1 && t[i][0] in "MLC") { cmd = t[i][0]; i++ }
        when (cmd) { 'M' -> p.moveTo(n(), n()); 'L' -> p.lineTo(n(), n()); 'C' -> p.cubicTo(n(), n(), n(), n(), n(), n()) }
    }
    return p
}

/** Fondo "aurora" de la web: imagen con deriva lenta, haces de luz que recorren las líneas, pulso azul y chispas. */
@Composable
fun AuroraBackground(modifier: Modifier = Modifier) {
    val inf = rememberInfiniteTransition(label = "bg")
    val drift by inf.animateFloat(0f, 1f, infiniteRepeatable(tween(48000, easing = EaseInOut), RepeatMode.Reverse), label = "drift")
    val drift2 by inf.animateFloat(0f, 1f, infiniteRepeatable(tween(30000, easing = EaseInOut), RepeatMode.Reverse), label = "drift2")
    val pulse by inf.animateFloat(0f, 1f, infiniteRepeatable(tween(5000, easing = EaseInOut), RepeatMode.Reverse), label = "pulse")
    val clock by inf.animateFloat(0f, 1f, infiniteRepeatable(tween(1_000_000, easing = LinearEasing)), label = "clock") // reloj de 1000 s
    val paths = remember { RUNS.map { r -> val p = svgPath(r.d); val m = PathMeasure(); m.setPath(p, false); Triple(r, m, m.length) } }
    val seg = remember { Path() }
    Box(modifier.fillMaxSize().background(W.base)) {
        // escena: imagen + líneas, con la misma deriva (translate -1.2 %, 1 % y scale 1.035)
        Box(Modifier.fillMaxSize().graphicsLayer {
            val s = 1.04f + 0.035f * drift; scaleX = s; scaleY = s
            translationX = -0.012f * size.width * drift; translationY = 0.01f * size.height * drift
        }) {
            Image(painterResource(R.drawable.aurora_bg), null, Modifier.fillMaxSize(), contentScale = ContentScale.Crop)
            androidx.compose.foundation.Canvas(Modifier.fillMaxSize()) {
                val sc = max(size.width / 841f, size.height / 1870f); val ox = (size.width - 841f * sc) / 2; val oy = (size.height - 1870f * sc) / 2
                val ms = clock * 1_000_000f
                withTransform({ translate(ox, oy); scale(sc, sc, Offset.Zero) }) {
                    paths.forEach { (r, m, len) ->
                        val ph = (((ms - r.delay) % r.dur) + r.dur) % r.dur / r.dur
                        val a = ph * len; val b = min(len, a + len * 0.06f)
                        seg.reset(); m.getSegment(a, b, seg, true)
                        drawPath(seg, Color(0x228FD0FF), style = Stroke(14f, cap = StrokeCap.Round))
                        drawPath(seg, Color(0x558FD0FF), style = Stroke(7f, cap = StrokeCap.Round))
                        drawPath(seg, Color(0xD98FD0FF), style = Stroke(3f, cap = StrokeCap.Round))
                    }
                }
            }
        }
        // pulso azul (dos manchas radiales)
        androidx.compose.foundation.Canvas(Modifier.fillMaxSize().graphicsLayer { alpha = 0.3f + 0.55f * pulse }) {
            fun blob(cx: Float, cy: Float, rx: Float, ry: Float, a: Float) {
                val c = Offset(size.width * cx, size.height * cy)
                scale(1f, ry / rx, c) { drawCircle(Brush.radialGradient(listOf(Color(37, 99, 235, (a * 255).toInt()), Color.Transparent), c, rx * 0.7f / 0.7f), rx, c) }
            }
            blob(.78f, .14f, size.width * .55f, size.height * .35f, .22f); blob(.18f, .86f, size.width * .60f, size.height * .38f, .20f)
        }
        // chispas que suben y se apagan
        androidx.compose.foundation.Canvas(Modifier.fillMaxSize().graphicsLayer { translationX = 0.015f * size.width * drift2; translationY = -0.02f * size.height * drift2 }) {
            val ms = clock * 1_000_000f; val dp = density
            DOTS.forEach { d ->
                val ph = (((ms - d.delay) % d.dur) + d.dur) % d.dur / d.dur
                val op = when { ph < .3f -> ph / .3f * .75f; ph < .6f -> .75f - (ph - .3f) / .3f * .4f; else -> .35f - (ph - .6f) / .4f * .35f }
                val sc = .6f + .4f * ph; val c = Offset(size.width * d.x, size.height * d.y - 26f * dp * ph); val r = (d.w + d.h) / 4f * dp * sc
                drawCircle(Color(96, 165, 250, (op * .45f * 255).toInt()), r * 3.2f, c)
                drawCircle(Color(0xFF9CCBFF).copy(alpha = op), r, c)
            }
        }
    }
}

/** Fondo claro (modo día de la web). */
@Composable fun DayBackground(modifier: Modifier = Modifier) = Box(modifier.fillMaxSize().background(Brush.verticalGradient(listOf(Color(0xFFF4F6FB), Color(0xFFE9EEF8)))))

/** Brillo que recorre el título una vez (aShine, 1,5 s tras 0,45 s). */
@Composable fun shineBrush(base: Color, glow: Color = W.blueInk): Brush {
    val k = remember { Animatable(-0.4f) }
    LaunchedEffect(Unit) { delay(450); k.animateTo(1.4f, tween(1500, easing = EaseInOut)) }
    val p = k.value
    return Brush.linearGradient(0f to base, max(0f, p - .12f) to base, p.coerceIn(0f, 1f) to glow, min(1f, p + .12f) to base, 1f to base,
        start = Offset(0f, 0f), end = Offset(900f, 160f))
}

/** Sombra de color difusa bajo un botón (box-shadow azul de la web). */
fun Modifier.glow(color: Color, radius: Dp = 18.dp, dy: Dp = 8.dp, alpha: Float = .55f): Modifier = this.drawBehind {
    val r = radius.toPx(); val c = Offset(size.width / 2, size.height / 2 + dy.toPx())
    scale(1f, (size.height + r) / (size.width + r), c) {
        drawCircle(Brush.radialGradient(listOf(color.copy(alpha = alpha), Color.Transparent), c, (size.width + r) / 1.6f), (size.width + r) / 1.6f, c)
    }
}

/** Un toque háptico breve (Haptics.tap de la web). */
fun tapFeedback() = Feedback.tap()

/** Para animaciones de "dibujo" (anillos, barras): 0→1 con retardo. */
@Composable fun drawIn(target: Float, durationMs: Int = 1100, delayMs: Int = 250): Float {
    val k = remember { Animatable(0f) }
    LaunchedEffect(target) { delay(delayMs.toLong()); k.animateTo(target, tween(durationMs, easing = EaseOut)) }
    return k.value
}

@Suppress("unused") private val keepEase = FastOutSlowInEasing
