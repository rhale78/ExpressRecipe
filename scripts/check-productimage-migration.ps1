# Script to manually apply ProductImage migration if needed
# Run this if ProductImage table is missing

Write-Host "Checking ProductService database migrations..." -ForegroundColor Cyan

# Connection string - update if needed
$connectionString = "Server=localhost,1433;Database=productdb;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True;Connection Timeout=30;"

try {
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $connection.Open()
    Write-Host "? Connected to database" -ForegroundColor Green

    # Check if ProductImage table exists
    $checkTableCmd = $connection.CreateCommand()
    $checkTableCmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'ProductImage'"
    $tableExists = [int]$checkTableCmd.ExecuteScalar()

    if ($tableExists -eq 0) {
        Write-Host "? ProductImage table does not exist" -ForegroundColor Red
        Write-Host "  Migration 011 needs to be applied" -ForegroundColor Yellow
        
        # Check if migration was recorded as applied
        $checkMigrationCmd = $connection.CreateCommand()
        $checkMigrationCmd.CommandText = "IF OBJECT_ID('__MigrationHistory', 'U') IS NOT NULL SELECT COUNT(*) FROM __MigrationHistory WHERE MigrationId = '011_CreateProductImageTable' ELSE SELECT 0"
        $migrationRecorded = [int]$checkMigrationCmd.ExecuteScalar()
        
        if ($migrationRecorded -gt 0) {
            Write-Host "  WARNING: Migration 011 is marked as applied but table doesn't exist!" -ForegroundColor Red
            Write-Host "  Removing migration record..." -ForegroundColor Yellow
            $removeMigrationCmd = $connection.CreateCommand()
            $removeMigrationCmd.CommandText = "DELETE FROM __MigrationHistory WHERE MigrationId = '011_CreateProductImageTable'"
            $removeMigrationCmd.ExecuteNonQuery() | Out-Null
            Write-Host "  ? Migration record removed" -ForegroundColor Green
        }
        
        Write-Host ""
        Write-Host "To fix this issue:" -ForegroundColor Cyan
        Write-Host "1. Restart the ProductService to auto-apply migrations" -ForegroundColor White
        Write-Host "2. OR manually run the migration SQL file:" -ForegroundColor White
        Write-Host "   src\Services\ExpressRecipe.ProductService\Data\Migrations\011_CreateProductImageTable.sql" -ForegroundColor Gray
    }
    else {
        Write-Host "? ProductImage table exists" -ForegroundColor Green
        
        # Check row count
        $countCmd = $connection.CreateCommand()
        $countCmd.CommandText = "SELECT COUNT(*) FROM ProductImage WHERE IsDeleted = 0"
        $imageCount = [int]$countCmd.ExecuteScalar()
        
        Write-Host "  ProductImage rows: $imageCount" -ForegroundColor Cyan
        
        if ($imageCount -eq 0) {
            Write-Host "  ? No images in database - check import logs for errors" -ForegroundColor Yellow
        }
    }

    # Check applied migrations
    Write-Host ""
    Write-Host "Applied migrations:" -ForegroundColor Cyan
    $migrationsCmd = $connection.CreateCommand()
    $migrationsCmd.CommandText = "IF OBJECT_ID('__MigrationHistory', 'U') IS NOT NULL SELECT MigrationId, AppliedAt FROM __MigrationHistory ORDER BY AppliedAt"
    $reader = $migrationsCmd.ExecuteReader()
    
    $count = 0
    while ($reader.Read()) {
        $migrationId = $reader.GetString(0)
        $appliedAt = $reader.GetDateTime(1)
        Write-Host "  $migrationId - $($appliedAt.ToString('yyyy-MM-dd HH:mm:ss'))" -ForegroundColor Gray
        $count++
    }
    $reader.Close()
    
    if ($count -eq 0) {
        Write-Host "  No migrations applied yet" -ForegroundColor Yellow
    }
    else {
        Write-Host "  Total: $count migrations" -ForegroundColor Cyan
    }

    $connection.Close()
}
catch {
    Write-Host "? Error: $_" -ForegroundColor Red
    Write-Host "  Make sure SQL Server is running and connection details are correct" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Done!" -ForegroundColor Green
