<#
.SYNOPSIS
  Stops what scripts/start-milestone3.ps1 started. Never deletes data.

.DESCRIPTION
  Stops the APIs and the client recorded in .run/pids.json, including their child processes.
  With -Keycloak, the Keycloak container is also stopped (docker compose stop). Its volume and
  every service database are kept, so the next start resumes with the same data.
#>
[CmdletBinding()]
param([switch] $Keycloak)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$pidFile = Join-Path $root '.run/pids.json'

if (Test-Path $pidFile) {
    $recorded = (Get-Content $pidFile | ConvertFrom-Json).PSObject.Properties
    foreach ($entry in $recorded) {
        if (Get-Process -Id $entry.Value -ErrorAction SilentlyContinue) {
            # /T stops the child process too (dotnet run and npm both start one).
            & taskkill /PID $entry.Value /T /F | Out-Null
            Write-Host "Stopped $($entry.Name) (pid $($entry.Value))"
        } else {
            Write-Host "$($entry.Name) was not running"
        }
    }
    Remove-Item $pidFile
} else {
    Write-Host 'Nothing recorded in .run/pids.json.'
}

if ($Keycloak) {
    & docker compose --env-file (Join-Path $root '.env') -f (Join-Path $root 'infra/compose.yaml') stop | Out-Null
    Write-Host 'Stopped Keycloak (volume kept).'
}
