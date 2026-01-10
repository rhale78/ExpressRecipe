@echo off
echo ============================================
echo Docker Container Runtime Diagnostic
echo ============================================
echo.

echo [1/4] Checking if Docker Desktop is running...
tasklist /FI "IMAGENAME eq Docker Desktop.exe" 2>NUL | find /I /N "Docker Desktop.exe">NUL
if "%ERRORLEVEL%"=="0" (
    echo [PASS] Docker Desktop process is running
) else (
    echo [FAIL] Docker Desktop is NOT running
    echo.
    echo ACTION REQUIRED:
    echo   1. Open Start Menu
    echo   2. Search for "Docker Desktop"
    echo   3. Launch Docker Desktop
    echo   4. Wait for it to fully start green status
    echo   5. Run this script again
    echo.
    pause
    exit /b 1
)
echo.

echo [2/4] Checking Docker CLI is available...
where docker >nul 2>&1
if errorlevel 1 (
    echo [FAIL] Docker CLI not found in PATH
    echo.
    echo ACTION REQUIRED:
    echo   Docker Desktop is running but 'docker' command not found.
    echo   This usually means:
    echo     - Docker Desktop just started wait 30 seconds
    echo     - Docker not in PATH variable
    echo     - Docker Desktop installation is incomplete
    echo.
    echo Try:
    echo   1. Wait 30 seconds for Docker to fully initialize
    echo   2. Run this script again
    echo   3. If still fails, restart Docker Desktop completely
    echo.
    pause
    exit /b 1
)
echo [PASS] Docker CLI found
echo.

echo [3/4] Testing Docker daemon connection...
docker ps >nul 2>&1
if errorlevel 1 (
    echo [FAIL] Cannot connect to Docker daemon
    echo.
    echo This is the problem preventing Aspire from starting!
    echo.
    echo SOLUTION:
    echo   1. Quit Docker Desktop completely:
    echo      - Right-click Docker icon in system tray
    echo      - Click "Quit Docker Desktop"
    echo      - Wait 10 seconds
    echo.
    echo   2. Start Docker Desktop again:
    echo      - Open Start Menu
    echo      - Search "Docker Desktop"
    echo      - Launch it
    echo      - Wait for solid icon green at bottom
    echo.
    echo   3. Run this script again to verify
    echo.
    echo Alternatively, try:
    echo   - Docker Desktop Settings -^> General -^> "Use WSL 2 based engine"
    echo   - Click "Apply ^& Restart"
    echo   - Wait for full restart
    echo.
    pause
    exit /b 1
)
echo [PASS] Docker daemon is accessible
echo.

echo [4/4] Getting Docker version info...
docker --version
echo.
docker info | findstr "Server Version"
echo.

echo ============================================
echo DIAGNOSIS COMPLETE
echo ============================================
echo.
echo ? Docker Desktop is running
echo ? Docker CLI is available
echo ? Docker daemon is accessible
echo ? Docker version information retrieved
echo.
echo Docker is ready for Aspire!
echo.
echo You can now run the AppHost:
echo   test-new-apphost.cmd
echo.
echo Or manually:
echo   cd src\ExpressRecipe.AppHost.New
echo   dotnet run
echo.
pause
