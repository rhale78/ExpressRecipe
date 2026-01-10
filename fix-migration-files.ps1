# Fix all service project files to copy migration SQL files to output directory

Write-Host "Fixing project files to include migration SQL files..." -ForegroundColor Cyan
Write-Host ""

$services = @(
    "AnalyticsService",
    "CommunityService", 
    "InventoryService",
    "MealPlanningService",
    "NotificationService",
    "PriceService",
    "RecipeService",
    "ScannerService",
    "SearchService",
    "ShoppingService",
    "SyncService"
)

$itemGroupXml = @"
  <ItemGroup>
    <None Include="Data\Migrations\*.sql" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
"@

foreach ($service in $services) {
    $projectFile = "src\Services\ExpressRecipe.$service\ExpressRecipe.$service.csproj"
    $migrationsDir = "src\Services\ExpressRecipe.$service\Data\Migrations"
    
    if (Test-Path $projectFile) {
        Write-Host "Processing ExpressRecipe.$service..." -ForegroundColor Yellow
        
        # Check if migration directory exists
        if (Test-Path $migrationsDir) {
            $content = Get-Content $projectFile -Raw
            
            # Check if ItemGroup already exists
            if ($content -notmatch 'Data\\Migrations\\\*\.sql') {
                Write-Host "  Adding migration ItemGroup to $service" -ForegroundColor Green
                
                # Create backup
                Copy-Item $projectFile "$projectFile.backup" -Force
                
                # Add ItemGroup before closing Project tag
                $newContent = $content -replace '</Project>', "$itemGroupXml`n</Project>"
                Set-Content $projectFile $newContent -NoNewline
                
                Write-Host "  ? Fixed $service" -ForegroundColor Green
            } else {
                Write-Host "  ? $service already configured" -ForegroundColor Gray
            }
        } else {
            Write-Host "  - No migrations directory in $service" -ForegroundColor Gray
        }
    } else {
        Write-Host "  WARNING: Project file not found: $projectFile" -ForegroundColor Red
    }
    Write-Host ""
}

Write-Host ""
Write-Host "Done! Project files have been updated." -ForegroundColor Green
Write-Host ""
Write-Host "IMPORTANT: You must rebuild the solution for changes to take effect:" -ForegroundColor Yellow
Write-Host "  dotnet clean" -ForegroundColor Cyan
Write-Host "  dotnet build src\ExpressRecipe.AppHost.New\ExpressRecipe.AppHost.New.csproj" -ForegroundColor Cyan
Write-Host ""
Write-Host "Press any key to continue..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
