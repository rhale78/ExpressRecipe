# HighSpeedDAL - High Performance Data Access Layer Framework

A high-performance DAL framework for .NET 9 with attribute-driven CRUD operations, intelligent caching, and enterprise-grade features.

## 📦 NuGet Packages

All HighSpeedDAL packages are available on GitHub Packages. See [GITHUB_PACKAGES.md](GITHUB_PACKAGES.md) for installation instructions.

| Package | Version | Description |
|---------|---------|-------------|
| **HighSpeedDAL.Core** | ![Version](https://img.shields.io/badge/version-1.0.0-blue) | Core abstractions, attributes, and base classes |
| **HighSpeedDAL.SourceGenerators** | ![Version](https://img.shields.io/badge/version-1.0.0-blue) | Roslyn source generators for code generation |
| **HighSpeedDAL.SqlServer** | ![Version](https://img.shields.io/badge/version-1.0.0-blue) | SQL Server provider implementation |
| **HighSpeedDAL.Sqlite** | ![Version](https://img.shields.io/badge/version-1.0.0-blue) | SQLite provider implementation |
| **HighSpeedDAL.DataManagement** | ![Version](https://img.shields.io/badge/version-1.0.0-blue) | Data archival, versioning, CDC features |
| **HighSpeedDAL.AdvancedCaching** | ![Version](https://img.shields.io/badge/version-1.0.0-blue) | Advanced caching strategies (Redis, etc.) |

### Quick Installation

```bash
# Add GitHub Packages source
dotnet nuget add source https://nuget.pkg.github.com/rhale78/index.json \
  --name github \
  --username YOUR_GITHUB_USERNAME \
  --password YOUR_GITHUB_PAT \
  --store-password-in-clear-text

# Install packages
dotnet add package HighSpeedDAL.Core
dotnet add package HighSpeedDAL.SqlServer
dotnet add package HighSpeedDAL.SourceGenerators
```

For detailed setup instructions, authentication, and troubleshooting, see [GITHUB_PACKAGES.md](GITHUB_PACKAGES.md).

## 🚀 Key Features

### Developer Experience
- **Attribute-Driven**: Simple attributes control all behaviors (caching, staging, auditing, soft deletes, etc.)
- **Type-Safe**: Compile-time safety with Roslyn source generators
- **Multi-Database**: SQL Server and SQLite support
- **Zero Configuration**: Auto-schema creation and migration
- **Flexible Primary Keys**: Support for both int (auto-increment) and Guid (application-generated) primary keys

### Performance
- **Intelligent Caching**: Multiple cache strategies (Memory, Distributed, TwoLayer)
- **Bulk Operations**: Optimized bulk inserts and updates
- **Staging Tables**: High-write scenarios with periodic batch merges
- **Connection Pooling**: Built-in connection management

### Enterprise Features
- **Auto Schema Management**: Tables auto-create as your entities evolve
- **Transient Error Retry**: Built-in retry policy for transient database errors
- **Comprehensive Logging**: Detailed logging at every layer via ILogger
- **Soft Deletes**: Mark records as deleted without removing them (properties auto-generated)
- **Audit Tracking**: Automatic CreatedBy, CreatedDate, ModifiedBy, ModifiedDate (properties auto-generated)
- **Property Auto-Generation**: Source generators create audit/soft delete properties automatically
- **Reference Tables**: Pre-populated lookup tables from CSV files

## 📦 Project Structure

```
HighSpeedDAL/
├── src/
│   ├── HighSpeedDAL.Core/              # Core abstractions, attributes, and base classes
│   ├── HighSpeedDAL.SourceGenerators/  # Roslyn source generators for CRUD
│   ├── HighSpeedDAL.SqlServer/         # SQL Server provider implementation
│   ├── HighSpeedDAL.Sqlite/            # SQLite provider implementation
│   ├── HighSpeedDAL.DataManagement/    # Data archival, versioning, CDC features
│   ├── HighSpeedDAL.AdvancedCaching/   # Advanced caching strategies
│   └── HighSpeedDAL.Example/           # Example application
├── tests/
│   ├── HighSpeedDAL.Core.Tests/
│   ├── HighSpeedDAL.SqlServer.Tests/
│   ├── HighSpeedDAL.Sqlite.Tests/
│   ├── HighSpeedDAL.DataManagement.Tests/
│   ├── HighSpeedDAL.AdvancedCaching.Tests/
│   └── HighSpeedDAL.SourceGenerators.Tests/
├── docs/                               # Documentation
└── examples/
    └── IntegrationExample/             # Docker-based integration example
```

## 🎯 Quick Start

### 1. Create Database Connection Class

```csharp
using HighSpeedDAL.Core.Base;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

// Define your database connection context
// Connection string will be loaded from appsettings.json using key "MyApp"
public class MyAppConnection : DatabaseConnectionBase
{
    public MyAppConnection(IConfiguration configuration, ILogger<MyAppConnection> logger)
        : base(configuration, logger)
    {
    }

    public override DatabaseProvider Provider => DatabaseProvider.SqlServer;

    // Optional: Override to customize the connection string key
    // Default behavior removes "Connection" suffix from class name
    // MyAppConnection -> looks for "MyApp" in ConnectionStrings section
    protected override string GetConnectionStringKey()
    {
        return "MyApp"; // Matches appsettings.json ConnectionStrings:MyApp
    }
}
```

### 2. Define Your Entities

> **Note:** The framework follows **convention over configuration**:
> - `[Table]` is **optional** - defaults to pluralized class name using [Humanizer](https://github.com/Humanizr/Humanizer) (Product → Products, Category → Categories, Person → People)
> - `[Table("CustomName")]` - override with custom table name
> - `[Column]` is **optional** - properties auto-map to columns using property name
> - `[PrimaryKey]` and `[Identity]` are **optional** for standard `int Id` properties
> - If no `Id` property exists, the framework auto-generates one
> - **`partial` class** - required when using `[AutoAudit]` or `[SoftDelete]` for property auto-generation

```csharp
using HighSpeedDAL.Core.Attributes;

// Truly minimal entity - framework handles everything!
// Creates "Products" table automatically (pluralized)
public class Product
{
    // Framework auto-generates: public int Id { get; set; }
    // with auto-increment primary key

    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
}

// With [Table] marker - still uses pluralization
[Table]  // Creates "Products" table
public class Product
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

// With [Table] and explicit table name override
[Table("ProductCatalog")]  // Creates "ProductCatalog" table (custom name)
public class Product
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

// With caching and explicit Id (no PK attributes needed!)
[Table]  // Optional: mark as DAL entity, uses "Products" table
[Cache(CacheStrategy.Memory, ExpirationSeconds = 300)]
public class Product
{
    public int Id { get; set; }  // Automatically detected as PK with auto-increment

    [Index]  // Optional: add index for performance
    public string Name { get; set; } = string.Empty;

    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
}

// Entity with auditing and soft delete
[Table]  // Creates "Customers" table (pluralized)
[Cache(CacheStrategy.TwoLayer, MaxSize = 1000)]
[AutoAudit]
[SoftDelete]
public partial class Customer
{
    // Only define your business properties - framework auto-generates the rest!
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    [Index(IsUnique = true)]
    public string Email { get; set; } = string.Empty;

    // Framework auto-generates ALL of these via source generator:
    // - public int Id { get; set; }
    // - public DateTime CreatedDate { get; set; }
    // - public string CreatedBy { get; set; } = string.Empty;
    // - public DateTime ModifiedDate { get; set; }
    // - public string ModifiedBy { get; set; } = string.Empty;
    // - public bool IsDeleted { get; set; }
    // - public DateTime? DeletedDate { get; set; }
    // - public string? DeletedBy { get; set; }
}

// Use [Column] only when you need overrides
[Table("Products")]  // Override: use exact name "Products" instead of "Products"
public class ProductWithOverrides
{
    public int Id { get; set; }  // No [PrimaryKey] needed - auto-detected

    // Override: Map property to different column name
    [Column(Name = "ProductName", MaxLength = 255)]
    public string Name { get; set; } = string.Empty;

    // Override: Specify SQL type and precision
    [Column(TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }

    // Override: Custom column name and type
    [Column(Name = "qty_available", TypeName = "int")]
    public int StockQuantity { get; set; }
}

// Only use [PrimaryKey] for non-standard primary keys
[Table("Products")]
public class ProductWithCustomKey
{
    [PrimaryKey]  // Required: custom PK not named "Id"
    public string ProductCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

// Reference table with CSV preload
[Table("StatusTypes")]  // Override: exact name "StatusTypes"
[ReferenceTable]
[Cache(CacheStrategy.TwoLayer)]
public class StatusType
{
    public int StatusId { get; set; }  // Auto-detected as PK (ends with "Id")

    [Index(IsUnique = true)]
    public string StatusCode { get; set; } = string.Empty;

    public string StatusName { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
}

// Entity with composite key - [PrimaryKey] IS needed here
[Table("CompositeKeyEntities")]  // Override pluralization
public class CompositeKeyEntity
{
    [PrimaryKey(Order = 1)]  // Required for composite keys
    public int TenantId { get; set; }

    [PrimaryKey(Order = 2)]  // Required for composite keys
    public int EntityId { get; set; }

    public string Data { get; set; } = string.Empty;
}

// Entity with Guid primary key (auto-generated)
[Table(PrimaryKeyType = PrimaryKeyType.Guid)]  // Specify Guid ID at table level
public partial class UserWithGuidId
{
    // Framework auto-generates: public Guid Id { get; set; }
    // Guid is generated by application before INSERT

    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

// Entity with explicit Guid primary key
[Table]
public partial class OrderWithGuid
{
    [PrimaryKey(AutoGenerate = false)]  // Explicit Guid ID (no auto-increment)
    public Guid Id { get; set; }

    public string OrderNumber { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public DateTime OrderDate { get; set; }
}

// High-volume entity with staging table
[Table("HighVolumeData")]  // Override pluralization
[StagingTable(60)] // Merge every 60 seconds
[Cache(CacheStrategy.None)]
public class HighVolumeEntity
{
    public long Id { get; set; }  // Auto-detected as auto-increment PK

    [Index]
    public DateTime EventTime { get; set; }

    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public int ProcessingStatus { get; set; }
}
```

### 3. Configuration

**appsettings.json**

```json
{
  "ConnectionStrings": {
    "MyApp": "Server=localhost;Database=HighSpeedDb;Integrated Security=true;TrustServerCertificate=true",
    "Analytics": "Server=analytics-server;Database=AnalyticsDb;User Id=app;Password=***;",
    "ReadOnly": "Server=readonly-replica;Database=HighSpeedDb;Integrated Security=true;"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "HighSpeedDAL": "Debug"
    }
  }
}
```

**Program.cs**

```csharp
using HighSpeedDAL.SqlServer;
using HighSpeedDAL.Core.Resilience;
using HighSpeedDAL.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

// Register connection factory
builder.Services.AddSingleton<IDbConnectionFactory, SqlServerConnectionFactory>();

// Register database connection (connection string loaded from appsettings.json)
builder.Services.AddSingleton<MyAppConnection>();

// Register retry policy factory
builder.Services.AddSingleton(sp =>
{
    var logger = sp.GetRequiredService<ILogger<DatabaseRetryPolicy>>();
    return new RetryPolicyFactory(logger, maxRetryAttempts: 3, delayMilliseconds: 100);
});

var app = builder.Build();
app.Run();
```

## 📋 Attributes Reference

### Table Configuration

| Attribute | Required? | Purpose | Example |
|-----------|-----------|---------|---------|  
| `[Table]` or `[Table(name)]` | Optional* | Mark as entity or override table name | `[Table]` or `[Table("CustomName", Schema = "dbo")]` |
| `[Column]` | Optional | Override column mapping | `[Column(Name = "ProductName", MaxLength = 255)]` |
| `[PrimaryKey]` | Optional** | Mark custom primary key | `[PrimaryKey]` or `[PrimaryKey(Order = 1)]` |\n| `[Identity]` | Optional** | Mark auto-increment | `[Identity]` |
| `[Index]` | Optional | Create index | `[Index(Name = "IX_Email", IsUnique = true)]` |

\* **Optional:** Without `[Table]` or with `[Table]` (no name), defaults to pluralized class name (Product → Products). Use `[Table("CustomName")]` to override.  
\** **Optional:** Auto-detected for `int Id` property. Use only for custom primary keys.

> **Convention over Configuration:** The framework automatically:
> - Pluralizes class names for table names using [Humanizer](https://github.com/Humanizr/Humanizer) (Product → Products, Category → Categories, Person → People, Octopus → Octopi)
> - Detects `int Id` as auto-increment primary key
> - Maps properties to columns using property name
> - Auto-generates `int Id` if no primary key defined

### Caching

| Attribute | Purpose | Example |
|-----------|---------|---------|
| `[Cache]` | Enable caching | `[Cache(CacheStrategy.Memory, MaxSize = 1000, ExpirationSeconds = 300, PreloadOnStartup = true)]` |

**Cache Strategies:**
- `CacheStrategy.None` - No caching
- `CacheStrategy.Memory` - In-memory cache (single instance)
- `CacheStrategy.Distributed` - Distributed cache (Redis, etc.)
- `CacheStrategy.TwoLayer` - L1 + L2 cache with lock-free reads

### Data Management

| Attribute | Purpose | Example |
|-----------|---------|---------|
| `[ReferenceTable]` | Pre-loaded lookup table | `[ReferenceTable(CsvFilePath = "data/statuses.csv", LoadOnStartup = true)]` |
| `[StagingTable]` | High-write staging pattern | `[StagingTable(SyncIntervalSeconds = 60, ConflictResolution = ConflictResolution.LastWriteWins)]` |
| `[AutoAudit]` | Auto audit columns | `[AutoAudit(TrackCreated = true, TrackModified = true)]` |
| `[SoftDelete]` | Soft delete support | `[SoftDelete(AutoPurge = true, RetentionDays = 90)]` |

> **Auto-Generation:** `[AutoAudit]` and `[SoftDelete]` automatically generate missing properties via source generators!  
> Just add the attribute to a `partial` class - the framework generates audit and soft delete properties if they don't exist.  
> **See [docs/AUDIT_SOFTDELETE_GUIDE.md](docs/AUDIT_SOFTDELETE_GUIDE.md) for complete examples and usage patterns.**

### Staging Table Options

```csharp
[StagingTable(
    SyncIntervalSeconds = 60,           // How often to merge staging → main
    ConflictResolution = ConflictResolution.LastWriteWins,
    MirrorIndexes = true,                // Copy indexes to staging table
    RetainStagingHistory = false,        // Clear staging after merge
    BatchSize = 10000,                   // Records per batch
    UseTransaction = true,               // Atomic merge
    SyncPriority = 100,                  // Priority when multiple staging tables
    AutoCreateStagingTable = true        // Create on startup
)]
```

**Conflict Resolution Strategies:**
- `LastWriteWins` - Most recent change wins
- `MainTableWins` - Main table takes precedence
- `StagingWins` - Staging always overwrites
- `ThrowOnConflict` - Requires manual resolution

## 🔧 Database Providers

### SQL Server

```csharp
using Microsoft.Data.SqlClient;
using HighSpeedDAL.SqlServer;

// Connection string
"Server=localhost;Database=MyDb;Integrated Security=true;TrustServerCertificate=true"

// Uses Microsoft.Data.SqlClient 5.2.1
```

### SQLite

```csharp
using Microsoft.Data.Sqlite;
using HighSpeedDAL.Sqlite;

// Connection string
"Data Source=mydb.db;Cache=Shared"

// Uses Microsoft.Data.Sqlite 9.0.0
```

## 🧪 Testing

The project includes comprehensive unit tests:

```csharp
using Xunit;
using FluentAssertions;
using HighSpeedDAL.Core.Tests;

[Fact]
public async Task Insert_ValidEntity_ReturnsId()
{
    // Test implementation
}
```

Test projects use:
- xUnit for test framework
- FluentAssertions for readable assertions
- Moq for mocking dependencies

## 🏗️ Architecture

### How Source Generation Works

1. **Compile-Time Discovery**: During compilation, the source generator scans all classes with `[Table]` attribute
2. **Metadata Extraction**: EntityParser extracts all metadata (properties, attributes, constraints)
3. **SQL Generation**: SqlGenerator creates optimized SQL statements for all operations
4. **DAL Class Generation**: DalClassGenerator produces a complete, type-safe DAL class
5. **Compilation**: Generated code is compiled alongside your project

### Source Generator Components

**DalSyntaxReceiver**: Identifies entity candidates during syntax analysis
- Finds classes with `[Table]`, `[ReferenceTable]`, or `[DalEntity]` attributes
- Discovers connection classes inheriting from `DatabaseConnectionBase`

**EntityParser**: Extracts comprehensive metadata from entity classes
- Parses all attributes: `[Table]`, `[Column]`, `[PrimaryKey]`, `[Identity]`, `[Index]`
- Extracts feature flags: `[Cache]`, `[AutoAudit]`, `[SoftDelete]`, `[StagingTable]`
- Builds property metadata including types, nullability, constraints

**SqlGenerator**: Creates optimized SQL for all operations
- `CREATE TABLE` with proper column types, constraints, and indexes
- `INSERT`, `UPDATE`, `DELETE` with audit field support
- `SELECT` queries with soft delete filtering
- Bulk operation SQL using `STRING_SPLIT` for batch IDs

**DalClassGenerator**: Produces complete DAL implementation
- Inherits from `SqlServerDalBase<TEntity, TConnection>`
- Implements all CRUD operations with error handling
- Integrates caching when `[Cache]` present
- Adds retry logic via `DatabaseRetryPolicy`
- Includes comprehensive logging at every operation

### Generated Code Structure

```csharp
// Auto-generated for: Product entity
public sealed class ProductDal : SqlServerDalBase<Product, ProductsConnection>
{
    private readonly ICacheManager<Product, int> _cache; // If [Cache]

    // Constructor with DI
    public ProductDal(
        ProductsConnection connection,
        ILogger<ProductDal> logger,
        IDbConnectionFactory connectionFactory,
        RetryPolicyFactory retryPolicyFactory)
        : base(connection, logger, connectionFactory, retryPolicyFactory.CreatePolicy())
    {
        // Initialize cache, schema, reference data
    }

    // SQL constants (compile-time)
    private const string SQL_INSERT = "INSERT INTO [Products]...";
    private const string SQL_UPDATE = "UPDATE [Products]...";
    // ... more SQL

    // All CRUD operations
    public async Task<int> InsertAsync(Product entity, string? userName = null, ...)
    public async Task<int> UpdateAsync(Product entity, string? userName = null, ...)
    public async Task<Product?> GetByIdAsync(int id, ...)
    // ... more operations

    // Helper methods
    private Product MapFromReader(IDataReader reader) { }
    private Dictionary<string, object> MapToParameters(Product entity) { }
}
```

### Core Components

- **DatabaseRetryPolicy**: Implements exponential backoff retry for transient errors
- **IDbConnectionFactory**: Abstraction for creating database connections
- **DalOperationsBase**: Base class providing common DAL operations and utilities
- **EntityMetadata**: Runtime representation of entity structure and attributes

## 🔄 Auto-Generated Operations

For each entity with `[Table]` attribute, the source generator automatically creates a complete DAL class with:

### Read Operations
- `GetByIdAsync(int id)` - Get single entity by primary key
- `GetAllAsync()` - Get all entities (respects soft delete filter)
- `GetByIdsAsync(IEnumerable<int> ids)` - Get multiple entities by IDs
- `CountAsync()` - Get total count
- `ExistsAsync(int id)` - Check if entity exists
- `GetAllIncludingDeletedAsync()` - Get all including soft-deleted (if `[SoftDelete]`)

### Write Operations
- `InsertAsync(entity, userName?)` - Insert new entity, returns new ID
- `UpdateAsync(entity, userName?)` - Update existing entity
- `DeleteAsync(id)` - Delete (soft delete if `[SoftDelete]`)
- `HardDeleteAsync(id)` - Permanent delete (if `[SoftDelete]`)

### Bulk Operations
- `BulkInsertAsync(IEnumerable<entity>)` - High-performance bulk insert using SqlBulkCopy
- `BulkUpdateAsync(IEnumerable<entity>)` - Bulk update multiple entities
- `BulkDeleteAsync(IEnumerable<int> ids)` - Bulk delete by IDs

### Reference Table Operations (if `[ReferenceTable]`)
- `GetByNameAsync(string name)` - Get by name field
- `PreloadDataAsync()` - Load reference data on startup

### Features
- **Automatic caching**: If `[Cache]` attribute present, all reads check cache first
- **Cache invalidation**: Writes automatically invalidate cache
- **Audit fields**: `userName` parameter auto-populates CreatedBy/ModifiedBy if `[AutoAudit]`
- **Retry logic**: All operations wrapped in retry policy for transient errors
- **Optimistic concurrency**: If `[RowVersion]` present, updates check row version
- **Soft delete filtering**: All queries automatically filter `IsDeleted = 0`
- **Comprehensive logging**: Every operation logs at Debug/Info/Warning levels

## 🚧 Current Status

**Completed:**
- ✅ Core infrastructure and base classes
- ✅ SQL Server provider
- ✅ SQLite provider
- ✅ **Source generator with full CRUD implementation**
- ✅ Attribute system (Table, Column, PrimaryKey, Identity, Index, Cache, etc.)
- ✅ Caching strategies (Memory, TwoLayer) with automatic cache integration
- ✅ Reference table support with preloading
- ✅ Staging table support
- ✅ Auto-audit tracking with userName parameter
- ✅ Soft delete with both Delete and HardDelete methods
- ✅ Retry policies integrated in generated code
- ✅ Comprehensive logging in generated DAL classes
- ✅ Bulk operations (BulkInsert, BulkUpdate, BulkDelete)
- ✅ Query operations (GetByIds, Count, Exists)
- ✅ **NuGet packages published to GitHub Packages**
- ✅ **Automated versioning and publishing via GitHub Actions**

**In Progress:**
- ⏳ Unit test coverage improvements
- ⏳ Integration tests
- ⏳ Performance benchmarking

**Planned:**
- 📋 Distributed caching (Redis)
- 📋 SignalR integration for cache invalidation
- 📋 Entity relationships (1-1, 1-Many, Many-Many)
- 📋 Query builder API
- 📋 Migration tooling
- 📋 Performance monitoring

## 📦 Package Versioning

HighSpeedDAL follows **Semantic Versioning** (major.minor.build):

- **Major (X.0.0)**: Breaking changes that require code updates
- **Minor (0.X.0)**: New features that are backward compatible
- **Build (0.0.X)**: Bug fixes and patches (auto-incremented on each commit to main)

### Automatic Publishing

- **On commit to main**: Build number automatically increments and packages are published to GitHub Packages
- **On tagged release**: Create a tag like `v1.2.0` to publish a specific version
- **Manual workflow**: Use GitHub Actions workflow dispatch to manually trigger a build with version bump

All packages are published to [GitHub Packages](https://github.com/rhale78?tab=packages&repo_name=HighSpeedDAL) with automatic version synchronization across all library packages.

## 📊 Dependencies

**.NET Version:** 9.0

**Key Packages:**
- Microsoft.Data.SqlClient 5.2.1
- Microsoft.Data.Sqlite 9.0.0
- Microsoft.Extensions.Logging.Abstractions 9.0.0
- Microsoft.Extensions.Diagnostics.HealthChecks 9.0.0
- Microsoft.CodeAnalysis.CSharp 4.11.0
- System.Diagnostics.DiagnosticSource 9.0.0
- Humanizer.Core 2.14.1 (for intelligent table name pluralization)

**Test Packages:**
- xUnit 2.9.2
- FluentAssertions 7.0.0
- Moq 4.20.72

## 📝 License

MIT License - See LICENSE file for details

## 🤝 Contributing

This project is in active development. Contributions are welcome!

## 📞 Support

- GitHub Issues: Report bugs or request features
- Documentation: See `/docs` folder for detailed documentation
- Examples: See `/examples` folder for working samples
