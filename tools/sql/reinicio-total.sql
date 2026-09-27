/* ============================================================================
   REINICIO TOTAL — dejar la plataforma en cero, conservando solo los adiestramientos
   y dos cuentas.

   Se ejecuta contra la base de la COMPAÑÍA (p. ej. TP_Advance) y toca también la del
   CATÁLOGO (usuarios, bitácora general), cuyo nombre va en :setvar. Todo va en UNA
   transacción: o se hace completo o no se hace nada.

   Se CONSERVA:
   - Las dos cuentas de @Conservar1 y @Conservar2 (con su contraseña, su rol, su doble
     factor y, si la tienen, su marca de oficial de cumplimiento).
   - Los adiestramientos: cursos, versiones, láminas y preguntas, sets, categorías, fotos
     y voz grabada (MediaAsset de cursos).
   - Las compañías (Tenant) y su configuración.

   Se BORRA, en esta compañía y en el catálogo:
   - Todas las demás cuentas, con sus membresías.
   - Todo el historial: intentos, respuestas, certificados y sus enlaces por correo,
     sesiones, solicitudes de «Pedir que lo repita», recordatorios enviados.
   - Asignaciones, grupos (con sus miembros y planes) y expedientes (certificaciones
     externas y sus documentos).
   - Las bitácoras completas: la de la compañía y la general del catálogo, más los
     eventos de seguridad, retos de doble factor y enlaces de contraseña pendientes.

   Cómo usarlo (SSMS):
     1. HAZ UN RESPALDO de las dos bases antes.
     2. Menú Query → «SQLCMD Mode» (tiene que quedar marcado).
     3. Revisa el nombre de la base del catálogo en :setvar y los dos correos de abajo.
        Conéctate a la base de la compañía.
     4. Déjalo con @Simular = 1 y ejecútalo: muestra qué cuentas quedan, cuáles se borran
        y cuántas filas por tabla, SIN borrar nada.
     5. Si cuadra, cambia a @Simular = 0 y vuelve a ejecutarlo.
     6. Borra a mano los archivos de expedientes que lista el resultado «Archivos» en
        App_Data\uploads del sitio (SQL no puede borrar archivos).
   Si el catálogo tiene OTRAS compañías, sus bases no se tocan: correr este script en
   cada una dejaría a sus usuarios sin cuenta. Revisa el resultado «Compañías».
   ============================================================================ */

:setvar Catalogo "TP_Catalog"

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Simular bit = 1;   -- 1 = solo enseña qué borraría; 0 = borra de verdad
DECLARE @Conservar1 nvarchar(320) = N'it@advancelogisticspr.com';   -- admin de plataforma
DECLARE @Conservar2 nvarchar(320) = N'lcasado@cerevelo.com';

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

/* ---- Las dos cuentas que se conservan: tienen que existir las dos ---- */
DECLARE @Quedan TABLE (UserId uniqueidentifier PRIMARY KEY, Email nvarchar(320));
INSERT @Quedan SELECT Id, Email FROM [$(Catalogo)].dbo.[User] WHERE Email IN (@Conservar1, @Conservar2);
IF (SELECT COUNT(*) FROM @Quedan) <> 2
BEGIN
    RAISERROR('No encontré las DOS cuentas a conservar por su correo exacto (revisa @Conservar1 y @Conservar2). No se borró nada.', 16, 1);
    SELECT N'Cuentas encontradas' AS Lista, Email FROM @Quedan;
    RETURN;
END

PRINT N'Compañía: ' + @NombreCompania + N'  base: ' + DB_NAME()
    + CASE WHEN @Simular = 1 THEN N'  — SIMULACIÓN, no se borra nada' ELSE N'  — SE BORRA DE VERDAD' END;

/* ---- Lo que hay antes de borrar ---- */
SELECT N'Compañías del catálogo' AS Lista, Name, Status, CASE WHEN Id = @Tenant THEN N'← esta base' ELSE N'(su base no se toca)' END AS Nota
  FROM [$(Catalogo)].dbo.Tenant ORDER BY Name;
SELECT N'Cuentas que QUEDAN' AS Lista, u.Email, u.Name, u.Role, CASE WHEN u.TenantId IS NULL THEN N'plataforma' ELSE N'compañía' END AS Tipo
  FROM [$(Catalogo)].dbo.[User] u WHERE u.Id IN (SELECT UserId FROM @Quedan);
SELECT N'Cuentas que se BORRAN' AS Lista, u.Email, u.Name, u.Role
  FROM [$(Catalogo)].dbo.[User] u WHERE u.Id NOT IN (SELECT UserId FROM @Quedan) ORDER BY u.Email;
SELECT N'Archivos' AS Lista, m.RelativePath AS ArchivoEnUploads, m.FileName
  FROM dbo.MediaAsset m WHERE m.Purpose = 'record' ORDER BY m.RelativePath;

/* ---- Borrado ---- */
DECLARE @Resumen TABLE (Orden int IDENTITY, Base nvarchar(20), Tabla nvarchar(60), Filas int);

BEGIN TRANSACTION;

    -- Base de la compañía: todo menos los adiestramientos
    DELETE FROM dbo.ItemResponse;                         INSERT @Resumen VALUES (N'compañía', N'ItemResponse (respuestas)', @@ROWCOUNT);
    DELETE FROM dbo.Certificate;                          INSERT @Resumen VALUES (N'compañía', N'Certificate (certificados)', @@ROWCOUNT);
    DELETE FROM dbo.Attempt;                              INSERT @Resumen VALUES (N'compañía', N'Attempt (intentos)', @@ROWCOUNT);
    DELETE FROM dbo.[Session];                            INSERT @Resumen VALUES (N'compañía', N'Session', @@ROWCOUNT);
    DELETE FROM dbo.RetakeRequest;                        INSERT @Resumen VALUES (N'compañía', N'RetakeRequest (pedir que lo repita)', @@ROWCOUNT);
    DELETE FROM dbo.NotificationLog;                      INSERT @Resumen VALUES (N'compañía', N'NotificationLog (recordatorios enviados)', @@ROWCOUNT);
    DELETE FROM dbo.Assignment;                           INSERT @Resumen VALUES (N'compañía', N'Assignment (asignaciones)', @@ROWCOUNT);
    DELETE FROM dbo.GroupCourse;                          INSERT @Resumen VALUES (N'compañía', N'GroupCourse (planes de los grupos)', @@ROWCOUNT);
    DELETE FROM dbo.UserGroupMember;                      INSERT @Resumen VALUES (N'compañía', N'UserGroupMember (miembros de grupos)', @@ROWCOUNT);
    DELETE FROM dbo.UserGroup;                            INSERT @Resumen VALUES (N'compañía', N'UserGroup (grupos)', @@ROWCOUNT);
    DELETE FROM dbo.ExternalCertification;                INSERT @Resumen VALUES (N'compañía', N'ExternalCertification (expedientes)', @@ROWCOUNT);
    DELETE FROM dbo.MediaAsset WHERE Purpose = 'record';  INSERT @Resumen VALUES (N'compañía', N'MediaAsset (documentos de expedientes)', @@ROWCOUNT);
    DELETE FROM dbo.AuditLog;                             INSERT @Resumen VALUES (N'compañía', N'AuditLog (bitácora de la compañía)', @@ROWCOUNT);

    -- Catálogo: enlaces de certificados de esta compañía, cuentas y rastros
    DELETE FROM [$(Catalogo)].dbo.CertificateLink WHERE TenantId = @Tenant;
                                                          INSERT @Resumen VALUES (N'catálogo', N'CertificateLink (enlaces de certificados)', @@ROWCOUNT);
    DELETE FROM [$(Catalogo)].dbo.UserCompany WHERE UserId NOT IN (SELECT UserId FROM @Quedan);
                                                          INSERT @Resumen VALUES (N'catálogo', N'UserCompany (membresías)', @@ROWCOUNT);
    DELETE FROM [$(Catalogo)].dbo.PasswordResetToken;     INSERT @Resumen VALUES (N'catálogo', N'PasswordResetToken (enlaces de contraseña)', @@ROWCOUNT);
    DELETE FROM [$(Catalogo)].dbo.TwoFactorChallenge;     INSERT @Resumen VALUES (N'catálogo', N'TwoFactorChallenge (retos de doble factor)', @@ROWCOUNT);
    DELETE FROM [$(Catalogo)].dbo.SecurityEvent;          INSERT @Resumen VALUES (N'catálogo', N'SecurityEvent (eventos de seguridad)', @@ROWCOUNT);
    DELETE FROM [$(Catalogo)].dbo.AuditLog;               INSERT @Resumen VALUES (N'catálogo', N'AuditLog (bitácora general)', @@ROWCOUNT);
    DELETE FROM [$(Catalogo)].dbo.[User] WHERE Id NOT IN (SELECT UserId FROM @Quedan);
                                                          INSERT @Resumen VALUES (N'catálogo', N'User (cuentas)', @@ROWCOUNT);
    -- Las dos cuentas que quedan: activas y sin bloqueos pendientes.
    UPDATE [$(Catalogo)].dbo.[User]
       SET DeactivatedAt = NULL, DeactivationReason = NULL, AccessFailedCount = 0, LockoutEnd = NULL,
           TwoFactorFailedCount = 0, TwoFactorLockedUntil = NULL
     WHERE Id IN (SELECT UserId FROM @Quedan);
    UPDATE [$(Catalogo)].dbo.UserCompany SET DeactivatedAt = NULL WHERE UserId IN (SELECT UserId FROM @Quedan);

IF @Simular = 1 ROLLBACK TRANSACTION; ELSE COMMIT TRANSACTION;

/* ---- Resultado ---- */
SELECT CASE WHEN @Simular = 1 THEN N'SE BORRARÍAN (simulación)' ELSE N'BORRADAS' END AS Filas, Base, Tabla, Filas AS Cantidad
  FROM @Resumen ORDER BY Orden;
