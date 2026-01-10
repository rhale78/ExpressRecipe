@echo off
echo ============================================
echo TESTING APPHOST FIX
echo ============================================
echo.
echo The AppHost was broken because Program.cs was excluded from compilation.
echo This has been FIXED.
echo.
echo You should now see console output immediately!
echo.
echo Press any key to test...
pause >nul
echo.
echo ============================================
echo STARTING APPHOST...
echo ============================================
echo.

cd src\ExpressRecipe.AppHost
dotnet run

echo.
echo ============================================
echo.
echo If you saw console output above, IT WORKS!
echo.
echo Expected output:
echo   - "Starting MINIMAL ExpressRecipe AppHost for diagnostics..."
echo   - "Builder created"
echo   - "Building..."
echo   - "Starting..."
echo   - "Dashboard should be at: https://localhost:15000"
echo.
echo If you saw that, the fix worked!
echo.
echo Next steps:
echo   1. Browser should open to dashboard
echo   2. Gradually uncomment services in Program.cs
echo   3. Test each addition one at a time
echo.
pause
