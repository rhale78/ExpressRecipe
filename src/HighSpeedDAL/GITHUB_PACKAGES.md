# Using HighSpeedDAL NuGet Packages from GitHub Packages

This document explains how to consume the HighSpeedDAL NuGet packages hosted on GitHub Packages.

## Prerequisites

- .NET 9.0 SDK or later
- A GitHub Personal Access Token (PAT) with `read:packages` permission

## Creating a Personal Access Token

1. Go to GitHub Settings > Developer settings > Personal access tokens > Tokens (classic)
2. Click "Generate new token (classic)"
3. Give it a descriptive name (e.g., "NuGet Package Access")
4. Select the `read:packages` scope
5. Click "Generate token"
6. **Copy the token immediately** (you won't be able to see it again)

## Configuring NuGet to Use GitHub Packages

### Option 1: Using nuget.config (Recommended)

Create a `nuget.config` file in your solution root:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
    <add key="github" value="https://nuget.pkg.github.com/rhale78/index.json" />
  </packageSources>
  <packageSourceCredentials>
    <github>
      <add key="Username" value="YOUR_GITHUB_USERNAME" />
      <add key="ClearTextPassword" value="YOUR_GITHUB_PAT" />
    </github>
  </packageSourceCredentials>
</configuration>
```

**Security Note**: Do NOT commit the `nuget.config` file with your PAT to source control. Use environment variables instead (see Option 2).

### Option 2: Using Environment Variables (More Secure)

Add the GitHub package source using the .NET CLI:

```bash
# Add the GitHub package source
dotnet nuget add source https://nuget.pkg.github.com/rhale78/index.json \
  --name github \
  --username YOUR_GITHUB_USERNAME \
  --password YOUR_GITHUB_PAT \
  --store-password-in-clear-text
```

Or use environment variables:

```bash
# Linux/macOS
export GITHUB_USERNAME="your-username"
export GITHUB_TOKEN="your-pat"

# Windows PowerShell
$env:GITHUB_USERNAME="your-username"
$env:GITHUB_TOKEN="your-pat"

# Then add source
dotnet nuget add source https://nuget.pkg.github.com/rhale78/index.json \
  --name github \
  --username $GITHUB_USERNAME \
  --password $GITHUB_TOKEN \
  --store-password-in-clear-text
```

## Installing Packages

Once configured, you can install HighSpeedDAL packages like any other NuGet package:

```bash
# Install Core package
dotnet add package HighSpeedDAL.Core

# Install SQL Server provider
dotnet add package HighSpeedDAL.SqlServer

# Install SQLite provider
dotnet add package HighSpeedDAL.Sqlite

# Install Source Generators
dotnet add package HighSpeedDAL.SourceGenerators

# Install Advanced Caching
dotnet add package HighSpeedDAL.AdvancedCaching

# Install Data Management
dotnet add package HighSpeedDAL.DataManagement
```

Or add directly to your `.csproj` file:

```xml
<ItemGroup>
  <PackageReference Include="HighSpeedDAL.Core" Version="1.0.0" />
  <PackageReference Include="HighSpeedDAL.SqlServer" Version="1.0.0" />
  <PackageReference Include="HighSpeedDAL.SourceGenerators" Version="1.0.0" />
</ItemGroup>
```

## Available Packages

| Package | Description | Dependencies |
|---------|-------------|--------------|
| **HighSpeedDAL.Core** | Core abstractions, attributes, and base classes | Required by all other packages |
| **HighSpeedDAL.SourceGenerators** | Roslyn source generators for code generation | HighSpeedDAL.Core |
| **HighSpeedDAL.SqlServer** | SQL Server provider implementation | HighSpeedDAL.Core |
| **HighSpeedDAL.Sqlite** | SQLite provider implementation | HighSpeedDAL.Core |
| **HighSpeedDAL.DataManagement** | Data archival, versioning, CDC features | - |
| **HighSpeedDAL.AdvancedCaching** | Advanced caching strategies (Redis, etc.) | - |

## Package Versioning

The packages follow **Semantic Versioning** in the format `major.minor.build`:

- **Major**: Breaking changes
- **Minor**: New features (backward compatible)
- **Build**: Bug fixes and patches (auto-incremented on each commit to main)

### Automatic Version Updates

- **On every commit to main**: Build number auto-increments (e.g., 1.0.0 → 1.0.1)
- **Manual version bumps**: Use GitHub Actions workflow dispatch to bump major or minor versions
- **Tagged releases**: Push a tag like `v1.2.0` to create a specific release version

## Troubleshooting

### Authentication Errors

If you get a 401 Unauthorized error:
1. Verify your PAT has the `read:packages` scope
2. Ensure your username and PAT are correct
3. Try regenerating your PAT

### Package Not Found

If the package isn't found:
1. Verify the package source is configured correctly
2. Check that the package has been published to GitHub Packages
3. Ensure you're using the correct package name (e.g., `HighSpeedDAL.Core` not `HighSpeedDal.Core`)

### Restore Fails

If `dotnet restore` fails:
1. Clear your NuGet cache: `dotnet nuget locals all --clear`
2. Verify your nuget.config is in the solution root
3. Check that the GitHub package source URL is correct

## CI/CD Integration

### GitHub Actions

```yaml
- name: Authenticate to GitHub Packages
  run: |
    dotnet nuget add source https://nuget.pkg.github.com/rhale78/index.json \
      --name github \
      --username ${{ github.actor }} \
      --password ${{ secrets.GITHUB_TOKEN }} \
      --store-password-in-clear-text

- name: Restore packages
  run: dotnet restore
```

### Azure DevOps

Add a NuGet Authenticate task before restore:

```yaml
- task: NuGetAuthenticate@1
  inputs:
    nuGetServiceConnections: 'GitHub'

- script: dotnet restore
```

## Support

For issues related to:
- **Package installation**: Check this guide and GitHub Packages documentation
- **Package bugs**: Open an issue at https://github.com/rhale78/HighSpeedDAL/issues
- **Feature requests**: Open an issue with the "enhancement" label

## Additional Resources

- [GitHub Packages Documentation](https://docs.github.com/en/packages)
- [NuGet CLI Reference](https://docs.microsoft.com/en-us/nuget/reference/nuget-exe-cli-reference)
- [HighSpeedDAL GitHub Repository](https://github.com/rhale78/HighSpeedDAL)
