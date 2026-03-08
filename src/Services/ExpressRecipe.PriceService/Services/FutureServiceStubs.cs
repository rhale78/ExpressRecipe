using ExpressRecipe.PriceService.Data;

namespace ExpressRecipe.PriceService.Services;

/// <summary>
/// Future: extract price rows from receipt image via Tesseract/Tabscanner.
/// Scaffold only — no implementation.
/// </summary>
public interface IReceiptOcrService
{
    Task<List<PriceHistoryRecord>> ParseReceiptAsync(Stream imageStream, CancellationToken ct);
}

/// <summary>
/// Future: pull weekly flyer deals from Flipp API.
/// Scaffold only — no implementation.
/// </summary>
public interface IFlyerImportService
{
    Task<List<CreateEnhancedDealRequest>> GetFlyerDealsAsync(string storeChain, string zipCode, CancellationToken ct);
}

/// <summary>
/// Future: extract price from a photo taken in store (shelf price tag).
/// Scaffold only — no implementation.
/// </summary>
public interface IShelfPriceOcrService
{
    Task<PriceHistoryRecord?> ParseShelfPhotoAsync(Stream imageStream, CancellationToken ct);
}
