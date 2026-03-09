using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ExpressRecipe.ShoppingService.Data;
using ExpressRecipe.ShoppingService.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ExpressRecipe.ShoppingService.Controllers;

[Authorize]
[ApiController]
[Route("api/shopping")]
public class PrintController : ControllerBase
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ILogger<PrintController> _logger;
    private readonly IShoppingRepository _repository;

    static PrintController()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public PrintController(ILogger<PrintController> logger, IShoppingRepository repository)
    {
        _logger = logger;
        _repository = repository;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Print shopping list as PDF or HTML.
    /// If storeId provided → single-store view; otherwise → multi-page per store from latest optimization.
    /// </summary>
    [HttpGet("{listId}/print")]
    public async Task<IActionResult> PrintList(
        Guid listId,
        [FromQuery] string format = "pdf",
        [FromQuery] Guid? storeId = null)
    {
        Guid userId = GetUserId();
        ShoppingListDto? list = await _repository.GetShoppingListAsync(listId, userId);
        if (list == null)
        {
            return NotFound();
        }

        // Try to load optimization result; fall back to raw items
        ShoppingListOptimizationDto? optimization = await _repository.GetOptimizationResultAsync(listId);
        _logger.LogPrintRequest(userId, listId, format, optimization != null);
        List<StoreShoppingGroup> storeGroups;

        if (optimization != null)
        {
            OptimizedShoppingPlan? plan = JsonSerializer.Deserialize<OptimizedShoppingPlan>(
                optimization.ResultJson, _jsonOptions);
            storeGroups = plan?.StoreGroups ?? new List<StoreShoppingGroup>();
        }
        else
        {
            // Fall back to raw items in a single unnamed group
            List<ShoppingListItemDto> rawItems = await _repository.GetListItemsAsync(listId, userId);
            storeGroups = new List<StoreShoppingGroup>
            {
                new()
                {
                    StoreId = Guid.Empty,
                    StoreName = list.StoreName ?? "Shopping List",
                    Items = rawItems.Select(i => new OptimizedShoppingItem
                    {
                        ShoppingListItemId = i.Id,
                        Name = i.CustomName ?? i.ProductName ?? string.Empty,
                        Quantity = i.Quantity,
                        Unit = i.Unit,
                        Aisle = i.Aisle,
                        AisleOrder = i.OrderIndex,
                        Price = i.EstimatedPrice,
                        HasDeal = i.HasDeal,
                        DealDescription = i.DealDescription
                    }).OrderBy(i => i.AisleOrder).ToList()
                }
            };
        }

        // Filter by storeId when provided
        if (storeId.HasValue)
        {
            storeGroups = storeGroups.Where(g => g.StoreId == storeId.Value).ToList();
        }

        if (format.Equals("html", StringComparison.OrdinalIgnoreCase))
        {
            string html = GenerateHtml(list.Name, storeGroups);
            _logger.LogPrintComplete(userId, listId, format, storeGroups.Count);
            return Content(html, "text/html");
        }

        byte[] pdf = GeneratePdf(list.Name, storeGroups);
        _logger.LogPrintComplete(userId, listId, format, storeGroups.Count);
        return File(pdf, "application/pdf", "ShoppingList.pdf");
    }

    // ── PDF generation ────────────────────────────────────────────────────────

    private static byte[] GeneratePdf(string listName, List<StoreShoppingGroup> storeGroups)
    {
        return Document.Create(container =>
        {
            if (storeGroups.Count == 0)
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.Content().AlignCenter().AlignMiddle()
                        .Text("No items").FontSize(18).FontColor(Colors.Grey.Medium);
                });
                return;
            }

            foreach (StoreShoppingGroup group in storeGroups)
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);

                    page.Header().Column(col =>
                    {
                        col.Item().Text(listName).FontSize(20).Bold().FontColor(Colors.Green.Darken2);
                        col.Item().Text(group.StoreName).FontSize(14).FontColor(Colors.Grey.Darken2);
                        if (!string.IsNullOrEmpty(group.StoreAddress))
                        {
                            col.Item().Text(group.StoreAddress).FontSize(11).FontColor(Colors.Grey.Medium);
                        }
                    });

                    page.Content().PaddingTop(12).Table(table =>
                    {
                        table.ColumnsDefinition(cd =>
                        {
                            cd.ConstantColumn(20); // checkbox
                            cd.RelativeColumn(4);  // name
                            cd.RelativeColumn(1);  // qty
                            cd.RelativeColumn(1);  // price
                            cd.RelativeColumn(1);  // aisle
                        });

                        // Header row
                        table.Header(header =>
                        {
                            header.Cell().Text("").FontSize(10);
                            header.Cell().Text("Item").FontSize(10).Bold();
                            header.Cell().Text("Qty").FontSize(10).Bold();
                            header.Cell().Text("Price").FontSize(10).Bold();
                            header.Cell().Text("Aisle").FontSize(10).Bold();
                        });

                        foreach (OptimizedShoppingItem item in group.Items)
                        {
                            table.Cell().Text("\u2610").FontSize(12); // empty checkbox
                            string nameText = item.Name;
                            if (item.HasDeal && !string.IsNullOrEmpty(item.DealDescription))
                            {
                                nameText += $" [DEAL: {item.DealDescription}]";
                            }
                            table.Cell().Text(nameText).FontSize(11);
                            table.Cell().Text($"{item.Quantity} {item.Unit}".Trim()).FontSize(11);
                            table.Cell().Text(item.Price.HasValue ? $"${item.Price:F2}" : "").FontSize(11);
                            table.Cell().Text(item.Aisle ?? "").FontSize(11);
                        }
                    });

                    page.Footer().AlignRight().Text($"Subtotal: ${group.SubTotal:F2}").FontSize(11).Bold();
                });
            }
        }).GeneratePdf();
    }

    // ── HTML generation ───────────────────────────────────────────────────────

    private static string GenerateHtml(string listName, List<StoreShoppingGroup> storeGroups)
    {
        StringBuilder sb = new();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset='UTF-8'>");
        sb.AppendLine("<style>body{font-family:Arial,sans-serif;margin:20px}");
        sb.AppendLine("h1{color:#2e7d32}h2{color:#555}");
        sb.AppendLine("table{border-collapse:collapse;width:100%}");
        sb.AppendLine("th,td{border:1px solid #ddd;padding:8px;text-align:left}");
        sb.AppendLine("th{background:#f5f5f5}.deal{color:#e65100;font-size:0.85em}");
        sb.AppendLine(".subtotal{text-align:right;font-weight:bold;margin-top:8px}");
        sb.AppendLine("</style></head><body>");
        sb.AppendLine($"<h1>{System.Net.WebUtility.HtmlEncode(listName)}</h1>");

        foreach (StoreShoppingGroup group in storeGroups)
        {
            sb.AppendLine($"<h2>{System.Net.WebUtility.HtmlEncode(group.StoreName)}</h2>");
            if (!string.IsNullOrEmpty(group.StoreAddress))
            {
                sb.AppendLine($"<p>{System.Net.WebUtility.HtmlEncode(group.StoreAddress)}</p>");
            }
            sb.AppendLine("<table><thead><tr><th></th><th>Item</th><th>Qty</th><th>Price</th><th>Aisle</th></tr></thead><tbody>");
            foreach (OptimizedShoppingItem item in group.Items)
            {
                sb.AppendLine("<tr>");
                sb.AppendLine("<td>\u2610</td>"); // empty checkbox
                string deal = item.HasDeal && !string.IsNullOrEmpty(item.DealDescription)
                    ? $" <span class='deal'>[DEAL: {System.Net.WebUtility.HtmlEncode(item.DealDescription)}]</span>"
                    : string.Empty;
                sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(item.Name)}{deal}</td>");
                sb.AppendLine($"<td>{item.Quantity} {System.Net.WebUtility.HtmlEncode(item.Unit ?? string.Empty)}</td>");
                sb.AppendLine($"<td>{(item.Price.HasValue ? $"${item.Price:F2}" : string.Empty)}</td>");
                sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(item.Aisle ?? string.Empty)}</td>");
                sb.AppendLine("</tr>");
            }
            sb.AppendLine("</tbody></table>");
            sb.AppendLine($"<p class='subtotal'>Subtotal: ${group.SubTotal:F2}</p>");
        }

        sb.AppendLine("</body></html>");
        return sb.ToString();
    }
}
