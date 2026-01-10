# Docker Container Runtime Fix

## Problem
Aspire AppHost shows error: "Container runtime not installed"

**Root Cause:** Docker Desktop is running, but the Docker CLI cannot connect to the daemon.

## Diagnosis Results
? Docker Desktop process is running (4 instances detected)
? Docker CLI cannot execute `docker ps` command
? Docker daemon not accessible from command line

## Solutions

### Solution 1: Restart Docker Desktop (Recommended)

1. **Close Docker Desktop completely:**
   - Right-click Docker icon in system tray
   - Click "Quit Docker Desktop"
   - Wait 10 seconds

2. **Start Docker Desktop:**
   - Open Start Menu
   - Search for "Docker Desktop"
   - Click to launch
   - **Wait for it to fully start** (icon turns solid, shows "Docker Desktop is running")

3. **Verify it works:**
   ```cmd
   docker ps
   ```
   Should show running containers (or empty list if none running)

4. **Try AppHost again:**
   ```cmd
   cd src\ExpressRecipe.AppHost.New
   dotnet run
   ```

### Solution 2: Check Docker Desktop Settings

If restart doesn't work:

1. **Open Docker Desktop**

2. **Go to Settings ? General**

3. **Verify these settings:**
   - ? "Use the WSL 2 based engine" (if on Windows 10/11)
   - ? "Expose daemon on tcp://localhost:2375 without TLS" (optional, for troubleshooting)

4. **Click "Apply & Restart"**

5. **Wait for Docker to fully restart** (green indicator in bottom-left)

### Solution 3: Check Windows PATH

The `docker` command might not be in your PATH.

1. **Check Docker installation location:**
   ```cmd
   where docker
   ```

2. **If not found, add Docker to PATH:**
   - Docker is typically at: `C:\Program Files\Docker\Docker\resources\bin`
   - Add to System PATH environment variable
   - Restart terminal/VS

### Solution 4: Use WSL 2 Backend

If using Windows:

1. **Enable WSL 2:**
   ```cmd
   wsl --install
   wsl --set-default-version 2
   ```

2. **In Docker Desktop Settings:**
   - General ? Enable "Use the WSL 2 based engine"
   - Resources ? WSL Integration ? Enable for your distro

3. **Restart Docker Desktop**

### Solution 5: Reinstall Docker Desktop

If all else fails:

1. **Uninstall Docker Desktop:**
   - Settings ? Apps ? Docker Desktop ? Uninstall
   - Delete `C:\Program Files\Docker` folder
   - Delete `%APPDATA%\Docker` folder

2. **Download latest Docker Desktop:**
   - https://www.docker.com/products/docker-desktop/

3. **Install and configure**

4. **Test with `docker ps`**

## Verification Steps

After applying any solution, verify with these commands:

### Step 1: Test Docker CLI
```cmd
docker --version
```
**Expected:** `Docker version XX.X.X, build XXXXXXX`

### Step 2: Test Docker Daemon
```cmd
docker ps
```
**Expected:** List of containers (or empty table if none running)

### Step 3: Test Docker Info
```cmd
docker info
```
**Expected:** Detailed Docker system information

### Step 4: Test AppHost
```cmd
cd src\ExpressRecipe.AppHost.New
dotnet run
```
**Expected:** Aspire starts, containers begin downloading/starting

## Common Docker Desktop Startup Issues

### Symptom: Docker Desktop stuck on "Starting..."

**Solution:**
1. Open Task Manager
2. End all "Docker Desktop" processes
3. Delete `%APPDATA%\Docker\` folder
4. Restart Docker Desktop

### Symptom: "Docker Desktop requires a newer WSL kernel version"

**Solution:**
```cmd
wsl --update
wsl --shutdown
```
Then restart Docker Desktop

### Symptom: "Hardware assisted virtualization and data execution protection must be enabled"

**Solution:**
1. Restart computer
2. Enter BIOS/UEFI (usually F2, F10, or Del during boot)
3. Enable:
   - Intel VT-x / AMD-V (Virtualization Technology)
   - Intel VT-d / AMD IOMMU (if available)
4. Save and exit BIOS
5. Boot Windows
6. Start Docker Desktop

## Why This Happens

### Aspire Requires Containers
Aspire AppHost uses Docker containers for:
- SQL Server (database)
- Redis (caching)
- RabbitMQ (messaging)

Without Docker, these cannot start.

### Docker Desktop Initialization
Docker Desktop has a multi-stage startup:
1. Process starts (visible in Task Manager)
2. Background services initialize
3. WSL 2 VM starts (if using WSL 2)
4. Docker daemon becomes ready
5. CLI can connect

**Problem:** Sometimes Docker Desktop shows as "running" but daemon isn't ready.

## Alternative: Skip Containers (Testing Only)

If you want to test without Docker temporarily, create a minimal AppHost:

```csharp
// src/ExpressRecipe.AppHost.New/AppHost.cs
var builder = DistributedApplication.CreateBuilder(args);

// No containers - just service projects
var authService = builder.AddProject<Projects.ExpressRecipe_AuthService>("authservice");
var webApp = builder.AddProject<Projects.ExpressRecipe_BlazorWeb>("webapp")
    .WithReference(authService);

builder.Build().Run();
```

**Note:** Services will fail without databases, but you can verify Aspire itself works.

## Quick Test Script

Run this to verify Docker is working:

```cmd
@echo off
echo Testing Docker...
echo.

docker --version
if errorlevel 1 (
    echo [FAIL] Docker CLI not found
    exit /b 1
)

echo [PASS] Docker CLI found
echo.

docker ps
if errorlevel 1 (
    echo [FAIL] Docker daemon not accessible
    echo Try restarting Docker Desktop
    exit /b 1
)

echo [PASS] Docker daemon accessible
echo.
echo Docker is ready for Aspire!
pause
```

Save as `test-docker.cmd` and run it.

## Expected Working State

When Docker is properly working, you should see:

```cmd
C:\> docker ps
CONTAINER ID   IMAGE     COMMAND   CREATED   STATUS    PORTS     NAMES
```

(Empty list is fine - means Docker daemon is running but no containers yet)

## Next Steps After Fix

1. **Verify Docker works:**
   ```cmd
   docker ps
   ```

2. **Run new AppHost:**
   ```cmd
   test-new-apphost.cmd
   ```

3. **Watch for:**
   - Aspire starts successfully
   - Browser opens to dashboard
   - Containers begin downloading (first time) or starting (subsequent)

## Still Having Issues?

If Docker still doesn't work:

1. **Check Windows Event Viewer:**
   - Windows Logs ? Application
   - Look for Docker errors

2. **Check Docker Desktop logs:**
   - Docker Desktop ? Troubleshoot ? View logs
   - Look for errors during startup

3. **Try Docker Desktop diagnostic:**
   - Docker Desktop ? Troubleshoot ? Run diagnostics
   - Upload results for Docker support

4. **Post in Docker forums:**
   - https://forums.docker.com/
   - Include diagnostic results

## Summary

**Problem:** Docker Desktop running but CLI can't connect to daemon

**Most Likely Fix:** Restart Docker Desktop completely (quit + relaunch)

**Verification:** `docker ps` should work without error

**Then:** Run `test-new-apphost.cmd` to test Aspire
