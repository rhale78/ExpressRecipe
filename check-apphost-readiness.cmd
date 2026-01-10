@echo off
echo ============================================
echo ExpressRecipe AppHost Diagnostic Check
echo ============================================
echo.

echo [1/6] Checking .NET SDK version...
dotnet --version
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: .NET SDK not found!
    goto :end
)
echo.

echo [2/6] Checking Aspire workload...
dotnet workload list | findstr aspire
if %ERRORLEVEL% NEQ 0 (
    echo WARNING: Aspire workload may not be installed
    echo Run: dotnet workload install aspire
) else (
    echo OK: Aspire workload found
)
echo.

echo [3/6] Checking Docker...
docker --version
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Docker not found or not running!
    echo Please start Docker Desktop
    goto :end
)
echo.

echo [4/6] Checking Docker daemon status...
docker ps >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Docker daemon not running!
    echo Please start Docker Desktop and wait for it to fully initialize
    goto :end
) else (
    echo OK: Docker is running
)
echo.

echo [5/6] Checking for port conflicts...
echo Checking Aspire Dashboard ports (15000, 15001)...
netstat -an | findstr ":15000" | findstr LISTENING
if %ERRORLEVEL% EQU 0 (
    echo WARNING: Port 15000 is already in use
)
netstat -an | findstr ":15001" | findstr LISTENING
if %ERRORLEVEL% EQU 0 (
    echo WARNING: Port 15001 is already in use
)

echo Checking SQL Server port (1433)...
netstat -an | findstr ":1433" | findstr LISTENING
if %ERRORLEVEL% EQU 0 (
    echo WARNING: Port 1433 is already in use (may conflict with SQL Server container)
)

echo Checking Redis port (6379)...
netstat -an | findstr ":6379" | findstr LISTENING
if %ERRORLEVEL% EQU 0 (
    echo WARNING: Port 6379 is already in use (may conflict with Redis container)
)

echo Checking RabbitMQ ports (5672, 15672)...
netstat -an | findstr ":5672" | findstr LISTENING
if %ERRORLEVEL% EQU 0 (
    echo WARNING: Port 5672 is already in use (may conflict with RabbitMQ)
)
netstat -an | findstr ":15672" | findstr LISTENING
if %ERRORLEVEL% EQU 0 (
    echo WARNING: Port 15672 is already in use (may conflict with RabbitMQ management)
)
echo.

echo [6/6] Checking existing containers...
docker ps -a --filter "name=sqlserver" --filter "name=redis" --filter "name=rabbitmq"
echo.

echo ============================================
echo Diagnostic Summary
echo ============================================
echo.
echo If all checks passed:
echo   1. Docker is running: ?
echo   2. No port conflicts: ?
echo   3. .NET SDK installed: ?
echo.
echo You can now try running the AppHost:
echo   cd src\ExpressRecipe.AppHost
echo   dotnet run
echo.
echo Watch for console output showing startup progress.
echo.
echo If AppHost hangs after "Starting application..." message:
echo   - Check Docker Desktop for container status
echo   - First run may take 5-15 minutes downloading images
echo   - Dashboard will open at https://localhost:15000
echo.
echo For more help, see APPHOST_TROUBLESHOOTING.md
echo.

:end
pause
