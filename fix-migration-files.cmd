@echo off
REM Fix all service project files to copy migration SQL files to output directory

echo Fixing project files to include migration SQL files...
echo.

set SERVICES=AnalyticsService CommunityService InventoryService MealPlanningService NotificationService PriceService RecipeService ScannerService SearchService ShoppingService SyncService

for %%s in (%SERVICES%) do (
    set PROJECT_FILE=src\Services\ExpressRecipe.%%s\ExpressRecipe.%%s.csproj
    
    if exist !PROJECT_FILE! (
        echo Processing ExpressRecipe.%%s...
        
        REM Check if migration directory exists
        if exist src\Services\ExpressRecipe.%%s\Data\Migrations (
            REM Check if ItemGroup already exists
            findstr /C:"Data\\Migrations\\*.sql" "!PROJECT_FILE!" >nul
            
            if errorlevel 1 (
                echo   Adding migration ItemGroup to %%s
                
                REM Create backup
                copy "!PROJECT_FILE!" "!PROJECT_FILE!.backup" >nul
                
                REM Add ItemGroup before closing Project tag
                powershell -Command "(Get-Content '!PROJECT_FILE!') -replace '</Project>', '  <ItemGroup>^n    <None Include=\"Data\Migrations\*.sql\" CopyToOutputDirectory=\"PreserveNewest\" />^n  </ItemGroup>^n</Project>' | Set-Content '!PROJECT_FILE!'"
                
                echo   ? Fixed %%s
            ) else (
                echo   ? %%s already configured
            )
        ) else (
            echo   - No migrations directory in %%s
        )
    ) else (
        echo   WARNING: Project file not found: !PROJECT_FILE!
    )
    echo.
)

echo.
echo Done! Project files have been updated.
echo.
echo IMPORTANT: You must rebuild the solution for changes to take effect:
echo   dotnet build src\ExpressRecipe.AppHost.New\ExpressRecipe.AppHost.New.csproj
echo.
pause
