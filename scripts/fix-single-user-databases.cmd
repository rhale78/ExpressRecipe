@echo off
echo ========================================
echo Fix SQL Server SINGLE_USER Databases
echo ========================================
echo.
echo This script will check all ExpressRecipe databases
echo and fix any stuck in SINGLE_USER mode
echo.

powershell -ExecutionPolicy Bypass -File "fix-single-user-databases.ps1"
