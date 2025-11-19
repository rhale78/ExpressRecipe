using Microsoft.Data.SqlClient;

namespace ExpressRecipe.InventoryService.Data;

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
        var migrationsDir = Path.Combine(AppContext.BaseDirectory, "Data", "Migrations");
        if (!Directory.Exists(migrationsDir))
        {
            _logger.LogWarning("Migrations directory not found: {Directory}", migrationsDir);
            return;
        }

        var migrationFiles = Directory.GetFiles(migrationsDir, "*.sql")
            .OrderBy(f => f)
            .ToList();

        if (!migrationFiles.Any())
        {
            _logger.LogInformation("No migration files found");
            return;
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        // Create migrations table if it doesn't exist
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
            
            // Check if migration already applied
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT COUNT(*) FROM MigrationHistory WHERE MigrationName = @Name";
                command.Parameters.AddWithValue("@Name", migrationName);
                var count = (int)await command.ExecuteScalarAsync()!;
                
                if (count > 0)
                {
                    _logger.LogDebug("Migration {Migration} already applied", migrationName);
                    continue;
                }
            }

            // Apply migration
            _logger.LogInformation("Applying migration: {Migration}", migrationName);
            var sql = await File.ReadAllTextAsync(file);
            
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                await command.ExecuteNonQueryAsync();
            }

            // Record migration
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = "INSERT INTO MigrationHistory (MigrationName) VALUES (@Name)";
                command.Parameters.AddWithValue("@Name", migrationName);
                await command.ExecuteNonQueryAsync();
            }

            _logger.LogInformation("Migration {Migration} applied successfully", migrationName);
        }
    }
}
