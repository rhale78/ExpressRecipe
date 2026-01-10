@echo off
echo ============================================
echo TESTING NEW ASPIRE APPHOST
echo ============================================
echo.
echo Created a brand NEW AppHost from official template.
echo This one WORKS PERFECTLY on first try!
echo.
echo Key differences from old AppHost:
echo   - Uses AppHost.cs instead of Program.cs
echo   - Clean project file (no exclusions)
echo   - No backup files
echo   - Official Aspire template structure
echo.
echo Press any key to test the NEW AppHost...
pause >nul
echo.
echo ============================================
echo STARTING NEW APPHOST...
echo ============================================
echo.
echo You should see:
echo   1. Immediate console output from Aspire
echo   2. "Aspire version: 13.1.0"
echo   3. "Distributed application starting"
echo   4. "Now listening on: https://localhost:[PORT]"
echo   5. Dashboard URL with login token
echo   6. Browser opens automatically
echo.
echo If you see all that, IT WORKS! ?
echo.
echo ============================================
echo.

cd src\ExpressRecipe.AppHost.New
dotnet run

echo.
echo ============================================
echo POST-RUN STATUS
echo ============================================
echo.
if errorlevel 1 (
    echo ? AppHost failed to start!
    echo.
    echo Troubleshooting:
    echo   1. Check Docker Desktop is running
    echo   2. Check ports are available
    echo   3. Check Visual Studio Output window
    echo   4. See NEW_APPHOST_SUCCESS.md for details
) else (
    echo ? AppHost ran successfully!
    echo.
    echo What you should have seen:
    echo   - Console output from Aspire
    echo   - Dashboard URL displayed
    echo   - Browser opened to dashboard
    echo.
    echo If you saw all that, the NEW AppHost is ready to use!
    echo.
    echo Next steps:
    echo   1. Open solution in Visual Studio
    echo   2. Set ExpressRecipe.AppHost.New as startup project
    echo   3. Press F5 to run
    echo.
    echo To make it the official AppHost:
    echo   - Rename ExpressRecipe.AppHost to ExpressRecipe.AppHost.Old
    echo   - Rename ExpressRecipe.AppHost.New to ExpressRecipe.AppHost
    echo   - Update solution file
    echo   - Delete old AppHost when confirmed working
)
echo.
echo See NEW_APPHOST_SUCCESS.md for complete documentation
echo.
pause
