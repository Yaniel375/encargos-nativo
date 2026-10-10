package com.agencia.encargos.nativo.ui

import androidx.activity.compose.BackHandler
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyListScope
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.Warning
import androidx.compose.material.icons.filled.Home
import androidx.compose.material.icons.filled.Inventory2
import androidx.compose.material.icons.filled.MoreVert
import androidx.compose.material.icons.filled.Notifications
import androidx.compose.material.icons.filled.People
import androidx.compose.material.icons.filled.StickyNote2
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Badge
import androidx.compose.material3.BadgedBox
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.NavigationBar
import androidx.compose.material3.NavigationBarItem
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarDuration
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.SnackbarResult
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
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
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.fragment.app.FragmentActivity
import com.agencia.encargos.nativo.EncargosApp
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
    fun go(s: Screen) { if (stack.last() != s) stack.add(s) }
    fun root(s: Screen) { stack.clear(); stack.add(s) }
    fun back(): Boolean { if (stack.size > 1) { stack.removeAt(stack.size - 1); return true }; if (stack.last() != Screen.Home) { root(Screen.Home); return true }; return false }
    fun replace(s: Screen) { stack.removeAt(stack.size - 1); stack.add(s) }
    fun toast(msg: String, undo: (() -> Unit)? = null) { scope.launch {
        snack.currentSnackbarData?.dismiss()
        val r = snack.showSnackbar(msg, actionLabel = if (undo != null) "Deshacer" else null, duration = if (undo != null) SnackbarDuration.Long else SnackbarDuration.Short)
        if (r == SnackbarResult.ActionPerformed) undo?.invoke() } }
    fun ask(title: String, text: String, ok: String = "Confirmar", danger: Boolean = false, onOk: () -> Unit) { confirm = ConfirmReq(title, text, ok, danger, onOk) }
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

@OptIn(ExperimentalMaterial3Api::class)
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

    CompositionLocalProvider(LocalUi provides ui, LocalVersion provides v + sv) {
        if (!unlocked) { LockScreen { unlocked = true; app.unlocked = true; app.sync.syncNow(); (act as? com.agencia.encargos.nativo.MainActivity)?.afterUnlock() }; Dialogs(ui) }
        else MainUi(ui, app) { unlocked = false; app.unlocked = false }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun MainUi(ui: Ui, app: EncargosApp, lock: () -> Unit) {
        @Suppress("UNUSED_VARIABLE") val ver = LocalVersion.current
        val snack = ui.snack
        BackHandler(enabled = ui.stack.size > 1 || ui.current != Screen.Home) { ui.back() }
        val cur = ui.current
        val tabs = listOf(Triple(Screen.Home, "Inicio", Icons.Filled.Home), Triple(Screen.Clients, "Clientes", Icons.Filled.People), Triple(Screen.NewOrder, "Nuevo", Icons.Filled.Add),
            Triple(Screen.Orders, "Encargos", Icons.Filled.Inventory2), Triple(Screen.Notes, "Notas", Icons.Filled.StickyNote2))
        val isTab = tabs.any { it.first == cur }
        var menu by remember { mutableStateOf(false) }
        Scaffold(
            containerColor = MaterialTheme.colorScheme.background,
            snackbarHost = { SnackbarHost(snack) },
            topBar = {
                TopAppBar(colors = TopAppBarDefaults.topAppBarColors(containerColor = MaterialTheme.colorScheme.background),
                    navigationIcon = { if (!isTab) IconButton(onClick = { ui.back() }) { Icon(Icons.AutoMirrored.Filled.ArrowBack, "Volver") } },
                    title = {
                        Column {
                            Text(if (isTab && cur == Screen.Home) "Encargos" else cur.title, fontWeight = FontWeight.ExtraBold, fontSize = 19.sp)
                            if (cur == Screen.Home) { val s = app.sync.status(); Text(s.text, fontSize = 11.sp, color = when (s.kind) { "ok" -> C.green; "error" -> C.rose; "offline", "pending" -> C.amber; else -> MaterialTheme.colorScheme.onSurfaceVariant }) }
                        }
                    },
                    actions = {
                        val attn = app.core.attention.badge(); val nf = app.core.notifs.unreadCount()
                        IconButton(onClick = { ui.go(Screen.Attention) }) { BadgedBox(badge = { if (attn > 0) Badge { Text(if (attn > 99) "99+" else "$attn") } }) { Icon(Icons.Filled.Warning, "Centro de atención") } }
                        IconButton(onClick = { ui.go(Screen.Notifs) }) { BadgedBox(badge = { if (nf > 0) Badge { Text(if (nf > 99) "99+" else "$nf") } }) { Icon(Icons.Filled.Notifications, "Notificaciones") } }
                        Box {
                            IconButton(onClick = { menu = true }) { Icon(Icons.Filled.MoreVert, "Más") }
                            DropdownMenu(menu, { menu = false }) {
                                listOf(Screen.Payments, Screen.Pending, Screen.Stats, Screen.Completed, Screen.Backup, Screen.Security, Screen.Sync, Screen.Settings).forEach { s ->
                                    DropdownMenuItem(text = { Text(s.title) }, onClick = { menu = false; ui.go(s) }) }
                                DropdownMenuItem(text = { Text("Bloquear ahora", color = C.rose) }, onClick = { menu = false; lock() })
                            }
                        }
                    })
            },
            bottomBar = {
                NavigationBar(containerColor = MaterialTheme.colorScheme.surface) {
                    tabs.forEach { (s, l, ic) -> NavigationBarItem(selected = cur == s, onClick = { ui.root(s) }, icon = { Icon(ic, l) }, label = { Text(l, fontSize = 11.sp) }) }
                }
            }
        ) { pad ->
            Box(Modifier.padding(pad).fillMaxSize()) { ScreenHost(cur) }
        }
        Dialogs(ui)
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

/** Lista con desplazamiento y márgenes comunes para todas las pantallas. */
@Composable fun Page(content: LazyListScope.() -> Unit) {
    LazyColumn(Modifier.fillMaxSize(), contentPadding = PaddingValues(horizontal = 14.dp, vertical = 10.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) { content(); item { Spacer(Modifier.height(24.dp)) } }
}

@Composable fun Dialogs(ui: Ui) {
    ui.confirm?.let { c ->
        AlertDialog(onDismissRequest = { ui.confirm = null }, title = { Text(c.title) }, text = { Text(c.text) },
            confirmButton = { TextButton(onClick = { ui.confirm = null; c.onOk() }) { Text(c.ok, color = if (c.danger) C.rose else C.blue, fontWeight = FontWeight.Bold) } },
            dismissButton = { TextButton(onClick = { ui.confirm = null }) { Text("Cancelar") } })
    }
    ui.auth?.let { a -> AuthSheet(a) { ok -> ui.auth = null; if (ok) a.onOk() } }
    ui.busy?.let { b -> AlertDialog(onDismissRequest = {}, confirmButton = {}, title = { Text(b) }, text = { Row { androidx.compose.material3.CircularProgressIndicator(); Spacer(Modifier.padding(8.dp)); Text("Un momento…") } }) }
}

@Composable fun IconLabel(icon: ImageVector, text: String, color: Color = MaterialTheme.colorScheme.onSurfaceVariant) {
    Row(verticalAlignment = androidx.compose.ui.Alignment.CenterVertically) { Icon(icon, null, tint = color, modifier = Modifier.padding(end = 6.dp)); Text(text, color = color, fontSize = 13.sp) }
}
