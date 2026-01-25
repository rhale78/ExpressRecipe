using System.Collections.Generic;
using System.Linq;
using HighSpeedDAL.SourceGenerators.Models;
using Microsoft.CodeAnalysis;

namespace HighSpeedDAL.SourceGenerators.Utilities
{
    /// <summary>
    /// Generates missing properties for entities with [AutoAudit] and [SoftDelete] attributes
    /// </summary>
    internal sealed class PropertyAutoGenerator
    {
        private readonly EntityMetadata _metadata;
        private readonly HashSet<string> _existingPropertyNames;

        public PropertyAutoGenerator(EntityMetadata metadata, INamedTypeSymbol? classSymbol = null)
        {
            _metadata = metadata;

            // If class symbol provided, use ACTUAL declared properties
            // Otherwise fall back to metadata properties (which may include auto-generated ones)
            if (classSymbol != null)
            {
                _existingPropertyNames = new HashSet<string>(
                    classSymbol.GetMembers()
                        .OfType<IPropertySymbol>()
                        .Where(p => p.DeclaredAccessibility == Accessibility.Public)
                        .Select(p => p.Name),
                    System.StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                _existingPropertyNames = new HashSet<string>(
                    metadata.Properties.Select(p => p.PropertyName),
                    System.StringComparer.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// Gets list of properties that need to be auto-generated
        /// </summary>
        public List<PropertyMetadata> GetMissingProperties()
        {
            List<PropertyMetadata> missingProperties = [];

            // Generate primary key if missing (must be first)
            PropertyMetadata? missingPrimaryKey = GetMissingPrimaryKey();
            if (missingPrimaryKey != null)
            {
                missingProperties.Add(missingPrimaryKey);
            }

            // Generate audit properties if [AutoAudit] present
            if (_metadata.IsAuditable)
            {
                missingProperties.AddRange(GetMissingAuditProperties());
            }

            // Generate soft delete properties if [SoftDelete] present
            if (_metadata.HasSoftDelete)
            {
                missingProperties.AddRange(GetMissingSoftDeleteProperties());
            }

            return missingProperties;
        }

        private PropertyMetadata? GetMissingPrimaryKey()
        {
            // Only generate Id if the entity doesn't already have it defined
            if (!PropertyExists("Id"))
            {
                // Determine type and auto-increment based on PrimaryKeyType from [Table] attribute
                string idType = _metadata.PrimaryKeyType == "Guid" ? "Guid" : "int";
                bool isAutoIncrement = _metadata.PrimaryKeyType != "Guid"; // Int is auto-increment, Guid is not

                return new PropertyMetadata
                {
                    PropertyName = "Id",
                    PropertyType = idType,
                    ColumnName = "Id",
                    IsPrimaryKey = true,
                    IsAutoIncrement = isAutoIncrement,
                    IsNullable = false,
                    IsNotMapped = false
                };
            }

            return null;
        }

        private List<PropertyMetadata> GetMissingAuditProperties()
        {
            List<PropertyMetadata> properties = [];

            // CreatedDate
            if (!PropertyExists("CreatedDate"))
            {
                properties.Add(new PropertyMetadata
                {
                    PropertyName = "CreatedDate",
                    PropertyType = "DateTime",
                    ColumnName = "CreatedDate",
                    IsNullable = false,
                    IsNotMapped = false
                });
            }

            // CreatedBy
            if (!PropertyExists("CreatedBy"))
            {
                properties.Add(new PropertyMetadata
                {
                    PropertyName = "CreatedBy",
                    PropertyType = "string",
                    ColumnName = "CreatedBy",
                    IsNullable = false,
                    MaxLength = 256,
                    IsNotMapped = false
                });
            }

            // ModifiedDate
            if (!PropertyExists("ModifiedDate"))
            {
                properties.Add(new PropertyMetadata
                {
                    PropertyName = "ModifiedDate",
                    PropertyType = "DateTime",
                    ColumnName = "ModifiedDate",
                    IsNullable = false,
                    IsNotMapped = false
                });
            }

            // ModifiedBy
            if (!PropertyExists("ModifiedBy"))
            {
                properties.Add(new PropertyMetadata
                {
                    PropertyName = "ModifiedBy",
                    PropertyType = "string",
                    ColumnName = "ModifiedBy",
                    IsNullable = false,
                    MaxLength = 256,
                    IsNotMapped = false
                });
            }

            return properties;
        }

        private List<PropertyMetadata> GetMissingSoftDeleteProperties()
        {
            List<PropertyMetadata> properties = [];

            // Get custom column names from attribute (if specified)
            string isDeletedColumn = _metadata.SoftDeleteColumn ?? "IsDeleted";
            string deletedDateColumn = _metadata.SoftDeleteDateColumn ?? "DeletedDate";

            // IsDeleted
            if (!PropertyExists("IsDeleted"))
            {
                properties.Add(new PropertyMetadata
                {
                    PropertyName = "IsDeleted",
                    PropertyType = "bool",
                    ColumnName = isDeletedColumn,
                    IsNullable = false,
                    IsNotMapped = false
                });
            }

            // DeletedDate
            if (!PropertyExists("DeletedDate"))
            {
                properties.Add(new PropertyMetadata
                {
                    PropertyName = "DeletedDate",
                    PropertyType = "DateTime?",
                    ColumnName = deletedDateColumn,
                    IsNullable = true,
                    IsNotMapped = false
                });
            }

            // DeletedBy (optional - always generate if missing)
            if (!PropertyExists("DeletedBy"))
            {
                properties.Add(new PropertyMetadata
                {
                    PropertyName = "DeletedBy",
                    PropertyType = "string?",
                    ColumnName = "DeletedBy",
                    IsNullable = true,
                    MaxLength = 256,
                    IsNotMapped = false
                });
            }

            return properties;
        }

        private bool PropertyExists(string propertyName)
        {
            return _existingPropertyNames.Contains(propertyName);
        }

        /// <summary>
        /// Checks if any properties need to be generated
        /// </summary>
        public bool HasMissingProperties()
        {
            return GetMissingProperties().Count > 0;
        }
    }
}
