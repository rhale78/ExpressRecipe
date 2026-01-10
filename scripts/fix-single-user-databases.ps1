# Fix SQL Server databases stuck in SINGLE_USER mode
# This happens when database operations fail or are interrupted

Write-Host "Fixing SQL Server databases stuck in SINGLE_USER mode..." -ForegroundColor Cyan
Write-Host ""

# Connection string - update if needed
$serverInstance = "localhost,1436"
$username = "sa"
$password = "sa"

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
    $connectionString = "Server=$serverInstance;User Id=$username;Password=$password;TrustServerCertificate=True;Connection Timeout=30;"
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $connection.Open()
    Write-Host "✓ Connected to SQL Server" -ForegroundColor Green
    Write-Host ""

    foreach ($dbName in $databases) {
        Write-Host "Checking database: $dbName" -ForegroundColor Yellow
        
        # Check if database exists
        $checkDbCmd = $connection.CreateCommand()
        $checkDbCmd.CommandText = "SELECT COUNT(*) FROM sys.databases WHERE name = @DbName"
        $checkDbCmd.Parameters.AddWithValue("@DbName", $dbName) | Out-Null
        $dbExists = [int]$checkDbCmd.ExecuteScalar()
        
        if ($dbExists -eq 0) {
            Write-Host "  Database does not exist - skipping" -ForegroundColor Gray
            Write-Host ""
            continue
        }
        
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
            
            Write-Host "  Current state: $stateDesc" -ForegroundColor Cyan
            Write-Host "  User access: $userAccessDesc" -ForegroundColor Cyan
            
            if ($userAccessDesc -eq "SINGLE_USER") {
                Write-Host "  ⚠ Database is in SINGLE_USER mode - fixing..." -ForegroundColor Red
                
                # Force kill all connections and set to MULTI_USER
                $fixCmd = $connection.CreateCommand()
                $fixCmd.CommandTimeout = 60
                $fixCmd.CommandText = @"
                    -- Kill all connections to the database
                    DECLARE @kill varchar(8000) = '';
                    SELECT @kill = @kill + 'KILL ' + CONVERT(varchar(5), spid) + ';'
                    FROM master..sysprocesses
                    WHERE dbid = DB_ID(@DbName) AND spid <> @@SPID;
                    
                    EXEC(@kill);
                    
                    -- Set database to MULTI_USER mode with immediate rollback
                    ALTER DATABASE [$dbName] SET MULTI_USER WITH ROLLBACK IMMEDIATE;
"@
                $fixCmd.CommandText = $fixCmd.CommandText.Replace('$dbName', $dbName)
                $fixCmd.Parameters.AddWithValue("@DbName", $dbName) | Out-Null
                
                try {
                    $fixCmd.ExecuteNonQuery() | Out-Null
                    Write-Host "  ✓ Fixed - database is now in MULTI_USER mode" -ForegroundColor Green
                }
                catch {
                    Write-Host "  ✗ Error fixing database: $_" -ForegroundColor Red
                }
            }
            else {
                Write-Host "  ✓ Database is OK ($userAccessDesc)" -ForegroundColor Green
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
    Write-Host "✅ Database check complete!" -ForegroundColor Green
}
catch {
    Write-Host "✗ Error: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "Common causes:" -ForegroundColor Yellow
    Write-Host "  1. SQL Server is not running" -ForegroundColor Gray
    Write-Host "  2. Wrong connection details (check server, username, password)" -ForegroundColor Gray
    Write-Host "  3. SQL Server not accepting TCP connections on port 1433" -ForegroundColor Gray
}

Write-Host ""
Write-Host "Press any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
