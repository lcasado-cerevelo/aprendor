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

