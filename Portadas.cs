using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TrainingPlatform.Multitenancy;
using TrainingPlatform.TenantData;

namespace TrainingPlatform;

// ============================================================================
// Foto de la tarjeta de cada curso: la de su lámina de portada (Info con layout
// 'cover'; si no hay, la foto de la pantalla de entrada). Puede estar incrustada
// (data:image/…, lo normal en los cursos de content/), subida a la plataforma
// (/media/{id}) o en internet (https). GET /media/portada/{trainingId} la entrega:
// va bajo /media para que la tarjeta la pueda pedir con ?access_token= en el
// background-image (el token por query solo se acepta en /media). /trainings dice
// en `hasCover` qué cursos la tienen, para no pedir una foto que no existe.
// Versión: para autores, el borrador si lo hay (refleja lo que editan); para los
// demás, la última publicada.
// ============================================================================
public static class Portadas
{
    private static readonly Regex DataImage = new(@"^data:(image/(png|jpe?g|gif|webp));base64,(.+)$", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex UrlMedia = new(@"^/media/([0-9a-fA-F-]{36})$", RegexOptions.Compiled);

    // Versión que muestra la tarjeta, por curso.
    public static async Task<Dictionary<Guid, Guid>> VersionesAsync(TenantDbContext db, bool autor, IEnumerable<Guid>? cursos = null)
    {
        var q = db.TrainingVersions.AsNoTracking().Where(v => v.Status == "published" || (autor && v.Status == "draft"));
        if (cursos is not null) { var ids = cursos.ToList(); q = q.Where(v => ids.Contains(v.TrainingId)); }
        var filas = await q.Select(v => new { v.Id, v.TrainingId, v.VersionNumber, v.Status }).ToListAsync();
        return filas.GroupBy(v => v.TrainingId).ToDictionary(g => g.Key,
            g => g.OrderByDescending(v => autor && v.Status == "draft").ThenByDescending(v => v.VersionNumber).First().Id);
    }

    // Cursos con foto de portada (se busca el texto en SQL, sin traer las fotos).
    public static async Task<HashSet<Guid>> ConPortadaAsync(TenantDbContext db, bool autor)
    {
        var versiones = await VersionesAsync(db, autor);
        var ids = versiones.Values.ToList();
        var conFoto = await db.TrainingItems.AsNoTracking()
            .Where(i => ids.Contains(i.TrainingVersionId) && i.Active && i.Type == "Info"
                && (i.PayloadJson.Contains("\"cover\"") || i.PayloadJson.Contains("\"intro\""))
                && i.PayloadJson.Contains("\"photo\""))
            .Select(i => i.TrainingVersionId).Distinct().ToListAsync();
        var porVersion = conFoto.ToHashSet();
        return versiones.Where(kv => porVersion.Contains(kv.Value)).Select(kv => kv.Key).ToHashSet();
    }

    private static string? FotoDe(string payloadJson, string layout)
    {
        try
        {
            if (JsonNode.Parse(payloadJson) is not JsonObject p) return null;
            if (p["layout"] is not JsonValue l || !l.TryGetValue<string>(out var ls) || ls.Trim().ToLowerInvariant() != layout) return null;
            return p["photo"] is JsonValue f && f.TryGetValue<string>(out var s) && s.Length > 0 ? s.Trim() : null;
        }
        catch { return null; }
    }

    public static void MapPortadas(this WebApplication app)
    {
        app.MapGet("/media/portada/{trainingId:guid}", async (Guid trainingId, ITenantContext tc, IServiceProvider sp,
            IWebHostEnvironment env, IConfiguration cfg, HttpContext http) =>
        {
            if (tc.TenantId is null) return Results.NotFound();
            var db = sp.GetRequiredService<TenantDbContext>();
            var autor = ContenidoAcceso.PuedeCrear(tc.Role);
            var training = await db.Trainings.AsNoTracking().FirstOrDefaultAsync(t => t.Id == trainingId);
            if (training is null || (!autor && training.Status == "archived")) return Results.NotFound();
            var versiones = await VersionesAsync(db, autor, new[] { trainingId });
            if (!versiones.TryGetValue(trainingId, out var versionId)) return Results.NotFound();

            var candidatos = await db.TrainingItems.AsNoTracking()
                .Where(i => i.TrainingVersionId == versionId && i.Active && i.Type == "Info" && i.PayloadJson.Contains("\"photo\""))
                .OrderBy(i => i.Order).Select(i => i.PayloadJson).ToListAsync();
            var foto = candidatos.Select(p => FotoDe(p, "cover")).FirstOrDefault(f => f is not null)
                    ?? candidatos.Select(p => FotoDe(p, "intro")).FirstOrDefault(f => f is not null);
            if (foto is null) return Results.NotFound();

            var h = http.Response.Headers;
            h["X-Content-Type-Options"] = "nosniff";
            h.CacheControl = "private, max-age=600";

            if (DataImage.Match(foto) is { Success: true } m)
            {
                byte[] bytes;
                try { bytes = Convert.FromBase64String(m.Groups[3].Value); } catch { return Results.NotFound(); }
                return Results.File(bytes, m.Groups[1].Value.Replace("image/jpg", "image/jpeg"));
            }
            if (UrlMedia.Match(foto) is { Success: true } mm && Guid.TryParse(mm.Groups[1].Value, out var mediaId))
            {
                var asset = await db.MediaAssets.AsNoTracking().FirstOrDefaultAsync(a => a.Id == mediaId && a.Purpose == "course");
                if (asset is null) return Results.NotFound();
                var root = Path.Combine(env.ContentRootPath, cfg["Storage:UploadsPath"] ?? "App_Data/uploads");
                var full = Path.Combine(root, asset.RelativePath);
                if (!File.Exists(full)) return Results.NotFound();
                var tipo = MediaTipos.PorExtension(Path.GetExtension(asset.RelativePath).ToLowerInvariant());
                if (tipo is null || !tipo.StartsWith("image/")) return Results.NotFound();
                return Results.File(full, tipo);
            }
            if (foto.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return Results.Redirect(foto);
            return Results.NotFound();
        }).RequireAuthorization();
    }
}
