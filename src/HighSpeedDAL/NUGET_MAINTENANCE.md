# NuGet Package Maintenance Guide

This guide explains how to maintain and update the HighSpeedDAL NuGet packages.

## Package Structure

All major library projects are configured as NuGet packages:

- **HighSpeedDAL.Core** - Core abstractions and base classes
- **HighSpeedDAL.SourceGenerators** - Roslyn source generators
- **HighSpeedDAL.Sqlite** - SQLite provider
- **HighSpeedDAL.DataManagement** - Data management features
- **HighSpeedDAL.AdvancedCaching** - Advanced caching strategies

> **Note**: When adding new packages, you must update the project list in both workflow files:
> - `.github/workflows/nuget-publish.yml` (lines ~115 and ~175)
> - `.github/workflows/nuget-release.yml` (line ~55)
> - And update this list in the maintenance guide

## Versioning

All packages use synchronized versioning in the format `major.minor.build`:

- **Major**: Breaking changes (manually incremented via workflow dispatch or tag)
- **Minor**: New features, backward compatible (manually incremented via workflow dispatch or tag)
- **Build**: Bug fixes and patches (auto-incremented on each commit to main)

## Automatic Publishing

### On Commit to Main

When code is pushed to the `main` branch:

1. The `nuget-publish.yml` workflow triggers automatically
2. Build number is incremented (e.g., 1.0.0 → 1.0.1)
3. All project files are updated with the new version
4. Projects are built and tested
5. NuGet packages are created and published to GitHub Packages
6. Version changes are committed back to the repository
7. A git tag is created (e.g., `v1.0.1`)

### On Tagged Release

When you push a version tag (e.g., `v1.2.0`):

1. The `nuget-release.yml` workflow triggers
2. Version is extracted from the tag
3. All project files are updated with the tagged version
4. Projects are built and tested
5. NuGet packages are created and published to GitHub Packages
6. If it's a GitHub Release, packages are attached as release assets

### Manual Workflow Dispatch

You can manually trigger a build with a specific version bump:

1. Go to the Actions tab in GitHub
2. Select "Publish NuGet Packages" workflow
3. Click "Run workflow"
4. Choose version bump type:
   - **build**: Increment build number (1.0.0 → 1.0.1)
   - **minor**: Increment minor version (1.0.0 → 1.1.0)
   - **major**: Increment major version (1.0.0 → 2.0.0)

## Manual Publishing (Local)

To manually build and test packages locally:

```bash
# Build all packages
dotnet pack src/HighSpeedDAL.Core/HighSpeedDAL.Core.csproj --configuration Release --output ./nupkgs
dotnet pack src/HighSpeedDAL.SourceGenerators/HighSpeedDAL.SourceGenerators.csproj --configuration Release --output ./nupkgs
dotnet pack src/HighSpeedDAL.SqlServer/HighSpeedDAL.SqlServer.csproj --configuration Release --output ./nupkgs
dotnet pack src/HighSpeedDAL.Sqlite/HighSpeedDAL.Sqlite.csproj --configuration Release --output ./nupkgs
dotnet pack src/HighSpeedDAL.DataManagement/HighSpeedDAL.DataManagement.csproj --configuration Release --output ./nupkgs
dotnet pack src/HighSpeedDAL.AdvancedCaching/HighSpeedDAL.AdvancedCaching.csproj --configuration Release --output ./nupkgs

# Inspect packages
ls -lh ./nupkgs/
unzip -l ./nupkgs/HighSpeedDAL.Core.1.0.0.nupkg
```

## Updating Package Metadata

To update package descriptions, tags, or other metadata:

1. Edit the `.csproj` file in the `src` directory
2. Update the relevant properties in the `<PropertyGroup>` section:
   - `Description`: Package description
   - `PackageTags`: Semicolon-separated tags
   - `PackageProjectUrl`: Project homepage URL
   - `RepositoryUrl`: Git repository URL
   - `PackageLicenseExpression`: License (e.g., MIT)
3. Commit and push changes - packages will be republished automatically

## Version Synchronization

All packages share the same version number. When one package is updated, all are updated together. This is managed automatically by the GitHub Actions workflows.

To manually update the version across all packages:

```bash
# PowerShell script to update all project files
$newVersion = "1.2.3"
$projectFiles = @(
  "src/HighSpeedDAL.Core/HighSpeedDAL.Core.csproj",
  "src/HighSpeedDAL.SourceGenerators/HighSpeedDAL.SourceGenerators.csproj",
  "src/HighSpeedDAL.SqlServer/HighSpeedDAL.SqlServer.csproj",
  "src/HighSpeedDAL.Sqlite/HighSpeedDAL.Sqlite.csproj",
  "src/HighSpeedDAL.DataManagement/HighSpeedDAL.DataManagement.csproj",
  "src/HighSpeedDAL.AdvancedCaching/HighSpeedDAL.AdvancedCaching.csproj"
)

foreach ($projectFile in $projectFiles) {
  [xml]$xml = Get-Content $projectFile
  $propertyGroup = $xml.Project.PropertyGroup | Where-Object { $_.Version -ne $null } | Select-Object -First 1
  $propertyGroup.Version = $newVersion
  $xml.Save((Resolve-Path $projectFile))
  Write-Host "Updated $projectFile to version $newVersion"
}
```

## Troubleshooting

### Workflow Fails with "Cannot create a package that has no dependencies nor content"

This warning appears for the SourceGenerators symbol package but is harmless. The main package is still created successfully.

### Package Not Found on GitHub Packages

1. Verify the workflow completed successfully in the Actions tab
2. Check that packages appear under the Packages section of the repository
3. Ensure the package visibility is set correctly (public or private)
4. For private packages, ensure users have proper authentication configured

### Version Not Incrementing

1. Check that the workflow has write permissions to the repository
2. Verify that the `[skip ci]` tag is working correctly
3. Check workflow logs for errors during the version update step

### Build Failures

1. Ensure all dependencies are properly restored
2. Check that the .NET SDK version matches (9.0.x)
3. Review build logs for specific compilation errors
4. Test locally using `dotnet build --configuration Release`

## Best Practices

1. **Always test locally** before pushing to main
2. **Use semantic versioning** correctly:
   - Major for breaking changes
   - Minor for new features
   - Build for bug fixes
3. **Update CHANGELOG.md** with notable changes
4. **Tag releases** for significant versions (v1.0.0, v1.1.0, etc.)
5. **Keep README.md updated** with current version badges
6. **Monitor workflow runs** to catch issues early
7. **Test package installation** in a separate project after publishing

## GitHub Actions Secrets

The workflows use the following secrets:

- `GITHUB_TOKEN`: Automatically provided by GitHub Actions
  - Used for authentication to GitHub Packages
  - Used for creating tags and committing version updates
  - No manual configuration needed

## Package Dependencies

Each package declares its dependencies in the `.csproj` file:

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="9.0.0" />
</ItemGroup>
```

When a package references another HighSpeedDAL package, use a project reference during development:

```xml
<ItemGroup>
  <ProjectReference Include="..\HighSpeedDAL.Core\HighSpeedDAL.Core.csproj" />
</ItemGroup>
```

The NuGet package will automatically convert project references to package references.

### SourceGenerators Package Special Case

The SourceGenerators package requires special handling for dependencies:
- Humanizer.Core dependency must be included in the package
- The path is currently hardcoded to version 2.14.1
- **When upgrading Humanizer version**, update the path in `HighSpeedDAL.SourceGenerators.csproj` (lines ~41-52)

Example:
```xml
<None Include="$(HOME)/.nuget/packages/humanizer.core/2.14.1/lib/netstandard2.0/Humanizer.dll" 
      Pack="true" PackagePath="analyzers/dotnet/cs" Visible="false" />
```

## Adding a New Package

To add a new library project as a NuGet package:

1. **Update the .csproj file** with NuGet metadata (see existing packages for template)
2. **Update workflow files**:
   - `.github/workflows/nuget-publish.yml` - Add to project list in two locations
   - `.github/workflows/nuget-release.yml` - Add to project list
3. **Add pack command** to both workflows
4. **Update documentation**:
   - This file (NUGET_MAINTENANCE.md)
   - README.md package table
   - GITHUB_PACKAGES.md package reference
5. **Test locally**: `dotnet pack [project] --configuration Release --output ./test-nupkgs`

## Resources

- [GitHub Packages Documentation](https://docs.github.com/en/packages)
- [GitHub Actions Documentation](https://docs.github.com/en/actions)
- [NuGet Package Documentation](https://docs.microsoft.com/en-us/nuget/)
- [Semantic Versioning](https://semver.org/)
