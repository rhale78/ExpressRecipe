using System.Data;
using System.Text;
using System.Text.Json;
using ExpressRecipe.Messaging.Saga.Abstractions;
using Microsoft.Data.SqlClient;

namespace ExpressRecipe.Messaging.Saga.Persistence;

/// <summary>
/// ADO.NET / SQL Server implementation of <see cref="ISagaBatchRepository{TState}"/>.
/// Uses raw SQL with batching for maximum performance.
/// Assumes a table named after <typeparamref name="TState"/> with at minimum:
///   CorrelationId NVARCHAR(128), CurrentMask BIGINT, Status INT, StartedAt DATETIMEOFFSET, CompletedAt DATETIMEOFFSET NULL, StateJson NVARCHAR(MAX)
/// </summary>
/// <typeparam name="TState">The saga state type.</typeparam>
public sealed class SqlSagaRepository<TState> : ISagaBatchRepository<TState>
    where TState : class, ISagaState
{
    private readonly string _connectionString;
    private readonly string _tableName;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Initializes the repository.
    /// </summary>
    /// <param name="connectionString">ADO.NET connection string for SQL Server.</param>
    /// <param name="tableName">
    /// The table name to use. Defaults to the name of <typeparamref name="TState"/>.
    /// </param>
    public SqlSagaRepository(string connectionString, string? tableName = null)
    {
        _connectionString = connectionString;
        _tableName = tableName ?? typeof(TState).Name;
    }

    /// <inheritdoc />
    public async Task<TState?> LoadAsync(string correlationId, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT StateJson FROM [{_tableName}] WHERE CorrelationId = @CorrelationId";
        cmd.Parameters.Add(new SqlParameter("@CorrelationId", SqlDbType.NVarChar, 128) { Value = correlationId });

        var json = (string?)await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return json is null ? null : JsonSerializer.Deserialize<TState>(json, _jsonOptions);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TState>> BatchLoadAsync(IEnumerable<string> correlationIds, CancellationToken cancellationToken = default)
    {
        var ids = correlationIds.ToList();
        if (ids.Count == 0) return Array.Empty<TState>();

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        var sb = new StringBuilder("SELECT StateJson FROM [");
        sb.Append(_tableName);
        sb.Append("] WHERE CorrelationId IN (");
        for (int i = 0; i < ids.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append($"@p{i}");
        }
        sb.Append(')');

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sb.ToString();
        for (int i = 0; i < ids.Count; i++)
            cmd.Parameters.Add(new SqlParameter($"@p{i}", SqlDbType.NVarChar, 128) { Value = ids[i] });

        var results = new List<TState>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var json = reader.GetString(0);
            var state = JsonSerializer.Deserialize<TState>(json, _jsonOptions);
            if (state is not null) results.Add(state);
        }
        return results;
    }

    /// <inheritdoc />
    public async Task SaveAsync(TState state, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(state, _jsonOptions);

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
            INSERT INTO [{_tableName}] (CorrelationId, CurrentMask, Status, StartedAt, CompletedAt, StateJson)
            VALUES (@CorrelationId, @CurrentMask, @Status, @StartedAt, @CompletedAt, @StateJson)";

        cmd.Parameters.Add(new SqlParameter("@CorrelationId", SqlDbType.NVarChar, 128) { Value = state.CorrelationId });
        cmd.Parameters.Add(new SqlParameter("@CurrentMask", SqlDbType.BigInt) { Value = state.CurrentMask });
        cmd.Parameters.Add(new SqlParameter("@Status", SqlDbType.Int) { Value = (int)state.Status });
        cmd.Parameters.Add(new SqlParameter("@StartedAt", SqlDbType.DateTimeOffset) { Value = state.StartedAt });
        cmd.Parameters.Add(new SqlParameter("@CompletedAt", SqlDbType.DateTimeOffset) { Value = (object?)state.CompletedAt ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@StateJson", SqlDbType.NVarChar) { Value = json });

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TState>> BatchUpdateMaskAsync(
        IEnumerable<(string CorrelationId, long MaskToAdd)> updates,
        CancellationToken cancellationToken = default)
    {
        var list = updates.ToList();
        if (list.Count == 0) return Array.Empty<TState>();

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = conn.BeginTransaction();

        var sb = new StringBuilder($"UPDATE [{_tableName}] SET CurrentMask = CurrentMask | CASE CorrelationId ");
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;

        for (int i = 0; i < list.Count; i++)
        {
            sb.Append($"WHEN @id{i} THEN @mask{i} ");
            cmd.Parameters.Add(new SqlParameter($"@id{i}", SqlDbType.NVarChar, 128) { Value = list[i].CorrelationId });
            cmd.Parameters.Add(new SqlParameter($"@mask{i}", SqlDbType.BigInt) { Value = list[i].MaskToAdd });
        }
        sb.Append("ELSE 0 END WHERE CorrelationId IN (");

        for (int i = 0; i < list.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append($"@id{i}");
        }
        sb.Append("); SELECT CorrelationId, CurrentMask, Status, StartedAt, CompletedAt, StateJson FROM [");
        sb.Append(_tableName);
        sb.Append("] WHERE CorrelationId IN (");
        for (int i = 0; i < list.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append($"@id{i}");
        }
        sb.Append(')');

        cmd.CommandText = sb.ToString();
        var results = new List<TState>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var json = reader.GetString(5);
            var state = JsonSerializer.Deserialize<TState>(json, _jsonOptions);
            if (state is not null) results.Add(state);
        }

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return results;
    }

    /// <inheritdoc />
    public async Task BatchUpdateStatusAsync(
        IEnumerable<(string CorrelationId, SagaStatus Status, DateTimeOffset? CompletedAt)> updates,
        CancellationToken cancellationToken = default)
    {
        var list = updates.ToList();
        if (list.Count == 0) return;

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = conn.BeginTransaction();

        var sb = new StringBuilder($"UPDATE [{_tableName}] SET ");
        sb.Append("Status = CASE CorrelationId ");
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;

        for (int i = 0; i < list.Count; i++)
        {
            sb.Append($"WHEN @id{i} THEN @status{i} ");
            cmd.Parameters.Add(new SqlParameter($"@id{i}", SqlDbType.NVarChar, 128) { Value = list[i].CorrelationId });
            cmd.Parameters.Add(new SqlParameter($"@status{i}", SqlDbType.Int) { Value = (int)list[i].Status });
        }
        sb.Append("ELSE Status END, CompletedAt = CASE CorrelationId ");

        for (int i = 0; i < list.Count; i++)
        {
            sb.Append($"WHEN @id{i} THEN @completed{i} ");
            cmd.Parameters.Add(new SqlParameter($"@completed{i}", SqlDbType.DateTimeOffset) { Value = (object?)list[i].CompletedAt ?? DBNull.Value });
        }
        sb.Append("ELSE CompletedAt END WHERE CorrelationId IN (");
        for (int i = 0; i < list.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append($"@id{i}");
        }
        sb.Append(')');

        cmd.CommandText = sb.ToString();
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
