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
CREATE TABLE [ActiveLobbies] (
    [Id] uniqueidentifier NOT NULL,
    [JoinCode] nvarchar(450) NOT NULL,
    [HostId] nvarchar(max) NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_ActiveLobbies] PRIMARY KEY ([Id])
);

CREATE TABLE [ActiveMatches] (
    [MatchId] nvarchar(100) NOT NULL,
    [PlayerAId] nvarchar(max) NOT NULL,
    [PlayerBId] nvarchar(max) NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    [PlayerAScore] int NOT NULL,
    [PlayerBScore] int NOT NULL,
    [CurrentQuestionIndex] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_ActiveMatches] PRIMARY KEY ([MatchId])
);

CREATE TABLE [Categories] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_Categories] PRIMARY KEY ([Id])
);

CREATE TABLE [EventLogs] (
    [Id] int NOT NULL IDENTITY,
    [Type] nvarchar(max) NOT NULL,
    [UserId] nvarchar(450) NOT NULL,
    [PayloadJson] nvarchar(max) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_EventLogs] PRIMARY KEY ([Id])
);

CREATE TABLE [MatchResults] (
    [Id] int NOT NULL IDENTITY,
    [MatchId] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [Score] int NOT NULL,
    [Won] bit NOT NULL,
    [PlayedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_MatchResults] PRIMARY KEY ([Id])
);

CREATE TABLE [UserPrefs] (
    [Id] int NOT NULL IDENTITY,
    [UserId] nvarchar(450) NOT NULL,
    [EmailEnabled] bit NOT NULL,
    [PushEnabled] bit NOT NULL,
    CONSTRAINT [PK_UserPrefs] PRIMARY KEY ([Id])
);

CREATE TABLE [Users] (
    [Id] uniqueidentifier NOT NULL,
    [Username] nvarchar(50) NOT NULL,
    [PasswordHash] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
);

CREATE TABLE [LobbyPlayers] (
    [Id] uniqueidentifier NOT NULL,
    [LobbyId] uniqueidentifier NOT NULL,
    [PlayerId] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_LobbyPlayers] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_LobbyPlayers_ActiveLobbies_LobbyId] FOREIGN KEY ([LobbyId]) REFERENCES [ActiveLobbies] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [PlayerStates] (
    [Id] int NOT NULL IDENTITY,
    [MatchId] nvarchar(100) NOT NULL,
    [UserId] nvarchar(450) NOT NULL,
    [Score] int NOT NULL,
    CONSTRAINT [PK_PlayerStates] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PlayerStates_ActiveMatches_MatchId] FOREIGN KEY ([MatchId]) REFERENCES [ActiveMatches] ([MatchId]) ON DELETE CASCADE
);

CREATE TABLE [Questions] (
    [Id] uniqueidentifier NOT NULL,
    [Prompt] nvarchar(max) NOT NULL,
    [CorrectAnswer] nvarchar(max) NOT NULL,
    [CategoryId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_Questions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Questions_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id]) ON DELETE NO ACTION
);

CREATE UNIQUE INDEX [IX_ActiveLobbies_JoinCode] ON [ActiveLobbies] ([JoinCode]);

CREATE INDEX [IX_EventLogs_UserId] ON [EventLogs] ([UserId]);

CREATE INDEX [IX_LobbyPlayers_LobbyId] ON [LobbyPlayers] ([LobbyId]);

CREATE UNIQUE INDEX [IX_MatchResults_MatchId_UserId] ON [MatchResults] ([MatchId], [UserId]);

CREATE INDEX [IX_MatchResults_UserId] ON [MatchResults] ([UserId]);

CREATE UNIQUE INDEX [IX_PlayerStates_MatchId_UserId] ON [PlayerStates] ([MatchId], [UserId]);

CREATE INDEX [IX_Questions_CategoryId] ON [Questions] ([CategoryId]);

CREATE UNIQUE INDEX [IX_UserPrefs_UserId] ON [UserPrefs] ([UserId]);

CREATE UNIQUE INDEX [IX_Users_Username] ON [Users] ([Username]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260924171427_InitialCreate', N'10.0.12');

COMMIT;
GO

