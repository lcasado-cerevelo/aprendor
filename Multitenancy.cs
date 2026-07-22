using Microsoft.EntityFrameworkCore;
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
