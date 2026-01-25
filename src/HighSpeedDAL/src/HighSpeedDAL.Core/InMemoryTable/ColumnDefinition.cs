using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace HighSpeedDAL.Core.InMemoryTable
{
    /// <summary>
    /// Defines metadata for a column in an in-memory table.
    /// Used for validation, constraint enforcement, and SQL generation.
    /// Includes cached property accessors for high-performance operations.
    /// </summary>
    public sealed class ColumnDefinition
    {
        /// <summary>
        /// Column name (matches property name by default, or [Column] attribute value)
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Property name in the entity class
        /// </summary>
        public string PropertyName { get; set; } = string.Empty;

        /// <summary>
        /// .NET type of the column (typeof(int), typeof(string), etc.)
        /// </summary>
        public Type DataType { get; set; } = typeof(object);

        /// <summary>
        /// SQL type name (INT, NVARCHAR, DATETIME2, etc.)
        /// </summary>
        public string SqlType { get; set; } = string.Empty;

        /// <summary>
        /// Cached property getter delegate for high-performance property access.
        /// Avoids reflection on every read operation.
        /// </summary>
        internal Func<object, object?>? PropertyGetter { get; private set; }

        /// <summary>
        /// Cached property setter delegate for high-performance property modification.
        /// Avoids reflection on every write operation.
        /// </summary>
        internal Action<object, object?>? PropertySetter { get; private set; }

        /// <summary>
        /// Maximum length for string/binary columns. Null for non-length types.
        /// </summary>
        public int? MaxLength { get; set; }

        /// <summary>
        /// Numeric precision for decimal types
        /// </summary>
        public int? Precision { get; set; }

        /// <summary>
        /// Numeric scale for decimal types
        /// </summary>
        public int? Scale { get; set; }

        /// <summary>
        /// Whether the column allows NULL values
        /// </summary>
        public bool IsNullable { get; set; }

        /// <summary>
        /// Whether this column is the primary key
        /// </summary>
        public bool IsPrimaryKey { get; set; }

        /// <summary>
        /// Whether this column auto-increments (for primary keys)
        /// </summary>
        public bool IsAutoIncrement { get; set; }

        /// <summary>
        /// Whether this column is indexed (non-unique)
        /// </summary>
        public bool IsIndexed { get; set; }

        /// <summary>
        /// Whether this column has a unique constraint
        /// </summary>
        public bool IsUnique { get; set; }

        /// <summary>
        /// Index name if column is part of an index
        /// </summary>
        public string? IndexName { get; set; }

        /// <summary>
        /// Default value expression (for SQL generation)
        /// </summary>
        public object? DefaultValue { get; set; }

        /// <summary>
        /// Order of the column in the table schema
        /// </summary>
        public int Ordinal { get; set; }

        /// <summary>
        /// Validates a value against this column's constraints.
        /// </summary>
        /// <param name="value">The value to validate</param>
        /// <returns>Validation result with any error messages</returns>
        public ValidationResult Validate(object? value)
        {
            List<string> errors = [];

            // Null check
            if (value == null || value == DBNull.Value)
            {
                if (!IsNullable)
                {
                    errors.Add($"Column '{Name}' does not allow NULL values");
                }
                return new ValidationResult(errors.Count == 0, errors);
            }

            // Type check
            Type valueType = value.GetType();
            if (!IsCompatibleType(valueType))
            {
                errors.Add($"Column '{Name}' expects type '{DataType.Name}' but got '{valueType.Name}'");
                return new ValidationResult(false, errors);
            }

            // String length check
            if (DataType == typeof(string) && MaxLength.HasValue)
            {
                string strValue = (string)value;
                if (strValue.Length > MaxLength.Value)
                {
                    errors.Add($"Column '{Name}' value exceeds maximum length of {MaxLength.Value} (actual: {strValue.Length})");
                }
            }

            // Byte array length check
            if (DataType == typeof(byte[]) && MaxLength.HasValue)
            {
                byte[] bytes = (byte[])value;
                if (bytes.Length > MaxLength.Value)
                {
                    errors.Add($"Column '{Name}' binary data exceeds maximum length of {MaxLength.Value} (actual: {bytes.Length})");
                }
            }

            // Decimal precision/scale check
            if (DataType == typeof(decimal) && Precision.HasValue)
            {
                decimal decValue = (decimal)value;
                string decStr = decValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
            
                // Remove negative sign and decimal point for counting
                string digitsOnly = decStr.Replace("-", "").Replace(".", "");
                int totalDigits = digitsOnly.Length;
            
                int decimalPlaces = 0;
                int dotIndex = decStr.IndexOf('.');
                if (dotIndex >= 0)
                {
                    decimalPlaces = decStr.Length - dotIndex - 1;
                }

                if (totalDigits > Precision.Value)
                {
                    errors.Add($"Column '{Name}' decimal value exceeds precision of {Precision.Value}");
                }

                if (Scale.HasValue && decimalPlaces > Scale.Value)
                {
                    errors.Add($"Column '{Name}' decimal value exceeds scale of {Scale.Value}");
                }
            }

            return new ValidationResult(errors.Count == 0, errors);
        }

        /// <summary>
        /// Checks if the given type is compatible with this column's data type.
        /// </summary>
        private bool IsCompatibleType(Type valueType)
        {
            // Exact match
            if (valueType == DataType)
            {
                return true;
            }

            // Nullable type match
            Type? underlyingType = Nullable.GetUnderlyingType(DataType);
            if (underlyingType != null && valueType == underlyingType)
            {
                return true;
            }

            // Numeric type compatibility
            if (IsNumericType(DataType) && IsNumericType(valueType))
            {
                // Allow implicit numeric conversions
                return CanConvertNumeric(valueType, DataType);
            }

            // DateTime compatibility
            return (DataType == typeof(DateTime) || DataType == typeof(DateTimeOffset)) &&
                (valueType == typeof(DateTime) || valueType == typeof(DateTimeOffset));
        }

        private static bool IsNumericType(Type type)
        {
            Type checkType = Nullable.GetUnderlyingType(type) ?? type;
            return checkType == typeof(byte) ||
                   checkType == typeof(sbyte) ||
                   checkType == typeof(short) ||
                   checkType == typeof(ushort) ||
                   checkType == typeof(int) ||
                   checkType == typeof(uint) ||
                   checkType == typeof(long) ||
                   checkType == typeof(ulong) ||
                   checkType == typeof(float) ||
                   checkType == typeof(double) ||
                   checkType == typeof(decimal);
        }

        private static bool CanConvertNumeric(Type from, Type to)
        {
            Type fromType = Nullable.GetUnderlyingType(from) ?? from;
            Type toType = Nullable.GetUnderlyingType(to) ?? to;

            // Define numeric type hierarchy
            Dictionary<Type, int> typeOrder = new Dictionary<Type, int>
            {
                { typeof(byte), 1 },
                { typeof(sbyte), 2 },
                { typeof(short), 3 },
                { typeof(ushort), 4 },
                { typeof(int), 5 },
                { typeof(uint), 6 },
                { typeof(long), 7 },
                { typeof(ulong), 8 },
                { typeof(float), 9 },
                { typeof(double), 10 },
                { typeof(decimal), 11 }
            };

            if (typeOrder.TryGetValue(fromType, out int fromOrder) &&
                typeOrder.TryGetValue(toType, out int toOrder))
            {
                // Allow widening conversions
                return fromOrder <= toOrder;
            }

            return false;
        }

        /// <summary>
        /// Converts a value to this column's data type.
        /// </summary>
        public object? ConvertValue(object? value)
        {
            if (value == null || value == DBNull.Value)
            {
                return IsNullable ? null : throw new InvalidOperationException($"Column '{Name}' cannot be NULL");
            }

            try
            {
                Type targetType = Nullable.GetUnderlyingType(DataType) ?? DataType;
                return Convert.ChangeType(value, targetType, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                throw new InvalidCastException($"Cannot convert value to type '{DataType.Name}' for column '{Name}'", ex);
            }
        }

        /// <summary>
        /// Initializes cached property accessors using compiled Expression trees.
        /// This is called once during schema initialization to avoid reflection in hot paths.
        /// </summary>
        /// <param name="entityType">The entity type that contains this property</param>
        internal void InitializePropertyAccessors(Type entityType)
        {
            PropertyInfo? property = entityType.GetProperty(PropertyName);
            if (property == null)
            {
                return; // Property not found, accessors remain null
            }

            // Create getter delegate: Func<object, object?>
            if (property.CanRead)
            {
                try
                {
                    // Parameter: object instance
                    ParameterExpression instanceParam = Expression.Parameter(typeof(object), "instance");

                    // Cast instance to entity type
                    UnaryExpression typedInstance = Expression.Convert(instanceParam, entityType);

                    // Access property
                    MemberExpression propertyAccess = Expression.Property(typedInstance, property);

                    // Box result to object
                    UnaryExpression boxed = Expression.Convert(propertyAccess, typeof(object));

                    // Compile to delegate
                    PropertyGetter = Expression.Lambda<Func<object, object?>>(boxed, instanceParam).Compile();
                }
                catch
                {
                    // If compilation fails, leave getter as null (will fall back to reflection)
                    PropertyGetter = null;
                }
            }

            // Create setter delegate: Action<object, object?>
            if (property.CanWrite)
            {
                try
                {
                    // Parameters: object instance, object value
                    ParameterExpression instanceParam = Expression.Parameter(typeof(object), "instance");
                    ParameterExpression valueParam = Expression.Parameter(typeof(object), "value");

                    // Cast instance to entity type
                    UnaryExpression typedInstance = Expression.Convert(instanceParam, entityType);

                    // Cast value to property type
                    UnaryExpression typedValue = Expression.Convert(valueParam, property.PropertyType);

                    // Assign property
                    BinaryExpression assignment = Expression.Assign(
                        Expression.Property(typedInstance, property),
                        typedValue);

                    // Compile to delegate
                    PropertySetter = Expression.Lambda<Action<object, object?>>(assignment, instanceParam, valueParam).Compile();
                }
                catch
                {
                    // If compilation fails, leave setter as null (will fall back to reflection)
                    PropertySetter = null;
                }
            }
        }

        /// <summary>
        /// Gets the property value from an entity instance using cached accessor.
        /// Falls back to reflection if accessor is not available.
        /// </summary>
        internal object? GetPropertyValue(object entity)
        {
            if (PropertyGetter != null)
            {
                return PropertyGetter(entity);
            }

            // Fallback to reflection
            PropertyInfo? property = entity.GetType().GetProperty(PropertyName);
            return property?.GetValue(entity);
        }

        /// <summary>
        /// Sets the property value on an entity instance using cached accessor.
        /// Falls back to reflection if accessor is not available.
        /// </summary>
        internal void SetPropertyValue(object entity, object? value)
        {
            if (PropertySetter != null)
            {
                PropertySetter(entity, value);
                return;
            }

            // Fallback to reflection
            PropertyInfo? property = entity.GetType().GetProperty(PropertyName);
            property?.SetValue(entity, value);
        }
    }

    /// <summary>
    /// Result of a validation operation
    /// </summary>
    public sealed class ValidationResult
    {
        public bool IsValid { get; }
        public IReadOnlyList<string> Errors { get; }

        public ValidationResult(bool isValid, IEnumerable<string>? errors = null)
        {
            IsValid = isValid;
            Errors = (errors as IReadOnlyList<string>) ?? new List<string>(errors ?? Array.Empty<string>());
        }

        public static ValidationResult Success { get; } = new ValidationResult(true);

        public static ValidationResult Failure(string error) => new ValidationResult(false, new[] { error });
        public static ValidationResult Failure(IEnumerable<string> errors) => new ValidationResult(false, errors);
    }
}
