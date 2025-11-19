using ExpressRecipe.Shared.DTOs.Product;
using ExpressRecipe.ProductService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ExpressRecipe.ProductService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StoresController : ControllerBase
{
    private readonly IStoreRepository _storeRepository;
    private readonly ILogger<StoresController> _logger;

    public StoresController(
        IStoreRepository storeRepository,
        ILogger<StoresController> logger)
    {
        _storeRepository = storeRepository;
        _logger = logger;
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return null;
        }
        return userId;
    }

    /// <summary>
    /// Search for stores
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<List<StoreDto>>> Search([FromQuery] StoreSearchRequest request)
    {
        try
        {
            var stores = await _storeRepository.SearchAsync(request);
            return Ok(stores);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching stores");
            return StatusCode(500, new { message = "An error occurred while searching stores" });
        }
    }

    /// <summary>
    /// Get stores near a location
    /// </summary>
    [HttpGet("nearby")]
    public async Task<ActionResult<List<StoreDto>>> GetNearby(
        [FromQuery] decimal latitude,
        [FromQuery] decimal longitude,
        [FromQuery] double radiusMiles = 10.0)
    {
        try
        {
            if (radiusMiles <= 0 || radiusMiles > 100)
            {
                return BadRequest(new { message = "Radius must be between 0 and 100 miles" });
            }

            var stores = await _storeRepository.GetNearbyStoresAsync(latitude, longitude, radiusMiles);
            return Ok(stores);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding nearby stores for location ({Latitude}, {Longitude})", latitude, longitude);
            return StatusCode(500, new { message = "An error occurred while finding nearby stores" });
        }
    }

    /// <summary>
    /// Get store by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StoreDto>> GetById(Guid id)
    {
        try
        {
            var store = await _storeRepository.GetByIdAsync(id);

            if (store == null)
            {
                return NotFound(new { message = "Store not found" });
            }

            return Ok(store);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving store {StoreId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving the store" });
        }
    }

    /// <summary>
    /// Create a new store
    /// </summary>
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<Guid>> Create([FromBody] CreateStoreRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var storeId = await _storeRepository.CreateAsync(request, userId);

            return CreatedAtAction(nameof(GetById), new { id = storeId }, storeId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating store");
            return StatusCode(500, new { message = "An error occurred while creating the store" });
        }
    }

    /// <summary>
    /// Update an existing store
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize]
    public async Task<ActionResult> Update(Guid id, [FromBody] UpdateStoreRequest request)
    {
        try
        {
            var exists = await _storeRepository.StoreExistsAsync(id);
            if (!exists)
            {
                return NotFound(new { message = "Store not found" });
            }

            var userId = GetCurrentUserId();
            var success = await _storeRepository.UpdateAsync(id, request, userId);

            if (!success)
            {
                return NotFound(new { message = "Store not found or could not be updated" });
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating store {StoreId}", id);
            return StatusCode(500, new { message = "An error occurred while updating the store" });
        }
    }

    /// <summary>
    /// Delete a store (soft delete)
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize]
    public async Task<ActionResult> Delete(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var success = await _storeRepository.DeleteAsync(id, userId);

            if (!success)
            {
                return NotFound(new { message = "Store not found" });
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting store {StoreId}", id);
            return StatusCode(500, new { message = "An error occurred while deleting the store" });
        }
    }
}
