using Microsoft.AspNetCore.Builder;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ExpressRecipe.Data.Common
{
    public class DatabaseManagementSettings
    {
        public bool DropDatabasesOnStartup { get; set; }
        public bool DropTablesOnStartup { get; set; }
        public bool RecreateSchemaOnStartup { get; set; }
        public bool RunMigrationsOnStartup { get; set; } = true;
        public bool SeedDataOnStartup { get; set; }
        public int DatabaseOperationTimeout { get; set; } = 300;
        public Dictionary<string, ServiceDatabaseSettings> Services { get; set; } = [];
    }

    public class ServiceDatabaseSettings
    {
        public string DatabaseName { get; set; } = string.Empty;
        public bool EnableManagement { get; set; }
    }

    public class DatabaseManager
    {
        private readonly string _serverConnectionString;
        private readonly ILogger<DatabaseManager>? _logger;
        private readonly DatabaseManagementSettings _settings;

        public DatabaseManager(
            string serverConnectionString,
            DatabaseManagementSettings settings,
            ILogger<DatabaseManager>? logger = null)
        {
            _serverConnectionString = serverConnectionString;
            _settings = settings;
            _logger = logger;
        }

        public async Task ManageDatabaseAsync(string serviceKey, string databaseName)
        {
            if (_settings.Services.TryGetValue(serviceKey, out ServiceDatabaseSettings? serviceSettings))
            {
                if (!serviceSettings.EnableManagement)
                {
                    _logger?.LogInformation("Database management disabled for {ServiceKey}", serviceKey);
                    return;
                }

                if (!string.IsNullOrWhiteSpace(serviceSettings.DatabaseName))
                {
                    databaseName = serviceSettings.DatabaseName;
                }
            }

            try
            {
                if (_settings.DropDatabasesOnStartup)
                {
                    _logger?.LogWarning("Dropping database {DatabaseName} for {ServiceKey}", databaseName, serviceKey);
                    await DropDatabaseAsync(databaseName);
                }

                // Ensure database exists
                await EnsureDatabaseExistsAsync(databaseName);

                // Optionally drop all tables
                if (_settings.DropTablesOnStartup)
                {
                    await DropAllTablesAsync(databaseName);
                }

                if (_settings.RecreateSchemaOnStartup)
                {
                    _logger?.LogWarning("Recreating schema for {DatabaseName}", databaseName);
                    // Schema recreation is handled by migrations after drop tables
                }

                _logger?.LogInformation("Database {DatabaseName} ready for {ServiceKey}", databaseName, serviceKey);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to manage database {DatabaseName} for {ServiceKey}", databaseName, serviceKey);
                throw;
            }
        }

        public async Task DropDatabaseAsync(string databaseName)
        {
            try
            {
                // First, try to set to MULTI_USER in case it's stuck in SINGLE_USER mode
                var resetSql = $@"
                IF EXISTS (SELECT name FROM sys.databases WHERE name = N'{databaseName}')
                BEGIN
                    ALTER DATABASE [{databaseName}] SET MULTI_USER WITH ROLLBACK IMMEDIATE;
                END";

                await using SqlConnection resetConnection = new SqlConnection(_serverConnectionString);
                await resetConnection.OpenAsync();

                await using (SqlCommand resetCommand = new SqlCommand(resetSql, resetConnection)
                {
                    CommandTimeout = _settings.DatabaseOperationTimeout
                })
                {
                    try
                    {
                        await resetCommand.ExecuteNonQueryAsync();
                        _logger?.LogDebug("Reset {DatabaseName} to MULTI_USER mode", databaseName);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Could not reset {DatabaseName} to MULTI_USER (may not exist)", databaseName);
                    }
                }

                // Now drop the database
                var dropSql = $@"
                IF EXISTS (SELECT name FROM sys.databases WHERE name = N'{databaseName}')
                BEGIN
                    ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [{databaseName}];
                END";

                await using SqlCommand dropCommand = new SqlCommand(dropSql, resetConnection)
                {
                    CommandTimeout = _settings.DatabaseOperationTimeout
                };
                await dropCommand.ExecuteNonQueryAsync();

                _logger?.LogWarning("Dropped database: {DatabaseName}", databaseName);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to drop database {DatabaseName}. Database may be left in SINGLE_USER mode. Run scripts/fix-single-user-databases.cmd to recover.", databaseName);

                // Try to recover by setting back to MULTI_USER
                try
                {
                    var recoverSql = $@"
                    IF EXISTS (SELECT name FROM sys.databases WHERE name = N'{databaseName}')
                    BEGIN
                        ALTER DATABASE [{databaseName}] SET MULTI_USER WITH ROLLBACK IMMEDIATE;
                    END";

                    await using SqlConnection recoverConnection = new SqlConnection(_serverConnectionString);
                    await recoverConnection.OpenAsync();
                    await using SqlCommand recoverCommand = new SqlCommand(recoverSql, recoverConnection)
                    {
                        CommandTimeout = _settings.DatabaseOperationTimeout
                    };
                    await recoverCommand.ExecuteNonQueryAsync();

                    _logger?.LogInformation("Recovered {DatabaseName} back to MULTI_USER mode", databaseName);
                }
                catch (Exception recoverEx)
                {
                    _logger?.LogError(recoverEx, "Could not recover {DatabaseName}. Manual intervention required: run scripts/fix-single-user-databases.cmd", databaseName);
                }

                throw;
            }
        }

        public async Task EnsureDatabaseExistsAsync(string databaseName)
        {
            var sql = $@"
            IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'{databaseName}')
            BEGIN
                CREATE DATABASE [{databaseName}];
            END";

            await using SqlConnection connection = new SqlConnection(_serverConnectionString);
            await connection.OpenAsync();

            await using SqlCommand command = new SqlCommand(sql, connection)
            {
                CommandTimeout = _settings.DatabaseOperationTimeout
            };
            await command.ExecuteNonQueryAsync();

            _logger?.LogInformation("Ensured database exists: {DatabaseName}", databaseName);
        }

        public async Task DropAllDatabasesAsync()
        {
            _logger?.LogCritical("Dropping ALL ExpressRecipe databases!");

            var databases = new[]
            {
                "ExpressRecipe.Auth",
                "ExpressRecipe.Users",
                "ExpressRecipe.Products",
                "ExpressRecipe.Recipes",
                "ExpressRecipe.Inventory",
                "ExpressRecipe.Scans",
                "ExpressRecipe.Shopping",
                "ExpressRecipe.MealPlanning",
                "ExpressRecipe.Pricing",
                "ExpressRecipe.Recalls",
                "ExpressRecipe.Notifications",
                "ExpressRecipe.Community",
                "ExpressRecipe.Sync",
                "ExpressRecipe.Search",
                "ExpressRecipe.Analytics"
            };

            foreach (var db in databases)
            {
                try
                {
                    await DropDatabaseAsync(db);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to drop database {DatabaseName}", db);
                }
            }
        }

        public static DatabaseManagementSettings GetSettings(IConfiguration configuration)
        {
            DatabaseManagementSettings settings = new DatabaseManagementSettings();
            configuration.GetSection("DatabaseManagement").Bind(settings);
            return settings;
        }

        private async Task DropAllTablesAsync(string databaseName)
        {
            _logger?.LogWarning("Dropping all tables in {DatabaseName}", databaseName);

            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(_serverConnectionString)
            {
                InitialCatalog = databaseName
            };

            const string dropSql = @"
            DECLARE @sql NVARCHAR(MAX) = N'';

            -- Drop foreign keys
            SELECT @sql += N'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(parent_object_id)) + N'.' + QUOTENAME(OBJECT_NAME(parent_object_id)) +
                           N' DROP CONSTRAINT ' + QUOTENAME(name) + N';'
            FROM sys.foreign_keys;

            -- Drop views
            SELECT @sql += N'DROP VIEW ' + QUOTENAME(SCHEMA_NAME(schema_id)) + N'.' + QUOTENAME(name) + N';'
            FROM sys.views;

            -- Drop tables
            SELECT @sql += N'DROP TABLE ' + QUOTENAME(SCHEMA_NAME(schema_id)) + N'.' + QUOTENAME(name) + N';'
            FROM sys.tables;

            -- Drop stored procedures
            SELECT @sql += N'DROP PROCEDURE ' + QUOTENAME(SCHEMA_NAME(schema_id)) + N'.' + QUOTENAME(name) + N';'
            FROM sys.procedures;

            -- Drop functions
            SELECT @sql += N'DROP FUNCTION ' + QUOTENAME(SCHEMA_NAME(schema_id)) + N'.' + QUOTENAME(name) + N';'
            FROM sys.objects
            WHERE type IN ('FN','IF','TF');

            EXEC sp_executesql @sql;";


            await using SqlConnection connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync();

            await using SqlCommand command = new SqlCommand(dropSql, connection)
            {
                CommandTimeout = _settings.DatabaseOperationTimeout
            };
            await command.ExecuteNonQueryAsync();

            // Reset migration history after dropping all objects
            const string resetHistory = @"
            IF OBJECT_ID('[dbo].[__MigrationHistory]', 'U') IS NOT NULL
            BEGIN
                DROP TABLE [dbo].[__MigrationHistory];
            END";

            await using SqlCommand resetCmd = new SqlCommand(resetHistory, connection)
            {
                CommandTimeout = _settings.DatabaseOperationTimeout
            };
            await resetCmd.ExecuteNonQueryAsync();

            _logger?.LogWarning("All tables dropped in {DatabaseName}", databaseName);
        }
    }

    public static class DatabaseManagementExtensions
    {
        public static async Task RunDatabaseManagementAsync(
            this WebApplication app,
            string serviceKey,
            string connectionName)
        {
            DatabaseManagementSettings settings = DatabaseManager.GetSettings(app.Configuration);

            if (!settings.DropDatabasesOnStartup &&
                !settings.DropTablesOnStartup &&
                !settings.RecreateSchemaOnStartup &&
                !settings.SeedDataOnStartup)
            {
                return;
            }

            var dbConnectionString = app.Configuration.GetConnectionString(connectionName);
            if (string.IsNullOrEmpty(dbConnectionString))
            {
                return;
            }

            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(dbConnectionString)
            {
                InitialCatalog = new SqlConnectionStringBuilder(dbConnectionString).InitialCatalog
            };
            var databaseName = builder.InitialCatalog;
            builder.InitialCatalog = "master";

            // Fix: Add encryption settings to prevent pre-login handshake errors
            // SQL Server 2022 uses self-signed certificates by default
            builder.TrustServerCertificate = true;

            var serverConnectionString = builder.ConnectionString;

            ILoggerFactory? loggerFactory = app.Services.GetService<ILoggerFactory>();
            ILogger<DatabaseManager>? logger = loggerFactory?.CreateLogger<DatabaseManager>();
            DatabaseManager manager = new DatabaseManager(serverConnectionString, settings, logger);

            await manager.ManageDatabaseAsync(serviceKey, databaseName);
        }
    }
}
