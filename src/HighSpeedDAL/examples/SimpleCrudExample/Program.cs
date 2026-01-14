using System;
using System.Linq;
using System.Threading.Tasks;
using HighSpeedDAL.Core.Interfaces;
using HighSpeedDAL.SimpleCrudExample.Data;
using HighSpeedDAL.SimpleCrudExample.Entities;
using HighSpeedDAL.SqlServer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HighSpeedDAL.SimpleCrudExample;

class Program
{
    static async Task Main(string[] args)
    {
        // Never emit audible notifications during console suite runs.
        // Some terminals/host environments map certain output patterns or calls to sounds.
        // We avoid using Console.Beep anywhere in the example app.

        // Setup dependency injection and configuration
        var services = new ServiceCollection();
        ConfigureServices(services);
        var serviceProvider = services.BuildServiceProvider();

        try
        {
            var userDal = serviceProvider.GetRequiredService<UserDal>();

            // Default behavior: run all demonstrations/test suites.
            // Use command-line switches to DISABLE specific suites.
            bool runFeatureShowcase = true;
            bool runCacheTests = true;
            bool runPerformanceTests = true;
            bool runMemoryMappedDemo = true;
            bool runGuidExamples = true;

            if (args.Any(a => string.Equals(a, "--help", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "-h", StringComparison.OrdinalIgnoreCase)))
            {
                ShowHelp();
                return;
            }

            if (args.Any(a => string.Equals(a, "--no-showcase", StringComparison.OrdinalIgnoreCase)))
            {
                runFeatureShowcase = false;
            }

            if (args.Any(a => string.Equals(a, "--no-cache-tests", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "--no-cache", StringComparison.OrdinalIgnoreCase)))
            {
                runCacheTests = false;
            }

            if (args.Any(a => string.Equals(a, "--no-performance-tests", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "--no-perf", StringComparison.OrdinalIgnoreCase)))
            {
                runPerformanceTests = false;
            }

            if (args.Any(a => string.Equals(a, "--no-memory-mapped-demo", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "--no-mmf", StringComparison.OrdinalIgnoreCase)))
            {
                runMemoryMappedDemo = false;
            }

            if (args.Any(a => string.Equals(a, "--no-guid-examples", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "--no-guid", StringComparison.OrdinalIgnoreCase)))
            {
                runGuidExamples = false;
            }

            if (runFeatureShowcase)
            {
                var showcase = new FeatureShowcase(userDal);
                await showcase.RunAllDemonstrationsAsync();
            }

            if (runCacheTests)
            {
                Console.WriteLine();
                Console.WriteLine("Running Cache Behavior Tests...");
                Console.WriteLine();
                var cacheSuite = new CacheStrategyTestSuite(userDal);
                await cacheSuite.RunAllTestsAsync();
            }

            if (runPerformanceTests)
            {
                Console.WriteLine();
                Console.WriteLine("Running Performance Test Suite...");
                Console.WriteLine();
                var perfSuite = new PerformanceBenchmarkSuite(userDal);
                await perfSuite.RunAllBenchmarksAsync();
            }

            if (runMemoryMappedDemo)
            {
                Console.WriteLine();
                Console.WriteLine("Running Memory-Mapped File demonstrations...");
                Console.WriteLine();
                var mmfSuite = serviceProvider.GetRequiredService<MemoryMappedTestSuite>();
                await mmfSuite.RunAllTestsAsync();
            }

            if (runGuidExamples)
            {
                Console.WriteLine();
                Console.WriteLine("Running Guid Primary Key Examples...");
                Console.WriteLine();
                var guidExample = serviceProvider.GetRequiredService<GuidPrimaryKeyExample>();
                await guidExample.RunAllExamplesAsync();
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine();
            Console.WriteLine("===========================================");
            Console.WriteLine("ERROR");
            Console.WriteLine("===========================================");
            Console.WriteLine($"Message: {ex.Message}");
            Console.WriteLine($"Type: {ex.GetType().Name}");
            Console.WriteLine();
            Console.WriteLine("Stack Trace:");
            Console.WriteLine(ex.StackTrace);
            Console.ResetColor();
        }

        Console.WriteLine();
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }

    static void ShowHelp()
    {
        Console.WriteLine("HighSpeedDAL - SimpleCrudExample");
        Console.WriteLine();
        Console.WriteLine("USAGE:");
        Console.WriteLine("  SimpleCrudExample [options]");
        Console.WriteLine();
        Console.WriteLine("OPTIONS:");
        Console.WriteLine("  (none)                    Run all suites (default)");
        Console.WriteLine("  --no-showcase             Skip feature showcase");
        Console.WriteLine("  --no-cache-tests          Skip cache behavior demonstrations");
        Console.WriteLine("  --no-cache                Alias for --no-cache-tests");
        Console.WriteLine("  --no-performance-tests    Skip comprehensive performance benchmarks");
        Console.WriteLine("  --no-perf                 Alias for --no-performance-tests");
        Console.WriteLine("  --no-memory-mapped-demo   Skip memory-mapped file demonstrations");
        Console.WriteLine("  --no-mmf                  Alias for --no-memory-mapped-demo");
        Console.WriteLine("  --no-guid-examples        Skip Guid primary key examples");
        Console.WriteLine("  --no-guid                 Alias for --no-guid-examples");
        Console.WriteLine("  --help, -h                Show this help message");
        Console.WriteLine();
        Console.WriteLine("EXAMPLES:");
        Console.WriteLine("  SimpleCrudExample");
        Console.WriteLine("  SimpleCrudExample --no-mmf");
        Console.WriteLine("  SimpleCrudExample --no-perf --no-cache");
        Console.WriteLine();
    }

    static void ConfigureServices(IServiceCollection services)
    {
        // Configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        services.AddSingleton<IConfiguration>(configuration);

        // Logging
        services.AddLogging(builder =>
        {
            builder.AddConfiguration(configuration.GetSection("Logging"));
            builder.AddConsole();
        });

        // HighSpeedDAL Components
        services.AddSingleton<IDbConnectionFactory, SqlServerConnectionFactory>();
        services.AddSingleton<UserDatabaseConnection>();

        // Retry policy
        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<Core.Resilience.DatabaseRetryPolicy>>();
            return new Core.Resilience.RetryPolicyFactory(logger, maxRetryAttempts: 3, delayMilliseconds: 100);
        });

                // Register UserDal (auto-generated by source generator)
                services.AddSingleton<UserDal>();
                services.AddSingleton<UserWithMemoryMappedDal>();
                services.AddSingleton<MemoryMappedTestSuite>();

                // Register Guid primary key DALs and example suite
                services.AddSingleton<UserWithGuidIdDal>();
                services.AddSingleton<OrderWithGuidDal>();
                services.AddSingleton<GuidPrimaryKeyExample>();
            }
        }
