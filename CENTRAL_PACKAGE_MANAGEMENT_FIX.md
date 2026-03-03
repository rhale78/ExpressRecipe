# Central Package Management (CPM) Fix

## Issue
Several Messaging projects had `NU1008` errors indicating that `PackageReference` items contained `Version` attributes, which violates Central Package Management (CPM) rules.

## Root Cause
When CPM is enabled via `<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>` in `Directory.Packages.props`, all package versions must be defined centrally in `PackageVersion` items. Individual project files must NOT specify versions directly.

## Projects Fixed

### 1. ExpressRecipe.Messaging.Core
- Removed versions from:
  - Microsoft.Extensions.DependencyInjection.Abstractions
  - Microsoft.Extensions.Logging.Abstractions
  - OpenTelemetry

### 2. ExpressRecipe.Messaging.Benchmarks
- Removed version from:
  - BenchmarkDotNet

### 3. ExpressRecipe.Messaging.RabbitMQ
- Removed versions from:
  - Aspire.RabbitMQ.Client
  - RabbitMQ.Client
  - Microsoft.Extensions.Hosting.Abstractions
  - Microsoft.Extensions.Options
  - Microsoft.Extensions.Logging.Abstractions

### 4. ExpressRecipe.Messaging.Saga
- Removed versions from:
  - Microsoft.Extensions.DependencyInjection.Abstractions
  - Microsoft.Extensions.Hosting.Abstractions
  - Microsoft.Extensions.Logging.Abstractions
  - Microsoft.Extensions.Options
  - Microsoft.Data.SqlClient

### 5. ExpressRecipe.Messaging.Demo
- Removed version from:
  - Microsoft.Extensions.Hosting

### 6. ExpressRecipe.Messaging.Saga.Tests
- Removed versions from:
  - Microsoft.NET.Test.Sdk
  - xunit
  - xunit.runner.visualstudio
  - Moq
  - Microsoft.Extensions.DependencyInjection
  - Microsoft.Extensions.Logging.Abstractions

### 7. ExpressRecipe.Messaging.Tests
- Removed versions from:
  - Microsoft.NET.Test.Sdk
  - xunit
  - xunit.runner.visualstudio
  - Moq
  - Microsoft.Extensions.DependencyInjection
  - Microsoft.Extensions.Logging.Abstractions

## Central Package Versions Added

Added missing central package definitions to `src/Directory.Packages.props`:

```xml
<PackageVersion Include="Microsoft.Extensions.DependencyInjection" Version="10.0.2" />
<PackageVersion Include="Microsoft.Extensions.Options" Version="10.0.2" />
<PackageVersion Include="Microsoft.Extensions.Hosting" Version="10.0.2" />
<PackageVersion Include="Aspire.RabbitMQ.Client" Version="13.1.2" />
<PackageVersion Include="OpenTelemetry" Version="1.14.0" />
<PackageVersion Include="BenchmarkDotNet" Version="0.15.1" />
```

## Verification
✅ Build successful with no NU1008 errors

## Best Practices for CPM

When adding new packages to any project in the solution:

1. **Add version ONLY to `Directory.Packages.props`:**
   ```xml
   <PackageVersion Include="PackageName" Version="1.0.0" />
   ```

2. **Reference WITHOUT version in project files:**
   ```xml
   <PackageReference Include="PackageName" />
   ```

3. **Never specify versions in project files when CPM is enabled**

4. **To update a package version, update it ONLY in `Directory.Packages.props`** - all projects will automatically use the new version

## Benefits of CPM

- ✅ Consistent package versions across all projects
- ✅ Single source of truth for package versions
- ✅ Easier dependency management
- ✅ Prevents version conflicts
- ✅ Simplified upgrades (one place to update)

## Related Files
- `src/Directory.Packages.props` - Central package version definitions
- All `.csproj` files in `src/Messaging/` folder
- Test projects in `src/Tests/` for Messaging components
