package com.agencia.encargos.nativo.ui

import androidx.activity.compose.BackHandler
import androidx.compose.animation.AnimatedContent
import androidx.compose.animation.animateColorAsState
import androidx.compose.animation.core.Animatable
import androidx.compose.animation.core.tween
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.togetherWith
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.asPaddingValues
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.navigationBars
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBars
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyListScope
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.Check
import androidx.compose.material.icons.filled.ErrorOutline
import androidx.compose.material.icons.filled.KeyboardArrowDown
import androidx.compose.material.icons.outlined.Payments
import androidx.compose.material.icons.automirrored.filled.KeyboardArrowRight
import androidx.compose.material.icons.outlined.CreditCard
import androidx.compose.material.icons.outlined.BarChart
import androidx.compose.material.icons.outlined.Settings
import androidx.compose.material.icons.outlined.Schedule
import androidx.compose.material.icons.outlined.TaskAlt
import androidx.compose.material.icons.outlined.Save
import androidx.compose.material.icons.outlined.Shield
import androidx.compose.material.icons.outlined.CloudSync
import androidx.compose.material.icons.outlined.DarkMode
import androidx.compose.material.icons.outlined.LightMode
import androidx.compose.material.icons.outlined.Lock
import androidx.compose.material.icons.outlined.Description
import androidx.compose.material.icons.outlined.Home
import androidx.compose.material.icons.outlined.Inventory2
import androidx.compose.material.icons.outlined.Notifications
import androidx.compose.material.icons.outlined.Person
import androidx.compose.material.icons.outlined.TrackChanges
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.SnackbarDuration
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.SnackbarResult
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.compositionLocalOf
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateListOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.runtime.staticCompositionLocalOf
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.fragment.app.FragmentActivity
import com.agencia.encargos.nativo.EncargosApp
import com.agencia.encargos.nativo.R
import com.agencia.encargos.nativo.util.Files
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

/* ======================= Navegación ======================= */
sealed class Screen(val title: String) {
    data object Home : Screen("Inicio"); data object NewOrder : Screen("Nuevo encargo"); data object Orders : Screen("Encargos"); data object Clients : Screen("Clientes")
    data object Notes : Screen("Notas"); data object Payments : Screen("Registro de pagos"); data object Pending : Screen("Pendientes"); data object Stats : Screen("Estadísticas")
    data object Attention : Screen("Centro de atención"); data object Notifs : Screen("Notificaciones"); data object Backup : Screen("Respaldo"); data object Security : Screen("Seguridad")
    data object Settings : Screen("Ajustes"); data object Sync : Screen("Sincronización"); data object SyncDiag : Screen("Diagnóstico de sincronización"); data object Completed : Screen("Encargos completados")
    data class Order(val id: String) : Screen("Ficha del encargo"); data class EditOrder(val id: String) : Screen("Editar encargo"); data class Client(val name: String) : Screen("Ficha del cliente")
    data class NewFor(val name: String, val phone: String) : Screen("Nuevo encargo")
}

class ConfirmReq(val title: String, val text: String, val ok: String, val danger: Boolean, val onOk: () -> Unit)
class AuthReq(val reason: String, val onOk: () -> Unit)

/** Servicios de interfaz al alcance de todas las pantallas. */
class Ui(val app: EncargosApp, val act: FragmentActivity, val snack: SnackbarHostState, val scope: CoroutineScope) {
    val core get() = app.core
    val stack = mutableStateListOf<Screen>(Screen.Home)
    val current get() = stack.last()
    var confirm by mutableStateOf<ConfirmReq?>(null)
    var auth by mutableStateOf<AuthReq?>(null)
    var busy by mutableStateOf<String?>(null)
    /** Se llama con cada pantalla que sale del historial: su estado guardado (desplazamiento, búsqueda, filtros) se descarta. */
    var onDrop: ((Screen) -> Unit)? = null
    private fun drop(s: Screen) { if (!stack.contains(s)) onDrop?.invoke(s) }
    fun go(s: Screen) { if (stack.last() != s) stack.add(s) }
    fun root(s: Screen) { val old = stack.toList(); stack.clear(); stack.add(s); old.forEach { drop(it) } }
    fun back(): Boolean { if (stack.size > 1) { val p = stack.removeAt(stack.size - 1); drop(p); return true }; if (stack.last() != Screen.Home) { root(Screen.Home); return true }; return false }
    fun replace(s: Screen) { val p = stack.removeAt(stack.size - 1); stack.add(s); drop(p) }
    fun toast(msg: String, undo: (() -> Unit)? = null) {
        if (msg.startsWith("✅")) com.agencia.encargos.nativo.util.Feedback.success()
        else if (Regex("^(No se pudo|Error|⚠️)|no es válid|incorrect", RegexOption.IGNORE_CASE).containsMatchIn(msg)) com.agencia.encargos.nativo.util.Feedback.error()
        scope.launch {
        snack.currentSnackbarData?.dismiss()
        val r = snack.showSnackbar(msg, actionLabel = if (undo != null) "Deshacer" else null, duration = if (undo != null) SnackbarDuration.Long else SnackbarDuration.Short)
        if (r == SnackbarResult.ActionPerformed) undo?.invoke() } }
    fun ask(title: String, text: String, ok: String = "Confirmar", danger: Boolean = false, onOk: () -> Unit) { com.agencia.encargos.nativo.util.Feedback.warning(); confirm = ConfirmReq(title, text, ok, danger, onOk) }
    /** Acciones sensibles: pide PIN o contraseña (con 2 minutos de gracia, como la app web). */
    fun requireAuth(reason: String, force: Boolean = false, onOk: () -> Unit) { if (!app.security.needsAuth(force)) onOk() else auth = AuthReq(reason, onOk) }
    fun whatsapp(phone: String?, msg: String?, who: String? = null) {
        val link = core.waLink(phone, msg); if (link == null) { toast("Este cliente no tiene teléfono registrado."); return }
        if (who != null) core.notified(who); Files.open(act, link)
    }
    /** Trabajo en segundo plano (red, archivos) con indicador de espera. */
    fun io(label: String? = null, block: suspend () -> Unit) { scope.launch { busy = label; try { withContext(Dispatchers.IO) { block() } } finally { busy = null } } }
}

val LocalUi = staticCompositionLocalOf<Ui> { error("Ui") }
val LocalVersion = compositionLocalOf { 0L }

@Composable
fun AppRoot(app: EncargosApp, act: FragmentActivity) {
    val snack = remember { SnackbarHostState() }
    val scope = rememberCoroutineScope()
    val ui = remember { Ui(app, act, snack, scope) }
    val v by app.version.collectAsState()
    val sv by app.syncVersion.collectAsState()
    val notice by app.notices.collectAsState()
    var unlocked by remember { mutableStateOf(app.unlocked) }
    LaunchedEffect(notice) { notice?.let { ui.toast(it); app.notices.value = null } }
    LaunchedEffect(Unit) { (act as? com.agencia.encargos.nativo.MainActivity)?.onLock = { unlocked = false; app.unlocked = false } }
    // iconos de la barra del sistema oscuros en modo día (dentro de la app) y claros sobre el fondo aurora
    val dayTheme = isDay()
    LaunchedEffect(unlocked, dayTheme) {
        try { val c = androidx.core.view.WindowCompat.getInsetsController(act.window, act.window.decorView); val light = dayTheme && unlocked; c.isAppearanceLightStatusBars = light; c.isAppearanceLightNavigationBars = light } catch (_: Exception) {}
    }

    CompositionLocalProvider(LocalUi provides ui, LocalVersion provides v + sv, androidx.compose.material3.LocalContentColor provides MaterialTheme.colorScheme.onBackground) {
        Box(Modifier.fillMaxSize()) {
            // el acceso siempre va sobre el fondo aurora (como la web); la app usa el fondo del tema elegido
            if (isDay() && unlocked) DayBackground() else AuroraBackground()
            // paso suave entre la pantalla de acceso y la app (lgVeilOut)
            AnimatedContent(unlocked, transitionSpec = { fadeIn(tween(420)) togetherWith fadeOut(tween(320)) }, label = "lock") { open ->
                if (!open) Box(Modifier.fillMaxSize()) { CompositionLocalProvider(androidx.compose.material3.LocalContentColor provides NIGHT.tx) { LockScreen { unlocked = true; app.unlocked = true; app.sync.syncNow(); (act as? com.agencia.encargos.nativo.MainActivity)?.afterUnlock() } } }
                else MainUi(ui, app) { unlocked = false; app.unlocked = false }
            }
            Toasts(ui)
            Dialogs(ui)
        }
    }
}

private class Tab(val s: Screen, val label: String, val icon: ImageVector)
private val TABS = listOf(Tab(Screen.Home, "Inicio", Icons.Outlined.Home), Tab(Screen.Clients, "Clientes", Icons.Outlined.Person), Tab(Screen.NewOrder, "Nuevo", Icons.Filled.Add),
    Tab(Screen.Orders, "Encargos", Icons.Outlined.Inventory2), Tab(Screen.Notes, "Notas", Icons.Outlined.Description))
fun isTabScreen(s: Screen) = TABS.any { it.s == s }

@Composable
private fun MainUi(ui: Ui, app: EncargosApp, lock: () -> Unit) {
    @Suppress("UNUSED_VARIABLE") val ver = LocalVersion.current
    BackHandler(enabled = ui.stack.size > 1 || ui.current != Screen.Home) { ui.back() }
    val cur = ui.current
    Box(Modifier.fillMaxSize()) {
        // contenido de la pantalla actual (cada pantalla anima sus tarjetas al entrar)
        // cada pantalla conserva su estado (desplazamiento, búsqueda, filtros) mientras siga en el historial:
        // al volver atrás aparece donde la dejaste; al cerrarla, su estado se descarta
        val holder = androidx.compose.runtime.saveable.rememberSaveableStateHolder()
        androidx.compose.runtime.DisposableEffect(holder) { ui.onDrop = { s -> holder.removeState(s.toString()) }; onDispose { ui.onDrop = null } }
        Box(Modifier.fillMaxSize().imePadding()) { androidx.compose.runtime.key(cur) { holder.SaveableStateProvider(cur.toString()) { ScreenHost(cur) } } }
        Header(ui, app, lock, Modifier.align(Alignment.TopCenter))
        BottomNav(ui, Modifier.align(Alignment.BottomCenter))
    }
}

/** Barra superior flotante (.header): logo, "Encargos", estado de sincronización, avisos y perfil. */
@Composable
private fun Header(ui: Ui, app: EncargosApp, lock: () -> Unit, modifier: Modifier) {
    var menu by remember { mutableStateOf(false) }
    val shape = RoundedCornerShape(20.dp)
    Row(modifier.statusBarsPadding().padding(horizontal = 14.dp, vertical = 8.dp).fillMaxWidth().clip(shape).background(W.bar).border(1.dp, W.line, shape)
        .padding(start = 14.dp, end = 10.dp, top = 9.dp, bottom = 10.dp), verticalAlignment = Alignment.CenterVertically) {
        Image(painterResource(R.mipmap.ic_launcher), null, Modifier.size(34.dp).clip(RoundedCornerShape(10.dp)).clickable { ui.root(Screen.Home) })
        Spacer(Modifier.width(9.dp))
        Column(Modifier.weight(1f)) {
            Text("Encargos", fontSize = 17.sp, fontWeight = FontWeight.ExtraBold, letterSpacing = (-0.2).sp, maxLines = 1)
            val s = app.sync.status()
            Text(if (s.kind == "off" && app.sync.cfg() == null) "Tu negocio, en orden" else s.text, fontSize = 11.sp, fontWeight = FontWeight.Medium, maxLines = 1, overflow = TextOverflow.Ellipsis,
                color = when (s.kind) { "ok" -> W.green; "error" -> W.rose; "offline", "pending" -> W.amber; else -> W.mut })
        }
        val attn = app.core.attention.badge(); val nf = app.core.notifs.unreadCount()
        HeadBtn(Icons.Outlined.TrackChanges, "Centro de atención", attn, Color(0xFFE11D48)) { ui.go(Screen.Attention) }
        Spacer(Modifier.width(6.dp))
        HeadBtn(Icons.Outlined.Notifications, "Notificaciones", nf, W.blue) { ui.go(Screen.Notifs) }
        Spacer(Modifier.width(6.dp))
        Box {
            val src = remember { MutableInteractionSource() }; val sh = RoundedCornerShape(14.dp)
            Row(Modifier.pressBounce(src, .94f).height(40.dp).clip(sh).background(W.btnBg).border(1.dp, W.btnLine, sh).clickable(src, null) { tapFeedback(); menu = true }.padding(start = 3.dp, end = 4.dp),
                verticalAlignment = Alignment.CenterVertically) {
                val ash = RoundedCornerShape(11.dp)
                Box(Modifier.size(33.dp).clip(ash).background(W.avatarBg).border(1.dp, W.blue.copy(alpha = .45f), ash), contentAlignment = Alignment.Center) {
                    Text(app.core.greetName.trim().take(1).uppercase().ifEmpty { "E" }, fontWeight = FontWeight.ExtraBold, fontSize = 15.sp, color = W.tx)
                }
                Icon(Icons.Filled.KeyboardArrowDown, "Más", Modifier.size(18.dp), tint = W.mut)
            }
            DropdownMenu(menu, { menu = false }, shape = RoundedCornerShape(16.dp), containerColor = W.menu, modifier = Modifier.width(226.dp).border(1.dp, W.line2, RoundedCornerShape(16.dp))) {
                ProfileMenu(ui, app, lock) { menu = false }
            }
        }
    }
}

/** Menú del perfil (imagen de referencia): usuario y rol → Pagos · Estadísticas · Ajustes → separador → Modo día/noche → Bloquear app.
 *  Lo que antes estaba aquí sigue accesible: Pendientes (Pagos e Inicio), Completados (Encargos y Clientes),
 *  Respaldo, Seguridad y Sincronización (Ajustes → Datos y seguridad). */
@Composable
private fun ProfileMenu(ui: Ui, app: EncargosApp, lock: () -> Unit, close: () -> Unit) {
    val core = app.core
    // cada acción cierra primero el menú: un toque = una sola navegación
    fun act(f: () -> Unit) { close(); tapFeedback(); f() }
    Column(Modifier.padding(horizontal = 6.dp, vertical = 0.dp), verticalArrangement = Arrangement.spacedBy(5.dp)) {
        // cabecera compacta: avatar, nombre y usuario; la flecha abre los ajustes (su destino actual)
        val src0 = remember { MutableInteractionSource() }
        Row(Modifier.fillMaxWidth().clip(RoundedCornerShape(12.dp)).clickable(src0, null) { act { ui.go(Screen.Settings) } }.padding(horizontal = 4.dp, vertical = 4.dp), verticalAlignment = Alignment.CenterVertically) {
            Avatar(core.greetName.ifEmpty { "E" }, C.blue, 30); Spacer(Modifier.width(8.dp))
            Column(Modifier.weight(1f)) {
                Text(core.greetName.ifEmpty { "Mi cuenta" }, fontWeight = FontWeight.ExtraBold, fontSize = 13.5.sp, color = W.tx, maxLines = 1, overflow = TextOverflow.Ellipsis)
                Text(app.security.userName().ifEmpty { "admin" }, fontSize = 11.sp, color = W.mut, maxLines = 1)
            }
            Icon(Icons.AutoMirrored.Filled.KeyboardArrowRight, "Ver perfil y ajustes", Modifier.size(16.dp), tint = W.mut)
        }
        // tres accesos en recuadros iguales
        Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(5.dp)) {
            listOf(Triple(Screen.Payments, "Pagos", Icons.Outlined.CreditCard), Triple(Screen.Stats, "Estadísticas", Icons.Outlined.BarChart), Triple(Screen.Settings, "Ajustes", Icons.Outlined.Settings)).forEach { (s, l, ic) ->
                val src = remember { MutableInteractionSource() }; val sh = RoundedCornerShape(10.dp)
                Column(Modifier.weight(1f).pressBounce(src, .94f).clip(sh).background(W.tile).border(1.dp, W.line, sh).clickable(src, null) { act { if (ui.current != s) ui.go(s) } }.padding(vertical = 7.dp),
                    horizontalAlignment = Alignment.CenterHorizontally) {
                    Icon(ic, s.title, Modifier.size(17.dp), tint = W.blueInk); Spacer(Modifier.height(3.dp))
                    Text(l, fontSize = 10.sp, fontWeight = FontWeight.Bold, color = W.tx, maxLines = 1)
                }
            }
        }
        Box(Modifier.fillMaxWidth().padding(horizontal = 4.dp).height(1.dp).background(W.line))
        // modo día / noche y bloquear: filas compactas
        val day = W.day
        @Composable fun MenuRow(icon: ImageVector, text: String, color: Color, tint: Color, bg: Color, f: () -> Unit) {
            val src = remember { MutableInteractionSource() }
            Row(Modifier.fillMaxWidth().pressBounce(src, .97f).clip(RoundedCornerShape(12.dp)).clickable(src, null) { act(f) }.padding(horizontal = 4.dp, vertical = 4.dp), verticalAlignment = Alignment.CenterVertically) {
                val ts = RoundedCornerShape(8.dp)
                Box(Modifier.size(26.dp).clip(ts).background(bg).border(1.dp, tint.copy(alpha = .25f), ts), contentAlignment = Alignment.Center) { Icon(icon, null, Modifier.size(14.dp), tint = tint) }
                Spacer(Modifier.width(9.dp))
                Text(text, fontWeight = FontWeight.SemiBold, fontSize = 12.5.sp, color = color)
            }
        }
        MenuRow(if (day) Icons.Outlined.DarkMode else Icons.Outlined.LightMode, if (day) "Modo noche" else "Modo día", W.tx, W.tx, W.tile) {
            core.ls.set("encargos_aurora_mode", if (day) "night" else "day"); ui.act.recreate() }
        MenuRow(Icons.Outlined.Lock, "Bloquear app", C.rose, C.rose, C.rose.copy(alpha = .10f)) { lock() }
    }
}

@Composable
private fun HeadBtn(icon: ImageVector, desc: String, count: Int, badge: Color, onClick: () -> Unit) {
    val src = remember { MutableInteractionSource() }; val sh = RoundedCornerShape(14.dp)
    Box(Modifier.size(40.dp)) {
        Box(Modifier.pressBounce(src, .92f).size(40.dp).clip(sh).background(W.btnBg).border(1.dp, W.btnLine, sh).clickable(src, null) { tapFeedback(); onClick() }, contentAlignment = Alignment.Center) {
            Icon(icon, desc, Modifier.size(20.dp), tint = MaterialTheme.colorScheme.onSurface)
        }
        if (count > 0) {
            // la cifra "salta" al cambiar (nPopK)
            val k = remember { Animatable(.8f) }
            LaunchedEffect(count) { k.snapTo(.8f); k.animateTo(1f, tween(300, easing = EaseBounce)) }
            Box(Modifier.align(Alignment.TopEnd).offset(5.dp, (-5).dp).graphicsLayer { scaleX = k.value; scaleY = k.value }.height(18.dp).widthIn(min = 18.dp).clip(RoundedCornerShape(9.dp)).background(badge)
                .border(2.dp, W.badgeRing, RoundedCornerShape(9.dp)).padding(horizontal = 4.dp), contentAlignment = Alignment.Center) {
                Text(if (count > 99) "99+" else "$count", fontSize = 10.sp, fontWeight = FontWeight.ExtraBold, color = Color.White, maxLines = 1)
            }
        }
    }
}

/** Barra inferior (.bNav) con el botón redondo "Nuevo" que sobresale. */
@Composable
private fun BottomNav(ui: Ui, modifier: Modifier) {
    val cur = ui.current
    Box(modifier.fillMaxWidth()) {
        Row(Modifier.fillMaxWidth().background(W.nav).border(1.dp, W.line, RoundedCornerShape(0.dp)).navigationBarsPadding().padding(horizontal = 6.dp, vertical = 6.dp),
            verticalAlignment = Alignment.Bottom) {
            TABS.forEach { t ->
                val on = cur == t.s || (t.s == Screen.NewOrder && cur is Screen.NewFor)
                val col by animateColorAsState(if (on) W.blue else W.navOff, tween(240), label = "nav")
                val src = remember { MutableInteractionSource() }
                Column(Modifier.weight(1f).pressBounce(src, .9f).clip(RoundedCornerShape(14.dp)).clickable(src, null) { tapFeedback(); ui.root(t.s) }.padding(top = 6.dp, bottom = 4.dp),
                    horizontalAlignment = Alignment.CenterHorizontally) {
                    if (t.s == Screen.NewOrder) Spacer(Modifier.height(22.dp)) else Icon(t.icon, t.label, Modifier.size(22.dp), tint = col)
                    Spacer(Modifier.height(3.dp))
                    Text(t.label, fontSize = 11.5.sp, fontWeight = FontWeight.SemiBold, color = if (t.s == Screen.NewOrder) MaterialTheme.colorScheme.onSurface else col, maxLines = 1)
                    Box(Modifier.padding(top = 3.dp).width(18.dp).height(3.dp).clip(RoundedCornerShape(2.dp)).background(if (on && t.s != Screen.NewOrder) W.blue else Color.Transparent))
                }
            }
        }
        // botón "+" (.bPlus): círculo azul con aro del color de la barra y sombra azul
        val src = remember { MutableInteractionSource() }
        Box(Modifier.align(Alignment.TopCenter).offset(y = (-16).dp).pressBounce(src, .9f).size(58.dp).glow(W.blueDeep, 10.dp, 10.dp, .7f).clip(CircleShape).background(W.nav).padding(5.dp)
            .clip(CircleShape).background(W.primary).clickable(src, null) { tapFeedback(); ui.root(Screen.NewOrder) }, contentAlignment = Alignment.Center) {
            Icon(Icons.Filled.Add, "Nuevo encargo", Modifier.size(26.dp), tint = Color.White)
        }
    }
}

@Composable fun ScreenHost(s: Screen) {
    when (s) {
        Screen.Home -> HomeScreen(); Screen.NewOrder -> NewOrderScreen(null, null); is Screen.NewFor -> NewOrderScreen(s.name, s.phone)
        Screen.Orders -> OrdersScreen(); Screen.Completed -> CompletedScreen(); Screen.Clients -> ClientsScreen(); Screen.Notes -> NotesScreen()
        Screen.Payments -> PaymentsScreen(); Screen.Pending -> PendingScreen(); Screen.Stats -> StatsScreen(); Screen.Attention -> AttentionScreen()
        Screen.Notifs -> NotifsScreen(); Screen.Backup -> BackupScreen(); Screen.Security -> SecurityScreen(); Screen.Settings -> SettingsScreen()
        Screen.Sync -> SyncScreen(); Screen.SyncDiag -> SyncDiagScreen()
        is Screen.Order -> OrderScreen(s.id); is Screen.EditOrder -> EditOrderScreen(s.id); is Screen.Client -> ClientScreen(s.name)
    }
}

const val AUTO_TITLE = "\u0000"

/** Lista de la pantalla: título grande (como los paneles de la web), márgenes y entrada escalonada de cada tarjeta. */
@Composable fun Page(title: String? = AUTO_TITLE, sub: String? = null, right: String? = null, extraBottom: androidx.compose.ui.unit.Dp = 0.dp, content: LazyListScope.() -> Unit) {
    val ui = LocalUi.current; val cur = ui.current
    val t = if (title == AUTO_TITLE) cur.title else title
    val top = WindowInsets.statusBars.asPaddingValues().calculateTopPadding() + 84.dp
    val bottom = WindowInsets.navigationBars.asPaddingValues().calculateBottomPadding() + 96.dp + extraBottom
    LazyColumn(Modifier.fillMaxSize(), contentPadding = PaddingValues(start = 16.dp, end = 16.dp, top = top, bottom = bottom), verticalArrangement = Arrangement.spacedBy(12.dp)) {
        val sc = EnterScope(this)
        if (t != null) sc.item { PageTitle(t, sub, right, back = !isTabScreen(cur)) }
        sc.content()
    }
}

@Composable fun PageTitle(title: String, sub: String? = null, right: String? = null, back: Boolean = false) {
    val ui = LocalUi.current
    // las fichas (encargo, cliente, editar) se presentan como en la referencia: título y botón "Cerrar" a la derecha
    val sheetLike = back && (ui.current is Screen.Order || ui.current is Screen.EditOrder || ui.current is Screen.Client)
    Row(Modifier.fillMaxWidth().padding(top = 4.dp), verticalAlignment = Alignment.CenterVertically) {
        if (back && !sheetLike) {
            val src = remember { MutableInteractionSource() }; val sh = RoundedCornerShape(12.dp)
            Box(Modifier.pressBounce(src, .9f).size(36.dp).clip(sh).background(W.btnBg).border(1.dp, W.btnLine, sh).clickable(src, null) { tapFeedback(); ui.back() }, contentAlignment = Alignment.Center) {
                Icon(Icons.AutoMirrored.Filled.ArrowBack, "Volver", Modifier.size(19.dp), tint = MaterialTheme.colorScheme.onSurface)
            }
            Spacer(Modifier.width(10.dp))
        }
        Column(Modifier.weight(1f)) {
            Text(title, fontSize = 24.sp, fontWeight = FontWeight.ExtraBold, letterSpacing = (-0.24).sp, maxLines = 2, overflow = TextOverflow.Ellipsis)
            if (sub != null) Text(sub, fontSize = 13.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
        if (right != null) Text(right, fontSize = 12.5.sp, fontWeight = FontWeight.SemiBold, color = MaterialTheme.colorScheme.onSurfaceVariant)
        if (sheetLike) { Spacer(Modifier.width(8.dp)); Secondary("Cerrar", Modifier.height(38.dp)) { ui.back() } }
    }
}

/** Avisos (.toast): vidrio oscuro con icono, encima de la barra inferior. */
@Composable private fun Toasts(ui: Ui) {
    Box(Modifier.fillMaxSize()) {
        SnackbarHost(ui.snack, Modifier.align(Alignment.BottomCenter).navigationBarsPadding().padding(start = 16.dp, end = 16.dp, bottom = 92.dp)) { d ->
            val msg = d.visuals.message; val ok = msg.startsWith("✅"); val bad = !ok && Regex("^(No se pudo|Error|⚠️)|no es válid|incorrect", RegexOption.IGNORE_CASE).containsMatchIn(msg)
            val sh = RoundedCornerShape(16.dp)
            Row(Modifier.fillMaxWidth().clip(sh).background(W.toast).glass(sh, W.line2).padding(horizontal = 14.dp, vertical = 12.dp), verticalAlignment = Alignment.CenterVertically) {
                if (ok || bad) {
                    Box(Modifier.size(24.dp).clip(CircleShape).background((if (ok) W.green else W.rose).copy(alpha = .18f)), contentAlignment = Alignment.Center) {
                        Icon(if (ok) Icons.Filled.Check else Icons.Filled.ErrorOutline, null, Modifier.size(15.dp), tint = if (ok) W.green else W.rose)
                    }
                    Spacer(Modifier.width(10.dp))
                }
                Text(msg.removePrefix("✅").trim(), Modifier.weight(1f), fontSize = 13.5.sp, fontWeight = FontWeight.SemiBold, color = W.tx)
                d.visuals.actionLabel?.let { l -> TextButton(onClick = { d.performAction() }) { Text(l, color = W.blue, fontWeight = FontWeight.ExtraBold) } }
            }
        }
    }
}

@Composable fun Dialogs(ui: Ui) {
    ui.confirm?.let { c ->
        AlertDialog(onDismissRequest = { ui.confirm = null }, shape = RoundedCornerShape(22.dp), title = { Text(c.title, fontWeight = FontWeight.ExtraBold) }, text = { Text(c.text, color = MaterialTheme.colorScheme.onSurfaceVariant) },
            confirmButton = { Primary(c.ok, color = if (c.danger) C.rose else C.blue) { ui.confirm = null; c.onOk() } },
            dismissButton = { Secondary("Cancelar") { ui.confirm = null } })
    }
    ui.auth?.let { a -> AuthSheet(a) { ok -> ui.auth = null; if (ok) a.onOk() } }
    ui.busy?.let { b -> AlertDialog(onDismissRequest = {}, shape = RoundedCornerShape(22.dp), confirmButton = {}, title = { Text(b, fontWeight = FontWeight.ExtraBold) },
        text = { Row(verticalAlignment = Alignment.CenterVertically) { CircularProgressIndicator(color = W.blue, strokeWidth = 3.dp, modifier = Modifier.size(26.dp)); Spacer(Modifier.width(14.dp)); Text("Un momento…") } }) }
}

@Composable fun IconLabel(icon: ImageVector, text: String, color: Color = MaterialTheme.colorScheme.onSurfaceVariant) {
    Row(verticalAlignment = Alignment.CenterVertically) { Icon(icon, null, tint = color, modifier = Modifier.padding(end = 6.dp)); Text(text, color = color, fontSize = 13.sp) }
}
