using Microsoft.Data.SqlClient;
using System.Text.RegularExpressions;

namespace ExpressRecipe.GroceryStoreLocationService.Data;

public static class DatabaseMigrator
{
    public static async Task MigrateAsync(string connectionString, ILogger logger)
    {
        var migrationsDir = Path.Combine(AppContext.BaseDirectory, "Data", "Migrations");
        if (!Directory.Exists(migrationsDir))
        {
            logger.LogWarning("Migrations directory not found: {Directory}", migrationsDir);
            return;
        }

        var migrationFiles = Directory.GetFiles(migrationsDir, "*.sql")
            .OrderBy(f => f)
            .ToList();

        if (!migrationFiles.Any())
        {
            logger.LogInformation("No migration files found");
            return;
        }

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = @"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MigrationHistory')
                BEGIN
                    CREATE TABLE MigrationHistory (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        MigrationName NVARCHAR(255) NOT NULL,
                        AppliedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
                    )
                END";
            await command.ExecuteNonQueryAsync();
        }

        foreach (var file in migrationFiles)
        {
            var migrationName = Path.GetFileName(file);

            await using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT COUNT(*) FROM MigrationHistory WHERE MigrationName = @Name";
                command.Parameters.AddWithValue("@Name", migrationName);
                var result = await command.ExecuteScalarAsync();
                var count = result != null ? (int)result : 0;

                if (count > 0)
                {
                    logger.LogDebug("Migration {Migration} already applied", migrationName);
                    continue;
                }
            }

            logger.LogInformation("Applying migration: {Migration}", migrationName);
            var sql = await File.ReadAllTextAsync(file);

            // Split on GO statements (SQL Server batch separator) – case-insensitive, handles varied whitespace
            var batches = Regex.Split(sql, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            foreach (var batch in batches)
            {
                var trimmed = batch.Trim();
                if (string.IsNullOrWhiteSpace(trimmed)) continue;

                await using var command = connection.CreateCommand();
                command.CommandText = trimmed;
                await command.ExecuteNonQueryAsync();
            }

            await using (var command = connection.CreateCommand())
            {
                command.CommandText = "INSERT INTO MigrationHistory (MigrationName) VALUES (@Name)";
                command.Parameters.AddWithValue("@Name", migrationName);
                await command.ExecuteNonQueryAsync();
            }

            logger.LogInformation("Migration {Migration} applied successfully", migrationName);
        }
    }
}
