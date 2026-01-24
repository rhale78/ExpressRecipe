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
- Pluggable flush strategies for atomic table swap operations

**InMemory Table Flush Strategies**:
- `IFlushStrategy<TEntity>`: Interface for implementing custom flush patterns
- `TableSwapFlushStrategy<TEntity>`: Atomic table swap (CREATE TEMP → BULK INSERT → SWAP)
- Configurable via `SetFlushStrategy()` and periodic flush via `ConfigurePeriodicFlush()`
- Manual triggers via `TriggerFlushAsync()` after batch operations
- Entity-specific optimization via partial class `BulkInsertToTempTableAsync()` override
- Serializable isolation level ensures all-or-nothing semantics

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

### InMemory Table Flush Strategy Implementation

The framework supports pluggable flush strategies for high-performance bulk writes to database. This pattern addresses the scenario where in-memory data must be atomically written to the backing store.

**When to Use**:
- Accumulating writes in memory and periodically flushing to database
- Requiring atomic all-or-nothing semantics (either all rows written or none)
- High-volume batch operations (1000+ rows) needing optimized bulk insert

**Core Components**:

1. **IFlushStrategy<TEntity>** (Core/InMemoryTable/IFlushStrategy.cs):
   - `string StrategyName { get; }`: Identifies the strategy
   - `Task<int> FlushAsync(List<TEntity> entities, CancellationToken cancellationToken)`: Executes flush, returns count flushed

2. **TableSwapFlushStrategy<TEntity>** (Core/InMemoryTable/TableSwapFlushStrategy.cs):
   - Default implementation using atomic table swap pattern
   - Pattern: CREATE TEMP TABLE → BULK INSERT → (within transaction) DROP original + RENAME temp
   - Configurable isolation level (default: Serializable)
   - Supports 300s timeout for bulk operations

3. **InMemoryTable<TEntity>** Enhanced Methods:
   - `SetFlushStrategy(IFlushStrategy<TEntity> strategy)`: Configure strategy
   - `ConfigurePeriodicFlush(int flushIntervalSeconds)`: Start timer for periodic flush
   - `TriggerFlushAsync()`: Manually trigger flush and clear table
   - `Dispose()`: Final flush on shutdown

**Implementation Example - SQL Server with SqlBulkCopy**:

```csharp
// Entity-specific partial DAL (ProductStagingEntityDal.Partial.cs)
public sealed partial class ProductStagingEntityDal
{
    /// <summary>
    /// Implements table swap bulk insert using SqlBulkCopy (50-100x faster than individual INSERTs)
    /// Called by TableSwapFlushStrategy during atomic flush operation
    /// </summary>
    public async Task<int> BulkInsertToTempTableAsync(
        List<ProductStagingEntity> entities,
        string tempTableName)
    {
        const int BATCH_SIZE = 1000; // Process in 1000-row batches for memory efficiency
        int totalInserted = 0;

        for (int i = 0; i < entities.Count; i += BATCH_SIZE)
        {
            var batch = entities.Skip(i).Take(BATCH_SIZE).ToList();

            using var connection = new SqlConnection(Connection.ConnectionString);
            await connection.OpenAsync();

            using var bulkCopy = new SqlBulkCopy(connection)
            {
                DestinationTableName = $"[{tempTableName}]",
                BatchSize = BATCH_SIZE,
                BulkCopyTimeout = 300 // 5 minutes for large batches
            };

            // Create schema-matching DataTable
            var dataTable = CreateProductStagingDataTable(batch);

            // Map columns
            AddBulkCopyColumnMappings(bulkCopy);

            // Execute bulk insert (single operation for entire batch)
            await bulkCopy.WriteToServerAsync(dataTable);
            totalInserted += batch.Count;
        }

        return totalInserted;
    }

    private DataTable CreateProductStagingDataTable(List<ProductStagingEntity> entities)
    {
        var dt = new DataTable("ProductStaging");

        // Add columns matching entity schema
        dt.Columns.Add("Id", typeof(Guid));
        dt.Columns.Add("ExternalId", typeof(string));
        dt.Columns.Add("ProductName", typeof(string));
        // ... (all 31 columns of ProductStagingEntity)

        // Populate rows with null handling
        foreach (var entity in entities)
        {
            var row = dt.NewRow();
            row["Id"] = entity.Id;
            row["ExternalId"] = (object?)entity.ExternalId ?? DBNull.Value;
            row["ProductName"] = (object?)entity.ProductName ?? DBNull.Value;
            // ... (all properties)
            dt.Rows.Add(row);
        }

        return dt;
    }

    private void AddBulkCopyColumnMappings(SqlBulkCopy bulkCopy)
    {
        bulkCopy.ColumnMappings.Add("Id", "Id");
        bulkCopy.ColumnMappings.Add("ExternalId", "ExternalId");
        bulkCopy.ColumnMappings.Add("ProductName", "ProductName");
        // ... (all columns)
    }
}
```

**Usage Pattern**:

```csharp
// In service or background job
var inMemoryTable = new InMemoryTable<ProductStagingEntity>(_logger, config);

// Configure atomic table swap flush strategy
var flushStrategy = new TableSwapFlushStrategy<ProductStagingEntity>(
    _logger,
    _connection,
    _retryPolicy);
inMemoryTable.SetFlushStrategy(flushStrategy);

// Option 1: Periodic automatic flush every 30 seconds
inMemoryTable.ConfigurePeriodicFlush(flushIntervalSeconds: 30);

// Option 2: Manual flush after batch insert
for (int i = 0; i < 2000; i++)
{
    await inMemoryTable.InsertAsync(new ProductStagingEntity { ... });
}
var flushedCount = await inMemoryTable.TriggerFlushAsync(); // Atomic write of all 2000 rows

// Performance: ~100-200ms for 2000 rows (vs 9+ seconds with individual updates)
```

**Key Design Decisions**:

1. **Partial Class Override Pattern**: Generated DAL creates `public sealed partial class {Entity}Dal`. Entity-specific optimizations override `BulkInsertToTempTableAsync()` in partial class, allowing schema-specific implementations without regenerating base code.

2. **Atomic Semantics**: Transaction wraps DROP + RENAME to guarantee all-or-nothing write. If flush fails mid-operation, original table is untouched.

3. **Source Generator Integration**: `DalClassGenerator.Part4.cs` generates:
   - SQL constants (CREATE TEMP, DROP, RENAME SQL)
   - `ConfigurePeriodicFlush()` method
   - Note: `BulkInsertToTempTableAsync()` NOT generated; implemented in entity-specific partial

4. **No Cache Bypass**: Flush writes to backing store but doesn't touch L1/L2/L3 caches (memory cache, .NET cache, Redis). Query caches (WHERE clauses, named queries) preserved.

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
