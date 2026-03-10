using ExpressRecipe.Shared.DTOs.Product;
using ExpressRecipe.ProductService.Data;
using ExpressRecipe.ProductService.Logging;
using ExpressRecipe.ProductService.Services;
using ExpressRecipe.Shared.Messages;
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
    private readonly IProductEventPublisher _events;
    private readonly IProductBatchChannel _batchChannel;

    public ProductsController(
        IProductRepository productRepository,
        IIngredientRepository ingredientRepository,
        IAllergenRepository allergenRepository,
        HybridCacheService cache,
        ILogger<ProductsController> logger,
        IProductEventPublisher events,
        IProductBatchChannel batchChannel)
    {
        _productRepository = productRepository;
        _ingredientRepository = ingredientRepository;
        _allergenRepository = allergenRepository;
        _cache = cache;
        _logger = logger;
        _events = events;
        _batchChannel = batchChannel;
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
                Products = await productsTask,
                TotalCount = await countTask,
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

            // Load ingredients and allergens in parallel (both are independently cached)
            var ingredientsCacheKey = CacheKeys.FormatKey("product:ingredients:{0}", id);
            var allergensCacheKey = CacheKeys.FormatKey("product:allergens:{0}", id);

            var ingredientsTask = _cache.GetOrSetAsync(
                ingredientsCacheKey,
                ct => new ValueTask<List<ProductIngredientDto>>(_ingredientRepository.GetProductIngredientsAsync(id)),
                expiration: TimeSpan.FromHours(1)
            );

            var allergensTask = _cache.GetOrSetAsync(
                allergensCacheKey,
                ct => new ValueTask<List<string>>(_allergenRepository.GetProductAllergensAsync(id)),
                expiration: TimeSpan.FromHours(1)
            );

            await Task.WhenAll(ingredientsTask, allergensTask);

            product.Ingredients = await ingredientsTask;
            product.Allergens   = await allergensTask;

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
                // Load ingredients and allergens in parallel for the response
                var ingredientsTask = _ingredientRepository.GetProductIngredientsAsync(productId);
                var allergensTask   = _allergenRepository.GetProductAllergensAsync(productId);
                await Task.WhenAll(ingredientsTask, allergensTask);
                product.Ingredients = await ingredientsTask;
                product.Allergens   = await allergensTask;
            }

            _logger.LogInformation("Product {ProductId} created by user {UserId}", productId, userId);

            // Publish lifecycle event (non-blocking, best-effort)
            await _events.PublishCreatedAsync(
                productId,
                product?.Name ?? request.Name ?? string.Empty,
                product?.Brand ?? request.Brand,
                product?.Barcode ?? request.Barcode,
                product?.Category ?? request.Category,
                product?.ApprovalStatus ?? "Pending",
                userId,
                HttpContext.RequestAborted);

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

            // Snapshot the product BEFORE updating so we can detect renames / barcode changes
            var before = await _productRepository.GetByIdAsync(id);
            if (before == null)
            {
                return NotFound(new { message = "Product not found" });
            }

            var success = await _productRepository.UpdateAsync(id, request, userId.Value);

            if (!success)
            {
                return NotFound(new { message = "Product not found" });
            }

            // Invalidate all caches for this product in parallel
            await Task.WhenAll(
                _cache.RemoveAsync(CacheKeys.FormatKey(CacheKeys.ProductById, id)),
                _cache.RemoveAsync(CacheKeys.FormatKey("product:ingredients:{0}", id)),
                _cache.RemoveAsync(CacheKeys.FormatKey("product:allergens:{0}", id))
            );

            // Also invalidate barcode cache if barcode exists
            var product = await _productRepository.GetByIdAsync(id);
            if (product?.Barcode != null)
            {
                await _cache.RemoveAsync(CacheKeys.FormatKey("product:barcode:{0}", product.Barcode));
            }

            if (product != null)
            {
                // Load ingredients and allergens in parallel for the response
                var ingredientsTask = _ingredientRepository.GetProductIngredientsAsync(id);
                var allergensTask   = _allergenRepository.GetProductAllergensAsync(id);
                await Task.WhenAll(ingredientsTask, allergensTask);
                product.Ingredients = await ingredientsTask;
                product.Allergens   = await allergensTask;
            }

            _logger.LogInformation("Product {ProductId} updated by user {UserId}", id, userId);

            // Detect and publish fine-grained lifecycle events
            var ct = HttpContext.RequestAborted;
            var changedFields = new List<string>();
            if (product != null)
            {
                // Name change: request.Name must be non-empty and different from the stored name
                if (!string.IsNullOrEmpty(request.Name) && request.Name != before.Name)
                {
                    changedFields.Add(nameof(product.Name));
                    _logger.LogProductRenamed(id, before.Name, request.Name);
                    await _events.PublishRenamedAsync(id, before.Name, request.Name, userId, ct);
                }
                // Barcode change: compare with null-safe equality so all three cases fire the event:
                //   null → value (setting a barcode), value → null (clearing), value → different value.
                if (!string.Equals(before.Barcode ?? string.Empty, request.Barcode ?? string.Empty, StringComparison.Ordinal))
                {
                    changedFields.Add(nameof(product.Barcode));
                    _logger.LogProductBarcodeChanged(id, before.Barcode, request.Barcode);
                    await _events.PublishBarcodeChangedAsync(id, before.Barcode, request.Barcode, userId, ct);
                }
                if (request.Brand    != null && request.Brand    != before.Brand)    changedFields.Add(nameof(product.Brand));
                if (request.Category != null && request.Category != before.Category) changedFields.Add(nameof(product.Category));

                await _events.PublishUpdatedAsync(
                    id,
                    product.Name, product.Brand, product.Barcode,
                    product.Category, product.ApprovalStatus, userId,
                    changedFields, ct);
            }

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

            // Invalidate all caches for this product in parallel
            var cacheInvalidations = new List<Task>
            {
                _cache.RemoveAsync(CacheKeys.FormatKey(CacheKeys.ProductById, id)),
                _cache.RemoveAsync(CacheKeys.FormatKey("product:ingredients:{0}", id)),
                _cache.RemoveAsync(CacheKeys.FormatKey("product:allergens:{0}", id))
            };
            if (product?.Barcode != null)
                cacheInvalidations.Add(_cache.RemoveAsync(CacheKeys.FormatKey("product:barcode:{0}", product.Barcode)));

            await Task.WhenAll(cacheInvalidations);

            _logger.LogInformation("Product {ProductId} deleted by user {UserId}", id, userId);

            // Notify other services that this product is gone
            await _events.PublishDeletedAsync(id, product?.Barcode, userId, HttpContext.RequestAborted);

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
    [Authorize(Roles = "Admin")]
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

            // Fetch name for the event payload (best-effort)
            var approvedProduct = await _productRepository.GetByIdAsync(id);
            var ct = HttpContext.RequestAborted;
            _logger.LogProductApprovalChanged(id, request.Approve ? "Approved" : "Rejected");
            if (request.Approve)
                await _events.PublishApprovedAsync(id, approvedProduct?.Name ?? string.Empty, approvedProduct?.Barcode, userId.Value, ct);
            else
                await _events.PublishRejectedAsync(id, approvedProduct?.Name ?? string.Empty, userId.Value, request.RejectionReason, ct);

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
            _logger.LogProductIngredientsChanged(id, addedCount: 1, removedCount: 0);
            await _events.PublishIngredientsChangedAsync(
                id, productName: null,
                added: new[] { request.IngredientId },
                removed: Array.Empty<Guid>(),
                changedBy: userId,
                ct: HttpContext.RequestAborted);

            return CreatedAtAction(nameof(GetById), new { id }, new { ingredientId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding ingredient to product {ProductId}", id);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Returns the normalized ingredient list for a product.
    /// Used internally by AllergyAnalysis to check known-safe ingredient lists.
    /// Returns allergen-relevant name and ID for each ingredient.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("{id:guid}/ingredients/normalized")]
    public async Task<ActionResult> GetNormalizedIngredients(Guid id)
    {
        try
        {
            if (!await _productRepository.ProductExistsAsync(id))
                return NotFound(new { message = "Product not found" });

            var ingredients = await _ingredientRepository.GetProductIngredientsAsync(id);

            var normalized = ingredients.Select(i => new
            {
                i.Id,
                i.IngredientId,
                NormalizedName = (i.IngredientName ?? i.IngredientListString ?? string.Empty).Trim().ToLowerInvariant(),
                i.IngredientName,
                i.IngredientListString,
                i.OrderIndex
            }).ToList();

            return Ok(new { ProductId = id, Ingredients = normalized });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving normalized ingredients for product {ProductId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving normalized ingredients" });
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
            _logger.LogProductIngredientsChanged(id, addedCount: 0, removedCount: 1);
            await _events.PublishIngredientsChangedAsync(
                id, productName: null,
                added: Array.Empty<Guid>(),
                removed: new[] { ingredientId },
                changedBy: userId,
                ct: HttpContext.RequestAborted);

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

    /// <summary>
    /// Submit multiple products in one call – asynchronous channel path.
    /// Each item is written to the <see cref="IProductBatchChannel"/> and processed by
    /// <see cref="ProductBatchChannelWorker"/> in the background, which also fires
    /// <see cref="IProductEventPublisher.PublishCreatedAsync"/> for each created product.
    /// For creating a single product use POST /api/products (sync REST path).
    /// </summary>
    [HttpPost("batch")]
    [Authorize]
    public async Task<IActionResult> BatchSubmit([FromBody] BatchSubmitProductsRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            if (request.Products == null || request.Products.Count == 0)
                return BadRequest(new { message = "products list cannot be empty" });

            const int maxBatch = 500;
            if (request.Products.Count > maxBatch)
                return BadRequest(new { message = $"Batch exceeds maximum of {maxBatch} items" });

            var sessionId = Guid.NewGuid().ToString("N");
            var accepted  = 0;

            foreach (var productRequest in request.Products)
            {
                var item = new ProductBatchItem
                {
                    Request     = productRequest,
                    SubmittedBy = userId.Value,
                    SessionId   = sessionId
                };

                if (_batchChannel.TryWrite(item))
                    accepted++;
                else
                {
                    // Channel full – wait for space
                    await _batchChannel.WriteAsync(item, HttpContext.RequestAborted);
                    accepted++;
                }
            }

            _logger.LogInformation(
                "[ProductsController] Batch submitted: session={SessionId} count={Count} by user {UserId}",
                sessionId, accepted, userId.Value);

            return Accepted(new
            {
                sessionId,
                accepted,
                message = "Product batch queued for async processing"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting product batch");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }
}

public class BulkBarcodeRequest
{
    public List<string> Barcodes { get; set; } = new();
}

/// <summary>
/// Request body for <c>POST /api/products/batch</c> (async channel path).
/// </summary>
public class BatchSubmitProductsRequest
{
    public List<CreateProductRequest> Products { get; set; } = new();
}
