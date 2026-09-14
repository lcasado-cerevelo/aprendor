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

    public static bool Verify(string password, string stored)
    {
        var parts = stored.Split('.');
        if (parts.Length != 2) return false;
        byte[] salt = Convert.FromBase64String(parts[0]);
        byte[] expected = Convert.FromBase64String(parts[1]);
        byte[] actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
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
    // de los teléfonos van con unos segundos de diferencia.
    public static bool Verificar(string? secretoBase32, string? codigo, int ventana = 1)
    {
        if (string.IsNullOrWhiteSpace(secretoBase32) || string.IsNullOrWhiteSpace(codigo)) return false;
        codigo = codigo.Trim().Replace(" ", "");
        if (codigo.Length != Digitos) return false;

        byte[] clave;
        try { clave = Base32Decode(secretoBase32); } catch { return false; }

        var paso = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / PeriodoSegundos;
        for (long d = -ventana; d <= ventana; d++)
            if (CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(Calcular(clave, paso + d)), Encoding.ASCII.GetBytes(codigo)))
                return true;
        return false;
    }

    public static string UriDeConfiguracion(string emisor, string cuenta, string secreto)
        => $"otpauth://totp/{Uri.EscapeDataString(emisor)}:{Uri.EscapeDataString(cuenta)}" +
           $"?secret={secreto}&issuer={Uri.EscapeDataString(emisor)}&algorithm=SHA1&digits={Digitos}&period={PeriodoSegundos}";

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

public class JwtTokenService
{
    private readonly IConfiguration _cfg;
    public JwtTokenService(IConfiguration cfg) => _cfg = cfg;

    // tenantId/role permiten emitir el token para una compañía distinta a la
    // principal, cuando el usuario pertenece a varias y cambia de una a otra.
    public string Create(AppUser u, Guid? tenantId = null, string? role = null)
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
        };
        if (compañia.HasValue)
            claims.Add(new Claim("tenant_id", compañia.Value.ToString()));

        var token = new JwtSecurityToken(
            issuer: _cfg["Jwt:Issuer"],
            audience: _cfg["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
