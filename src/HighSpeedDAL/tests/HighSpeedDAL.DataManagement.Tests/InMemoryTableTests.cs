using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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
/// Comprehensive test suite for InMemoryTable functionality.
/// Tests cover CRUD operations, indexes, constraints, WHERE clauses, and data validation.
/// </summary>
public class InMemoryTableTests : IDisposable
{
    private readonly Mock<ILogger<InMemoryTable<TestEntity>>> _loggerMock;
    private readonly InMemoryTableAttribute _defaultConfig;
    private InMemoryTable<TestEntity>? _table;

    public InMemoryTableTests()
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

    #region Insert Tests

    [Fact]
    public async Task Insert_SingleEntity_Success()
    {
        // Arrange
        var table = CreateTable();
        var entity = new TestEntity { Name = "Test", Email = "test@example.com", Age = 25 };

        // Act
        var id = await table.InsertAsync(entity);

        // Assert
        ((int)id).Should().BeGreaterThan(0);
        entity.Id.Should().BeGreaterThan(0);
        table.RowCount.Should().Be(1);
    }

    [Fact]
    public async Task Insert_MultipleEntities_AutoIncrementIds()
    {
        // Arrange
        var table = CreateTable();

        // Act
        var id1 = await table.InsertAsync(new TestEntity { Name = "Entity1", Email = "e1@test.com" });
        var id2 = await table.InsertAsync(new TestEntity { Name = "Entity2", Email = "e2@test.com" });
        var id3 = await table.InsertAsync(new TestEntity { Name = "Entity3", Email = "e3@test.com" });

        // Assert - IDs should be sequential
        ((int)id2).Should().Be((int)id1 + 1);
        ((int)id3).Should().Be((int)id2 + 1);
        table.RowCount.Should().Be(3);
    }

    [Fact]
    public async Task Insert_NullEntity_ThrowsArgumentNullException()
    {
        // Arrange
        var table = CreateTable();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => table.InsertAsync(null!));
    }

    [Fact]
    public async Task BulkInsert_MultipleEntities_Success()
    {
        // Arrange
        var table = CreateTable();
        var entities = Enumerable.Range(1, 100)
            .Select(i => new TestEntity { Name = $"Entity{i}", Email = $"e{i}@test.com", Age = i })
            .ToList();

        // Act
        var count = await table.BulkInsertAsync(entities);

        // Assert
        count.Should().Be(100);
        table.RowCount.Should().Be(100);
    }

    #endregion

    #region Select Tests

    [Fact]
    public async Task Select_All_ReturnsAllEntities()
    {
        // Arrange
        var table = CreateTable();
        await table.InsertAsync(new TestEntity { Name = "Entity1", Email = "e1@test.com" });
        await table.InsertAsync(new TestEntity { Name = "Entity2", Email = "e2@test.com" });
        await table.InsertAsync(new TestEntity { Name = "Entity3", Email = "e3@test.com" });

        // Act
        var results = table.Select().ToList();

        // Assert
        results.Should().HaveCount(3);
    }

    [Fact]
    public async Task Select_ByPrimaryKey_ReturnsCorrectEntity()
    {
        // Arrange
        var table = CreateTable();
        var entity1 = new TestEntity { Name = "Entity1", Email = "e1@test.com" };
        var entity2 = new TestEntity { Name = "Entity2", Email = "e2@test.com" };
        await table.InsertAsync(entity1);
        await table.InsertAsync(entity2);

        // Act - use the actual ID from the inserted entity
        var entity = table.GetById(entity2.Id);

        // Assert
        entity.Should().NotBeNull();
        entity!.Name.Should().Be("Entity2");
    }

    [Fact]
    public async Task Select_ByPrimaryKey_NotFound_ReturnsNull()
    {
        // Arrange
        var table = CreateTable();
        await table.InsertAsync(new TestEntity { Name = "Entity1", Email = "e1@test.com" });

        // Act - use an ID that definitely doesn't exist
        var entity = table.GetById(999999);

        // Assert
        entity.Should().BeNull();
    }

    [Fact]
    public async Task Select_WithPredicate_FiltersCorrectly()
    {
        // Arrange
        var table = CreateTable();
        await table.InsertAsync(new TestEntity { Name = "Alice", Email = "alice@test.com", Age = 25 });
        await table.InsertAsync(new TestEntity { Name = "Bob", Email = "bob@test.com", Age = 30 });
        await table.InsertAsync(new TestEntity { Name = "Charlie", Email = "charlie@test.com", Age = 35 });

        // Act
        var results = table.Select(e => e.Age >= 30).ToList();

        // Assert
        results.Should().HaveCount(2);
        results.Select(e => e.Name).Should().Contain(new[] { "Bob", "Charlie" });
    }

    [Fact]
    public async Task Count_ReturnsCorrectCount()
    {
        // Arrange
        var table = CreateTable();
        await table.InsertAsync(new TestEntity { Name = "Entity1", Email = "e1@test.com" });
        await table.InsertAsync(new TestEntity { Name = "Entity2", Email = "e2@test.com" });
        await table.InsertAsync(new TestEntity { Name = "Entity3", Email = "e3@test.com" });

        // Act & Assert
        table.RowCount.Should().Be(3);
    }

    [Fact]
    public async Task Exists_ById_ReturnsCorrectResult()
    {
        // Arrange
        var table = CreateTable();
        var entity = new TestEntity { Name = "Entity1", Email = "e1@test.com" };
        await table.InsertAsync(entity);

        // Act & Assert
        table.ExistsById(entity.Id).Should().BeTrue();
        table.ExistsById(999999).Should().BeFalse();
    }

    #endregion

    #region Update Tests

    [Fact]
    public async Task Update_ExistingEntity_Success()
    {
        // Arrange
        var table = CreateTable();
        var entity = new TestEntity { Name = "Original", Email = "original@test.com", Age = 25 };
        await table.InsertAsync(entity);
        var insertedId = entity.Id;

        // Act
        entity.Name = "Updated";
        entity.Age = 30;
        var affected = await table.UpdateAsync(entity);

        // Assert
        affected.Should().Be(1);
        var updated = table.GetById(insertedId);
        updated.Should().NotBeNull();
        updated!.Name.Should().Be("Updated");
        updated.Age.Should().Be(30);
    }

    [Fact]
    public async Task Update_NonExistingEntity_ReturnsZero()
    {
        // Arrange
        var table = CreateTable();
        var entity = new TestEntity { Id = 999, Name = "NonExistent", Email = "ne@test.com" };

        // Act
        var affected = await table.UpdateAsync(entity);

        // Assert
        affected.Should().Be(0);
    }

    [Fact]
    public async Task Update_WithWhereClause_UpdatesMatchingRows()
    {
        // Arrange
        var table = CreateTable();
        await table.InsertAsync(new TestEntity { Name = "Alice", Email = "alice@test.com", Age = 25 });
        await table.InsertAsync(new TestEntity { Name = "Bob", Email = "bob@test.com", Age = 25 });
        await table.InsertAsync(new TestEntity { Name = "Charlie", Email = "charlie@test.com", Age = 35 });

        // Act
        var affected = await table.UpdateAsync(
            "Age = 25",
            new Dictionary<string, object?> { { "Age", 26 } });

        // Assert
        affected.Should().Be(2);
        table.Select(e => e.Age == 26).Should().HaveCount(2);
    }

    [Fact]
    public async Task BulkUpdate_MultipleEntities_Success()
    {
        // Arrange
        var table = CreateTable();
        var entities = new List<TestEntity>
        {
            new TestEntity { Name = "Entity1", Email = "e1@test.com" },
            new TestEntity { Name = "Entity2", Email = "e2@test.com" },
            new TestEntity { Name = "Entity3", Email = "e3@test.com" }
        };
        foreach (var e in entities)
        {
            await table.InsertAsync(e);
        }

        // Act
        entities[0].Name = "Updated1";
        entities[1].Name = "Updated2";
        var affected = await table.BulkUpdateAsync(entities.Take(2));

        // Assert
        affected.Should().Be(2);
        table.GetById(entities[0].Id)!.Name.Should().Be("Updated1");
        table.GetById(entities[1].Id)!.Name.Should().Be("Updated2");
        table.GetById(entities[2].Id)!.Name.Should().Be("Entity3");
    }

    #endregion

    #region Delete Tests

    [Fact]
    public async Task Delete_ByPrimaryKey_Success()
    {
        // Arrange
        var table = CreateTable();
        var e1 = new TestEntity { Name = "Entity1", Email = "e1@test.com" };
        var e2 = new TestEntity { Name = "Entity2", Email = "e2@test.com" };
        await table.InsertAsync(e1);
        await table.InsertAsync(e2);

        // Act
        var affected = await table.DeleteAsync(e1.Id);

        // Assert
        affected.Should().Be(1);
        table.RowCount.Should().Be(1);
        table.ExistsById(e1.Id).Should().BeFalse();
        table.ExistsById(e2.Id).Should().BeTrue();
    }

    [Fact]
    public async Task Delete_NonExisting_ReturnsZero()
    {
        // Arrange
        var table = CreateTable();
        await table.InsertAsync(new TestEntity { Name = "Entity1", Email = "e1@test.com" });

        // Act
        var affected = await table.DeleteAsync(999999);

        // Assert
        affected.Should().Be(0);
        table.RowCount.Should().Be(1);
    }

    [Fact]
    public async Task Delete_WithWhereClause_DeletesMatchingRows()
    {
        // Arrange
        var table = CreateTable();
        await table.InsertAsync(new TestEntity { Name = "Alice", Email = "alice@test.com", Age = 25 });
        await table.InsertAsync(new TestEntity { Name = "Bob", Email = "bob@test.com", Age = 30 });
        await table.InsertAsync(new TestEntity { Name = "Charlie", Email = "charlie@test.com", Age = 35 });

        // Act
        var affected = await table.DeleteAsync("Age >= 30");

        // Assert
        affected.Should().Be(2);
        table.RowCount.Should().Be(1);
        table.Select().First().Name.Should().Be("Alice");
    }

    [Fact]
    public async Task BulkDelete_MultipleIds_Success()
    {
        // Arrange
        var table = CreateTable();
        var e1 = new TestEntity { Name = "Entity1", Email = "e1@test.com" };
        var e2 = new TestEntity { Name = "Entity2", Email = "e2@test.com" };
        var e3 = new TestEntity { Name = "Entity3", Email = "e3@test.com" };
        await table.InsertAsync(e1);
        await table.InsertAsync(e2);
        await table.InsertAsync(e3);

        // Act
        var affected = await table.BulkDeleteAsync(new object[] { e1.Id, e3.Id });

        // Assert
        affected.Should().Be(2);
        table.RowCount.Should().Be(1);
        table.GetById(e2.Id).Should().NotBeNull();
    }

    #endregion

    #region WHERE Clause Tests

    [Fact]
    public async Task Select_WhereEquals_ReturnsMatchingRows()
    {
        // Arrange
        var table = CreateTable();
        await table.InsertAsync(new TestEntity { Name = "Alice", Email = "alice@test.com", Age = 25 });
        await table.InsertAsync(new TestEntity { Name = "Bob", Email = "bob@test.com", Age = 30 });

        // Act
        var results = table.Select("Name = 'Alice'").ToList();

        // Assert
        results.Should().HaveCount(1);
        results[0].Name.Should().Be("Alice");
    }

    [Fact]
    public async Task Select_WhereNotEquals_ReturnsMatchingRows()
    {
        // Arrange
        var table = CreateTable();
        await table.InsertAsync(new TestEntity { Name = "Alice", Email = "alice@test.com", Age = 25 });
        await table.InsertAsync(new TestEntity { Name = "Bob", Email = "bob@test.com", Age = 30 });

        // Act
        var results = table.Select("Name != 'Alice'").ToList();

        // Assert
        results.Should().HaveCount(1);
        results[0].Name.Should().Be("Bob");
    }

    [Fact]
    public async Task Select_WhereGreaterThan_ReturnsMatchingRows()
    {
        // Arrange
        var table = CreateTable();
        await table.InsertAsync(new TestEntity { Name = "Alice", Email = "alice@test.com", Age = 25 });
        await table.InsertAsync(new TestEntity { Name = "Bob", Email = "bob@test.com", Age = 30 });
        await table.InsertAsync(new TestEntity { Name = "Charlie", Email = "charlie@test.com", Age = 35 });

        // Act
        var results = table.Select("Age > 25").ToList();

        // Assert
        results.Should().HaveCount(2);
    }

    [Fact]
    public async Task Select_WhereLessThanOrEqual_ReturnsMatchingRows()
    {
        // Arrange
        var table = CreateTable();
        await table.InsertAsync(new TestEntity { Name = "Alice", Email = "alice@test.com", Age = 25 });
        await table.InsertAsync(new TestEntity { Name = "Bob", Email = "bob@test.com", Age = 30 });
        await table.InsertAsync(new TestEntity { Name = "Charlie", Email = "charlie@test.com", Age = 35 });

        // Act
        var results = table.Select("Age <= 30").ToList();

        // Assert
        results.Should().HaveCount(2);
    }

    [Fact]
    public async Task Select_WhereAnd_ReturnsMatchingRows()
    {
        // Arrange
        var table = CreateTable();
        await table.InsertAsync(new TestEntity { Name = "Alice", Email = "alice@test.com", Age = 25, IsActive = true });
        await table.InsertAsync(new TestEntity { Name = "Bob", Email = "bob@test.com", Age = 30, IsActive = false });
        await table.InsertAsync(new TestEntity { Name = "Charlie", Email = "charlie@test.com", Age = 25, IsActive = false });

        // Act
        var results = table.Select("Age = 25 AND IsActive = TRUE").ToList();

        // Assert
        results.Should().HaveCount(1);
        results[0].Name.Should().Be("Alice");
    }

    [Fact]
    public async Task Select_WhereOr_ReturnsMatchingRows()
    {
        // Arrange
        var table = CreateTable();
        await table.InsertAsync(new TestEntity { Name = "Alice", Email = "alice@test.com", Age = 25 });
        await table.InsertAsync(new TestEntity { Name = "Bob", Email = "bob@test.com", Age = 30 });
        await table.InsertAsync(new TestEntity { Name = "Charlie", Email = "charlie@test.com", Age = 35 });

        // Act
        var results = table.Select("Age = 25 OR Age = 35").ToList();

        // Assert
        results.Should().HaveCount(2);
        results.Select(e => e.Name).Should().Contain(new[] { "Alice", "Charlie" });
    }

    [Fact]
    public async Task Select_WhereLike_ReturnsMatchingRows()
    {
        // Arrange
        var table = CreateTable();
        await table.InsertAsync(new TestEntity { Name = "Alice", Email = "alice@example.com", Age = 25 });
        await table.InsertAsync(new TestEntity { Name = "Bob", Email = "bob@test.com", Age = 30 });
        await table.InsertAsync(new TestEntity { Name = "Charlie", Email = "charlie@example.com", Age = 35 });

        // Act
        var results = table.Select("Email LIKE '%@example.com'").ToList();

        // Assert
        results.Should().HaveCount(2);
    }

    [Fact]
    public async Task Select_WhereIn_ReturnsMatchingRows()
    {
        // Arrange
        var table = CreateTable();
        await table.InsertAsync(new TestEntity { Name = "Alice", Email = "alice@test.com", Age = 25 });
        await table.InsertAsync(new TestEntity { Name = "Bob", Email = "bob@test.com", Age = 30 });
        await table.InsertAsync(new TestEntity { Name = "Charlie", Email = "charlie@test.com", Age = 35 });

        // Act
        var results = table.Select("Age IN (25, 35)").ToList();

        // Assert
        results.Should().HaveCount(2);
    }

    [Fact]
    public async Task Select_WhereBetween_ReturnsMatchingRows()
    {
        // Arrange
        var table = CreateTable();
        await table.InsertAsync(new TestEntity { Name = "Alice", Email = "alice@test.com", Age = 20 });
        await table.InsertAsync(new TestEntity { Name = "Bob", Email = "bob@test.com", Age = 30 });
        await table.InsertAsync(new TestEntity { Name = "Charlie", Email = "charlie@test.com", Age = 40 });

        // Act
        var results = table.Select("Age BETWEEN 25 AND 35").ToList();

        // Assert
        results.Should().HaveCount(1);
        results[0].Name.Should().Be("Bob");
    }

    [Fact]
    public async Task Select_WhereIsNull_ReturnsMatchingRows()
    {
        // Arrange
        var table = CreateTable();
        await table.InsertAsync(new TestEntity { Name = "Alice", Email = "alice@test.com", Age = 25, NullableValue = "Value" });
        await table.InsertAsync(new TestEntity { Name = "Bob", Email = "bob@test.com", Age = 30, NullableValue = null });

        // Act
        var results = table.Select("NullableValue IS NULL").ToList();

        // Assert
        results.Should().HaveCount(1);
        results[0].Name.Should().Be("Bob");
    }

    [Fact]
    public async Task Select_WhereIsNotNull_ReturnsMatchingRows()
    {
        // Arrange
        var table = CreateTable();
        await table.InsertAsync(new TestEntity { Name = "Alice", Email = "alice@test.com", Age = 25, NullableValue = "Value" });
        await table.InsertAsync(new TestEntity { Name = "Bob", Email = "bob@test.com", Age = 30, NullableValue = null });

        // Act
        var results = table.Select("NullableValue IS NOT NULL").ToList();

        // Assert
        results.Should().HaveCount(1);
        results[0].Name.Should().Be("Alice");
    }

    [Fact]
    public async Task Select_WithParameter_ReturnsMatchingRows()
    {
        // Arrange
        var table = CreateTable();
        await table.InsertAsync(new TestEntity { Name = "Alice", Email = "alice@test.com", Age = 25 });
        await table.InsertAsync(new TestEntity { Name = "Bob", Email = "bob@test.com", Age = 30 });
        table.SetParameter("targetAge", 25);

        // Act
        var results = table.Select("Age = @targetAge").ToList();

        // Assert
        results.Should().HaveCount(1);
        results[0].Name.Should().Be("Alice");
    }

    [Fact]
    public async Task Count_WithWhereClause_ReturnsCorrectCount()
    {
        // Arrange
        var table = CreateTable();
        await table.InsertAsync(new TestEntity { Name = "Alice", Email = "alice@test.com", Age = 25 });
        await table.InsertAsync(new TestEntity { Name = "Bob", Email = "bob@test.com", Age = 30 });
        await table.InsertAsync(new TestEntity { Name = "Charlie", Email = "charlie@test.com", Age = 35 });

        // Act
        var count = table.CountWhere("Age >= 30");

        // Assert
        count.Should().Be(2);
    }

    [Fact]
    public async Task Exists_WithWhereClause_ReturnsCorrectResult()
    {
        // Arrange
        var table = CreateTable();
        await table.InsertAsync(new TestEntity { Name = "Alice", Email = "alice@test.com", Age = 25 });
        await table.InsertAsync(new TestEntity { Name = "Bob", Email = "bob@test.com", Age = 30 });

        // Act & Assert
        table.Exists("Name = 'Alice'").Should().BeTrue();
        table.Exists("Name = 'NotExists'").Should().BeFalse();
    }

    #endregion

    #region Clear and AcceptChanges Tests

    [Fact]
    public async Task Clear_RemovesAllData()
    {
        // Arrange
        var table = CreateTable();
        await table.InsertAsync(new TestEntity { Name = "Entity1", Email = "e1@test.com" });
        await table.InsertAsync(new TestEntity { Name = "Entity2", Email = "e2@test.com" });
        table.RowCount.Should().Be(2);

        // Act
        table.Clear();

        // Assert
        table.RowCount.Should().Be(0);
    }

    [Fact]
    public async Task AcceptChanges_ResetsRowStates()
    {
        // Arrange
        var table = CreateTable();
        await table.InsertAsync(new TestEntity { Name = "Entity1", Email = "e1@test.com" });
        await table.InsertAsync(new TestEntity { Name = "Entity2", Email = "e2@test.com" });

        // Act
        table.AcceptChanges();
        var pendingChanges = table.GetPendingChanges();

        // Assert
        pendingChanges.Should().BeEmpty();
    }

    [Fact]
    public async Task AcceptChanges_RemovesDeletedRows()
    {
        // Arrange
        var table = CreateTable();
        var e1 = new TestEntity { Name = "Entity1", Email = "e1@test.com" };
        var e2 = new TestEntity { Name = "Entity2", Email = "e2@test.com" };
        await table.InsertAsync(e1);
        await table.InsertAsync(e2);
        await table.DeleteAsync(e1.Id);

        // Before AcceptChanges, deleted row is still in TotalRowCount
        table.TotalRowCount.Should().Be(2);
        table.RowCount.Should().Be(1);

        // Act
        table.AcceptChanges();

        // Assert
        table.TotalRowCount.Should().Be(1);
        table.RowCount.Should().Be(1);
    }

    #endregion

    #region Operation Log Tests

    [Fact]
    public async Task OperationLog_TracksInserts()
    {
        // Arrange
        var config = new InMemoryTableAttribute { TrackOperations = true };
        var table = new InMemoryTable<TestEntity>(_loggerMock.Object, config);

        // Act
        await table.InsertAsync(new TestEntity { Name = "Entity1", Email = "e1@test.com" });
        await table.InsertAsync(new TestEntity { Name = "Entity2", Email = "e2@test.com" });

        // Assert
        var log = table.GetOperationLog();
        log.Should().HaveCount(2);
        log.All(o => o.Operation == OperationType.Insert).Should().BeTrue();

        table.Dispose();
    }

    [Fact]
    public async Task OperationLog_TracksUpdates()
    {
        // Arrange
        var config = new InMemoryTableAttribute { TrackOperations = true };
        var table = new InMemoryTable<TestEntity>(_loggerMock.Object, config);
        var entity = new TestEntity { Name = "Entity1", Email = "e1@test.com" };
        await table.InsertAsync(entity);

        // Act
        entity.Name = "Updated";
        await table.UpdateAsync(entity);

        // Assert
        var log = table.GetOperationLog();
        log.Should().HaveCount(2);
        log[0].Operation.Should().Be(OperationType.Insert);
        log[1].Operation.Should().Be(OperationType.Update);

        table.Dispose();
    }

    [Fact]
    public async Task OperationLog_TracksDeletes()
    {
        // Arrange
        var config = new InMemoryTableAttribute { TrackOperations = true };
        var table = new InMemoryTable<TestEntity>(_loggerMock.Object, config);
        var entity = new TestEntity { Name = "Entity1", Email = "e1@test.com" };
        await table.InsertAsync(entity);

        // Act
        await table.DeleteAsync(entity.Id);

        // Assert
        var log = table.GetOperationLog();
        log.Should().HaveCount(2);
        log[1].Operation.Should().Be(OperationType.Delete);

        table.Dispose();
    }

    [Fact]
    public async Task ClearOperationLog_ClearsLog()
    {
        // Arrange
        var config = new InMemoryTableAttribute { TrackOperations = true };
        var table = new InMemoryTable<TestEntity>(_loggerMock.Object, config);
        await table.InsertAsync(new TestEntity { Name = "Entity1", Email = "e1@test.com" });
        table.GetOperationLog().Should().NotBeEmpty();

        // Act
        table.ClearOperationLog();

        // Assert
        table.GetOperationLog().Should().BeEmpty();

        table.Dispose();
    }

    #endregion

    #region Performance Tests

    [Fact]
    public async Task BulkInsert_10000Rows_CompletesQuickly()
    {
        // Arrange
        var table = CreateTable();
        var entities = Enumerable.Range(1, 10000)
            .Select(i => new TestEntity 
            { 
                Name = $"Entity{i}", 
                Email = $"e{i}@test.com", 
                Age = i % 100 
            })
            .ToList();

        // Act
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await table.BulkInsertAsync(entities);
        sw.Stop();

        // Assert
        table.RowCount.Should().Be(10000);
        sw.ElapsedMilliseconds.Should().BeLessThan(5000); // Should complete in under 5 seconds
    }

    [Fact]
    public async Task Select_FromLargeTable_CompletesQuickly()
    {
        // Arrange
        var table = CreateTable();
        var entities = Enumerable.Range(1, 10000)
            .Select(i => new TestEntity 
            { 
                Name = $"Entity{i}", 
                Email = $"e{i}@test.com", 
                Age = i % 100 
            })
            .ToList();
        await table.BulkInsertAsync(entities);

        // Act
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var results = table.Select("Age > 50 AND Age < 75").ToList();
        sw.Stop();

        // Assert
        results.Should().HaveCountGreaterThan(0);
        sw.ElapsedMilliseconds.Should().BeLessThan(500); // Should complete in under 500ms
    }

    #endregion
}

#region Test Entity

/// <summary>
/// Test entity for InMemoryTable tests
/// </summary>
public class TestEntity
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
