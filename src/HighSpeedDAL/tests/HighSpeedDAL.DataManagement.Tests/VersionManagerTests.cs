using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HighSpeedDAL.DataManagement.Versioning;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HighSpeedDAL.DataManagement.Tests
{
    /// <summary>
    /// Comprehensive test suite for the VersionManager component.
    /// Tests all versioning strategies, concurrency detection, and temporal queries.
    /// </summary>
    public class VersionManagerTests : IDisposable
    {
        private readonly Mock<ILogger<VersionManager>> _loggerMock;
        private readonly string _connectionString;
        private readonly string _dbPath;
        private readonly VersionManager _versionManager;

        public VersionManagerTests()
        {
            _loggerMock = new Mock<ILogger<VersionManager>>();
            
            // Use SQLite for testing (easier to set up than SQL Server)
            _dbPath = Path.Combine(Path.GetTempPath(), $"test_version_{Guid.NewGuid()}.db");
            _connectionString = $"Data Source={_dbPath}";
            
            _versionManager = new VersionManager(_loggerMock.Object, _connectionString, isSqlServer: false);
            
            // Create test tables
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
        public void GetVersionedAttribute_ForVersionedEntity_ReturnsAttribute()
        {
            // Act
            VersionedAttribute? attribute = _versionManager.GetVersionedAttribute<VersionedProduct>();

            // Assert
            Assert.NotNull(attribute);
            Assert.Equal(VersionStrategy.Integer, attribute!.Strategy);
        }

        [Fact]
        public void GetVersionedAttribute_ForNonVersionedEntity_ReturnsNull()
        {
            // Act
            VersionedAttribute? attribute = _versionManager.GetVersionedAttribute<NonVersionedProduct>();

            // Assert
            Assert.Null(attribute);
        }

        [Fact]
        public void IsVersioned_ForVersionedEntity_ReturnsTrue()
        {
            // Act
            bool isVersioned = _versionManager.IsVersioned<VersionedProduct>();

            // Assert
            Assert.True(isVersioned);
        }

        [Fact]
        public void IsVersioned_ForNonVersionedEntity_ReturnsFalse()
        {
            // Act
            bool isVersioned = _versionManager.IsVersioned<NonVersionedProduct>();

            // Assert
            Assert.False(isVersioned);
        }

        #endregion

        #region Version Info Tests

        [Fact]
        public void GetVersionInfo_WithValidEntity_ReturnsVersionInfo()
        {
            // Arrange
            VersionedProduct product = new VersionedProduct
            {
                Id = 1,
                Name = "Test Product",
                Version = 5
            };

            // Act
            VersionInfo? versionInfo = _versionManager.GetVersionInfo(product);

            // Assert
            Assert.NotNull(versionInfo);
            Assert.Equal(VersionStrategy.Integer, versionInfo!.Strategy);
            Assert.Equal(5, versionInfo.IntegerValue);
            Assert.True(versionInfo.HasValue);
        }

        [Fact]
        public void GetVersionInfo_WithNullEntity_ThrowsException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                _versionManager.GetVersionInfo<VersionedProduct>(null!));
        }

        [Fact]
        public void GetVersionInfo_ForNonVersionedEntity_ReturnsNull()
        {
            // Arrange
            NonVersionedProduct product = new NonVersionedProduct
            {
                Id = 1,
                Name = "Test Product"
            };

            // Act
            VersionInfo? versionInfo = _versionManager.GetVersionInfo(product);

            // Assert
            Assert.Null(versionInfo);
        }

        [Fact]
        public async Task GetVersionInfoByIdAsync_WithExistingEntity_ReturnsVersionInfo()
        {
            // Arrange
            await InsertTestProductAsync(1, "Product 1", 3);

            // Act
            VersionInfo? versionInfo = await _versionManager.GetVersionInfoByIdAsync<VersionedProduct>(
                1, CancellationToken.None);

            // Assert
            Assert.NotNull(versionInfo);
            Assert.Equal(3, versionInfo!.IntegerValue);
        }

        [Fact]
        public async Task GetVersionInfoByIdAsync_WithNonExistentEntity_ReturnsNull()
        {
            // Act
            VersionInfo? versionInfo = await _versionManager.GetVersionInfoByIdAsync<VersionedProduct>(
                999, CancellationToken.None);

            // Assert
            Assert.Null(versionInfo);
        }

        #endregion

        #region Version Increment Tests

        [Fact]
        public void IncrementVersion_WithIntegerStrategy_IncrementsValue()
        {
            // Arrange
            VersionedProduct product = new VersionedProduct
            {
                Id = 1,
                Name = "Test Product",
                Version = 5
            };

            // Act
            _versionManager.IncrementVersion(product);

            // Assert
            Assert.Equal(6, product.Version);
        }

        [Fact]
        public void IncrementVersion_WithNullVersion_SetsToOne()
        {
            // Arrange
            VersionedProduct product = new VersionedProduct
            {
                Id = 1,
                Name = "Test Product",
                Version = 0
            };

            // Act
            _versionManager.IncrementVersion(product);

            // Assert
            Assert.Equal(1, product.Version);
        }

        [Fact]
        public void IncrementVersion_WithTimestampStrategy_UpdatesTimestamp()
        {
            // Arrange
            DateTime before = DateTime.UtcNow;
            TimestampVersionedProduct product = new TimestampVersionedProduct
            {
                Id = 1,
                Name = "Test Product",
                LastModified = DateTime.UtcNow.AddHours(-1)
            };

            // Act
            _versionManager.IncrementVersion(product);
            DateTime after = DateTime.UtcNow;

            // Assert
            Assert.True(product.LastModified >= before);
            Assert.True(product.LastModified <= after);
        }

        [Fact]
        public void IncrementVersion_WithGuidStrategy_GeneratesNewGuid()
        {
            // Arrange
            Guid originalGuid = Guid.NewGuid();
            GuidVersionedProduct product = new GuidVersionedProduct
            {
                Id = 1,
                Name = "Test Product",
                VersionGuid = originalGuid
            };

            // Act
            _versionManager.IncrementVersion(product);

            // Assert
            Assert.NotEqual(originalGuid, product.VersionGuid);
            Assert.NotEqual(Guid.Empty, product.VersionGuid);
        }

        #endregion

        #region Version Validation Tests

        [Fact]
        public async Task ValidateVersionAsync_WithMatchingVersions_ReturnsTrue()
        {
            // Arrange
            await InsertTestProductAsync(1, "Product 1", 3);
            
            VersionedProduct product = new VersionedProduct
            {
                Id = 1,
                Name = "Product 1",
                Version = 3
            };

            // Act
            bool isValid = await _versionManager.ValidateVersionAsync(product, CancellationToken.None);

            // Assert
            Assert.True(isValid);
        }

        [Fact]
        public async Task ValidateVersionAsync_WithConflictingVersions_ThrowsException()
        {
            // Arrange
            await InsertTestProductAsync(1, "Product 1", 5);
            
            VersionedProduct product = new VersionedProduct
            {
                Id = 1,
                Name = "Product 1",
                Version = 3 // Old version
            };

            // Act & Assert
            await Assert.ThrowsAsync<VersionConflictException>(() =>
                _versionManager.ValidateVersionAsync(product, CancellationToken.None));
        }

        [Fact]
        public async Task ValidateVersionAsync_WithNewEntity_ReturnsTrue()
        {
            // Arrange
            VersionedProduct product = new VersionedProduct
            {
                Id = 0, // New entity
                Name = "New Product",
                Version = 1
            };

            // Act
            bool isValid = await _versionManager.ValidateVersionAsync(product, CancellationToken.None);

            // Assert
            Assert.True(isValid);
        }

        [Fact]
        public async Task ValidateVersionAsync_ForNonVersionedEntity_ReturnsTrue()
        {
            // Arrange
            NonVersionedProduct product = new NonVersionedProduct
            {
                Id = 1,
                Name = "Product 1"
            };

            // Act
            bool isValid = await _versionManager.ValidateVersionAsync(product, CancellationToken.None);

            // Assert
            Assert.True(isValid);
        }

        #endregion

        #region Update with Version Check Tests

        [Fact]
        public async Task UpdateWithVersionCheckAsync_WithValidVersion_Succeeds()
        {
            // Arrange
            await InsertTestProductAsync(1, "Product 1", 3);
            
            VersionedProduct product = new VersionedProduct
            {
                Id = 1,
                Name = "Updated Product",
                Version = 3
            };

            // Act
            bool result = await _versionManager.UpdateWithVersionCheckAsync(product, CancellationToken.None);

            // Assert
            Assert.True(result);
            Assert.Equal(4, product.Version); // Should be incremented
        }

        [Fact]
        public async Task UpdateWithVersionCheckAsync_WithInvalidVersion_ThrowsException()
        {
            // Arrange
            await InsertTestProductAsync(1, "Product 1", 5);
            
            VersionedProduct product = new VersionedProduct
            {
                Id = 1,
                Name = "Updated Product",
                Version = 3 // Old version
            };

            // Act & Assert
            await Assert.ThrowsAsync<VersionConflictException>(() =>
                _versionManager.UpdateWithVersionCheckAsync(product, CancellationToken.None));
        }

        [Fact]
        public async Task UpdateWithVersionCheckAsync_ForNonVersionedEntity_ThrowsException()
        {
            // Arrange
            NonVersionedProduct product = new NonVersionedProduct
            {
                Id = 1,
                Name = "Product 1"
            };

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _versionManager.UpdateWithVersionCheckAsync(product, CancellationToken.None));
        }

        [Fact]
        public async Task UpdateManyWithVersionCheckAsync_WithMultipleEntities_ReturnsResults()
        {
            // Arrange
            await InsertTestProductAsync(1, "Product 1", 3);
            await InsertTestProductAsync(2, "Product 2", 5);
            
            List<VersionedProduct> products = new List<VersionedProduct>
            {
                new VersionedProduct { Id = 1, Name = "Updated 1", Version = 3 }, // Valid
                new VersionedProduct { Id = 2, Name = "Updated 2", Version = 2 }  // Invalid
            };

            // Act
            Dictionary<VersionedProduct, bool> results = await _versionManager.UpdateManyWithVersionCheckAsync(
                products, CancellationToken.None);

            // Assert
            Assert.Equal(2, results.Count);
            Assert.True(results[products[0]]); // First update should succeed
            Assert.False(results[products[1]]); // Second update should fail due to version conflict
        }

        #endregion

        #region Version Comparison Tests

        [Fact]
        public void VersionsEqual_WithMatchingIntegers_ReturnsTrue()
        {
            // Act
            bool result = _versionManager.VersionsEqual(5, 5, VersionStrategy.Integer);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void VersionsEqual_WithDifferentIntegers_ReturnsFalse()
        {
            // Act
            bool result = _versionManager.VersionsEqual(5, 7, VersionStrategy.Integer);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void VersionsEqual_WithMatchingGuids_ReturnsTrue()
        {
            // Arrange
            Guid guid = Guid.NewGuid();

            // Act
            bool result = _versionManager.VersionsEqual(guid, guid, VersionStrategy.Guid);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void VersionsEqual_WithDifferentGuids_ReturnsFalse()
        {
            // Act
            bool result = _versionManager.VersionsEqual(Guid.NewGuid(), Guid.NewGuid(), VersionStrategy.Guid);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void VersionsEqual_WithMatchingByteArrays_ReturnsTrue()
        {
            // Arrange
            byte[] version1 = new byte[] { 1, 2, 3, 4 };
            byte[] version2 = new byte[] { 1, 2, 3, 4 };

            // Act
            bool result = _versionManager.VersionsEqual(version1, version2, VersionStrategy.RowVersion);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void VersionsEqual_WithDifferentByteArrays_ReturnsFalse()
        {
            // Arrange
            byte[] version1 = new byte[] { 1, 2, 3, 4 };
            byte[] version2 = new byte[] { 1, 2, 3, 5 };

            // Act
            bool result = _versionManager.VersionsEqual(version1, version2, VersionStrategy.RowVersion);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void VersionsEqual_WithBothNull_ReturnsTrue()
        {
            // Act
            bool result = _versionManager.VersionsEqual(null, null, VersionStrategy.Integer);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void VersionsEqual_WithOneNull_ReturnsFalse()
        {
            // Act
            bool result = _versionManager.VersionsEqual(5, null, VersionStrategy.Integer);

            // Assert
            Assert.False(result);
        }

        #endregion

        #region VersionInfo Equality Tests

        [Fact]
        public void VersionInfo_EqualsVersion_WithMatchingVersions_ReturnsTrue()
        {
            // Arrange
            VersionInfo version1 = new VersionInfo
            {
                Strategy = VersionStrategy.Integer,
                IntegerValue = 5
            };

            VersionInfo version2 = new VersionInfo
            {
                Strategy = VersionStrategy.Integer,
                IntegerValue = 5
            };

            // Act
            bool result = version1.EqualsVersion(version2);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void VersionInfo_EqualsVersion_WithDifferentVersions_ReturnsFalse()
        {
            // Arrange
            VersionInfo version1 = new VersionInfo
            {
                Strategy = VersionStrategy.Integer,
                IntegerValue = 5
            };

            VersionInfo version2 = new VersionInfo
            {
                Strategy = VersionStrategy.Integer,
                IntegerValue = 7
            };

            // Act
            bool result = version1.EqualsVersion(version2);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void VersionInfo_EqualsVersion_WithDifferentStrategies_ReturnsFalse()
        {
            // Arrange
            VersionInfo version1 = new VersionInfo
            {
                Strategy = VersionStrategy.Integer,
                IntegerValue = 5
            };

            VersionInfo version2 = new VersionInfo
            {
                Strategy = VersionStrategy.Guid,
                GuidValue = Guid.NewGuid()
            };

            // Act
            bool result = version1.EqualsVersion(version2);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void VersionInfo_HasValue_WithValidInteger_ReturnsTrue()
        {
            // Arrange
            VersionInfo versionInfo = new VersionInfo
            {
                Strategy = VersionStrategy.Integer,
                IntegerValue = 5
            };

            // Assert
            Assert.True(versionInfo.HasValue);
        }

        [Fact]
        public void VersionInfo_HasValue_WithNullInteger_ReturnsFalse()
        {
            // Arrange
            VersionInfo versionInfo = new VersionInfo
            {
                Strategy = VersionStrategy.Integer,
                IntegerValue = null
            };

            // Assert
            Assert.False(versionInfo.HasValue);
        }

        #endregion

        #region Concurrency Exception Tests

        [Fact]
        public void VersionConflictException_CreatesProperMessage()
        {
            // Arrange & Act
            VersionConflictException exception = new VersionConflictException(
                typeof(VersionedProduct),
                1,
                expectedVersion: 3,
                actualVersion: 5);

            // Assert
            Assert.Contains("VersionedProduct", exception.Message);
            Assert.Contains("ID 1", exception.Message);
            Assert.Contains("Expected version: 3", exception.Message);
            Assert.Contains("Actual version: 5", exception.Message);
            Assert.Equal(typeof(VersionedProduct), exception.EntityType);
            Assert.Equal(1, exception.EntityId);
            Assert.Equal(3, exception.ExpectedVersion);
            Assert.Equal(5, exception.ActualVersion);
        }

        [Fact]
        public void VersionConflictException_WithByteArrayVersions_FormatsCorrectly()
        {
            // Arrange & Act
            VersionConflictException exception = new VersionConflictException(
                typeof(VersionedProduct),
                1,
                expectedVersion: new byte[] { 1, 2, 3 },
                actualVersion: new byte[] { 4, 5, 6 });

            // Assert
            Assert.Contains("01-02-03", exception.Message);
            Assert.Contains("04-05-06", exception.Message);
        }

        #endregion

        #region Test Helper Methods

        private async Task InitializeDatabaseAsync()
        {
            using (SqliteConnection connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                // Create VersionedProducts table
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        CREATE TABLE VersionedProducts (
                            Id INTEGER PRIMARY KEY,
                            Name TEXT NOT NULL,
                            Version INTEGER NOT NULL DEFAULT 1
                        )";
                    await command.ExecuteNonQueryAsync();
                }

                // Create TimestampVersionedProducts table
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        CREATE TABLE TimestampVersionedProducts (
                            Id INTEGER PRIMARY KEY,
                            Name TEXT NOT NULL,
                            LastModified TEXT NOT NULL
                        )";
                    await command.ExecuteNonQueryAsync();
                }

                // Create GuidVersionedProducts table
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        CREATE TABLE GuidVersionedProducts (
                            Id INTEGER PRIMARY KEY,
                            Name TEXT NOT NULL,
                            VersionGuid TEXT NOT NULL
                        )";
                    await command.ExecuteNonQueryAsync();
                }

                // Create NonVersionedProducts table
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        CREATE TABLE NonVersionedProducts (
                            Id INTEGER PRIMARY KEY,
                            Name TEXT NOT NULL
                        )";
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task InsertTestProductAsync(int id, string name, int version)
        {
            using (SqliteConnection connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        INSERT INTO VersionedProducts (Id, Name, Version)
                        VALUES (@Id, @Name, @Version)";
                    
                    command.Parameters.AddWithValue("@Id", id);
                    command.Parameters.AddWithValue("@Name", name);
                    command.Parameters.AddWithValue("@Version", version);

                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        #endregion

        #region Test Entity Classes

        [Versioned(Strategy = VersionStrategy.Integer, PropertyName = "Version")]
        public class VersionedProduct
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public int Version { get; set; }
        }

        [Versioned(Strategy = VersionStrategy.Timestamp, PropertyName = "LastModified")]
        public class TimestampVersionedProduct
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public DateTime LastModified { get; set; }
        }

        [Versioned(Strategy = VersionStrategy.Guid, PropertyName = "VersionGuid")]
        public class GuidVersionedProduct
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public Guid VersionGuid { get; set; }
        }

        public class NonVersionedProduct
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        #endregion
    }
}
