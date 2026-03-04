# GroceryStore Service Not Showing in Aspire Dashboard - Troubleshooting

## Problem
The **GroceryStoreLocationService** and **grocerystoredb** database are configured in AppHost but not appearing in the Aspire dashboard.

## ✅ Verified Configuration

### 1. AppHost Configuration (CORRECT)
```csharp
// Line 29 - Database is defined
var groceryStoreDb = sqlServer.AddDatabase("grocerystoredb", "ExpressRecipe.GroceryStores");

// Lines 57-59 - Service is defined
var groceryStoreService = builder.AddProject<Projects.ExpressRecipe_GroceryStoreLocationService>("grocerystoreservice")
    .WithReference(groceryStoreDb)
    .WithReference(redis);
```

### 2. AppHost Project Reference (CORRECT)
```xml
<!-- Line 25 in ExpressRecipe.AppHost.New.csproj -->
<ProjectReference Include="..\Services\ExpressRecipe.GroceryStoreLocationService\ExpressRecipe.GroceryStoreLocationService.csproj" />
```

### 3. Service Configuration (CORRECT)
- `Program.cs` exists and compiles ✅
- No compilation errors ✅
- Service uses Aspire defaults ✅

---

## 🔍 Likely Causes

### Cause 1: Service Failing to Start (Most Common)
The service is registered but crashes during startup.

**How to check:**
1. Open **Aspire Dashboard** (usually http://localhost:15888)
2. Look for "grocerystoreservice" in the Resources list
3. Check the status - it might show:
   - ❌ **Exited** - Service crashed
   - ⚠️ **Failed** - Startup error
   - ⏸️ **Not started** - Never launched

4. **Click on the service name** to see logs
5. Look for error messages in the console output

### Cause 2: Database Connection String Missing
The service can't connect to `grocerystoredb`.

**Check Program.cs:**
```csharp
// Line 20 - Should match the database name in AppHost
builder.AddSqlServerClient("grocerystoredb");
```

The connection string name MUST match the database name in AppHost (line 29).

### Cause 3: Migration Failure
Database migrations might be failing on startup.

**Check if migrations run:**
```csharp
// In Program.cs, look for:
await MigrationRunner.RunMigrationsAsync(...);
```

Common migration errors:
- SQL syntax errors
- Missing migration files
- Wrong connection string
- SQL Server not started

### Cause 4: Aspire Dashboard Not Refreshed
Sometimes the dashboard needs a refresh.

**Try:**
1. Stop the AppHost (Ctrl+C)
2. Close the Aspire Dashboard browser tab
3. Restart AppHost (`dotnet run`)
4. Open dashboard again

---

## 🔧 Diagnostic Steps

### Step 1: Check Aspire Dashboard
1. Navigate to **http://localhost:15888** (or the port shown in console)
2. Look in the **Resources** section
3. Find **grocerystoreservice**
4. Click on it and check:
   - **State**: Should be "Running"
   - **Logs**: Look for error messages
   - **Environment**: Check connection strings

### Step 2: Check AppHost Console Output
When you run the AppHost, look for:
```
✅ Good output:
- grocerystoreservice: Running HTTP/1.1 localhost:5XXX
- grocerystoredb: Ready

❌ Bad output:
- grocerystoreservice: Exited with code 1
- grocerystoreservice: Failed to start
- Error connecting to database
```

### Step 3: Build the Service Independently
Test if the service can build and run standalone:

```powershell
cd src/Services/ExpressRecipe.GroceryStoreLocationService
dotnet build
```

If it fails to build, fix the compilation errors first.

### Step 4: Check Connection String
In **appsettings.json**, verify the connection string name:

```json
{
  "ConnectionStrings": {
    "grocerystoredb": "Server=..." // Must match AppHost database name
  }
}
```

### Step 5: Test SQL Server Connection
Make sure SQL Server container is running:

```powershell
docker ps | Select-String sqlserver
```

Should show a running container.

### Step 6: Check for Port Conflicts
Another service might be using the same port.

```powershell
# Check what's using ports 5000-5050
netstat -ano | Select-String "5[0-9]{3}"
```

---

## 🚀 Solutions

### Solution 1: Force Rebuild
```powershell
# Clean everything
.\Clean-BuildArtifacts.ps1

# Rebuild solution
dotnet build

# Run AppHost
cd src/ExpressRecipe.AppHost.New
dotnet run
```

### Solution 2: Check Service Logs in Aspire
1. Open dashboard
2. Click **grocerystoreservice** (if it appears at all)
3. View the **Console** or **Logs** tab
4. Look for the FIRST error message
5. Fix that error first

### Solution 3: Remove and Re-add Service
Sometimes Aspire gets confused. Try temporarily removing it:

```csharp
// Comment out in AppHost.cs (lines 57-59)
// var groceryStoreService = builder.AddProject<Projects.ExpressRecipe_GroceryStoreLocationService>("grocerystoreservice")
//     .WithReference(groceryStoreDb)
//     .WithReference(redis);
```

Run AppHost, then uncomment and run again.

### Solution 4: Verify Database Name Match
**In AppHost.cs:**
```csharp
var groceryStoreDb = sqlServer.AddDatabase("grocerystoredb", "ExpressRecipe.GroceryStores");
```

**In GroceryStoreLocationService Program.cs:**
```csharp
builder.AddSqlServerClient("grocerystoredb"); // MUST MATCH
```

**These must be identical!**

### Solution 5: Check Migration Files
Verify migrations exist:
```
src/Services/ExpressRecipe.GroceryStoreLocationService/Data/Migrations/
  └── 001_CreateGroceryStoreTables.sql
```

If missing, the service might crash on startup.

### Solution 6: Disable Migrations Temporarily
To test if migrations are the issue, comment out migration code:

```csharp
// In Program.cs, comment out:
// await MigrationRunner.RunMigrationsAsync(...);
```

If service starts without migrations, the SQL is the problem.

---

## 📊 Expected Dashboard View

When working correctly, you should see:

```
Resources:
├── 📦 sqlserver (Container) - Running
│   ├── 🗄️ grocerystoredb - Ready
│   ├── 🗄️ productdb - Ready
│   └── ... (other databases)
├── 🔴 redis (Container) - Running
├── 🐰 messaging (Container) - Running
└── Services:
    ├── ✅ grocerystoreservice - Running HTTP/1.1 localhost:5XXX
    ├── ✅ priceservice - Running HTTP/1.1 localhost:5XXX
    └── ... (other services)
```

---

## 🐛 Common Startup Errors

### Error 1: "Cannot connect to database"
```
Fix: Make sure SQL Server container is running
     Wait for it to be "Ready" in dashboard
```

### Error 2: "Migration failed"
```
Fix: Check SQL syntax in migration files
     Make sure table names don't already exist
```

### Error 3: "Port already in use"
```
Fix: Change port in launchSettings.json
     Or kill the process using that port
```

### Error 4: "Project not found"
```
Fix: Rebuild the AppHost project
     Make sure .csproj file exists
```

---

## 📝 Quick Checklist

- [ ] GroceryStoreLocationService project compiles
- [ ] AppHost.csproj has project reference (line 25)
- [ ] AppHost.cs defines groceryStoreDb (line 29)
- [ ] AppHost.cs defines groceryStoreService (lines 57-59)
- [ ] Program.cs has correct connection string name ("grocerystoredb")
- [ ] Migration file exists: `001_CreateGroceryStoreTables.sql`
- [ ] SQL Server container is running
- [ ] No port conflicts
- [ ] Aspire dashboard is open and refreshed
- [ ] Check service logs in dashboard for errors

---

## 🎯 Next Steps

1. **Start AppHost** and watch console output closely
2. **Open Aspire Dashboard** (http://localhost:15888)
3. **Find grocerystoreservice** in Resources
4. **Click on it** and check the Logs tab
5. **Copy the error message** (if any)
6. **Share the error** for specific troubleshooting

The service IS configured correctly in code, so it's likely a runtime startup issue. The logs will tell us exactly what's wrong!

---

**Most Common Fix:** Check the Aspire Dashboard logs for the service - it's probably failing to start due to a database connection or migration error.
