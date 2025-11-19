using ExpressRecipe.Shared.DTOs.Product;
using ExpressRecipe.ProductService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ExpressRecipe.ProductService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CouponsController : ControllerBase
{
    private readonly ICouponRepository _couponRepository;
    private readonly ILogger<CouponsController> _logger;

    public CouponsController(
        ICouponRepository couponRepository,
        ILogger<CouponsController> logger)
    {
        _couponRepository = couponRepository;
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
    /// Search for coupons
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<List<CouponDto>>> Search([FromQuery] CouponSearchRequest request)
    {
        try
        {
            var coupons = await _couponRepository.SearchAsync(request);
            return Ok(coupons);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching coupons");
            return StatusCode(500, new { message = "An error occurred while searching coupons" });
        }
    }

    /// <summary>
    /// Get available coupons for a product
    /// </summary>
    [HttpGet("product/{productId:guid}")]
    public async Task<ActionResult<List<CouponDto>>> GetForProduct(Guid productId, [FromQuery] Guid? storeId = null)
    {
        try
        {
            var coupons = await _couponRepository.GetAvailableCouponsForProductAsync(productId, storeId);
            return Ok(coupons);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving coupons for product {ProductId}", productId);
            return StatusCode(500, new { message = "An error occurred while retrieving coupons" });
        }
    }

    /// <summary>
    /// Get coupon by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CouponDto>> GetById(Guid id)
    {
        try
        {
            var coupon = await _couponRepository.GetByIdAsync(id);

            if (coupon == null)
            {
                return NotFound(new { message = "Coupon not found" });
            }

            return Ok(coupon);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving coupon {CouponId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving the coupon" });
        }
    }

    /// <summary>
    /// Get user's clipped coupons
    /// </summary>
    [HttpGet("my-coupons")]
    [Authorize]
    public async Task<ActionResult<List<UserCouponDto>>> GetMyCoupons([FromQuery] bool activeOnly = true)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var coupons = await _couponRepository.GetUserCouponsAsync(userId.Value, activeOnly);
            return Ok(coupons);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user coupons");
            return StatusCode(500, new { message = "An error occurred while retrieving your coupons" });
        }
    }

    /// <summary>
    /// Create a new coupon
    /// </summary>
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<Guid>> Create([FromBody] CreateCouponRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var couponId = await _couponRepository.CreateAsync(request, userId);

            return CreatedAtAction(nameof(GetById), new { id = couponId }, couponId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating coupon");
            return StatusCode(500, new { message = "An error occurred while creating the coupon" });
        }
    }

    /// <summary>
    /// Update an existing coupon
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize]
    public async Task<ActionResult> Update(Guid id, [FromBody] UpdateCouponRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var success = await _couponRepository.UpdateAsync(id, request, userId);

            if (!success)
            {
                return NotFound(new { message = "Coupon not found or could not be updated" });
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating coupon {CouponId}", id);
            return StatusCode(500, new { message = "An error occurred while updating the coupon" });
        }
    }

    /// <summary>
    /// Delete a coupon (soft delete)
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize]
    public async Task<ActionResult> Delete(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var success = await _couponRepository.DeleteAsync(id, userId);

            if (!success)
            {
                return NotFound(new { message = "Coupon not found" });
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting coupon {CouponId}", id);
            return StatusCode(500, new { message = "An error occurred while deleting the coupon" });
        }
    }

    /// <summary>
    /// Clip a coupon to user's account
    /// </summary>
    [HttpPost("clip")]
    [Authorize]
    public async Task<ActionResult<Guid>> ClipCoupon([FromBody] ClipCouponRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var userCouponId = await _couponRepository.ClipCouponAsync(userId.Value, request);

            return Ok(new { id = userCouponId, message = "Coupon clipped successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clipping coupon");
            return StatusCode(500, new { message = "An error occurred while clipping the coupon" });
        }
    }

    /// <summary>
    /// Use a clipped coupon
    /// </summary>
    [HttpPost("use")]
    [Authorize]
    public async Task<ActionResult> UseCoupon([FromBody] UseCouponRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var success = await _couponRepository.UseCouponAsync(userId.Value, request);

            if (!success)
            {
                return BadRequest(new { message = "Coupon could not be used. It may already be used or expired." });
            }

            return Ok(new { message = "Coupon used successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error using coupon");
            return StatusCode(500, new { message = "An error occurred while using the coupon" });
        }
    }
}
