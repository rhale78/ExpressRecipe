using ExpressRecipe.RecipeService.Data;
using ExpressRecipe.Shared.CQRS;
using ExpressRecipe.Shared.Services;

namespace ExpressRecipe.RecipeService.CQRS.Commands;

/// <summary>
/// Handler for creating recipes
/// </summary>
public class CreateRecipeCommandHandler : ICommandHandler<CreateRecipeCommand, Guid>
{
    private readonly IRecipeRepository _repository;
    private readonly EventPublisher _eventPublisher;
    private readonly ILogger<CreateRecipeCommandHandler> _logger;

    public CreateRecipeCommandHandler(
        IRecipeRepository repository,
        EventPublisher eventPublisher,
        ILogger<CreateRecipeCommandHandler> logger)
    {
        _repository = repository;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async Task<Guid> HandleAsync(CreateRecipeCommand command, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating recipe '{RecipeName}' for user {UserId}", command.Name, command.UserId);

        // Validate command
        if (string.IsNullOrWhiteSpace(command.Name))
            throw new ArgumentException("Recipe name is required", nameof(command.Name));

        if (command.Servings <= 0)
            throw new ArgumentException("Servings must be greater than 0", nameof(command.Servings));

        if (command.Ingredients.Count == 0)
            throw new ArgumentException("At least one ingredient is required", nameof(command.Ingredients));

        if (command.Instructions.Count == 0)
            throw new ArgumentException("At least one instruction step is required", nameof(command.Instructions));

        // Calculate total time
        var totalTime = (command.PrepTimeMinutes ?? 0) + (command.CookTimeMinutes ?? 0);

        // Create recipe
        var recipeId = await _repository.CreateRecipeAsync(
            command.UserId,
            command.Name,
            command.Description,
            command.PrepTimeMinutes,
            command.CookTimeMinutes,
            totalTime > 0 ? totalTime : null,
            command.Servings,
            command.Difficulty
        );

        // Add categories
        foreach (var category in command.Categories)
        {
            await _repository.AddRecipeCategoryAsync(recipeId, category);
        }

        // Add tags
        foreach (var tag in command.Tags)
        {
            await _repository.AddRecipeTagAsync(recipeId, tag);
        }

        // Add ingredients
        foreach (var ingredient in command.Ingredients)
        {
            await _repository.AddIngredientAsync(
                recipeId,
                ingredient.ProductId,
                ingredient.Name,
                ingredient.Quantity,
                ingredient.Unit,
                ingredient.Notes,
                ingredient.IsOptional
            );
        }

        // Add instructions
        foreach (var instruction in command.Instructions)
        {
            await _repository.AddInstructionAsync(
                recipeId,
                instruction.StepNumber,
                instruction.Instruction,
                instruction.TimeMinutes
            );
        }

        // Add nutrition if provided
        if (command.Nutrition != null)
        {
            await _repository.UpdateNutritionAsync(
                recipeId,
                command.Nutrition.Calories,
                command.Nutrition.Protein,
                command.Nutrition.Carbs,
                command.Nutrition.Fat,
                command.Nutrition.Fiber,
                command.Nutrition.Sugar
            );
        }

        // Publish event
        await _eventPublisher.PublishAsync("recipe.created", new
        {
            RecipeId = recipeId,
            UserId = command.UserId,
            Name = command.Name
        });

        _logger.LogInformation("Recipe '{RecipeName}' created with ID {RecipeId}", command.Name, recipeId);

        return recipeId;
    }
}
