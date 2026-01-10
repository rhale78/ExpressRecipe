# AppHost Startup Diagnostics - Implementation Summary

## Problem
The AppHost shows no output when started, making it impossible to diagnose startup issues.

## Root Causes (Most Likely)
1. **Docker Desktop not running** - Aspire requires Docker for infrastructure containers
2. **Silent image download** - First run downloads large container images with no visible progress
3. **No diagnostic output** - Original code had no console feedback during startup

## Solution Implemented

### 1. Enhanced Program.cs with Diagnostic Output
**File:** `src/ExpressRecipe.AppHost/Program.cs`

Added console output at every stage:
```csharp
Console.WriteLine("Starting ExpressRecipe AppHost...");
Console.WriteLine("Builder created successfully");
Console.WriteLine("SQL Server configured");
Console.WriteLine("15 databases configured");
Console.WriteLine("Redis configured");
Console.WriteLine("RabbitMQ configured");
Console.WriteLine("14 microservices configured");
Console.WriteLine("Blazor web app configured");
Console.WriteLine("Building application...");
Console.WriteLine("Starting application...");
Console.WriteLine("If Docker is not running, the application will hang here.");
```

**Benefits:**
- Identifies exactly where startup hangs
- Provides helpful context about what's happening
- Shows dashboard URL when ready

### 2. Minimal Test Configuration
**File:** `src/ExpressRecipe.AppHost/Program.Minimal.txt`

Created a stripped-down version with only:
- SQL Server (1 container)
- Redis (1 container)
- AuthService (1 service)

**Usage:**
```bash
cd src/ExpressRecipe.AppHost
ren Program.cs Program.Full.cs
ren Program.Minimal.txt Program.cs
dotnet run
```

**To restore:**
```bash
ren Program.cs Program.Minimal.cs
ren Program.Full.cs Program.cs
```

**Benefits:**
- Faster startup for testing
- Isolates infrastructure issues
- Less resource intensive

### 3. Diagnostic Check Script
**File:** `check-apphost-readiness.cmd`

Automated checks for:
- ? .NET SDK version (10.0+)
- ? Aspire workload installed
- ? Docker installed and running
- ? Port availability (15000, 15001, 1433, 6379, 5672, 15672)
- ? Existing containers

**Usage:**
```bash
check-apphost-readiness.cmd
```

**Output Example:**
```
[1/6] Checking .NET SDK version...
10.0.101

[2/6] Checking Aspire workload...
OK: Aspire workload found

[3/6] Checking Docker...
Docker version 24.0.7

[4/6] Checking Docker daemon status...
OK: Docker is running

[5/6] Checking for port conflicts...
No conflicts detected

[6/6] Checking existing containers...
None found
```

### 4. Startup Script with Diagnostics
**File:** `start-apphost.cmd`

One-command startup with:
- Pre-flight Docker check
- Environment variable configuration
- Verbose output
- Stage-by-stage progress explanation
- Error handling and troubleshooting hints

**Usage:**
```bash
start-apphost.cmd
```

### 5. Comprehensive Troubleshooting Guide
**File:** `APPHOST_TROUBLESHOOTING.md`

Complete guide covering:
- Common issues and solutions
- Docker-related problems
- Port conflicts
- First-time setup
- Performance expectations
- Testing strategies
- Manual verification steps

## How to Use This Solution

### Quick Start (Recommended)
1. Run the diagnostic check:
   ```bash
   check-apphost-readiness.cmd
   ```

2. If all checks pass, start the AppHost:
   ```bash
   start-apphost.cmd
   ```

3. Watch the console output to see progress

### If It Hangs

**At "Starting application..." message:**
- **Most likely:** Docker is downloading images (first run)
- **Check:** Open Docker Desktop ? Containers
- **Look for:** SQL Server, Redis, RabbitMQ downloading
- **Wait:** 5-15 minutes on first run
- **Expected:** Eventually shows dashboard URL

**Before "Starting application..." message:**
- Note which stage it stopped at (see console output)
- Refer to "Hang Points" section in APPHOST_TROUBLESHOOTING.md
- The stage name tells you what failed

### Testing with Minimal Config

If the full AppHost doesn't work:
```bash
cd src/ExpressRecipe.AppHost
ren Program.cs Program.Full.cs
ren Program.Minimal.txt Program.cs
dotnet run
```

This tests with just 3 components instead of 18.

## Expected Behavior

### First Run (with diagnostic output)
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

[Long pause here while images download - check Docker Desktop for progress]

info: Aspire.Hosting.DistributedApplication[0]
      Aspire version: 13.1.0
info: Aspire.Hosting.DistributedApplication[0]
      Distributed application starting.
info: Aspire.Hosting.Dashboard[0]
      Dashboard listening on: https://localhost:15000
```

### Subsequent Runs
Much faster (30-90 seconds) as images are cached.

## Key Files Modified/Created

| File | Purpose | Type |
|------|---------|------|
| `src/ExpressRecipe.AppHost/Program.cs` | Enhanced with diagnostics | Modified |
| `src/ExpressRecipe.AppHost/Program.Minimal.txt` | Minimal test config | New |
| `APPHOST_TROUBLESHOOTING.md` | Comprehensive guide | New |
| `check-apphost-readiness.cmd` | Pre-flight checks | New |
| `start-apphost.cmd` | Enhanced startup | New |

## Docker Requirements

### Containers Created
1. **SQL Server 2022** (~1.5 GB image)
   - Port: 1433
   - 15 databases

2. **Redis** (~100 MB image)
   - Port: 6379

3. **RabbitMQ with Management** (~200 MB image)
   - AMQP Port: 5672
   - Management UI: 15672

### First Run Download Time
- Fast Internet (100 Mbps): ~5 minutes
- Average Internet (25 Mbps): ~10-15 minutes
- Slow Internet (10 Mbps): ~20-30 minutes

## Aspire Dashboard

Once running, the dashboard provides:
- Real-time service status
- Logs from all services
- Resource metrics
- Dependency visualization
- Direct links to service endpoints

**Access:**
- HTTPS: https://localhost:15000 (auto-opens)
- HTTP: http://localhost:15001

## Success Criteria

? Console shows all startup stages
? No hanging after "Starting application..."
? Docker shows 3 running containers
? Dashboard opens automatically
? All services show green status
? No error messages in console

## Troubleshooting Flowchart

```
No output when starting?
  ?? Run check-apphost-readiness.cmd
  ?    ?? Docker not running? ? Start Docker Desktop
  ?    ?? Port conflicts? ? See port resolution in guide
  ?    ?? All checks pass? ? Use start-apphost.cmd
  ?
  ?? Hangs after "Starting application..."?
  ?    ?? First run? ? Wait 5-15 min (downloading images)
  ?    ?? Check Docker Desktop ? See container download progress
  ?    ?? Still stuck? ? Try Program.Minimal.cs
  ?
  ?? Hangs before "Starting application..."?
  ?    ?? Note the last console message
  ?    ?? See "Hang Points" section in guide
  ?
  ?? Error messages?
       ?? See specific error solutions in guide
```

## Next Steps After Successful Start

1. **Verify Dashboard:** All services should show up
2. **Check Health:** Services should be green
3. **Test Endpoints:** Click service URLs in dashboard
4. **View Logs:** Use dashboard to see service logs
5. **Monitor Resources:** Check CPU/memory usage

## Notes

- Diagnostic console output is intentionally verbose
- You can remove it later once everything works
- Keep Program.Minimal.txt for troubleshooting
- Docker containers persist between runs (ContainerLifetime.Persistent)
- To reset: Stop and remove containers in Docker Desktop

## Performance Tips

- Use WSL 2 backend for Docker Desktop (faster)
- Allocate 4+ GB RAM to Docker
- Enable "Use the new Virtualization framework" in Docker settings
- Close unused containers to free resources

## Additional Resources

- Aspire Documentation: https://learn.microsoft.com/dotnet/aspire/
- Docker Desktop: https://www.docker.com/products/docker-desktop/
- .NET Aspire Community: https://github.com/dotnet/aspire
