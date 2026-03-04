using ExpressRecipe.Shared.DTOs.Product;
using ExpressRecipe.ProductService.Data;
using ExpressRecipe.Shared.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;

namespace ExpressRecipe.ProductService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductRepository _productRepository;
    private readonly IIngredientRepository _ingredientRepository;
    private readonly IAllergenRepository _allergenRepository;
    private readonly HybridCacheService _cache;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(
        IProductRepository productRepository,
        IIngredientRepository ingredientRepository,
        IAllergenRepository allergenRepository,
        HybridCacheService cache,
        ILogger<ProductsController> logger)
    {
        _productRepository = productRepository;
        _ingredientRepository = ingredientRepository;
        _allergenRepository = allergenRepository;
        _cache = cache;
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
    /// Search for products
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<ProductSearchResult>> Search([FromQuery] ProductSearchRequest request)
    {
        try
        {
            // Get products and total count in parallel for better performance
            var productsTask = _productRepository.SearchAsync(request);
            var countTask = _productRepository.GetSearchCountAsync(request);

            await Task.WhenAll(productsTask, countTask);

            var result = new ProductSearchResult
            {
                Products = productsTask.Result,
                TotalCount = countTask.Result,
                Page = request.PageNumber,
                PageSize = request.PageSize
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching products");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Get product counts grouped by first letter (for alphabetical pagination)
    /// </summary>
    [HttpGet("letter-counts")]
    public async Task<ActionResult<Dictionary<string, int>>> GetLetterCounts([FromQuery] ProductSearchRequest request)
    {
        try
        {
            // Create cache key based on request parameters
            var cacheKey = CacheKeys.FormatKey(
                CacheKeys.ProductLetterCounts,
                $"{request.SearchTerm}_{request.Brand}_{request.Category}"
            );

            // Cache for 30 minutes (letter counts don't change frequently)
            var letterCounts = await _cache.GetOrSetAsync(
                cacheKey,
                ct => new ValueTask<Dictionary<string, int>>(_productRepository.GetLetterCountsAsync(request)),
                expiration: TimeSpan.FromMinutes(30)
            );

            return Ok(letterCounts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting letter counts");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Get product by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductDto>> GetById(Guid id)
    {
        try
        {
            var cacheKey = CacheKeys.FormatKey(CacheKeys.ProductById, id);

            // Cache product for 1 hour (products don't change often)
            var product = await _cache.GetOrSetAsync<ProductDto?>(
                cacheKey,
                ct => new ValueTask<ProductDto?>(_productRepository.GetByIdAsync(id)),
                expiration: TimeSpan.FromHours(1)
            );

            if (product == null)
            {
                return NotFound(new { message = "Product not found" });
            }

            // Load ingredients and allergens (cache these too)
            var ingredientsCacheKey = CacheKeys.FormatKey("product:ingredients:{0}", id);
            var allergensCacheKey = CacheKeys.FormatKey("product:allergens:{0}", id);

            product.Ingredients = await _cache.GetOrSetAsync(
                ingredientsCacheKey,
                ct => new ValueTask<List<ProductIngredientDto>>(_ingredientRepository.GetProductIngredientsAsync(id)),
                expiration: TimeSpan.FromHours(1)
            );

            product.Allergens = await _cache.GetOrSetAsync(
                allergensCacheKey,
                ct => new ValueTask<List<string>>(_allergenRepository.GetProductAllergensAsync(id)),
                expiration: TimeSpan.FromHours(1)
            );

            return Ok(product);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving product {ProductId}", id);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Get product by barcode (for scanner)
    /// </summary>
    [HttpGet("barcode/{barcode}")]
    [AllowAnonymous] // Allow service-to-service calls without JWT
    public async Task<ActionResult<ProductDto>> GetByBarcode(string barcode)
    {
        try
        {
            // Bypass cache - go directly to database to avoid Redis hanging issues
            // Cache is causing 10-second timeouts during price imports
            var product = await _productRepository.GetByBarcodeAsync(barcode);

            if (product == null)
            {
                return NotFound(new { message = "Product not found with this barcode" });
            }

            // Skip loading ingredients for service-to-service calls to improve performance
            // PriceService doesn't need ingredient details
            product.Ingredients = new List<ProductIngredientDto>();

            return Ok(product);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving product by barcode {Barcode}", barcode);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Submit a new product
    /// </summary>
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<ProductDto>> Create([FromBody] CreateProductRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var productId = await _productRepository.CreateAsync(request, userId.Value);

            // Add ingredients if provided
            if (request.IngredientIds.Any())
            {
                for (int i = 0; i < request.IngredientIds.Count; i++)
                {
                    await _ingredientRepository.AddProductIngredientAsync(
                        productId,
                        new AddProductIngredientRequest
                        {
                            IngredientId = request.IngredientIds[i],
                            OrderIndex = i
                        },
                        userId.Value);
                }
            }

            var product = await _productRepository.GetByIdAsync(productId);
            if (product != null)
            {
                product.Ingredients = await _ingredientRepository.GetProductIngredientsAsync(productId);
            }

            _logger.LogInformation("Product {ProductId} created by user {UserId}", productId, userId);

            return CreatedAtAction(nameof(GetById), new { id = productId }, product);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating product");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Update a product
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize]
    public async Task<ActionResult<ProductDto>> Update(Guid id, [FromBody] UpdateProductRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var success = await _productRepository.UpdateAsync(id, request, userId.Value);

            if (!success)
            {
                return NotFound(new { message = "Product not found" });
            }

            // Invalidate all caches for this product
            await _cache.RemoveAsync(CacheKeys.FormatKey(CacheKeys.ProductById, id));
            await _cache.RemoveAsync(CacheKeys.FormatKey("product:ingredients:{0}", id));
            await _cache.RemoveAsync(CacheKeys.FormatKey("product:allergens:{0}", id));

            // Also invalidate barcode cache if barcode exists
            var product = await _productRepository.GetByIdAsync(id);
            if (product?.Barcode != null)
            {
                await _cache.RemoveAsync(CacheKeys.FormatKey("product:barcode:{0}", product.Barcode));
            }

            if (product != null)
            {
                product.Ingredients = await _ingredientRepository.GetProductIngredientsAsync(id);
            }

            _logger.LogInformation("Product {ProductId} updated by user {UserId}", id, userId);

            return Ok(product);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating product {ProductId}", id);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Delete a product
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize]
    public async Task<ActionResult> Delete(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            // Get product before deletion to invalidate barcode cache
            var product = await _productRepository.GetByIdAsync(id);

            var success = await _productRepository.DeleteAsync(id, userId.Value);

            if (!success)
            {
                return NotFound(new { message = "Product not found" });
            }

            // Invalidate all caches for this product
            await _cache.RemoveAsync(CacheKeys.FormatKey(CacheKeys.ProductById, id));
            await _cache.RemoveAsync(CacheKeys.FormatKey("product:ingredients:{0}", id));
            await _cache.RemoveAsync(CacheKeys.FormatKey("product:allergens:{0}", id));

            // Invalidate barcode cache
            if (product?.Barcode != null)
            {
                await _cache.RemoveAsync(CacheKeys.FormatKey("product:barcode:{0}", product.Barcode));
            }

            _logger.LogInformation("Product {ProductId} deleted by user {UserId}", id, userId);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting product {ProductId}", id);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Approve or reject a product (admin only)
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    [Authorize] // TODO: Add admin role check
    public async Task<ActionResult> Approve(Guid id, [FromBody] ApproveProductRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var success = await _productRepository.ApproveAsync(
                id,
                request.Approve,
                userId.Value,
                request.RejectionReason);

            if (!success)
            {
                return NotFound(new { message = "Product not found" });
            }

            _logger.LogInformation(
                "Product {ProductId} {Action} by user {UserId}",
                id,
                request.Approve ? "approved" : "rejected",
                userId);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving product {ProductId}", id);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Add ingredient to product
    /// </summary>
    [HttpPost("{id:guid}/ingredients")]
    [Authorize]
    public async Task<ActionResult> AddIngredient(Guid id, [FromBody] AddProductIngredientRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            // Verify product exists
            if (!await _productRepository.ProductExistsAsync(id))
            {
                return NotFound(new { message = "Product not found" });
            }

            var ingredientId = await _ingredientRepository.AddProductIngredientAsync(id, request, userId.Value);

            _logger.LogInformation("Ingredient added to product {ProductId}", id);

            return CreatedAtAction(nameof(GetById), new { id }, new { ingredientId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding ingredient to product {ProductId}", id);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Remove ingredient from product
    /// </summary>
    [HttpDelete("{id:guid}/ingredients/{ingredientId:guid}")]
    [Authorize]
    public async Task<ActionResult> RemoveIngredient(Guid id, Guid ingredientId)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var success = await _ingredientRepository.RemoveProductIngredientAsync(ingredientId, userId.Value);

            if (!success)
            {
                return NotFound(new { message = "Product ingredient not found" });
            }

            _logger.LogInformation("Ingredient {IngredientId} removed from product {ProductId}", ingredientId, id);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing ingredient from product {ProductId}", id);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Bulk lookup products by barcodes (for batched operations from PriceService)
    /// </summary>
    [HttpPost("barcode/bulk")]
    [AllowAnonymous]
    public async Task<ActionResult<Dictionary<string, ProductDto>>> GetByBarcodesBulk([FromBody] BulkBarcodeRequest request)
    {
        try
        {
            if (request?.Barcodes == null || !request.Barcodes.Any())
            {
                return BadRequest(new { message = "Barcodes list cannot be empty" });
            }

            // Limit bulk requests to prevent abuse
            const int maxBulkSize = 500;
            if (request.Barcodes.Count > maxBulkSize)
            {
                return BadRequest(new { message = $"Bulk request exceeds maximum size of {maxBulkSize}" });
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();

            var result = await _productRepository.GetByBarcodesAsync(request.Barcodes);

            sw.Stop();
            _logger.LogInformation("[Products] Bulk barcode lookup: {Requested} barcodes -> {Found} products in {Ms}ms",
                request.Barcodes.Count, result.Count, sw.ElapsedMilliseconds);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Products] Bulk barcode lookup error: {Count} barcodes", request?.Barcodes?.Count ?? 0);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }
}

public class BulkBarcodeRequest
{
    public List<string> Barcodes { get; set; } = new();
}
