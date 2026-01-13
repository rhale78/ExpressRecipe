using System;

namespace HighSpeedDAL.Core.Attributes;

/// <summary>
/// Marks a class as a reference/lookup table with optional CSV preload support.
/// Reference tables are typically small, static datasets loaded on startup.
/// 
/// HighSpeedDAL Framework v0.1
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ReferenceTableAttribute : Attribute
{
    /// <summary>
    /// Optional path to CSV file for data preload.
    /// - Relative paths (e.g., "Data/States.csv") are resolved from application root
    /// - Absolute paths (e.g., "C:\Data\States.csv" or "/data/states.csv") are used as-is
    /// - If file not found at relative path, attempts absolute resolution
    /// </summary>
    public string CsvFilePath { get; set; }

    /// <summary>
    /// Strategy for merging CSV data with existing database data.
    /// Default: MergeOrInsert (update existing rows by key, insert new ones)
    /// </summary>
    public MergeStrategy MergeStrategy { get; set; }

    /// <summary>
    /// Gets or sets whether to load this reference table on application startup
    /// Default: true
    /// </summary>
    public bool LoadOnStartup { get; set; } = true;

    public ReferenceTableAttribute()
    {
        CsvFilePath = string.Empty;
        MergeStrategy = MergeStrategy.MergeOrInsert;
    }

    public ReferenceTableAttribute(string csvFilePath, MergeStrategy mergeStrategy = MergeStrategy.MergeOrInsert)
    {
        CsvFilePath = csvFilePath ?? string.Empty;
        MergeStrategy = mergeStrategy;
    }
}

/// <summary>
/// Defines how CSV data should be merged with existing database data
/// </summary>
public enum MergeStrategy
{
    /// <summary>
    /// Update existing rows (matched by primary key), insert new rows
    /// </summary>
    MergeOrInsert = 0,

    /// <summary>
    /// Delete all existing rows, then insert all CSV rows
    /// </summary>
    ReplaceAll = 1,

    /// <summary>
    /// Only insert rows that don't exist (skip existing)
    /// </summary>
    InsertOnly = 2
}
