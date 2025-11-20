using ExpressRecipe.Shared.DTOs.User;
using ExpressRecipe.UserService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ExpressRecipe.UserService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PointsController : ControllerBase
{
    private readonly IPointsRepository _pointsRepository;
    private readonly ILogger<PointsController> _logger;

    public PointsController(
        IPointsRepository pointsRepository,
        ILogger<PointsController> logger)
    {
        _pointsRepository = pointsRepository;
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
    /// Get user's points summary
    /// </summary>
    [HttpGet("summary")]
    public async Task<ActionResult<UserPointsSummaryDto>> GetSummary()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var summary = await _pointsRepository.GetUserPointsSummaryAsync(userId.Value);
            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving points summary");
            return StatusCode(500, new { message = "An error occurred while retrieving your points summary" });
        }
    }

    /// <summary>
    /// Get user's current point balance
    /// </summary>
    [HttpGet("balance")]
    public async Task<ActionResult<int>> GetBalance()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var balance = await _pointsRepository.GetUserPointBalanceAsync(userId.Value);
            return Ok(new { balance });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving point balance");
            return StatusCode(500, new { message = "An error occurred while retrieving your balance" });
        }
    }

    /// <summary>
    /// Get user's point transaction history
    /// </summary>
    [HttpGet("transactions")]
    public async Task<ActionResult<List<PointTransactionDto>>> GetTransactions([FromQuery] int limit = 50)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            if (limit <= 0 || limit > 200)
            {
                return BadRequest(new { message = "Limit must be between 1 and 200" });
            }

            var transactions = await _pointsRepository.GetUserTransactionsAsync(userId.Value, limit);
            return Ok(transactions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving transactions");
            return StatusCode(500, new { message = "An error occurred while retrieving your transactions" });
        }
    }

    /// <summary>
    /// Get user's contributions
    /// </summary>
    [HttpGet("contributions")]
    public async Task<ActionResult<List<UserContributionDto>>> GetContributions(
        [FromQuery] bool? approvedOnly = null,
        [FromQuery] int limit = 50)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            if (limit <= 0 || limit > 200)
            {
                return BadRequest(new { message = "Limit must be between 1 and 200" });
            }

            var contributions = await _pointsRepository.GetUserContributionsAsync(userId.Value, approvedOnly, limit);
            return Ok(contributions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving contributions");
            return StatusCode(500, new { message = "An error occurred while retrieving your contributions" });
        }
    }

    /// <summary>
    /// Get all contribution types
    /// </summary>
    [HttpGet("contribution-types")]
    [AllowAnonymous]
    public async Task<ActionResult<List<ContributionTypeDto>>> GetContributionTypes([FromQuery] bool activeOnly = true)
    {
        try
        {
            var types = await _pointsRepository.GetContributionTypesAsync(activeOnly);
            return Ok(types);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving contribution types");
            return StatusCode(500, new { message = "An error occurred while retrieving contribution types" });
        }
    }

    /// <summary>
    /// Get active rewards
    /// </summary>
    [HttpGet("rewards")]
    [AllowAnonymous]
    public async Task<ActionResult<List<RewardItemDto>>> GetRewards()
    {
        try
        {
            var rewards = await _pointsRepository.GetActiveRewardsAsync();
            return Ok(rewards);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving rewards");
            return StatusCode(500, new { message = "An error occurred while retrieving rewards" });
        }
    }

    /// <summary>
    /// Get reward by ID
    /// </summary>
    [HttpGet("rewards/{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<RewardItemDto>> GetReward(Guid id)
    {
        try
        {
            var reward = await _pointsRepository.GetRewardByIdAsync(id);

            if (reward == null)
            {
                return NotFound(new { message = "Reward not found" });
            }

            return Ok(reward);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving reward {RewardId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving the reward" });
        }
    }

    /// <summary>
    /// Get user's redeemed rewards
    /// </summary>
    [HttpGet("redeemed-rewards")]
    public async Task<ActionResult<List<RewardItemDto>>> GetRedeemedRewards([FromQuery] bool activeOnly = true)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var rewards = await _pointsRepository.GetUserRedeemedRewardsAsync(userId.Value, activeOnly);
            return Ok(rewards);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving redeemed rewards");
            return StatusCode(500, new { message = "An error occurred while retrieving your redeemed rewards" });
        }
    }

    /// <summary>
    /// Redeem a reward
    /// </summary>
    [HttpPost("redeem")]
    public async Task<ActionResult> RedeemReward([FromBody] RedeemRewardRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var success = await _pointsRepository.RedeemRewardAsync(userId.Value, request.RewardItemId);

            if (!success)
            {
                return BadRequest(new { message = "Reward could not be redeemed. Check your points balance and reward availability." });
            }

            return Ok(new { message = "Reward redeemed successfully" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to redeem reward");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error redeeming reward");
            return StatusCode(500, new { message = "An error occurred while redeeming the reward" });
        }
    }

    /// <summary>
    /// Approve or reject a contribution (admin only)
    /// </summary>
    [HttpPost("contributions/{id:guid}/approve")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> ApproveContribution(
        Guid id,
        [FromBody] ApproveContributionRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var success = await _pointsRepository.ApproveContributionAsync(
                id,
                userId.Value,
                request.Approve,
                request.RejectionReason);

            if (!success)
            {
                return NotFound(new { message = "Contribution not found or already approved" });
            }

            return Ok(new { message = request.Approve ? "Contribution approved" : "Contribution rejected" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving/rejecting contribution {ContributionId}", id);
            return StatusCode(500, new { message = "An error occurred while processing the contribution" });
        }
    }
}

public class ApproveContributionRequest
{
    public bool Approve { get; set; }
    public string? RejectionReason { get; set; }
}
