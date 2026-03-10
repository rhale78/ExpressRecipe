using ExpressRecipe.InventoryService.Data;
using ExpressRecipe.InventoryService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpressRecipe.InventoryService.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class EquipmentController : ControllerBase
{
    private readonly IEquipmentRepository _repository;
    private readonly IEquipmentCapabilityResolver _resolver;

    public EquipmentController(IEquipmentRepository repository, IEquipmentCapabilityResolver resolver)
    {
        _repository = repository;
        _resolver   = resolver;
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
            return StatusCode(500, $"Failed to retrieve equipment templates: {ex.Message}");
        }
    }

    /// <summary>GET /api/equipment — list household equipment instances</summary>
    [HttpGet]
    public async Task<IActionResult> GetInstances([FromQuery] Guid? addressId, [FromQuery] bool? activeOnly, CancellationToken ct)
    {
        Guid? householdId = GetHouseholdId();
        if (householdId is null)
            return Unauthorized();

        try
        {
            List<EquipmentInstanceDto> instances =
                await _repository.GetInstancesAsync(householdId.Value, addressId, activeOnly, ct);
            return Ok(instances);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Failed to retrieve equipment instances: {ex.Message}");
        }
    }

    /// <summary>POST /api/equipment — add equipment instance, copying template default capabilities when none are provided</summary>
    [HttpPost]
    public async Task<IActionResult> AddInstance([FromBody] AddEquipmentRequest request, CancellationToken ct)
    {
        Guid? householdId = GetHouseholdId();
        if (householdId is null)
            return Unauthorized();

        try
        {
            Guid instanceId = await _repository.AddInstanceAsync(
                householdId.Value,
                request.AddressId,
                request.TemplateId,
                request.CustomName,
                request.Brand,
                request.ModelNumber,
                request.SizeValue,
                request.SizeUnit,
                request.Notes,
                ct);

            // Determine capabilities: use explicit list if provided, otherwise copy template defaults
            IEnumerable<string> caps;
            if (request.Capabilities is { Count: > 0 })
            {
                caps = request.Capabilities;
            }
            else if (request.TemplateId.HasValue)
            {
                List<EquipmentTemplateDto> templates = await _repository.GetTemplatesAsync(ct);
                EquipmentTemplateDto? template = templates.FirstOrDefault(t => t.Id == request.TemplateId.Value);
                caps = template?.DefaultCapabilities ?? Enumerable.Empty<string>();
            }
            else
            {
                caps = Enumerable.Empty<string>();
            }

            if (caps.Any())
                await _repository.SetCapabilitiesAsync(instanceId, caps, ct);

            return Ok(new { id = instanceId });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Failed to add equipment instance: {ex.Message}");
        }
    }

    /// <summary>PUT /api/equipment/{id}/capabilities — set capabilities for an instance (ownership required)</summary>
    [HttpPut("{id:guid}/capabilities")]
    public async Task<IActionResult> SetCapabilities(Guid id, [FromBody] SetCapabilitiesRequest request, CancellationToken ct)
    {
        Guid? householdId = GetHouseholdId();
        if (householdId is null)
            return Unauthorized();

        try
        {
            EquipmentInstanceDto? instance = await _repository.GetInstanceByIdAsync(id, ct);
            if (instance is null) return NotFound();
            if (instance.HouseholdId != householdId.Value) return Forbid();

            await _repository.SetCapabilitiesAsync(id, request.Capabilities ?? Enumerable.Empty<string>(), ct);
            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Failed to set capabilities: {ex.Message}");
        }
    }

    /// <summary>DELETE /api/equipment/{id} — deactivate equipment instance (ownership required)</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        Guid? householdId = GetHouseholdId();
        if (householdId is null)
            return BadRequest("HouseholdId claim required");

        try
        {
            EquipmentInstanceDto? instance = await _repository.GetInstanceByIdAsync(id, ct);
            if (instance is null) return NotFound();
            if (instance.HouseholdId != householdId.Value) return Forbid();

            await _repository.UpdateInstanceAsync(id, null, null, null, null, null, null, false, ct);
            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Failed to deactivate equipment instance: {ex.Message}");
        }
    }

    /// <summary>GET /api/equipment/resolve/{capability} — find instances that have this capability</summary>
    [HttpGet("resolve/{capability}")]
    public async Task<IActionResult> Resolve(string capability, CancellationToken ct)
    {
        Guid? householdId = GetHouseholdId();
        if (householdId is null)
            return Unauthorized();

        try
        {
            List<EquipmentInstanceDto> instances =
                await _resolver.ResolveAsync(householdId.Value, capability, ct);
            return Ok(instances);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Failed to resolve equipment by capability: {ex.Message}");
        }
    }

    /// <summary>GET /api/equipment/substitute?equipmentName=X — find substitute equipment</summary>
    [HttpGet("substitute")]
    public async Task<IActionResult> Substitute([FromQuery] string equipmentName, CancellationToken ct)
    {
        Guid? householdId = GetHouseholdId();
        if (householdId is null)
            return Unauthorized();

        try
        {
            string? message = await _resolver.GetSubstituteMessageAsync(householdId.Value, equipmentName, ct);
            return Ok(new { found = message is not null, message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Failed to find equipment substitutes: {ex.Message}");
        }
    }

    private Guid? GetHouseholdId()
    {
        string? claim = User.FindFirst("household_id")?.Value
            ?? User.FindFirst("HouseholdId")?.Value;
        return Guid.TryParse(claim, out Guid id) ? id : null;
    }
}

public sealed class AddEquipmentRequest
{
    public Guid? TemplateId { get; set; }
    public Guid? AddressId { get; set; }
    public string? CustomName { get; set; }
    public string? Brand { get; set; }
    public string? ModelNumber { get; set; }
    public decimal? SizeValue { get; set; }
    public string? SizeUnit { get; set; }
    public string? Notes { get; set; }
    public List<string>? Capabilities { get; set; }
}

public sealed class SetCapabilitiesRequest
{
    public List<string>? Capabilities { get; set; }
}
