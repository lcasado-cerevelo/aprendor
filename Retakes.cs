using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using TrainingPlatform.Catalog;
using TrainingPlatform.Multitenancy;
using TrainingPlatform.Notifications;
using TrainingPlatform.TenantData;

namespace TrainingPlatform;

// ============================================================================
//  «Pedir que lo repita»
//  ---------------------
//  Una sola acción para dos casos, según la respuesta a «¿El adiestramiento
//  anterior sigue siendo válido?»:
//    renewal (sí)  -> renovación anticipada. El certificado sigue vigente con sus
//                     fechas; al aprobar de nuevo pasa a Reemplazado.
//    void    (no)  -> lo tomó otra persona, error, incidente. El certificado se
//                     ANULA (definitivo), sus enlaces /c/ se revocan al instante y el
//                     intento aprobado deja de contar.
//  La persona queda con una solicitud abierta (RetakeRequest) y una fecha límite; el
//  estado que ve ella, el panel de cumplimiento y los avisos salen de
//  CatalogLogic.ResolveAsync. La solicitud se cumple al aprobar (RetakeService.CumplirAsync).
//  Solo el Admin de la compañía (o el admin de plataforma dentro de ella) la pide o la
//  cancela; el oficial de cumplimiento solo la ve.
// ============================================================================

public static class RetakeService
{
    public const int DiasPorDefecto = 7;
    public const int MaxPersonas = 500;

    public static bool ModoValido(string? m) => m is "renewal" or "void";

    // Fecha límite: el día elegido se interpreta en la hora de la aplicación (App:TimeZone,
    // Puerto Rico) y vence al final de ese día allí (23:59:59 locales, guardadas en UTC). Se
    // muestra como ese día (HoraLocal en el servidor, la misma zona en el front). Las
    // solicitudes anteriores quedaron a las 23:59:59 UTC, que en Puerto Rico es el mismo día.
    public static DateTime FinDelDia(DateTime dia) => HoraLocal.FinDelDia(dia);

    public static DateTime Utc(DateTime d) => DateTime.SpecifyKind(d, DateTimeKind.Utc);

    // Plazo en días para repetir un curso y de dónde sale (se enseña en el modal):
    //   DueDays del plan de su grupo si el curso está en el plan (el más corto de sus grupos);
    //   si no, OnboardingDays de su grupo; si no, Training.OnboardingDays;
    //   si no, Training.RenewLeadDays; si nada, 7.
    public record Plazo(int Dias, string Origen);

    public static async Task<Dictionary<Guid, Plazo>> PlazosAsync(TenantDbContext db, Training t, List<Guid> userIds)
    {
        var miembros = await db.UserGroupMembers.AsNoTracking().Where(m => userIds.Contains(m.UserId))
            .Select(m => new { m.UserId, m.UserGroupId }).ToListAsync();
        var gids = miembros.Select(m => m.UserGroupId).Distinct().ToList();
        var grupos = await db.UserGroups.AsNoTracking().Where(g => gids.Contains(g.Id)).ToDictionaryAsync(g => g.Id);
        var plan = await db.GroupCourses.AsNoTracking().Where(c => c.TrainingId == t.Id && gids.Contains(c.UserGroupId)).ToListAsync();

        string Dias(int n) => n == 1 ? "1 día" : $"{n} días";
        var r = new Dictionary<Guid, Plazo>();
        foreach (var uid in userIds.Distinct())
        {
            var suyos = miembros.Where(m => m.UserId == uid && grupos.ContainsKey(m.UserGroupId))
                .Select(m => grupos[m.UserGroupId]).ToList();
            var enPlan = plan.Where(c => suyos.Any(g => g.Id == c.UserGroupId))
                .Select(c => new { Dias = Math.Max(0, c.DueDays ?? grupos[c.UserGroupId].OnboardingDays), Grupo = grupos[c.UserGroupId], Propio = c.DueDays is not null })
                .OrderBy(x => x.Dias).ThenBy(x => x.Grupo.Name).FirstOrDefault();
            if (enPlan is not null)
                r[uid] = new Plazo(enPlan.Dias, (enPlan.Propio ? "plan del grupo " : "plazo del grupo ") + $"{enPlan.Grupo.Name}: {Dias(enPlan.Dias)}");
            else if (suyos.OrderBy(g => g.OnboardingDays).ThenBy(g => g.Name).FirstOrDefault() is UserGroup g)
                r[uid] = new Plazo(Math.Max(0, g.OnboardingDays), $"plazo del grupo {g.Name}: {Dias(Math.Max(0, g.OnboardingDays))}");
            else if (t.OnboardingDays is int od)
                r[uid] = new Plazo(Math.Max(0, od), $"plazo de ingreso del curso: {Dias(Math.Max(0, od))}");
            else if (t.RenewLeadDays > 0)
                r[uid] = new Plazo(t.RenewLeadDays, $"anticipación de renovación del curso: {Dias(t.RenewLeadDays)}");
            else
                r[uid] = new Plazo(DiasPorDefecto, $"plazo por defecto: {Dias(DiasPorDefecto)}");
        }
        return r;
    }

    // La aprobación que cuenta de cada persona en el curso: la última aprobada, si no está
    // anulada (si la última se anuló, ya tiene que repetirlo y lo de antes tampoco cuenta).
    public static async Task<Dictionary<Guid, Attempt>> AprobacionesAsync(TenantDbContext db, Guid trainingId, List<Guid> userIds)
        => (await UltimasAprobacionesAsync(db, trainingId, userIds)).Values
            .Where(a => a.VoidedAt == null)
            .ToDictionary(a => a.UserId!.Value);

    // La última aprobación de cada persona en el curso, anulada o no (con VoidedAt, a quien
    // se le anuló se le dice cuándo en lugar de «todavía no lo ha aprobado»).
    public static async Task<Dictionary<Guid, Attempt>> UltimasAprobacionesAsync(TenantDbContext db, Guid trainingId, List<Guid> userIds)
    {
        var lista = await (from a in db.Attempts
                           where a.UserId != null && userIds.Contains(a.UserId.Value)
                                 && a.Passed && a.CompletedAt != null
                           join v in db.TrainingVersions on a.TrainingVersionId equals v.Id
                           where v.TrainingId == trainingId
                           select a).ToListAsync();
        return lista.GroupBy(a => a.UserId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.CompletedAt).First());
    }

    // Por qué no se le puede pedir que lo repita a quien no tiene una aprobación que cuente.
    public static string MotivoNo(Attempt? ultima, bool largo)
        => ultima?.VoidedAt is DateTime anulada
            ? $"Su aprobación fue anulada el {HoraLocal.Fecha(anulada)}: no se le puede volver a pedir que lo repita hasta que apruebe."
            : largo ? "Todavía no lo ha aprobado: no hay nada que repetir." : "Todavía no lo ha aprobado.";

    // Al aprobar (/complete y /grade): cumple la solicitud abierta de esa persona en ese
    // curso (si la pidieron antes de terminar este intento) y pasa a «Reemplazado» los
    // certificados anteriores que seguían vigentes. Sin solicitud abierta no toca nada.
    public static async Task CumplirAsync(TenantDbContext db, Attempt attempt, Certificate? nuevo)
    {
        if (!attempt.Passed || attempt.UserId is not Guid uid || attempt.VoidedAt is not null) return;
        var tId = await db.TrainingVersions.Where(v => v.Id == attempt.TrainingVersionId)
            .Select(v => v.TrainingId).FirstOrDefaultAsync();
        if (tId == Guid.Empty) return;
        var ahora = DateTime.UtcNow;
        var terminado = attempt.CompletedAt ?? ahora;

        var abiertas = await db.RetakeRequests
            .Where(r => r.UserId == uid && r.TrainingId == tId && r.FulfilledAt == null && r.CancelledAt == null
                        && r.CreatedAt <= terminado)
            .ToListAsync();
        if (abiertas.Count == 0) return;
        foreach (var r in abiertas)
        {
            r.FulfilledAt = ahora;
            r.FulfilledAttemptId = attempt.Id;
        }

        var anteriores = await db.Certificates
            .Where(c => c.UserId == uid && c.TrainingId == tId && c.Status == CertificateStatus.Valid && c.AttemptId != attempt.Id)
            .ToListAsync();
        foreach (var c in anteriores)
        {
            c.Status = CertificateStatus.Superseded;
            c.StatusChangedAt = ahora;
            c.StatusReason = nuevo?.Serial;   // folio del que lo reemplaza
            c.StatusByUserId = null;
        }

        var titulo = await db.Trainings.Where(t => t.Id == tId).Select(t => t.Title).FirstOrDefaultAsync();
        db.AuditLogs.Add(new TenantAuditLog
        {
            Action = "retake-fulfilled",
            Detail = $"{attempt.LearnerName} aprobó de nuevo «{titulo}»" +
                     (anteriores.Count > 0 ? $"; reemplazados: {string.Join(", ", anteriores.Select(c => c.Serial))}" : "") +
                     (nuevo is not null ? $"; nuevo certificado {nuevo.Serial}" : ""),
            UserId = uid
        });
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException) { /* nunca tumba la aprobación */ }
    }
}

public static class RetakeEndpoints
{
    // Admin de la compañía activa. El admin de plataforma entra a una compañía con
    // /me/switch-company y ahí lleva su rol de esa compañía.
    private static bool PuedePedir(ITenantContext tc) => tc.TenantId is not null && tc.Role == "Admin";

    private static bool CanAuthor(string? role) => role is "Admin" or "Author" or "Moderator";

    private static object? SolicitudJson(RetakeRequest? r) => r is null ? null : new
    {
        id = r.Id, mode = r.Mode, reason = r.Reason, dueAt = RetakeService.Utc(r.DueAt),
        createdAt = RetakeService.Utc(r.CreatedAt), createdByName = r.CreatedByName
    };

    // Personas pedidas: la lista de ids o los miembros del grupo. Solo las que pertenecen
    // a la compañía (las demás se devuelven aparte para decirlo).
    private static async Task<(List<CompanyUsers.Miembro> ok, List<Guid> ajenas, string? error)> PersonasAsync(
        TenantDbContext db, CatalogDbContext catalog, Guid tenantId, IEnumerable<Guid>? userIds, Guid? groupId)
    {
        var ids = new List<Guid>();
        if (groupId is Guid gid)
        {
            if (!await db.UserGroups.AnyAsync(g => g.Id == gid)) return (new(), new(), "El grupo no existe.");
            ids.AddRange(await db.UserGroupMembers.Where(m => m.UserGroupId == gid).Select(m => m.UserId).ToListAsync());
        }
        if (userIds is not null) ids.AddRange(userIds.Where(x => x != Guid.Empty));
        ids = ids.Distinct().ToList();
        if (ids.Count == 0) return (new(), new(), groupId is null ? "Indica a quién pedírselo." : "El grupo no tiene miembros.");
        if (ids.Count > RetakeService.MaxPersonas) return (new(), new(), $"Como máximo {RetakeService.MaxPersonas} personas a la vez.");

        var compañia = (await CompanyUsers.OfAsync(catalog, tenantId)).ToDictionary(u => u.Id);
        var ok = ids.Where(compañia.ContainsKey).Select(i => compañia[i]).OrderBy(u => u.Name).ToList();
        var ajenas = ids.Where(i => !compañia.ContainsKey(i)).ToList();
        return (ok, ajenas, null);
    }

    private static List<Guid> ParseIds(string? csv)
    {
        var l = new List<Guid>();
        foreach (var s in (csv ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (Guid.TryParse(s, out var g)) l.Add(g);
        return l;
    }

    // Curso que se puede pedir: existe, no está archivado y tiene una versión publicada.
    private static async Task<(Training? t, string? error)> CursoAsync(TenantDbContext db, Guid trainingId)
    {
        var t = await db.Trainings.FindAsync(trainingId);
        if (t is null) return (null, "El curso no existe.");
        if (t.Status == "archived") return (null, "El curso está archivado: ya no se puede tomar.");
        if (!await db.TrainingVersions.AnyAsync(v => v.TrainingId == trainingId && v.Status == "published"))
            return (null, "El curso no tiene una versión publicada.");
        return (t, null);
    }

    public static void MapRetakes(this WebApplication app)
    {
        // ---- Vista previa del modal: estado, certificado vigente y fecha límite calculada ----
        // GET /retakes/preview?trainingId=&userIds=a,b  (o &groupId= para todo un grupo)
        app.MapGet("/retakes/preview", async (Guid trainingId, string? userIds, Guid? groupId,
            ITenantContext tc, IServiceProvider sp, CatalogDbContext catalog) =>
        {
            if (tc.TenantId is not Guid tid) return Results.BadRequest("No tenant context.");
            if (!PuedePedir(tc)) return Results.Forbid();
            var db = sp.GetRequiredService<TenantDbContext>();
            var (t, errCurso) = await CursoAsync(db, trainingId);
            if (t is null) return Results.BadRequest(errCurso);
            var (personas, ajenas, err) = await PersonasAsync(db, catalog, tid, ParseIds(userIds), groupId);
            if (err is not null) return Results.BadRequest(err);

            var ids = personas.Select(p => p.Id).ToList();
            var plazos = await RetakeService.PlazosAsync(db, t, ids);
            var ultimas = await RetakeService.UltimasAprobacionesAsync(db, trainingId, ids);
            var certs = (await db.Certificates.AsNoTracking()
                    .Where(c => c.UserId != null && ids.Contains(c.UserId.Value) && c.TrainingId == trainingId && c.Status == CertificateStatus.Valid)
                    .ToListAsync())
                .GroupBy(c => c.UserId!.Value).ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.IssuedAt).First());
            var abiertas = (await db.RetakeRequests.AsNoTracking()
                    .Where(r => ids.Contains(r.UserId) && r.TrainingId == trainingId && r.FulfilledAt == null && r.CancelledAt == null)
                    .ToListAsync())
                .GroupBy(r => r.UserId).ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.CreatedAt).First());
            var membresias = await db.UserGroupMembers.AsNoTracking().Where(m => ids.Contains(m.UserId))
                .Select(m => new { m.UserId, m.UserGroupId }).ToListAsync();
            var ingresos = await CatalogLogic.FechasIngresoAsync(catalog, tid);

            var gente = new List<object>();
            foreach (var p in personas)
            {
                var grupos = membresias.Where(m => m.UserId == p.Id).Select(m => m.UserGroupId).Distinct().ToList();
                var estado = (await CatalogLogic.ResolveAsync(db, p.Id, grupos, ingresos.TryGetValue(p.Id, out var f) ? f : null))
                    .FirstOrDefault(x => x.TrainingId == trainingId);
                var plazo = plazos[p.Id];
                // La aprobación que cuenta: la última, si no está anulada.
                var ultima = ultimas.TryGetValue(p.Id, out var a) ? a : null;
                var aprobado = ultima?.VoidedAt is null ? ultima : null;
                certs.TryGetValue(p.Id, out var cert);
                abiertas.TryGetValue(p.Id, out var abierta);
                gente.Add(new
                {
                    userId = p.Id, name = string.IsNullOrWhiteSpace(p.Name) ? p.Email : p.Name, email = p.Email,
                    status = estado?.Status ?? "none",
                    approvedAt = aprobado?.CompletedAt is DateTime ap ? RetakeService.Utc(ap) : (DateTime?)null,
                    certificate = cert is null ? null : new
                    {
                        serial = cert.Serial, issuedAt = RetakeService.Utc(cert.IssuedAt),
                        expiresAt = cert.ExpiresAt is DateTime e ? RetakeService.Utc(e) : (DateTime?)null
                    },
                    canRequest = aprobado is not null,
                    reasonNot = aprobado is null ? RetakeService.MotivoNo(ultima, largo: true) : null,
                    voidedAt = ultima?.VoidedAt is DateTime va ? RetakeService.Utc(va) : (DateTime?)null,
                    dueAt = RetakeService.FinDelDia(HoraLocal.Hoy().AddDays(plazo.Dias)),
                    dueDays = plazo.Dias,
                    dueSource = plazo.Origen,
                    openRequest = SolicitudJson(abierta)
                });
            }

            return Results.Ok(new
            {
                trainingId = t.Id, title = t.Title,
                people = gente,
                notInCompany = ajenas.Count
            });
        }).RequireAuthorization();

        // ---- Pedir que lo repita ----
        // POST /retakes { trainingId, userIds[] | groupId, mode: renewal|void, reason?, dueAt? }
        // dueAt: un día (yyyy-MM-dd); sin él, cada persona con su plazo calculado.
        app.MapPost("/retakes", async (RetakeCreateRequest req, ITenantContext tc, ClaimsPrincipal principal,
            IServiceProvider sp, CatalogDbContext catalog, IEmailSender email, IConfiguration config) =>
        {
            if (tc.TenantId is not Guid tid) return Results.BadRequest("No tenant context.");
            if (!PuedePedir(tc)) return Results.Forbid();
            var db = sp.GetRequiredService<TenantDbContext>();

            var modo = (req.Mode ?? "").Trim().ToLowerInvariant();
            if (!RetakeService.ModoValido(modo))
                return Results.BadRequest("Responde si el adiestramiento anterior sigue siendo válido.");
            var motivo = string.IsNullOrWhiteSpace(req.Reason) ? null : req.Reason.Trim();
            if (modo == "void" && motivo is null) return Results.BadRequest("Escribe el motivo de la anulación.");
            if (motivo is not null && motivo.Length > RetakeRequest.MaxReason)
                return Results.BadRequest($"El motivo no puede pasar de {RetakeRequest.MaxReason} caracteres.");

            var ahora = DateTime.UtcNow;
            DateTime? limiteFijo = null;
            if (req.DueAt is DateTime d)
            {
                // «Hoy» es el de la aplicación (Puerto Rico): de noche allí, en UTC ya es mañana.
                if (d.Date < HoraLocal.Hoy()) return Results.BadRequest("La fecha límite tiene que ser hoy o un día futuro.");
                var fin = RetakeService.FinDelDia(d);
                if (fin > ahora.AddDays(3650)) return Results.BadRequest("La fecha límite está demasiado lejos.");
                limiteFijo = fin;
            }

            var (t, errCurso) = await CursoAsync(db, req.TrainingId);
            if (t is null) return Results.BadRequest(errCurso);
            var (personas, ajenas, err) = await PersonasAsync(db, catalog, tid, req.UserIds, req.GroupId);
            if (err is not null) return Results.BadRequest(err);
            if (ajenas.Count > 0 && req.GroupId is null)
                return Results.BadRequest("Hay personas que no pertenecen a esta compañía.");

            var ids = personas.Select(p => p.Id).ToList();
            var plazos = await RetakeService.PlazosAsync(db, t, ids);
            var ultimas = await RetakeService.UltimasAprobacionesAsync(db, t.Id, ids);
            var abiertas = await db.RetakeRequests
                .Where(r => ids.Contains(r.UserId) && r.TrainingId == t.Id && r.FulfilledAt == null && r.CancelledAt == null)
                .ToListAsync();
            var quien = principal.FindFirst("name")?.Value ?? principal.FindFirst("email")?.Value;
            if (quien is { Length: > 200 }) quien = quien[..200];

            var creadas = new List<(RetakeRequest r, CompanyUsers.Miembro p)>();
            var omitidas = new List<object>();
            var anulados = new List<Certificate>();
            foreach (var p in personas)
            {
                // Solo a quien tiene una aprobación que cuenta (la última, sin anular).
                ultimas.TryGetValue(p.Id, out var aprobado);
                if (aprobado is null || aprobado.VoidedAt is not null)
                {
                    omitidas.Add(new { userId = p.Id, name = p.Name, reason = RetakeService.MotivoNo(aprobado, largo: false) });
                    continue;
                }
                // Una abierta por persona y curso: la nueva reemplaza a la anterior.
                foreach (var vieja in abiertas.Where(r => r.UserId == p.Id))
                {
                    vieja.CancelledAt = ahora;
                    vieja.CancelledByUserId = tc.UserId;
                }
                var limite = limiteFijo ?? RetakeService.FinDelDia(HoraLocal.Hoy().AddDays(plazos[p.Id].Dias));
                var sol = new RetakeRequest
                {
                    UserId = p.Id, TrainingId = t.Id, Mode = modo, Reason = motivo, DueAt = limite,
                    CreatedByUserId = tc.UserId, CreatedByName = quien, CreatedAt = ahora
                };
                db.RetakeRequests.Add(sol);
                creadas.Add((sol, p));

                var detalle = $"{quien ?? "(admin)"} pidió repetir «{t.Title}» a {p.Name} ({p.Email}): " +
                              (modo == "void" ? "no fue válido (anulación)" : "renovación anticipada") +
                              $", fecha límite {HoraLocal.Fecha(limite)}" + (motivo is null ? "" : $", motivo: {motivo}");

                if (modo == "void")
                {
                    // El intento aprobado deja de contar y su certificado queda anulado (definitivo).
                    aprobado.VoidedAt = ahora;
                    aprobado.VoidReason = motivo;
                    // La anulación invalida también lo aprobado antes (así lo cuenta ResolveAsync):
                    // si aprobó el curso más de una vez sin solicitud, sus certificados anteriores
                    // seguían «valid». Se anulan todos los vigentes de ese curso y persona, además
                    // del del intento, para que ninguno quede descargable ni abra por /c/.
                    var certs = await db.Certificates
                        .Where(c => c.Status != CertificateStatus.Voided
                                    && (c.AttemptId == aprobado.Id
                                        || (c.UserId == p.Id && c.TrainingId == t.Id && c.Status == CertificateStatus.Valid)))
                        .ToListAsync();
                    foreach (var c in certs)
                    {
                        c.Status = CertificateStatus.Voided;
                        c.StatusChangedAt = ahora;
                        c.StatusReason = motivo;
                        c.StatusByUserId = tc.UserId;
                        anulados.Add(c);
                    }
                    // Los intentos de esos certificados anteriores también quedan anulados, para
                    // que el expediente no los muestre como aprobados vigentes.
                    var otrosIntentos = certs.Where(c => c.AttemptId != aprobado.Id)
                        .Select(c => c.AttemptId).Distinct().ToList();
                    if (otrosIntentos.Count > 0)
                        foreach (var a in await db.Attempts.Where(a => otrosIntentos.Contains(a.Id) && a.VoidedAt == null).ToListAsync())
                        {
                            a.VoidedAt = ahora;
                            a.VoidReason = motivo;
                        }
                    if (certs.Count > 0) detalle += $"; certificado anulado: {string.Join(", ", certs.Select(c => c.Serial))}";
                }
                db.AuditLogs.Add(new TenantAuditLog { Action = "retake-request", Detail = detalle, UserId = tc.UserId });
            }

            if (creadas.Count == 0)
                return Results.BadRequest(personas.Count == 1
                    ? (ultimas.TryGetValue(personas[0].Id, out var u) && u.VoidedAt is not null
                        ? RetakeService.MotivoNo(u, largo: true)
                        : "Esta persona todavía no ha aprobado el curso: no hay nada que repetir.")
                    : "Ninguna de estas personas ha aprobado el curso: no hay nada que repetir.");

            // Enlaces /c/ de los certificados anulados: revocados al instante (viven en el catálogo).
            var revocados = 0;
            if (anulados.Count > 0)
            {
                var certIds = anulados.Select(c => c.Id).ToList();
                var vivos = await catalog.CertificateLinks
                    .Where(l => l.TenantId == tid && certIds.Contains(l.CertificateId) && l.RevokedAt == null).ToListAsync();
                foreach (var l in vivos) l.RevokedAt = ahora;
                revocados = vivos.Count;
                await catalog.SaveChangesAsync();
                if (revocados > 0)
                    db.AuditLogs.Add(new TenantAuditLog
                    {
                        Action = "certificate-links-revoked",
                        Detail = $"{string.Join(", ", anulados.Select(c => c.Serial))}: {revocados} enlace(s) (certificado anulado)",
                        UserId = tc.UserId
                    });
            }
            await db.SaveChangesAsync();

            // Aviso inmediato a cada persona. Un fallo de correo no deshace la solicitud.
            var appUrl = (CertificateLinks.BaseUrl(config["App:BaseUrl"]) is string b ? b + "/" : null);
            var avisados = 0;
            foreach (var (r, p) in creadas)
            {
                if (string.IsNullOrWhiteSpace(p.Email)) continue;
                try
                {
                    var (asunto, html) = EmailTemplates.RetakeRequested(p.Name, t.Title, r.Mode, r.DueAt, r.Reason, appUrl);
                    await email.SendAsync(p.Email, p.Name, asunto, html);
                    avisados++;
                }
                catch { /* se registra igual; el recordatorio de plazo sale después */ }
            }

            return Results.Ok(new
            {
                created = creadas.Count,
                skipped = omitidas,
                voidedCertificates = anulados.Select(c => c.Serial),
                revokedLinks = revocados,
                notified = avisados,
                requests = creadas.Select(x => SolicitudJson(x.r))
            });
        }).RequireAuthorization();

        // ---- Lista de solicitudes ----
        // GET /retakes?trainingId=&userId=&open=true
        // Admin, autores, moderadores y oficiales de cumplimiento ven las de cualquiera; el
        // empleado, solo las suyas.
        app.MapGet("/retakes", async (Guid? trainingId, Guid? userId, bool? open, ITenantContext tc,
            IServiceProvider sp, CatalogDbContext catalog) =>
        {
            if (tc.TenantId is not Guid tid) return Results.BadRequest("No tenant context.");
            var db = sp.GetRequiredService<TenantDbContext>();
            var todos = CanAuthor(tc.Role) || await ComplianceAccess.EsOficialAsync(catalog, tc);
            if (!todos)
            {
                if (userId is not null && userId != tc.UserId) return Results.Forbid();
                userId = tc.UserId;
            }

            var q = db.RetakeRequests.AsNoTracking().AsQueryable();
            if (trainingId is Guid t) q = q.Where(r => r.TrainingId == t);
            if (userId is Guid u) q = q.Where(r => r.UserId == u);
            if (open == true) q = q.Where(r => r.FulfilledAt == null && r.CancelledAt == null);
            var filas = await q.OrderByDescending(r => r.CreatedAt).Take(1000).ToListAsync();

            var tids = filas.Select(r => r.TrainingId).Distinct().ToList();
            var titulos = await db.Trainings.Where(x => tids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Title);
            var nombres = (await CompanyUsers.OfAsync(catalog, tid)).ToDictionary(x => x.Id, x => string.IsNullOrWhiteSpace(x.Name) ? x.Email : x.Name);

            return Results.Ok(filas.Select(r => new
            {
                id = r.Id, userId = r.UserId, name = nombres.TryGetValue(r.UserId, out var n) ? n : "(persona)",
                trainingId = r.TrainingId, title = titulos.TryGetValue(r.TrainingId, out var ti) ? ti : "(curso)",
                mode = r.Mode, reason = r.Reason, dueAt = RetakeService.Utc(r.DueAt),
                createdAt = RetakeService.Utc(r.CreatedAt), createdByName = r.CreatedByName,
                fulfilledAt = r.FulfilledAt is DateTime fa ? RetakeService.Utc(fa) : (DateTime?)null,
                cancelledAt = r.CancelledAt is DateTime ca ? RetakeService.Utc(ca) : (DateTime?)null,
                status = r.CancelledAt is not null ? "cancelled" : r.FulfilledAt is not null ? "fulfilled" : "open",
                overdue = r.FulfilledAt == null && r.CancelledAt == null && r.DueAt <= DateTime.UtcNow
            }));
        }).RequireAuthorization();

        // ---- Cancelar una solicitud abierta ----
        // Si era una anulación, el certificado SIGUE anulado: la anulación es definitiva.
        app.MapDelete("/retakes/{id:guid}", async (Guid id, ITenantContext tc, IServiceProvider sp, CatalogDbContext catalog) =>
        {
            if (tc.TenantId is not Guid tid) return Results.BadRequest("No tenant context.");
            if (!PuedePedir(tc)) return Results.Forbid();
            var db = sp.GetRequiredService<TenantDbContext>();
            var r = await db.RetakeRequests.FindAsync(id);
            if (r is null) return Results.NotFound();
            if (r.FulfilledAt is not null) return Results.Conflict("Esta solicitud ya se cumplió: la persona aprobó de nuevo.");
            if (r.CancelledAt is not null) return Results.Ok(new { cancelled = true, mode = r.Mode, alreadyCancelled = true });

            r.CancelledAt = DateTime.UtcNow;
            r.CancelledByUserId = tc.UserId;
            var titulo = await db.Trainings.Where(t => t.Id == r.TrainingId).Select(t => t.Title).FirstOrDefaultAsync();
            var persona = (await CompanyUsers.OfAsync(catalog, tid)).FirstOrDefault(u => u.Id == r.UserId);
            db.AuditLogs.Add(new TenantAuditLog
            {
                Action = "retake-cancelled",
                Detail = $"Solicitud de repetir «{titulo}» a {persona?.Name ?? r.UserId.ToString()} cancelada " +
                         (r.Mode == "void" ? "(era anulación: el certificado sigue anulado)" : "(renovación anticipada)"),
                UserId = tc.UserId
            });
            await db.SaveChangesAsync();
            return Results.Ok(new { cancelled = true, mode = r.Mode, voidStays = r.Mode == "void" });
        }).RequireAuthorization();
    }
}

public record RetakeCreateRequest(Guid TrainingId, List<Guid>? UserIds, Guid? GroupId, string? Mode, string? Reason, DateTime? DueAt);
