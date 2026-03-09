using ExpressRecipe.Shared.DTOs.Recipe;

namespace ExpressRecipe.RecipeService.Services;

public interface IRecipePrintService
{
    byte[] GeneratePdf(RecipeDto recipe);
    string GenerateHtml(RecipeDto recipe);
}
