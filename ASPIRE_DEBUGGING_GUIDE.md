# How to Debug and Monitor Aspire AppHost

## The Problem You're Experiencing

**You're not seeing console output or hitting breakpoints in the AppHost** - This is NORMAL for Aspire!

## Why This Happens

### Console Output Redirection
- Aspire redirects all output to the **Aspire Dashboard**, not a console window
- `Console.WriteLine()` goes to dashboard logs, not visible in VS
- Use `ILogger` instead for proper logging

### Debugger Attachment
- Visual Studio doesn't attach debugger to AppHost by default
- AppHost is an orchestrator, not a service you typically debug
- Debug the **individual services**, not the AppHost itself

## Where to See What's Happening

### 1. Aspire Dashboard (PRIMARY)
**URL:** https://localhost:15000

This is where EVERYTHING happens:
- ? See all console output and logs
- ? Monitor container status
- ? View service health
- ? Check environment variables
- ? Access service endpoints
- ? View metrics and traces

**How to Access:**
1. Run the AppHost project
2. Browser should auto-open to dashboard
3. If not, manually go to `https://localhost:15000`

**Dashboard Sections:**
- **Resources** - All services, containers, databases
- **Console** - Combined log output from all resources
- **Structured Logs** - Filtered, searchable logs
- **Traces** - Distributed tracing
- **Metrics** - Performance monitoring

### 2. Visual Studio Output Window
**View ? Output ? Show output from: ".NET Aspire"**

You'll see:
- AppHost startup messages
- Container pull progress
- Service startup notifications
- Error messages

### 3. Docker Desktop
**Open Docker Desktop ? Containers**

Shows:
- SQL Server container status
- Redis container status
- RabbitMQ container status
- Download progress (first run)

## How to Verify AppHost is Running

### Check 1: Dashboard Opens
- Browser opens to `https://localhost:15000`
- Shows Aspire Dashboard with resources listed

### Check 2: Docker Containers Running
```cmd
docker ps
```
Should show:
- SQL Server container
- Redis container
- RabbitMQ container

### Check 3: Port Listening
```cmd
netstat -ano | findstr "15000"
```
Should show port 15000 in use

### Check 4: Visual Studio Output
1. Go to: **View ? Output**
2. Select: **Show output from: .NET Aspire**
3. Should see startup messages

## How to Debug (Properly)

### DON'T Debug the AppHost
The AppHost is just configuration - there's nothing to step through.

### DO Debug Individual Services

**Option 1: Attach to Running Service**
1. Start AppHost (F5)
2. Wait for services to start (check dashboard)
3. In Visual Studio: **Debug ? Attach to Process**
4. Find your service (e.g., `ExpressRecipe.AuthService.exe`)
5. Click **Attach**
6. Set breakpoints in service code
7. Hit service endpoint

**Option 2: Debug Service Directly**
1. Right-click service project (e.g., AuthService)
2. **Debug ? Start New Instance**
3. Service runs standalone (without Aspire)
4. Set breakpoints as normal

**Option 3: Multiple Startup Projects**
1. Right-click solution
2. **Properties ? Multiple startup projects**
3. Set AppHost + specific services to **Start**
4. Debugger attaches to all

## Viewing Logs

### In Aspire Dashboard
1. Go to `https://localhost:15000`
2. Click on **Console** tab (left sidebar)
3. See all output from all resources
4. Use filters to show specific service

**Filter Examples:**
- Show only errors: Filter by level = Error
- Show specific service: Filter by resource name
- Time range: Use time selector

### In Visual Studio
1. **View ? Output**
2. **Show output from: .NET Aspire**
3. Scroll to see AppHost logs

### In Docker Desktop
1. Open Docker Desktop
2. Go to **Containers** tab
3. Click on container (SQL, Redis, RabbitMQ)
4. View **Logs** tab

## Understanding AppHost Startup

### What Actually Happens

```
1. AppHost starts (you press F5)
   ?
2. Aspire reads configuration
   ?
3. Docker containers start (SQL, Redis, RabbitMQ)
   ? [This takes 30-60 seconds, or 5-15 min first time]
4. Services build
   ?
5. Services start (one by one)
   ?
6. Dashboard shows all green
   ?
7. You can now use the application
```

### Timeline
- **AppHost startup:** Instant
- **Docker pull (first time):** 5-15 minutes
- **Containers start:** 30-60 seconds
- **Services build:** 10-30 seconds each
- **Services start:** 5-10 seconds each
- **Total (first time):** 6-20 minutes
- **Total (subsequent):** 1-3 minutes

### Where to Look at Each Stage

| Stage | Where to Check |
|-------|----------------|
| AppHost starting | VS Output (.NET Aspire) |
| Docker pulling | Docker Desktop ? Images |
| Containers starting | Docker Desktop ? Containers |
| Services building | VS Output (.NET Aspire) |
| Services starting | Aspire Dashboard ? Resources |
| Services running | Aspire Dashboard (all green) |

## Troubleshooting "No Output"

### Symptom: No console output at all

**Cause:** Console output goes to dashboard, not console

**Solution:** Open dashboard at `https://localhost:15000`

### Symptom: Can't hit breakpoints

**Cause:** Debugger not attached to service processes

**Solution:** 
1. Let AppHost start all services
2. Attach debugger to specific service process
3. Or run service standalone

### Symptom: Dashboard doesn't open

**Possible Causes:**
1. Docker not running
2. Port 15000 already in use
3. AppHost crashed before dashboard started

**Check:**
```cmd
# Is Docker running?
docker ps

# Is port 15000 free?
netstat -ano | findstr "15000"

# Check VS Output for errors
View ? Output ? .NET Aspire
```

### Symptom: Dashboard shows but no services

**Possible Causes:**
1. Services failed to build
2. Services crashed on startup
3. Connection strings incorrect

**Check Dashboard:**
1. Click on service that's not green
2. View its logs
3. Look for error messages
4. Fix the error in service code
5. Restart AppHost

## Best Practices

### During Development

1. **Always check the dashboard first**
   - Don't rely on console output
   - Dashboard is your primary tool

2. **Use structured logging in services**
   ```csharp
   logger.LogInformation("User {UserId} logged in", userId);
   ```
   - Shows nicely in dashboard
   - Searchable and filterable

3. **Monitor container health**
   - Docker Desktop shows CPU/Memory
   - Dashboard shows restart counts

4. **Start small, scale up**
   - Use `Program.Minimal.txt` to test infrastructure
   - Add services one at a time if issues

### For Debugging Services

1. **Let AppHost start infrastructure**
   - SQL, Redis, RabbitMQ containers
   - Then attach debugger to services

2. **Use debug configuration**
   ```json
   // In launchSettings.json
   "ASPNETCORE_ENVIRONMENT": "Development"
   "Logging__LogLevel__Default": "Debug"
   ```

3. **Test services independently**
   - Right-click service ? Debug ? Start New Instance
   - Faster than full AppHost restart

## Modified AppHost Logging

The updated `Program.cs` now uses `ILogger` instead of `Console.WriteLine`:

```csharp
var logger = builder.Services.BuildServiceProvider().GetRequiredService<ILogger<Program>>();
logger.LogInformation("Starting ExpressRecipe AppHost");
```

**Where to see these logs:**
1. **Aspire Dashboard** ? Console tab ? Filter by "apphost"
2. **VS Output** ? Show output from: .NET Aspire
3. **Dashboard** ? Structured Logs tab

## Key Takeaways

? **AppHost console output** ? Goes to Aspire Dashboard, not console window

? **Debugging** ? Attach to individual services, not AppHost

? **Monitoring** ? Use Aspire Dashboard at `https://localhost:15000`

? **Logs** ? Dashboard Console tab shows everything

? **First run** ? Takes 5-15 minutes (downloading Docker images)

? **Subsequent runs** ? Takes 1-3 minutes

? **Don't expect** ? Console window with output

? **Don't try** ? Setting breakpoints in AppHost Program.cs (nothing to debug there)

? **Don't wait** ? At blank console (dashboard is where action is)

## Quick Reference Commands

```cmd
# Check if AppHost is running
netstat -ano | findstr "15000"

# Check Docker status
docker ps

# Check Docker images
docker images

# View container logs
docker logs <container-id>

# Restart containers
docker restart <container-id>

# Stop all containers
docker stop $(docker ps -q)

# Remove all containers
docker rm $(docker ps -aq)
```

## Need Help?

1. **Dashboard not opening?**
   - Check VS Output window for errors
   - Verify Docker is running
   - Check port 15000 availability

2. **Services not starting?**
   - Click service in dashboard
   - View its console logs
   - Look for red error messages

3. **Containers not starting?**
   - Open Docker Desktop
   - Check container status
   - View container logs

4. **Still stuck?**
   - Check `APPHOST_TROUBLESHOOTING.md`
   - Run `check-apphost-readiness.cmd`
   - View VS Output ? .NET Aspire

## The Bottom Line

**Aspire AppHost works differently than a normal console app:**
- No console window = NORMAL
- No breakpoints hit in AppHost = EXPECTED
- Dashboard is the UI = BY DESIGN

**Use the dashboard, it's awesome!** ??
