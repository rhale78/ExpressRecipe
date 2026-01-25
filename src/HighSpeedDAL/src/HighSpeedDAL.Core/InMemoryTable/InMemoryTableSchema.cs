using System;
using System.Collections.Generic;
using System.Linq;

namespace HighSpeedDAL.Core.InMemoryTable
{
    /// <summary>
    /// Defines the schema for an in-memory table.
    /// Contains column definitions, indexes, and constraints.
    /// </summary>
    public sealed class InMemoryTableSchema
    {
        private readonly string _tableName;
        private readonly List<ColumnDefinition> _columns;
        private readonly Dictionary<string, ColumnDefinition> _columnsByName;
        private readonly List<InMemoryIndex> _indexes;
        private readonly Dictionary<string, InMemoryIndex> _indexesByName;
        private ColumnDefinition? _primaryKeyColumn;
        private InMemoryIndex? _primaryKeyIndex;

        /// <summary>
        /// Name of the table
        /// </summary>
        public string TableName => _tableName;

        /// <summary>
        /// All columns in the table
        /// </summary>
        public IReadOnlyList<ColumnDefinition> Columns => _columns;

        /// <summary>
        /// All indexes on the table
        /// </summary>
        public IReadOnlyList<InMemoryIndex> Indexes => _indexes;

        /// <summary>
        /// The primary key column (null if no primary key)
        /// </summary>
        public ColumnDefinition? PrimaryKeyColumn => _primaryKeyColumn;

        /// <summary>
        /// The primary key index (null if no primary key)
        /// </summary>
        public InMemoryIndex? PrimaryKeyIndex => _primaryKeyIndex;

        public InMemoryTableSchema(string tableName)
        {
            _tableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
            _columns = [];
            _columnsByName = new Dictionary<string, ColumnDefinition>(StringComparer.OrdinalIgnoreCase);
            _indexes = [];
            _indexesByName = new Dictionary<string, InMemoryIndex>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Adds a column to the schema
        /// </summary>
        public InMemoryTableSchema AddColumn(ColumnDefinition column)
        {
            if (column == null)
            {
                throw new ArgumentNullException(nameof(column));
            }

            if (_columnsByName.ContainsKey(column.Name))
            {
                throw new InvalidOperationException($"Column '{column.Name}' already exists in table '{_tableName}'");
            }

            column.Ordinal = _columns.Count;
            _columns.Add(column);
            _columnsByName[column.Name] = column;

            if (column.IsPrimaryKey)
            {
                if (_primaryKeyColumn != null)
                {
                    throw new InvalidOperationException($"Table '{_tableName}' already has a primary key column '{_primaryKeyColumn.Name}'");
                }
                _primaryKeyColumn = column;

                // Create primary key index
                _primaryKeyIndex = new InMemoryIndex(
                    $"PK_{_tableName}",
                    new[] { column.Name },
                    isUnique: true,
                    isPrimaryKey: true);
                AddIndex(_primaryKeyIndex);
            }

            // Create index if column is indexed
            if (column.IsIndexed && !column.IsPrimaryKey)
            {
                string indexName = column.IndexName ?? $"IX_{_tableName}_{column.Name}";
                InMemoryIndex index = new InMemoryIndex(indexName, new[] { column.Name }, column.IsUnique);
                AddIndex(index);
            }

            return this;
        }

        /// <summary>
        /// Adds an index to the schema
        /// </summary>
        public InMemoryTableSchema AddIndex(InMemoryIndex index)
        {
            if (index == null)
            {
                throw new ArgumentNullException(nameof(index));
            }

            // Validate all columns exist
            foreach (string columnName in index.ColumnNames)
            {
                if (!HasColumn(columnName))
                {
                    throw new InvalidOperationException($"Column '{columnName}' does not exist in table '{_tableName}'");
                }
            }

            if (_indexesByName.ContainsKey(index.Name))
            {
                // Index already exists, skip
                return this;
            }

            _indexes.Add(index);
            _indexesByName[index.Name] = index;

            return this;
        }

        /// <summary>
        /// Gets a column by name
        /// </summary>
        public ColumnDefinition? GetColumn(string name)
        {
            return _columnsByName.GetValueOrDefault(name);
        }

        /// <summary>
        /// Checks if a column exists
        /// </summary>
        public bool HasColumn(string name)
        {
            return _columnsByName.ContainsKey(name);
        }

        /// <summary>
        /// Gets an index by name
        /// </summary>
        public InMemoryIndex? GetIndex(string name)
        {
            return _indexesByName.GetValueOrDefault(name);
        }

        /// <summary>
        /// Creates a schema from an entity type using reflection
        /// </summary>
        public static InMemoryTableSchema FromEntityType<TEntity>(string? tableName = null)
        {
            return FromEntityType(typeof(TEntity), tableName);
        }

        /// <summary>
        /// Creates a schema from an entity type using reflection
        /// </summary>
        public static InMemoryTableSchema FromEntityType(Type entityType, string? tableName = null)
        {
            tableName ??= entityType.Name + "s"; // Simple pluralization

            InMemoryTableSchema schema = new InMemoryTableSchema(tableName);

            System.Reflection.PropertyInfo[] properties = entityType.GetProperties(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            foreach (System.Reflection.PropertyInfo property in properties)
            {
                // Skip properties without getter/setter
                if (!property.CanRead || !property.CanWrite)
                {
                    continue;
                }

                // Skip [NotMapped] properties
                if (property.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.Schema.NotMappedAttribute), false).Any())
                {
                    continue;
                }

                ColumnDefinition column = CreateColumnFromProperty(property, tableName);

                // Initialize cached property accessors for performance
                column.InitializePropertyAccessors(entityType);

                schema.AddColumn(column);
            }

            return schema;
        }

        private static ColumnDefinition CreateColumnFromProperty(System.Reflection.PropertyInfo property, string tableName)
        {
            Type propertyType = property.PropertyType;
            Type underlyingType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
            bool isNullable = !propertyType.IsValueType || Nullable.GetUnderlyingType(propertyType) != null;

            ColumnDefinition column = new ColumnDefinition
            {
                Name = property.Name,
                PropertyName = property.Name,
                DataType = underlyingType,
                IsNullable = isNullable
            };

            // Check for [Key] attribute
            if (property.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.KeyAttribute), false).Any())
            {
                column.IsPrimaryKey = true;
                column.IsNullable = false;
            }
            // Convention: property named "Id" is primary key
            else if (property.Name.Equals("Id", StringComparison.OrdinalIgnoreCase))
            {
                column.IsPrimaryKey = true;
                column.IsNullable = false;
                if (underlyingType == typeof(int) || underlyingType == typeof(long))
                {
                    column.IsAutoIncrement = true;
                }
            }

            // Check for [MaxLength] or [StringLength] attribute
            object[] maxLengthAttrs = property.GetCustomAttributes(
                typeof(System.ComponentModel.DataAnnotations.MaxLengthAttribute), false);
            if (maxLengthAttrs.Length > 0)
            {
                column.MaxLength = ((System.ComponentModel.DataAnnotations.MaxLengthAttribute)maxLengthAttrs[0]).Length;
            }

            object[] stringLengthAttrs = property.GetCustomAttributes(
                typeof(System.ComponentModel.DataAnnotations.StringLengthAttribute), false);
            if (stringLengthAttrs.Length > 0)
            {
                column.MaxLength = ((System.ComponentModel.DataAnnotations.StringLengthAttribute)stringLengthAttrs[0]).MaximumLength;
            }

            // Check for [Required] attribute (makes non-nullable)
            if (property.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.RequiredAttribute), false).Any())
            {
                column.IsNullable = false;
            }

            // Determine SQL type
            column.SqlType = GetSqlType(underlyingType, column.MaxLength);

            return column;
        }

        private static string GetSqlType(Type type, int? maxLength)
        {
            if (type == typeof(int))
            {
                return "INT";
            }

            if (type == typeof(long))
            {
                return "BIGINT";
            }

            if (type == typeof(short))
            {
                return "SMALLINT";
            }

            if (type == typeof(byte))
            {
                return "TINYINT";
            }

            if (type == typeof(bool))
            {
                return "BIT";
            }

            if (type == typeof(decimal))
            {
                return "DECIMAL(18,2)";
            }

            if (type == typeof(float))
            {
                return "REAL";
            }

            if (type == typeof(double))
            {
                return "FLOAT";
            }

            if (type == typeof(DateTime))
            {
                return "DATETIME2";
            }

            if (type == typeof(DateTimeOffset))
            {
                return "DATETIMEOFFSET";
            }

            if (type == typeof(TimeSpan))
            {
                return "TIME";
            }

            return type == typeof(Guid)
                ? "UNIQUEIDENTIFIER"
                : type == typeof(byte[])
                ? maxLength.HasValue ? $"VARBINARY({maxLength})" : "VARBINARY(MAX)"
                : type == typeof(string) ? maxLength.HasValue ? $"NVARCHAR({maxLength})" : "NVARCHAR(MAX)" : "NVARCHAR(MAX)";
        }
    }
}
