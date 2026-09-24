using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace TrainingPlatform.TenantData;

public class Category
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public Guid? ParentId { get; set; } // optional nesting
}

public class Training
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public Guid? CategoryId { get; set; }
    public string Status { get; set; } = "draft"; // draft | published | archived
    public Guid? CreatedByUserId { get; set; }
    // Vigencia. Tres modos, según qué esté puesto:
    //   ambos null                  -> una sola vez, no caduca
    //   RecurrenceMonths            -> vence N meses después de que CADA persona lo aprobó
    //   ExpiresOn                   -> vence ese día para todos, sin importar cuándo lo tomaron
    //   ExpiresOn + RecurrenceMonths-> fecha fija que rueda: 31/dic + 12 meses = 31/dic del año siguiente
    public int? RecurrenceMonths { get; set; }
    public DateTime? ExpiresOn { get; set; }
    public int RenewLeadDays { get; set; } = 30;   // días antes del vencimiento en que reabre para renovar

    // A quién se le exige el curso:
    //   everyone -> a todos los usuarios de la compañía (como siempre)
    //   groups   -> sólo a quienes pertenecen a un grupo que lo tiene en su plan (GroupCourse)
    public string Audience { get; set; } = "everyone";
    // Sólo para `everyone`: plazo en días desde que la persona ingresó a la compañía
    // (UserCompany.CreatedAt) para completarlo. null = sin fecha límite.
    public int? OnboardingDays { get; set; }

    // Avisos por correo de este curso: { "enabled": true, "onOpen": true, "daysBefore": [15,5] }
    public string NotificationConfigJson { get; set; } = "{}";
    // Plantilla del certificado ESPECÍFICA de este curso: firmante, entidad emisora, leyenda,
    // logo y qué mostrar. La estructura visual (marco, sello, folio) es común a todos los cursos;
    // esto solo sobrescribe los campos configurables. Ver CertificateConfig en Certificates.cs.
    public string CertificateConfigJson { get; set; } = "{}";
    // Comportamiento del reproductor para este curso. Hoy: { "allowBack": true }.
    // Un solo blob para no pedir una migración por cada opción nueva.
    public string PlayerConfigJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Certificación tomada FUERA de la plataforma (OSHA, CPR, licencias, cursos
// presenciales) que el autor del cliente sube al expediente del empleado.
// El documento puede vivir aquí (MediaAssetId) o en otro sistema como Tempox
// (ExternalSource + ExternalRef + ExternalUrl), sin duplicar el archivo.
public class ExternalCertification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }                  // AppUser.Id (catálogo)
    public string Title { get; set; } = "";
    public string? Issuer { get; set; }               // quién la emitió
    public string? CredentialId { get; set; }         // folio o número de la certificación
    public DateTime IssuedOn { get; set; }
    public DateTime? ExpiresOn { get; set; }          // null = no caduca
    public Guid? MediaAssetId { get; set; }           // documento subido a la plataforma
    public string? ExternalSource { get; set; }       // p. ej. "tempox"
    public string? ExternalRef { get; set; }          // id del documento en ese sistema
    public string? ExternalUrl { get; set; }          // enlace para verlo allá
    public string? Notes { get; set; }
    public Guid? CreatedByUserId { get; set; }        // autor que la registró
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Recordatorios ya enviados. Existe para no repetir el mismo aviso: la clave real
// es (UserId, TrainingId, Kind), donde Kind lleva la versión o la fecha de
// vencimiento incrustada para que un ciclo nuevo sí vuelva a avisar.
public class NotificationLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid TrainingId { get; set; }
    public string Kind { get; set; } = "";   // open:{versionId} | due15:{yyyyMMdd} | due5:{yyyyMMdd}
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}

// Immutable published snapshot. Attempts reference a version so history never changes.
public class TrainingVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TrainingId { get; set; }
    public int VersionNumber { get; set; }
    public string Status { get; set; } = "draft"; // draft | published
    public int PassPercent { get; set; } = 70;
    public string ConfigJson { get; set; } = "{}"; // delivery mode, identity, scoring, etc.
    public DateTime? PublishedAt { get; set; }
}

public class TrainingItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TrainingVersionId { get; set; }
    public int Order { get; set; }
    public string Type { get; set; } = "Info"; // Info | MultipleChoice | RiskScenario | Poll | FreeText
    public string PayloadJson { get; set; } = "{}";
    public int Points { get; set; }
    public bool Required { get; set; } = true;
    public bool Active { get; set; } = true;
    public Guid StableKey { get; set; } = Guid.NewGuid(); // identidad estable entre versiones
}

public class Session
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TrainingVersionId { get; set; }
    public string Mode { get; set; } = "facilitated"; // facilitated | selfpaced
    public string? Code { get; set; }                  // room code for facilitated mode
    public Guid? ModeratorUserId { get; set; }         // null when self-paced
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
}

// The record of who took a training.
public class Attempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TrainingVersionId { get; set; }
    public Guid? SetId { get; set; }       // qué set tomó (null = contenido completo)
    public Guid? SessionId { get; set; }   // null for self-paced without a live session
    public Guid? UserId { get; set; }      // null when anonymous
    public string? LearnerName { get; set; } // denormalized display name (snapshot)
    public string? GuestToken { get; set; } // anonymous identifier
    public string? GuestName { get; set; }
    public int Score { get; set; }
    public bool Passed { get; set; }
    public string Status { get; set; } = "in-progress"; // in-progress | completed | cancellation-requested | cancelled
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public int ActiveSeconds { get; set; }            // tiempo real frente al curso (latidos con tope)
    public DateTime? LastHeartbeatAt { get; set; }    // último latido recibido

    // Cancellation workflow (both steps documented by a comment)
    public string? CancelRequestComment { get; set; }
    public DateTime? CancelRequestedAt { get; set; }
    public string? CancelApprovalComment { get; set; }
    public string? CanceledByName { get; set; }
    public Guid? CanceledByUserId { get; set; }
    public DateTime? CanceledAt { get; set; }
}

// Registro inmutable de un certificado emitido. Se crea UNA vez cuando un intento aprueba,
// con los datos "congelados" (nombre, curso, puntaje, vigencia, plantilla) tal como estaban
// al emitir, para que el documento siga siendo válido aunque el curso cambie después.
public class Certificate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Serial { get; set; } = "";           // folio legible y único (p. ej. CERT-2026-000123)
    public Guid AttemptId { get; set; }                 // intento aprobado que lo origina (1:1)
    public Guid TrainingId { get; set; }                // maestro
    public Guid TrainingVersionId { get; set; }         // versión exacta tomada
    public Guid? UserId { get; set; }                   // AppUser.Id (null si fue invitado)
    public string LearnerName { get; set; } = "";       // snapshot del nombre mostrado
    public string TrainingTitle { get; set; } = "";     // snapshot del título del curso
    public int ScorePercent { get; set; }               // % obtenido al aprobar (snapshot)
    public int PassPercent { get; set; }                // umbral de aprobación vigente (snapshot)
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }            // según recurrencia al emitir (null = sin caducidad)
    public string ConfigSnapshotJson { get; set; } = "{}"; // plantilla del curso congelada al emitir
}

public class ItemResponse
{
    public long Id { get; set; }
    public Guid AttemptId { get; set; }
    public Guid ItemId { get; set; }
    public string AnswerJson { get; set; } = "{}";
    public bool? IsCorrect { get; set; }
    public int PointsAwarded { get; set; }
    public DateTime AnsweredAt { get; set; } = DateTime.UtcNow;

    // Moderator grading (open/explanation questions marked as gradable)
    public bool NeedsGrading { get; set; }
    public string? GraderComment { get; set; }
    public string? GradedByName { get; set; }
    public Guid? GradedByUserId { get; set; }
    public DateTime? GradedAt { get; set; }
}

public class MediaAsset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "application/octet-stream";
    public string RelativePath { get; set; } = "";
    public long Size { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}

public class TenantAuditLog
{
    public long Id { get; set; }
    public string Action { get; set; } = "";
    public string? Detail { get; set; }
    public Guid? UserId { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
}

// ---- Versiones (sets) / asignación ----
// Un set: selección de ítems de un adiestramiento maestro. El usuario ve el
// nombre del maestro; el set solo decide qué ítems se le muestran.
public class TrainingSet
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TrainingId { get; set; }            // maestro
    public string Name { get; set; } = "";          // etiqueta interna para el autor
    public bool IsDefault { get; set; }             // el que ve quien no tiene asignación
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Un ítem EXCLUIDO de un set (por identidad estable). Por defecto todo se incluye;
// guardar una exclusión = el autor desmarcó ese ítem.
public class TrainingSetExclusion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SetId { get; set; }
    public Guid StableKey { get; set; }             // TrainingItem.StableKey excluido
}

// Grupo de usuarios (en la UI se llama «Grupo»). Además de decidir qué set ve
// cada quien, un grupo tiene un plan de onboarding: los cursos que se exigen a
// sus miembros y el plazo por defecto para completarlos.
public class UserGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    // Plazo por defecto (días) para completar cada curso del plan, contado desde
    // que la persona entró al grupo o desde que el curso se añadió al plan, lo
    // que sea más tarde. Cada curso del plan puede sobrescribirlo (GroupCourse.DueDays).
    public int OnboardingDays { get; set; } = 7;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class UserGroupMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserGroupId { get; set; }
    public Guid UserId { get; set; }   // AppUser.Id (catálogo)
    // Cuándo entró al grupo: desde aquí corre el plazo del plan. Las filas que ya
    // existían al añadir la columna reciben la fecha de la migración.
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}

// Un curso del plan de onboarding de un grupo: obligatorio para sus miembros,
// con plazo propio opcional (DueDays) que sobrescribe UserGroup.OnboardingDays.
public class GroupCourse
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserGroupId { get; set; }
    public Guid TrainingId { get; set; }            // maestro
    public int? DueDays { get; set; }               // null = usa el plazo del grupo
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;   // cuándo entró al plan
}

// Qué set ve un usuario o un grupo para un maestro.
public class Assignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TrainingId { get; set; }            // maestro
    public Guid SetId { get; set; }                 // set a servir
    public string TargetType { get; set; } = "user"; // "user" | "group"
    public Guid TargetId { get; set; }              // UserId o UserGroupId
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class TenantDbContext : DbContext
{
    public TenantDbContext(DbContextOptions<TenantDbContext> options) : base(options) { }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Training> Trainings => Set<Training>();
    public DbSet<TrainingVersion> TrainingVersions => Set<TrainingVersion>();
    public DbSet<TrainingItem> TrainingItems => Set<TrainingItem>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Attempt> Attempts => Set<Attempt>();
    public DbSet<Certificate> Certificates => Set<Certificate>();
    public DbSet<ItemResponse> ItemResponses => Set<ItemResponse>();
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();
    public DbSet<TenantAuditLog> AuditLogs => Set<TenantAuditLog>();
    public DbSet<TrainingSet> TrainingSets => Set<TrainingSet>();
    public DbSet<TrainingSetExclusion> TrainingSetExclusions => Set<TrainingSetExclusion>();
    public DbSet<UserGroup> UserGroups => Set<UserGroup>();
    public DbSet<UserGroupMember> UserGroupMembers => Set<UserGroupMember>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<GroupCourse> GroupCourses => Set<GroupCourse>();
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();
    public DbSet<ExternalCertification> ExternalCertifications => Set<ExternalCertification>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Nombres de tabla en singular.
        b.Entity<Category>().ToTable("Category");
        b.Entity<Training>().ToTable("Training");
        b.Entity<TrainingVersion>().ToTable("TrainingVersion");
        b.Entity<TrainingItem>().ToTable("TrainingItem");
        b.Entity<Session>().ToTable("Session");
        b.Entity<Attempt>().ToTable("Attempt");
        b.Entity<Certificate>().ToTable("Certificate");
        b.Entity<ItemResponse>().ToTable("ItemResponse");
        b.Entity<MediaAsset>().ToTable("MediaAsset");
        b.Entity<TenantAuditLog>().ToTable("AuditLog");
        b.Entity<TrainingSet>().ToTable("TrainingSet");
        b.Entity<TrainingSetExclusion>().ToTable("TrainingSetExclusion");
        b.Entity<UserGroup>().ToTable("UserGroup");
        b.Entity<UserGroupMember>().ToTable("UserGroupMember");
        b.Entity<Assignment>().ToTable("Assignment");
        b.Entity<GroupCourse>().ToTable("GroupCourse");
        b.Entity<NotificationLog>().ToTable("NotificationLog");
        b.Entity<ExternalCertification>().ToTable("ExternalCertification");

        b.Entity<Training>().HasIndex(t => t.CategoryId);
        b.Entity<Training>().Property(t => t.RenewLeadDays).HasDefaultValue(30);
        b.Entity<TrainingVersion>().HasIndex(v => new { v.TrainingId, v.VersionNumber }).IsUnique();
        b.Entity<TrainingItem>().HasIndex(i => i.TrainingVersionId);
        b.Entity<TrainingItem>().HasIndex(i => i.StableKey);
        b.Entity<TrainingItem>().Property(i => i.Active).HasDefaultValue(true);
        // Cada ítem existente recibe una identidad estable única al añadir la columna.
        b.Entity<TrainingItem>().Property(i => i.StableKey).HasDefaultValueSql("NEWID()");
        b.Entity<Attempt>().HasIndex(a => new { a.TrainingVersionId, a.UserId });
        b.Entity<Attempt>().HasIndex(a => a.SessionId);
        b.Entity<Certificate>().HasIndex(c => c.Serial).IsUnique();
        b.Entity<Certificate>().HasIndex(c => c.AttemptId).IsUnique(); // un certificado por intento
        b.Entity<Certificate>().HasIndex(c => c.UserId);
        b.Entity<ItemResponse>().HasIndex(r => r.AttemptId);
        b.Entity<Session>().HasIndex(s => s.Code);
        b.Entity<TrainingSet>().HasIndex(s => s.TrainingId);
        b.Entity<TrainingSetExclusion>().HasIndex(e => e.SetId);
        b.Entity<Training>().Property(t => t.Audience).HasDefaultValue("everyone");
        b.Entity<UserGroup>().Property(g => g.OnboardingDays).HasDefaultValue(7);
        b.Entity<UserGroupMember>().HasIndex(m => new { m.UserGroupId, m.UserId }).IsUnique();
        // Los miembros que ya existían al añadir la columna reciben la fecha de la migración.
        b.Entity<UserGroupMember>().Property(m => m.JoinedAt).HasDefaultValueSql("GETUTCDATE()");
        b.Entity<GroupCourse>().HasIndex(c => new { c.UserGroupId, c.TrainingId }).IsUnique();
        b.Entity<GroupCourse>().HasIndex(c => c.TrainingId);
        b.Entity<Assignment>().HasIndex(a => a.TrainingId);
        b.Entity<NotificationLog>().HasIndex(n => new { n.UserId, n.TrainingId, n.Kind }).IsUnique();
        b.Entity<ExternalCertification>().HasIndex(c => c.UserId);
        b.Entity<ExternalCertification>().HasIndex(c => new { c.ExternalSource, c.ExternalRef });
    }
}

// Uses a template/design connection so `dotnet ef migrations add ... --context TenantDbContext` works.
public class TenantDbContextFactory : IDesignTimeDbContextFactory<TenantDbContext>
{
    public TenantDbContext CreateDbContext(string[] args)
    {
        var cfg = new ConfigurationBuilder().AddJsonFile("appsettings.json", optional: false).Build();
        var opts = new DbContextOptionsBuilder<TenantDbContext>()
            .UseSqlServer(cfg.GetConnectionString("DesignTenant"))
            .Options;
        return new TenantDbContext(opts);
    }
}
