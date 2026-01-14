using FluentAssertions;
using HighSpeedDAL.Core.Attributes;
using HighSpeedDAL.Core.Tests.Models;
using System.Reflection;

namespace HighSpeedDAL.Core.Tests.Attributes;

/// <summary>
/// Tests for Table attribute
/// </summary>
public class TableAttributeTests
{
    [Fact]
    public void TableAttribute_WithTableName_ShouldSetCorrectly()
    {
        // Arrange & Act
        TableAttribute attribute = new TableAttribute("MyTable");

        // Assert
        attribute.TableName.Should().Be("MyTable");
        attribute.Schema.Should().Be("dbo"); // Default schema
    }

    [Fact]
    public void TableAttribute_WithCustomSchema_ShouldSetCorrectly()
    {
        // Arrange & Act
        TableAttribute attribute = new TableAttribute("MyTable", Schema = "custom");

        // Assert
        attribute.TableName.Should().Be("MyTable");
        attribute.Schema.Should().Be("custom");
    }

    [Fact]
    public void TableAttribute_OnEntity_ShouldBeRetrievable()
    {
        // Arrange
        Type entityType = typeof(TestEntity);

        // Act
        TableAttribute? attribute = entityType.GetCustomAttribute<TableAttribute>();

        // Assert
        attribute.Should().NotBeNull();
        attribute!.TableName.Should().Be("TestEntities");
    }

    [Fact]
    public void TableAttribute_WithCustomSchema_ShouldBeRetrievable()
    {
        // Arrange
        Type entityType = typeof(CustomSchemaEntity);

        // Act
        TableAttribute? attribute = entityType.GetCustomAttribute<TableAttribute>();

        // Assert
        attribute.Should().NotBeNull();
        attribute!.TableName.Should().Be("CustomSchemaEntity");
        attribute.Schema.Should().Be("dbo");
    }
}

/// <summary>
/// Tests for PrimaryKey attribute
/// </summary>
public class PrimaryKeyAttributeTests
{
    [Fact]
    public void PrimaryKeyAttribute_DefaultOrder_ShouldBeZero()
    {
        // Arrange & Act
        PrimaryKeyAttribute attribute = new PrimaryKeyAttribute();

        // Assert
        attribute.Order.Should().Be(0);
    }

    [Fact]
    public void PrimaryKeyAttribute_WithOrder_ShouldSetCorrectly()
    {
        // Arrange & Act
        PrimaryKeyAttribute attribute = new PrimaryKeyAttribute { Order = 2 };

        // Assert
        attribute.Order.Should().Be(2);
    }

    [Fact]
    public void PrimaryKeyAttribute_OnSingleKeyEntity_ShouldBeRetrievable()
    {
        // Arrange
        Type entityType = typeof(TestEntity);
        PropertyInfo idProperty = entityType.GetProperty("Id")!;

        // Act
        PrimaryKeyAttribute? attribute = idProperty.GetCustomAttribute<PrimaryKeyAttribute>();

        // Assert
        attribute.Should().NotBeNull();
        attribute!.Order.Should().Be(0);
    }

    [Fact]
    public void PrimaryKeyAttribute_OnCompositeKey_ShouldRetrieveMultiple()
    {
        // Arrange
        Type entityType = typeof(CompositeKeyEntity);
        PropertyInfo[] properties = entityType.GetProperties();

        // Act
        List<(PropertyInfo Property, int Order)> primaryKeys = properties
            .Select(p => (Property: p, Attribute: p.GetCustomAttribute<PrimaryKeyAttribute>()))
            .Where(x => x.Attribute != null)
            .Select(x => (x.Property, x.Attribute!.Order))
            .OrderBy(x => x.Order)
            .ToList();

        // Assert
        primaryKeys.Should().HaveCount(2);
        primaryKeys[0].Property.Name.Should().Be("TenantId");
        primaryKeys[0].Order.Should().Be(1);
        primaryKeys[1].Property.Name.Should().Be("EntityId");
        primaryKeys[1].Order.Should().Be(2);
    }
}

/// <summary>
/// Tests for Column attribute
/// </summary>
public class ColumnAttributeTests
{
    [Fact]
    public void ColumnAttribute_WithoutColumnName_ShouldUsePropertyName()
    {
        // Arrange
        PropertyInfo property = typeof(TestEntity).GetProperty("Description")!;

        // Act
        ColumnAttribute? attribute = property.GetCustomAttribute<ColumnAttribute>();

        // Assert
        attribute.Should().NotBeNull();
        attribute!.ColumnName.Should().BeNullOrEmpty(); // Will use property name
    }

    [Fact]
    public void ColumnAttribute_WithColumnName_ShouldSetCorrectly()
    {
        // Arrange
        PropertyInfo property = typeof(TestEntity).GetProperty("Name")!;

        // Act
        ColumnAttribute? attribute = property.GetCustomAttribute<ColumnAttribute>();

        // Assert
        attribute.Should().NotBeNull();
        attribute!.ColumnName.Should().Be("EntityName");
    }

    [Fact]
    public void ColumnAttribute_AllProperties_ShouldBeRetrievable()
    {
        // Arrange
        Type entityType = typeof(TestEntity);

        // Act
        List<PropertyInfo> columnsWithAttribute = entityType.GetProperties()
            .Where(p => p.GetCustomAttribute<ColumnAttribute>() != null)
            .ToList();

        // Assert
        columnsWithAttribute.Should().NotBeEmpty();
        columnsWithAttribute.Should().Contain(p => p.Name == "Name");
        columnsWithAttribute.Should().Contain(p => p.Name == "Description");
    }
}

/// <summary>
/// Tests for Index attribute
/// </summary>
public class IndexAttributeTests
{
    [Fact]
    public void IndexAttribute_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        IndexAttribute attribute = new IndexAttribute();

        // Assert
        attribute.IsUnique.Should().BeFalse();
        attribute.Order.Should().Be(0);
        attribute.IndexName.Should().BeNullOrEmpty();
    }

    [Fact]
    public void IndexAttribute_WithUniqueFlag_ShouldSetCorrectly()
    {
        // Arrange & Act
        IndexAttribute attribute = new IndexAttribute { IsUnique = true };

        // Assert
        attribute.IsUnique.Should().BeTrue();
    }

    [Fact]
    public void IndexAttribute_WithIndexName_ShouldSetCorrectly()
    {
        // Arrange & Act
        IndexAttribute attribute = new IndexAttribute { IndexName = "IX_Custom" };

        // Assert
        attribute.IndexName.Should().Be("IX_Custom");
    }

    [Fact]
    public void IndexAttribute_OnProperties_ShouldBeRetrievable()
    {
        // Arrange
        PropertyInfo nameProperty = typeof(TestEntity).GetProperty("Name")!;
        PropertyInfo createdDateProperty = typeof(TestEntity).GetProperty("CreatedDate")!;

        // Act
        IndexAttribute? nameIndex = nameProperty.GetCustomAttribute<IndexAttribute>();
        IndexAttribute? createdDateIndex = createdDateProperty.GetCustomAttribute<IndexAttribute>();

        // Assert
        nameIndex.Should().NotBeNull();
        createdDateIndex.Should().NotBeNull();
    }
}

/// <summary>
/// Tests for Cache attribute
/// </summary>
public class CacheAttributeTests
{
    [Fact]
    public void CacheAttribute_WithMemoryStrategy_ShouldSetCorrectly()
    {
        // Arrange & Act
        CacheAttribute attribute = new CacheAttribute(CacheStrategy.Memory);

        // Assert
        attribute.Strategy.Should().Be(CacheStrategy.Memory);
        attribute.ExpirationSeconds.Should().Be(300); // Default
    }

    [Fact]
    public void CacheAttribute_WithTwoLayerStrategy_ShouldSetCorrectly()
    {
        // Arrange & Act
        CacheAttribute attribute = new CacheAttribute(CacheStrategy.TwoLayer);

        // Assert
        attribute.Strategy.Should().Be(CacheStrategy.TwoLayer);
    }

    [Fact]
    public void CacheAttribute_WithCustomExpiration_ShouldSetCorrectly()
    {
        // Arrange & Act
        CacheAttribute attribute = new CacheAttribute(CacheStrategy.Memory, ExpirationSeconds = 600);

        // Assert
        attribute.ExpirationSeconds.Should().Be(600);
    }

    [Fact]
    public void CacheAttribute_OnEntity_ShouldBeRetrievable()
    {
        // Arrange
        Type entityType = typeof(TestEntity);

        // Act
        CacheAttribute? attribute = entityType.GetCustomAttribute<CacheAttribute>();

        // Assert
        attribute.Should().NotBeNull();
        attribute!.Strategy.Should().Be(CacheStrategy.Memory);
        attribute.ExpirationSeconds.Should().Be(300);
    }

    [Fact]
    public void CacheAttribute_NoneStrategy_ShouldDisableCache()
    {
        // Arrange
        Type entityType = typeof(HighVolumeEntity);

        // Act
        CacheAttribute? attribute = entityType.GetCustomAttribute<CacheAttribute>();

        // Assert
        attribute.Should().NotBeNull();
        attribute!.Strategy.Should().Be(CacheStrategy.None);
    }
}

/// <summary>
/// Tests for Audit attribute
/// </summary>
public class AuditAttributeTests
{
    [Fact]
    public void AuditAttribute_OnProperties_ShouldBeRetrievable()
    {
        // Arrange
        Type entityType = typeof(TestEntity);
        PropertyInfo lastModifiedProperty = entityType.GetProperty("LastModified")!;
        PropertyInfo modifiedByProperty = entityType.GetProperty("ModifiedBy")!;

        // Act
        AuditAttribute? lastModifiedAttr = lastModifiedProperty.GetCustomAttribute<AuditAttribute>();
        AuditAttribute? modifiedByAttr = modifiedByProperty.GetCustomAttribute<AuditAttribute>();

        // Assert
        lastModifiedAttr.Should().NotBeNull();
        modifiedByAttr.Should().NotBeNull();
    }

    [Fact]
    public void AutoAuditAttribute_OnEntity_ShouldBeRetrievable()
    {
        // Arrange
        Type entityType = typeof(AuditedEntity);

        // Act
        AutoAuditAttribute? attribute = entityType.GetCustomAttribute<AutoAuditAttribute>();

        // Assert
        attribute.Should().NotBeNull();
    }
}

/// <summary>
/// Tests for ReferenceTable attribute
/// </summary>
public class ReferenceTableAttributeTests
{
    [Fact]
    public void ReferenceTableAttribute_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        ReferenceTableAttribute attribute = new ReferenceTableAttribute();

        // Assert
        attribute.LoadOnStartup.Should().BeFalse(); // Default
    }

    [Fact]
    public void ReferenceTableAttribute_WithLoadOnStartup_ShouldSetCorrectly()
    {
        // Arrange & Act
        ReferenceTableAttribute attribute = new ReferenceTableAttribute { LoadOnStartup = true };

        // Assert
        attribute.LoadOnStartup.Should().BeTrue();
    }

    [Fact]
    public void ReferenceTableAttribute_OnEntity_ShouldBeRetrievable()
    {
        // Arrange
        Type entityType = typeof(StatusType);

        // Act
        ReferenceTableAttribute? attribute = entityType.GetCustomAttribute<ReferenceTableAttribute>();

        // Assert
        attribute.Should().NotBeNull();
        attribute!.LoadOnStartup.Should().BeTrue();
    }
}

/// <summary>
/// Tests for StagingTable attribute
/// </summary>
public class StagingTableAttributeTests
{
    [Fact]
    public void StagingTableAttribute_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        StagingTableAttribute attribute = new StagingTableAttribute();

        // Assert
        attribute.MergeIntervalSeconds.Should().Be(60); // Default
    }

    [Fact]
    public void StagingTableAttribute_WithCustomInterval_ShouldSetCorrectly()
    {
        // Arrange & Act
        StagingTableAttribute attribute = new StagingTableAttribute { MergeIntervalSeconds = 120 };

        // Assert
        attribute.MergeIntervalSeconds.Should().Be(120);
    }

    [Fact]
    public void StagingTableAttribute_OnEntity_ShouldBeRetrievable()
    {
        // Arrange
        Type entityType = typeof(HighVolumeEntity);

        // Act
        StagingTableAttribute? attribute = entityType.GetCustomAttribute<StagingTableAttribute>();

        // Assert
        attribute.Should().NotBeNull();
        attribute!.MergeIntervalSeconds.Should().Be(60);
    }
}

/// <summary>
/// Tests for SoftDelete attribute
/// </summary>
public class SoftDeleteAttributeTests
{
    [Fact]
    public void SoftDeleteAttribute_WithColumns_ShouldSetCorrectly()
    {
        // Arrange & Act
        SoftDeleteAttribute attribute = new SoftDeleteAttribute 
        { 
            DeletedColumn = "IsDeleted",
            DeletedDateColumn = "DeletedDate"
        };

        // Assert
        attribute.DeletedColumn.Should().Be("IsDeleted");
        attribute.DeletedDateColumn.Should().Be("DeletedDate");
    }

    [Fact]
    public void SoftDeleteAttribute_OnEntity_ShouldBeRetrievable()
    {
        // Arrange
        Type entityType = typeof(SoftDeleteEntity);

        // Act
        SoftDeleteAttribute? attribute = entityType.GetCustomAttribute<SoftDeleteAttribute>();

        // Assert
        attribute.Should().NotBeNull();
        attribute!.DeletedColumn.Should().Be("IsDeleted");
        attribute!.DeletedDateColumn.Should().Be("DeletedDate");
    }
}

/// <summary>
/// Tests for Identity attribute
/// </summary>
public class IdentityAttributeTests
{
    [Fact]
    public void IdentityAttribute_OnProperty_ShouldBeRetrievable()
    {
        // Arrange
        PropertyInfo idProperty = typeof(TestEntity).GetProperty("Id")!;

        // Act
        IdentityAttribute? attribute = idProperty.GetCustomAttribute<IdentityAttribute>();

        // Assert
        attribute.Should().NotBeNull();
    }

    [Fact]
    public void IdentityAttribute_CombinedWithPrimaryKey_ShouldWork()
    {
        // Arrange
        PropertyInfo idProperty = typeof(TestEntity).GetProperty("Id")!;

        // Act
        PrimaryKeyAttribute? pkAttribute = idProperty.GetCustomAttribute<PrimaryKeyAttribute>();
        IdentityAttribute? idAttribute = idProperty.GetCustomAttribute<IdentityAttribute>();

        // Assert
        pkAttribute.Should().NotBeNull();
        idAttribute.Should().NotBeNull();
    }
}
