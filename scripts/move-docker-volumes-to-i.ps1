# Move Docker Volumes to Drive I
# This script helps clean up old volumes on C: and ensures new volumes use I:

Write-Host "=== ExpressRecipe Docker Volume Migration ===" -ForegroundColor Cyan
Write-Host ""

# Check if drive I exists
if (-not (Test-Path "I:\")) {
    Write-Host "ERROR: Drive I:\ not found!" -ForegroundColor Red
    exit 1
}

# Check available space on I:
$driveI = Get-PSDrive I
$freeSpaceGB = [math]::Round($driveI.Free / 1GB, 2)
Write-Host "Drive I: has $freeSpaceGB GB free space" -ForegroundColor Green
Write-Host ""

# Create directory structure on I:
Write-Host "Creating directory structure on I:..." -ForegroundColor Yellow
$basePath = "I:\DockerVolumes\ExpressRecipe"
$dirs = @("sqlserver", "redis", "rabbitmq")

foreach ($dir in $dirs) {
    $fullPath = Join-Path $basePath $dir
    if (-not (Test-Path $fullPath)) {
        New-Item -ItemType Directory -Path $fullPath -Force | Out-Null
        Write-Host "  Created: $fullPath" -ForegroundColor Green
    } else {
        Write-Host "  Exists: $fullPath" -ForegroundColor Gray
    }
}
Write-Host ""

# List Docker volumes
Write-Host "Current Docker volumes:" -ForegroundColor Yellow
docker volume ls | Select-String "expressrecipe"
Write-Host ""

# Prompt to remove old volumes
Write-Host "Do you want to remove old ExpressRecipe Docker volumes from C:? (This will delete all data!)" -ForegroundColor Yellow
Write-Host "Make sure all containers are stopped first!" -ForegroundColor Red
$response = Read-Host "Type 'yes' to continue"

if ($response -eq "yes") {
    Write-Host ""
    Write-Host "Stopping all ExpressRecipe containers..." -ForegroundColor Yellow
    docker ps --filter "name=expressrecipe" --format "{{.ID}}" | ForEach-Object {
        docker stop $_ 2>$null
    }

    Write-Host "Removing old ExpressRecipe volumes..." -ForegroundColor Yellow
    docker volume ls --filter "name=expressrecipe" --format "{{.Name}}" | ForEach-Object {
        Write-Host "  Removing volume: $_" -ForegroundColor Red
        docker volume rm $_ 2>$null
    }

    Write-Host ""
    Write-Host "Old volumes removed!" -ForegroundColor Green
    Write-Host "Next time you start the AppHost, new volumes will be created on I:" -ForegroundColor Cyan
} else {
    Write-Host "Skipped volume removal" -ForegroundColor Gray
}

Write-Host ""
Write-Host "=== Additional Cleanup ===" -ForegroundColor Cyan
Write-Host "To free up even more space on C:, you can:" -ForegroundColor Yellow
Write-Host "  1. Clean Docker system: docker system prune -a --volumes" -ForegroundColor White
Write-Host "  2. Clean build artifacts: dotnet clean" -ForegroundColor White
Write-Host "  3. Remove unused images: docker image prune -a" -ForegroundColor White
Write-Host ""

# Show disk space
Write-Host "Current disk space:" -ForegroundColor Cyan
Get-PSDrive C, I | Select-Object Name, @{Name="Used(GB)";Expression={[math]::Round($_.Used/1GB,2)}}, @{Name="Free(GB)";Expression={[math]::Round($_.Free/1GB,2)}}, @{Name="Total(GB)";Expression={[math]::Round(($_.Used+$_.Free)/1GB,2)}} | Format-Table -AutoSize

Write-Host "Done!" -ForegroundColor Green
