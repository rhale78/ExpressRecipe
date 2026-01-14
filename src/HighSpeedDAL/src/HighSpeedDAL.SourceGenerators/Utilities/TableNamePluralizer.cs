using System;
using Humanizer;

namespace HighSpeedDAL.SourceGenerators.Utilities;

/// <summary>
/// Provides English pluralization for table names using Humanizer library.
/// Product ? Products, Category ? Categories, Person ? People, etc.
/// 
/// HighSpeedDAL Framework v0.1
/// </summary>
public static class TableNamePluralizer
{
    /// <summary>
    /// Pluralizes a table name based on the class name using Humanizer.
    /// Examples:
    /// - Product ? Products
    /// - Category ? Categories  
    /// - Person ? People
    /// - Address ? Addresses
    /// - Octopus ? Octopi
    /// - Cactus ? Cacti
    /// </summary>
    public static string Pluralize(string className)
    {
        if (string.IsNullOrWhiteSpace(className))
        {
            throw new ArgumentException("Class name cannot be null or empty", nameof(className));
        }

        // Use Humanizer's Pluralize extension method
        return className.Pluralize(inputIsKnownToBeSingular: false);
    }
}
