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
    private readonly IInventoryRepository _repository;

    public HouseholdController(IInventoryRepository repository)
    {
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
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var householdId = await _repository.CreateHouseholdAsync(userId.Value, request.Name, request.Description);
        var household = await _repository.GetHouseholdByIdAsync(householdId);
        return CreatedAtAction(nameof(GetHousehold), new { id = householdId }, household);
    }

    /// <summary>
    /// Get user's households
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetHouseholds()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var households = await _repository.GetUserHouseholdsAsync(userId.Value);
        return Ok(households);
    }

    /// <summary>
    /// Get household by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetHousehold(Guid id)
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

    /// <summary>
    /// Get household members
    /// </summary>
    [HttpGet("{id}/members")]
    public async Task<IActionResult> GetHouseholdMembers(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        if (!await _repository.IsUserMemberOfHouseholdAsync(id, userId.Value))
            return Forbid();
        var members = await _repository.GetHouseholdMembersAsync(id);
        return Ok(members);
    }

    /// <summary>
    /// Add member to household
    /// </summary>
    [HttpPost("{id}/members")]
    public async Task<IActionResult> AddMember(Guid id, [FromBody] AddMemberRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        if (!await _repository.IsUserMemberOfHouseholdAsync(id, userId.Value))
            return Forbid();
        var memberId = await _repository.AddHouseholdMemberAsync(id, request.UserId, request.Role, userId.Value);
        return Ok(new { id = memberId });
    }

    /// <summary>
    /// Update member permissions
    /// </summary>
    [HttpPut("members/{memberId}")]
    public async Task<IActionResult> UpdateMemberPermissions(Guid memberId, [FromBody] UpdateMemberPermissionsRequest request)
    {
        await _repository.UpdateMemberPermissionsAsync(
            memberId,
            request.CanManageInventory,
            request.CanManageShopping,
            request.CanManageMembers);
        return NoContent();
    }

    /// <summary>
    /// Remove member from household
    /// </summary>
    [HttpDelete("members/{memberId}")]
    public async Task<IActionResult> RemoveMember(Guid memberId)
    {
        await _repository.RemoveHouseholdMemberAsync(memberId);
        return NoContent();
    }

    #endregion

    #region Address Management

    /// <summary>
    /// Create address for household
    /// </summary>
    [HttpPost("{householdId}/addresses")]
    public async Task<IActionResult> CreateAddress(Guid householdId, [FromBody] CreateAddressRequest request)
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

    /// <summary>
    /// Get addresses for household
    /// </summary>
    [HttpGet("{householdId}/addresses")]
    public async Task<IActionResult> GetAddresses(Guid householdId)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        if (!await _repository.IsUserMemberOfHouseholdAsync(householdId, userId.Value))
            return Forbid();
        var addresses = await _repository.GetHouseholdAddressesAsync(householdId);
        return Ok(addresses);
    }

    /// <summary>
    /// Get address by ID
    /// </summary>
    [HttpGet("addresses/{id}")]
    public async Task<IActionResult> GetAddress(Guid id)
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

    /// <summary>
    /// Detect nearest address based on current GPS location
    /// </summary>
    [HttpPost("{householdId}/addresses/detect")]
    public async Task<IActionResult> DetectNearestAddress(Guid householdId, [FromBody] DetectAddressRequest request)
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

    /// <summary>
    /// Update address GPS coordinates
    /// </summary>
    [HttpPut("addresses/{id}/coordinates")]
    public async Task<IActionResult> UpdateCoordinates(Guid id, [FromBody] UpdateCoordinatesRequest request)
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

    /// <summary>
    /// Set primary address for household
    /// </summary>
    [HttpPut("{householdId}/addresses/{addressId}/primary")]
    public async Task<IActionResult> SetPrimaryAddress(Guid householdId, Guid addressId)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        if (!await _repository.IsUserMemberOfHouseholdAsync(householdId, userId.Value))
            return Forbid();
        await _repository.SetPrimaryAddressAsync(householdId, addressId);
        return NoContent();
    }

    /// <summary>
    /// Delete address
    /// </summary>
    [HttpDelete("addresses/{id}")]
    public async Task<IActionResult> DeleteAddress(Guid id)
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
