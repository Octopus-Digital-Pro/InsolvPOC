@echo off
setlocal EnableDelayedExpansion

title Insolvex - Full Stack Startup
echo.
echo  ============================================
echo   INSOLVEX - Full Stack Development Startup
echo  ============================================
echo.

:: -----------------------------------------------
:: 1. Check prerequisites
:: -----------------------------------------------
echo [CHECK] Verifying prerequisites...

where docker >nul 2>&1
if !ERRORLEVEL! neq 0 (
    echo [ERROR] Docker is not installed or not in PATH.
    echo         Install Docker Desktop: https://www.docker.com/products/docker-desktop
    pause
    exit /b 1
)

where dotnet >nul 2>&1
if !ERRORLEVEL! neq 0 (
    echo [ERROR] .NET SDK is not installed or not in PATH.
    echo         Install .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)

where npm >nul 2>&1
if !ERRORLEVEL! neq 0 (
    echo [ERROR] Node.js / npm is not installed or not in PATH.
    echo         Install Node.js: https://nodejs.org
pause
    exit /b 1
)

echo         All prerequisites found.

:: -----------------------------------------------
:: 2. Start SQL Server in Docker
:: -----------------------------------------------
echo.
echo [1/6] Starting SQL Server container...
docker-compose up -d sqlserver
if !ERRORLEVEL! neq 0 (
    echo [ERROR] Failed to start SQL Server container.
 echo       Make sure Docker Desktop is running.
    pause
    exit /b 1
)

:: -----------------------------------------------
:: 3. Wait for SQL Server to be healthy
::    Uses 'docker inspect' to read the healthcheck status
::    set by docker-compose. Falls back to a simple TCP probe.
:: -----------------------------------------------
echo [2/6] Waiting for SQL Server to accept connections...

set RETRIES=0
set MAX_RETRIES=40

:WAIT_LOOP
if !RETRIES! geq !MAX_RETRIES! (
    echo.
    echo [ERROR] SQL Server did not become ready within ~80 seconds.
    echo     Check: docker logs insolvex-db
    pause
    exit /b 1
)

:: Method 1: check Docker healthcheck status
for /f "tokens=*" %%H in ('docker inspect --format "{{.State.Health.Status}}" insolvex-db 2^>nul') do (
    set "HEALTH=%%H"
)

if "!HEALTH!"=="healthy" (
    echo  SQL Server is ready!  [healthy]
    goto SQL_READY
)

:: Method 2: fallback – try a direct sqlcmd probe
::   (captures output to a temp file so ERRORLEVEL works in CMD)
docker exec insolvex-db /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "InsolvexDev2025#Strong" -Q "SELECT 1" -C -b > "%TEMP%\insolvex_sqlcheck.tmp" 2>&1
if !ERRORLEVEL! equ 0 (
    echo         SQL Server is ready!  [sqlcmd]
    goto SQL_READY
)

set /a RETRIES+=1
echo         Waiting... (!RETRIES!/!MAX_RETRIES!)  status=!HEALTH!
timeout /t 2 /nobreak >nul
goto WAIT_LOOP

:SQL_READY

:: -----------------------------------------------
:: 4. Install EF Core tools if needed
:: -----------------------------------------------
echo [3/6] Checking EF Core tools...
dotnet tool list -g 2>nul | findstr /i "dotnet-ef" >nul 2>&1
if !ERRORLEVEL! neq 0 (
    echo   Installing dotnet-ef tool...
    dotnet tool install --global dotnet-ef
)

:: -----------------------------------------------
:: 5. Apply database migrations
:: -----------------------------------------------
echo [4/6] Applying database migrations...
pushd "%~dp0Insolvex.API"
dotnet ef database update
if !ERRORLEVEL! neq 0 (
    echo [ERROR] Failed to apply migrations.
    echo       Check connection string in appsettings.json
    popd
    pause
    exit /b 1
)
popd
echo         Migrations applied successfully!

:: -----------------------------------------------
:: 6. Install npm dependencies for frontend if needed
:: -----------------------------------------------
echo [5/6] Checking frontend npm dependencies...
if not exist "%~dp0Insolvex.Web\node_modules" (
    echo Installing npm packages for Insolvex.Web...
    pushd "%~dp0Insolvex.Web"
    call npm install
    popd
)

:: -----------------------------------------------
:: 7. Launch BE and FE in parallel windows
:: -----------------------------------------------
echo [6/6] Starting backend and frontend...
echo.
echo  ============================================
echo.
echo   Backend API  : http://localhost:5000
echo   Swagger UI   : http://localhost:5000/swagger
echo   Frontend     : http://localhost:5173
echo.
echo  ============================================
echo.
echo   Demo accounts (seeded on first run):
echo.
echo     admin@insolvex.local        / Admin123!      (GlobalAdmin)
echo     practitioner@insolvex.local / Pract123!      (Practitioner)
echo     secretary@insolvex.local    / Secr123!       (Secretary)
echo.
echo  ============================================
echo.

:: Start backend in a new window
start "Insolvex API" cmd /k "cd /d "%~dp0Insolvex.API" && dotnet run --launch-profile Insolvex.API"

:: Give the API a few seconds to bind its port
echo   Waiting for API to start...
timeout /t 4 /nobreak >nul

:: Start frontend in a new window (use Insolvex.Web project folder)
start "Insolvex React" cmd /k "cd /d "%~dp0Insolvex.Web" && npm run dev"

echo.
echo   Both servers are starting in separate windows.
echo   Press any key to close this launcher (servers keep running).
echo.
pause >nul
