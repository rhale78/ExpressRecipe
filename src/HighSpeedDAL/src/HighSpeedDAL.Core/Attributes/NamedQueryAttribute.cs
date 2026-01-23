using System;

namespace HighSpeedDAL.Core.Attributes;

/// <summary>
/// Generates a query method based on the specified property names.
/// The method will be named Get{Name}Async and will query by the specified properties.
///
/// Examples:
/// <code>
/// // Single property query
/// [NamedQuery("ByCategory", nameof(Category))]
/// // Generates: GetByCategoryAsync(string? category)
/// // SQL: WHERE [Category] = @Category AND [IsDeleted] = 0
///
/// // Multi-property query
/// [NamedQuery("ByProductIdAndStatus", nameof(ProductId), nameof(Status))]
/// // Generates: GetByProductIdAndStatusAsync(Guid productId, string? status)
///
/// // Single result query
/// [NamedQuery("ByBarcode", nameof(Barcode), IsSingle = true)]
/// // Generates: GetByBarcodeAsync(string? barcode) -> returns T?
/// </code>
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class NamedQueryAttribute : Attribute
{
    /// <summary>
    /// The name of the query (used in method name: Get{Name}Async)
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The property names to include in the WHERE clause
    /// </summary>
    public string[] Properties { get; }

    /// <summary>
    /// If true, the query returns a single result (FirstOrDefault).
    /// If false (default), returns a List.
    /// </summary>
    public bool IsSingle { get; set; }

    /// <summary>
    /// If true, automatically adds IsDeleted = 0 filter for [SoftDelete] entities.
    /// Default: true
    /// </summary>
    public bool AutoFilterDeleted { get; set; } = true;

    /// <summary>
    /// If true, results are cached using the entity's cache strategy.
    /// Default: true (if entity has [Cache])
    /// </summary>
    public bool EnableCache { get; set; } = true;

    /// <summary>
    /// Creates a named query attribute.
    /// </summary>
    /// <param name="name">The name of the query (used in method name: Get{Name}Async)</param>
    /// <param name="properties">The property names to include in the WHERE clause</param>
    public NamedQueryAttribute(string name, params string[] properties)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Properties = properties ?? throw new ArgumentNullException(nameof(properties));

        if (properties.Length == 0)
        {
            throw new ArgumentException("At least one property must be specified", nameof(properties));
        }
    }
}
