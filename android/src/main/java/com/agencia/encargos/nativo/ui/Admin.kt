package com.agencia.encargos.nativo.ui

import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.material.icons.Icons
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.graphics.Color
import androidx.compose.material.icons.outlined.FormatSize
import androidx.compose.material.icons.outlined.Key
import androidx.compose.material.icons.outlined.Pin
import androidx.compose.material.icons.outlined.Fingerprint
import androidx.compose.material.icons.outlined.Login
import androidx.compose.material.icons.outlined.Schedule
import androidx.compose.material.icons.outlined.Person
import androidx.compose.material.icons.outlined.GppMaybe
import androidx.compose.material.icons.outlined.VisibilityOff
import androidx.compose.material.icons.outlined.Delete
import androidx.compose.material.icons.outlined.VerifiedUser
import androidx.compose.material.icons.outlined.Autorenew
import androidx.compose.material.icons.outlined.Event
import androidx.compose.material.icons.outlined.Lock
import androidx.compose.material.icons.outlined.TableChart
import androidx.compose.material.icons.outlined.Mail
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.background
import androidx.compose.ui.draw.clip
import androidx.compose.foundation.layout.width
import androidx.compose.material.icons.outlined.Storefront
import androidx.compose.material.icons.outlined.LocalShipping
import androidx.compose.material.icons.outlined.Sell
import androidx.compose.material.icons.outlined.Chat
import androidx.compose.material.icons.outlined.Phone
import androidx.compose.material.icons.outlined.Notifications
import androidx.compose.material.icons.outlined.VolumeUp
import androidx.compose.material.icons.outlined.WavingHand
import androidx.compose.material.icons.outlined.DarkMode
import androidx.compose.material.icons.outlined.Shield
import androidx.compose.material.icons.outlined.Save
import androidx.compose.material.icons.outlined.CloudSync
import androidx.compose.material.icons.filled.CloudSync
import androidx.compose.material.icons.filled.Download
import androidx.compose.material.icons.filled.Lock
import androidx.compose.material.icons.filled.Sync
import androidx.compose.material.icons.filled.Upload
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateMapOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalClipboardManager
import androidx.compose.ui.text.AnnotatedString
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.agencia.encargos.core.Backup
import com.agencia.encargos.core.JArr
import com.agencia.encargos.core.JObj
import com.agencia.encargos.core.Js
import com.agencia.encargos.core.TelegramSync
import com.agencia.encargos.core.str
import com.agencia.encargos.nativo.MainActivity
import com.agencia.encargos.nativo.util.Bio
import com.agencia.encargos.nativo.util.Files
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import androidx.compose.material.icons.outlined.Description
import androidx.compose.material.icons.outlined.Devices
import androidx.compose.material.icons.outlined.History
import androidx.compose.material.icons.outlined.Info
import androidx.compose.material.icons.outlined.Lightbulb
import androidx.compose.material.icons.outlined.Link
import androidx.compose.material.icons.outlined.PhoneAndroid
import androidx.compose.material.icons.outlined.SyncProblem
import androidx.compose.material.icons.outlined.Tune
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.border
import androidx.compose.material3.Icon
import androidx.compose.material.icons.outlined.PhotoCamera

/* ---------------- piezas comunes ---------------- */

@Composable
fun ToggleRow(title: String, sub: String = "", checked: Boolean, onChange: (Boolean) -> Unit) {
    Row(Modifier.fillMaxWidth().padding(vertical = 4.dp), verticalAlignment = Alignment.CenterVertically) {
        Column(Modifier.weight(1f)) {
            Text(title, fontWeight = FontWeight.Bold)
            if (sub.isNotEmpty()) Text(sub, fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
        Switch(checked, onChange)
    }
}

@Composable
fun Hint(text: String) = Text(text, fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)

private fun timeLabel(t: Any?): String = if (Js.num(t) > 0) Js.fmt(t) else "—"

/** Diálogo de contraseña (con confirmación opcional). */
@Composable
fun PasswordDialog(title: String, text: String, confirm: Boolean, ok: String, dismiss: () -> Unit, onOk: (String) -> Unit) {
    var a by remember { mutableStateOf("") }; var b by remember { mutableStateOf("") }; var err by remember { mutableStateOf("") }
    AlertDialog(onDismissRequest = dismiss, icon = Icons.Outlined.Lock, title = { Text(title) },
        text = { Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
            Text(text, fontSize = 13.sp)
            Field(a, { a = it; err = "" }, "Contraseña", password = true)
            if (confirm) Field(b, { b = it; err = "" }, "Repite la contraseña", password = true)
            if (err.isNotEmpty()) Text(err, color = C.rose, fontSize = 12.sp)
        } },
        confirmButton = { TextButton(onClick = {
            if (a.length < 6) err = "Usa al menos 6 caracteres."
            else if (confirm && a != b) err = "Las contraseñas no coinciden."
            else { onOk(a); dismiss() }
        }) { Text(ok) } },
        dismissButton = { TextButton(onClick = dismiss) { Text("Cancelar") } })
}

/* ---------------- Respaldo ---------------- */

private class PendingSave(val bytes: ByteArray, val okMsg: String, val encrypted: Boolean?)

@Composable
fun BackupScreen() {
    val ui = LocalUi.current; val app = ui.app; val bk = app.backup; val v = LocalVersion.current
    var pending by remember { mutableStateOf<PendingSave?>(null) }
    var askEnc by remember { mutableStateOf(false) }
    var encFile by remember { mutableStateOf<JObj?>(null) }
    var toImport by remember { mutableStateOf<Any?>(null) }
    var hasImport by remember { mutableStateOf(false) }

    val save = rememberLauncherForActivityResult(ActivityResultContracts.CreateDocument("application/octet-stream")) { uri ->
        val p = pending; pending = null
        if (uri != null && p != null) ui.scope.launch {
            val ok = withContext(Dispatchers.IO) { Files.writeUri(ui.act, uri, p.bytes) }
            if (ok) ui.toast(p.okMsg) else ui.toast("No se pudo guardar el archivo.")
        }
    }
    fun startSave(name: String, bytes: ByteArray, okMsg: String) { pending = PendingSave(bytes, okMsg, null); save.launch(name) }

    fun confirmImport(o: Any?) {
        val n = ((if (o is ArrayList<*>) o else (o as? JObj)?.get("data")) as? List<*>)?.size
        if (n == null) { ui.toast("El archivo de respaldo no es válido."); return }
        toImport = o; hasImport = true
        ui.ask("📥 Importar respaldo", "Se reemplazarán los encargos de este dispositivo por los $n del archivo. Si tienes sincronización activa, los cambios también llegarán a tus otros dispositivos. ¿Continuar?", "Importar", true) {
            ui.requireAuth("Confirma tu identidad para importar") {
                try { val c = bk.applyImport(toImport); ui.toast("✅ Respaldo importado: $c encargo${if (c == 1) "" else "s"}.") }
                catch (e: Exception) { ui.toast(e.message ?: "El archivo de respaldo no es válido.") }
                toImport = null; hasImport = false
            }
        }
    }

    val open = rememberLauncherForActivityResult(ActivityResultContracts.OpenDocument()) { uri ->
        if (uri != null) ui.scope.launch {
            val text = withContext(Dispatchers.IO) { Files.readUri(ui.act, uri) }
            if (text == null) { ui.toast("No se pudo leer el archivo."); return@launch }
            when (val p = bk.parse(text)) {
                is Backup.Parsed.Invalid -> ui.toast(p.why)
                is Backup.Parsed.Encrypted -> encFile = p.o
                is Backup.Parsed.Plain -> confirmImport(p.o)
            }
        }
    }

    val last = (app.core.ls.get("encargos_last_backup", "0") ?: "0").toDoubleOrNull() ?: 0.0
    val core = app.core
    fun exportJson() = ui.requireAuth("Confirma tu identidad para exportar") { val s = bk.exportJson(); startSave(bk.fileName(), s.toByteArray(Charsets.UTF_8), "✅ Respaldo guardado.") }
    Page {
        item {
            SectionCard {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    IconTile(Icons.Outlined.VerifiedUser, C.green, 48); androidx.compose.foundation.layout.Spacer(Modifier.width(14.dp))
                    Column { Text("Tus datos están guardados", fontSize = 17.sp, fontWeight = FontWeight.ExtraBold); Hint("Se guardan solos en este teléfono") }
                }
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    listOf("Encargos" to core.orders().size, "Clientes" to core.clients().size, "Pagos" to core.orders().sumOf { core.payments(it).size }, "Notas" to core.notes().size).forEach { (l, n) ->
                        Kpi(l, "$n", modifier = Modifier.weight(1f)) }
                }
            }
        }
        item {
            SectionCard("Respaldo automático", icon = Icons.Outlined.Autorenew) {
                Choice(listOf("off" to "Desactivado", "daily" to "Cada día", "weekly" to "Cada semana"), bk.autoMode) { bk.autoMode = it }
                Hint(if (bk.autoMode == "off") "No se guardarán copias automáticas." else "Al abrir la app se guarda una copia en Descargas/Encargos " + (if (bk.autoMode == "weekly") "una vez por semana." else "una vez al día.") +
                        if (bk.lastAuto > 0) " Última: " + Js.fmt(bk.lastAuto) else " Todavía no se ha hecho ningún respaldo automático.")
                Secondary("Hacer copia automática ahora", Modifier.fillMaxWidth()) {
                    val payload = bk.autoDue(true) ?: return@Secondary
                    ui.io { val ok = Files.saveToDownloads(ui.act, "respaldo_auto_" + Js.nowIso().take(10) + ".json", payload.toByteArray(), "application/json")
                        withContext(Dispatchers.Main) { if (ok) { bk.autoDone(); ui.toast("✅ Copia guardada en Descargas/Encargos.") } else ui.toast("No se pudo guardar la copia.") } }
                }
            }
        }
        item { Column(Modifier.fillMaxWidth().glass()) { NavRow(Icons.Outlined.Event, "Último respaldo", if (last > 0) Js.fmt(last) else "Aún no has hecho ningún respaldo · Te recomendamos hacer uno.", tint = C.amber, onClick = null) } }
        item { ActionCard(Icons.Filled.Download, "Respaldar datos", "Guarda un archivo con todos tus encargos, clientes, pagos, notas y gastos.", primary = true) { exportJson() } }
        item { ActionCard(Icons.Outlined.Lock, "Respaldo cifrado", "Protegido con una contraseña que tú eliges. Sin ella no se puede abrir.") { ui.requireAuth("Confirma tu identidad para exportar") { askEnc = true } } }
        item { ActionCard(Icons.Filled.Upload, "Importar respaldo", "Recupera tus datos desde un archivo .json normal o cifrado (web, Android o PC). Reemplaza los datos actuales.") { open.launch(arrayOf("application/json", "application/octet-stream", "text/plain", "*/*")) } }
        item { ActionCard(Icons.Outlined.TableChart, "Exportar a Excel (.csv)", "Una hoja con todos los encargos para abrir en Excel.") {
            ui.requireAuth("Confirma tu identidad para exportar") { startSave(bk.fileName("encargos_", ".csv"), bk.csv().toByteArray(Charsets.UTF_8), "✅ Archivo CSV guardado.") } } }
        item {
            SectionCard("Consejos", icon = Icons.Outlined.Lightbulb) {
                IconLabel(Icons.Outlined.Autorenew, "Haz un respaldo cada semana o después de muchos cambios.")
                IconLabel(Icons.Outlined.Mail, "Envía el archivo a tu correo o a la nube, por si pierdes el teléfono.")
                IconLabel(Icons.Filled.Upload, "Para recuperar todo en otro teléfono, usa Importar respaldo.")
                IconLabel(Icons.Outlined.Lock, "El respaldo cifrado solo se abre con su contraseña. Si la olvidas, no se puede recuperar.")
            }
        }
    }
    if (askEnc) PasswordDialog("🔒 Respaldo cifrado", "Elige una contraseña para este archivo.", true, "Cifrar y guardar", { askEnc = false }) { pw ->
        ui.io("Cifrando…") {
            val s = bk.exportEncrypted(pw)
            withContext(Dispatchers.Main) { startSave(bk.fileName("respaldo_encargos_cifrado_"), s.toByteArray(Charsets.UTF_8), "✅ Respaldo cifrado guardado.") }
        }
    }
    encFile?.let { f ->
        PasswordDialog("🔒 Respaldo cifrado", "Escribe la contraseña con la que se guardó este archivo.", false, "Abrir", { encFile = null }) { pw ->
            ui.io("Descifrando…") {
                val o = try { bk.decrypt(f, pw) } catch (e: Exception) { null }
                withContext(Dispatchers.Main) { if (o == null) ui.toast("Contraseña incorrecta o archivo dañado.") else confirmImport(o) }
            }
        }
    }
    @Suppress("UNUSED_VARIABLE") val u = v + if (hasImport) 1 else 0
}

/* ---------------- Seguridad ---------------- */

@Composable
fun SecurityScreen() {
    val ui = LocalUi.current; val sec = ui.app.security; val v = LocalVersion.current
    var tick by remember { mutableStateOf(0) }; fun refresh() { tick++ }
    var pinDlg by remember { mutableStateOf(false) }; var credDlg by remember { mutableStateOf(false) }; var wipeDlg by remember { mutableStateOf(false) }
    val lock = sec.lockCfg()
    val act = ui.act as? MainActivity
    var lockSheet by remember { mutableStateOf(false) }
    val bioAvail = Bio.available(ui.act)
    val score = listOf(sec.pinOn(), lock.on, sec.sensitiveOn()).count { it }
    val (lvl, lvlColor) = when (score) { 3 -> "Protección completa" to C.green; 0 -> "Sin protección" to C.rose; else -> "Protección parcial" to C.amber }
    Page(sub = "Protege la información de tu negocio.") {
        item {
            SectionCard {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    androidx.compose.foundation.layout.Box(Modifier.size(12.dp).clip(androidx.compose.foundation.shape.CircleShape).background(lvlColor)); androidx.compose.foundation.layout.Spacer(Modifier.width(10.dp))
                    Text(lvl, fontSize = 19.sp, fontWeight = FontWeight.ExtraBold)
                }
                StatRow("PIN", if (sec.pinOn()) "Activo · ${sec.pinLen()} dígitos" else "Desactivado", if (sec.pinOn()) C.green else MaterialTheme.colorScheme.onSurface)
                StatRow("Huella / biometría", if (!bioAvail) "No disponible" else if (sec.bioOn()) "Activa" else "Desactivada")
                StatRow("Bloqueo automático", if (lock.on) sec.lockLabel(lock.minutes) else "Nunca")
                StatRow("Acciones sensibles", if (sec.sensitiveOn()) "✓ Protegidas" else "Sin proteger", if (sec.sensitiveOn()) C.green else C.amber)
                StatRow("Última actividad", sec.logEntries().firstOrNull()?.let { timeLabel(it["t"]) } ?: "—")
            }
        }
        item { RowGroup("Acceso") {
            NavRow(Icons.Outlined.Key, "Bloqueo con PIN", if (sec.pinOn()) "Activo · ${sec.pinLen()} dígitos" else "Crea un PIN de 4 a 6 números", right = {
                Switch(sec.pinOn(), { on -> ui.requireAuth("Confirma tu identidad", true) { if (on) pinDlg = true else { sec.disablePin(); refresh(); ui.toast("PIN desactivado.") } } }) }, onClick = null)
            if (sec.pinOn()) { RowSep(); NavRow(Icons.Outlined.Pin, "Cambiar PIN", "Elige un PIN nuevo") { ui.requireAuth("Confirma tu identidad", true) { pinDlg = true } } }
            if (sec.pinOn() && bioAvail) { RowSep(); NavRow(Icons.Outlined.Fingerprint, "Desbloquear con huella", "Usa la huella o el rostro de este teléfono", right = {
                Switch(sec.bioOn(), { on -> if (on) Bio.prompt(ui.act) { ok -> if (ok) { sec.setBio(true); refresh(); ui.toast("✅ Huella activada.") } } else { sec.setBio(false); refresh() } }) }, onClick = null) }
            RowSep()
            NavRow(Icons.Outlined.Login, "Pedir desbloqueo al abrir", "Siempre que abres la app", right = { Switch(lock.onOpen, { sec.setLockOnOpen(it); refresh() }) }, onClick = null); RowSep()
            NavRow(Icons.Outlined.Schedule, "Bloqueo automático", if (lock.on) "Después de " + sec.lockLabel(lock.minutes).lowercase() + " fuera de la app" else "La app no se bloqueará sola", if (lock.on) sec.lockLabel(lock.minutes) else "Nunca") { lockSheet = true }; RowSep()
            NavRow(Icons.Outlined.Person, "Usuario y contraseña", "Usuario: ${sec.userName().ifEmpty { "—" }} · acceso de respaldo si olvidas el PIN") { ui.requireAuth("Confirma tu identidad", true) { credDlg = true } }
        } }
        item { RowGroup("Protección de datos") {
            NavRow(Icons.Outlined.GppMaybe, "Proteger acciones sensibles", "Eliminar, importar, exportar y cambios críticos", right = { Switch(sec.sensitiveOn(), { on ->
                if (on) { sec.setSensitive(true); refresh() } else ui.requireAuth("Confirma tu identidad", true) { sec.setSensitive(false); refresh() } }) }, onClick = null); RowSep()
            NavRow(Icons.Outlined.VisibilityOff, "Ocultar en apps recientes", "Oculta el contenido en recientes y bloquea capturas de pantalla", right = { Switch(sec.privacyOn(), { sec.setPrivacy(it); act?.applyPrivacy(); refresh() }) }, onClick = null)
        } }
        item {
            SectionCard("Actividad de seguridad", icon = Icons.Outlined.History) {
                val log = sec.logEntries().take(15)
                if (log.isEmpty()) Hint("Sin actividad registrada.")
                log.forEach { e -> Row { Text(if (Js.truthy(e["ok"])) "✅ " else "⚠️ "); Column(Modifier.weight(1f)) { Text(e.str("text"), fontSize = 13.sp); Hint(timeLabel(e["t"])) } } }
            }
        }
        item { RowGroup("Zona de peligro") {
            NavRow(Icons.Outlined.Delete, "Eliminar todos los datos", "Requiere confirmar tu identidad y escribir ELIMINAR", tint = C.rose, titleColor = C.rose) { ui.requireAuth("Confirma tu identidad", true) { wipeDlg = true } }
        } }
        item { Hint("El PIN se guarda con sal y hash iterado; las huellas nunca salen del sistema Android. Ningún respaldo incluye tu PIN ni tu contraseña.") }
    }
    if (lockSheet) Sheet("Bloqueo automático", { lockSheet = false }, Icons.Outlined.Schedule, "Cuándo se vuelve a pedir el PIN o la contraseña") {
        val opts = listOf("0" to "Al salir", "1" to "1 min", "5" to "5 min", "15" to "15 min", "30" to "30 min", "never" to "Nunca")
        val l2 = sec.lockCfg(); val sel = if (!l2.on) "never" else Js.numberToString(l2.minutes)
        Choice(opts, sel) { k -> sec.setLock(if (k == "never") null else k.toDouble()); refresh() }
        Hint(if (l2.on) "Se pedirá tu PIN o contraseña tras " + sec.lockLabel(l2.minutes).lowercase() + " fuera de la app." else "La app no se bloqueará sola.")
    }
    if (pinDlg) PinDialog({ pinDlg = false }) { pin -> sec.setPin(pin).onSuccess { ui.toast("✅ PIN guardado."); refresh() }.onFailure { ui.toast(it.message ?: "") } }
    if (credDlg) CredentialsDialog(sec.userName(), { credDlg = false }) { u, p -> sec.setCredentials(u, p).onSuccess { ui.toast("✅ Usuario y contraseña actualizados."); refresh() }.onFailure { ui.toast(it.message ?: "") } }
    if (wipeDlg) WipeDialog({ wipeDlg = false }) { reset -> sec.wipe(reset); refresh(); ui.root(Screen.Home); ui.toast("Datos eliminados de este dispositivo.") }
    @Suppress("UNUSED_VARIABLE") val u = v + tick
}

@Composable
private fun PinDialog(dismiss: () -> Unit, onOk: (String) -> Unit) {
    var a by remember { mutableStateOf("") }; var b by remember { mutableStateOf("") }; var err by remember { mutableStateOf("") }
    AlertDialog(onDismissRequest = dismiss, icon = Icons.Outlined.Pin, title = { Text("PIN") },
        text = { Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
            Hint("Elige un PIN de 4 a 6 números.")
            Field(a, { s -> a = s.filter { it.isDigit() }.take(6); err = "" }, "PIN nuevo", number = true, password = true)
            Field(b, { s -> b = s.filter { it.isDigit() }.take(6); err = "" }, "Repite el PIN", number = true, password = true)
            if (err.isNotEmpty()) Text(err, color = C.rose, fontSize = 12.sp)
        } },
        confirmButton = { TextButton(onClick = { if (a.length !in 4..6) err = "El PIN debe tener de 4 a 6 números." else if (a != b) err = "Los PIN no coinciden." else { onOk(a); dismiss() } }) { Text("Guardar PIN") } },
        dismissButton = { TextButton(onClick = dismiss) { Text("Cancelar") } })
}

@Composable
private fun CredentialsDialog(user0: String, dismiss: () -> Unit, onOk: (String, String) -> Unit) {
    var u by remember { mutableStateOf(user0) }; var a by remember { mutableStateOf("") }; var b by remember { mutableStateOf("") }; var err by remember { mutableStateOf("") }
    AlertDialog(onDismissRequest = dismiss, icon = Icons.Outlined.Person, title = { Text("Usuario y contraseña") },
        text = { Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
            Field(u, { u = it; err = "" }, "Usuario")
            Field(a, { a = it; err = "" }, "Contraseña nueva", password = true)
            Field(b, { b = it; err = "" }, "Repite la contraseña", password = true)
            if (err.isNotEmpty()) Text(err, color = C.rose, fontSize = 12.sp)
        } },
        confirmButton = { TextButton(onClick = { if (a != b) err = "Las contraseñas no coinciden." else { onOk(u, a); dismiss() } }) { Text("Guardar") } },
        dismissButton = { TextButton(onClick = dismiss) { Text("Cancelar") } })
}

@Composable
private fun WipeDialog(dismiss: () -> Unit, onOk: (Boolean) -> Unit) {
    var t by remember { mutableStateOf("") }; var reset by remember { mutableStateOf(false) }
    AlertDialog(onDismissRequest = dismiss, icon = Icons.Outlined.Delete, title = { Text("Borrar datos") },
        text = { Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
            Text("Se eliminarán los encargos, pagos, notas y gastos de ESTE dispositivo. Esta acción no se puede deshacer.", fontSize = 13.sp)
            Hint("Los respaldos de Telegram no se borran.")
            ToggleRow("Restablecer también el acceso", "Borra usuario, contraseña y PIN", reset) { reset = it }
            Field(t, { t = it }, "Escribe ELIMINAR para confirmar")
        } },
        confirmButton = { TextButton(enabled = t.trim() == "ELIMINAR", tone = C.rose, onClick = { onOk(reset); dismiss() }) { Text("Borrar todo") } },
        dismissButton = { TextButton(onClick = dismiss) { Text("Cancelar") } })
}

/* ---------------- Ajustes ---------------- */

@Composable
fun SettingsScreen() {
    val ui = LocalUi.current; val core = ui.core; val v = LocalVersion.current
    var biz by remember { mutableStateOf(core.businessName) }
    var greet by remember { mutableStateOf(core.greetName) }
    var cc by remember { mutableStateOf(core.phoneCC) }
    val rates = remember { mutableStateMapOf<String, String>().apply { core.shippingRates.forEach { (k, x) -> put(k, Js.numberToString(Js.num(x))) } } }
    val buy = remember { mutableStateMapOf<String, String>().apply { core.buyRates().forEach { (k, x) -> put(k, Js.numberToString(Js.num(x))) } } }
    var applyActive by remember { mutableStateOf(false) }
    var newName by remember { mutableStateOf("") }; var newPrice by remember { mutableStateOf("") }
    var tArr by remember { mutableStateOf(core.tpl("arrival")) }; var tRem by remember { mutableStateOf(core.tpl("reminder")) }; var tTh by remember { mutableStateOf(core.tpl("thanks")) }
    var warn by remember { mutableStateOf(core.transitWarn.toString()) }; var late by remember { mutableStateOf(core.transitLate.toString()) }
    val day = core.ls.get("encargos_aurora_mode", "") == "day"
    val keys = core.shippingRates.keys.toList()
    var sheet by remember { mutableStateOf<String?>(null) }
    val fb = com.agencia.encargos.nativo.util.Feedback
    fun rateLine(m: Map<String, Any?>) = keys.joinToString(" · ") { k -> core.shippingName(k) + " " + money(m[k]) }
    // foto de perfil: se elige de la galería, se reduce (400 px) y se guarda solo en este dispositivo
    val pickPhoto = rememberLauncherForActivityResult(ActivityResultContracts.GetContent()) { uri ->
        if (uri != null) ui.scope.launch {
            val d = withContext(Dispatchers.IO) { com.agencia.encargos.nativo.util.Images.fromUri(ui.act, uri, 400, 80) }
            if (d != null) { core.ls.set(Profile.KEY, d); Profile.photo = d; ui.toast("✅ Foto de perfil actualizada.") } else ui.toast("No se pudo leer la imagen.")
        }
    }
    Page {
        item {
            SectionCard {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Box(Modifier.clip(RoundedCornerShape(20.dp)).clickable { tapFeedback(); pickPhoto.launch("image/*") }) {
                        ProfilePic(core.greetName.ifEmpty { "E" }, 64)
                        Box(Modifier.align(Alignment.BottomEnd).size(22.dp).clip(CircleShape).background(W.blue).border(2.dp, W.menu, CircleShape), contentAlignment = Alignment.Center) {
                            Icon(Icons.Outlined.PhotoCamera, "Cambiar foto de perfil", Modifier.size(12.dp), tint = Color.White) }
                    }
                    androidx.compose.foundation.layout.Spacer(Modifier.width(14.dp))
                    Column(Modifier.weight(1f)) { Text(core.greetName.ifEmpty { "Tu nombre" }, fontSize = 19.sp, fontWeight = FontWeight.ExtraBold); Hint("Usuario: " + ui.app.security.userName().ifEmpty { "—" }) }
                }
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    Secondary(if (Profile.photo.isEmpty()) "Agregar foto" else "Cambiar foto", Modifier.weight(1f), Icons.Outlined.PhotoCamera) { pickPhoto.launch("image/*") }
                    if (Profile.photo.isNotEmpty()) Secondary("Quitar foto", Modifier.weight(1f), Icons.Outlined.Delete, C.rose) {
                        ui.ask("Quitar foto de perfil", "¿Quitar tu foto de perfil? Volverá a mostrarse tu inicial.", "Quitar", true) { core.ls.set(Profile.KEY, ""); Profile.photo = ""; ui.toast("Foto de perfil quitada.") }
                    }
                }
                Secondary("Editar nombre del saludo", Modifier.fillMaxWidth(), Icons.Outlined.WavingHand) { sheet = "greet" }
            }
        }
        item { RowGroup("Negocio y tarifas") {
            NavRow(Icons.Outlined.Storefront, "Nombre del negocio", core.businessName.ifEmpty { "Sin nombre" }) { sheet = "biz" }; RowSep()
            NavRow(Icons.Outlined.LocalShipping, "Precio por libra (lo que cobras)", rateLine(core.shippingRates)) { sheet = "rates" }; RowSep()
            NavRow(Icons.Outlined.Sell, "Costo por libra (lo que te cobran)", rateLine(core.buyRates())) { sheet = "buy" }
        } }
        item { RowGroup("WhatsApp") {
            NavRow(Icons.Outlined.Chat, "Mensajes de WhatsApp", "Llegada, recordatorio de pago y agradecimiento") { sheet = "tpl" }; RowSep()
            NavRow(Icons.Outlined.Phone, "Código de país", "Se agrega a los números cortos", "+" + core.phoneCC) { sheet = "cc" }
        } }
        item { RowGroup("Notificaciones y alertas") {
            val cfg = core.notifs.cfg(); val on = listOf("inapp", "pay", "order", "client", "system", "important").count { Js.truthy(cfg[it]) }
            NavRow(Icons.Outlined.Notifications, "Notificaciones", "Avisos y categorías · $on de 6 activas") { sheet = "notif" }; RowSep()
            NavRow(Icons.Outlined.LocalShipping, "Alertas de tránsito", "Avisar a los ${core.transitWarn} días · retrasado a los ${core.transitLate}") { sheet = "transit" }; RowSep()
            NavRow(Icons.Outlined.VolumeUp, "Sonido y vibración", (if (fb.soundOn()) "Sonido al entrar" else "Sin sonido") + " · " + (if (fb.hapticsOn()) "vibración activada" else "sin vibración")) { sheet = "fb" }
        } }
        item { RowGroup("Apariencia") {
            NavRow(Icons.Outlined.WavingHand, "Nombre del saludo", "\"¡Hola, ${core.greetName.ifEmpty { "…" }}!\"") { sheet = "greet" }; RowSep()
            NavRow(Icons.Outlined.FormatSize, "Tamaño de la interfaz", "Ajusta qué tan grande se ve todo · ${UiScale.label(UiScale.value)}", UiScale.pct(UiScale.value)) { sheet = "scale" }; RowSep()
            NavRow(Icons.Outlined.DarkMode, "Modo noche", if (!day) "Encendido · fondo oscuro" else "Apagado · colores claros", right = {
                Switch(!day, { on -> core.ls.set("encargos_aurora_mode", if (on) "night" else "day"); ui.act.recreate() }) }, onClick = null)
        } }
        item { RowGroup("Datos y seguridad") {
            val sec = ui.app.security
            NavRow(Icons.Outlined.Shield, "Seguridad", (if (sec.pinOn()) "PIN activo" else "Sin PIN") + " · bloqueo y privacidad") { ui.go(Screen.Security) }; RowSep()
            NavRow(Icons.Outlined.Save, "Respaldo e importación", "Respaldos, respaldo cifrado, importar y Excel") { ui.go(Screen.Backup) }; RowSep()
            NavRow(Icons.Outlined.CloudSync, "Sincronización (Telegram)", ui.app.sync.status().text) { ui.go(Screen.Sync) }
        } }
        item { Hint("Encargos nativo para Android · tus datos se guardan en este teléfono y, si lo activas, en tu propio bot de Telegram cifrados.") }
    }
    when (sheet) {
        "biz" -> Sheet("Nombre del negocio", { sheet = null }, Icons.Outlined.Storefront, "Aparece en recibos y mensajes") {
            InfoBox("Aparece en los recibos y en los mensajes de WhatsApp.")
            Field(biz, { biz = it }, "Nombre del negocio (aparece en recibos)")
            Primary("Guardar", Modifier.fillMaxWidth()) { core.businessName = biz.trim(); ui.toast("✅ Datos guardados."); sheet = null }
        }
        "greet" -> Sheet("Nombre del saludo", { sheet = null }, Icons.Outlined.WavingHand, "Cómo te saluda la app en Inicio") {
            Field(greet, { greet = it }, "Tu nombre (para el saludo)")
            Primary("Guardar", Modifier.fillMaxWidth()) { core.greetName = greet.trim(); ui.toast("✅ Datos guardados."); sheet = null }
        }
        "cc" -> Sheet("Código de país", { sheet = null }, Icons.Outlined.Phone, "Para abrir WhatsApp con números cortos") {
            InfoBox("Se agrega a los números de 8 cifras al abrir WhatsApp.")
            Field(cc, { s -> cc = s.filter { it.isDigit() }.take(4) }, "Código de país para WhatsApp", number = true, prefix = "+")
            Primary("Guardar", Modifier.fillMaxWidth()) { core.phoneCC = cc.ifEmpty { "53" }; ui.toast("✅ Datos guardados."); sheet = null }
        }
        "rates" -> Sheet("Precios de envío por libra", { sheet = null }, Icons.Outlined.LocalShipping, "Lo que cobras por libra en cada tipo de envío") {
            InfoBox("Configura todos tus tipos de envío. Cada tipo puede tener su propio precio por libra. Al crear un encargo, el precio se selecciona automáticamente según el envío.")
            keys.forEach { k ->
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Field(rates[k] ?: "", { rates[k] = it }, core.shippingLabel(k), Modifier.weight(1f), number = true, prefix = "$", suffix = "USD/lb")
                    if (k != "avion" && k != "barco") TextButton(onClick = { ui.ask("Eliminar tarifa", "¿Eliminar la tarifa \"${core.shippingLabel(k)}\"? Los encargos que ya la usan no cambian.", "Eliminar", true) { core.removeShippingRate(k); rates.remove(k) } }) { Text("Quitar", color = C.rose) }
                }
            }
            Text("Agregar otro tipo de envío", fontWeight = FontWeight.ExtraBold)
            Field(newName, { newName = it }, "Nombre del envío (ej. Express)")
            Field(newPrice, { newPrice = it }, "Precio · USD/lb", number = true, prefix = "$")
            Secondary("Agregar tipo", Modifier.fillMaxWidth()) {
                val p = newPrice.replace(',', '.').toDoubleOrNull()
                if (newName.trim().isEmpty() || p == null || p < 0) ui.toast("Escribe un nombre y un precio válido.")
                else { val k = core.addShippingRate(newName, p); if (k != null) { rates[k] = Js.numberToString(p); buy[k] = buy[k] ?: "0"; newName = ""; newPrice = ""; ui.toast("✅ Tarifa agregada.") } else ui.toast("No se pudo agregar la tarifa.") }
            }
            Primary("Guardar precios", Modifier.fillMaxWidth()) {
                val m = HashMap<String, Double>(); var bad = false
                core.shippingRates.keys.forEach { k -> val n = (rates[k] ?: "").replace(',', '.').toDoubleOrNull(); if (n == null || n < 0) bad = true else m[k] = n }
                if (bad) ui.toast("Revisa las tarifas: deben ser números de 0 en adelante.") else { core.setShippingRates(m); ui.toast("✅ Tarifas guardadas. Se aplican a los encargos nuevos."); sheet = null }
            }
        }
        "buy" -> Sheet("Lo que te cuesta a ti la libra", { sheet = null }, Icons.Outlined.Sell, "Se usa para calcular tu ganancia") {
            InfoBox("Se usa para calcular tu ganancia por encargo.")
            keys.forEach { k -> Field(buy[k] ?: "", { buy[k] = it }, core.shippingLabel(k), number = true, prefix = "$", suffix = "USD/lb") }
            ToggleRow("Aplicar también a encargos activos", "Actualiza el costo en los encargos de clientes que aún no terminan", applyActive) { applyActive = it }
            Primary("Guardar costos", Modifier.fillMaxWidth()) {
                val m = LinkedHashMap<String, Double>(); keys.forEach { k -> (buy[k] ?: "").replace(',', '.').toDoubleOrNull()?.let { if (it >= 0) m[k] = it } }
                core.saveBuyRates(m, applyActive); ui.toast("✅ Costos guardados."); sheet = null
            }
        }
        "tpl" -> Sheet("Mensajes de WhatsApp", { sheet = null }, Icons.Outlined.Chat, "Llegada, recordatorio de pago y agradecimiento") {
            InfoBox("Puedes usar {nombre}, {total}, {pagado}, {saldo}, {libras} y {negocio}.")
            Field(tArr, { tArr = it }, "Aviso de llegada", singleLine = false, minLines = 3)
            Field(tRem, { tRem = it }, "Recordatorio de pago", singleLine = false, minLines = 3)
            Field(tTh, { tTh = it }, "Agradecimiento", singleLine = false, minLines = 3)
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                Secondary("Restaurar", Modifier.weight(1f)) { ui.ask("Restaurar mensajes", "¿Volver a los mensajes originales?", "Restaurar") { core.resetTemplates(); tArr = core.tpl("arrival"); tRem = core.tpl("reminder"); tTh = core.tpl("thanks") } }
                Primary("Guardar mensajes", Modifier.weight(1.4f)) { core.saveTemplates(tArr, tRem, tTh); ui.toast("✅ Mensajes guardados."); sheet = null }
            }
        }
        "transit" -> Sheet("Alertas de tránsito", { sheet = null }, Icons.Outlined.LocalShipping, "Cuándo avisar de un pedido que tarda") {
            InfoBox("El centro de atención te avisa cuando un encargo lleva muchos días en tránsito.")
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                Field(warn, { s -> warn = s.filter { it.isDigit() } }, "Avisar a los (días)", Modifier.weight(1f), number = true)
                Field(late, { s -> late = s.filter { it.isDigit() } }, "Retrasado a los (días)", Modifier.weight(1f), number = true)
            }
            Primary("Guardar alertas", Modifier.fillMaxWidth()) {
                core.saveTransitAlerts(warn.toIntOrNull() ?: core.transitWarn, late.toIntOrNull() ?: core.transitLate)
                warn = core.transitWarn.toString(); late = core.transitLate.toString(); ui.toast("✅ Alertas guardadas."); sheet = null
            }
        }
        "notif" -> Sheet("Notificaciones", { sheet = null }, Icons.Outlined.Notifications, "Qué avisos quieres recibir") {
            var tick by remember { mutableStateOf(0) }
            val cfg = core.notifs.cfg(); @Suppress("UNUSED_VARIABLE") val t = tick
            listOf("inapp" to "Avisos dentro de la app", "pay" to "Pagos", "order" to "Encargos", "client" to "Clientes", "system" to "Sistema y seguridad", "important" to "Avisos importantes").forEach { (k, l) ->
                ToggleRow(l, "", Js.truthy(cfg[k])) { core.notifs.toggleCfg(k); tick++ }
            }
        }
        "scale" -> Sheet("Tamaño de la interfaz", { sheet = null }, Icons.Outlined.FormatSize, "Textos, botones y tarjetas de toda la app") {
            var pending by remember { mutableStateOf(UiScale.value) }
            InfoBox("Cambia el tamaño visual de toda la app: textos, iconos, botones, tarjetas, menús, ventanas y gráficas. No cambia tus datos ni tus importes. Se aplica al soltar el control y se recuerda al volver a abrir la app.")
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text(UiScale.label(pending), fontWeight = FontWeight.ExtraBold, fontSize = 16.sp, modifier = Modifier.weight(1f))
                Text(UiScale.pct(pending), fontWeight = FontWeight.ExtraBold, fontSize = 22.sp, color = W.blueInk)
            }
            androidx.compose.material3.Slider(pending, { pending = UiScale.clamp(it) }, valueRange = 0.80f..1.05f, steps = 4,
                onValueChangeFinished = { UiScale.value = pending; core.ls.set(UiScale.KEY, pending.toString()) },
                colors = androidx.compose.material3.SliderDefaults.colors(thumbColor = W.blue, activeTrackColor = W.blue, inactiveTrackColor = W.track, activeTickColor = Color.White.copy(alpha = .6f), inactiveTickColor = W.mut.copy(alpha = .6f)),
                modifier = Modifier.semantics { contentDescription = "Tamaño de la interfaz" })
            Row(Modifier.fillMaxWidth()) { UiScale.LEVELS.forEach { (lv, _) -> Text("${Math.round(lv * 100)}", fontSize = 11.sp, color = if (lv == pending) W.blueInk else W.mut, fontWeight = if (lv == pending) FontWeight.ExtraBold else FontWeight.Normal, textAlign = TextAlign.Center, modifier = Modifier.weight(1f)) } }
            // vista previa con el tamaño elegido (antes de aplicarlo)
            val d = androidx.compose.ui.platform.LocalDensity.current
            androidx.compose.runtime.CompositionLocalProvider(androidx.compose.ui.platform.LocalDensity provides androidx.compose.ui.unit.Density(d.density / UiScale.value * pending, d.fontScale)) {
                Column(Modifier.fillMaxWidth().glass().padding(12.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    Text("Vista previa", fontSize = 11.sp, color = W.mut)
                    Row(verticalAlignment = Alignment.CenterVertically) { Avatar("Ana", C.blue, 38); androidx.compose.foundation.layout.Spacer(Modifier.width(10.dp)); Column(Modifier.weight(1f)) { Text("Ana Gómez", fontWeight = FontWeight.ExtraBold); Text("2 encargos · Pedido", fontSize = 12.sp, color = W.mut) }; StatusChip("Pedido") }
                    Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) { Kpi("Total", "$61.00", modifier = Modifier.weight(1f)); Kpi("Saldo", "- $51.00", C.rose, modifier = Modifier.weight(1f)) }
                }
            }
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                Secondary("Restaurar 100 %", Modifier.weight(1f)) { pending = 1f; UiScale.value = 1f; core.ls.set(UiScale.KEY, "1.0") }
                Primary("Listo", Modifier.weight(1f)) { if (UiScale.value != pending) { UiScale.value = pending; core.ls.set(UiScale.KEY, pending.toString()) }; sheet = null }
            }
        }
        "fb" -> Sheet("Sonido y vibración", { sheet = null }, Icons.Outlined.VolumeUp, "Respuesta al entrar y al tocar") {
            var snd by remember { mutableStateOf(fb.soundOn()) }
            var hap by remember { mutableStateOf(fb.hapticsOn()) }
            ToggleRow("Sonido de inicio de sesión", "Un sonido corto y discreto al entrar correctamente", snd) { snd = it; fb.set("loginSoundEnabled", it); if (it) fb.loginSound() }
            ToggleRow("Vibración háptica", "Un toque muy breve al interactuar con la app", hap) { hap = it; fb.set("hapticFeedbackEnabled", it); if (it) fb.tap() }
        }
    }
    @Suppress("UNUSED_VARIABLE") val u = v
}

/* ---------------- Sincronización con Telegram ---------------- */

@Composable
fun SyncScreen() {
    val ui = LocalUi.current; val app = ui.app; val sync = app.sync; val v = LocalVersion.current
    val clip = LocalClipboardManager.current
    var tok by remember { mutableStateOf("") }; var code by remember { mutableStateOf("") }
    var mode by remember { mutableStateOf("") }
    var existing by remember { mutableStateOf(false) }; var needStart by remember { mutableStateOf(false) }
    var shownCode by remember { mutableStateOf<String?>(null) }
    val cfg = sync.cfg()
    fun result(r: TelegramSync.ConnectResult, okMsg: String) {
        when (r) {
            is TelegramSync.ConnectResult.Ok -> { tok = ""; code = ""; mode = ""; ui.toast(okMsg) }
            is TelegramSync.ConnectResult.NeedStart -> needStart = true
            is TelegramSync.ConnectResult.Existing -> existing = true
            is TelegramSync.ConnectResult.Error -> ui.toast(r.msg)
        }
    }
    Page {
        if (cfg == null) {
            item {
                SectionCard {
                    Text("Sincroniza con Telegram", fontSize = 18.sp, fontWeight = FontWeight.ExtraBold)
                    Hint("Tus datos viajan cifrados a tu propio bot de Telegram. Solo tus dispositivos tienen la llave para leerlos. En Telegram se guarda únicamente el último respaldo válido.")
                }
            }
            item {
                SectionCard("¿Es tu primer dispositivo?", icon = Icons.Outlined.PhoneAndroid) {
                    Hint("1. En Telegram, abre @BotFather y crea un bot con /newbot.\n2. Abre tu bot nuevo y envíale /start.\n3. Copia el token que te dio BotFather y pégalo aquí.")
                    Field(tok, { tok = it.trim() }, "Token del bot", password = true)
                    Primary("Conectar", Modifier.fillMaxWidth(), Icons.Filled.CloudSync, enabled = tok.isNotEmpty()) {
                        val t = tok; ui.io("Conectando con Telegram…") { val r = sync.connect(t); withContext(Dispatchers.Main) { result(r, "✅ Sincronización activada.") } }
                    }
                }
            }
            item {
                SectionCard("¿Ya lo usas en otro dispositivo?", icon = Icons.Outlined.Devices) {
                    Hint("En el otro dispositivo ve a Sincronización → \"Código para enlazar\", cópialo y pégalo aquí.")
                    Field(code, { code = it.trim() }, "Código de enlace (ENC1…)", password = true)
                    Primary("Enlazar", Modifier.fillMaxWidth(), Icons.Filled.Sync, enabled = code.isNotEmpty()) {
                        val c = code; ui.io("Enlazando y descargando…") { val r = sync.link(c); withContext(Dispatchers.Main) { result(r, "✅ Dispositivo enlazado.") } }
                    }
                }
            }
        } else {
            val st = sync.status(); val m = sync.metaInfo(); val paused = Js.truthy(cfg["paused"])
            item {
                SectionCard {
                    Text(st.text, fontSize = 18.sp, fontWeight = FontWeight.ExtraBold, color = when (st.kind) { "ok" -> C.green; "error" -> C.rose; "offline", "pending" -> C.amber; else -> MaterialTheme.colorScheme.onSurface })
                    if (sync.lastErr.isNotEmpty() && st.kind != "ok") Text(sync.lastErr, color = C.rose, fontSize = 13.sp)
                    Line("Cambios pendientes", sync.pending().toString())
                    Line("Última sincronización", timeLabel(m["lastSync"]))
                    Line("Versión del respaldo", Js.numberToString(Js.num(m["localVersion"])).let { if (it == "0") "—" else it })
                    Line("Este dispositivo", sync.deviceId())
                }
            }
            item {
                Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    Primary("Sincronizar ahora", Modifier.fillMaxWidth(), Icons.Filled.Sync, enabled = !paused) { ui.io { val ok = sync.syncNowBlocking(); withContext(Dispatchers.Main) { ui.toast(if (ok) "✅ Sincronizado." else sync.lastErr.ifEmpty { "Sincronización en curso…" }) } } }
                    Secondary("Respaldar ahora en Telegram", Modifier.fillMaxWidth(), Icons.Filled.CloudSync) { ui.io("Subiendo respaldo…") { val ok = sync.backupNow(); withContext(Dispatchers.Main) { ui.toast(if (ok) "✅ Respaldo actualizado en Telegram." else sync.lastErr.ifEmpty { "No se pudo respaldar." }) } } }
                    Secondary("Crear respaldo local", Modifier.fillMaxWidth(), Icons.Filled.Download) { ui.go(Screen.Backup) }
                }
            }
            item {
                SectionCard("Enlazar otro dispositivo", icon = Icons.Outlined.Link) {
                    Hint("El código permite a otro teléfono o PC leer y escribir tus datos. Trátalo como una contraseña: no lo compartas con nadie.")
                    Secondary("Mostrar código para enlazar", Modifier.fillMaxWidth(), Icons.Filled.Lock) { ui.requireAuth("Confirma tu identidad para ver el código", true) { shownCode = sync.linkCode() } }
                }
            }
            item {
                SectionCard("Opciones", icon = Icons.Outlined.Tune) {
                    ToggleRow("Pausar sincronización", "Los cambios se guardan aquí y se envían al reanudar", paused) { sync.pause(it) }
                    Secondary("Restaurar desde Telegram", Modifier.fillMaxWidth()) {
                        ui.ask("Restaurar desde Telegram", "Se reemplazarán los datos de este dispositivo por el último respaldo de Telegram. Primero se comprueba que el respaldo esté completo. ¿Continuar?", "Restaurar", true) {
                            ui.requireAuth("Confirma tu identidad", true) {
                                ui.io("Restaurando…") { val r = sync.restore(); withContext(Dispatchers.Main) { r.onSuccess { ui.toast("✅ Datos restaurados desde Telegram.") }.onFailure { ui.toast("No se pudo restaurar: " + (it.message ?: "")) } } }
                            }
                        }
                    }
                    Secondary("Diagnóstico", Modifier.fillMaxWidth()) { ui.go(Screen.SyncDiag) }
                    Secondary("Desconectar este dispositivo", Modifier.fillMaxWidth(), color = C.rose) {
                        ui.ask("Desconectar", "Este dispositivo dejará de sincronizar. Tus datos locales y el respaldo de Telegram no se borran.", "Desconectar", true) { sync.disconnect(); ui.toast("Sincronización desconectada.") }
                    }
                }
            }
        }
    }
    if (needStart) AlertDialog(onDismissRequest = { needStart = false }, icon = Icons.Outlined.Info, title = { Text("Falta un paso") },
        text = { Text("Abre tu bot en Telegram y envíale /start. Después vuelve aquí y toca \"Conectar\" otra vez.") },
        confirmButton = { TextButton(onClick = { needStart = false }) { Text("Entendido") } })
    if (existing) AlertDialog(onDismissRequest = { existing = false }, icon = Icons.Outlined.CloudSync, title = { Text("Este bot ya tiene datos") },
        text = { Text("Ya hay un respaldo de Encargos en este bot. Para ver esos datos aquí, usa el código de enlace de tu otro dispositivo.\n\nSi ya no tienes ese dispositivo, puedes empezar de nuevo con este bot: se creará un respaldo nuevo con los datos de este teléfono. El respaldo anterior no se borra.") },
        confirmButton = { TextButton(onClick = { existing = false; tok = "" }) { Text("Usaré el código") } },
        dismissButton = { TextButton(onClick = { existing = false; ui.io("Creando sincronización…") { val ok = sync.freshStart(); withContext(Dispatchers.Main) { tok = ""; ui.toast(if (ok) "✅ Sincronización nueva activada." else sync.lastErr.ifEmpty { "No se pudo activar." }) } } }) { Text("Empezar de nuevo", color = C.amber) } })
    shownCode?.let { c ->
        AlertDialog(onDismissRequest = { shownCode = null }, icon = Icons.Outlined.Key, title = { Text("Código para enlazar") },
            text = { Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                Hint("Cópialo y pégalo en el otro dispositivo. No lo envíes por chats ni lo publiques.")
                Text(c, fontFamily = FontFamily.Monospace, fontSize = 11.sp, maxLines = 6)
            } },
            confirmButton = { TextButton(onClick = { clip.setText(AnnotatedString(c)); ui.toast("Código copiado."); shownCode = null }) { Text("Copiar") } },
            dismissButton = { TextButton(onClick = { shownCode = null }) { Text("Cerrar") } })
    }
    @Suppress("UNUSED_VARIABLE") val u = v
}

@Composable
fun SyncDiagScreen() {
    val ui = LocalUi.current; val sync = ui.app.sync; val v = LocalVersion.current
    val m = sync.metaInfo(); val cfg = sync.cfg()
    Page {
        item {
            SectionCard("Estado", icon = Icons.Outlined.Info) {
                Line("Conectado", if (cfg == null) "No" else if (Js.truthy(cfg["paused"])) "En pausa" else "Sí")
                Line("Estado", sync.state); Line("Pendientes", sync.pending().toString())
                Line("Dispositivo", sync.deviceId())
                Line("Base de datos", cfg?.str("db")?.take(8)?.plus("…") ?: "—")
                Line("Versión local", Js.numberToString(Js.num(m["localVersion"])))
                Line("Última sincronización", timeLabel(m["lastSync"]))
                Line("Mensaje del respaldo", Js.numberToString(Js.num(m["backupMsg"])).let { if (it == "0") "—" else it })
                Line("Mensaje fijado", Js.numberToString(Js.num(m["manifestMsg"])).let { if (it == "0") "—" else it })
                Line("Limpiezas pendientes", ((m["cleanup"] as? JArr)?.size ?: 0).toString())
                Line("Publicación en curso", if (m["inflight"] != null) "Sí (se recupera sola)" else "No")
                if (sync.lastErr.isNotEmpty()) Text("Último error: " + sync.lastErr, color = C.rose, fontSize = 12.sp)
                Hint("El token y la llave de cifrado nunca se muestran aquí.")
            }
        }
        val conf = sync.conflicts()
        if (conf.isNotEmpty()) item {
            SectionCard("Conflictos resueltos (${conf.size})", icon = Icons.Outlined.SyncProblem) {
                Hint("Cuando dos dispositivos cambian lo mismo, gana el cambio más reciente. Los pagos nunca se pierden.")
                conf.take(20).forEach { c -> Text("• " + c.str("entity") + " · ganó " + (if (c.str("kept") == "local") "este dispositivo" else "otro dispositivo (" + c.str("remoteDevice") + ")") + " · " + timeLabel(c["t"]), fontSize = 12.sp) }
            }
        }
        item {
            SectionCard("Registro", icon = Icons.Outlined.Description) {
                val log = sync.logEntries().take(60)
                if (log.isEmpty()) Hint("Sin registros.")
                log.forEach { e -> Column(Modifier.padding(vertical = 2.dp)) {
                    Text(when (e.str("kind")) { "err" -> "⚠️ "; "in" -> "⬇️ "; "out" -> "⬆️ "; else -> "• " } + e.str("text"), fontSize = 12.sp, color = if (e.str("kind") == "err") C.rose else MaterialTheme.colorScheme.onSurface)
                    Hint(timeLabel(e["t"]) + " · " + e.str("dev"))
                } }
            }
        }
        item { Secondary("Copiar diagnóstico (sin secretos)", Modifier.fillMaxWidth()) {
            val txt = buildString {
                append("Encargos nativo · diagnóstico\n"); append("Estado: ${sync.state} · pendientes ${sync.pending()}\n"); append("Versión local: ${Js.numberToString(Js.num(m["localVersion"]))}\n")
                append("Última sync: ${timeLabel(m["lastSync"])}\n"); if (sync.lastErr.isNotEmpty()) append("Error: ${sync.lastErr}\n")
                sync.logEntries().take(30).forEach { append(timeLabel(it["t"]) + " " + it.str("kind") + " " + it.str("text") + "\n") }
            }
            Files.shareText(ui.act, txt)
        } }
    }
    @Suppress("UNUSED_VARIABLE") val u = v
}
