package com.agencia.encargos.nativo.util

import android.content.Context
import com.agencia.encargos.core.Encargos
import com.agencia.encargos.core.Js
import com.agencia.encargos.core.str
import java.io.File

/** Solo en la copia de PRUEBA automática: crea encargos ficticios con casos distintos (nombre largo, barco, pagado de más,
 *  varios pagos, sin pagos) y guarda la imagen del recibo de cada encargo para revisarla. Nunca se usa con datos reales. */
object TestReceipts {
    fun run(ctx: Context, core: Encargos) {
        fun has(n: String) = core.orders().any { it.str("name") == n }
        if (!has("María Fernanda Rodríguez Hernández")) core.createOrder(Encargos.NewOrder("María Fernanda Rodríguez Hernández", "55500011", 125.5, 4.0, 12.75, 200.0, "Efectivo", "Entregado", "barco"))
        if (!has("Pedro Varios Pagos")) core.createOrder(Encargos.NewOrder("Pedro Varios Pagos", "55500022", 80.0, 7.0, 6.5, 20.0, "Efectivo", "En tránsito", "avion")).onSuccess { x ->
            core.saveEdit(x["id"], 0.0, 15.0, "USD", "segundo pago", "En tránsito"); core.saveEdit(x["id"], 0.0, 10.0, "Efectivo", "", "Recibido") }
        if (!has("Ana Sin Pago")) core.createOrder(Encargos.NewOrder("Ana Sin Pago", "", 60.0, 7.0, 2.0, 0.0))
        val dir = File(ctx.getExternalFilesDir(null), "recibos"); dir.mkdirs(); dir.listFiles()?.forEach { it.delete() }
        core.orders().forEachIndexed { i, x ->
            val bytes = Receipt.png(Receipt.bitmap(core, x))
            File(dir, "recibo_${i + 1}_" + x.str("name").replace(Regex("[^A-Za-z0-9]+"), "_").take(24) + ".png").writeBytes(bytes)
        }
        @Suppress("UNUSED_VARIABLE") val n = Js.num(0)
    }
}
