package com.agencia.encargos.nativo.util

import android.graphics.Bitmap
import android.graphics.Canvas
import android.graphics.Color
import android.graphics.Paint
import android.util.Base64
import com.agencia.encargos.core.Encargos
import com.agencia.encargos.core.str
import java.io.ByteArrayOutputStream

/** SOLO para la copia de PRUEBA automática (BuildConfig.CAPTURE_TEST): crea fotos de muestra con un marco rojo y
 *  marcas en las cuatro esquinas para comprobar en las capturas que las fotos se ven completas (sin recortes).
 *  Nunca se ejecuta en la app normal ni toca datos reales: solo el encargo y la nota de prueba del emulador. */
object TestPhotos {
    private fun make(w: Int, h: Int, label: String): String {
        val bm = Bitmap.createBitmap(w, h, Bitmap.Config.ARGB_8888); val c = Canvas(bm)
        c.drawColor(Color.rgb(226, 232, 240))
        val p = Paint(Paint.ANTI_ALIAS_FLAG)
        p.color = Color.rgb(37, 99, 235); c.drawRect(w * .12f, h * .12f, w * .88f, h * .88f, p)
        p.color = Color.rgb(225, 29, 72); p.style = Paint.Style.STROKE; p.strokeWidth = 18f; c.drawRect(9f, 9f, w - 9f, h - 9f, p)
        p.style = Paint.Style.FILL; val m = minOf(w, h) * .12f
        listOf(0f to 0f, w - m to 0f, 0f to h - m, w - m to h - m).forEach { (x, y) -> c.drawRect(x, y, x + m, y + m, p) }
        p.color = Color.WHITE; p.textSize = minOf(w, h) * .09f; p.textAlign = Paint.Align.CENTER
        c.drawText(label, w / 2f, h / 2f, p); c.drawText("${w}×${h}", w / 2f, h / 2f + p.textSize * 1.3f, p)
        val out = ByteArrayOutputStream(); bm.compress(Bitmap.CompressFormat.JPEG, 70, out)
        return "data:image/jpeg;base64," + Base64.encodeToString(out.toByteArray(), Base64.NO_WRAP)
    }
    fun seed(core: Encargos) {
        // foto de perfil de prueba (solo en la copia de prueba automática)
        if (core.ls.get("encargos_profile_photo", "").isNullOrEmpty()) core.ls.set("encargos_profile_photo", make(400, 400, "PERFIL"))
        val x = core.orders().firstOrNull { it.str("name") == "Cliente Prueba" } ?: return
        if (core.orderPhotos(x).isNotEmpty()) return
        val v = make(600, 1200, "VERTICAL"); val h = make(1200, 600, "HORIZONTAL"); val s = make(800, 800, "CUADRADA")
        core.addOrderPhoto(x["id"], v); core.addOrderPhoto(x["id"], h)
        core.saveNote(null, "Nota con fotos", "Fotos de prueba del recorrido", 0.0, "blue", true, listOf(v, h, s))
        // artículos en venta de prueba (solo en el emulador): uno con varias fotos, uno sin costo y uno agotado
        core.saveSaleItem(null, "Tenis de prueba", "Talla 40 · nuevos", 25.0, 15.0, 4, listOf(s, v, h)).getOrNull()?.let { core.sellSaleItem(it.str("id"), 1, 24.0) }
        core.saveSaleItem(null, "Gorra de prueba", "", 8.0, 0.0, 2, listOf(make(700, 700, "GORRA")))
        core.saveSaleItem(null, "Bolso de prueba", "Agotado", 30.0, 18.0, 0, emptyList())
    }
}
