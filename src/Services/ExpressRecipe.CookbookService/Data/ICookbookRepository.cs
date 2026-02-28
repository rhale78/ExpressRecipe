using ExpressRecipe.CookbookService.Models;

namespace ExpressRecipe.CookbookService.Data;

public interface ICookbookRepository
{
    // Cookbook CRUD
    Task<Guid> CreateCookbookAsync(CreateCookbookRequest request, Guid ownerId);
    Task<CookbookDto?> GetCookbookByIdAsync(Guid id, bool includeSections = true);
    Task<CookbookDto?> GetCookbookBySlugAsync(string slug);
    Task<List<CookbookSummaryDto>> GetUserCookbooksAsync(Guid userId, int page = 1, int pageSize = 20);
    Task<int> GetUserCookbookCountAsync(Guid userId);
    Task<List<CookbookSummaryDto>> SearchCookbooksAsync(string? searchTerm, string? visibility, int page = 1, int pageSize = 20);
    Task<int> SearchCookbooksCountAsync(string? searchTerm, string? visibility);
    Task<bool> UpdateCookbookAsync(Guid id, Guid userId, UpdateCookbookRequest request);
    Task<bool> DeleteCookbookAsync(Guid id, Guid userId);

    // Section management
    Task<Guid> CreateSectionAsync(Guid cookbookId, Guid userId, CreateCookbookSectionRequest request);
    Task<bool> UpdateSectionAsync(Guid sectionId, Guid userId, UpdateCookbookSectionRequest request);
    Task<bool> DeleteSectionAsync(Guid sectionId, Guid userId);
    Task<bool> ReorderSectionsAsync(Guid cookbookId, Guid userId, List<Guid> sectionIds);

    // Recipe management
    Task<Guid> AddRecipeToCookbookAsync(Guid cookbookId, Guid userId, AddCookbookRecipeRequest request);
    Task<bool> AddRecipesBatchAsync(Guid cookbookId, Guid userId, Guid? sectionId, List<AddCookbookRecipeRequest> recipes);
    Task<bool> RemoveRecipeFromCookbookAsync(Guid cookbookRecipeId, Guid userId);
    Task<bool> MoveRecipeToSectionAsync(Guid cookbookRecipeId, Guid userId, Guid? newSectionId);
    Task<bool> ReorderRecipesAsync(Guid cookbookId, Guid? sectionId, List<Guid> recipeIds);

    // Ratings and comments
    Task<bool> RateCookbookAsync(Guid cookbookId, Guid userId, int rating);
    Task<(decimal Average, int Count)> GetRatingsAsync(Guid cookbookId);
    Task<Guid> AddCommentAsync(Guid cookbookId, Guid userId, string content);
    Task<List<CookbookCommentDto>> GetCommentsAsync(Guid cookbookId, int page = 1, int pageSize = 20);
    Task<bool> DeleteCommentAsync(Guid commentId, Guid userId);

    // Favorites
    Task<bool> FavoriteCookbookAsync(Guid cookbookId, Guid userId);
    Task<bool> UnfavoriteCookbookAsync(Guid cookbookId, Guid userId);
    Task<bool> IsFavoritedAsync(Guid cookbookId, Guid userId);
    Task<List<CookbookSummaryDto>> GetFavoriteCookbooksAsync(Guid userId, int page = 1, int pageSize = 20);

    // Merge and split
    Task<Guid> MergeCookbooksAsync(Guid userId, MergeCookbooksRequest request);
    Task<List<Guid>> SplitCookbookAsync(Guid cookbookId, Guid userId, SplitCookbookRequest request);
    Task<List<Guid>> ExtractRecipesFromCookbookAsync(Guid cookbookId, Guid userId);

    // Sharing
    Task<bool> ShareCookbookAsync(Guid cookbookId, Guid ownerId, Guid targetUserId, bool canEdit);
    Task<bool> RevokeCookbookShareAsync(Guid cookbookId, Guid ownerId, Guid targetUserId);

    // View tracking
    Task IncrementViewCountAsync(Guid cookbookId);

    // Ownership check
    Task<bool> IsOwnerAsync(Guid cookbookId, Guid userId);
    Task<bool> CanViewAsync(Guid cookbookId, Guid userId);
    Task<bool> CanEditAsync(Guid cookbookId, Guid userId);
}
