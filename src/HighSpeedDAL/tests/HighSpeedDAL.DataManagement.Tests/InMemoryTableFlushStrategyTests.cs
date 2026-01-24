using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HighSpeedDAL.Core.Attributes;
using HighSpeedDAL.Core.InMemoryTable;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace HighSpeedDAL.DataManagement.Tests;

/// <summary>
/// Tests for InMemoryTable flush strategy configuration and periodic flush behavior.
/// Validates that the table swap pattern can be properly configured and controlled.
/// </summary>
public class InMemoryTableFlushStrategyTests : IDisposable
{
    private readonly Mock<ILogger<InMemoryTable<TestEntity>>> _loggerMock;
    private readonly InMemoryTableAttribute _defaultConfig;
    private InMemoryTable<TestEntity>? _table;

    public InMemoryTableFlushStrategyTests()
    {
        _loggerMock = new Mock<ILogger<InMemoryTable<TestEntity>>>();
        _defaultConfig = new InMemoryTableAttribute
        {
            FlushIntervalSeconds = 0, // Disable auto-flush for testing
            MaxRowCount = 10000,
            EnforceConstraints = true,
            ValidateOnWrite = true
        };
    }

    public void Dispose()
    {
        _table?.Dispose();
    }

    private InMemoryTable<TestEntity> CreateTable(InMemoryTableAttribute? config = null)
    {
        _table = new InMemoryTable<TestEntity>(_loggerMock.Object, config ?? _defaultConfig);
        return _table;
    }

    #region Flush Strategy Configuration Tests

    /// <summary>
    /// Tests that flush strategy can be set on an InMemoryTable.
    /// This is a prerequisite for periodic flush functionality.
    /// </summary>
    [Fact]
    public async Task SetFlushStrategy_WithValidStrategy_Success()
    {
        // Arrange
        var table = CreateTable();
        var mockStrategy = new Mock<IFlushStrategy<TestEntity>>();
        mockStrategy.Setup(s => s.StrategyName).Returns("TestStrategy");

        // Act
        table.SetFlushStrategy(mockStrategy.Object);

        // Assert - Should not throw
        // Verify by inserting data and checking that strategy is set internally
        var entity = new TestEntity { Name = "Test", Email = "test@example.com" };
        await table.InsertAsync(entity);

        table.RowCount.Should().Be(1);
    }

    /// <summary>
    /// Tests that ConfigurePeriodicFlush can be called without errors.
    /// The actual periodic flush execution requires a database implementation.
    /// </summary>
    [Fact]
    public void ConfigurePeriodicFlush_WithValidInterval_Success()
    {
        // Arrange
        var table = CreateTable();

        // Act - Should not throw
        var action = () => table.ConfigurePeriodicFlush(flushIntervalSeconds: 60);

        // Assert
        action.Should().NotThrow();
    }

    /// <summary>
    /// Tests that ConfigurePeriodicFlush with custom interval is accepted.
    /// </summary>
    [Fact]
    public void ConfigurePeriodicFlush_WithCustomInterval_Accepted()
    {
        // Arrange
        var table = CreateTable();

        // Act - Multiple valid intervals
        var action1 = () => table.ConfigurePeriodicFlush(30);
        var action2 = () => table.ConfigurePeriodicFlush(300);
        var action3 = () => table.ConfigurePeriodicFlush(1800);

        // Assert - All should succeed
        action1.Should().NotThrow();
        action2.Should().NotThrow();
        action3.Should().NotThrow();
    }

    #endregion

    #region Flush Preparation Tests

    /// <summary>
    /// Tests that GetAllDataForFlush returns all entities in the table.
    /// This validates the data collection aspect of the flush strategy.
    /// </summary>
    [Fact]
    public async Task GetAllDataForFlush_WithMultipleEntities_ReturnsAll()
    {
        // Arrange
        var table = CreateTable();
        var entities = new List<TestEntity>
        {
            new TestEntity { Name = "Entity1", Email = "e1@test.com", Age = 25 },
            new TestEntity { Name = "Entity2", Email = "e2@test.com", Age = 30 },
            new TestEntity { Name = "Entity3", Email = "e3@test.com", Age = 35 }
        };

        // Act - Insert all entities
        foreach (var entity in entities)
        {
            await table.InsertAsync(entity);
        }

        // Get all data for flush (simulates what flush strategy would do)
        var allData = new List<TestEntity>();
        await table.ExecuteQueryAsync(allData, null); // null WHERE clause = all rows

        // Assert
        allData.Should().HaveCount(3);
        allData.Select(e => e.Name).Should().Contain("Entity1", "Entity2", "Entity3");
    }

    /// <summary>
    /// Tests that large batches can be prepared for flush.
    /// Validates performance characteristic needed for 2000+ item flush.
    /// </summary>
    [Fact]
    public async Task GetAllDataForFlush_WithLargeBatch_Succeeds()
    {
        // Arrange
        var table = CreateTable(new InMemoryTableAttribute
        {
            FlushIntervalSeconds = 0,
            MaxRowCount = 50000 // Support large batches
        });

        // Act - Insert 2000 entities
        var insertTasks = new List<Task>();
        for (int i = 0; i < 2000; i++)
        {
            var entity = new TestEntity
            {
                Name = $"Entity{i:D4}",
                Email = $"entity{i:D4}@test.com",
                Age = 20 + (i % 50)
            };
            insertTasks.Add(table.InsertAsync(entity));
        }
        await Task.WhenAll(insertTasks);

        // Prepare all data for flush
        var allData = new List<TestEntity>();
        await table.ExecuteQueryAsync(allData, null);

        // Assert
        allData.Should().HaveCount(2000);
        table.RowCount.Should().Be(2000);
    }

    /// <summary>
    /// Tests that flush can be manually triggered.
    /// This simulates the explicit flush call after batch operations.
    /// </summary>
    [Fact]
    public async Task TriggerFlushAsync_WithValidStrategy_CallsFlush()
    {
        // Arrange
        var table = CreateTable();
        var mockStrategy = new Mock<IFlushStrategy<TestEntity>>();
        mockStrategy.Setup(s => s.StrategyName).Returns("TestStrategy");
        mockStrategy.Setup(s => s.FlushAsync(It.IsAny<List<TestEntity>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(3); // Simulate flushing 3 rows

        table.SetFlushStrategy(mockStrategy.Object);

        // Insert test data
        for (int i = 0; i < 3; i++)
        {
            await table.InsertAsync(new TestEntity { Name = $"Entity{i}", Email = $"e{i}@test.com" });
        }

        // Act
        var flushedCount = await table.TriggerFlushAsync();

        // Assert
        flushedCount.Should().Be(3);
        mockStrategy.Verify(s => s.FlushAsync(It.IsAny<List<TestEntity>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Tests that TriggerFlushAsync clears the table after successful flush.
    /// This validates the clear-after-flush semantics.
    /// </summary>
    [Fact]
    public async Task TriggerFlushAsync_AfterSuccessfulFlush_ClearsTable()
    {
        // Arrange
        var table = CreateTable();
        var mockStrategy = new Mock<IFlushStrategy<TestEntity>>();
        mockStrategy.Setup(s => s.StrategyName).Returns("TestStrategy");
        mockStrategy.Setup(s => s.FlushAsync(It.IsAny<List<TestEntity>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        table.SetFlushStrategy(mockStrategy.Object);

        // Insert test data
        await table.InsertAsync(new TestEntity { Name = "Entity1", Email = "e1@test.com" });
        await table.InsertAsync(new TestEntity { Name = "Entity2", Email = "e2@test.com" });

        table.RowCount.Should().Be(2);

        // Act
        var flushedCount = await table.TriggerFlushAsync();

        // Assert
        flushedCount.Should().Be(2);
        table.RowCount.Should().Be(0); // Table should be cleared after flush
    }

    #endregion

    #region Flush Error Handling Tests

    /// <summary>
    /// Tests that TriggerFlushAsync without strategy configured doesn't crash.
    /// Handles the case where flush strategy isn't configured.
    /// </summary>
    [Fact]
    public async Task TriggerFlushAsync_WithoutStrategyConfigured_HandlesGracefully()
    {
        // Arrange
        var table = CreateTable();

        // Insert test data
        await table.InsertAsync(new TestEntity { Name = "Entity1", Email = "e1@test.com" });

        // Act - Should handle gracefully (no strategy set)
        var flushedCount = await table.TriggerFlushAsync();

        // Assert - Should return 0 since no strategy
        flushedCount.Should().Be(0);
        table.RowCount.Should().Be(1); // Data not cleared if flush failed
    }

    /// <summary>
    /// Tests that failed flush attempt doesn't corrupt table state.
    /// Validates that table remains intact if flush strategy throws.
    /// </summary>
    [Fact]
    public async Task TriggerFlushAsync_IfStrategyThrows_TableStatePreserved()
    {
        // Arrange
        var table = CreateTable();
        var mockStrategy = new Mock<IFlushStrategy<TestEntity>>();
        mockStrategy.Setup(s => s.StrategyName).Returns("TestStrategy");
        mockStrategy.Setup(s => s.FlushAsync(It.IsAny<List<TestEntity>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Flush failed"));

        table.SetFlushStrategy(mockStrategy.Object);

        // Insert test data
        await table.InsertAsync(new TestEntity { Name = "Entity1", Email = "e1@test.com" });
        await table.InsertAsync(new TestEntity { Name = "Entity2", Email = "e2@test.com" });

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => table.TriggerFlushAsync());

        // Table state should be preserved (not cleared) if flush failed
        table.RowCount.Should().Be(2);
    }

    #endregion

    #region Multiple Flush Scenarios

    /// <summary>
    /// Tests repeated flush cycles: insert, flush, insert again.
    /// Validates that flush cycle can be repeated without corruption.
    /// </summary>
    [Fact]
    public async Task MultipleFlushs_RepeatedCycles_Success()
    {
        // Arrange
        var table = CreateTable();
        var mockStrategy = new Mock<IFlushStrategy<TestEntity>>();
        mockStrategy.Setup(s => s.StrategyName).Returns("TestStrategy");
        mockStrategy.Setup(s => s.FlushAsync(It.IsAny<List<TestEntity>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<TestEntity> entities, CancellationToken _) => entities.Count);

        table.SetFlushStrategy(mockStrategy.Object);

        // Act & Assert - Multiple cycles
        // Cycle 1
        await table.InsertAsync(new TestEntity { Name = "Batch1Item1", Email = "b1i1@test.com" });
        var flush1 = await table.TriggerFlushAsync();
        flush1.Should().Be(1);
        table.RowCount.Should().Be(0);

        // Cycle 2
        for (int i = 0; i < 3; i++)
        {
            await table.InsertAsync(new TestEntity { Name = $"Batch2Item{i}", Email = $"b2i{i}@test.com" });
        }
        var flush2 = await table.TriggerFlushAsync();
        flush2.Should().Be(3);
        table.RowCount.Should().Be(0);

        // Cycle 3
        for (int i = 0; i < 5; i++)
        {
            await table.InsertAsync(new TestEntity { Name = $"Batch3Item{i}", Email = $"b3i{i}@test.com" });
        }
        var flush3 = await table.TriggerFlushAsync();
        flush3.Should().Be(5);
        table.RowCount.Should().Be(0);
    }

    #endregion
}

/// <summary>
/// Simple test entity for in-memory table tests.
/// </summary>
public class TestEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Age { get; set; }
}
