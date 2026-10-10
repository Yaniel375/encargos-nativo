using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Encargos.Core;

/// <summary>
/// Sincronización por Telegram, compatible byte a byte con la app web y con Android (respaldo único ENCARGOS_BACKUP_LATEST).
/// Traducción directa del motor: mismas claves locales, mismos formatos, mismo orden de pasos.
/// </summary>
public sealed class TelegramSync
{
    public const int SCHEMA = 1;
    public static readonly string[] ENT = { "order", "payment", "order_media", "client", "note", "expense", "setting" };
    public const string LATEST = "ENCARGOS_BACKUP_LATEST";
    public const string BK_NAME = "encargos-latest.enc";
    public const double MAX_BYTES = 19.5 * 1048576;
    public const long REFRESH_MS = 40 * 3600000L;
    static readonly long[] BACKOFF = { 5000, 15000, 30000, 60000, 300000 };
    public const string RESET_MSG = "La sincronización se reinició en otro dispositivo. Ve a Ajustes → Sincronización → Desconectar y pega el código de enlace nuevo.";
    static readonly HashSet<string> OMIT = new() { "id", "uid", "paid", "balance", "paymentHistory", "photo", "photos", "total", "_u" };
    static class K
    {
        public const string cfg = "encargos_tg_cfg", dev = "encargos_device_id", q = "encargos_tg_queue", h = "encargos_tg_hashes", st = "encargos_tg_stamps",
            pp = "encargos_tg_processed_pkgs", po = "encargos_tg_processed_ops", meta = "encargos_tg_meta", log = "encargos_tg_log", conf = "encargos_tg_conflicts", seq = "encargos_tg_seq";
    }

    readonly EncargosApp _app; readonly ISecrets _secrets; readonly string _apiBase; readonly Action<Action> _onApp;
    LS Ls => _app.Ls;
    public bool IsTest => _apiBase != "https://api.telegram.org";
    long MinGap() => IsTest ? 0L : 15000L;
    public ITgApi Api;

    /// <param name="onApp">ejecuta un bloque en el hilo de la interfaz y espera (los datos solo se tocan desde ahí).</param>
    public TelegramSync(EncargosApp app, ISecrets secrets, string apiBase = "https://api.telegram.org", Action<Action>? onApp = null)
    {
        _app = app; _secrets = secrets; _apiBase = apiBase; _onApp = onApp ?? (a => a());
        Api = new HttpTgApi(apiBase, Token);
        _worker = new Thread(WorkLoop) { IsBackground = true, Name = "encargos-sync" }; _worker.Start();
    }
    T OnUi<T>(Func<T> f) { T r = default!; Exception? err = null; _onApp(() => { try { r = f(); } catch (Exception e) { err = e; } }); if (err != null) throw err; return r; }
    void OnUi(Action f) => OnUi(() => { f(); return 0; });

    /* ---------- configuración (en el almacén seguro) ---------- */
    public JObj? Cfg() => Json.Obj(_secrets.Get(K.cfg));
    void PutCfg(JObj? c) { if (c == null) _secrets.Remove(K.cfg); else _secrets.Set(K.cfg, Json.Stringify(c)); }
    public bool Enabled() { var c = Cfg(); return c != null && c.Str("token").Length > 0 && c["chatId"] != null && c.Str("key").Length > 0 && c.Str("db").Length > 0 && !Js.Truthy(c["paused"]); }
    string Token() => Cfg()?.Str("token") ?? "";
    object? ChatId() => Cfg()?["chatId"];
    byte[] KeyBytes() => Crypto.B64U.Dec(Cfg()!.Str("key"));
    string Db() => Cfg()!.Str("db");

    public string DeviceId()
    {
        var d = Ls.Get(K.dev, "") ?? "";
        if (d.Length == 0 || !Regex.IsMatch(d, "^(ANDROID|PC|WEB|PHONE)-")) { d = _app.Platform + "-" + Crypto.Uuid(); Ls.Set(K.dev, d); }
        return d;
    }
    double NextSeq() { var n = (double.TryParse(Ls.Get(K.seq, "0"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0.0) + 1; Ls.Set(K.seq, Js.NumberToString(n)); return n; }
    JObj Meta() => Ls.Obj(K.meta);
    void PutMeta(JObj m) => Ls.Put(K.meta, m);
    void UpdMeta(Action<JObj> f) { var m = Meta(); f(m); PutMeta(m); }

    public void Slog(string kind, string text) { var a = Ls.Arr(K.log); a.Insert(0, JObj.Of(("t", (double)Js.NowMs()), ("dev", _app.Platform), ("kind", kind), ("text", text))); Ls.Put(K.log, new JArr(a.Take(200))); }
    public List<JObj> LogEntries() => Ls.Arr(K.log).Objs();
    public List<JObj> Conflicts() => Ls.Arr(K.conf).Objs();

    static bool Empty(object? v) => v == null || Js.Eq(v, "");

    /* ---------- identificadores deterministas para datos existentes ---------- */
    public void EnsureIds()
    {
        var changed = false;
        foreach (var x in _app.Orders())
        {
            if (Empty(x["uid"])) { x["uid"] = Crypto.DetUuid("order|" + Js.Str(x["id"]) + "|" + Js.Str(x["date"]) + "|" + Js.Str(x["name"])); changed = true; }
            if (x["paymentHistory"] is not JArr) { x["paymentHistory"] = new JArr(); changed = true; }
            var ph = (JArr)x["paymentHistory"]!;
            for (var i = 0; i < ph.Count; i++)
            {
                if (ph[i] is not JObj h) continue;
                if (Empty(h["sid"])) { h["sid"] = Crypto.DetUuid("pay|" + Js.Str(x["uid"]) + "|" + Js.Str(h["id"]) + "|" + Js.Str(h["date"]) + "|" + Js.NumberToString(Js.Num(h["amount"])) + "|" + i); changed = true; }
            }
            var sum = ph.Objs().Sum(h => Js.Num(h["amount"]));
            if (Math.Abs(Js.Num(x["paid"]) - sum) > 0.005 && !ph.Objs().Any(h => Js.Truthy(h["adj"])))
            {
                var date = x["date"] != null && Js.Str(x["date"]).Length > 0 ? Js.Str(x["date"]) : Js.NowIso();
                ph.Insert(0, JObj.Of(("id", "adj"), ("sid", Crypto.DetUuid("payadj|" + Js.Str(x["uid"]))), ("adj", true), ("date", date),
                    ("amount", Js.ToFixed2(Js.Num(x["paid"]) - sum)), ("method", "Efectivo"), ("note", "Pagos anteriores"))); changed = true;
            }
        }
        var notes = Ls.Arr("encargos_notes"); var nch = false;
        foreach (var n in notes.Objs()) if (Empty(n["uid"])) { n["uid"] = Crypto.DetUuid("note|" + Js.Str(n["id"]) + "|" + Js.Str(n["created"])); nch = true; }
        if (nch) Ls.Put("encargos_notes", notes);
        var ex = Ls.Arr("encargos_expenses"); var ech = false;
        foreach (var e in ex.Objs()) if (Empty(e["uid"])) { e["uid"] = Crypto.DetUuid("exp|" + Js.Str(e["id"]) + "|" + Js.Str(e["date"])); ech = true; }
        if (ech) Ls.Put("encargos_expenses", ex);
        if (changed) { _app.ApplyingRemote = true; try { _app.Storage.Set(EncargosApp.KEY, Json.Stringify(_app.Data)); } finally { _app.ApplyingRemote = false; } }
    }

    /// <summary>Modelo local → entidades (mismo formato que la app web).</summary>
    public Dictionary<string, JObj> Snapshot()
    {
        EnsureIds(); var r = new Dictionary<string, JObj>(); foreach (var e in ENT) r[e] = new JObj();
        foreach (var x in _app.Orders())
        {
            var p = new JObj(); foreach (var kv in x) if (!OMIT.Contains(kv.Key)) p[kv.Key] = Json.DeepCopy(kv.Value); p["localId"] = x["id"]; r["order"][Js.Str(x["uid"])] = p;
            foreach (var h in ((JArr)x["paymentHistory"]!).Objs()) { var q = (JObj)Json.DeepCopy(h)!; q.Remove("sid"); q["order"] = x["uid"]; r["payment"][Js.Str(h["sid"])] = q; }
            var photos = x["photos"] as JArr;
            if (Js.Truthy(x["photo"]) || (photos != null && photos.Count > 0))
                r["order_media"][Js.Str(x["uid"])] = JObj.Of(("order", x["uid"]), ("photo", Js.Truthy(x["photo"]) ? x["photo"] : null), ("photos", photos != null ? Json.DeepCopy(photos) : new JArr()));
        }
        foreach (var kv in Ls.Obj("encargos_client_info")) { var o = JObj.Of(("key", kv.Key)); if (kv.Value is JObj v) foreach (var ab in v) o[ab.Key] = ab.Value; r["client"][Crypto.DetUuid("client|" + kv.Key)] = o; }
        foreach (var n in Ls.Arr("encargos_notes").Objs()) r["note"][Js.Str(n["uid"])] = n;
        foreach (var e in Ls.Arr("encargos_expenses").Objs()) r["expense"][Js.Str(e["uid"])] = e;
        var s = new List<(string, object?)> { ("templates", Ls.JsonOf("encargos_templates")), ("buy_rates", Ls.JsonOf("encargos_buy_rates")),
            ("shipping", JObj.Of(("rates", _app.ShippingRates), ("names", _app.ShippingNames))), ("business", Ls.Get("encargos_business_name", null)) };
        foreach (var (k, v) in s) if (v != null) r["setting"][Crypto.DetUuid("setting|" + k)] = JObj.Of(("key", k), ("value", v));
        return r;
    }
    static string HashOf(object? p) => Crypto.Sha256Hex(Json.Stringify(p));
    /// <summary>newer(a,b): timestamp y, si empatan, deviceId como desempate.</summary>
    public static bool Newer(JObj a, JObj? b) => b == null || Js.Num(a["ts"]) > Js.Num(b["ts"]) || (Js.Num(a["ts"]) == Js.Num(b["ts"]) && Js.Ord(Js.Str(a["dev"]), Js.Str(b["dev"])) > 0);

    /* ---------- cola de operaciones ---------- */
    public JArr Queue() => Ls.Arr(K.q);
    void SaveQ(IEnumerable<object?> q) => Ls.Put(K.q, new JArr(q));
    public int Pending() => Queue().Count;
    readonly Dictionary<string, object?> _lastPayloads = new();
    static string OpName(string ent, object? prev, bool isNew, bool deleted, object? payload)
    {
        if (ent == "payment") return deleted ? "PAYMENT_VOID" : isNew ? "PAYMENT_ADD" : "UPDATE";
        if (deleted) return "DELETE"; if (isNew) return "CREATE";
        if (ent == "order" && prev is JObj pv && !Js.Eq(pv["status"], (payload as JObj)?["status"])) return "ORDER_STATUS_CHANGE";
        if (ent == "client") return "CLIENT_UPDATE"; if (ent == "setting") return "SETTINGS_UPDATE"; return "UPDATE";
    }
    public int Collect()
    {
        if (!Enabled()) return 0;
        var snap = Snapshot(); var known = Ls.Obj(K.h); var stamps = Ls.Obj(K.st); var q = Queue(); var meta = Meta(); var dev = DeviceId(); var n = 0; double now = Js.NowMs();
        var removed = new List<string>();
        foreach (var t in ENT)
        {
            var cur = snap[t];
            foreach (var kv in cur)
            {
                var k = t + ":" + kv.Key; var h = HashOf(kv.Value); if (Js.Eq(known[k], h)) continue;
                var isNew = known[k] == null; known[k] = h; stamps[k] = JObj.Of(("ts", now), ("dev", dev));
                q.Add(JObj.Of(("opId", Crypto.Uuid()), ("deviceId", dev), ("sequence", NextSeq()), ("timestamp", now), ("entity", t), ("entityId", kv.Key),
                    ("operation", OpName(t, _lastPayloads.GetValueOrDefault(k), isNew, false, kv.Value)), ("data", kv.Value), ("baseVersion", Js.Num(meta["localVersion"]))));
                _lastPayloads[k] = kv.Value; n++;
            }
            foreach (var k in known.Keys) if (k.StartsWith(t + ":", StringComparison.Ordinal) && !cur.ContainsKey(k[(t.Length + 1)..])) removed.Add(k);
        }
        var ko = known.Keys.Count(k => k.StartsWith("order:", StringComparison.Ordinal)); var ro = removed.Count(k => k.StartsWith("order:", StringComparison.Ordinal));
        var massive = ro > 3 && ro > ko * 0.3;
        foreach (var k in removed)
        {
            known.Remove(k); if (massive) continue; var i = k.IndexOf(':'); var t = k[..i]; var id = k[(i + 1)..];
            stamps[k] = JObj.Of(("ts", now), ("dev", dev), ("deleted", true));
            q.Add(JObj.Of(("opId", Crypto.Uuid()), ("deviceId", dev), ("sequence", NextSeq()), ("timestamp", now), ("entity", t), ("entityId", id),
                ("operation", OpName(t, null, false, true, null)), ("data", null), ("deleted", true), ("baseVersion", Js.Num(meta["localVersion"])))); n++;
        }
        if (massive) Slog("warn", "Importación o borrado masivo detectado: no se borrará nada en tus otros dispositivos.");
        Ls.Put(K.h, known); Ls.Put(K.st, stamps); SaveQ(q); return n;
    }

    /* ---------- aplicar operaciones (merge) ---------- */
    public sealed record Applied(int N, List<JObj> Late);
    public Applied ApplyOps(List<JObj> ops, string source, bool track = true)
    {
        var processed = new LinkedSet(Ls.Arr(K.po).Select(Js.Str)); var stamps = Ls.Obj(K.st); var known = Ls.Obj(K.h); var conflicts = Ls.Arr(K.conf);
        EnsureIds(); var n = 0; var pay = 0; var ord = 0;
        var M = new Dictionary<string, JObj>(); foreach (var x in _app.Orders()) M[Js.Str(x["uid"])] = x;
        var notes = Ls.Arr("encargos_notes"); var exps = Ls.Arr("encargos_expenses"); var ci = Ls.Obj("encargos_client_info");
        bool nT = false, eT = false, cT = false;
        var pendLocal = Queue().Objs().Select(o => o.Str("entity") + ":" + o.Str("entityId")).ToHashSet();
        var sorted = ops.OrderBy(o => Js.Num(o["timestamp"])).ThenBy(o => Js.Str(o["deviceId"]), Js.Collator).ThenBy(o => Js.Num(o["sequence"])).ToList();
        var late = new List<JObj>(); var touched = new HashSet<string>();
        foreach (var op in sorted)
        {
            var opId = Js.Str(op["opId"]);
            if (processed.Contains(opId)) continue;
            var ent = op.Str("entity"); var eid = op.Str("entityId"); var k = ent + ":" + eid; var deleted = Js.Truthy(op["deleted"]);
            var st = JObj.Of(("ts", Js.Num(op["timestamp"])), ("dev", op["deviceId"]), ("deleted", deleted));
            if (!Newer(st, stamps[k] as JObj))
            {
                if (track) processed.Add(opId);
                if (track && ent != "payment" && pendLocal.Contains(k) && !deleted) conflicts.Insert(0, JObj.Of(("t", (double)Js.NowMs()), ("entity", ent), ("id", eid), ("kept", "local"), ("remoteDevice", op["deviceId"])));
                continue;
            }
            if (ent != "payment" && pendLocal.Contains(k)) conflicts.Insert(0, JObj.Of(("t", (double)Js.NowMs()), ("entity", ent), ("id", eid), ("kept", "remote"), ("remoteDevice", op["deviceId"])));
            var d = op["data"] as JObj ?? new JObj();
            switch (ent)
            {
                case "order":
                    {
                        M.TryGetValue(eid, out var x);
                        if (deleted) { if (x != null) { _app.Data.Remove(x); M.Remove(eid); } }
                        else
                        {
                            var p = (JObj)Json.DeepCopy(d)!; p.Remove("localId");
                            if (x != null) { foreach (var kk in x.Keys) if (!OMIT.Contains(kk) && !p.ContainsKey(kk)) x.Remove(kk); x.PutAll(p); }
                            else
                            {
                                var id = Js.Num(d["localId"]); if (id == 0.0 || double.IsNaN(id)) id = Js.NowMs();
                                while (_app.Orders().Any(o => Js.Num(o["id"]) == id)) id += 1;
                                var nx = JObj.Of(("id", id), ("uid", eid), ("paid", 0.0), ("balance", 0.0), ("paymentHistory", new JArr())); nx.PutAll(p); _app.Data.Insert(0, nx); M[eid] = nx;
                            }
                        }
                        ord++; break;
                    }
                case "payment":
                    {
                        M.TryGetValue(Js.Str(d["order"]), out var ox);
                        ox ??= _app.Orders().FirstOrDefault(o => (o["paymentHistory"] as JArr)?.Objs().Any(h => Js.Eq(h["sid"], eid)) == true);
                        if (ox == null) { if (!deleted) { late.Add(op); continue; } }
                        else
                        {
                            if (ox["paymentHistory"] is not JArr ph) { ph = new JArr(); ox["paymentHistory"] = ph; }
                            var i = ph.FindIndex(it => it is JObj h && Js.Eq(h["sid"], eid));
                            if (deleted) { if (i >= 0) ph.RemoveAt(i); }
                            else { var h = (JObj)Json.DeepCopy(d)!; h["sid"] = eid; h.Remove("order"); if (i >= 0) ph[i] = h; else ph.Add(h); }
                        }
                        pay++; break;
                    }
                case "order_media":
                    {
                        if (M.TryGetValue(eid, out var x))
                        {
                            if (deleted) { x.Remove("photo"); x.Remove("photos"); }
                            else { if (Js.Truthy(d["photo"])) x["photo"] = d["photo"]; else x.Remove("photo"); x["photos"] = d["photos"] is JArr a ? Json.DeepCopy(a) : new JArr(); }
                        }
                        break;
                    }
                case "client":
                    {
                        if (d["key"] != null || deleted)
                        {
                            var key = d["key"] != null ? Js.Str(d["key"]) : ci.Keys.FirstOrDefault(c => Crypto.DetUuid("client|" + c) == eid);
                            if (key != null) { if (deleted) ci.Remove(key); else { var v = (JObj)Json.DeepCopy(d)!; v.Remove("key"); ci[key] = v; } cT = true; }
                        }
                        break;
                    }
                case "note":
                    {
                        var i = notes.FindIndex(it => it is JObj o && Js.Eq(o["uid"], eid));
                        if (deleted) { if (i >= 0) notes.RemoveAt(i); } else { var v = (JObj)Json.DeepCopy(d)!; v["uid"] = eid; if (i >= 0) notes[i] = v; else notes.Add(v); }
                        nT = true; break;
                    }
                case "expense":
                    {
                        var i = exps.FindIndex(it => it is JObj o && Js.Eq(o["uid"], eid));
                        if (deleted) { if (i >= 0) exps.RemoveAt(i); } else { var v = (JObj)Json.DeepCopy(d)!; v["uid"] = eid; if (i >= 0) exps[i] = v; else exps.Add(v); }
                        eT = true; break;
                    }
                case "setting":
                    {
                        if (!deleted && op["data"] != null)
                            switch (d.Str("key"))
                            {
                                case "templates": Ls.Put("encargos_templates", d["value"]); break;
                                case "buy_rates": Ls.Put("encargos_buy_rates", d["value"]); break;
                                case "business": Ls.Set("encargos_business_name", Js.Str(d["value"])); break;
                                case "shipping":
                                    if (d["value"] is JObj v)
                                    {
                                        if (v["rates"] is JObj rr) foreach (var kv in rr) _app.ShippingRates[kv.Key] = kv.Value;
                                        if (v["names"] is JObj nn) foreach (var kv in nn) _app.ShippingNames[kv.Key] = kv.Value;
                                        Ls.Put(EncargosApp.SHIPPING_KEY, JObj.Of(("rates", _app.ShippingRates), ("names", _app.ShippingNames)));
                                    }
                                    break;
                            }
                        break;
                    }
            }
            stamps[k] = st; if (track) processed.Add(opId); n++; touched.Add(k);
        }
        // lo pagado se deriva siempre de los pagos
        foreach (var x in _app.Orders())
        {
            if (x["paymentHistory"] is not JArr ph) continue;
            var sortedPh = ph.Objs().OrderBy(h => { var v = Js.ParseDate(h["date"]); return double.IsNaN(v) ? 0.0 : v; }).ToList();
            ph.Clear(); ph.AddRange(sortedPh); x["paid"] = Js.ToFixed2(sortedPh.Sum(h => Js.Num(h["amount"])));
        }
        if (nT) Ls.Put("encargos_notes", notes); if (eT) Ls.Put("encargos_expenses", exps); if (cT) Ls.Put("encargos_client_info", ci);
        if (n > 0)
        {
            _app.ApplyingRemote = true;
            try
            {
                _app.RecalcAll(); _app.Storage.Set(EncargosApp.KEY, Json.Stringify(_app.Data));
                var snap = Snapshot();
                foreach (var kk in touched)
                {
                    if (pendLocal.Contains(kk)) continue; var i = kk.IndexOf(':'); var t = kk[..i]; var id = kk[(i + 1)..];
                    var p = snap.TryGetValue(t, out var tt) ? tt[id] : null; if (p != null) known[kk] = HashOf(p); else known.Remove(kk);
                }
                if (touched.Any(t => t.StartsWith("payment:", StringComparison.Ordinal)))
                    foreach (var x in _app.Orders()) { var kk = "order:" + Js.Str(x["uid"]); if (known[kk] != null && !pendLocal.Contains(kk) && snap["order"][Js.Str(x["uid"])] is { } so) known[kk] = HashOf(so); }
                Ls.Put(K.h, known);
            }
            finally { _app.ApplyingRemote = false; }
            _app.Changed();
        }
        Ls.Put(K.st, stamps); Ls.Put(K.po, new JArr(processed.TakeLast(20000))); Ls.Put(K.conf, new JArr(conflicts.Take(100)));
        if (n > 0)
        {
            var parts = new List<string>();
            if (pay > 0) parts.Add($"{pay} pago" + (pay == 1 ? "" : "s"));
            if (ord > 0) parts.Add($"{ord} encargo" + (ord == 1 ? "" : "s"));
            if (n - pay - ord > 0) parts.Add($"{n - pay - ord} dato" + (n - pay - ord == 1 ? "" : "s") + " más");
            Slog("in", "Recibido: " + string.Join(", ", parts) + $" ({source})");
        }
        return new Applied(n, late);
    }

    /// <summary>Conjunto que conserva el orden de inserción (como LinkedHashSet).</summary>
    sealed class LinkedSet
    {
        readonly HashSet<string> _s = new(); readonly List<string> _l = new();
        public LinkedSet(IEnumerable<string> items) { foreach (var i in items) Add(i); }
        public bool Contains(string x) => _s.Contains(x);
        public void Add(string x) { if (_s.Add(x)) _l.Add(x); }
        public IEnumerable<object?> TakeLast(int n) => _l.Skip(Math.Max(0, _l.Count - n)).Cast<object?>();
    }

    /* ---------- Telegram ---------- */
    object? Tg(string method, JObj p) => Api.Call(method, p);
    (double msgId, string fileId) SendFile(byte[] bytes, string name, string caption)
    {
        var m = (JObj)Api.SendFile("sendDocument", new Dictionary<string, string> { ["chat_id"] = Js.Str(ChatId()), ["caption"] = caption, ["disable_notification"] = "true" }, "document", name, bytes, "application/octet-stream")!;
        return (Js.Num(m["message_id"]), ((JObj)m["document"]!).Str("file_id"));
    }
    byte[] FetchFile(string fileId) { var f = (JObj)Tg("getFile", JObj.Of(("file_id", fileId)))!; return Api.Download(f.Str("file_path")); }
    static string Sha(byte[] b) => Crypto.ShaHex(b);
    byte[] Seal(object? o) => Crypto.Seal(o, KeyBytes(), Db());
    object? Unseal(byte[] b) => Crypto.Unseal(b, KeyBytes(), Db());

    /* ---------- coordinador: mensaje fijado ---------- */
    string DbTag() => Crypto.DbTag(Db());
    static bool IsLatestPin(JObj? pm) => pm != null && pm["text"] is string t && t.Contains("#" + LATEST);
    static bool IsLegacyPin(JObj? pm) => pm != null && (pm["document"] as JObj)?.Str("file_name").StartsWith("encargos-manifest", StringComparison.Ordinal) == true;
    public static JObj? ParseMan(string t)
    {
        var i = t.IndexOf("#" + LATEST, StringComparison.Ordinal); var a = i < 0 ? -1 : t.IndexOf('{', i); var b = t.LastIndexOf('}');
        if (i < 0 || a < 0 || b < a) return null;
        try { return Json.Parse(t.Substring(a, b - a + 1)) is JObj o && Js.Eq(o["type"], LATEST) ? o : null; } catch { return null; }
    }
    public static string ManText(JObj m)
    {
        var d = Js.Local(Js.ParseDate(m["timestamp"])); static string P(int n) => n.ToString().PadLeft(2, '0');
        var dev = m.Str("deviceId"); var from = dev.StartsWith("ANDROID") ? "📱 Android" : dev.StartsWith("PC") ? "💻 PC" : "🌐 Web";
        return $"📦 Última sincronización\nVersión {Js.NumberToString(Js.Num(m["version"]))}\n{P(d.Day)}/{P(d.Month)}/{d.Year} {P(d.Hour)}:{P(d.Minute)}\n✓ Correctamente sincronizado\nDesde: {from}\n\nNo borres ni desfijes este mensaje ni el archivo {BK_NAME}.\n\n#{LATEST}\n{Json.Stringify(m)}";
    }
    public sealed record Remote(string Kind, double PinId, JObj? M, bool Foreign = false);
    public Remote ReadRemote()
    {
        var ch = (JObj)Tg("getChat", JObj.Of(("chat_id", ChatId())))!; var pm = ch["pinned_message"] as JObj;
        var fresh = Js.Truthy(Cfg()?["fresh"]);
        if (IsLatestPin(pm))
        {
            var m = ParseMan(pm!.Str("text")) ?? throw new TgError("El mensaje fijado de Encargos en Telegram está dañado; se volverá a intentar");
            if (!Js.Eq(m["db"], DbTag())) { if (fresh) return new Remote("none", Js.Num(pm["message_id"]), null, true); throw new TgError(RESET_MSG); }
            if (Js.Num(m["schemaVersion"]) > SCHEMA) throw new TgError("Este dispositivo necesita actualizar Encargos para leer los datos");
            return new Remote("latest", Js.Num(pm["message_id"]), m);
        }
        if (IsLegacyPin(pm))
        {
            var bytes = FetchFile(((JObj)pm!["document"]!).Str("file_id"));
            JObj m;
            try { m = (JObj)Unseal(bytes)!; } catch (Exception) { if (fresh) return new Remote("none", Js.Num(pm["message_id"]), null, true); throw new TgError(RESET_MSG); }
            if (!Js.Eq(m["databaseId"], Db())) { if (fresh) return new Remote("none", Js.Num(pm["message_id"]), null, true); throw new TgError(RESET_MSG); }
            if (Js.Num(m["schemaVersion"]) > SCHEMA) throw new TgError("Este dispositivo necesita actualizar Encargos para leer los datos");
            if (m["packages"] is not JArr) m["packages"] = new JArr();
            return new Remote("legacy", Js.Num(pm["message_id"]), m);
        }
        return new Remote("none", pm != null ? Js.Num(pm["message_id"]) : 0.0, null);
    }
    static bool SameHead(Remote a, Remote b) => a.Kind == b.Kind && a.Kind switch { "latest" => Js.Eq(a.M!["packageId"], b.M!["packageId"]), "legacy" => a.PinId == b.PinId, _ => true };
    double WriteManifest(JObj man, Remote cur)
    {
        var text = ManText(man);
        if (cur.Kind == "latest") { Tg("editMessageText", JObj.Of(("chat_id", ChatId()), ("message_id", cur.PinId), ("text", text))); return cur.PinId; }
        var t = (JObj)Tg("sendMessage", JObj.Of(("chat_id", ChatId()), ("text", text), ("disable_notification", true)))!; var mid = Js.Num(t["message_id"]);
        try { Tg("pinChatMessage", JObj.Of(("chat_id", ChatId()), ("message_id", mid), ("disable_notification", true))); } catch { DelMsg(mid); throw; }
        return mid;
    }
    /// <summary>'ok' (borrado o ya no existe) · 'old' (no se puede: más de 48 h) · 'net' (sin respuesta)</summary>
    string DelMsg(double? id)
    {
        if (id == null || id == 0.0) return "ok";
        try { Tg("deleteMessage", JObj.Of(("chat_id", ChatId()), ("message_id", id.Value))); return "ok"; }
        catch (TgError e) { if ((e.Message ?? "").Contains("not found", StringComparison.OrdinalIgnoreCase)) return "ok"; return e.Code is 400 or 403 ? "old" : "net"; }
        catch { return "net"; }
    }
    void AddCleanup(double? id)
    {
        if (id == null || id == 0.0) return;
        UpdMeta(m => { var l = new List<double>(); foreach (var x in (m["cleanup"] as JArr ?? new JArr())) { var v = Js.Num(x); if (!l.Contains(v)) l.Add(v); } if (!l.Contains(id.Value)) l.Add(id.Value); m["cleanup"] = new JArr(l.Skip(Math.Max(0, l.Count - 50)).Cast<object?>()); });
    }
    void Leftover(int n) => UpdMeta(m => m["leftover"] = Js.Num(m["leftover"]) + n);
    void RunCleanup()
    {
        var m = Meta(); var list = (m["cleanup"] as JArr ?? new JArr()).Select(Js.Num).ToList(); if (list.Count == 0) return;
        var keep = new List<double>();
        foreach (var id in list)
        {
            if (id == Js.Num(m["backupMsg"]) || id == Js.Num(m["manifestMsg"])) continue;
            switch (DelMsg(id)) { case "net": keep.Add(id); break; case "old": Leftover(1); break; }
        }
        UpdMeta(mm => mm["cleanup"] = new JArr(keep.Cast<object?>()));
    }
    void Neutralize(double id)
    {
        try
        {
            var media = Json.Stringify(JObj.Of(("type", "document"), ("media", "attach://f"), ("caption", "Respaldo antiguo vaciado por Encargos. Puedes borrar este mensaje.")));
            Api.SendFile("editMessageMedia", new Dictionary<string, string> { ["chat_id"] = Js.Str(ChatId()), ["message_id"] = Js.NumberToString(id), ["media"] = media }, "f", "encargos-antiguo-vacio.txt", Crypto.Utf8("Encargos: respaldo antiguo sin datos."), "text/plain");
        }
        catch { }
        Leftover(1);
    }

    /* ---------- formato anterior (solo para migrar) ---------- */
    int DownloadMissing(JObj m)
    {
        var meta = Meta(); var done = new LinkedSet(Ls.Arr(K.pp).Select(Js.Str)); var applied = 0; var late = new List<JObj>();
        var list = new List<JObj>(); var snap = m["snapshot"] as JObj;
        if (snap != null && !done.Contains(snap.Str("packageId")) && Js.Num(meta["localVersion"]) < Js.Num(snap["version"])) list.Add(snap);
        if (m["packages"] is JArr pk) foreach (var p in pk.Objs()) if (!done.Contains(p.Str("packageId"))) list.Add(p);
        foreach (var p in list)
        {
            var bytes = FetchFile(p.Str("fileId"));
            if (p["checksum"] != null && Sha(bytes) != p.Str("checksum")) throw new TgError("Un paquete llegó dañado (checksum); se volverá a intentar");
            var pkg = (JObj)Unseal(bytes)!; if (!Js.Eq(pkg["databaseId"], Db()) || Js.Num(pkg["schemaVersion"]) > SCHEMA) throw new TgError("Paquete no compatible");
            var ops = late.Concat((pkg["operations"] as JArr)?.Objs() ?? new List<JObj>()).ToList();
            var r = OnUi(() => ApplyOps(ops, p.Str("packageId"))); late = r.Late; applied += r.N;
            done.Add(p.Str("packageId")); Ls.Put(K.pp, new JArr(done.TakeLast(5000)));
        }
        if (late.Count > 0) { var l2 = late; OnUi(() => ApplyOps(l2, "late")); }
        UpdMeta(mm => { mm["remoteVersion"] = m["latestVersion"]; if (Js.Num(mm["localVersion"]) < Js.Num(m["latestVersion"])) mm["localVersion"] = m["latestVersion"]; });
        return applied;
    }

    /* ---------- remoto distinto: descargar → checksum → descifrar → validar → MERGE ---------- */
    int PullLatest(Remote r)
    {
        var m = r.M!;
        byte[] bytes;
        try { bytes = FetchFile(m.Str("telegramFileId")); } catch (TgError) { throw new TgError("No se encontró el respaldo en Telegram; se volverá a intentar"); }
        if (Sha(bytes) != m.Str("checksum")) throw new TgError("El respaldo de Telegram llegó dañado (checksum); no se aplicó nada y se volverá a intentar");
        var pkg = Unseal(bytes) as JObj;
        if (pkg == null || !Js.Eq(pkg["type"], LATEST) || !Js.Eq(pkg["databaseId"], Db()) || !Js.Eq(pkg["packageId"], m["packageId"]) || pkg["operations"] is not JArr) throw new TgError("El respaldo de Telegram no tiene un formato válido; no se aplicó nada");
        if (Js.Num(pkg["schemaVersion"]) > SCHEMA) throw new TgError("Este dispositivo necesita actualizar Encargos para leer los datos");
        var ops = ((JArr)pkg["operations"]!).Objs();
        return OnUi(() =>
        {
            var r1 = ApplyOps(ops, "respaldo v" + Js.NumberToString(Js.Num(m["version"])), false); var n = r1.N; if (r1.Late.Count > 0) n += ApplyOps(r1.Late, "late", false).N;
            var rs = new Dictionary<string, JObj>(); foreach (var o in ops) rs[o.Str("entity") + ":" + o.Str("entityId")] = JObj.Of(("ts", Js.Num(o["timestamp"])), ("dev", o["deviceId"]));
            var stamps = Ls.Obj(K.st); var behind = stamps.Any(kv => !rs.TryGetValue(kv.Key, out var rv) || Newer((JObj)kv.Value!, rv));
            UpdMeta(mm =>
            {
                mm["appliedPackage"] = m["packageId"]; mm["remoteVersion"] = m["version"]; mm["localVersion"] = Math.Max(Js.Num(mm["localVersion"]), Js.Num(m["version"])); mm["backupMsg"] = m["messageId"]; mm["manifestMsg"] = r.PinId;
                mm["lastPull"] = (double)Js.NowMs(); mm["latest"] = JObj.Of(("version", m["version"]), ("size", m["size"] ?? (double)bytes.Length), ("checksum", m["checksum"]), ("timestamp", m["timestamp"]), ("deviceId", m["deviceId"]));
                if (behind) mm["dirty"] = true;
            });
            if (n == 0) Slog("in", $"Respaldo versión {Js.NumberToString(Js.Num(m["version"]))} revisado: sin cambios nuevos");
            return n;
        });
    }
    JArr SnapOps(string packageId, string dev, double baseV)
    {
        var s = Snapshot(); var stamps = Ls.Obj(K.st); var ops = new JArr(); var ch = false;
        foreach (var t in ENT)
            foreach (var kv in s[t])
            {
                var k = t + ":" + kv.Key; var st = stamps[k] as JObj;
                if (st == null || Js.Truthy(st["deleted"])) { st = JObj.Of(("ts", (double)Js.NowMs()), ("dev", dev)); stamps[k] = st; ch = true; }
                ops.Add(JObj.Of(("opId", $"L-{packageId}-{k}"), ("deviceId", st["dev"]), ("sequence", 0.0), ("timestamp", st["ts"]), ("entity", t), ("entityId", kv.Key), ("operation", "UPDATE"), ("data", kv.Value), ("baseVersion", baseV)));
            }
        foreach (var kv in stamps)
        {
            if (kv.Value is not JObj st || !Js.Truthy(st["deleted"])) continue; var i = kv.Key.IndexOf(':'); var t = kv.Key[..i]; var id = kv.Key[(i + 1)..];
            if (s.TryGetValue(t, out var sm) && sm.ContainsKey(id)) continue;
            ops.Add(JObj.Of(("opId", $"L-{packageId}-del-{kv.Key}"), ("deviceId", st["dev"]), ("sequence", 0.0), ("timestamp", st["ts"]), ("entity", t), ("entityId", id), ("operation", "DELETE"), ("data", null), ("deleted", true), ("baseVersion", baseV)));
        }
        if (ch) Ls.Put(K.st, stamps); return ops;
    }
    sealed record Published(bool Ok, int Ops = 0, double Version = 0.0);
    Published Publish(Remote r)
    {
        var dev = DeviceId(); var meta = Meta();
        var baseV = Math.Max(r.Kind == "latest" ? Js.Num(r.M!["version"]) : r.Kind == "legacy" ? Js.Num(r.M!["latestVersion"]) : 0.0, Js.Num(meta["localVersion"]));
        var version = baseV + 1; var packageId = Crypto.Uuid();
        // 1) paquete (con la cola local ya integrada) → 2) gzip → 3) cifrar
        var (pkg, qIds) = OnUi(() =>
        {
            var q = Queue().Objs().Select(o => o.Str("opId")).ToList();
            return (JObj.Of(("type", LATEST), ("schemaVersion", (double)SCHEMA), ("databaseId", Db()), ("deviceId", dev), ("packageId", packageId), ("version", version), ("baseVersion", baseV),
                ("createdAt", Js.NowIso()), ("operations", SnapOps(packageId, dev, baseV))), q);
        });
        var bytes = Seal(pkg); if (bytes.Length > MAX_BYTES) throw new TgError("El respaldo pesa más de 20 MB (límite de Telegram para bots). Elimina fotos que ya no necesites.");
        var checksum = Sha(bytes);
        // 4) subir el nuevo (el anterior no se toca)
        var (msgId, fileId) = SendFile(bytes, BK_NAME, $"{LATEST}\nVersión {Js.NumberToString(version)}");
        // 5) verificar: se descarga otra vez y se compara el checksum
        try { var back = FetchFile(fileId); if (Sha(back) != checksum) throw new TgError("El archivo subido a Telegram no coincide (checksum). El respaldo anterior se conserva; se volverá a intentar."); }
        catch { if (DelMsg(msgId) == "net") AddCleanup(msgId); throw; }
        // 6) registrar en el coordinador, solo si nadie publicó mientras tanto
        Remote cur;
        try { cur = ReadRemote(); } catch { AddCleanup(msgId); throw; }
        if (!SameHead(cur, r)) { if (DelMsg(msgId) == "net") AddCleanup(msgId); return new Published(false); }
        var man = JObj.Of(("type", LATEST), ("version", version), ("packageId", packageId), ("deviceId", dev), ("timestamp", Js.NowIso()), ("checksum", checksum), ("telegramFileId", fileId),
            ("messageId", msgId), ("size", (double)bytes.Length), ("schemaVersion", (double)SCHEMA), ("db", DbTag()));
        var prev = new JArr();
        void AddPrev(object? id, bool backup = false) { var v = Js.Num(id); if (v != 0.0 && !prev.Objs().Any(p => Js.Num(p["id"]) == v)) prev.Add(JObj.Of(("id", v), ("backup", backup))); }
        if (cur.Kind == "latest") { if (cur.M!["messageId"] != null) AddPrev(cur.M["messageId"], true); }
        else if (cur.Kind == "legacy") { AddPrev(cur.PinId); if (cur.M!["snapshot"] is JObj sn) AddPrev(sn["messageId"]); if (cur.M["packages"] is JArr pk) foreach (var p in pk.Objs()) AddPrev(p["messageId"]); }
        if (!cur.Foreign) { if (meta["backupMsg"] != null) AddPrev(meta["backupMsg"], true); if (cur.Kind != "latest" && meta["manifestMsg"] != null) AddPrev(meta["manifestMsg"]); }
        var f = JObj.Of(("packageId", packageId), ("messageId", msgId), ("version", version), ("qIds", new JArr(qIds.Cast<object?>())), ("prev", prev), ("man", man));
        UpdMeta(m => m["inflight"] = f);
        try { WriteManifest(man, cur); }
        catch (TgError e) { if (e.Code != null) { UpdMeta(m => m.Remove("inflight")); if (DelMsg(msgId) == "net") AddCleanup(msgId); } throw; }
        Thread.Sleep(IsTest ? 150 : 1200);
        var chk = ReadRemote();
        if (chk.Kind != "latest" || !Js.Eq(chk.M!["packageId"], packageId)) { UpdMeta(m => m.Remove("inflight")); if (DelMsg(msgId) == "net") AddCleanup(msgId); return new Published(false); }
        Finalize(f, chk.PinId);
        return new Published(true, ((JArr)pkg["operations"]!).Count, version);
    }
    /// <summary>7) versión registrada → 8) recién ahora se borra lo anterior → 9) queda solo el respaldo nuevo</summary>
    void Finalize(JObj f, double pinId)
    {
        var man = (JObj)f["man"]!;
        OnUi(() =>
        {
            var q = (f["qIds"] as JArr ?? new JArr()).Select(Js.Str).ToHashSet(); SaveQ(Queue().Objs().Where(o => !q.Contains(o.Str("opId"))));
            var po = new LinkedSet(Ls.Arr(K.po).Select(Js.Str)); foreach (var x in q) po.Add(x); Ls.Put(K.po, new JArr(po.TakeLast(20000)));
            UpdMeta(mm =>
            {
                mm["localVersion"] = f["version"]; mm["remoteVersion"] = f["version"]; mm["lastSyncedVersion"] = f["version"]; mm["appliedPackage"] = f["packageId"]; mm["backupMsg"] = f["messageId"]; mm["manifestMsg"] = pinId;
                mm["lastSync"] = (double)Js.NowMs(); mm["lastPublish"] = (double)Js.NowMs(); mm["dirty"] = false; mm["linked"] = true;
                mm["latest"] = JObj.Of(("version", f["version"]), ("size", man["size"]), ("checksum", man["checksum"]), ("timestamp", man["timestamp"]), ("deviceId", man["deviceId"])); mm.Remove("inflight"); mm.Remove("manifestCache");
            });
            var c = Cfg(); if (c != null && c.ContainsKey("fresh")) { c.Remove("fresh"); PutCfg(c); }
            ForceSnap = false;
            Slog("out", $"Respaldo único actualizado · versión {Js.NumberToString(Js.Num(f["version"]))}");
        });
        var old = 0;
        if (f["prev"] is JArr prev)
            foreach (var p in prev.Objs())
            {
                var id = Js.Num(p["id"]); if (id == Js.Num(f["messageId"]) || id == pinId) continue;
                switch (DelMsg(id)) { case "net": AddCleanup(id); break; case "old": if (Js.Truthy(p["backup"])) Neutralize(id); else { old++; Leftover(1); } break; }
            }
        if (old > 0) Slog("warn", $"{old} archivo{(old == 1 ? "" : "s")} del sistema anterior no se pudo{(old == 1 ? "" : "ieron")} borrar (Telegram no deja al bot borrar mensajes de más de 48 h). Puedes borrarlos a mano en el chat.");
        RunCleanup();
    }
    void ResolveInflight(Remote r)
    {
        if (Meta()["inflight"] is not JObj f) return;
        if (r.Kind == "latest" && Js.Eq(r.M!["packageId"], f["packageId"])) { Finalize(f, r.PinId); return; }
        UpdMeta(m => m.Remove("inflight"));
        if (!(r.Kind == "latest" && Js.Num(r.M!["messageId"]) == Js.Num(f["messageId"]))) AddCleanup(Js.Num(f["messageId"]));
    }

    public volatile bool ForceSnap;
    int Cycle()
    {
        if (Js.Truthy(Meta()["linked"])) OnUi(() => Collect());
        for (var round = 0; round < 5; round++)
        {
            var r = ReadRemote();
            ResolveInflight(r);
            var meta = Meta();
            if (r.Kind == "latest" && !Js.Eq(r.M!["packageId"], meta["appliedPackage"])) PullLatest(r);
            else if (r.Kind == "latest") UpdMeta(m => m["remoteVersion"] = r.M!["version"]);
            else if (r.Kind == "legacy") { DownloadMissing(r.M!); UpdMeta(m => m["dirty"] = true); }
            OnUi(() =>
            {
                if (!Js.Truthy(Meta()["linked"]))
                {
                    var stamps = Ls.Obj(K.st); var known = Ls.Obj(K.h); var snap = Snapshot();
                    foreach (var t in ENT) foreach (var kv in snap[t]) { var k = t + ":" + kv.Key; if (stamps[k] != null) known[k] = HashOf(kv.Value); }
                    Ls.Put(K.h, known); UpdMeta(m => m["linked"] = true);
                }
                Collect();
            });
            meta = Meta();
            var force = ForceSnap; var q = Queue(); var hasData = _app.Data.Count > 0 || !Ls.Obj(K.st).IsEmpty;
            var stale = r.Kind == "latest" && Js.NowMs() - Js.ParseDate(r.M!["timestamp"]) > REFRESH_MS;
            if (!(force || q.Count > 0 || Js.Truthy(meta["dirty"]) || (r.Kind != "latest" && hasData) || stale)) { RunCleanup(); return 0; }
            if (!force && r.Kind == "latest" && meta["lastPublish"] != null && Js.NowMs() - Js.Num(meta["lastPublish"]) < MinGap())
            {
                Schedule(MinGap() - (Js.NowMs() - (long)Js.Num(meta["lastPublish"])) + 300); return 0;
            }
            var res = Publish(r);
            if (res.Ok) return res.Ops;
            // otro dispositivo publicó a la vez: se combina lo suyo y se vuelve a intentar
        }
        throw new TgError("Otro dispositivo está sincronizando al mismo tiempo; se reintentará");
    }

    /* ---------- ejecución: un ciclo a la vez, reintentos con espera ---------- */
    readonly Thread _worker; readonly BlockingCollection<Action> _jobs = new();
    void WorkLoop() { foreach (var j in _jobs.GetConsumingEnumerable()) { try { j(); } catch { } } }
    void Post(Action a) => _jobs.Add(a);
    Timer? _retry, _later, _deb;
    Timer After(long ms, Action a) => new(_ => Post(a), null, Math.Max(0, ms), Timeout.Infinite);

    public volatile string State = "idle";
    public volatile string LastErr = "";
    volatile bool _running, _again;
    int _backoff;
    public Action? OnState;
    void Paint() { try { OnState?.Invoke(); } catch { } }
    void Schedule(long ms) { _later?.Dispose(); _later = After(ms, () => SyncNowBlocking()); }

    /// <summary>Lanza una sincronización en segundo plano.</summary>
    public void SyncNow(Action<bool, string>? done = null) => Post(() => { var ok = SyncNowBlocking(); done?.Invoke(ok, LastErr); });
    /// <summary>Ejecuta un trabajo en el hilo de sincronización (para no bloquear la interfaz).</summary>
    public Task<T> Run<T>(Func<T> f) { var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously); Post(() => { try { tcs.SetResult(f()); } catch (Exception e) { tcs.SetException(e); } }); return tcs.Task; }

    /// <summary>Sincroniza en el hilo actual.</summary>
    public bool SyncNowBlocking()
    {
        if (!Enabled()) return false;
        lock (this) { if (_running) { _again = true; return false; } _running = true; }
        State = "syncing"; Paint();
        try
        {
            Cycle(); State = "ok"; LastErr = ""; _backoff = 0; UpdMeta(m => m["lastSync"] = (double)Js.NowMs()); return true;
        }
        catch (Exception e)
        {
            var off = e is IOException && e is not TgError;
            var code = (e as TgError)?.Code;
            State = off ? "offline" : "error";
            LastErr = off ? "Sin conexión. Tus cambios se guardaron en este equipo." : code == 401 ? "El token del bot no es válido." :
                code == 400 && (e.Message ?? "").Contains("chat not found", StringComparison.OrdinalIgnoreCase) ? "No se encuentra el chat de Telegram." : e.Message ?? e.ToString();
            Slog("err", LastErr);
            var wait = (e as TgError)?.RetryAfter is int ra ? ra * 1000L : BACKOFF[Math.Min(_backoff, BACKOFF.Length - 1)]; _backoff++;
            _retry?.Dispose(); _retry = After(wait, () => SyncNowBlocking());
            return false;
        }
        finally
        {
            lock (this) _running = false; Paint();
            if (_again) { _again = false; Schedule(600); }
        }
    }

    /// <summary>Después de cada guardado: agrupa 2 s y sincroniza.</summary>
    public void ChangedLocally() { if (!Enabled()) return; _deb?.Dispose(); _deb = After(2000, () => { OnUi(() => Collect()); SyncNowBlocking(); }); }

    public sealed record Status(string Kind, string Text);
    public Status GetStatus()
    {
        var c = Cfg(); if (c == null) return new Status("off", "Tu negocio, en orden");
        if (Js.Truthy(c["paused"])) return new Status("off", "Sincronización en pausa");
        var p = Pending(); var m = Meta();
        if (State == "syncing") return new Status("run", "Sincronizando…");
        if (State == "offline") return new Status("offline", p > 0 ? $"Sin conexión · {p} pendiente{(p == 1 ? "" : "s")}" : "Sin conexión");
        if (State == "error") return new Status("error", "Error de sincronización");
        if (p > 0) return new Status("pending", $"Pendiente de sincronizar ({p})");
        var ls2 = Js.Num(m["lastSync"]);
        if (ls2 > 0)
        {
            var s = Math.Max(0L, (Js.NowMs() - (long)ls2) / 1000);
            return new Status("ok", "Sincronizado " + (s < 60 ? $"hace {s} s" : s < 3600 ? $"hace {Math.Round(s / 60.0)} min" : $"hace {Math.Round(s / 3600.0)} h"));
        }
        return new Status("ok", "Sincronizado");
    }
    public JObj MetaInfo() => Meta();

    /* ---------- conectar, enlazar, restaurar ---------- */
    public abstract record ConnectResult
    {
        public sealed record Ok : ConnectResult; public sealed record NeedStart : ConnectResult; public sealed record Existing : ConnectResult; public sealed record Error(string Msg) : ConnectResult;
    }
    string? _pendingTok; object? _pendingChat;
    static readonly string[] SYNC_KEYS = { K.h, K.st, K.pp, K.po, K.meta, K.q };

    /// <summary>"Este es mi primer dispositivo": conecta el bot con el token. Bloqueante (llamar fuera del hilo de la interfaz).</summary>
    public ConnectResult Connect(string tok)
    {
        var t = tok.Trim(); if (!Regex.IsMatch(t, "^[0-9]{5,}:[A-Za-z0-9_-]{20,}$")) return new ConnectResult.Error("Ese token no parece válido. Cópialo completo desde @BotFather.");
        try
        {
            PutCfg(JObj.Of(("token", t), ("chatId", null), ("key", null), ("db", null), ("paused", true)));
            Tg("getMe", new JObj());
            var ups = (Tg("getUpdates", JObj.Of(("limit", 100.0), ("timeout", 0.0))) as JArr)?.Objs() ?? new List<JObj>();
            var priv = ups.Select(u => (u["message"] ?? u["edited_message"]) as JObj).Where(m => m != null && Js.Eq((m["chat"] as JObj)?["type"], "private")).ToList();
            if (priv.Count == 0) { PutCfg(null); return new ConnectResult.NeedStart(); }
            var chat = ((JObj)priv[^1]!["chat"]!)["id"];
            PutCfg(JObj.Of(("token", t), ("chatId", chat), ("key", Crypto.B64U.Enc(Crypto.Random(32))), ("db", Crypto.Uuid()), ("paused", false)));
            var ch = (JObj)Tg("getChat", JObj.Of(("chat_id", chat)))!; var pm = ch["pinned_message"] as JObj;
            if (IsLatestPin(pm) || IsLegacyPin(pm)) { PutCfg(null); _pendingTok = t; _pendingChat = chat; return new ConnectResult.Existing(); }
            OnUi(() => { foreach (var k in SYNC_KEYS) Ls.Del(k); });
            Slog("ok", $"Bot conectado en {_app.Platform}"); ForceSnap = true; OnUi(() => Collect());
            return SyncNowBlocking() ? (ConnectResult)new ConnectResult.Ok() : new ConnectResult.Error(LastErr);
        }
        catch (Exception e) { PutCfg(null); return new ConnectResult.Error((e as TgError)?.Code == 401 ? "El token no es válido." : "No se pudo conectar con Telegram: " + e.Message); }
    }
    /// <summary>"Empezar de nuevo con este bot" (cuando no se tiene el código).</summary>
    public bool FreshStart()
    {
        var t = _pendingTok; var c = _pendingChat; if (t == null || c == null) return false;
        OnUi(() => { foreach (var k in SYNC_KEYS) Ls.Del(k); });
        PutCfg(JObj.Of(("token", t), ("chatId", c), ("key", Crypto.B64U.Enc(Crypto.Random(32))), ("db", Crypto.Uuid()), ("paused", false), ("fresh", true)));
        Slog("ok", $"Sincronización nueva creada en {_app.Platform}"); ForceSnap = true; OnUi(() => Collect());
        return SyncNowBlocking();
    }
    public string? LinkCode() { var c = Cfg(); return c == null ? null : new global::Encargos.Core.LinkCode(c.Str("token"), c["chatId"]!, c.Str("key"), c.Str("db")).Encode(); }
    /// <summary>"Ya lo tengo en otro dispositivo": pega el código ENC1.</summary>
    public ConnectResult Link(string code)
    {
        var o = global::Encargos.Core.LinkCode.Parse(code); if (o == null) return new ConnectResult.Error("El código no es válido. Cópialo completo.");
        OnUi(() => { foreach (var k in SYNC_KEYS) Ls.Del(k); });
        PutCfg(JObj.Of(("token", o.Token), ("chatId", o.ChatId), ("key", o.Key), ("db", o.Db), ("paused", false)));
        Slog("ok", $"Dispositivo enlazado: {_app.Platform}");
        return SyncNowBlocking() ? (ConnectResult)new ConnectResult.Ok() : new ConnectResult.Error(LastErr);
    }
    public bool BackupNow() { ForceSnap = true; OnUi(() => Collect()); return SyncNowBlocking(); }
    public void Pause(bool p) { var c = Cfg(); if (c == null) return; c["paused"] = p; PutCfg(c); Paint(); if (!p) SyncNow(); }
    public void Disconnect() { PutCfg(null); OnUi(() => { foreach (var k in SYNC_KEYS) Ls.Del(k); }); State = "idle"; LastErr = ""; Paint(); }

    /// <summary>Restaurar: comprueba el archivo ANTES de tocar los datos locales; luego reconstruye este equipo.</summary>
    public Res<bool> Restore()
    {
        try
        {
            var r = ReadRemote();
            if (r.Kind == "none") return Res.Fail("Todavía no hay un respaldo en Telegram.");
            if (r.Kind == "latest") { var b = FetchFile(r.M!.Str("telegramFileId")); if (Sha(b) != r.M.Str("checksum")) throw new TgError("el respaldo llegó dañado (checksum)"); Unseal(b); }
            var keep = Meta();
            OnUi(() =>
            {
                _app.Data.Clear(); foreach (var k in new[] { "encargos_notes", "encargos_expenses", "encargos_client_info" }) Ls.Del(k);
                foreach (var k in new[] { K.h, K.st, K.pp, K.po, K.q }) Ls.Del(k);
                PutMeta(JObj.Of(("cleanup", keep["cleanup"] ?? new JArr()), ("backupMsg", keep["backupMsg"]), ("manifestMsg", keep["manifestMsg"]), ("leftover", Js.Num(keep["leftover"]))));
                _app.Storage.Set(EncargosApp.KEY, "[]");
            });
            if (r.Kind == "latest") PullLatest(r); else DownloadMissing(r.M!);
            OnUi(() =>
            {
                var snap = Snapshot(); var known = new JObj(); var stamps = Ls.Obj(K.st);
                foreach (var t in ENT) foreach (var kv in snap[t]) { var k = t + ":" + kv.Key; if (stamps[k] != null) known[k] = HashOf(kv.Value); }
                Ls.Put(K.h, known);
                UpdMeta(m => { m["linked"] = true; m["dirty"] = r.Kind != "latest"; m["lastSync"] = (double)Js.NowMs(); });
                Slog("ok", "Respaldo restaurado"); _app.LoadShippingConfig(); _app.Refresh();
            });
            return Res.Ok();
        }
        catch (Exception e) { return Res.Fail(e.Message); }
    }
}
