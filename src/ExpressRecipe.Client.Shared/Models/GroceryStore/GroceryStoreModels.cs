namespace ExpressRecipe.Client.Shared.Models.GroceryStore;

public class GroceryStoreDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Chain { get; set; }
    public string? StoreType { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public string? County { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Website { get; set; }
    public bool AcceptsSnap { get; set; }
    public bool IsActive { get; set; }
    public string? DataSource { get; set; }
    public double? DistanceMiles { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class GroceryStoreSearchRequest
{
    public string? Name { get; set; }
    public string? Chain { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public string? StoreType { get; set; }
    public bool? AcceptsSnap { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class GroceryStoreSearchResponse
{
    public List<GroceryStoreDto> Stores { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

public class NearbyStoresRequest
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double RadiusMiles { get; set; } = 10;
    public int Limit { get; set; } = 50;
}
