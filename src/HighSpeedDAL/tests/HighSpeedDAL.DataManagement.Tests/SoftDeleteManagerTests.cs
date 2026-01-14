using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HighSpeedDAL.DataManagement.SoftDelete;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HighSpeedDAL.DataManagement.Tests
{
    /// <summary>
    /// Comprehensive test suite for the SoftDeleteManager component.
    /// </summary>
    public class SoftDeleteManagerTests : IDisposable
    {
        private readonly Mock<ILogger<SoftDeleteManager>> _loggerMock;
        private readonly string _connectionString;
        private readonly string _dbPath;
        private readonly SoftDeleteManager _softDeleteManager;

        public SoftDeleteManagerTests()
        {
            _loggerMock = new Mock<ILogger<SoftDeleteManager>>();
            
            _dbPath = Path.Combine(Path.GetTempPath(), $"test_softdelete_{Guid.NewGuid()}.db");
            _connectionString = $"Data Source={_dbPath}";
            
            _softDeleteManager = new SoftDeleteManager(
                _loggerMock.Object,
                _connectionString,
                isSqlServer: false);
            
            InitializeDatabaseAsync().GetAwaiter().GetResult();
        }

        public void Dispose()
        {
            try
            {
                if (File.Exists(_dbPath))
                {
                    File.Delete(_dbPath);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        #region Attribute and Configuration Tests

        [Fact]
        public void GetSoftDeleteAttribute_ForSoftDeleteEntity_ReturnsAttribute()
        {
            // Act
            SoftDeleteAttribute? attribute = _softDeleteManager.GetSoftDeleteAttribute<SoftDeleteProduct>();

            // Assert
            Assert.NotNull(attribute);
            Assert.Equal(30, attribute!.RetentionDays);
        }

        [Fact]
        public void GetSoftDeleteAttribute_ForNonSoftDeleteEntity_ReturnsNull()
        {
            // Act
            SoftDeleteAttribute? attribute = _softDeleteManager.GetSoftDeleteAttribute<NonSoftDeleteProduct>();

            // Assert
            Assert.Null(attribute);
        }

        [Fact]
        public void IsSoftDeleteEnabled_ForSoftDeleteEntity_ReturnsTrue()
        {
            // Act
            bool isEnabled = _softDeleteManager.IsSoftDeleteEnabled<SoftDeleteProduct>();

            // Assert
            Assert.True(isEnabled);
        }

        [Fact]
        public void IsSoftDeleteEnabled_ForNonSoftDeleteEntity_ReturnsFalse()
        {
            // Act
            bool isEnabled = _softDeleteManager.IsSoftDeleteEnabled<NonSoftDeleteProduct>();

            // Assert
            Assert.False(isEnabled);
        }

        [Fact]
        public void ValidateEntity_WithValidEntity_DoesNotThrow()
        {
            // Act & Assert - should not throw
            _softDeleteManager.ValidateEntity<SoftDeleteProduct>();
        }

        #endregion

        #region Soft Delete Tests

        [Fact]
        public async Task SoftDeleteAsync_WithValidId_MarkAsDeleted()
        {
            // Arrange
            await InsertTestProductAsync(1, "Product 1");

            // Act
            SoftDeleteResult result = await _softDeleteManager.SoftDeleteAsync<SoftDeleteProduct>(
                1, false, "TestUser");

            // Assert
            Assert.True(result.Success);
            Assert.Equal(1, result.EntitiesDeleted);
            Assert.Equal(0, result.RelatedEntitiesDeleted);
            Assert.Equal("TestUser", result.DeletedBy);

            // Verify entity is marked as deleted
            bool isDeleted = await _softDeleteManager.IsSoftDeletedAsync<SoftDeleteProduct>(1);
            Assert.True(isDeleted);
        }

        [Fact]
        public async Task SoftDeleteAsync_WithNonExistentId_ReturnsFailure()
        {
            // Act
            SoftDeleteResult result = await _softDeleteManager.SoftDeleteAsync<SoftDeleteProduct>(
                999, false, "TestUser");

            // Assert
            Assert.False(result.Success);
            Assert.Contains("not found", result.ErrorMessage ?? "");
        }

        [Fact]
        public async Task SoftDeleteAsync_WithAlreadyDeletedEntity_ReturnsFailure()
        {
            // Arrange
            await InsertTestProductAsync(1, "Product 1");
            await _softDeleteManager.SoftDeleteAsync<SoftDeleteProduct>(1, false, "TestUser");

            // Act - try to delete again
            SoftDeleteResult result = await _softDeleteManager.SoftDeleteAsync<SoftDeleteProduct>(
                1, false, "TestUser");

            // Assert
            Assert.False(result.Success);
        }

        [Fact]
        public async Task SoftDeleteManyAsync_WithMultipleIds_DeletesAll()
        {
            // Arrange
            await InsertTestProductAsync(1, "Product 1");
            await InsertTestProductAsync(2, "Product 2");
            await InsertTestProductAsync(3, "Product 3");

            List<object> ids = new List<object> { 1, 2, 3 };

            // Act
            SoftDeleteResult result = await _softDeleteManager.SoftDeleteManyAsync<SoftDeleteProduct>(
                ids, false, "TestUser");

            // Assert
            Assert.True(result.Success);
            Assert.Equal(3, result.EntitiesDeleted);

            // Verify all are deleted
            Assert.True(await _softDeleteManager.IsSoftDeletedAsync<SoftDeleteProduct>(1));
            Assert.True(await _softDeleteManager.IsSoftDeletedAsync<SoftDeleteProduct>(2));
            Assert.True(await _softDeleteManager.IsSoftDeletedAsync<SoftDeleteProduct>(3));
        }

        #endregion

        #region Recovery Tests

        [Fact]
        public async Task RecoverAsync_WithSoftDeletedEntity_RestoresEntity()
        {
            // Arrange
            await InsertTestProductAsync(1, "Product 1");
            await _softDeleteManager.SoftDeleteAsync<SoftDeleteProduct>(1, false, "TestUser");

            // Verify it's deleted
            Assert.True(await _softDeleteManager.IsSoftDeletedAsync<SoftDeleteProduct>(1));

            // Act
            bool recovered = await _softDeleteManager.RecoverAsync<SoftDeleteProduct>(1);

            // Assert
            Assert.True(recovered);
            Assert.False(await _softDeleteManager.IsSoftDeletedAsync<SoftDeleteProduct>(1));
        }

        [Fact]
        public async Task RecoverAsync_WithNonDeletedEntity_ReturnsFalse()
        {
            // Arrange
            await InsertTestProductAsync(1, "Product 1");

            // Act - try to recover non-deleted entity
            bool recovered = await _softDeleteManager.RecoverAsync<SoftDeleteProduct>(1);

            // Assert
            Assert.False(recovered);
        }

        [Fact]
        public async Task RecoverManyAsync_WithMultipleEntities_RecoverAll()
        {
            // Arrange
            await InsertTestProductAsync(1, "Product 1");
            await InsertTestProductAsync(2, "Product 2");
            await _softDeleteManager.SoftDeleteAsync<SoftDeleteProduct>(1, false, "TestUser");
            await _softDeleteManager.SoftDeleteAsync<SoftDeleteProduct>(2, false, "TestUser");

            List<object> ids = new List<object> { 1, 2 };

            // Act
            int recovered = await _softDeleteManager.RecoverManyAsync<SoftDeleteProduct>(ids);

            // Assert
            Assert.Equal(2, recovered);
            Assert.False(await _softDeleteManager.IsSoftDeletedAsync<SoftDeleteProduct>(1));
            Assert.False(await _softDeleteManager.IsSoftDeletedAsync<SoftDeleteProduct>(2));
        }

        #endregion

        #region Purge Tests

        [Fact]
        public async Task PurgeAsync_WithSoftDeletedEntity_PermanentlyDeletes()
        {
            // Arrange
            await InsertTestProductAsync(1, "Product 1");
            await _softDeleteManager.SoftDeleteAsync<SoftDeleteProduct>(1, false, "TestUser");

            // Act
            bool purged = await _softDeleteManager.PurgeAsync<SoftDeleteProduct>(1);

            // Assert
            Assert.True(purged);

            // Verify entity is gone completely
            int count = await GetTotalProductCountAsync();
            Assert.Equal(0, count);
        }

        [Fact]
        public async Task PurgeAsync_WithNonDeletedEntity_DoesNotDelete()
        {
            // Arrange
            await InsertTestProductAsync(1, "Product 1");

            // Act - try to purge non-deleted entity
            bool purged = await _softDeleteManager.PurgeAsync<SoftDeleteProduct>(1);

            // Assert
            Assert.False(purged);

            // Entity should still exist
            int count = await GetTotalProductCountAsync();
            Assert.Equal(1, count);
        }

        [Fact]
        public async Task PurgeExpiredAsync_WithExpiredRecords_DeletesOnlyExpired()
        {
            // Arrange - insert products with old deleted dates
            await InsertTestProductAsync(1, "Product 1");
            await InsertTestProductAsync(2, "Product 2");
            await InsertTestProductAsync(3, "Product 3");

            // Soft delete and manually set old deleted date
            await _softDeleteManager.SoftDeleteAsync<SoftDeleteProduct>(1, false, "TestUser");
            await UpdateDeletedAtAsync(1, DateTime.UtcNow.AddDays(-35)); // Older than retention

            await _softDeleteManager.SoftDeleteAsync<SoftDeleteProduct>(2, false, "TestUser");
            await UpdateDeletedAtAsync(2, DateTime.UtcNow.AddDays(-20)); // Within retention

            // Act
            int purged = await _softDeleteManager.PurgeExpiredAsync<SoftDeleteProduct>(
                olderThanDays: 30);

            // Assert
            Assert.Equal(1, purged); // Only the 35-day old record

            // Verify product 2 still exists (soft deleted)
            Assert.True(await _softDeleteManager.IsSoftDeletedAsync<SoftDeleteProduct>(2));
        }

        #endregion

        #region Query Tests

        [Fact]
        public async Task GetSoftDeletedAsync_ReturnsOnlySoftDeleted()
        {
            // Arrange
            await InsertTestProductAsync(1, "Product 1");
            await InsertTestProductAsync(2, "Product 2");
            await InsertTestProductAsync(3, "Product 3");

            await _softDeleteManager.SoftDeleteAsync<SoftDeleteProduct>(1, false, "TestUser");
            await _softDeleteManager.SoftDeleteAsync<SoftDeleteProduct>(2, false, "TestUser");

            // Act
            List<SoftDeleteProduct> deleted = await _softDeleteManager.GetSoftDeletedAsync<SoftDeleteProduct>();

            // Assert
            Assert.Equal(2, deleted.Count);
        }

        [Fact]
        public async Task GetSoftDeletedInRangeAsync_ReturnsOnlyInRange()
        {
            // Arrange
            DateTime startDate = DateTime.UtcNow.AddDays(-10);
            DateTime endDate = DateTime.UtcNow;

            await InsertTestProductAsync(1, "Product 1");
            await InsertTestProductAsync(2, "Product 2");

            await _softDeleteManager.SoftDeleteAsync<SoftDeleteProduct>(1, false, "TestUser");
            await UpdateDeletedAtAsync(1, DateTime.UtcNow.AddDays(-5)); // In range

            await _softDeleteManager.SoftDeleteAsync<SoftDeleteProduct>(2, false, "TestUser");
            await UpdateDeletedAtAsync(2, DateTime.UtcNow.AddDays(-15)); // Out of range

            // Act
            List<SoftDeleteProduct> inRange = await _softDeleteManager.GetSoftDeletedInRangeAsync<SoftDeleteProduct>(
                startDate, endDate);

            // Assert
            Assert.Equal(1, inRange.Count);
            Assert.Equal(1, inRange[0].Id);
        }

        [Fact]
        public async Task IsSoftDeletedAsync_WithDeletedEntity_ReturnsTrue()
        {
            // Arrange
            await InsertTestProductAsync(1, "Product 1");
            await _softDeleteManager.SoftDeleteAsync<SoftDeleteProduct>(1, false, "TestUser");

            // Act
            bool isDeleted = await _softDeleteManager.IsSoftDeletedAsync<SoftDeleteProduct>(1);

            // Assert
            Assert.True(isDeleted);
        }

        [Fact]
        public async Task IsSoftDeletedAsync_WithNonDeletedEntity_ReturnsFalse()
        {
            // Arrange
            await InsertTestProductAsync(1, "Product 1");

            // Act
            bool isDeleted = await _softDeleteManager.IsSoftDeletedAsync<SoftDeleteProduct>(1);

            // Assert
            Assert.False(isDeleted);
        }

        [Fact]
        public async Task GetSoftDeletedCountAsync_ReturnsCorrectCount()
        {
            // Arrange
            await InsertTestProductAsync(1, "Product 1");
            await InsertTestProductAsync(2, "Product 2");
            await InsertTestProductAsync(3, "Product 3");

            await _softDeleteManager.SoftDeleteAsync<SoftDeleteProduct>(1, false, "TestUser");
            await _softDeleteManager.SoftDeleteAsync<SoftDeleteProduct>(2, false, "TestUser");

            // Act
            int count = await _softDeleteManager.GetSoftDeletedCountAsync<SoftDeleteProduct>();

            // Assert
            Assert.Equal(2, count);
        }

        #endregion

        #region Test Helper Methods

        private async Task InitializeDatabaseAsync()
        {
            using (SqliteConnection connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        CREATE TABLE SoftDeleteProducts (
                            Id INTEGER PRIMARY KEY,
                            Name TEXT NOT NULL,
                            IsDeleted INTEGER NOT NULL DEFAULT 0,
                            DeletedAt TEXT NULL,
                            DeletedBy TEXT NULL
                        )";
                    await command.ExecuteNonQueryAsync();
                }

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        CREATE TABLE NonSoftDeleteProducts (
                            Id INTEGER PRIMARY KEY,
                            Name TEXT NOT NULL
                        )";
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task InsertTestProductAsync(int id, string name)
        {
            using (SqliteConnection connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        INSERT INTO SoftDeleteProducts (Id, Name, IsDeleted)
                        VALUES (@Id, @Name, 0)";
                    
                    command.Parameters.AddWithValue("@Id", id);
                    command.Parameters.AddWithValue("@Name", name);

                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task UpdateDeletedAtAsync(int id, DateTime deletedAt)
        {
            using (SqliteConnection connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        UPDATE SoftDeleteProducts
                        SET DeletedAt = @DeletedAt
                        WHERE Id = @Id";
                    
                    command.Parameters.AddWithValue("@Id", id);
                    command.Parameters.AddWithValue("@DeletedAt", deletedAt.ToString("o"));

                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task<int> GetTotalProductCountAsync()
        {
            using (SqliteConnection connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT COUNT(*) FROM SoftDeleteProducts";
                    object? result = await command.ExecuteScalarAsync();
                    return Convert.ToInt32(result);
                }
            }
        }

        #endregion

        #region Test Entity Classes

        [SoftDelete(RetentionDays = 30)]
        public class SoftDeleteProduct
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public bool IsDeleted { get; set; }
            public DateTime? DeletedAt { get; set; }
            public string? DeletedBy { get; set; }
        }

        public class NonSoftDeleteProduct
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        #endregion
    }
}
