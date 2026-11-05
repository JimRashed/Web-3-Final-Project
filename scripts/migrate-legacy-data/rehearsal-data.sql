-- Synthetic legacy data for the cutover rehearsal. Every identity here is invented; nothing is real.
-- Users: alice and bob are mapped in rehearsal-mapping.csv; carol is deliberately unmapped.
INSERT dbo.Users (Id, Username, PasswordHash) VALUES
 ('11111111-1111-1111-1111-111111111111', N'alice', N'legacy-hash-never-copied'),
 ('22222222-2222-2222-2222-222222222222', N'bob', N'legacy-hash-never-copied'),
 ('33333333-3333-3333-3333-333333333333', N'carol', N'legacy-hash-never-copied');

-- Results: alice has a historical score above 5 (kept); carol's result has no mapping (quarantined).
SET IDENTITY_INSERT dbo.MatchResults ON;
INSERT dbo.MatchResults (Id, MatchId, UserId, Score, Won, PlayedAt) VALUES
 (1, 'AAAAAAAA-0000-0000-0000-000000000001', '11111111-1111-1111-1111-111111111111', 3, 1, '2026-09-20T10:00:00'),
 (2, 'AAAAAAAA-0000-0000-0000-000000000002', '11111111-1111-1111-1111-111111111111', 7, 1, '2026-09-21T10:00:00'),
 (3, 'AAAAAAAA-0000-0000-0000-000000000002', '22222222-2222-2222-2222-222222222222', 2, 0, '2026-09-21T10:00:00'),
 (4, 'AAAAAAAA-0000-0000-0000-000000000003', '33333333-3333-3333-3333-333333333333', 1, 0, '2026-09-22T10:00:00');
SET IDENTITY_INSERT dbo.MatchResults OFF;

-- Preferences reference players by GUID text, by exact username, and one unmapped player.
SET IDENTITY_INSERT dbo.UserPrefs ON;
INSERT dbo.UserPrefs (Id, UserId, EmailEnabled, PushEnabled) VALUES
 (1, '11111111-1111-1111-1111-111111111111', 1, 0),
 (2, N'bob', 0, 1),
 (3, N'carol', 1, 1);
SET IDENTITY_INSERT dbo.UserPrefs OFF;

-- Events: a valid created/completed pair, an unknown type, and a payload that is not JSON.
SET IDENTITY_INSERT dbo.EventLogs ON;
INSERT dbo.EventLogs (Id, Type, UserId, PayloadJson, CreatedAt) VALUES
 (1, N'MATCH_CREATED', N'22222222-2222-2222-2222-222222222222', N'{"matchId":"legacy-match-1"}', '2026-09-21T09:59:00'),
 (2, N'MATCH_COMPLETED', N'bob', N'{"matchId":"legacy-match-1","winner":"11111111-1111-1111-1111-111111111111"}', '2026-09-21T10:00:00'),
 (3, N'LEGACY_PING', N'alice', N'{}', '2026-09-21T10:01:00'),
 (4, N'MATCH_CREATED', N'alice', N'not json', '2026-09-21T10:02:00');
SET IDENTITY_INSERT dbo.EventLogs OFF;

-- Catalog without options, and transient lobby/match state.
INSERT dbo.Categories (Id, Name) VALUES ('C0000000-0000-0000-0000-000000000001', N'Science');
INSERT dbo.Questions (Id, Prompt, CorrectAnswer, CategoryId) VALUES
 ('D0000000-0000-0000-0000-000000000001', N'Red planet?', N'Mars', 'C0000000-0000-0000-0000-000000000001'),
 ('D0000000-0000-0000-0000-000000000002', N'H2O is?', N'Water', 'C0000000-0000-0000-0000-000000000001');
INSERT dbo.ActiveLobbies (Id, JoinCode, HostId, Status) VALUES ('E0000000-0000-0000-0000-000000000001', N'ABC123', N'alice', N'Open');
INSERT dbo.LobbyPlayers (Id, LobbyId, PlayerId) VALUES
 ('F0000000-0000-0000-0000-000000000001', 'E0000000-0000-0000-0000-000000000001', N'alice'),
 ('F0000000-0000-0000-0000-000000000002', 'E0000000-0000-0000-0000-000000000001', N'bob');
INSERT dbo.ActiveMatches (MatchId, PlayerAId, PlayerBId, Status, PlayerAScore, PlayerBScore, CurrentQuestionIndex, CreatedAt)
VALUES (N'legacy-match-2', N'alice', N'bob', N'Active', 1, 0, 1, '2026-09-23T10:00:00');
SET IDENTITY_INSERT dbo.PlayerStates ON;
INSERT dbo.PlayerStates (Id, MatchId, UserId, Score) VALUES (1, N'legacy-match-2', N'alice', 1), (2, N'legacy-match-2', N'bob', 0);
SET IDENTITY_INSERT dbo.PlayerStates OFF;
