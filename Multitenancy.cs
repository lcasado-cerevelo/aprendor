using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TrainingPlatform.Catalog;

namespace TrainingPlatform.Multitenancy;

// Scoped per request. Holds who the caller is and which DB to talk to.
public interface ITenantContext
{
    Guid? UserId { get; }
    Guid? TenantId { get; }
    string? Role { get; }
    string? ConnectionString { get; }
    void Set(Guid? userId, Guid? tenantId, string? role, string? connectionString);
}

public class TenantContext : ITenantContext
{
    public Guid? UserId { get; private set; }
    public Guid? TenantId { get; private set; }
    public string? Role { get; private set; }
    public string? ConnectionString { get; private set; }

    public void Set(Guid? userId, Guid? tenantId, string? role, string? connectionString)
    {
        UserId = userId;
        TenantId = tenantId;
        Role = role;
        ConnectionString = connectionString;
    }
}

// Reads the authenticated user's claims and resolves the tenant's connection string
// from the catalog. Runs once per request; the TenantDbContext is built from this.
public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    public TenantResolutionMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx, CatalogDbContext catalog, ITenantContext tenant)
    {
        var user = ctx.User;
        if (user.Identity?.IsAuthenticated == true)
        {
            Guid? userId = Guid.TryParse(user.FindFirst("sub")?.Value, out var uid) ? uid : null;
            var role = user.FindFirst("role")?.Value;
            Guid? tenantId = Guid.TryParse(user.FindFirst("tenant_id")?.Value, out var tid) ? tid : null;

            string? conn = null;
            if (tenantId is not null)
            {
                conn = await catalog.Tenants
                    .Where(t => t.Id == tenantId && t.Status == "active")
                    .Select(t => t.ConnectionString)
                    .FirstOrDefaultAsync();
            }

            tenant.Set(userId, tenantId, role, conn);
        }

        await _next(ctx);
    }
}

// IP real del cliente. UseForwardedHeaders (primer middleware, ver Program.cs) ya
// reemplazó RemoteIpAddress con la de X-Forwarded-For o CF-Connecting-IP cuando la
// petición llega desde un proxy de confianza (loopback o Security:TrustedProxies).
// Todo límite por IP y toda auditoría con IP usan este helper, nunca la conexión cruda.
public static class ClientIp
{
    public static string Of(HttpContext ctx)
    {
        var ip = ctx.Connection.RemoteIpAddress;
        if (ip is null) return "?";
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        return ip.ToString();
    }
}

// Admin de plataforma = rol Admin SIN compañía (AppUser.TenantId == null). Un Admin de
// compañía también lleva role=Admin en el token, así que no basta con el rol: además
// de que el token no traiga tenant_id, se confirma en el catálogo (con caché corta
// para no ir a la base en cada petición).
public static class AdminPlataforma
{
    public const string Politica = "PlatformAdmin";
    private static readonly TimeSpan Cache = TimeSpan.FromSeconds(60);

    public static bool EsPorToken(ClaimsPrincipal u)
        => u.HasClaim("role", "Admin") && !u.HasClaim(c => c.Type == "tenant_id");

    public static async Task<bool> EsAsync(CatalogDbContext catalog, IMemoryCache cache, Guid userId)
        => await cache.GetOrCreateAsync($"platform-admin:{userId}", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = Cache;
            return await catalog.Users.AsNoTracking()
                .AnyAsync(x => x.Id == userId && x.TenantId == null && x.Role == "Admin");
        });

    public static async Task<bool> EsAsync(ClaimsPrincipal u, CatalogDbContext catalog, IMemoryCache cache)
        => EsPorToken(u)
           && Guid.TryParse(u.FindFirst("sub")?.Value, out var id)
           && await EsAsync(catalog, cache, id);

    // Tras cambiar el rol o la compañía de alguien, que la caché no lo siga dando por admin.
    public static void Olvidar(IMemoryCache cache, Guid userId) => cache.Remove($"platform-admin:{userId}");
}

public sealed class PlatformAdminRequirement : IAuthorizationRequirement { }

public sealed class PlatformAdminHandler : AuthorizationHandler<PlatformAdminRequirement>
{
    private readonly CatalogDbContext _catalog;
    private readonly IMemoryCache _cache;
    public PlatformAdminHandler(CatalogDbContext catalog, IMemoryCache cache) { _catalog = catalog; _cache = cache; }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PlatformAdminRequirement requirement)
    {
        if (await AdminPlataforma.EsAsync(context.User, _catalog, _cache)) context.Succeed(requirement);
    }
}

// Pertenencia de una persona a una compañía: principal (AppUser.TenantId) o por
// UserCompany. Se usa para validar ids de usuario que llegan del cliente.
public static class Membresias
{
    public static async Task<bool> EsMiembroAsync(CatalogDbContext catalog, Guid tenantId, Guid? userId)
    {
        if (userId is not Guid id || id == Guid.Empty) return false;
        return await catalog.Users.AnyAsync(u => u.Id == id && u.TenantId == tenantId)
            || await catalog.UserCompanies.AnyAsync(m => m.UserId == id && m.TenantId == tenantId);
    }
}
