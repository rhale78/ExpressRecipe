using ExpressRecipe.Shared.DTOs.User;
using ExpressRecipe.UserService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ExpressRecipe.UserService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FriendsController : ControllerBase
{
    private readonly IFriendsRepository _friendsRepository;
    private readonly ILogger<FriendsController> _logger;

    public FriendsController(
        IFriendsRepository friendsRepository,
        ILogger<FriendsController> logger)
    {
        _friendsRepository = friendsRepository;
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
    /// Get user's friends summary
    /// </summary>
    [HttpGet("summary")]
    public async Task<ActionResult<FriendsSummaryDto>> GetSummary()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var summary = await _friendsRepository.GetFriendsSummaryAsync(userId.Value);
            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving friends summary");
            return StatusCode(500, new { message = "An error occurred while retrieving your friends summary" });
        }
    }

    /// <summary>
    /// Get user's friends
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<UserFriendDto>>> GetFriends([FromQuery] string? status = null)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var friends = await _friendsRepository.GetUserFriendsAsync(userId.Value, status);
            return Ok(friends);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving friends");
            return StatusCode(500, new { message = "An error occurred while retrieving your friends" });
        }
    }

    /// <summary>
    /// Send a friend request
    /// </summary>
    [HttpPost("request")]
    public async Task<ActionResult<Guid>> SendFriendRequest([FromBody] SendFriendRequestRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            if (userId.Value == request.FriendUserId)
            {
                return BadRequest(new { message = "You cannot send a friend request to yourself" });
            }

            var friendRequestId = await _friendsRepository.SendFriendRequestAsync(
                userId.Value,
                request.FriendUserId,
                request.Notes);

            return Ok(new { id = friendRequestId, message = "Friend request sent successfully" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to send friend request");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending friend request");
            return StatusCode(500, new { message = "An error occurred while sending the friend request" });
        }
    }

    /// <summary>
    /// Accept a friend request
    /// </summary>
    [HttpPost("accept")]
    public async Task<ActionResult> AcceptFriendRequest([FromBody] AcceptFriendRequestRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var success = await _friendsRepository.AcceptFriendRequestAsync(request.FriendRequestId, userId.Value);

            if (!success)
            {
                return NotFound(new { message = "Friend request not found or already processed" });
            }

            return Ok(new { message = "Friend request accepted" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error accepting friend request");
            return StatusCode(500, new { message = "An error occurred while accepting the friend request" });
        }
    }

    /// <summary>
    /// Reject a friend request
    /// </summary>
    [HttpPost("reject")]
    public async Task<ActionResult> RejectFriendRequest([FromBody] AcceptFriendRequestRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var success = await _friendsRepository.RejectFriendRequestAsync(request.FriendRequestId, userId.Value);

            if (!success)
            {
                return NotFound(new { message = "Friend request not found" });
            }

            return Ok(new { message = "Friend request rejected" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting friend request");
            return StatusCode(500, new { message = "An error occurred while rejecting the friend request" });
        }
    }

    /// <summary>
    /// Remove a friend
    /// </summary>
    [HttpDelete("{friendUserId:guid}")]
    public async Task<ActionResult> RemoveFriend(Guid friendUserId)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var success = await _friendsRepository.RemoveFriendAsync(userId.Value, friendUserId);

            if (!success)
            {
                return NotFound(new { message = "Friendship not found" });
            }

            return Ok(new { message = "Friend removed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing friend");
            return StatusCode(500, new { message = "An error occurred while removing the friend" });
        }
    }

    /// <summary>
    /// Block a user
    /// </summary>
    [HttpPost("block")]
    public async Task<ActionResult> BlockUser([FromBody] BlockUserRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            if (userId.Value == request.UserIdToBlock)
            {
                return BadRequest(new { message = "You cannot block yourself" });
            }

            await _friendsRepository.BlockUserAsync(userId.Value, request.UserIdToBlock, request.Reason);

            return Ok(new { message = "User blocked successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error blocking user");
            return StatusCode(500, new { message = "An error occurred while blocking the user" });
        }
    }

    /// <summary>
    /// Unblock a user
    /// </summary>
    [HttpPost("unblock/{userToUnblockId:guid}")]
    public async Task<ActionResult> UnblockUser(Guid userToUnblockId)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var success = await _friendsRepository.UnblockUserAsync(userId.Value, userToUnblockId);

            if (!success)
            {
                return NotFound(new { message = "Blocked user not found" });
            }

            return Ok(new { message = "User unblocked successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unblocking user");
            return StatusCode(500, new { message = "An error occurred while unblocking the user" });
        }
    }

    /// <summary>
    /// Send an invitation to a new user
    /// </summary>
    [HttpPost("invite")]
    public async Task<ActionResult<Guid>> SendInvitation([FromBody] InviteFriendRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var invitationId = await _friendsRepository.SendInvitationAsync(
                userId.Value,
                request.InviteeEmail,
                request.InviteePhone,
                request.InvitationMessage);

            return Ok(new { id = invitationId, message = "Invitation sent successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending invitation");
            return StatusCode(500, new { message = "An error occurred while sending the invitation" });
        }
    }

    /// <summary>
    /// Get user's sent invitations
    /// </summary>
    [HttpGet("invitations")]
    public async Task<ActionResult<List<FriendInvitationDto>>> GetInvitations()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var invitations = await _friendsRepository.GetUserInvitationsAsync(userId.Value);
            return Ok(invitations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving invitations");
            return StatusCode(500, new { message = "An error occurred while retrieving your invitations" });
        }
    }

    /// <summary>
    /// Accept an invitation (public endpoint for new users)
    /// </summary>
    [HttpPost("accept-invitation")]
    [AllowAnonymous]
    public async Task<ActionResult> AcceptInvitation([FromQuery] string invitationCode)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated. Please register or login first." });
            }

            var success = await _friendsRepository.AcceptInvitationAsync(invitationCode, userId.Value);

            if (!success)
            {
                return BadRequest(new { message = "Invalid or expired invitation code" });
            }

            return Ok(new { message = "Invitation accepted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error accepting invitation");
            return StatusCode(500, new { message = "An error occurred while accepting the invitation" });
        }
    }

    /// <summary>
    /// Get invitation details by code (public for validation)
    /// </summary>
    [HttpGet("invitation/{invitationCode}")]
    [AllowAnonymous]
    public async Task<ActionResult<FriendInvitationDto>> GetInvitationByCode(string invitationCode)
    {
        try
        {
            var invitation = await _friendsRepository.GetInvitationByCodeAsync(invitationCode);

            if (invitation == null)
            {
                return NotFound(new { message = "Invitation not found" });
            }

            return Ok(invitation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving invitation");
            return StatusCode(500, new { message = "An error occurred while retrieving the invitation" });
        }
    }
}
