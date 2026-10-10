package com.agencia.encargos.nativo.data

import android.content.Context
import android.content.SharedPreferences
import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import android.util.Base64
import androidx.room.Dao
import androidx.room.Database
import androidx.room.Entity
import androidx.room.PrimaryKey
import androidx.room.Query
import androidx.room.Room
import androidx.room.RoomDatabase
import androidx.room.Upsert
import com.agencia.encargos.core.Secrets
import com.agencia.encargos.core.Storage
import java.security.KeyStore
import java.util.concurrent.ConcurrentHashMap
import java.util.concurrent.CountDownLatch
import java.util.concurrent.Executors
import javax.crypto.Cipher
import javax.crypto.KeyGenerator
import javax.crypto.SecretKey
import javax.crypto.spec.GCMParameterSpec

/* ======================= Room: base de datos local ======================= */

/** Cada clave del modelo de Encargos (encargos_v3_data, encargos_notes, …) es una fila. */
@Entity(tableName = "kv")
data class KvEntry(@PrimaryKey val key: String, val value: String, val updated: Long)

@Dao
interface KvDao {
    @Query("SELECT * FROM kv") fun all(): List<KvEntry>
    @Upsert fun put(e: KvEntry)
    @Query("DELETE FROM kv WHERE `key` = :key") fun delete(key: String)
}

@Database(entities = [KvEntry::class], version = 1, exportSchema = true)
abstract class EncargosDb : RoomDatabase() {
    abstract fun kv(): KvDao
    companion object {
        fun open(ctx: Context): EncargosDb = Room.databaseBuilder(ctx, EncargosDb::class.java, "encargos.db")
            .allowMainThreadQueries() // solo para la lectura inicial; las escrituras van en su propio hilo
            .setJournalMode(JournalMode.WRITE_AHEAD_LOGGING)
            .build()
    }
}

/**
 * Storage del núcleo sobre Room: lee todo al abrir y escribe en orden en un hilo propio.
 * [flush] espera a que todo esté en disco (se llama al salir de la app).
 */
class RoomStorage(private val db: EncargosDb) : Storage {
    private val cache = ConcurrentHashMap<String, String>()
    private val writer = Executors.newSingleThreadExecutor { Thread(it, "encargos-db") }
    init { db.kv().all().forEach { cache[it.key] = it.value } }
    override fun get(key: String): String? = cache[key]
    override fun set(key: String, value: String) { cache[key] = value; writer.execute { db.kv().put(KvEntry(key, value, System.currentTimeMillis())) } }
    override fun remove(key: String) { cache.remove(key); writer.execute { db.kv().delete(key) } }
    override fun keys(): List<String> = cache.keys.toList()
    fun flush() { val l = CountDownLatch(1); writer.execute { l.countDown() }; l.await() }
}

/* ======================= Secretos: Android Keystore ======================= */

/**
 * Token de Telegram, clave de cifrado y secreto del dispositivo: cifrados con una clave AES
 * que vive en el Android Keystore y nunca sale del teléfono. Fuera de él solo hay texto cifrado.
 */
class KeystoreSecrets(ctx: Context) : Secrets {
    private val prefs: SharedPreferences = ctx.getSharedPreferences("encargos_secure", Context.MODE_PRIVATE)
    private val alias = "encargos_secrets_v1"

    private fun key(): SecretKey {
        val ks = KeyStore.getInstance("AndroidKeyStore").apply { load(null) }
        (ks.getEntry(alias, null) as? KeyStore.SecretKeyEntry)?.let { return it.secretKey }
        val g = KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, "AndroidKeyStore")
        g.init(KeyGenParameterSpec.Builder(alias, KeyProperties.PURPOSE_ENCRYPT or KeyProperties.PURPOSE_DECRYPT)
            .setBlockModes(KeyProperties.BLOCK_MODE_GCM).setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE).setKeySize(256).build())
        return g.generateKey()
    }
    @Synchronized override fun get(name: String): String? {
        val v = prefs.getString(name, null) ?: return null
        return try {
            val raw = Base64.decode(v, Base64.NO_WRAP)
            val c = Cipher.getInstance("AES/GCM/NoPadding"); c.init(Cipher.DECRYPT_MODE, key(), GCMParameterSpec(128, raw, 0, 12))
            String(c.doFinal(raw, 12, raw.size - 12), Charsets.UTF_8)
        } catch (e: Exception) { null }
    }
    @Synchronized override fun set(name: String, value: String) {
        val c = Cipher.getInstance("AES/GCM/NoPadding"); c.init(Cipher.ENCRYPT_MODE, key())
        val out = c.iv + c.doFinal(value.toByteArray(Charsets.UTF_8))
        prefs.edit().putString(name, Base64.encodeToString(out, Base64.NO_WRAP)).commit()
    }
    @Synchronized override fun remove(name: String) { prefs.edit().remove(name).commit() }

    /** Secreto de 32 bytes de este dispositivo (se usa para el PIN y la contraseña). */
    fun deviceSecret(): ByteArray {
        get("device_secret")?.let { return Base64.decode(it, Base64.NO_WRAP) }
        val b = ByteArray(32).also { java.security.SecureRandom().nextBytes(it) }
        set("device_secret", Base64.encodeToString(b, Base64.NO_WRAP)); return b
    }
}
