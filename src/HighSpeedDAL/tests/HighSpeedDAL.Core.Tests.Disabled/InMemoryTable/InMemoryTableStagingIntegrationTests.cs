using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using HighSpeedDAL.Core.Attributes;
using HighSpeedDAL.Core.InMemoryTable;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace HighSpeedDAL.Core.Tests.InMemoryTable;

/// <summary>
/// Integration tests for InMemoryTable staging functionality.
/// Tests flush to staging, flush to main table, and load from database using SQLite.
/// </summary>
public class InMemoryTableStagingIntegrationTests : IDisposable
{
    private readonly Mock<ILogger<InMemoryTable<StagingTestEntity>>> _loggerMock;
    private readonly Mock<ILogger<InMemoryTableManager>> _managerLoggerMock;
    private readonly SqliteConnection _connection;
    private InMemoryTable<StagingTestEntity>? _table;
    private InMemoryTableManager? _manager;

    public InMemoryTableStagingIntegrationTests()
    {
        _loggerMock = new Mock<ILogger<InMemoryTable<StagingTestEntity>>>();
        _managerLoggerMock = new Mock<ILogger<InMemoryTableManager>>();

        // Use in-memory SQLite database for testing
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public void Dispose()
    {
        _table?.Dispose();
        _manager?.Dispose();
        _connection?.Dispose();
    }

    private async Task CreateMainTableAsync()
    {
        string createTableSql = @"
            CREATE TABLE IF NOT EXISTS TestEntities (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Email TEXT NOT NULL,
                Age INTEGER NOT NULL,
                IsActive INTEGER NOT NULL DEFAULT 1,
                CreatedAt TEXT NOT NULL,
                NullableValue TEXT
            )";

        using (DbCommand command = _connection.CreateCommand())
        {
            command.CommandText = createTableSql;
            await command.ExecuteNonQueryAsync();
        }
    }

    private async Task CreateStagingTableAsync()
    {
        string createTableSql = @"
            CREATE TABLE IF NOT EXISTS TestEntities_Staging (
                Id INTEGER PRIMARY KEY,
                Name TEXT NOT NULL,
                Email TEXT NOT NULL,
                Age INTEGER NOT NULL,
                IsActive INTEGER NOT NULL DEFAULT 1,
                CreatedAt TEXT NOT NULL,
                NullableValue TEXT
            )";

        using (DbCommand command = _connection.CreateCommand())
        {
            command.CommandText = createTableSql;
            await command.ExecuteNonQueryAsync();
        }
    }

    private async Task<int> GetTableRowCountAsync(string tableName)
    {
        using (DbCommand command = _connection.CreateCommand())
        {
            command.CommandText = $"SELECT COUNT(*) FROM {tableName}";
            object? result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }
    }

    #region Flush to Staging Tests

    [Fact]
    public async Task FlushToStaging_InsertsData_Success()
    {
        // Arrange
        await CreateStagingTableAsync();

        var config = new InMemoryTableAttribute
        {
            FlushIntervalSeconds = 0,
            FlushToStaging = true,
            TrackOperations = true
        };

        _table = new InMemoryTable<StagingTestEntity>(_loggerMock.Object, config, "TestEntities");

        // Insert test data
        await _table.InsertAsync(new StagingTestEntity { Name = "Alice", Email = "alice@test.com", Age = 25 });
        await _table.InsertAsync(new StagingTestEntity { Name = "Bob", Email = "bob@test.com", Age = 30 });

        // Act - Flush to staging
        _manager = new InMemoryTableManager(_managerLoggerMock.Object, () => _connection);
        var wrapper = new InMemoryTableFlushableWrapper<StagingTestEntity>(_table, config, _loggerMock.Object);
        int flushed = await wrapper.FlushAsync(_connection, default);

        // Assert
        flushed.Should().Be(2);
        int stagingRowCount = await GetTableRowCountAsync("TestEntities_Staging");
        stagingRowCount.Should().Be(2);
    }

    [Fact]
    public async Task FlushToStaging_UpdatesData_Success()
    {
        // Arrange
        await CreateStagingTableAsync();

        var config = new InMemoryTableAttribute
        {
            FlushToStaging = true,
            TrackOperations = true
        };

        _table = new InMemoryTable<StagingTestEntity>(_loggerMock.Object, config, "TestEntities");

        // Insert and then update
        var entity = new StagingTestEntity { Name = "Original", Email = "original@test.com", Age = 25 };
        await _table.InsertAsync(entity);

        // Flush insert
        var wrapper = new InMemoryTableFlushableWrapper<StagingTestEntity>(_table, config, _loggerMock.Object);
        await wrapper.FlushAsync(_connection, default);

        // Update
        entity.Name = "Updated";
        entity.Age = 30;
        await _table.UpdateAsync(entity);

        // Act - Flush update
        int flushed = await wrapper.FlushAsync(_connection, default);

        // Assert
        flushed.Should().Be(1);

        // Verify data in staging table
        using (DbCommand command = _connection.CreateCommand())
        {
            command.CommandText = "SELECT Name, Age FROM TestEntities_Staging WHERE Id = @Id";
            var param = command.CreateParameter();
            param.ParameterName = "@Id";
            param.Value = entity.Id;
            command.Parameters.Add(param);

            using (DbDataReader reader = await command.ExecuteReaderAsync())
            {
                reader.Read().Should().BeTrue();
                reader.GetString(0).Should().Be("Updated");
                reader.GetInt32(1).Should().Be(30);
            }
        }
    }

    [Fact]
    public async Task FlushToStaging_DeletesData_Success()
    {
        // Arrange
        await CreateStagingTableAsync();

        var config = new InMemoryTableAttribute
        {
            FlushToStaging = true,
            TrackOperations = true
        };

        _table = new InMemoryTable<StagingTestEntity>(_loggerMock.Object, config, "TestEntities");

        // Insert and flush
        var entity = new StagingTestEntity { Name = "ToDelete", Email = "delete@test.com", Age = 25 };
        await _table.InsertAsync(entity);

        var wrapper = new InMemoryTableFlushableWrapper<StagingTestEntity>(_table, config, _loggerMock.Object);
        await wrapper.FlushAsync(_connection, default);

        int stagingRowCount = await GetTableRowCountAsync("TestEntities_Staging");
        stagingRowCount.Should().Be(1);

        // Delete
        await _table.DeleteAsync(entity.Id);

        // Act - Flush delete
        int flushed = await wrapper.FlushAsync(_connection, default);

        // Assert
        flushed.Should().Be(1);
        stagingRowCount = await GetTableRowCountAsync("TestEntities_Staging");
        stagingRowCount.Should().Be(0);
    }

    #endregion

    #region Flush to Main Table Tests

    [Fact]
    public async Task FlushToMainTable_InsertsData_Success()
    {
        // Arrange
        await CreateMainTableAsync();

        var config = new InMemoryTableAttribute
        {
            FlushToStaging = false, // Flush directly to main table
            TrackOperations = true
        };

        _table = new InMemoryTable<StagingTestEntity>(_loggerMock.Object, config, "TestEntities");

        // Insert test data
        await _table.InsertAsync(new StagingTestEntity { Name = "Alice", Email = "alice@test.com", Age = 25 });
        await _table.InsertAsync(new StagingTestEntity { Name = "Bob", Email = "bob@test.com", Age = 30 });

        // Act - Flush to main table
        var wrapper = new InMemoryTableFlushableWrapper<StagingTestEntity>(_table, config, _loggerMock.Object);
        int flushed = await wrapper.FlushAsync(_connection, default);

        // Assert
        flushed.Should().Be(2);
        int mainRowCount = await GetTableRowCountAsync("TestEntities");
        mainRowCount.Should().Be(2);
    }

    #endregion

    #region Load from Database Tests

    [Fact]
    public async Task LoadFromDatabase_LoadsExistingData_Success()
    {
        // Arrange - Insert data directly into database
        await CreateMainTableAsync();

        using (DbCommand command = _connection.CreateCommand())
        {
            command.CommandText = @"
                INSERT INTO TestEntities (Name, Email, Age, IsActive, CreatedAt)
                VALUES ('Alice', 'alice@test.com', 25, 1, datetime('now'));
                INSERT INTO TestEntities (Name, Email, Age, IsActive, CreatedAt)
                VALUES ('Bob', 'bob@test.com', 30, 1, datetime('now'));
                INSERT INTO TestEntities (Name, Email, Age, IsActive, CreatedAt)
                VALUES ('Charlie', 'charlie@test.com', 35, 1, datetime('now'));";
            await command.ExecuteNonQueryAsync();
        }

        var config = new InMemoryTableAttribute();
        _table = new InMemoryTable<StagingTestEntity>(_loggerMock.Object, config, "TestEntities");

        // Act - Load from database
        int loaded = await _table.LoadFromDatabaseAsync(_connection);

        // Assert
        loaded.Should().Be(3);
        _table.RowCount.Should().Be(3);

        var entities = _table.Select().ToList();
        entities.Should().HaveCount(3);
        entities.Should().Contain(e => e.Name == "Alice");
        entities.Should().Contain(e => e.Name == "Bob");
        entities.Should().Contain(e => e.Name == "Charlie");
    }

    [Fact]
    public async Task LoadFromDatabase_WithWhereClause_LoadsFilteredData_Success()
    {
        // Arrange
        await CreateMainTableAsync();

        using (DbCommand command = _connection.CreateCommand())
        {
            command.CommandText = @"
                INSERT INTO TestEntities (Name, Email, Age, IsActive, CreatedAt)
                VALUES ('Alice', 'alice@test.com', 25, 1, datetime('now'));
                INSERT INTO TestEntities (Name, Email, Age, IsActive, CreatedAt)
                VALUES ('Bob', 'bob@test.com', 30, 1, datetime('now'));
                INSERT INTO TestEntities (Name, Email, Age, IsActive, CreatedAt)
                VALUES ('Charlie', 'charlie@test.com', 35, 1, datetime('now'));";
            await command.ExecuteNonQueryAsync();
        }

        var config = new InMemoryTableAttribute();
        _table = new InMemoryTable<StagingTestEntity>(_loggerMock.Object, config, "TestEntities");

        // Act - Load only records where Age >= 30
        int loaded = await _table.LoadFromDatabaseAsync(_connection, "Age >= 30");

        // Assert
        loaded.Should().Be(2);
        _table.RowCount.Should().Be(2);

        var entities = _table.Select().ToList();
        entities.Should().HaveCount(2);
        entities.Should().Contain(e => e.Name == "Bob");
        entities.Should().Contain(e => e.Name == "Charlie");
        entities.Should().NotContain(e => e.Name == "Alice");
    }

    [Fact]
    public async Task LoadFromStaging_LoadsFromStagingTable_Success()
    {
        // Arrange
        await CreateStagingTableAsync();

        using (DbCommand command = _connection.CreateCommand())
        {
            command.CommandText = @"
                INSERT INTO TestEntities_Staging (Id, Name, Email, Age, IsActive, CreatedAt)
                VALUES (1, 'Staged1', 'staged1@test.com', 25, 1, datetime('now'));
                INSERT INTO TestEntities_Staging (Id, Name, Email, Age, IsActive, CreatedAt)
                VALUES (2, 'Staged2', 'staged2@test.com', 30, 1, datetime('now'));";
            await command.ExecuteNonQueryAsync();
        }

        var config = new InMemoryTableAttribute();
        _table = new InMemoryTable<StagingTestEntity>(_loggerMock.Object, config, "TestEntities");

        // Act - Load from staging
        int loaded = await _table.LoadFromStagingAsync(_connection);

        // Assert
        loaded.Should().Be(2);
        _table.RowCount.Should().Be(2);

        var entities = _table.Select().ToList();
        entities.Should().HaveCount(2);
        entities.Should().Contain(e => e.Name == "Staged1");
        entities.Should().Contain(e => e.Name == "Staged2");
    }

    #endregion

    #region Round-trip Tests

    [Fact]
    public async Task RoundTrip_FlushAndLoad_DataPersists_Success()
    {
        // Arrange
        await CreateMainTableAsync();

        var config = new InMemoryTableAttribute
        {
            FlushToStaging = false,
            TrackOperations = true
        };

        _table = new InMemoryTable<StagingTestEntity>(_loggerMock.Object, config, "TestEntities");

        // Insert data
        await _table.InsertAsync(new StagingTestEntity { Name = "Alice", Email = "alice@test.com", Age = 25 });
        await _table.InsertAsync(new StagingTestEntity { Name = "Bob", Email = "bob@test.com", Age = 30 });

        // Flush to database
        var wrapper = new InMemoryTableFlushableWrapper<StagingTestEntity>(_table, config, _loggerMock.Object);
        int flushed = await wrapper.FlushAsync(_connection, default);
        flushed.Should().Be(2);

        // Clear in-memory table
        _table.Clear();
        _table.RowCount.Should().Be(0);

        // Act - Load from database
        int loaded = await _table.LoadFromDatabaseAsync(_connection);

        // Assert
        loaded.Should().Be(2);
        _table.RowCount.Should().Be(2);

        var entities = _table.Select().ToList();
        entities.Should().HaveCount(2);
        entities.Should().Contain(e => e.Name == "Alice" && e.Age == 25);
        entities.Should().Contain(e => e.Name == "Bob" && e.Age == 30);
    }

    [Fact]
    public async Task RoundTrip_FlushToStagingAndLoadBack_Success()
    {
        // Arrange
        await CreateStagingTableAsync();

        var config = new InMemoryTableAttribute
        {
            FlushToStaging = true,
            TrackOperations = true
        };

        _table = new InMemoryTable<StagingTestEntity>(_loggerMock.Object, config, "TestEntities");

        // Insert data
        await _table.InsertAsync(new StagingTestEntity { Name = "StagedEntity", Email = "staged@test.com", Age = 40 });

        // Flush to staging
        var wrapper = new InMemoryTableFlushableWrapper<StagingTestEntity>(_table, config, _loggerMock.Object);
        await wrapper.FlushAsync(_connection, default);

        // Clear
        _table.Clear();

        // Act - Load from staging
        int loaded = await _table.LoadFromStagingAsync(_connection);

        // Assert
        loaded.Should().Be(1);
        var entity = _table.Select().First();
        entity.Name.Should().Be("StagedEntity");
        entity.Age.Should().Be(40);
    }

    #endregion

    #region Performance Tests

    [Fact]
    public async Task LoadFromDatabase_LargeDataset_CompletesInReasonableTime()
    {
        // Arrange
        await CreateMainTableAsync();

        // Insert 1000 rows into database
        using (DbCommand command = _connection.CreateCommand())
        {
            command.CommandText = "BEGIN TRANSACTION";
            await command.ExecuteNonQueryAsync();

            for (int i = 1; i <= 1000; i++)
            {
                command.CommandText = $@"
                    INSERT INTO TestEntities (Name, Email, Age, IsActive, CreatedAt)
                    VALUES ('Entity{i}', 'entity{i}@test.com', {i % 100}, 1, datetime('now'));";
                await command.ExecuteNonQueryAsync();
            }

            command.CommandText = "COMMIT";
            await command.ExecuteNonQueryAsync();
        }

        var config = new InMemoryTableAttribute();
        _table = new InMemoryTable<StagingTestEntity>(_loggerMock.Object, config, "TestEntities");

        // Act
        var sw = System.Diagnostics.Stopwatch.StartNew();
        int loaded = await _table.LoadFromDatabaseAsync(_connection);
        sw.Stop();

        // Assert
        loaded.Should().Be(1000);
        _table.RowCount.Should().Be(1000);
        sw.ElapsedMilliseconds.Should().BeLessThan(2000); // Should complete in under 2 seconds
    }

    #endregion
}

#region Test Entity

/// <summary>
/// Test entity for staging integration tests
/// </summary>
public class StagingTestEntity
{
    public int Id { get; set; }

    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    public int Age { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? NullableValue { get; set; }
}

#endregion
