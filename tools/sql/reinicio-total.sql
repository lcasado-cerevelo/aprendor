/* ============================================================================
   REINICIO TOTAL — dejar la plataforma en cero, conservando solo los adiestramientos
   y dos cuentas.

   Bases: TP_Advance (la compañía) y TP_Catalog (usuarios, bitácora general). Los nombres
   van fijos en el script. Todo va en UNA transacción: o se hace completo o no se hace nada.

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
     2. Abre el script y ejecútalo completo (F5), sin seleccionar nada y sin modo SQLCMD.
        Da igual a qué base estés conectado: cada tabla lleva el nombre completo.
        No le añadas GO: un GO a mitad del script borra las variables y todo falla.
     3. Déjalo con @Simular = 1 la primera vez: muestra qué cuentas quedan, cuáles se
        borran y cuántas filas por tabla, SIN borrar nada.
     4. Si cuadra, cambia a @Simular = 0 y vuelve a ejecutarlo.
     5. Borra a mano los archivos de expedientes que lista el resultado «Archivos» en
        App_Data\uploads del sitio (SQL no puede borrar archivos).
   Si el catálogo tiene OTRAS compañías, sus bases no se tocan: correr este script en
   cada una dejaría a sus usuarios sin cuenta. Revisa el resultado «Compañías».
   ============================================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Simular bit = 1;   -- 1 = solo enseña qué borraría; 0 = borra de verdad
DECLARE @Conservar1 nvarchar(320) = N'it@advancelogisticspr.com';   -- admin de plataforma
DECLARE @Conservar2 nvarchar(320) = N'lcasado@cerevelo.com';

/* ---- La compañía de esta base ---- */
DECLARE @Tenant uniqueidentifier, @NombreCompania nvarchar(200), @Coincidencias int;
SELECT @Coincidencias = COUNT(*) FROM TP_Catalog.dbo.Tenant
 WHERE REPLACE(ConnectionString, N' ', N'') + N';' LIKE N'%=TP[_]Advance;%';
IF @Coincidencias <> 1
BEGIN
    RAISERROR('No encontré (o encontré más de una) compañía en TP_Catalog cuya conexión apunte a TP_Advance. No se borró nada.', 16, 1);
    RETURN;
END
SELECT @Tenant = Id, @NombreCompania = Name FROM TP_Catalog.dbo.Tenant
 WHERE REPLACE(ConnectionString, N' ', N'') + N';' LIKE N'%=TP[_]Advance;%';

/* ---- Las dos cuentas que se conservan: tienen que existir las dos ---- */
DECLARE @Quedan TABLE (UserId uniqueidentifier PRIMARY KEY, Email nvarchar(320));
INSERT @Quedan SELECT Id, Email FROM TP_Catalog.dbo.[User] WHERE Email IN (@Conservar1, @Conservar2);
IF (SELECT COUNT(*) FROM @Quedan) <> 2
BEGIN
    RAISERROR('No encontré las DOS cuentas a conservar por su correo exacto (revisa @Conservar1 y @Conservar2). No se borró nada.', 16, 1);
    SELECT N'Cuentas encontradas' AS Lista, Email FROM @Quedan;
    RETURN;
END

PRINT N'Compañía: ' + @NombreCompania + N'  base: TP_Advance'
    + CASE WHEN @Simular = 1 THEN N'  — SIMULACIÓN, no se borra nada' ELSE N'  — SE BORRA DE VERDAD' END;

/* ---- Lo que hay antes de borrar ---- */
SELECT N'Compañías del catálogo' AS Lista, Name, Status, CASE WHEN Id = @Tenant THEN N'← esta base' ELSE N'(su base no se toca)' END AS Nota
  FROM TP_Catalog.dbo.Tenant ORDER BY Name;
SELECT N'Cuentas que QUEDAN' AS Lista, u.Email, u.Name, u.Role, CASE WHEN u.TenantId IS NULL THEN N'plataforma' ELSE N'compañía' END AS Tipo
  FROM TP_Catalog.dbo.[User] u WHERE u.Id IN (SELECT UserId FROM @Quedan);
SELECT N'Cuentas que se BORRAN' AS Lista, u.Email, u.Name, u.Role
  FROM TP_Catalog.dbo.[User] u WHERE u.Id NOT IN (SELECT UserId FROM @Quedan) ORDER BY u.Email;
SELECT N'Archivos' AS Lista, m.RelativePath AS ArchivoEnUploads, m.FileName
  FROM TP_Advance.dbo.MediaAsset m WHERE m.Purpose = 'record' ORDER BY m.RelativePath;

/* ---- Borrado ---- */
DECLARE @Resumen TABLE (Orden int IDENTITY, Base nvarchar(20), Tabla nvarchar(60), Filas int);

BEGIN TRANSACTION;

    -- Base de la compañía: todo menos los adiestramientos
    DELETE FROM TP_Advance.dbo.ItemResponse;                         INSERT @Resumen VALUES (N'compañía', N'ItemResponse (respuestas)', @@ROWCOUNT);
    DELETE FROM TP_Advance.dbo.Certificate;                          INSERT @Resumen VALUES (N'compañía', N'Certificate (certificados)', @@ROWCOUNT);
    DELETE FROM TP_Advance.dbo.Attempt;                              INSERT @Resumen VALUES (N'compañía', N'Attempt (intentos)', @@ROWCOUNT);
    DELETE FROM TP_Advance.dbo.[Session];                            INSERT @Resumen VALUES (N'compañía', N'Session', @@ROWCOUNT);
    DELETE FROM TP_Advance.dbo.RetakeRequest;                        INSERT @Resumen VALUES (N'compañía', N'RetakeRequest (pedir que lo repita)', @@ROWCOUNT);
    DELETE FROM TP_Advance.dbo.NotificationLog;                      INSERT @Resumen VALUES (N'compañía', N'NotificationLog (recordatorios enviados)', @@ROWCOUNT);
    DELETE FROM TP_Advance.dbo.Assignment;                           INSERT @Resumen VALUES (N'compañía', N'Assignment (asignaciones)', @@ROWCOUNT);
    DELETE FROM TP_Advance.dbo.GroupCourse;                          INSERT @Resumen VALUES (N'compañía', N'GroupCourse (planes de los grupos)', @@ROWCOUNT);
    DELETE FROM TP_Advance.dbo.UserGroupMember;                      INSERT @Resumen VALUES (N'compañía', N'UserGroupMember (miembros de grupos)', @@ROWCOUNT);
    DELETE FROM TP_Advance.dbo.UserGroup;                            INSERT @Resumen VALUES (N'compañía', N'UserGroup (grupos)', @@ROWCOUNT);
    DELETE FROM TP_Advance.dbo.ExternalCertification;                INSERT @Resumen VALUES (N'compañía', N'ExternalCertification (expedientes)', @@ROWCOUNT);
    DELETE FROM TP_Advance.dbo.MediaAsset WHERE Purpose = 'record';  INSERT @Resumen VALUES (N'compañía', N'MediaAsset (documentos de expedientes)', @@ROWCOUNT);
    DELETE FROM TP_Advance.dbo.AuditLog;                             INSERT @Resumen VALUES (N'compañía', N'AuditLog (bitácora de la compañía)', @@ROWCOUNT);

    -- Catálogo: enlaces de certificados de esta compañía, cuentas y rastros
    DELETE FROM TP_Catalog.dbo.CertificateLink WHERE TenantId = @Tenant;
                                                          INSERT @Resumen VALUES (N'catálogo', N'CertificateLink (enlaces de certificados)', @@ROWCOUNT);
    DELETE FROM TP_Catalog.dbo.UserCompany WHERE UserId NOT IN (SELECT UserId FROM @Quedan);
                                                          INSERT @Resumen VALUES (N'catálogo', N'UserCompany (membresías)', @@ROWCOUNT);
    DELETE FROM TP_Catalog.dbo.PasswordResetToken;     INSERT @Resumen VALUES (N'catálogo', N'PasswordResetToken (enlaces de contraseña)', @@ROWCOUNT);
    DELETE FROM TP_Catalog.dbo.TwoFactorChallenge;     INSERT @Resumen VALUES (N'catálogo', N'TwoFactorChallenge (retos de doble factor)', @@ROWCOUNT);
    DELETE FROM TP_Catalog.dbo.SecurityEvent;          INSERT @Resumen VALUES (N'catálogo', N'SecurityEvent (eventos de seguridad)', @@ROWCOUNT);
    DELETE FROM TP_Catalog.dbo.AuditLog;               INSERT @Resumen VALUES (N'catálogo', N'AuditLog (bitácora general)', @@ROWCOUNT);
    DELETE FROM TP_Catalog.dbo.[User] WHERE Id NOT IN (SELECT UserId FROM @Quedan);
                                                          INSERT @Resumen VALUES (N'catálogo', N'User (cuentas)', @@ROWCOUNT);
    -- Las dos cuentas que quedan: activas y sin bloqueos pendientes.
    UPDATE TP_Catalog.dbo.[User]
       SET DeactivatedAt = NULL, DeactivationReason = NULL, AccessFailedCount = 0, LockoutEnd = NULL,
           TwoFactorFailedCount = 0, TwoFactorLockedUntil = NULL
     WHERE Id IN (SELECT UserId FROM @Quedan);
    UPDATE TP_Catalog.dbo.UserCompany SET DeactivatedAt = NULL WHERE UserId IN (SELECT UserId FROM @Quedan);

IF @Simular = 1
    ROLLBACK TRANSACTION;
ELSE
    COMMIT TRANSACTION;

/* ---- Resultado ---- */
SELECT CASE WHEN @Simular = 1 THEN N'SE BORRARÍAN (simulación)' ELSE N'BORRADAS' END AS Filas, Base, Tabla, Filas AS Cantidad
  FROM @Resumen ORDER BY Orden;
