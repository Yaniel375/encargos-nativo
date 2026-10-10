package com.agencia.encargos.nativo.util

import android.content.Context
import android.media.AudioAttributes
import android.media.AudioFormat
import android.media.AudioTrack
import android.os.Build
import android.os.SystemClock
import android.os.VibrationEffect
import android.os.Vibrator
import android.os.VibratorManager
import com.agencia.encargos.core.JObj
import com.agencia.encargos.core.Js
import com.agencia.encargos.core.LS
import com.agencia.encargos.core.jobj
import kotlin.math.PI
import kotlin.math.exp
import kotlin.math.ln
import kotlin.math.sin

/**
 * Sonido de inicio de sesión y vibración háptica — igual que la app web:
 *  - sonido: tres notas suaves (Mi5, Si5, Mi6) con armónicos, solo al entrar correctamente;
 *  - vibración: tap 10 ms, éxito 12 ms, aviso 14 ms, error 18 ms; una sola por interacción.
 *  Preferencias en 'encargos_feedback' {loginSoundEnabled, hapticFeedbackEnabled} (misma clave que la web).
 */
object Feedback {
    private const val KEY = "encargos_feedback"
    private var ctx: Context? = null
    private var ls: LS? = null
    private var lastAt = 0L

    fun init(context: Context, store: LS) { ctx = context.applicationContext; ls = store }

    private fun prefs(): JObj { val p = jobj("loginSoundEnabled" to true, "hapticFeedbackEnabled" to true); (ls?.json(KEY) as? JObj)?.forEach { (k, v) -> p[k] = v }; return p }
    fun soundOn() = Js.truthy(prefs()["loginSoundEnabled"])
    fun hapticsOn() = Js.truthy(prefs()["hapticFeedbackEnabled"])
    fun set(key: String, on: Boolean) { val p = prefs(); p[key] = on; ls?.put(KEY, p) }

    private fun vibrator(): Vibrator? = ctx?.let { c ->
        if (Build.VERSION.SDK_INT >= 31) (c.getSystemService(Context.VIBRATOR_MANAGER_SERVICE) as? VibratorManager)?.defaultVibrator
        else @Suppress("DEPRECATION") (c.getSystemService(Context.VIBRATOR_SERVICE) as? Vibrator)
    }
    private fun vib(ms: Long, force: Boolean = false) {
        try {
            if (!hapticsOn()) return
            val now = SystemClock.elapsedRealtime(); if (!force && now - lastAt < 180) return; lastAt = now
            val v = vibrator() ?: return; if (!v.hasVibrator()) return
            v.vibrate(VibrationEffect.createOneShot(ms, VibrationEffect.DEFAULT_AMPLITUDE))
        } catch (_: Exception) {}
    }
    fun tap() = vib(10)
    fun success() = vib(12, true)
    fun warning() = vib(14, true)
    fun error() = vib(18, true)

    /** Mismo acorde que la web (WebAudio): 659.25 Hz, 987.77 Hz (+90 ms) y 1318.5 Hz (+170 ms). */
    fun loginSound() {
        if (!soundOn()) return
        Thread {
            try {
                val rate = 44100; val total = (rate * 0.78).toInt(); val buf = FloatArray(total)
                fun note(f: Double, start: Double, dur: Double, g: Double) {
                    listOf(Triple(1.0, g, 1.0), Triple(2.01, g * .16, .6), Triple(3.0, g * .05, .6)).forEach { (m, gg, k) ->
                        val d = dur * k; val s0 = ((start + 0.02) * rate).toInt(); val n = ((d + 0.05) * rate).toInt()
                        for (i in 0 until n) {
                            val t = i.toDouble() / rate; val idx = s0 + i; if (idx >= total) break
                            // rampa exponencial: 0.0001 → gg en 12 ms, luego → 0.0001 al final de la nota
                            val env = if (t < 0.012) 0.0001 * exp(ln(gg / 0.0001) * t / 0.012)
                                      else if (t < d) gg * exp(ln(0.0001 / gg) * (t - 0.012) / (d - 0.012)) else 0.0
                            buf[idx] += (sin(2 * PI * f * m * t) * env).toFloat()
                        }
                    }
                }
                note(659.25, 0.0, 0.32, 0.28); note(987.77, 0.09, 0.42, 0.24); note(1318.5, 0.17, 0.5, 0.10)
                val pcm = ShortArray(total) { i -> (buf[i] * 0.35f * 32767f).toInt().coerceIn(-32767, 32767).toShort() }
                val track = AudioTrack.Builder()
                    .setAudioAttributes(AudioAttributes.Builder().setUsage(AudioAttributes.USAGE_ASSISTANCE_SONIFICATION).setContentType(AudioAttributes.CONTENT_TYPE_SONIFICATION).build())
                    .setAudioFormat(AudioFormat.Builder().setEncoding(AudioFormat.ENCODING_PCM_16BIT).setSampleRate(rate).setChannelMask(AudioFormat.CHANNEL_OUT_MONO).build())
                    .setTransferMode(AudioTrack.MODE_STATIC).setBufferSizeInBytes(pcm.size * 2).build()
                track.write(pcm, 0, pcm.size); track.play()
                Thread.sleep(1200); track.release() // libera el audio: nada queda sonando
            } catch (_: Exception) {}
        }.start()
    }
}
