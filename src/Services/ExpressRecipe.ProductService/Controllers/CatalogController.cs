using ExpressRecipe.Data.Common;
using ExpressRecipe.ProductService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace ExpressRecipe.ProductService.Controllers;

/// <summary>
/// Catalog endpoints for cross-service queries such as ingredient density lookups.
/// </summary>
[ApiController]
[Route("api/catalog")]
public class CatalogController : ControllerBase
{
    private readonly IngredientDensityResolver _densityResolver;
    private readonly ILogger<CatalogController> _logger;

    public CatalogController(
        IngredientDensityResolver densityResolver,
        ILogger<CatalogController> logger)
    {
        _densityResolver = densityResolver;
        _logger = logger;
    }

    /// <summary>
    /// Get the density (g/ml) for an ingredient by its ID.
    /// Returns 404 if no density data is available.
    /// </summary>
    [HttpGet("density/{ingredientId:guid}")]
    public async Task<IActionResult> GetDensityById(Guid ingredientId, CancellationToken ct)
    {
        decimal? density = await _densityResolver.GetDensityAsync(ingredientId, null, ct);
        if (density is null)
        {
            return NotFound(new { message = $"No density data for ingredient {ingredientId}" });
        }
        return Ok(new { GramsPerMl = density });
    }

    /// <summary>
    /// Get the density (g/ml) for an ingredient by its name.
    /// Returns 404 if no density data is available.
    /// </summary>
    [HttpGet("density/by-name/{ingredientName}")]
    public async Task<IActionResult> GetDensityByName(string ingredientName, CancellationToken ct)
    {
        decimal? density = await _densityResolver.GetDensityAsync(null, ingredientName, ct);
        if (density is null)
        {
            return NotFound(new { message = $"No density data for ingredient '{ingredientName}'" });
        }
        return Ok(new { GramsPerMl = density });
    }
}
