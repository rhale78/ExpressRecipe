using ExpressRecipe.Shared.DTOs.Product;
using ExpressRecipe.ProductService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ExpressRecipe.ProductService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductRepository _productRepository;
    private readonly IIngredientRepository _ingredientRepository;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(
        IProductRepository productRepository,
        IIngredientRepository ingredientRepository,
        ILogger<ProductsController> logger)
    {
        _productRepository = productRepository;
        _ingredientRepository = ingredientRepository;
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
    public async Task<ActionResult<List<ProductDto>>> Search([FromQuery] ProductSearchRequest request)
    {
        try
        {
            var products = await _productRepository.SearchAsync(request);
            return Ok(products);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching products");
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
            var product = await _productRepository.GetByIdAsync(id);

            if (product == null)
            {
                return NotFound(new { message = "Product not found" });
            }

            // Load ingredients
            product.Ingredients = await _ingredientRepository.GetProductIngredientsAsync(id);

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
            var product = await _productRepository.GetByBarcodeAsync(barcode);

            if (product == null)
            {
                return NotFound(new { message = "Product not found with this barcode" });
            }

            // Load ingredients
            product.Ingredients = await _ingredientRepository.GetProductIngredientsAsync(product.Id);

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

            var product = await _productRepository.GetByIdAsync(id);
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

            var success = await _productRepository.DeleteAsync(id, userId.Value);

            if (!success)
            {
                return NotFound(new { message = "Product not found" });
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
}
