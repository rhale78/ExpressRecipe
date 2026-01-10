# AppHost Startup Troubleshooting Guide

## Issue: No Output When Starting AppHost

The AppHost not showing output is typically caused by one of these issues:

## 1. Docker Desktop Not Running ?? **MOST COMMON**

**Symptoms:**
- Application hangs with no output
- No error messages displayed

**Solution:**
1. Start Docker Desktop
2. Wait for Docker to fully initialize (green status icon)
3. Restart the AppHost

**Verification:**
```bash
docker ps
```
Should show running containers, not an error.

## 2. First-Time Container Download

**Symptoms:**
- Long wait with no visible progress
- Eventually starts after several minutes

**What's Happening:**
- SQL Server container: ~1.5 GB
- Redis container: ~100 MB
- RabbitMQ container: ~200 MB

**Solution:**
- Be patient, especially on first run
- Check Docker Desktop to see download progress
- The diagnostic output in Program.cs will now show progress

## 3. Port Conflicts

**Symptoms:**
- Error about ports already in use
- Application crashes after starting

**Check These Ports:**
- 15000, 15001 - Aspire Dashboard
- 1433 - SQL Server
- 6379 - Redis
- 5672, 15672 - RabbitMQ

**Solution:**
```bash
# Windows - find processes using ports
netstat -ano | findstr "15000"
netstat -ano | findstr "1433"
netstat -ano | findstr "6379"
netstat -ano | findstr "5672"

# Kill process by PID if needed
taskkill /PID <pid> /F
```

## 4. Missing Aspire Workload

**Symptoms:**
- Build errors related to Aspire
- Missing types or namespaces

**Solution:**
```bash
dotnet workload update
dotnet workload install aspire
```

## 5. Container Startup Failures

**Symptoms:**
- AppHost starts but services show errors
- Containers restart repeatedly

**Diagnosis:**
1. Open Docker Desktop
2. Check container logs for errors
3. Look for:
   - SQL Server initialization errors
   - Permission issues
   - Disk space issues

## Testing Strategy

### Step 1: Test with Minimal Configuration

Temporarily rename your Program.cs and use the minimal version:

```bash
cd src/ExpressRecipe.AppHost
ren Program.cs Program.Full.cs
ren Program.Minimal.txt Program.cs
dotnet run
```

This starts only:
- SQL Server
- Redis
- AuthService

If this works, the issue is with the full configuration.

**To restore full configuration:**
```bash
cd src/ExpressRecipe.AppHost
ren Program.cs Program.Minimal.cs
ren Program.Full.cs Program.cs
```

### Step 2: Check Docker Manually

```bash
# Start containers manually to test
docker run -d --name test-sqlserver -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=Your_password123" -p 1433:1433 mcr.microsoft.com/mssql/server:2022-latest

docker run -d --name test-redis -p 6379:6379 redis:latest

# Verify they're running
docker ps

# Clean up
docker stop test-sqlserver test-redis
docker rm test-sqlserver test-redis
```

### Step 3: Check Aspire Dashboard Access

Once running, the dashboard should be accessible at:
- HTTPS: https://localhost:15000
- HTTP: http://localhost:15001

If you can't access these URLs:
1. Check Windows Firewall
2. Check antivirus software
3. Try accessing from `http://127.0.0.1:15001`

## Expected Startup Output (with diagnostics)

You should now see:
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

After this, you should see the Aspire Dashboard URL and service startup logs.

## Hang Points to Watch For

The diagnostic output will help identify where it's hanging:

1. **After "Builder created"** ? Problem with Aspire SDK
2. **After "SQL Server configured"** ? Docker not running or SQL Server image download
3. **After "Redis configured"** ? Redis image download or port conflict
4. **After "RabbitMQ configured"** ? RabbitMQ image download
5. **After "Building application"** ? Compilation issue in services
6. **After "Starting application"** ? Docker container startup issue

## Quick Fixes

### Reset Everything
```bash
# Stop all containers
docker stop $(docker ps -aq)

# Remove all containers
docker rm $(docker ps -aq)

# Remove volumes (CAUTION: deletes data)
docker volume prune -f

# Restart AppHost
cd src/ExpressRecipe.AppHost
dotnet run
```

### Check Aspire Version Compatibility
```bash
dotnet --version  # Should be 10.0 or higher
dotnet workload list  # Should show aspire
```

## Environment Variables

If issues persist, try setting:

```bash
# Increase verbosity
set ASPIRE_DASHBOARD_VERBOSE=true

# Disable telemetry if it's causing issues
set ASPIRE_DISABLE_TELEMETRY=true

# Force Docker Desktop
set DOCKER_HOST=npipe:////./pipe/docker_engine
```

## Still Not Working?

1. Check the Output window in Visual Studio (View ? Output)
2. Select "Show output from: .NET Aspire"
3. Look for detailed error messages

Or run with verbose logging:
```bash
cd src/ExpressRecipe.AppHost
dotnet run --verbosity detailed
```

## Success Indicators

When working correctly, you should see:
1. Console output showing all stages
2. Docker Desktop showing 3 new containers (SQL, Redis, RabbitMQ)
3. Aspire Dashboard opens in browser automatically
4. Dashboard shows all services with status
5. Services turn green as they start successfully

## Performance Notes

**First Start:** 5-15 minutes (downloading images)
**Subsequent Starts:** 30-90 seconds
**With Persistent Containers:** 10-30 seconds

## Getting Help

If still stuck, provide:
1. The last line of console output you see
2. Docker Desktop status (running/stopped)
3. Output from `docker ps`
4. Output from `dotnet --version`
5. Output from `dotnet workload list`
