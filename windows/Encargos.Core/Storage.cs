using System.Collections.Concurrent;

namespace Encargos.Core;

/// <summary>Almacenamiento clave/valor con las MISMAS claves que la app web (localStorage). Windows lo implementa con SQLite.</summary>
public interface IStorage
{
    string? Get(string key);
    void Set(string key, string value);
    void Remove(string key);
    IReadOnlyList<string> Keys();
}

public sealed class MemoryStorage : IStorage
{
    public readonly ConcurrentDictionary<string, string> Map = new();
    public string? Get(string key) => Map.TryGetValue(key, out var v) ? v : null;
    public void Set(string key, string value) => Map[key] = value;
    public void Remove(string key) => Map.TryRemove(key, out _);
    public IReadOnlyList<string> Keys() => Map.Keys.ToList();
}

/// <summary>Almacenamiento en un archivo JSON (pruebas).</summary>
public sealed class FileStorage : IStorage
{
    readonly string _path; readonly JObj _map; readonly object _lock = new();
    public FileStorage(string path)
    {
        _path = path; _map = File.Exists(path) ? (Json.Obj(File.ReadAllText(path)) ?? new JObj()) : new JObj();
    }
    void Flush() { var tmp = _path + ".tmp"; File.WriteAllText(tmp, Json.Stringify(_map)); File.Move(tmp, _path, true); }
    public string? Get(string key) { lock (_lock) return _map[key] as string; }
    public void Set(string key, string value) { lock (_lock) { _map[key] = value; Flush(); } }
    public void Remove(string key) { lock (_lock) { _map.Remove(key); Flush(); } }
    public IReadOnlyList<string> Keys() { lock (_lock) return _map.Keys; }
}

/// <summary>Ayudas iguales al objeto LS de la app web.</summary>
public sealed class LS
{
    public readonly IStorage S;
    public LS(IStorage s) { S = s; }
    public string? Get(string k, string? d = null) { try { return S.Get(k) ?? d; } catch { return d; } }
    public bool Set(string k, string v) { try { S.Set(k, v); return true; } catch { return false; } }
    public void Del(string k) { try { S.Remove(k); } catch { } }
    public object? JsonOf(string k) { try { var s = S.Get(k); return s == null ? null : Json.Parse(s); } catch { return null; } }
    public JObj Obj(string k) => JsonOf(k) as JObj ?? new JObj();
    public JArr Arr(string k) => JsonOf(k) as JArr ?? new JArr();
    public bool Put(string k, object? v) => Set(k, Json.Stringify(v));
}

/// <summary>Almacén de secretos de la plataforma (DPAPI en Windows). Aquí viven el token y la clave.</summary>
public interface ISecrets
{
    string? Get(string name);
    void Set(string name, string value);
    void Remove(string name);
}
public sealed class MemorySecrets : ISecrets
{
    readonly Dictionary<string, string> _m = new();
    public string? Get(string name) => _m.TryGetValue(name, out var v) ? v : null;
    public void Set(string name, string value) => _m[name] = value;
    public void Remove(string name) => _m.Remove(name);
}
