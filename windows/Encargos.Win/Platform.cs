using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Encargos.Core;
using Microsoft.Data.Sqlite;
using Microsoft.UI.Dispatching;

namespace Encargos.Win;

/// <summary>Almacenamiento clave/valor en SQLite (mismas claves que la app web). Lecturas desde memoria, escrituras en transacción.</summary>
public sealed class SqliteStorage : IStorage
{
    readonly SqliteConnection _db; readonly Dictionary<string, string> _cache = new(); readonly object _lock = new();
    public SqliteStorage(string path)
    {
        _db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadWriteCreate }.ToString());
        _db.Open();
        Exec("PRAGMA journal_mode=WAL; PRAGMA synchronous=FULL; CREATE TABLE IF NOT EXISTS kv (k TEXT PRIMARY KEY, v TEXT NOT NULL);");
        using var c = _db.CreateCommand(); c.CommandText = "SELECT k, v FROM kv";
        using var r = c.ExecuteReader(); while (r.Read()) _cache[r.GetString(0)] = r.GetString(1);
    }
    void Exec(string sql) { using var c = _db.CreateCommand(); c.CommandText = sql; c.ExecuteNonQuery(); }
    public string? Get(string key) { lock (_lock) return _cache.TryGetValue(key, out var v) ? v : null; }
    public void Set(string key, string value)
    {
        lock (_lock)
        {
            using var c = _db.CreateCommand(); c.CommandText = "INSERT INTO kv(k, v) VALUES($k, $v) ON CONFLICT(k) DO UPDATE SET v = excluded.v";
            c.Parameters.AddWithValue("$k", key); c.Parameters.AddWithValue("$v", value); c.ExecuteNonQuery(); _cache[key] = value;
        }
    }
    public void Remove(string key)
    {
        lock (_lock) { using var c = _db.CreateCommand(); c.CommandText = "DELETE FROM kv WHERE k = $k"; c.Parameters.AddWithValue("$k", key); c.ExecuteNonQuery(); _cache.Remove(key); }
    }
    public IReadOnlyList<string> Keys() { lock (_lock) return _cache.Keys.ToList(); }
}

/// <summary>Secretos (token del bot, clave de cifrado) protegidos con DPAPI para el usuario de Windows. Nunca se escriben en texto plano.</summary>
public sealed class DpapiSecrets : ISecrets
{
    readonly string _path; readonly object _lock = new();
    static readonly byte[] Entropy = Crypto.Utf8("encargos-nativo-secrets-v1");
    public DpapiSecrets(string path) { _path = path; }
    JObj Load()
    {
        try { if (!File.Exists(_path)) return new JObj(); return Json.Obj(Crypto.FromUtf8(ProtectedData.Unprotect(File.ReadAllBytes(_path), Entropy, DataProtectionScope.CurrentUser))) ?? new JObj(); }
        catch { return new JObj(); }
    }
    void Save(JObj o) { var tmp = _path + ".tmp"; File.WriteAllBytes(tmp, ProtectedData.Protect(Crypto.Utf8(Json.Stringify(o)), Entropy, DataProtectionScope.CurrentUser)); File.Move(tmp, _path, true); }
    public string? Get(string name) { lock (_lock) return Load()[name] as string; }
    public void Set(string name, string value) { lock (_lock) { var o = Load(); o[name] = value; Save(o); } }
    public void Remove(string name) { lock (_lock) { var o = Load(); o.Remove(name); Save(o); } }
    /// <summary>Secreto de 32 bytes de este equipo (se mezcla con contraseña y PIN).</summary>
    public byte[] DeviceSecret()
    {
        var s = Get("device_secret");
        if (s == null) { s = Crypto.B64(Crypto.Random(32)); Set("device_secret", s); }
        return Crypto.UnB64(s);
    }
}

/// <summary>Objetos únicos de la app.</summary>
public static class Services
{
    public static readonly string DataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EncargosNativo");
    static SqliteStorage? _storage;
    public static SqliteStorage Storage => _storage ??= Init();
    static SqliteStorage Init() { Directory.CreateDirectory(DataDir); return new SqliteStorage(Path.Combine(DataDir, "encargos.db")); }
    public static DpapiSecrets Secrets { get; private set; } = null!;
    public static EncargosApp Core { get; private set; } = null!;
    public static TelegramSync Sync { get; private set; } = null!;
    public static Security Security { get; private set; } = null!;
    public static Backup Backup { get; private set; } = null!;
    public static Stats Stats { get; private set; } = null!;
    public static DispatcherQueue Ui { get; private set; } = null!;
    public static bool Unlocked;

    public static bool DayMode() => Storage.Get("encargos_aurora_mode") == "day";

    public static void Start(DispatcherQueue ui)
    {
        Ui = ui;
        Secrets = new DpapiSecrets(Path.Combine(DataDir, "secretos.bin"));
        Core = new EncargosApp(Storage, "PC");
        Sync = new TelegramSync(Core, Secrets, "https://api.telegram.org", OnUiWait);
        Security = new Security(Core, Secrets.DeviceSecret()) { BioName = "Windows Hello" };
        Backup = new Backup(Core); Stats = new Stats(Core);
        Core.OnPersist = () => Sync.ChangedLocally();
    }

    /// <summary>Ejecuta en el hilo de la interfaz y espera (el motor de sincronización lo usa para tocar los datos).</summary>
    public static void OnUiWait(Action a)
    {
        if (Ui.HasThreadAccess) { a(); return; }
        using var done = new ManualResetEventSlim(false); Exception? err = null;
        if (!Ui.TryEnqueue(() => { try { a(); } catch (Exception e) { err = e; } finally { done.Set(); } })) throw new InvalidOperationException("La ventana se está cerrando");
        done.Wait(); if (err != null) throw err;
    }
    public static void Post(Action a) => Ui?.TryEnqueue(() => a());

    public static void Report(Exception e)
    {
        try { File.AppendAllText(Path.Combine(DataDir, "errores.log"), DateTime.Now.ToString("s") + " " + e.GetType().Name + ": " + Redact(e.Message) + "\n"); } catch { }
    }
    /// <summary>Quita cualquier token de bot de un texto antes de guardarlo o mostrarlo.</summary>
    public static string Redact(string s) => System.Text.RegularExpressions.Regex.Replace(s ?? "", @"[0-9]{5,}:[A-Za-z0-9_-]{20,}", "<oculto>");

    /// <summary>Carpeta Documentos\Encargos (respaldos automáticos y archivos exportados).</summary>
    public static string DocsDir() { var d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Encargos"); Directory.CreateDirectory(d); return d; }
}

/// <summary>Funciones del sistema: privacidad de pantalla y Windows Hello.</summary>
public static class Native
{
    [DllImport("user32.dll")] static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint affinity);
    const uint WDA_NONE = 0, WDA_EXCLUDEFROMCAPTURE = 0x11;
    /// <summary>Excluye la ventana de capturas de pantalla y grabaciones.</summary>
    public static void Privacy(IntPtr hwnd, bool on) { try { SetWindowDisplayAffinity(hwnd, on ? WDA_EXCLUDEFROMCAPTURE : WDA_NONE); } catch { } }

    public static async Task<bool> HelloAvailable()
    {
        try { return await Windows.Security.Credentials.UI.UserConsentVerifier.CheckAvailabilityAsync() == Windows.Security.Credentials.UI.UserConsentVerifierAvailability.Available; }
        catch { return false; }
    }
    public static async Task<bool> Hello(IntPtr hwnd, string message)
    {
        try
        {
            // UserConsentVerifierInterop (diálogo de Windows Hello sobre nuestra ventana) se busca en tiempo de ejecución:
            // así compila aunque la proyección de C#/WinRT instalada no incluya esa clase auxiliar.
            var t = typeof(Windows.Security.Credentials.UI.UserConsentVerifier).Assembly.GetType("Windows.Security.Credentials.UI.UserConsentVerifierInterop");
            var m = t?.GetMethod("RequestVerificationForWindowAsync", new[] { typeof(IntPtr), typeof(string) });
            if (m == null) throw new PlatformNotSupportedException();
            var op = (Windows.Foundation.IAsyncOperation<Windows.Security.Credentials.UI.UserConsentVerificationResult>)m.Invoke(null, new object[] { hwnd, message })!;
            var r = await op;
            return r == Windows.Security.Credentials.UI.UserConsentVerificationResult.Verified;
        }
        catch
        {
            try { return await Windows.Security.Credentials.UI.UserConsentVerifier.RequestVerificationAsync(message) == Windows.Security.Credentials.UI.UserConsentVerificationResult.Verified; }
            catch { return false; }
        }
    }
}
