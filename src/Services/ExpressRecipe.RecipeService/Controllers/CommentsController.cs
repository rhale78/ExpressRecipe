using ExpressRecipe.Shared.DTOs.User;
using ExpressRecipe.RecipeService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;

namespace ExpressRecipe.RecipeService.Controllers
{
    public class FlagCommentRequest
    {
        [Required]
        [StringLength(500, MinimumLength = 10)]
        public string Reason { get; set; } = string.Empty;
    }

    [ApiController]
    [Route("api/[controller]")]
    public class CommentsController : ControllerBase
    {
        private readonly ICommentsRepository _commentsRepository;
        private readonly ILogger<CommentsController> _logger;

        public CommentsController(
            ICommentsRepository commentsRepository,
            ILogger<CommentsController> logger)
        {
            _commentsRepository = commentsRepository;
            _logger = logger;
        }

        private Guid? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId) ? null : userId;
        }

        /// <summary>
        /// Get comments for a recipe
        /// </summary>
        [HttpGet("recipe/{recipeId:guid}")]
        public async Task<ActionResult<List<RecipeCommentDto>>> GetRecipeComments(
            Guid recipeId,
            [FromQuery] string sortBy = "CreatedAt",
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                if (pageNumber < 1 || pageSize < 1 || pageSize > 100)
                {
                    return BadRequest(new { message = "Invalid pagination parameters" });
                }

                List<RecipeCommentDto> comments = await _commentsRepository.GetRecipeCommentsAsync(recipeId, sortBy, pageNumber, pageSize);
                return Ok(comments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving comments for recipe {RecipeId}", recipeId);
                return StatusCode(500, new { message = "An error occurred while retrieving comments" });
            }
        }

        /// <summary>
        /// Get a specific comment
        /// </summary>
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<RecipeCommentDto>> GetComment(Guid id)
        {
            try
            {
                RecipeCommentDto? comment = await _commentsRepository.GetCommentByIdAsync(id);

                return comment == null ? (ActionResult<RecipeCommentDto>)NotFound(new { message = "Comment not found" }) : (ActionResult<RecipeCommentDto>)Ok(comment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving comment {CommentId}", id);
                return StatusCode(500, new { message = "An error occurred while retrieving the comment" });
            }
        }

        /// <summary>
        /// Get replies to a comment
        /// </summary>
        [HttpGet("{id:guid}/replies")]
        public async Task<ActionResult<List<RecipeCommentDto>>> GetReplies(Guid id)
        {
            try
            {
                List<RecipeCommentDto> replies = await _commentsRepository.GetCommentRepliesAsync(id);
                return Ok(replies);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving replies for comment {CommentId}", id);
                return StatusCode(500, new { message = "An error occurred while retrieving replies" });
            }
        }

        /// <summary>
        /// Get current user's comments
        /// </summary>
        [HttpGet("user")]
        [Authorize]
        public async Task<ActionResult<List<RecipeCommentDto>>> GetUserComments()
        {
            try
            {
                Guid? userId = GetCurrentUserId();
                if (!userId.HasValue)
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                List<RecipeCommentDto> comments = await _commentsRepository.GetUserCommentsAsync(userId.Value);
                return Ok(comments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user comments");
                return StatusCode(500, new { message = "An error occurred while retrieving your comments" });
            }
        }

        /// <summary>
        /// Get flagged comments (admin only)
        /// </summary>
        [HttpGet("flagged")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<List<RecipeCommentDto>>> GetFlaggedComments()
        {
            try
            {
                List<RecipeCommentDto> comments = await _commentsRepository.GetFlaggedCommentsAsync();
                return Ok(comments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving flagged comments");
                return StatusCode(500, new { message = "An error occurred while retrieving flagged comments" });
            }
        }

        /// <summary>
        /// Create a comment
        /// </summary>
        [HttpPost]
        [Authorize]
        public async Task<ActionResult<Guid>> CreateComment([FromBody] CreateRecipeCommentRequest request)
        {
            try
            {
                Guid? userId = GetCurrentUserId();
                if (!userId.HasValue)
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                Guid commentId = await _commentsRepository.CreateCommentAsync(userId.Value, request);

                return CreatedAtAction(nameof(GetComment), new { id = commentId }, new { id = commentId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating comment");
                return StatusCode(500, new { message = "An error occurred while creating the comment" });
            }
        }

        /// <summary>
        /// Update a comment
        /// </summary>
        [HttpPut("{id:guid}")]
        [Authorize]
        public async Task<ActionResult> UpdateComment(Guid id, [FromBody] UpdateRecipeCommentRequest request)
        {
            try
            {
                Guid? userId = GetCurrentUserId();
                if (!userId.HasValue)
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                var success = await _commentsRepository.UpdateCommentAsync(id, userId.Value, request);

                return !success ? NotFound(new { message = "Comment not found or you do not have permission to update it" }) : NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating comment {CommentId}", id);
                return StatusCode(500, new { message = "An error occurred while updating the comment" });
            }
        }

        /// <summary>
        /// Delete a comment
        /// </summary>
        [HttpDelete("{id:guid}")]
        [Authorize]
        public async Task<ActionResult> DeleteComment(Guid id)
        {
            try
            {
                Guid? userId = GetCurrentUserId();
                if (!userId.HasValue)
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                var success = await _commentsRepository.DeleteCommentAsync(id, userId.Value);

                return !success ? NotFound(new { message = "Comment not found or you do not have permission to delete it" }) : NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting comment {CommentId}", id);
                return StatusCode(500, new { message = "An error occurred while deleting the comment" });
            }
        }

        /// <summary>
        /// Like a comment
        /// </summary>
        [HttpPost("{id:guid}/like")]
        [Authorize]
        public async Task<ActionResult> LikeComment(Guid id)
        {
            try
            {
                Guid? userId = GetCurrentUserId();
                if (!userId.HasValue)
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                var success = await _commentsRepository.LikeCommentAsync(id, userId.Value);

                if (!success)
                {
                    return BadRequest(new { message = "Already liked or comment not found" });
                }

                return Ok(new { message = "Comment liked successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error liking comment {CommentId}", id);
                return StatusCode(500, new { message = "An error occurred while liking the comment" });
            }
        }

        /// <summary>
        /// Unlike a comment
        /// </summary>
        [HttpDelete("{id:guid}/like")]
        [Authorize]
        public async Task<ActionResult> UnlikeComment(Guid id)
        {
            try
            {
                Guid? userId = GetCurrentUserId();
                if (!userId.HasValue)
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                var success = await _commentsRepository.UnlikeCommentAsync(id, userId.Value);

                return !success ? NotFound(new { message = "Like not found" }) : NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unliking comment {CommentId}", id);
                return StatusCode(500, new { message = "An error occurred while unliking the comment" });
            }
        }

        /// <summary>
        /// Dislike a comment
        /// </summary>
        [HttpPost("{id:guid}/dislike")]
        [Authorize]
        public async Task<ActionResult> DislikeComment(Guid id)
        {
            try
            {
                Guid? userId = GetCurrentUserId();
                if (!userId.HasValue)
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                var success = await _commentsRepository.DislikeCommentAsync(id, userId.Value);

                if (!success)
                {
                    return BadRequest(new { message = "Already disliked or comment not found" });
                }

                return Ok(new { message = "Comment disliked successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disliking comment {CommentId}", id);
                return StatusCode(500, new { message = "An error occurred while disliking the comment" });
            }
        }

        /// <summary>
        /// Undislike a comment
        /// </summary>
        [HttpDelete("{id:guid}/dislike")]
        [Authorize]
        public async Task<ActionResult> UndislikeComment(Guid id)
        {
            try
            {
                Guid? userId = GetCurrentUserId();
                if (!userId.HasValue)
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                var success = await _commentsRepository.UndislikeCommentAsync(id, userId.Value);

                return !success ? NotFound(new { message = "Dislike not found" }) : NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error undisliking comment {CommentId}", id);
                return StatusCode(500, new { message = "An error occurred while undisliking the comment" });
            }
        }

        /// <summary>
        /// Flag a comment
        /// </summary>
        [HttpPost("{id:guid}/flag")]
        [Authorize]
        public async Task<ActionResult> FlagComment(Guid id, [FromBody] FlagCommentRequest request)
        {
            try
            {
                Guid? userId = GetCurrentUserId();
                if (!userId.HasValue)
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                var success = await _commentsRepository.FlagCommentAsync(id, userId.Value, request.Reason);

                if (!success)
                {
                    return NotFound(new { message = "Comment not found" });
                }

                _logger.LogWarning("Comment {CommentId} flagged by user {UserId}: {Reason}",
                    id, userId.Value, request.Reason);

                return Ok(new { message = "Comment flagged successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error flagging comment {CommentId}", id);
                return StatusCode(500, new { message = "An error occurred while flagging the comment" });
            }
        }

        /// <summary>
        /// Unflag a comment (admin only)
        /// </summary>
        [HttpDelete("{id:guid}/flag")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> UnflagComment(Guid id)
        {
            try
            {
                Guid? userId = GetCurrentUserId();
                if (!userId.HasValue)
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                var success = await _commentsRepository.UnflagCommentAsync(id, userId.Value);

                return !success ? NotFound(new { message = "Comment not found" }) : NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unflagging comment {CommentId}", id);
                return StatusCode(500, new { message = "An error occurred while unflagging the comment" });
            }
        }
    }
}
