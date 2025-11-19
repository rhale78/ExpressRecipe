using System.ComponentModel.DataAnnotations;

namespace ExpressRecipe.Shared.DTOs.Recipe;

// Recipe Versioning

public class RecipeVersionDto
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public int VersionNumber { get; set; }
    public string? ChangeDescription { get; set; }
    public Guid CreatedBy { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }
    public string SnapshotData { get; set; } = string.Empty; // JSON snapshot
}

public class RecipeForkDto
{
    public Guid Id { get; set; }
    public Guid OriginalRecipeId { get; set; }
    public string? OriginalRecipeName { get; set; }
    public Guid ForkedRecipeId { get; set; }
    public string? ForkedRecipeName { get; set; }
    public Guid ForkedBy { get; set; }
    public string? ForkedByName { get; set; }
    public string? ForkReason { get; set; }
    public DateTime ForkedAt { get; set; }
}

public class ForkRecipeRequest
{
    [Required]
    public Guid OriginalRecipeId { get; set; }

    [Required]
    [StringLength(300)]
    public string NewRecipeName { get; set; } = string.Empty;

    [StringLength(500)]
    public string? ForkReason { get; set; }

    public bool MakePrivate { get; set; }
}

// Recipe Import

public class RecipeImportSourceDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty; // MealMaster, JSON, XML, WebScraper, API
    public string? Description { get; set; }
    public string? ParserClassName { get; set; }
    public bool IsActive { get; set; }
    public string? SupportedFileExtensions { get; set; }
    public bool RequiresApiKey { get; set; }
    public string? Website { get; set; }
}

public class RecipeImportJobDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ImportSourceId { get; set; }
    public string? ImportSourceName { get; set; }
    public string? FileName { get; set; }
    public string? FileUrl { get; set; }
    public string Status { get; set; } = string.Empty; // Pending, Processing, Completed, Failed, PartialSuccess
    public int TotalRecipes { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public string? ErrorLog { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public List<RecipeImportResultDto>? Results { get; set; }
}

public class RecipeImportResultDto
{
    public Guid Id { get; set; }
    public Guid ImportJobId { get; set; }
    public string? SourceRecipeId { get; set; }
    public string? SourceRecipeName { get; set; }
    public Guid? ImportedRecipeId { get; set; }
    public string Status { get; set; } = string.Empty; // Success, Failed, Skipped, Duplicate
    public string? ErrorMessage { get; set; }
    public string? RawData { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class StartImportJobRequest
{
    [Required]
    public Guid ImportSourceId { get; set; }

    [StringLength(500)]
    public string? FileName { get; set; }

    [Url]
    [StringLength(1000)]
    public string? FileUrl { get; set; }

    public string? FileContent { get; set; } // For direct paste/upload
}

public class ImportFromUrlRequest
{
    [Required]
    [Url]
    [StringLength(1000)]
    public string Url { get; set; } = string.Empty;

    public Guid? ImportSourceId { get; set; } // Optional: auto-detect if not specified
}

public class ImportFromFileRequest
{
    [Required]
    public Guid ImportSourceId { get; set; }

    [Required]
    [StringLength(500)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    public string FileContent { get; set; } = string.Empty;
}

// Recipe Export

public class RecipeExportHistoryDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string ExportFormat { get; set; } = string.Empty; // MealMaster, JSON, PDF, XML, etc.
    public int RecipeCount { get; set; }
    public string? FileName { get; set; }
    public long? FileSize { get; set; }
    public string? FileUrl { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ExportRecipesRequest
{
    [Required]
    [StringLength(50)]
    public string ExportFormat { get; set; } = string.Empty;

    [Required]
    public List<Guid> RecipeIds { get; set; } = new();

    [StringLength(500)]
    public string? FileName { get; set; }
}

// Recipe Collections

public class RecipeCollectionDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsPublic { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    // Navigation properties
    public int RecipeCount { get; set; }
    public List<RecipeCollectionItemDto>? Items { get; set; }
}

public class RecipeCollectionItemDto
{
    public Guid Id { get; set; }
    public Guid CollectionId { get; set; }
    public Guid RecipeId { get; set; }
    public string? RecipeName { get; set; }
    public string? RecipeImageUrl { get; set; }
    public int OrderIndex { get; set; }
    public string? Notes { get; set; }
    public DateTime AddedAt { get; set; }
}

public class CreateRecipeCollectionRequest
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Url]
    [StringLength(500)]
    public string? ImageUrl { get; set; }

    public bool IsPublic { get; set; }

    public int SortOrder { get; set; }
}

public class UpdateRecipeCollectionRequest
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Url]
    [StringLength(500)]
    public string? ImageUrl { get; set; }

    public bool IsPublic { get; set; }

    public int SortOrder { get; set; }
}

public class AddRecipeToCollectionRequest
{
    [Required]
    public Guid RecipeId { get; set; }

    public int OrderIndex { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}

public class UpdateCollectionItemRequest
{
    public int OrderIndex { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}

public class ImportSummaryDto
{
    public int TotalJobs { get; set; }
    public int PendingJobs { get; set; }
    public int CompletedJobs { get; set; }
    public int FailedJobs { get; set; }
    public int TotalRecipesImported { get; set; }
    public DateTime? LastImportDate { get; set; }
    public List<RecipeImportJobDto>? RecentJobs { get; set; }
}

public class CollectionSummaryDto
{
    public int TotalCollections { get; set; }
    public int PublicCollections { get; set; }
    public int PrivateCollections { get; set; }
    public int TotalRecipes { get; set; }
    public List<RecipeCollectionDto>? RecentCollections { get; set; }
}
