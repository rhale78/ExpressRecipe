using ExpressRecipe.CookbookService.Data;
using ExpressRecipe.CookbookService.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text;

namespace ExpressRecipe.CookbookService.Controllers;

[ApiController]
[Route("api/cookbooks/{id:guid}")]
[Authorize]
public class CookbookExportController : ControllerBase
{
    private readonly ICookbookRepository _repository;
    private readonly ILogger<CookbookExportController> _logger;

    public CookbookExportController(ICookbookRepository repository, ILogger<CookbookExportController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    private Guid? GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(claim) || !Guid.TryParse(claim, out var uid)) return null;
        return uid;
    }

    [HttpGet("print-preview")]
    [AllowAnonymous]
    public async Task<ActionResult<string>> PrintPreview(Guid id)
    {
        try
        {
            var cookbook = await _repository.GetCookbookByIdAsync(id, includeSections: true);
            if (cookbook == null) return NotFound(new { message = "Cookbook not found" });

            if (cookbook.Visibility != "Public")
            {
                var userId = GetCurrentUserId();
                if (!userId.HasValue || !await _repository.CanViewAsync(id, userId.Value))
                    return Forbid();
            }

            var html = GenerateCookbookHtml(cookbook);
            return Content(html, "text/html");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating print preview for cookbook {Id}", id);
            return StatusCode(500, new { message = "An error occurred while generating the preview" });
        }
    }

    [HttpPost("export/pdf")]
    public async Task<ActionResult> ExportPdf(Guid id, [FromBody] ExportCookbookRequest? request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized(new { message = "User not authenticated" });

            var cookbook = await _repository.GetCookbookByIdAsync(id, includeSections: true);
            if (cookbook == null) return NotFound(new { message = "Cookbook not found" });

            if (!await _repository.CanViewAsync(id, userId.Value)) return Forbid();

            var html = GenerateCookbookHtml(cookbook, request);
            var bytes = Encoding.UTF8.GetBytes(html);
            return File(bytes, "text/html", $"{cookbook.Title}.html");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting cookbook {Id} as PDF", id);
            return StatusCode(500, new { message = "An error occurred while exporting the cookbook" });
        }
    }

    [HttpPost("export/word")]
    public async Task<ActionResult> ExportWord(Guid id, [FromBody] ExportCookbookRequest? request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized(new { message = "User not authenticated" });

            var cookbook = await _repository.GetCookbookByIdAsync(id, includeSections: true);
            if (cookbook == null) return NotFound(new { message = "Cookbook not found" });

            if (!await _repository.CanViewAsync(id, userId.Value)) return Forbid();

            var html = GenerateCookbookHtml(cookbook, request);
            var bytes = Encoding.UTF8.GetBytes(html);
            return File(bytes, "text/html", $"{cookbook.Title}.html");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting cookbook {Id} as Word", id);
            return StatusCode(500, new { message = "An error occurred while exporting the cookbook" });
        }
    }

    private static string GenerateCookbookHtml(CookbookDto cookbook, ExportCookbookRequest? options = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine($"<meta charset=\"UTF-8\"><title>{System.Net.WebUtility.HtmlEncode(cookbook.Title)}</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body { font-family: Georgia, serif; max-width: 800px; margin: 0 auto; padding: 2rem; }");
        sb.AppendLine("h1 { font-size: 2.5rem; text-align: center; border-bottom: 2px solid #333; }");
        sb.AppendLine("h2 { font-size: 1.8rem; margin-top: 2rem; }");
        sb.AppendLine("h3 { font-size: 1.4rem; }");
        sb.AppendLine(".recipe-item { margin: 0.5rem 0; padding: 0.5rem; border-left: 3px solid #ccc; }");
        sb.AppendLine(".section { margin: 2rem 0; }");
        sb.AppendLine("@media print { .no-print { display: none; } }");
        sb.AppendLine("</style>");
        sb.AppendLine("</head><body>");

        if (options == null || options.IncludeTitlePage)
        {
            sb.AppendLine($"<h1>{System.Net.WebUtility.HtmlEncode(cookbook.Title)}</h1>");
            if (!string.IsNullOrEmpty(cookbook.Subtitle))
                sb.AppendLine($"<h2 style=\"text-align:center;font-style:italic\">{System.Net.WebUtility.HtmlEncode(cookbook.Subtitle)}</h2>");
            if (!string.IsNullOrEmpty(cookbook.AuthorName))
                sb.AppendLine($"<p style=\"text-align:center\">By {System.Net.WebUtility.HtmlEncode(cookbook.AuthorName)}</p>");
            if (!string.IsNullOrEmpty(cookbook.TitlePageContent))
                sb.AppendLine($"<div>{cookbook.TitlePageContent}</div>");
        }

        if ((options == null || options.IncludeIntroduction) && !string.IsNullOrEmpty(cookbook.IntroductionContent))
        {
            sb.AppendLine("<hr><h2>Introduction</h2>");
            sb.AppendLine($"<div>{cookbook.IntroductionContent}</div>");
        }

        if (!string.IsNullOrEmpty(cookbook.Description))
        {
            sb.AppendLine($"<p>{System.Net.WebUtility.HtmlEncode(cookbook.Description)}</p>");
        }

        foreach (var section in cookbook.Sections)
        {
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine($"<h2>{System.Net.WebUtility.HtmlEncode(section.Title)}</h2>");
            if (!string.IsNullOrEmpty(section.Description))
                sb.AppendLine($"<p>{System.Net.WebUtility.HtmlEncode(section.Description)}</p>");
            if (!string.IsNullOrEmpty(section.TitlePageContent))
                sb.AppendLine($"<div>{section.TitlePageContent}</div>");

            foreach (var recipe in section.Recipes)
            {
                sb.AppendLine("<div class=\"recipe-item\">");
                sb.AppendLine($"<h3>{System.Net.WebUtility.HtmlEncode(recipe.RecipeName)}</h3>");
                if (!string.IsNullOrEmpty(recipe.Notes))
                    sb.AppendLine($"<p><em>{System.Net.WebUtility.HtmlEncode(recipe.Notes)}</em></p>");
                sb.AppendLine("</div>");
            }
            sb.AppendLine("</div>");
        }

        if (cookbook.UnsectionedRecipes.Count > 0)
        {
            sb.AppendLine("<div class=\"section\"><h2>Recipes</h2>");
            foreach (var recipe in cookbook.UnsectionedRecipes)
            {
                sb.AppendLine($"<div class=\"recipe-item\"><h3>{System.Net.WebUtility.HtmlEncode(recipe.RecipeName)}</h3>");
                if (!string.IsNullOrEmpty(recipe.Notes))
                    sb.AppendLine($"<p><em>{System.Net.WebUtility.HtmlEncode(recipe.Notes)}</em></p>");
                sb.AppendLine("</div>");
            }
            sb.AppendLine("</div>");
        }

        if (!string.IsNullOrEmpty(cookbook.NotesContent))
        {
            sb.AppendLine("<hr><h2>Notes</h2>");
            sb.AppendLine($"<div>{cookbook.NotesContent}</div>");
        }

        sb.AppendLine("</body></html>");
        return sb.ToString();
    }
}
