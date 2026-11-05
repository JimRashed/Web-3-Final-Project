<#
.SYNOPSIS
  Rehearses the whole cutover on disposable databases and asserts every expected outcome.

.DESCRIPTION
  Creates Rehearsal_QuizArenaDb from legacy-schema.sql plus rehearsal-data.sql (synthetic only), and
  Rehearsal_ copies of the Identity, Statistics and Notification databases from their own migrations.
  Then runs Backup, DryRun, Import, Verify, a second Import, Rollback and Verify, checking counts after
  each step and that the source never changes. Real databases are never touched. All Rehearsal_ databases
  are dropped at the end unless -Keep is given.
#>
[CmdletBinding()]
param(
    [string] $Server = '(localdb)\MSSQLLocalDB',
    [string] $ConnectionOptions = 'Integrated Security=true;TrustServerCertificate=true',
    [switch] $Keep
)

$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
$root = Split-Path -Parent (Split-Path -Parent $here)
$prefix = 'Rehearsal_'
$source = "$($prefix)QuizArenaDb"
$ledger = "$($prefix)QuizArenaMigrationDb"
$targets = @{ Identity = "$($prefix)QuizArenaIdentityDb"; Statistics = "$($prefix)QuizArenaStatisticsDb"; Notification = "$($prefix)QuizArenaNotificationDb" }
$all = @($source, $ledger) + @($targets.Values)
$failures = New-Object System.Collections.Generic.List[string]

function Scalar([string] $database, [string] $text) {
    $c = New-Object System.Data.SqlClient.SqlConnection "Server=$Server;Database=$database;$ConnectionOptions"
    $c.Open()
    try { $cmd = $c.CreateCommand(); $cmd.CommandText = $text; return $cmd.ExecuteScalar() } finally { $c.Close() }
}
function Run-Batches([string] $database, [string] $script) {
    $c = New-Object System.Data.SqlClient.SqlConnection "Server=$Server;Database=$database;$ConnectionOptions"
    $c.Open()
    try {
        foreach ($batch in [regex]::Split($script, '(?im)^\s*GO\s*$')) {
            if ($batch.Trim()) { $cmd = $c.CreateCommand(); $cmd.CommandText = $batch; [void] $cmd.ExecuteNonQuery() }
        }
    } finally { $c.Close() }
}
function Drop-All {
    foreach ($db in $all) {
        Scalar 'master' "IF DB_ID('$db') IS NOT NULL BEGIN ALTER DATABASE [$db] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$db]; END" | Out-Null
    }
}
function Expect([string] $name, $actual, $expected) {
    if ("$actual" -eq "$expected") { Write-Host "  PASS $name ($actual)" -ForegroundColor Green }
    else { Write-Host "  FAIL $name (expected $expected, got $actual)" -ForegroundColor Red; $failures.Add($name) }
}
function Migrate([string[]] $arguments) {
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $here 'Invoke-LegacyMigration.ps1') -Server $Server -ConnectionOptions $ConnectionOptions `
        -SourceDatabase $source -TargetPrefix $prefix -LedgerDatabase $ledger @arguments | Out-Host
    return $LASTEXITCODE
}
function Counts {
    [pscustomobject]@{
        Profiles = Scalar $targets.Identity 'SELECT COUNT(*) FROM dbo.UserProfiles'
        Results = Scalar $targets.Statistics 'SELECT COUNT(*) FROM dbo.MatchResults'
        Prefs = Scalar $targets.Notification 'SELECT COUNT(*) FROM dbo.UserPreferences'
        Events = Scalar $targets.Notification 'SELECT COUNT(*) FROM dbo.Events'
    }
}
$sourceChecksum = @'
SELECT CONCAT((SELECT CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM dbo.Users), '|', (SELECT CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM dbo.MatchResults), '|',
  (SELECT CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM dbo.UserPrefs), '|', (SELECT CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM dbo.EventLogs), '|',
  (SELECT COUNT(*) FROM dbo.Questions), '|', (SELECT COUNT(*) FROM dbo.ActiveMatches), '|', (SELECT COUNT(*) FROM dbo.PlayerStates))
'@

Write-Host '==> Preparing disposable databases'
Drop-All
Scalar 'master' "CREATE DATABASE [$source]" | Out-Null
Run-Batches $source (Get-Content (Join-Path $here 'legacy-schema.sql') -Raw)
Run-Batches $source (Get-Content (Join-Path $here 'rehearsal-data.sql') -Raw)
Push-Location $root
try {
    & dotnet tool restore | Out-Null
    $variables = @{ Identity = 'IDENTITY'; Statistics = 'STATISTICS'; Notification = 'NOTIFICATION' }
    foreach ($service in $targets.Keys) {
        Set-Item "env:QUIZARENA_$($variables[$service])_CONNECTION" "Server=$Server;Database=$($targets[$service]);$ConnectionOptions"
        & dotnet tool run dotnet-ef database update --project "src/QuizArena.$service.Data" --startup-project "src/QuizArena.$service.Api" --context "$($service)DbContext" | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Could not create $($targets[$service])." }
        Remove-Item "env:QUIZARENA_$($variables[$service])_CONNECTION"
    }
} finally { Pop-Location }
# bob already signed in to the new Identity service before the cutover: his live profile must be kept.
Scalar $targets.Identity "INSERT dbo.UserProfiles (Id, Issuer, Subject, DisplayName, CreatedAt, UpdatedAt) VALUES (NEWID(), 'http://localhost:8080/realms/quizarena', 'Rehearsal-Bob-Subject', 'Bob (new)', SYSUTCDATETIME(), SYSUTCDATETIME())" | Out-Null
$before = Scalar $source $sourceChecksum

Write-Host '==> Backup'
$backupDir = Join-Path $env:TEMP 'quizarena-rehearsal-backup'
Expect 'backup exit code' (Migrate @('-Mode', 'Backup', '-BackupDirectory', $backupDir)) 0
Expect 'backup file written' ([bool](Get-ChildItem $backupDir -Filter "$source-*.bak")) $true

$mapping = Join-Path $here 'rehearsal-mapping.csv'
Write-Host '==> Dry run'
Expect 'dry run exit code' (Migrate @('-Mode', 'DryRun', '-MappingFile', $mapping)) 0
$c = Counts
Expect 'dry run writes nothing (profiles: only bob)' $c.Profiles 1
Expect 'dry run writes nothing (results)' $c.Results 0

Write-Host '==> Import'
Expect 'import exit code' (Migrate @('-Mode', 'Import', '-MappingFile', $mapping)) 0
$run = Scalar $ledger "SELECT TOP 1 CONVERT(nvarchar(36), RunId) FROM dbo.Runs ORDER BY StartedAt DESC"
$c = Counts
Expect 'profiles = bob (kept) + alice (imported)' $c.Profiles 2
Expect 'results = alice x2 + bob x1 (carol quarantined)' $c.Results 3
Expect 'historical score 7 kept' (Scalar $targets.Statistics "SELECT COUNT(*) FROM dbo.MatchResults WHERE Score = 7") 1
Expect 'GUID match id stored as lowercase text' (Scalar $targets.Statistics "SELECT COUNT(*) FROM dbo.MatchResults WHERE MatchId = 'aaaaaaaa-0000-0000-0000-000000000002'") 2
Expect 'preferences by GUID and by username (carol quarantined)' $c.Prefs 2
Expect 'events: created + completed (unknown type and bad payload quarantined)' $c.Events 2
Expect 'completed event winner mapped to alice' (Scalar $targets.Notification "SELECT WinnerUserId FROM dbo.Events WHERE Type = 1") 'rehearsal-alice-subject'
Expect 'bob''s live profile untouched' (Scalar $targets.Identity "SELECT DisplayName FROM dbo.UserProfiles WHERE Subject = 'Rehearsal-Bob-Subject'") 'Bob (new)'
Expect 'no password hash anywhere in the ledger' (Scalar $ledger "SELECT COUNT(*) FROM dbo.Ledger WHERE SourceJson LIKE '%legacy-hash%'") 0
Expect 'mapping rows rejected (carol no proof, ghost not in source)' (Scalar $ledger "SELECT COUNT(*) FROM dbo.IdentityMapping WHERE Valid = 0") 2
Expect 'quarantined rows (1 user, 1 result, 1 preference, 2 events, 2 questions, 1 category)' (Scalar $ledger "SELECT COUNT(*) FROM dbo.Ledger WHERE State = 'Quarantined'") 8
Expect 'archived lobby and match rows' (Scalar $ledger "SELECT COUNT(*) FROM dbo.Ledger WHERE State = 'Archived'") 6

Write-Host '==> Verify'
Expect 'verify passes' (Migrate @('-Mode', 'Verify')) 0

Write-Host '==> Second import (idempotent)'
Expect 'second import exit code' (Migrate @('-Mode', 'Import', '-MappingFile', $mapping)) 0
$again = Counts
Expect 'no duplicate profiles' $again.Profiles 2
Expect 'no duplicate results' $again.Results 3
Expect 'no duplicate preferences' $again.Prefs 2
Expect 'no duplicate events' $again.Events 2

Write-Host '==> Rollback of the first run'
# alice changes her preferences in the new service after the cutover: rollback must keep that row.
Scalar $targets.Notification "UPDATE dbo.UserPreferences SET PushEnabled = 1 WHERE Subject = 'rehearsal-alice-subject'" | Out-Null
Expect 'rollback exit code' (Migrate @('-Mode', 'Rollback', '-RunId', $run)) 0
$c = Counts
Expect 'imported profile removed, bob kept' $c.Profiles 1
Expect 'imported results removed' $c.Results 0
Expect 'bob''s untouched preference removed, alice''s changed one kept' $c.Prefs 1
Expect 'imported events removed' $c.Events 0
Expect 'verify after rollback passes' (Migrate @('-Mode', 'Verify')) 0

Write-Host '==> Source unchanged'
Expect 'source checksum identical' (Scalar $source $sourceChecksum) $before

if (-not $Keep) { Drop-All; Remove-Item -Recurse -Force $backupDir -ErrorAction SilentlyContinue }
if ($failures.Count) { Write-Host "REHEARSAL FAILED: $($failures.Count) check(s)" -ForegroundColor Red; exit 1 }
Write-Host 'REHEARSAL PASSED' -ForegroundColor Green
