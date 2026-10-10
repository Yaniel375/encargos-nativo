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
        var mode = bk.AutoMode;
        var counts = new[] { ("Encargos", Core.Orders().Count), ("Clientes", Core.Clients().Count), ("Pagos", Core.Orders().Sum(o => Core.Payments(o).Count)), ("Notas", Core.Notes().Count) };
        var head = H("Tus datos están guardados", 17);
        p.Children.Add(Card(Lead(IconTile("\uE83D", 48).Also(t => ((FontIcon)t.Child).Foreground = Green), V(2, head, Muted("Se guardan solos en este equipo", 13))),
            Cols(counts.Select(c => (UIElement)Kpi(c.Item1, c.Item2.ToString())).ToArray())));
        void ExportJson() => S.RequireAuth("Confirma tu identidad para exportar", async () =>
        {
            var s = bk.ExportJson(); if (await Media.SaveFile(Backup.FileName(), ".json", "Respaldo de Encargos", Crypto.Utf8(s))) S.Toast("✅ Respaldo guardado.");
        });
        var autoCard = IconCard("\uE895", "Respaldo automático", null,
            Choice(new[] { ("off", "Desactivado"), ("daily", "Cada día"), ("weekly", "Cada semana") }, mode, k => { bk.AutoMode = k; Redraw(); }),
            Muted(mode == "off" ? "No se guardarán copias automáticas." : "Al abrir la app se guarda una copia en Documentos\\Encargos " + (mode == "weekly" ? "una vez por semana." : "una vez al día.") +
                (bk.LastAuto > 0 ? " Última: " + Js.Fmt(bk.LastAuto) : " Todavía no se ha hecho ningún respaldo automático."), 13),
            Cols(Btn("Hacer copia automática ahora", () =>
            {
                var payload = bk.AutoDue(true); if (payload == null) return;
                try { File.WriteAllText(Path.Combine(Services.DocsDir(), "respaldo_auto_" + Js.NowIso()[..10] + ".json"), payload); bk.AutoDone(); S.Toast("✅ Copia guardada en Documentos\\Encargos."); }
                catch (Exception e) { Services.Report(e); S.Toast("No se pudo guardar la copia."); }
            }), Btn("Abrir carpeta de copias", async () => await Windows.System.Launcher.LaunchFolderPathAsync(Services.DocsDir()))));
        var lastRow = new Border { Child = NavRow("\uE787", "Último respaldo", last > 0 ? Js.Fmt(last) : "Aún no has hecho ningún respaldo · Te recomendamos hacer uno.", null, tint: Amber), CornerRadius = new CornerRadius(18), Background = Glass, BorderBrush = LineBr, BorderThickness = new Thickness(1) };
        var actions = V(12,
            ActionCard("\uE896", "Respaldar datos", "Guarda un archivo con todos tus encargos, clientes, pagos, notas y gastos.", ExportJson, true),
            ActionCard("\uE72E", "Respaldo cifrado", "Protegido con una contraseña que tú eliges. Sin ella no se puede abrir.", () => S.RequireAuth("Confirma tu identidad para exportar", async () =>
            {
                var pw = await AdminUtil.AskPassword("🔒 Respaldo cifrado", "Elige una contraseña para este archivo. Si la olvidas, no se puede recuperar.", true, "Cifrar y guardar");
                if (pw == null) return;
                var s = await S.Busy("Cifrando…", () => bk.ExportEncrypted(pw));
                if (await Media.SaveFile(Backup.FileName("respaldo_encargos_cifrado_"), ".json", "Respaldo cifrado de Encargos", Crypto.Utf8(s))) S.Toast("✅ Respaldo cifrado guardado.");
            })),
            ActionCard("\uE898", "Importar respaldo", "Recupera tus datos desde un archivo .json normal o cifrado (web, Android o PC). Reemplaza los datos actuales.", Import),
            ActionCard("\uE9F9", "Exportar a Excel (.csv)", "Una hoja con todos los encargos para abrir en Excel.", () => S.RequireAuth("Confirma tu identidad para exportar", async () =>
            {
                if (await Media.SaveFile(Backup.FileName("encargos_", ".csv"), ".csv", "CSV para Excel", Crypto.Utf8(bk.Csv()))) S.Toast("✅ Archivo CSV guardado.");
            })));
        var tips = IconCard("\uE82F", "Consejos", null,
            Lead(new FontIcon { Glyph = "\uE895", FontSize = 14, Foreground = Mut }, T("Haz un respaldo cada semana o después de muchos cambios.", 13)),
            Lead(new FontIcon { Glyph = "\uE715", FontSize = 14, Foreground = Mut }, T("Envía el archivo a tu correo, a una memoria USB o a la nube, por si pierdes el equipo.", 13)),
            Lead(new FontIcon { Glyph = "\uE898", FontSize = 14, Foreground = Mut }, T("Para recuperar todo en otro equipo o teléfono, usa Importar respaldo.", 13)),
            Lead(new FontIcon { Glyph = "\uE72E", FontSize = 14, Foreground = Mut }, T("El respaldo cifrado solo se abre con su contraseña. Si la olvidas, no se puede recuperar.", 13)));
        p.Children.Add(S.ContentWidth < 760 ? V(12, autoCard, lastRow, actions, tips) : Cols(V(12, autoCard, lastRow, tips), actions));
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
    public override string? Sub => "Protege la información de tu negocio.";
    bool? _hello;

    public override void Build(StackPanel p)
    {
        var sec = Services.Security; var lk = sec.GetLockCfg();
        if (_hello == null) { _ = CheckHello(); }
        var score = new[] { sec.PinOn(), lk.On, sec.SensitiveOn() }.Count(x => x);
        var (lvl, lvlC) = score == 3 ? ("Protección completa", Green) : score == 0 ? ("Sin protección", Rose) : ("Protección parcial", Amber);
        var lvlT = H(lvl, 19);
        var status = Card(Lead(Dot(lvlC, 12), lvlT),
            StatRow("PIN", sec.PinOn() ? $"Activo · {sec.PinLen()} dígitos" : "Desactivado", sec.PinOn() ? Green : null),
            StatRow("Windows Hello", _hello == false ? "No disponible" : _hello == null ? "Comprobando…" : sec.BioOn() ? "Activo" : "Desactivado"),
            StatRow("Bloqueo automático", lk.On ? Security.LockLabel(lk.Minutes) : "Nunca"),
            StatRow("Acciones sensibles", sec.SensitiveOn() ? "✓ Protegidas" : "Sin proteger", sec.SensitiveOn() ? Green : Amber),
            StatRow("Última actividad", sec.LogEntries().FirstOrDefault() is JObj le ? AdminUtil.TimeLabel(le["t"]) : "—"));
        var access = Group("Acceso",
            NavRow("\uE8D7", "Bloqueo con PIN", sec.PinOn() ? $"Activo · {sec.PinLen()} dígitos" : "Crea un PIN de 4 a 6 números", null, null, Switch(sec.PinOn(), on =>
                S.RequireAuth("Confirma tu identidad", () => { if (on) PinDialog(); else { sec.DisablePin(); S.Toast("PIN desactivado."); } Redraw(); }, true))),
            sec.PinOn() ? NavRow("\uE8D7", "Cambiar PIN", "Elige un PIN nuevo", () => S.RequireAuth("Confirma tu identidad", PinDialog, true)) : null,
            sec.PinOn() && _hello == true ? NavRow("\uE928", "Desbloquear con Windows Hello", "Huella, rostro o PIN de Windows", null, null, Switch(sec.BioOn(), async on =>
            {
                if (on) { if (await Native.Hello(S.Hwnd, "Activar Windows Hello en Encargos")) { sec.SetBio(true); S.Toast("✅ Windows Hello activado."); } }
                else sec.SetBio(false);
                Redraw();
            })) : null,
            NavRow("\uE8AD", "Pedir desbloqueo al abrir", "Siempre que abres la app", null, null, Switch(lk.OnOpen, v => sec.SetLockOnOpen(v))),
            NavRow("\uE823", "Bloqueo automático", lk.On ? "Después de " + Security.LockLabel(lk.Minutes).ToLowerInvariant() + " fuera de la app" : "La app no se bloqueará sola", LockSheet, lk.On ? Security.LockLabel(lk.Minutes) : "Nunca"),
            NavRow("\uE77B", "Usuario y contraseña", $"Usuario: {sec.UserName().IfEmpty("—")} · acceso de respaldo si olvidas el PIN", () => S.RequireAuth("Confirma tu identidad", Credentials, true)));
        var data = Group("Protección de datos",
            NavRow("\uE7BA", "Proteger acciones sensibles", "Eliminar, importar, exportar y cambios críticos", null, null, Switch(sec.SensitiveOn(), v =>
            {
                if (v) { sec.SetSensitive(true); Redraw(); } else S.RequireAuth("Confirma tu identidad", () => { sec.SetSensitive(false); Redraw(); }, true);
            })),
            NavRow("\uED1A", "Ocultar en capturas de pantalla", "Excluye la ventana de capturas, grabaciones y pantallas compartidas", null, null, Switch(sec.PrivacyOn(), v => { sec.SetPrivacy(v); S.ApplyPrivacy(); })));
        var log = sec.LogEntries().Take(15).ToList();
        var activity = IconCard("\uE81C", "Actividad de seguridad", null, log.Count == 0 ? new UIElement[] { Muted("Sin actividad registrada.", 14) } :
            log.Select(e => (UIElement)Lead(T(Js.Truthy(e["ok"]) ? "✅" : "⚠️"), V(0, T(e.Str("text"), 13), Muted(AdminUtil.TimeLabel(e["t"]), 11)))).ToArray());
        var danger = Group("Zona de peligro", NavRow("\uE74D", "Eliminar todos los datos", "Requiere confirmar tu identidad y escribir ELIMINAR", () => S.RequireAuth("Confirma tu identidad", Wipe, true), null, null, Rose, Rose));
        var foot = Muted("El PIN se guarda con sal y hash iterado; Windows Hello no comparte tu huella con la app. Ningún respaldo incluye tu PIN ni tu contraseña.", 12);
        p.Children.Add(S.ContentWidth < 760 ? V(14, status, access, data, activity, danger, foot) : Cols(V(14, status, access, data), V(14, activity, danger, foot)));
    }
    async void LockSheet()
    {
        var sec = Services.Security; var lk = sec.GetLockCfg();
        var opts = new[] { ("0", "Al salir"), ("1", "1 min"), ("5", "5 min"), ("15", "15 min"), ("30", "30 min"), ("never", "Nunca") };
        var sel = !lk.On ? "never" : Js.NumberToString(lk.Minutes);
        await S.Dialog("Bloqueo automático", V(10, Muted("Se pedirá tu PIN o contraseña cuando pase este tiempo fuera de la app.", 13),
            Choice(opts, sel, k => sec.SetLock(k == "never" ? (double?)null : double.Parse(k, System.Globalization.CultureInfo.InvariantCulture)))), "Listo", null, null);
        Redraw();
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

/* ---------------- Ajustes (grupos de filas que abren hojas, como la referencia) ---------------- */
public sealed class SettingsPage : Screen
{
    public override string Title => "Ajustes";
    public override string Nav => "settings";
    public override bool Live => false;

    public override void Build(StackPanel p)
    {
        var keys = Core.ShippingRates.Keys.ToList();
        string RateLine(Func<string, object?> get) => string.Join(" · ", keys.Select(k => Core.ShippingName(k) + " " + Money(get(k))));
        var buyR = Core.BuyRates();
        var nm = H(Core.GreetName.IfEmpty("Tu nombre"), 19);
        var hasPhoto = Media.ProfilePhoto.Length > 0;
        var pic = Click(Media.ProfilePic(Core.GreetName.IfEmpty("E"), 64), ChangePhoto); pic.CornerRadius = new CornerRadius(20); pic.HorizontalAlignment = HorizontalAlignment.Left;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(pic, "Cambiar foto de perfil");
        p.Children.Add(Card(Lead(pic, V(2, nm, Muted("Usuario: " + Services.Security.UserName().IfEmpty("—"), 13))),
            Cols(Btn(hasPhoto ? "Cambiar foto" : "Agregar foto", ChangePhoto, false, null, "\uE722"),
                hasPhoto ? Btn("Quitar foto", () => S.Ask("Quitar foto de perfil", "¿Quitar tu foto de perfil? Volverá a mostrarse tu inicial.", "Quitar", true, () => { Core.Ls.Set(Media.ProfileKey, ""); S.RefreshProfile(); S.Toast("Foto de perfil quitada."); Redraw(); }), false, Rose, "\uE74D") : null,
                Btn("Editar nombre del saludo", Greet, false, null, "\uE76E"))));
        var cfg = Core.Notifs.Cfg(); var on = new[] { "inapp", "pay", "order", "client", "system", "important" }.Count(k => Js.Truthy(cfg[k]));
        var day = Core.Ls.Get("encargos_aurora_mode", "") == "day";
        var sec = Services.Security;
        var left = V(14,
            Group("Negocio y tarifas",
                NavRow("\uE719", "Nombre del negocio", Core.BusinessName.IfEmpty("Sin nombre"), Biz),
                NavRow("\uE806", "Precio por libra (lo que cobras)", RateLine(k => Core.ShippingRates[k]), Rates),
                NavRow("\uE8EC", "Costo por libra (lo que te cobran)", RateLine(k => buyR[k]), Buy)),
            Group("WhatsApp",
                NavRow("\uE8BD", "Mensajes de WhatsApp", "Llegada, recordatorio de pago y agradecimiento", Templates),
                NavRow("\uE717", "Código de país", "Se agrega a los números cortos", CountryCode, "+" + Core.PhoneCC)),
            Group("Notificaciones y alertas",
                NavRow("\uEA8F", "Notificaciones", $"Avisos y categorías · {on} de 6 activas", Notifs),
                NavRow("\uE806", "Alertas de tránsito", $"Avisar a los {Core.TransitWarn} días · retrasado a los {Core.TransitLate}", Transit),
                NavRow("\uE767", "Sonido", Sound.On() ? "Sonido al entrar" : "Sin sonido", null, null, Switch(Sound.On(), v => { Sound.Set(v); if (v) Sound.Login(); }))));
        var right = V(14,
            Group("Apariencia",
                NavRow("\uE740", "Adaptar a la pantalla", MainWindow.FitAuto() ? $"Automático: todo crece o se reduce según la resolución y el tamaño de la ventana · ahora {Math.Round(S.Fit * 100)} %" : "Desactivado: tamaño normal en cualquier ventana", null, null,
                    Switch(MainWindow.FitAuto(), v => { Core.Ls.Set("encargos_win_fit", v ? "auto" : "off"); S.RefreshFit(); Redraw(); })),
                NavRow("\uE8D4", "Nombre del saludo", $"\"¡Hola, {Core.GreetName.IfEmpty("…")}!\"", Greet),
                NavRow("\uE708", "Modo noche", !day ? "Encendido · fondo oscuro" : "Apagado · colores claros", null, null, Switch(!day, v =>
                {
                    Core.Ls.Set("encargos_aurora_mode", v ? "night" : "day");
                    Services.Post(() => S.ApplyTheme(!v));
                }))),
            Group("Datos y seguridad",
                NavRow("\uE72E", "Seguridad", (sec.PinOn() ? "PIN activo" : "Sin PIN") + " · bloqueo y privacidad", () => S.Go(new SecurityPage())),
                NavRow("\uE74E", "Respaldo e importación", "Respaldos, respaldo cifrado, importar y Excel", () => S.Go(new BackupPage())),
                NavRow("\uE895", "Sincronización (Telegram)", Services.Sync.GetStatus().Text, () => S.Go(new SyncPage()))),
            Muted("Encargos para Windows · tus datos se guardan en este equipo y, si lo activas, cifrados en tu propio bot de Telegram.", 12));
        p.Children.Add(S.ContentWidth < 760 ? V(14, left, right) : Cols(left, right));
    }

    async void Biz()
    {
        var v = Core.BusinessName;
        if (await S.Dialog("Nombre del negocio", V(10, InfoBox("Aparece en los recibos y en los mensajes de WhatsApp."), Box("Nombre del negocio (aparece en recibos)", v, x => v = x)), "Guardar"))
        { Core.BusinessName = v.Trim(); S.Toast("✅ Datos guardados."); Redraw(); }
    }
    /// <summary>Foto de perfil: se guarda solo en este equipo.</summary>
    async void ChangePhoto()
    {
        if (await Media.PickProfile()) { S.RefreshProfile(); S.Toast("✅ Foto de perfil actualizada."); Redraw(); }
    }
    async void Greet()
    {
        var v = Core.GreetName;
        if (await S.Dialog("Nombre del saludo", Box("Tu nombre (para el saludo)", v, x => v = x), "Guardar")) { Core.GreetName = v.Trim(); S.RefreshProfile(); S.Toast("✅ Datos guardados."); Redraw(); }
    }
    async void CountryCode()
    {
        var v = Core.PhoneCC;
        if (await S.Dialog("Código de país", V(10, InfoBox("Se agrega a los números de 8 cifras al abrir WhatsApp."), Box("Código de país para WhatsApp (sin +)", v, x => v = x, false, null, 4)), "Guardar"))
        { Core.PhoneCC = new string(v.Where(char.IsDigit).ToArray()).IfEmpty("53"); S.Toast("✅ Datos guardados."); Redraw(); }
    }
    async void Rates()
    {
        var keys = Core.ShippingRates.Keys.ToList();
        var rates = keys.ToDictionary(k => k, k => Js.NumberToString(Js.Num(Core.ShippingRates[k])));
        string newName = "", newPrice = "";
        var rows = new List<UIElement> { InfoBox("Configura todos tus tipos de envío. Cada tipo puede tener su propio precio por libra. Al crear un encargo, el precio se selecciona automáticamente según el envío.") };
        foreach (var k in keys)
        {
            var key = k;
            rows.Add(Row(Box(Core.ShippingLabel(k) + " · USD/lb", rates[k], v => rates[key] = v),
                k is "avion" or "barco" ? null : Btn("Quitar", () => S.Ask("Eliminar tarifa", $"¿Eliminar la tarifa \"{Core.ShippingLabel(key)}\"? Los encargos que ya la usan no cambian.", "Eliminar", true, () => { Core.RemoveShippingRate(key); Redraw(); }), false, Rose, null, false)));
        }
        rows.Add(T("Agregar otro tipo de envío", 14, true));
        rows.Add(Cols(Box("Nombre del envío (ej. Express)", "", v => newName = v), Box("Precio · USD/lb", "", v => newPrice = v)));
        var ok = await S.Dialog("Precios de envío por libra", V(10, rows.ToArray()), "Guardar precios", () =>
        {
            var m = new Dictionary<string, double>(); var bad = false;
            foreach (var k in keys) { var n = Num(rates.GetValueOrDefault(k, "")); if (double.IsNaN(n) || n < 0 || rates.GetValueOrDefault(k, "").Trim().Length == 0) bad = true; else m[k] = n; }
            if (bad) { S.Toast("Revisa las tarifas: deben ser números de 0 en adelante."); return false; }
            if (newName.Trim().Length > 0 || newPrice.Trim().Length > 0)
            {
                var pr = Num(newPrice);
                if (newName.Trim().Length == 0 || double.IsNaN(pr) || pr < 0 || newPrice.Trim().Length == 0) { S.Toast("Escribe un nombre y un precio válido."); return false; }
                if (Core.AddShippingRate(newName, pr) == null) { S.Toast("No se pudo agregar la tarifa."); return false; }
            }
            Core.SetShippingRates(m); return true;
        });
        if (ok) { S.Toast("✅ Tarifas guardadas. Se aplican a los encargos nuevos."); Redraw(); }
    }
    async void Buy()
    {
        var keys = Core.ShippingRates.Keys.ToList(); var buyR = Core.BuyRates();
        var buy = keys.ToDictionary(k => k, k => Js.NumberToString(Js.Num(buyR[k]))); var applyActive = false;
        var rows = new List<UIElement> { InfoBox("Se usa para calcular tu ganancia por encargo.") };
        foreach (var k in keys) { var key = k; rows.Add(Box(Core.ShippingLabel(k) + " · USD/lb", buy[k], v => buy[key] = v)); }
        rows.Add(Toggle("Aplicar también a encargos activos", "Actualiza el costo en los encargos de clientes que aún no terminan", false, v => applyActive = v));
        if (await S.Dialog("Lo que te cuesta a ti la libra", V(10, rows.ToArray()), "Guardar costos"))
        {
            var m = new Dictionary<string, double>(); foreach (var k in keys) { var n = Num(buy.GetValueOrDefault(k, "")); if (!double.IsNaN(n) && n >= 0) m[k] = n; }
            Core.SaveBuyRates(m, applyActive); S.Toast("✅ Costos guardados."); Redraw();
        }
    }
    async void Templates()
    {
        string a = Core.Tpl("arrival"), r = Core.Tpl("reminder"), t = Core.Tpl("thanks");
        var restore = false;
        var ok = await S.Dialog("Mensajes de WhatsApp", V(10, InfoBox("Puedes usar {nombre}, {total}, {pagado}, {saldo}, {libras} y {negocio}."),
            Box("Aviso de llegada", a, v => a = v, true), Box("Recordatorio de pago", r, v => r = v, true), Box("Agradecimiento", t, v => t = v, true)), "Guardar mensajes", null, "Cancelar", "Restaurar", () => restore = true);
        if (ok) { Core.SaveTemplates(a, r, t); S.Toast("✅ Mensajes guardados."); }
        else if (restore) S.Ask("Restaurar mensajes", "¿Volver a los mensajes originales?", "Restaurar", false, () => { Core.ResetTemplates(); S.Toast("✅ Mensajes restaurados."); });
    }
    async void Transit()
    {
        string w = Core.TransitWarn.ToString(), l = Core.TransitLate.ToString();
        if (await S.Dialog("Alertas de tránsito", V(10, InfoBox("El centro de atención te avisa cuando un encargo lleva muchos días en tránsito."), Cols(Box("Avisar a los (días)", w, v => w = v), Box("Retrasado a los (días)", l, v => l = v))), "Guardar alertas"))
        { Core.SaveTransitAlerts(int.TryParse(w, out var a) ? a : Core.TransitWarn, int.TryParse(l, out var b) ? b : Core.TransitLate); S.Toast("✅ Alertas guardadas."); Redraw(); }
    }
    async void Notifs()
    {
        var cfg = Core.Notifs.Cfg();
        await S.Dialog("Notificaciones", V(6, new[] { ("inapp", "Avisos dentro de la app"), ("pay", "Pagos"), ("order", "Encargos"), ("client", "Clientes"), ("system", "Sistema y seguridad"), ("important", "Avisos importantes") }
            .Select(o => (UIElement)Toggle(o.Item2, "", Js.Truthy(cfg[o.Item1]), _ => Core.Notifs.ToggleCfg(o.Item1))).ToArray()), "Cerrar", null, null);
        Redraw();
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
                IconCard("\uE8EA", "¿Es tu primer dispositivo?", null, Muted("1. En Telegram, abre @BotFather y crea un bot con /newbot.\n2. Abre tu bot nuevo y envíale /start.\n3. Copia el token que te dio BotFather y pégalo aquí.", 13),
                    tokBox, Btn("Conectar", Connect, true, null, "")),
                IconCard("\uE772", "¿Ya lo usas en otro dispositivo?", null, Muted("En el otro dispositivo ve a Sincronización → \"Código para enlazar\", cópialo y pégalo aquí.", 13),
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
        p.Children.Add(IconCard("\uE71B", "Enlazar otro dispositivo", null, Muted("El código permite a otro teléfono o PC leer y escribir tus datos. Trátalo como una contraseña: no lo compartas con nadie.", 13),
            Btn("Mostrar código para enlazar", () => S.RequireAuth("Confirma tu identidad para ver el código", ShowCode, true), false, null, "")));
        p.Children.Add(IconCard("\uE713", "Opciones", null,
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
        p.Children.Add(IconCard("\uE946", "Estado", null,
            Line("Conectado", cfg == null ? "No" : Js.Truthy(cfg["paused"]) ? "En pausa" : "Sí"), Line("Estado", sync.State), Line("Pendientes", sync.Pending().ToString()),
            Line("Equipo", sync.DeviceId()), Line("Base de datos", cfg != null ? cfg.Str("db").Take(8) + "…" : "—"),
            Line("Versión local", Js.NumberToString(Js.Num(m["localVersion"]))), Line("Última sincronización", AdminUtil.TimeLabel(m["lastSync"])),
            Line("Mensaje del respaldo", N(m["backupMsg"])), Line("Mensaje fijado", N(m["manifestMsg"])),
            Line("Limpiezas pendientes", ((m["cleanup"] as JArr)?.Count ?? 0).ToString()), Line("Publicación en curso", m["inflight"] != null ? "Sí (se recupera sola)" : "No"),
            sync.LastErr.Length > 0 ? T("Último error: " + Services.Redact(sync.LastErr), 12, false, Rose) : null,
            Muted("El token y la llave de cifrado nunca se muestran aquí.", 12)));
        var conf = sync.Conflicts();
        if (conf.Count > 0)
            p.Children.Add(IconCard("\uE895", $"Conflictos resueltos ({conf.Count})", null, new UIElement[] { Muted("Cuando dos dispositivos cambian lo mismo, gana el cambio más reciente. Los pagos nunca se pierden.", 13) }
                .Concat(conf.Take(20).Select(c => (UIElement)T("• " + c.Str("entity") + " · ganó " + (c.Str("kept") == "local" ? "este equipo" : "otro dispositivo (" + c.Str("remoteDevice") + ")") + " · " + AdminUtil.TimeLabel(c["t"]), 12))).ToArray()));
        var log = sync.LogEntries().Take(60).ToList();
        p.Children.Add(IconCard("\uE8A5", "Registro", null, log.Count == 0 ? new UIElement[] { Muted("Sin registros.", 14) } :
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
