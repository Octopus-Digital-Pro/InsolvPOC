#!/usr/bin/env pwsh
# Insolvex � Full Stack Development Startup
# Usage: ./start-dev.ps1

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "  ============================================" -ForegroundColor Cyan
Write-Host " INSOLVEX - Full Stack Development Startup" -ForegroundColor Cyan
Write-Host "  ============================================" -ForegroundColor Cyan
Write-Host ""

# -----------------------------------------------
# 1. Start SQL Server in Docker
# -----------------------------------------------
Write-Host "[1/6] Starting SQL Server container..." -ForegroundColor Yellow
docker-compose up -d sqlserver
if ($LASTEXITCODE -ne 0) { throw "Failed to start SQL Server container. Is Docker Desktop running?" }

# -----------------------------------------------
# 2. Wait for SQL Server to be ready
# -----------------------------------------------
Write-Host "[2/6] Waiting for SQL Server to accept connections..." -ForegroundColor Yellow
$retries = 0
$maxRetries = 40
$ready = $false

while ($retries -lt $maxRetries) {
    # Method 1: Docker healthcheck
 $health = (docker inspect --format "{{.State.Health.Status}}" insolvex-db 2>$null).Trim()
    if ($health -eq "healthy") {
      Write-Host "   SQL Server is ready! [healthy]" -ForegroundColor Green
   $ready = $true
        break
    }

    # Method 2: Direct sqlcmd probe
    $null = docker exec insolvex-db /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "InsolvexDev2025#Strong" -Q "SELECT 1" -C -b 2>&1
    if ($LASTEXITCODE -eq 0) {
   Write-Host "        SQL Server is ready! [sqlcmd]" -ForegroundColor Green
        $ready = $true
 break
    }

    $retries++
    Write-Host "  Waiting... ($retries/$maxRetries)  status=$health"
    Start-Sleep -Seconds 2
}

if (-not $ready) {
    Write-Host "[ERROR] SQL Server did not start in time. Check: docker logs insolvex-db" -ForegroundColor Red
    exit 1
}

# -----------------------------------------------
# 3. Check EF Core tools
# -----------------------------------------------
Write-Host "[3/6] Checking EF Core tools..." -ForegroundColor Yellow
$efCheck = dotnet tool list -g 2>&1 | Select-String "dotnet-ef"
if (-not $efCheck) {
    Write-Host "        Installing dotnet-ef tool..."
    dotnet tool install --global dotnet-ef
}

# -----------------------------------------------
# 4. Apply migrations
# -----------------------------------------------
Write-Host "[4/6] Applying database migrations..." -ForegroundColor Yellow
Push-Location "$PSScriptRoot/Insolvex.API"
dotnet ef database update
if ($LASTEXITCODE -ne 0) { Pop-Location; throw "Failed to apply migrations" }
Pop-Location
Write-Host "        Migrations applied!" -ForegroundColor Green

# -----------------------------------------------
# 5. Install npm dependencies if needed
# -----------------------------------------------
Write-Host "[5/6] Checking npm dependencies..." -ForegroundColor Yellow
if (-not (Test-Path "$PSScriptRoot/node_modules")) {
    Write-Host "        Installing npm packages..."
    Push-Location $PSScriptRoot
    npm install
    Pop-Location
}

# -----------------------------------------------
# 6. Launch BE and FE
# -----------------------------------------------
Write-Host "[6/6] Starting backend and frontend..." -ForegroundColor Yellow
Write-Host ""
Write-Host "  ============================================" -ForegroundColor Cyan
Write-Host " Backend API  : http://localhost:5000"       -ForegroundColor White
Write-Host "   Swagger UI   : http://localhost:5000/swagger" -ForegroundColor White
Write-Host "   Frontend     : http://localhost:5173"       -ForegroundColor White
Write-Host "  ============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "   Demo accounts (seeded on first run):"       -ForegroundColor Gray
Write-Host "     admin@insolvex.local        / Admin123!"  -ForegroundColor Gray
Write-Host "     practitioner@insolvex.local / Pract123!" -ForegroundColor Gray
Write-Host "     secretary@insolvex.local    / Secr123!"  -ForegroundColor Gray
Write-Host ""

# Start API in background job
$apiJob = Start-Job -ScriptBlock {
    Set-Location "$using:PSScriptRoot/Insolvex.API"
    dotnet run --launch-profile Insolvex.API
}

# Wait for API to bind
Start-Sleep -Seconds 4

# Start frontend (foreground � Ctrl+C stops everything)
try {
    Push-Location $PSScriptRoot
    npm run dev
}
finally {
    Write-Host "`nStopping API..." -ForegroundColor Yellow
    Stop-Job $apiJob -ErrorAction SilentlyContinue
    Remove-Job $apiJob -Force -ErrorAction SilentlyContinue
    Pop-Location
    Write-Host "Goodbye!" -ForegroundColor Green
}
