namespace ExpressRecipe.ProfileService.Data;

/// <summary>
/// Placeholder for any future database management tasks specific to ProfileService.
/// Migrations are handled via MigrationExtensions from ExpressRecipe.Data.Common.
/// </summary>
public class DatabaseMigrator
{
    private readonly string _connectionString;
    private readonly ILogger<DatabaseMigrator> _logger;

    public DatabaseMigrator(string connectionString, ILogger<DatabaseMigrator> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public Task MigrateAsync()
    {
        _logger.LogInformation("ProfileService database migrator invoked");
        return Task.CompletedTask;
    }
}
