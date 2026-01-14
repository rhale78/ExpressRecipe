# Copilot Instructions - HighSpeedDAL

## Project Overview

HighSpeedDAL is a high-performance Data Access Layer (DAL) framework for .NET 9 that provides attribute-driven CRUD operations, intelligent caching, and enterprise-grade features. The framework uses Roslyn source generators to create optimized, type-safe DAL classes at compile-time.

## Architecture

- **Core Layer**: Base abstractions, attributes, and interfaces (`HighSpeedDAL.Core`)
- **Source Generators**: Roslyn-based code generation for CRUD operations (`HighSpeedDAL.SourceGenerators`)
- **Database Providers**: SQL Server and SQLite implementations
- **Advanced Features**: Caching strategies, data management, staging tables
- **Convention over Configuration**: Automatic table name pluralization (using Humanizer), auto-detection of primary keys, property auto-generation

## Tech Stack

- **.NET Version**: 9.0
- **Language**: C# with latest language features
- **Nullable Reference Types**: Enabled (`<Nullable>enable</Nullable>`)
- **Implicit Usings**: Enabled for most projects
- **Database Providers**: 
  - Microsoft.Data.SqlClient 5.2.1
  - Microsoft.Data.Sqlite 9.0.0
- **Key Libraries**:
  - Microsoft.CodeAnalysis.CSharp 4.11.0 (source generators)
  - Polly 8.5.0 (retry policies)
  - Humanizer.Core 2.14.1 (pluralization)
- **Testing**: xUnit, FluentAssertions, Moq
- **Build Tool**: dotnet CLI

## Coding Standards and Conventions

### Naming Conventions
- **Interfaces**: Prefix with `I` (e.g., `ICacheManager`, `IDbConnectionFactory`)
- **Attributes**: Suffix with `Attribute` (e.g., `TableAttribute`, `CacheAttribute`)
- **Test Classes**: Suffix with `Tests` (e.g., `RetryPolicyTests`, `VersionManagerTests`)
- **Private Fields**: Use underscore prefix (e.g., `_logger`, `_connectionString`)

### Code Style
- **XML Documentation**: Required for all public types and members (CS1591 warning enabled)
- **Warnings as Errors**: Configured in project files but currently disabled temporarily (`<TreatWarningsAsErrors>false</TreatWarningsAsErrors>`)
- **Async/Await**: All database operations are asynchronous with `CancellationToken` support
- **Logging**: Use `ILogger` for comprehensive logging at Debug/Info/Warning levels
- **Error Handling**: Implement retry policies for transient database errors using Polly

### Attribute-Driven Development Pattern
The framework follows an attribute-driven approach where entity classes are decorated with attributes to control behavior:

- **`[Table]`**: Optional - marks class as entity, defaults to pluralized class name
- **`[Table("CustomName")]`**: Override table name
- **`[Column]`**: Optional - override column properties (Name, TypeName, MaxLength)
- **`[PrimaryKey]`**: Optional - auto-detected for `int Id` properties
- **`[Identity]`**: Optional - marks auto-increment columns
- **`[Index]`**: Create database indexes
- **`[Cache]`**: Enable caching with strategies (Memory, Distributed, TwoLayer)
- **`[AutoAudit]`**: Auto-generate audit properties (requires `partial` class)
- **`[SoftDelete]`**: Enable soft delete support (requires `partial` class)
- **`[StagingTable]`**: High-write scenarios with batch merges
- **`[ReferenceTable]`**: Pre-loaded lookup tables

### Source Generator Guidelines
When working with source generators:
- Entity classes with `[AutoAudit]` or `[SoftDelete]` MUST be declared as `partial`
- Source generators automatically create missing properties (Id, CreatedBy, CreatedDate, ModifiedBy, ModifiedDate, IsDeleted, DeletedDate, DeletedBy)
- Generated DAL classes inherit from `SqlServerDalBase<TEntity, TConnection>` or `SqliteDalBase<TEntity, TConnection>`
- All generated operations include logging, retry logic, and cache integration

### Testing Conventions
- Use xUnit for test framework
- Use FluentAssertions for readable assertions (`.Should()` pattern)
- Use Moq for mocking dependencies (`Mock<ILogger>`)
- Test classes implement `IDisposable` when managing resources
- Use in-memory SQLite for database tests (`"Data Source=:memory:"`)
- Test method naming: `MethodName_Scenario_ExpectedBehavior` pattern

## Building and Testing

### Build Commands
```bash
# Build entire solution
dotnet build

# Build specific project
dotnet build src/HighSpeedDAL.Core/HighSpeedDAL.Core.csproj

# Clean and rebuild
dotnet clean && dotnet build
```

### Test Commands
```bash
# Run all tests
dotnet test

# Run tests for specific project
dotnet test tests/HighSpeedDAL.Core.Tests/HighSpeedDAL.Core.Tests.csproj

# Run tests with detailed output
dotnet test --verbosity detailed

# Run tests with code coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Restore Dependencies
```bash
dotnet restore
```

## Key Design Patterns

### Convention over Configuration
- Table names automatically pluralized using Humanizer (Product → Products, Person → People, Category → Categories)
- Primary key auto-detected if property named `Id` or ends with `Id`
- If no primary key exists, framework auto-generates `int Id` property
- Column names default to property names unless overridden with `[Column]`

### Auto-Generated Operations
For each entity with `[Table]` attribute, source generators create:
- Read operations: `GetByIdAsync`, `GetAllAsync`, `GetByIdsAsync`, `CountAsync`, `ExistsAsync`
- Write operations: `InsertAsync`, `UpdateAsync`, `DeleteAsync`, `HardDeleteAsync`
- Bulk operations: `BulkInsertAsync`, `BulkUpdateAsync`, `BulkDeleteAsync`
- All operations include automatic caching, retry logic, and audit field population

### Resilience and Performance
- Database retry policies with exponential backoff for transient errors
- Connection pooling and factory pattern for connections
- Multiple caching strategies (Memory, Distributed, TwoLayer)
- Staging tables for high-write scenarios with periodic batch merges
- Bulk operations optimized with SqlBulkCopy

## Important Guidelines

### When Adding New Features
1. Follow the attribute-driven pattern - add attributes to control behavior
2. Update source generators if new code generation is needed
3. Add comprehensive XML documentation for all public APIs
4. Include unit tests using xUnit and FluentAssertions
5. Ensure async/await patterns with CancellationToken support
6. Add logging at appropriate levels (Debug for details, Info for operations, Warning for issues)

### When Modifying Existing Code
1. Maintain backward compatibility with existing attributes and APIs
2. Do not break existing source generator functionality
3. Update XML documentation if changing public APIs
4. Add or update tests for modified functionality
5. Ensure generated code remains optimized and type-safe

### Security Considerations
- Always use parameterized queries (framework handles this automatically)
- Validate input in public APIs
- Use secure connection strings (avoid hardcoding credentials)
- Implement proper error handling without exposing sensitive information
- Support audit tracking for compliance requirements

### Performance Best Practices
- Prefer bulk operations for multiple records
- Use appropriate cache strategies based on data access patterns
- Leverage staging tables for high-write scenarios
- Enable indexes on frequently queried columns using `[Index]` attribute
- Monitor and log performance metrics

## Common Scenarios

### Creating a New Entity
```csharp
[Table]  // Creates "Products" table (pluralized)
[Cache(CacheStrategy.Memory, ExpirationSeconds = 300)]
public class Product
{
    // Framework auto-generates: public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}
```

### Entity with Audit and Soft Delete
```csharp
[Table]
[AutoAudit]
[SoftDelete]
public partial class Customer  // MUST be partial for auto-generation
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    
    // Framework auto-generates all audit and soft delete properties
}
```

### Creating Tests
```csharp
public class MyFeatureTests : IDisposable
{
    private readonly Mock<ILogger<MyFeature>> _loggerMock;
    
    public MyFeatureTests()
    {
        _loggerMock = new Mock<ILogger<MyFeature>>();
    }
    
    [Fact]
    public async Task MethodName_Scenario_ExpectedBehavior()
    {
        // Arrange
        var sut = new MyFeature(_loggerMock.Object);
        
        // Act
        var result = await sut.MethodAsync();
        
        // Assert
        result.Should().NotBeNull();
        result.Should().Be(expectedValue);
    }
    
    public void Dispose()
    {
        // Cleanup resources
    }
}
```

## Documentation

- Main README: `/README.md` - comprehensive framework overview and quick start
- Additional docs in `/docs` folder for detailed documentation
- Examples in `/examples` folder for working samples

## Contributing

When contributing to this repository:
1. Follow all coding standards and conventions outlined above
2. Ensure all tests pass before submitting changes
3. Add tests for new functionality
4. Update documentation for public API changes
5. Use meaningful commit messages
6. Maintain the attribute-driven architecture pattern
