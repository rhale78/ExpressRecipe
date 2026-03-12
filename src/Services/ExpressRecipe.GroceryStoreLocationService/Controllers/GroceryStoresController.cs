using ExpressRecipe.GroceryStoreLocationService.Data;
using ExpressRecipe.GroceryStoreLocationService.Services;
using ExpressRecipe.GroceryStoreLocationService.Workers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpressRecipe.GroceryStoreLocationService.Controllers;

[ApiController]
[Route("api/grocerystores")]
public class GroceryStoresController : ControllerBase
{
    private readonly IGroceryStoreRepository _repository;
    private readonly StoreLocationImportWorker? _importWorker;
    private readonly ILogger<GroceryStoresController> _logger;

    public GroceryStoresController(
        IGroceryStoreRepository repository,
        ILogger<GroceryStoresController> logger,
        StoreLocationImportWorker? importWorker = null)
    {
        _repository = repository;
        _importWorker = importWorker;
        _logger = logger;
    }

    /// <summary>GET /api/grocerystores - Search stores with filters</summary>
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string? name,
        [FromQuery] string? chain,
        [FromQuery] string? city,
        [FromQuery] string? state,
        [FromQuery] string? zipCode,
        [FromQuery] string? storeType,
        [FromQuery] bool? acceptsSnap,
        [FromQuery] bool? isVerified,
        [FromQuery] string? normalizedChain,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        page = Math.Max(1, page);

        var request = new GroceryStoreSearchRequest
        {
            Name = name,
            Chain = chain,
            NormalizedChain = normalizedChain,
            City = city,
            State = state,
            ZipCode = zipCode,
            StoreType = storeType,
            AcceptsSnap = acceptsSnap,
            IsActive = true,
            IsVerified = isVerified,
            Page = page,
            PageSize = pageSize
        };

        var stores = await _repository.SearchAsync(request);
        var total = await _repository.GetSearchCountAsync(request);

        return Ok(new
        {
            Data = stores,
            Total = total,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(total / (double)pageSize)
        });
    }

    /// <summary>GET /api/grocerystores/{id} - Get store by ID</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var store = await _repository.GetByIdAsync(id);
        if (store == null) return NotFound();
        return Ok(store);
    }

    /// <summary>GET /api/grocerystores/nearby - Get stores near a location</summary>
    [HttpGet("nearby")]
    public async Task<IActionResult> GetNearby(
        [FromQuery] double lat,
        [FromQuery] double lon,
        [FromQuery] double radiusMiles = 10.0,
        [FromQuery] int limit = 50)
    {
        if (lat < -90.0 || lat > 90.0)
            return BadRequest(new { message = "lat must be between -90 and 90." });
        if (lon < -180.0 || lon > 180.0)
            return BadRequest(new { message = "lon must be between -180 and 180." });
        limit = Math.Clamp(limit, 1, 200);
        radiusMiles = Math.Clamp(radiusMiles, 0.1, 100.0);

        var stores = await _repository.GetNearbyAsync(lat, lon, radiusMiles, limit);
        return Ok(stores);
    }

    /// <summary>GET /api/grocerystores/chain/{normalizedChain} - Get stores by canonical chain name</summary>
    [HttpGet("chain/{normalizedChain}")]
    public async Task<IActionResult> GetByChain(string normalizedChain, [FromQuery] int limit = 100)
    {
        limit = Math.Clamp(limit, 1, 500);
        var stores = await _repository.GetByChainAsync(normalizedChain, limit);
        return Ok(stores);
    }

    /// <summary>GET /api/grocerystores/chains - List all known chains</summary>
    [HttpGet("chains")]
    public async Task<IActionResult> GetChains()
    {
        var chains = await _repository.GetAllChainsAsync();
        return Ok(chains);
    }

    /// <summary>GET /api/grocerystores/{id}/hours - Get structured store hours</summary>
    [HttpGet("{id:guid}/hours")]
    public async Task<IActionResult> GetStoreHours(Guid id)
    {
        var store = await _repository.GetByIdAsync(id);
        if (store == null) return NotFound();

        var hours = await _repository.GetStoreHoursAsync(id);
        return Ok(hours);
    }

    /// <summary>PUT /api/grocerystores/{id}/hours - Update store hours (admin only)</summary>
    [HttpPut("{id:guid}/hours")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpsertStoreHours(Guid id, [FromBody] List<StoreHoursRequest> hours)
    {
        var store = await _repository.GetByIdAsync(id);
        if (store == null) return NotFound();

        var invalidDays = hours.Where(h => !h.IsHoliday && h.DayOfWeek > 6).ToList();
        if (invalidDays.Count > 0)
        {
            return BadRequest($"DayOfWeek must be 0–6 (Sunday–Saturday). Invalid values: {string.Join(", ", invalidDays.Select(h => h.DayOfWeek))}");
        }

        var updated = await _repository.UpsertStoreHoursAsync(id, hours);
        return Ok(new { Updated = updated });
    }

    /// <summary>POST /api/grocerystores/import/trigger - Trigger import (admin only)</summary>
    [HttpPost("import/trigger")]
    [Authorize(Roles = "Admin")]
    public IActionResult TriggerImport([FromQuery] string source = "all")
    {
        _logger.LogInformation("Manual import triggered by {User} for source: {Source}",
            User.Identity?.Name ?? "unknown", source);

        if (_importWorker == null)
            return StatusCode(503, "Import worker is not available");

        // Delegate to the hosted StoreLocationImportWorker so the work is tracked
        // and will be cancelled cleanly on application shutdown.
        _ = _importWorker.RunImportAsync(source, HttpContext.RequestAborted);

        return Accepted(new { Message = $"Import triggered for source: {source}", Status = "accepted" });
    }

    /// <summary>GET /api/grocerystores/import/status - Get last import status per source</summary>
    [HttpGet("import/status")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetImportStatus()
    {
        var snapLog = await _repository.GetLastImportAsync("USDA_SNAP");
        var osmLog = await _repository.GetLastImportAsync("OPENSTREETMAP");
        var openPricesLog = await _repository.GetLastImportAsync("OpenPrices");
        var overtureLog = await _repository.GetLastImportAsync("OVERTURE_MAPS");
        var hifldLog = await _repository.GetLastImportAsync("HIFLD");

        return Ok(new
        {
            USDA_SNAP = snapLog,
            OSM = osmLog,
            OpenPrices = openPricesLog,
            Overture = overtureLog,
            HIFLD = hifldLog
        });
    }
}
