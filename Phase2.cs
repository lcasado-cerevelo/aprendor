using System.Security.Claims;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using TrainingPlatform.Catalog;
using TrainingPlatform.Multitenancy;
using TrainingPlatform.Notifications;
using TrainingPlatform.TenantData;

namespace TrainingPlatform;

public static class Phase2Endpoints
{
    private static TenantDbContext? Db(IServiceProvider sp, ITenantContext tc)
        => tc.TenantId is null ? null : sp.GetRequiredService<TenantDbContext>();

    private static bool CanAuthor(string? role) => role is "Admin" or "Author" or "Moderator";

    public static void MapPhase2(this WebApplication app)
    {
        // ---------------- Authoring ----------------
        app.MapGet("/trainings/{id:guid}/draft", async (Guid id, ITenantContext tc, IServiceProvider sp) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");
            if (!CanAuthor(tc.Role)) return Results.Forbid();
            var version = await GetOrCreateDraftAsync(db, id);
            if (version is null) return Results.NotFound("Training not found.");
            var items = await db.TrainingItems.Where(i => i.TrainingVersionId == version.Id)
                .OrderBy(i => i.Order).ToListAsync();
            return Results.Ok(new { versionId = version.Id, status = version.Status, version.PassPercent, items });
        }).RequireAuthorization();

        app.MapPost("/trainings/{id:guid}/items", async (Guid id, AddItemRequest req, ITenantContext tc, IServiceProvider sp) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");
            if (!CanAuthor(tc.Role)) return Results.Forbid();
            var version = await GetOrCreateDraftAsync(db, id);
            if (version is null) return Results.NotFound();
            if (version.Status != "draft") return Results.BadRequest("Version already published; create a new draft.");

            var siblings = await db.TrainingItems.Where(i => i.TrainingVersionId == version.Id)
                .OrderBy(i => i.Order).ThenBy(i => i.Id).ToListAsync();
            for (int k = 0; k < siblings.Count; k++) siblings[k].Order = k; // normaliza
            // posición de inserción: justo después del ítem de referencia; si no hay, al final
            int insertAt = siblings.Count;
            if (req.AfterItemId is Guid after)
            {
                var ai = siblings.FindIndex(i => i.Id == after);
                if (ai >= 0) insertAt = ai + 1;
            }
            foreach (var s in siblings.Where(s => s.Order >= insertAt)) s.Order += 1; // abre hueco

            var item = new TrainingItem
            {
                TrainingVersionId = version.Id,
                Order = insertAt,
                Type = req.Type,
                PayloadJson = req.PayloadJson,
                Points = req.Points,
                Required = req.Required,
                Active = req.Active ?? true
            };
            db.TrainingItems.Add(item);
            await db.SaveChangesAsync();
            return Results.Ok(item);
        }).RequireAuthorization();

        app.MapPut("/items/{itemId:guid}", async (Guid itemId, AddItemRequest req, ITenantContext tc, IServiceProvider sp) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");
            if (!CanAuthor(tc.Role)) return Results.Forbid();
            var item = await db.TrainingItems.FindAsync(itemId);
            if (item is null) return Results.NotFound();
            var version = await db.TrainingVersions.FindAsync(item.TrainingVersionId);
            if (version is null || version.Status != "draft")
                return Results.BadRequest("Solo se pueden editar ítems de un borrador.");
            item.Type = req.Type;
            item.PayloadJson = req.PayloadJson;
            item.Points = req.Points;
            item.Required = req.Required;
            item.Active = req.Active ?? true;
            await db.SaveChangesAsync();
            return Results.Ok(item);
        }).RequireAuthorization();

        app.MapPost("/items/{itemId:guid}/active", async (Guid itemId, ActiveRequest req, ITenantContext tc, IServiceProvider sp) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");
            if (!CanAuthor(tc.Role)) return Results.Forbid();
            var item = await db.TrainingItems.FindAsync(itemId);
            if (item is null) return Results.NotFound();
            var version = await db.TrainingVersions.FindAsync(item.TrainingVersionId);
            if (version is null || version.Status != "draft")
                return Results.BadRequest("Solo se pueden activar/desactivar ítems de un borrador.");
            item.Active = req.Active;
            await db.SaveChangesAsync();
            return Results.Ok(new { item.Id, item.Active });
        }).RequireAuthorization();

        app.MapDelete("/items/{itemId:guid}", async (Guid itemId, ITenantContext tc, IServiceProvider sp) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");
            if (!CanAuthor(tc.Role)) return Results.Forbid();
            var item = await db.TrainingItems.FindAsync(itemId);
            if (item is null) return Results.NotFound();
            db.TrainingItems.Remove(item);
            await db.SaveChangesAsync();
            return Results.Ok();
        }).RequireAuthorization();

        // Reordenar: intercambia el Order del ítem con su vecino (arriba/abajo) en el borrador.
        app.MapPost("/items/{itemId:guid}/move", async (Guid itemId, MoveRequest req, ITenantContext tc, IServiceProvider sp) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");
            if (!CanAuthor(tc.Role)) return Results.Forbid();
            var item = await db.TrainingItems.FindAsync(itemId);
            if (item is null) return Results.NotFound();
            var version = await db.TrainingVersions.FindAsync(item.TrainingVersionId);
            if (version is null || version.Status != "draft")
                return Results.BadRequest("Solo se pueden reordenar ítems de un borrador.");

            var siblings = await db.TrainingItems.Where(i => i.TrainingVersionId == item.TrainingVersionId)
                .OrderBy(i => i.Order).ThenBy(i => i.Id).ToListAsync();
            var idx = siblings.FindIndex(i => i.Id == itemId);
            var swap = req.Direction == "up" ? idx - 1 : idx + 1;
            if (idx < 0 || swap < 0 || swap >= siblings.Count) return Results.Ok(new { moved = false });

            // normaliza por si hay Order repetidos/huecos, luego intercambia
            for (int k = 0; k < siblings.Count; k++) siblings[k].Order = k;
            (siblings[idx].Order, siblings[swap].Order) = (siblings[swap].Order, siblings[idx].Order);
            await db.SaveChangesAsync();
            return Results.Ok(new { moved = true });
        }).RequireAuthorization();

        app.MapPost("/trainings/{id:guid}/publish", async (Guid id, ITenantContext tc, IServiceProvider sp) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");
            if (!CanAuthor(tc.Role)) return Results.Forbid();
            var version = await db.TrainingVersions
                .Where(v => v.TrainingId == id && v.Status == "draft")
                .OrderByDescending(v => v.VersionNumber).FirstOrDefaultAsync();
            if (version is null) return Results.BadRequest("No draft to publish.");
            if (!await db.TrainingItems.AnyAsync(i => i.TrainingVersionId == version.Id))
                return Results.BadRequest("Add at least one item before publishing.");

            version.Status = "published";
            version.PublishedAt = DateTime.UtcNow;
            var training = await db.Trainings.FindAsync(id);
            if (training is not null) training.Status = "published";
            await db.SaveChangesAsync();
            return Results.Ok(new { version.Id, version.VersionNumber, version.PublishedAt });
        }).RequireAuthorization();

        // ---------------- Opciones del reproductor por curso ----------------
        // allowBack: si el learner puede volver a la pantalla anterior mientras lo toma.
        // immediateFeedback: si ve si acertó o falló cada pregunta al momento de contestarla,
        // en vez de solo al terminar el intento (por defecto, apagado: se califica al final —
        // ver la nota de diseño en PlayerConfig.ImmediateFeedback).
        app.MapGet("/trainings/{id:guid}/player-config", async (Guid id, ITenantContext tc, IServiceProvider sp) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");
            if (!CanAuthor(tc.Role)) return Results.Forbid();
            var t = await db.Trainings.FindAsync(id);
            if (t is null) return Results.NotFound();
            return Results.Ok(new
            {
                allowBack = PlayerConfig.AllowBack(t.PlayerConfigJson),
                reviewAfterPass = PlayerConfig.ReviewAfterPass(t.PlayerConfigJson),
                immediateFeedback = PlayerConfig.ImmediateFeedback(t.PlayerConfigJson),
                presentation = PlayerConfig.Presentation(t.PlayerConfigJson)
            });
        }).RequireAuthorization();

        // El bloque `presentation` es opcional: si viene, se normaliza (valores por defecto,
        // colores válidos, transición conocida) y reemplaza al guardado; si no viene, se
        // conserva el que había — así el PUT de las otras opciones no lo pisa.
        app.MapPut("/trainings/{id:guid}/player-config", async (Guid id, PlayerConfigRequest req, ITenantContext tc, IServiceProvider sp) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");
            if (!CanAuthor(tc.Role)) return Results.Forbid();
            var t = await db.Trainings.FindAsync(id);
            if (t is null) return Results.NotFound();
            var node = JsonNode.Parse(string.IsNullOrWhiteSpace(t.PlayerConfigJson) ? "{}" : t.PlayerConfigJson)?.AsObject() ?? new JsonObject();
            node["allowBack"] = req.AllowBack;
            if (req.ReviewAfterPass is bool rap) node["reviewAfterPass"] = rap;
            if (req.ImmediateFeedback is bool ifb) node["immediateFeedback"] = ifb;
            if (req.Presentation is not null) node["presentation"] = PlayerConfig.Normalizar(req.Presentation).ToJson();
            t.PlayerConfigJson = node.ToJsonString();
            await db.SaveChangesAsync();
            return Results.Ok(new
            {
                allowBack = req.AllowBack,
                reviewAfterPass = PlayerConfig.ReviewAfterPass(t.PlayerConfigJson),
                immediateFeedback = PlayerConfig.ImmediateFeedback(t.PlayerConfigJson),
                presentation = PlayerConfig.Presentation(t.PlayerConfigJson)
            });
        }).RequireAuthorization();

        // Lo que necesita el reproductor: se resuelve desde la versión para no
        // cambiar la forma de /take, que ya está en uso.
        app.MapGet("/versions/{versionId:guid}/config", async (Guid versionId, ITenantContext tc, IServiceProvider sp) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");
            var cfg = await (from v in db.TrainingVersions
                             where v.Id == versionId
                             join t in db.Trainings on v.TrainingId equals t.Id
                             select t.PlayerConfigJson).FirstOrDefaultAsync();
            return Results.Ok(new
            {
                allowBack = PlayerConfig.AllowBack(cfg),
                immediateFeedback = PlayerConfig.ImmediateFeedback(cfg),
                presentation = PlayerConfig.Presentation(cfg)
            });
        }).RequireAuthorization();

        // ---------------- Renombrar, archivar y restaurar ----------------

        // Renombrar / editar la descripción. El título del certificado se congela al emitirse
        // (Certificate.TrainingTitle), así que renombrar no altera los ya emitidos.
        app.MapPut("/trainings/{id:guid}", async (Guid id, UpdateTrainingRequest req, ITenantContext tc, IServiceProvider sp) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");
            if (!CanAuthor(tc.Role)) return Results.Forbid();
            var t = await db.Trainings.FindAsync(id);
            if (t is null) return Results.NotFound();

            var title = (req.Title ?? "").Trim();
            if (title.Length < 3) return Results.BadRequest("El título debe tener al menos 3 caracteres.");
            t.Title = title;
            t.Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim();
            await db.SaveChangesAsync();
            return Results.Ok(new { t.Id, t.Title, t.Description, t.Status });
        }).RequireAuthorization();

        // Archivar: lo saca del catálogo del learner sin borrar nada (contenido, intentos,
        // certificados e historial quedan intactos). Doble verificación: hay que escribir el
        // título exacto, y el servidor lo valida — no basta con el confirm del navegador.
        app.MapPost("/trainings/{id:guid}/archive", async (Guid id, ConfirmTitleRequest req, ITenantContext tc, IServiceProvider sp) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");
            if (!CanAuthor(tc.Role)) return Results.Forbid();
            var t = await db.Trainings.FindAsync(id);
            if (t is null) return Results.NotFound();
            if (t.Status == "archived") return Results.Ok(new { t.Id, t.Status, alreadyArchived = true });

            if (!string.Equals((req.ConfirmTitle ?? "").Trim(), t.Title, StringComparison.OrdinalIgnoreCase))
                return Results.BadRequest("El título escrito no coincide con el del adiestramiento. No se archivó nada.");

            t.Status = "archived";
            await db.SaveChangesAsync();
            return Results.Ok(new { t.Id, t.Status });
        }).RequireAuthorization();

        // Restaurar: vuelve a "published" si tiene alguna versión publicada; si no, a borrador.
        app.MapPost("/trainings/{id:guid}/unarchive", async (Guid id, ITenantContext tc, IServiceProvider sp) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");
            if (!CanAuthor(tc.Role)) return Results.Forbid();
            var t = await db.Trainings.FindAsync(id);
            if (t is null) return Results.NotFound();
            if (t.Status != "archived") return Results.Ok(new { t.Id, t.Status });

            var tienePublicada = await db.TrainingVersions.AnyAsync(v => v.TrainingId == id && v.Status == "published");
            t.Status = tienePublicada ? "published" : "draft";
            await db.SaveChangesAsync();
            return Results.Ok(new { t.Id, t.Status });
        }).RequireAuthorization();

        // ---------------- Taking (self-paced, registered learner) ----------------
        app.MapGet("/catalog", async (ITenantContext tc, IServiceProvider sp) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");
            var cohortIds = await db.UserGroupMembers.Where(m => m.UserId == tc.UserId)
                .Select(m => m.UserGroupId).ToListAsync();
            var pending = await CatalogLogic.ResolveAsync(db, tc.UserId, cohortIds);

            // Cursos marcados como "disponibles para repaso": el learner puede volver a
            // abrirlos después de aprobados (manual del empleado, onboarding).
            var ids = pending.Select(p => p.TrainingId).ToList();
            var repaso = await db.Trainings.Where(t => ids.Contains(t.Id))
                .Select(t => new { t.Id, t.PlayerConfigJson }).ToListAsync();
            var puedeRepasar = repaso.ToDictionary(x => x.Id, x => PlayerConfig.ReviewAfterPass(x.PlayerConfigJson));

            return Results.Ok(pending.Select(p => new
            {
                trainingId = p.TrainingId, title = p.Title, versionId = p.VersionId,
                setId = p.SetId, status = p.Status, expiresAt = p.ExpiresAt,
                canReview = puedeRepasar.TryGetValue(p.TrainingId, out var r) && r
            }));
        }).RequireAuthorization();

        app.MapGet("/versions/{versionId:guid}/take", async (Guid versionId, Guid? setId, ITenantContext tc, IServiceProvider sp) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");
            var items = await db.TrainingItems.Where(i => i.TrainingVersionId == versionId && i.Active)
                .OrderBy(i => i.Order).ToListAsync();
            if (setId is not null)
            {
                var excluded = await db.TrainingSetExclusions.Where(e => e.SetId == setId).Select(e => e.StableKey).ToListAsync();
                if (excluded.Count > 0) items = items.Where(i => !excluded.Contains(i.StableKey)).ToList();
            }
            var safe = items.Select(i => new
            {
                i.Id, i.Type, i.Order, i.Points, i.Required,
                payloadJson = Sanitize(i.Type, i.PayloadJson)
            });
            return Results.Ok(safe);
        }).RequireAuthorization();

        // Author preview: same shape as /take but no attempt and author-only (works on draft too).
        app.MapGet("/versions/{versionId:guid}/preview", async (Guid versionId, Guid? setId, ITenantContext tc, IServiceProvider sp) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");
            if (!CanAuthor(tc.Role)) return Results.Forbid();
            var items = await db.TrainingItems.Where(i => i.TrainingVersionId == versionId && i.Active)
                .OrderBy(i => i.Order).ToListAsync();
            if (setId is not null)
            {
                var excluded = await db.TrainingSetExclusions.Where(e => e.SetId == setId).Select(e => e.StableKey).ToListAsync();
                if (excluded.Count > 0) items = items.Where(i => !excluded.Contains(i.StableKey)).ToList();
            }
            var safe = items.Select(i => new
            {
                i.Id, i.Type, i.Order, i.Points, i.Required,
                payloadJson = Sanitize(i.Type, i.PayloadJson)
            });
            return Results.Ok(safe);
        }).RequireAuthorization();

        app.MapPost("/versions/{versionId:guid}/attempts", async (Guid versionId, Guid? setId, ClaimsPrincipal principal, ITenantContext tc, IServiceProvider sp) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");

            // One active attempt per (user, version). "Active" = in-progress or awaiting cancellation approval.
            var active = await db.Attempts.FirstOrDefaultAsync(a =>
                a.TrainingVersionId == versionId && a.UserId == tc.UserId &&
                (a.Status == "in-progress" || a.Status == "cancellation-requested"));
            if (active is not null && active.Status == "cancellation-requested")
                return Results.Ok(new { pendingCancellation = true });

            var attempt = active;
            if (attempt is null)
            {
                // Un curso archivado no admite intentos NUEVOS; quien ya lo tenía empezado
                // (active != null) puede terminarlo.
                var estado = await (from v in db.TrainingVersions
                                    where v.Id == versionId
                                    join t in db.Trainings on v.TrainingId equals t.Id
                                    select t.Status).FirstOrDefaultAsync();
                if (estado == "archived")
                    return Results.BadRequest("Este adiestramiento está archivado y ya no se puede tomar.");

                attempt = new Attempt
                {
                    TrainingVersionId = versionId,
                    SetId = setId,
                    UserId = tc.UserId,
                    LearnerName = principal.FindFirst("name")?.Value ?? principal.FindFirst("email")?.Value,
                    Status = "in-progress"
                };
                db.Attempts.Add(attempt);
                await db.SaveChangesAsync();
            }

            var responses = await db.ItemResponses.Where(r => r.AttemptId == attempt.Id)
                .Select(r => new { r.ItemId, r.AnswerJson, r.IsCorrect }).ToListAsync();
            return Results.Ok(new { attemptId = attempt.Id, setId = attempt.SetId, responses });
        }).RequireAuthorization();

        app.MapPost("/attempts/{attemptId:guid}/answer", async (Guid attemptId, AnswerRequest req, ITenantContext tc, IServiceProvider sp) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");
            var attempt = await db.Attempts.FindAsync(attemptId);
            if (attempt is null) return Results.NotFound();
            var item = await db.TrainingItems.FindAsync(req.ItemId);
            if (item is null || item.TrainingVersionId != attempt.TrainingVersionId)
                return Results.BadRequest("Item does not belong to this attempt.");

            var (correct, points) = Score(item, req.AnswerJson);
            var resp = await db.ItemResponses.FirstOrDefaultAsync(r => r.AttemptId == attemptId && r.ItemId == req.ItemId);
            if (resp is null)
            {
                resp = new ItemResponse { AttemptId = attemptId, ItemId = req.ItemId };
                db.ItemResponses.Add(resp);
            }
            resp.AnswerJson = req.AnswerJson;
            resp.IsCorrect = correct;
            resp.PointsAwarded = points;
            resp.NeedsGrading = item.Type == "OpenResponse" && IsGraded(item.PayloadJson);
            resp.AnsweredAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(new { isCorrect = correct, points });
        }).RequireAuthorization();

        // Latido: el reproductor lo manda mientras la pestaña está visible. Acumula tiempo real.
        app.MapPost("/attempts/{attemptId:guid}/heartbeat", async (Guid attemptId, ITenantContext tc, IServiceProvider sp) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");
            var attempt = await db.Attempts.FindAsync(attemptId);
            if (attempt is null) return Results.NotFound();
            if (attempt.Status != "in-progress")
                return Results.Ok(new { activeSeconds = attempt.ActiveSeconds });
            AccrueActive(attempt);
            await db.SaveChangesAsync();
            return Results.Ok(new { activeSeconds = attempt.ActiveSeconds });
        }).RequireAuthorization();

        app.MapPost("/attempts/{attemptId:guid}/complete", async (Guid attemptId, ITenantContext tc, IServiceProvider sp, CatalogDbContext catalog, IEmailSender email, IConfiguration cfg) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");
            var attempt = await db.Attempts.FindAsync(attemptId);
            if (attempt is null) return Results.NotFound();
            AccrueActive(attempt); // cuenta el último tramo visible antes de finalizar
            var version = await db.TrainingVersions.FindAsync(attempt.TrainingVersionId);
            var total = await SetTotalAsync(db, attempt);
            var score = await db.ItemResponses.Where(r => r.AttemptId == attemptId).SumAsync(r => r.PointsAwarded);

            attempt.Score = score;
            var pending = await db.ItemResponses.AnyAsync(r => r.AttemptId == attemptId && r.NeedsGrading);
            if (pending)
            {
                attempt.Passed = false;
                attempt.Status = "pending-grading";
                attempt.CompletedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
                return Results.Ok(new { attempt.Score, total, attempt.Passed, status = "pending-grading", pendingGrading = true, activeSeconds = attempt.ActiveSeconds });
            }
            attempt.Passed = total == 0 ? true : (score * 100 / total) >= (version?.PassPercent ?? 70);
            attempt.Status = "completed";
            attempt.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            string? certificateSerial = null;
            if (attempt.Passed)
            {
                var title = await (from v in db.TrainingVersions where v.Id == attempt.TrainingVersionId
                                   join t in db.Trainings on v.TrainingId equals t.Id select t.Title).FirstOrDefaultAsync();
                await CompletionAlert.SendAsync(catalog, email, tc.TenantId, attempt.LearnerName, title, score, total);
                var cert = await CertificateService.EnsureIssuedAsync(db, attempt);
                certificateSerial = cert?.Serial;
                // El certificado en PDF va al learner y a quien esté configurado para archivarlo.
                if (cert is not null)
                    await CertificateEndpoints.MailAsync(catalog, email, cert, tc.TenantId, cfg["App:BaseUrl"]);
            }
            return Results.Ok(new { attempt.Score, total, attempt.Passed, status = "completed", activeSeconds = attempt.ActiveSeconds, certificateSerial });
        }).RequireAuthorization();

        // ---------------- Media (upload & serve files) ----------------
        app.MapPost("/media", async (IFormFile file, ITenantContext tc, IServiceProvider sp, IWebHostEnvironment env, IConfiguration cfg) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");
            if (!CanAuthor(tc.Role)) return Results.Forbid();
            if (file is null || file.Length == 0) return Results.BadRequest("Empty file.");

            var root = Path.Combine(env.ContentRootPath, cfg["Storage:UploadsPath"] ?? "App_Data/uploads");
            Directory.CreateDirectory(Path.Combine(root, tc.TenantId!.Value.ToString()));
            var id = Guid.NewGuid();
            var rel = Path.Combine(tc.TenantId.Value.ToString(), id + Path.GetExtension(file.FileName));
            await using (var fs = File.Create(Path.Combine(root, rel)))
                await file.CopyToAsync(fs);

            var asset = new MediaAsset
            {
                Id = id,
                FileName = file.FileName,
                ContentType = string.IsNullOrEmpty(file.ContentType) ? "application/octet-stream" : file.ContentType,
                RelativePath = rel,
                Size = file.Length
            };
            db.MediaAssets.Add(asset);
            await db.SaveChangesAsync();
            return Results.Ok(new { id, url = $"/media/{id}", asset.ContentType });
        }).RequireAuthorization().DisableAntiforgery();

        app.MapGet("/media/{id:guid}", async (Guid id, ITenantContext tc, IServiceProvider sp, IWebHostEnvironment env, IConfiguration cfg) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");
            var asset = await db.MediaAssets.FindAsync(id);
            if (asset is null) return Results.NotFound();
            var root = Path.Combine(env.ContentRootPath, cfg["Storage:UploadsPath"] ?? "App_Data/uploads");
            var full = Path.Combine(root, asset.RelativePath);
            if (!File.Exists(full)) return Results.NotFound();
            return Results.File(full, asset.ContentType, enableRangeProcessing: true);
        }).RequireAuthorization();

        // ---------------- History & reporting ----------------
        app.MapGet("/me/attempts", async (ITenantContext tc, IServiceProvider sp) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");
            var rows = await (from a in db.Attempts
                              where a.UserId == tc.UserId
                              join v in db.TrainingVersions on a.TrainingVersionId equals v.Id
                              join t in db.Trainings on v.TrainingId equals t.Id
                              orderby a.StartedAt descending
                              select new { a.Id, t.Title, a.Score, a.Passed, a.Status, a.StartedAt, a.CompletedAt, a.ActiveSeconds, a.CancelRequestComment, a.CancelApprovalComment })
                             .ToListAsync();
            return Results.Ok(rows);
        }).RequireAuthorization();

        // Review a completed/own attempt: full items (with correct answers) + the learner's responses.
        app.MapGet("/attempts/{attemptId:guid}/review", async (Guid attemptId, ITenantContext tc, IServiceProvider sp) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");
            var attempt = await db.Attempts.FindAsync(attemptId);
            if (attempt is null) return Results.NotFound();
            if (attempt.UserId != tc.UserId) return Results.Forbid();

            var items = await db.TrainingItems.Where(i => i.TrainingVersionId == attempt.TrainingVersionId)
                .OrderBy(i => i.Order)
                .Select(i => new { i.Id, i.Type, i.Order, i.Points, i.PayloadJson, i.StableKey }).ToListAsync();
            if (attempt.SetId is not null)
            {
                var excl = await db.TrainingSetExclusions.Where(e => e.SetId == attempt.SetId).Select(e => e.StableKey).ToListAsync();
                if (excl.Count > 0) items = items.Where(i => !excl.Contains(i.StableKey)).ToList();
            }
            var responses = await db.ItemResponses.Where(r => r.AttemptId == attemptId)
                .Select(r => new { r.ItemId, r.AnswerJson, r.IsCorrect, r.PointsAwarded, r.GraderComment, r.NeedsGrading }).ToListAsync();
            var title = await (from v in db.TrainingVersions
                               where v.Id == attempt.TrainingVersionId
                               join t in db.Trainings on v.TrainingId equals t.Id
                               select t.Title).FirstOrDefaultAsync();
            return Results.Ok(new { title, attempt.Score, attempt.Passed, attempt.Status, attempt.ActiveSeconds, items, responses });
        }).RequireAuthorization();

        app.MapGet("/trainings/{id:guid}/attempts", async (Guid id, ITenantContext tc, IServiceProvider sp) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");
            if (!CanAuthor(tc.Role)) return Results.Forbid();
            var rows = await (from a in db.Attempts
                              join v in db.TrainingVersions on a.TrainingVersionId equals v.Id
                              where v.TrainingId == id
                              orderby a.StartedAt descending
                              select new { a.Id, a.LearnerName, a.Score, a.Passed, a.Status, a.StartedAt, a.CompletedAt, a.ActiveSeconds })
                             .ToListAsync();
            return Results.Ok(rows);
        }).RequireAuthorization();

        // Resumen para el dashboard del autor: borradores, publicados y publicados sin intentos.
        app.MapGet("/authoring/stats", async (ITenantContext tc, IServiceProvider sp) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");
            if (!CanAuthor(tc.Role)) return Results.Forbid();

            var publishedIds = await db.TrainingVersions.Where(v => v.Status == "published")
                .Select(v => v.TrainingId).Distinct().ToListAsync();
            var totalTrainings = await db.Trainings.CountAsync();
            var attemptedIds = await (from a in db.Attempts
                                      join v in db.TrainingVersions on a.TrainingVersionId equals v.Id
                                      select v.TrainingId).Distinct().ToListAsync();

            var published = publishedIds.Count;
            var drafts = totalTrainings - published;                              // sin versión publicada
            var untouched = publishedIds.Count(id => !attemptedIds.Contains(id)); // publicados que nadie ha tomado

            return Results.Ok(new { drafts, published, untouched });
        }).RequireAuthorization();

        // Configurar la recurrencia/caducidad de un adiestramiento.
        app.MapPost("/trainings/{id:guid}/recurrence", async (Guid id, RecurrenceRequest req, ITenantContext tc, IServiceProvider sp) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");
            if (!CanAuthor(tc.Role)) return Results.Forbid();
            var t = await db.Trainings.FindAsync(id);
            if (t is null) return Results.NotFound();
            if (req.RecurrenceMonths is < 1) return Results.BadRequest("Los meses deben ser 1 o más (o vacío para 'una sola vez').");
            t.RecurrenceMonths = req.RecurrenceMonths;                 // null = una sola vez
            t.ExpiresOn = req.ExpiresOn?.Date;                         // null = sin fecha fija
            t.RenewLeadDays = req.RenewLeadDays is null ? 30 : Math.Max(0, req.RenewLeadDays.Value);
            await db.SaveChangesAsync();
            return Results.Ok(new { t.RecurrenceMonths, t.ExpiresOn, t.RenewLeadDays });
        }).RequireAuthorization();

        // ---------------- Avisos por curso ----------------
        app.MapGet("/trainings/{id:guid}/notification-config", async (Guid id, ITenantContext tc, IServiceProvider sp) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");
            if (!CanAuthor(tc.Role)) return Results.Forbid();
            var t = await db.Trainings.FindAsync(id);
            if (t is null) return Results.NotFound();
            var cfg = NotificationConfig.Parse(t.NotificationConfigJson);
            return Results.Ok(new { cfg.Enabled, cfg.OnOpen, cfg.DaysBefore });
        }).RequireAuthorization();

        app.MapPut("/trainings/{id:guid}/notification-config", async (Guid id, NotificationConfig req, ITenantContext tc, IServiceProvider sp) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");
            if (!CanAuthor(tc.Role)) return Results.Forbid();
            var t = await db.Trainings.FindAsync(id);
            if (t is null) return Results.NotFound();
            req.DaysBefore = (req.DaysBefore ?? new List<int>())
                .Where(d => d is > 0 and <= 365).Distinct().OrderByDescending(d => d).ToList();
            t.NotificationConfigJson = req.ToJson();
            await db.SaveChangesAsync();
            return Results.Ok(new { req.Enabled, req.OnOpen, req.DaysBefore });
        }).RequireAuthorization();

        // ---------------- Expediente de certificaciones ----------------
        // Certificaciones tomadas fuera de la plataforma, registradas por el autor.
        app.MapPost("/certifications", async (ExternalCertRequest req, ITenantContext tc, IServiceProvider sp) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");
            if (!CanAuthor(tc.Role)) return Results.Forbid();
            if (string.IsNullOrWhiteSpace(req.Title)) return Results.BadRequest("Pon el nombre de la certificación.");
            if (req.UserId == Guid.Empty) return Results.BadRequest("Falta el empleado.");
            if (req.MediaAssetId is null && string.IsNullOrWhiteSpace(req.ExternalUrl))
                return Results.BadRequest("Sube el documento o enlaza el que está en el otro sistema.");
            if (req.ExpiresOn is DateTime e && e.Date < req.IssuedOn.Date)
                return Results.BadRequest("La fecha de vencimiento no puede ser anterior a la de emisión.");

            var cert = new ExternalCertification
            {
                UserId = req.UserId,
                Title = req.Title.Trim(),
                Issuer = req.Issuer?.Trim(),
                CredentialId = req.CredentialId?.Trim(),
                IssuedOn = req.IssuedOn.Date,
                ExpiresOn = req.ExpiresOn?.Date,
                MediaAssetId = req.MediaAssetId,
                ExternalSource = req.ExternalSource?.Trim(),
                ExternalRef = req.ExternalRef?.Trim(),
                ExternalUrl = req.ExternalUrl?.Trim(),
                Notes = req.Notes?.Trim(),
                CreatedByUserId = tc.UserId
            };
            db.ExternalCertifications.Add(cert);
            await db.SaveChangesAsync();
            return Results.Ok(cert);
        }).RequireAuthorization();

        app.MapPut("/certifications/{certId:guid}", async (Guid certId, ExternalCertRequest req, ITenantContext tc, IServiceProvider sp) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");
            if (!CanAuthor(tc.Role)) return Results.Forbid();
            var cert = await db.ExternalCertifications.FindAsync(certId);
            if (cert is null) return Results.NotFound();
            cert.Title = req.Title.Trim();
            cert.Issuer = req.Issuer?.Trim();
            cert.CredentialId = req.CredentialId?.Trim();
            cert.IssuedOn = req.IssuedOn.Date;
            cert.ExpiresOn = req.ExpiresOn?.Date;
            if (req.MediaAssetId is not null) cert.MediaAssetId = req.MediaAssetId;
            cert.ExternalSource = req.ExternalSource?.Trim();
            cert.ExternalRef = req.ExternalRef?.Trim();
            cert.ExternalUrl = req.ExternalUrl?.Trim();
            cert.Notes = req.Notes?.Trim();
            await db.SaveChangesAsync();
            return Results.Ok(cert);
        }).RequireAuthorization();

        app.MapDelete("/certifications/{certId:guid}", async (Guid certId, ITenantContext tc, IServiceProvider sp) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");
            if (!CanAuthor(tc.Role)) return Results.Forbid();
            var cert = await db.ExternalCertifications.FindAsync(certId);
            if (cert is null) return Results.NotFound();
            db.ExternalCertifications.Remove(cert);
            await db.SaveChangesAsync();
            return Results.Ok();
        }).RequireAuthorization();

        // Expediente completo de una persona: lo tomado en la plataforma y lo de fuera,
        // en una sola lista ordenada. El learner ve el suyo; el autor ve el de cualquiera.
        app.MapGet("/record/{userId:guid}", async (Guid userId, ITenantContext tc, IServiceProvider sp) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");
            if (userId != tc.UserId && !CanAuthor(tc.Role)) return Results.Forbid();

            var internos = await (from c in db.Certificates
                                  where c.UserId == userId
                                  select new
                                  {
                                      origen = "plataforma",
                                      id = c.Id,
                                      title = c.TrainingTitle,
                                      issuer = (string?)null,
                                      credentialId = c.Serial,
                                      issuedOn = c.IssuedAt,
                                      expiresOn = c.ExpiresAt,
                                      documentUrl = "/certificate.html?serial=" + c.Serial,
                                      externalSource = (string?)null,
                                      notes = (string?)null
                                  }).ToListAsync();

            var externos = await (from c in db.ExternalCertifications
                                  where c.UserId == userId
                                  select new
                                  {
                                      origen = "externa",
                                      id = c.Id,
                                      title = c.Title,
                                      issuer = c.Issuer,
                                      credentialId = c.CredentialId,
                                      issuedOn = c.IssuedOn,
                                      expiresOn = c.ExpiresOn,
                                      documentUrl = c.MediaAssetId != null ? "/media/" + c.MediaAssetId : c.ExternalUrl,
                                      externalSource = c.ExternalSource,
                                      notes = c.Notes
                                  }).ToListAsync();

            var todo = internos.Concat(externos).OrderByDescending(x => x.issuedOn).ToList();
            var hoy = DateTime.UtcNow.Date;
            return Results.Ok(todo.Select(x => new
            {
                x.origen, x.id, x.title, x.issuer, x.credentialId, x.issuedOn, x.expiresOn,
                x.documentUrl, x.externalSource, x.notes,
                vigente = x.expiresOn == null || x.expiresOn.Value.Date >= hoy
            }));
        }).RequireAuthorization();

        app.MapGet("/me/record", async (ITenantContext tc, IServiceProvider sp, HttpContext http) =>
        {
            if (tc.UserId is null) return Results.BadRequest("Sin usuario.");
            http.Response.Redirect($"/record/{tc.UserId}", permanent: false);
            return Results.Empty;
        }).RequireAuthorization();

        // ---------------- Grading (open/explanation questions) ----------------
        app.MapGet("/grading/pending", async (ITenantContext tc, IServiceProvider sp) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");
            if (!CanAuthor(tc.Role)) return Results.Forbid();
            var rows = await (from r in db.ItemResponses
                              where r.NeedsGrading
                              join a in db.Attempts on r.AttemptId equals a.Id
                              join it in db.TrainingItems on r.ItemId equals it.Id
                              join v in db.TrainingVersions on a.TrainingVersionId equals v.Id
                              join t in db.Trainings on v.TrainingId equals t.Id
                              orderby a.CompletedAt
                              select new
                              {
                                  attemptId = a.Id, itemId = r.ItemId, learnerName = a.LearnerName,
                                  title = t.Title, payloadJson = it.PayloadJson, maxPoints = it.Points,
                                  answerJson = r.AnswerJson, completedAt = a.CompletedAt
                              }).ToListAsync();
            return Results.Ok(rows);
        }).RequireAuthorization();

        app.MapPost("/attempts/{attemptId:guid}/grade", async (Guid attemptId, GradeRequest req, ClaimsPrincipal principal, ITenantContext tc, IServiceProvider sp, CatalogDbContext catalog, IEmailSender email) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");
            if (!CanAuthor(tc.Role)) return Results.Forbid();
            var resp = await db.ItemResponses.FirstOrDefaultAsync(r => r.AttemptId == attemptId && r.ItemId == req.ItemId);
            if (resp is null) return Results.NotFound();
            var item = await db.TrainingItems.FindAsync(req.ItemId);
            int max = item?.Points ?? 0;
            int pts = req.Points < 0 ? 0 : (req.Points > max ? max : req.Points);
            resp.PointsAwarded = pts;
            resp.NeedsGrading = false;
            resp.GraderComment = string.IsNullOrWhiteSpace(req.Comment) ? null : req.Comment.Trim();
            resp.GradedByUserId = tc.UserId;
            resp.GradedByName = principal.FindFirst("name")?.Value ?? principal.FindFirst("email")?.Value;
            resp.GradedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            // Finalize the attempt once nothing else is pending.
            var stillPending = await db.ItemResponses.AnyAsync(r => r.AttemptId == attemptId && r.NeedsGrading);
            if (!stillPending)
            {
                var attempt = await db.Attempts.FindAsync(attemptId);
                if (attempt is not null)
                {
                    var version = await db.TrainingVersions.FindAsync(attempt.TrainingVersionId);
                    var total = await SetTotalAsync(db, attempt);
                    var score = await db.ItemResponses.Where(r => r.AttemptId == attemptId).SumAsync(r => r.PointsAwarded);
                    attempt.Score = score;
                    attempt.Passed = total == 0 ? true : (score * 100 / total) >= (version?.PassPercent ?? 70);
                    attempt.Status = "completed";
                    await db.SaveChangesAsync();

                    if (attempt.Passed)
                    {
                        var title = await (from v in db.TrainingVersions where v.Id == attempt.TrainingVersionId
                                           join t in db.Trainings on v.TrainingId equals t.Id select t.Title).FirstOrDefaultAsync();
                        await CompletionAlert.SendAsync(catalog, email, tc.TenantId, attempt.LearnerName, title, score, total);
                        await CertificateService.EnsureIssuedAsync(db, attempt);
                    }
                }
            }
            return Results.Ok(new { graded = true, finalized = !stillPending });
        }).RequireAuthorization();

        // ---------------- Cancellations ----------------
        // Learner requests cancellation of an in-progress attempt (taken by mistake). Comment required.
        app.MapPost("/attempts/{attemptId:guid}/request-cancel", async (Guid attemptId, CommentRequest req, ITenantContext tc, IServiceProvider sp) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");
            if (string.IsNullOrWhiteSpace(req.Comment)) return Results.BadRequest("Se requiere un comentario.");
            var attempt = await db.Attempts.FindAsync(attemptId);
            if (attempt is null) return Results.NotFound();
            if (attempt.UserId != tc.UserId) return Results.Forbid();
            if (attempt.Status != "in-progress") return Results.BadRequest("Solo se puede solicitar la cancelación de un curso en progreso.");
            attempt.Status = "cancellation-requested";
            attempt.CancelRequestComment = req.Comment.Trim();
            attempt.CancelRequestedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok();
        }).RequireAuthorization();

        // Reviewer roles see pending cancellation requests.
        app.MapGet("/cancellations/pending", async (ITenantContext tc, IServiceProvider sp) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");
            if (!CanAuthor(tc.Role)) return Results.Forbid();
            var rows = await (from a in db.Attempts
                              where a.Status == "cancellation-requested"
                              join v in db.TrainingVersions on a.TrainingVersionId equals v.Id
                              join t in db.Trainings on v.TrainingId equals t.Id
                              orderby a.CancelRequestedAt
                              select new { a.Id, a.LearnerName, title = t.Title, a.CancelRequestComment, a.CancelRequestedAt })
                             .ToListAsync();
            return Results.Ok(rows);
        }).RequireAuthorization();

        // Reviewer approves the cancellation. Comment required; cannot approve your own request.
        app.MapPost("/attempts/{attemptId:guid}/approve-cancel", async (Guid attemptId, CommentRequest req, ClaimsPrincipal principal, ITenantContext tc, IServiceProvider sp) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");
            if (!CanAuthor(tc.Role)) return Results.Forbid();
            if (string.IsNullOrWhiteSpace(req.Comment)) return Results.BadRequest("Se requiere un comentario.");
            var attempt = await db.Attempts.FindAsync(attemptId);
            if (attempt is null) return Results.NotFound();
            if (attempt.Status != "cancellation-requested") return Results.BadRequest("Esta solicitud ya no está pendiente.");
            if (attempt.UserId == tc.UserId) return Results.BadRequest("No puedes aprobar tu propia solicitud de cancelación.");
            attempt.Status = "cancelled";
            attempt.CancelApprovalComment = req.Comment.Trim();
            attempt.CanceledByUserId = tc.UserId;
            attempt.CanceledByName = principal.FindFirst("name")?.Value ?? principal.FindFirst("email")?.Value;
            attempt.CanceledAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok();
        }).RequireAuthorization();

        // ============ Versiones (sets) y asignación ============

        // Usuarios del tenant (para asignar).
        app.MapGet("/tenant-users", async (ITenantContext tc, CatalogDbContext catalog) =>
        {
            if (!CanAuthor(tc.Role)) return Results.Forbid();
            if (tc.TenantId is null) return Results.BadRequest("No tenant context.");
            var users = (await CompanyUsers.OfAsync(catalog, tc.TenantId.Value))
                .OrderBy(u => u.Name).ToList();
            return Results.Ok(users);
        }).RequireAuthorization();

        // ---- Cohortes ----
        app.MapGet("/user-groups", async (ITenantContext tc, IServiceProvider sp) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");
            if (!CanAuthor(tc.Role)) return Results.Forbid();
            var groups = await db.UserGroups.OrderBy(g => g.Name).ToListAsync();
            var members = await db.UserGroupMembers.ToListAsync();
            return Results.Ok(groups.Select(g => new {
                g.Id, g.Name,
                memberIds = members.Where(m => m.UserGroupId == g.Id).Select(m => m.UserId).ToList()
            }));
        }).RequireAuthorization();

        app.MapPost("/user-groups", async (NameRequest req, ITenantContext tc, IServiceProvider sp) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");
            if (!CanAuthor(tc.Role)) return Results.Forbid();
            if (string.IsNullOrWhiteSpace(req.Name)) return Results.BadRequest("Nombre requerido.");
            var g = new UserGroup { Name = req.Name.Trim() };
            db.UserGroups.Add(g); await db.SaveChangesAsync();
            return Results.Ok(g);
        }).RequireAuthorization();

        app.MapDelete("/user-groups/{id:guid}", async (Guid id, ITenantContext tc, IServiceProvider sp) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");
            if (!CanAuthor(tc.Role)) return Results.Forbid();
            var g = await db.UserGroups.FindAsync(id);
            if (g is null) return Results.NotFound();
            db.UserGroupMembers.RemoveRange(await db.UserGroupMembers.Where(m => m.UserGroupId == id).ToListAsync());
            db.Assignments.RemoveRange(await db.Assignments.Where(a => a.TargetType == "group" && a.TargetId == id).ToListAsync());
            db.UserGroups.Remove(g); await db.SaveChangesAsync();
            return Results.Ok();
        }).RequireAuthorization();

        app.MapPost("/user-groups/{id:guid}/members", async (Guid id, UserRefRequest req, ITenantContext tc, IServiceProvider sp) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");
            if (!CanAuthor(tc.Role)) return Results.Forbid();
            if (!await db.UserGroups.AnyAsync(g => g.Id == id)) return Results.NotFound();
            if (!await db.UserGroupMembers.AnyAsync(m => m.UserGroupId == id && m.UserId == req.UserId))
            { db.UserGroupMembers.Add(new UserGroupMember { UserGroupId = id, UserId = req.UserId }); await db.SaveChangesAsync(); }
            return Results.Ok();
        }).RequireAuthorization();

        app.MapDelete("/user-groups/{id:guid}/members/{userId:guid}", async (Guid id, Guid userId, ITenantContext tc, IServiceProvider sp) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");
            if (!CanAuthor(tc.Role)) return Results.Forbid();
            var m = await db.UserGroupMembers.FirstOrDefaultAsync(x => x.UserGroupId == id && x.UserId == userId);
            if (m is not null) { db.UserGroupMembers.Remove(m); await db.SaveChangesAsync(); }
            return Results.Ok();
        }).RequireAuthorization();

        // ---- Sets (versiones de un maestro) ----
        app.MapGet("/trainings/{id:guid}/sets", async (Guid id, ITenantContext tc, IServiceProvider sp) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");
            if (!CanAuthor(tc.Role)) return Results.Forbid();
            if (!await db.Trainings.AnyAsync(t => t.Id == id)) return Results.NotFound();

            var srcVersion = await db.TrainingVersions.Where(v => v.TrainingId == id && v.Status == "published")
                                 .OrderByDescending(v => v.VersionNumber).FirstOrDefaultAsync()
                          ?? await db.TrainingVersions.Where(v => v.TrainingId == id)
                                 .OrderByDescending(v => v.VersionNumber).FirstOrDefaultAsync();

            object items = Array.Empty<object>();
            if (srcVersion is not null)
                items = await db.TrainingItems.Where(i => i.TrainingVersionId == srcVersion.Id)
                    .OrderBy(i => i.Order)
                    .Select(i => new { i.StableKey, i.Type, i.PayloadJson, i.Points, i.Active })
                    .ToListAsync();

            var sets = await db.TrainingSets.Where(s => s.TrainingId == id).OrderBy(s => s.CreatedAt).ToListAsync();
            var setIds = sets.Select(s => s.Id).ToList();
            var exclusions = await db.TrainingSetExclusions.Where(e => setIds.Contains(e.SetId)).ToListAsync();
            var setResult = sets.Select(s => new {
                s.Id, s.Name, s.IsDefault,
                excluded = exclusions.Where(e => e.SetId == s.Id).Select(e => e.StableKey).ToList()
            });
            return Results.Ok(new { published = srcVersion != null && srcVersion.Status == "published", items, sets = setResult });
        }).RequireAuthorization();

        app.MapPost("/trainings/{id:guid}/sets", async (Guid id, NameRequest req, ITenantContext tc, IServiceProvider sp) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");
            if (!CanAuthor(tc.Role)) return Results.Forbid();
            if (!await db.Trainings.AnyAsync(t => t.Id == id)) return Results.NotFound();
            var first = !await db.TrainingSets.AnyAsync(s => s.TrainingId == id);
            var s = new TrainingSet { TrainingId = id, Name = string.IsNullOrWhiteSpace(req.Name) ? "Set" : req.Name.Trim(), IsDefault = first };
            db.TrainingSets.Add(s); await db.SaveChangesAsync();
            return Results.Ok(s);
        }).RequireAuthorization();

        app.MapPost("/sets/{setId:guid}/rename", async (Guid setId, NameRequest req, ITenantContext tc, IServiceProvider sp) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");
            if (!CanAuthor(tc.Role)) return Results.Forbid();
            var s = await db.TrainingSets.FindAsync(setId);
            if (s is null) return Results.NotFound();
            if (!string.IsNullOrWhiteSpace(req.Name)) { s.Name = req.Name.Trim(); await db.SaveChangesAsync(); }
            return Results.Ok();
        }).RequireAuthorization();

        app.MapPost("/sets/{setId:guid}/default", async (Guid setId, ITenantContext tc, IServiceProvider sp) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");
            if (!CanAuthor(tc.Role)) return Results.Forbid();
            var s = await db.TrainingSets.FindAsync(setId);
            if (s is null) return Results.NotFound();
            foreach (var other in await db.TrainingSets.Where(x => x.TrainingId == s.TrainingId).ToListAsync())
                other.IsDefault = other.Id == setId;
            await db.SaveChangesAsync();
            return Results.Ok();
        }).RequireAuthorization();

        app.MapDelete("/sets/{setId:guid}", async (Guid setId, ITenantContext tc, IServiceProvider sp) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");
            if (!CanAuthor(tc.Role)) return Results.Forbid();
            var s = await db.TrainingSets.FindAsync(setId);
            if (s is null) return Results.NotFound();
            var siblings = await db.TrainingSets.Where(x => x.TrainingId == s.TrainingId).ToListAsync();
            if (siblings.Count <= 1) return Results.BadRequest("Debe quedar al menos un set.");
            db.TrainingSetExclusions.RemoveRange(await db.TrainingSetExclusions.Where(e => e.SetId == setId).ToListAsync());
            db.Assignments.RemoveRange(await db.Assignments.Where(a => a.SetId == setId).ToListAsync());
            db.TrainingSets.Remove(s);
            if (s.IsDefault) siblings.First(x => x.Id != setId).IsDefault = true; // promover otro
            await db.SaveChangesAsync();
            return Results.Ok();
        }).RequireAuthorization();

        // Marcar/desmarcar un ítem en un set. included=false => se excluye.
        app.MapPost("/sets/{setId:guid}/item", async (Guid setId, SetItemRequest req, ITenantContext tc, IServiceProvider sp) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");
            if (!CanAuthor(tc.Role)) return Results.Forbid();
            if (!await db.TrainingSets.AnyAsync(s => s.Id == setId)) return Results.NotFound();
            var ex = await db.TrainingSetExclusions.FirstOrDefaultAsync(e => e.SetId == setId && e.StableKey == req.StableKey);
            if (req.Included) { if (ex is not null) db.TrainingSetExclusions.Remove(ex); }
            else { if (ex is null) db.TrainingSetExclusions.Add(new TrainingSetExclusion { SetId = setId, StableKey = req.StableKey }); }
            await db.SaveChangesAsync();
            return Results.Ok();
        }).RequireAuthorization();

        // ---- Asignaciones (usuario/cohorte -> set) ----
        app.MapGet("/trainings/{id:guid}/assignments", async (Guid id, ITenantContext tc, IServiceProvider sp) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");
            if (!CanAuthor(tc.Role)) return Results.Forbid();
            var asg = await db.Assignments.Where(a => a.TrainingId == id).OrderByDescending(a => a.CreatedAt).ToListAsync();
            return Results.Ok(asg);
        }).RequireAuthorization();

        app.MapPost("/trainings/{id:guid}/assignments", async (Guid id, AssignRequest req, ITenantContext tc, IServiceProvider sp) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");
            if (!CanAuthor(tc.Role)) return Results.Forbid();
            if (req.TargetType is not ("user" or "group")) return Results.BadRequest("targetType inválido.");
            var set = await db.TrainingSets.FirstOrDefaultAsync(s => s.Id == req.SetId && s.TrainingId == id);
            if (set is null) return Results.BadRequest("El set no pertenece a este adiestramiento.");
            var existing = await db.Assignments.FirstOrDefaultAsync(a => a.TrainingId == id && a.TargetType == req.TargetType && a.TargetId == req.TargetId);
            if (existing is not null) existing.SetId = req.SetId;
            else db.Assignments.Add(new Assignment { TrainingId = id, SetId = req.SetId, TargetType = req.TargetType, TargetId = req.TargetId });
            await db.SaveChangesAsync();
            return Results.Ok();
        }).RequireAuthorization();

        app.MapDelete("/assignments/{assignmentId:guid}", async (Guid assignmentId, ITenantContext tc, IServiceProvider sp) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");
            if (!CanAuthor(tc.Role)) return Results.Forbid();
            var a = await db.Assignments.FindAsync(assignmentId);
            if (a is not null) { db.Assignments.Remove(a); await db.SaveChangesAsync(); }
            return Results.Ok();
        }).RequireAuthorization();

    }

    // ---------------- helpers ----------------

    // Tope por hueco: si entre dos latidos pasaron más de esto, la salida no cuenta (5 min).
    private const int HeartbeatGapCapSeconds = 300;

    // Acumula el tiempo activo: suma el delta desde el último latido solo si es ≤ tope.
    private static void AccrueActive(Attempt a)
    {
        var now = DateTime.UtcNow;
        if (a.LastHeartbeatAt is not null)
        {
            var d = (now - a.LastHeartbeatAt.Value).TotalSeconds;
            if (d > 0 && d <= HeartbeatGapCapSeconds) a.ActiveSeconds += (int)Math.Round(d);
        }
        a.LastHeartbeatAt = now;
    }

    // Total de puntos de los ítems que el intento realmente recibió (activos y,
    // si tomó un set, excluyendo los ítems desmarcados de ese set).
    private static async Task<int> SetTotalAsync(TenantDbContext db, Attempt attempt)
    {
        var items = await db.TrainingItems
            .Where(i => i.TrainingVersionId == attempt.TrainingVersionId && i.Active)
            .Select(i => new { i.Points, i.StableKey }).ToListAsync();
        if (attempt.SetId is not null)
        {
            var excl = await db.TrainingSetExclusions.Where(e => e.SetId == attempt.SetId)
                .Select(e => e.StableKey).ToListAsync();
            if (excl.Count > 0) items = items.Where(i => !excl.Contains(i.StableKey)).ToList();
        }
        return items.Sum(i => i.Points);
    }

    private static async Task<TrainingVersion?> GetOrCreateDraftAsync(TenantDbContext db, Guid trainingId)
    {
        var training = await db.Trainings.FindAsync(trainingId);
        if (training is null) return null;

        var draft = await db.TrainingVersions
            .Where(v => v.TrainingId == trainingId && v.Status == "draft")
            .OrderByDescending(v => v.VersionNumber).FirstOrDefaultAsync();
        if (draft is not null) return draft;

        var latestPublished = await db.TrainingVersions
            .Where(v => v.TrainingId == trainingId && v.Status == "published")
            .OrderByDescending(v => v.VersionNumber).FirstOrDefaultAsync();

        var nextNumber = (await db.TrainingVersions.Where(v => v.TrainingId == trainingId)
            .MaxAsync(v => (int?)v.VersionNumber) ?? 0) + 1;

        draft = new TrainingVersion { TrainingId = trainingId, VersionNumber = nextNumber, Status = "draft" };
        db.TrainingVersions.Add(draft);
        await db.SaveChangesAsync();

        if (latestPublished is not null)
        {
            var prev = await db.TrainingItems.Where(i => i.TrainingVersionId == latestPublished.Id)
                .OrderBy(i => i.Order).ToListAsync();
            foreach (var p in prev)
                db.TrainingItems.Add(new TrainingItem
                {
                    TrainingVersionId = draft.Id, Order = p.Order, Type = p.Type,
                    PayloadJson = p.PayloadJson, Points = p.Points, Required = p.Required,
                    Active = p.Active, StableKey = p.StableKey
                });
            await db.SaveChangesAsync();
        }
        return draft;
    }

    // Hide the correct answer when serving an item to a learner.
    private static string Sanitize(string type, string payloadJson)
    {
        try
        {
            var node = JsonNode.Parse(payloadJson)?.AsObject();
            if (node is null) return payloadJson;
            if (type == "MultipleChoice" || type == "MultiSelect")
            {
                node.Remove("correctOptionId");
                node.Remove("correctOptionIds");
                return node.ToJsonString();
            }
            if (type == "Matching")
            {
                if (node["prompts"] is JsonArray prompts)
                    foreach (var pr in prompts)
                        (pr as JsonObject)?.Remove("correctChoiceId");
                return node.ToJsonString();
            }
            return payloadJson;
        }
        catch { return payloadJson; }
    }

    private static bool IsGraded(string payloadJson)
    {
        try { return JsonNode.Parse(payloadJson)?.AsObject()?["graded"]?.GetValue<bool>() ?? false; }
        catch { return false; }
    }

    // Server-side scoring (never trust the client).
    private static (bool? correct, int points) Score(TrainingItem item, string answerJson)
    {
        try
        {
            var p = JsonNode.Parse(item.PayloadJson)?.AsObject();
            var a = JsonNode.Parse(answerJson)?.AsObject();

            if (item.Type == "MultipleChoice")
            {
                var correctId = p?["correctOptionId"]?.GetValue<string>();
                var sel = a?["selectedOptionId"]?.GetValue<string>();
                bool ok = correctId is not null && sel is not null && correctId == sel;
                return (ok, ok ? item.Points : 0);
            }

            if (item.Type == "MultiSelect")
            {
                var correct = (p?["correctOptionIds"] as JsonArray)?
                    .Select(n => n!.GetValue<string>()).OrderBy(x => x).ToList() ?? new List<string>();
                var sel = (a?["selectedOptionIds"] as JsonArray)?
                    .Select(n => n!.GetValue<string>()).OrderBy(x => x).ToList() ?? new List<string>();
                bool ok = correct.Count > 0 && correct.SequenceEqual(sel); // all-or-nothing
                return (ok, ok ? item.Points : 0);
            }

            if (item.Type == "Matching")
            {
                var prompts = p?["prompts"] as JsonArray;
                var matches = a?["matches"] as JsonObject;
                if (prompts is null || prompts.Count == 0) return (null, 0);
                int correctCount = 0;
                foreach (var pr in prompts)
                {
                    var pid = pr?["id"]?.GetValue<string>();
                    var corr = pr?["correctChoiceId"]?.GetValue<string>();
                    var selChoice = (pid is not null && matches is not null) ? matches[pid]?.GetValue<string>() : null;
                    if (corr is not null && selChoice is not null && corr == selChoice) correctCount++;
                }
                int totalPrompts = prompts.Count;
                int pts = (item.Points * correctCount + totalPrompts / 2) / totalPrompts; // partial credit, rounded
                return (correctCount == totalPrompts, pts);
            }

            return (null, 0); // Info, ModuleHeader, etc. are not scored
        }
        catch { return (null, 0); }
    }
}

public record AddItemRequest(string Type, string PayloadJson, int Points, bool Required, bool? Active = true, Guid? AfterItemId = null);
public record UpdateTrainingRequest(string Title, string? Description);
public record ConfirmTitleRequest(string ConfirmTitle);
public record PlayerConfigRequest(bool AllowBack, bool? ReviewAfterPass = null, bool? ImmediateFeedback = null,
    PresentationConfig? Presentation = null);

// Modo presentación: el reproductor dibuja cada página en un escenario 16:9 (1280×720)
// escalado a la ventana, con transición entre láminas, en vez de la página con scroll.
// Apagado por defecto: sin `enabled` el reproductor se comporta exactamente como antes.
public record PresentationConfig(bool Enabled = false, PresentationTheme? Theme = null, string? Transition = null)
{
    public const string TransicionPorDefecto = "cover";
    public static readonly string[] Transiciones = { "cover", "fade", "none" };

    public JsonObject ToJson() => new()
    {
        ["enabled"] = Enabled,
        ["theme"] = (Theme ?? new PresentationTheme()).ToJson(),
        ["transition"] = Transition ?? TransicionPorDefecto
    };
}

// bg: fondo del escenario; accent: color de líneas y marcos; panel: si las láminas
// `split` llevan el panel diagonal oscuro; panelTitle: texto en mayúsculas de ese panel
// (vacío = el título del curso).
public record PresentationTheme(string? Bg = null, string? Accent = null, bool Panel = true, string? PanelTitle = null)
{
    public const string BgPorDefecto = "#0d0d0d";
    public const string AccentPorDefecto = "#f97316";

    public JsonObject ToJson() => new()
    {
        ["bg"] = Bg ?? BgPorDefecto,
        ["accent"] = Accent ?? AccentPorDefecto,
        ["panel"] = Panel,
        ["panelTitle"] = PanelTitle ?? ""
    };
}

// Lectura tolerante del blob de opciones del reproductor: si falta o está corrupto,
// se comporta como antes (se puede volver atrás, y no queda disponible para repaso).
public static class PlayerConfig
{
    // Bloque `presentation` ya normalizado (siempre completo, con valores por defecto).
    public static PresentationConfig Presentation(string? json)
    {
        try
        {
            var node = JsonNode.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json!)?["presentation"];
            if (node is null) return Normalizar(null);
            var cfg = System.Text.Json.JsonSerializer.Deserialize<PresentationConfig>(node.ToJsonString(),
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return Normalizar(cfg);
        }
        catch { return Normalizar(null); }
    }

    // Rellena lo que falte y descarta lo inválido: colores que no sean #rgb/#rrggbb y
    // transiciones desconocidas vuelven al valor por defecto; el título del panel se
    // recorta a 120 caracteres.
    public static PresentationConfig Normalizar(PresentationConfig? cfg)
    {
        cfg ??= new PresentationConfig();
        var th = cfg.Theme ?? new PresentationTheme();
        var transition = (cfg.Transition ?? "").Trim().ToLowerInvariant();
        if (!PresentationConfig.Transiciones.Contains(transition)) transition = PresentationConfig.TransicionPorDefecto;
        return new PresentationConfig(
            cfg.Enabled,
            new PresentationTheme(
                Color(th.Bg) ?? PresentationTheme.BgPorDefecto,
                Color(th.Accent) ?? PresentationTheme.AccentPorDefecto,
                th.Panel,
                (th.PanelTitle ?? "").Trim() is { Length: > 0 } pt ? (pt.Length > 120 ? pt[..120] : pt) : ""),
            transition);
    }

    private static string? Color(string? s)
    {
        s = (s ?? "").Trim().ToLowerInvariant();
        if (s.Length is not (4 or 7) || s[0] != '#') return null;
        return s.Skip(1).All(Uri.IsHexDigit) ? s : null;
    }

    public static bool AllowBack(string? json) => Leer(json, "allowBack", true);

    // Manual del empleado, onboarding: una vez aprobado, el learner puede volver a
    // abrirlo cuando quiera, sin intento nuevo y sin afectar su historial.
    public static bool ReviewAfterPass(string? json) => Leer(json, "reviewAfterPass", false);

    // Si el learner ve al instante si acertó o falló cada pregunta (en vez de solo al
    // terminar el intento). Por defecto apagado: para cursos de cumplimiento legal es
    // mejor calificar solo al final — combinado con allowBack, el feedback inmediato
    // permitiría retroceder, ver que falló y corregir, sin haber sabido la respuesta la
    // primera vez, lo cual le resta valor como evidencia de que aprendió el contenido.
    // wwwroot/player.html bloquea la respuesta ya revelada para que no se pueda cambiar
    // después de verla, incluso si allowBack está encendido.
    public static bool ImmediateFeedback(string? json) => Leer(json, "immediateFeedback", false);

    private static bool Leer(string? json, string clave, bool porDefecto)
    {
        try { return JsonNode.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json!)?[clave]?.GetValue<bool>() ?? porDefecto; }
        catch { return porDefecto; }
    }
}
public record ActiveRequest(bool Active);
public record MoveRequest(string Direction);
public record NameRequest(string Name);
public record UserRefRequest(Guid UserId);
public record SetItemRequest(Guid StableKey, bool Included);
public record AssignRequest(Guid SetId, string TargetType, Guid TargetId);
public record RecurrenceRequest(int? RecurrenceMonths, int? RenewLeadDays, DateTime? ExpiresOn = null);
public record ExternalCertRequest(Guid UserId, string Title, string? Issuer, string? CredentialId,
    DateTime IssuedOn, DateTime? ExpiresOn, Guid? MediaAssetId, string? ExternalSource, string? ExternalRef,
    string? ExternalUrl, string? Notes);
public record AnswerRequest(Guid ItemId, string AnswerJson);
public record CommentRequest(string Comment);
public record GradeRequest(Guid ItemId, int Points, string? Comment);
