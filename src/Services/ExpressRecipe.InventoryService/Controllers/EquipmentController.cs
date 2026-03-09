using ExpressRecipe.InventoryService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpressRecipe.InventoryService.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class EquipmentController : ControllerBase
{
    private readonly IEquipmentRepository _repository;
    private readonly ILogger<EquipmentController> _logger;

    public EquipmentController(IEquipmentRepository repository, ILogger<EquipmentController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>GET /api/equipment/templates — list all templates grouped by category</summary>
    [HttpGet("templates")]
    public async Task<IActionResult> GetTemplates(CancellationToken ct)
    {
        try
        {
            List<EquipmentTemplateDto> templates = await _repository.GetTemplatesAsync(ct);
            return Ok(templates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving equipment templates");
            return StatusCode(500, "Failed to retrieve equipment templates");
        }
    }

    /// <summary>GET /api/equipment — list household equipment instances</summary>
    [HttpGet]
    public async Task<IActionResult> GetInstances(CancellationToken ct)
    {
        try
        {
            Guid? householdId = GetHouseholdId();
            if (householdId is null)
            {
                return BadRequest("HouseholdId claim required");
            }

            List<EquipmentInstanceDto> instances =
                await _repository.GetInstancesByHouseholdAsync(householdId.Value, ct);
            return Ok(instances);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving equipment instances");
            return StatusCode(500, "Failed to retrieve equipment instances");
        }
    }

    /// <summary>POST /api/equipment — add equipment instance</summary>
    [HttpPost]
    public async Task<IActionResult> CreateInstance([FromBody] CreateEquipmentInstanceRequest request, CancellationToken ct)
    {
        try
        {
            Guid? householdId = GetHouseholdId();
            if (householdId is null)
            {
                return BadRequest("HouseholdId claim required");
            }

            Guid instanceId = await _repository.CreateInstanceAsync(
                householdId.Value,
                request.AddressId,
                request.TemplateId,
                request.CustomName,
                request.Brand,
                request.ModelNumber,
                request.SizeValue,
                request.SizeUnit,
                request.Notes,
                request.Capabilities ?? Enumerable.Empty<string>(),
                ct);

            return CreatedAtAction(nameof(GetInstances), new { }, new { id = instanceId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating equipment instance");
            return StatusCode(500, "Failed to create equipment instance");
        }
    }

    /// <summary>DELETE /api/equipment/{id} — deactivate equipment instance</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeactivateInstance(Guid id, CancellationToken ct)
    {
        try
        {
            await _repository.DeactivateInstanceAsync(id, ct);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating equipment instance {InstanceId}", id);
            return StatusCode(500, "Failed to deactivate equipment instance");
        }
    }

    /// <summary>GET /api/equipment/resolve/{capability} — find instances that have this capability</summary>
    [HttpGet("resolve/{capability}")]
    public async Task<IActionResult> ResolveByCapability(string capability, CancellationToken ct)
    {
        try
        {
            Guid? householdId = GetHouseholdId();
            if (householdId is null)
            {
                return BadRequest("HouseholdId claim required");
            }

            List<EquipmentInstanceDto> instances =
                await _repository.ResolveByCapabilityAsync(householdId.Value, capability, ct);
            return Ok(instances);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving equipment by capability {Capability}", capability);
            return StatusCode(500, "Failed to resolve equipment by capability");
        }
    }

    /// <summary>GET /api/equipment/substitute?equipmentName=X — find substitute equipment</summary>
    [HttpGet("substitute")]
    public async Task<IActionResult> FindSubstitutes([FromQuery] string equipmentName, CancellationToken ct)
    {
        try
        {
            Guid? householdId = GetHouseholdId();
            if (householdId is null)
            {
                return BadRequest("HouseholdId claim required");
            }

            List<EquipmentSubstituteDto> substitutes =
                await _repository.FindSubstitutesAsync(householdId.Value, equipmentName, ct);
            return Ok(substitutes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding substitutes for {EquipmentName}", equipmentName);
            return StatusCode(500, "Failed to find equipment substitutes");
        }
    }

    private Guid? GetHouseholdId()
    {
        string? claim = User.FindFirst("household_id")?.Value
            ?? User.FindFirst("HouseholdId")?.Value;
        return Guid.TryParse(claim, out Guid id) ? id : null;
    }
}

public sealed class CreateEquipmentInstanceRequest
{
    public Guid TemplateId { get; set; }
    public Guid? AddressId { get; set; }
    public string? CustomName { get; set; }
    public string? Brand { get; set; }
    public string? ModelNumber { get; set; }
    public decimal? SizeValue { get; set; }
    public string? SizeUnit { get; set; }
    public string? Notes { get; set; }
    public List<string>? Capabilities { get; set; }
}
