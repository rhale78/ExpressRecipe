namespace ExpressRecipe.SafeForkService.Contracts.Requests;

public class AddCuratedAllergenRequest
{
    public Guid AllergenId { get; set; }
    public string ExposureThreshold { get; set; } = "IngestionOnly";
    public string Severity { get; set; } = "Moderate";
}
