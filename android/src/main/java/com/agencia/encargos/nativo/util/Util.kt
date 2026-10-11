package com.agencia.encargos.nativo.util

import android.content.ContentValues
import android.content.Context
import android.content.Intent
import android.graphics.Bitmap
import android.graphics.BitmapFactory
import android.graphics.Canvas
import android.graphics.Color
import android.graphics.Paint
import android.graphics.RectF
import android.graphics.Typeface
import android.graphics.pdf.PdfDocument
import android.net.Uri
import android.os.Build
import android.provider.MediaStore
import android.util.Base64
import androidx.biometric.BiometricManager
import androidx.biometric.BiometricPrompt
import androidx.core.content.ContextCompat
import androidx.core.content.FileProvider
import androidx.fragment.app.FragmentActivity
import androidx.print.PrintHelper
import com.agencia.encargos.core.Encargos
import com.agencia.encargos.core.JArr
import com.agencia.encargos.core.JObj
import com.agencia.encargos.core.Js
import com.agencia.encargos.core.objs
import com.agencia.encargos.core.str
import java.io.ByteArrayOutputStream
import java.io.File
import kotlin.math.max
import kotlin.math.min

/* ======================= Imágenes (las fotos se guardan como data URL, igual que la app web) ======================= */
object Images {
    private val cache = android.util.LruCache<String, Bitmap>(40)
    fun decode(dataUrl: String?, maxDim: Int = 900): Bitmap? {
        if (dataUrl.isNullOrEmpty()) return null
        val key = dataUrl.length.toString() + ":" + dataUrl.hashCode() + ":" + maxDim
        cache.get(key)?.let { return it }
        return try {
            val b64 = dataUrl.substringAfter("base64,", dataUrl)
            val bytes = Base64.decode(b64, Base64.DEFAULT)
            val o = BitmapFactory.Options().apply { inJustDecodeBounds = true }; BitmapFactory.decodeByteArray(bytes, 0, bytes.size, o)
            var s = 1; while (max(o.outWidth, o.outHeight) / s > maxDim * 2) s *= 2
            BitmapFactory.decodeByteArray(bytes, 0, bytes.size, BitmapFactory.Options().apply { inSampleSize = s })?.also { cache.put(key, it) }
        } catch (e: Exception) { null }
    }
    /** compressPhotoFile: lado mayor 1000 px (900 en notas), JPEG calidad 60 (55 en notas). */
    fun fromUri(ctx: Context, uri: Uri, maxDim: Int = 1000, quality: Int = 60): String? = try {
        val bytes = ctx.contentResolver.openInputStream(uri)!!.use { it.readBytes() }
        val o = BitmapFactory.Options().apply { inJustDecodeBounds = true }; BitmapFactory.decodeByteArray(bytes, 0, bytes.size, o)
        var s = 1; while (max(o.outWidth, o.outHeight) / s > maxDim * 2) s *= 2
        var bm = BitmapFactory.decodeByteArray(bytes, 0, bytes.size, BitmapFactory.Options().apply { inSampleSize = s })!!
        val big = max(bm.width, bm.height)
        if (big > maxDim) { val k = maxDim.toFloat() / big; bm = Bitmap.createScaledBitmap(bm, (bm.width * k).toInt(), (bm.height * k).toInt(), true) }
        val out = ByteArrayOutputStream(); bm.compress(Bitmap.CompressFormat.JPEG, quality, out)
        "data:image/jpeg;base64," + Base64.encodeToString(out.toByteArray(), Base64.NO_WRAP)
    } catch (e: Exception) { null }
}

/* ======================= Archivos, compartir y enlaces ======================= */
object Files {
    /** Guarda en Descargas/Encargos (sin pedir permisos en Android 10+). */
    fun saveToDownloads(ctx: Context, name: String, bytes: ByteArray, mime: String, sub: String = "Encargos"): Boolean = try {
        if (Build.VERSION.SDK_INT >= 29) {
            val v = ContentValues().apply { put(MediaStore.Downloads.DISPLAY_NAME, name); put(MediaStore.Downloads.MIME_TYPE, mime); put(MediaStore.Downloads.RELATIVE_PATH, "Download/$sub") }
            val uri = ctx.contentResolver.insert(MediaStore.Downloads.EXTERNAL_CONTENT_URI, v)!!
            ctx.contentResolver.openOutputStream(uri)!!.use { it.write(bytes) }; true
        } else {
            @Suppress("DEPRECATION") val dir = File(android.os.Environment.getExternalStoragePublicDirectory(android.os.Environment.DIRECTORY_DOWNLOADS), sub).apply { mkdirs() }
            File(dir, name).writeBytes(bytes); true
        }
    } catch (e: Exception) { false }

    fun writeUri(ctx: Context, uri: Uri, bytes: ByteArray): Boolean = try { ctx.contentResolver.openOutputStream(uri, "wt")!!.use { it.write(bytes) }; true } catch (e: Exception) { false }
    fun readUri(ctx: Context, uri: Uri): String? = try { ctx.contentResolver.openInputStream(uri)!!.use { it.readBytes().toString(Charsets.UTF_8) } } catch (e: Exception) { null }

    private fun cacheFile(ctx: Context, name: String, bytes: ByteArray): Uri {
        val dir = File(ctx.cacheDir, "compartir").apply { mkdirs() }; dir.listFiles()?.forEach { it.delete() }
        val f = File(dir, name); f.writeBytes(bytes)
        return FileProvider.getUriForFile(ctx, ctx.packageName + ".files", f)
    }
    /** Envía un archivo (recibo) por WhatsApp al chat del cliente; si no hay WhatsApp, abre el selector. */
    fun shareFile(ctx: Context, name: String, bytes: ByteArray, mime: String, text: String, waNumber: String?) {
        val uri = cacheFile(ctx, name, bytes)
        val i = Intent(Intent.ACTION_SEND).apply { type = mime; putExtra(Intent.EXTRA_STREAM, uri); putExtra(Intent.EXTRA_TEXT, text); addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
            if (!waNumber.isNullOrEmpty()) putExtra("jid", "$waNumber@s.whatsapp.net") }
        for (pkg in listOf("com.whatsapp", "com.whatsapp.w4b")) {
            try { ctx.startActivity(Intent(i).setPackage(pkg).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)); return } catch (e: Exception) {}
        }
        ctx.startActivity(Intent.createChooser(i, "Enviar recibo").addFlags(Intent.FLAG_ACTIVITY_NEW_TASK))
    }
    fun shareText(ctx: Context, text: String) { ctx.startActivity(Intent.createChooser(Intent(Intent.ACTION_SEND).apply { type = "text/plain"; putExtra(Intent.EXTRA_TEXT, text) }, "Compartir").addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)) }
    fun open(ctx: Context, url: String) { try { ctx.startActivity(Intent(Intent.ACTION_VIEW, Uri.parse(url)).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)) } catch (e: Exception) {} }
}

/* ======================= Recibo (misma información que el recibo de la app web) ======================= */
object Receipt {
    private const val W = 1080; private const val P = 56; private const val IW = W - P * 2; private const val ROW = 74
    fun bitmap(app: Encargos, x: JObj): Bitmap {
        val pays = (x["paymentHistory"] as? JArr)?.objs()?.sortedBy { Js.parseDate(it["date"]) } ?: emptyList()
        val detailH = 110 + ROW * 3 + 20; val payRows = max(1, pays.size) + 1; val payH = 110 + ROW * payRows + 20
        val H = 56 + 170 + 330 + 40 + 200 + 40 + detailH + 40 + payH + 140
        val bm = Bitmap.createBitmap(W, H, Bitmap.Config.ARGB_8888); val c = Canvas(bm)
        val bg = Color.parseColor("#0B1220"); val card = Color.parseColor("#131C2E"); val line = Color.parseColor("#26324A")
        val tx = Color.parseColor("#F1F5F9"); val mut = Color.parseColor("#94A3B8"); val blue = Color.parseColor("#3B82F6"); val green = Color.parseColor("#34D399"); val rose = Color.parseColor("#FB7185")
        c.drawColor(bg)
        val p = Paint(Paint.ANTI_ALIAS_FLAG)
        fun rr(x0: Float, y0: Float, w: Float, h: Float, r: Float, fill: Int, stroke: Int? = null) { p.style = Paint.Style.FILL; p.color = fill; c.drawRoundRect(RectF(x0, y0, x0 + w, y0 + h), r, r, p); if (stroke != null) { p.style = Paint.Style.STROKE; p.strokeWidth = 2f; p.color = stroke; c.drawRoundRect(RectF(x0, y0, x0 + w, y0 + h), r, r, p) }; p.style = Paint.Style.FILL }
        fun text(t: String, x0: Float, y0: Float, size: Float, col: Int, bold: Boolean = false, align: Paint.Align = Paint.Align.LEFT, maxW: Float = 0f) {
            p.textSize = size; p.color = col; p.typeface = Typeface.create(Typeface.DEFAULT, if (bold) Typeface.BOLD else Typeface.NORMAL); p.textAlign = align
            var s = t; if (maxW > 0) while (s.length > 1 && p.measureText(s) > maxW) s = s.dropLast(2) + "…"
            c.drawText(s, x0, y0, p) }
        val b = Js.num(x["balance"]); val tot = Js.num(x["total"]); val paid = Js.num(x["paid"]); val st = x.str("status").ifEmpty { "Pedido" }
        val stc = when (st) { "En tránsito" -> Color.parseColor("#94A3B8"); "Recibido" -> Color.parseColor("#7DD3FC"); "Entregado" -> green; "Cancelado" -> mut; else -> blue }
        var y = 56f
        rr(P.toFloat(), y, 88f, 88f, 26f, blue); text("e", P + 44f, y + 64, 54f, Color.WHITE, true, Paint.Align.CENTER)
        text("Recibo de compra", P + 112f, y + 48, 50f, tx, true); text(Js.fmt(x["date"]) + if (app.businessName.isNotBlank()) "  ·  " + app.businessName else "", P + 112f, y + 90, 30f, mut, maxW = IW - 112f)
        y += 170
        rr(P.toFloat(), y, IW.toFloat(), 300f, 40f, card, line)
        rr(P + 36f, y + 36, 108f, 108f, 32f, Color.parseColor("#1E293B"), blue)
        text(x.str("name").take(1).uppercase(), P + 90f, y + 110, 54f, tx, true, Paint.Align.CENTER)
        p.textSize = 28f; val chipW = min(300f, p.measureText(st) + 64)
        rr(P + IW - 36 - chipW, y + 40, chipW, 56f, 28f, Color.argb(40, Color.red(stc), Color.green(stc), Color.blue(stc)))
        text(st, P + IW - 36 - chipW + 32, y + 79, 28f, stc, true)
        text(x.str("name").ifEmpty { "Cliente" }, P + 170f, y + 86, 46f, tx, true, maxW = IW - 220 - chipW)
        text(x.str("phone").ifEmpty { "Sin teléfono" }, P + 170f, y + 130, 30f, mut)
        text("Pagado ${Js.money(paid)} de ${Js.money(tot)}", P + 36f, y + 218, 30f, tx, true)
        text(if (b > 0.005) "Debe ${Js.money(b)}" else if (b < -0.005) "A favor ${Js.money(-b)}" else "Pagado completo", P + IW - 36f, y + 218, 30f, if (b > 0.005) rose else green, true, Paint.Align.RIGHT)
        val k = if (tot > 0) (paid / tot).coerceIn(0.0, 1.0) else 1.0
        rr(P + 36f, y + 244, IW - 72f, 18f, 9f, line); if (k > 0) rr(P + 36f, y + 244, max(18f, ((IW - 72) * k).toFloat()), 18f, 9f, green)
        y += 370
        val bw = (IW - 40) / 3f
        listOf(Triple("Total", Js.money(tot), tx), Triple("Pagado", Js.money(paid), green), Triple(if (b < -0.005) "A favor" else "Saldo", if (b > 0.005) "-" + Js.money(b) else if (b < -0.005) "+" + Js.money(-b) else "$0.00", if (b > 0.005) rose else green))
            .forEachIndexed { i, (l, v, col) -> val bx = P + i * (bw + 20); rr(bx, y, bw, 170f, 28f, card, line); text(l, bx + 30, y + 62, 28f, mut); text(v, bx + 30, y + 124, 44f, col, true, maxW = bw - 60) }
        y += 240
        rr(P.toFloat(), y, IW.toFloat(), detailH.toFloat(), 36f, card, line); text("Detalle del pedido", P + 36f, y + 70, 34f, tx, true)
        var ry = y + 110
        fun row(l: String, v: String, bold: Boolean = false, lc: Int = tx, vc: Int = tx, last: Boolean = false) {
            text(l, P + 36f, ry + 46, 32f, lc, bold, maxW = IW - 300f); text(v, P + IW - 36f, ry + 46, 32f, vc, true, Paint.Align.RIGHT)
            if (!last) { p.color = line; p.strokeWidth = 2f; c.drawLine(P + 36f, ry + ROW, P + IW - 36f, ry + ROW, p) }; ry += ROW }
        row("Costo del pedido", Js.money(x["cost"]))
        row(app.shippingLabel(x.str("shippingType").ifEmpty { "avion" }).replace(Regex("^\\S+\\s"), "Envío ") + " · " + Js.toFixed(Js.num(x["pounds"]), 2) + " lb × " + Js.money(x["lbCost"]), Js.money(x["shipping"]))
        row("Total", Js.money(tot), bold = true, last = true)
        y += detailH + 40
        rr(P.toFloat(), y, IW.toFloat(), payH.toFloat(), 36f, card, line); text("Pagos", P + 36f, y + 70, 34f, tx, true); ry = y + 110
        if (pays.isNotEmpty()) pays.forEach { pp -> row(Js.fmt(pp["date"]) + " · " + pp.str("method").ifEmpty { "Pago" }, "+" + Js.money(pp["amount"]), lc = mut, vc = green) }
        else row("Sin pagos registrados", "—", lc = mut, vc = mut)
        row("Total pagado", Js.money(paid), bold = true, vc = green, last = true)
        y += payH
        text("Gracias por tu confianza", W / 2f, y + 80, 30f, mut, align = Paint.Align.CENTER)
        return bm
    }
    fun png(bm: Bitmap): ByteArray { val o = ByteArrayOutputStream(); bm.compress(Bitmap.CompressFormat.PNG, 100, o); return o.toByteArray() }
    fun pdf(bm: Bitmap): ByteArray {
        val doc = PdfDocument(); val pw = 595; val ph = (bm.height * 595f / bm.width).toInt()
        val page = doc.startPage(PdfDocument.PageInfo.Builder(pw, ph, 1).create())
        page.canvas.drawBitmap(Bitmap.createScaledBitmap(bm, pw, ph, true), 0f, 0f, null); doc.finishPage(page)
        val o = ByteArrayOutputStream(); doc.writeTo(o); doc.close(); return o.toByteArray()
    }
    fun fileBase(x: JObj) = "Recibo_" + x.str("name").replace(Regex("[^\\p{L}\\p{N}]+"), "_").trim('_').ifEmpty { "cliente" } + "_" + Js.str(x["orderNo"])
    fun print(act: FragmentActivity, x: JObj, bm: Bitmap) { PrintHelper(act).apply { scaleMode = PrintHelper.SCALE_MODE_FIT }.printBitmap(fileBase(x), bm) }
}

/* ======================= Biometría del sistema ======================= */
object Bio {
    fun available(ctx: Context) = BiometricManager.from(ctx).canAuthenticate(BiometricManager.Authenticators.BIOMETRIC_WEAK) == BiometricManager.BIOMETRIC_SUCCESS
    fun prompt(act: FragmentActivity, onResult: (Boolean) -> Unit) {
        val p = BiometricPrompt(act, ContextCompat.getMainExecutor(act), object : BiometricPrompt.AuthenticationCallback() {
            override fun onAuthenticationSucceeded(result: BiometricPrompt.AuthenticationResult) = onResult(true)
            override fun onAuthenticationError(errorCode: Int, errString: CharSequence) = onResult(false)
        })
        p.authenticate(BiometricPrompt.PromptInfo.Builder().setTitle("Encargos").setSubtitle("Desbloquea para continuar").setNegativeButtonText("Usar PIN")
            .setAllowedAuthenticators(BiometricManager.Authenticators.BIOMETRIC_WEAK).build())
    }
}
