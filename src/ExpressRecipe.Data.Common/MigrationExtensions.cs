using Microsoft.AspNetCore.Builder;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ExpressRecipe.Data.Common;

/// <summary>
/// Extension methods for running database migrations
/// </summary>
public static class MigrationExtensions
{
    /// <summary>
    /// Runs database migrations from embedded SQL files or provided dictionary
    /// </summary>
    /// <param name="app">The application builder</param>
    /// <param name="connectionString">Database connection string</param>
    /// <param name="migrations">Dictionary of migration ID to SQL script content</param>
    public static async Task RunMigrationsAsync(
        this WebApplication app,
        string connectionString,
        IDictionary<string, string> migrations)
    {
        using var scope = app.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<MigrationRunner>>();

        logger.LogInformation("Starting database migrations...");

        // Wait for database to be available (helpful when using containers)
        await WaitForDatabaseAsync(connectionString, logger);

        var runner = new MigrationRunner(connectionString, logger);
        await runner.ApplyMigrationsAsync(migrations);

        logger.LogInformation("Database migrations completed");
    }

    /// <summary>
    /// Waits for the database to be available with retry logic
    /// </summary>
    private static async Task WaitForDatabaseAsync(string connectionString, ILogger logger, int maxRetries = 10)
    {
        var retryCount = 0;
        var delay = TimeSpan.FromSeconds(2);

        while (retryCount < maxRetries)
        {
            try
            {
                await using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();
                logger.LogInformation("Database connection successful");
                return;
            }
            catch (SqlException ex)
            {
                retryCount++;
                if (retryCount >= maxRetries)
                {
                    logger.LogError(ex, "Failed to connect to database after {MaxRetries} attempts", maxRetries);
                    throw;
                }

                logger.LogWarning(
                    "Database not available (attempt {RetryCount}/{MaxRetries}). Retrying in {Delay} seconds...",
                    retryCount, maxRetries, delay.TotalSeconds);

                await Task.Delay(delay);
                delay = TimeSpan.FromSeconds(delay.TotalSeconds * 1.5); // Exponential backoff
            }
        }
    }

    /// <summary>
    /// Loads migration scripts from the Data/Migrations directory
    /// </summary>
    /// <param name="migrationsPath">Path to migrations directory</param>
    /// <returns>Dictionary of migration ID to SQL script</returns>
    public static Dictionary<string, string> LoadMigrationsFromDirectory(string migrationsPath)
    {
        var migrations = new Dictionary<string, string>();

        if (!Directory.Exists(migrationsPath))
        {
            return migrations;
        }

        var sqlFiles = Directory.GetFiles(migrationsPath, "*.sql")
            .OrderBy(f => f);

        foreach (var file in sqlFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            var content = File.ReadAllText(file);
            migrations[fileName] = content;
        }

        return migrations;
    }
}
