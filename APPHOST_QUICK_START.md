# Quick Start: Diagnosing AppHost Startup Issues

## ?? IMPORTANT: How Aspire AppHost Works

### You Won't See Console Output - This is NORMAL!

**Aspire redirects ALL output to the Aspire Dashboard, not a console window.**

- ? No console window with text = **EXPECTED**
- ? Can't step through AppHost code = **BY DESIGN**
- ? Open browser to `https://localhost:15000` = **THIS IS WHERE YOU LOOK**

## The Aspire Dashboard IS Your Console

When you run the AppHost:
1. Browser should automatically open to `https://localhost:15000`
2. **This dashboard shows EVERYTHING:**
   - All log output
   - Container status
   - Service health
   - Error messages

## How to Know If It's Working

### ? Success Indicators
1. Browser opens to `https://localhost:15000` (Aspire Dashboard)
2. Dashboard shows list of services and containers
3. Resources turn green as they start
4. Docker Desktop shows 3 containers running

### ? Failure Indicators
1. Browser doesn't open at all
2. Error message in Visual Studio Output window
3. Docker Desktop shows no containers
4. Port 15000 shows error message

## Quick Diagnostic Steps

### Step 1: Check Visual Studio Output
1. In Visual Studio: **View ? Output**
2. **Show output from:** Select **".NET Aspire"**
3. Look for error messages or startup log

### Step 2: Try to Open Dashboard Manually
Open browser and go to: `https://localhost:15000`

- **Dashboard loads?** ? AppHost is running!
- **Connection refused?** ? AppHost not running
- **Takes forever?** ? Containers downloading (first run)

### Step 3: Check Docker
```bash
docker ps
```
Should show 3 containers (or being created):
- SQL Server
- Redis  
- RabbitMQ

### Step 4: Check Port 15000
```bash
netstat -ano | findstr "15000"
```
Should show port in use if AppHost is running.

## TL;DR - Run These Commands

```bash
# 1. Check if everything is ready
check-apphost-readiness.cmd

# 2. If checks pass, start AppHost with diagnostics
start-apphost.cmd

# 3. Open dashboard manually if browser doesn't auto-open
start https://localhost:15000
```

## What You'll See Now

### Before (No Output)
```
[Nothing - just hangs]
```

### After (With Diagnostics)
```
Starting ExpressRecipe AppHost...
Builder created successfully
Configuring infrastructure services...
SQL Server configured
15 databases configured
Redis configured
RabbitMQ configured
Configuring microservices...
14 microservices configured
Configuring frontend applications...
Blazor web app configured
Building application...
Starting application... (this may take a while on first run while Docker images are downloaded)
If Docker is not running, the application will hang here.
Once started, the Aspire Dashboard will be available at https://localhost:15000 or http://localhost:15001
```

## Most Likely Issue: Docker Not Running

**If you see no output or it hangs after "Starting application...":**

1. Open Docker Desktop
2. Wait for green status icon
3. Run `start-apphost.cmd` again

**If Docker is running and it still hangs:**
- **First run?** Images are downloading (5-15 minutes)
- **Check:** Docker Desktop ? Containers ? See download progress
- **Be patient:** SQL Server container is 1.5 GB

## Files Created for You

| File | What It Does |
|------|--------------|
| `check-apphost-readiness.cmd` | Checks Docker, ports, .NET |
| `start-apphost.cmd` | Starts AppHost with diagnostics |
| `APPHOST_TROUBLESHOOTING.md` | Complete troubleshooting guide |
| `APPHOST_DIAGNOSTICS_SOLUTION.md` | Technical details |
| `src/ExpressRecipe.AppHost/Program.Minimal.txt` | Minimal test configuration |

## Modified Files

| File | What Changed |
|------|--------------|
| `src/ExpressRecipe.AppHost/Program.cs` | Added console output at each stage |

## Quick Fixes

### "Docker is not running"
```bash
# Start Docker Desktop and wait for green status
```

### "Port already in use"
```bash
# Find what's using the port
netstat -ano | findstr "15000"

# Kill the process (replace PID)
taskkill /PID <pid> /F
```

### "Still not working"
```bash
# Try minimal configuration
cd src/ExpressRecipe.AppHost
ren Program.cs Program.Full.cs
ren Program.Minimal.txt Program.cs
dotnet run

# Restore when done
ren Program.cs Program.Minimal.cs
ren Program.Full.cs Program.cs
```

## What to Expect

### Timeline
- **Configuration:** Instant (you'll see console messages)
- **Building:** 5-10 seconds
- **First container pull:** 5-15 minutes (one-time)
- **Container startup:** 30-90 seconds
- **Dashboard opens:** Automatically in browser

### Success Indicators
? All console messages appear
? Docker Desktop shows 3 containers running
? Browser opens to https://localhost:15000
? Dashboard shows all services
? Services turn green as they start

## Still Stuck?

1. **Check console output** - Note the last message you see
2. **Read the guides:**
   - `APPHOST_TROUBLESHOOTING.md` - Detailed solutions
   - `APPHOST_DIAGNOSTICS_SOLUTION.md` - Technical details
3. **Run diagnostic check:**
   ```bash
   check-apphost-readiness.cmd
   ```

## Common Scenarios

### Scenario 1: Hangs immediately with no output
**Cause:** Program not running or crashed before any output
**Fix:** Check that Docker Desktop is running

### Scenario 2: Shows messages then hangs at "Starting application..."
**Cause:** Downloading Docker images (first run)
**Fix:** Be patient, check Docker Desktop for progress

### Scenario 3: Shows messages then error about Docker
**Cause:** Docker not running or not accessible
**Fix:** Start Docker Desktop, wait for green status

### Scenario 4: Shows all messages but dashboard doesn't open
**Cause:** Browser didn't auto-open or port conflict
**Fix:** Manually go to https://localhost:15000

## Next Steps After Successful Start

1. Explore the **Aspire Dashboard** at https://localhost:15000
2. Click on services to see their logs and status
3. Verify all services eventually show green status
4. Test accessing service endpoints
5. Refer to main documentation for development workflow

## Need More Help?

The diagnostic output will tell you exactly where it's failing:
- Last console message = what was happening when it failed
- No message = Docker or environment issue
- Error message = specific problem to solve

Check `APPHOST_TROUBLESHOOTING.md` for solutions to specific error messages.

## Understanding the Output

Each line tells you what's being configured:

```
Starting ExpressRecipe AppHost...          ? Program started
Builder created successfully               ? Aspire initialized
Configuring infrastructure services...     ? About to add containers
SQL Server configured                      ? SQL container added
15 databases configured                    ? All databases ready
Redis configured                           ? Redis container added
RabbitMQ configured                        ? RabbitMQ container added
Configuring microservices...              ? About to add services
14 microservices configured               ? All services added
Configuring frontend applications...      ? About to add web app
Blazor web app configured                 ? Web app added
Building application...                    ? Compiling configuration
Starting application...                    ? Running containers
[Hang point for Docker operations]        ? Downloads/starts containers
Dashboard URL appears                      ? Success!
```

If it hangs between any two messages, you know what failed.
