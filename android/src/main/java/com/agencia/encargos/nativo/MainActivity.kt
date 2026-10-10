package com.agencia.encargos.nativo

import android.os.Bundle
import android.os.Handler
import android.os.Looper
import android.view.WindowManager
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.fragment.app.FragmentActivity
import com.agencia.encargos.nativo.ui.AppRoot
import com.agencia.encargos.nativo.ui.EncargosTheme
import com.agencia.encargos.nativo.util.Files

class MainActivity : FragmentActivity() {
    private val app get() = application as EncargosApp
    private var hiddenAt = 0L
    /** Lo asigna la interfaz: vuelve a la pantalla de bloqueo. */
    var onLock: (() -> Unit)? = null
    private val h = Handler(Looper.getMainLooper())
    private val tick = object : Runnable { override fun run() { if (app.unlocked && app.sync.enabled()) app.sync.syncNow(); h.postDelayed(this, 45000) } }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge(androidx.activity.SystemBarStyle.dark(android.graphics.Color.TRANSPARENT), androidx.activity.SystemBarStyle.dark(android.graphics.Color.TRANSPARENT))
        applyPrivacy()
        // "Pedir contraseña al abrir" desactivado y con acceso ya creado: entra directo
        if (!app.unlocked && app.security.hasCredentials() && !app.security.lockCfg().onOpen) app.unlocked = true
        setContent { EncargosTheme(day = app.core.ls.get("encargos_aurora_mode", "") == "day") { AppRoot(app, this) } }
    }
    /** Privacidad en recientes: oculta el contenido en la vista de apps recientes y bloquea capturas. */
    fun applyPrivacy() { if (app.security.privacyOn() && !BuildConfig.CAPTURE_TEST) window.addFlags(WindowManager.LayoutParams.FLAG_SECURE) else window.clearFlags(WindowManager.LayoutParams.FLAG_SECURE) }

    fun afterUnlock() {
        // respaldo automático en Descargas/Encargos (diario o semanal)
        app.backup.autoDue()?.let { payload ->
            if (Files.saveToDownloads(this, "respaldo_auto_" + com.agencia.encargos.core.Js.nowIso().take(10) + ".json", payload.toByteArray(), "application/json")) app.backup.autoDone()
        }
        app.core.notifs.sysEvents()
    }

    override fun onResume() {
        super.onResume()
        if (hiddenAt > 0 && app.unlocked && app.security.hasCredentials() && app.security.shouldLockAfter(System.currentTimeMillis() - hiddenAt)) {
            app.security.log(true, "Bloqueo al volver a la app"); app.unlocked = false; onLock?.invoke()
        }
        hiddenAt = 0
        if (app.unlocked) app.sync.syncNow()
        h.removeCallbacks(tick); h.postDelayed(tick, 45000)
    }
    override fun onPause() { super.onPause(); hiddenAt = System.currentTimeMillis(); h.removeCallbacks(tick); Thread { app.storage.flush() }.start() }
}
