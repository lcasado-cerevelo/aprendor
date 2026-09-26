using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using TrainingPlatform.Catalog;

namespace TrainingPlatform.Auth;

// Dependency-free PBKDF2 hashing (no external package).
public static class PasswordHasher
{
    private const int Iterations = 100_000;

    public static string Hash(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    // Tolera nulos y hashes vacíos o corruptos (cuenta invitada que aún no fijó su clave):
    // en esos casos compara contra el hash ficticio, así tarda lo mismo y devuelve false.
    public static bool Verify(string? password, string? stored)
    {
        password ??= "";
        var parts = (stored ?? "").Split('.');
        byte[] salt, expected;
        bool valido = parts.Length == 2;
        try
        {
            salt = valido ? Convert.FromBase64String(parts[0]) : SalFicticia;
            expected = valido ? Convert.FromBase64String(parts[1]) : EsperadoFicticio;
        }
        catch (FormatException) { salt = SalFicticia; expected = EsperadoFicticio; valido = false; }
        byte[] actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        return CryptographicOperations.FixedTimeEquals(actual, expected) && valido;
    }

    // Hash ficticio precalculado al arrancar: cuando el usuario no existe o está bloqueado
    // se verifica contra él, para que la respuesta tarde lo mismo que con un usuario real
    // (si no, el tiempo delata qué correos están registrados).
    private static readonly byte[] SalFicticia = RandomNumberGenerator.GetBytes(16);
    private static readonly byte[] EsperadoFicticio = RandomNumberGenerator.GetBytes(32);
    public static readonly string Ficticio = $"{Convert.ToBase64String(SalFicticia)}.{Convert.ToBase64String(EsperadoFicticio)}";

    // Gasta el mismo tiempo que una verificación real, sin resultado.
    public static void VerifyFicticio(string? password) => Verify(password, Ficticio);
}

// Tokens de recuperación de contraseña: se envía el valor en claro por correo y en
// la base solo queda su SHA-256, igual que con una contraseña.
public static class ResetTokens
{
    public const int VigenciaMinutos = 10;

    public static (string token, string hash) Create()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var token = Base64Url(bytes);
        return (token, Hash(token));
    }

    public static string Hash(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string Base64Url(byte[] b)
        => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

// TOTP (RFC 6238) para apps autenticadoras: Google Authenticator, Authy, Microsoft
// Authenticator, 1Password. HMAC-SHA1, 6 dígitos, ventanas de 30 segundos.
public static class Totp
{
    public const int Digitos = 6;
    public const int PeriodoSegundos = 30;

    public static string NuevoSecreto() => Base32Encode(RandomNumberGenerator.GetBytes(20));

    // Acepta la ventana actual y una hacia atrás y otra hacia delante: los relojes
    // de los teléfonos van con unos segundos de diferencia. Devuelve el paso (contador de
    // 30 s) que coincidió, o null: quien llama lo compara con AppUser.LastTotpStep para
    // que un código ya aceptado no se pueda volver a usar dentro de su ventana.
    public static long? Verificar(string? secretoBase32, string? codigo, int ventana = 1)
    {
        if (string.IsNullOrWhiteSpace(secretoBase32) || string.IsNullOrWhiteSpace(codigo)) return null;
        codigo = codigo.Trim().Replace(" ", "");
        if (codigo.Length != Digitos) return null;

        byte[] clave;
        try { clave = Base32Decode(secretoBase32); } catch { return null; }

        var paso = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / PeriodoSegundos;
        long? acierto = null;
        for (long d = -ventana; d <= ventana; d++)
            if (CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(Calcular(clave, paso + d)), Encoding.ASCII.GetBytes(codigo)))
                acierto ??= paso + d;
        return acierto;
    }

    public static string UriDeConfiguracion(string emisor, string cuenta, string secreto)
        => $"otpauth://totp/{Uri.EscapeDataString(emisor)}:{Uri.EscapeDataString(cuenta)}" +
           $"?secret={secreto}&issuer={Uri.EscapeDataString(emisor)}&algorithm=SHA1&digits={Digitos}&period={PeriodoSegundos}";

    // Código QR del URI otpauth, en SVG y como data URI (data:image/svg+xml;base64,...),
    // para escanearlo con la app en vez de teclear la clave. Se genera en el servidor
    // (QRCoder, MIT) y no sale a ningún servicio externo: el QR lleva el secreto.
    // Corrección M y zona tranquila de 4 módulos, lo que leen bien todas las apps.
    public static string QrDataUri(string uri)
    {
        using var generador = new QRCoder.QRCodeGenerator();
        using var datos = generador.CreateQrCode(uri, QRCoder.QRCodeGenerator.ECCLevel.M);
        var svg = new QRCoder.SvgQRCode(datos).GetGraphic(8, "#000000", "#ffffff", true,
            QRCoder.SvgQRCode.SizingMode.ViewBoxAttribute);
        return "data:image/svg+xml;base64," + Convert.ToBase64String(Encoding.UTF8.GetBytes(svg));
    }

    private static string Calcular(byte[] clave, long contador)
    {
        var bytes = BitConverter.GetBytes(contador);
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        var hmac = HMACSHA1.HashData(clave, bytes);
        int offset = hmac[^1] & 0x0F;
        int binario = ((hmac[offset] & 0x7F) << 24) | (hmac[offset + 1] << 16)
                    | (hmac[offset + 2] << 8) | hmac[offset + 3];
        return (binario % (int)Math.Pow(10, Digitos)).ToString(new string('0', Digitos));
    }

    private const string Alfabeto = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    private static string Base32Encode(byte[] datos)
    {
        var sb = new StringBuilder();
        int buffer = 0, bits = 0;
        foreach (var b in datos)
        {
            buffer = (buffer << 8) | b; bits += 8;
            while (bits >= 5) { sb.Append(Alfabeto[(buffer >> (bits - 5)) & 31]); bits -= 5; }
        }
        if (bits > 0) sb.Append(Alfabeto[(buffer << (5 - bits)) & 31]);
        return sb.ToString();
    }

    private static byte[] Base32Decode(string s)
    {
        s = s.Trim().Replace(" ", "").Replace("=", "").ToUpperInvariant();
        var salida = new List<byte>(s.Length * 5 / 8);
        int buffer = 0, bits = 0;
        foreach (var c in s)
        {
            int v = Alfabeto.IndexOf(c);
            if (v < 0) throw new FormatException("Secreto base32 inválido.");
            buffer = (buffer << 5) | v; bits += 5;
            if (bits >= 8) { salida.Add((byte)((buffer >> (bits - 8)) & 0xFF)); bits -= 8; }
        }
        return salida.ToArray();
    }
}

// Códigos de un solo uso enviados por correo (segundo factor y confirmación de alta).
public static class OtpCodigos
{
    public static string Nuevo() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("000000");
    public static string Hash(string codigo) => ResetTokens.Hash(codigo.Trim());
}

// Alcance del token (claim "scope"). Solo "full" sirve para la API; los demás son
// tokens de 15 minutos que únicamente abren /me, /me/password, /me/2fa/* y /me/email/*,
// para completar lo que falta antes de entrar. Orden: cambio de clave obligatorio >
// inscripción del doble factor exigida > correo sin validar.
public static class Alcances
{
    public const string Full = "full";
    public const string ChangePassword = "change-password";
    public const string Enroll2fa = "enroll-2fa";
    public const string VerifyEmail = "verify-email";

    public static readonly TimeSpan VidaCompleta = TimeSpan.FromHours(8);
    public static readonly TimeSpan VidaRestringida = TimeSpan.FromMinutes(15);

    public static TimeSpan Vida(string scope) => scope == Full ? VidaCompleta : VidaRestringida;

    // politicaEfectiva: la más estricta entre las compañías del usuario (off|optional|required).
    public static string Calcular(AppUser u, string politicaEfectiva)
    {
        var tiene2fa = u.TwoFactorMode == "totp" && u.TwoFactorConfirmedAt is not null;
        if (u.MustChangePassword) return ChangePassword;
        if (politicaEfectiva == "required" && !tiene2fa) return Enroll2fa;
        if (u.EmailVerifiedAt is null) return VerifyEmail;
        return Full;
    }
}

// Cómo se autenticó la sesión (claim "amr"): pwd = solo contraseña; mfa = contraseña y
// segundo factor (o el alta del autenticador); mfa-trusted = red de confianza (bloque S3).
public static class Amr
{
    public const string Pwd = "pwd";
    public const string Mfa = "mfa";
    public const string MfaTrusted = "mfa-trusted";

    public static bool EsMfa(string? amr) => amr is Mfa or MfaTrusted;
}

public class JwtTokenService
{
    private readonly IConfiguration _cfg;
    public JwtTokenService(IConfiguration cfg) => _cfg = cfg;

    // tenantId/role permiten emitir el token para una compañía distinta a la
    // principal, cuando el usuario pertenece a varias y cambia de una a otra.
    // expiraAntesDe: tope de expiración (cambio de compañía: no extiende la sesión).
    public string Create(AppUser u, Guid? tenantId = null, string? role = null,
        string scope = Alcances.Full, string amr = Amr.Pwd, DateTime? expiraAntesDe = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_cfg["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var compañia = tenantId ?? u.TenantId;
        var claims = new List<Claim>
        {
            new("sub", u.Id.ToString()),
            new("email", u.Email),
            new("name", u.Name),
            new("role", string.IsNullOrWhiteSpace(role) ? u.Role : role!),
            new("sst", u.SecurityStamp.ToString()),
            new("scope", scope),
            new("amr", amr),
        };
        if (compañia.HasValue)
            claims.Add(new Claim("tenant_id", compañia.Value.ToString()));

        var expira = DateTime.UtcNow.Add(Alcances.Vida(scope));
        if (expiraAntesDe is DateTime tope)
        {
            // Las fechas que vuelven de SQL Server llegan con Kind=Unspecified y el
            // manejador de JWT las tomaría como hora local: todas son UTC.
            if (tope.Kind != DateTimeKind.Utc) tope = DateTime.SpecifyKind(tope, DateTimeKind.Utc);
            if (tope < expira) expira = tope;
        }

        var token = new JwtSecurityToken(
            issuer: _cfg["Jwt:Issuer"],
            audience: _cfg["Jwt:Audience"],
            claims: claims,
            expires: expira,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
