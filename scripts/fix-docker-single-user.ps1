# Fix SQL Server databases stuck in SINGLE_USER mode (Docker Container on port 1436)

Write-Host "Fixing SQL Server databases in Docker container (port 1436)..." -ForegroundColor Cyan
Write-Host ""

# Docker SQL Server connection - Windows Auth won't work with Docker, use sa
$serverInstance = "localhost,1436"
$username = "sa"

# Prompt for password securely
Write-Host "Enter SQL Server 'sa' password:" -ForegroundColor Yellow
$securePassword = Read-Host -AsSecureString
$password = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword))

$connectionString = "Server=$serverInstance;User Id=$username;Password=$password;TrustServerCertificate=True;Connection Timeout=30;"

Write-Host ""
Write-Host "Connecting to Docker SQL Server on port 1436..." -ForegroundColor Cyan

# List of databases to check and fix
$databases = @(
    "productdb",
    "userdb",
    "authdb",
    "recipedb",
    "inventorydb",
    "shoppingdb",
    "mealplandb",
    "notificationdb",
    "recalldb",
    "pricedb",
    "scannerdb",
    "communitydb",
    "searchdb",
    "syncdb",
    "analyticsdb"
)

try {
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $connection.Open()
    Write-Host "? Connected to SQL Server (Docker container on port 1436)" -ForegroundColor Green
    Write-Host ""

    $foundDatabases = $false
    $fixedCount = 0

    foreach ($dbName in $databases) {
        # Check if database exists
        $checkDbCmd = $connection.CreateCommand()
        $checkDbCmd.CommandText = "SELECT COUNT(*) FROM sys.databases WHERE name = @DbName"
        $checkDbCmd.Parameters.AddWithValue("@DbName", $dbName) | Out-Null
        $dbExists = [int]$checkDbCmd.ExecuteScalar()
        
        if ($dbExists -eq 0) {
            continue  # Skip non-existent databases quietly
        }
        
        $foundDatabases = $true
        Write-Host "Checking database: $dbName" -ForegroundColor Yellow
        
        # Check current state
        $checkStateCmd = $connection.CreateCommand()
        $checkStateCmd.CommandText = @"
            SELECT 
                state_desc,
                user_access_desc
            FROM sys.databases 
            WHERE name = @DbName
"@
        $checkStateCmd.Parameters.AddWithValue("@DbName", $dbName) | Out-Null
        $reader = $checkStateCmd.ExecuteReader()
        
        if ($reader.Read()) {
            $stateDesc = $reader.GetString(0)
            $userAccessDesc = $reader.GetString(1)
            $reader.Close()
            
            Write-Host "  State: $stateDesc | Access: $userAccessDesc" -ForegroundColor Cyan
            
            if ($userAccessDesc -eq "SINGLE_USER") {
                Write-Host "  ? SINGLE_USER mode detected - fixing..." -ForegroundColor Red
                
                # Force kill all connections and set to MULTI_USER
                $fixCmd = $connection.CreateCommand()
                $fixCmd.CommandTimeout = 60
                $fixCmd.CommandText = @"
                    -- Kill all connections
                    DECLARE @kill varchar(8000) = '';
                    SELECT @kill = @kill + 'KILL ' + CONVERT(varchar(5), spid) + ';'
                    FROM master..sysprocesses
                    WHERE dbid = DB_ID(@DbName) AND spid <> @@SPID;
                    
                    EXEC(@kill);
                    
                    -- Set to MULTI_USER
                    DECLARE @sql NVARCHAR(MAX) = N'ALTER DATABASE [' + @DbName + N'] SET MULTI_USER WITH ROLLBACK IMMEDIATE;';
                    EXEC sp_executesql @sql;
"@
                $fixCmd.Parameters.AddWithValue("@DbName", $dbName) | Out-Null
                
                try {
                    $fixCmd.ExecuteNonQuery() | Out-Null
                    Write-Host "  ? FIXED - now in MULTI_USER mode" -ForegroundColor Green
                    $fixedCount++
                }
                catch {
                    Write-Host "  ? Error: $_" -ForegroundColor Red
                }
            }
            else {
                Write-Host "  ? OK" -ForegroundColor Green
            }
        }
        else {
            $reader.Close()
            Write-Host "  Could not read database state" -ForegroundColor Gray
        }
        
        Write-Host ""
    }
    
    $connection.Close()
    
    Write-Host ""
    if (-not $foundDatabases) {
        Write-Host "? No ExpressRecipe databases found in Docker container" -ForegroundColor Yellow
        Write-Host "  This is normal if services haven't started yet" -ForegroundColor Gray
    }
    elseif ($fixedCount -eq 0) {
        Write-Host "? All databases are OK - no fixes needed!" -ForegroundColor Green
    }
    else {
        Write-Host "? Fixed $fixedCount database(s)!" -ForegroundColor Green
    }
}
catch {
    Write-Host ""
    Write-Host "? Connection Error: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "Troubleshooting:" -ForegroundColor Yellow
    Write-Host "  1. Check Docker container is running:" -ForegroundColor White
    Write-Host "     docker ps | findstr sql" -ForegroundColor Gray
    Write-Host "  2. Verify port 1436 is mapped:" -ForegroundColor White
    Write-Host "     docker port <container-name>" -ForegroundColor Gray
    Write-Host "  3. Check password is correct (from .env file)" -ForegroundColor White
}

Write-Host ""
Write-Host "Press any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
