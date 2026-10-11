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
    /** Contexto de la app para cargar la tipografía Manrope, el logo y el fondo aurora (se fija al abrir la app). */
    @Volatile var ctx: Context? = null
    private const val S = 3f                      // 1 dp de la app = 3 px en el recibo (1080 px de ancho = 360 dp)
    private const val W = 1080
    private fun dp(v: Float) = v * S
    private fun font(id: Int, bold: Boolean): Typeface = try { ctx?.let { androidx.core.content.res.ResourcesCompat.getFont(it, id) } } catch (e: Exception) { null }
        ?: Typeface.create(Typeface.DEFAULT, if (bold) Typeface.BOLD else Typeface.NORMAL)

    /** Imagen del recibo con el mismo estilo que la app (modo noche aurora): fondo, logo, Manrope, tarjetas de cristal,
     *  ficha de saldo, etiqueta de estado y recuadros de cifras como en la ficha del encargo. Las cifras no cambian. */
    fun bitmap(app: Encargos, x: JObj): Bitmap {
        val c0 = ctx
        val fReg = font(com.agencia.encargos.nativo.R.font.manrope_regular, false); val fSemi = font(com.agencia.encargos.nativo.R.font.manrope_semibold, true)
        val fBold = font(com.agencia.encargos.nativo.R.font.manrope_bold, true); val fXb = font(com.agencia.encargos.nativo.R.font.manrope_extrabold, true)
        // colores de la app (modo noche)
        val tx = Color.parseColor("#F1F5F9"); val mut = Color.parseColor("#9AA6B8"); val blue = Color.parseColor("#3B82F6"); val blueInk = Color.parseColor("#93C5FD")
        val green = Color.parseColor("#34D399"); val rose = Color.parseColor("#FB7185")
        val line = Color.argb(36, 148, 163, 184); val line2 = Color.argb(61, 148, 163, 184); val tile = Color.argb(20, 148, 163, 184); val track = Color.argb(40, 148, 163, 184)
        val glassA = Color.argb(158, 30, 41, 59); val glassB = Color.argb(184, 15, 23, 42)
        fun alpha(col: Int, a: Float) = Color.argb((a * 255).toInt(), Color.red(col), Color.green(col), Color.blue(col))
        fun ink(col: Int) = Color.rgb((Color.red(col) + (255 - Color.red(col)) * .45f).toInt(), (Color.green(col) + (255 - Color.green(col)) * .45f).toInt(), (Color.blue(col) + (255 - Color.blue(col)) * .45f).toInt())

        val b = Js.num(x["balance"]); val tot = Js.num(x["total"]); val paid = Js.num(x["paid"]); val st = x.str("status").ifEmpty { "Pedido" }
        val stc = when (st) { "En tránsito" -> Color.parseColor("#94A3B8"); "Recibido" -> Color.parseColor("#7DD3FC"); "Entregado" -> green; "Cancelado" -> Color.parseColor("#64748B"); else -> blue }
        val owes = b > 0.005; val tone = if (owes) rose else green
        val pays = (x["paymentHistory"] as? JArr)?.objs()?.sortedBy { Js.parseDate(it["date"]) } ?: emptyList()

        // se dibuja en una capa alta y transparente; al final se recorta y se pone sobre el fondo aurora
        val layer = Bitmap.createBitmap(W, 6000, Bitmap.Config.ARGB_8888); val c = Canvas(layer)
        val p = Paint(Paint.ANTI_ALIAS_FLAG)
        val P = dp(16f); val IW = W - P * 2
        fun rr(x0: Float, y0: Float, w: Float, h: Float, r: Float, fill: Int? = null, stroke: Int? = null, shader: android.graphics.Shader? = null) {
            val rect = RectF(x0, y0, x0 + w, y0 + h)
            if (fill != null || shader != null) { p.style = Paint.Style.FILL; p.shader = shader; if (shader == null) p.color = fill!!; c.drawRoundRect(rect, r, r, p); p.shader = null }
            if (stroke != null) { p.style = Paint.Style.STROKE; p.strokeWidth = dp(1f); p.color = stroke; c.drawRoundRect(RectF(rect.left + 1, rect.top + 1, rect.right - 1, rect.bottom - 1), r, r, p) }
            p.style = Paint.Style.FILL
        }
        fun glass(x0: Float, y0: Float, w: Float, h: Float, r: Float = dp(18f)) = rr(x0, y0, w, h, r, stroke = line, shader = android.graphics.LinearGradient(x0, y0, x0 + w, y0 + h, glassA, glassB, android.graphics.Shader.TileMode.CLAMP))
        fun text(t: String, x0: Float, y0: Float, sp: Float, col: Int, tf: Typeface = fReg, align: Paint.Align = Paint.Align.LEFT, maxW: Float = 0f): Float {
            p.textSize = dp(sp); p.color = col; p.typeface = tf; p.textAlign = align
            var s2 = t; if (maxW > 0) while (s2.length > 1 && p.measureText(s2) > maxW) s2 = s2.dropLast(2) + "…"
            c.drawText(s2, x0, y0, p); return p.measureText(s2)
        }
        fun width(t: String, sp: Float, tf: Typeface): Float { p.textSize = dp(sp); p.typeface = tf; return p.measureText(t) }

        var y = dp(16f)
        // ---- barra superior como la de la app: logo, nombre del negocio y "Recibo" ----
        val barH = dp(58f); rr(P, y, IW, barH, dp(20f), Color.argb(199, 9, 13, 24), line)
        val logoS = dp(34f); val lx = P + dp(14f); val ly = y + (barH - logoS) / 2
        val logo = try { c0?.let { BitmapFactory.decodeResource(it.resources, com.agencia.encargos.nativo.R.mipmap.ic_launcher) } } catch (e: Exception) { null }
        if (logo != null) {
            val save = c.save(); val path = android.graphics.Path().apply { addRoundRect(RectF(lx, ly, lx + logoS, ly + logoS), dp(10f), dp(10f), android.graphics.Path.Direction.CW) }
            c.clipPath(path); c.drawBitmap(logo, null, RectF(lx, ly, lx + logoS, ly + logoS), Paint(Paint.FILTER_BITMAP_FLAG)); c.restoreToCount(save)
        } else { rr(lx, ly, logoS, logoS, dp(10f), blue); text("e", lx + logoS / 2, ly + logoS * .72f, 20f, Color.WHITE, fXb, Paint.Align.CENTER) }
        val bizName = app.businessName.ifBlank { "Encargos" }
        // el nombre del negocio queda centrado a la altura del logo
        text(bizName, lx + logoS + dp(9f), y + barH / 2 + dp(6f), 17f, tx, fXb, maxW = IW - dp(170f))
        val chipT = "Recibo"; val cw = width(chipT, 12f, fBold) + dp(24f)
        rr(P + IW - dp(14f) - cw, y + (barH - dp(28f)) / 2, cw, dp(28f), dp(14f), alpha(blue, .14f), alpha(blue, .38f))
        text(chipT, P + IW - dp(14f) - cw / 2, y + barH / 2 + dp(4.5f), 12f, blueInk, fBold, Paint.Align.CENTER)
        y += barH + dp(18f)
        // ---- título de la página ----
        text("Recibo de compra", P + dp(2f), y + dp(22f), 24f, tx, fXb); y += dp(30f)
        text(Js.fmt(x["date"]), P + dp(2f), y + dp(10f), 13f, mut, fReg); y += dp(22f)

        // ---- ficha: igual que la cabecera de la ficha del encargo ----
        val cardTop = y; val pad = dp(18f)
        var yy = cardTop + pad
        val av = dp(60f); rr(P + pad, yy, av, av, av * .3f, tile, if (owes) alpha(rose, .35f) else line2)
        text(x.str("name").trim().take(1).uppercase().ifEmpty { "?" }, P + pad + av / 2, yy + av / 2 + dp(9f), 25f, tx, fXb, Paint.Align.CENTER)
        // etiqueta de estado (punto + texto) a la derecha
        // el nombre usa todo el ancho; la etiqueta de estado va a la derecha de los datos de contacto
        val nx = P + pad + av + dp(14f)
        text(x.str("name").ifEmpty { "Cliente" }, nx, yy + dp(20f), 21f, tx, fXb, maxW = P + IW - pad - nx)
        val chW = width(st, 11.5f, fBold) + dp(32f); val chX = P + IW - pad - chW; val chY = yy + dp(30f)
        rr(chX, chY, chW, dp(26f), dp(13f), alpha(stc, .14f), alpha(stc, .38f))
        p.color = stc; c.drawCircle(chX + dp(13f), chY + dp(13f), dp(3f), p)
        text(st, chX + dp(22f), chY + dp(17.5f), 11.5f, ink(stc), fBold)
        text("Tel. " + x.str("phone").ifEmpty { "sin teléfono" }, nx, yy + dp(39f), 13f, mut, fReg, maxW = chX - nx - dp(8f))
        text(app.shippingName(app.typeOf(x)) + " · " + Js.toFixed(Js.num(x["pounds"]), 2) + " lb", nx, yy + dp(56f), 13f, mut, fReg, maxW = chX - nx - dp(8f))
        text("Encargo del " + Js.fmt(x["date"]).substringBefore(","), nx, yy + dp(73f), 12f, mut, fReg)
        yy += dp(84f)
        // recuadro del saldo (rojo si debe, verde si está pagado o tiene saldo a favor)
        val heroH = dp(112f); rr(P + pad, yy, IW - pad * 2, heroH, dp(16f), alpha(tone, .08f), alpha(tone, .30f))
        val hx = P + pad + dp(14f); val hr = P + IW - pad - dp(14f)
        text(if (owes) "Pendiente por cobrar" else if (b < -0.005) "Saldo a favor" else "Pagado completo", hx, yy + dp(24f), 12.5f, mut, fReg)
        text(if (owes) "- " + Js.money(b) else if (b < -0.005) "+ " + Js.money(-b) else Js.money(0), hx, yy + dp(56f), 26f, tone, fXb)
        text("Total", hr, yy + dp(32f), 12f, mut, fReg, Paint.Align.RIGHT); text(Js.money(tot), hr, yy + dp(54f), 17f, tx, fXb, Paint.Align.RIGHT)
        val k = if (tot > 0) (paid / tot).coerceIn(0.0, 1.0) else 1.0
        val bw = hr - hx; rr(hx, yy + dp(70f), bw, dp(6f), dp(3f), track)
        if (k > 0) { val w2 = max(dp(6f), (bw * k).toFloat()); rr(hx, yy + dp(70f), w2, dp(6f), dp(3f), shader = android.graphics.LinearGradient(hx, 0f, hx + w2, 0f, alpha(green, .75f), green, android.graphics.Shader.TileMode.CLAMP)) }
        text("Pagó ${Js.money(paid)} de ${Js.money(tot)}" + if (tot > 0) " · ${Math.round(k * 100)} %" else "", hx, yy + dp(97f), 12.5f, mut, fReg)
        yy += heroH + dp(10f)
        // recuadros de cifras (como Kpi): Pedido · Envío · Pagado
        val kw = (IW - pad * 2 - dp(16f)) / 3f; val kh = dp(68f)
        listOf(Triple("Pedido", Js.money(x["cost"]), null), Triple("Envío", Js.money(x["shipping"]), Js.toFixed(Js.num(x["pounds"]), 2) + " × " + Js.money(x["lbCost"])), Triple("Pagado", Js.money(paid), null)).forEachIndexed { i, (l, v, sub) ->
            val kx = P + pad + i * (kw + dp(8f)); val col = if (i == 2) green else null
            rr(kx, yy, kw, kh, dp(12f), if (col != null) alpha(col, .08f) else tile, if (col != null) alpha(col, .30f) else line)
            text(l, kx + dp(11f), yy + dp(20f), 11.5f, mut, fSemi); text(v, kx + dp(11f), yy + dp(42f), 17f, col ?: tx, fXb, maxW = kw - dp(20f))
            if (sub != null) text(sub, kx + dp(11f), yy + dp(58f), 11f, mut, fReg, maxW = kw - dp(20f))
        }
        yy += kh + pad
        // la tarjeta se dibuja detrás (se pinta ahora con su altura real y se vuelve a pintar el contenido encima)
        val cardH = yy - cardTop
        run {
            val tmp = Bitmap.createBitmap(W, cardH.toInt() + 4, Bitmap.Config.ARGB_8888)
            Canvas(tmp).drawBitmap(layer, android.graphics.Rect(0, cardTop.toInt(), W, (cardTop + cardH).toInt() + 4), android.graphics.Rect(0, 0, W, cardH.toInt() + 4), null)
            val clr = Paint().apply { xfermode = android.graphics.PorterDuffXfermode(android.graphics.PorterDuff.Mode.CLEAR) }
            c.drawRect(0f, cardTop, W.toFloat(), cardTop + cardH + 4, clr)
            glass(P, cardTop, IW, cardH); c.drawBitmap(tmp, 0f, cardTop, null)
        }
        y = yy + dp(12f)

        // ---- tarjetas de sección (título con punto azul, filas con separador) ----
        fun section(title: String, rows: List<Triple<String, String, Int>>, boldLast: Boolean) {
            val rowH = dp(40f); val h = dp(52f) + rowH * rows.size + dp(6f)
            glass(P, y, IW, h)
            p.color = blue; c.drawCircle(P + dp(24f), y + dp(28f), dp(4f), p)
            text(title, P + dp(36f), y + dp(34f), 16f, tx, fXb)
            var ry = y + dp(52f)
            rows.forEachIndexed { i, (l, v, vc) ->
                val last = boldLast && i == rows.size - 1
                if (i > 0) { p.color = line; p.strokeWidth = dp(1f); c.drawLine(P + dp(18f), ry, P + IW - dp(18f), ry, p) }
                text(l, P + dp(18f), ry + dp(26f), 14f, if (last) tx else mut, if (last) fBold else fReg, maxW = IW - dp(36f) - width(v, 14f, fSemi) - dp(14f))
                text(v, P + IW - dp(18f), ry + dp(26f), 14f, vc, if (last) fXb else fSemi, Paint.Align.RIGHT)
                ry += rowH
            }
            y += h + dp(12f)
        }
        section("Detalle del pedido", listOf(
            Triple("Costo del pedido", Js.money(x["cost"]), tx),
            Triple("Envío " + app.shippingName(app.typeOf(x)) + " · " + Js.toFixed(Js.num(x["pounds"]), 2) + " lb × " + Js.money(x["lbCost"]), Js.money(x["shipping"]), tx),
            Triple("Total", Js.money(tot), tx)), true)
        section("Pagos", (if (pays.isNotEmpty()) pays.map { pp -> Triple(Js.fmt(pp["date"]) + " · " + pp.str("method").ifEmpty { "Efectivo" }, "+" + Js.money(pp["amount"]), green) }
            else listOf(Triple("Sin pagos registrados", "—", mut))) + listOf(Triple("Total pagado", Js.money(paid), green)), true)
        // ---- pie ----
        p.color = blue; c.drawRoundRect(RectF(W / 2f - dp(11f), y + dp(6f), W / 2f + dp(11f), y + dp(9f)), dp(2f), dp(2f), p)
        text("Gracias por tu confianza", W / 2f, y + dp(30f), 13f, mut, fSemi, Paint.Align.CENTER)
        text(bizName, W / 2f, y + dp(48f), 11f, Color.parseColor("#7C8BA5"), fReg, Paint.Align.CENTER)
        y += dp(64f)

        // ---- fondo aurora de la app y composición final ----
        val H = y.toInt()
        val out = Bitmap.createBitmap(W, H, Bitmap.Config.ARGB_8888); val oc = Canvas(out)
        oc.drawColor(Color.parseColor("#020611"))
        val bg = try { c0?.let { BitmapFactory.decodeResource(it.resources, com.agencia.encargos.nativo.R.drawable.aurora_bg) } } catch (e: Exception) { null }
        if (bg != null) {   // recorte centrado que cubre todo el recibo
            val sc = max(W.toFloat() / bg.width, H.toFloat() / bg.height); val dw = bg.width * sc; val dh = bg.height * sc
            oc.drawBitmap(bg, null, RectF((W - dw) / 2, (H - dh) / 2, (W + dw) / 2, (H + dh) / 2), Paint(Paint.FILTER_BITMAP_FLAG))
            oc.drawColor(Color.argb(90, 2, 6, 17))
        }
        oc.drawBitmap(layer, android.graphics.Rect(0, 0, W, H), android.graphics.Rect(0, 0, W, H), null)
        layer.recycle()
        return out
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
