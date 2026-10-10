using System.Text.RegularExpressions;

namespace Encargos.Core;

/// <summary>
/// Seguridad de las apps nativas: sin contraseña por defecto; contraseña y PIN con PBKDF2-SHA256 + sal + un secreto
/// del equipo (protegido con DPAPI), esperas progresivas tras intentos fallidos, registro, bloqueo automático,
/// privacidad de pantalla y confirmación para acciones sensibles.
/// </summary>
public sealed class Security
{
    readonly EncargosApp _app; readonly byte[] _deviceSecret;
    LS Ls => _app.Ls;
    const string K_AUTH = "encargos_native_auth", K_PIN = "encargos_native_pin", K_ON = "encargos_pin_on", K_BIO = "encargos_bio_on",
        K_FAILS = "encargos_sec_fails", K_LOG = "encargos_sec_log", K_SENS = "encargos_sec_sensitive", K_PRIV = "encargos_privacy";
    const int ITER_PW = 120000, ITER_PIN = 60000;
    public long RecentAuth { get; private set; }
    /// <summary>Nombre de la biometría en esta plataforma ("Huella" en Android, "Windows Hello" en PC).</summary>
    public string BioName = "Windows Hello";

    public Security(EncargosApp app, byte[] deviceSecret) { _app = app; _deviceSecret = deviceSecret; }

    string Derive(string secret, byte[] salt, int it) => Crypto.Hex(Crypto.Pbkdf2(secret, salt.Concat(_deviceSecret).ToArray(), it));

    /* ---------- usuario y contraseña ---------- */
    public bool HasCredentials() => Ls.JsonOf(K_AUTH) is JObj;
    public string UserName() => Ls.Obj(K_AUTH).Str("user");
    public Res<bool> SetCredentials(string user, string password)
    {
        if (user.Trim().Length < 3) return Res.Fail("El usuario debe tener al menos 3 caracteres.");
        if (password.Length < 6) return Res.Fail("La contraseña debe tener al menos 6 caracteres.");
        var salt = Crypto.Random(16);
        Ls.Put(K_AUTH, JObj.Of(("user", user.Trim()), ("salt", Crypto.B64(salt)), ("it", (double)ITER_PW), ("h", Derive(password, salt, ITER_PW))));
        Log(true, "Usuario y contraseña actualizados"); return Res.Ok();
    }
    public bool CheckPassword(string user, string password)
    {
        var a = Ls.Obj(K_AUTH); if (a.IsEmpty) return false;
        var okUser = user.Trim().ToLowerInvariant() == a.Str("user").ToLowerInvariant();
        var h = Derive(password, Crypto.UnB64(a.Str("salt")), (int)Js.Num(a["it"]));
        return okUser && Crypto.CtEq(h, a.Str("h"));
    }

    /* ---------- PIN ---------- */
    public bool HasPin() => Ls.JsonOf(K_PIN) is JObj;
    public bool PinOn() => HasPin() && Ls.Get(K_ON, "") == "on";
    public int PinLen() { var n = (int)Js.Num(Ls.Obj(K_PIN)["len"]); return n is >= 4 and <= 6 ? n : 4; }
    public Res<bool> SetPin(string pin)
    {
        if (!Regex.IsMatch(pin, "^[0-9]{4,6}$")) return Res.Fail("El PIN debe tener entre 4 y 6 números.");
        var salt = Crypto.Random(16); var had = HasPin();
        Ls.Put(K_PIN, JObj.Of(("salt", Crypto.B64(salt)), ("it", (double)ITER_PIN), ("h", Derive(pin, salt, ITER_PIN)), ("len", (double)pin.Length))); Ls.Set(K_ON, "on");
        Log(true, had ? "PIN cambiado" : "PIN creado"); _app.Notifs.SysEvents(); return Res.Ok();
    }
    public bool VerifyPin(string pin) { var v = Ls.Obj(K_PIN); if (v.IsEmpty) return false; return Crypto.CtEq(Derive(pin, Crypto.UnB64(v.Str("salt")), (int)Js.Num(v["it"])), v.Str("h")); }
    public void DisablePin() { Ls.Del(K_PIN); Ls.Set(K_ON, "off"); Ls.Set(K_BIO, "off"); Log(true, "PIN desactivado"); _app.Notifs.SysEvents(); }

    /* ---------- biometría ---------- */
    public bool BioOn() => Ls.Get(K_BIO, "") == "on" && PinOn();
    public void SetBio(bool on) { Ls.Set(K_BIO, on ? "on" : "off"); Log(true, BioName + (on ? " activado" : " desactivado")); _app.Notifs.SysEvents(); }

    /* ---------- intentos fallidos: espera progresiva, nunca bloqueo permanente ---------- */
    public long WaitMs() { var s = Ls.Obj(K_FAILS); return Math.Max(0L, (long)Js.Num(s["until"]) - Js.NowMs()); }
    public string Fail(string kind)
    {
        var s = Ls.Obj(K_FAILS); var n = (int)Js.Num(s["n"]) + 1;
        long w = n >= 12 ? 15 * 60000L : n >= 10 ? 5 * 60000L : n >= 8 ? 2 * 60000L : n >= 5 ? 30000L : 0L;
        s["n"] = (double)n; if (w > 0) s["until"] = (double)(Js.NowMs() + w); Ls.Put(K_FAILS, s);
        Log(false, "Intento de acceso incorrecto" + kind switch { "pin" => " (PIN)", "password" => " (contraseña)", _ => "" });
        return w > 0 ? "Demasiados intentos. Espera " + (w >= 60000 ? $"{w / 60000} min" : $"{w / 1000} s") + "." : kind == "pin" ? "PIN incorrecto." : "Usuario o contraseña incorrectos.";
    }
    public void Ok(string method)
    {
        Ls.Put(K_FAILS, JObj.Of(("n", 0.0), ("until", 0.0))); RecentAuth = Js.NowMs(); Touch();
        Log(true, "Aplicación desbloqueada" + method switch { "pin" => " con PIN", "bio" => " con " + BioName, "password" => " con contraseña", _ => "" });
    }

    /* ---------- bloqueo automático ---------- */
    public sealed record LockCfg(bool On, double Minutes, bool OnOpen);
    public LockCfg GetLockCfg()
    {
        var on = Ls.Get("encargos_lock_enabled", null) != "off";
        var raw = Ls.Get("encargos_lock_minutes", null);
        var min = double.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var m) ? m : 5.0; if (!(min >= 0)) min = 5.0;
        return new LockCfg(on, min, Ls.Get("encargos_lock_on_open", null) != "off");
    }
    public void SetLock(double? minutes)
    {
        if (minutes == null) { Ls.Set("encargos_lock_enabled", "off"); Log(true, "Bloqueo automático: nunca"); }
        else { Ls.Set("encargos_lock_enabled", "on"); Ls.Set("encargos_lock_minutes", Js.NumberToString(minutes.Value)); Log(true, "Bloqueo automático: " + LockLabel(minutes.Value)); }
        _app.Notifs.SysEvents(); _app.Changed(false);
    }
    public static string LockLabel(double m) => m == 0.0 ? "Inmediatamente" : m < 1 ? $"{Math.Round(m * 60)} segundos" : m == 1.0 ? "1 minuto" : $"{Js.NumberToString(m)} minutos";
    public void SetLockOnOpen(bool on) { Ls.Set("encargos_lock_on_open", on ? "on" : "off"); _app.Changed(false); }
    public bool ShouldLockAfter(long hiddenMs) { var c = GetLockCfg(); return c.On && hiddenMs > c.Minutes * 60000; }
    public void Touch() => Ls.Set("encargos_sec_last", Js.NumberToString(Js.NowMs()));

    /* ---------- privacidad y acciones sensibles ---------- */
    public bool PrivacyOn() => Ls.Get(K_PRIV, "on") != "off";
    public void SetPrivacy(bool on) { Ls.Set(K_PRIV, on ? "on" : "off"); Log(true, on ? "Privacidad de pantalla activada" : "Privacidad de pantalla desactivada"); _app.Notifs.SysEvents(); _app.Changed(false); }
    public bool SensitiveOn() => Ls.Get(K_SENS, "on") != "off";
    public void SetSensitive(bool on) { Ls.Set(K_SENS, on ? "on" : "off"); Log(true, "Protección de acciones sensibles " + (on ? "activada" : "desactivada")); _app.Notifs.SysEvents(); _app.Changed(false); }
    /// <summary>true si hay que pedir PIN/contraseña antes de una acción sensible.</summary>
    public bool NeedsAuth(bool force = false) => force || (SensitiveOn() && Js.NowMs() - RecentAuth >= 120000);
    public void Forget() => RecentAuth = 0;

    /* ---------- registro ---------- */
    public void Log(bool ok, string text) { var a = Ls.Arr(K_LOG); a.Insert(0, JObj.Of(("t", (double)Js.NowMs()), ("ok", ok), ("text", text))); Ls.Put(K_LOG, new JArr(a.Take(80))); }
    public List<JObj> LogEntries() => Ls.Arr(K_LOG).Objs();

    /// <summary>Borrar todo: requiere escribir ELIMINAR en la interfaz.</summary>
    public void Wipe(bool resetAccess)
    {
        _app.Data.Clear(); _app.Persist();
        foreach (var k in new[] { "encargos_notes", "encargos_expenses", "encargos_client_info", "encargos_activity", "encargos_attn_reviewed", "encargos_attn_last" }) Ls.Del(k);
        if (resetAccess) { Ls.Del(K_AUTH); Ls.Del(K_PIN); Ls.Set(K_ON, "off"); Ls.Set(K_BIO, "off"); Ls.Put(K_FAILS, JObj.Of(("n", 0.0), ("until", 0.0))); }
        Log(true, resetAccess ? "App restablecida (datos y acceso)" : "Todos los datos eliminados"); _app.Notifs.SysEvents(); _app.Reload();
    }
}
