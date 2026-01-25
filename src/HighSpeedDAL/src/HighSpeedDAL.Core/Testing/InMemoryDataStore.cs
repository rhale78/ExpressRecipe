using System;
using System.Collections.Generic;
using System.Linq;

namespace HighSpeedDAL.Core.Testing
{
    /// <summary>
    /// In-memory data store for unit testing without a database
    /// </summary>
    public class InMemoryDataStore
    {
        private readonly Dictionary<string, List<Dictionary<string, object?>>> _tables = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new();

        /// <summary>
        /// Creates a table with the specified schema
        /// </summary>
        public void CreateTable(string tableName, Dictionary<string, Type> columns)
        {
            lock (_lock)
            {
                if (_tables.ContainsKey(tableName))
                {
                    throw new InvalidOperationException($"Table {tableName} already exists");
                }

                _tables[tableName] = [];
            }
        }

        /// <summary>
        /// Inserts a row into the table
        /// </summary>
        public void Insert(string tableName, Dictionary<string, object?> row)
        {
            lock (_lock)
            {
                if (!_tables.TryGetValue(tableName, out List<Dictionary<string, object?>>? table))
                {
                    throw new InvalidOperationException($"Table {tableName} not found");
                }

                table.Add(new Dictionary<string, object?>(row, StringComparer.OrdinalIgnoreCase));
            }
        }

        /// <summary>
        /// Updates rows in the table matching the predicate
        /// </summary>
        public int Update(string tableName, Func<Dictionary<string, object?>, bool> predicate, Dictionary<string, object?> updates)
        {
            lock (_lock)
            {
                if (!_tables.TryGetValue(tableName, out List<Dictionary<string, object?>>? table))
                {
                    throw new InvalidOperationException($"Table {tableName} not found");
                }

                int count = 0;
                foreach (Dictionary<string, object?>? row in table.Where(predicate).ToList())
                {
                    foreach (KeyValuePair<string, object?> kvp in updates)
                    {
                        row[kvp.Key] = kvp.Value;
                    }
                    count++;
                }
                return count;
            }
        }

        /// <summary>
        /// Deletes rows from the table matching the predicate
        /// </summary>
        public int Delete(string tableName, Func<Dictionary<string, object?>, bool> predicate)
        {
            lock (_lock)
            {
                if (!_tables.TryGetValue(tableName, out List<Dictionary<string, object?>>? table))
                {
                    throw new InvalidOperationException($"Table {tableName} not found");
                }

                List<Dictionary<string, object?>> toRemove = table.Where(predicate).ToList();
                foreach (Dictionary<string, object?> row in toRemove)
                {
                    table.Remove(row);
                }
                return toRemove.Count;
            }
        }

        /// <summary>
        /// Selects rows from the table matching the predicate
        /// </summary>
        public List<Dictionary<string, object?>> Select(string tableName, Func<Dictionary<string, object?>, bool>? predicate = null)
        {
            lock (_lock)
            {
                return !_tables.TryGetValue(tableName, out List<Dictionary<string, object?>>? table)
                    ? throw new InvalidOperationException($"Table {tableName} not found")
                    : predicate == null
                    ? table.Select(row => new Dictionary<string, object?>(row, StringComparer.OrdinalIgnoreCase)).ToList()
                    : table
                    .Where(predicate)
                    .Select(row => new Dictionary<string, object?>(row, StringComparer.OrdinalIgnoreCase))
                    .ToList();
            }
        }

        /// <summary>
        /// Selects a single row from the table matching the predicate
        /// </summary>
        public Dictionary<string, object?>? SelectSingle(string tableName, Func<Dictionary<string, object?>, bool> predicate)
        {
            lock (_lock)
            {
                if (!_tables.TryGetValue(tableName, out List<Dictionary<string, object?>>? table))
                {
                    throw new InvalidOperationException($"Table {tableName} not found");
                }

                Dictionary<string, object?>? row = table.FirstOrDefault(predicate);
                return row != null ? new Dictionary<string, object?>(row, StringComparer.OrdinalIgnoreCase) : null;
            }
        }

        /// <summary>
        /// Counts rows in the table matching the predicate
        /// </summary>
        public int Count(string tableName, Func<Dictionary<string, object?>, bool>? predicate = null)
        {
            lock (_lock)
            {
                return !_tables.TryGetValue(tableName, out List<Dictionary<string, object?>>? table)
                    ? throw new InvalidOperationException($"Table {tableName} not found")
                    : predicate == null ? table.Count : table.Count(predicate);
            }
        }

        /// <summary>
        /// Checks if a row exists in the table matching the predicate
        /// </summary>
        public bool Exists(string tableName, Func<Dictionary<string, object?>, bool> predicate)
        {
            lock (_lock)
            {
                return !_tables.TryGetValue(tableName, out List<Dictionary<string, object?>>? table)
                    ? throw new InvalidOperationException($"Table {tableName} not found")
                    : table.Any(predicate);
            }
        }

        /// <summary>
        /// Clears all data from the table
        /// </summary>
        public void ClearTable(string tableName)
        {
            lock (_lock)
            {
                if (!_tables.TryGetValue(tableName, out List<Dictionary<string, object?>>? table))
                {
                    throw new InvalidOperationException($"Table {tableName} not found");
                }

                table.Clear();
            }
        }

        /// <summary>
        /// Gets the table data (for testing/debugging)
        /// </summary>
        public List<Dictionary<string, object?>> GetTable(string tableName)
        {
            lock (_lock)
            {
                return !_tables.TryGetValue(tableName, out List<Dictionary<string, object?>>? table)
                    ? throw new InvalidOperationException($"Table {tableName} not found")
                    : table.Select(row => new Dictionary<string, object?>(row, StringComparer.OrdinalIgnoreCase)).ToList();
            }
        }

        /// <summary>
        /// Gets all table names
        /// </summary>
        public IEnumerable<string> GetTableNames()
        {
            lock (_lock)
            {
                return _tables.Keys.ToList();
            }
        }

        /// <summary>
        /// Clears all tables and data
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _tables.Clear();
            }
        }
    }
}
