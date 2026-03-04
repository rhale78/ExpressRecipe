namespace ExpressRecipe.PriceService.Services;

public class ImportResult
{
    public bool Success { get; set; }
    public string DataSource { get; set; } = string.Empty;
    public int Processed { get; set; }
    public int Imported { get; set; }
    public int Skipped { get; set; }
    public int Errors { get; set; }
}

public class OpenPriceRecord
{
    public string? ProductCode { get; set; }
    public string? ProductName { get; set; }
    public decimal Price { get; set; }
    public string? LocationName { get; set; }
    public string? LocationCity { get; set; }
    public string? LocationCountry { get; set; }
    public string? Currency { get; set; }
    public DateOnly? Date { get; set; }
}
