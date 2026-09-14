IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610033443_InitialTenant'
)
BEGIN
    CREATE TABLE [Attempts] (
        [Id] uniqueidentifier NOT NULL,
        [TrainingVersionId] uniqueidentifier NOT NULL,
        [SessionId] uniqueidentifier NULL,
        [UserId] uniqueidentifier NULL,
        [GuestToken] nvarchar(max) NULL,
        [GuestName] nvarchar(max) NULL,
        [Score] int NOT NULL,
        [Passed] bit NOT NULL,
        [Status] nvarchar(max) NOT NULL,
        [StartedAt] datetime2 NOT NULL,
        [CompletedAt] datetime2 NULL,
        CONSTRAINT [PK_Attempts] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610033443_InitialTenant'
)
BEGIN
    CREATE TABLE [AuditLogs] (
        [Id] bigint NOT NULL IDENTITY,
        [Action] nvarchar(max) NOT NULL,
        [Detail] nvarchar(max) NULL,
        [UserId] uniqueidentifier NULL,
        [At] datetime2 NOT NULL,
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610033443_InitialTenant'
)
BEGIN
    CREATE TABLE [Categories] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [ParentId] uniqueidentifier NULL,
        CONSTRAINT [PK_Categories] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610033443_InitialTenant'
)
BEGIN
    CREATE TABLE [ItemResponses] (
        [Id] bigint NOT NULL IDENTITY,
        [AttemptId] uniqueidentifier NOT NULL,
        [ItemId] uniqueidentifier NOT NULL,
        [AnswerJson] nvarchar(max) NOT NULL,
        [IsCorrect] bit NULL,
        [PointsAwarded] int NOT NULL,
        [AnsweredAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ItemResponses] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610033443_InitialTenant'
)
BEGIN
    CREATE TABLE [Sessions] (
        [Id] uniqueidentifier NOT NULL,
        [TrainingVersionId] uniqueidentifier NOT NULL,
        [Mode] nvarchar(max) NOT NULL,
        [Code] nvarchar(450) NULL,
        [ModeratorUserId] uniqueidentifier NULL,
        [StartedAt] datetime2 NOT NULL,
        [EndedAt] datetime2 NULL,
        CONSTRAINT [PK_Sessions] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610033443_InitialTenant'
)
BEGIN
    CREATE TABLE [TrainingItems] (
        [Id] uniqueidentifier NOT NULL,
        [TrainingVersionId] uniqueidentifier NOT NULL,
        [Order] int NOT NULL,
        [Type] nvarchar(max) NOT NULL,
        [PayloadJson] nvarchar(max) NOT NULL,
        [Required] bit NOT NULL,
        CONSTRAINT [PK_TrainingItems] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610033443_InitialTenant'
)
BEGIN
    CREATE TABLE [Trainings] (
        [Id] uniqueidentifier NOT NULL,
        [Title] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [CategoryId] uniqueidentifier NULL,
        [Status] nvarchar(max) NOT NULL,
        [CreatedByUserId] uniqueidentifier NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Trainings] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610033443_InitialTenant'
)
BEGIN
    CREATE TABLE [TrainingVersions] (
        [Id] uniqueidentifier NOT NULL,
        [TrainingId] uniqueidentifier NOT NULL,
        [VersionNumber] int NOT NULL,
        [ConfigJson] nvarchar(max) NOT NULL,
        [PublishedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_TrainingVersions] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610033443_InitialTenant'
)
BEGIN
    CREATE INDEX [IX_Attempts_SessionId] ON [Attempts] ([SessionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610033443_InitialTenant'
)
BEGIN
    CREATE INDEX [IX_Attempts_TrainingVersionId_UserId] ON [Attempts] ([TrainingVersionId], [UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610033443_InitialTenant'
)
BEGIN
    CREATE INDEX [IX_ItemResponses_AttemptId] ON [ItemResponses] ([AttemptId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610033443_InitialTenant'
)
BEGIN
    CREATE INDEX [IX_Sessions_Code] ON [Sessions] ([Code]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610033443_InitialTenant'
)
BEGIN
    CREATE INDEX [IX_TrainingItems_TrainingVersionId] ON [TrainingItems] ([TrainingVersionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610033443_InitialTenant'
)
BEGIN
    CREATE INDEX [IX_Trainings_CategoryId] ON [Trainings] ([CategoryId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610033443_InitialTenant'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TrainingVersions_TrainingId_VersionNumber] ON [TrainingVersions] ([TrainingId], [VersionNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610033443_InitialTenant'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260610033443_InitialTenant', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610041741_Fase2'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TrainingVersions]') AND [c].[name] = N'PublishedAt');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [TrainingVersions] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [TrainingVersions] ALTER COLUMN [PublishedAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610041741_Fase2'
)
BEGIN
    ALTER TABLE [TrainingVersions] ADD [PassPercent] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610041741_Fase2'
)
BEGIN
    ALTER TABLE [TrainingVersions] ADD [Status] nvarchar(max) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610041741_Fase2'
)
BEGIN
    ALTER TABLE [TrainingItems] ADD [Points] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610041741_Fase2'
)
BEGIN
    ALTER TABLE [Attempts] ADD [LearnerName] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610041741_Fase2'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260610041741_Fase2', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610051212_Fase2Media'
)
BEGIN
    CREATE TABLE [MediaAssets] (
        [Id] uniqueidentifier NOT NULL,
        [FileName] nvarchar(max) NOT NULL,
        [ContentType] nvarchar(max) NOT NULL,
        [RelativePath] nvarchar(max) NOT NULL,
        [Size] bigint NOT NULL,
        [UploadedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_MediaAssets] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610051212_Fase2Media'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260610051212_Fase2Media', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610061400_Fase2Cancel'
)
BEGIN
    ALTER TABLE [Attempts] ADD [CancelApprovalComment] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610061400_Fase2Cancel'
)
BEGIN
    ALTER TABLE [Attempts] ADD [CancelRequestComment] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610061400_Fase2Cancel'
)
BEGIN
    ALTER TABLE [Attempts] ADD [CancelRequestedAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610061400_Fase2Cancel'
)
BEGIN
    ALTER TABLE [Attempts] ADD [CanceledAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610061400_Fase2Cancel'
)
BEGIN
    ALTER TABLE [Attempts] ADD [CanceledByName] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610061400_Fase2Cancel'
)
BEGIN
    ALTER TABLE [Attempts] ADD [CanceledByUserId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610061400_Fase2Cancel'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260610061400_Fase2Cancel', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610064847_Fase2Open'
)
BEGIN
    ALTER TABLE [ItemResponses] ADD [GradedAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610064847_Fase2Open'
)
BEGIN
    ALTER TABLE [ItemResponses] ADD [GradedByName] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610064847_Fase2Open'
)
BEGIN
    ALTER TABLE [ItemResponses] ADD [GradedByUserId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610064847_Fase2Open'
)
BEGIN
    ALTER TABLE [ItemResponses] ADD [GraderComment] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610064847_Fase2Open'
)
BEGIN
    ALTER TABLE [ItemResponses] ADD [NeedsGrading] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610064847_Fase2Open'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260610064847_Fase2Open', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610231916_Fase2ItemActive'
)
BEGIN
    ALTER TABLE [TrainingItems] ADD [Active] bit NOT NULL DEFAULT CAST(1 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610231916_Fase2ItemActive'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260610231916_Fase2ItemActive', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610233740_Fase3Versiones'
)
BEGIN
    ALTER TABLE [Trainings] ADD [DisplayGroupId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610233740_Fase3Versiones'
)
BEGIN
    CREATE TABLE [Assignments] (
        [Id] uniqueidentifier NOT NULL,
        [DisplayGroupId] uniqueidentifier NOT NULL,
        [TrainingId] uniqueidentifier NOT NULL,
        [TargetType] nvarchar(max) NOT NULL,
        [TargetId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Assignments] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610233740_Fase3Versiones'
)
BEGIN
    CREATE TABLE [DisplayGroups] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [DefaultTrainingId] uniqueidentifier NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_DisplayGroups] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610233740_Fase3Versiones'
)
BEGIN
    CREATE TABLE [UserGroupMembers] (
        [Id] uniqueidentifier NOT NULL,
        [UserGroupId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_UserGroupMembers] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610233740_Fase3Versiones'
)
BEGIN
    CREATE TABLE [UserGroups] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_UserGroups] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610233740_Fase3Versiones'
)
BEGIN
    CREATE INDEX [IX_Trainings_DisplayGroupId] ON [Trainings] ([DisplayGroupId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610233740_Fase3Versiones'
)
BEGIN
    CREATE INDEX [IX_Assignments_DisplayGroupId] ON [Assignments] ([DisplayGroupId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610233740_Fase3Versiones'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UserGroupMembers_UserGroupId_UserId] ON [UserGroupMembers] ([UserGroupId], [UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610233740_Fase3Versiones'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260610233740_Fase3Versiones', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611001951_Fase3Sets'
)
BEGIN
    DROP TABLE [DisplayGroups];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611001951_Fase3Sets'
)
BEGIN
    DROP INDEX [IX_Trainings_DisplayGroupId] ON [Trainings];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611001951_Fase3Sets'
)
BEGIN
    DROP INDEX [IX_Assignments_DisplayGroupId] ON [Assignments];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611001951_Fase3Sets'
)
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Trainings]') AND [c].[name] = N'DisplayGroupId');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Trainings] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [Trainings] DROP COLUMN [DisplayGroupId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611001951_Fase3Sets'
)
BEGIN
    EXEC sp_rename N'[Assignments].[DisplayGroupId]', N'SetId', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611001951_Fase3Sets'
)
BEGIN
    ALTER TABLE [TrainingItems] ADD [StableKey] uniqueidentifier NOT NULL DEFAULT (NEWID());
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611001951_Fase3Sets'
)
BEGIN
    ALTER TABLE [Attempts] ADD [SetId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611001951_Fase3Sets'
)
BEGIN
    CREATE TABLE [TrainingSetExclusions] (
        [Id] uniqueidentifier NOT NULL,
        [SetId] uniqueidentifier NOT NULL,
        [StableKey] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_TrainingSetExclusions] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611001951_Fase3Sets'
)
BEGIN
    CREATE TABLE [TrainingSets] (
        [Id] uniqueidentifier NOT NULL,
        [TrainingId] uniqueidentifier NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [IsDefault] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_TrainingSets] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611001951_Fase3Sets'
)
BEGIN
    CREATE INDEX [IX_TrainingItems_StableKey] ON [TrainingItems] ([StableKey]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611001951_Fase3Sets'
)
BEGIN
    CREATE INDEX [IX_Assignments_TrainingId] ON [Assignments] ([TrainingId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611001951_Fase3Sets'
)
BEGIN
    CREATE INDEX [IX_TrainingSetExclusions_SetId] ON [TrainingSetExclusions] ([SetId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611001951_Fase3Sets'
)
BEGIN
    CREATE INDEX [IX_TrainingSets_TrainingId] ON [TrainingSets] ([TrainingId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611001951_Fase3Sets'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260611001951_Fase3Sets', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611013726_SingularTableNames'
)
BEGIN
    ALTER TABLE [UserGroups] DROP CONSTRAINT [PK_UserGroups];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611013726_SingularTableNames'
)
BEGIN
    ALTER TABLE [UserGroupMembers] DROP CONSTRAINT [PK_UserGroupMembers];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611013726_SingularTableNames'
)
BEGIN
    ALTER TABLE [TrainingVersions] DROP CONSTRAINT [PK_TrainingVersions];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611013726_SingularTableNames'
)
BEGIN
    ALTER TABLE [TrainingSets] DROP CONSTRAINT [PK_TrainingSets];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611013726_SingularTableNames'
)
BEGIN
    ALTER TABLE [TrainingSetExclusions] DROP CONSTRAINT [PK_TrainingSetExclusions];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611013726_SingularTableNames'
)
BEGIN
    ALTER TABLE [Trainings] DROP CONSTRAINT [PK_Trainings];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611013726_SingularTableNames'
)
BEGIN
    ALTER TABLE [TrainingItems] DROP CONSTRAINT [PK_TrainingItems];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611013726_SingularTableNames'
)
BEGIN
    ALTER TABLE [Sessions] DROP CONSTRAINT [PK_Sessions];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611013726_SingularTableNames'
)
BEGIN
    ALTER TABLE [MediaAssets] DROP CONSTRAINT [PK_MediaAssets];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611013726_SingularTableNames'
)
BEGIN
    ALTER TABLE [ItemResponses] DROP CONSTRAINT [PK_ItemResponses];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611013726_SingularTableNames'
)
BEGIN
    ALTER TABLE [Categories] DROP CONSTRAINT [PK_Categories];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611013726_SingularTableNames'
)
BEGIN
    ALTER TABLE [AuditLogs] DROP CONSTRAINT [PK_AuditLogs];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611013726_SingularTableNames'
)
BEGIN
    ALTER TABLE [Attempts] DROP CONSTRAINT [PK_Attempts];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611013726_SingularTableNames'
)
BEGIN
    ALTER TABLE [Assignments] DROP CONSTRAINT [PK_Assignments];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611013726_SingularTableNames'
)
BEGIN
    EXEC sp_rename N'[UserGroups]', N'UserGroup';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611013726_SingularTableNames'
)
BEGIN
    EXEC sp_rename N'[UserGroupMembers]', N'UserGroupMember';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611013726_SingularTableNames'
)
BEGIN
    EXEC sp_rename N'[TrainingVersions]', N'TrainingVersion';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611013726_SingularTableNames'
)
BEGIN
    EXEC sp_rename N'[TrainingSets]', N'TrainingSet';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611013726_SingularTableNames'
)
BEGIN
    EXEC sp_rename N'[TrainingSetExclusions]', N'TrainingSetExclusion';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611013726_SingularTableNames'
)
BEGIN
    EXEC sp_rename N'[Trainings]', N'Training';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611013726_SingularTableNames'
)
BEGIN
    EXEC sp_rename N'[TrainingItems]', N'TrainingItem';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611013726_SingularTableNames'
)
BEGIN
    EXEC sp_rename N'[Sessions]', N'Session';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611013726_SingularTableNames'
)
BEGIN
    EXEC sp_rename N'[MediaAssets]', N'MediaAsset';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611013726_SingularTableNames'
)
BEGIN
    EXEC sp_rename N'[ItemResponses]', N'ItemResponse';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611013726_SingularTableNames'
)
BEGIN
    EXEC sp_rename N'[Categories]', N'Category';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611013726_SingularTableNames'
)
BEGIN
    EXEC sp_rename N'[AuditLogs]', N'AuditLog';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611013726_SingularTableNames'
)
BEGIN
    EXEC sp_rename N'[Attempts]', N'Attempt';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611013726_SingularTableNames'
)
BEGIN
    EXEC sp_rename N'[Assignments]', N'Assignment';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611013726_SingularTableNames'
)
BEGIN
    EXEC sp_rename N'[UserGroupMember].[IX_UserGroupMembers_UserGroupId_UserId]', N'IX_UserGroupMember_UserGroupId_UserId', N'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611013726_SingularTableNames'
)
BEGIN
    EXEC sp_rename N'[TrainingVersion].[IX_TrainingVersions_TrainingId_VersionNumber]', N'IX_TrainingVersion_TrainingId_VersionNumber', N'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611013726_SingularTableNames'
)
BEGIN
    EXEC sp_rename N'[TrainingSet].[IX_TrainingSets_TrainingId]', N'IX_TrainingSet_TrainingId', N'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611013726_SingularTableNames'
)
BEGIN
    EXEC sp_rename N'[TrainingSetExclusion].[IX_TrainingSetExclusions_SetId]', N'IX_TrainingSetExclusion_SetId', N'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611013726_SingularTableNames'
)
BEGIN
    EXEC sp_rename N'[Training].[IX_Trainings_CategoryId]', N'IX_Training_CategoryId', N'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611013726_SingularTableNames'
)
BEGIN
    EXEC sp_rename N'[TrainingItem].[IX_TrainingItems_TrainingVersionId]', N'IX_TrainingItem_TrainingVersionId', N'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611013726_SingularTableNames'
)
BEGIN
    EXEC sp_rename N'[TrainingItem].[IX_TrainingItems_StableKey]', N'IX_TrainingItem_StableKey', N'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611013726_SingularTableNames'
)
BEGIN
    EXEC sp_rename N'[Session].[IX_Sessions_Code]', N'IX_Session_Code', N'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611013726_SingularTableNames'
)
BEGIN
    EXEC sp_rename N'[ItemResponse].[IX_ItemResponses_AttemptId]', N'IX_ItemResponse_AttemptId', N'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611013726_SingularTableNames'
)
BEGIN
    EXEC sp_rename N'[Attempt].[IX_Attempts_TrainingVersionId_UserId]', N'IX_Attempt_TrainingVersionId_UserId', N'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611013726_SingularTableNames'
)
BEGIN
    EXEC sp_rename N'[Attempt].[IX_Attempts_SessionId]', N'IX_Attempt_SessionId', N'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611013726_SingularTableNames'
)
BEGIN
    EXEC sp_rename N'[Assignment].[IX_Assignments_TrainingId]', N'IX_Assignment_TrainingId', N'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611013726_SingularTableNames'
)
BEGIN
    ALTER TABLE [UserGroup] ADD CONSTRAINT [PK_UserGroup] PRIMARY KEY ([Id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611013726_SingularTableNames'
)
BEGIN
    ALTER TABLE [UserGroupMember] ADD CONSTRAINT [PK_UserGroupMember] PRIMARY KEY ([Id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611013726_SingularTableNames'
)
BEGIN
    ALTER TABLE [TrainingVersion] ADD CONSTRAINT [PK_TrainingVersion] PRIMARY KEY ([Id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611013726_SingularTableNames'
)
BEGIN
    ALTER TABLE [TrainingSet] ADD CONSTRAINT [PK_TrainingSet] PRIMARY KEY ([Id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611013726_SingularTableNames'
)
BEGIN
    ALTER TABLE [TrainingSetExclusion] ADD CONSTRAINT [PK_TrainingSetExclusion] PRIMARY KEY ([Id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611013726_SingularTableNames'
)
BEGIN
    ALTER TABLE [Training] ADD CONSTRAINT [PK_Training] PRIMARY KEY ([Id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611013726_SingularTableNames'
)
BEGIN
    ALTER TABLE [TrainingItem] ADD CONSTRAINT [PK_TrainingItem] PRIMARY KEY ([Id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611013726_SingularTableNames'
)
BEGIN
    ALTER TABLE [Session] ADD CONSTRAINT [PK_Session] PRIMARY KEY ([Id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611013726_SingularTableNames'
)
BEGIN
    ALTER TABLE [MediaAsset] ADD CONSTRAINT [PK_MediaAsset] PRIMARY KEY ([Id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611013726_SingularTableNames'
)
BEGIN
    ALTER TABLE [ItemResponse] ADD CONSTRAINT [PK_ItemResponse] PRIMARY KEY ([Id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611013726_SingularTableNames'
)
BEGIN
    ALTER TABLE [Category] ADD CONSTRAINT [PK_Category] PRIMARY KEY ([Id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611013726_SingularTableNames'
)
BEGIN
    ALTER TABLE [AuditLog] ADD CONSTRAINT [PK_AuditLog] PRIMARY KEY ([Id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611013726_SingularTableNames'
)
BEGIN
    ALTER TABLE [Attempt] ADD CONSTRAINT [PK_Attempt] PRIMARY KEY ([Id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611013726_SingularTableNames'
)
BEGIN
    ALTER TABLE [Assignment] ADD CONSTRAINT [PK_Assignment] PRIMARY KEY ([Id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611013726_SingularTableNames'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260611013726_SingularTableNames', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611014245_Fase4Recurrencia'
)
BEGIN
    ALTER TABLE [Training] ADD [RecurrenceMonths] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611014245_Fase4Recurrencia'
)
BEGIN
    ALTER TABLE [Training] ADD [RenewLeadDays] int NOT NULL DEFAULT 30;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611014245_Fase4Recurrencia'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260611014245_Fase4Recurrencia', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611055157_Fase5Tiempo'
)
BEGIN
    ALTER TABLE [Attempt] ADD [ActiveSeconds] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611055157_Fase5Tiempo'
)
BEGIN
    ALTER TABLE [Attempt] ADD [LastHeartbeatAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611055157_Fase5Tiempo'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260611055157_Fase5Tiempo', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721213944_Fase6Certificados'
)
BEGIN
    ALTER TABLE [Training] ADD [CertificateConfigJson] nvarchar(max) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721213944_Fase6Certificados'
)
BEGIN
    CREATE TABLE [Certificate] (
        [Id] uniqueidentifier NOT NULL,
        [Serial] nvarchar(450) NOT NULL,
        [AttemptId] uniqueidentifier NOT NULL,
        [TrainingId] uniqueidentifier NOT NULL,
        [TrainingVersionId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NULL,
        [LearnerName] nvarchar(max) NOT NULL,
        [TrainingTitle] nvarchar(max) NOT NULL,
        [ScorePercent] int NOT NULL,
        [PassPercent] int NOT NULL,
        [IssuedAt] datetime2 NOT NULL,
        [ExpiresAt] datetime2 NULL,
        [ConfigSnapshotJson] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_Certificate] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721213944_Fase6Certificados'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Certificate_AttemptId] ON [Certificate] ([AttemptId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721213944_Fase6Certificados'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Certificate_Serial] ON [Certificate] ([Serial]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721213944_Fase6Certificados'
)
BEGIN
    CREATE INDEX [IX_Certificate_UserId] ON [Certificate] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721213944_Fase6Certificados'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260721213944_Fase6Certificados', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824161559_AddPlayerConfigAndNotificationLog'
)
BEGIN
    ALTER TABLE [Training] ADD [PlayerConfigJson] nvarchar(max) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824161559_AddPlayerConfigAndNotificationLog'
)
BEGIN
    CREATE TABLE [NotificationLog] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [TrainingId] uniqueidentifier NOT NULL,
        [Kind] nvarchar(450) NOT NULL,
        [SentAt] datetime2 NOT NULL,
        CONSTRAINT [PK_NotificationLog] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824161559_AddPlayerConfigAndNotificationLog'
)
BEGIN
    CREATE UNIQUE INDEX [IX_NotificationLog_UserId_TrainingId_Kind] ON [NotificationLog] ([UserId], [TrainingId], [Kind]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824161559_AddPlayerConfigAndNotificationLog'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260824161559_AddPlayerConfigAndNotificationLog', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824193735_AddCertificationRecordAndFixedExpiry'
)
BEGIN
    ALTER TABLE [Training] ADD [ExpiresOn] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824193735_AddCertificationRecordAndFixedExpiry'
)
BEGIN
    ALTER TABLE [Training] ADD [NotificationConfigJson] nvarchar(max) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824193735_AddCertificationRecordAndFixedExpiry'
)
BEGIN
    CREATE TABLE [ExternalCertification] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Title] nvarchar(max) NOT NULL,
        [Issuer] nvarchar(max) NULL,
        [CredentialId] nvarchar(max) NULL,
        [IssuedOn] datetime2 NOT NULL,
        [ExpiresOn] datetime2 NULL,
        [MediaAssetId] uniqueidentifier NULL,
        [ExternalSource] nvarchar(450) NULL,
        [ExternalRef] nvarchar(450) NULL,
        [ExternalUrl] nvarchar(max) NULL,
        [Notes] nvarchar(max) NULL,
        [CreatedByUserId] uniqueidentifier NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ExternalCertification] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824193735_AddCertificationRecordAndFixedExpiry'
)
BEGIN
    CREATE INDEX [IX_ExternalCertification_ExternalSource_ExternalRef] ON [ExternalCertification] ([ExternalSource], [ExternalRef]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824193735_AddCertificationRecordAndFixedExpiry'
)
BEGIN
    CREATE INDEX [IX_ExternalCertification_UserId] ON [ExternalCertification] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824193735_AddCertificationRecordAndFixedExpiry'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260824193735_AddCertificationRecordAndFixedExpiry', N'8.0.8');
END;
GO

COMMIT;
GO

