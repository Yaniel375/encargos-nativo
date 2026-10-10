package com.agencia.encargos.nativo.ui

import androidx.compose.foundation.Image
import androidx.compose.animation.animateColorAsState
import androidx.compose.animation.core.Animatable
import androidx.compose.animation.core.tween
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.interaction.collectIsFocusedAsState
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.layout.widthIn
import androidx.compose.material.icons.automirrored.filled.Backspace
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.foundation.text.BasicTextField
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material.icons.automirrored.filled.Login
import androidx.compose.material.icons.filled.Check
import androidx.compose.material.icons.filled.Close
import androidx.compose.material.icons.outlined.Lock
import androidx.compose.material.icons.outlined.Person
import androidx.compose.material.icons.outlined.Shield
import androidx.compose.material.icons.outlined.Visibility
import androidx.compose.material.icons.outlined.VisibilityOff
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.SolidColor
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.text.input.VisualTransformation
import com.agencia.encargos.nativo.util.Feedback
import kotlinx.coroutines.delay
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Backspace
import androidx.compose.material.icons.filled.Fingerprint
import androidx.compose.material.icons.filled.Key
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.agencia.encargos.nativo.R
import com.agencia.encargos.nativo.util.Bio
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

/** Pantalla de entrada (igual que la web): logo que aparece y sube, tarjeta de vidrio, "Verificando…" y "Acceso" en verde con sonido. */
@Composable
fun LockScreen(onUnlocked: () -> Unit) {
    val ui = LocalUi.current; val sec = ui.app.security
    var setup by remember { mutableStateOf(!sec.hasCredentials()) }
    var usePass by remember { mutableStateOf(!sec.pinOn()) }
    var user by remember { mutableStateOf(sec.userName()) }; var pass by remember { mutableStateOf("") }; var pass2 by remember { mutableStateOf("") }
    var msg by remember { mutableStateOf("") }; var working by remember { mutableStateOf(false) }; var granted by remember { mutableStateOf(false) }
    var pin by remember { mutableStateOf("") }
    val greet = ui.core.greetName
    val scope = rememberCoroutineScope()
    val shake = remember { Animatable(0f) }

    // ----- secuencia de entrada (lgLogoIn → lgBrandMove → lgCardIn) -----
    val logo = remember { Animatable(0f) }; val move = remember { Animatable(0f) }; val card = remember { Animatable(0f) }; val chip = remember { Animatable(0f) }
    LaunchedEffect(Unit) {
        delay(250); logo.animateTo(1f, tween(520, easing = EaseBounce))
        delay(120); launch { move.animateTo(1f, tween(720, easing = EaseOut)) }
        delay(380); launch { chip.animateTo(1f, tween(420, easing = EaseOut)) }
        delay(120); card.animateTo(1f, tween(620, easing = EaseOut))
    }
    fun fail(m: String) { msg = m; Feedback.error(); scope.launch { repeat(2) { shake.animateTo(-6f, tween(70)); shake.animateTo(6f, tween(70)) }; shake.animateTo(0f, tween(60)) } }
    fun done(method: String) {
        sec.ok(method); msg = ""; granted = true; Feedback.success(); Feedback.loginSound()
        scope.launch { delay(750); onUnlocked() }
    }
    LaunchedEffect(Unit) { if (!setup && sec.bioOn() && Bio.available(ui.act)) { delay(900); Bio.prompt(ui.act) { ok -> if (ok) done("bio") } } }
    LaunchedEffect(pin) {
        if (!setup && !usePass && pin.length == sec.pinLen()) {
            val w = sec.waitMs(); if (w > 0) { fail("Demasiados intentos. Espera ${if (w >= 60000) "${w / 60000} min" else "${w / 1000} s"}."); pin = ""; return@LaunchedEffect }
            working = true; val ok = withContext(Dispatchers.Default) { sec.verifyPin(pin) }; working = false
            if (ok) done("pin") else { fail(sec.fail("pin")); pin = "" }
        }
    }
    val introY = with(LocalDensity.current) { 210.dp.toPx() }
    val green = NIGHT.green
    // brillo verde del borde al autorizar (aGreen)
    val glowK = remember { Animatable(0f) }
    LaunchedEffect(granted) { if (granted) { glowK.animateTo(1f, tween(380)); glowK.animateTo(.55f, tween(900)) } }

    Box(Modifier.fillMaxSize().background(Brush.verticalGradient(listOf(Color(2, 6, 17, 64), Color(2, 6, 17, 115)))).imePadding()) {
        androidx.compose.foundation.layout.BoxWithConstraints(Modifier.fillMaxSize().statusBarsPadding().navigationBarsPadding()) {
        val minH = maxHeight
        Column(Modifier.fillMaxWidth().verticalScroll(rememberScrollState()).heightIn(min = minH).padding(horizontal = 24.dp, vertical = 20.dp),
            horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.Center) {
            // logo con brillo
            Box(Modifier.graphicsLayer { translationY = (1 - move.value) * introY; alpha = logo.value; scaleX = .8f + .2f * logo.value; scaleY = .8f + .2f * logo.value }
                .glow(NIGHT.blue, 26.dp, 6.dp, .45f * logo.value)) {
                Image(painterResource(R.mipmap.ic_launcher), null, Modifier.size(52.dp).clip(RoundedCornerShape(15.dp)).border(1.dp, Color(96, 165, 250, 90), RoundedCornerShape(15.dp)))
            }
            Spacer(Modifier.height(10.dp))
            Column(Modifier.graphicsLayer { translationY = (1 - move.value) * introY * .9f; alpha = move.value }, horizontalAlignment = Alignment.CenterHorizontally) {
                Text("Encargos", fontSize = 24.sp, fontWeight = FontWeight.ExtraBold, letterSpacing = (-0.4).sp, color = Color(0xFFE8EEF8))
                Text("Tu negocio, en orden.", fontSize = 13.sp, color = NIGHT.mut)
            }
            Spacer(Modifier.height(12.dp))
            // chip "Acceso seguro" → "Acceso autorizado"
            val chipCol by animateColorAsState(if (granted) green else NIGHT.blueInk, tween(300), label = "chip")
            Row(Modifier.graphicsLayer { alpha = chip.value; translationY = (1 - chip.value) * 18f }.clip(RoundedCornerShape(50)).background(chipCol.copy(alpha = .10f)).border(1.dp, chipCol.copy(alpha = .35f), RoundedCornerShape(50))
                .padding(horizontal = 12.dp, vertical = 5.dp), verticalAlignment = Alignment.CenterVertically) {
                Icon(if (granted) Icons.Filled.Check else Icons.Outlined.Lock, null, Modifier.size(13.dp), tint = chipCol); Spacer(Modifier.width(6.dp))
                Text(if (granted) "Acceso autorizado" else "Acceso seguro", fontSize = 12.sp, fontWeight = FontWeight.Bold, color = chipCol)
            }
            Spacer(Modifier.height(14.dp))
            // ----- tarjeta (compacta y centrada) -----
            val cardShape = RoundedCornerShape(22.dp)
            Column(Modifier.widthIn(max = 360.dp).fillMaxWidth().graphicsLayer { alpha = card.value; translationY = (1 - card.value) * 60f + 0f; translationX = shake.value * density }
                .let { if (glowK.value > 0f) it.glow(green, 18.dp, 0.dp, .35f * glowK.value) else it }
                .clip(cardShape).background(Brush.linearGradient(listOf(Color(17, 24, 39, 214), Color(10, 15, 28, 224)))).border(1.dp, lerpColor(NIGHT.line2, green.copy(alpha = .75f), glowK.value), cardShape)
                .padding(horizontal = 18.dp, vertical = if (!setup && !usePass) 16.dp else 20.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                // encabezado centrado: candado pequeño, saludo y una línea de ayuda
                val pinMode = !setup && !usePass
                Column(Modifier.fillMaxWidth(), horizontalAlignment = Alignment.CenterHorizontally) {
                    // con PIN la cápsula de puntos ya indica el acceso: se omite el candado para que todo quepa centrado
                    if (!pinMode) Box(Modifier.size(46.dp).clip(CircleShape).background(Color(15, 23, 42, 160)).border(1.5.dp, lerpColor(Color(59, 130, 246, 150), green, glowK.value), CircleShape), contentAlignment = Alignment.Center) {
                        Icon(if (granted) Icons.Filled.Check else Icons.Outlined.Lock, null, Modifier.size(20.dp), tint = if (granted) green else Color(0xFF60A5FA))
                    }
                    if (!pinMode) Spacer(Modifier.height(10.dp))
                    Text(if (setup) "Crea tu acceso" else "Hola, $greet", fontSize = 20.sp, fontWeight = FontWeight.ExtraBold, letterSpacing = (-0.2).sp, color = Color(0xFFF1F5F9), textAlign = TextAlign.Center)
                    Spacer(Modifier.height(3.dp))
                    // con PIN, el aviso de error ocupa el lugar de la instrucción (en rojo) para no alargar la tarjeta
                    val pinErr = pinMode && msg.isNotEmpty()
                    Text(if (pinErr) msg else if (setup) "Elige un usuario y una contraseña para proteger tus datos. No hay contraseña por defecto." else if (pinMode) "Escribe tu PIN para entrar." else "Escribe tu usuario y contraseña para entrar.",
                        fontSize = 12.5.sp, color = if (pinErr) NIGHT.rose else NIGHT.mut, fontWeight = if (pinErr) FontWeight.SemiBold else FontWeight.Normal, textAlign = TextAlign.Center)
                }
                Spacer(Modifier.height(2.dp))
                if (setup || usePass) {
                    LgField(Icons.Outlined.Person, "Usuario", user, { user = it; msg = "" }, placeholder = "admin")
                    LgField(Icons.Outlined.Lock, if (setup) "Contraseña (mínimo 6)" else "Contraseña", pass, { pass = it; msg = "" }, password = true, placeholder = "Contraseña")
                    if (setup) LgField(Icons.Outlined.Lock, "Repite la contraseña", pass2, { pass2 = it; msg = "" }, password = true, placeholder = "Contraseña")
                    GoButton(if (granted) "Acceso" else if (working) "Verificando…" else if (setup) "Crear acceso" else "Iniciar sesión", working, granted) {
                        if (working || granted) return@GoButton
                        if (setup) {
                            if (pass != pass2) { fail("Las contraseñas no coinciden."); return@GoButton }
                            working = true
                            ui.scope.launchIo({ sec.setCredentials(user, pass) }) { r -> working = false; r.onSuccess { setup = false; usePass = true; done("password") }.onFailure { fail(it.message ?: "") } }
                        } else {
                            val w = sec.waitMs(); if (w > 0) { fail("Demasiados intentos. Espera ${if (w >= 60000) "${w / 60000} min" else "${w / 1000} s"}."); return@GoButton }
                            if (user.isBlank() || pass.isEmpty()) { fail("Completa usuario y contraseña."); return@GoButton }
                            working = true
                            ui.scope.launchIo({ sec.checkPassword(user, pass) }) { ok -> working = false; if (ok) done("password") else { pass = ""; fail(sec.fail("password")) } }
                        }
                    }
                    if (!setup && sec.pinOn()) Text("Entrar con PIN ›", Modifier.clickable { usePass = false; msg = "" }.padding(4.dp), color = NIGHT.blue, fontWeight = FontWeight.Bold, fontSize = 13.5.sp)
                } else {
                    // cápsula con un punto por dígito (se rellenan al escribir; verde al entrar)
                    val pillShape = RoundedCornerShape(50)
                    Row(Modifier.align(Alignment.CenterHorizontally).clip(pillShape).background(Color(15, 23, 42, 150)).border(1.dp, if (msg.isNotEmpty()) NIGHT.rose.copy(alpha = .6f) else NIGHT.line2, pillShape)
                        .padding(horizontal = 18.dp, vertical = 12.dp).semantics { contentDescription = "PIN: ${pin.length} de ${sec.pinLen()} dígitos" }, horizontalArrangement = Arrangement.spacedBy(14.dp)) {
                        repeat(sec.pinLen()) { i ->
                            val on = i < pin.length
                            val sc by androidx.compose.animation.core.animateFloatAsState(if (on) 1.15f else 1f, tween(160, easing = EaseBounce), label = "dot")
                            Box(Modifier.size(12.dp).graphicsLayer { scaleX = sc; scaleY = sc }.clip(CircleShape).background(if (on) (if (granted) green else NIGHT.blue) else Color0).border(1.5.dp, if (granted) green else NIGHT.blue.copy(alpha = .8f), CircleShape))
                        }
                    }
                    Spacer(Modifier.height(2.dp))
                    PinPad(onKey = { k -> if (!working && pin.length < sec.pinLen()) { pin += k; msg = "" } }, onDel = { pin = pin.dropLast(1) }, pal = NIGHT, key = 56,
                        left = { if (sec.bioOn() && Bio.available(ui.act)) Box(Modifier.size(56.dp).clip(CircleShape).background(NIGHT.blue.copy(alpha = .12f)).border(1.dp, NIGHT.blue.copy(alpha = .4f), CircleShape).clickable { Bio.prompt(ui.act) { ok -> if (ok) done("bio") } }, contentAlignment = Alignment.Center) { Icon(Icons.Filled.Fingerprint, "Entrar con huella", Modifier.size(28.dp), tint = NIGHT.blueInk) }
                                 else Box(Modifier.size(56.dp).clip(CircleShape).clickable { usePass = true; msg = "" }, contentAlignment = Alignment.Center) { Column(horizontalAlignment = Alignment.CenterHorizontally) { Icon(Icons.Filled.Key, null, Modifier.size(20.dp), tint = NIGHT.mut); Text("Clave", fontSize = 10.5.sp, color = NIGHT.mut) } } })
                    Box(Modifier.fillMaxWidth().padding(horizontal = 8.dp).height(1.dp).background(NIGHT.line))
                    Text("Entrar con usuario y contraseña", Modifier.align(Alignment.CenterHorizontally).clip(RoundedCornerShape(10.dp)).clickable { usePass = true; msg = "" }.padding(horizontal = 10.dp, vertical = 6.dp), color = NIGHT.blueInk, fontWeight = FontWeight.Bold, fontSize = 13.sp)
                }
                if (msg.isNotEmpty() && !pinMode) Text(msg, color = NIGHT.rose, fontSize = 13.sp, fontWeight = FontWeight.SemiBold, textAlign = TextAlign.Center, modifier = Modifier.fillMaxWidth())
                Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.Center, verticalAlignment = Alignment.CenterVertically) {
                    Icon(Icons.Outlined.Shield, null, Modifier.size(14.dp), tint = NIGHT.mut); Spacer(Modifier.width(6.dp))
                    Text("Tus datos se guardan solo en este teléfono", fontSize = 11.5.sp, color = NIGHT.mut)
                }
            }
            Spacer(Modifier.height(16.dp))
            Column(Modifier.graphicsLayer { alpha = card.value }, horizontalAlignment = Alignment.CenterHorizontally) {
                Box(Modifier.width(22.dp).height(3.dp).clip(RoundedCornerShape(2.dp)).background(NIGHT.blue)); Spacer(Modifier.height(10.dp))
                Text("Encargos  •  Rápido  •  Seguro  •  Siempre contigo", fontSize = 11.sp, color = Color(0xFF7C8BA5))
            }
        }
        }
    }
}

private fun lerpColor(a: Color, b: Color, t: Float) = Color(a.red + (b.red - a.red) * t, a.green + (b.green - a.green) * t, a.blue + (b.blue - a.blue) * t, a.alpha + (b.alpha - a.alpha) * t)

/** Campo del acceso (.lgF): icono redondo, etiqueta pequeña, botón borrar u ojo. */
@Composable
private fun LgField(icon: androidx.compose.ui.graphics.vector.ImageVector, label: String, value: String, onChange: (String) -> Unit, password: Boolean = false, placeholder: String = "") {
    val src = remember { MutableInteractionSource() }; val focused by src.collectIsFocusedAsState()
    var show by remember { mutableStateOf(false) }
    val bd by animateColorAsState(if (focused) NIGHT.blue.copy(alpha = .8f) else NIGHT.line2, tween(200), label = "lgf")
    val sh = RoundedCornerShape(14.dp)
    BasicTextField(value, onChange, Modifier.fillMaxWidth(), singleLine = true, interactionSource = src, cursorBrush = SolidColor(NIGHT.blue),
        textStyle = TextStyle(color = NIGHT.tx, fontFamily = Manrope, fontSize = 15.sp, fontWeight = FontWeight.SemiBold),
        visualTransformation = if (password && !show) PasswordVisualTransformation() else VisualTransformation.None,
        keyboardOptions = KeyboardOptions(keyboardType = if (password) KeyboardType.Password else KeyboardType.Text),
        decorationBox = { inner ->
            Row(Modifier.fillMaxWidth().clip(sh).background(Color(30, 41, 59, 110)).border(1.dp, bd, sh).padding(horizontal = 10.dp, vertical = 7.dp), verticalAlignment = Alignment.CenterVertically) {
                Box(Modifier.size(34.dp).clip(CircleShape).background(NIGHT.btnBg).border(1.dp, NIGHT.btnLine, CircleShape), contentAlignment = Alignment.Center) { Icon(icon, null, Modifier.size(17.dp), tint = Color(0xFFCBD5E1)) }
                Spacer(Modifier.width(10.dp))
                Column(Modifier.weight(1f)) {
                    Text(label, fontSize = 11.5.sp, fontWeight = FontWeight.SemiBold, color = NIGHT.mut)
                    Box { if (value.isEmpty()) Text(placeholder, color = Color(0xFF64748B), fontSize = 15.sp); inner() }
                }
                if (password) Icon(if (show) Icons.Outlined.VisibilityOff else Icons.Outlined.Visibility, "Mostrar contraseña", Modifier.size(22.dp).clickable { show = !show }, tint = NIGHT.mut)
                else if (value.isNotEmpty()) Box(Modifier.size(22.dp).clip(CircleShape).background(NIGHT.btnBg).clickable { onChange("") }, contentAlignment = Alignment.Center) { Icon(Icons.Filled.Close, "Borrar", Modifier.size(13.dp), tint = NIGHT.mut) }
            }
        })
}

/** Botón "Iniciar sesión": azul; gira mientras verifica; verde con ✓ "Acceso" al entrar. */
@Composable
private fun GoButton(text: String, working: Boolean, granted: Boolean, onClick: () -> Unit) {
    val src = remember { MutableInteractionSource() }; val sh = RoundedCornerShape(14.dp)
    val c1 by animateColorAsState(if (granted) Color(0xFF34D399) else NIGHT.blueTop, tween(320), label = "g1")
    val c2 by animateColorAsState(if (granted) Color(0xFF059669) else NIGHT.blueDeep, tween(320), label = "g2")
    Row(Modifier.fillMaxWidth().height(48.dp).pressBounce(src).glow(if (granted) NIGHT.green else NIGHT.blue, 14.dp, 8.dp, .5f).clip(sh).background(Brush.linearGradient(listOf(c1, c2)))
        .clickable(src, null) { tapFeedback(); onClick() }.padding(horizontal = 20.dp), horizontalArrangement = Arrangement.Center, verticalAlignment = Alignment.CenterVertically) {
        if (working) { CircularProgressIndicator(Modifier.size(18.dp), color = Color.White, strokeWidth = 2.dp); Spacer(Modifier.width(10.dp)) }
        else { Icon(if (granted) Icons.Filled.Check else Icons.AutoMirrored.Filled.Login, null, Modifier.size(18.dp), tint = Color.White); Spacer(Modifier.width(8.dp)) }
        Text(text, fontWeight = FontWeight.ExtraBold, fontSize = 15.sp, color = Color.White)
    }
}
private val Color0 = androidx.compose.ui.graphics.Color.Transparent

/** Corre un trabajo en segundo plano y vuelve al hilo principal con el resultado. */
fun <T> CoroutineScope.launchIo(work: () -> T, then: (T) -> Unit) { launch { val r = withContext(Dispatchers.Default) { work() }; then(r) } }

@Composable
fun PinPad(onKey: (String) -> Unit, onDel: () -> Unit, pal: Palette = W.p, key: Int = 60, left: @Composable () -> Unit) {
    val rows = listOf(listOf("1", "2", "3"), listOf("4", "5", "6"), listOf("7", "8", "9"))
    Column(verticalArrangement = Arrangement.spacedBy(8.dp), horizontalAlignment = Alignment.CenterHorizontally) {
        rows.forEach { r -> Row(horizontalArrangement = Arrangement.spacedBy(18.dp)) { r.forEach { k -> Key(k, pal, key) { onKey(k) } } } }
        Row(horizontalArrangement = Arrangement.spacedBy(18.dp), verticalAlignment = Alignment.CenterVertically) {
            Box(Modifier.size(key.dp), contentAlignment = Alignment.Center) { left() }
            Key("0", pal, key) { onKey("0") }
            val src = remember { MutableInteractionSource() }
            Box(Modifier.pressBounce(src, .9f).size(key.dp).clip(CircleShape).clickable(src, null) { tapFeedback(); onDel() }, contentAlignment = Alignment.Center) {
                Icon(Icons.AutoMirrored.Filled.Backspace, "Borrar", Modifier.size(22.dp), tint = pal.mut)
            }
        }
    }
}
@Composable private fun Key(k: String, pal: Palette, size: Int, onClick: () -> Unit) {
    val src = remember { MutableInteractionSource() }
    Box(Modifier.pressBounce(src, .9f).size(size.dp).clip(CircleShape).background(Brush.linearGradient(listOf(pal.glassA, pal.glassB))).border(1.dp, pal.line2, CircleShape)
        .clickable(src, null) { tapFeedback(); onClick() }.semantics { contentDescription = k }, contentAlignment = Alignment.Center) {
        Text(k, fontSize = (size * .36f).sp, fontWeight = FontWeight.SemiBold, color = pal.tx)
    }
}

/** Confirmar identidad para acciones sensibles. */
@Composable
fun AuthSheet(a: AuthReq, onDone: (Boolean) -> Unit) {
    val ui = LocalUi.current; val sec = ui.app.security
    var pin by remember { mutableStateOf("") }; var pass by remember { mutableStateOf("") }; var msg by remember { mutableStateOf("") }
    val usePin = sec.pinOn()
    LaunchedEffect(pin) {
        if (usePin && pin.length == sec.pinLen()) {
            val ok = withContext(Dispatchers.Default) { sec.verifyPin(pin) }
            if (ok) { sec.ok("pin"); onDone(true) } else { msg = sec.fail("pin"); pin = "" }
        }
    }
    AlertDialog(onDismissRequest = { onDone(false) }, shape = RoundedCornerShape(22.dp), title = { Text("🔒 Confirma tu identidad", fontWeight = FontWeight.ExtraBold) },
        text = {
            Column(horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.spacedBy(10.dp)) {
                Text(a.reason, color = MaterialTheme.colorScheme.onSurfaceVariant)
                if (usePin) {
                    Row(horizontalArrangement = Arrangement.spacedBy(12.dp)) { repeat(sec.pinLen()) { i -> Box(Modifier.size(12.dp).clip(CircleShape).background(if (i < pin.length) C.blue else Color0).border(1.5.dp, C.blue, CircleShape)) } }
                    PinPad(onKey = { if (pin.length < sec.pinLen()) pin += it }, onDel = { pin = pin.dropLast(1) },
                        left = { if (sec.bioOn() && Bio.available(ui.act)) IconButton(onClick = { Bio.prompt(ui.act) { ok -> if (ok) { sec.ok("bio"); onDone(true) } } }) { Icon(Icons.Filled.Fingerprint, "Huella") } })
                } else Field(pass, { pass = it }, "Contraseña", password = true)
                if (msg.isNotEmpty()) Text(msg, color = W.rose)
            }
        },
        confirmButton = { if (!usePin) TextButton(onClick = { if (sec.checkPassword(sec.userName(), pass)) { sec.ok("password"); onDone(true) } else { msg = sec.fail("password"); pass = "" } }) { Text("Confirmar") } },
        dismissButton = { TextButton(onClick = { onDone(false) }) { Text("Cancelar") } })
}
