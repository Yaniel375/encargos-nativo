package com.agencia.encargos.nativo.ui

import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.AddAPhoto
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateListOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.foundation.layout.Spacer
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.agencia.encargos.core.JArr
import com.agencia.encargos.core.JObj
import com.agencia.encargos.core.Js
import com.agencia.encargos.core.str
import com.agencia.encargos.nativo.util.Files
import com.agencia.encargos.nativo.util.Images
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

private val NOTE_COLORS = linkedMapOf("blue" to C.blue, "green" to C.green, "amber" to C.amber, "rose" to C.rose, "vio" to C.vio)
private val URL_RE = Regex("\\b((?:https?://|www\\.)[^\\s<>\"']+|wa\\.me/[^\\s<>\"']+)", RegexOption.IGNORE_CASE)
fun noteLinks(t: String): List<String> = URL_RE.findAll(t).map { it.value.trimEnd('.', ',', ';', ':', '!', '?', ')', ']') }.distinct().toList()
fun fullUrl(u: String) = if (Regex("^https?://", RegexOption.IGNORE_CASE).containsMatchIn(u)) u else "https://$u"

@Composable
fun NotesScreen() {
    val ui = LocalUi.current; val core = ui.core; val v = LocalVersion.current
    var q by rememberSaveable { mutableStateOf("") }; var editing by remember { mutableStateOf<JObj?>(null) }; var creating by remember { mutableStateOf(false) }
    val all = core.notesSorted(); val nq = Js.normSearch(q)
    val list = all.filter { nq.isEmpty() || Js.normSearch(it.str("title") + " " + it.str("body")).contains(nq) }
    val owe = all.sumOf { Js.num(it["amount"]) }; val oweN = all.count { Js.num(it["amount"]) > 0 }
    if (editing != null || creating) { NoteEditor(editing) { editing = null; creating = false }; return }
    Page(right = "${all.size} nota${if (all.size == 1) "" else "s"}") {
        if (owe > 0) item { SectionCard { Row(verticalAlignment = Alignment.CenterVertically) {
            Column(Modifier.weight(1f)) { Text("Te deben en notas", fontWeight = FontWeight.Bold, fontSize = 13.5.sp, color = MaterialTheme.colorScheme.onSurfaceVariant); Text("$oweN nota${if (oweN == 1) "" else "s"} con monto", fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant) }
            Text(money(owe), fontSize = 22.sp, fontWeight = FontWeight.ExtraBold, color = C.rose) } } }
        item { Primary("Nueva nota", Modifier.fillMaxWidth(), Icons.Filled.Add) { creating = true } }
        if (all.isNotEmpty()) item { SearchBox(q, { q = it }, "Buscar en tus notas") }
        if (all.isEmpty()) item { Empty("Aún no tienes notas", "Guarda recordatorios, pedidos por hacer, direcciones o lo que necesites.") }
        else if (list.isEmpty()) item { Empty("Ninguna nota coincide con la búsqueda.") }
        val pinned = list.filter { Js.truthy(it["pinned"]) }; val rest = list.filter { !Js.truthy(it["pinned"]) }
        listOf("Fijadas" to pinned, "Otras notas" to rest).forEach { (h, ns) ->
            if (ns.isNotEmpty()) {
                if (pinned.isNotEmpty()) item { DotHeader(h, if (h == "Fijadas") C.blue else C.mut) }
                ns.chunked(2).forEach { pair -> item {
                    Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(10.dp), verticalAlignment = Alignment.Top) {
                        pair.forEach { n -> NoteCard(n, Modifier.weight(1f)) { editing = n } }
                        if (pair.size == 1) Spacer(Modifier.weight(1f))
                    }
                } }
            }
        }
    }
    @Suppress("UNUSED_VARIABLE") val u = v
}

/** Nota en tarjeta de vidrio con punto de color (cuadrícula de dos columnas, como en la referencia). */
@Composable
fun NoteCard(n: JObj, modifier: Modifier, open: () -> Unit) {
    val ui = LocalUi.current
    val c = NOTE_COLORS[n.str("color")] ?: C.blue
    Column(modifier.glass(RoundedCornerShape(18.dp)).clickable { open() }.padding(13.dp), verticalArrangement = Arrangement.spacedBy(7.dp)) {
        Row(verticalAlignment = Alignment.CenterVertically) { Box(Modifier.size(9.dp).clip(CircleShape).background(c)); Text("  " + n.str("title").ifEmpty { "Sin título" }, fontWeight = FontWeight.ExtraBold, maxLines = 2, modifier = Modifier.weight(1f)); if (Js.truthy(n["pinned"])) Text("📌", fontSize = 12.sp) }
        if (Js.num(n["amount"]) > 0) Chip("Me deben ${money(n["amount"])}", C.rose)
        if (n.str("body").isNotEmpty()) Text(n.str("body"), maxLines = 6, fontSize = 13.5.sp)
        noteLinks(n.str("body")).forEach { u -> val sh = RoundedCornerShape(10.dp)
            Text("🔗 " + u.removePrefix("https://").removePrefix("http://").take(40), color = C.blue, fontSize = 12.5.sp, fontWeight = FontWeight.Bold, maxLines = 1,
                modifier = Modifier.fillMaxWidth().clip(sh).background(W.tile).border(1.dp, W.line, sh).clickable { Files.open(ui.act, fullUrl(u)) }.padding(horizontal = 10.dp, vertical = 8.dp)) }
        val ph = (n["photos"] as? JArr)?.filterIsInstance<String>() ?: emptyList()
        if (ph.isNotEmpty()) Row(Modifier.horizontalScroll(rememberScrollState()), horizontalArrangement = Arrangement.spacedBy(6.dp)) { ph.forEach { Photo(Images.decode(it, 300), Modifier.size(60.dp)) } }
        Text(dayLabel(n["updated"]), fontSize = 11.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
    }
}

@Composable
fun NoteEditor(n: JObj?, close: () -> Unit) {
    val ui = LocalUi.current; val core = ui.core
    var title by remember { mutableStateOf(n?.str("title") ?: "") }; var body by remember { mutableStateOf(n?.str("body") ?: "") }
    var amt by remember { mutableStateOf(if (n != null && Js.num(n["amount"]) > 0) Js.toFixed(Js.num(n["amount"]), 2) else "") }
    var color by remember { mutableStateOf(n?.str("color")?.ifEmpty { null } ?: "blue") }; var pinned by remember { mutableStateOf(n != null && Js.truthy(n["pinned"])) }
    val photos = remember { mutableStateListOf<String>().apply { (n?.get("photos") as? JArr)?.filterIsInstance<String>()?.let { addAll(it) } } }
    var viewer by remember { mutableStateOf<Int?>(null) }
    androidx.activity.compose.BackHandler { close() }
    val pick = rememberLauncherForActivityResult(ActivityResultContracts.GetContent()) { uri -> if (uri != null) ui.scope.launch { withContext(Dispatchers.IO) { Images.fromUri(ui.act, uri, 900, 55) }?.let { photos.add(it); ui.toast("📷 Foto agregada. Toca \"Guardar nota\" para guardarla.") } } }
    Page(title = if (n == null) "Nueva nota" else "Editar nota") {
        item { Field(title, { title = it }, "Título") }
        item { Field(body, { body = it }, "Nota (puedes pegar enlaces https://…)", singleLine = false, minLines = 5) }
        val links = noteLinks(body)
        if (links.isNotEmpty()) item { Column { Text("Enlaces en esta nota · toca para abrir", fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant); links.forEach { u -> Text("🔗 " + u, color = C.blue, modifier = Modifier.clickable { Files.open(ui.act, fullUrl(u)) }.padding(vertical = 3.dp)) } } }
        item { Field(amt, { amt = it }, "Me deben (opcional)", number = true, prefix = "$") }
        item {
            SectionCard("Fotos de artículos") {
                Row(Modifier.horizontalScroll(rememberScrollState()), horizontalArrangement = Arrangement.spacedBy(6.dp)) { photos.forEachIndexed { i, s -> Photo(Images.decode(s, 300), Modifier.size(80.dp)) { viewer = i } } }
                Secondary("Agregar foto", Modifier.fillMaxWidth(), Icons.Filled.AddAPhoto) { pick.launch("image/*") }
            }
        }
        item { Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) { NOTE_COLORS.forEach { (k, c) -> Box(Modifier.size(34.dp).clip(CircleShape).background(c).border(if (k == color) 3.dp else 0.dp, Color.White, CircleShape).clickable { color = k }) } } }
        item { Row(verticalAlignment = Alignment.CenterVertically) { Column(Modifier.weight(1f)) { Text("Fijar arriba", fontWeight = FontWeight.Bold); Text("Las notas fijadas aparecen primero", fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant) }; Switch(pinned, { pinned = it }) } }
        if (n != null) item { Text("Creada: ${Js.fmt(n["created"])}" + if (Js.num(n["updated"]) != Js.num(n["created"])) " · Editada: ${Js.fmt(n["updated"])}" else "", fontSize = 11.sp, color = MaterialTheme.colorScheme.onSurfaceVariant) }
        item { Primary("Guardar nota", Modifier.fillMaxWidth()) { core.saveNote(n?.str("id"), title, body, kotlin.math.max(0.0, Js.num(amt)), color, pinned, photos.toList()).onSuccess { ui.toast(if (n != null) "✅ Nota actualizada." else "✅ Nota guardada."); close() }.onFailure { ui.toast(it.message ?: "") } } }
        item { if (n != null) Secondary("Eliminar", Modifier.fillMaxWidth(), color = C.rose) { ui.ask("🗑️ Eliminar nota", "¿Eliminar esta nota? No se puede deshacer.", "Eliminar", true) { core.deleteNote(n.str("id")); close() } } else Secondary("Cancelar", Modifier.fillMaxWidth()) { close() } }
    }
    viewer?.let { i -> PhotoViewer(photos.toList(), i, onDelete = { idx -> photos.removeAt(idx); viewer = null }) { viewer = null } }
}
