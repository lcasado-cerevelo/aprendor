/* ============================================================================
   Borrado DEFINITIVO de un adiestramiento y todo lo que cuelga de él.
   Se ejecuta contra la base del TENANT (la que tiene Training, Attempt, etc.),
   no contra la del catálogo.

   Preferible archivar (botón "Archivar" en la app): conserva historial y se
   puede restaurar. Esto no tiene vuelta atrás: saca respaldo antes.

   Cómo usarlo:
     1. Pon el Id y el título exacto abajo.
     2. Déjalo con @Simular = 1 y ejecútalo: te dice qué borraría, sin borrar.
     3. Si el conteo cuadra, cambia a @Simular = 0 y vuelve a ejecutarlo.

   Por defecto se NIEGA a borrar un curso que tenga intentos o certificados
   (@PermitirConHistorial = 0). Los certificados son la evidencia de que la
   gente se adiestró; para HIPAA eso se conserva. Si de verdad quieres arrasar
   con el historial, pon @PermitirConHistorial = 1 a conciencia.
   ============================================================================ */

SET NOCOUNT ON;

DECLARE @TrainingId       uniqueidentifier = '00000000-0000-0000-0000-000000000000';  -- <-- Id del curso
DECLARE @ConfirmarTitulo  nvarchar(400)    = N'ESCRIBE AQUI EL TITULO EXACTO';        -- <-- doble verificación
DECLARE @Simular          bit = 1;   -- 1 = solo enseña qué borraría; 0 = borra de verdad
DECLARE @PermitirConHistorial bit = 0;

/* ---- Verificaciones ---- */
DECLARE @TituloReal nvarchar(400) = (SELECT Title FROM Training WHERE Id = @TrainingId);

IF @TituloReal IS NULL
BEGIN
    RAISERROR('No existe ningún adiestramiento con ese Id en esta base. No se borró nada.', 16, 1);
    RETURN;
END

IF @TituloReal <> @ConfirmarTitulo
BEGIN
    RAISERROR('El título de confirmación no coincide con el del adiestramiento (%s). No se borró nada.', 16, 1, @TituloReal);
    RETURN;
END

/* ---- Qué cuelga de este curso ---- */
DECLARE @Versiones TABLE (Id uniqueidentifier PRIMARY KEY);
INSERT INTO @Versiones (Id) SELECT Id FROM TrainingVersion WHERE TrainingId = @TrainingId;

DECLARE @Intentos TABLE (Id uniqueidentifier PRIMARY KEY);
INSERT INTO @Intentos (Id) SELECT Id FROM Attempt WHERE TrainingVersionId IN (SELECT Id FROM @Versiones);

DECLARE @Sets TABLE (Id uniqueidentifier PRIMARY KEY);
INSERT INTO @Sets (Id) SELECT Id FROM TrainingSet WHERE TrainingId = @TrainingId;

SELECT
    Titulo        = @TituloReal,
    Versiones     = (SELECT COUNT(*) FROM @Versiones),
    Contenido     = (SELECT COUNT(*) FROM TrainingItem WHERE TrainingVersionId IN (SELECT Id FROM @Versiones)),
    Sesiones      = (SELECT COUNT(*) FROM Session      WHERE TrainingVersionId IN (SELECT Id FROM @Versiones)),
    Intentos      = (SELECT COUNT(*) FROM @Intentos),
    Respuestas    = (SELECT COUNT(*) FROM ItemResponse WHERE AttemptId IN (SELECT Id FROM @Intentos)),
    Certificados  = (SELECT COUNT(*) FROM Certificate  WHERE TrainingId = @TrainingId
                                                          OR AttemptId IN (SELECT Id FROM @Intentos)),
    Sets          = (SELECT COUNT(*) FROM @Sets),
    Exclusiones   = (SELECT COUNT(*) FROM TrainingSetExclusion WHERE SetId IN (SELECT Id FROM @Sets)),
    Asignaciones  = (SELECT COUNT(*) FROM Assignment   WHERE TrainingId = @TrainingId);

DECLARE @NumIntentos int = (SELECT COUNT(*) FROM @Intentos);
DECLARE @NumCerts    int = (SELECT COUNT(*) FROM Certificate WHERE TrainingId = @TrainingId
                                                                OR AttemptId IN (SELECT Id FROM @Intentos));

IF (@NumIntentos > 0 OR @NumCerts > 0) AND @PermitirConHistorial = 0
BEGIN
    RAISERROR('Este adiestramiento tiene historial (%d intentos, %d certificados). No se borró nada. Archívalo, o pon @PermitirConHistorial = 1 si de verdad quieres perder esa evidencia.', 16, 1, @NumIntentos, @NumCerts);
    RETURN;
END

IF @Simular = 1
BEGIN
    PRINT 'SIMULACIÓN: no se borró nada. Si los números de arriba cuadran, pon @Simular = 0 y vuelve a ejecutar.';
    RETURN;
END

/* ---- Borrado, de las hojas al tronco, todo o nada ---- */
BEGIN TRY
    BEGIN TRANSACTION;

        DELETE FROM ItemResponse         WHERE AttemptId IN (SELECT Id FROM @Intentos);
        DELETE FROM Certificate          WHERE TrainingId = @TrainingId
                                            OR AttemptId IN (SELECT Id FROM @Intentos);
        DELETE FROM Attempt              WHERE TrainingVersionId IN (SELECT Id FROM @Versiones);
        DELETE FROM Session              WHERE TrainingVersionId IN (SELECT Id FROM @Versiones);
        DELETE FROM TrainingItem         WHERE TrainingVersionId IN (SELECT Id FROM @Versiones);
        DELETE FROM TrainingVersion      WHERE TrainingId = @TrainingId;
        DELETE FROM TrainingSetExclusion WHERE SetId IN (SELECT Id FROM @Sets);
        DELETE FROM Assignment           WHERE TrainingId = @TrainingId;
        DELETE FROM TrainingSet          WHERE TrainingId = @TrainingId;
        DELETE FROM Training             WHERE Id = @TrainingId;

    COMMIT TRANSACTION;
    PRINT 'Borrado completo: ' + @TituloReal;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    DECLARE @err nvarchar(2000) = ERROR_MESSAGE();
    RAISERROR('Falló el borrado, se revirtió todo: %s', 16, 1, @err);
END CATCH

/* Nota: los archivos subidos (MediaAsset) no se tocan — son compartidos entre
   cursos y viven en App_Data/uploads. Este curso no usa medios subidos: sus
   imágenes van incrustadas en el propio contenido. */
