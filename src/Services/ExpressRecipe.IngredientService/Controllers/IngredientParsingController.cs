using ExpressRecipe.IngredientService.Services.Parsing;
using ExpressRecipe.Shared.DTOs.Product;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpressRecipe.IngredientService.Controllers;

[ApiController]
[Route("api/ingredient/parse")]
[AllowAnonymous] // Allow internal service calls
public class IngredientParsingController : ControllerBase
{
    private readonly IIngredientListParser _listParser;
    private readonly IIngredientParser _ingredientParser;

    public IngredientParsingController(
        IIngredientListParser listParser,
        IIngredientParser ingredientParser)
    {
        _listParser = listParser;
        _ingredientParser = ingredientParser;
    }

    [HttpPost("list")]
    public ActionResult<List<string>> ParseList([FromBody] string text)
    {
        return Ok(_listParser.ParseIngredients(text));
    }

    [HttpPost("list/bulk")]
    public ActionResult<Dictionary<string, List<string>>> BulkParseList([FromBody] List<string> texts)
    {
        return Ok(_listParser.BulkParseIngredients(texts));
    }

    [HttpPost("string")]
    public async Task<ActionResult<ParsedIngredientResult>> ParseString([FromBody] string text)
    {
        return Ok(await _ingredientParser.ParseIngredientStringAsync(text));
    }

    [HttpPost("string/bulk")]
    public async Task<ActionResult<Dictionary<string, ParsedIngredientResult>>> BulkParseStrings([FromBody] List<string> texts)
    {
        return Ok(await _ingredientParser.BulkParseIngredientStringsAsync(texts));
    }

    [HttpGet("validate")]
    public ActionResult<IngredientValidationResult> Validate([FromQuery] string ingredient)
    {
        return Ok(_listParser.ValidateIngredient(ingredient));
    }
}
