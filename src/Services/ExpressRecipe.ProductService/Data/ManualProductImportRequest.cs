using System.ComponentModel.DataAnnotations;

namespace ExpressRecipe.ProductService.Data;

/// <summary>
/// Admin manual product import — bypasses the approval queue.
/// Mirrors ProductSubmission fields but sets ApprovalStatus='Approved' immediately.
/// Used by AdminController.ForceImportProduct and ProductRepository.ForceImportAsync.
/// </summary>
public class ManualProductImportRequest
{
    [Required]
    public string ProductName { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? Barcode { get; set; }
    public string? Category { get; set; }
    public string? Description { get; set; }
    public string? IngredientsText { get; set; }
    public string? NutritionJson { get; set; }
}
