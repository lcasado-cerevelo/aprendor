/* ============================================================================
   LIMPIEZA DESPUÉS DE LAS PRUEBAS — deja una compañía lista para empezar de verdad.

   Se ejecuta contra la base de la COMPAÑÍA (p. ej. TP_Advance) y toca también la del
   CATÁLOGO (usuarios, bitácora general), cuyo nombre va en la variable de SQLCMD de
   abajo. Todo va en UNA transacción: o se hace completo o no se hace nada.

   Qué hace:
   1. REINICIA EL PROGRESO DE TODOS: intentos, respuestas, certificados (y sus enlaces
      por correo), sesiones, solicitudes de «Pedir que lo repita», recordatorios
      enviados, asignaciones de sets, planes de los grupos (qué cursos tiene cada
      grupo), expedientes (certificaciones externas y sus documentos) y la bitácora de
      la compañía.
   2. BORRA POR COMPLETO A LOS USUARIOS DADOS DE BAJA de esta compañía:
      - los DESACTIVADOS cuya compañía principal es esta: la cuenta completa, con sus
        membresías, enlaces, retos, eventos de seguridad y su rastro en la bitácora
        general (lo que hicieron y lo que menciona su correo);
      - los desactivados SOLO en esta compañía (su principal es otra): su membresía
        aquí; su cuenta sigue en la otra compañía;
      - los HUÉRFANOS que dejó el «Eliminar» de antes (su cuenta ya no existe pero su
        historial seguía en esta base), con su rastro en la bitácora general.
      En todos los casos se quitan también de los grupos.

   Qué NO toca: los usuarios activos, los cursos (contenido, versiones, sets, fotos y
   voz), las categorías y los grupos con sus miembros activos.

   Cómo usarlo (SSMS):
     1. HAZ UN RESPALDO de las dos bases antes.
     2. Menú Query → «SQLCMD Mode» (tiene que quedar marcado).
     3. Revisa el nombre de la base del catálogo en :setvar (el de ConnectionStrings:Catalog
        del servidor). Conéctate a la base de la compañía.
     4. Déjalo con @Simular = 1 y ejecútalo: al final muestra cuántas filas borraría de
        cada tabla y la lista de usuarios que borraría, SIN borrar nada.
     5. Si cuadra, cambia a @Simular = 0 y vuelve a ejecutarlo.
     6. Borra a mano los archivos de expedientes que lista el resultado «Archivos» en
        App_Data\uploads del sitio (SQL no puede borrar archivos).
   ============================================================================ */

:setvar Catalogo "TP_Catalog"

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Simular bit = 1;   -- 1 = solo enseña qué borraría; 0 = borra de verdad
DECLARE @BorrarBitacoraCompania bit = 1;   -- 1 = vacía también la bitácora de la compañía

/* ---- La compañía de esta base ---- */
DECLARE @Tenant uniqueidentifier, @NombreCompania nvarchar(200), @Coincidencias int;
SELECT @Coincidencias = COUNT(*) FROM [$(Catalogo)].dbo.Tenant
 WHERE ConnectionString LIKE N'%Database=' + DB_NAME() + N';%' OR ConnectionString LIKE N'%Initial Catalog=' + DB_NAME() + N';%';
IF @Coincidencias <> 1
BEGIN
    RAISERROR('No encontré (o encontré más de una) compañía del catálogo cuya conexión apunte a esta base. ¿Estás conectado a la base de la compañía y el nombre del catálogo es correcto? No se borró nada.', 16, 1);
    RETURN;
END
SELECT @Tenant = Id, @NombreCompania = Name FROM [$(Catalogo)].dbo.Tenant
 WHERE ConnectionString LIKE N'%Database=' + DB_NAME() + N';%' OR ConnectionString LIKE N'%Initial Catalog=' + DB_NAME() + N';%';
PRINT N'Compañía: ' + @NombreCompania + N'  (' + CONVERT(nvarchar(36), @Tenant) + N')  base: ' + DB_NAME()
    + CASE WHEN @Simular = 1 THEN N'  — SIMULACIÓN, no se borra nada' ELSE N'  — SE BORRA DE VERDAD' END;

/* ---- A quién se borra ----
   Tipo: cuenta     = desactivado con principal aquí y sin otra membresía activa
         membresia  = desactivado solo en esta compañía (su principal es otra)
         huerfano   = su cuenta ya no existe en el catálogo
         exmiembro  = su cuenta existe, pero ya no pertenece a esta compañía          */
DECLARE @Purgar TABLE (UserId uniqueidentifier PRIMARY KEY, Tipo varchar(10), Email nvarchar(320) NULL, Nombre nvarchar(300) NULL);

INSERT @Purgar (UserId, Tipo, Email, Nombre)
SELECT u.Id, 'cuenta', u.Email, u.Name
  FROM [$(Catalogo)].dbo.[User] u
 WHERE u.TenantId = @Tenant AND u.DeactivatedAt IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM [$(Catalogo)].dbo.UserCompany m
                    WHERE m.UserId = u.Id AND m.TenantId <> @Tenant AND m.DeactivatedAt IS NULL);

INSERT @Purgar (UserId, Tipo, Email, Nombre)
SELECT u.Id, 'membresia', u.Email, u.Name
  FROM [$(Catalogo)].dbo.UserCompany m
  JOIN [$(Catalogo)].dbo.[User] u ON u.Id = m.UserId
 WHERE m.TenantId = @Tenant AND m.DeactivatedAt IS NOT NULL
   AND (u.TenantId IS NULL OR u.TenantId <> @Tenant)
   AND NOT EXISTS (SELECT 1 FROM @Purgar p WHERE p.UserId = u.Id);

-- Huérfanos: aparecen en el historial de esta base pero su cuenta ya no existe.
INSERT @Purgar (UserId, Tipo)
SELECT DISTINCT x.UserId, 'huerfano' FROM (
          SELECT UserId FROM dbo.Attempt WHERE UserId IS NOT NULL
    UNION SELECT UserId FROM dbo.Certificate WHERE UserId IS NOT NULL
    UNION SELECT UserId FROM dbo.RetakeRequest
    UNION SELECT UserId FROM dbo.NotificationLog
    UNION SELECT UserId FROM dbo.UserGroupMember
    UNION SELECT TargetId FROM dbo.Assignment WHERE TargetType = 'user'
    UNION SELECT UserId FROM dbo.ExternalCertification
    UNION SELECT OwnerUserId FROM dbo.MediaAsset WHERE Purpose = 'record' AND OwnerUserId IS NOT NULL
    UNION SELECT UserId FROM dbo.AuditLog WHERE UserId IS NOT NULL
) x
WHERE NOT EXISTS (SELECT 1 FROM [$(Catalogo)].dbo.[User] u WHERE u.Id = x.UserId)
  AND NOT EXISTS (SELECT 1 FROM @Purgar p WHERE p.UserId = x.UserId);

-- Ex miembros: su cuenta existe (en otra compañía) pero ya no pertenecen a esta; el
-- «Quitar de la compañía» de antes borraba la membresía y dejaba aquí su historial. Solo
-- se limpia lo de esta base; su cuenta no se toca. El admin de plataforma no cuenta.
INSERT @Purgar (UserId, Tipo, Email, Nombre)
SELECT DISTINCT u.Id, 'exmiembro', u.Email, u.Name FROM (
          SELECT UserId FROM dbo.Attempt WHERE UserId IS NOT NULL
    UNION SELECT UserId FROM dbo.Certificate WHERE UserId IS NOT NULL
    UNION SELECT UserId FROM dbo.UserGroupMember
    UNION SELECT UserId FROM dbo.ExternalCertification
) x
JOIN [$(Catalogo)].dbo.[User] u ON u.Id = x.UserId
WHERE (u.TenantId IS NULL OR u.TenantId <> @Tenant)
  AND NOT (u.TenantId IS NULL AND u.Role = 'Admin')
  AND NOT EXISTS (SELECT 1 FROM [$(Catalogo)].dbo.UserCompany m WHERE m.UserId = u.Id AND m.TenantId = @Tenant)
  AND NOT EXISTS (SELECT 1 FROM @Purgar p WHERE p.UserId = u.Id);

-- Nombre de los huérfanos, del último intento o certificado que dejaron (para el listado).
UPDATE p SET Nombre = COALESCE(
        (SELECT TOP 1 a.LearnerName FROM dbo.Attempt a WHERE a.UserId = p.UserId AND a.LearnerName IS NOT NULL ORDER BY a.StartedAt DESC),
        (SELECT TOP 1 c.LearnerName FROM dbo.Certificate c WHERE c.UserId = p.UserId ORDER BY c.IssuedAt DESC))
  FROM @Purgar p WHERE p.Tipo = 'huerfano';

-- Correos de cuentas ya borradas por el «Eliminar» de antes (la bitácora general guardó
-- «user-deleted» con el correo): su rastro se borra de la bitácora general. Solo los
-- correos que hoy no son de ninguna cuenta.
DECLARE @CorreosBorrados TABLE (Email nvarchar(320) PRIMARY KEY);
INSERT @CorreosBorrados (Email)
SELECT DISTINCT LTRIM(RTRIM(a.Detail)) FROM [$(Catalogo)].dbo.AuditLog a
 WHERE a.Action = 'user-deleted' AND a.Detail LIKE '%@%' AND a.Detail NOT LIKE '% %'
   AND NOT EXISTS (SELECT 1 FROM [$(Catalogo)].dbo.[User] u WHERE u.Email = LTRIM(RTRIM(a.Detail)));
INSERT @CorreosBorrados (Email)
SELECT p.Email FROM @Purgar p WHERE p.Tipo = 'cuenta' AND p.Email IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM @CorreosBorrados c WHERE c.Email = p.Email);

/* ---- Archivos de expedientes a borrar a mano después ---- */
SELECT N'Archivos' AS Lista, m.RelativePath AS ArchivoEnUploads, m.FileName, m.OwnerUserId
  FROM dbo.MediaAsset m WHERE m.Purpose = 'record' ORDER BY m.RelativePath;

/* ---- Borrado ---- */
DECLARE @Resumen TABLE (Orden int IDENTITY, Base nvarchar(20), Tabla nvarchar(60), Filas int);

BEGIN TRANSACTION;

    -- 1. Progreso de todos (base de la compañía)
    DELETE FROM dbo.ItemResponse;                         INSERT @Resumen VALUES (N'compañía', N'ItemResponse (respuestas)', @@ROWCOUNT);
    DELETE FROM dbo.Certificate;                          INSERT @Resumen VALUES (N'compañía', N'Certificate (certificados)', @@ROWCOUNT);
    DELETE FROM dbo.Attempt;                              INSERT @Resumen VALUES (N'compañía', N'Attempt (intentos)', @@ROWCOUNT);
    DELETE FROM dbo.[Session];                            INSERT @Resumen VALUES (N'compañía', N'Session', @@ROWCOUNT);
    DELETE FROM dbo.RetakeRequest;                        INSERT @Resumen VALUES (N'compañía', N'RetakeRequest (pedir que lo repita)', @@ROWCOUNT);
    DELETE FROM dbo.NotificationLog;                      INSERT @Resumen VALUES (N'compañía', N'NotificationLog (recordatorios enviados)', @@ROWCOUNT);
    DELETE FROM dbo.Assignment;                           INSERT @Resumen VALUES (N'compañía', N'Assignment (asignaciones de sets)', @@ROWCOUNT);
    DELETE FROM dbo.GroupCourse;                          INSERT @Resumen VALUES (N'compañía', N'GroupCourse (planes de los grupos)', @@ROWCOUNT);
    DELETE FROM dbo.ExternalCertification;                INSERT @Resumen VALUES (N'compañía', N'ExternalCertification (expedientes)', @@ROWCOUNT);
    DELETE FROM dbo.MediaAsset WHERE Purpose = 'record';  INSERT @Resumen VALUES (N'compañía', N'MediaAsset (documentos de expedientes)', @@ROWCOUNT);
    IF @BorrarBitacoraCompania = 1
    BEGIN
        DELETE FROM dbo.AuditLog;                         INSERT @Resumen VALUES (N'compañía', N'AuditLog (bitácora de la compañía)', @@ROWCOUNT);
    END
    ELSE
    BEGIN
        DELETE FROM dbo.AuditLog WHERE UserId IN (SELECT UserId FROM @Purgar);
                                                          INSERT @Resumen VALUES (N'compañía', N'AuditLog (solo de los usuarios borrados)', @@ROWCOUNT);
    END
    DELETE FROM dbo.UserGroupMember WHERE UserId IN (SELECT UserId FROM @Purgar);
                                                          INSERT @Resumen VALUES (N'compañía', N'UserGroupMember (de los usuarios borrados)', @@ROWCOUNT);

    -- Enlaces de certificados por correo de esta compañía (base del catálogo)
    DELETE FROM [$(Catalogo)].dbo.CertificateLink WHERE TenantId = @Tenant;
                                                          INSERT @Resumen VALUES (N'catálogo', N'CertificateLink (enlaces de certificados)', @@ROWCOUNT);

    -- 2. Usuarios dados de baja (base del catálogo)
    DELETE FROM [$(Catalogo)].dbo.UserCompany
     WHERE (UserId IN (SELECT UserId FROM @Purgar WHERE Tipo = 'cuenta'))
        OR (TenantId = @Tenant AND UserId IN (SELECT UserId FROM @Purgar WHERE Tipo = 'membresia'));
                                                          INSERT @Resumen VALUES (N'catálogo', N'UserCompany (membresías)', @@ROWCOUNT);
    DELETE FROM [$(Catalogo)].dbo.PasswordResetToken WHERE UserId IN (SELECT UserId FROM @Purgar WHERE Tipo IN ('cuenta', 'huerfano'));
                                                          INSERT @Resumen VALUES (N'catálogo', N'PasswordResetToken', @@ROWCOUNT);
    DELETE FROM [$(Catalogo)].dbo.TwoFactorChallenge WHERE UserId IN (SELECT UserId FROM @Purgar WHERE Tipo IN ('cuenta', 'huerfano'));
                                                          INSERT @Resumen VALUES (N'catálogo', N'TwoFactorChallenge', @@ROWCOUNT);
    DELETE FROM [$(Catalogo)].dbo.SecurityEvent WHERE UserId IN (SELECT UserId FROM @Purgar WHERE Tipo IN ('cuenta', 'huerfano'));
                                                          INSERT @Resumen VALUES (N'catálogo', N'SecurityEvent (eventos de seguridad)', @@ROWCOUNT);
    DELETE a FROM [$(Catalogo)].dbo.AuditLog a
     WHERE a.UserId IN (SELECT UserId FROM @Purgar WHERE Tipo IN ('cuenta', 'huerfano'))
        OR EXISTS (SELECT 1 FROM @CorreosBorrados c WHERE a.Detail LIKE N'%' + c.Email + N'%');
                                                          INSERT @Resumen VALUES (N'catálogo', N'AuditLog (rastro de los usuarios borrados)', @@ROWCOUNT);
    DELETE FROM [$(Catalogo)].dbo.[User] WHERE Id IN (SELECT UserId FROM @Purgar WHERE Tipo = 'cuenta');
                                                          INSERT @Resumen VALUES (N'catálogo', N'User (cuentas)', @@ROWCOUNT);

IF @Simular = 1 ROLLBACK TRANSACTION; ELSE COMMIT TRANSACTION;

/* ---- Resultado ---- */
SELECT N'Usuarios que se borran' AS Lista, Tipo, Nombre, Email, UserId FROM @Purgar ORDER BY Tipo, Nombre;
SELECT CASE WHEN @Simular = 1 THEN N'SE BORRARÍAN (simulación)' ELSE N'BORRADAS' END AS Filas, Base, Tabla, Filas AS Cantidad
  FROM @Resumen ORDER BY Orden;
