using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace ExpressRecipe.Data.Common;

/// <summary>
/// Handles database migrations by executing SQL scripts in order
/// </summary>
public class MigrationRunner
{
    private readonly string _connectionString;
    private readonly ILogger<MigrationRunner>? _logger;

    public MigrationRunner(string connectionString, ILogger<MigrationRunner>? logger = null)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    /// <summary>
    /// Ensures the migration tracking table exists
    /// </summary>
    private async Task EnsureMigrationTableExistsAsync()
    {
        const string sql = @"
            IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[__MigrationHistory]') AND type in (N'U'))
            BEGIN
                CREATE TABLE [dbo].[__MigrationHistory] (
                    MigrationId NVARCHAR(150) NOT NULL PRIMARY KEY,
                    AppliedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
                );
            END";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();

        _logger?.LogInformation("Migration tracking table ensured");
    }

    /// <summary>
    /// Checks if a migration has already been applied
    /// </summary>
    private async Task<bool> IsMigrationAppliedAsync(string migrationId)
    {
        const string sql = "SELECT COUNT(*) FROM [dbo].[__MigrationHistory] WHERE MigrationId = @MigrationId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@MigrationId", migrationId);

        var count = (int)(await command.ExecuteScalarAsync() ?? 0);
        return count > 0;
    }

    /// <summary>
    /// Records that a migration has been applied
    /// </summary>
    private async Task RecordMigrationAsync(string migrationId)
    {
        const string sql = "INSERT INTO [dbo].[__MigrationHistory] (MigrationId, AppliedAt) VALUES (@MigrationId, GETUTCDATE())";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@MigrationId", migrationId);

        await command.ExecuteNonQueryAsync();
        _logger?.LogInformation("Migration {MigrationId} recorded", migrationId);
    }

    /// <summary>
    /// Applies a single migration script
    /// </summary>
    /// <param name="migrationId">Unique identifier for the migration (e.g., "001_CreateUserTable")</param>
    /// <param name="sqlScript">The SQL script to execute</param>
    public async Task ApplyMigrationAsync(string migrationId, string sqlScript)
    {
        await EnsureMigrationTableExistsAsync();

        if (await IsMigrationAppliedAsync(migrationId))
        {
            _logger?.LogInformation("Migration {MigrationId} already applied, skipping", migrationId);
            return;
        }

        _logger?.LogInformation("Applying migration {MigrationId}...", migrationId);

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        // Split script by GO statements (SQL Server batch separator)
        var batches = sqlScript
            .Split(new[] { "\nGO\n", "\nGO\r\n", "\r\nGO\r\n", "\r\nGO\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Where(batch => !string.IsNullOrWhiteSpace(batch))
            .ToArray();

        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            foreach (var batch in batches)
            {
                if (string.IsNullOrWhiteSpace(batch))
                    continue;

                await using var command = new SqlCommand(batch.Trim(), connection, (SqlTransaction)transaction);
                command.CommandTimeout = 120; // 2 minutes for complex migrations
                await command.ExecuteNonQueryAsync();
            }

            await RecordMigrationAsync(migrationId);
            await transaction.CommitAsync();

            _logger?.LogInformation("Migration {MigrationId} applied successfully", migrationId);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger?.LogError(ex, "Failed to apply migration {MigrationId}", migrationId);
            throw;
        }
    }

    /// <summary>
    /// Applies multiple migrations in order
    /// </summary>
    /// <param name="migrations">Dictionary of migration ID to SQL script</param>
    public async Task ApplyMigrationsAsync(IDictionary<string, string> migrations)
    {
        await EnsureMigrationTableExistsAsync();

        // Sort migrations by ID to ensure they're applied in order
        var orderedMigrations = migrations.OrderBy(m => m.Key);

        foreach (var migration in orderedMigrations)
        {
            await ApplyMigrationAsync(migration.Key, migration.Value);
        }

        _logger?.LogInformation("All migrations applied successfully");
    }

    /// <summary>
    /// Gets list of applied migrations
    /// </summary>
    public async Task<List<string>> GetAppliedMigrationsAsync()
    {
        await EnsureMigrationTableExistsAsync();

        const string sql = "SELECT MigrationId FROM [dbo].[__MigrationHistory] ORDER BY AppliedAt";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        var migrations = new List<string>();
        while (await reader.ReadAsync())
        {
            migrations.Add(reader.GetString(0));
        }

        return migrations;
    }
}
