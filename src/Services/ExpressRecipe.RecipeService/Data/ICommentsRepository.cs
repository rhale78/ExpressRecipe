using ExpressRecipe.Shared.DTOs.User;

namespace ExpressRecipe.RecipeService.Data;

public interface ICommentsRepository
{
    Task<List<RecipeCommentDto>> GetRecipeCommentsAsync(Guid recipeId, string sortBy = "CreatedAt", int pageNumber = 1, int pageSize = 50);
    Task<RecipeCommentDto?> GetCommentByIdAsync(Guid id);
    Task<List<RecipeCommentDto>> GetCommentRepliesAsync(Guid parentCommentId);
    Task<Guid> CreateCommentAsync(Guid userId, CreateRecipeCommentRequest request);
    Task<bool> UpdateCommentAsync(Guid id, Guid userId, UpdateRecipeCommentRequest request);
    Task<bool> DeleteCommentAsync(Guid id, Guid userId);
    Task<bool> LikeCommentAsync(Guid commentId, Guid userId);
    Task<bool> UnlikeCommentAsync(Guid commentId, Guid userId);
    Task<bool> DislikeCommentAsync(Guid commentId, Guid userId);
    Task<bool> UndislikeCommentAsync(Guid commentId, Guid userId);
    Task<bool> FlagCommentAsync(Guid commentId, Guid userId, string reason);
    Task<bool> UnflagCommentAsync(Guid commentId, Guid userId);
    Task<List<RecipeCommentDto>> GetUserCommentsAsync(Guid userId);
    Task<List<RecipeCommentDto>> GetFlaggedCommentsAsync();
}
