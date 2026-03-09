using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ExpressRecipe.InventoryService.Data;
using ExpressRecipe.InventoryService.Services;

namespace ExpressRecipe.InventoryService.Controllers;

[Authorize]
[ApiController]
[Route("api/equipment")]
public sealed class EquipmentController : ControllerBase
{
    private readonly IEquipmentRepository _equipment;
    private readonly IEquipmentCapabilityResolver _resolver;

    public EquipmentController(IEquipmentRepository equipment, IEquipmentCapabilityResolver resolver)
    { _equipment = equipment; _resolver = resolver; }

    private Guid GetHouseholdId()
        => Guid.Parse(User.FindFirstValue("household_id") ?? Guid.Empty.ToString());

    [HttpGet("templates")]
    public async Task<IActionResult> GetTemplates(CancellationToken ct)
        => Ok(await _equipment.GetTemplatesAsync(ct));

    [HttpGet]
    public async Task<IActionResult> GetInstances(
        [FromQuery] Guid? addressId, [FromQuery] bool activeOnly = true, CancellationToken ct = default)
        => Ok(await _equipment.GetInstancesAsync(GetHouseholdId(), addressId, activeOnly, ct));

    [HttpPost]
    public async Task<IActionResult> AddInstance(
        [FromBody] AddEquipmentRequest req, CancellationToken ct)
    {
        Guid id = await _equipment.AddInstanceAsync(GetHouseholdId(), req.AddressId, req.TemplateId,
            req.CustomName, req.Brand, req.ModelNumber, req.SizeValue, req.SizeUnit, req.Notes, ct);

        // If template selected and no capabilities provided, copy template defaults
        if (req.TemplateId.HasValue && (req.Capabilities == null || req.Capabilities.Count == 0))
        {
            List<EquipmentTemplateDto> templates = await _equipment.GetTemplatesAsync(ct);
            EquipmentTemplateDto? tmpl = templates.FirstOrDefault(t => t.Id == req.TemplateId.Value);
            if (tmpl is not null && tmpl.DefaultCapabilities.Count > 0)
            { await _equipment.SetCapabilitiesAsync(id, tmpl.DefaultCapabilities, ct); }
        }
        else if (req.Capabilities != null && req.Capabilities.Count > 0)
        { await _equipment.SetCapabilitiesAsync(id, req.Capabilities, ct); }

        return Ok(new { id });
    }

    [HttpPut("{id}/capabilities")]
    public async Task<IActionResult> SetCapabilities(Guid id,
        [FromBody] SetCapabilitiesRequest req, CancellationToken ct)
    { await _equipment.SetCapabilitiesAsync(id, req.Capabilities, ct); return NoContent(); }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    { await _equipment.UpdateInstanceAsync(id, null, null, null, null, null, null, false, ct); return NoContent(); }

    [HttpGet("resolve/{capability}")]
    public async Task<IActionResult> Resolve(string capability, CancellationToken ct)
        => Ok(await _resolver.ResolveAsync(GetHouseholdId(), capability, ct));

    [HttpGet("substitute")]
    public async Task<IActionResult> Substitute([FromQuery] string equipmentName, CancellationToken ct)
    {
        string? message = await _resolver.GetSubstituteMessageAsync(GetHouseholdId(), equipmentName, ct);
        return Ok(new { message, found = message is not null });
    }
}

public sealed class AddEquipmentRequest
{
    public Guid? AddressId { get; set; }
    public Guid? TemplateId { get; set; }
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
    public List<string> Capabilities { get; set; } = new();
}
