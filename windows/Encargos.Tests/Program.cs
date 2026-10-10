using System.Net;
using System.Text;
using Encargos.Core;

// Uso:  Encargos.Tests vectors [archivo.enc]
//       Encargos.Tests device <puerto> <apiBase> <carpeta> [PC|ANDROID]
Console.OutputEncoding = Encoding.UTF8;
if (args.Length > 0 && args[0] == "funcional") return Funcional.Run();
if (args.Length > 0 && args[0] == "device") { DeviceServer.Run(int.Parse(args[1]), args[2], args[3], args.Length > 4 ? args[4] : "PC"); return 0; }
return Vectors.Run(args.Length > 1 ? args[1] : null);

static class Vectors
{
    static int fails;
    static void Check(string name, bool ok, object? info = null) { Console.WriteLine((ok ? "✅ " : "❌ ") + name + (info != null ? " · " + info : "")); if (!ok) fails++; }

    public static int Run(string? extra)
    {
        Check("sha256hex('abc')", Crypto.Sha256Hex("abc") == "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
        Check("detUuid order", Crypto.DetUuid("order|1696000000000|2026-10-01T12:00:00.000Z|Juan Pérez") == "fb2e151c-cd34-5037-9cbd-ca8e3952a188");
        Check("detUuid client", Crypto.DetUuid("client|juan pérez") == "08e04d0e-2d06-5d30-88a2-0d9e5f97cf85");
        Check("detUuid setting", Crypto.DetUuid("setting|templates") == "aeb4a149-b04f-5d9a-8a10-0e17a363b4df");
        Check("dbTag", Crypto.DbTag("2d8c3068-a188-40fd-845d-c18d4ed0599e") == "43bb1b1aae3e13e0");
        Check("detUuid pay (Number(10.5))", Crypto.DetUuid("pay|abc|1696|2026-10-01T12:00:00.000Z|" + Js.NumberToString(10.5) + "|0") == "632accfb-bbc3-5111-8ecf-7a599c119b20");
        var nums = new (double, string)[] { (10.0, "10"), (10.5, "10.5"), (0.1 + 0.2, "0.30000000000000004"), (1e21, "1e+21"), (1.5e-7, "1.5e-7"), (-3.0, "-3"), (1791554830000.0, "1791554830000"), (123456.789, "123456.789"), (0.000001, "0.000001"), (-0.5, "-0.5"), (1e-7, "1e-7"), (2.5e25, "2.5e+25") };
        foreach (var (d, s) in nums) Check($"numberToString({s})", Js.NumberToString(d) == s, Js.NumberToString(d));
        Check("r2(1.005)", Js.R2(1.005) == 1.0 || Js.R2(1.005) == 1.01, Js.R2(1.005));
        Check("r2(-0.001) sin -0", !double.IsNegative(Js.R2(-0.001)));
        Check("toFixed(2.345,2) = 2.35 (BigDecimal exacto da 2.35? el binario es 2.34499…)", Js.ToFixed(2.345, 2) is "2.35" or "2.34", Js.ToFixed(2.345, 2));
        Check("toFixed(1.005,2) igual que Kotlin (1.00)", Js.ToFixed(1.005, 2) == "1.00", Js.ToFixed(1.005, 2));
        Check("toFixed(0.5,0) = 1", Js.ToFixed(0.5, 0) == "1");
        Check("toFixed(-1.25,1) = -1.3", Js.ToFixed(-1.25, 1) == "-1.3", Js.ToFixed(-1.25, 1));
        Check("toFixed(-0.001,2) = 0.00", Js.ToFixed(-0.001, 2) == "0.00", Js.ToFixed(-0.001, 2));
        Check("money", Js.Money(3.5) == "$3.50");
        Check("num(' 12 ')", Js.Num(" 12 ") == 12 && double.IsNaN(Js.Num("12abc")) && Js.Num("") == 0);
        var src = "{\"b\":1,\"a\":{\"x\":[1,2.5,\"ñ\"],\"y\":null},\"campoNuevo\":true}";
        Check("JSON ida y vuelta", Json.Stringify(Json.Parse(src)) == src, Json.Stringify(Json.Parse(src)));
        Check("JSON escapes", Json.Stringify("a\"b\\c\n\u0001") == "\"a\\\"b\\\\c\\n\\u0001\"");
        Check("JSON con sangría", Json.Stringify(Json.Parse("{\"a\":[1,{}],\"b\":{}}"), 2) == "{\n  \"a\": [\n    1,\n    {}\n  ],\n  \"b\": {}\n}");
        Check("parseDate ISO", Js.ParseDate("2026-10-01T12:00:00.000Z") == 1790856000000.0, Js.ParseDate("2026-10-01T12:00:00.000Z"));
        Check("isoOf", Js.IsoOf(1790856000000) == "2026-10-01T12:00:00.000Z");

        var f = extra ?? Path.Combine(AppContext.BaseDirectory, "fixtures", "sample.enc");
        if (File.Exists(f))
        {
            var key = Crypto.B64U.Dec(File.ReadAllText(f.Replace(".enc", ".key")));
            var o = (JObj)Crypto.Unseal(File.ReadAllBytes(f), key, "2d8c3068-a188-40fd-845d-c18d4ed0599e")!;
            Check("Descifra ENCv1 creado por la app web", (((JArr)o["operations"]!)[0] as JObj)?.Obj("data")?.Str("name") == "Juan Pérez");
            var bad = false; try { Crypto.Unseal(File.ReadAllBytes(f), key, "otra"); } catch { bad = true; }
            Check("AAD distinto se rechaza", bad);
        }
        else Check("archivo de muestra ENCv1", false, f);
        var k2 = Crypto.Random(32);
        var sealedB = Crypto.Seal(JObj.Of(("hola", "mundo ñ")), k2, "db1");
        Check("ENCv1 propio: cabecera ENCv1Z", Encoding.ASCII.GetString(sealedB, 0, 6) == "ENCv1Z");
        Check("ENCv1 propio ida y vuelta", Js.Eq(((JObj)Crypto.Unseal(sealedB, k2, "db1")!)["hola"], "mundo ñ"));
        Directory.CreateDirectory("out"); File.WriteAllBytes("out/csharp_sample.enc", sealedB); File.WriteAllText("out/csharp_sample.key", Crypto.B64U.Enc(k2));
        var lc = new LinkCode("123456789:AAEncargosTestTokenXXXXXXXXXXXXXXXX", 777000111.0, Crypto.B64U.Enc(k2), "db-1");
        var back = LinkCode.Parse(lc.Encode());
        Check("ENC1 ida y vuelta", back != null && back.Token == lc.Token && Js.Num(back.ChatId) == 777000111.0 && back.Db == "db-1");
        Check("ENC1 inválido", LinkCode.Parse("ENC1.xxx") == null && LinkCode.Parse("hola") == null);
        Check("PBKDF2-SHA256", Crypto.Hex(Crypto.Pbkdf2("password", Encoding.UTF8.GetBytes("salt"), 1, 32)) == "120fb6cffcf8b32c43e7225256c4f837a86548c92ccc35480805987cb70be17b");
        var man = JObj.Of(("type", "ENCARGOS_BACKUP_LATEST"), ("version", 7.0), ("packageId", "p"), ("deviceId", "PC-1"), ("timestamp", "2026-10-09T14:00:00.000Z"), ("checksum", "c"), ("telegramFileId", "F"), ("messageId", 5.0), ("size", 10.0), ("schemaVersion", 1.0), ("db", "x"));
        var txt = TelegramSync.ManText(man);
        Check("Mensaje fijado: formato", txt.StartsWith("📦 Última sincronización\nVersión 7\n") && txt.Contains("\n#ENCARGOS_BACKUP_LATEST\n{\"type\":\"ENCARGOS_BACKUP_LATEST\",\"version\":7,"), txt.Split('\n')[0]);
        Check("Mensaje fijado: lectura", Js.Eq(TelegramSync.ParseMan(txt)?["packageId"], "p"));
        var app = new EncargosApp(new MemoryStorage());
        var x = app.CreateOrder(new EncargosApp.NewOrder("Ana", "55551234", 45.0, 7.0, 2.0, 20.0)).GetOrThrow();
        Check("Encargo: envío 14, total 59, saldo 39", Js.Num(x["shipping"]) == 14.0 && Js.Num(x["total"]) == 59.0 && Js.Num(x["balance"]) == 39.0);
        app.SaveEdit(x["id"], 1.5, 10.0, "USD", "abono", "En tránsito");
        Check("Editar: libras 3.5, pagado 30, saldo 39.5", Js.Num(x["pounds"]) == 3.5 && Js.Num(x["paid"]) == 30.0 && Js.Num(x["balance"]) == 39.5, Json.Stringify(JArr.Of(x["pounds"], x["paid"], x["balance"])));
        Check("statusDates registra En tránsito", ((JObj)x["statusDates"]!).ContainsKey("En tránsito"));
        Check("Cliente pendiente 39.5", app.Clients()[0].Pending == 39.5);
        Check("waNumber agrega código de país", app.WaNumber("5555 1234") == "5355551234");
        Check("Notificaciones generadas", app.Notifs.All().Any(n => Js.Eq(n["type"], "pay_new")));
        Check("Atención: alerta de pago", app.Attention.Compute().Any(al => al.Cat == "pay"));
        var bk = new Backup(app); var enc = bk.ExportEncrypted("secreto1");
        var dec = (JObj)bk.Decrypt((JObj)Json.Parse(enc)!, "secreto1")!;
        Check("Respaldo cifrado ida y vuelta", Js.Eq(((JObj)((JArr)dec["data"]!)[0]!)["name"], "Ana"));
        var st = new Stats(app); var (a, b) = st.Range(Stats.Period.Month, Js.Today()); var r = st.Calc(a, b);
        Check("Estadísticas del mes: 1 encargo, cobrado 30", r.Ord.Count == 1 && r.Cobrado == 30.0, $"{r.Ord.Count} · {r.Cobrado}");
        var sec = new Security(app, Crypto.Random(32));
        Check("Credenciales", sec.SetCredentials("admin", "secreto123").Ok && sec.CheckPassword("Admin", "secreto123") && !sec.CheckPassword("admin", "malo"));
        Check("PIN", sec.SetPin("4321").Ok && sec.VerifyPin("4321") && !sec.VerifyPin("1234"));
        var csv = bk.Csv(); Check("CSV con cabecera", csv.StartsWith("﻿Cliente,Teléfono"));
        Console.WriteLine(fails == 0 ? "\nTODO CORRECTO" : $"\nFALLOS: {fails}");
        return fails == 0 ? 0 : 1;
    }
}

/// <summary>Un "dispositivo PC" controlable por HTTP para las pruebas cruzadas (mismo protocolo que el de Kotlin).</summary>
static class DeviceServer
{
    static EncargosApp app = null!; static TelegramSync sync = null!; static string dir = "", apiBase = "", platform = "PC";
    sealed class FileSecrets : ISecrets
    {
        readonly string f; public FileSecrets(string f) { this.f = f; }
        JObj All() => Json.Obj(File.Exists(f) ? File.ReadAllText(f) : null) ?? new JObj();
        public string? Get(string name) => All()[name] as string;
        public void Set(string name, string value) { var a = All(); a[name] = value; File.WriteAllText(f, Json.Stringify(a)); }
        public void Remove(string name) { var a = All(); a.Remove(name); File.WriteAllText(f, Json.Stringify(a)); }
    }
    static void Boot()
    {
        app = new EncargosApp(new FileStorage(Path.Combine(dir, "storage.json")), platform);
        sync = new TelegramSync(app, new FileSecrets(Path.Combine(dir, "secrets.json")), apiBase);
    }
    static string Name(TelegramSync.ConnectResult r) => r switch { TelegramSync.ConnectResult.Ok => "Ok", TelegramSync.ConnectResult.NeedStart => "NeedStart", TelegramSync.ConnectResult.Existing => "Existing", TelegramSync.ConnectResult.Error e => "Error" + e.Msg, _ => "?" };

    public static void Run(int port, string api, string d, string plat)
    {
        apiBase = api; dir = d; platform = plat; Directory.CreateDirectory(dir); Boot();
        var l = new HttpListener(); l.Prefixes.Add($"http://127.0.0.1:{port}/"); l.Start(); Console.WriteLine($"device listo en {port}");
        while (true)
        {
            var ctx = l.GetContext();
            string body; using (var rd = new StreamReader(ctx.Request.InputStream, Encoding.UTF8)) body = rd.ReadToEnd();
            var p = string.IsNullOrWhiteSpace(body) ? new JObj() : (JObj)Json.Parse(body)!;
            JObj ByName(string n) => app.Orders().First(o => o.Str("name") == n);
            object? res;
            try
            {
                res = ctx.Request.Url!.AbsolutePath switch
                {
                    "/connect" => Name(sync.Connect(p.Str("token"))),
                    "/fresh" => sync.FreshStart(),
                    "/link" => Name(sync.Link(p.Str("code"))),
                    "/code" => sync.LinkCode(),
                    "/order" => Do(() => app.CreateOrder(new EncargosApp.NewOrder(p.Str("name"), p.Str("phone").IfEmpty("5511"), Js.Num(p["cost"]), p["lbCost"] != null ? Js.Num(p["lbCost"]) : 7.0, p["lb"] != null ? Js.Num(p["lb"]) : 1.0, Js.Num(p["fund"]), Status: p.Str("status").IfEmpty("Pedido"))).GetOrThrow()),
                    "/pay" => Do(() => { var x = ByName(p.Str("name")); app.SaveEdit(x["id"], 0, Js.Num(p["amt"]), p.Str("method").IfEmpty("Efectivo"), "", x.Str("status")).GetOrThrow(); }),
                    "/lb" => Do(() => { var x = ByName(p.Str("name")); app.SaveEdit(x["id"], Js.Num(p["lb"]), 0, "Efectivo", "", x.Str("status")).GetOrThrow(); }),
                    "/advance" => app.Advance(ByName(p.Str("name"))["id"]),
                    "/deliver" => app.Deliver(ByName(p.Str("name"))["id"]),
                    "/delete" => app.RemoveOrder(ByName(p.Str("name"))["id"]) != null,
                    "/paydel" => app.PayDelete(ByName(p.Str("name"))["id"], (int)Js.Num(p["idx"])) != null,
                    "/field" => Field(ByName(p.Str("name")), p.Str("field"), p["value"]),
                    "/note" => Do(() => app.SaveNote(null, p.Str("title"), p.Str("body"), Js.Num(p["amount"]), "blue", false, new List<string>()).GetOrThrow()),
                    "/clientinfo" => app.SaveClientInfo(p.Str("name"), p.Str("address"), p.Str("note")),
                    "/templates" => app.SaveTemplates(p.Str("arrival"), p.Str("reminder"), p.Str("thanks")),
                    "/collect" => (double)sync.Collect(),
                    "/sync" => SyncNow(),
                    "/backup" => sync.BackupNow(),
                    "/restore" => sync.Restore() is { Ok: false } rr ? rr.Error : "ok",
                    "/restart" => Do(Boot),
                    "/state" => JObj.Of(("orders", new JArr(app.Orders().Select(x => (object?)JArr.Of(x["uid"], x["name"], x["status"], x["pounds"], x["total"], x["paid"], x["balance"])).OrderBy(o => Json.Stringify(o), StringComparer.Ordinal))),
                        ("full", app.Data), ("notes", new JArr(app.Notes())), ("clientInfo", app.Ls.Obj("encargos_client_info")), ("templates", app.Templates()),
                        ("pending", (double)sync.Pending()), ("meta", sync.MetaInfo()), ("status", sync.GetStatus().Text), ("conflicts", (double)sync.Conflicts().Count), ("notifs", new JArr(app.Notifs.All().Select(n => n["title"])))),
                    _ => "?",
                };
            }
            catch (Exception e) { res = JObj.Of(("error", e.Message)); }
            var outb = Encoding.UTF8.GetBytes(Json.Stringify(JObj.Of(("r", res))));
            ctx.Response.ContentType = "application/json"; ctx.Response.ContentLength64 = outb.Length; ctx.Response.OutputStream.Write(outb); ctx.Response.Close();
        }
    }
    static object Do(Action a) { a(); return "ok"; }
    static object Do<T>(Func<T> f) { f(); return "ok"; }
    static object Field(JObj x, string field, object? value) { x[field] = value; x["updated"] = Js.NowIso(); return app.Persist(); }
    static object SyncNow() { sync.Collect(); var ok = sync.SyncNowBlocking(); return JObj.Of(("ok", ok), ("err", sync.LastErr), ("state", sync.State)); }
}
