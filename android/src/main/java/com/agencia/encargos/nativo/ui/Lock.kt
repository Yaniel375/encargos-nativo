package com.agencia.encargos.nativo.ui

import androidx.compose.foundation.Image
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

/** Pantalla de entrada: crear acceso (primera vez), contraseña, PIN y biometría. */
@Composable
fun LockScreen(onUnlocked: () -> Unit) {
    val ui = LocalUi.current; val sec = ui.app.security
    var setup by remember { mutableStateOf(!sec.hasCredentials()) }
    var usePass by remember { mutableStateOf(!sec.pinOn()) }
    var user by remember { mutableStateOf(sec.userName()) }; var pass by remember { mutableStateOf("") }; var pass2 by remember { mutableStateOf("") }
    var msg by remember { mutableStateOf("") }; var working by remember { mutableStateOf(false) }
    var pin by remember { mutableStateOf("") }
    val greet = ui.core.greetName

    fun done(method: String) { sec.ok(method); msg = ""; onUnlocked() }
    LaunchedEffect(Unit) { if (!setup && sec.bioOn() && Bio.available(ui.act)) Bio.prompt(ui.act) { ok -> if (ok) done("bio") } }
    LaunchedEffect(pin) {
        if (!setup && !usePass && pin.length == sec.pinLen()) {
            val w = sec.waitMs(); if (w > 0) { msg = "Demasiados intentos. Espera ${if (w >= 60000) "${w / 60000} min" else "${w / 1000} s"}."; pin = ""; return@LaunchedEffect }
            working = true; val ok = withContext(Dispatchers.Default) { sec.verifyPin(pin) }; working = false
            if (ok) done("pin") else { msg = sec.fail("pin"); pin = "" }
        }
    }

    Box(Modifier.fillMaxSize().background(MaterialTheme.colorScheme.background).imePadding(), contentAlignment = Alignment.Center) {
        Column(Modifier.fillMaxWidth().verticalScroll(rememberScrollState()).padding(24.dp), horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.spacedBy(12.dp)) {
            Image(painterResource(R.mipmap.ic_launcher), null, Modifier.size(84.dp).clip(RoundedCornerShape(24.dp)))
            Text("Encargos", fontSize = 26.sp, fontWeight = FontWeight.ExtraBold, color = MaterialTheme.colorScheme.onBackground)
            Text(if (setup) "Crea tu acceso" else "Hola, $greet", color = MaterialTheme.colorScheme.onSurfaceVariant)
            if (setup) {
                Text("Elige un usuario y una contraseña para proteger tus datos. No hay contraseña por defecto.", textAlign = TextAlign.Center, fontSize = 13.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                Field(user, { user = it }, "Usuario"); Field(pass, { pass = it }, "Contraseña (mínimo 6)", password = true); Field(pass2, { pass2 = it }, "Repite la contraseña", password = true)
                Primary(if (working) "Guardando…" else "Crear acceso", Modifier.fillMaxWidth(), enabled = !working) {
                    if (pass != pass2) { msg = "Las contraseñas no coinciden."; return@Primary }
                    working = true
                    ui.scope.launchIo({ sec.setCredentials(user, pass) }) { r -> working = false; r.onSuccess { setup = false; usePass = true; done("password") }.onFailure { msg = it.message ?: "" } }
                }
            } else if (!usePass) {
                Text("Escribe tu PIN", color = MaterialTheme.colorScheme.onSurfaceVariant)
                Row(horizontalArrangement = Arrangement.spacedBy(14.dp)) { repeat(sec.pinLen()) { i -> Box(Modifier.size(14.dp).clip(CircleShape).background(if (i < pin.length) C.blue else Color0).border(1.5.dp, C.blue, CircleShape)) } }
                PinPad(onKey = { k -> if (!working && pin.length < sec.pinLen()) pin += k }, onDel = { pin = pin.dropLast(1) },
                    left = { if (sec.bioOn() && Bio.available(ui.act)) IconButton(onClick = { Bio.prompt(ui.act) { ok -> if (ok) done("bio") } }) { Icon(Icons.Filled.Fingerprint, "Huella", Modifier.size(30.dp)) } else Spacer(Modifier.size(48.dp)) })
                TextButton(onClick = { usePass = true; msg = "" }) { Icon(Icons.Filled.Key, null); Spacer(Modifier.width(6.dp)); Text("Entrar con usuario y contraseña") }
            } else {
                Field(user, { user = it }, "Usuario"); Field(pass, { pass = it }, "Contraseña", password = true)
                Primary(if (working) "Verificando…" else "Iniciar sesión", Modifier.fillMaxWidth(), enabled = !working) {
                    val w = sec.waitMs(); if (w > 0) { msg = "Demasiados intentos. Espera ${if (w >= 60000) "${w / 60000} min" else "${w / 1000} s"}."; return@Primary }
                    if (user.isBlank() || pass.isEmpty()) { msg = "Completa usuario y contraseña."; return@Primary }
                    working = true
                    ui.scope.launchIo({ sec.checkPassword(user, pass) }) { ok -> working = false; pass = ""; if (ok) done("password") else msg = sec.fail("password") }
                }
                if (sec.pinOn()) TextButton(onClick = { usePass = false; msg = "" }) { Text("Entrar con PIN") }
            }
            if (msg.isNotEmpty()) Text(msg, color = C.rose, textAlign = TextAlign.Center)
            Spacer(Modifier.height(8.dp))
            Text("Encargos · Rápido · Seguro · Siempre contigo", fontSize = 11.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
    }
}
private val Color0 = androidx.compose.ui.graphics.Color.Transparent

/** Corre un trabajo en segundo plano y vuelve al hilo principal con el resultado. */
fun <T> CoroutineScope.launchIo(work: () -> T, then: (T) -> Unit) { launch { val r = withContext(Dispatchers.Default) { work() }; then(r) } }

@Composable
fun PinPad(onKey: (String) -> Unit, onDel: () -> Unit, left: @Composable () -> Unit) {
    val rows = listOf(listOf("1", "2", "3"), listOf("4", "5", "6"), listOf("7", "8", "9"))
    Column(verticalArrangement = Arrangement.spacedBy(10.dp), horizontalAlignment = Alignment.CenterHorizontally) {
        rows.forEach { r -> Row(horizontalArrangement = Arrangement.spacedBy(18.dp)) { r.forEach { k -> Key(k) { onKey(k) } } } }
        Row(horizontalArrangement = Arrangement.spacedBy(18.dp), verticalAlignment = Alignment.CenterVertically) {
            Box(Modifier.size(68.dp), contentAlignment = Alignment.Center) { left() }
            Key("0") { onKey("0") }
            Box(Modifier.size(68.dp).clip(CircleShape).clickable { onDel() }, contentAlignment = Alignment.Center) { Icon(Icons.Filled.Backspace, "Borrar") }
        }
    }
}
@Composable private fun Key(k: String, onClick: () -> Unit) {
    Box(Modifier.size(68.dp).clip(CircleShape).background(MaterialTheme.colorScheme.surfaceVariant).border(1.dp, MaterialTheme.colorScheme.outline, CircleShape).clickable { onClick() }, contentAlignment = Alignment.Center) {
        Text(k, fontSize = 24.sp, fontWeight = FontWeight.Bold)
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
    AlertDialog(onDismissRequest = { onDone(false) }, title = { Text("Confirma tu identidad") },
        text = {
            Column(horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.spacedBy(10.dp)) {
                Text(a.reason, color = MaterialTheme.colorScheme.onSurfaceVariant)
                if (usePin) {
                    Row(horizontalArrangement = Arrangement.spacedBy(12.dp)) { repeat(sec.pinLen()) { i -> Box(Modifier.size(12.dp).clip(CircleShape).background(if (i < pin.length) C.blue else Color0).border(1.5.dp, C.blue, CircleShape)) } }
                    PinPad(onKey = { if (pin.length < sec.pinLen()) pin += it }, onDel = { pin = pin.dropLast(1) },
                        left = { if (sec.bioOn() && Bio.available(ui.act)) IconButton(onClick = { Bio.prompt(ui.act) { ok -> if (ok) { sec.ok("bio"); onDone(true) } } }) { Icon(Icons.Filled.Fingerprint, "Huella") } })
                } else Field(pass, { pass = it }, "Contraseña", password = true)
                if (msg.isNotEmpty()) Text(msg, color = C.rose)
            }
        },
        confirmButton = { if (!usePin) TextButton(onClick = { if (sec.checkPassword(sec.userName(), pass)) { sec.ok("password"); onDone(true) } else { msg = sec.fail("password"); pass = "" } }) { Text("Confirmar") } },
        dismissButton = { TextButton(onClick = { onDone(false) }) { Text("Cancelar") } })
}
