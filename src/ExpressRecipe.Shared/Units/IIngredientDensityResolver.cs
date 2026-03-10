namespace ExpressRecipe.Shared.Units;
public interface IIngredientDensityResolver
{
    Task<decimal?> GetDensityAsync(Guid? ingredientId, string? ingredientName, CancellationToken ct = default);
}
public sealed record IngredientDensity(Guid IngredientId, string PreparationNote, decimal GramsPerMl, string Source);
