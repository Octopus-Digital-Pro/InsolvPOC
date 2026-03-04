@echo off
echo ============================================
echo  Insolvex Development Environment
echo ============================================
echo.
echo Starting Docker (SQL Server)...
start /B docker-compose up -d
timeout /t 3 /nobreak >nul

echo Starting .NET API (port 5000)...
start cmd /k "cd Insolvex.API && dotnet watch run"

echo Waiting for API to start on port 5000...
:WAIT_API
powershell -NoProfile -Command "try { $t = New-Object System.Net.Sockets.TcpClient; $t.Connect('localhost',5000); $t.Close(); exit 0 } catch { exit 1 }" >nul 2>&1
if errorlevel 1 (
    timeout /t 1 /nobreak >nul
    goto WAIT_API
)
echo API is ready!

echo Starting React Dev Server (port 5173)...
start cmd /k "cd Insolvex.Web && npm run dev"

echo.
echo  API:      http://localhost:5000
echo  Swagger:  http://localhost:5000/swagger
echo  Frontend: http://localhost:5173
echo ============================================
