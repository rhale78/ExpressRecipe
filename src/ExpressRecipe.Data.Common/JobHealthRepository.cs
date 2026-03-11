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
public sealed class JobHealthRepository : SqlHelper, IJobHealthRepository
{
    public JobHealthRepository(string connectionString) : base(connectionString) { }

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

        await ExecuteNonQueryAsync(sql, ct);
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

        await ExecuteNonQueryAsync(sql, ct,
            CreateParameter("@JobName", jobName),
            CreateParameter("@Success", success),
            CreateParameter("@ErrorMessage", (object?)errorMessage ?? DBNull.Value));
    }

    public async Task<List<JobHealthCheckDto>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT JobName, LastRunAt, LastSuccess, ErrorMessage, RunCount
            FROM JobHealthCheck
            ORDER BY JobName";

        return await ExecuteReaderAsync(sql, reader => new JobHealthCheckDto
        {
            JobName = GetString(reader, "JobName") ?? string.Empty,
            LastRunAt = GetDateTime(reader, "LastRunAt"),
            LastSuccess = GetBoolean(reader, "LastSuccess"),
            ErrorMessage = GetString(reader, "ErrorMessage"),
            RunCount = GetInt32(reader, "RunCount")
        }, ct);
    }
}
