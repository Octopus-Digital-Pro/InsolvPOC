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

echo Starting React Dev Server (port 5173)...
start cmd /k "cd Insolvex.Web && npm run dev"

echo.
echo  API:      http://localhost:5000
echo  Swagger:  http://localhost:5000/swagger
echo  Frontend: http://localhost:5173
echo ============================================
