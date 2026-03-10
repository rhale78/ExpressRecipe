using Microsoft.Data.SqlClient;

namespace ExpressRecipe.Data.Common;

/// <summary>
/// Tracks the last-run status of background workers in the service database.
/// Each BackgroundService calls <see cref="UpdateAsync"/> at the end of each iteration.
/// Admin dashboard reads all <c>JobHealthCheck</c> rows via HTTP health endpoints.
/// Alerts when <c>LastRunAt</c> is older than 2× the expected interval.
/// </summary>
public interface IJobHealthRepository
{
    Task UpdateAsync(string jobName, bool success, string? errorMessage = null, CancellationToken ct = default);
    Task<List<JobHealthCheckDto>> GetAllAsync(CancellationToken ct = default);
}

public sealed class JobHealthCheckDto
{
    public string JobName { get; set; } = string.Empty;
    public DateTime LastRunAt { get; set; }
    public bool LastSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public int RunCount { get; set; }
}

/// <summary>
/// SQL Server–backed job health repository.
/// Requires the <c>JobHealthCheck</c> table to exist (created via migration or EnsureTableAsync).
/// </summary>
public sealed class JobHealthRepository : IJobHealthRepository
{
    private readonly string _connectionString;

    public JobHealthRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    /// <summary>
    /// Creates the <c>JobHealthCheck</c> table if it does not yet exist.
    /// Call this once on service startup before the first <see cref="UpdateAsync"/>.
    /// </summary>
    public async Task EnsureTableAsync(CancellationToken ct = default)
    {
        const string sql = @"
            IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'JobHealthCheck') AND type = 'U')
            BEGIN
                CREATE TABLE JobHealthCheck (
                    JobName      NVARCHAR(100) NOT NULL PRIMARY KEY,
                    LastRunAt    DATETIME2     NOT NULL,
                    LastSuccess  BIT           NOT NULL DEFAULT 1,
                    ErrorMessage NVARCHAR(MAX) NULL,
                    RunCount     INT           NOT NULL DEFAULT 0
                );
            END";

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task UpdateAsync(string jobName, bool success, string? errorMessage = null, CancellationToken ct = default)
    {
        const string sql = @"
            IF EXISTS (SELECT 1 FROM JobHealthCheck WHERE JobName = @JobName)
                UPDATE JobHealthCheck
                SET LastRunAt    = GETUTCDATE(),
                    LastSuccess  = @Success,
                    ErrorMessage = @ErrorMessage,
                    RunCount     = RunCount + 1
                WHERE JobName = @JobName
            ELSE
                INSERT INTO JobHealthCheck (JobName, LastRunAt, LastSuccess, ErrorMessage, RunCount)
                VALUES (@JobName, GETUTCDATE(), @Success, @ErrorMessage, 1)";

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@JobName", jobName);
        cmd.Parameters.AddWithValue("@Success", success ? 1 : 0);
        cmd.Parameters.AddWithValue("@ErrorMessage", (object?)errorMessage ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<JobHealthCheckDto>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT JobName, LastRunAt, LastSuccess, ErrorMessage, RunCount
            FROM JobHealthCheck
            ORDER BY JobName";

        var results = new List<JobHealthCheckDto>();

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            results.Add(new JobHealthCheckDto
            {
                JobName = reader.GetString(reader.GetOrdinal("JobName")),
                LastRunAt = reader.GetDateTime(reader.GetOrdinal("LastRunAt")),
                LastSuccess = reader.GetBoolean(reader.GetOrdinal("LastSuccess")),
                ErrorMessage = reader.IsDBNull(reader.GetOrdinal("ErrorMessage"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("ErrorMessage")),
                RunCount = reader.GetInt32(reader.GetOrdinal("RunCount"))
            });
        }

        return results;
    }
}
