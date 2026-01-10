@echo off
REM Move Docker Volumes to Drive I
REM This script moves ExpressRecipe Docker volumes from C: to I:

echo.
echo Running PowerShell script to move Docker volumes...
echo.

powershell -ExecutionPolicy Bypass -File "%~dp0move-docker-volumes-to-i.ps1"

pause
