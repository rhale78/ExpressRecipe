# Clean all bin and obj folders to fix VS reference issues
Write-Host "Cleaning all bin and obj folders..." -ForegroundColor Cyan

$foldersToDelete = Get-ChildItem -Path $PSScriptRoot -Include bin,obj -Recurse -Directory

$count = 0
foreach ($folder in $foldersToDelete) {
    try {
        Remove-Item $folder.FullName -Recurse -Force -ErrorAction Stop
        $count++
        Write-Host "Deleted: $($folder.FullName)" -ForegroundColor Green
    }
    catch {
        Write-Host "Failed to delete: $($folder.FullName) - $($_.Exception.Message)" -ForegroundColor Yellow
    }
}

Write-Host "`n✅ Cleaned $count folders" -ForegroundColor Green
Write-Host "`n📋 Next steps:" -ForegroundColor Cyan
Write-Host "   1. Close Visual Studio completely"
Write-Host "   2. Reopen the solution"
Write-Host "   3. Build → Rebuild Solution"
Write-Host "`nThis will force VS to rebuild all project references.`n"
