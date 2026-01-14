using System;
using Xunit;

namespace HighSpeedDAL.SqlServer.Tests;

/// <summary>
/// Placeholder for legacy SQL Server "raw ADO" integration tests.
///
/// These tests previously used raw SqlClient/SqlCommand/SqlBulkCopy, which conflicts with
/// the repository rule that tests should validate HighSpeedDAL framework usage rather than
/// raw SQL patterns.
///
/// Framework-driven SQL Server usage and integration scenarios are covered by
/// `HighSpeedDAL.FrameworkUsage.Tests` and other provider-specific integration tests.
///
/// To run any legacy raw-SQL diagnostics locally, create a dedicated diagnostics project
/// outside of the normal test suite.
/// </summary>
public sealed class SqlServerHighPerformanceIntegrationTests
{
    [Fact(Skip = "Legacy raw SqlClient integration suite removed from CI. Use framework-driven tests instead.")]
    public void LegacySuiteDisabled()
    {
    }
}
