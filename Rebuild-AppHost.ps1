# Clean rebuild script for Aspire AppHost
# This ensures the Projects.* types are regenerated

Write-Host "=== Cleaning and Rebuilding Aspire AppHost ===" -ForegroundColor Cyan

$appHostPath = "src\ExpressRecipe.AppHost.New"

# Step 1: Clean the AppHost project
Write-Host "`n[1/4] Cleaning AppHost..." -ForegroundColor Yellow
Set-Location $appHostPath
dotnet clean --verbosity quiet
if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ Clean failed" -ForegroundColor Red
    exit 1
}
Write-Host "✓ Clean complete" -ForegroundColor Green

# Step 2: Delete bin and obj folders to force complete rebuild
Write-Host "`n[2/4] Removing bin/obj folders..." -ForegroundColor Yellow
if (Test-Path "bin") { Remove-Item "bin" -Recurse -Force }
if (Test-Path "obj") { Remove-Item "obj" -Recurse -Force }
Write-Host "✓ Folders removed" -ForegroundColor Green

# Step 3: Restore packages
Write-Host "`n[3/4] Restoring packages..." -ForegroundColor Yellow
dotnet restore --verbosity quiet
if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ Restore failed" -ForegroundColor Red
    exit 1
}
Write-Host "✓ Restore complete" -ForegroundColor Green

# Step 4: Build AppHost
Write-Host "`n[4/4] Building AppHost..." -ForegroundColor Yellow
dotnet build --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ Build failed" -ForegroundColor Red
    Write-Host "`nCheck for errors above. Common issues:" -ForegroundColor Yellow
    Write-Host "  - Missing project references in AppHost.csproj" -ForegroundColor Gray
    Write-Host "  - Invalid service names in AppHost.cs" -ForegroundColor Gray
    Write-Host "  - Missing database declarations" -ForegroundColor Gray
    exit 1
}
Write-Host "✓ Build complete" -ForegroundColor Green

# Step 5: Verify generated Projects class
Write-Host "`n[5/5] Verifying generated types..." -ForegroundColor Yellow
$projectsFile = "obj\Debug\net10.0\Projects.g.cs"
if (Test-Path $projectsFile) {
    $content = Get-Content $projectsFile -Raw
    if ($content -match "ExpressRecipe_GroceryStoreLocationService") {
        Write-Host "✓ Projects.ExpressRecipe_GroceryStoreLocationService found!" -ForegroundColor Green
    } else {
        Write-Host "✗ Projects.ExpressRecipe_GroceryStoreLocationService NOT found" -ForegroundColor Red
        Write-Host "`nAvailable project types:" -ForegroundColor Yellow
        $content -match 'public static class (\w+)' | Out-Null
        $matches[1] | ForEach-Object { Write-Host "  - $_" -ForegroundColor Gray }
    }
} else {
    Write-Host "✗ Projects.g.cs not found at: $projectsFile" -ForegroundColor Red
}

# Step 6: Return to root
Set-Location ..\..

Write-Host "`n=== Rebuild Complete ===" -ForegroundColor Cyan
Write-Host "`nTo run Aspire:" -ForegroundColor Yellow
Write-Host "  cd $appHostPath" -ForegroundColor Gray
Write-Host "  dotnet run" -ForegroundColor Gray
Write-Host "`nAspire Dashboard will be at: http://localhost:15000" -ForegroundColor Cyan
