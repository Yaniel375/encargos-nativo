using Encargos.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using static Encargos.Win.K;

namespace Encargos.Win;

static class AdminUtil
{
    public static string TimeLabel(object? t) => Js.Num(t) > 0 ? Js.Fmt(t) : "—";
    /// <summary>Pide una contraseña (con confirmación opcional). null = cancelado.</summary>
    public static async Task<string?> AskPassword(string title, string text, bool confirm, string ok)
    {
        string a = "", b = ""; var err = T("", 13, false, Rose);
        var good = await MainWindow.I.Dialog(title, V(10, Muted(text, 13), Pass("Contraseña", v => a = v), confirm ? Pass("Repite la contraseña", v => b = v) : null, err), ok, () =>
        {
            if (a.Length < 6) { err.Text = "Usa al menos 6 caracteres."; return false; }
            if (confirm && a != b) { err.Text = "Las contraseñas no coinciden."; return false; }
            return true;
        });
        return good ? a : null;
    }
}

/* ---------------- Respaldo ---------------- */
public sealed class BackupPage : Screen
{
    public override string Title => "Respaldo";
    public override string Nav => "backup";

    public override void Build(StackPanel p)
    {
        var bk = Services.Backup;
        var last = double.TryParse(Core.Ls.Get("encargos_last_backup", "0"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var l) ? l : 0;
        p.Children.Add(Card("Último respaldo", null, T(last > 0 ? Js.Fmt(last) : "Todavía no has hecho un respaldo.", 15, true),
            Muted("Guarda una copia de tus encargos, pagos, notas y gastos. Puedes guardarla en una memoria USB, en tu nube o enviarla por WhatsApp.", 13)));
        p.Children.Add(Cols(
            Card("Exportar", null,
                Btn("Guardar respaldo (.json)", () => S.RequireAuth("Confirma tu identidad para exportar", async () =>
                {
                    var s = bk.ExportJson(); if (await Media.SaveFile(Backup.FileName(), ".json", "Respaldo de Encargos", Crypto.Utf8(s))) S.Toast("✅ Respaldo guardado.");
                }), true, null, ""),
                Btn("Guardar respaldo cifrado (con contraseña)", () => S.RequireAuth("Confirma tu identidad para exportar", async () =>
                {
                    var pw = await AdminUtil.AskPassword("🔒 Respaldo cifrado", "Elige una contraseña para este archivo. Si la olvidas, no se puede recuperar.", true, "Cifrar y guardar");
                    if (pw == null) return;
                    var s = await S.Busy("Cifrando…", () => bk.ExportEncrypted(pw));
                    if (await Media.SaveFile(Backup.FileName("respaldo_encargos_cifrado_"), ".json", "Respaldo cifrado de Encargos", Crypto.Utf8(s))) S.Toast("✅ Respaldo cifrado guardado.");
                }), false, null, ""),
                Btn("Exportar a Excel (.csv)", () => S.RequireAuth("Confirma tu identidad para exportar", async () =>
                {
                    if (await Media.SaveFile(Backup.FileName("encargos_", ".csv"), ".csv", "CSV para Excel", Crypto.Utf8(bk.Csv()))) S.Toast("✅ Archivo CSV guardado.");
                }), false, null, "")),
            Card("Importar", null,
                Btn("Abrir archivo de respaldo", Import, false, null, ""),
                Muted("Acepta respaldos .json normales y cifrados de Encargos (web, Android o PC). Reemplaza los encargos de este equipo.", 13))));
        var mode = bk.AutoMode;
        p.Children.Add(Card("Respaldo automático", null,
            Choice(new[] { ("daily", "Diario"), ("weekly", "Semanal"), ("off", "Desactivado") }, mode, k => bk.AutoMode = k),
            Muted(mode == "off" ? "No se guardarán copias automáticas." : "Al abrir la app se guarda una copia en Documentos\\Encargos " + (mode == "weekly" ? "una vez por semana." : "una vez al día.") +
                (bk.LastAuto > 0 ? " Última: " + Js.Fmt(bk.LastAuto) : ""), 13),
            Cols(Btn("Hacer copia automática ahora", () =>
            {
                var payload = bk.AutoDue(true); if (payload == null) return;
                try { File.WriteAllText(Path.Combine(Services.DocsDir(), "respaldo_auto_" + Js.NowIso()[..10] + ".json"), payload); bk.AutoDone(); S.Toast("✅ Copia guardada en Documentos\\Encargos."); }
                catch (Exception e) { Services.Report(e); S.Toast("No se pudo guardar la copia."); }
            }), Btn("Abrir carpeta de copias", async () => await Windows.System.Launcher.LaunchFolderPathAsync(Services.DocsDir())))));
    }

    async void Import()
    {
        var text = await Media.OpenText(".json", ".txt");
        if (text == null) return;
        var bk = Services.Backup;
        switch (bk.Parse(text))
        {
            case Backup.Parsed.Invalid inv: S.Toast(inv.Why); break;
            case Backup.Parsed.Plain pl: Confirm(pl.O); break;
            case Backup.Parsed.Encrypted enc:
                {
                    var pw = await AdminUtil.AskPassword("🔒 Respaldo cifrado", "Escribe la contraseña con la que se guardó este archivo.", false, "Abrir");
                    if (pw == null) return;
                    var o = await S.Busy("Descifrando…", () => { try { return bk.Decrypt(enc.O, pw); } catch { return null; } });
                    if (o == null) S.Toast("Contraseña incorrecta o archivo dañado."); else Confirm(o);
                    break;
                }
        }
    }
    void Confirm(object? o)
    {
        var n = ((o as JArr) ?? ((o as JObj)?["data"] as JArr))?.Count;
        if (n == null) { S.Toast("El archivo de respaldo no es válido."); return; }
        S.Ask("📥 Importar respaldo", $"Se reemplazarán los encargos de este equipo por los {n} del archivo. Si tienes sincronización activa, los cambios también llegarán a tus otros dispositivos. ¿Continuar?", "Importar", true, () =>
            S.RequireAuth("Confirma tu identidad para importar", () =>
            {
                try { var c = Services.Backup.ApplyImport(o); S.Toast($"✅ Respaldo importado: {c} encargo{(c == 1 ? "" : "s")}."); }
                catch (Exception e) { S.Toast(e.Message); }
            }));
    }
}

/* ---------------- Seguridad ---------------- */
public sealed class SecurityPage : Screen
{
    public override string Title => "Seguridad";
    public override string Nav => "security";
    bool? _hello;

    public override void Build(StackPanel p)
    {
        var sec = Services.Security; var lk = sec.GetLockCfg();
        if (_hello == null) { _ = CheckHello(); }
        p.Children.Add(Cols(
            Card("Acceso", null, Line("Usuario", sec.UserName().IfEmpty("—")), Btn("Cambiar usuario y contraseña", () => S.RequireAuth("Confirma tu identidad", Credentials, true))),
            Card("PIN y Windows Hello", null, sec.PinOn()
                ? V(8, Line("PIN", $"Activo · {sec.PinLen()} dígitos", Green),
                    Cols(Btn("Cambiar PIN", () => S.RequireAuth("Confirma tu identidad", PinDialog, true)),
                        Btn("Desactivar", () => S.RequireAuth("Confirma tu identidad", () => { sec.DisablePin(); S.Toast("PIN desactivado."); Redraw(); }, true), false, Rose)),
                    _hello == true ? (UIElement)Toggle("Desbloquear con Windows Hello", "Huella, rostro o PIN de Windows", sec.BioOn(), async on =>
                    {
                        if (on) { if (await Native.Hello(S.Hwnd, "Activar Windows Hello en Encargos")) { sec.SetBio(true); S.Toast("✅ Windows Hello activado."); } }
                        else sec.SetBio(false);
                        Redraw();
                    }) : Muted(_hello == false ? "Windows Hello no está configurado en este equipo." : "Comprobando Windows Hello…"))
                : V(8, Muted("Con un PIN entras más rápido sin escribir tu contraseña.", 13), Btn("Crear PIN", () => S.RequireAuth("Confirma tu identidad", PinDialog, true), true)))));
        var opts = new[] { ("0", "Al salir"), ("1", "1 min"), ("5", "5 min"), ("15", "15 min"), ("30", "30 min"), ("never", "Nunca") };
        var sel = !lk.On ? "never" : Js.NumberToString(lk.Minutes);
        p.Children.Add(Card("Bloqueo automático", null, Choice(opts, sel, k => { sec.SetLock(k == "never" ? (double?)null : double.Parse(k, System.Globalization.CultureInfo.InvariantCulture)); Redraw(); }),
            Muted(lk.On ? "Se pedirá tu PIN o contraseña tras " + Security.LockLabel(lk.Minutes).ToLowerInvariant() + " fuera de la app." : "La app no se bloqueará sola.", 13),
            Toggle("Pedir acceso al abrir la app", "", lk.OnOpen, v => { sec.SetLockOnOpen(v); })));
        p.Children.Add(Card("Privacidad", null,
            Toggle("Ocultar en capturas de pantalla", "Excluye la ventana de capturas, grabaciones y pantallas compartidas", sec.PrivacyOn(), v => { sec.SetPrivacy(v); S.ApplyPrivacy(); }),
            Toggle("Proteger acciones sensibles", "Pide tu identidad para eliminar, importar o exportar", sec.SensitiveOn(), v =>
            {
                if (v) sec.SetSensitive(true); else S.RequireAuth("Confirma tu identidad", () => sec.SetSensitive(false), true);
                Redraw();
            })));
        var log = sec.LogEntries().Take(15).ToList();
        p.Children.Add(Cols(
            Card("Actividad de seguridad", null, log.Count == 0 ? new UIElement[] { Muted("Sin actividad registrada.", 14) } :
                log.Select(e => (UIElement)Lead(T(Js.Truthy(e["ok"]) ? "✅" : "⚠️"), V(0, T(e.Str("text"), 13), Muted(AdminUtil.TimeLabel(e["t"]), 11)))).ToArray()),
            Card("Zona de peligro", null, Muted("Borra los datos de este equipo. Haz un respaldo antes. Los respaldos de Telegram no se borran.", 13),
                Btn("Borrar datos de este equipo", () => S.RequireAuth("Confirma tu identidad", Wipe, true), false, Rose))));
    }
    async Task CheckHello() { _hello = await Native.HelloAvailable(); Redraw(); }

    async void PinDialog()
    {
        string a = "", b = ""; var err = T("", 13, false, Rose);
        var ok = await S.Dialog("🔢 PIN", V(10, Muted("Elige un PIN de 4 a 6 números.", 13), new PasswordBox { Header = "PIN nuevo", MaxLength = 6 }.Also(pb => pb.PasswordChanged += (_, _) => a = pb.Password),
            new PasswordBox { Header = "Repite el PIN", MaxLength = 6 }.Also(pb => pb.PasswordChanged += (_, _) => b = pb.Password), err), "Guardar PIN", () =>
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(a, "^[0-9]{4,6}$")) { err.Text = "El PIN debe tener de 4 a 6 números."; return false; }
                if (a != b) { err.Text = "Los PIN no coinciden."; return false; }
                return true;
            });
        if (!ok) return;
        var r = await S.Busy("Guardando…", () => Services.Security.SetPin(a));
        S.Toast(r.Ok ? "✅ PIN guardado." : r.Error); Redraw();
    }
    async void Credentials()
    {
        var sec = Services.Security; string u = sec.UserName(), a = "", b = ""; var err = T("", 13, false, Rose);
        var ok = await S.Dialog("👤 Usuario y contraseña", V(10, Box("Usuario", u, v => u = v), Pass("Contraseña nueva", v => a = v), Pass("Repite la contraseña", v => b = v), err), "Guardar", () =>
        {
            if (a != b) { err.Text = "Las contraseñas no coinciden."; return false; }
            if (u.Trim().Length < 3) { err.Text = "El usuario debe tener al menos 3 caracteres."; return false; }
            if (a.Length < 6) { err.Text = "La contraseña debe tener al menos 6 caracteres."; return false; }
            return true;
        });
        if (!ok) return;
        var r = await S.Busy("Guardando…", () => sec.SetCredentials(u, a));
        S.Toast(r.Ok ? "✅ Usuario y contraseña actualizados." : r.Error); Redraw();
    }
    async void Wipe()
    {
        var t = ""; var reset = false;
        var ok = await S.Dialog("⚠️ Borrar datos", V(10, T("Se eliminarán los encargos, pagos, notas y gastos de ESTE equipo. Esta acción no se puede deshacer."), Muted("Los respaldos de Telegram no se borran.", 13),
            Toggle("Restablecer también el acceso", "Borra usuario, contraseña y PIN", false, v => reset = v), Box("Escribe ELIMINAR para confirmar", "", v => t = v)), "Borrar todo", () => t.Trim() == "ELIMINAR");
        if (!ok) return;
        Services.Security.Wipe(reset); S.Root(new HomePage()); S.Toast("Datos eliminados de este equipo.");
    }
}

public static class Ext { public static T Also<T>(this T x, Action<T> f) { f(x); return x; } }

/* ---------------- Ajustes ---------------- */
public sealed class SettingsPage : Screen
{
    public override string Title => "Ajustes";
    public override string Nav => "settings";
    public override bool Live => false;
    string? _biz, _greet, _cc, _tArr, _tRem, _tTh, _warn, _late; string _newName = "", _newPrice = "";
    readonly Dictionary<string, string> _rates = new(), _buy = new(); bool _applyActive;

    public override void Build(StackPanel p)
    {
        _biz ??= Core.BusinessName; _greet ??= Core.GreetName; _cc ??= Core.PhoneCC;
        _tArr ??= Core.Tpl("arrival"); _tRem ??= Core.Tpl("reminder"); _tTh ??= Core.Tpl("thanks");
        _warn ??= Core.TransitWarn.ToString(); _late ??= Core.TransitLate.ToString();
        var keys = Core.ShippingRates.Keys.ToList(); var buyR = Core.BuyRates();
        foreach (var k in keys) { if (!_rates.ContainsKey(k)) _rates[k] = Js.NumberToString(Js.Num(Core.ShippingRates[k])); if (!_buy.ContainsKey(k)) _buy[k] = Js.NumberToString(Js.Num(buyR[k])); }

        p.Children.Add(Cols(
            Card("Tu negocio", null, Box("Nombre del negocio (aparece en recibos)", _biz, v => _biz = v), Box("Tu nombre (para el saludo)", _greet, v => _greet = v),
                Box("Código de país para WhatsApp (sin +)", _cc, v => _cc = v, false, null, 4),
                Btn("Guardar", () => { Core.BusinessName = _biz!.Trim(); Core.GreetName = _greet!.Trim(); Core.PhoneCC = _cc!.IfEmpty("53"); S.Toast("✅ Datos guardados."); }, true)),
            Card("Alertas de tránsito", null, Cols(Box("Avisar a los (días)", _warn, v => _warn = v), Box("Retrasado a los (días)", _late, v => _late = v)),
                Btn("Guardar alertas", () =>
                {
                    Core.SaveTransitAlerts(int.TryParse(_warn, out var w) ? w : Core.TransitWarn, int.TryParse(_late, out var l) ? l : Core.TransitLate);
                    _warn = Core.TransitWarn.ToString(); _late = Core.TransitLate.ToString(); S.Toast("✅ Alertas guardadas."); Redraw();
                }))));

        var rateRows = new List<UIElement>();
        foreach (var k in keys)
        {
            var key = k;
            rateRows.Add(Row(Box(Core.ShippingLabel(k) + " ($/lb)", _rates[k], v => _rates[key] = v),
                k is "avion" or "barco" ? null : Btn("Quitar", () => S.Ask("Eliminar tarifa", $"¿Eliminar la tarifa \"{Core.ShippingLabel(key)}\"? Los encargos que ya la usan no cambian.", "Eliminar", true, () => { Core.RemoveShippingRate(key); _rates.Remove(key); Redraw(); }), false, Rose, null, false)));
        }
        rateRows.Add(Btn("Guardar tarifas", () =>
        {
            var m = new Dictionary<string, double>(); var bad = false;
            foreach (var k in keys) { var n = Num(_rates.GetValueOrDefault(k, "")); if (double.IsNaN(n) || n < 0 || _rates.GetValueOrDefault(k, "").Trim().Length == 0) bad = true; else m[k] = n; }
            if (bad) S.Toast("Revisa las tarifas: deben ser números de 0 en adelante."); else { Core.SetShippingRates(m); S.Toast("✅ Tarifas guardadas. Se aplican a los encargos nuevos."); }
        }, true));
        rateRows.Add(T("Agregar tarifa", 14, true));
        rateRows.Add(Cols(Box("Nombre (ej. Express)", _newName, v => _newName = v), Box("Precio por libra ($)", _newPrice, v => _newPrice = v)));
        rateRows.Add(Btn("+ Agregar tarifa", () =>
        {
            var pr = Num(_newPrice);
            if (_newName.Trim().Length == 0 || double.IsNaN(pr) || pr < 0 || _newPrice.Trim().Length == 0) { S.Toast("Escribe un nombre y un precio válido."); return; }
            var k = Core.AddShippingRate(_newName, pr);
            if (k == null) { S.Toast("No se pudo agregar la tarifa."); return; }
            _rates[k] = Js.NumberToString(pr); _newName = ""; _newPrice = ""; S.Toast("✅ Tarifa agregada."); Redraw();
        }));
        var buyRows = new List<UIElement> { Muted("Se usa para calcular tu ganancia por encargo.", 13) };
        foreach (var k in keys) { var key = k; buyRows.Add(Box(Core.ShippingLabel(k) + " ($/lb)", _buy.GetValueOrDefault(k, "0"), v => _buy[key] = v)); }
        buyRows.Add(Toggle("Aplicar también a encargos activos", "Actualiza el costo en los encargos de clientes que aún no terminan", _applyActive, v => _applyActive = v));
        buyRows.Add(Btn("Guardar costos", () =>
        {
            var m = new Dictionary<string, double>(); foreach (var k in keys) { var n = Num(_buy.GetValueOrDefault(k, "")); if (!double.IsNaN(n) && n >= 0) m[k] = n; }
            Core.SaveBuyRates(m, _applyActive); S.Toast("✅ Costos guardados.");
        }, true));
        p.Children.Add(Cols(Card("Tarifas de envío (precio por libra)", null, rateRows.ToArray()), Card("Lo que te cuesta a ti la libra", null, buyRows.ToArray())));

        p.Children.Add(Card("Mensajes de WhatsApp", null, Muted("Puedes usar {nombre}, {total}, {pagado}, {saldo}, {libras} y {negocio}.", 13),
            Box("Aviso de llegada", _tArr, v => _tArr = v, true), Box("Recordatorio de pago", _tRem, v => _tRem = v, true), Box("Agradecimiento", _tTh, v => _tTh = v, true),
            Cols(Btn("Guardar mensajes", () => { Core.SaveTemplates(_tArr!, _tRem!, _tTh!); S.Toast("✅ Mensajes guardados."); }, true),
                Btn("Restaurar", () => S.Ask("Restaurar mensajes", "¿Volver a los mensajes originales?", "Restaurar", false, () => { Core.ResetTemplates(); _tArr = Core.Tpl("arrival"); _tRem = Core.Tpl("reminder"); _tTh = Core.Tpl("thanks"); Redraw(); })))));

        var cfg = Core.Notifs.Cfg();
        p.Children.Add(Cols(
            Card("Notificaciones", null, new[] { ("inapp", "Avisos dentro de la app"), ("pay", "Pagos"), ("order", "Encargos"), ("client", "Clientes"), ("system", "Sistema y seguridad"), ("important", "Avisos importantes") }
                .Select(o => (UIElement)Toggle(o.Item2, "", Js.Truthy(cfg[o.Item1]), _ => Core.Notifs.ToggleCfg(o.Item1))).ToArray()),
            Card("Sonido", null, Toggle("Sonido de inicio de sesión", "Un sonido corto y discreto al entrar correctamente", Sound.On(), on => { Sound.Set(on); if (on) Sound.Login(); })),
            Card("Apariencia", null, Toggle("Modo día", "Colores claros; el cambio es inmediato y se recuerda", Core.Ls.Get("encargos_aurora_mode", "") == "day", on =>
            {
                Core.Ls.Set("encargos_aurora_mode", on ? "day" : "night");
                Services.Post(() => S.ApplyTheme(on));
            }), Muted("Encargos para Windows · tus datos se guardan en este equipo y, si lo activas, cifrados en tu propio bot de Telegram.", 12))));
    }
}

/* ---------------- Sincronización con Telegram ---------------- */
public sealed class SyncPage : Screen
{
    public override string Title => "Sincronización";
    public override string Nav => "sync";
    string _tok = "", _code = "";

    public override void Build(StackPanel p)
    {
        var sync = Services.Sync; var cfg = sync.Cfg();
        if (cfg == null)
        {
            p.Children.Add(Card(T("Sincroniza con Telegram", 18, true),
                Muted("Tus datos viajan cifrados a tu propio bot de Telegram. Solo tus dispositivos tienen la llave para leerlos. En Telegram se guarda únicamente el último respaldo válido.", 13)));
            var tokBox = new PasswordBox { Header = "Token del bot" }; tokBox.PasswordChanged += (_, _) => _tok = tokBox.Password.Trim();
            var codeBox = new PasswordBox { Header = "Código de enlace (ENC1…)" }; codeBox.PasswordChanged += (_, _) => _code = codeBox.Password.Trim();
            p.Children.Add(Cols(
                Card("¿Es tu primer dispositivo?", null, Muted("1. En Telegram, abre @BotFather y crea un bot con /newbot.\n2. Abre tu bot nuevo y envíale /start.\n3. Copia el token que te dio BotFather y pégalo aquí.", 13),
                    tokBox, Btn("Conectar", Connect, true, null, "")),
                Card("¿Ya lo usas en otro dispositivo?", null, Muted("En el otro dispositivo ve a Sincronización → \"Código para enlazar\", cópialo y pégalo aquí.", 13),
                    codeBox, Btn("Enlazar", Link, true, null, ""))));
            return;
        }
        var st = sync.GetStatus(); var m = sync.MetaInfo(); var paused = Js.Truthy(cfg["paused"]);
        var lv = Js.NumberToString(Js.Num(m["localVersion"]));
        p.Children.Add(Cols(
            Card(T(st.Text, 18, true, st.Kind switch { "ok" => Green, "error" => Rose, "offline" or "pending" => Amber, _ => null }),
                sync.LastErr.Length > 0 && st.Kind != "ok" ? T(Services.Redact(sync.LastErr), 13, false, Rose) : null,
                Line("Cambios pendientes", sync.Pending().ToString()), Line("Última sincronización", AdminUtil.TimeLabel(m["lastSync"])),
                Line("Versión del respaldo", lv == "0" ? "—" : lv), Line("Este equipo", sync.DeviceId())),
            V(8, Btn("Sincronizar ahora", async () => { var ok = await S.BusySync("Sincronizando…", () => sync.SyncNowBlocking()); S.Toast(ok ? "✅ Sincronizado." : Services.Redact(sync.LastErr).IfEmpty("Sincronización en curso…")); Redraw(); }, true, null, ""),
                Btn("Respaldar ahora en Telegram", async () => { var ok = await S.BusySync("Subiendo respaldo…", () => sync.BackupNow()); S.Toast(ok ? "✅ Respaldo actualizado en Telegram." : Services.Redact(sync.LastErr).IfEmpty("No se pudo respaldar.")); Redraw(); }, false, null, ""),
                Btn("Crear respaldo local", () => S.Root(new BackupPage()), false, null, ""))));
        p.Children.Add(Card("Enlazar otro dispositivo", null, Muted("El código permite a otro teléfono o PC leer y escribir tus datos. Trátalo como una contraseña: no lo compartas con nadie.", 13),
            Btn("Mostrar código para enlazar", () => S.RequireAuth("Confirma tu identidad para ver el código", ShowCode, true), false, null, "")));
        p.Children.Add(Card("Opciones", null,
            Toggle("Pausar sincronización", "Los cambios se guardan aquí y se envían al reanudar", paused, v => { sync.Pause(v); Redraw(); }),
            Cols(Btn("Restaurar desde Telegram", () => S.Ask("Restaurar desde Telegram", "Se reemplazarán los datos de este equipo por el último respaldo de Telegram. Primero se comprueba que el respaldo esté completo. ¿Continuar?", "Restaurar", true,
                    () => S.RequireAuth("Confirma tu identidad", async () =>
                    {
                        var r = await S.BusySync("Restaurando…", () => sync.Restore());
                        S.Toast(r.Ok ? "✅ Datos restaurados desde Telegram." : "No se pudo restaurar: " + Services.Redact(r.Error)); Redraw();
                    }, true))),
                Btn("Diagnóstico", () => S.Go(new SyncDiagPage())),
                Btn("Desconectar este equipo", () => S.Ask("Desconectar", "Este equipo dejará de sincronizar. Tus datos locales y el respaldo de Telegram no se borran.", "Desconectar", true, () => { sync.Disconnect(); S.Toast("Sincronización desconectada."); Redraw(); }), false, Rose))));
    }

    async void Result(TelegramSync.ConnectResult r, string okMsg)
    {
        switch (r)
        {
            case TelegramSync.ConnectResult.Ok: _tok = _code = ""; S.Toast(okMsg); Redraw(); break;
            case TelegramSync.ConnectResult.NeedStart: S.Info("Falta un paso", T("Abre tu bot en Telegram y envíale /start. Después vuelve aquí y pulsa \"Conectar\" otra vez.")); break;
            case TelegramSync.ConnectResult.Error e: S.Toast(Services.Redact(e.Msg)); break;
            case TelegramSync.ConnectResult.Existing:
                {
                    var fresh = false;
                    await S.Dialog("Este bot ya tiene datos", T("Ya hay un respaldo de Encargos en este bot. Para ver esos datos aquí, usa el código de enlace de tu otro dispositivo.\n\nSi ya no tienes ese dispositivo, puedes empezar de nuevo con este bot: se creará un respaldo nuevo con los datos de este equipo. El respaldo anterior no se borra."),
                        "Usaré el código", null, null, "Empezar de nuevo", () => fresh = true);
                    _tok = "";
                    if (fresh)
                    {
                        var ok = await S.BusySync("Creando sincronización…", () => Services.Sync.FreshStart());
                        S.Toast(ok ? "✅ Sincronización nueva activada." : Services.Redact(Services.Sync.LastErr).IfEmpty("No se pudo activar.")); Redraw();
                    }
                    break;
                }
        }
    }
    async void Connect() { if (_tok.Length == 0) return; var t = _tok; var r = await S.BusySync("Conectando con Telegram…", () => Services.Sync.Connect(t)); Result(r, "✅ Sincronización activada."); }
    async void Link() { if (_code.Length == 0) return; var c = _code; var r = await S.BusySync("Enlazando y descargando…", () => Services.Sync.Link(c)); Result(r, "✅ Equipo enlazado."); }
    async void ShowCode()
    {
        var c = Services.Sync.LinkCode(); if (c == null) return;
        var box = new TextBox { Text = c, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"), FontSize = 11 };
        if (await S.Dialog("🔑 Código para enlazar", V(8, Muted("Cópialo y pégalo en el otro dispositivo. No lo envíes por chats ni lo publiques.", 13), box), "Copiar", null, "Cerrar"))
        {
            var dp = new DataPackage(); dp.SetText(c); Clipboard.SetContent(dp); S.Toast("Código copiado.");
        }
    }
}

public sealed class SyncDiagPage : Screen
{
    public override string Title => "Diagnóstico de sincronización";
    public override string Nav => "sync";

    public override void Build(StackPanel p)
    {
        var sync = Services.Sync; var m = sync.MetaInfo(); var cfg = sync.Cfg();
        string N(object? v) { var s = Js.NumberToString(Js.Num(v)); return s == "0" ? "—" : s; }
        p.Children.Add(Card("Estado", null,
            Line("Conectado", cfg == null ? "No" : Js.Truthy(cfg["paused"]) ? "En pausa" : "Sí"), Line("Estado", sync.State), Line("Pendientes", sync.Pending().ToString()),
            Line("Equipo", sync.DeviceId()), Line("Base de datos", cfg != null ? cfg.Str("db").Take(8) + "…" : "—"),
            Line("Versión local", Js.NumberToString(Js.Num(m["localVersion"]))), Line("Última sincronización", AdminUtil.TimeLabel(m["lastSync"])),
            Line("Mensaje del respaldo", N(m["backupMsg"])), Line("Mensaje fijado", N(m["manifestMsg"])),
            Line("Limpiezas pendientes", ((m["cleanup"] as JArr)?.Count ?? 0).ToString()), Line("Publicación en curso", m["inflight"] != null ? "Sí (se recupera sola)" : "No"),
            sync.LastErr.Length > 0 ? T("Último error: " + Services.Redact(sync.LastErr), 12, false, Rose) : null,
            Muted("El token y la llave de cifrado nunca se muestran aquí.", 12)));
        var conf = sync.Conflicts();
        if (conf.Count > 0)
            p.Children.Add(Card($"Conflictos resueltos ({conf.Count})", null, new UIElement[] { Muted("Cuando dos dispositivos cambian lo mismo, gana el cambio más reciente. Los pagos nunca se pierden.", 13) }
                .Concat(conf.Take(20).Select(c => (UIElement)T("• " + c.Str("entity") + " · ganó " + (c.Str("kept") == "local" ? "este equipo" : "otro dispositivo (" + c.Str("remoteDevice") + ")") + " · " + AdminUtil.TimeLabel(c["t"]), 12))).ToArray()));
        var log = sync.LogEntries().Take(60).ToList();
        p.Children.Add(Card("Registro", null, log.Count == 0 ? new UIElement[] { Muted("Sin registros.", 14) } :
            log.Select(e => (UIElement)V(0, T((e.Str("kind") switch { "err" => "⚠️ ", "in" => "⬇️ ", "out" => "⬆️ ", _ => "• " }) + Services.Redact(e.Str("text")), 12, false, e.Str("kind") == "err" ? Rose : null),
                Muted(AdminUtil.TimeLabel(e["t"]) + " · " + e.Str("dev"), 11))).ToArray()));
        p.Children.Add(Btn("Copiar diagnóstico (sin secretos)", () =>
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("Encargos para Windows · diagnóstico\n").Append($"Estado: {sync.State} · pendientes {sync.Pending()}\n").Append($"Versión local: {Js.NumberToString(Js.Num(m["localVersion"]))}\n")
              .Append($"Última sync: {AdminUtil.TimeLabel(m["lastSync"])}\n");
            if (sync.LastErr.Length > 0) sb.Append("Error: " + Services.Redact(sync.LastErr) + "\n");
            foreach (var e in sync.LogEntries().Take(30)) sb.Append(AdminUtil.TimeLabel(e["t"]) + " " + e.Str("kind") + " " + Services.Redact(e.Str("text")) + "\n");
            var dp = new DataPackage(); dp.SetText(sb.ToString()); Clipboard.SetContent(dp); S.Toast("Diagnóstico copiado.");
        }));
    }
}
