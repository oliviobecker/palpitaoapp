<#
.SYNOPSIS
    Wipes all Palpitao DB data, keeping only the seeded super-admin user plus the
    seeded reference data (team catalogue + default group + admin membership).

.DESCRIPTION
    Runs scripts/reset-db-keep-admin.sql against the local Docker Postgres
    container (palpitao-postgres). Connection defaults match docker-compose.yml
    and are overridden by a root .env file if present.

    THIS IS DESTRUCTIVE. You are prompted to type the database name to confirm,
    unless -Force is given.

.PARAMETER AdminEmail
    Email of the user to keep. Default: admin@palpitao.local

.PARAMETER Force
    Skip the interactive confirmation prompt.

.EXAMPLE
    ./scripts/reset-db-keep-admin.ps1
    ./scripts/reset-db-keep-admin.ps1 -AdminEmail admin@palpitao.local -Force
#>
[CmdletBinding()]
param(
    [string]$AdminEmail = "admin@palpitao.local",
    [string]$Container  = "palpitao-postgres",
    [switch]$Force
)

$ErrorActionPreference = "Stop"

$repoRoot  = Split-Path -Parent $PSScriptRoot
$sqlFile   = Join-Path $PSScriptRoot "reset-db-keep-admin.sql"
$envFile   = Join-Path $repoRoot ".env"

if (-not (Test-Path $sqlFile)) {
    throw "SQL file not found: $sqlFile"
}

# --- Resolve connection settings (docker-compose defaults, overridable via .env) ---
$dbUser = "palpitao"
$dbName = "palpitao"
if (Test-Path $envFile) {
    foreach ($line in Get-Content $envFile) {
        if ($line -match '^\s*POSTGRES_USER\s*=\s*(.+?)\s*$') { $dbUser = $Matches[1] }
        if ($line -match '^\s*POSTGRES_DB\s*=\s*(.+?)\s*$')   { $dbName = $Matches[1] }
    }
}

$defaultGroupId = "33333333-3333-3333-3333-333333333301"

# --- Confirm the container is running ---
$running = docker ps --filter "name=$Container" --format "{{.Names}}"
if ($running -notcontains $Container) {
    throw "Container '$Container' is not running. Start it with: docker compose up -d"
}

Write-Host "Target container : $Container"
Write-Host "Database         : $dbName (user $dbUser)"
Write-Host "Keeping admin    : $AdminEmail"
Write-Host "Keeping group    : $defaultGroupId (default group) + all Teams" -ForegroundColor DarkGray
Write-Host ""

if (-not $Force) {
    Write-Host "This will PERMANENTLY DELETE all data except the admin above." -ForegroundColor Red
    $answer = Read-Host "Type the database name ('$dbName') to proceed"
    if ($answer -ne $dbName) {
        Write-Host "Aborted." -ForegroundColor Yellow
        exit 1
    }
}

# --- Execute: pipe the SQL file into psql inside the container ---
Get-Content -Raw $sqlFile | docker exec -i $Container psql `
    -U $dbUser -d $dbName `
    -v ON_ERROR_STOP=1 `
    --set=admin_email=$AdminEmail `
    --set=default_group_id=$defaultGroupId `
    -f -

if ($LASTEXITCODE -ne 0) {
    throw "psql exited with code $LASTEXITCODE - database was NOT modified (transaction rolled back)."
}

Write-Host ""
Write-Host "Done. Database wiped, admin '$AdminEmail' preserved." -ForegroundColor Green
