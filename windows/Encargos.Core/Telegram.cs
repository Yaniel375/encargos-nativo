using System.Net.Http.Headers;
using System.Text.RegularExpressions;

namespace Encargos.Core;

/// <summary>Error devuelto por Telegram (ok:false). Sin código = no hubo respuesta.</summary>
public class TgError : Exception
{
    public int? Code { get; }
    public int? RetryAfter { get; }
    public TgError(string message, int? code = null, int? retryAfter = null) : base(message) { Code = code; RetryAfter = retryAfter; }
}

/// <summary>Fallo de red (sin respuesta de Telegram).</summary>
public class NetError : IOException { public NetError(string m) : base(m) { } }

/// <summary>Acceso a la Bot API. Solo métodos reales; el token nunca se escribe en registros ni mensajes de error.</summary>
public interface ITgApi
{
    object? Call(string method, JObj p);
    object? SendFile(string method, IDictionary<string, string> fields, string fileField, string fileName, byte[] bytes, string mime);
    byte[] Download(string filePath);
}

public sealed class HttpTgApi : ITgApi
{
    readonly string _base; readonly Func<string> _token;
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };
    public HttpTgApi(string apiBase, Func<string> token) { _base = apiBase.TrimEnd('/'); _token = token; }

    /// <summary>Oculta el token en cualquier texto.</summary>
    string Clean(string? s)
    {
        var t = _token(); var r = s ?? "";
        if (t.Length > 0) r = r.Replace(t, "<oculto>");
        return Regex.Replace(r, @"bot[0-9]{5,}:[A-Za-z0-9_-]{20,}", "bot<oculto>");
    }

    object? Parse(HttpResponseMessage res)
    {
        var code = (int)res.StatusCode;
        string body; using (var st = res.Content.ReadAsStream()) using (var rd = new StreamReader(st)) body = rd.ReadToEnd();
        JObj? j; try { j = Json.Parse(body) as JObj; } catch { j = null; }
        if (j == null) throw new TgError("Respuesta no válida", code);
        if (!Js.Eq(j["ok"], true))
        {
            var p = j["parameters"] as JObj; var ec = (int)Js.Num(j["error_code"]);
            throw new TgError(Clean(j.Str("description").IfEmpty("HTTP " + code)), ec == 0 ? code : ec, p?["retry_after"] is { } ra ? (int)Js.Num(ra) : null);
        }
        return j["result"];
    }
    HttpResponseMessage Send(HttpRequestMessage req)
    {
        try { return Http.Send(req, HttpCompletionOption.ResponseContentRead); }
        catch (HttpRequestException e) { throw new NetError(Clean(e.Message)); }
        catch (TaskCanceledException) { throw new NetError("Tiempo de espera agotado"); }
    }
    public object? Call(string method, JObj p)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{_base}/bot{_token()}/{method}")
        { Content = new ByteArrayContent(Crypto.Utf8(Json.Stringify(p))) };
        req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        using var res = Send(req); return Parse(res);
    }
    public object? SendFile(string method, IDictionary<string, string> fields, string fileField, string fileName, byte[] bytes, string mime)
    {
        var b = "----encargos" + Crypto.Hex(Crypto.Random(12));
        using var ms = new MemoryStream();
        void W(string s) { var x = Crypto.Utf8(s); ms.Write(x, 0, x.Length); }
        foreach (var kv in fields) W($"--{b}\r\nContent-Disposition: form-data; name=\"{kv.Key}\"\r\n\r\n{kv.Value}\r\n");
        W($"--{b}\r\nContent-Disposition: form-data; name=\"{fileField}\"; filename=\"{fileName}\"\r\nContent-Type: {mime}\r\n\r\n"); ms.Write(bytes, 0, bytes.Length); W($"\r\n--{b}--\r\n");
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{_base}/bot{_token()}/{method}") { Content = new ByteArrayContent(ms.ToArray()) };
        req.Content.Headers.TryAddWithoutValidation("Content-Type", "multipart/form-data; boundary=" + b);
        using var res = Send(req); return Parse(res);
    }
    public byte[] Download(string filePath)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"{_base}/file/bot{_token()}/{filePath}");
        using var res = Send(req);
        if ((int)res.StatusCode != 200) throw new TgError("No se pudo descargar el paquete", (int)res.StatusCode);
        using var st = res.Content.ReadAsStream(); using var o = new MemoryStream(); st.CopyTo(o); return o.ToArray();
    }
}
