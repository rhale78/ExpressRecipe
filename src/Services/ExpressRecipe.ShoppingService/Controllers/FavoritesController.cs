using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ExpressRecipe.ShoppingService.Data;

namespace ExpressRecipe.ShoppingService.Controllers;

[Authorize]
[ApiController]
[Route("api/shopping/[controller]")]
public class FavoritesController : ControllerBase
{
    private readonly ILogger<FavoritesController> _logger;
    private readonly IShoppingRepository _repository;

    public FavoritesController(ILogger<FavoritesController> logger, IShoppingRepository repository)
    {
        _logger = logger;
        _repository = repository;
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    /// <summary>
    /// Get user's favorite items
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetFavorites()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var favorites = await _repository.GetUserFavoritesAsync(userId.Value);
        return Ok(favorites);
    }

    /// <summary>
    /// Get household's favorite items
    /// </summary>
    [HttpGet("household/{householdId}")]
    public async Task<IActionResult> GetHouseholdFavorites(Guid householdId)
    {
        var favorites = await _repository.GetHouseholdFavoritesAsync(householdId);
        return Ok(favorites);
    }

    /// <summary>
    /// Add new favorite item
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> AddFavorite([FromBody] AddFavoriteRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var favoriteId = await _repository.AddFavoriteItemAsync(
            userId.Value,
            request.HouseholdId,
            request.ProductId,
            request.CustomName,
            request.PreferredBrand,
            request.TypicalQuantity,
            request.TypicalUnit,
            request.Category,
            request.IsGeneric
        );

        _logger.LogInformation("User {UserId} added favorite {FavoriteId}", userId, favoriteId);
        return CreatedAtAction(nameof(GetFavorites), new { id = favoriteId }, new { id = favoriteId });
    }

    /// <summary>
    /// Update favorite usage count
    /// </summary>
    [HttpPut("{favoriteId}/use")]
    public async Task<IActionResult> UpdateUsage(Guid favoriteId)
    {
        await _repository.UpdateFavoriteUsageAsync(favoriteId);
        _logger.LogInformation("Updated usage for favorite {FavoriteId}", favoriteId);
        return NoContent();
    }

    /// <summary>
    /// Remove favorite item
    /// </summary>
    [HttpDelete("{favoriteId}")]
    public async Task<IActionResult> RemoveFavorite(Guid favoriteId)
    {
        var userId = GetUserId();
        await _repository.RemoveFavoriteAsync(favoriteId);
        _logger.LogInformation("User {UserId} removed favorite {FavoriteId}", userId, favoriteId);
        return NoContent();
    }

    /// <summary>
    /// Add favorite item to shopping list
    /// </summary>
    [HttpPost("{favoriteId}/add-to-list/{listId}")]
    public async Task<IActionResult> AddFavoriteToList(Guid favoriteId, Guid listId)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        
        // Get the favorite item details
        var favorites = await _repository.GetUserFavoritesAsync(userId.Value);
        var favorite = favorites.FirstOrDefault(f => f.Id == favoriteId);
        
        if (favorite == null)
            return NotFound("Favorite not found");

        // Add to shopping list
        var itemId = await _repository.AddItemToListAsync(
            listId,
            userId.Value,
            favorite.ProductId,
            favorite.CustomName,
            favorite.TypicalQuantity,
            favorite.TypicalUnit,
            favorite.Category,
            isFavorite: true,
            isGeneric: favorite.IsGeneric,
            preferredBrand: favorite.PreferredBrand
        );

        // Update favorite usage
        await _repository.UpdateFavoriteUsageAsync(favoriteId);

        _logger.LogInformation("User {UserId} added favorite {FavoriteId} to list {ListId} as item {ItemId}", 
            userId, favoriteId, listId, itemId);
        
        return Ok(new { itemId });
    }
}

public record AddFavoriteRequest(
    Guid? HouseholdId,
    Guid? ProductId,
    string? CustomName,
    string? PreferredBrand,
    decimal TypicalQuantity,
    string? TypicalUnit,
    string? Category,
    bool IsGeneric
);
