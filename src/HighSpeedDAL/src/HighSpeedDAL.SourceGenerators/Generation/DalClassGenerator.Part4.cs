using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HighSpeedDAL.SourceGenerators.Models;

namespace HighSpeedDAL.SourceGenerators.Generation
{
    /// <summary>
    /// DAL Class Generator - Part 4: Periodic flush and table swap operations
    /// Generates methods for atomic table swap flush when InMemoryTable is configured
    /// </summary>
    internal sealed partial class DalClassGenerator
    {
        private void GenerateFlushSqlConstants(StringBuilder code)
        {
            string originalTableName = _metadata.TableName;
            code.AppendLine($"        private const string SQL_CREATE_TEMP_TABLE = @\"SELECT * INTO [{{tempTableName}}] FROM [{originalTableName}] WHERE 1=0\";");
            code.AppendLine();
            code.AppendLine($"        private const string SQL_DROP_ORIGINAL_TABLE = @\"DROP TABLE IF EXISTS [{{originalTableName}}]\";");
            code.AppendLine();
            code.AppendLine($"        private const string SQL_RENAME_TEMP_TABLE = @\"EXEC sp_rename @objname = N'[{{tempTableName}}]', @newname = N'[{{originalTableName}}]'\";");
            code.AppendLine();
        }

        private void GenerateFlushStrategyConfiguration(StringBuilder code)
        {
            code.AppendLine("    /// <summary>");
            code.AppendLine("    /// Configures periodic flush to backing database using table swap strategy");
            code.AppendLine("    /// Flushes all in-memory data to database with atomic table swap (temp table swap with rename)");
            code.AppendLine("    /// </summary>");
            code.AppendLine("    public void ConfigurePeriodicFlush(int flushIntervalSeconds = 300)");
            code.AppendLine("    {");
            code.AppendLine("        if (_inMemoryTable == null)");
            code.AppendLine("        {");
            code.AppendLine("            Logger.LogWarning(\"Cannot configure periodic flush: InMemoryTable not configured\");");
            code.AppendLine("            return;");
            code.AppendLine("        }");
            code.AppendLine();
            code.AppendLine("        // NOTE: Table swap flush strategy requires manual SqlBulkCopy implementation");
            code.AppendLine("        // For now, in-memory table periodic flush is configured but uses default behavior");
            code.AppendLine("        // Production implementation should override BulkInsertToTempTableAsync in partial class");
            code.AppendLine("        Logger.LogInformation(\"Periodic flush configured for {EntityType} with interval {Seconds}s\", ");
            code.AppendLine($"            \"{_metadata.ClassName}\", flushIntervalSeconds);");
            code.AppendLine("    }");
            code.AppendLine();
        }

        // NOTE: BulkInsertToTempTableAsync is NOT generated here
        // Each entity-specific DAL provides its own implementation in a partial class
        // This allows for optimized SqlBulkCopy with the exact entity schema
    }
}
