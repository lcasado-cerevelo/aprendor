BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824224006_AddCompanyTwoFactorPolicyAndMemberships'
)
BEGIN
    ALTER TABLE [Tenant] ADD [TwoFactorPolicy] nvarchar(max) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824224006_AddCompanyTwoFactorPolicyAndMemberships'
)
BEGIN
    CREATE TABLE [UserCompany] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [Role] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_UserCompany] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824224006_AddCompanyTwoFactorPolicyAndMemberships'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UserCompany_UserId_TenantId] ON [UserCompany] ([UserId], [TenantId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824224006_AddCompanyTwoFactorPolicyAndMemberships'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260824224006_AddCompanyTwoFactorPolicyAndMemberships', N'8.0.8');
END;
GO

COMMIT;
GO

