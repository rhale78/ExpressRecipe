# UserService.Tests Reference Resolution Fix

## Problem

The **ExpressRecipe.UserService.Tests** project shows compilation errors claiming it cannot find types from **ExpressRecipe.UserService**, even though:

- ✅ Both projects exist in the solution
- ✅ The project reference is correctly configured
- ✅ All types (`IUserFavoritesRepository`, `IUserProductRatingRepository`, `IFamilyRelationshipRepository`, `UserFavoritesController`) are **public** and exist
- ✅ The namespaces are correct (`ExpressRecipe.UserService.Data`, `ExpressRecipe.UserService.Controllers`)

## Root Cause

This is a **Visual Studio IntelliSense/project reference caching issue** in VS 2026 (18.4.0-insiders). The IDE's internal cache is stale and doesn't recognize the project reference.

## Solution Steps

### Option 1: Quick Fix (Recommended)

1. **Close Visual Studio completely**
2. **Run the cleanup script:**
   ```powershell
   .\Clean-BuildArtifacts.ps1
   ```
3. **Reopen the solution in Visual Studio**
4. **Build → Rebuild Solution** (Ctrl+Shift+B)

### Option 2: Manual Fix

1. **In Visual Studio:**
   - Right-click on **ExpressRecipe.UserService.Tests** project
   - Select **Unload Project**
   - Right-click again → **Reload Project**

2. **If that doesn't work:**
   - Close Visual Studio
   - Manually delete these folders:
     ```
     src/Services/ExpressRecipe.UserService/bin
     src/Services/ExpressRecipe.UserService/obj
     src/Tests/ExpressRecipe.UserService.Tests/bin
     src/Tests/ExpressRecipe.UserService.Tests/obj
     ```
   - Reopen Visual Studio
   - Rebuild Solution

### Option 3: Nuclear Option

If the above don't work:

1. **Close Visual Studio**
2. **Delete `.vs` folder** in solution root (hidden folder)
3. **Run cleanup script** (`.\Clean-BuildArtifacts.ps1`)
4. **Reopen solution**
5. **Rebuild**

## Verification

After the fix, verify these types are now recognized:

```csharp
using ExpressRecipe.UserService.Controllers;  // UserFavoritesController
using ExpressRecipe.UserService.Data;         // IUserFavoritesRepository, etc.
```

In `UserFavoritesControllerTests.cs`:
- Line 15: `Mock<IUserFavoritesRepository>` should compile
- Line 16: `Mock<IUserProductRatingRepository>` should compile
- Line 17: `Mock<ILogger<UserFavoritesController>>` should compile
- Line 18: `UserFavoritesController` should compile

In `FamilyMembersControllerTests.cs`:
- Line 20: `Mock<IFamilyRelationshipRepository>` should compile

## Technical Details

### Project Reference (Correct)

From `ExpressRecipe.UserService.Tests.csproj`:
```xml
<ItemGroup>
  <ProjectReference Include="..\..\Services\ExpressRecipe.UserService\ExpressRecipe.UserService.csproj" />
  <ProjectReference Include="..\..\ExpressRecipe.Shared\ExpressRecipe.Shared.csproj" />
</ItemGroup>
```

### Solution Configuration (Correct)

From `ExpressRecipe.sln`:
```
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "ExpressRecipe.UserService", 
  "src\Services\ExpressRecipe.UserService\ExpressRecipe.UserService.csproj", 
  "{26814460-00DF-471A-B074-9D362A07133C}"

Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "ExpressRecipe.UserService.Tests", 
  "src\Tests\ExpressRecipe.UserService.Tests\ExpressRecipe.UserService.Tests.csproj", 
  "{26814460-00DF-471A-B074-9D362A07133D}"
```

### Types Exist (Verified)

All interfaces are **public** in:
- `src/Services/ExpressRecipe.UserService/Data/UserFavoritesRepository.cs`
- `src/Services/ExpressRecipe.UserService/Data/UserProductRatingRepository.cs`
- `src/Services/ExpressRecipe.UserService/Data/FamilyRelationshipRepository.cs`

Controller is **public** in:
- `src/Services/ExpressRecipe.UserService/Controllers/UserFavoritesController.cs`

## Why This Happens

Visual Studio 2026 (especially insiders/preview builds) can have issues with:
1. **Project reference caching** - Old metadata cached
2. **IntelliSense database** - Out of sync with actual files
3. **MSBuild cache** - Stale assembly references
4. **Design-time builds** - Background compilation failing

The cleanup script forces VS to rebuild all these caches.

## Prevention

To avoid this in the future:
1. **Restart VS after major changes** (adding projects, restructuring)
2. **Use "Rebuild Solution"** instead of "Build" when in doubt
3. **Keep VS updated** (insider builds can have bugs)
4. **Clear caches periodically** with the cleanup script

---

**Status:** Ready to fix - Run `.\Clean-BuildArtifacts.ps1` and restart VS
