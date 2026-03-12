namespace ExpressRecipe.CookbookService.Models;

public class CookbookDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string? Description { get; set; }
    public string? CoverImageUrl { get; set; }
    public string? AuthorName { get; set; }
    public string Visibility { get; set; } = "Private";
    public bool IsFavorite { get; set; }
    public string? Tags { get; set; }
    public string? TitlePageContent { get; set; }
    public string? IntroductionContent { get; set; }
    public string? IndexContent { get; set; }
    public string? NotesContent { get; set; }
    public string? WebSlug { get; set; }
    public int ViewCount { get; set; }
    public Guid OwnerId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public decimal AverageRating { get; set; }
    public int RatingCount { get; set; }
    public int CommentCount { get; set; }
    public List<CookbookSectionDto> Sections { get; set; } = new();
    public List<CookbookRecipeDto> UnsectionedRecipes { get; set; } = new();
    public bool IsUserFavorite { get; set; }
}

public class CookbookSummaryDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string? CoverImageUrl { get; set; }
    public string? AuthorName { get; set; }
    public string Visibility { get; set; } = "Private";
    public bool IsFavorite { get; set; }
    public string? Tags { get; set; }
    public string? WebSlug { get; set; }
    public int ViewCount { get; set; }
    public Guid OwnerId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public decimal AverageRating { get; set; }
    public int RatingCount { get; set; }
    public int RecipeCount { get; set; }
    public int SectionCount { get; set; }
    public bool IsUserFavorite { get; set; }
}

public class CookbookSectionDto
{
    public Guid Id { get; set; }
    public Guid CookbookId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? TitlePageContent { get; set; }
    public string? CategoryOrMealType { get; set; }
    public int SortOrder { get; set; }
    public List<CookbookRecipeDto> Recipes { get; set; } = new();
}

public class CookbookRecipeDto
{
    public Guid Id { get; set; }
    public Guid CookbookId { get; set; }
    public Guid? SectionId { get; set; }
    public Guid RecipeId { get; set; }
    public string RecipeName { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public string? Notes { get; set; }
    public int? PageNumber { get; set; }
}

public class CookbookCommentDto
{
    public Guid Id { get; set; }
    public Guid CookbookId { get; set; }
    public Guid UserId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateCookbookRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string? Description { get; set; }
    public string? CoverImageUrl { get; set; }
    public string? AuthorName { get; set; }
    public string Visibility { get; set; } = "Private";
    public string? Tags { get; set; }
    public string? TitlePageContent { get; set; }
    public string? IntroductionContent { get; set; }
}

public class UpdateCookbookRequest
{
    public string? Title { get; set; }
    public string? Subtitle { get; set; }
    public string? Description { get; set; }
    public string? CoverImageUrl { get; set; }
    public string? AuthorName { get; set; }
    public string? Visibility { get; set; }
    public bool? IsFavorite { get; set; }
    public string? Tags { get; set; }
    public string? TitlePageContent { get; set; }
    public string? IntroductionContent { get; set; }
    public string? IndexContent { get; set; }
    public string? NotesContent { get; set; }
    public string? WebSlug { get; set; }
}

public class CreateCookbookSectionRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? TitlePageContent { get; set; }
    public string? CategoryOrMealType { get; set; }
    public int SortOrder { get; set; }
}

public class UpdateCookbookSectionRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? TitlePageContent { get; set; }
    public string? CategoryOrMealType { get; set; }
    public int? SortOrder { get; set; }
}

public class AddCookbookRecipeRequest
{
    public Guid RecipeId { get; set; }
    public string RecipeName { get; set; } = string.Empty;
    public Guid? SectionId { get; set; }
    public int SortOrder { get; set; }
    public string? Notes { get; set; }
}

public class MergeCookbooksRequest
{
    public List<Guid> SourceCookbookIds { get; set; } = new();
    public string NewTitle { get; set; } = string.Empty;
    public string? NewDescription { get; set; }
    public bool DeleteSources { get; set; } = false;
}

public class SplitCookbookRequest
{
    public List<Guid> SectionIds { get; set; } = new();
}

public class CookbookSearchRequest
{
    public string? SearchTerm { get; set; }
    public string? Visibility { get; set; }
    public string? Tag { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class ReorderRequest
{
    public List<Guid> Ids { get; set; } = new();
}

public class RateCookbookRequest
{
    public int Rating { get; set; }
}

public class AddCommentRequest
{
    public string Content { get; set; } = string.Empty;
}

public class ShareCookbookRequest
{
    public Guid TargetUserId { get; set; }
    public bool CanEdit { get; set; } = false;
}

public class MoveRecipeRequest
{
    public Guid? NewSectionId { get; set; }
}

public class ExportCookbookRequest
{
    public string Format { get; set; } = "pdf";
    public bool IncludeTitlePage { get; set; } = true;
    public bool IncludeIndex { get; set; } = true;
    public bool IncludeIntroduction { get; set; } = true;
}
