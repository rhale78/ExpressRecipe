using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ExpressRecipe.Data.Common
{
    /// <summary>
    /// Tracks which properties have changed in an entity to support conditional updates.
    /// Enables updating only changed fields instead of all fields, reducing query payload and I/O.
    ///
    /// Usage:
    /// var entity = new Product { Name = "Original", Brand = "BrandA" };
    /// var tracker = new EntityChangeTracker(entity);
    ///
    /// entity.Name = "Updated";
    /// entity.Brand = "BrandB";
    ///
    /// var changedFields = tracker.GetChangedFields();
    /// // Update only the changed fields in the database
    /// </summary>
    public sealed class EntityChangeTracker
    {
        private readonly object _entity;
        private readonly Dictionary<string, object?> _originalValues;
        private readonly HashSet<string> _trackedProperties;

        /// <summary>
        /// Creates a change tracker for an entity, capturing current state.
        /// </summary>
        public EntityChangeTracker(object entity)
        {
            _entity = entity ?? throw new ArgumentNullException(nameof(entity));
            _trackedProperties = GetPublicProperties(entity).ToHashSet();
            _originalValues = CaptureCurrentValues();
        }

        /// <summary>
        /// Creates a change tracker for an entity, tracking only specified properties.
        /// </summary>
        public EntityChangeTracker(object entity, params string[] propertiesToTrack)
        {
            _entity = entity ?? throw new ArgumentNullException(nameof(entity));
            _trackedProperties = new HashSet<string>(propertiesToTrack);
            _originalValues = CaptureCurrentValues();
        }

        /// <summary>
        /// Gets all properties that have changed since creation.
        /// </summary>
        public IReadOnlyList<string> GetChangedProperties()
        {
            List<string> changed = [];
            Dictionary<string, object?> currentValues = GetCurrentValues();

            foreach (var property in _trackedProperties)
            {
                if (!_originalValues.TryGetValue(property, out var originalValue))
                {
                    continue;
                }

                if (!currentValues.TryGetValue(property, out var currentValue))
                {
                    continue;
                }

                // Compare using proper equality
                if (!Equals(originalValue, currentValue))
                {
                    changed.Add(property);
                }
            }

            return changed;
        }

        /// <summary>
        /// Gets all properties with their changed values (original → current).
        /// </summary>
        public IReadOnlyDictionary<string, (object? Original, object? Current)> GetChangedPropertyValues()
        {
            Dictionary<string, (object?, object?)> result = [];
            Dictionary<string, object?> currentValues = GetCurrentValues();

            foreach (var property in _trackedProperties)
            {
                if (!_originalValues.TryGetValue(property, out var originalValue))
                {
                    continue;
                }

                if (!currentValues.TryGetValue(property, out var currentValue))
                {
                    continue;
                }

                if (!Equals(originalValue, currentValue))
                {
                    result[property] = (originalValue, currentValue);
                }
            }

            return result;
        }

        /// <summary>
        /// Checks if a specific property has changed.
        /// </summary>
        public bool HasPropertyChanged(string propertyName)
        {
            if (!_originalValues.TryGetValue(propertyName, out var originalValue))
            {
                return false;
            }

            PropertyInfo? prop = _entity.GetType().GetProperty(propertyName);
            if (prop == null)
            {
                return false;
            }

            var currentValue = prop.GetValue(_entity);
            return !Equals(originalValue, currentValue);
        }

        /// <summary>
        /// Checks if any properties have changed.
        /// </summary>
        public bool HasAnyChanges => GetChangedProperties().Count > 0;

        /// <summary>
        /// Resets the tracker to the current state (marks all as original).
        /// Useful after a successful save operation.
        /// </summary>
        public void MarkAsUnchanged()
        {
            _originalValues.Clear();
            foreach (KeyValuePair<string, object?> kvp in GetCurrentValues())
            {
                _originalValues[kvp.Key] = kvp.Value;
            }
        }

        /// <summary>
        /// Resets specific properties as unchanged.
        /// </summary>
        public void MarkPropertiesAsUnchanged(params string[] propertyNames)
        {
            Dictionary<string, object?> currentValues = GetCurrentValues();
            foreach (var propertyName in propertyNames)
            {
                if (currentValues.TryGetValue(propertyName, out var value))
                {
                    _originalValues[propertyName] = value;
                }
            }
        }

        /// <summary>
        /// Gets the original value of a property (at tracker creation time).
        /// </summary>
        public object? GetOriginalValue(string propertyName)
        {
            return _originalValues.TryGetValue(propertyName, out var value) ? value : null;
        }

        /// <summary>
        /// Gets the current value of a property.
        /// </summary>
        public object? GetCurrentValue(string propertyName)
        {
            PropertyInfo? prop = _entity.GetType().GetProperty(propertyName);
            return prop?.GetValue(_entity);
        }

        private Dictionary<string, object?> CaptureCurrentValues()
        {
            return GetCurrentValues();
        }

        private Dictionary<string, object?> GetCurrentValues()
        {
            Dictionary<string, object?> values = [];
            Type entityType = _entity.GetType();

            foreach (var propertyName in _trackedProperties)
            {
                PropertyInfo? prop = entityType.GetProperty(propertyName);
                if (prop != null && prop.CanRead)
                {
                    values[propertyName] = prop.GetValue(_entity);
                }
            }

            return values;
        }

        private static IEnumerable<string> GetPublicProperties(object entity)
        {
            return entity.GetType()
                .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite)
                .Select(p => p.Name);
        }
    }

    /// <summary>
    /// Helper for building conditional UPDATE statements based on changed fields.
    /// </summary>
    public sealed class ConditionalUpdateBuilder
    {
        private readonly string _tableName;
        private readonly List<string> _updateClauses = [];
        private readonly List<(string Column, object? Value)> _parameters = [];
        private string? _whereClause;

        public ConditionalUpdateBuilder(string tableName)
        {
            _tableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
        }

        /// <summary>
        /// Adds a changed field to the UPDATE clause.
        /// </summary>
        public ConditionalUpdateBuilder AddChangedField(string columnName, object? value)
        {
            _updateClauses.Add($"[{columnName}] = @{columnName}");
            _parameters.Add((columnName, value));
            return this;
        }

        /// <summary>
        /// Adds multiple changed fields from a change tracker.
        /// </summary>
        public ConditionalUpdateBuilder AddChangedFieldsFromTracker(EntityChangeTracker tracker,
            Func<string, string> propertyToColumnMapper)
        {
            IReadOnlyDictionary<string, (object? Original, object? Current)> changedValues = tracker.GetChangedPropertyValues();
            foreach (KeyValuePair<string, (object? Original, object? Current)> kvp in changedValues)
            {
                var columnName = propertyToColumnMapper(kvp.Key);
                AddChangedField(columnName, kvp.Value.Current);
            }
            return this;
        }

        /// <summary>
        /// Sets the WHERE clause for the update.
        /// </summary>
        public ConditionalUpdateBuilder Where(string whereClause)
        {
            _whereClause = whereClause;
            return this;
        }

        /// <summary>
        /// Builds the SQL UPDATE statement.
        /// </summary>
        public string Build()
        {
            if (_updateClauses.Count == 0)
            {
                throw new InvalidOperationException("No fields to update");
            }

            if (string.IsNullOrWhiteSpace(_whereClause))
            {
                throw new InvalidOperationException("WHERE clause is required for safety");
            }

            var sql = $"UPDATE [{_tableName}]\nSET {string.Join(", ", _updateClauses)}\n{_whereClause}";
            return sql;
        }

        /// <summary>
        /// Gets the parameters for the SQL statement.
        /// </summary>
        public IReadOnlyList<(string ParameterName, object? Value)> GetParameters()
        {
            return _parameters.AsReadOnly();
        }

        /// <summary>
        /// Checks if there are any fields to update.
        /// </summary>
        public bool HasChanges => _updateClauses.Count > 0;
    }
}
