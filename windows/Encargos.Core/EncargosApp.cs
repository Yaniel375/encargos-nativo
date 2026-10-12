using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Encargos.Core;

/// <summary>
/// Núcleo de Encargos: los mismos datos, cálculos y reglas que la app web y que la app Android.
/// Todo se guarda con las mismas claves (encargos_v3_data, encargos_notes, …).
/// La interfaz llama a estas funciones y vuelve a dibujar cuando cambia <see cref="Version"/>.
/// </summary>
public sealed class EncargosApp
{
    public readonly IStorage Storage;
    public readonly string Platform;
    public readonly LS Ls;
    public long Version { get; private set; }
    readonly List<Action> _listeners = new();
    public void OnChange(Action l) { lock (_listeners) _listeners.Add(l); }
    /// <summary>Se llama cada vez que se guardan datos del negocio (la sincronización se engancha aquí).</summary>
    public Action? OnPersist;
    /// <summary>true mientras se aplican cambios que llegaron de otro dispositivo.</summary>
    public bool ApplyingRemote;

    public JArr Data = new();
    public JObj ShippingRates = new(); public JObj ShippingNames = new();

    public readonly Notifications Notifs;
    public readonly Attention Attention;

    public const string KEY = "encargos_v3_data";
    public const string SHIPPING_KEY = "encargos_shipping_rates_v2";
    public static readonly (string, double)[] DEFAULT_SHIPPING_RATES = { ("avion", 7.0), ("barco", 4.0) };
    public static readonly (string, string)[] DEFAULT_SHIPPING_NAMES = { ("avion", "✈️ Avión"), ("barco", "🚢 Barco") };
    public static readonly string[] FLOW = { "Pedido", "En tránsito", "Recibido", "Entregado" };
    public static readonly string[] STATES = { "Pedido", "En tránsito", "Recibido", "Entregado", "Cancelado" };
    public static readonly (string, double)[] DEF_BUY = { ("avion", 5.0), ("barco", 2.8) };
    public static readonly Dictionary<string, string> DEF_TPL = new()
    {
        ["arrival"] = "Hola {nombre}, te informamos que tu pedido ya llegó. 📦✅\n\nPor favor, ponte en contacto conmigo para coordinar la recogida.",
        ["reminder"] = "Hola {nombre}, ¿cómo estás? 😊 Te escribo para recordarte que tienes un saldo pendiente de {saldo} por tu pedido (total {total}, pagado {pagado}).\n\nCuando puedas, me avisas. ¡Gracias!",
        ["thanks"] = "Hola {nombre}, ¡muchas gracias por tu compra! 🙌 Fue un placer atenderte. Aquí estoy para tu próximo encargo.",
    };
    public static readonly (string, string)[] EXP_CATS = { ("transporte", "Transporte"), ("aduana", "Aduana"), ("empaque", "Empaques"), ("otro", "Otros") };
    const string CODE_CHARS = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";

    public EncargosApp(IStorage storage, string platform = "PC")
    {
        Storage = storage; Platform = platform; Ls = new LS(storage);
        Notifs = new Notifications(this); Attention = new Attention(this);
        Reload();
    }

    /// <summary>Vuelve a leer todo desde el almacenamiento (también tras importar o restaurar).</summary>
    public void Reload() { Data = Load(); LoadShippingConfig(); Notifs.ResetSnapshot(); Changed(false); }

    public void Changed(bool fromPersist = true)
    {
        Version++;
        if (fromPersist) { try { Notifs.Diff(); } catch { } }
        List<Action> ls; lock (_listeners) ls = _listeners.ToList();
        foreach (var l in ls) { try { l(); } catch { } }
    }

    /* ---------------- Carga y guardado ---------------- */
    static string RandCode() { var sb = new StringBuilder(); for (var i = 0; i < 6; i++) sb.Append(CODE_CHARS[RandomNumberGenerator.GetInt32(CODE_CHARS.Length)]); return sb.ToString(); }
    public string GenAccessCode(IEnumerable<object?> existing) { string c; var l = existing.ToList(); do { c = RandCode(); } while (l.Any(it => it is JObj o && Js.Eq(o["accessCode"], c))); return c; }

    public bool NormalizeClientAccessCodes(JArr list)
    {
        var byName = new Dictionary<string, string>(); var used = new HashSet<string>(); var changed = false;
        foreach (var a in list)
        {
            if (a is not JObj x) continue;
            var k = x.Str("name").Trim().ToLowerInvariant(); if (k.Length == 0) continue;
            if (!byName.ContainsKey(k))
            {
                var cand = x.Str("accessCode").Trim();
                if (cand.Length > 0 && !used.Contains(cand)) { byName[k] = cand; used.Add(cand); }
                else { string code; do { code = RandCode(); } while (used.Contains(code)); byName[k] = code; used.Add(code); }
            }
            if (!Js.Eq(x["accessCode"], byName[k])) { x["accessCode"] = byName[k]; changed = true; }
        }
        return changed;
    }

    JArr Load()
    {
        try
        {
            var a = Json.Arr(Storage.Get(KEY)); if (a == null) return new JArr();
            var arr = new JArr(a.Where(it => it is JObj));
            double next = 0; var changed = false;
            foreach (var o in Enumerable.Reverse(arr).ToList())
            {
                var x = (JObj)o!;
                var n = Js.Num(x["orderNo"]);
                if (n == 0.0 || double.IsNaN(n)) { next += 1; x["orderNo"] = next; changed = true; } else next = Math.Max(next, n);
                if (!Js.Truthy(x["accessCode"])) { x["accessCode"] = GenAccessCode(arr); changed = true; }
            }
            if (NormalizeClientAccessCodes(arr)) changed = true;
            if (changed) Storage.Set(KEY, Json.Stringify(arr));
            return arr;
        }
        catch { return new JArr(); }
    }

    /// <summary>persist(): guarda los encargos y avisa a la sincronización.</summary>
    public bool Persist()
    {
        try { Storage.Set(KEY, Json.Stringify(Data)); if (!ApplyingRemote) OnPersist?.Invoke(); Changed(); return true; }
        catch { return false; }
    }
    /// <summary>Guarda otra clave del negocio (notas, gastos, ajustes) y avisa a la sincronización.</summary>
    public bool PutBusiness(string key, object? v) { var ok = Ls.Put(key, v); if (ok) { if (!ApplyingRemote) OnPersist?.Invoke(); Changed(); } return ok; }

    /* ---------------- Tarifas de envío ---------------- */
    public void LoadShippingConfig()
    {
        var rates = new JObj(); foreach (var (k, v) in DEFAULT_SHIPPING_RATES) rates[k] = v;
        var names = new JObj(); foreach (var (k, v) in DEFAULT_SHIPPING_NAMES) names[k] = v;
        try
        {
            if (Ls.JsonOf("encargos_shipping_rates_v1") is JObj legacy)
            {
                var a = Js.Num(legacy["avion"]); if (double.IsFinite(a) && legacy["avion"] != null) rates["avion"] = a;
                var b = Js.Num(legacy["barco"]); if (double.IsFinite(b) && legacy["barco"] != null) rates["barco"] = b;
            }
            if (Ls.JsonOf(SHIPPING_KEY) is JObj raw)
            {
                if (raw["rates"] is JObj r) foreach (var kv in r) { var n = Js.Num(kv.Value); if (double.IsFinite(n) && n >= 0) rates[kv.Key] = n; }
                if (raw["names"] is JObj nm) foreach (var kv in nm) { var s = Js.Str(kv.Value).Trim(); if (s.Length > 0) names[kv.Key] = s; }
            }
        }
        catch { }
        ShippingRates = rates; ShippingNames = names;
    }
    public bool SaveShippingRates() => PutBusiness(SHIPPING_KEY, JObj.Of(("rates", ShippingRates), ("names", ShippingNames)));
    public string ShippingLabel(string? type) { var t = string.IsNullOrEmpty(type) ? "avion" : type; return ShippingNames[t] as string ?? ("📦 " + Regex.Replace(t, "[-_]+", " ")); }
    public string ShippingName(string? type) => Regex.Replace(ShippingLabel(type), @"^\S+\s", "");
    public double RateOf(string? type)
    {
        var t = string.IsNullOrEmpty(type) ? "avion" : type;
        if (ShippingRates[t] is { } v) return Js.Num(v);
        foreach (var (k, d) in DEFAULT_SHIPPING_RATES) if (k == t) return d;
        return 0.0;
    }
    public string? AddShippingRate(string name, double price)
    {
        if (string.IsNullOrWhiteSpace(name) || !double.IsFinite(price) || price < 0) return null;
        var key = Js.NormSearch(name);
        key = Regex.Replace(key, "[^a-z0-9]+", "_").Trim('_').Take(28).IfEmpty("envio");
        if (ShippingRates.ContainsKey(key)) key = key + "_" + Js.NowMs().ToString().TakeLast(5);
        ShippingRates[key] = price; ShippingNames[key] = "📦 " + name.Trim(); SaveShippingRates(); return key;
    }
    public void RemoveShippingRate(string key) { if (key == "avion" || key == "barco") return; ShippingRates.Remove(key); ShippingNames.Remove(key); SaveShippingRates(); }
    public bool SetShippingRates(IDictionary<string, double> values)
    {
        if (values.Values.Any(v => !double.IsFinite(v) || v < 0)) return false;
        var r = new JObj(); foreach (var k in ShippingRates.Keys) r[k] = values.TryGetValue(k, out var v) ? v : Js.Num(ShippingRates[k]);
        ShippingRates = r; SaveShippingRates(); return true;
    }

    /* ---------------- Ajustes simples ---------------- */
    public string PhoneCC
    {
        get => Regex.Replace(Ls.Get("encargos_phone_cc_v1", "53") ?? "53", @"\D", "").IfEmpty("53");
        set { Ls.Set("encargos_phone_cc_v1", Regex.Replace(value, @"\D", "")); Changed(false); }
    }
    public string BusinessName
    {
        get { var v = Ls.Get("encargos_business_name", null); return string.IsNullOrEmpty(v) ? "Encargos" : v; }
        set { Ls.Set("encargos_business_name", value.Trim().IfEmpty("Encargos")); if (!ApplyingRemote) OnPersist?.Invoke(); Changed(false); }
    }
    public string GreetName
    {
        get { var v = Ls.Get("encargos_greet_name", null); return string.IsNullOrEmpty(v) ? "Yaniel" : v; }
        set { Ls.Set("encargos_greet_name", value.Trim()); Changed(false); }
    }
    static int IntOr(string? s, int d) => double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x) ? (int)x : d;
    public int TransitWarn { get => Math.Max(1, IntOr(Ls.Get("encargos_transit_warn", "7"), 7)); set => Ls.Set("encargos_transit_warn", value.ToString()); }
    public int TransitLate { get => Math.Max(2, IntOr(Ls.Get("encargos_transit_late", "15"), 15)); set => Ls.Set("encargos_transit_late", value.ToString()); }
    public void SaveTransitAlerts(int w, int l) { var ww = Math.Max(1, w); TransitWarn = ww; TransitLate = Math.Max(ww + 1, l); Changed(false); }

    public JObj BuyRates() { var r = new JObj(); foreach (var (k, v) in DEF_BUY) r[k] = v; if (Ls.JsonOf("encargos_buy_rates") is JObj o) foreach (var kv in o) r[kv.Key] = kv.Value; return r; }
    public void SaveBuyRates(IDictionary<string, double> values, bool applyToActive)
    {
        var r = new JObj(); foreach (var kv in values) r[kv.Key] = kv.Value;
        PutBusiness("encargos_buy_rates", r);
        if (applyToActive)
        {
            var done = CompletedClients().Select(c => c.Name.Trim().ToLowerInvariant()).ToHashSet();
            foreach (var x in Orders()) if (!done.Contains(x.Str("name").Trim().ToLowerInvariant())) { var t = TypeOf(x); if (r[t] != null) x["lbBuy"] = r[t]; }
            Persist();
        }
    }
    public double BuyOf(JObj x) => x["lbBuy"] != null && !Js.Eq(x["lbBuy"], "") ? Js.Num(x["lbBuy"]) : Js.Num(BuyRates()[TypeOf(x)]);
    public double MarginOf(JObj x) => Js.Num(x["lbCost"]) - BuyOf(x);
    public double Profit(JObj x) => Js.Eq(x["status"], "Cancelado") ? 0.0 : Js.Num(x["pounds"]) * MarginOf(x);
    public string TypeOf(JObj x) => x.Str("shippingType").IfEmpty("avion");

    public JObj Templates() => Ls.JsonOf("encargos_templates") as JObj ?? new JObj();
    public string Tpl(string k) { var t = Templates()[k]; return t != null && Js.Str(t).Trim().Length > 0 ? Js.Str(t) : DEF_TPL.GetValueOrDefault(k, ""); }
    public bool SaveTemplates(string arrival, string reminder, string thanks) => PutBusiness("encargos_templates", JObj.Of(("arrival", arrival), ("reminder", reminder), ("thanks", thanks)));
    public bool ResetTemplates() => PutBusiness("encargos_templates", new JObj());

    /* ---------------- Encargos ---------------- */
    public List<JObj> Orders() => Data.Objs();
    public JObj? ById(object? id) { var s = Js.Str(id); return Orders().FirstOrDefault(o => Js.Str(o["id"]) == s); }
    public string OrderLabel(JObj x)
    {
        var n = Js.Num(x["orderNo"]); long v = double.IsFinite(n) && n > 0 ? (long)n : Math.Max(1, Data.IndexOf(x) + 1);
        return "ENC-" + v.ToString().PadLeft(4, '0');
    }

    static double R2(object? v) => Js.R2(v);
    static string Sig(JObj x) => Json.Stringify(JArr.Of(x["name"], x["phone"], x["paid"], x["shipping"], x["total"], x["balance"]));
    /// <summary>recalcOrder de la app (redondeo a centavos).</summary>
    public bool RecalcOrder(JObj x)
    {
        var before = Sig(x);
        x["name"] = (x["name"] == null ? "" : Js.Str(x["name"])).Trim().IfEmpty("Sin nombre");
        x["phone"] = x["phone"] == null ? "" : Js.Str(x["phone"]);
        x["paid"] = R2(x["paid"]);
        var sh = x["shipping"];
        x["shipping"] = sh == null || Js.Eq(sh, "") ? R2(Js.Num(x["lbCost"]) * Js.Num(x["pounds"])) : R2(sh);
        x["total"] = R2(Js.Num(x["cost"]) + Js.Num(x["shipping"]));
        x["balance"] = R2(Js.Num(x["total"]) - Js.Num(x["paid"]));
        return before != Sig(x);
    }
    public bool RecalcAll() { var ch = false; foreach (var x in Orders()) if (RecalcOrder(x)) ch = true; return ch; }
    /// <summary>renderAll de la app recalcula y guarda si algo cambió.</summary>
    public void Refresh() { if (RecalcAll()) { try { Storage.Set(KEY, Json.Stringify(Data)); } catch { } } Changed(); }

    public void Stamp(JObj x, string st) { if (x["statusDates"] is not JObj sd) { sd = new JObj(); x["statusDates"] = sd; } if (sd[st] == null) sd[st] = Js.NowIso(); }

    public sealed record NewOrder(string Name, string Phone, double Cost, double LbCost, double Pounds, double Fund,
        string FundMethod = "Efectivo", string Status = "Pedido", string ShippingType = "avion", string Photo = "");

    /// <summary>save() de la app: crea un encargo.</summary>
    public Res<JObj> CreateOrder(NewOrder o)
    {
        var name = o.Name.Trim(); if (name.Length == 0) return Res<JObj>.Fail("Escribe el nombre del cliente.");
        if (new[] { o.Cost, o.LbCost, o.Pounds, o.Fund }.Any(v => !double.IsFinite(v) || v < 0)) return Res<JObj>.Fail("Los valores no pueden ser negativos.");
        var now = Js.NowIso(); var nowMs = Js.NowMs();
        double id = nowMs + (long)(System.Random.Shared.NextDouble() * 999); while (ById(id) != null) id += 1;
        var maxNo = Orders().Aggregate(0.0, (m, a) => { var v = Js.Num(a["orderNo"]); return Math.Max(m, double.IsNaN(v) ? 0 : v); });
        var payment = o.Fund != 0.0 ? JArr.Of(JObj.Of(("id", (double)nowMs), ("date", now), ("amount", o.Fund), ("method", o.FundMethod), ("note", "Pago inicial"))) : new JArr();
        var existing = Orders().FirstOrDefault(it => it.Str("name").Trim().ToLowerInvariant() == name.ToLowerInvariant());
        var code = existing != null && Js.Str(existing["accessCode"]).Length > 0 ? Js.Str(existing["accessCode"]) : GenAccessCode(Data);
        var ship = o.LbCost * o.Pounds;
        var x = JObj.Of(("id", id), ("orderNo", maxNo + 1), ("accessCode", code), ("name", name), ("phone", o.Phone.Trim()), ("cost", o.Cost), ("lbCost", o.LbCost),
            ("pounds", o.Pounds), ("shippingType", o.ShippingType), ("shipping", ship), ("total", o.Cost + ship), ("paid", o.Fund), ("balance", o.Cost + ship - o.Fund),
            ("status", o.Status), ("date", now), ("updated", now), ("paymentHistory", payment),
            ("lbHistory", o.Pounds != 0.0 ? JArr.Of(JObj.Of(("date", now), ("from", 0.0), ("to", o.Pounds), ("change", o.Pounds))) : new JArr()), ("photo", o.Photo));
        Stamp(x, o.Status.IfEmpty("Pedido"));
        x["lbBuy"] = Js.Num(BuyRates()[o.ShippingType.IfEmpty("avion")]);
        Data.Insert(0, x);
        if (!Persist()) { Data.RemoveAt(0); return Res<JObj>.Fail("No se pudo guardar."); }
        Attention.Log("created", "Encargo creado · " + name);
        if (o.Fund > 0) Attention.Log("payment", "Pago registrado · " + name, o.Fund);
        return Res<JObj>.Success(x);
    }

    static void Restore(JObj x, JObj snap) { x.Clear(); x.PutAll(snap); }

    /// <summary>saveEdit() de la app: estado, libras añadidas y un pago.</summary>
    public Res<JObj> SaveEdit(object? id, double addLb, double pay, string method, string payNote, string status)
    {
        var x = ById(id); if (x == null) return Res<JObj>.Fail("No existe el encargo");
        if (!double.IsFinite(addLb) || !double.IsFinite(pay) || addLb < 0 || pay < 0) return Res<JObj>.Fail("Los valores no pueden ser negativos.");
        var snapshot = (JObj)Json.DeepCopy(x)!;
        var prevSt = Js.Str(x["status"]); var prevPaid = Js.Num(x["paid"]);
        var now = Js.NowIso(); var old = Js.Num(x["pounds"]); var newP = old + addLb;
        if (addLb != 0.0) { x.Arr("lbHistory").Add(JObj.Of(("date", now), ("from", old), ("to", newP), ("change", addLb))); x["pounds"] = newP; }
        if (pay != 0.0) { x.Arr("paymentHistory").Add(JObj.Of(("id", (double)Js.NowMs()), ("date", now), ("amount", pay), ("method", method), ("note", payNote.Trim()))); x["paid"] = Js.Num(x["paid"]) + pay; }
        x["status"] = status;
        x["shipping"] = Js.Num(x["lbCost"]) * Js.Num(x["pounds"]); x["total"] = Js.Num(x["cost"]) + Js.Num(x["shipping"]); x["balance"] = Js.Num(x["total"]) - Js.Num(x["paid"]); x["updated"] = now;
        if (status != prevSt) Stamp(x, status);
        if (!Persist()) { Restore(x, snapshot); return Res<JObj>.Fail("No se pudo guardar."); }
        var nm = x.Str("name");
        if (status != prevSt) Attention.Log(status == "Entregado" ? "delivered" : "status", status == "Entregado" ? "Pedido entregado · " + nm : $"Estado actualizado · {nm}: {status}");
        var d = Js.Num(x["paid"]) - prevPaid; if (d > 0.005) Attention.Log("payment", "Pago registrado · " + nm, d);
        Refresh(); return Res<JObj>.Success(x);
    }

    public string? NextStatus(string s) { var i = Array.IndexOf(FLOW, s); return i >= 0 && i < FLOW.Length - 1 ? FLOW[i + 1] : null; }
    /// <summary>advance(): pasa al siguiente estado. Devuelve el estado anterior (para deshacer).</summary>
    public string? Advance(object? id)
    {
        var x = ById(id); if (x == null) return null;
        var prev = x.Str("status").IfEmpty("Pedido"); var nx = NextStatus(prev); if (nx == null) return null;
        x["status"] = nx; x["updated"] = Js.NowIso(); Stamp(x, nx);
        if (!Persist()) { x["status"] = prev; return null; }
        Attention.Log(nx == "Entregado" ? "delivered" : "status", nx == "Entregado" ? "Pedido entregado · " + x.Str("name") : $"Estado actualizado · {x.Str("name")}: {nx}");
        return prev;
    }
    public void SetStatus(object? id, string st) { var x = ById(id); if (x == null) return; x["status"] = st; Persist(); }
    public bool Deliver(object? id)
    {
        var x = ById(id); if (x == null) return false; var prev = x["status"];
        x["status"] = "Entregado"; Stamp(x, "Entregado"); x["updated"] = Js.NowIso();
        if (!Persist()) { x["status"] = prev; return false; }
        Attention.Log("delivered", "Pedido entregado · " + x.Str("name")); return true;
    }
    public bool SetDate(object? id, string iso) { var x = ById(id); if (x == null || double.IsNaN(Js.ParseDate(iso))) return false; x["date"] = iso; x["updated"] = Js.NowIso(); return Persist(); }
    public bool SetNote(object? id, string note) { var x = ById(id); if (x == null) return false; x["note"] = note.Trim(); x["updated"] = Js.NowIso(); return Persist(); }
    public bool AddOrderPhoto(object? id, string dataUrl) { var x = ById(id); if (x == null) return false; x.Arr("photos").Add(dataUrl); x["updated"] = Js.NowIso(); return Persist(); }
    public bool DeleteOrderPhoto(object? id, int idx) { var x = ById(id); if (x?["photos"] is not JArr p || idx < 0 || idx >= p.Count) return false; p.RemoveAt(idx); return Persist(); }
    public List<string> OrderPhotos(JObj x)
    {
        var l = new List<string>(); var ph = x.Str("photo"); if (ph.Length > 0) l.Add(ph);
        if (x["photos"] is JArr a) foreach (var it in a) if (it is string s && s.Length > 0) l.Add(s);
        return l;
    }

    public sealed record Removed(JObj Item, int Index);
    public Removed? RemoveOrder(object? id)
    {
        var idx = Data.FindIndex(it => it is JObj o && Js.Str(o["id"]) == Js.Str(id)); if (idx < 0) return null;
        var item = (JObj)Data[idx]!; Data.RemoveAt(idx);
        if (!Persist()) { Data.Insert(idx, item); return null; }
        return new Removed(item, idx);
    }
    public bool UndoRemove(Removed r)
    {
        if (ById(r.Item["id"]) != null) return false;
        Data.Insert(Math.Clamp(r.Index, 0, Data.Count), r.Item); return Persist();
    }

    /* ---------------- Pagos ---------------- */
    public JArr Payments(JObj x) { if (x["paymentHistory"] is JArr a) return a; var n = new JArr(); x["paymentHistory"] = n; return n; }
    static double SumAmounts(JArr a) => a.Sum(it => Js.Num((it as JObj)?["amount"]));
    /// <summary>paySave(): edita un pago y mantiene lo pagado coherente.</summary>
    public Res<bool> PayEdit(object? orderId, int idx, double amount, string method, string note)
    {
        var x = ById(orderId); if (x == null) return Res.Fail("No existe");
        var ph = Payments(x); if (idx < 0 || idx >= ph.Count || ph[idx] is not JObj p) return Res.Fail("No existe el pago");
        if (!(amount > 0)) return Res.Fail("La cantidad debe ser mayor que 0. Para quitarlo usa \"Eliminar pago\".");
        var snap = (JObj)Json.DeepCopy(x)!;
        var before = Js.Num(x["paid"]) - SumAmounts(ph);
        p["amount"] = amount; p["method"] = method.IfEmpty(p.Str("method").IfEmpty("Efectivo")); p["note"] = note.Trim(); p["edited"] = Js.NowIso();
        x["paid"] = SumAmounts(ph); x["balance"] = Js.Num(x["total"]) - Js.Num(x["paid"]); x["updated"] = Js.NowIso();
        x["paid"] = Js.Num(x["paid"]) + before; x["balance"] = Js.Num(x["total"]) - Js.Num(x["paid"]);
        if (!Persist()) { Restore(x, snap); return Res.Fail("No se pudo guardar."); }
        Refresh(); return Res.Ok();
    }
    public JObj? PayDelete(object? orderId, int idx)
    {
        var x = ById(orderId); if (x == null) return null;
        var ph = Payments(x); if (idx < 0 || idx >= ph.Count || ph[idx] is not JObj p) return null;
        var snap = (JObj)Json.DeepCopy(x)!;
        ph.RemoveAt(idx); x["paid"] = Js.Num(x["paid"]) - Js.Num(p["amount"]); x["balance"] = Js.Num(x["total"]) - Js.Num(x["paid"]); x["updated"] = Js.NowIso();
        if (!Persist()) { Restore(x, snap); return null; }
        Refresh(); return snap;
    }
    public void RestoreOrder(JObj snapshot) { var x = ById(snapshot["id"]); if (x == null) return; Restore(x, (JObj)Json.DeepCopy(snapshot)!); Persist(); Refresh(); }

    /* ---------------- Clientes ---------------- */
    public sealed class Client
    {
        public string Name { get; } public string Phone { get; } public List<JObj> OrderList { get; } = new();
        public double Paid, Pending, Funds, Pounds;
        public Client(string name, string phone) { Name = name; Phone = phone; }
        public string Key => Name.Trim().ToLowerInvariant();
    }
    /* Vista calculada una sola vez por versión de los datos (igual que en Android): se recalcula solo cuando cambia
     * Version, es decir, tras guardar un cambio. Se devuelven copias de las listas para que nadie altere la vista compartida. */
    long _cacheVer = -1; readonly object _cacheLock = new();
    List<Client> _cClients = new(), _cActive = new(), _cDone = new(); List<JObj> _cActiveOrders = new(); Dictionary<string, Client> _cByKey = new();
    void View()
    {
        lock (_cacheLock)
        {
            if (_cacheVer == Version) return;
            var all = ComputeClients(); var done = all.Where(IsCompletedClient).ToList(); var doneKeys = done.Select(c => c.Key).ToHashSet();
            _cClients = all; _cDone = done; _cActive = all.Where(c => !IsCompletedClient(c)).ToList();
            _cActiveOrders = Orders().Where(o => !doneKeys.Contains(o.Str("name").Trim().ToLowerInvariant())).ToList();
            _cByKey = new Dictionary<string, Client>(); foreach (var c in all) _cByKey[c.Name.ToLowerInvariant()] = c;
            _cacheVer = Version;
        }
    }
    public List<Client> Clients() { View(); return new List<Client>(_cClients); }
    List<Client> ComputeClients()
    {
        var map = new Dictionary<string, Client>(); var order = new List<string>();
        foreach (var x in Orders())
        {
            var k = x.Str("name").Trim().ToLowerInvariant();
            if (!map.TryGetValue(k, out var c)) { c = new Client(x.Str("name"), x.Str("phone")); map[k] = c; order.Add(k); }
            c.OrderList.Add(x); c.Paid += Js.Num(x["paid"]); c.Pounds += Js.Num(x["pounds"]);
            var b = Js.Num(x["balance"]); if (b > 0) c.Pending += b; if (b < 0) c.Funds += -b;
        }
        return order.Select(k => map[k]).OrderBy(c => c.Name, Js.Collator).ToList();
    }
    static bool Settled(JObj o) => Js.Eq(o["status"], "Cancelado") || (Js.Num(o["total"]) > 0 && Math.Abs(Js.Num(o["balance"])) < 0.005);
    public bool IsCompletedClient(Client? c) => c != null && c.OrderList.Count > 0 && c.OrderList.All(Settled);
    public List<Client> ActiveClients() { View(); return new List<Client>(_cActive); }
    public List<Client> CompletedClients() { View(); return new List<Client>(_cDone); }
    public List<JObj> ActiveOrders() { View(); return new List<JObj>(_cActiveOrders); }
    public Client? FindClient(string name) { View(); return _cByKey.TryGetValue(name.ToLowerInvariant(), out var c) ? c : _cClients.FirstOrDefault(x => x.Name.ToLowerInvariant() == name.ToLowerInvariant()); }

    public JObj ClientInfo(string name) => (Ls.JsonOf("encargos_client_info") as JObj)?[name.Trim().ToLowerInvariant()] as JObj ?? new JObj();
    public bool SaveClientInfo(string name, string address, string note)
    {
        var all = Ls.JsonOf("encargos_client_info") as JObj ?? new JObj();
        all[name.Trim().ToLowerInvariant()] = JObj.Of(("address", address.Trim()), ("note", note.Trim()));
        var ok = PutBusiness("encargos_client_info", all);
        if (ok) Notifs.Add(JObj.Of(("cat", "client"), ("type", "client_edit"), ("title", "Cliente actualizado"), ("text", $"Se actualizaron los datos de {name}."), ("short", name), ("ref", JObj.Of(("client", name)))));
        return ok;
    }

    /* ---------------- WhatsApp ---------------- */
    public string WaNumber(string? phone) { var p = Regex.Replace(phone ?? "", @"\D", ""); if (p.Length > 0 && p.Length <= 8) p = PhoneCC + p; return p; }
    public string? WaLink(string? phone, string? msg)
    {
        var p = WaNumber(phone); if (p.Length == 0) return null;
        return "https://wa.me/" + p + (!string.IsNullOrEmpty(msg) ? "?text=" + Uri.EscapeDataString(msg) : "");
    }
    public static string Fill(string t, IDictionary<string, string> v) => Regex.Replace(t, @"\{(nombre|saldo|total|pagado|libras|negocio)\}", m => v.TryGetValue(m.Groups[1].Value, out var s) ? s : m.Value);
    public Dictionary<string, string> CtxClient(Client c) => new()
    {
        ["nombre"] = c.Name, ["saldo"] = Js.Money(c.Pending), ["total"] = Js.Money(c.OrderList.Sum(o => Js.Num(o["total"]))), ["pagado"] = Js.Money(c.Paid),
        ["libras"] = Js.ToFixed(c.Pounds, 2) + " lb", ["negocio"] = BusinessName,
    };
    public Dictionary<string, string> CtxOrder(JObj x) => new()
    {
        ["nombre"] = x.Str("name"), ["saldo"] = Js.Money(Math.Max(0.0, Js.Num(x["balance"]))), ["total"] = Js.Money(x["total"]), ["pagado"] = Js.Money(x["paid"]),
        ["libras"] = Js.ToFixed(Js.Num(x["pounds"]), 2) + " lb", ["negocio"] = BusinessName,
    };
    public string ArrivalMessage(Client c) => Fill(Tpl("arrival"), CtxClient(c));
    public string ReminderClient(Client c) => Fill(Tpl("reminder"), CtxClient(c));
    public string ReminderOrder(JObj x) => Fill(Tpl("reminder"), CtxOrder(x));
    public string ThanksOrder(JObj x) => Fill(Tpl("thanks"), CtxOrder(x));
    public string ThanksClient(Client c) => Fill(Tpl("thanks"), CtxClient(c));
    public string ReceiptCaption(JObj x)
    {
        var b = Js.Num(x["balance"]);
        return $"Hola {x.Str("name")}, aquí tienes el recibo de tu compra.\nTotal: {Js.Money(x["total"])} · Pagado: {Js.Money(x["paid"])}" + (b > 0.005 ? $" · Pendiente: {Js.Money(b)}" : " · ✅ Pagado completo");
    }
    public void Notified(string who) => Attention.Log("notified", "Cliente notificado · " + who);

    /* ---------------- Notas y gastos ---------------- */
    public List<JObj> Notes() => Ls.Arr("encargos_notes").Objs();
    public List<JObj> NotesSorted() => Notes().OrderByDescending(n => Js.Truthy(n["pinned"]) ? 1 : 0).ThenByDescending(n => Js.Num(n["updated"])).ToList();
    public Res<JObj> SaveNote(string? id, string title, string body, double amount, string color, bool pinned, IList<string> photos)
    {
        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(body) && photos.Count == 0) return Res<JObj>.Fail("Escribe un título, el contenido o agrega una foto.");
        var all = Ls.Arr("encargos_notes"); double now = Js.NowMs();
        JObj n;
        if (!string.IsNullOrEmpty(id))
        {
            n = all.FirstOrDefault(it => it is JObj o && Js.Eq(o["id"], id)) as JObj; if (n == null) return Res<JObj>.Fail("No existe la nota");
            n["title"] = title.Trim(); n["body"] = body.Trim(); n["color"] = color; n["pinned"] = pinned; n["updated"] = now; n["photos"] = new JArr(photos);
            if (amount > 0) n["amount"] = amount; else n.Remove("amount");
        }
        else
        {
            n = JObj.Of(("id", "n" + Js.Base36((long)now) + Js.Base36((long)(System.Random.Shared.NextDouble() * 1.6e6)).Take(4)), ("title", title.Trim()), ("body", body.Trim()), ("color", color),
                ("pinned", pinned), ("created", now), ("updated", now), ("photos", new JArr(photos)));
            if (amount > 0) n["amount"] = Math.Max(0.0, amount);
            all.Add(n);
        }
        return PutBusiness("encargos_notes", all) ? Res<JObj>.Success(n) : Res<JObj>.Fail("No se pudo guardar la nota.");
    }
    public bool DeleteNote(string id) => PutBusiness("encargos_notes", new JArr(Ls.Arr("encargos_notes").Where(it => !(it is JObj o && Js.Eq(o["id"], id)))));

    /* ---------------- Artículos en venta (se guardan como notas con kind = "sale") ---------------- */
    public sealed record SaleSummary(int Items, int Available, int Units, double Value, double Invested, double Potential,
                                     int SoldUnits, double Income, double Realized, double Lbs = 0, double TotalInvested = 0);
    public static bool IsSale(JObj n) => n.Str("kind") == "sale";
    public List<JObj> SaleItems() => Notes().Where(IsSale).OrderByDescending(n => Js.Num(n["updated"])).ToList();
    public static List<JObj> SaleSales(JObj n) => n["sales"] is JArr a ? a.Objs() : new List<JObj>();
    /// <summary>Envío de una unidad: libras × costo por libra.</summary>
    public static double SaleShipCost(JObj n) => Js.Num(n["weight"]) * Js.Num(n["lbRate"]);
    /// <summary>Lo que te cuesta una unidad puesta en tus manos: compra + envío por peso + otros gastos.</summary>
    public static double SaleUnitCost(JObj n) => Js.Num(n["cost"]) + SaleShipCost(n) + Js.Num(n["extra"]);
    /// <summary>Cálculos de todos los artículos: lo que vale lo que tienes, la inversión, la ganancia neta posible y la ya obtenida.</summary>
    public SaleSummary SaleSummaryOf(List<JObj>? list = null)
    {
        list ??= SaleItems();
        int units = 0, soldU = 0; double value = 0, invested = 0, income = 0, realized = 0, lbs = 0, soldCost = 0;
        foreach (var n in list)
        {
            int st = Math.Max(0, (int)Js.Num(n["stock"])); double pr = Js.Num(n["price"]), uc = SaleUnitCost(n);
            units += st; value += st * pr; invested += st * uc; lbs += st * Js.Num(n["weight"]);
            foreach (var v in SaleSales(n))
            {
                int q = (int)Js.Num(v["qty"]); soldU += q; income += q * Js.Num(v["price"]); soldCost += q * Js.Num(v["cost"]);
                realized += q * (Js.Num(v["price"]) - Js.Num(v["cost"]));
            }
        }
        return new SaleSummary(list.Count, list.Count(it => Js.Num(it["stock"]) > 0), units, value, invested, value - invested, soldU, income, realized, lbs, invested + soldCost);
    }
    private static string SaleBody(JObj n)
    {
        int st = (int)Js.Num(n["stock"]); double w = Js.Num(n["weight"]);
        return $"Artículo en venta · {Js.Money(n["price"])} · {(st == 1 ? "1 disponible" : $"{st} disponibles")}" +
            (w > 0 ? $" · {Js.ToFixed(w, 2)} lb c/u" : "") + $" · costo {Js.Money(SaleUnitCost(n))} c/u" +
            (string.IsNullOrWhiteSpace(n.Str("desc")) ? "" : "\n" + n.Str("desc"));
    }
    /// <summary>Guarda un artículo. cost = precio de compra por unidad; weight = libras por unidad; lbRate = costo del envío por libra;
    /// extra = otros gastos por unidad (aduana, empaque…).</summary>
    public Res<JObj> SaveSaleItem(string? id, string name, string desc, double price, double cost, int stock, IList<string> photos,
                                  double weight = 0, string shipType = "avion", double lbRate = 0, double extra = 0)
    {
        if (string.IsNullOrWhiteSpace(name)) return Res<JObj>.Fail("Escribe el nombre del artículo.");
        if (double.IsNaN(price) || price <= 0) return Res<JObj>.Fail("Escribe el precio de venta.");
        if (double.IsNaN(cost) || cost < 0) return Res<JObj>.Fail("Revisa el precio de compra.");
        if (double.IsNaN(weight) || weight < 0 || double.IsNaN(lbRate) || lbRate < 0) return Res<JObj>.Fail("Revisa el peso y el costo por libra.");
        if (double.IsNaN(extra) || extra < 0) return Res<JObj>.Fail("Revisa los otros gastos.");
        int st = Math.Max(0, stock); var all = Ls.Arr("encargos_notes"); double now = Js.NowMs();
        JObj n;
        if (!string.IsNullOrEmpty(id))
        {
            n = all.FirstOrDefault(it => it is JObj o && Js.Eq(o["id"], id)) as JObj; if (n == null) return Res<JObj>.Fail("No existe el artículo");
        }
        else
        {
            n = JObj.Of(("id", "s" + Js.Base36((long)now) + Js.Base36((long)(System.Random.Shared.NextDouble() * 1.6e6)).Take(4)), ("kind", "sale"), ("created", now),
                ("sales", new JArr()), ("color", "green"), ("pinned", false));
            all.Add(n);
        }
        n["title"] = name.Trim(); n["desc"] = desc.Trim(); n["price"] = Js.ToFixed2(price); n["cost"] = Js.ToFixed2(cost); n["stock"] = (double)st;
        n["weight"] = Js.ToFixed2(weight); n["shipType"] = string.IsNullOrEmpty(shipType) ? "avion" : shipType; n["lbRate"] = Js.ToFixed2(lbRate); n["extra"] = Js.ToFixed2(extra);
        n["photos"] = new JArr(photos); n["updated"] = now; n["body"] = SaleBody(n); n.Remove("amount");
        return PutBusiness("encargos_notes", all) ? Res<JObj>.Success(n) : Res<JObj>.Fail("No se pudo guardar el artículo (memoria llena).");
    }
    /// <summary>Registra una venta: baja las unidades y guarda el precio y el costo completo de ese momento (para la ganancia neta real).</summary>
    public Res<JObj> SellSaleItem(string id, int qty, double price)
    {
        var all = Ls.Arr("encargos_notes");
        if (all.FirstOrDefault(it => it is JObj o && Js.Eq(o["id"], id)) is not JObj n) return Res<JObj>.Fail("No existe el artículo");
        int st = (int)Js.Num(n["stock"]);
        if (qty <= 0) return Res<JObj>.Fail("La cantidad debe ser 1 o más.");
        if (qty > st) return Res<JObj>.Fail(st <= 0 ? "No quedan unidades de este artículo." : $"Solo quedan {st}.");
        if (double.IsNaN(price) || price < 0) return Res<JObj>.Fail("Revisa el precio de venta.");
        double now = Js.NowMs();
        if (n["sales"] is not JArr sales) { sales = new JArr(); n["sales"] = sales; }
        sales.Add(JObj.Of(("t", now), ("qty", (double)qty), ("price", Js.ToFixed2(price)), ("cost", Js.ToFixed2(SaleUnitCost(n)))));
        n["stock"] = (double)(st - qty); n["updated"] = now; n["body"] = SaleBody(n);
        return PutBusiness("encargos_notes", all) ? Res<JObj>.Success(n) : Res<JObj>.Fail("No se pudo guardar la venta.");
    }
    /// <summary>Deshace la última venta registrada del artículo (devuelve las unidades).</summary>
    public bool UndoLastSale(string id)
    {
        var all = Ls.Arr("encargos_notes");
        if (all.FirstOrDefault(it => it is JObj o && Js.Eq(o["id"], id)) is not JObj n) return false;
        if (n["sales"] is not JArr sales || sales.Count == 0) return false;
        if (sales[^1] is not JObj last) return false;
        sales.RemoveAt(sales.Count - 1);
        n["stock"] = (double)((int)Js.Num(n["stock"]) + (int)Js.Num(last["qty"])); n["updated"] = (double)Js.NowMs(); n["body"] = SaleBody(n);
        return PutBusiness("encargos_notes", all);
    }

    public List<JObj> Expenses() => Ls.Arr("encargos_expenses").Objs();
    public Res<bool> SaveExpense(string? id, string concept, double amount, string dateYmd, string cat)
    {
        if (string.IsNullOrWhiteSpace(concept)) return Res.Fail("Escribe el concepto del gasto.");
        if (!(amount > 0)) return Res.Fail("Escribe un monto mayor que 0.");
        var all = Ls.Arr("encargos_expenses");
        // new Date(d+'T12:00:00') es hora local del equipo
        string iso;
        if (DateOnly.TryParseExact(dateYmd, "yyyy-MM-dd", out var day))
        {
            var local = day.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Unspecified);
            iso = Js.IsoOf(new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, TimeZoneInfo.Local)).ToUnixTimeMilliseconds());
        }
        else iso = Js.NowIso();
        if (!string.IsNullOrEmpty(id))
        {
            if (all.FirstOrDefault(it => it is JObj o && Js.Eq(o["id"], id)) is JObj e) { e["concept"] = concept.Trim(); e["amount"] = amount; e["date"] = iso; e["cat"] = cat; }
        }
        else all.Add(JObj.Of(("id", "g" + Js.Base36(Js.NowMs())), ("concept", concept.Trim()), ("amount", amount), ("date", iso), ("cat", cat)));
        return PutBusiness("encargos_expenses", all) ? Res.Ok() : Res.Fail("No hay espacio suficiente para guardar.");
    }
    public bool DeleteExpense(string id) => PutBusiness("encargos_expenses", new JArr(Ls.Arr("encargos_expenses").Where(it => !(it is JObj o && Js.Eq(o["id"], id)))));
}
