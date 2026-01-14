# HighSpeedDAL Performance Benchmark Suite

## Overview

The **Performance Benchmark Suite** is a comprehensive testing framework that compares the performance of different data access strategies in HighSpeedDAL. It provides real-time metrics, detailed comparisons, and category-based performance rankings.

## Features

### 🎯 What It Tests

The suite benchmarks **7 operation categories**:

1. **SELECT** - Single record retrieval by ID
2. **INSERT** - Single record insertion
3. **UPDATE** - Single record updates
4. **DELETE** - Single record deletion
5. **BULK INSERT** - Batch insertion operations
6. **BULK UPDATE** - Batch update operations
7. **BULK DELETE** - Batch deletion operations

### 🏆 Comparison Scenarios

Each test compares performance across different access patterns:

- **DB Only (No Cache)** - Baseline database performance
- **DB + Memory Cache** - In-memory dictionary caching
- **DB + TwoLayer Cache** - L1 (fast) + L2 (thread-safe) caching
- **InMemoryTable** _(planned)_ - Ultra-fast in-memory data structures
- **Memory-Mapped Files** _(planned)_ - Persistent memory-mapped storage

### 📊 Live Metrics

Each test provides real-time feedback:

```
Running DB Only... 7s | 1,245 ops | 178 ops/sec
```

- **Elapsed Time** - Current test duration
- **Total Operations** - Cumulative operations completed
- **Current Throughput** - Operations per second (updated every second)

### 🎨 Results Display

After each category, results are ranked with medals:

```
Category: SELECT
─────────────────────────────────────────────────────
  🥇 TwoLayer Cache    125,340 ops/sec  |  0.008ms avg  |  1,253,400 total ops
  🥈 Memory Cache       98,520 ops/sec  |  0.010ms avg  |    985,200 total ops
  🥉 DB Only             1,250 ops/sec  |  0.800ms avg  |     12,500 total ops
  → Best performer is 100.3x faster than slowest
```

## How to Run

### From the Console Menu

1. Run the SimpleCrudExample project:
   ```bash
   cd examples/SimpleCrudExample
   dotnet run
   ```

2. Select option **6** from the menu:
   ```
   Select operation mode:
   1. Run Standard CRUD Examples (Default)
   2. Run Memory-Mapped File Test Suite
   3. Run Cache & Performance Benchmarks (Legacy)
   4. Run High-Performance Cache Tests (10s per test, live metrics)
   5. Run Cache Strategy Tests (TwoLayer Cache Behavior)
   6. Run COMPREHENSIVE Performance Benchmark Suite (ALL scenarios)
   
   Enter choice [1-6]: 6
   ```

### Programmatically

```csharp
var userDal = serviceProvider.GetRequiredService<UserDal>();
var benchmarkSuite = new PerformanceBenchmarkSuite(userDal);
await benchmarkSuite.RunAllBenchmarksAsync();
```

## Configuration

### Test Constraints

The suite enforces the following limits:

- **Max Test Duration**: 10 seconds per test
- **Max Memory Usage**: 8GB
- **Test Data**: 1,000 baseline records prepared before benchmarking

### Modifying Constraints

Edit `PerformanceBenchmarkSuite.cs`:

```csharp
private const int MaxTestDurationSeconds = 10;  // Change test duration
private const long MaxMemoryBytes = 8L * 1024 * 1024 * 1024;  // Change memory limit
```

## Output Format

### Header

```
╔═══════════════════════════════════════════════════════════════════════════════╗
║          HIGH-PERFORMANCE DATA ACCESS LAYER BENCHMARK SUITE                  ║
╚═══════════════════════════════════════════════════════════════════════════════╝

Testing Scenarios:
  • DB Only (No Cache)         - Baseline database performance
  • DB + Memory Cache          - In-memory dictionary caching
  • DB + TwoLayer Cache        - L1 (fast) + L2 (thread-safe) caching
  • InMemoryTable              - Ultra-fast in-memory data structures
  • Memory-Mapped Files        - Persistent memory-mapped storage

Constraints:
  • Max Test Duration: 10 seconds per test
  • Max Memory Usage: 8GB
  • Live metrics updated every second
```

### Test Execution

Each test shows:

1. **Live progress** with real-time metrics
2. **Completion summary** with totals
3. **Category winner** announcement

### Final Summary

```
╔═══════════════════════════════════════════════════════════════════════════════╗
║                          FINAL SUMMARY                                        ║
╚═══════════════════════════════════════════════════════════════════════════════╝

Category: SELECT
─────────────────────────────────────────────────────────────────────────────
  🥇 TwoLayer Cache    125,340 ops/sec  |  0.008ms avg  |  1,253,400 total ops
  🥈 Memory Cache       98,520 ops/sec  |  0.010ms avg  |    985,200 total ops
  🥉 DB Only             1,250 ops/sec  |  0.800ms avg  |     12,500 total ops
  → Best performer is 100.3x faster than slowest

Category: INSERT
─────────────────────────────────────────────────────────────────────────────
  🥇 DB + Cache         5,420 ops/sec  |  0.184ms avg  |     54,200 total ops
  🥈 DB Only            5,280 ops/sec  |  0.189ms avg  |     52,800 total ops
  → Best performer is 1.0x faster than slowest

[... more categories ...]

═══════════════════════════════════════════════════════════════════════════════
                  BENCHMARK SUITE COMPLETED SUCCESSFULLY
═══════════════════════════════════════════════════════════════════════════════
```

## Memory Tracking

The suite tracks memory usage:

```
Initial Memory: 45.23 MB

[... tests run ...]

Final Memory: 128.47 MB
Memory Used: 83.24 MB
```

## Performance Metrics

### Metrics Captured

For each scenario, the suite captures:

- **Total Operations** - Number of operations completed
- **Elapsed Time** - Total test duration in milliseconds
- **Throughput (ops/sec)** - Operations per second
- **Average Latency** - Average time per operation in milliseconds

### Comparison Metrics

The suite automatically calculates:

- **Performance Improvement** - How much faster vs. slowest scenario
- **Category Winners** - Top 3 performers per category
- **Cross-Category Analysis** - Overall best performers

## Implementation Details

### Architecture

```
PerformanceBenchmarkSuite
├── BenchmarkCategory (per operation type)
│   ├── Results (list of BenchmarkResult)
│   └── Winner (top performer)
└── BenchmarkResult (per scenario)
    ├── ScenarioName
    ├── TotalOps
    ├── ElapsedMs
    ├── OpsPerSecond
    └── AvgLatencyMs
```

### Key Methods

- `RunAllBenchmarksAsync()` - Main entry point
- `BenchmarkSelectOperationsAsync()` - SELECT benchmarks
- `BenchmarkInsertOperationsAsync()` - INSERT benchmarks
- `BenchmarkUpdateOperationsAsync()` - UPDATE benchmarks
- `BenchmarkDeleteOperationsAsync()` - DELETE benchmarks
- `BenchmarkBulkInsertOperationsAsync()` - BULK INSERT benchmarks
- `BenchmarkBulkUpdateOperationsAsync()` - BULK UPDATE benchmarks
- `BenchmarkBulkDeleteOperationsAsync()` - BULK DELETE benchmarks
- `RunTimedBenchmarkAsync()` - Generic timed benchmark runner with live metrics

### Thread Safety

The suite uses:

- `CancellationTokenSource` for timeout management
- Thread-safe counters for live metrics
- Async/await patterns throughout
- Proper resource cleanup with try/finally blocks

## Prerequisites

- SQL Server instance (configured in `appsettings.json`)
- .NET 9.0 SDK
- HighSpeedDAL Core and SqlServer packages

## Database Configuration

Update `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "UserDatabase": "Server=.;Database=SimpleCrudExample;Integrated Security=true;TrustServerCertificate=true"
  }
}
```

## Extending the Suite

### Adding New Scenarios

To add a new comparison scenario (e.g., InMemoryTable):

1. Add test method:
   ```csharp
   private async Task BenchmarkInMemoryTableAsync()
   {
       // Implementation
   }
   ```

2. Call from `RunAllBenchmarksAsync()`:
   ```csharp
   await BenchmarkInMemoryTableAsync();
   ```

3. Add results to appropriate category:
   ```csharp
   category.AddResult("InMemoryTable", opsPerSecond, totalOps, avgLatency);
   ```

### Adding New Categories

1. Initialize in `InitializeCategories()`:
   ```csharp
   _categoryResults["NEW_CATEGORY"] = new BenchmarkCategory { Name = "NEW CATEGORY" };
   ```

2. Create benchmark method:
   ```csharp
   private async Task BenchmarkNewCategoryAsync()
   {
       var category = _categoryResults["NEW_CATEGORY"];
       // Run tests and add results
   }
   ```

## Troubleshooting

### Common Issues

**Issue**: Test times out immediately
- **Solution**: Check database connectivity in `appsettings.json`

**Issue**: Memory usage exceeds 8GB
- **Solution**: Reduce batch sizes or number of test records

**Issue**: No live metrics displayed
- **Solution**: Ensure console supports `\r` (carriage return) for in-place updates

**Issue**: Build warnings about unused fields
- **Solution**: These are benign and don't affect functionality

## Best Practices

1. **Run on dedicated hardware** - Avoid running other intensive apps during benchmarking
2. **Warm up the database** - First run may be slower due to cold cache
3. **Check memory before running** - Ensure sufficient available memory
4. **Review connection pooling** - Database connection pool size affects performance
5. **Monitor SQL Server** - Use SQL Server Management Studio to monitor during tests

## Future Enhancements

Planned additions:

- [ ] InMemoryTable performance comparisons
- [ ] Memory-Mapped file benchmarks
- [ ] Distributed cache scenarios (Redis)
- [ ] Concurrent access patterns
- [ ] Complex query benchmarks
- [ ] Transaction performance
- [ ] Export results to CSV/JSON
- [ ] Historical comparison tracking
- [ ] Automated regression detection

## Related Documentation

- [SimpleCrudExample README](README.md)
- [HighPerformanceCacheTestSuite](HighPerformanceCacheTestSuite.cs)
- [CacheStrategyTestSuite](CacheStrategyTestSuite.cs)
- [MemoryMappedTestSuite](MemoryMappedTestSuite.cs)

## Contributing

To contribute improvements:

1. Test your changes with various data volumes
2. Ensure all scenarios complete within time limits
3. Add appropriate error handling
4. Update this documentation
5. Submit a pull request

## License

This benchmark suite is part of the HighSpeedDAL project and follows the same license terms.
