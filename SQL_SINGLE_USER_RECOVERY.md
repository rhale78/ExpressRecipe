# SQL Server SINGLE_USER Mode Recovery

## Problem
SQL Server databases are stuck in SINGLE_USER mode and cannot be accessed. This typically happens when:
- A database drop operation was interrupted or failed
- The application crashed during a database management operation
- The DatabaseManager set SINGLE_USER mode but didn't complete the operation

## Symptoms
```
Cannot open database "productdb" requested by the login. The login failed.
Login failed for user 'sa'.
```

Or when checking database state:
```sql
SELECT name, user_access_desc 
FROM sys.databases 
WHERE name LIKE '%db'

-- Shows: SINGLE_USER instead of MULTI_USER
```

## Quick Fix

### Option 1: Run the Recovery Script (Easiest)
```cmd
scripts\fix-single-user-databases.cmd
```

This will automatically:
1. Check all ExpressRecipe databases
2. Identify any in SINGLE_USER mode
3. Kill all connections
4. Reset to MULTI_USER mode

### Option 2: Manual SQL Fix
If you know the specific database name:

```sql
-- Kill all connections
DECLARE @kill varchar(8000) = '';
SELECT @kill = @kill + 'KILL ' + CONVERT(varchar(5), spid) + ';'
FROM master..sysprocesses
WHERE dbid = DB_ID('productdb') AND spid <> @@SPID;

EXEC(@kill);

-- Set to MULTI_USER
ALTER DATABASE [productdb] SET MULTI_USER WITH ROLLBACK IMMEDIATE;
```

Replace `productdb` with your database name.

### Option 3: Fix All Databases at Once (SQL)
```sql
-- Generate fix commands for all SINGLE_USER databases
SELECT 
    'ALTER DATABASE [' + name + '] SET MULTI_USER WITH ROLLBACK IMMEDIATE;' AS FixCommand
FROM sys.databases
WHERE user_access_desc = 'SINGLE_USER'
  AND name NOT IN ('master', 'tempdb', 'model', 'msdb');

-- Then execute each command
```

## Prevention

The `DatabaseManager.cs` has been updated to:
1. **Always reset to MULTI_USER first** before attempting to drop
2. **Auto-recover on failure** by setting back to MULTI_USER if drop fails
3. **Log helpful error messages** directing you to the recovery script

### Updated DatabaseManager Behavior
```csharp
// Before drop: Reset to MULTI_USER (in case it's stuck)
ALTER DATABASE [db] SET MULTI_USER WITH ROLLBACK IMMEDIATE;

// Then set to SINGLE_USER for drop
ALTER DATABASE [db] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
DROP DATABASE [db];

// If drop fails: Auto-recover to MULTI_USER
// (prevents database from being left in SINGLE_USER state)
```

## Troubleshooting

### Script fails with "Cannot connect to SQL Server"
1. Check if SQL Server is running:
   ```powershell
   Get-Service MSSQL*
   ```

2. Verify connection details in script:
   - Server: `localhost,1433`
   - Username: `sa`
   - Password: Check your `.env` file

3. Test connection:
   ```powershell
   sqlcmd -S localhost,1433 -U sa -P "YourStrong@Passw0rd" -Q "SELECT @@VERSION"
   ```

### Database still shows SINGLE_USER after running script
1. Check if there are active connections:
   ```sql
   SELECT spid, loginame, hostname, program_name
   FROM master..sysprocesses
   WHERE dbid = DB_ID('productdb');
   ```

2. Manually kill connections and retry:
   ```sql
   KILL <spid>;  -- For each spid from above
   ALTER DATABASE [productdb] SET MULTI_USER WITH ROLLBACK IMMEDIATE;
   ```

### Multiple databases affected
Run the script - it checks all ExpressRecipe databases automatically.

## Files Created

1. **scripts/fix-single-user-databases.ps1** - PowerShell recovery script
2. **scripts/fix-single-user-databases.cmd** - Windows command wrapper
3. **src/ExpressRecipe.Data.Common/DatabaseManager.cs** - Updated with auto-recovery

## When to Use

- ? After application crashes during database operations
- ? After interrupting database drop/recreate operations
- ? When getting "cannot open database" login errors
- ? Before restarting services if databases are inaccessible
- ? As part of environment cleanup/reset

## Example Output

```
Checking database: productdb
  Current state: ONLINE
  User access: SINGLE_USER
  ? Database is in SINGLE_USER mode - fixing...
  ? Fixed - database is now in MULTI_USER mode

Checking database: userdb
  Current state: ONLINE
  User access: MULTI_USER
  ? Database is OK (MULTI_USER)

? Database check complete!
```

## Alternative: Restart SQL Server

If the script doesn't work, restarting SQL Server will also fix the issue:
```powershell
Restart-Service MSSQL$SQLEXPRESS
# or
Restart-Service MSSQLSERVER
```

However, the script is preferred as it's faster and doesn't interrupt other databases.
