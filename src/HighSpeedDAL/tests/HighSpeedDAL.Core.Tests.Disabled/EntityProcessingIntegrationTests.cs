using Xunit;
using FluentAssertions;
using System.Collections.Generic;
using System.Linq;
using HighSpeedDAL.SourceGenerators.Generators;
using HighSpeedDAL.SourceGenerators.Utilities;

namespace HighSpeedDAL.Tests.Integration;

/// <summary>
/// Integration tests validating complete entity processing scenarios.
/// Tests the full pipeline: entity → auto-generation → table creation.
/// </summary>
public class EntityProcessingIntegrationTests
{
    #region Minimal Entity Tests

    [Fact]
    public void ProcessEntity_MinimalPoco_GeneratesIdAndTableName()
    {
        // Arrange - Minimal POCO with just business properties
        EntityMetadata metadata = new EntityMetadata
        {
            ClassName = "Product",
            Namespace = "MyApp.Entities",
            Properties = new List<PropertyDefinition>
            {
                new PropertyDefinition { Name = "Name", TypeName = "string" },
                new PropertyDefinition { Name = "Price", TypeName = "decimal" }
            }
        };

        PropertyAutoGenerator generator = new PropertyAutoGenerator(metadata);

        // Act
        List<PropertyDefinition> allProperties = generator.GetAllProperties();
        string tableName = TableNamePluralizer.Pluralize(metadata.ClassName);

        // Assert
        tableName.Should().Be("Products", "class name should be pluralized for table");
        allProperties.Should().Contain(p => p.Name == "Id" && p.IsPrimaryKey, 
            "framework should auto-generate Id");
        allProperties.Should().HaveCount(3, "2 user properties + 1 auto-generated Id");
    }

    #endregion

    #region Audit-Enabled Entity Tests

    [Fact]
    public void ProcessEntity_AuditEnabled_GeneratesAllAuditPropertiesAndId()
    {
        // Arrange
        EntityMetadata metadata = new EntityMetadata
        {
            ClassName = "Customer",
            HasAuditAttribute = true,
            Properties = new List<PropertyDefinition>
            {
                new PropertyDefinition { Name = "Name", TypeName = "string" },
                new PropertyDefinition { Name = "Email", TypeName = "string" }
            }
        };

        PropertyAutoGenerator generator = new PropertyAutoGenerator(metadata);

        // Act
        List<PropertyDefinition> allProperties = generator.GetAllProperties();
        string tableName = TableNamePluralizer.Pluralize(metadata.ClassName);

        // Assert
        tableName.Should().Be("Customers");
        
        // Should have: 2 user + 1 Id + 4 audit = 7 total
        allProperties.Should().HaveCount(7);
        allProperties.Should().Contain(p => p.Name == "Id");
        allProperties.Should().Contain(p => p.Name == "CreatedDate");
        allProperties.Should().Contain(p => p.Name == "CreatedBy");
        allProperties.Should().Contain(p => p.Name == "ModifiedDate");
        allProperties.Should().Contain(p => p.Name == "ModifiedBy");
    }

    #endregion

    #region SoftDelete-Enabled Entity Tests

    [Fact]
    public void ProcessEntity_SoftDeleteEnabled_GeneratesAllSoftDeletePropertiesAndId()
    {
        // Arrange
        EntityMetadata metadata = new EntityMetadata
        {
            ClassName = "Order",
            HasSoftDeleteAttribute = true,
            Properties = new List<PropertyDefinition>
            {
                new PropertyDefinition { Name = "OrderNumber", TypeName = "string" },
                new PropertyDefinition { Name = "Total", TypeName = "decimal" }
            }
        };

        PropertyAutoGenerator generator = new PropertyAutoGenerator(metadata);

        // Act
        List<PropertyDefinition> allProperties = generator.GetAllProperties();
        string tableName = TableNamePluralizer.Pluralize(metadata.ClassName);

        // Assert
        tableName.Should().Be("Orders");
        
        // Should have: 2 user + 1 Id + 3 soft delete = 6 total
        allProperties.Should().HaveCount(6);
        allProperties.Should().Contain(p => p.Name == "Id");
        allProperties.Should().Contain(p => p.Name == "IsDeleted");
        allProperties.Should().Contain(p => p.Name == "DeletedDate");
        allProperties.Should().Contain(p => p.Name == "DeletedBy");
    }

    #endregion

    #region Fully-Featured Entity Tests

    [Fact]
    public void ProcessEntity_AuditAndSoftDeleteEnabled_GeneratesAllProperties()
    {
        // Arrange - Entity with both Audit and SoftDelete
        EntityMetadata metadata = new EntityMetadata
        {
            ClassName = "Invoice",
            HasAuditAttribute = true,
            HasSoftDeleteAttribute = true,
            Properties = new List<PropertyDefinition>
            {
                new PropertyDefinition { Name = "InvoiceNumber", TypeName = "string" },
                new PropertyDefinition { Name = "Amount", TypeName = "decimal" },
                new PropertyDefinition { Name = "DueDate", TypeName = "DateTime" }
            }
        };

        PropertyAutoGenerator generator = new PropertyAutoGenerator(metadata);

        // Act
        List<PropertyDefinition> allProperties = generator.GetAllProperties();
        string tableName = TableNamePluralizer.Pluralize(metadata.ClassName);

        // Assert
        tableName.Should().Be("Invoices");
        
        // Should have: 3 user + 1 Id + 4 audit + 3 soft delete = 11 total
        allProperties.Should().HaveCount(11);
        
        // Verify all expected properties present
        string[] expectedProperties = new[]
        {
            "Id", "InvoiceNumber", "Amount", "DueDate",
            "CreatedDate", "CreatedBy", "ModifiedDate", "ModifiedBy",
            "IsDeleted", "DeletedDate", "DeletedBy"
        };

        foreach (string propertyName in expectedProperties)
        {
            allProperties.Should().Contain(p => p.Name == propertyName,
                because: $"{propertyName} should be present");
        }
    }

    #endregion

    #region Reference Table Tests

    [Fact]
    public void ProcessEntity_ReferenceTableWithCsv_ConfiguresCorrectly()
    {
        // Arrange
        EntityMetadata metadata = new EntityMetadata
        {
            ClassName = "State",
            IsReferenceTable = true,
            CsvFilePath = "Data/States.csv",
            MergeStrategy = "MergeOrInsert",
            Properties = new List<PropertyDefinition>
            {
                new PropertyDefinition { Name = "Code", TypeName = "string" },
                new PropertyDefinition { Name = "Name", TypeName = "string" }
            }
        };

        PropertyAutoGenerator generator = new PropertyAutoGenerator(metadata);

        // Act
        List<PropertyDefinition> allProperties = generator.GetAllProperties();
        string tableName = TableNamePluralizer.Pluralize(metadata.ClassName);

        // Assert
        tableName.Should().Be("States");
        metadata.CsvFilePath.Should().Be("Data/States.csv");
        metadata.MergeStrategy.Should().Be("MergeOrInsert");
        
        // Should have: 2 user + 1 Id = 3 total
        allProperties.Should().HaveCount(3);
        allProperties.Should().Contain(p => p.Name == "Id");
    }

    #endregion

    #region Custom Primary Key Tests

    [Fact]
    public void ProcessEntity_CustomPrimaryKey_DoesNotGenerateId()
    {
        // Arrange
        EntityMetadata metadata = new EntityMetadata
        {
            ClassName = "LegacyProduct",
            Properties = new List<PropertyDefinition>
            {
                new PropertyDefinition { Name = "ProductCode", TypeName = "string", IsPrimaryKey = true },
                new PropertyDefinition { Name = "Name", TypeName = "string" },
                new PropertyDefinition { Name = "Price", TypeName = "decimal" }
            }
        };

        PropertyAutoGenerator generator = new PropertyAutoGenerator(metadata);

        // Act
        List<PropertyDefinition> allProperties = generator.GetAllProperties();

        // Assert
        allProperties.Should().NotContain(p => p.Name == "Id", 
            "user specified custom PK, don't auto-generate Id");
        allProperties.Should().Contain(p => p.Name == "ProductCode" && p.IsPrimaryKey);
        allProperties.Should().HaveCount(3, "3 user properties, no auto-generated Id");
    }

    #endregion

    #region User-Provided Audit Properties Tests

    [Fact]
    public void ProcessEntity_UserProvidesPartialAudit_GeneratesOnlyMissing()
    {
        // Arrange - User provides some audit properties, framework fills in the rest
        EntityMetadata metadata = new EntityMetadata
        {
            ClassName = "Article",
            HasAuditAttribute = true,
            Properties = new List<PropertyDefinition>
            {
                new PropertyDefinition { Name = "Title", TypeName = "string" },
                new PropertyDefinition { Name = "CreatedDate", TypeName = "DateTime" }, // User provided
                new PropertyDefinition { Name = "ModifiedBy", TypeName = "string" } // User provided
            }
        };

        PropertyAutoGenerator generator = new PropertyAutoGenerator(metadata);

        // Act
        List<PropertyDefinition> allProperties = generator.GetAllProperties();

        // Assert
        // Should have: 3 user + 1 Id + 2 missing audit (CreatedBy, ModifiedDate) = 6 total
        allProperties.Should().HaveCount(6);
        
        // User-provided should exist once
        allProperties.Count(p => p.Name == "CreatedDate").Should().Be(1);
        allProperties.Count(p => p.Name == "ModifiedBy").Should().Be(1);
        
        // Framework should generate missing ones
        allProperties.Should().Contain(p => p.Name == "CreatedBy");
        allProperties.Should().Contain(p => p.Name == "ModifiedDate");
    }

    #endregion

    #region Complex Pluralization Scenarios Tests

    [Theory]
    [InlineData("Category", "Categories")]
    [InlineData("Person", "People")]
    [InlineData("Address", "Addresses")]
    [InlineData("Company", "Companies")]
    [InlineData("Child", "Children")]
    public void ProcessEntity_ComplexPluralization_GeneratesCorrectTableNames(string className, string expectedTableName)
    {
        // Arrange
        EntityMetadata metadata = new EntityMetadata
        {
            ClassName = className,
            Properties = new List<PropertyDefinition>
            {
                new PropertyDefinition { Name = "Name", TypeName = "string" }
            }
        };

        // Act
        string tableName = TableNamePluralizer.Pluralize(metadata.ClassName);

        // Assert
        tableName.Should().Be(expectedTableName);
    }

    #endregion

    #region Zero-Configuration Entity Tests

    [Fact]
    public void ProcessEntity_ZeroAttributes_WorksWithJustInheritance()
    {
        // Arrange - Developer writes minimal POCO, framework does everything
        EntityMetadata metadata = new EntityMetadata
        {
            ClassName = "BlogPost",
            Properties = new List<PropertyDefinition>
            {
                new PropertyDefinition { Name = "Title", TypeName = "string" },
                new PropertyDefinition { Name = "Content", TypeName = "string" },
                new PropertyDefinition { Name = "PublishedDate", TypeName = "DateTime" }
            }
        };

        PropertyAutoGenerator generator = new PropertyAutoGenerator(metadata);

        // Act
        List<PropertyDefinition> allProperties = generator.GetAllProperties();
        string tableName = TableNamePluralizer.Pluralize(metadata.ClassName);
        string generatedCode = generator.GenerateAutoProperties();

        // Assert
        tableName.Should().Be("BlogPosts");
        allProperties.Should().HaveCount(4, "3 user + 1 auto-generated Id");
        generatedCode.Should().Contain("public int Id { get; set; }");
        generatedCode.Should().Contain("AUTO-GENERATED by HighSpeedDAL framework");
    }

    #endregion

    #region Developer Experience Tests

    [Fact]
    public void ProcessEntity_DeveloperProvidesEverything_FrameworkRespectsChoices()
    {
        // Arrange - Developer provides all properties manually
        EntityMetadata metadata = new EntityMetadata
        {
            ClassName = "CustomEntity",
            HasAuditAttribute = true,
            HasSoftDeleteAttribute = true,
            Properties = new List<PropertyDefinition>
            {
                new PropertyDefinition { Name = "Id", TypeName = "int", IsPrimaryKey = true },
                new PropertyDefinition { Name = "Name", TypeName = "string" },
                new PropertyDefinition { Name = "CreatedDate", TypeName = "DateTime" },
                new PropertyDefinition { Name = "CreatedBy", TypeName = "string" },
                new PropertyDefinition { Name = "ModifiedDate", TypeName = "DateTime" },
                new PropertyDefinition { Name = "ModifiedBy", TypeName = "string" },
                new PropertyDefinition { Name = "IsDeleted", TypeName = "bool" },
                new PropertyDefinition { Name = "DeletedDate", TypeName = "DateTime?" },
                new PropertyDefinition { Name = "DeletedBy", TypeName = "string" }
            }
        };

        PropertyAutoGenerator generator = new PropertyAutoGenerator(metadata);

        // Act
        string generatedCode = generator.GenerateAutoProperties();
        List<PropertyDefinition> allProperties = generator.GetAllProperties();

        // Assert
        generatedCode.Should().BeEmpty("user provided everything, nothing to generate");
        allProperties.Should().HaveCount(9, "all user-provided properties");
        
        // Verify no duplicates
        allProperties.Count(p => p.Name == "Id").Should().Be(1);
        allProperties.Count(p => p.Name == "CreatedDate").Should().Be(1);
        allProperties.Count(p => p.Name == "IsDeleted").Should().Be(1);
    }

    #endregion
}
