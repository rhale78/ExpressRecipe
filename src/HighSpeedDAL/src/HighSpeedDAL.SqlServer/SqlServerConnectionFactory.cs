using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using HighSpeedDAL.Core.Base;
using HighSpeedDAL.Core.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace HighSpeedDAL.SqlServer
{
    /// <summary>
    /// Connection factory for SQL Server databases
    /// </summary>
    public sealed class SqlServerConnectionFactory : IDbConnectionFactory
    {
        private readonly ILogger<SqlServerConnectionFactory> _logger;

        public SqlServerConnectionFactory(ILogger<SqlServerConnectionFactory> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IDbConnection> CreateConnectionAsync(
            string connectionString,
            DatabaseProvider provider,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));
            }

            if (provider != DatabaseProvider.SqlServer)
            {
                string errorMessage = $"Invalid provider {provider} for SqlServerConnectionFactory. Expected: {DatabaseProvider.SqlServer}";
                _logger.LogError(errorMessage);
                throw new ArgumentException(errorMessage, nameof(provider));
            }

            try
            {
                SqlConnection connection = new SqlConnection(connectionString);
                await connection.OpenAsync(cancellationToken);

                _logger.LogDebug("SQL Server connection opened successfully");
                return connection;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open SQL Server connection");
                throw;
            }
        }
    }

    /// <summary>
    /// Wrapper to make SqlCommand async-compatible with our interface
    /// </summary>
    internal sealed class SqlCommandWrapper : IDbCommand, Core.Base.IDbCommandAsync
    {
        private readonly SqlCommand _command;

        public SqlCommandWrapper(SqlCommand command)
        {
            _command = command ?? throw new ArgumentNullException(nameof(command));
        }

        public string CommandText
        {
            get => _command.CommandText;
#pragma warning disable CS8767
            set => _command.CommandText = value!;
#pragma warning restore CS8767
        }

        public int CommandTimeout
        {
            get => _command.CommandTimeout;
            set => _command.CommandTimeout = value;
        }

        public CommandType CommandType
        {
            get => _command.CommandType;
            set => _command.CommandType = value;
        }

        public IDbConnection? Connection
        {
            get => _command.Connection;
            set => _command.Connection = (SqlConnection?)value;
        }

        public IDbTransaction? Transaction
        {
            get => _command.Transaction;
            set => _command.Transaction = (SqlTransaction?)value;
        }

        public UpdateRowSource UpdatedRowSource
        {
            get => _command.UpdatedRowSource;
            set => _command.UpdatedRowSource = value;
        }

        public IDataParameterCollection Parameters => _command.Parameters;

        public void Cancel() => _command.Cancel();

        public IDbDataParameter CreateParameter() => _command.CreateParameter();

        public int ExecuteNonQuery() => _command.ExecuteNonQuery();

        public IDataReader ExecuteReader() => _command.ExecuteReader();

        public IDataReader ExecuteReader(CommandBehavior behavior) => _command.ExecuteReader(behavior);

        public object? ExecuteScalar() => _command.ExecuteScalar();

        public void Prepare() => _command.Prepare();

        public void Dispose() => _command.Dispose();

        // Async methods
        public async Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
        {
            return await _command.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task<IDataReader> ExecuteReaderAsync(CancellationToken cancellationToken)
        {
            return await _command.ExecuteReaderAsync(cancellationToken);
        }

        public async Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken)
        {
            return await _command.ExecuteScalarAsync(cancellationToken);
        }
    }
}
