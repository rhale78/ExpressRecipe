using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ExpressRecipe.InventoryService.Data;

namespace ExpressRecipe.InventoryService.Controllers;

/// <summary>
/// Household and address management endpoints
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class HouseholdController : ControllerBase
{
    private readonly ILogger<HouseholdController> _logger;
    private readonly IInventoryRepository _repository;

    public HouseholdController(ILogger<HouseholdController> logger, IInventoryRepository repository)
    {
        _logger = logger;
        _repository = repository;
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    #region Household Management

    /// <summary>
    /// Create a new household
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateHousehold([FromBody] CreateHouseholdRequest request)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            var householdId = await _repository.CreateHouseholdAsync(userId.Value, request.Name, request.Description);
            var household = await _repository.GetHouseholdByIdAsync(householdId);
            return CreatedAtAction(nameof(GetHousehold), new { id = householdId }, household);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating household");
            return StatusCode(500, new { message = "An error occurred while creating the household" });
        }
    }

    /// <summary>
    /// Get user's households
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetHouseholds()
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            var households = await _repository.GetUserHouseholdsAsync(userId.Value);
            return Ok(households);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving households");
            return StatusCode(500, new { message = "An error occurred while retrieving households" });
        }
    }

    /// <summary>
    /// Get household by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetHousehold(Guid id)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            if (!await _repository.IsUserMemberOfHouseholdAsync(id, userId.Value))
                return Forbid();
            var household = await _repository.GetHouseholdByIdAsync(id);
            if (household == null)
                return NotFound();
            return Ok(household);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving household {HouseholdId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving the household" });
        }
    }

    /// <summary>
    /// Get household members
    /// </summary>
    [HttpGet("{id}/members")]
    public async Task<IActionResult> GetHouseholdMembers(Guid id)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            if (!await _repository.IsUserMemberOfHouseholdAsync(id, userId.Value))
                return Forbid();
            var members = await _repository.GetHouseholdMembersAsync(id);
            return Ok(members);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving members for household {HouseholdId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving household members" });
        }
    }

    /// <summary>
    /// Add member to household
    /// </summary>
    [HttpPost("{id}/members")]
    public async Task<IActionResult> AddMember(Guid id, [FromBody] AddMemberRequest request)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            if (!await _repository.IsUserMemberOfHouseholdAsync(id, userId.Value))
                return Forbid();
            var memberId = await _repository.AddHouseholdMemberAsync(id, request.UserId, request.Role, userId.Value);
            return Ok(new { id = memberId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding member to household {HouseholdId}", id);
            return StatusCode(500, new { message = "An error occurred while adding the household member" });
        }
    }

    /// <summary>
    /// Update member permissions
    /// </summary>
    [HttpPut("members/{memberId}")]
    public async Task<IActionResult> UpdateMemberPermissions(Guid memberId, [FromBody] UpdateMemberPermissionsRequest request)
    {
        try
        {
            await _repository.UpdateMemberPermissionsAsync(
                memberId,
                request.CanManageInventory,
                request.CanManageShopping,
                request.CanManageMembers);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating permissions for member {MemberId}", memberId);
            return StatusCode(500, new { message = "An error occurred while updating member permissions" });
        }
    }

    /// <summary>
    /// Remove member from household
    /// </summary>
    [HttpDelete("members/{memberId}")]
    public async Task<IActionResult> RemoveMember(Guid memberId)
    {
        try
        {
            await _repository.RemoveHouseholdMemberAsync(memberId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing member {MemberId}", memberId);
            return StatusCode(500, new { message = "An error occurred while removing the household member" });
        }
    }

    #endregion

    #region Address Management

    /// <summary>
    /// Create address for household
    /// </summary>
    [HttpPost("{householdId}/addresses")]
    public async Task<IActionResult> CreateAddress(Guid householdId, [FromBody] CreateAddressRequest request)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            if (!await _repository.IsUserMemberOfHouseholdAsync(householdId, userId.Value))
                return Forbid();
            var addressId = await _repository.CreateAddressAsync(
                householdId,
                request.Name,
                request.Street,
                request.City,
                request.State,
                request.ZipCode,
                request.Country ?? "USA",
                request.Latitude,
                request.Longitude,
                request.IsPrimary);
            var address = await _repository.GetAddressByIdAsync(addressId);
            return CreatedAtAction(nameof(GetAddress), new { id = addressId }, address);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating address for household {HouseholdId}", householdId);
            return StatusCode(500, new { message = "An error occurred while creating the address" });
        }
    }

    /// <summary>
    /// Get addresses for household
    /// </summary>
    [HttpGet("{householdId}/addresses")]
    public async Task<IActionResult> GetAddresses(Guid householdId)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            if (!await _repository.IsUserMemberOfHouseholdAsync(householdId, userId.Value))
                return Forbid();
            var addresses = await _repository.GetHouseholdAddressesAsync(householdId);
            return Ok(addresses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving addresses for household {HouseholdId}", householdId);
            return StatusCode(500, new { message = "An error occurred while retrieving addresses" });
        }
    }

    /// <summary>
    /// Get address by ID
    /// </summary>
    [HttpGet("addresses/{id}")]
    public async Task<IActionResult> GetAddress(Guid id)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            var address = await _repository.GetAddressByIdAsync(id);
            if (address == null)
                return NotFound();
            if (!await _repository.IsUserMemberOfHouseholdAsync(address.HouseholdId, userId.Value))
                return Forbid();
            return Ok(address);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving address {AddressId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving the address" });
        }
    }

    /// <summary>
    /// Detect nearest address based on current GPS location
    /// </summary>
    [HttpPost("{householdId}/addresses/detect")]
    public async Task<IActionResult> DetectNearestAddress(Guid householdId, [FromBody] DetectAddressRequest request)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            if (!await _repository.IsUserMemberOfHouseholdAsync(householdId, userId.Value))
                return Forbid();
            var address = await _repository.DetectNearestAddressAsync(
                householdId,
                request.Latitude,
                request.Longitude,
                request.MaxDistanceKm ?? 1.0);
            if (address == null)
                return NotFound(new { message = "No nearby addresses found" });
            return Ok(address);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting nearest address for household {HouseholdId}", householdId);
            return StatusCode(500, new { message = "An error occurred while detecting the nearest address" });
        }
    }

    /// <summary>
    /// Update address GPS coordinates
    /// </summary>
    [HttpPut("addresses/{id}/coordinates")]
    public async Task<IActionResult> UpdateCoordinates(Guid id, [FromBody] UpdateCoordinatesRequest request)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            var address = await _repository.GetAddressByIdAsync(id);
            if (address == null) return NotFound();
            if (!await _repository.IsUserMemberOfHouseholdAsync(address.HouseholdId, userId.Value))
                return Forbid();
            await _repository.UpdateAddressCoordinatesAsync(id, request.Latitude, request.Longitude);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating coordinates for address {AddressId}", id);
            return StatusCode(500, new { message = "An error occurred while updating address coordinates" });
        }
    }

    /// <summary>
    /// Set primary address for household
    /// </summary>
    [HttpPut("{householdId}/addresses/{addressId}/primary")]
    public async Task<IActionResult> SetPrimaryAddress(Guid householdId, Guid addressId)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            if (!await _repository.IsUserMemberOfHouseholdAsync(householdId, userId.Value))
                return Forbid();
            await _repository.SetPrimaryAddressAsync(householdId, addressId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting primary address {AddressId} for household {HouseholdId}", addressId, householdId);
            return StatusCode(500, new { message = "An error occurred while setting the primary address" });
        }
    }

    /// <summary>
    /// Delete address
    /// </summary>
    [HttpDelete("addresses/{id}")]
    public async Task<IActionResult> DeleteAddress(Guid id)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            var address = await _repository.GetAddressByIdAsync(id);
            if (address == null) return NotFound();
            if (!await _repository.IsUserMemberOfHouseholdAsync(address.HouseholdId, userId.Value))
                return Forbid();
            await _repository.DeleteAddressAsync(id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting address {AddressId}", id);
            return StatusCode(500, new { message = "An error occurred while deleting the address" });
        }
    }

    #endregion
}

#region Request DTOs

public class CreateHouseholdRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class AddMemberRequest
{
    public Guid UserId { get; set; }
    public string Role { get; set; } = "Member";
}

public class UpdateMemberPermissionsRequest
{
    public bool CanManageInventory { get; set; }
    public bool CanManageShopping { get; set; }
    public bool CanManageMembers { get; set; }
}

public class CreateAddressRequest
{
    public string Name { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string? Country { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public bool IsPrimary { get; set; }
}

public class DetectAddressRequest
{
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public double? MaxDistanceKm { get; set; }
}

public class UpdateCoordinatesRequest
{
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
}

#endregion
