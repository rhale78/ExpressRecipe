using Xunit;
using FluentAssertions;
using System.Collections.Generic;
using System.Linq;
using HighSpeedDAL.SourceGenerators.Generators;

namespace HighSpeedDAL.Tests.SourceGenerators;

/// <summary>
/// Comprehensive tests for PropertyAutoGenerator.
/// Validates auto-generation of Id, Audit, and SoftDelete properties.
/// </summary>
public class PropertyAutoGeneratorTests
{
    #region Id Auto-Generation Tests

    [Fact]
    public void GenerateAutoProperties_NoIdProperty_GeneratesIntId()
    {
        // Arrange
        EntityMetadata metadata = new EntityMetadata
        {
            ClassName = "Product",
            Properties = new List<PropertyDefinition>
            {
                new PropertyDefinition { Name = "Name", TypeName = "string" },
                new PropertyDefinition { Name = "Price", TypeName = "decimal" }
            }
        };

        PropertyAutoGenerator generator = new PropertyAutoGenerator(metadata);

        // Act
        List<PropertyDefinition> allProperties = generator.GetAllProperties();

        // Assert
        PropertyDefinition idProperty = allProperties.FirstOrDefault(p => p.Name == "Id");
        idProperty.Should().NotBeNull("framework should auto-generate Id when missing");
        idProperty.TypeName.Should().Be("int");
        idProperty.IsPrimaryKey.Should().BeTrue();
        idProperty.IsAutoIncrement.Should().BeTrue();
        idProperty.IsNullable.Should().BeFalse();
    }

    [Fact]
    public void GenerateAutoProperties_HasIdProperty_DoesNotGenerateId()
    {
        // Arrange
        EntityMetadata metadata = new EntityMetadata
        {
            ClassName = "Product",
            Properties = new List<PropertyDefinition>
            {
                new PropertyDefinition { Name = "Id", TypeName = "int", IsPrimaryKey = true },
                new PropertyDefinition { Name = "Name", TypeName = "string" }
            }
        };

        PropertyAutoGenerator generator = new PropertyAutoGenerator(metadata);

        // Act
        string autoGenCode = generator.GenerateAutoProperties();

        // Assert
        autoGenCode.Should().NotContain("public int Id", "user already provided Id property");
    }

    [Fact]
    public void GenerateAutoProperties_HasCustomPrimaryKey_DoesNotGenerateId()
    {
        // Arrange
        EntityMetadata metadata = new EntityMetadata
        {
            ClassName = "Product",
            Properties = new List<PropertyDefinition>
            {
                new PropertyDefinition { Name = "ProductCode", TypeName = "string", IsPrimaryKey = true },
                new PropertyDefinition { Name = "Name", TypeName = "string" }
            }
        };

        PropertyAutoGenerator generator = new PropertyAutoGenerator(metadata);

        // Act
        List<PropertyDefinition> allProperties = generator.GetAllProperties();

        // Assert
        allProperties.Should().NotContain(p => p.Name == "Id", 
            "user specified custom PK, don't auto-generate Id");
        allProperties.Should().Contain(p => p.Name == "ProductCode" && p.IsPrimaryKey);
    }

    [Fact]
    public void GenerateAutoProperties_GuidIdProperty_UsesAsIs()
    {
        // Arrange
        EntityMetadata metadata = new EntityMetadata
        {
            ClassName = "Product",
            Properties = new List<PropertyDefinition>
            {
                new PropertyDefinition { Name = "Id", TypeName = "Guid" },
                new PropertyDefinition { Name = "Name", TypeName = "string" }
            }
        };

        PropertyAutoGenerator generator = new PropertyAutoGenerator(metadata);

        // Act
        string autoGenCode = generator.GenerateAutoProperties();

        // Assert
        autoGenCode.Should().NotContain("public Guid Id", 
            "user provided Guid Id, framework should not generate another");
    }

    #endregion

    #region Audit Auto-Generation Tests

    [Fact]
    public void GenerateAutoProperties_AuditAttribute_GeneratesAllAuditProperties()
    {
        // Arrange
        EntityMetadata metadata = new EntityMetadata
        {
            ClassName = "Product",
            HasAuditAttribute = true,
            Properties = new List<PropertyDefinition>
            {
                new PropertyDefinition { Name = "Name", TypeName = "string" }
            }
        };

        PropertyAutoGenerator generator = new PropertyAutoGenerator(metadata);

        // Act
        List<PropertyDefinition> allProperties = generator.GetAllProperties();

        // Assert
        allProperties.Should().Contain(p => p.Name == "Id", "auto-generate Id");
        allProperties.Should().Contain(p => p.Name == "CreatedDate" && p.TypeName == "DateTime");
        allProperties.Should().Contain(p => p.Name == "CreatedBy" && p.TypeName == "string");
        allProperties.Should().Contain(p => p.Name == "ModifiedDate" && p.TypeName == "DateTime");
        allProperties.Should().Contain(p => p.Name == "ModifiedBy" && p.TypeName == "string");
    }

    [Fact]
    public void GenerateAutoProperties_AuditAttribute_GeneratesVisibleProperties()
    {
        // Arrange
        EntityMetadata metadata = new EntityMetadata
        {
            ClassName = "Product",
            HasAuditAttribute = true,
            Properties = new List<PropertyDefinition>
            {
                new PropertyDefinition { Name = "Name", TypeName = "string" }
            }
        };

        PropertyAutoGenerator generator = new PropertyAutoGenerator(metadata);

        // Act
        string autoGenCode = generator.GenerateAutoProperties();

        // Assert
        autoGenCode.Should().Contain("public DateTime CreatedDate { get; set; }");
        autoGenCode.Should().Contain("public string CreatedBy { get; set; }");
        autoGenCode.Should().Contain("public DateTime ModifiedDate { get; set; }");
        autoGenCode.Should().Contain("public string ModifiedBy { get; set; }");
        autoGenCode.Should().Contain("AUTO-GENERATED by HighSpeedDAL framework");
    }

    [Fact]
    public void GenerateAutoProperties_AuditAttributeWithExistingCreatedDate_OnlyGeneratesMissing()
    {
        // Arrange
        EntityMetadata metadata = new EntityMetadata
        {
            ClassName = "Product",
            HasAuditAttribute = true,
            Properties = new List<PropertyDefinition>
            {
                new PropertyDefinition { Name = "CreatedDate", TypeName = "DateTime" },
                new PropertyDefinition { Name = "Name", TypeName = "string" }
            }
        };

        PropertyAutoGenerator generator = new PropertyAutoGenerator(metadata);

        // Act
        List<PropertyDefinition> allProperties = generator.GetAllProperties();

        // Assert
        int createdDateCount = allProperties.Count(p => p.Name == "CreatedDate");
        createdDateCount.Should().Be(1, "user provided CreatedDate, don't duplicate");
        
        allProperties.Should().Contain(p => p.Name == "CreatedBy", "generate missing audit properties");
        allProperties.Should().Contain(p => p.Name == "ModifiedDate");
        allProperties.Should().Contain(p => p.Name == "ModifiedBy");
    }

    [Fact]
    public void GenerateAutoProperties_AuditAttributeWithAllAuditProperties_GeneratesNothing()
    {
        // Arrange
        EntityMetadata metadata = new EntityMetadata
        {
            ClassName = "Product",
            HasAuditAttribute = true,
            Properties = new List<PropertyDefinition>
            {
                new PropertyDefinition { Name = "Id", TypeName = "int" },
                new PropertyDefinition { Name = "CreatedDate", TypeName = "DateTime" },
                new PropertyDefinition { Name = "CreatedBy", TypeName = "string" },
                new PropertyDefinition { Name = "ModifiedDate", TypeName = "DateTime" },
                new PropertyDefinition { Name = "ModifiedBy", TypeName = "string" },
                new PropertyDefinition { Name = "Name", TypeName = "string" }
            }
        };

        PropertyAutoGenerator generator = new PropertyAutoGenerator(metadata);

        // Act
        string autoGenCode = generator.GenerateAutoProperties();

        // Assert
        autoGenCode.Should().NotContain("CreatedDate", "user provided all audit properties");
        autoGenCode.Should().NotContain("CreatedBy");
        autoGenCode.Should().NotContain("ModifiedDate");
        autoGenCode.Should().NotContain("ModifiedBy");
    }

    #endregion

    #region SoftDelete Auto-Generation Tests

    [Fact]
    public void GenerateAutoProperties_SoftDeleteAttribute_GeneratesAllSoftDeleteProperties()
    {
        // Arrange
        EntityMetadata metadata = new EntityMetadata
        {
            ClassName = "Product",
            HasSoftDeleteAttribute = true,
            Properties = new List<PropertyDefinition>
            {
                new PropertyDefinition { Name = "Name", TypeName = "string" }
            }
        };

        PropertyAutoGenerator generator = new PropertyAutoGenerator(metadata);

        // Act
        List<PropertyDefinition> allProperties = generator.GetAllProperties();

        // Assert
        allProperties.Should().Contain(p => p.Name == "Id", "auto-generate Id");
        allProperties.Should().Contain(p => p.Name == "IsDeleted" && p.TypeName == "bool");
        allProperties.Should().Contain(p => p.Name == "DeletedDate" && p.TypeName == "DateTime?");
        allProperties.Should().Contain(p => p.Name == "DeletedBy" && p.TypeName == "string" && p.IsNullable);
    }

    [Fact]
    public void GenerateAutoProperties_SoftDeleteAttribute_GeneratesVisibleProperties()
    {
        // Arrange
        EntityMetadata metadata = new EntityMetadata
        {
            ClassName = "Product",
            HasSoftDeleteAttribute = true,
            Properties = new List<PropertyDefinition>
            {
                new PropertyDefinition { Name = "Name", TypeName = "string" }
            }
        };

        PropertyAutoGenerator generator = new PropertyAutoGenerator(metadata);

        // Act
        string autoGenCode = generator.GenerateAutoProperties();

        // Assert
        autoGenCode.Should().Contain("public bool IsDeleted { get; set; }");
        autoGenCode.Should().Contain("public DateTime? DeletedDate { get; set; }");
        autoGenCode.Should().Contain("public string DeletedBy { get; set; }");
        autoGenCode.Should().Contain("AUTO-GENERATED by HighSpeedDAL framework");
    }

    [Fact]
    public void GenerateAutoProperties_SoftDeleteWithExistingIsDeleted_OnlyGeneratesMissing()
    {
        // Arrange
        EntityMetadata metadata = new EntityMetadata
        {
            ClassName = "Product",
            HasSoftDeleteAttribute = true,
            Properties = new List<PropertyDefinition>
            {
                new PropertyDefinition { Name = "IsDeleted", TypeName = "bool" },
                new PropertyDefinition { Name = "Name", TypeName = "string" }
            }
        };

        PropertyAutoGenerator generator = new PropertyAutoGenerator(metadata);

        // Act
        List<PropertyDefinition> allProperties = generator.GetAllProperties();

        // Assert
        int isDeletedCount = allProperties.Count(p => p.Name == "IsDeleted");
        isDeletedCount.Should().Be(1, "user provided IsDeleted, don't duplicate");
        
        allProperties.Should().Contain(p => p.Name == "DeletedDate");
        allProperties.Should().Contain(p => p.Name == "DeletedBy");
    }

    #endregion

    #region Combined Scenarios Tests

    [Fact]
    public void GenerateAutoProperties_AuditAndSoftDelete_GeneratesAllProperties()
    {
        // Arrange
        EntityMetadata metadata = new EntityMetadata
        {
            ClassName = "Product",
            HasAuditAttribute = true,
            HasSoftDeleteAttribute = true,
            Properties = new List<PropertyDefinition>
            {
                new PropertyDefinition { Name = "Name", TypeName = "string" },
                new PropertyDefinition { Name = "Price", TypeName = "decimal" }
            }
        };

        PropertyAutoGenerator generator = new PropertyAutoGenerator(metadata);

        // Act
        List<PropertyDefinition> allProperties = generator.GetAllProperties();

        // Assert - should have: Id + 2 user props + 4 audit + 3 soft delete = 10 total
        allProperties.Should().HaveCount(10);
        
        // Verify all auto-generated properties present
        allProperties.Should().Contain(p => p.Name == "Id");
        allProperties.Should().Contain(p => p.Name == "CreatedDate");
        allProperties.Should().Contain(p => p.Name == "CreatedBy");
        allProperties.Should().Contain(p => p.Name == "ModifiedDate");
        allProperties.Should().Contain(p => p.Name == "ModifiedBy");
        allProperties.Should().Contain(p => p.Name == "IsDeleted");
        allProperties.Should().Contain(p => p.Name == "DeletedDate");
        allProperties.Should().Contain(p => p.Name == "DeletedBy");
    }

    [Fact]
    public void GenerateAutoProperties_MixOfUserProvidedAndAutoGenerated_Works()
    {
        // Arrange
        EntityMetadata metadata = new EntityMetadata
        {
            ClassName = "Product",
            HasAuditAttribute = true,
            HasSoftDeleteAttribute = true,
            Properties = new List<PropertyDefinition>
            {
                new PropertyDefinition { Name = "Id", TypeName = "int" }, // User provided
                new PropertyDefinition { Name = "Name", TypeName = "string" },
                new PropertyDefinition { Name = "CreatedDate", TypeName = "DateTime" }, // User provided
                new PropertyDefinition { Name = "IsDeleted", TypeName = "bool" } // User provided
            }
        };

        PropertyAutoGenerator generator = new PropertyAutoGenerator(metadata);

        // Act
        List<PropertyDefinition> allProperties = generator.GetAllProperties();

        // Assert - should have: 4 user + 3 audit (missing) + 2 soft delete (missing) = 9 total
        allProperties.Should().HaveCount(9);
        
        // User-provided should exist once
        allProperties.Count(p => p.Name == "Id").Should().Be(1);
        allProperties.Count(p => p.Name == "CreatedDate").Should().Be(1);
        allProperties.Count(p => p.Name == "IsDeleted").Should().Be(1);
        
        // Auto-generated missing ones should exist
        allProperties.Should().Contain(p => p.Name == "CreatedBy");
        allProperties.Should().Contain(p => p.Name == "ModifiedDate");
        allProperties.Should().Contain(p => p.Name == "ModifiedBy");
        allProperties.Should().Contain(p => p.Name == "DeletedDate");
        allProperties.Should().Contain(p => p.Name == "DeletedBy");
    }

    [Fact]
    public void GenerateAutoProperties_NoAttributesNoId_OnlyGeneratesId()
    {
        // Arrange
        EntityMetadata metadata = new EntityMetadata
        {
            ClassName = "Product",
            HasAuditAttribute = false,
            HasSoftDeleteAttribute = false,
            Properties = new List<PropertyDefinition>
            {
                new PropertyDefinition { Name = "Name", TypeName = "string" },
                new PropertyDefinition { Name = "Price", TypeName = "decimal" }
            }
        };

        PropertyAutoGenerator generator = new PropertyAutoGenerator(metadata);

        // Act
        List<PropertyDefinition> allProperties = generator.GetAllProperties();

        // Assert - should have: 2 user props + Id = 3 total
        allProperties.Should().HaveCount(3);
        allProperties.Should().Contain(p => p.Name == "Id");
        allProperties.Should().NotContain(p => p.Name == "CreatedDate");
        allProperties.Should().NotContain(p => p.Name == "IsDeleted");
    }

    #endregion
}
