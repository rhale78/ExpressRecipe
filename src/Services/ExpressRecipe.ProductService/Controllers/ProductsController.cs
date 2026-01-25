using ExpressRecipe.Shared.DTOs.Product;
using ExpressRecipe.ProductService.Data;
using ExpressRecipe.Shared.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;

namespace ExpressRecipe.ProductService.Controllers
{
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
            return string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId) ? null : userId;
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
                Task<List<ProductDto>> productsTask = _productRepository.SearchAsync(request);
                Task<int> countTask = _productRepository.GetSearchCountAsync(request);

                await Task.WhenAll(productsTask, countTask);

                ProductSearchResult result = new ProductSearchResult
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
                Dictionary<string, int> letterCounts = await _cache.GetOrSetAsync(
                    cacheKey,
                    () => _productRepository.GetLetterCountsAsync(request),
                    memoryExpiry: TimeSpan.FromMinutes(10),
                    distributedExpiry: TimeSpan.FromMinutes(30)
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
                ProductDto? product = await _cache.GetOrSetAsync(
                    cacheKey,
                    () => _productRepository.GetByIdAsync(id)!,
                    memoryExpiry: TimeSpan.FromMinutes(15),
                    distributedExpiry: TimeSpan.FromHours(1)
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
                    () => _ingredientRepository.GetProductIngredientsAsync(id),
                    memoryExpiry: TimeSpan.FromMinutes(15),
                    distributedExpiry: TimeSpan.FromHours(1)
                );

                product.Allergens = await _cache.GetOrSetAsync(
                    allergensCacheKey,
                    () => _allergenRepository.GetProductAllergensAsync(id),
                    memoryExpiry: TimeSpan.FromMinutes(15),
                    distributedExpiry: TimeSpan.FromHours(1)
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
        public async Task<ActionResult<ProductDto>> GetByBarcode(string barcode)
        {
            try
            {
                var cacheKey = CacheKeys.FormatKey("product:barcode:{0}", barcode);

                ProductDto? product = await _cache.GetOrSetAsync(
                    cacheKey,
                    () => _productRepository.GetByBarcodeAsync(barcode)!,
                    memoryExpiry: TimeSpan.FromMinutes(15),
                    distributedExpiry: TimeSpan.FromHours(2)
                );

                if (product == null)
                {
                    return NotFound(new { message = "Product not found with this barcode" });
                }

                // Load ingredients (also cached)
                var ingredientsCacheKey = CacheKeys.FormatKey("product:ingredients:{0}", product.Id);
                product.Ingredients = await _cache.GetOrSetAsync(
                    ingredientsCacheKey,
                    () => _ingredientRepository.GetProductIngredientsAsync(product.Id),
                    memoryExpiry: TimeSpan.FromMinutes(15),
                    distributedExpiry: TimeSpan.FromHours(2)
                );

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
                Guid? userId = GetCurrentUserId();
                if (userId == null)
                {
                    return Unauthorized();
                }

                Guid productId = await _productRepository.CreateAsync(request, userId.Value);

                // Add ingredients if provided
                if (request.IngredientIds.Count != 0)
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

                ProductDto? product = await _productRepository.GetByIdAsync(productId);
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
                Guid? userId = GetCurrentUserId();
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
                ProductDto? product = await _productRepository.GetByIdAsync(id);
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
                Guid? userId = GetCurrentUserId();
                if (userId == null)
                {
                    return Unauthorized();
                }

                // Get product before deletion to invalidate barcode cache
                ProductDto? product = await _productRepository.GetByIdAsync(id);

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
                Guid? userId = GetCurrentUserId();
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
                Guid? userId = GetCurrentUserId();
                if (userId == null)
                {
                    return Unauthorized();
                }

                // Verify product exists
                if (!await _productRepository.ProductExistsAsync(id))
                {
                    return NotFound(new { message = "Product not found" });
                }

                Guid ingredientId = await _ingredientRepository.AddProductIngredientAsync(id, request, userId.Value);

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
                Guid? userId = GetCurrentUserId();
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
    }
}
