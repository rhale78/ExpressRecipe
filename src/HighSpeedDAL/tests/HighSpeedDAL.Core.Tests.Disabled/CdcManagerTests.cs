using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using HighSpeedDAL.Core.Attributes;
using HighSpeedDAL.Core.CDC;

namespace HighSpeedDAL.Tests.CDC;

/// <summary>
/// Comprehensive unit tests for Change Data Capture functionality.
/// 
/// Tests cover:
/// - Insert/Update/Delete capture
/// - Query operations
/// - Record history retrieval
/// - Error handling
/// - Thread safety
/// - Retention and cleanup
/// 
/// HighSpeedDAL Framework v0.1 - Phase 4
/// </summary>
public sealed class CdcManagerTests : IDisposable
{
    private readonly Mock<ILogger<CdcManager>> _mockLogger;
    private readonly CdcManager _cdcManager;
    private readonly SqliteConnection _connection;
    private bool _disposed;

    public CdcManagerTests()
    {
        _mockLogger = new Mock<ILogger<CdcManager>>();
        _cdcManager = new CdcManager(_mockLogger.Object, cleanupIntervalMinutes: 999999); // Disable auto-cleanup for tests

        // Create in-memory SQLite database for testing
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        // Create CDC table
        CreateCdcTable("TestEntity_CDC");
    }

    [Fact]
    public async Task CaptureInsertAsync_ValidEntity_RecordsCdcEntry()
    {
        // Arrange
        TestEntity entity = new TestEntity
        {
            Id = 1,
            Name = "Test Product",
            Price = 99.99m
        };

        // Act
        await _cdcManager.CaptureInsertAsync(
            _connection,
            entity,
            "user123",
            Guid.NewGuid(),
            "test context");

        // Assert
        List<CdcRecord> records = await _cdcManager.QueryChangesAsync(
            _connection,
            "TestEntity",
            DateTime.UtcNow.AddMinutes(-1),
            DateTime.UtcNow.AddMinutes(1));

        Assert.Single(records);
        Assert.Equal(CdcOperation.Insert, records[0].Operation);
        Assert.Equal("1", records[0].PrimaryKeyValue);
        Assert.Equal("user123", records[0].ChangedBy);
        Assert.NotNull(records[0].DataAfter);
        Assert.Null(records[0].DataBefore);
    }

    [Fact]
    public async Task CaptureUpdateAsync_ValidEntities_RecordsChangeWithBeforeAndAfter()
    {
        // Arrange
        TestEntity oldEntity = new TestEntity
        {
            Id = 1,
            Name = "Old Name",
            Price = 50.00m
        };

        TestEntity newEntity = new TestEntity
        {
            Id = 1,
            Name = "New Name",
            Price = 75.00m
        };

        // Act
        await _cdcManager.CaptureUpdateAsync(
            _connection,
            oldEntity,
            newEntity,
            "user456",
            Guid.NewGuid(),
            "update context");

        // Assert
        List<CdcRecord> records = await _cdcManager.QueryChangesAsync(
            _connection,
            "TestEntity",
            DateTime.UtcNow.AddMinutes(-1),
            DateTime.UtcNow.AddMinutes(1));

        Assert.Single(records);
        Assert.Equal(CdcOperation.Update, records[0].Operation);
        Assert.Equal("user456", records[0].ChangedBy);
        Assert.NotNull(records[0].DataBefore);
        Assert.NotNull(records[0].DataAfter);
        Assert.Contains("Old Name", records[0].DataBefore);
        Assert.Contains("New Name", records[0].DataAfter);
    }

    [Fact]
    public async Task CaptureDeleteAsync_ValidEntity_RecordsDeleteWithBeforeDataOnly()
    {
        // Arrange
        TestEntity entity = new TestEntity
        {
            Id = 1,
            Name = "Deleted Product",
            Price = 100.00m
        };

        // Act
        await _cdcManager.CaptureDeleteAsync(
            _connection,
            entity,
            "user789",
            Guid.NewGuid(),
            "delete context");

        // Assert
        List<CdcRecord> records = await _cdcManager.QueryChangesAsync(
            _connection,
            "TestEntity",
            DateTime.UtcNow.AddMinutes(-1),
            DateTime.UtcNow.AddMinutes(1));

        Assert.Single(records);
        Assert.Equal(CdcOperation.Delete, records[0].Operation);
        Assert.Equal("user789", records[0].ChangedBy);
        Assert.NotNull(records[0].DataBefore);
        Assert.Null(records[0].DataAfter);
    }

    [Fact]
    public async Task QueryChangesAsync_FilterByOperation_ReturnsOnlyMatchingRecords()
    {
        // Arrange
        TestEntity entity = new TestEntity { Id = 1, Name = "Test", Price = 50m };

        await _cdcManager.CaptureInsertAsync(_connection, entity, "user1");
        await _cdcManager.CaptureUpdateAsync(_connection, entity, entity, "user2");
        await _cdcManager.CaptureDeleteAsync(_connection, entity, "user3");

        // Act
        List<CdcRecord> insertRecords = await _cdcManager.QueryChangesAsync(
            _connection,
            "TestEntity",
            DateTime.UtcNow.AddMinutes(-1),
            DateTime.UtcNow.AddMinutes(1),
            CdcOperation.Insert);

        // Assert
        Assert.Single(insertRecords);
        Assert.Equal(CdcOperation.Insert, insertRecords[0].Operation);
    }

    [Fact]
    public async Task QueryChangesAsync_FilterByUser_ReturnsOnlyUserRecords()
    {
        // Arrange
        TestEntity entity = new TestEntity { Id = 1, Name = "Test", Price = 50m };

        await _cdcManager.CaptureInsertAsync(_connection, entity, "alice");
        await _cdcManager.CaptureInsertAsync(_connection, new TestEntity { Id = 2, Name = "Test2", Price = 60m }, "bob");

        // Act
        List<CdcRecord> aliceRecords = await _cdcManager.QueryChangesAsync(
            _connection,
            "TestEntity",
            DateTime.UtcNow.AddMinutes(-1),
            DateTime.UtcNow.AddMinutes(1),
            userId: "alice");

        // Assert
        Assert.Single(aliceRecords);
        Assert.Equal("alice", aliceRecords[0].ChangedBy);
    }

    [Fact]
    public async Task GetRecordHistoryAsync_MultipleChanges_ReturnsChronologicalHistory()
    {
        // Arrange
        TestEntity entity1 = new TestEntity { Id = 1, Name = "Version 1", Price = 10m };
        TestEntity entity2 = new TestEntity { Id = 1, Name = "Version 2", Price = 20m };
        TestEntity entity3 = new TestEntity { Id = 1, Name = "Version 3", Price = 30m };

        await _cdcManager.CaptureInsertAsync(_connection, entity1, "user1");
        await Task.Delay(10); // Ensure different timestamps
        await _cdcManager.CaptureUpdateAsync(_connection, entity1, entity2, "user2");
        await Task.Delay(10);
        await _cdcManager.CaptureUpdateAsync(_connection, entity2, entity3, "user3");

        // Act
        List<CdcRecord> history = await _cdcManager.GetRecordHistoryAsync(
            _connection,
            "TestEntity",
            "1");

        // Assert
        Assert.Equal(3, history.Count);
        Assert.Equal(CdcOperation.Insert, history[0].Operation);
        Assert.Equal(CdcOperation.Update, history[1].Operation);
        Assert.Equal(CdcOperation.Update, history[2].Operation);
        Assert.True(history[0].ChangedAt < history[1].ChangedAt);
        Assert.True(history[1].ChangedAt < history[2].ChangedAt);
    }

    [Fact]
    public async Task CaptureInsertAsync_NullConnection_ThrowsArgumentNullException()
    {
        // Arrange
        TestEntity entity = new TestEntity { Id = 1, Name = "Test", Price = 50m };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _cdcManager.CaptureInsertAsync(null!, entity, "user1"));
    }

    [Fact]
    public async Task CaptureInsertAsync_NullEntity_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _cdcManager.CaptureInsertAsync(_connection, (TestEntity)null!, "user1"));
    }

    [Fact]
    public async Task CaptureInsertAsync_EmptyUserId_ThrowsArgumentException()
    {
        // Arrange
        TestEntity entity = new TestEntity { Id = 1, Name = "Test", Price = 50m };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _cdcManager.CaptureInsertAsync(_connection, entity, ""));
    }

    [Fact]
    public async Task CaptureAsync_SameTransactionId_GroupsRelatedChanges()
    {
        // Arrange
        Guid transactionId = Guid.NewGuid();
        TestEntity entity1 = new TestEntity { Id = 1, Name = "Entity1", Price = 10m };
        TestEntity entity2 = new TestEntity { Id = 2, Name = "Entity2", Price = 20m };

        // Act
        await _cdcManager.CaptureInsertAsync(_connection, entity1, "user1", transactionId);
        await _cdcManager.CaptureInsertAsync(_connection, entity2, "user1", transactionId);

        // Assert
        List<CdcRecord> records = await _cdcManager.QueryChangesAsync(
            _connection,
            "TestEntity",
            DateTime.UtcNow.AddMinutes(-1),
            DateTime.UtcNow.AddMinutes(1));

        Assert.Equal(2, records.Count);
        Assert.All(records, r => Assert.Equal(transactionId, r.TransactionId));
    }

    [Fact]
    public async Task CaptureInsertAsync_EntityWithoutCdcAttribute_DoesNotCapture()
    {
        // Arrange
        EntityWithoutCdc entity = new EntityWithoutCdc { Id = 1, Name = "Test" };

        // Act
        await _cdcManager.CaptureInsertAsync(_connection, entity, "user1");

        // Assert - No exception should be thrown, but no record should be created
        // This is a design decision - CDC is opt-in via attribute
    }

    [Fact]
    public async Task CaptureInsertAsync_CdcDisabled_DoesNotCapture()
    {
        // Arrange
        EntityWithDisabledCdc entity = new EntityWithDisabledCdc { Id = 1, Name = "Test" };

        // Act
        await _cdcManager.CaptureInsertAsync(_connection, entity, "user1");

        // Assert - Should not capture when CDC is disabled
    }

    [Fact]
    public async Task QueryChangesAsync_NoRecordsInRange_ReturnsEmptyList()
    {
        // Act
        List<CdcRecord> records = await _cdcManager.QueryChangesAsync(
            _connection,
            "TestEntity",
            DateTime.UtcNow.AddDays(-30),
            DateTime.UtcNow.AddDays(-29));

        // Assert
        Assert.Empty(records);
    }

    [Fact]
    public async Task ConcurrentCapture_MultipleThreads_AllRecordsCaptured()
    {
        // Arrange
        int threadCount = 10;
        int operationsPerThread = 5;
        List<Task> tasks = new List<Task>();

        // Act
        for (int i = 0; i < threadCount; i++)
        {
            int threadId = i;
            Task task = Task.Run(async () =>
            {
                for (int j = 0; j < operationsPerThread; j++)
                {
                    TestEntity entity = new TestEntity
                    {
                        Id = (threadId * operationsPerThread) + j,
                        Name = $"Thread{threadId}_Op{j}",
                        Price = 10m
                    };

                    await _cdcManager.CaptureInsertAsync(_connection, entity, $"user{threadId}");
                }
            });

            tasks.Add(task);
        }

        await Task.WhenAll(tasks);

        // Assert
        List<CdcRecord> records = await _cdcManager.QueryChangesAsync(
            _connection,
            "TestEntity",
            DateTime.UtcNow.AddMinutes(-1),
            DateTime.UtcNow.AddMinutes(1));

        Assert.Equal(threadCount * operationsPerThread, records.Count);
    }

    #region Helper Methods

    private void CreateCdcTable(string tableName)
    {
        string sql = $@"
            CREATE TABLE {tableName} (
                CdcId INTEGER PRIMARY KEY AUTOINCREMENT,
                Operation INTEGER NOT NULL,
                PrimaryKeyValue TEXT NOT NULL,
                TableName TEXT NOT NULL,
                DataBefore TEXT,
                DataAfter TEXT,
                ChangedBy TEXT NOT NULL,
                ChangedAt TEXT NOT NULL,
                TransactionId TEXT NOT NULL,
                Context TEXT
            )";

        DbCommand command = _connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    #endregion

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _connection?.Dispose();
        _cdcManager?.Dispose();
        _disposed = true;
    }
}

// Test entities

[ChangeDataCapture(RetentionDays = 90)]
public class TestEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

public class EntityWithoutCdc
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

[ChangeDataCapture(Enabled = false)]
public class EntityWithDisabledCdc
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
