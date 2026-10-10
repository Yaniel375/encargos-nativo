using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Encargos.Core;

/// <summary>Primitivas criptográficas compatibles byte a byte con la app web y con el núcleo Kotlin.</summary>
public static class Crypto
{
    static readonly UTF8Encoding U8 = new(false);
    public static byte[] Utf8(string s) => U8.GetBytes(s);
    public static string FromUtf8(byte[] b) => U8.GetString(b);

    public static string Hex(byte[] b) => Convert.ToHexString(b).ToLowerInvariant();
    public static byte[] Sha256(byte[] b) => SHA256.HashData(b);
    /// <summary>sha256hex(str) de la app: SHA-256 del texto en UTF-8, en hexadecimal.</summary>
    public static string Sha256Hex(string s) => Hex(Sha256(Utf8(s)));
    public static string ShaHex(byte[] b) => Hex(Sha256(b));
    public static byte[] Random(int n) => RandomNumberGenerator.GetBytes(n);

    /// <summary>crypto.randomUUID()</summary>
    public static string Uuid()
    {
        var b = Random(16); b[6] = (byte)((b[6] & 15) | 64); b[8] = (byte)((b[8] & 63) | 128);
        var h = Hex(b); return $"{h[..8]}-{h[8..12]}-{h[12..16]}-{h[16..20]}-{h[20..]}";
    }

    /// <summary>detUuid(s) de la app (identificadores deterministas para datos antiguos).</summary>
    public static string DetUuid(string s)
    {
        var h = Sha256Hex("encargos-sync|" + s);
        var v = (8 + (Convert.ToInt32(h[16].ToString(), 16) & 3)).ToString("x");
        return $"{h[..8]}-{h[8..12]}-5{h[13..16]}-{v}{h[17..20]}-{h[20..32]}";
    }

    public static string DbTag(string db) => Sha256Hex("encargos-db|" + db)[..16];

    public static class B64U
    {
        public static string Enc(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        public static byte[] Dec(string s)
        {
            var t = s.Trim().TrimEnd('=').Replace('-', '+').Replace('_', '/');
            switch (t.Length % 4) { case 2: t += "=="; break; case 3: t += "="; break; case 1: throw new FormatException("base64url"); }
            return Convert.FromBase64String(t);
        }
    }
    public static string B64(byte[] b) => Convert.ToBase64String(b);
    public static byte[] UnB64(string s) => Convert.FromBase64String(s.Trim());

    public static byte[] Gzip(byte[] b) { using var ms = new MemoryStream(); using (var z = new GZipStream(ms, CompressionLevel.Optimal, true)) z.Write(b); return ms.ToArray(); }
    public static byte[] Gunzip(byte[] b) { using var i = new MemoryStream(b); using var z = new GZipStream(i, CompressionMode.Decompress); using var o = new MemoryStream(); z.CopyTo(o); return o.ToArray(); }

    /* ---------- ENCv1: "ENCv1" + Z|N + IV(12) + AES-256-GCM(ciphertext+tag), AAD = "ENCv1|" + db ---------- */
    public static byte[] Seal(object? obj, byte[] key, string db)
    {
        var z = Gzip(Utf8(Json.Stringify(obj)));
        var iv = Random(12);
        var ct = new byte[z.Length]; var tag = new byte[16];
        using (var g = new AesGcm(key, 16)) g.Encrypt(iv, z, ct, tag, Utf8("ENCv1|" + db));
        var head = Encoding.ASCII.GetBytes("ENCv1Z");
        var out_ = new byte[6 + 12 + ct.Length + 16];
        Buffer.BlockCopy(head, 0, out_, 0, 6); Buffer.BlockCopy(iv, 0, out_, 6, 12); Buffer.BlockCopy(ct, 0, out_, 18, ct.Length); Buffer.BlockCopy(tag, 0, out_, 18 + ct.Length, 16);
        return out_;
    }

    public static object? Unseal(byte[] bytes, byte[] key, string db)
    {
        if (bytes.Length < 18 + 16) throw new InvalidDataException("Formato desconocido");
        var h = Encoding.ASCII.GetString(bytes, 0, 6);
        if (!h.StartsWith("ENCv1", StringComparison.Ordinal)) throw new InvalidDataException("Formato desconocido");
        var iv = bytes.AsSpan(6, 12); var n = bytes.Length - 18 - 16;
        var ct = bytes.AsSpan(18, n); var tag = bytes.AsSpan(18 + n, 16); var pt = new byte[n];
        using (var g = new AesGcm(key, 16)) g.Decrypt(iv, ct, tag, pt, Utf8("ENCv1|" + db));
        var raw = h[5] == 'Z' ? Gunzip(pt) : pt;
        return Json.Parse(FromUtf8(raw));
    }

    /// <summary>PBKDF2-HMAC-SHA256 (igual que WebCrypto).</summary>
    public static byte[] Pbkdf2(string password, byte[] salt, int iterations, int len = 32) =>
        Rfc2898DeriveBytes.Pbkdf2(Utf8(password), salt, iterations, HashAlgorithmName.SHA256, len);

    public static byte[] AesGcmEncrypt(byte[] key, byte[] iv, byte[] pt)
    {
        var ct = new byte[pt.Length]; var tag = new byte[16];
        using (var g = new AesGcm(key, 16)) g.Encrypt(iv, pt, ct, tag);
        return ct.Concat(tag).ToArray();
    }
    public static byte[] AesGcmDecrypt(byte[] key, byte[] iv, byte[] ctTag)
    {
        if (ctTag.Length < 16) throw new CryptographicException("datos");
        var n = ctTag.Length - 16; var pt = new byte[n];
        using (var g = new AesGcm(key, 16)) g.Decrypt(iv, ctTag.AsSpan(0, n), ctTag.AsSpan(n, 16), pt);
        return pt;
    }

    /// <summary>Comparación en tiempo constante.</summary>
    public static bool CtEq(string a, string b) { if (a.Length != b.Length) return false; var r = 0; for (var i = 0; i < a.Length; i++) r |= a[i] ^ b[i]; return r == 0; }
}

/// <summary>Código de enlace ENC1 (mismo formato que la web y Android).</summary>
public sealed record LinkCode(string Token, object ChatId, string Key, string Db)
{
    public string Encode() => "ENC1." + Crypto.B64U.Enc(Crypto.Utf8(Json.Stringify(JObj.Of(("t", Token), ("c", ChatId), ("k", Key), ("d", Db)))));
    public static LinkCode? Parse(string s)
    {
        try
        {
            var t = s.Trim(); if (!t.StartsWith("ENC1.", StringComparison.Ordinal)) return null;
            if (Json.Parse(Crypto.FromUtf8(Crypto.B64U.Dec(t[5..]))) is not JObj o) return null;
            var token = o["t"] as string; var c = o["c"]; var k = o["k"] as string; var d = o["d"] as string;
            return string.IsNullOrEmpty(token) || c == null || string.IsNullOrEmpty(k) || string.IsNullOrEmpty(d) ? null : new LinkCode(token, c, k, d);
        }
        catch { return null; }
    }
}
