using System;
using System.Collections.Generic;

namespace HighSpeedDAL.Core.InMemoryTable
{
    /// <summary>
    /// Represents a single row in an in-memory table.
    /// Stores column values in a dictionary for flexible schema support.
    /// </summary>
    public sealed class InMemoryRow
    {
        private readonly Dictionary<string, object?> _values;
        private readonly InMemoryTableSchema _schema;
        private RowState _state;
        private DateTime _createdAt;
        private DateTime _modifiedAt;

        /// <summary>
        /// Current state of the row (New, Modified, Deleted, Unchanged)
        /// </summary>
        public RowState State
        {
            get => _state;
            internal set
            {
                _state = value;
                if (value != RowState.Unchanged)
                {
                    _modifiedAt = DateTime.UtcNow;
                }
            }
        }

        /// <summary>
        /// When the row was created in memory
        /// </summary>
        public DateTime CreatedAt => _createdAt;

        /// <summary>
        /// When the row was last modified
        /// </summary>
        public DateTime ModifiedAt => _modifiedAt;

        /// <summary>
        /// Gets the primary key value for this row
        /// </summary>
        public object? PrimaryKeyValue => _schema.PrimaryKeyColumn != null 
            ? _values.GetValueOrDefault(_schema.PrimaryKeyColumn.Name) 
            : null;

        /// <summary>
        /// Creates a new row with the given schema
        /// </summary>
        public InMemoryRow(InMemoryTableSchema schema)
        {
            _schema = schema ?? throw new ArgumentNullException(nameof(schema));
            _values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            _state = RowState.New;
            _createdAt = DateTime.UtcNow;
            _modifiedAt = _createdAt;

            // Initialize with default values
            foreach (ColumnDefinition column in _schema.Columns)
            {
                if (column.DefaultValue != null)
                {
                    _values[column.Name] = column.DefaultValue;
                }
                else if (column.IsNullable)
                {
                    _values[column.Name] = null;
                }
            }
        }

        /// <summary>
        /// Gets a column value by name
        /// </summary>
        public object? this[string columnName]
        {
            get
            {
                return !_schema.HasColumn(columnName)
                    ? throw new ArgumentException($"Column '{columnName}' does not exist in table schema")
                    : _values.GetValueOrDefault(columnName);
            }
            set
            {
                SetValue(columnName, value);
            }
        }

        /// <summary>
        /// Gets a column value by index
        /// </summary>
        public object? this[int ordinal]
        {
            get
            {
                return ordinal < 0 || ordinal >= _schema.Columns.Count
                    ? throw new ArgumentOutOfRangeException(nameof(ordinal))
                    : _values.GetValueOrDefault(_schema.Columns[ordinal].Name);
            }
            set
            {
                if (ordinal < 0 || ordinal >= _schema.Columns.Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(ordinal));
                }
                SetValue(_schema.Columns[ordinal].Name, value);
            }
        }

        /// <summary>
        /// Sets a column value with validation
        /// </summary>
        public void SetValue(string columnName, object? value)
        {
            ColumnDefinition? column = _schema.GetColumn(columnName);
            if (column == null)
            {
                throw new ArgumentException($"Column '{columnName}' does not exist in table schema");
            }

            // Skip validation for auto-increment columns being set internally
            if (column.IsAutoIncrement && column.IsPrimaryKey && _state == RowState.New)
            {
                _values[columnName] = value;
                return;
            }

            // Validate the value
            ValidationResult validation = column.Validate(value);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException(
                    $"Validation failed for column '{columnName}': {string.Join("; ", validation.Errors)}");
            }

            // Convert and store
            object? convertedValue = column.ConvertValue(value);
        
            object? oldValue = _values.GetValueOrDefault(columnName);
            _values[columnName] = convertedValue;

            // Track modification if value changed
            if (_state == RowState.Unchanged && !Equals(oldValue, convertedValue))
            {
                _state = RowState.Modified;
                _modifiedAt = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Gets a strongly-typed column value
        /// </summary>
        public T? GetValue<T>(string columnName)
        {
            object? value = this[columnName];
            if (value == null || value == DBNull.Value)
            {
                return default;
            }

            if (value is T typedValue)
            {
                return typedValue;
            }

            try
            {
                return (T)Convert.ChangeType(value, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
            }
            catch
            {
                return default;
            }
        }

        /// <summary>
        /// Checks if a column has a value (not null)
        /// </summary>
        public bool HasValue(string columnName)
        {
            return _values.TryGetValue(columnName, out object? value) && value != null && value != DBNull.Value;
        }

        /// <summary>
        /// Gets all column names with values
        /// </summary>
        public IEnumerable<string> GetColumnNames()
        {
            return _values.Keys;
        }

        /// <summary>
        /// Gets all values as a dictionary
        /// </summary>
        public IReadOnlyDictionary<string, object?> GetValues()
        {
            return _values;
        }

        /// <summary>
        /// Validates all values in the row against the schema
        /// </summary>
        public ValidationResult Validate()
        {
            List<string> errors = [];

            foreach (ColumnDefinition column in _schema.Columns)
            {
                // Skip auto-increment primary keys for new rows
                if (column.IsAutoIncrement && column.IsPrimaryKey && _state == RowState.New)
                {
                    continue;
                }

                object? value = _values.GetValueOrDefault(column.Name);
                ValidationResult result = column.Validate(value);
            
                if (!result.IsValid)
                {
                    errors.AddRange(result.Errors);
                }
            }

            return new ValidationResult(errors.Count == 0, errors);
        }

        /// <summary>
        /// Marks the row as deleted
        /// </summary>
        public void Delete()
        {
            _state = RowState.Deleted;
            _modifiedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Accepts changes, marking the row as unchanged
        /// </summary>
        public void AcceptChanges()
        {
            if (_state != RowState.Deleted)
            {
                _state = RowState.Unchanged;
            }
        }

        /// <summary>
        /// Creates a copy of this row
        /// </summary>
        public InMemoryRow Clone()
        {
            InMemoryRow clone = new InMemoryRow(_schema);
            foreach (KeyValuePair<string, object?> kvp in _values)
            {
                clone._values[kvp.Key] = kvp.Value;
            }
            clone._state = _state;
            clone._createdAt = _createdAt;
            clone._modifiedAt = _modifiedAt;
            return clone;
        }

        /// <summary>
        /// Converts the row to an entity instance using cached property accessors for performance.
        /// </summary>
        public TEntity ToEntity<TEntity>() where TEntity : class, new()
        {
            TEntity entity = new TEntity();

            foreach (ColumnDefinition column in _schema.Columns)
            {
                object? value = _values.GetValueOrDefault(column.Name);
                if (value == null || value == DBNull.Value)
                {
                    continue;
                }

                try
                {
                    object? convertedValue = column.ConvertValue(value);

                    // Use cached property setter (avoids reflection)
                    column.SetPropertyValue(entity, convertedValue);
                }
                catch
                {
                    // Skip properties that can't be set
                }
            }

            return entity;
        }

        /// <summary>
        /// Populates the row from an entity instance using cached property accessors for performance.
        /// </summary>
        public void FromEntity<TEntity>(TEntity entity) where TEntity : class
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            foreach (ColumnDefinition column in _schema.Columns)
            {
                // Use cached property getter (avoids reflection)
                object? value = column.GetPropertyValue(entity);

                // Skip auto-increment primary keys with default values
                if (column.IsAutoIncrement && column.IsPrimaryKey)
                {
                    if (value == null || (value is int intVal && intVal == 0) || (value is long longVal && longVal == 0))
                    {
                        continue;
                    }
                }

                _values[column.Name] = value;
            }
        }

        public override string ToString()
        {
            object? pk = PrimaryKeyValue;
            return $"Row[{_state}] PK={pk ?? "null"} Columns={_values.Count}";
        }
    }

    /// <summary>
    /// State of a row in the in-memory table
    /// </summary>
    public enum RowState
    {
        /// <summary>
        /// Row is newly created, not yet flushed
        /// </summary>
        New,

        /// <summary>
        /// Row has been modified since last flush
        /// </summary>
        Modified,

        /// <summary>
        /// Row has been marked for deletion
        /// </summary>
        Deleted,

        /// <summary>
        /// Row is unchanged since last flush
        /// </summary>
        Unchanged
    }
}
