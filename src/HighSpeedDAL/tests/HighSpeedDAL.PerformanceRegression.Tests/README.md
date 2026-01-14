# HighSpeedDAL Performance Regression Test Suite

This test suite uses BenchmarkDotNet to track performance over time and detect regressions. It establishes baseline metrics and fails tests when performance degrades beyond acceptable thresholds.

## Overview

The performance regression suite consists of three main test classes:

1. **BulkOperationsRegressionTests** - Bulk insert, update, delete operations
2. **CacheRegressionTests** - Caching operations and defensive cloning
3. **ConcurrentAccessRegressionTests** - Parallel reads, writes, and mixed workloads

## Baseline Metrics

All baselines are established from the SQLite integration tests (11 tests, 100% passing).

### Bulk Operations

| Test | Baseline | Threshold | Tolerance |
|------|----------|-----------|-----------|
| Bulk Insert 10K | 228,930 rows/sec (43ms) | 205,000 rows/sec (50ms) | 10% |
| Bulk Insert 5K (with indexes) | <2s | <3s | 10% |
| Bulk Update 5K | 24ms | 3s | 10% |
| Bulk Soft Delete 5K | 1.5s | 2s | 10% |
| Mixed Workload 1K | 137,129 ops/sec (7ms) | 90,000 ops/sec (12ms) | 10% |

### Caching Operations

| Test | Baseline | Threshold | Tolerance |
|------|----------|-----------|-----------|
| Cache Hit (Sequential) 10K | <1ms per op | <2ms per op | 20% |
| Cache Hit (Random) 10K | <1ms per op | <2ms per op | 20% |
| Cache Miss 10K | <1ms per op | <2ms per op | 20% |
| Cache Write 10K | <1ms per op | <2ms per op | 20% |
| Cache Update 10K | <1ms per op | <2ms per op | 20% |
| Cache Remove 10K | <1ms per op | <2ms per op | 20% |
| Shallow Clone 10K | <10ms total | <15ms total | 20% |
| Deep Clone 10K | <20ms total | <30ms total | 20% |
| Mixed Cache Ops 10K | <1ms per op | <2ms per op | 20% |

### Concurrent Access

| Test | Baseline | Threshold | Tolerance |
|------|----------|-----------|-----------|
| Concurrent Reads (10 queries) | 100ms | 200ms | 15% |
| Concurrent Reads (50 queries) | 500ms | 1s | 15% |
| Concurrent Reads (100 queries) | 1s | 2s | 15% |
| Sequential Inserts (1K) | 2s | 3s | 15% |
| Sequential Updates (1K) | 2s | 3s | 15% |
| Mixed (50 reads + 100 writes) | 1.5s | 2s | 15% |
| Concurrent Updates (100 bulk) | 100ms | 200ms | 15% |
| High-Frequency Reads (1K) | 400ms | 500ms | 15% |
| Read-Heavy (90 reads + 10 writes) | 800ms | 1s | 15% |

## Running the Tests

### Prerequisites

- .NET 9.0 SDK or later
- Visual Studio 2022 or JetBrains Rider (optional)

### Run All Regression Tests

```bash
cd tests/HighSpeedDAL.PerformanceRegression.Tests
dotnet run -c Release
```

### Run Specific Test Class

```bash
# Bulk operations only
dotnet run -c Release --filter *BulkOperationsRegressionTests*

# Cache operations only
dotnet run -c Release --filter *CacheRegressionTests*

# Concurrent access only
dotnet run -c Release --filter *ConcurrentAccessRegressionTests*
```

### Run Specific Benchmark

```bash
# Bulk insert baseline only
dotnet run -c Release --filter *BulkInsert_10K_Products_Baseline*
```

## Understanding Results

### BenchmarkDotNet Output

BenchmarkDotNet provides detailed performance metrics:

```
| Method                         | Mean      | Error     | StdDev    | Median    | Min       | Max       |
|--------------------------------|-----------|-----------|-----------|-----------|-----------|-----------|
| BulkInsert_10K_Products_Baseline| 43.21 ms  | 2.15 ms   | 0.12 ms   | 43.15 ms  | 41.05 ms  | 45.37 ms  |
```

**Metrics**:
- **Mean**: Average execution time
- **Error**: Margin of error (confidence interval)
- **StdDev**: Standard deviation (consistency)
- **Median**: Middle value (ignores outliers)
- **Min/Max**: Fastest/slowest runs

### Regression Detection

A regression is detected when:
1. Mean execution time exceeds baseline + threshold
2. Median execution time exceeds baseline + threshold
3. Minimum execution time exceeds baseline threshold

**Example**:
- Baseline: 43ms (228,930 rows/sec)
- Threshold: 50ms (205,000 rows/sec)
- Current: 55ms ? **REGRESSION DETECTED** (27% slower)

## Updating Baselines

Baselines should be updated after:
1. Major performance optimizations
2. Framework version upgrades
3. Significant architectural changes
4. New hardware/environment

### How to Update

1. Run all regression tests to get new measurements
2. Verify improvements are real (not environment-specific)
3. Update `baseline-metrics.json` with new values
4. Document the reason for baseline change
5. Commit updated baselines with clear description

**Example**:
```json
{
  "bulkOperations": {
    "bulkInsert10K": {
      "baselineRowsPerSecond": 300000,  // Updated from 228,930
      "thresholdRowsPerSecond": 270000,  // Updated from 205,000
      "lastUpdated": "2025-01-15",
      "reason": "Optimized SqlBulkCopy batch size"
    }
  }
}
```

## Interpreting Failures

### Scenario 1: Consistent Regression

**Symptoms**:
- All runs consistently slower than baseline
- StdDev is low (consistent results)
- Regression exceeds tolerance

**Actions**:
1. Check recent code changes (git log)
2. Profile the slow operation
3. Identify bottleneck
4. Fix and re-run tests

### Scenario 2: Inconsistent Results

**Symptoms**:
- High StdDev (inconsistent results)
- Some runs pass, others fail
- Large Min/Max spread

**Actions**:
1. Check system load (CPU, memory, disk)
2. Close background applications
3. Increase warmup iterations
4. Run tests multiple times

### Scenario 3: Environment Change

**Symptoms**:
- All tests slower (not just one)
- Consistent degradation across categories
- New hardware or OS upgrade

**Actions**:
1. Verify environment specifications
2. Re-establish baselines for new environment
3. Document environment change
4. Update baseline-metrics.json

## Best Practices

### 1. Run in Release Mode

Always run benchmarks in Release mode for accurate measurements:
```bash
dotnet run -c Release
```

### 2. Close Background Applications

Minimize system load during benchmarks:
- Close browsers, IDEs, development tools
- Disable antivirus real-time scanning (if safe)
- Ensure no other CPU-intensive processes

### 3. Multiple Runs

Run benchmarks multiple times to verify consistency:
```bash
dotnet run -c Release --iterationCount 20
```

### 4. Monitor Trends

Track performance over time, not just single runs:
- Keep historical data in `baseline-metrics.json`
- Plot trends to identify gradual degradation
- Set alerts for significant deviations

### 5. Automate in CI/CD

Integrate regression tests into CI/CD pipeline:
```yaml
# Example: GitHub Actions
- name: Run Performance Regression Tests
  run: dotnet run -c Release --project tests/HighSpeedDAL.PerformanceRegression.Tests
  
- name: Compare Against Baseline
  run: ./scripts/compare-performance.ps1
```

## Troubleshooting

### Issue: BenchmarkDotNet Not Running

**Symptom**: Tests execute as xUnit tests instead of benchmarks

**Solution**: Use `dotnet run` instead of `dotnet test`:
```bash
# Wrong
dotnet test tests/HighSpeedDAL.PerformanceRegression.Tests

# Correct
dotnet run -c Release --project tests/HighSpeedDAL.PerformanceRegression.Tests
```

### Issue: Out of Memory

**Symptom**: Tests fail with OutOfMemoryException

**Solution**: Reduce data size or increase available memory:
```csharp
private const int DataSize = 1000;  // Reduced from 10000
```

### Issue: SQLite Database Locked

**Symptom**: "Database is locked" errors during concurrent tests

**Solution**: SQLite in-memory databases don't support true concurrent writes. Tests are designed for sequential writes with concurrent reads.

### Issue: Inconsistent Cache Results

**Symptom**: Cache tests show high variance

**Solution**: Ensure cache is warmed up before benchmarks:
```csharp
[GlobalSetup]
public void Setup()
{
    // Pre-populate cache
    for (int i = 0; i < CacheSize; i++)
    {
        _cacheManager.Set($"key:{i}", data, expiration);
    }
}
```

## Advanced Usage

### Custom Benchmarks

Add new benchmarks for specific scenarios:

```csharp
[Benchmark]
public async Task CustomScenario_MyUseCase()
{
    // Your custom benchmark logic
}
```

### Parameterized Benchmarks

Test with different data sizes:

```csharp
[Params(1000, 5000, 10000)]
public int DataSize { get; set; }

[Benchmark]
public async Task BulkInsert_Parameterized()
{
    var products = GenerateProducts(DataSize);
    await _productDal.BulkInsertAsync(products);
}
```

### Memory Diagnostics

Analyze memory allocation patterns:

```csharp
[MemoryDiagnoser]
public class MyRegressionTests
{
    [Benchmark]
    public void MyOperation()
    {
        // BenchmarkDotNet will report:
        // - Gen0/Gen1/Gen2 collections
        // - Allocated bytes
        // - Memory pressure
    }
}
```

## Integration with CI/CD

### GitHub Actions Example

```yaml
name: Performance Regression Tests

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main ]

jobs:
  regression-tests:
    runs-on: ubuntu-latest
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: 9.0.x
    
    - name: Restore dependencies
      run: dotnet restore
    
    - name: Run Regression Tests
      run: dotnet run -c Release --project tests/HighSpeedDAL.PerformanceRegression.Tests
    
    - name: Upload Results
      uses: actions/upload-artifact@v3
      with:
        name: benchmark-results
        path: tests/HighSpeedDAL.PerformanceRegression.Tests/BenchmarkDotNet.Artifacts/results/
    
    - name: Compare Against Baseline
      run: ./scripts/compare-performance.ps1
      continue-on-error: false  # Fail CI if regression detected
```

### Azure DevOps Example

```yaml
trigger:
  branches:
    include:
    - main
    - develop

pool:
  vmImage: 'ubuntu-latest'

steps:
- task: UseDotNet@2
  inputs:
    version: '9.0.x'

- script: dotnet restore
  displayName: 'Restore dependencies'

- script: dotnet run -c Release --project tests/HighSpeedDAL.PerformanceRegression.Tests
  displayName: 'Run Regression Tests'

- task: PublishBuildArtifacts@1
  inputs:
    pathToPublish: 'tests/HighSpeedDAL.PerformanceRegression.Tests/BenchmarkDotNet.Artifacts/results'
    artifactName: 'benchmark-results'

- script: ./scripts/compare-performance.ps1
  displayName: 'Compare Against Baseline'
```

## Performance Optimization Tips

If regression tests detect performance degradation:

### 1. Profile the Operation

Use BenchmarkDotNet's built-in profiling:
```bash
dotnet run -c Release --profiler EP  # Event Pipe Profiler
```

### 2. Check Database Indexes

Ensure proper indexes exist:
```sql
CREATE INDEX idx_Products_Category ON Products(Category);
CREATE INDEX idx_Products_Price ON Products(Price);
```

### 3. Optimize Bulk Operations

- Use `BulkInsertAsync` instead of multiple `InsertAsync`
- Batch updates: `BulkUpdateAsync` instead of loop
- Transaction wrapping for multiple operations

### 4. Cache Effectively

- Use appropriate cache strategies (Memory vs TwoLayer)
- Set reasonable expiration times
- Leverage defensive cloning

### 5. Review Query Patterns

- Avoid N+1 queries
- Use projections to reduce data transfer
- Leverage indexes for WHERE clauses

## Reporting

### Generate Markdown Report

```bash
dotnet run -c Release --exporters markdown
```

Output: `BenchmarkDotNet.Artifacts/results/BulkOperationsRegressionTests-report.md`

### Generate HTML Report

```bash
dotnet run -c Release --exporters html
```

Output: `BenchmarkDotNet.Artifacts/results/BulkOperationsRegressionTests-report.html`

### Generate CSV for Analysis

```bash
dotnet run -c Release --exporters csv
```

Output: `BenchmarkDotNet.Artifacts/results/BulkOperationsRegressionTests-report.csv`

## Historical Tracking

Maintain performance history in `baseline-metrics.json`:

```json
{
  "historicalData": {
    "dataPoints": [
      {
        "date": "2025-01-01",
        "bulkInsert10K": {
          "meanTimeMs": 43,
          "rowsPerSecond": 228930
        }
      },
      {
        "date": "2025-01-15",
        "bulkInsert10K": {
          "meanTimeMs": 38,
          "rowsPerSecond": 263158,
          "improvement": "14.7%",
          "reason": "Optimized batch size"
        }
      }
    ]
  }
}
```

## Summary

The performance regression test suite provides:

? **Automated Performance Tracking** - BenchmarkDotNet precision measurements  
? **Regression Detection** - Fail tests when performance degrades >10%  
? **Historical Baseline** - Track performance trends over time  
? **CI/CD Integration** - Automated regression detection on commits  
? **Comprehensive Coverage** - Bulk ops, caching, concurrent access  
? **Actionable Results** - Clear metrics for optimization decisions  

**Key Metrics**:
- Bulk insert: 228,930 rows/sec baseline (10% tolerance)
- Mixed workload: 137,129 ops/sec baseline (10% tolerance)
- Cache operations: <1ms per operation (20% tolerance)
- Concurrent reads: 10-100 queries validated (15% tolerance)

**Next Steps**:
1. Run initial baseline: `dotnet run -c Release`
2. Review results and verify baselines
3. Integrate into CI/CD pipeline
4. Track performance over time
5. Update baselines after optimizations

For questions or issues, see [Main README](../../README.md) or [GitHub Issues](https://github.com/rhale78/HighSpeedDAL/issues).
