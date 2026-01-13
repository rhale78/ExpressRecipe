using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HighSpeedDAL.SourceGenerators.Generation;
using HighSpeedDAL.SourceGenerators.Models;
using HighSpeedDAL.SourceGenerators.Parsing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace HighSpeedDAL.SourceGenerators;

/// <summary>
/// Source generator that creates DAL classes for entities marked with [DalEntity]
/// </summary>
[Generator]
public sealed class DalSourceGenerator : ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context)
    {
        // Register syntax receiver to find DAL entities
        context.RegisterForSyntaxNotifications(() => new DalSyntaxReceiver());
    }

    public void Execute(GeneratorExecutionContext context)
    {
        try
        {
            // Get the syntax receiver
            if (context.SyntaxReceiver is not DalSyntaxReceiver receiver)
            {
                return;
            }

            // Find all database connection classes
            Dictionary<string, string> connectionClasses = DiscoverConnectionClasses(receiver, context);

            // Deduplicate entities by symbol to handle partial classes
            HashSet<ISymbol> processedSymbols = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

            // Process each entity
            foreach (ClassDeclarationSyntax entityClass in receiver.EntityCandidates)
            {
                // Get symbol
                SemanticModel semanticModel = context.Compilation.GetSemanticModel(entityClass.SyntaxTree);
                INamedTypeSymbol? classSymbol = semanticModel.GetDeclaredSymbol(entityClass) as INamedTypeSymbol;

                // Skip if we've already processed this entity (e.g. other partial parts)
                if (classSymbol == null || !processedSymbols.Add(classSymbol))
                {
                    continue;
                }

                try
                {
                    ProcessEntity(context, entityClass, connectionClasses, receiver);
                }
                catch (Exception ex)
                {
                    // Report diagnostic for this entity
                    Diagnostic diagnostic = Diagnostic.Create(
                        new DiagnosticDescriptor(
                            "HSDAL001",
                            "Error processing entity",
                            $"Error processing entity {{0}}: {{1}}",
                            "HighSpeedDAL",
                            DiagnosticSeverity.Error,
                            isEnabledByDefault: true),
                        entityClass.GetLocation(),
                        entityClass.Identifier.Text,
                        ex.Message);

                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
        catch (Exception ex)
        {
            // Report general diagnostic
            Diagnostic diagnostic = Diagnostic.Create(
                new DiagnosticDescriptor(
                    "HSDAL000",
                    "Source generator error",
                    $"HighSpeedDAL source generator error: {{0}}",
                    "HighSpeedDAL",
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                Location.None,
                ex.Message);

            context.ReportDiagnostic(diagnostic);
        }
    }

    private Dictionary<string, string> DiscoverConnectionClasses(
        DalSyntaxReceiver receiver,
        GeneratorExecutionContext context)
    {
        Dictionary<string, string> connectionClasses = new Dictionary<string, string>();

        // Report how many connection candidates were found
        if (receiver.ConnectionCandidates.Count == 0)
        {
            Diagnostic noCandidatesDiag = Diagnostic.Create(
                new DiagnosticDescriptor(
                    "HSDAL202",
                    "No connection candidates found",
                    "DalSyntaxReceiver found 0 connection candidates. Syntax trees: {0}. Classes found: {1}",
                    "HighSpeedDAL",
                    DiagnosticSeverity.Warning,
                    isEnabledByDefault: true),
                Location.None,
                context.Compilation.SyntaxTrees.Count(),
                string.Join(", ", receiver.AllClassesFound));

            context.ReportDiagnostic(noCandidatesDiag);
        }

        foreach (ClassDeclarationSyntax connectionClass in receiver.ConnectionCandidates)
        {
            SemanticModel model = context.Compilation.GetSemanticModel(connectionClass.SyntaxTree);
            INamedTypeSymbol? classSymbol = model.GetDeclaredSymbol(connectionClass) as INamedTypeSymbol;

            if (classSymbol != null)
            {
                string fullName = classSymbol.ToDisplayString();
                string className = classSymbol.Name;
                connectionClasses[className] = fullName;
            }
        }

        return connectionClasses;
    }

    private void ProcessEntity(
        GeneratorExecutionContext context,
        ClassDeclarationSyntax entityClass,
        Dictionary<string, string> connectionClasses,
        DalSyntaxReceiver receiver)
    {
        // Get semantic model
        SemanticModel semanticModel = context.Compilation.GetSemanticModel(entityClass.SyntaxTree);

        // Get class symbol (needed for property checking)
        INamedTypeSymbol? classSymbol = semanticModel.GetDeclaredSymbol(entityClass) as INamedTypeSymbol;
        if (classSymbol == null)
        {
            return;
        }

        // Parse entity metadata
        EntityParser parser = new EntityParser(semanticModel);
        EntityMetadata? metadata = parser.ParseEntity(entityClass);

        if (metadata == null)
        {
            return;
        }

        // Find the connection class for this entity's namespace
        string? connectionClassName = FindConnectionClass(metadata, connectionClasses);
        string? connectionClassFullName = null;

        if (!string.IsNullOrWhiteSpace(connectionClassName))
        {
            connectionClassFullName = connectionClasses[connectionClassName!];
        }

        if (string.IsNullOrWhiteSpace(connectionClassName))
        {
            // Report diagnostic about missing connection
            Diagnostic missingConnectionDiag = Diagnostic.Create(
                new DiagnosticDescriptor(
                    "HSDAL201",
                    "No connection class found",
                    $"No connection class found for entity {{0}} in namespace {{1}}. Available connections: {{2}}",
                    "HighSpeedDAL",
                    DiagnosticSeverity.Warning,
                    isEnabledByDefault: true),
                entityClass.GetLocation(),
                metadata.ClassName,
                metadata.Namespace,
                string.Join(", ", connectionClasses.Select(kvp => $"{kvp.Key}={kvp.Value}")));

            context.ReportDiagnostic(missingConnectionDiag);

            // Default to a generic connection name
            connectionClassName = "DatabaseConnection";
        }

        // Check if we need to generate missing audit/soft delete/primary key properties
        // IMPORTANT: Pass classSymbol to check ACTUAL declared properties, not metadata which may include auto-generated ones
        Utilities.PropertyAutoGenerator propertyGenerator = new Utilities.PropertyAutoGenerator(metadata, classSymbol);
        List<PropertyMetadata> missingProperties = propertyGenerator.GetMissingProperties();

        // Generate partial entity class with missing properties (if any)
        if (missingProperties.Count > 0)
        {
            EntityPropertyGenerator entityPropertyGen = new EntityPropertyGenerator(metadata, missingProperties);
            string entityPartialCode = entityPropertyGen.GeneratePartialClass();

            if (!string.IsNullOrWhiteSpace(entityPartialCode))
            {
                string entityFileName = $"{metadata.Namespace}.{metadata.ClassName}.g.cs";
                context.AddSource(entityFileName, SourceText.From(entityPartialCode, Encoding.UTF8));

                // Report property generation
                Diagnostic propertyDiagnostic = Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "HSDAL101",
                        "Entity properties generated",
                        $"Generated {{0}} missing properties for {{1}}",
                        "HighSpeedDAL",
                        DiagnosticSeverity.Info,
                        isEnabledByDefault: true),
                    entityClass.GetLocation(),
                    missingProperties.Count,
                    metadata.ClassName);

                context.ReportDiagnostic(propertyDiagnostic);
            }

            // Add the generated properties to metadata so DAL generator knows about them
            foreach (PropertyMetadata prop in missingProperties)
            {
                // Defensive: ensure we don't duplicate properties if metadata already contains them.
                // Duplication can lead to invalid generated code (e.g., duplicate clone initializers
                // and duplicate data-reader ordinals such as ordId).
                if (!metadata.Properties.Any(p => string.Equals(p.PropertyName, prop.PropertyName, StringComparison.Ordinal)))
                {
                    metadata.Properties.Add(prop);
                }
            }
        }

        // ALWAYS generate clone methods for defensive copying in caches
        // NOTE: This must happen AFTER adding missing properties to metadata
        EntityPropertyGenerator cloneMethodsGen = new EntityPropertyGenerator(metadata, new List<PropertyMetadata>());
        string cloneMethodsCode = cloneMethodsGen.GenerateCloneMethods();

        if (!string.IsNullOrWhiteSpace(cloneMethodsCode))
        {
            string cloneFileName = $"{metadata.Namespace}.{metadata.ClassName}.Clone.g.cs";
            context.AddSource(cloneFileName, SourceText.From(cloneMethodsCode, Encoding.UTF8));
        }

        // Detect database provider
        // Prefer explicit MSBuild property to avoid mismatches when both providers are referenced.
        // (e.g., examples/tests may reference both packages but run against a single provider.)
        DatabaseProvider dbProvider = DetectDatabaseProvider(context);

        // Generate DAL class with database provider information
        DalClassGenerator dalGenerator = new DalClassGenerator(metadata, connectionClassName!, connectionClassFullName, dbProvider);
        string dalClassCode = dalGenerator.GenerateDalClass();

        // Add the generated source to the compilation
        string fileName = $"{metadata.ClassName}Dal.g.cs";
        context.AddSource(fileName, SourceText.From(dalClassCode, Encoding.UTF8));

        // Report successful generation
        Diagnostic diagnostic = Diagnostic.Create(
            new DiagnosticDescriptor(
                "HSDAL100",
                "DAL class generated",
                $"Generated DAL class for {{0}}",
                "HighSpeedDAL",
                DiagnosticSeverity.Info,
                isEnabledByDefault: true),
            entityClass.GetLocation(),
            metadata.ClassName);

        context.ReportDiagnostic(diagnostic);

        // Generate service registration extension method
        if (receiver.EntityCandidates.Last() == entityClass)
        {
            GenerateServiceRegistration(context, receiver, semanticModel);
        }
    }

    private string? FindConnectionClass(EntityMetadata metadata, Dictionary<string, string> connectionClasses)
    {
        // Look for a connection class in the same namespace or parent namespace
        string entityNamespace = metadata.Namespace;

        // Try exact namespace match first
        foreach (KeyValuePair<string, string> kvp in connectionClasses)
        {
            if (kvp.Value.StartsWith(entityNamespace))
            {
                return kvp.Key;
            }
        }

        // If no match, try parent namespace (e.g., Entity in "App.Entities", Connection in "App.Data")
        int lastDot = entityNamespace.LastIndexOf('.');
        if (lastDot > 0)
        {
            string parentNamespace = entityNamespace.Substring(0, lastDot);
            foreach (KeyValuePair<string, string> kvp in connectionClasses)
            {
                if (kvp.Value.StartsWith(parentNamespace))
                {
                    return kvp.Key;
                }
            }
        }

        // Try root namespace match (e.g., both start with same root)
        if (entityNamespace.Contains("."))
        {
            string rootNamespace = entityNamespace.Split('.')[0];
            foreach (KeyValuePair<string, string> kvp in connectionClasses)
            {
                if (kvp.Value.StartsWith(rootNamespace))
                {
                    return kvp.Key;
                }
            }
        }

        // Return first connection class if any found
        if (connectionClasses.Count > 0)
        {
            return connectionClasses.First().Key;
        }

        // Default fallback
        return null;
    }

    private void GenerateServiceRegistration(
        GeneratorExecutionContext context,
        DalSyntaxReceiver receiver,
        SemanticModel semanticModel)
    {
        StringBuilder code = new StringBuilder();
        HashSet<string> entityNamespaces = new HashSet<string>();

        // Collect all entity namespaces
        foreach (ClassDeclarationSyntax entityClass in receiver.EntityCandidates)
        {
            SemanticModel model = context.Compilation.GetSemanticModel(entityClass.SyntaxTree);
            INamedTypeSymbol? classSymbol = model.GetDeclaredSymbol(entityClass) as INamedTypeSymbol;
            if (classSymbol != null)
            {
                entityNamespaces.Add(classSymbol.ContainingNamespace.ToDisplayString());
            }
        }

        code.AppendLine("// <auto-generated />");
        code.AppendLine("#nullable enable");
        code.AppendLine();
        code.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        foreach (string ns in entityNamespaces.OrderBy(n => n))
        {
            code.AppendLine($"using {ns};");
        }
        code.AppendLine();
        code.AppendLine("namespace HighSpeedDAL.Generated;");
        code.AppendLine();
        code.AppendLine("/// <summary>");
        code.AppendLine("/// Extension methods for registering generated DAL classes");
        code.AppendLine("/// </summary>");
        code.AppendLine("public static class DalServiceRegistration");
        code.AppendLine("{");
        code.AppendLine("    /// <summary>");
        code.AppendLine("    /// Registers all generated DAL classes with dependency injection");
        code.AppendLine("    /// </summary>");
        code.AppendLine("    public static IServiceCollection AddGeneratedDalServices(this IServiceCollection services)");
        code.AppendLine("    {");

        HashSet<string> registeredClasses = new HashSet<string>();

        // Register each DAL class
        foreach (ClassDeclarationSyntax entityClass in receiver.EntityCandidates)
        {
            SemanticModel model = context.Compilation.GetSemanticModel(entityClass.SyntaxTree);
            INamedTypeSymbol? classSymbol = model.GetDeclaredSymbol(entityClass) as INamedTypeSymbol;

            if (classSymbol != null)
            {
                string className = classSymbol.Name;

                if (!registeredClasses.Add(className))
                {
                    continue;
                }

                string dalClassName = $"{className}Dal";

                code.AppendLine($"        services.AddScoped<{dalClassName}>();");
            }
        }

        code.AppendLine();
        code.AppendLine("        return services;");
        code.AppendLine("    }");
        code.AppendLine("}");

        context.AddSource("DalServiceRegistration.g.cs", SourceText.From(code.ToString(), Encoding.UTF8));
    }

    /// <summary>
    /// Detects which database provider assemblies are referenced in the compilation.
    /// Priority: Sqlite > SqlServer (if both referenced, Sqlite wins for compatibility)
    /// </summary>
    private static DatabaseProvider DetectDatabaseProvider(GeneratorExecutionContext context)
    {
        // 1) Explicit override from MSBuild (AnalyzerConfigOptions)
        // Set in csproj as: <HighSpeedDAL_DatabaseProvider>SqlServer</HighSpeedDAL_DatabaseProvider>
        if (context.AnalyzerConfigOptions.GlobalOptions.TryGetValue(
                "build_property.HighSpeedDAL_DatabaseProvider",
                out string? providerOverride) &&
            !string.IsNullOrWhiteSpace(providerOverride))
        {
            if (string.Equals(providerOverride, "Sqlite", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(providerOverride, "SQLite", StringComparison.OrdinalIgnoreCase))
            {
                return DatabaseProvider.Sqlite;
            }

            if (string.Equals(providerOverride, "SqlServer", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(providerOverride, "Mssql", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(providerOverride, "MSSQL", StringComparison.OrdinalIgnoreCase))
            {
                return DatabaseProvider.SqlServer;
            }
        }

        // 2) Fallback: infer from referenced assemblies
        bool hasSqlite = false;
        bool hasSqlServer = false;

        // Check referenced assemblies
        foreach (MetadataReference reference in context.Compilation.References)
        {
            if (reference is not PortableExecutableReference peReference)
            {
                continue;
            }

            string? assemblyName = peReference.Display;
            if (string.IsNullOrWhiteSpace(assemblyName))
            {
                continue;
            }

            // Check for Sqlite assembly
            if (assemblyName.Contains("HighSpeedDAL.Sqlite", StringComparison.OrdinalIgnoreCase) ||
                assemblyName.Contains("Microsoft.Data.Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                hasSqlite = true;
            }

            // Check for SQL Server assembly
            if (assemblyName.Contains("HighSpeedDAL.SqlServer", StringComparison.OrdinalIgnoreCase) ||
                assemblyName.Contains("Microsoft.Data.SqlClient", StringComparison.OrdinalIgnoreCase))
            {
                hasSqlServer = true;
            }
        }

        // Priority: SqlServer > Sqlite (matches production/default expectation)
        // Note: when both are referenced, callers should set HighSpeedDAL_DatabaseProvider.
        if (hasSqlServer)
        {
            return DatabaseProvider.SqlServer;
        }

        if (hasSqlite)
        {
            return DatabaseProvider.Sqlite;
        }

        // Default to SqlServer if no specific provider detected
        return DatabaseProvider.SqlServer;
    }
}
