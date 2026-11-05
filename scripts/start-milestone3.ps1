<#
.SYNOPSIS
  Starts the Milestone 3 local environment: Keycloak, database migrations, the six APIs and the React client.

.DESCRIPTION
  Safe to rerun. Nothing is reset: Keycloak keeps its Docker volume and every service keeps its database.
  Processes started here are recorded in .run/pids.json and stopped by scripts/stop-milestone3.ps1.
  Logs go to .run/logs/<name>.log. No secret value is printed.

.EXAMPLE
  ./scripts/start-milestone3.ps1                  # everything
  ./scripts/start-milestone3.ps1 -ProvisionUsers  # also (re)create the demo players
  ./scripts/start-milestone3.ps1 -Services Identity,Statistics -SkipFrontend
#>
[CmdletBinding()]
param(
    [ValidateSet('Identity', 'Statistics', 'QuestionBank', 'Lobby', 'Gameplay', 'Notification')]
    [string[]] $Services = @('Identity', 'Statistics', 'QuestionBank', 'Lobby', 'Gameplay', 'Notification'),
    [switch] $SkipKeycloak,
    [switch] $SkipMigrations,
    [switch] $SkipFrontend,
    [switch] $ProvisionUsers,
    [int] $TimeoutSeconds = 120
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$runDir = Join-Path $root '.run'
$logDir = Join-Path $runDir 'logs'
$pidFile = Join-Path $runDir 'pids.json'
$ports = @{ Identity = 5101; Statistics = 5102; QuestionBank = 5103; Lobby = 5104; Gameplay = 5105; Notification = 5106 }

function Step([string] $message) { Write-Host "==> $message" -ForegroundColor Cyan }
function Fail([string] $message) { Write-Host "ERROR: $message" -ForegroundColor Red; exit 1 }

function Wait-Http([string] $url, [string] $name) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 5
            if ($response.StatusCode -eq 200) { return $true }
        } catch { Start-Sleep -Seconds 2 }
    }
    Write-Host "  $name did not answer 200 at $url within $TimeoutSeconds s. See .run/logs." -ForegroundColor Yellow
    return $false
}

function Test-PortFree([int] $port) {
    -not (Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue)
}

# 1. Prerequisites --------------------------------------------------------------------------------
Step 'Checking prerequisites'
foreach ($tool in 'dotnet', 'node', 'npm') {
    if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) { Fail "$tool is not on PATH. See docs/milestone3/runbook.md#prerequisites." }
}
$sdk = (& dotnet --version)
if (-not $sdk.StartsWith('10.')) { Fail ".NET SDK 10 is required (found $sdk)." }
if (-not $SkipKeycloak -and -not (Get-Command docker -ErrorAction SilentlyContinue)) { Fail 'docker is not on PATH (needed for Keycloak). Start Docker Desktop or pass -SkipKeycloak.' }
if (-not $env:QUIZARENA_SQLSERVER -and -not (Get-Command sqllocaldb -ErrorAction SilentlyContinue)) {
    Fail 'SQL Server LocalDB was not found. Install it, or set QUIZARENA_SQLSERVER (see the runbook).'
}
$envFile = Join-Path $root '.env'
if (-not (Test-Path $envFile)) { Fail '.env is missing. Copy .env.example to .env and fill in the blank values.' }
$envKeys = Get-Content $envFile | Where-Object { $_ -match '^[A-Z_]+=.+' } | ForEach-Object { ($_ -split '=', 2)[0] }
foreach ($key in 'KC_BOOTSTRAP_ADMIN_USERNAME', 'KC_BOOTSTRAP_ADMIN_PASSWORD', 'QUIZARENA_GAMEPLAY_CLIENT_SECRET', 'QUIZARENA_DEMO_PASSWORD') {
    if ($envKeys -notcontains $key) { Fail ".env has no value for $key." }
}
$gameplaySecret = (Get-Content $envFile | Where-Object { $_ -match '^QUIZARENA_GAMEPLAY_CLIENT_SECRET=' } | Select-Object -First 1) -replace '^QUIZARENA_GAMEPLAY_CLIENT_SECRET=', ''
New-Item -ItemType Directory -Force $logDir | Out-Null
$running = @{}
if (Test-Path $pidFile) { (Get-Content $pidFile | ConvertFrom-Json).PSObject.Properties | ForEach-Object { $running[$_.Name] = $_.Value } }

# Optional SQL Server override: each API reads ConnectionStrings__DefaultConnection, the EF tools read QUIZARENA_<SERVICE>_CONNECTION.
function Connection([string] $service) {
    if (-not $env:QUIZARENA_SQLSERVER) { return $null }
    "$($env:QUIZARENA_SQLSERVER);Database=QuizArena$($service)Db"
}
$efVariable = @{ Identity = 'IDENTITY'; Statistics = 'STATISTICS'; QuestionBank = 'QUESTION_BANK'; Lobby = 'LOBBY'; Gameplay = 'GAMEPLAY'; Notification = 'NOTIFICATION' }

# 2. Keycloak -------------------------------------------------------------------------------------
if (-not $SkipKeycloak) {
    Step 'Starting Keycloak (data kept in the quizarena_keycloak-data volume)'
    & docker compose --env-file $envFile -f (Join-Path $root 'infra/compose.yaml') up -d | Out-Null
    if ($LASTEXITCODE -ne 0) { Fail 'docker compose up failed. Is Docker Desktop running?' }
    if (-not (Wait-Http 'http://localhost:9000/health/ready' 'Keycloak')) { Fail 'Keycloak is not ready.' }
    if ($ProvisionUsers) {
        Step 'Provisioning demo players (idempotent)'
        & node "--env-file=$envFile" (Join-Path $root 'infra/oidc/provision-demo-users.mjs')
        if ($LASTEXITCODE -ne 0) { Fail 'Demo-user provisioning failed.' }
    }
}

# 3. Build and migrate ----------------------------------------------------------------------------
Step 'Building the solution'
& dotnet build (Join-Path $root 'QuizArena.sln') --nologo -v quiet | Out-Null
if ($LASTEXITCODE -ne 0) { Fail 'dotnet build failed. Run it manually to see the errors.' }

if (-not $SkipMigrations) {
    Step 'Applying database migrations (never drops data)'
    Push-Location $root
    try {
        & dotnet tool restore | Out-Null
        foreach ($service in $Services) {
            $override = Connection $service
            if ($override) { Set-Item "env:QUIZARENA_$($efVariable[$service])_CONNECTION" $override }
            & dotnet tool run dotnet-ef database update --no-build --project "src/QuizArena.$service.Data" --startup-project "src/QuizArena.$service.Api" --context "$($service)DbContext" *> (Join-Path $logDir "migrate-$service.log")
            if ($LASTEXITCODE -ne 0) { Fail "Migrations failed for $service. See .run/logs/migrate-$service.log." }
            Write-Host "  $service database is up to date"
        }
    } finally { Pop-Location }
}

# 4. APIs -----------------------------------------------------------------------------------------
Step 'Starting the APIs'
foreach ($service in $Services) {
    $port = $ports[$service]
    if (-not (Test-PortFree $port)) { Write-Host "  $service : port $port already in use, skipping (stop it first if it is stale)" -ForegroundColor Yellow; continue }
    $log = Join-Path $logDir "$service.log"
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $override = Connection $service
    if ($override) { $env:ConnectionStrings__DefaultConnection = $override } else { Remove-Item env:ConnectionStrings__DefaultConnection -ErrorAction SilentlyContinue }
    # Only Gameplay gets the machine-client secret (it calls Lobby, QuestionBank, Statistics and Notification).
    # It is passed through this process's environment and never written to disk or printed.
    if ($service -eq 'Gameplay') { $env:Authentication__MachineClientSecret = $gameplaySecret } else { Remove-Item env:Authentication__MachineClientSecret -ErrorAction SilentlyContinue }
    $process = Start-Process dotnet -ArgumentList @('run', '--no-build', '--project', "src/QuizArena.$service.Api", '--no-launch-profile', '--urls', "http://localhost:$port") `
        -WorkingDirectory $root -RedirectStandardOutput $log -RedirectStandardError "$log.err" -WindowStyle Hidden -PassThru
    $running[$service] = $process.Id
}
Remove-Item env:ConnectionStrings__DefaultConnection -ErrorAction SilentlyContinue
Remove-Item env:Authentication__MachineClientSecret -ErrorAction SilentlyContinue

# 5. Frontend -------------------------------------------------------------------------------------
if (-not $SkipFrontend) {
    $client = Join-Path $root 'src/QuizArena.Client'
    # Check the vite launcher, not just the folder, so a half-finished install is redone.
    if (-not (Test-Path (Join-Path $client 'node_modules/.bin/vite.cmd'))) {
        Step 'Installing client packages (npm ci)'
        # npm.cmd, not npm: Windows PowerShell 5.1 mangles the arguments of the npm.ps1 shim ("Unknown command: pm").
        Push-Location $client
        try { & npm.cmd ci --no-audit --no-fund *> (Join-Path $logDir 'npm-ci.log') } finally { Pop-Location }
        if ($LASTEXITCODE -ne 0) { Fail 'npm ci failed for the client. See .run/logs/npm-ci.log.' }
    }
    if (Test-PortFree 5173) {
        Step 'Starting the React client on http://localhost:5173'
        $log = Join-Path $logDir 'client.log'
        $process = Start-Process npm.cmd -ArgumentList @('run', 'dev', '--', '--port', '5173', '--strictPort') -WorkingDirectory $client `
            -RedirectStandardOutput $log -RedirectStandardError "$log.err" -WindowStyle Hidden -PassThru
        $running['Client'] = $process.Id
    } else { Write-Host '  Port 5173 already in use, skipping the client' -ForegroundColor Yellow }
}
$running | ConvertTo-Json | Set-Content $pidFile -Encoding utf8

# 6. Health ---------------------------------------------------------------------------------------
Step 'Health'
$healthy = $true
$rows = @(foreach ($service in $Services) {
    $ok = Wait-Http "http://localhost:$($ports[$service])/health" $service
    $healthy = $healthy -and $ok
    [pscustomobject]@{ Component = $service; Url = "http://localhost:$($ports[$service])/health"; Status = $(if ($ok) { 'ok' } else { 'NOT READY' }) }
})
if (-not $SkipKeycloak) { $rows += [pscustomobject]@{ Component = 'Keycloak'; Url = 'http://localhost:9000/health/ready'; Status = 'ok' } }
if (-not $SkipFrontend) {
    $ok = Wait-Http 'http://localhost:5173/' 'Client'
    $healthy = $healthy -and $ok
    $rows += [pscustomobject]@{ Component = 'Client'; Url = 'http://localhost:5173/'; Status = $(if ($ok) { 'ok' } else { 'NOT READY' }) }
}
$rows | Format-Table -AutoSize | Out-Host
if (-not $healthy) { Fail 'Some components are not ready. Check .run/logs and docs/milestone3/runbook.md#troubleshooting.' }
Write-Host 'Ready. Open http://localhost:5173 and sign in as a demo player. Stop with ./scripts/stop-milestone3.ps1' -ForegroundColor Green
