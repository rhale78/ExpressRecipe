@echo off
REM Quick check for ProductImage migration status
echo Checking ProductImage migration status...
echo.

powershell -ExecutionPolicy Bypass -File "%~dp0check-productimage-migration.ps1"

echo.
pause
