using ExpressRecipe.Messaging.Core.Abstractions;

namespace ExpressRecipe.Shared.Messages;

/// <summary>
/// Request: look up a single product by its barcode.
/// ProductService handles this request and returns <see cref="ProductByBarcodeResponse"/>.
/// </summary>
public record RequestProductByBarcodeQuery(
    string CorrelationId,
    string Barcode) : IMessage;

/// <summary>
/// Response to <see cref="RequestProductByBarcodeQuery"/>.
/// <see cref="Found"/> is <c>false</c> when no matching product exists.
/// </summary>
public record ProductByBarcodeResponse(
    string CorrelationId,
    string Barcode,
    bool   Found,
    Guid?  ProductId,
    string? Name,
    string? Brand,
    string? Category) : IMessage;

/// <summary>
/// Request: look up multiple products by their barcodes in a single round-trip.
/// ProductService handles this request and returns <see cref="ProductsByBarcodesResponse"/>.
/// </summary>
public record RequestProductsByBarcodesQuery(
    string       CorrelationId,
    List<string> Barcodes) : IMessage;

/// <summary>
/// Response to <see cref="RequestProductsByBarcodesQuery"/>.
/// <see cref="Products"/> is a dictionary keyed by barcode (case-insensitive).
/// </summary>
public record ProductsByBarcodesResponse(
    string CorrelationId,
    Dictionary<string, ProductByBarcodeResponse> Products) : IMessage;
