using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace HighSpeedDAL.Core.Search
{
    /// <summary>
    /// Full-text search manager for SQL Server FTS integration.
    /// 
    /// Features:
    /// - Multiple search modes (Contains, Exact, Wildcard, Near, Boolean)
    /// - Relevance ranking
    /// - Multi-column search
    /// - Auto-complete suggestions
    /// - Easy catalog and index management
    /// 
    /// Supports SQL Server Full-Text Search (FTS) for high-performance searching.
    /// 
    /// Example usage:
    /// FullTextSearchManager searchManager = new FullTextSearchManager(logger, connection);
    /// 
    /// // Search with ranking
    /// List<SearchResult> results = await searchManager.SearchAsync(
    ///     "Products", 
    ///     new[] { "Name", "Description" }, 
    ///     "laptop gaming", 
    ///     SearchMode.Contains);
    /// 
    /// HighSpeedDAL Framework v0.1 - Phase 4
    /// </summary>
    public sealed class FullTextSearchManager
    {
        private readonly ILogger<FullTextSearchManager> _logger;

        public FullTextSearchManager(ILogger<FullTextSearchManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _logger.LogInformation("Full-Text Search Manager initialized");
        }

        /// <summary>
        /// Performs a full-text search on specified columns.
        /// </summary>
        /// <param name="connection">Database connection</param>
        /// <param name="tableName">Table to search</param>
        /// <param name="columns">Columns to search in</param>
        /// <param name="searchText">Text to search for</param>
        /// <param name="mode">Search mode</param>
        /// <param name="top">Maximum results to return</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of search results with ranking</returns>
        public async Task<List<FullTextSearchResult>> SearchAsync(
            DbConnection connection,
            string tableName,
            string[] columns,
            string searchText,
            SearchMode mode = SearchMode.Contains,
            int top = 100,
            CancellationToken cancellationToken = default)
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }
            if (string.IsNullOrWhiteSpace(tableName))
            {
                throw new ArgumentException("Table name cannot be null or empty", nameof(tableName));
            }
            if (columns == null || columns.Length == 0)
            {
                throw new ArgumentException("At least one column must be specified", nameof(columns));
            }
            if (string.IsNullOrWhiteSpace(searchText))
            {
                throw new ArgumentException("Search text cannot be null or empty", nameof(searchText));
            }

            List<FullTextSearchResult> results = [];

            try
            {
                string searchPredicate = BuildSearchPredicate(searchText, mode);
                string columnList = string.Join(", ", columns);

                StringBuilder sql = new StringBuilder();
                sql.AppendLine($"SELECT TOP {top}");
                sql.AppendLine($"    ft.KEY AS [Key],");
                sql.AppendLine($"    ft.RANK,");
                sql.AppendLine($"    *");
                sql.AppendLine($"FROM {tableName} t");
                sql.AppendLine($"INNER JOIN CONTAINSTABLE({tableName}, ({columnList}), @SearchText) AS ft");
                sql.AppendLine($"    ON t.Id = ft.KEY");
                sql.AppendLine($"ORDER BY ft.RANK DESC");

                DbCommand command = connection.CreateCommand();
                command.CommandText = sql.ToString();

                DbParameter param = command.CreateParameter();
                param.ParameterName = "@SearchText";
                param.Value = searchPredicate;
                command.Parameters.Add(param);

                if (connection.State != ConnectionState.Open)
                {
                    await connection.OpenAsync(cancellationToken);
                }

                DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

                while (await reader.ReadAsync(cancellationToken))
                {
                    FullTextSearchResult result = new FullTextSearchResult
                    {
                        Rank = reader.GetInt32(reader.GetOrdinal("RANK"))
                    };

                    // Read all columns into dictionary
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        string columnName = reader.GetName(i);
                        if (columnName != "Key" && columnName != "RANK")
                        {
                            result.Data[columnName] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                        }
                    }

                    results.Add(result);
                }

                await reader.CloseAsync();

                _logger.LogInformation(
                    "Full-text search completed. Table: {Table}, Mode: {Mode}, Results: {Count}",
                    tableName, mode, results.Count);

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error performing full-text search on table: {Table}",
                    tableName);
                throw;
            }
        }

        /// <summary>
        /// Gets auto-complete suggestions based on prefix.
        /// </summary>
        /// <param name="connection">Database connection</param>
        /// <param name="tableName">Table to search</param>
        /// <param name="column">Column to search in</param>
        /// <param name="prefix">Text prefix</param>
        /// <param name="top">Maximum suggestions</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of suggestions</returns>
        public async Task<List<string>> GetSuggestionsAsync(
            DbConnection connection,
            string tableName,
            string column,
            string prefix,
            int top = 10,
            CancellationToken cancellationToken = default)
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }
            if (string.IsNullOrWhiteSpace(tableName))
            {
                throw new ArgumentException("Table name cannot be null or empty", nameof(tableName));
            }
            if (string.IsNullOrWhiteSpace(column))
            {
                throw new ArgumentException("Column cannot be null or empty", nameof(column));
            }
            if (string.IsNullOrWhiteSpace(prefix))
            {
                throw new ArgumentException("Prefix cannot be null or empty", nameof(prefix));
            }

            List<string> suggestions = [];

            try
            {
                string sql = $@"
                SELECT DISTINCT TOP {top} {column}
                FROM {tableName}
                WHERE {column} LIKE @Prefix + '%'
                ORDER BY {column}";

                DbCommand command = connection.CreateCommand();
                command.CommandText = sql;

                DbParameter param = command.CreateParameter();
                param.ParameterName = "@Prefix";
                param.Value = prefix;
                command.Parameters.Add(param);

                if (connection.State != ConnectionState.Open)
                {
                    await connection.OpenAsync(cancellationToken);
                }

                DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

                while (await reader.ReadAsync(cancellationToken))
                {
                    if (!reader.IsDBNull(0))
                    {
                        suggestions.Add(reader.GetString(0));
                    }
                }

                await reader.CloseAsync();

                _logger.LogDebug(
                    "Generated {Count} suggestions for prefix: {Prefix}",
                    suggestions.Count, prefix);

                return suggestions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error generating suggestions for table: {Table}, column: {Column}",
                    tableName, column);
                throw;
            }
        }

        /// <summary>
        /// Creates a full-text catalog.
        /// </summary>
        /// <param name="connection">Database connection</param>
        /// <param name="catalogName">Catalog name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public async Task CreateCatalogAsync(
            DbConnection connection,
            string catalogName,
            CancellationToken cancellationToken = default)
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }
            if (string.IsNullOrWhiteSpace(catalogName))
            {
                throw new ArgumentException("Catalog name cannot be null or empty", nameof(catalogName));
            }

            try
            {
                string sql = $"CREATE FULLTEXT CATALOG [{catalogName}]";

                DbCommand command = connection.CreateCommand();
                command.CommandText = sql;

                if (connection.State != ConnectionState.Open)
                {
                    await connection.OpenAsync(cancellationToken);
                }

                await command.ExecuteNonQueryAsync(cancellationToken);

                _logger.LogInformation("Created full-text catalog: {Catalog}", catalogName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating full-text catalog: {Catalog}", catalogName);
                throw;
            }
        }

        /// <summary>
        /// Creates a full-text index on specified columns.
        /// </summary>
        /// <param name="connection">Database connection</param>
        /// <param name="tableName">Table name</param>
        /// <param name="columns">Columns to index</param>
        /// <param name="catalogName">Catalog name</param>
        /// <param name="uniqueIndexName">Name of unique index (typically primary key)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public async Task CreateIndexAsync(
            DbConnection connection,
            string tableName,
            string[] columns,
            string catalogName,
            string uniqueIndexName = "PK_Id",
            CancellationToken cancellationToken = default)
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }
            if (string.IsNullOrWhiteSpace(tableName))
            {
                throw new ArgumentException("Table name cannot be null or empty", nameof(tableName));
            }
            if (columns == null || columns.Length == 0)
            {
                throw new ArgumentException("At least one column must be specified", nameof(columns));
            }
            if (string.IsNullOrWhiteSpace(catalogName))
            {
                throw new ArgumentException("Catalog name cannot be null or empty", nameof(catalogName));
            }

            try
            {
                StringBuilder columnList = new StringBuilder();
                for (int i = 0; i < columns.Length; i++)
                {
                    if (i > 0)
                    {
                        columnList.Append(", ");
                    }
                    columnList.Append($"[{columns[i]}]");
                }

                string sql = $@"
                CREATE FULLTEXT INDEX ON [{tableName}]
                ({columnList})
                KEY INDEX [{uniqueIndexName}]
                ON [{catalogName}]";

                DbCommand command = connection.CreateCommand();
                command.CommandText = sql;

                if (connection.State != ConnectionState.Open)
                {
                    await connection.OpenAsync(cancellationToken);
                }

                await command.ExecuteNonQueryAsync(cancellationToken);

                _logger.LogInformation(
                    "Created full-text index on table: {Table}, columns: {Columns}",
                    tableName, string.Join(", ", columns));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error creating full-text index on table: {Table}",
                    tableName);
                throw;
            }
        }

        #region Private Methods

        private string BuildSearchPredicate(string searchText, SearchMode mode)
        {
            switch (mode)
            {
                case SearchMode.Contains:
                    return $"\"{searchText}\"";

                case SearchMode.ExactPhrase:
                    return $"\"{searchText}\"";

                case SearchMode.Wildcard:
                    return $"\"{searchText}*\"";

                case SearchMode.Near:
                    string[] words = searchText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    return $"NEAR(({string.Join(", ", words)}))";

                case SearchMode.Boolean:
                    return searchText; // User provides full boolean expression

                default:
                    return $"\"{searchText}\"";
            }
        }

        #endregion
    }

    /// <summary>
    /// Full-text search result with ranking.
    /// </summary>
    public sealed class FullTextSearchResult
    {
        public Dictionary<string, object?> Data { get; set; } = [];
        public int Rank { get; set; }
    }

    /// <summary>
    /// Full-text search modes.
    /// </summary>
    public enum SearchMode
    {
        /// <summary>
        /// CONTAINS - matches whole words
        /// </summary>
        Contains = 0,

        /// <summary>
        /// Exact phrase match
        /// </summary>
        ExactPhrase = 1,

        /// <summary>
        /// Wildcard search (prefix matching)
        /// </summary>
        Wildcard = 2,

        /// <summary>
        /// NEAR proximity search
        /// </summary>
        Near = 3,

        /// <summary>
        /// Boolean operators (AND, OR, NOT)
        /// </summary>
        Boolean = 4
    }
}
