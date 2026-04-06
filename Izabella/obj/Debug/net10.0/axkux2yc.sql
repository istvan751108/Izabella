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
CREATE TABLE [LiquidManures] (
    [Id] int NOT NULL IDENTITY,
    [Date] datetime2 NOT NULL,
    [TotalAmount] float NOT NULL,
    [Cow] float NOT NULL,
    [Young6_9] float NOT NULL,
    [Young9_12] float NOT NULL,
    [Young12Preg] float NOT NULL,
    [PregnantHeifer] float NOT NULL,
    [VoucherIn] nvarchar(max) NOT NULL,
    [VoucherOut] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_LiquidManures] PRIMARY KEY ([Id])
);

CREATE TABLE [SolidManureDailies] (
    [Id] int NOT NULL IDENTITY,
    [Date] datetime2 NOT NULL,
    [TotalNet] float NOT NULL,
    [VoucherIn] nvarchar(max) NOT NULL,
    [VoucherOut] nvarchar(max) NOT NULL,
    [Cow] float NOT NULL,
    [CalfMilk] float NOT NULL,
    [Calf3_6] float NOT NULL,
    [Young6_9] float NOT NULL,
    [Young9_12] float NOT NULL,
    [PregnantHeifer] float NOT NULL,
    CONSTRAINT [PK_SolidManureDailies] PRIMARY KEY ([Id])
);

CREATE TABLE [SolidManureLoads] (
    [Id] int NOT NULL IDENTITY,
    [Date] datetime2 NOT NULL,
    [LicensePlate] nvarchar(max) NOT NULL,
    [GrossWeight] float NOT NULL,
    [TareWeight] float NOT NULL,
    [NetWeight] float NOT NULL,
    [Destination] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_SolidManureLoads] PRIMARY KEY ([Id])
);

CREATE TABLE [SolidManures] (
    [Id] int NOT NULL IDENTITY,
    [Date] datetime2 NOT NULL,
    [Gross] float NOT NULL,
    [Tare] float NOT NULL,
    [Net] float NOT NULL,
    [Cow] float NOT NULL,
    [CalfMilk] float NOT NULL,
    [Calf3_6] float NOT NULL,
    [Young6_9] float NOT NULL,
    [Young9_12] float NOT NULL,
    [PregnantHeifer] float NOT NULL,
    [VoucherIn] nvarchar(max) NOT NULL,
    [VoucherOut] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_SolidManures] PRIMARY KEY ([Id])
);

CREATE TABLE [Vouchers] (
    [Id] int NOT NULL IDENTITY,
    [Year] int NOT NULL,
    [Warehouse] int NOT NULL,
    [SequenceNumber] int NOT NULL,
    [Type] nvarchar(max) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Vouchers] PRIMARY KEY ([Id])
);

CREATE TABLE [LiquidManureSplits] (
    [Id] int NOT NULL IDENTITY,
    [LiquidManureId] int NOT NULL,
    [Amount] float NOT NULL,
    [VoucherNumber] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_LiquidManureSplits] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_LiquidManureSplits_LiquidManures_LiquidManureId] FOREIGN KEY ([LiquidManureId]) REFERENCES [LiquidManures] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_LiquidManureSplits_LiquidManureId] ON [LiquidManureSplits] ([LiquidManureId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260327165206_InitialAll', N'10.0.5');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [AspNetRoles] (
    [Id] nvarchar(450) NOT NULL,
    [Name] nvarchar(256) NULL,
    [NormalizedName] nvarchar(256) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
);

CREATE TABLE [AspNetUsers] (
    [Id] nvarchar(450) NOT NULL,
    [UserName] nvarchar(256) NULL,
    [NormalizedUserName] nvarchar(256) NULL,
    [Email] nvarchar(256) NULL,
    [NormalizedEmail] nvarchar(256) NULL,
    [EmailConfirmed] bit NOT NULL,
    [PasswordHash] nvarchar(max) NULL,
    [SecurityStamp] nvarchar(max) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    [PhoneNumber] nvarchar(max) NULL,
    [PhoneNumberConfirmed] bit NOT NULL,
    [TwoFactorEnabled] bit NOT NULL,
    [LockoutEnd] datetimeoffset NULL,
    [LockoutEnabled] bit NOT NULL,
    [AccessFailedCount] int NOT NULL,
    CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
);

CREATE TABLE [AspNetRoleClaims] (
    [Id] int NOT NULL IDENTITY,
    [RoleId] nvarchar(450) NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserClaims] (
    [Id] int NOT NULL IDENTITY,
    [UserId] nvarchar(450) NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserLogins] (
    [LoginProvider] nvarchar(128) NOT NULL,
    [ProviderKey] nvarchar(128) NOT NULL,
    [ProviderDisplayName] nvarchar(max) NULL,
    [UserId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
    CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserRoles] (
    [UserId] nvarchar(450) NOT NULL,
    [RoleId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
    CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserTokens] (
    [UserId] nvarchar(450) NOT NULL,
    [LoginProvider] nvarchar(128) NOT NULL,
    [Name] nvarchar(128) NOT NULL,
    [Value] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
    CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);

CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL;

CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);

CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);

CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);

CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);

CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260329113310_IdentityTablesFix', N'10.0.5');

COMMIT;
GO

BEGIN TRANSACTION;
INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260329171421_InitialFix', N'10.0.5');

COMMIT;
GO

BEGIN TRANSACTION;
INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260329171645_InitialIdentity', N'10.0.5');

COMMIT;
GO

BEGIN TRANSACTION;
INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260329172211_Init', N'10.0.5');

COMMIT;
GO

BEGIN TRANSACTION;
INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260402161528_IdentityTables', N'10.0.5');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [Cattles] (
    [Id] int NOT NULL IDENTITY,
    [EarTag] nvarchar(max) NOT NULL,
    [EnarNumber] nvarchar(max) NOT NULL,
    [PassportNumber] nvarchar(7) NOT NULL,
    [PassportSequence] int NOT NULL,
    [HerdCode] nvarchar(max) NOT NULL,
    [BirthDate] datetime2 NOT NULL,
    [BirthWeight] float NOT NULL,
    [Gender] int NOT NULL,
    [MotherEnar] nvarchar(max) NULL,
    [FatherKlsz] nvarchar(max) NULL,
    [ExitDate] datetime2 NULL,
    [ExitType] int NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_Cattles] PRIMARY KEY ([Id])
);

CREATE TABLE [BreedingDatas] (
    [Id] int NOT NULL IDENTITY,
    [CattleId] int NOT NULL,
    [LastInseminationDate] datetime2 NULL,
    [SireKlsz] nvarchar(max) NULL,
    [PregnancyTestDate] datetime2 NULL,
    [IsPregnant] bit NULL,
    [AbortionDate] datetime2 NULL,
    CONSTRAINT [PK_BreedingDatas] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_BreedingDatas_Cattles_CattleId] FOREIGN KEY ([CattleId]) REFERENCES [Cattles] ([Id]) ON DELETE CASCADE
);

CREATE UNIQUE INDEX [IX_BreedingDatas_CattleId] ON [BreedingDatas] ([CattleId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260402173328_CattleModuleInit', N'10.0.5');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [Cattles] ADD [CompanyId] int NOT NULL DEFAULT 0;

ALTER TABLE [Cattles] ADD [DamAgeAtCalving] nvarchar(max) NULL;

ALTER TABLE [Cattles] ADD [IsAlive] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [Cattles] ADD [IsTwin] bit NOT NULL DEFAULT CAST(0 AS bit);

CREATE TABLE [Companies] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NOT NULL,
    [HerdCode] nvarchar(max) NOT NULL,
    [DefaultPrefix] nvarchar(max) NOT NULL,
    [EnarPrefix] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_Companies] PRIMARY KEY ([Id])
);

CREATE INDEX [IX_Cattles_CompanyId] ON [Cattles] ([CompanyId]);

ALTER TABLE [Cattles] ADD CONSTRAINT [FK_Cattles_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE CASCADE;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260402180739_CompanyAndCattleExtension', N'10.0.5');

COMMIT;
GO

BEGIN TRANSACTION;
EXEC sp_rename N'[Cattles].[HerdCode]', N'AgeGroup', 'COLUMN';

DECLARE @var nvarchar(max);
SELECT @var = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Companies]') AND [c].[name] = N'EnarPrefix');
IF @var IS NOT NULL EXEC(N'ALTER TABLE [Companies] DROP CONSTRAINT ' + @var + ';');
ALTER TABLE [Companies] ALTER COLUMN [EnarPrefix] nvarchar(max) NULL;

DECLARE @var1 nvarchar(max);
SELECT @var1 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Companies]') AND [c].[name] = N'DefaultPrefix');
IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Companies] DROP CONSTRAINT ' + @var1 + ';');
ALTER TABLE [Companies] ALTER COLUMN [DefaultPrefix] nvarchar(max) NULL;

ALTER TABLE [Cattles] ADD [CurrentHerdId] int NOT NULL DEFAULT 0;

CREATE TABLE [Herds] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NOT NULL,
    [HerdCode] nvarchar(max) NOT NULL,
    [CompanyId] int NOT NULL,
    [DefaultPrefix] nvarchar(max) NULL,
    [EnarPrefix] nvarchar(max) NULL,
    CONSTRAINT [PK_Herds] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Herds_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION
);

CREATE INDEX [IX_Cattles_CurrentHerdId] ON [Cattles] ([CurrentHerdId]);

CREATE INDEX [IX_Herds_CompanyId] ON [Herds] ([CompanyId]);

ALTER TABLE [Cattles] ADD CONSTRAINT [FK_Cattles_Herds_CurrentHerdId] FOREIGN KEY ([CurrentHerdId]) REFERENCES [Herds] ([Id]) ON DELETE NO ACTION;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260403081748_FinalFixForHerdRelation', N'10.0.5');

COMMIT;
GO

BEGIN TRANSACTION;
DECLARE @var2 nvarchar(max);
SELECT @var2 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Companies]') AND [c].[name] = N'DefaultPrefix');
IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [Companies] DROP CONSTRAINT ' + @var2 + ';');
ALTER TABLE [Companies] DROP COLUMN [DefaultPrefix];

DECLARE @var3 nvarchar(max);
SELECT @var3 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Companies]') AND [c].[name] = N'EnarPrefix');
IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [Companies] DROP CONSTRAINT ' + @var3 + ';');
ALTER TABLE [Companies] DROP COLUMN [EnarPrefix];

DECLARE @var4 nvarchar(max);
SELECT @var4 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Companies]') AND [c].[name] = N'HerdCode');
IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [Companies] DROP CONSTRAINT ' + @var4 + ';');
ALTER TABLE [Companies] DROP COLUMN [HerdCode];

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260403085108_CleanCompanyTable', N'10.0.5');

COMMIT;
GO

BEGIN TRANSACTION;
INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260403192734_IncreasePassportNumberLength', N'10.0.5');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [DeathLogs] (
    [Id] int NOT NULL IDENTITY,
    [CattleId] int NOT NULL,
    [DeathDate] datetime2 NOT NULL,
    [Reason] nvarchar(max) NOT NULL,
    [EstimatedWeight] float NOT NULL,
    [EarTagAtDeath] nvarchar(max) NOT NULL,
    [EnarNumberAtDeath] nvarchar(max) NOT NULL,
    [IsEnarReported] bit NOT NULL,
    CONSTRAINT [PK_DeathLogs] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_DeathLogs_Cattles_CattleId] FOREIGN KEY ([CattleId]) REFERENCES [Cattles] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [DeathReasons] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_DeathReasons] PRIMARY KEY ([Id])
);

CREATE INDEX [IX_DeathLogs_CattleId] ON [DeathLogs] ([CattleId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260404152501_AddDeathHandling', N'10.0.5');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [DeathLogs] ADD [IsTransported] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [DeathLogs] ADD [TransportDate] datetime2 NULL;

ALTER TABLE [DeathLogs] ADD [TransportReceiptNumber] nvarchar(max) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260405083620_AddTransportTrackingToDeathLog', N'10.0.5');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [DeathLogs] ADD [IsPassportSent] bit NOT NULL DEFAULT CAST(0 AS bit);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260405093411_AddPassportTracking', N'10.0.5');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [Cattles] ADD [BreedCode] int NOT NULL DEFAULT 22;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260406064944_AddBreedCodeToCattle', N'10.0.5');

COMMIT;
GO

