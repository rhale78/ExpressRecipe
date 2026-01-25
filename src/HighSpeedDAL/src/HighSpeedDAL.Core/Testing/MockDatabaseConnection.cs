using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace HighSpeedDAL.Core.Testing
{
    /// <summary>
    /// Mock database connection for unit testing without a real database
    /// </summary>
    public class MockDatabaseConnection : IDbConnection
    {
        private readonly InMemoryDataStore _dataStore;
        private readonly List<(string sql, Dictionary<string, object?> parameters)> _executedQueries = [];
        private int _nextAutoIncrementId = 1;
        private ConnectionState _state = ConnectionState.Closed;

        public MockDatabaseConnection(InMemoryDataStore dataStore)
        {
            _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
        }

        #region IDbConnection Implementation

        public string ConnectionString { get; set; } = string.Empty;
        public int ConnectionTimeout => 30;
        public string Database => "mock";
        public ConnectionState State => _state;

        public IDbTransaction BeginTransaction()
        {
            return new MockDbTransaction(this);
        }

        public IDbTransaction BeginTransaction(IsolationLevel il)
        {
            return new MockDbTransaction(this);
        }

        public void ChangeDatabase(string databaseName)
        {
            // No-op for mock
        }

        public void Close()
        {
            _state = ConnectionState.Closed;
        }

        public IDbCommand CreateCommand()
        {
            return new MockDbCommand(this);
        }

        public void Dispose()
        {
            Close();
        }

        public void Open()
        {
            _state = ConnectionState.Open;
        }

        #endregion

        #region Mock-Specific Methods

        /// <summary>
        /// Executes a SQL command and returns the number of rows affected
        /// </summary>
        public async Task<int> ExecuteNonQueryAsync(string sql, Dictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default)
        {
            parameters ??= [];
            _executedQueries.Add((sql, parameters));
            await Task.Yield();

            return sql.StartsWith("INSERT", StringComparison.OrdinalIgnoreCase)
                ? ExecuteInsert(sql, parameters)
                : sql.StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase)
                ? ExecuteUpdate(sql, parameters)
                : sql.StartsWith("DELETE", StringComparison.OrdinalIgnoreCase) ? ExecuteDelete(sql, parameters) : 0;
        }

        /// <summary>
        /// Executes a SQL query and returns a single scalar value
        /// </summary>
        public async Task<object?> ExecuteScalarAsync(string sql, Dictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default)
        {
            parameters ??= [];
            _executedQueries.Add((sql, parameters));
            await Task.Yield();

            if (sql.Contains("COUNT", StringComparison.OrdinalIgnoreCase))
            {
                // Extract table name from SQL
                Match match = System.Text.RegularExpressions.Regex.Match(sql, @"FROM\s+\[?(\w+)\]?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    string tableName = match.Groups[1].Value;
                    try
                    {
                        return _dataStore.Count(tableName);
                    }
                    catch { }
                }
                return 0;
            }

            return sql.Contains("SCOPE_IDENTITY", StringComparison.OrdinalIgnoreCase) ||
                sql.Contains("last_insert_rowid", StringComparison.OrdinalIgnoreCase)
                ? _nextAutoIncrementId - 1
                : null;
        }

        /// <summary>
        /// Executes a SQL query and returns a data reader
        /// </summary>
        public async Task<IDataReader?> ExecuteReaderAsync(string sql, Dictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default)
        {
            parameters ??= [];
            _executedQueries.Add((sql, parameters));
            await Task.Yield();

            // Extract table name from SQL
            Match match = System.Text.RegularExpressions.Regex.Match(sql, @"FROM\s+\[?(\w+)\]?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string tableName = match.Groups[1].Value;
                try
                {
                    List<Dictionary<string, object?>> rows = _dataStore.Select(tableName);
                    return new MockDataReader(rows);
                }
                catch { }
            }

            return new MockDataReader([]);
        }

        /// <summary>
        /// Gets all executed queries (for testing/verification)
        /// </summary>
        public IReadOnlyList<(string sql, Dictionary<string, object?> parameters)> GetExecutedQueries()
        {
            return _executedQueries.AsReadOnly();
        }

        /// <summary>
        /// Clears the executed queries log
        /// </summary>
        public void ClearExecutedQueries()
        {
            _executedQueries.Clear();
        }

        #endregion

        #region Private Helpers

        private int ExecuteInsert(string sql, Dictionary<string, object?> parameters)
        {
            Match match = System.Text.RegularExpressions.Regex.Match(sql, @"INTO\s+\[?(\w+)\]?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return 0;
            }

            string tableName = match.Groups[1].Value;
            Dictionary<string, object?> row = new Dictionary<string, object?>(parameters, StringComparer.OrdinalIgnoreCase);
        
            // Add auto-increment if needed
            if (!row.ContainsKey("Id") && !row.ContainsKey("id"))
            {
                row["Id"] = _nextAutoIncrementId++;
            }

            try
            {
                _dataStore.Insert(tableName, row);
                return 1;
            }
            catch
            {
                return 0;
            }
        }

        private int ExecuteUpdate(string sql, Dictionary<string, object?> parameters)
        {
            Match match = System.Text.RegularExpressions.Regex.Match(sql, @"UPDATE\s+\[?(\w+)\]?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return 0;
            }

            string tableName = match.Groups[1].Value;

            // Extract where clause and parameters
            Match whereMatch = System.Text.RegularExpressions.Regex.Match(sql, @"WHERE\s+(\w+)\s*=\s*@(\w+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!whereMatch.Success)
            {
                return 0;
            }

            string columnName = whereMatch.Groups[1].Value;
            string paramName = whereMatch.Groups[2].Value;

            if (!parameters.TryGetValue(paramName, out var whereValue))
            {
                return 0;
            }

            // Extract update columns
            Dictionary<string, object?> updateValues = parameters
                .Where(kvp => !kvp.Key.Equals(paramName, StringComparison.OrdinalIgnoreCase))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);

            try
            {
                return _dataStore.Update(tableName, 
                    row => row.ContainsKey(columnName) && row[columnName]?.Equals(whereValue) == true,
                    updateValues);
            }
            catch
            {
                return 0;
            }
        }

        private int ExecuteDelete(string sql, Dictionary<string, object?> parameters)
        {
            Match match = System.Text.RegularExpressions.Regex.Match(sql, @"FROM\s+\[?(\w+)\]?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return 0;
            }

            string tableName = match.Groups[1].Value;

            // Extract where clause
            Match whereMatch = System.Text.RegularExpressions.Regex.Match(sql, @"WHERE\s+(\w+)\s*=\s*@(\w+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!whereMatch.Success)
            {
                return 0;
            }

            string columnName = whereMatch.Groups[1].Value;
            string paramName = whereMatch.Groups[2].Value;

            if (!parameters.TryGetValue(paramName, out var whereValue))
            {
                return 0;
            }

            try
            {
                return _dataStore.Delete(tableName,
                    row => row.ContainsKey(columnName) && row[columnName]?.Equals(whereValue) == true);
            }
            catch
            {
                return 0;
            }
        }

        #endregion
    }

    /// <summary>
    /// Mock database transaction
    /// </summary>
    public class MockDbTransaction : IDbTransaction
    {
        private readonly MockDatabaseConnection _connection;
        private bool _disposed;

        public MockDbTransaction(MockDatabaseConnection connection)
        {
            _connection = connection;
        }

        public IDbConnection Connection => _connection;
        public IsolationLevel IsolationLevel { get; } = IsolationLevel.Unspecified;

        public void Commit() { }
        public void Dispose() => _disposed = true;
        public void Rollback() { }
    }

    /// <summary>
    /// Mock database command
    /// </summary>
    public class MockDbCommand : IDbCommand
    {
        private readonly MockDatabaseConnection _connection;

        public MockDbCommand(MockDatabaseConnection connection)
        {
            _connection = connection;
        }

        public string CommandText { get; set; } = string.Empty;
        public int CommandTimeout { get; set; } = 30;
        public CommandType CommandType { get; set; } = CommandType.Text;
        public IDbConnection Connection
        {
            get => _connection;
            set { }
        }
        public IDataParameterCollection Parameters => new MockDataParameterCollection();
        public IDbTransaction? Transaction { get; set; }
        public UpdateRowSource UpdatedRowSource { get; set; }

        public void Cancel() { }
        public IDbDataParameter CreateParameter() => new MockDataParameter();
        public void Dispose() { }
        public int ExecuteNonQuery() => 0;
        public IDataReader ExecuteReader() => new MockDataReader([]);
        public IDataReader ExecuteReader(CommandBehavior behavior) => new MockDataReader([]);
        public object? ExecuteScalar() => null;
        public void Prepare() { }
    }

    /// <summary>
    /// Mock data parameter
    /// </summary>
    public class MockDataParameter : IDbDataParameter
    {
        public DbType DbType { get; set; }
        public ParameterDirection Direction { get; set; }
        public bool IsNullable { get; set; }
        public string ParameterName { get; set; } = string.Empty;
        public string SourceColumn { get; set; } = string.Empty;
        public DataRowVersion SourceVersion { get; set; }
        public object? Value { get; set; }
        public byte Precision { get; set; }
        public byte Scale { get; set; }
        public int Size { get; set; }
    }

    /// <summary>
    /// Mock data parameter collection
    /// </summary>
    public class MockDataParameterCollection : IDataParameterCollection
    {
        private readonly List<IDataParameter> _parameters = [];

        public int Count => _parameters.Count;
        public bool IsFixedSize => false;
        public bool IsReadOnly => false;
        public bool IsSynchronized => false;
        public object SyncRoot => new();

        public object? this[int index]
        {
            get => _parameters[index];
            set => _parameters[index] = (IDataParameter)value!;
        }

        public object? this[string parameterName]
        {
            get => _parameters.FirstOrDefault(p => p.ParameterName == parameterName);
            set => _parameters[_parameters.FindIndex(p => p.ParameterName == parameterName)] = (IDataParameter)value!;
        }

        public int Add(object value) { _parameters.Add((IDataParameter)value); return _parameters.Count - 1; }
        public void Clear() => _parameters.Clear();
        public bool Contains(object value) => _parameters.Contains((IDataParameter)value);
        public bool Contains(string parameterName) => _parameters.Any(p => p.ParameterName == parameterName);
        public void CopyTo(Array array, int index) => _parameters.CopyTo((IDataParameter[])array, index);
        public IEnumerator<IDataParameter> GetEnumerator() => _parameters.GetEnumerator();
        public int IndexOf(object value) => _parameters.IndexOf((IDataParameter)value);
        public int IndexOf(string parameterName) => _parameters.FindIndex(p => p.ParameterName == parameterName);
        public void Insert(int index, object value) => _parameters.Insert(index, (IDataParameter)value);
        public void Remove(object value) => _parameters.Remove((IDataParameter)value);
        public void RemoveAt(int index) => _parameters.RemoveAt(index);
        public void RemoveAt(string parameterName) => _parameters.RemoveAll(p => p.ParameterName == parameterName);

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _parameters.GetEnumerator();
    }

    /// <summary>
    /// Mock data reader
    /// </summary>
    public class MockDataReader : IDataReader
    {
        private readonly List<Dictionary<string, object?>> _rows;
        private int _currentIndex = -1;
        private bool _disposed;

        public MockDataReader(List<Dictionary<string, object?>> rows)
        {
            _rows = rows ?? [];
        }

        public int Depth => 0;
        public bool HasRows => _rows.Count > 0;
        public bool IsClosed => _disposed;
        public int RecordsAffected => _rows.Count;
        public int FieldCount => _currentIndex >= 0 && _currentIndex < _rows.Count
            ? _rows[_currentIndex].Count
            : 0;

        public object this[int ordinal] => GetValue(ordinal);
        public object this[string name] => GetValue(GetOrdinal(name));

        public bool GetBoolean(int ordinal) => (bool?)GetValue(ordinal) ?? false;
        public byte GetByte(int ordinal) => (byte?)GetValue(ordinal) ?? 0;
        public long GetBytes(int ordinal, long dataIndex, byte[]? buffer, int bufferIndex, int length) => 0;
        public char GetChar(int ordinal) => (char?)GetValue(ordinal) ?? '\0';
        public long GetChars(int ordinal, long dataIndex, char[]? buffer, int bufferIndex, int length) => 0;
        public IDataReader GetData(int ordinal) => this;
        public string GetDataTypeName(int ordinal) => GetValue(ordinal)?.GetType().Name ?? "object";
        public DateTime GetDateTime(int ordinal) => (DateTime?)GetValue(ordinal) ?? DateTime.MinValue;
        public decimal GetDecimal(int ordinal) => (decimal?)GetValue(ordinal) ?? 0m;
        public double GetDouble(int ordinal) => (double?)GetValue(ordinal) ?? 0d;
        public Type GetFieldType(int ordinal) => GetValue(ordinal)?.GetType() ?? typeof(object);
        public float GetFloat(int ordinal) => (float?)GetValue(ordinal) ?? 0f;
        public Guid GetGuid(int ordinal) => (Guid?)GetValue(ordinal) ?? Guid.Empty;
        public short GetInt16(int ordinal) => (short?)GetValue(ordinal) ?? 0;
        public int GetInt32(int ordinal) => (int?)GetValue(ordinal) ?? 0;
        public long GetInt64(int ordinal) => (long?)GetValue(ordinal) ?? 0L;
        public string GetName(int ordinal) => _currentIndex >= 0 && _currentIndex < _rows.Count
            ? _rows[_currentIndex].Keys.ElementAtOrDefault(ordinal) ?? string.Empty
            : string.Empty;
        public int GetOrdinal(string name) => _currentIndex >= 0 && _currentIndex < _rows.Count
            ? _rows[_currentIndex].Keys.ToList().FindIndex(k => k == name)
            : -1;
        public DataTable GetSchemaTable() => new();
        public string GetString(int ordinal) => GetValue(ordinal)?.ToString() ?? string.Empty;
        public object? GetValue(int ordinal)
        {
            if (_currentIndex < 0 || _currentIndex >= _rows.Count)
            {
                return null;
            }

            List<string> keys = _rows[_currentIndex].Keys.ToList();
            return ordinal >= 0 && ordinal < keys.Count ? _rows[_currentIndex][keys[ordinal]] : null;
        }
        public int GetValues(object?[] values)
        {
            if (_currentIndex < 0 || _currentIndex >= _rows.Count)
            {
                return 0;
            }

            KeyValuePair<string, object?>[] kvps = _rows[_currentIndex].ToArray();
            int count = Math.Min(values.Length, kvps.Length);
            for (int i = 0; i < count; i++)
            {
                values[i] = kvps[i].Value;
            }

            return count;
        }
        public bool IsDBNull(int ordinal) => GetValue(ordinal) == null;
        public bool NextResult() => false;
        public bool Read() => ++_currentIndex < _rows.Count;
        public void Close() => Dispose();
        public DataTable? GetSchemaTable(string? tableName) => null;
        public void Dispose() => _disposed = true;
    }
}
