using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace TrainingPlatform.Catalog;

// Maps a client organization to the database where its data lives.
public class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string ConnectionString { get; set; } = "";
    public string Status { get; set; } = "active"; // active | disabled
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Política de doble factor de ESTA compañía. La decide la compañía, no el usuario:
    //   off      = no se usa
    //   optional = quien quiera lo activa desde su perfil
    //   required = obligatorio; sin él no se entra
    public string TwoFactorPolicy { get; set; } = "optional";

    // Reglas de cumplimiento de la compañía (blob JSON, ver ComplianceConfig):
    // a quién más se copia, cómo se entrega el certificado y cadencia de avisos.
    public string ComplianceConfigJson { get; set; } = "{}";

    // Seguridad de acceso de la compañía (blob JSON, ver SecurityConfig): redes de
    // confianza, recuperación del 2FA por correo y avisos a los Admin. Migración
    // CompanySecurityConfig; las filas que ya existían toman "{}" (valores por defecto).
    public string SecurityConfigJson { get; set; } = "{}";
}

// Seguridad de acceso por compañía, guardada en Tenant.SecurityConfigJson. Lectura
// tolerante (como ComplianceConfig): un blob que falta, corrupto o con valores malos
// deja el valor por defecto de ese campo; las redes que no se entienden se descartan.
public class SecurityConfig
{
    public const int MaxRedes = 50;

    // Redes (CIDR, o una IP sola) desde las que no se pide el doble factor ni su alta.
    public List<string> TrustedNetworks { get; set; } = new();
    // «Perdí mi autenticador»: recuperar el acceso con un código al correo validado.
    public bool EmailRecoveryEnabled { get; set; } = true;
    // Avisar a los Admin de la compañía de recuperaciones y restablecimientos de clave.
    public bool NotifyAdmins { get; set; } = true;

    public List<Cidr> Redes()
    {
        var lista = new List<Cidr>();
        foreach (var s in TrustedNetworks)
            if (Cidr.TryParse(s, out var c, out _)) lista.Add(c);
        return lista;
    }

    public static SecurityConfig Parse(string? json)
    {
        var cfg = new SecurityConfig();
        System.Text.Json.Nodes.JsonObject? n;
        try { n = System.Text.Json.Nodes.JsonNode.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json!) as System.Text.Json.Nodes.JsonObject; }
        catch { return cfg; }
        if (n is null) return cfg;

        IEnumerable<string?> redes = Array.Empty<string?>();
        if (n["trustedNetworks"] is System.Text.Json.Nodes.JsonArray arr) redes = arr.Select(Texto);
        else if (Texto(n["trustedNetworks"]) is string lista) redes = lista.Split(',', ';');
        cfg.TrustedNetworks = Normalizar(redes, out _);

        cfg.EmailRecoveryEnabled = Booleano(n["emailRecoveryEnabled"]) ?? cfg.EmailRecoveryEnabled;
        cfg.NotifyAdmins = Booleano(n["notifyAdmins"]) ?? cfg.NotifyAdmins;
        return cfg;
    }

    // Deja cada red en su forma canónica (dirección de red/prefijo), sin repetidas y
    // con el tope de MaxRedes. invalidas: las que no se entendieron (para el 400 del PUT).
    public static List<string> Normalizar(IEnumerable<string?> redes, out List<string> invalidas)
    {
        var ok = new List<string>();
        invalidas = new List<string>();
        foreach (var r in redes)
        {
            var s = (r ?? "").Trim();
            if (s.Length == 0) continue;
            if (Cidr.TryParse(s, out var c, out _))
            {
                var canon = c.ToString();
                if (!ok.Contains(canon, StringComparer.OrdinalIgnoreCase)) ok.Add(canon);
            }
            else invalidas.Add(s);
        }
        return ok.Take(MaxRedes).ToList();
    }

    public string ToJson() => System.Text.Json.JsonSerializer.Serialize(new
    {
        trustedNetworks = TrustedNetworks,
        emailRecoveryEnabled = EmailRecoveryEnabled,
        notifyAdmins = NotifyAdmins
    });

    private static string? Texto(System.Text.Json.Nodes.JsonNode? x)
    {
        try { return x?.GetValue<string>(); } catch { return null; }
    }

    // Acepta true/false y "true"/"false".
    private static bool? Booleano(System.Text.Json.Nodes.JsonNode? x)
    {
        if (x is null) return null;
        try { return x.GetValue<bool>(); }
        catch { return bool.TryParse(Texto(x), out var b) ? b : null; }
    }
}

// Red IP en notación CIDR (203.0.113.0/24, 2001:db8::/48) o una IP sola (/32 o /128).
// Las IPv4 escritas como IPv6 (::ffff:1.2.3.4) se tratan como IPv4. Se rechazan las
// redes demasiado amplias (menos de /8 en IPv4 o de /32 en IPv6): una red de confianza
// quita el doble factor a quien entre desde ella, y 0.0.0.0/0 sería quitárselo a todos.
public readonly struct Cidr
{
    public const int MinPrefijoV4 = 8;
    public const int MinPrefijoV6 = 32;

    public System.Net.IPAddress Red { get; }
    public int Prefijo { get; }

    private Cidr(System.Net.IPAddress red, int prefijo) { Red = red; Prefijo = prefijo; }

    public static bool TryParse(string? texto, out Cidr cidr, out string? error)
    {
        cidr = default;
        error = null;
        var s = (texto ?? "").Trim();
        if (s.Length == 0 || s.Length > 64) { error = "vacía o demasiado larga"; return false; }
        var partes = s.Split('/');
        if (partes.Length > 2 || partes[0].Contains('%')) { error = "formato no válido"; return false; }
        // IPAddress.TryParse acepta formas raras de IPv4 ("10" = 0.0.0.10): se exigen los 4 números.
        var esV6 = partes[0].Contains(':');
        if (!esV6 && partes[0].Count(ch => ch == '.') != 3) { error = "formato no válido"; return false; }
        if (!System.Net.IPAddress.TryParse(partes[0], out var ip)) { error = "IP no válida"; return false; }
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();

        var v4 = ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork;
        var prefijo = v4 ? 32 : 128;
        if (partes.Length == 2)
        {
            if (!int.TryParse(partes[1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out prefijo)
                || prefijo > (esV6 ? 128 : 32))
            { error = "prefijo no válido"; return false; }
            // ::ffff:1.2.3.0/120 → 1.2.3.0/24
            if (esV6 && v4) prefijo -= 96;
            if (prefijo < 0) { error = "prefijo no válido"; return false; }
        }
        if (prefijo < (v4 ? MinPrefijoV4 : MinPrefijoV6)) { error = "red demasiado amplia"; return false; }

        cidr = new Cidr(new System.Net.IPAddress(Enmascarar(ip.GetAddressBytes(), prefijo)), prefijo);
        return true;
    }

    public bool Contiene(System.Net.IPAddress? ip)
    {
        if (ip is null || Red is null) return false;
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        if (ip.AddressFamily != Red.AddressFamily) return false;
        var a = Enmascarar(ip.GetAddressBytes(), Prefijo);
        return a.AsSpan().SequenceEqual(Red.GetAddressBytes());
    }

    public bool Contiene(string? ip)
        => System.Net.IPAddress.TryParse(ip ?? "", out var dir) && Contiene(dir);

    private static byte[] Enmascarar(byte[] bytes, int prefijo)
    {
        var r = (byte[])bytes.Clone();
        for (int i = 0; i < r.Length; i++)
        {
            var bits = Math.Clamp(prefijo - i * 8, 0, 8);
            r[i] &= (byte)(0xFF << (8 - bits));
        }
        return r;
    }

    public override string ToString() => Red is null ? "" : $"{Red}/{Prefijo}";
}

// Enlace directo a un certificado (GET /c/{token}), con vencimiento. Vive en el
// catálogo porque el enlace es anónimo: hay que saber de qué compañía es ANTES de
// conectarse a su base. Del token solo se guarda el hash (SHA-256 en base64): quien
// lea la tabla no puede abrir ningún certificado.
public class CertificateLink
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid CertificateId { get; set; }             // Certificate.Id en la base de la compañía
    public string Purpose { get; set; } = "learner";    // learner | officer | extra
    public string TokenHash { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public int AccessCount { get; set; }
    public DateTime? LastAccessAt { get; set; }
    public DateTime? RevokedAt { get; set; }            // no nulo = revocado a mano
}

// Reglas de cumplimiento por compañía, guardadas en Tenant.ComplianceConfigJson.
// Lectura tolerante (como NotificationConfig): si el blob falta, está corrupto o
// trae valores fuera de rango, se usa el valor por defecto de ese campo.
public class ComplianceConfig
{
    // Correos que reciben copia del certificado y de los resúmenes, además de los
    // oficiales de cumplimiento marcados.
    public List<string> ExtraEmails { get; set; } = new();
    public int DueSoonDays { get; set; } = 30;                  // ventana de "por vencer"
    public string DigestFrequency { get; set; } = "weekly";     // daily | weekly | monthly
    public string DigestDayOfWeek { get; set; } = "Monday";     // solo weekly
    public int DigestDayOfMonth { get; set; } = 1;              // solo monthly
    public int DigestHour { get; set; } = 8;                    // hora mínima de envío (hora de la aplicación, App:TimeZone)
    public int ExpiredRepeatDays { get; set; } = 14;            // cada cuánto se repite un vencido
    public bool IncludeNotStarted { get; set; } = true;
    public string CertificateDelivery { get; set; } = "link";   // link | attachment
    public int LinkDays { get; set; } = 30;                     // vigencia del enlace del certificado

    public static ComplianceConfig Parse(string? json)
    {
        var cfg = new ComplianceConfig();
        System.Text.Json.Nodes.JsonObject? n;
        try { n = System.Text.Json.Nodes.JsonNode.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json!) as System.Text.Json.Nodes.JsonObject; }
        catch { return cfg; }
        if (n is null) return cfg;

        // Cada campo por separado: uno malo no tumba a los demás.
        if (n["extraEmails"] is System.Text.Json.Nodes.JsonArray arr)
            cfg.ExtraEmails = LimpiarCorreos(arr.Select(x => Texto(x)));
        else if (Texto(n["extraEmails"]) is string lista)   // también "a@x.com, b@y.com"
            cfg.ExtraEmails = LimpiarCorreos(lista.Split(',', ';'));

        cfg.DueSoonDays = Entero(n["dueSoonDays"], 1, 365) ?? cfg.DueSoonDays;
        var freq = Texto(n["digestFrequency"])?.Trim().ToLowerInvariant();
        if (freq is "daily" or "weekly" or "monthly") cfg.DigestFrequency = freq;
        if (Enum.TryParse<DayOfWeek>(Texto(n["digestDayOfWeek"])?.Trim(), true, out var dia) && Enum.IsDefined(dia))
            cfg.DigestDayOfWeek = dia.ToString();
        cfg.DigestDayOfMonth = Entero(n["digestDayOfMonth"], 1, 31) ?? cfg.DigestDayOfMonth;
        cfg.DigestHour = Entero(n["digestHour"], 0, 23) ?? cfg.DigestHour;
        cfg.ExpiredRepeatDays = Entero(n["expiredRepeatDays"], 1, 365) ?? cfg.ExpiredRepeatDays;
        try { if (n["includeNotStarted"] is not null) cfg.IncludeNotStarted = n["includeNotStarted"]!.GetValue<bool>(); } catch { }
        var entrega = Texto(n["certificateDelivery"])?.Trim().ToLowerInvariant();
        if (entrega is "link" or "attachment") cfg.CertificateDelivery = entrega;
        cfg.LinkDays = Entero(n["linkDays"], 1, 365) ?? cfg.LinkDays;
        return cfg;
    }

    public string ToJson() => System.Text.Json.JsonSerializer.Serialize(new
    {
        extraEmails = ExtraEmails,
        dueSoonDays = DueSoonDays,
        digestFrequency = DigestFrequency,
        digestDayOfWeek = DigestDayOfWeek,
        digestDayOfMonth = DigestDayOfMonth,
        digestHour = DigestHour,
        expiredRepeatDays = ExpiredRepeatDays,
        includeNotStarted = IncludeNotStarted,
        certificateDelivery = CertificateDelivery,
        linkDays = LinkDays
    });

    private static string? Texto(System.Text.Json.Nodes.JsonNode? x)
    {
        try { return x?.GetValue<string>(); } catch { return null; }
    }

    // Acepta 30 y "30"; fuera de rango o ilegible = null (se queda el default).
    private static int? Entero(System.Text.Json.Nodes.JsonNode? x, int min, int max)
    {
        if (x is null) return null;
        int v;
        try { v = x.GetValue<int>(); }
        catch
        {
            if (!int.TryParse(Texto(x), out v)) return null;
        }
        return v >= min && v <= max ? v : null;
    }

    private static List<string> LimpiarCorreos(IEnumerable<string?> correos)
        => correos.Select(c => (c ?? "").Trim())
                  .Where(c => c.Length is > 3 and <= 254 && c.Contains('@') && !c.Contains(' '))
                  .Distinct(StringComparer.OrdinalIgnoreCase)
                  .Take(20)
                  .ToList();
}

// Un usuario puede pertenecer a varias compañías, con rol distinto en cada una.
// La compañía "principal" sigue viviendo en AppUser.TenantId (es la que se resuelve
// al entrar); esta tabla añade las demás.
public class UserCompany
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public string Role { get; set; } = "Learner";   // Admin | Author | Learner (Moderator se retiró en sep 2026 y pasó a Author)
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Oficial de cumplimiento de ESTA compañía. No es un rol: se suma al rol que tenga.
    // Recibe copia de los certificados y los resúmenes de cumplimiento, ve el panel de
    // cumplimiento y el expediente de cualquier empleado. Para marcar a alguien cuya
    // compañía principal es esta (AppUser.TenantId) se crea una fila "espejo" de la
    // principal (mismo rol y misma fecha de alta que la cuenta): la principal sigue
    // mandando en todo lo demás.
    public bool IsComplianceOfficer { get; set; }
}

// Identity + routing live centrally so we know the tenant before connecting to its DB.
public class AppUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = "";
    public string Name { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "Learner"; // Admin | Author | Learner (Moderator se retiró en sep 2026 y pasó a Author)
    public Guid? TenantId { get; set; }            // null = platform admin (no tenant)
    public bool MustChangePassword { get; set; }   // true tras un reseteo del admin
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Validación del correo. Mientras esté en null, la app pide confirmarlo con un
    // código antes de dejar entrar: el certificado se manda a esta dirección, así que
    // tiene que ser correcta y del propio empleado.
    public DateTime? EmailVerifiedAt { get; set; }

    // Verificación en dos pasos. "none" = solo contraseña.
    public string TwoFactorMode { get; set; } = "none";   // none | email | totp
    public string? TotpSecret { get; set; }               // base32, solo para el modo totp
    public DateTime? TwoFactorConfirmedAt { get; set; }   // null = configurado pero sin confirmar

    // ---- Seguridad de la sesión y del acceso (migración SecurityHardening) ----

    // Sello de seguridad: va en el token (claim "sst") y se compara en cada petición.
    // Cambiarlo invalida todas las sesiones abiertas: se rota al cambiar o restablecer la
    // contraseña, al tocar el doble factor, al cambiar el rol y al quitar una membresía.
    // Las filas que ya existían reciben uno nuevo al migrar (NEWID()).
    public Guid SecurityStamp { get; set; } = Guid.NewGuid();

    // Bloqueo por contraseña: 5 fallos seguidos bloquean la entrada 15 minutos.
    public int AccessFailedCount { get; set; }
    public DateTime? LockoutEnd { get; set; }

    // Bloqueo del segundo factor: 5 códigos fallidos, sumando todos los retos (TOTP y
    // códigos por correo), bloquean 15 minutos. LastTotpStep es el último paso de 30 s
    // aceptado: un código ya usado no vuelve a valer aunque siga en su ventana.
    public int TwoFactorFailedCount { get; set; }
    public DateTime? TwoFactorLockedUntil { get; set; }
    public long? LastTotpStep { get; set; }

    // Secreto TOTP nuevo mientras se confirma (alta o cambio de autenticador): el vigente
    // sigue en TotpSecret hasta que el usuario demuestra que el nuevo funciona.
    public string? PendingTotpSecret { get; set; }

    // Vencimiento de una contraseña temporal generada por el admin (72 h). Null = la
    // contraseña la escogió el propio usuario.
    public DateTime? TempPasswordExpiresAt { get; set; }
}

// Reto de segundo factor: se crea al validar la contraseña y se consume con el código.
// Del código por correo solo se guarda el hash.
public class TwoFactorChallenge
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Mode { get; set; } = "email";      // email | totp
    public string Purpose { get; set; } = "login";   // login | enroll
    public string? CodeHash { get; set; }            // solo en modo email
    public int Attempts { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }

    // Reto abierto por /me/switch-company hacia una compañía que exige doble factor: al
    // verificarlo se emite el token para ESA compañía, sin pasar de la expiración que
    // tenía la sesión (SessionExpiresAt).
    public Guid? TargetTenantId { get; set; }
    public DateTime? SessionExpiresAt { get; set; }
}

// Token de recuperación de contraseña pedido desde el login.
// Se guarda solo el HASH del token: si alguien lee la tabla no puede usarlo.
public class PasswordResetToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = "";      // SHA-256 del token enviado por correo
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }            // no nulo = ya se consumió

    // reset  = "¿Olvidaste tu contraseña?" (10 min)
    // invite = invitación del admin para que el usuario cree su clave (72 h)
    public string Purpose { get; set; } = "reset";
}

public class CatalogAuditLog
{
    public long Id { get; set; }
    public string Action { get; set; } = "";
    public string? Detail { get; set; }
    public Guid? UserId { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
    // IP real de quien hizo la petición (ClientIp.Of). La pone CatalogDbContext al
    // guardar, a partir de AuditoriaIp; null en los procesos en segundo plano.
    public string? Ip { get; set; }
}

// Rastro de seguridad: intentos fallidos, bloqueos, códigos enviados, solicitudes de
// restablecimiento... Sirve para contar por cuenta (límites por usuario que no dependen
// de la IP) y para saber desde qué IP pasó cada cosa. Kind: ver EventosSeguridad.
public class SecurityEvent
{
    public long Id { get; set; }
    public Guid? UserId { get; set; }
    public Guid? TenantId { get; set; }
    public string Kind { get; set; } = "";
    public string? Ip { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
}

// IP de la petición en curso, para rellenar CatalogAuditLog.Ip sin pasarla a mano en
// cada endpoint. La fija un middleware de Program.cs (después de UseForwardedHeaders);
// es AsyncLocal: cada petición ve la suya.
public static class AuditoriaIp
{
    private static readonly AsyncLocal<string?> _ip = new();
    public static string? Actual { get => _ip.Value; set => _ip.Value = value; }
}

// Quién pertenece a una compañía: los que la tienen como principal más los que
// llegan por UserCompany. Se usa en todos los sitios que antes filtraban por
// AppUser.TenantId, para que un usuario multicompañía cuente en las dos.
public static class CompanyUsers
{
    public record Miembro(Guid Id, string Email, string Name, string Role);

    public static async Task<List<Miembro>> OfAsync(CatalogDbContext catalog, Guid tenantId)
    {
        var principales = await catalog.Users.Where(u => u.TenantId == tenantId)
            .Select(u => new Miembro(u.Id, u.Email, u.Name, u.Role)).ToListAsync();

        var extras = await (from m in catalog.UserCompanies
                            where m.TenantId == tenantId
                            join u in catalog.Users on m.UserId equals u.Id
                            select new Miembro(u.Id, u.Email, u.Name, m.Role)).ToListAsync();

        return principales.Concat(extras).GroupBy(x => x.Id).Select(g => g.First()).ToList();
    }
}

// Oficiales de cumplimiento de una compañía (marca UserCompany.IsComplianceOfficer).
// Consulta directa al catálogo en vez de un claim en el JWT: así quitar la marca
// surte efecto de inmediato, sin esperar a que venza el token.
public static class ComplianceOfficers
{
    public static async Task<List<CompanyUsers.Miembro>> OfAsync(CatalogDbContext catalog, Guid tenantId)
    {
        var lista = await (from m in catalog.UserCompanies
                           where m.TenantId == tenantId && m.IsComplianceOfficer
                           join u in catalog.Users on m.UserId equals u.Id
                           // Si es su compañía principal, el rol que vale es el de la cuenta.
                           select new CompanyUsers.Miembro(u.Id, u.Email, u.Name, u.TenantId == tenantId ? u.Role : m.Role))
                          .ToListAsync();
        return lista.GroupBy(x => x.Id).Select(g => g.First()).ToList();
    }

    public static Task<bool> EsOficialAsync(CatalogDbContext catalog, Guid? userId, Guid? tenantId)
        => userId is null || tenantId is null
            ? Task.FromResult(false)
            : catalog.UserCompanies.AnyAsync(m => m.UserId == userId && m.TenantId == tenantId && m.IsComplianceOfficer);
}

public class CatalogDbContext : DbContext
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options) { }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<CatalogAuditLog> AuditLogs => Set<CatalogAuditLog>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<TwoFactorChallenge> TwoFactorChallenges => Set<TwoFactorChallenge>();
    public DbSet<UserCompany> UserCompanies => Set<UserCompany>();
    public DbSet<CertificateLink> CertificateLinks => Set<CertificateLink>();
    public DbSet<SecurityEvent> SecurityEvents => Set<SecurityEvent>();

    // Toda fila nueva de auditoría lleva la IP de la petición que la originó.
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        PonerIpAuditoria();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        PonerIpAuditoria();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void PonerIpAuditoria()
    {
        var ip = AuditoriaIp.Actual;
        if (string.IsNullOrEmpty(ip)) return;
        foreach (var e in ChangeTracker.Entries<CatalogAuditLog>())
            if (e.State == EntityState.Added && e.Entity.Ip is null) e.Entity.Ip = ip;
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Nombres de tabla en singular.
        b.Entity<Tenant>().ToTable("Tenant");
        b.Entity<AppUser>().ToTable("User");
        b.Entity<CatalogAuditLog>().ToTable("AuditLog");

        b.Entity<PasswordResetToken>().ToTable("PasswordResetToken");

        b.Entity<AppUser>().HasIndex(u => u.Email).IsUnique();
        b.Entity<Tenant>().HasIndex(t => t.Name).IsUnique();
        b.Entity<PasswordResetToken>().HasIndex(t => t.TokenHash);
        b.Entity<PasswordResetToken>().HasIndex(t => t.UserId);
        b.Entity<TwoFactorChallenge>().ToTable("TwoFactorChallenge");
        b.Entity<TwoFactorChallenge>().HasIndex(c => c.UserId);
        b.Entity<UserCompany>().ToTable("UserCompany");
        b.Entity<UserCompany>().HasIndex(m => new { m.UserId, m.TenantId }).IsUnique();

        // Las filas que ya existían toman "{}" (config por defecto) al migrar.
        b.Entity<Tenant>().Property(t => t.ComplianceConfigJson).HasDefaultValue("{}");

        b.Entity<CertificateLink>().ToTable("CertificateLink");
        b.Entity<CertificateLink>().Property(l => l.TokenHash).HasMaxLength(64);
        b.Entity<CertificateLink>().Property(l => l.Purpose).HasMaxLength(16);
        b.Entity<CertificateLink>().HasIndex(l => l.TokenHash).IsUnique();
        b.Entity<CertificateLink>().HasIndex(l => new { l.TenantId, l.CertificateId });

        // ---- SecurityHardening ----
        // Filas que ya existían: sello nuevo (NEWID()), contadores en 0, propósito "reset".
        b.Entity<AppUser>().Property(u => u.SecurityStamp).HasDefaultValueSql("NEWID()");
        b.Entity<AppUser>().Property(u => u.PendingTotpSecret).HasMaxLength(64);
        b.Entity<PasswordResetToken>().Property(t => t.Purpose).HasMaxLength(16).HasDefaultValue("reset");
        b.Entity<CatalogAuditLog>().Property(a => a.Ip).HasMaxLength(64);

        b.Entity<SecurityEvent>().ToTable("SecurityEvent");
        b.Entity<SecurityEvent>().Property(e => e.Kind).HasMaxLength(40);
        b.Entity<SecurityEvent>().Property(e => e.Ip).HasMaxLength(64);
        b.Entity<SecurityEvent>().HasIndex(e => new { e.Kind, e.UserId, e.At });
        b.Entity<SecurityEvent>().HasIndex(e => new { e.Kind, e.Ip, e.At });

        // ---- CompanySecurityConfig ----
        b.Entity<Tenant>().Property(t => t.SecurityConfigJson).HasDefaultValue("{}");
    }
}

// Lets `dotnet ef migrations add ... --context CatalogDbContext` work at design time.
public class CatalogDbContextFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    public CatalogDbContext CreateDbContext(string[] args)
    {
        var cfg = new ConfigurationBuilder().AddJsonFile("appsettings.json", optional: false).Build();
        var opts = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseSqlServer(cfg.GetConnectionString("Catalog"))
            .Options;
        return new CatalogDbContext(opts);
    }
}
