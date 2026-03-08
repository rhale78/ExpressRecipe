using Microsoft.Data.SqlClient;
using System.Text.RegularExpressions;

namespace ExpressRecipe.PreferencesService.Data;

public class DatabaseMigrator
{
    private readonly string _connectionString;
    private readonly ILogger _logger;

    public DatabaseMigrator(string connectionString, ILogger logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task MigrateAsync()
    {
        string migrationsDir = Path.Combine(AppContext.BaseDirectory, "Data", "Migrations");
        if (!Directory.Exists(migrationsDir))
        {
            _logger.LogWarning("Migrations directory not found: {Directory}", migrationsDir);
            return;
        }

        List<string> migrationFiles = Directory.GetFiles(migrationsDir, "*.sql")
            .OrderBy(f => f)
            .ToList();

        if (!migrationFiles.Any())
        {
            _logger.LogInformation("No migration files found");
            return;
        }

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using (SqlCommand createTableCmd = connection.CreateCommand())
        {
            createTableCmd.CommandText = @"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MigrationHistory')
                BEGIN
                    CREATE TABLE MigrationHistory (
                        Id            INT IDENTITY(1,1) PRIMARY KEY,
                        MigrationName NVARCHAR(255) NOT NULL,
                        AppliedAt     DATETIME2 NOT NULL DEFAULT GETUTCDATE()
                    )
                END";
            await createTableCmd.ExecuteNonQueryAsync();
        }

        foreach (string file in migrationFiles)
        {
            string migrationName = Path.GetFileName(file);

            await using (SqlCommand checkCmd = connection.CreateCommand())
            {
                checkCmd.CommandText = "SELECT COUNT(*) FROM MigrationHistory WHERE MigrationName = @Name";
                checkCmd.Parameters.AddWithValue("@Name", migrationName);
                object? scalar = await checkCmd.ExecuteScalarAsync();
                int count = scalar is int intResult ? intResult : 0;

                if (count > 0)
                {
                    _logger.LogDebug("Migration {Migration} already applied", migrationName);
                    continue;
                }
            }

            _logger.LogInformation("Applying migration: {Migration}", migrationName);
            string sql = await File.ReadAllTextAsync(file);

            string[] batches = Regex.Split(sql, @"^\s*GO\s*$(\r?\n)?", RegexOptions.Multiline | RegexOptions.IgnoreCase)
                .Select(b => b?.Trim())
                .Where(b => !string.IsNullOrWhiteSpace(b))
                .ToArray();

            await using SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync();
            try
            {
                foreach (string batch in batches)
                {
                    await using SqlCommand cmd = connection.CreateCommand();
                    cmd.Transaction = transaction;
                    cmd.CommandText = batch;
                    cmd.CommandTimeout = 120;
                    await cmd.ExecuteNonQueryAsync();
                }

                await using (SqlCommand recordCmd = connection.CreateCommand())
                {
                    recordCmd.Transaction = transaction;
                    recordCmd.CommandText = "INSERT INTO MigrationHistory (MigrationName) VALUES (@Name)";
                    recordCmd.Parameters.AddWithValue("@Name", migrationName);
                    await recordCmd.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
                _logger.LogInformation("Migration {Migration} applied successfully", migrationName);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to apply migration {Migration}", migrationName);
                throw;
            }
        }
    }
}
