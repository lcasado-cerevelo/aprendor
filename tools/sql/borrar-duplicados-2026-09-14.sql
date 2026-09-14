/* ============================================================================
   Borrado de las 4 copias VIEJAS y duplicadas (creadas 2026-09-13 22:02) de los
   cursos nuevos, dejando las copias nuevas (2026-09-14 00:12) que sí tienen el
   contenido actualizado. Se ejecuta contra la base del TENANT de Advance Logistics.

   Cómo usarlo:
     1. Con @Simular = 1, ejecuta todo el script: por cada uno de los 4 Ids te dice
        cuántas versiones, intentos y certificados tiene, sin borrar nada.
     2. Si todos dan 0 intentos y 0 certificados (deberían, son drafts de ayer que
        nadie ha tomado), cambia a @Simular = 0 y vuelve a ejecutar.
   ============================================================================ */

SET NOCOUNT ON;

DECLARE @Simular bit = 1;   -- 1 = solo enseña qué borraría; 0 = borra de verdad

DECLARE @Viejos TABLE (Id uniqueidentifier PRIMARY KEY, TituloEsperado nvarchar(400));
INSERT INTO @Viejos (Id, TituloEsperado) VALUES
    ('DF672471-2F97-4FE3-BDF7-7735E6E893F9', N'Cumplimiento con la Ley HIPAA'),
    ('480FBD0E-9938-415E-87A5-9FD4A353E1C0', N'Protocolo de Manejo de Situaciones de Violencia Doméstica en el Empleo'),
    ('A1081C17-2CB4-41E6-AC48-CDBEB80D2B53', N'Acoso Laboral en el Empleo'),
    ('AC14D7BB-A913-4548-8CDF-BE11CF689858', N'Hostigamiento Sexual en el Empleo');

/* ---- Verificación: que el título de cada Id siga siendo el esperado ---- */
IF EXISTS (
    SELECT 1 FROM @Viejos v
    LEFT JOIN Training t ON t.Id = v.Id
    WHERE t.Id IS NULL OR t.Title <> v.TituloEsperado
)
BEGIN
    RAISERROR('Al menos un Id no existe o su título no coincide con lo esperado. Revisa antes de continuar. No se borró nada.', 16, 1);
    SELECT v.Id, Esperado = v.TituloEsperado, Real = t.Title, Existe = IIF(t.Id IS NULL, 'NO', 'SI')
    FROM @Viejos v LEFT JOIN Training t ON t.Id = v.Id;
    RETURN;
END

/* ---- Qué cuelga de estos 4 cursos ---- */
DECLARE @Versiones TABLE (Id uniqueidentifier PRIMARY KEY, TrainingId uniqueidentifier);
INSERT INTO @Versiones (Id, TrainingId) SELECT Id, TrainingId FROM TrainingVersion WHERE TrainingId IN (SELECT Id FROM @Viejos);

DECLARE @Intentos TABLE (Id uniqueidentifier PRIMARY KEY);
INSERT INTO @Intentos (Id) SELECT Id FROM Attempt WHERE TrainingVersionId IN (SELECT Id FROM @Versiones);

SELECT
    t.Title,
    t.Id,
    Versiones    = (SELECT COUNT(*) FROM @Versiones vv WHERE vv.TrainingId = t.Id),
    Contenido    = (SELECT COUNT(*) FROM TrainingItem WHERE TrainingVersionId IN (SELECT Id FROM @Versiones vv WHERE vv.TrainingId = t.Id)),
    Intentos     = (SELECT COUNT(*) FROM Attempt WHERE TrainingVersionId IN (SELECT Id FROM @Versiones vv WHERE vv.TrainingId = t.Id)),
    Certificados = (SELECT COUNT(*) FROM Certificate WHERE TrainingId = t.Id
                                                        OR AttemptId IN (SELECT Id FROM @Intentos))
FROM Training t
WHERE t.Id IN (SELECT Id FROM @Viejos);

DECLARE @NumIntentos int = (SELECT COUNT(*) FROM @Intentos);
DECLARE @NumCerts    int = (SELECT COUNT(*) FROM Certificate WHERE TrainingId IN (SELECT Id FROM @Viejos)
                                                                OR AttemptId IN (SELECT Id FROM @Intentos));

IF (@NumIntentos > 0 OR @NumCerts > 0)
BEGIN
    RAISERROR('Al menos una de las 4 copias viejas tiene intentos o certificados. No se borró nada — revisa los números de arriba antes de decidir.', 16, 1);
    RETURN;
END

IF @Simular = 1
BEGIN
    PRINT 'SIMULACIÓN: no se borró nada. Si los números de arriba dan 0 intentos y 0 certificados, pon @Simular = 0 y vuelve a ejecutar.';
    RETURN;
END

/* ---- Borrado, de las hojas al tronco, todo o nada ---- */
BEGIN TRY
    BEGIN TRANSACTION;

        DELETE FROM ItemResponse         WHERE AttemptId IN (SELECT Id FROM @Intentos);
        DELETE FROM Certificate          WHERE TrainingId IN (SELECT Id FROM @Viejos)
                                            OR AttemptId IN (SELECT Id FROM @Intentos);
        DELETE FROM Attempt              WHERE TrainingVersionId IN (SELECT Id FROM @Versiones);
        DELETE FROM Session              WHERE TrainingVersionId IN (SELECT Id FROM @Versiones);
        DELETE FROM TrainingItem         WHERE TrainingVersionId IN (SELECT Id FROM @Versiones);
        DELETE FROM TrainingVersion      WHERE TrainingId IN (SELECT Id FROM @Viejos);
        DELETE FROM TrainingSetExclusion WHERE SetId IN (SELECT Id FROM TrainingSet WHERE TrainingId IN (SELECT Id FROM @Viejos));
        DELETE FROM Assignment           WHERE TrainingId IN (SELECT Id FROM @Viejos);
        DELETE FROM TrainingSet          WHERE TrainingId IN (SELECT Id FROM @Viejos);
        DELETE FROM Training             WHERE Id IN (SELECT Id FROM @Viejos);

    COMMIT TRANSACTION;
    PRINT 'Borradas las 4 copias viejas y duplicadas.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    DECLARE @err nvarchar(2000) = ERROR_MESSAGE();
    RAISERROR('Falló el borrado, se revirtió todo: %s', 16, 1, @err);
END CATCH
