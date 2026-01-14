# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

HighSpeedDAL is a high-performance Data Access Layer framework for .NET 9 using Roslyn source generators to create type-safe DAL classes at compile-time. The framework uses attribute-driven configuration with convention-over-configuration principles.

## Build and Development Commands

### Building
```bash
# Build entire solution
dotnet build HighSpeedDAL.sln

# Build specific project
dotnet build src/HighSpeedDAL.Core/HighSpeedDAL.Core.csproj

# Build in Release mode
dotnet build HighSpeedDAL.sln -c Release
```

### Testing
```bash
# Run all tests
dotnet test HighSpeedDAL.sln

# Run specific test project
dotnet test tests/HighSpeedDAL.Core.Tests/HighSpeedDAL.Core.Tests.csproj

# Run tests with detailed output
dotnet test HighSpeedDAL.sln --logger "console;verbosity=detailed"

# Run single test by filter
dotnet test --filter "FullyQualifiedName~InMemoryTable"
```

### Source Generator Development
```bash
# Clean build output (important when changing source generators)
dotnet clean HighSpeedDAL.sln
dotnet build HighSpeedDAL.sln

# View generated source files (in obj/Debug/net9.0/generated/)
# Example: obj/Debug/net9.0/generated/HighSpeedDAL.SourceGenerators/ProductDal.g.cs
```

**Important:** Source generator changes require a clean rebuild. Generated files are in the `obj/Debug/net9.0/generated/` directory of projects that reference the generators.

## Architecture

### Source Generation Pipeline

The framework's core feature is compile-time DAL class generation:

1. **DalSyntaxReceiver** (Parsing/DalSyntaxReceiver.cs)
   - Scans syntax trees during compilation
   - Identifies classes with `[Table]`, `[ReferenceTable]`, or `[DalEntity]` attributes
   - Discovers `DatabaseConnectionBase` subclasses

2. **EntityParser** (Parsing/EntityParser.cs)
   - Extracts entity metadata from class declarations
   - Parses all attributes (`[Table]`, `[Column]`, `[PrimaryKey]`, `[Cache]`, etc.)
   - Builds `EntityMetadata` with properties, indexes, and feature flags
   - Auto-detects primary keys (int Id property or properties ending with "Id")

3. **PropertyAutoGenerator** (Utilities/PropertyAutoGenerator.cs)
   - Identifies missing audit/soft delete properties when `[AutoAudit]` or `[SoftDelete]` present
   - Generates required properties (CreatedDate, ModifiedBy, IsDeleted, etc.)
   - Creates partial class extensions for entities

4. **SqlGenerator** (Generation/SqlGenerator.cs)
   - Generates database-specific SQL for CRUD operations
   - Creates table schemas with indexes and constraints
   - Handles soft delete filtering in queries
   - Supports bulk operations using `STRING_SPLIT`

5. **DalClassGenerator** (Generation/DalClassGenerator.Part1.cs, Part2.cs)
   - Produces complete DAL implementation inheriting from provider-specific base
   - Integrates caching when `[Cache]` attribute present
   - Adds retry logic via `DatabaseRetryPolicy`
   - Implements all CRUD operations with logging

### Convention Over Configuration

The framework minimizes required attributes:

- **Table names**: Auto-pluralized using Humanizer (Product → Products, Person → People)
- **Primary keys**: `int Id` is auto-detected; no `[PrimaryKey]` needed
- **Auto-increment**: Assumed for int primary keys; no `[Identity]` needed
- **Column mapping**: Property names map directly to columns; `[Column]` only for overrides
- **Missing Id**: Framework auto-generates `public int Id { get; set; }` if no PK exists

### Project Dependencies

```
HighSpeedDAL.Core (base abstractions)
├── HighSpeedDAL.SourceGenerators (compile-time code generation)
├── HighSpeedDAL.SqlServer (SQL Server provider)
├── HighSpeedDAL.Sqlite (SQLite provider)
├── HighSpeedDAL.AdvancedCaching (distributed cache strategies)
└── HighSpeedDAL.DataManagement (archival, CDC, versioning)
```

### Key Components

**Core Layer (HighSpeedDAL.Core)**:
- `DatabaseConnectionBase`: Base class for connection contexts; strips "Connection"/"Database"/"Db" suffix from class name to derive connection string key
- `DalOperationsBase<TEntity, TConnection>`: Base DAL class with common operations
- `DatabaseRetryPolicy`: Exponential backoff for transient errors (Polly-based)
- `ICacheManager`: Abstraction for caching strategies
- `InMemoryTable<TEntity>`: Thread-safe in-memory table with SQL-like queries, indexing, and flush-to-database capabilities

**Provider Implementations**:
- `SqlServerDalBase<TEntity, TConnection>`: SQL Server-specific DAL base (uses Microsoft.Data.SqlClient 5.2.1)
- `SqliteDalBase<TEntity, TConnection>`: SQLite-specific DAL base (uses Microsoft.Data.Sqlite 9.0.0)

**Generated Code**:
- `{Entity}Dal.g.cs`: Full DAL implementation with CRUD, bulk operations, caching
- `{Entity}.g.cs`: Partial class with auto-generated audit/soft delete properties
- `DalServiceRegistration.g.cs`: Extension method for DI registration

### Attribute-Driven Features

**Caching** (`[Cache]`):
- Strategies: None, Memory, Distributed, TwoLayer
- Generated DAL auto-integrates `ICacheManager<TEntity, TKey>`
- Cache invalidation on write operations

**Auto-Audit** (`[AutoAudit]`):
- Auto-generates: CreatedDate, CreatedBy, ModifiedDate, ModifiedBy
- Requires `partial` class for property generation
- DAL methods accept `userName` parameter

**Soft Delete** (`[SoftDelete]`):
- Auto-generates: IsDeleted, DeletedDate, DeletedBy
- Requires `partial` class for property generation
- All queries filter `IsDeleted = 0` automatically
- Provides both `DeleteAsync()` (soft) and `HardDeleteAsync()` (permanent)

**Staging Tables** (`[StagingTable]`):
- High-write scenarios with periodic batch merges
- Conflict resolution strategies: LastWriteWins, MainTableWins, StagingWins, ThrowOnConflict
- Configurable sync intervals and batch sizes

**InMemory Tables** (`[InMemoryTable]`):
- Thread-safe concurrent operations
- Auto-flush to database (staging or main table)
- Index support and constraint validation
- SQL-like WHERE clause parsing

**Reference Tables** (`[ReferenceTable]`):
- Preload lookup data on startup
- Support CSV file import
- Typically combined with `[Cache]` for performance

## Important Implementation Details

### Working with Source Generators

1. **Changes require clean rebuild**: `dotnet clean && dotnet build`
2. **View generated code**: Check `obj/Debug/net9.0/generated/HighSpeedDAL.SourceGenerators/`
3. **Debugging**: Use `ReportDiagnostic()` in generator code for compile-time messages
4. **Partial classes**: Required when using `[AutoAudit]` or `[SoftDelete]` for property auto-generation

### Connection String Configuration

Connection classes inherit from `DatabaseConnectionBase` and override `Provider`:

```csharp
public class MyAppConnection : DatabaseConnectionBase
{
    public MyAppConnection(IConfiguration config, ILogger<MyAppConnection> logger)
        : base(config, logger) { }

    public override DatabaseProvider Provider => DatabaseProvider.SqlServer;
}
```

**Convention**: Class name `MyAppConnection` → looks for `appsettings.json` key `ConnectionStrings:MyApp` (strips "Connection" suffix)

### Entity-Connection Matching

The source generator matches entities to connections by namespace:
1. Exact namespace match
2. Parent namespace match (Entity in `App.Entities`, Connection in `App.Data`)
3. Root namespace match (both start with same root)
4. Falls back to first available connection

### Database Provider Differences

**SQL Server**:
- Uses `[dbo]` schema by default
- Supports `SqlBulkCopy` for high-performance bulk inserts
- Identity columns with `SCOPE_IDENTITY()`

**SQLite**:
- No schema support
- Autoincrement via `AUTOINCREMENT` keyword
- Limited bulk operation optimization

## Common Development Patterns

### Adding New Entity Attributes

1. Add attribute class to `src/HighSpeedDAL.Core/Attributes/`
2. Update `EntityParser.ParseEntity()` to extract attribute metadata
3. Add metadata fields to `EntityMetadata.cs`
4. Update `SqlGenerator` if SQL changes needed
5. Update `DalClassGenerator` to use new metadata in generated code

### Adding New DAL Operations

1. Add method signature to appropriate base class (`DalOperationsBase` or provider-specific)
2. Update `DalClassGenerator.Part2.cs` to generate implementation
3. Add SQL constant generation in `SqlGenerator`
4. Update tests in corresponding test project

### Testing Strategy

- **Unit tests**: Test individual components (parsers, generators, utilities)
- **Integration tests**: Test full source generation pipeline
- **Provider tests**: Test database-specific implementations against real databases
- Test projects use xUnit, FluentAssertions, and Moq

## .NET Version

- **Target Framework**: .NET 9.0
- **Language Version**: Latest C# features enabled
- **Nullable Reference Types**: Enabled across all projects
- **SDK Version**: 10.0.101+

## Notes

- The framework uses Humanizer.Core 2.14.1 for intelligent table name pluralization
- Source generators use Microsoft.CodeAnalysis.CSharp 4.11.0
- Resilience/retry uses Polly 8.5.0
- Generated code includes comprehensive logging via `ILogger<T>`
- All operations are async-first for scalability
