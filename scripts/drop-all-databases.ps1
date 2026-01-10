# Drop All ExpressRecipe Databases
# Use with EXTREME CAUTION - this will delete all data!

param(
    [string]$Server = "localhost,1433",
    [string]$Username = "sa",
    [string]$Password = "",
    [switch]$Confirm = $false
)

Write-Host "========================================" -ForegroundColor Yellow
Write-Host "  DROP ALL EXPRESSRECIPE DATABASES" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Yellow
Write-Host ""
Write-Host "WARNING: This will DELETE ALL DATA in:" -ForegroundColor Red
Write-Host ""

$databases = @(
    "ExpressRecipe.Auth",
    "ExpressRecipe.Users",
    "ExpressRecipe.Products",
    "ExpressRecipe.Recipes",
    "ExpressRecipe.Inventory",
    "ExpressRecipe.Scans",
    "ExpressRecipe.Shopping",
    "ExpressRecipe.MealPlanning",
    "ExpressRecipe.Pricing",
    "ExpressRecipe.Recalls",
    "ExpressRecipe.Notifications",
    "ExpressRecipe.Community",
    "ExpressRecipe.Sync",
    "ExpressRecipe.Search",
    "ExpressRecipe.Analytics"
)

foreach ($db in $databases) {
    Write-Host "  - $db" -ForegroundColor Cyan
}

Write-Host ""

if (-not $Confirm) {
    $response = Read-Host "Are you ABSOLUTELY SURE? Type 'DROP ALL' to confirm"
    if ($response -ne "DROP ALL") {
        Write-Host "Operation cancelled." -ForegroundColor Green
        exit 0
    }
}

# Get password if not provided
if ([string]::IsNullOrEmpty($Password)) {
    $securePassword = Read-Host "Enter SA password" -AsSecureString
    $BSTR = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword)
    $Password = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($BSTR)
}

Write-Host ""
Write-Host "Connecting to SQL Server..." -ForegroundColor Yellow

# Build connection string
$connectionString = "Server=$Server;User Id=$Username;Password=$Password;TrustServerCertificate=True;Connection Timeout=30"

try {
    # Load SQL Client assembly
    Add-Type -AssemblyName "System.Data.SqlClient"
    
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $connection.Open()
    
    Write-Host "Connected successfully!" -ForegroundColor Green
    Write-Host ""
    
    $dropped = 0
    $failed = 0
    
    foreach ($dbName in $databases) {
        Write-Host "Dropping database: $dbName..." -NoNewline
        
        $sql = @"
IF EXISTS (SELECT name FROM sys.databases WHERE name = N'$dbName')
BEGIN
    ALTER DATABASE [$dbName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [$dbName];
    SELECT 1 AS Dropped;
END
ELSE
BEGIN
    SELECT 0 AS Dropped;
END
"@
        
        try {
            $command = New-Object System.Data.SqlClient.SqlCommand($sql, $connection)
            $command.CommandTimeout = 300
            $result = $command.ExecuteScalar()
            
            if ($result -eq 1) {
                Write-Host " ? Dropped" -ForegroundColor Green
                $dropped++
            } else {
                Write-Host " - Not found" -ForegroundColor Gray
            }
        }
        catch {
            Write-Host " ? Failed: $($_.Exception.Message)" -ForegroundColor Red
            $failed++
        }
    }
    
    $connection.Close()
    
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Yellow
    Write-Host "Summary:" -ForegroundColor Yellow
    Write-Host "  Dropped: $dropped" -ForegroundColor Green
    Write-Host "  Failed:  $failed" -ForegroundColor Red
    Write-Host "  Total:   $($databases.Count)" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Yellow
    
    if ($dropped -gt 0) {
        Write-Host ""
        Write-Host "All databases have been dropped!" -ForegroundColor Green
        Write-Host "Restart your services to recreate them with fresh schemas." -ForegroundColor Yellow
    }
}
catch {
    Write-Host ""
    Write-Host "ERROR: Failed to connect to SQL Server" -ForegroundColor Red
    Write-Host "Message: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "Troubleshooting:" -ForegroundColor Yellow
    Write-Host "  1. Verify SQL Server is running: docker ps" -ForegroundColor Gray
    Write-Host "  2. Check connection string: $connectionString" -ForegroundColor Gray
    Write-Host "  3. Verify password is correct" -ForegroundColor Gray
    exit 1
}
