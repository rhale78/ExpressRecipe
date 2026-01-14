using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using HighSpeedDAL.Core.Interfaces;
using Xunit;

namespace HighSpeedDAL.DataManagement.Tests
{
    // ============================================================================
    // TEST ENTITIES FOR EDGE CASE TESTING
    // ============================================================================

    /// <summary>
    /// Entity with nullable properties for testing null handling
    /// </summary>
    public class EntityWithNullables : IEntityCloneable<EntityWithNullables>
    {
        public int Id { get; set; }
        public string? NullableString { get; set; }
        public int? NullableInt { get; set; }
        public decimal? NullableDecimal { get; set; }
        public DateTime? NullableDateTime { get; set; }
        public bool? NullableBool { get; set; }

        public EntityWithNullables ShallowClone()
        {
            return new EntityWithNullables
            {
                Id = this.Id,
                NullableString = this.NullableString,
                NullableInt = this.NullableInt,
                NullableDecimal = this.NullableDecimal,
                NullableDateTime = this.NullableDateTime,
                NullableBool = this.NullableBool
            };
        }

        public EntityWithNullables DeepClone() => ShallowClone();
    }

    /// <summary>
    /// Entity with collections for testing deep cloning
    /// </summary>
    public class EntityWithCollections : IEntityCloneable<EntityWithCollections>
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new List<string>();
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
        public int[] Scores { get; set; } = Array.Empty<int>();

        public EntityWithCollections ShallowClone()
        {
            return new EntityWithCollections
            {
                Id = this.Id,
                Name = this.Name,
                Tags = this.Tags, // Shallow: reference copy
                Metadata = this.Metadata, // Shallow: reference copy
                Scores = this.Scores // Shallow: reference copy
            };
        }

        public EntityWithCollections DeepClone()
        {
            return new EntityWithCollections
            {
                Id = this.Id,
                Name = this.Name,
                Tags = this.Tags?.ToList() ?? new List<string>(),
                Metadata = this.Metadata != null ? new Dictionary<string, string>(this.Metadata) : new Dictionary<string, string>(),
                Scores = this.Scores?.ToArray() ?? Array.Empty<int>()
            };
        }
    }

    /// <summary>
    /// Large entity for performance testing
    /// </summary>
    public class LargeEntity : IEntityCloneable<LargeEntity>
    {
        public int Id { get; set; }
        public string Property01 { get; set; } = string.Empty;
        public string Property02 { get; set; } = string.Empty;
        public string Property03 { get; set; } = string.Empty;
        public string Property04 { get; set; } = string.Empty;
        public string Property05 { get; set; } = string.Empty;
        public string Property06 { get; set; } = string.Empty;
        public string Property07 { get; set; } = string.Empty;
        public string Property08 { get; set; } = string.Empty;
        public string Property09 { get; set; } = string.Empty;
        public string Property10 { get; set; } = string.Empty;
        public decimal Value01 { get; set; }
        public decimal Value02 { get; set; }
        public decimal Value03 { get; set; }
        public decimal Value04 { get; set; }
        public decimal Value05 { get; set; }
        public DateTime Date01 { get; set; }
        public DateTime Date02 { get; set; }
        public DateTime Date03 { get; set; }
        public DateTime Date04 { get; set; }
        public DateTime Date05 { get; set; }

        public LargeEntity ShallowClone()
        {
            return new LargeEntity
            {
                Id = this.Id,
                Property01 = this.Property01,
                Property02 = this.Property02,
                Property03 = this.Property03,
                Property04 = this.Property04,
                Property05 = this.Property05,
                Property06 = this.Property06,
                Property07 = this.Property07,
                Property08 = this.Property08,
                Property09 = this.Property09,
                Property10 = this.Property10,
                Value01 = this.Value01,
                Value02 = this.Value02,
                Value03 = this.Value03,
                Value04 = this.Value04,
                Value05 = this.Value05,
                Date01 = this.Date01,
                Date02 = this.Date02,
                Date03 = this.Date03,
                Date04 = this.Date04,
                Date05 = this.Date05
            };
        }

        public LargeEntity DeepClone() => ShallowClone();
    }

    /// <summary>
    /// Entity with nested complex types
    /// </summary>
    public class NestedEntity : IEntityCloneable<NestedEntity>
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public Address? BillingAddress { get; set; }
        public Address? ShippingAddress { get; set; }
        public List<OrderLine> OrderLines { get; set; } = new List<OrderLine>();

        public NestedEntity ShallowClone()
        {
            return new NestedEntity
            {
                Id = this.Id,
                Name = this.Name,
                BillingAddress = this.BillingAddress,
                ShippingAddress = this.ShippingAddress,
                OrderLines = this.OrderLines
            };
        }

        public NestedEntity DeepClone()
        {
            return new NestedEntity
            {
                Id = this.Id,
                Name = this.Name,
                BillingAddress = this.BillingAddress != null ? new Address
                {
                    Street = this.BillingAddress.Street,
                    City = this.BillingAddress.City,
                    ZipCode = this.BillingAddress.ZipCode
                } : null,
                ShippingAddress = this.ShippingAddress != null ? new Address
                {
                    Street = this.ShippingAddress.Street,
                    City = this.ShippingAddress.City,
                    ZipCode = this.ShippingAddress.ZipCode
                } : null,
                OrderLines = this.OrderLines?.Select(ol => new OrderLine
                {
                    ProductId = ol.ProductId,
                    Quantity = ol.Quantity,
                    Price = ol.Price
                }).ToList() ?? new List<OrderLine>()
            };
        }
    }

    public class Address
    {
        public string Street { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string ZipCode { get; set; } = string.Empty;
    }

    public class OrderLine
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }

    // ============================================================================
    // CLONING EDGE CASE TESTS
    // ============================================================================

    public class CloningEdgeCaseTests
    {
        // ====================================================================
        // NULLABLE PROPERTY TESTS
        // ====================================================================

        [Fact]
        public void ShallowClone_WithAllNullValues_CreatesIndependentCopy()
        {
            // Arrange
            EntityWithNullables original = new EntityWithNullables
            {
                Id = 1,
                NullableString = null,
                NullableInt = null,
                NullableDecimal = null,
                NullableDateTime = null,
                NullableBool = null
            };

            // Act
            EntityWithNullables clone = original.ShallowClone();
            clone.Id = 999;

            // Assert
            original.Id.Should().Be(1);
            clone.Id.Should().Be(999);
            clone.NullableString.Should().BeNull();
            clone.NullableInt.Should().BeNull();
            clone.NullableDecimal.Should().BeNull();
            clone.NullableDateTime.Should().BeNull();
            clone.NullableBool.Should().BeNull();
        }

        [Fact]
        public void ShallowClone_WithAllNonNullValues_CopiesValuesCorrectly()
        {
            // Arrange
            DateTime testDate = DateTime.UtcNow;
            EntityWithNullables original = new EntityWithNullables
            {
                Id = 1,
                NullableString = "Test Value",
                NullableInt = 42,
                NullableDecimal = 123.456m,
                NullableDateTime = testDate,
                NullableBool = true
            };

            // Act
            EntityWithNullables clone = original.ShallowClone();
            clone.NullableString = "Modified";
            clone.NullableInt = 999;

            // Assert
            original.NullableString.Should().Be("Test Value");
            original.NullableInt.Should().Be(42);
            clone.NullableString.Should().Be("Modified");
            clone.NullableInt.Should().Be(999);
            clone.NullableDecimal.Should().Be(123.456m);
            clone.NullableDateTime.Should().Be(testDate);
            clone.NullableBool.Should().BeTrue();
        }

        [Fact]
        public void ShallowClone_WithMixedNullValues_HandlesCorrectly()
        {
            // Arrange
            EntityWithNullables original = new EntityWithNullables
            {
                Id = 1,
                NullableString = "Test",
                NullableInt = null,
                NullableDecimal = 99.99m,
                NullableDateTime = null,
                NullableBool = false
            };

            // Act
            EntityWithNullables clone = original.ShallowClone();

            // Assert
            clone.NullableString.Should().Be("Test");
            clone.NullableInt.Should().BeNull();
            clone.NullableDecimal.Should().Be(99.99m);
            clone.NullableDateTime.Should().BeNull();
            clone.NullableBool.Should().BeFalse();
        }

        // ====================================================================
        // COLLECTION TESTS
        // ====================================================================

        [Fact]
        public void ShallowClone_WithCollections_SharesReferences()
        {
            // Arrange
            EntityWithCollections original = new EntityWithCollections
            {
                Id = 1,
                Name = "Original",
                Tags = new List<string> { "tag1", "tag2", "tag3" },
                Metadata = new Dictionary<string, string> { ["key1"] = "value1" },
                Scores = new int[] { 10, 20, 30 }
            };

            // Act
            EntityWithCollections clone = original.ShallowClone();
            clone.Tags.Add("tag4"); // Modifies shared list
            clone.Metadata["key2"] = "value2"; // Modifies shared dictionary
            clone.Scores[0] = 999; // Modifies shared array

            // Assert - Shallow clone shares collection references
            original.Tags.Should().HaveCount(4); // Modified by clone
            original.Tags.Should().Contain("tag4");
            original.Metadata.Should().ContainKey("key2");
            original.Scores[0].Should().Be(999);
        }

        [Fact]
        public void DeepClone_WithCollections_CreatesIndependentCopies()
        {
            // Arrange
            EntityWithCollections original = new EntityWithCollections
            {
                Id = 1,
                Name = "Original",
                Tags = new List<string> { "tag1", "tag2", "tag3" },
                Metadata = new Dictionary<string, string> { ["key1"] = "value1" },
                Scores = new int[] { 10, 20, 30 }
            };

            // Act
            EntityWithCollections clone = original.DeepClone();
            clone.Tags.Add("tag4");
            clone.Metadata["key2"] = "value2";
            clone.Scores[0] = 999;

            // Assert - Deep clone creates independent collections
            original.Tags.Should().HaveCount(3); // Not modified
            original.Tags.Should().NotContain("tag4");
            original.Metadata.Should().NotContainKey("key2");
            original.Scores[0].Should().Be(10);
        }

        [Fact]
        public void DeepClone_WithEmptyCollections_HandlesCorrectly()
        {
            // Arrange
            EntityWithCollections original = new EntityWithCollections
            {
                Id = 1,
                Name = "Empty Collections",
                Tags = new List<string>(),
                Metadata = new Dictionary<string, string>(),
                Scores = Array.Empty<int>()
            };

            // Act
            EntityWithCollections clone = original.DeepClone();
            clone.Tags.Add("new-tag");

            // Assert
            original.Tags.Should().BeEmpty();
            clone.Tags.Should().ContainSingle("new-tag");
        }

        [Fact]
        public void DeepClone_WithNullCollections_HandlesGracefully()
        {
            // Arrange
            EntityWithCollections original = new EntityWithCollections
            {
                Id = 1,
                Name = "Null Collections",
                Tags = null!,
                Metadata = null!,
                Scores = null!
            };

            // Act
            EntityWithCollections clone = original.DeepClone();

            // Assert - Should create empty collections instead of null
            clone.Tags.Should().NotBeNull();
            clone.Metadata.Should().NotBeNull();
            clone.Scores.Should().NotBeNull();
        }

        // ====================================================================
        // PERFORMANCE TESTS
        // ====================================================================

        [Fact]
        public void ShallowClone_LargeEntity_CompletesQuickly()
        {
            // Arrange
            LargeEntity original = new LargeEntity
            {
                Id = 1,
                Property01 = "Value01",
                Property02 = "Value02",
                Property03 = "Value03",
                Property04 = "Value04",
                Property05 = "Value05",
                Property06 = "Value06",
                Property07 = "Value07",
                Property08 = "Value08",
                Property09 = "Value09",
                Property10 = "Value10",
                Value01 = 100.01m,
                Value02 = 200.02m,
                Value03 = 300.03m,
                Value04 = 400.04m,
                Value05 = 500.05m,
                Date01 = DateTime.UtcNow,
                Date02 = DateTime.UtcNow.AddDays(1),
                Date03 = DateTime.UtcNow.AddDays(2),
                Date04 = DateTime.UtcNow.AddDays(3),
                Date05 = DateTime.UtcNow.AddDays(4)
            };

            // Act & Assert - Should complete in reasonable time (< 1ms typically)
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            LargeEntity clone = original.ShallowClone();
            sw.Stop();

            sw.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(10));
            clone.Property01.Should().Be("Value01");
            clone.Value05.Should().Be(500.05m);
        }

        [Fact]
        public void ShallowClone_BulkCloning_HandlesThousandsEfficiently()
        {
            // Arrange
            List<LargeEntity> originals = new List<LargeEntity>();
            for (int i = 0; i < 1000; i++)
            {
                originals.Add(new LargeEntity
                {
                    Id = i,
                    Property01 = $"Value-{i}",
                    Value01 = i * 10m,
                    Date01 = DateTime.UtcNow.AddDays(i)
                });
            }

            // Act
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            List<LargeEntity> clones = originals.Select(e => e.ShallowClone()).ToList();
            sw.Stop();

            // Assert - Should handle 1000 clones in < 100ms
            sw.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(100));
            clones.Should().HaveCount(1000);
            clones[500].Id.Should().Be(500);
            clones[500].Property01.Should().Be("Value-500");
        }

        // ====================================================================
        // NESTED ENTITY TESTS
        // ====================================================================

        [Fact]
        public void ShallowClone_NestedEntity_SharesNestedReferences()
        {
            // Arrange
            NestedEntity original = new NestedEntity
            {
                Id = 1,
                Name = "Order-1",
                BillingAddress = new Address { Street = "123 Main St", City = "City1", ZipCode = "12345" },
                ShippingAddress = new Address { Street = "456 Oak Ave", City = "City2", ZipCode = "67890" },
                OrderLines = new List<OrderLine>
                {
                    new OrderLine { ProductId = 1, Quantity = 2, Price = 50m },
                    new OrderLine { ProductId = 2, Quantity = 1, Price = 100m }
                }
            };

            // Act
            NestedEntity clone = original.ShallowClone();
            clone.BillingAddress!.Street = "MODIFIED";
            clone.OrderLines.Add(new OrderLine { ProductId = 3, Quantity = 3, Price = 25m });

            // Assert - Shallow clone shares nested references
            original.BillingAddress!.Street.Should().Be("MODIFIED");
            original.OrderLines.Should().HaveCount(3);
        }

        [Fact]
        public void DeepClone_NestedEntity_CreatesIndependentNestedCopies()
        {
            // Arrange
            NestedEntity original = new NestedEntity
            {
                Id = 1,
                Name = "Order-1",
                BillingAddress = new Address { Street = "123 Main St", City = "City1", ZipCode = "12345" },
                ShippingAddress = new Address { Street = "456 Oak Ave", City = "City2", ZipCode = "67890" },
                OrderLines = new List<OrderLine>
                {
                    new OrderLine { ProductId = 1, Quantity = 2, Price = 50m },
                    new OrderLine { ProductId = 2, Quantity = 1, Price = 100m }
                }
            };

            // Act
            NestedEntity clone = original.DeepClone();
            clone.BillingAddress!.Street = "MODIFIED";
            clone.OrderLines.Add(new OrderLine { ProductId = 3, Quantity = 3, Price = 25m });
            clone.OrderLines[0].Quantity = 999;

            // Assert - Deep clone creates independent nested objects
            original.BillingAddress!.Street.Should().Be("123 Main St");
            original.OrderLines.Should().HaveCount(2);
            original.OrderLines[0].Quantity.Should().Be(2);
        }

        [Fact]
        public void DeepClone_WithNullNestedObjects_HandlesCorrectly()
        {
            // Arrange
            NestedEntity original = new NestedEntity
            {
                Id = 1,
                Name = "Order-1",
                BillingAddress = null,
                ShippingAddress = new Address { Street = "123 Main", City = "City", ZipCode = "12345" },
                OrderLines = new List<OrderLine>()
            };

            // Act
            NestedEntity clone = original.DeepClone();
            clone.ShippingAddress!.Street = "Modified";

            // Assert
            original.ShippingAddress!.Street.Should().Be("123 Main");
            clone.BillingAddress.Should().BeNull();
            clone.OrderLines.Should().NotBeNull();
        }

        // ====================================================================
        // BOUNDARY VALUE TESTS
        // ====================================================================

        [Fact]
        public void ShallowClone_WithBoundaryValues_HandlesCorrectly()
        {
            // Arrange
            EntityWithNullables original = new EntityWithNullables
            {
                Id = int.MaxValue,
                NullableInt = int.MinValue,
                NullableDecimal = decimal.MaxValue,
                NullableDateTime = DateTime.MaxValue,
                NullableString = string.Empty
            };

            // Act
            EntityWithNullables clone = original.ShallowClone();

            // Assert
            clone.Id.Should().Be(int.MaxValue);
            clone.NullableInt.Should().Be(int.MinValue);
            clone.NullableDecimal.Should().Be(decimal.MaxValue);
            clone.NullableDateTime.Should().Be(DateTime.MaxValue);
            clone.NullableString.Should().BeEmpty();
        }

        [Fact]
        public void ShallowClone_WithLargeStringValues_HandlesCorrectly()
        {
            // Arrange
            string largeString = new string('X', 10000);
            EntityWithNullables original = new EntityWithNullables
            {
                Id = 1,
                NullableString = largeString
            };

            // Act
            EntityWithNullables clone = original.ShallowClone();
            clone.NullableString = "Modified";

            // Assert - Strings are immutable, so this is always safe
            original.NullableString.Should().HaveLength(10000);
            clone.NullableString.Should().Be("Modified");
        }

        // ====================================================================
        // INDEPENDENCE VERIFICATION TESTS
        // ====================================================================

        [Fact]
        public void ShallowClone_MultipleClones_AreIndependent()
        {
            // Arrange
            EntityWithNullables original = new EntityWithNullables
            {
                Id = 1,
                NullableString = "Original",
                NullableInt = 100
            };

            // Act
            EntityWithNullables clone1 = original.ShallowClone();
            EntityWithNullables clone2 = original.ShallowClone();
            EntityWithNullables clone3 = original.ShallowClone();

            clone1.Id = 10;
            clone1.NullableInt = 1000;
            clone2.Id = 20;
            clone2.NullableInt = 2000;
            clone3.Id = 30;
            clone3.NullableInt = 3000;

            // Assert - All clones and original are independent
            original.Id.Should().Be(1);
            original.NullableInt.Should().Be(100);
            clone1.Id.Should().Be(10);
            clone1.NullableInt.Should().Be(1000);
            clone2.Id.Should().Be(20);
            clone2.NullableInt.Should().Be(2000);
            clone3.Id.Should().Be(30);
            clone3.NullableInt.Should().Be(3000);
        }

        [Fact]
        public void DeepClone_NestedCloning_AllLevelsIndependent()
        {
            // Arrange
            NestedEntity original = new NestedEntity
            {
                Id = 1,
                Name = "Original",
                BillingAddress = new Address { Street = "123 Main", City = "City1", ZipCode = "12345" }
            };

            // Act - Clone the original, then clone the clone
            NestedEntity clone1 = original.DeepClone();
            NestedEntity clone2 = clone1.DeepClone();

            clone1.Name = "Clone1";
            clone1.BillingAddress!.Street = "Clone1 Street";
            clone2.Name = "Clone2";
            clone2.BillingAddress!.Street = "Clone2 Street";

            // Assert - All three entities are independent
            original.Name.Should().Be("Original");
            original.BillingAddress!.Street.Should().Be("123 Main");
            clone1.Name.Should().Be("Clone1");
            clone1.BillingAddress!.Street.Should().Be("Clone1 Street");
            clone2.Name.Should().Be("Clone2");
            clone2.BillingAddress!.Street.Should().Be("Clone2 Street");
        }

        [Fact]
        public void ShallowClone_ValueTypeProperties_AlwaysIndependent()
        {
            // Arrange
            LargeEntity original = new LargeEntity
            {
                Id = 1,
                Value01 = 100m,
                Value02 = 200m,
                Date01 = DateTime.UtcNow
            };

            // Act
            LargeEntity clone = original.ShallowClone();
            clone.Id = 999;
            clone.Value01 = 999m;
            clone.Date01 = DateTime.MinValue;

            // Assert - Value types are always independent in any clone
            original.Id.Should().Be(1);
            original.Value01.Should().Be(100m);
            original.Date01.Should().NotBe(DateTime.MinValue);
        }
    }
}
