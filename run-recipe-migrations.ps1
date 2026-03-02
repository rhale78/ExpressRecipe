# Quick Migration Runner

# Run these migrations in order against your SQL Server database

$sqlServerHost = "localhost,1436"
$database = "ExpressRecipe.Recipes"
$migrations = @(
    "src\Services\ExpressRecipe.RecipeService\Data\Migrations\013_AddPerformanceIndexes.sql",
    "src\Services\ExpressRecipe.RecipeService\Data\Migrations\014_OptimizeIndexesForBulkOperations.sql"
)

foreach ($migration in $migrations) {
    Write-Host "Running migration: $migration" -ForegroundColor Cyan
    
    if (!(Test-Path $migration)) {
        Write-Host "ERROR: Migration file not found: $migration" -ForegroundColor Red
        exit 1
    }
    
    $sql = Get-Content $migration -Raw
    
    # Using sqlcmd (ensure it's installed)
    sqlcmd -S $sqlServerHost -d $database -Q $sql -b
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ Migration completed successfully" -ForegroundColor Green
    } else {
        Write-Host "✗ Migration failed with exit code: $LASTEXITCODE" -ForegroundColor Red
        exit $LASTEXITCODE
    }
    
    Write-Host ""
}

Write-Host "All migrations completed! 🎉" -ForegroundColor Green
Write-Host "Next: Restart your RecipeService to activate code optimizations" -ForegroundColor Yellow
