using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using ExpressRecipe.Data.Common.Logging;

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

        _logger?.LogMigrationTableEnsured();
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
    }

    /// <summary>
    /// Applies a single migration script
    /// </summary>
    /// <param name="migrationId">Unique identifier for the migration (e.g., "001_CreateUserTable")</param>
    /// <param name="sqlScript">The SQL script to execute</param>
    public async Task ApplyMigrationAsync(string migrationId, string sqlScript)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await EnsureMigrationTableExistsAsync();

        if (await IsMigrationAppliedAsync(migrationId))
        {
            _logger?.LogMigrationSkipped(migrationId);
            return;
        }

        _logger?.LogApplyingMigration(migrationId);

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        // Robust split on GO batch separators: lines containing only 'GO' (case-insensitive)
        var batches = Regex.Split(sqlScript, @"^\s*GO\s*$(\r?\n)?", RegexOptions.Multiline | RegexOptions.IgnoreCase)
            .Select(b => b?.Trim())
            .Where(b => !string.IsNullOrWhiteSpace(b))
            .ToArray();

        // Some statements (e.g., FULLTEXT operations) cannot run inside a user transaction.
        // We'll commit any active transaction before such a batch, execute it outside a tx, then resume transactions.
        SqlTransaction? tx = null;
        try
        {
            foreach (var batch in batches)
            {
                var requiresNonTx = Regex.IsMatch(batch!, @"\bFULLTEXT\b", RegexOptions.IgnoreCase);

                if (requiresNonTx)
                {
                    // Commit any active transaction before executing non-transactional batch
                    if (tx != null)
                    {
                        await tx.CommitAsync();
                        await tx.DisposeAsync();
                        tx = null;
                    }

                    await using var nonTxCommand = new SqlCommand(batch, connection);
                    nonTxCommand.CommandTimeout = 120; // 2 minutes for complex migrations
                    await nonTxCommand.ExecuteNonQueryAsync();
                }
                else
                {
                    // Ensure we have a transaction for transactional batches
                    if (tx == null)
                    {
                        tx = (SqlTransaction)await connection.BeginTransactionAsync();
                    }
                       
                    await using var command = new SqlCommand(batch, connection, tx);
                    command.CommandTimeout = 120; // 2 minutes for complex migrations
                    await command.ExecuteNonQueryAsync();
                }
            }

            // Commit any remaining transactional work before recording migration
            if (tx != null)
            {
                await tx.CommitAsync();
                await tx.DisposeAsync();
                tx = null;
            }

            await RecordMigrationAsync(migrationId);

            sw.Stop();
            _logger?.LogMigrationCompleted(migrationId, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            if (tx != null)
            {
                await tx.RollbackAsync();
                await tx.DisposeAsync();
            }
            _logger?.LogMigrationFailed(migrationId, ex);
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

        _logger?.LogAllMigrationsCompleted();
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
