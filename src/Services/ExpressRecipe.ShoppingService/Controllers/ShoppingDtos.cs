namespace ExpressRecipe.ShoppingService.Controllers;

// Store DTOs
public record CreateStoreRequestDto(
    string Name,
    string? Chain,
    string? Address,
    string? City,
    string? State,
    string? ZipCode,
    decimal? Latitude,
    decimal? Longitude
);

public record UpdateStoreRequestDto(
    string Name,
    string? Address,
    decimal? Latitude,
    decimal? Longitude
);

public record NearbyStoresRequest(
    decimal Latitude,
    decimal Longitude,
    double? MaxDistanceKm
);

public record CreateStoreLayoutRequest(
    string CategoryName,
    string? Aisle,
    int OrderIndex
);

public record UpdateStoreLayoutRequest(
    string? Aisle,
    int OrderIndex
);

public record RecordPriceRequest(
    Guid? ProductId,
    Guid StoreId,
    decimal Price,
    decimal? UnitPrice,
    decimal? Size,
    string? Unit,
    bool HasDeal,
    string? DealType,
    DateTime? DealEndDate
);
