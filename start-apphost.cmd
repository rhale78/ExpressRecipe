@echo off
setlocal enabledelayedexpansion

echo ============================================
echo Starting ExpressRecipe AppHost with Diagnostics
echo ============================================
echo.

REM Check Docker first
docker ps >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Docker is not running!
    echo.
    echo Please:
    echo   1. Start Docker Desktop
    echo   2. Wait for it to fully initialize green status
    echo   3. Run this script again
    echo.
    pause
    exit /b 1
)

echo Docker is running ?
echo.

REM Set environment variables for verbose output
set ASPIRE_DASHBOARD_VERBOSE=true
set DOTNET_CLI_UI_LANGUAGE=en-US

echo Starting AppHost...
echo ============================================
echo.
echo Watch for these stages:
echo   1. "Starting ExpressRecipe AppHost..."
echo   2. "Builder created successfully"
echo   3. "SQL Server configured"
echo   4. "Redis configured"
echo   5. "RabbitMQ configured"
echo   6. "14 microservices configured"
echo   7. "Building application..."
echo   8. "Starting application..."
echo.
echo If it hangs after step 8, Docker is downloading images.
echo Check Docker Desktop to see download progress.
echo.
echo First run may take 5-15 minutes.
echo Dashboard will open at: https://localhost:15000
echo.
echo ============================================
echo.

cd src\ExpressRecipe.AppHost
dotnet run --verbosity normal

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ============================================
    echo ERROR: AppHost failed to start
    echo ============================================
    echo.
    echo Troubleshooting:
    echo   1. Check the error message above
    echo   2. Run check-apphost-readiness.cmd
    echo   3. See APPHOST_TROUBLESHOOTING.md
    echo.
    pause
    exit /b %ERRORLEVEL%
)
