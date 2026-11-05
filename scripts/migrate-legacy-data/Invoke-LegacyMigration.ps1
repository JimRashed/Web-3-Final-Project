<#
.SYNOPSIS
  Moves owned data from the Milestone 2 QuizArenaDb into the six service databases (D02, issue #63).

.DESCRIPTION
  Modes, run in this order (see docs/milestone3/data-cutover.md):
    Backup   COPY_ONLY backup of the source with CHECKSUM, then RESTORE VERIFYONLY.
    DryRun   Plans every source row and prints what Import would do. Writes nothing anywhere.
    Import   Applies the plan in one transaction and records every row in the ledger database.
    Verify   Reconciles: source = imported + already present + quarantined + archived, and imported rows exist.
    Rollback Removes the rows a given run imported, only where the target row is still unchanged.

  The source database is only ever read. Identities are mapped exclusively from a verified mapping file
  (legacy user id -> issuer + subject). Unmapped, invalid or conflicting rows are quarantined with their
  source data kept in the ledger, never dropped or guessed. Passwords are never read. Transient lobby and
  match state is archived, not resumed. Rerunning Import creates no duplicates.

.EXAMPLE
  ./Invoke-LegacyMigration.ps1 -Mode Backup -BackupDirectory C:\backups
  ./Invoke-LegacyMigration.ps1 -Mode DryRun -MappingFile .\identity-mapping.csv
  ./Invoke-LegacyMigration.ps1 -Mode Import -MappingFile .\identity-mapping.csv
  ./Invoke-LegacyMigration.ps1 -Mode Verify
  ./Invoke-LegacyMigration.ps1 -Mode Rollback -RunId <guid from Import>
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Backup', 'DryRun', 'Import', 'Verify', 'Rollback')] [string] $Mode,
    [string] $Server = '(localdb)\MSSQLLocalDB',
    [string] $ConnectionOptions = 'Integrated Security=true;TrustServerCertificate=true',
    [string] $SourceDatabase = 'QuizArenaDb',
    [string] $TargetPrefix = '',
    [string] $LedgerDatabase = 'QuizArenaMigrationDb',
    [string] $MappingFile,
    [string] $BackupDirectory,
    [guid] $RunId = [guid]::Empty
)

$ErrorActionPreference = 'Stop'
$names = @{
    Source = $SourceDatabase; Ledger = $LedgerDatabase
    Identity = "$($TargetPrefix)QuizArenaIdentityDb"; Statistics = "$($TargetPrefix)QuizArenaStatisticsDb"
    Notification = "$($TargetPrefix)QuizArenaNotificationDb"
}
foreach ($name in $names.Values) { if ($name -notmatch '^[A-Za-z0-9_]+$') { throw "Unsafe database name '$name'." } }

$connection = New-Object System.Data.SqlClient.SqlConnection "Server=$Server;Database=master;$ConnectionOptions"
$connection.Open()

function Sql([string] $text, [hashtable] $parameters = @{}, [System.Data.SqlClient.SqlTransaction] $transaction = $null) {
    $command = $connection.CreateCommand()
    $command.CommandTimeout = 600
    $command.Transaction = $transaction
    $command.CommandText = $text.Replace('{SRC}', "[$($names.Source)]").Replace('{LEDGER}', "[$($names.Ledger)]").
        Replace('{IDENTITY}', "[$($names.Identity)]").Replace('{STATISTICS}', "[$($names.Statistics)]").
        Replace('{NOTIFICATION}', "[$($names.Notification)]")
    foreach ($key in $parameters.Keys) {
        $value = $parameters[$key]
        if ($null -eq $value) { $value = [DBNull]::Value }
        [void] $command.Parameters.AddWithValue("@$key", $value)
    }
    $table = New-Object System.Data.DataTable
    $reader = $command.ExecuteReader()
    try { $table.Load($reader) } finally { $reader.Close() }
    return , $table
}

function Exists([string] $database) { (Sql 'SELECT DB_ID(@n) AS id' @{ n = $database }).Rows[0].id -isnot [DBNull] }
function Show($table) { $table | Format-Table -AutoSize | Out-String -Width 220 | Write-Host }

if (-not (Exists $names.Source)) { throw "Source database $($names.Source) was not found on $Server." }

# ------------------------------------------------------------------------------------------- Backup
if ($Mode -eq 'Backup') {
    if (-not $BackupDirectory) { throw '-BackupDirectory is required for Backup.' }
    New-Item -ItemType Directory -Force $BackupDirectory | Out-Null
    $file = Join-Path (Resolve-Path $BackupDirectory) ("$($names.Source)-{0:yyyyMMdd-HHmmss}.bak" -f (Get-Date).ToUniversalTime())
    Sql "BACKUP DATABASE {SRC} TO DISK = @f WITH COPY_ONLY, CHECKSUM, INIT" @{ f = $file } | Out-Null
    Sql 'RESTORE VERIFYONLY FROM DISK = @f WITH CHECKSUM' @{ f = $file } | Out-Null
    Write-Host "Backup written and verified: $file"
    return
}

# ------------------------------------------------------------------------------------ Ledger schema
if (-not (Exists $names.Ledger)) { Sql "CREATE DATABASE [$($names.Ledger)] COLLATE Latin1_General_100_BIN2" | Out-Null }
Sql @'
IF OBJECT_ID('{LEDGER}.dbo.Runs') IS NULL
CREATE TABLE {LEDGER}.dbo.Runs (
    RunId uniqueidentifier NOT NULL PRIMARY KEY, Mode nvarchar(16) NOT NULL, SourceDatabase sysname NOT NULL,
    MappingSha256 char(64) NULL, StartedAt datetime2 NOT NULL, FinishedAt datetime2 NULL, Summary nvarchar(max) NULL);
IF OBJECT_ID('{LEDGER}.dbo.Ledger') IS NULL
CREATE TABLE {LEDGER}.dbo.Ledger (
    SourceTable nvarchar(64) NOT NULL, SourceKey nvarchar(450) NOT NULL,
    State nvarchar(20) NOT NULL, Reason nvarchar(400) NULL,
    TargetDatabase sysname NULL, TargetTable sysname NULL, TargetKey nvarchar(450) NULL, TargetJson nvarchar(max) NULL,
    SourceJson nvarchar(max) NOT NULL, RunId uniqueidentifier NOT NULL, RecordedAt datetime2 NOT NULL,
    CONSTRAINT PK_Ledger PRIMARY KEY (SourceTable, SourceKey));
IF OBJECT_ID('{LEDGER}.dbo.IdentityMapping') IS NULL
CREATE TABLE {LEDGER}.dbo.IdentityMapping (
    LegacyUserId nvarchar(36) NOT NULL, Issuer nvarchar(256) NULL, Subject nvarchar(200) NULL,
    VerifiedBy nvarchar(200) NULL, ProofReference nvarchar(400) NULL, Valid bit NOT NULL, Reason nvarchar(400) NULL,
    RunId uniqueidentifier NOT NULL, RecordedAt datetime2 NOT NULL);
'@ | Out-Null

# ---------------------------------------------------------------------------------------- Rollback
if ($Mode -eq 'Rollback') {
    if ($RunId -eq [guid]::Empty) { throw '-RunId is required for Rollback (printed by Import).' }
    $transaction = $connection.BeginTransaction()
    try {
        $result = Sql @'
DECLARE @removed TABLE (SourceTable nvarchar(64) COLLATE Latin1_General_100_BIN2, SourceKey nvarchar(450) COLLATE Latin1_General_100_BIN2);
-- Profiles: only if nobody changed them since import (UpdatedAt still equals CreatedAt and the name is unchanged).
DELETE p OUTPUT 'Users', l.SourceKey INTO @removed
FROM {IDENTITY}.dbo.UserProfiles p JOIN {LEDGER}.dbo.Ledger l ON l.SourceTable = 'Users' AND l.State = 'Imported' AND l.RunId = @run
 AND p.Id = TRY_CONVERT(uniqueidentifier, l.TargetKey)
WHERE p.UpdatedAt = p.CreatedAt AND p.DisplayName COLLATE Latin1_General_100_BIN2 = JSON_VALUE(l.TargetJson, '$.DisplayName') COLLATE Latin1_General_100_BIN2;
-- Results are immutable once recorded.
DELETE r OUTPUT 'MatchResults', l.SourceKey INTO @removed
FROM {STATISTICS}.dbo.MatchResults r JOIN {LEDGER}.dbo.Ledger l ON l.SourceTable = 'MatchResults' AND l.State = 'Imported' AND l.RunId = @run
 AND r.Issuer = JSON_VALUE(l.TargetJson, '$.Issuer') AND r.MatchId = JSON_VALUE(l.TargetJson, '$.MatchId') AND r.UserId = JSON_VALUE(l.TargetJson, '$.UserId');
-- Preferences: only if the player has not changed them since.
DELETE p OUTPUT 'UserPrefs', l.SourceKey INTO @removed
FROM {NOTIFICATION}.dbo.UserPreferences p JOIN {LEDGER}.dbo.Ledger l ON l.SourceTable = 'UserPrefs' AND l.State = 'Imported' AND l.RunId = @run
 AND p.Issuer = JSON_VALUE(l.TargetJson, '$.Issuer') AND p.Subject = JSON_VALUE(l.TargetJson, '$.Subject')
WHERE p.EmailEnabled = CAST(JSON_VALUE(l.TargetJson, '$.EmailEnabled') AS bit) AND p.PushEnabled = CAST(JSON_VALUE(l.TargetJson, '$.PushEnabled') AS bit);
DELETE e OUTPUT 'EventLogs', l.SourceKey INTO @removed
FROM {NOTIFICATION}.dbo.Events e JOIN {LEDGER}.dbo.Ledger l ON l.SourceTable = 'EventLogs' AND l.State = 'Imported' AND l.RunId = @run
 AND e.EventId = TRY_CONVERT(uniqueidentifier, l.TargetKey);
UPDATE l SET State = 'RolledBack', RecordedAt = SYSUTCDATETIME()
FROM {LEDGER}.dbo.Ledger l JOIN @removed r ON r.SourceTable = l.SourceTable AND r.SourceKey = l.SourceKey;
SELECT l.SourceTable, SUM(CASE WHEN l.State = 'RolledBack' THEN 1 ELSE 0 END) AS RolledBack,
       SUM(CASE WHEN l.State = 'Imported' THEN 1 ELSE 0 END) AS KeptBecauseChanged
FROM {LEDGER}.dbo.Ledger l WHERE l.RunId = @run AND l.State IN ('RolledBack', 'Imported') GROUP BY l.SourceTable;
'@ @{ run = $RunId } $transaction
        $transaction.Commit()
    } catch { $transaction.Rollback(); throw }
    Write-Host "Rolled back run $RunId. Rows changed after import were kept; review them before re-importing."
    Show $result
    return
}

# ------------------------------------------------------------------------------------------ Verify
if ($Mode -eq 'Verify') {
    $report = Sql @'
WITH Source AS (
    SELECT 'Users' AS SourceTable, COUNT(*) AS SourceRows FROM {SRC}.dbo.Users UNION ALL
    SELECT 'MatchResults', COUNT(*) FROM {SRC}.dbo.MatchResults UNION ALL
    SELECT 'UserPrefs', COUNT(*) FROM {SRC}.dbo.UserPrefs UNION ALL
    SELECT 'EventLogs', COUNT(*) FROM {SRC}.dbo.EventLogs UNION ALL
    SELECT 'Questions', COUNT(*) FROM {SRC}.dbo.Questions UNION ALL
    SELECT 'Categories', COUNT(*) FROM {SRC}.dbo.Categories UNION ALL
    SELECT 'ActiveLobbies', COUNT(*) FROM {SRC}.dbo.ActiveLobbies UNION ALL
    SELECT 'LobbyPlayers', COUNT(*) FROM {SRC}.dbo.LobbyPlayers UNION ALL
    SELECT 'ActiveMatches', COUNT(*) FROM {SRC}.dbo.ActiveMatches UNION ALL
    SELECT 'PlayerStates', COUNT(*) FROM {SRC}.dbo.PlayerStates)
SELECT s.SourceTable, s.SourceRows,
       SUM(CASE WHEN l.State = 'Imported' THEN 1 ELSE 0 END) AS Imported,
       SUM(CASE WHEN l.State = 'AlreadyPresent' THEN 1 ELSE 0 END) AS AlreadyPresent,
       SUM(CASE WHEN l.State = 'Quarantined' THEN 1 ELSE 0 END) AS Quarantined,
       SUM(CASE WHEN l.State = 'Archived' THEN 1 ELSE 0 END) AS Archived,
       SUM(CASE WHEN l.State = 'RolledBack' THEN 1 ELSE 0 END) AS RolledBack,
       s.SourceRows - COUNT(l.SourceKey) AS Unaccounted
FROM Source s LEFT JOIN {LEDGER}.dbo.Ledger l ON l.SourceTable = s.SourceTable
GROUP BY s.SourceTable, s.SourceRows ORDER BY s.SourceTable;
'@
    $missing = Sql @'
SELECT l.SourceTable, COUNT(*) AS ImportedButMissing FROM {LEDGER}.dbo.Ledger l
WHERE l.State = 'Imported' AND NOT (
     (l.SourceTable = 'Users' AND EXISTS (SELECT 1 FROM {IDENTITY}.dbo.UserProfiles p WHERE p.Id = TRY_CONVERT(uniqueidentifier, l.TargetKey)))
  OR (l.SourceTable = 'MatchResults' AND EXISTS (SELECT 1 FROM {STATISTICS}.dbo.MatchResults r WHERE r.Issuer = JSON_VALUE(l.TargetJson, '$.Issuer')
        AND r.MatchId = JSON_VALUE(l.TargetJson, '$.MatchId') AND r.UserId = JSON_VALUE(l.TargetJson, '$.UserId')))
  OR (l.SourceTable = 'UserPrefs' AND EXISTS (SELECT 1 FROM {NOTIFICATION}.dbo.UserPreferences p WHERE p.Issuer = JSON_VALUE(l.TargetJson, '$.Issuer') AND p.Subject = JSON_VALUE(l.TargetJson, '$.Subject')))
  OR (l.SourceTable = 'EventLogs' AND EXISTS (SELECT 1 FROM {NOTIFICATION}.dbo.Events e WHERE e.EventId = TRY_CONVERT(uniqueidentifier, l.TargetKey))))
GROUP BY l.SourceTable;
'@
    $totals = Sql @'
-- Per mapped player: games, wins and score in the source equal the imported rows in Statistics.
SELECT JSON_VALUE(l.TargetJson, '$.UserId') AS Subject,
       COUNT(*) AS SourceGames, SUM(CAST(s.Won AS int)) AS SourceWins, SUM(s.Score) AS SourceScore,
       SUM(CASE WHEN r.Id IS NULL THEN 0 ELSE 1 END) AS TargetGames, SUM(CAST(ISNULL(r.Won, 0) AS int)) AS TargetWins, SUM(ISNULL(r.Score, 0)) AS TargetScore
FROM {LEDGER}.dbo.Ledger l
JOIN {SRC}.dbo.MatchResults s ON CONVERT(nvarchar(450), s.Id) = l.SourceKey
LEFT JOIN {STATISTICS}.dbo.MatchResults r ON r.Issuer = JSON_VALUE(l.TargetJson, '$.Issuer') AND r.MatchId = JSON_VALUE(l.TargetJson, '$.MatchId') AND r.UserId = JSON_VALUE(l.TargetJson, '$.UserId')
WHERE l.SourceTable = 'MatchResults' AND l.State IN ('Imported', 'AlreadyPresent')
GROUP BY JSON_VALUE(l.TargetJson, '$.UserId');
'@
    Write-Host 'Reconciliation (Unaccounted must be 0):'; Show $report
    $bad = @($report | Where-Object { $_.Unaccounted -ne 0 })
    if ($missing.Rows.Count -gt 0) { Write-Host 'Imported rows missing from their target:' -ForegroundColor Red; Show $missing }
    $mismatch = @($totals | Where-Object { $_.SourceGames -ne $_.TargetGames -or $_.SourceWins -ne $_.TargetWins -or $_.SourceScore -ne $_.TargetScore })
    Write-Host "Per-player Statistics totals checked: $($totals.Rows.Count), mismatched: $($mismatch.Count)"
    if ($bad.Count -or $missing.Rows.Count -or $mismatch.Count) { Write-Host 'VERIFY FAILED' -ForegroundColor Red; exit 1 }
    Write-Host 'VERIFY PASSED' -ForegroundColor Green
    return
}

# ------------------------------------------------------------------------------------ Plan (DryRun / Import)
if (-not $MappingFile) { throw '-MappingFile is required for DryRun and Import (see identity-mapping.example.csv).' }
foreach ($target in 'Identity', 'Statistics', 'Notification') {
    if (-not (Exists $names[$target])) { throw "Target database $($names[$target]) does not exist. Apply the service migrations first." }
}
$mappingPath = Resolve-Path $MappingFile
$mappingHash = (Get-FileHash $mappingPath -Algorithm SHA256).Hash.ToLowerInvariant()
$mapping = @(Import-Csv $mappingPath)
foreach ($column in 'LegacyUserId', 'Issuer', 'Subject', 'VerifiedBy', 'ProofReference') {
    if ($mapping.Count -and -not ($mapping[0].PSObject.Properties.Name -contains $column)) { throw "Mapping file is missing the column $column." }
}
if ($RunId -eq [guid]::Empty) { $RunId = [guid]::NewGuid() }
$cutover = (Get-Date).ToUniversalTime()

Sql 'IF OBJECT_ID(''tempdb..#Map'') IS NOT NULL DROP TABLE #Map; CREATE TABLE #Map (Row int, LegacyUserId nvarchar(64), Issuer nvarchar(400), Subject nvarchar(400), VerifiedBy nvarchar(400), ProofReference nvarchar(800), Valid bit NULL, Reason nvarchar(400) NULL);' | Out-Null
$row = 0
foreach ($entry in $mapping) {
    $row++
    Sql 'INSERT #Map (Row, LegacyUserId, Issuer, Subject, VerifiedBy, ProofReference) VALUES (@r, @u, @i, @s, @v, @p)' @{
        r = $row; u = $entry.LegacyUserId; i = $entry.Issuer; s = $entry.Subject; v = $entry.VerifiedBy; p = $entry.ProofReference } | Out-Null
}

# Session temp tables are created in a parameterless batch: a parameterised batch runs inside sp_executesql,
# and temp tables created there would be dropped when it returns.
Sql @'
IF OBJECT_ID('tempdb..#Ref') IS NOT NULL DROP TABLE #Ref;
CREATE TABLE #Ref (Ref nvarchar(450) COLLATE Latin1_General_100_BIN2 NOT NULL,
    Issuer nvarchar(400) COLLATE Latin1_General_100_BIN2 NOT NULL, Subject nvarchar(400) COLLATE Latin1_General_100_BIN2 NOT NULL);
IF OBJECT_ID('tempdb..#Plan') IS NOT NULL DROP TABLE #Plan;
CREATE TABLE #Plan (
    SourceTable nvarchar(64) COLLATE Latin1_General_100_BIN2 NOT NULL, SourceKey nvarchar(450) COLLATE Latin1_General_100_BIN2 NOT NULL,
    SourceJson nvarchar(max) COLLATE Latin1_General_100_BIN2 NOT NULL, State nvarchar(20) COLLATE Latin1_General_100_BIN2 NULL,
    Reason nvarchar(400) COLLATE Latin1_General_100_BIN2 NULL, TargetDatabase sysname NULL, TargetTable sysname NULL,
    TargetKey nvarchar(450) COLLATE Latin1_General_100_BIN2 NULL,
    -- typed target values
    -- identifiers use the services' binary collation so comparisons are exact and never conflict
    Id uniqueidentifier NULL, Issuer nvarchar(256) COLLATE Latin1_General_100_BIN2 NULL, Subject nvarchar(200) COLLATE Latin1_General_100_BIN2 NULL,
    DisplayName nvarchar(80) NULL, MatchId nvarchar(100) COLLATE Latin1_General_100_BIN2 NULL, Score int NULL, Won bit NULL, At datetime2 NULL,
    EmailEnabled bit NULL, PushEnabled bit NULL, EventType int NULL, WinnerUserId nvarchar(200) COLLATE Latin1_General_100_BIN2 NULL,
    PRIMARY KEY (SourceTable, SourceKey));
'@ | Out-Null

# Mapping rules: exact subject (never trimmed or lower-cased), required provenance, no duplicates, no silent merges.
Sql @'
UPDATE #Map SET LegacyUserId = LOWER(CONVERT(nvarchar(36), TRY_CONVERT(uniqueidentifier, LegacyUserId)));
UPDATE #Map SET Valid = 0, Reason = 'LegacyUserId is not a GUID' WHERE LegacyUserId IS NULL;
UPDATE #Map SET Valid = 0, Reason = 'Issuer, Subject, VerifiedBy and ProofReference are required'
WHERE Valid IS NULL AND (NULLIF(LTRIM(Issuer), '') IS NULL OR NULLIF(LTRIM(Subject), '') IS NULL OR NULLIF(LTRIM(VerifiedBy), '') IS NULL OR NULLIF(LTRIM(ProofReference), '') IS NULL);
UPDATE #Map SET Valid = 0, Reason = 'Subject is padded, longer than 200 or issuer longer than 256'
WHERE Valid IS NULL AND (Subject <> LTRIM(RTRIM(Subject)) OR LEN(Subject) > 200 OR LEN(Issuer) > 256);
UPDATE m SET Valid = 0, Reason = 'Legacy user listed more than once'
FROM #Map m WHERE Valid IS NULL AND (SELECT COUNT(*) FROM #Map d WHERE d.LegacyUserId = m.LegacyUserId) > 1;
UPDATE m SET Valid = 0, Reason = 'Several legacy users map to the same subject (merging needs explicit approval)'
FROM #Map m WHERE Valid IS NULL AND (SELECT COUNT(*) FROM #Map d WHERE d.Issuer = m.Issuer COLLATE Latin1_General_100_BIN2 AND d.Subject = m.Subject COLLATE Latin1_General_100_BIN2) > 1;
UPDATE m SET Valid = 0, Reason = 'Legacy user not found in the source'
FROM #Map m WHERE Valid IS NULL AND NOT EXISTS (SELECT 1 FROM {SRC}.dbo.Users u WHERE u.Id = TRY_CONVERT(uniqueidentifier, m.LegacyUserId));
UPDATE #Map SET Valid = 1 WHERE Valid IS NULL;

-- Resolves any legacy user reference (GUID text or exact username) to a verified issuer + subject.
INSERT #Ref (Ref, Issuer, Subject)
SELECT LOWER(CONVERT(nvarchar(36), u.Id)), m.Issuer, m.Subject
FROM {SRC}.dbo.Users u JOIN #Map m ON m.Valid = 1 AND m.LegacyUserId = LOWER(CONVERT(nvarchar(36), u.Id))
UNION ALL
SELECT u.Username COLLATE Latin1_General_100_BIN2, m.Issuer, m.Subject
FROM {SRC}.dbo.Users u JOIN #Map m ON m.Valid = 1 AND m.LegacyUserId = LOWER(CONVERT(nvarchar(36), u.Id));



-- Users -> Identity.UserProfiles. PasswordHash is never selected.
INSERT #Plan (SourceTable, SourceKey, SourceJson, TargetDatabase, TargetTable, Id, Issuer, Subject, DisplayName, At)
SELECT 'Users', LOWER(CONVERT(nvarchar(36), u.Id)), (SELECT u.Id, u.Username FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
       '{IDENTITY}', 'UserProfiles', u.Id, r.Issuer, r.Subject, LEFT(LTRIM(RTRIM(u.Username)), 80), @cutover
FROM {SRC}.dbo.Users u LEFT JOIN #Ref r ON r.Ref = LOWER(CONVERT(nvarchar(36), u.Id));
UPDATE #Plan SET State = 'Quarantined', Reason = 'No verified identity mapping' WHERE SourceTable = 'Users' AND Subject IS NULL;
UPDATE #Plan SET State = 'Quarantined', Reason = 'Display name is blank' WHERE SourceTable = 'Users' AND State IS NULL AND NULLIF(DisplayName, '') IS NULL;
UPDATE p SET State = 'AlreadyPresent', Reason = 'Profile already exists for this subject; kept as is', TargetKey = LOWER(CONVERT(nvarchar(36), t.Id))
FROM #Plan p JOIN {IDENTITY}.dbo.UserProfiles t ON t.Issuer = p.Issuer AND t.Subject = p.Subject WHERE p.SourceTable = 'Users' AND p.State IS NULL;
UPDATE p SET State = 'Quarantined', Reason = 'Profile id already used by another identity'
FROM #Plan p JOIN {IDENTITY}.dbo.UserProfiles t ON t.Id = p.Id WHERE p.SourceTable = 'Users' AND p.State IS NULL;
UPDATE #Plan SET State = 'Imported', TargetKey = SourceKey WHERE SourceTable = 'Users' AND State IS NULL;

-- MatchResults -> Statistics.MatchResults. GUID match ids become lowercase canonical text; historical scores above 5 are kept.
INSERT #Plan (SourceTable, SourceKey, SourceJson, TargetDatabase, TargetTable, Issuer, Subject, MatchId, Score, Won, At)
SELECT 'MatchResults', CONVERT(nvarchar(450), s.Id), (SELECT s.Id, s.MatchId, s.UserId, s.Score, s.Won, s.PlayedAt FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
       '{STATISTICS}', 'MatchResults', r.Issuer, r.Subject, LOWER(CONVERT(nvarchar(36), s.MatchId)), s.Score, s.Won, s.PlayedAt
FROM {SRC}.dbo.MatchResults s LEFT JOIN #Ref r ON r.Ref = LOWER(CONVERT(nvarchar(36), s.UserId));
UPDATE #Plan SET State = 'Quarantined', Reason = 'Player has no verified identity mapping' WHERE SourceTable = 'MatchResults' AND Subject IS NULL;
UPDATE #Plan SET State = 'Quarantined', Reason = 'Negative score' WHERE SourceTable = 'MatchResults' AND State IS NULL AND Score < 0;
UPDATE p SET State = CASE WHEN t.Score = p.Score AND t.Won = p.Won AND t.PlayedAt = p.At THEN 'AlreadyPresent' ELSE 'Quarantined' END,
             Reason = CASE WHEN t.Score = p.Score AND t.Won = p.Won AND t.PlayedAt = p.At THEN 'Identical result already recorded'
                           ELSE 'Conflicts with a result already recorded for this match and player' END
FROM #Plan p JOIN {STATISTICS}.dbo.MatchResults t ON t.Issuer = p.Issuer AND t.MatchId = p.MatchId AND t.UserId = p.Subject
WHERE p.SourceTable = 'MatchResults' AND p.State IS NULL;
UPDATE #Plan SET State = 'Imported', TargetKey = CONCAT(Issuer, '|', MatchId, '|', Subject) WHERE SourceTable = 'MatchResults' AND State IS NULL;

-- UserPrefs -> Notification.UserPreferences.
INSERT #Plan (SourceTable, SourceKey, SourceJson, TargetDatabase, TargetTable, Issuer, Subject, EmailEnabled, PushEnabled)
SELECT 'UserPrefs', CONVERT(nvarchar(450), s.Id), (SELECT s.Id, s.UserId, s.EmailEnabled, s.PushEnabled FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
       '{NOTIFICATION}', 'UserPreferences', r.Issuer, r.Subject, s.EmailEnabled, s.PushEnabled
FROM {SRC}.dbo.UserPrefs s
LEFT JOIN #Ref r ON r.Ref = CASE WHEN TRY_CONVERT(uniqueidentifier, s.UserId) IS NULL THEN s.UserId COLLATE Latin1_General_100_BIN2
                                 ELSE LOWER(CONVERT(nvarchar(36), TRY_CONVERT(uniqueidentifier, s.UserId))) END;
UPDATE #Plan SET State = 'Quarantined', Reason = 'Player has no verified identity mapping' WHERE SourceTable = 'UserPrefs' AND Subject IS NULL;
UPDATE p SET State = CASE WHEN t.EmailEnabled = p.EmailEnabled AND t.PushEnabled = p.PushEnabled THEN 'AlreadyPresent' ELSE 'Quarantined' END,
             Reason = CASE WHEN t.EmailEnabled = p.EmailEnabled AND t.PushEnabled = p.PushEnabled THEN 'Same preferences already stored'
                           ELSE 'Player already set different preferences in the new service; current values kept' END
FROM #Plan p JOIN {NOTIFICATION}.dbo.UserPreferences t ON t.Issuer = p.Issuer AND t.Subject = p.Subject
WHERE p.SourceTable = 'UserPrefs' AND p.State IS NULL;
UPDATE #Plan SET State = 'Imported', TargetKey = CONCAT(Issuer, '|', Subject) WHERE SourceTable = 'UserPrefs' AND State IS NULL;

-- EventLogs -> Notification.Events, with a deterministic EventId so a rerun never duplicates.
INSERT #Plan (SourceTable, SourceKey, SourceJson, TargetDatabase, TargetTable, Id, Issuer, Subject, MatchId, EventType, WinnerUserId, At)
SELECT 'EventLogs', CONVERT(nvarchar(450), s.Id), (SELECT s.Id, s.Type, s.UserId, s.PayloadJson, s.CreatedAt FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
       '{NOTIFICATION}', 'Events',
       CONVERT(uniqueidentifier, HASHBYTES('MD5', CONCAT(N'quizarena-legacy-event:', DB_NAME(DB_ID(@source)), N':', s.Id))),
       r.Issuer, r.Subject,
       CASE WHEN ISJSON(s.PayloadJson) = 1 THEN JSON_VALUE(s.PayloadJson, '$.matchId') END,
       CASE s.Type WHEN 'MATCH_CREATED' THEN 0 WHEN 'MATCH_COMPLETED' THEN 1 END,
       w.Subject, s.CreatedAt
FROM {SRC}.dbo.EventLogs s
LEFT JOIN #Ref r ON r.Ref = CASE WHEN TRY_CONVERT(uniqueidentifier, s.UserId) IS NULL THEN s.UserId COLLATE Latin1_General_100_BIN2
                                 ELSE LOWER(CONVERT(nvarchar(36), TRY_CONVERT(uniqueidentifier, s.UserId))) END
LEFT JOIN #Ref w ON ISJSON(s.PayloadJson) = 1 AND w.Ref = CASE WHEN TRY_CONVERT(uniqueidentifier, JSON_VALUE(s.PayloadJson, '$.winner')) IS NULL
                                 THEN JSON_VALUE(s.PayloadJson, '$.winner') COLLATE Latin1_General_100_BIN2
                                 ELSE LOWER(CONVERT(nvarchar(36), TRY_CONVERT(uniqueidentifier, JSON_VALUE(s.PayloadJson, '$.winner')))) END;
UPDATE #Plan SET State = 'Quarantined', Reason = 'Unknown legacy event type' WHERE SourceTable = 'EventLogs' AND EventType IS NULL;
UPDATE #Plan SET State = 'Quarantined', Reason = 'Recipient has no verified identity mapping' WHERE SourceTable = 'EventLogs' AND State IS NULL AND Subject IS NULL;
UPDATE #Plan SET State = 'Quarantined', Reason = 'Payload has no usable matchId' WHERE SourceTable = 'EventLogs' AND State IS NULL AND (NULLIF(LTRIM(MatchId), '') IS NULL OR LEN(MatchId) > 100);
UPDATE p SET State = 'Quarantined', Reason = 'Winner has no verified identity mapping'
FROM #Plan p JOIN {SRC}.dbo.EventLogs s ON CONVERT(nvarchar(450), s.Id) = p.SourceKey
WHERE p.SourceTable = 'EventLogs' AND p.State IS NULL AND p.EventType = 1 AND p.WinnerUserId IS NULL AND JSON_VALUE(s.PayloadJson, '$.winner') IS NOT NULL;
UPDATE p SET State = 'AlreadyPresent', Reason = 'Event already imported'
FROM #Plan p JOIN {NOTIFICATION}.dbo.Events t ON t.EventId = p.Id WHERE p.SourceTable = 'EventLogs' AND p.State IS NULL;
UPDATE #Plan SET State = 'Imported', TargetKey = LOWER(CONVERT(nvarchar(36), Id)) WHERE SourceTable = 'EventLogs' AND State IS NULL;

-- Legacy questions have no answer options; the QuestionBank requires exactly four. Kept, not invented.
INSERT #Plan (SourceTable, SourceKey, SourceJson, State, Reason)
SELECT 'Questions', LOWER(CONVERT(nvarchar(36), q.Id)), (SELECT q.Id, q.Prompt, q.CorrectAnswer, q.CategoryId FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
       'Quarantined', 'Legacy question has no answer options; QuestionBank requires exactly four'
FROM {SRC}.dbo.Questions q;
INSERT #Plan (SourceTable, SourceKey, SourceJson, State, Reason)
SELECT 'Categories', LOWER(CONVERT(nvarchar(36), c.Id)), (SELECT c.Id, c.Name FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
       'Quarantined', 'Kept with its legacy questions'
FROM {SRC}.dbo.Categories c;

-- Transient state is archived, never resumed as new lobbies or playable five-round matches (ADR-08).
INSERT #Plan (SourceTable, SourceKey, SourceJson, State, Reason)
SELECT 'ActiveLobbies', LOWER(CONVERT(nvarchar(36), x.Id)), (SELECT x.* FOR JSON PATH, WITHOUT_ARRAY_WRAPPER), 'Archived', 'Transient legacy lobby; archived, not resumed' FROM {SRC}.dbo.ActiveLobbies x
UNION ALL SELECT 'LobbyPlayers', LOWER(CONVERT(nvarchar(36), x.Id)), (SELECT x.* FOR JSON PATH, WITHOUT_ARRAY_WRAPPER), 'Archived', 'Transient legacy lobby membership; archived' FROM {SRC}.dbo.LobbyPlayers x
UNION ALL SELECT 'ActiveMatches', x.MatchId, (SELECT x.* FOR JSON PATH, WITHOUT_ARRAY_WRAPPER), 'Archived', 'Legacy mock-question match; archived, not playable' FROM {SRC}.dbo.ActiveMatches x
UNION ALL SELECT 'PlayerStates', CONVERT(nvarchar(450), x.Id), (SELECT x.* FOR JSON PATH, WITHOUT_ARRAY_WRAPPER), 'Archived', 'Legacy mock-question match state; archived' FROM {SRC}.dbo.PlayerStates x;

-- Rows a previous run already imported stay as they are (idempotent rerun).
UPDATE p SET State = 'PreviouslyImported', Reason = 'Imported by an earlier run; left unchanged'
FROM #Plan p JOIN {LEDGER}.dbo.Ledger l ON l.SourceTable = p.SourceTable AND l.SourceKey = p.SourceKey AND l.State = 'Imported';
'@ @{ cutover = $cutover; source = $names.Source } | Out-Null

$mappingReport = Sql 'SELECT Row, LegacyUserId, Reason FROM #Map WHERE Valid = 0 ORDER BY Row'
$summary = Sql 'SELECT SourceTable, State, ISNULL(Reason, '''') AS Reason, COUNT(*) AS Rows FROM #Plan GROUP BY SourceTable, State, Reason ORDER BY SourceTable, State'
Write-Host "Mapping file: $($mapping.Count) entries, $($mappingReport.Rows.Count) rejected (SHA-256 $mappingHash)"
if ($mappingReport.Rows.Count) { Show $mappingReport }
Write-Host "Plan for $($names.Source) (run $RunId):"; Show $summary

if ($Mode -eq 'DryRun') { Write-Host 'Dry run only: nothing was written.' -ForegroundColor Yellow; return }

# ------------------------------------------------------------------------------------------ Import
$transaction = $connection.BeginTransaction()
try {
    Sql @'
INSERT {LEDGER}.dbo.Runs (RunId, Mode, SourceDatabase, MappingSha256, StartedAt) VALUES (@run, 'Import', @source, @hash, SYSUTCDATETIME());
INSERT {LEDGER}.dbo.IdentityMapping (LegacyUserId, Issuer, Subject, VerifiedBy, ProofReference, Valid, Reason, RunId, RecordedAt)
SELECT ISNULL(LegacyUserId, '(invalid)'), LEFT(Issuer, 256), LEFT(Subject, 200), LEFT(VerifiedBy, 200), LEFT(ProofReference, 400), Valid, Reason, @run, SYSUTCDATETIME() FROM #Map;

INSERT {IDENTITY}.dbo.UserProfiles (Id, Issuer, Subject, DisplayName, CreatedAt, UpdatedAt)
SELECT Id, Issuer, Subject, DisplayName, At, At FROM #Plan WHERE SourceTable = 'Users' AND State = 'Imported';
INSERT {STATISTICS}.dbo.MatchResults (Issuer, MatchId, UserId, Score, Won, PlayedAt)
SELECT Issuer, MatchId, Subject, Score, Won, At FROM #Plan WHERE SourceTable = 'MatchResults' AND State = 'Imported';
INSERT {NOTIFICATION}.dbo.UserPreferences (Issuer, Subject, EmailEnabled, PushEnabled)
SELECT Issuer, Subject, EmailEnabled, PushEnabled FROM #Plan WHERE SourceTable = 'UserPrefs' AND State = 'Imported';
INSERT {NOTIFICATION}.dbo.Events (EventId, Type, Issuer, UserId, MatchId, WinnerUserId, Draw, CreatedAt)
SELECT Id, EventType, Issuer, Subject, MatchId, WinnerUserId, 0, At FROM #Plan WHERE SourceTable = 'EventLogs' AND State = 'Imported';

MERGE {LEDGER}.dbo.Ledger AS l
USING (SELECT * FROM #Plan WHERE State <> 'PreviouslyImported') AS p ON l.SourceTable = p.SourceTable AND l.SourceKey = p.SourceKey
WHEN MATCHED THEN UPDATE SET State = p.State, Reason = p.Reason, TargetDatabase = p.TargetDatabase, TargetTable = p.TargetTable, TargetKey = p.TargetKey,
    TargetJson = CASE WHEN p.State = 'Imported' THEN (SELECT p.Issuer, p.Subject, p.Subject AS UserId, p.MatchId, p.DisplayName, p.EmailEnabled, p.PushEnabled FOR JSON PATH, WITHOUT_ARRAY_WRAPPER) END,
    SourceJson = p.SourceJson, RunId = @run, RecordedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT (SourceTable, SourceKey, State, Reason, TargetDatabase, TargetTable, TargetKey, TargetJson, SourceJson, RunId, RecordedAt)
    VALUES (p.SourceTable, p.SourceKey, p.State, p.Reason, p.TargetDatabase, p.TargetTable, p.TargetKey,
            CASE WHEN p.State = 'Imported' THEN (SELECT p.Issuer, p.Subject, p.Subject AS UserId, p.MatchId, p.DisplayName, p.EmailEnabled, p.PushEnabled FOR JSON PATH, WITHOUT_ARRAY_WRAPPER) END,
            p.SourceJson, @run, SYSUTCDATETIME());

UPDATE {LEDGER}.dbo.Runs SET FinishedAt = SYSUTCDATETIME(),
    Summary = (SELECT SourceTable, State, COUNT(*) AS Rows FROM #Plan GROUP BY SourceTable, State FOR JSON PATH) WHERE RunId = @run;
'@ @{ run = $RunId; source = $names.Source; hash = $mappingHash } $transaction | Out-Null
    $transaction.Commit()
} catch { $transaction.Rollback(); throw }
Write-Host "Import committed. Run id: $RunId (needed for Rollback). Next: -Mode Verify." -ForegroundColor Green
