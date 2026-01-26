using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace ExpressRecipe.Data.Common
{
    /// <summary>
    /// Extension methods for loading layered configuration from the shared Config directory
    /// </summary>
    public static class ConfigurationExtensions
    {
        /// <summary>
        /// Adds layered configuration from the shared Config directory.
        /// Loading order (last wins):
        /// 1. Config/appsettings.Global.json (base settings for all services)
        /// 2. Config/appsettings.{Environment}.json (environment-specific overrides)
        /// 3. Local appsettings.json (service-specific settings)
        /// 4. Local appsettings.{Environment}.json (service + environment specific)
        /// 5. Environment variables (highest priority)
        /// 6. Command line arguments (if provided)
        /// </summary>
        public static IConfigurationBuilder AddLayeredConfiguration(
            this IConfigurationBuilder builder,
            IHostEnvironment environment,
            string[]? args = null)
        {
            // Get the solution root directory (assumes Config folder is at solution root)
            var currentDirectory = Directory.GetCurrentDirectory();
            var solutionRoot = FindSolutionRoot(currentDirectory);
            var configDirectory = Path.Combine(solutionRoot, "Config");

            // Verify Config directory exists
            if (!Directory.Exists(configDirectory))
            {
                throw new DirectoryNotFoundException(
                    $"Config directory not found at: {configDirectory}. " +
                    $"Please ensure the Config folder exists at the solution root.");
            }

            var environmentName = environment.EnvironmentName;

            // Load in order of precedence (last wins)
        
            // 1. Global configuration (applies to all services)
            var globalConfigPath = Path.Combine(configDirectory, "appsettings.Global.json");
            builder.AddJsonFile(globalConfigPath, optional: false, reloadOnChange: true);

            // 2. Environment-specific global configuration
            var globalEnvConfigPath = Path.Combine(configDirectory, $"appsettings.{environmentName}.json");
            builder.AddJsonFile(globalEnvConfigPath, optional: true, reloadOnChange: true);

            // 3. Optional: Database management configuration
            var dbMgmtConfigPath = Path.Combine(configDirectory, "appsettings.DatabaseManagement.json");
            builder.AddJsonFile(dbMgmtConfigPath, optional: true, reloadOnChange: true);

            // 4. Local service configuration (in service directory)
            builder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

            // 5. Local environment-specific configuration
            builder.AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: true);

            // 6. Environment variables (highest priority for secrets)
            builder.AddEnvironmentVariables();

            // 7. Command line arguments (if provided)
            if (args != null && args.Length > 0)
            {
                builder.AddCommandLine(args);
            }

            return builder;
        }

        /// <summary>
        /// Adds layered configuration for WebApplicationBuilder
        /// </summary>
        public static WebApplicationBuilder AddLayeredConfiguration(this WebApplicationBuilder builder, string[]? args = null)
        {
            // Clear existing configuration sources (except the ones we want to keep)
            List<IConfigurationSource> existingSources = builder.Configuration.Sources.ToList();
            builder.Configuration.Sources.Clear();

            // Add layered configuration
            builder.Configuration.AddLayeredConfiguration(builder.Environment, args);

            return builder;
        }

        /// <summary>
        /// Adds layered configuration for HostApplicationBuilder (Aspire services)
        /// </summary>
        public static IHostApplicationBuilder AddLayeredConfiguration(this IHostApplicationBuilder builder, string[]? args = null)
        {
            // Get the solution root directory
            var currentDirectory = Directory.GetCurrentDirectory();
            var solutionRoot = FindSolutionRoot(currentDirectory);
            var configDirectory = Path.Combine(solutionRoot, "Config");

            // Verify Config directory exists
            if (!Directory.Exists(configDirectory))
            {
                throw new DirectoryNotFoundException(
                    $"Config directory not found at: {configDirectory}");
            }

            var environmentName = builder.Environment.EnvironmentName;

            // Load global configurations first
            builder.Configuration.AddJsonFile(
                Path.Combine(configDirectory, "appsettings.Global.json"),
                optional: false,
                reloadOnChange: true);

            builder.Configuration.AddJsonFile(
                Path.Combine(configDirectory, $"appsettings.{environmentName}.json"),
                optional: true,
                reloadOnChange: true);

            // Also include DatabaseManagement config
            builder.Configuration.AddJsonFile(
                Path.Combine(configDirectory, "appsettings.DatabaseManagement.json"),
                optional: true,
                reloadOnChange: true);

            // Local configs are already loaded by default
            // Just add environment variables and command line
            if (args != null && args.Length > 0)
            {
                builder.Configuration.AddCommandLine(args);
            }

            return builder;
        }

        /// <summary>
        /// Finds the solution root directory by looking for the .sln file or Config directory
        /// </summary>
        private static string FindSolutionRoot(string startDirectory)
        {
            DirectoryInfo? directory = new DirectoryInfo(startDirectory);

            while (directory != null)
            {
                // Check if Config directory exists
                if (Directory.Exists(Path.Combine(directory.FullName, "Config")))
                {
                    return directory.FullName;
                }

                // Check if .sln file exists
                if (directory.GetFiles("*.sln").Length > 0)
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            // Fallback: go up 3 levels from typical service location
            // (e.g., src/Services/ServiceName -> root)
            var fallbackPath = Path.GetFullPath(Path.Combine(startDirectory, "..", "..", ".."));
            return Directory.Exists(Path.Combine(fallbackPath, "Config"))
                ? fallbackPath
                : throw new DirectoryNotFoundException(
                $"Could not find solution root starting from: {startDirectory}. " +
                $"Looked for .sln file or Config directory.");
        }
    }
}
