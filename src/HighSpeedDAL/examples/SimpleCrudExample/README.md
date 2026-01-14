# SimpleCrudExample - HighSpeedDAL Console Application

A clean, educational console application demonstrating HighSpeedDAL framework features.

## Overview

This example showcases how to use HighSpeedDAL in a clear, step-by-step manner. By default, it runs an educational feature showcase. Advanced performance tests and benchmarks are available via command-line arguments.

## Quick Start

```bash
# Run default feature showcase (educational demonstrations)
dotnet run

# Show available options
dotnet run -- --help

# Run memory-mapped file demonstrations
dotnet run -- --mmf

# Run performance benchmarks
dotnet run -- --perf

# Run cache behavior tests
dotnet run -- --cache-tests
```

## Features Demonstrated

### Core Features (Default Showcase)
- ? **Basic CRUD Operations** - Create, Read, Update, Delete with explanations
- ? **Caching Behavior** - Cache hits, defensive cloning, cache invalidation
- ? **Bulk Operations** - Efficient bulk insert with performance timing
- ? **Memory-Mapped Files** - Explanation of advanced ultra-high throughput feature

### Advanced Features (Optional Tests)
- ? **Performance Benchmarks** - Comprehensive suite comparing all data access strategies
- ? **Cache Strategy Tests** - TwoLayer cache behavior demonstrations
- ? **Memory-Mapped File Tests** - Full test suite for MMF functionality
- ? **Automatic Property Generation** - Framework auto-generates Id property
- ? **Staging Tables** - High-write performance with background merging
- ? **Connection Management** - Database connection with retry policies
- ? **Dependency Injection** - Proper DI setup with configuration

## Project Structure

```
SimpleCrudExample/
??? Program.cs                      # Simple entry point with command-line args
??? FeatureShowcase.cs             # Educational feature demonstrations
??? PerformanceBenchmarkSuite.cs   # Comprehensive performance tests
??? CacheStrategyTestSuite.cs      # Cache behavior tests
??? MemoryMappedTestSuite.cs       # Memory-mapped file tests
??? HighPerformanceCacheTestSuite.cs # High-performance cache tests
??? Entities/
?   ??? User.cs                    # User entity with caching and staging
?   ??? UserWithMemoryMapped.cs    # Memory-mapped file variant
??? Data/
?   ??? UserDatabaseConnection.cs  # Database connection class
??? appsettings.json               # Configuration file
??? SimpleCrudExample.csproj       # Project file
```

## Prerequisites

- .NET 9.0 SDK
- SQL Server LocalDB (or update connection string in appsettings.json)

## Configuration

### Database Connection

Edit `appsettings.json` to configure your database:

```json
{
  "ConnectionStrings": {
    "UserDatabase": "Server=(localdb)\\mssqllocaldb;Database=SimpleCrudExample;Integrated Security=true;TrustServerCertificate=true"
  }
}
```

### Alternative Database Options

**SQL Server:**
```json
"UserDatabase": "Server=localhost;Database=SimpleCrudExample;User Id=sa;Password=YourPassword;TrustServerCertificate=true"
```

**SQL Server Express:**
```json
"UserDatabase": "Server=.\\SQLEXPRESS;Database=SimpleCrudExample;Integrated Security=true;TrustServerCertificate=true"
```

## Usage Modes

### 1. Default Mode: Feature Showcase (Recommended for Learning)

Simply run without arguments to see educational demonstrations:

```bash
dotnet run
```

**What You'll See:**
- **Section 1: Basic CRUD Operations**
  - Creating users (INSERT)
  - Reading user by ID and all users (SELECT)
  - Updating user data (UPDATE)
  - Deleting users (DELETE)
  - Clear explanations at each step

- **Section 2: Caching Behavior**
  - How memory cache works (first read vs. cached read)
  - Defensive cloning demonstration (mutations don't affect cache)
  - Cache invalidation on updates
  - Performance comparisons

- **Section 3: Bulk Operations**
  - Bulk inserting 1,000 users
  - Performance timing (operations per second)
  - Staging table explanation

- **Section 4: Memory-Mapped Files**
  - Explanation of ultra-high throughput feature
  - Use cases (logging, telemetry)
  - How to enable in your entities

### 2. Memory-Mapped File Demonstrations

Run comprehensive memory-mapped file tests:

```bash
dotnet run -- --memory-mapped-demo
# or short form:
dotnet run -- --mmf
```

**Tests Include:**
- CRUD operations with InMemoryTable
- Direct memory-mapped file operations
- Performance benchmarks (1M+ ops/sec)
- Concurrent access tests
- Long-running stress tests

### 3. Performance Benchmarks

Run comprehensive performance tests comparing all data access strategies:

```bash
dotnet run -- --performance-tests
# or short form:
dotnet run -- --perf
```

**Benchmark Categories:**
- SELECT operations (DB only, Memory Cache, TwoLayer Cache)
- INSERT operations
- UPDATE operations
- DELETE operations
- BULK INSERT operations
- BULK UPDATE operations
- BULK DELETE operations

**Features:**
- Live metrics (updates every second)
- Category winners with medals (??????)
- Performance comparisons (how much faster vs. baseline)
- Memory tracking
- Maximum 10 seconds per test

### 4. Cache Behavior Tests

Run TwoLayer cache behavior demonstrations:

```bash
dotnet run -- --cache-tests
```

**Tests Include:**
- Cache warming and hit rates
- Cache invalidation behavior
- TwoLayer cache synchronization
- Concurrent access patterns

### 5. Help

Show all available options:

```bash
dotnet run -- --help
# or short form:
dotnet run -- -h
```

## Entity Configuration

The `User` entity is configured with:

```csharp
[Table("Users")]
[Cache(CacheStrategy.Memory, MaxSize = 1000, ExpirationSeconds = 300)]
[StagingTable(SyncIntervalSeconds = 30)]
public partial class User
{
    // Properties: Username, Email, FirstName, LastName, CreatedAt, IsActive
    // Id auto-generated by framework (convention: partial class + no Id property = auto-generated)
}
```

**Key Features:**
- **Table Name**: "Users" (explicitly configured)
- **Caching**: In-memory, max 1000 items, 5-minute expiration
- **Staging Table**: 30-second sync interval for high-write performance
- **Auto-Generated Id**: Framework automatically adds `public int Id { get; set; }`

## Running the Example

### From Command Line

```bash
cd examples/SimpleCrudExample
dotnet run                    # Feature showcase
dotnet run -- --mmf          # Memory-mapped demos
dotnet run -- --perf         # Performance benchmarks
dotnet run -- --cache-tests  # Cache behavior tests
```

### From Visual Studio

1. Set `SimpleCrudExample` as startup project
2. To pass command-line arguments:
   - Right-click project ? Properties ? Debug ? General
   - Add to "Command line arguments": `--mmf` or `--perf` or `--cache-tests`
3. Press F5 or click Run

### From Visual Studio Code

1. Open terminal in VS Code
2. Navigate to `examples/SimpleCrudExample`
3. Run `dotnet run` or `dotnet run -- --mmf` etc.

## What the Default Showcase Demonstrates

The feature showcase (`FeatureShowcase.cs`) provides step-by-step demonstrations:

### 1. Basic CRUD Operations
- Creates two users (John Doe, Jane Smith)
- Retrieves users by ID and all users
- Updates user email and status
- Deletes a user and verifies deletion
- Shows actual framework method calls

### 2. Caching Behavior
- Explains cache configuration (Memory cache, 300s expiration)
- Demonstrates first read (populates cache)
- Demonstrates second read (from cache - faster!)
- Shows defensive cloning protection:
  - Modifying returned object doesn't affect cache
  - Fresh copy from cache is unchanged
  - Cache protected from mutations

### 3. Bulk Operations
- Creates 1,000 users in bulk
- Shows performance timing (milliseconds and ops/second)
- Explains staging table workflow:
  - Writes go to staging table immediately
  - Background process merges every 30 seconds
  - Reads continue without blocking

### 4. Memory-Mapped Files
- Explains ultra-high throughput capability (1M+ ops/sec)
- Describes use cases (logging, telemetry, activity tracking)
- Shows example attribute configuration
- Directs to `--memory-mapped-demo` for hands-on testing

## Advanced Performance Benchmarks

When you run `--perf`, the comprehensive benchmark suite executes:

### Benchmark Features
- **7 Operation Categories**: Tests all major database operations
- **Multiple Scenarios**: Compares DB only, Memory Cache, TwoLayer Cache
- **Live Metrics**: Real-time operations per second every second
- **Category Winners**: Top 3 performers with medals and performance multipliers
- **Max 10s per test**: Fast execution with meaningful results
- **Memory Safe**: Monitors and reports memory usage

### Example Output
```
?????????????????????????????????????????????????????????????????????????????????
?          HIGH-PERFORMANCE DATA ACCESS LAYER BENCHMARK SUITE                  ?
?????????????????????????????????????????????????????????????????????????????????

BENCHMARK: SELECT OPERATIONS
???????????????????????????????????????????????????????????????????????????????

Running DB Only... 10s | 12,500 ops | 1,250 ops/sec
  ? Completed: 12,500 operations in 10,000ms
  • Throughput: 1,250 ops/sec
  • Avg Latency: 0.800ms per operation

Running Memory Cache... 10s | 985,200 ops | 98,520 ops/sec
  ? Completed: 985,200 operations in 10,000ms
  • Throughput: 98,520 ops/sec
  • Avg Latency: 0.010ms per operation

???????????????????????????????????????????????????????????????????????????????
?? WINNER: Memory Cache
   • 98,520 ops/sec
   • 78.8x faster than baseline
???????????????????????????????????????????????????????????????????????????????
```

## Memory-Mapped File Tests

When you run `--mmf`, the memory-mapped file test suite executes:

### Test Categories
1. **CRUD Operations**: Insert, Select, Update, Delete with InMemoryTable
2. **Direct MMF Operations**: Low-level memory-mapped file access
3. **Performance Benchmarks**: Measures throughput (typically 1M+ ops/sec)
4. **Concurrent Access**: Multiple threads accessing shared memory
5. **Long-Running Stress**: Extended tests for stability validation

### Key Features
- Ultra-high throughput (orders of magnitude faster than database)
- Shared memory across processes
- Ideal for logging, telemetry, real-time analytics
- Non-blocking concurrent access

## Cache Strategy Tests

When you run `--cache-tests`, demonstrates:

### Test Categories
1. **Cache Warming**: Populating cache with initial data
2. **Hit Rate Analysis**: Measuring cache effectiveness
3. **Invalidation Patterns**: How updates affect cache
4. **TwoLayer Cache**: Memory + distributed caching together
5. **Concurrent Access**: Multiple threads reading cached data

## Key Learnings

After running this example, you'll understand:

1. **How to define entities** - `[Table]`, `[Cache]`, `[StagingTable]` attributes
2. **How to perform CRUD** - `InsertAsync`, `GetByIdAsync`, `UpdateAsync`, `DeleteAsync`
3. **How caching works** - First read populates, subsequent reads are cached, updates invalidate
4. **How defensive cloning protects cache** - Returned objects are copies, mutations don't affect cache
5. **How bulk operations work** - `BulkInsertAsync` for high-throughput scenarios
6. **How staging tables improve write performance** - Non-blocking writes with background merging
7. **When to use memory-mapped files** - Ultra-high throughput logging and telemetry

## Next Steps

After exploring this example:

1. **Read the Entity Code**: Check `Entities/User.cs` to see attribute configuration
2. **Read the Showcase Code**: Study `FeatureShowcase.cs` for implementation patterns
3. **Experiment**: Modify configurations and see how behavior changes
4. **Run Benchmarks**: Use `--perf` to understand performance characteristics
5. **Explore Advanced Features**: Try `--mmf` for memory-mapped file capabilities
6. **Build Your Own**: Apply patterns to your own entities and database

## Troubleshooting

### Database Connection Errors

**Error**: Cannot connect to SQL Server LocalDB

**Solution**: Install SQL Server LocalDB or update connection string in `appsettings.json`

### Build Errors

**Error**: Source generator warnings or errors

**Solution**: 
- Ensure entities are `partial` if using `[AutoAudit]` or `[SoftDelete]`
- Clean and rebuild: `dotnet clean && dotnet build`

### Cache Not Working

**Issue**: Cache doesn't seem to speed up queries

**Check**:
- Entity has `[Cache]` attribute
- Cache expiration not too short
- Running same query multiple times
- Check logs for cache hit/miss information

## Support

For issues or questions:
- Check the main README.md in repository root
- Review comprehensive documentation in `/docs` folder
- Examine source code in `FeatureShowcase.cs` for usage patterns
- Run `--help` to see all available options
