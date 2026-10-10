package com.agencia.encargos.nativo

import android.app.Application
import android.net.ConnectivityManager
import android.net.Network
import android.os.Handler
import android.os.Looper
import androidx.work.Constraints
import androidx.work.ExistingPeriodicWorkPolicy
import androidx.work.NetworkType
import androidx.work.PeriodicWorkRequestBuilder
import androidx.work.WorkManager
import androidx.work.Worker
import androidx.work.WorkerParameters
import com.agencia.encargos.core.Backup
import com.agencia.encargos.core.Encargos
import com.agencia.encargos.core.Security
import com.agencia.encargos.core.Stats
import com.agencia.encargos.core.TelegramSync
import com.agencia.encargos.nativo.data.EncargosDb
import com.agencia.encargos.nativo.data.KeystoreSecrets
import com.agencia.encargos.nativo.data.RoomStorage
import kotlinx.coroutines.flow.MutableStateFlow
import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit

/** Punto de unión: núcleo compartido + Room + Keystore + sincronización en segundo plano. */
class EncargosApp : Application() {
    lateinit var storage: RoomStorage; private set
    lateinit var secrets: KeystoreSecrets; private set
    lateinit var core: Encargos; private set
    lateinit var sync: TelegramSync; private set
    lateinit var security: Security; private set
    lateinit var backup: Backup; private set
    lateinit var stats: Stats; private set
    /** Cambia cada vez que cambian los datos: la interfaz se vuelve a dibujar. */
    val version = MutableStateFlow(0L)
    val syncVersion = MutableStateFlow(0L)
    /** Avisos breves (toasts) que pide el núcleo. */
    val notices = MutableStateFlow<String?>(null)
    private val main = Handler(Looper.getMainLooper())
    @Volatile var unlocked = false

    /** Ejecuta en el hilo principal y espera (los datos solo se tocan desde ahí). */
    fun onMain(block: () -> Unit) {
        if (Looper.myLooper() == Looper.getMainLooper()) { block(); return }
        val l = CountDownLatch(1); var err: Throwable? = null
        main.post { try { block() } catch (t: Throwable) { err = t } finally { l.countDown() } }
        l.await(); err?.let { throw it }
    }

    override fun onCreate() {
        super.onCreate()
        instance = this
        storage = RoomStorage(EncargosDb.open(this))
        secrets = KeystoreSecrets(this)
        core = Encargos(storage, "ANDROID")
        com.agencia.encargos.nativo.util.Feedback.init(this, core.ls)
        sync = TelegramSync(core, secrets, onApp = { onMain(it) })
        security = Security(core, secrets.deviceSecret())
        backup = Backup(core)
        stats = Stats(core)
        core.onChange { main.post { version.value = version.value + 1 } }
        core.onPersist = { sync.changedLocally() }
        core.notifs.onImportant = { n -> main.post { notices.value = n["title"].toString() + " · " + (n["short"] ?: n["text"]).toString() } }
        sync.onState = { main.post { syncVersion.value = syncVersion.value + 1 } }
        // al volver Internet: sincronizar
        try {
            val cm = getSystemService(ConnectivityManager::class.java)
            cm.registerDefaultNetworkCallback(object : ConnectivityManager.NetworkCallback() {
                override fun onAvailable(network: Network) { if (unlocked) sync.syncNow() }
            })
        } catch (e: Exception) {}
        // respaldo: cada 15 min con red, aunque la app esté cerrada (lo mínimo que permite Android)
        val req = PeriodicWorkRequestBuilder<SyncWorker>(15, TimeUnit.MINUTES)
            .setConstraints(Constraints.Builder().setRequiredNetworkType(NetworkType.CONNECTED).build()).build()
        WorkManager.getInstance(this).enqueueUniquePeriodicWork("encargos-sync", ExistingPeriodicWorkPolicy.KEEP, req)
    }

    companion object { lateinit var instance: EncargosApp; private set }
}

/** Sincronización periódica en segundo plano. */
class SyncWorker(ctx: android.content.Context, params: WorkerParameters) : Worker(ctx, params) {
    override fun doWork(): Result {
        val app = EncargosApp.instance
        if (!app.sync.enabled()) return Result.success()
        return try { app.onMain { app.sync.collect() }; app.sync.syncNowBlocking(); app.storage.flush(); Result.success() } catch (e: Exception) { Result.retry() }
    }
}
