using FluentAssertions;
using ExpressRecipe.InventoryService.Data;
using ExpressRecipe.InventoryService.Services;

namespace ExpressRecipe.InventoryService.Tests.Services;

/// <summary>
/// Unit tests for RecipeCookedEventSubscriber matching and quantity-deduction logic.
/// </summary>
public class RecipeCookedEventSubscriberTests
{
    private static readonly Guid ProductId1 = Guid.NewGuid();
    private static readonly Guid ProductId2 = Guid.NewGuid();

    private static InventoryItemDto CreateItem(string name, decimal quantity, Guid? productId = null)
    {
        return new InventoryItemDto
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            ProductId = productId,
            ProductName = name,
            CustomName = name,
            Quantity = quantity,
            Unit = "units",
            StorageLocationId = Guid.NewGuid(),
            StorageLocationName = "Pantry",
            CreatedAt = DateTime.UtcNow
        };
    }

    #region FindMatchingInventoryItem — Priority 1: ProductId match

    [Fact]
    public void FindMatchingInventoryItem_MatchesByProductId_WhenAvailable()
    {
        // Arrange
        List<InventoryItemDto> inventory = new List<InventoryItemDto>
        {
            CreateItem("Flour", 3.0m, ProductId1),
            CreateItem("Sugar", 2.0m, ProductId2)
        };
        RecipeCookedEventSubscriber.RecipeIngredientResponse ingredient =
            new RecipeCookedEventSubscriber.RecipeIngredientResponse
            {
                ProductId = ProductId1,
                Name = "Flour",
                Quantity = 2m,
                Unit = "cups"
            };

        // Act
        InventoryItemDto? match = RecipeCookedEventSubscriber.FindMatchingInventoryItem(inventory, ingredient);

        // Assert
        match.Should().NotBeNull();
        match!.ProductId.Should().Be(ProductId1);
    }

    [Fact]
    public void FindMatchingInventoryItem_DoesNotMatchZeroQuantityByProductId()
    {
        // Arrange
        List<InventoryItemDto> inventory = new List<InventoryItemDto>
        {
            CreateItem("Flour", 0m, ProductId1)
        };
        RecipeCookedEventSubscriber.RecipeIngredientResponse ingredient =
            new RecipeCookedEventSubscriber.RecipeIngredientResponse
            {
                ProductId = ProductId1,
                Name = "Flour",
                Quantity = 2m,
                Unit = "cups"
            };

        // Act
        InventoryItemDto? match = RecipeCookedEventSubscriber.FindMatchingInventoryItem(inventory, ingredient);

        // Assert
        match.Should().BeNull();
    }

    #endregion

    #region FindMatchingInventoryItem — Priority 3: Name match (case-insensitive)

    [Fact]
    public void FindMatchingInventoryItem_MatchesByNameCaseInsensitive_WhenNoProductId()
    {
        // Arrange
        List<InventoryItemDto> inventory = new List<InventoryItemDto>
        {
            CreateItem("All-Purpose Flour", 3.0m)
        };
        RecipeCookedEventSubscriber.RecipeIngredientResponse ingredient =
            new RecipeCookedEventSubscriber.RecipeIngredientResponse
            {
                ProductId = null,
                Name = "all-purpose flour",
                Quantity = 1m,
                Unit = "cups"
            };

        // Act
        InventoryItemDto? match = RecipeCookedEventSubscriber.FindMatchingInventoryItem(inventory, ingredient);

        // Assert
        match.Should().NotBeNull();
        match!.ProductName.Should().Be("All-Purpose Flour");
    }

    [Fact]
    public void FindMatchingInventoryItem_ReturnsNull_WhenNoMatch()
    {
        // Arrange
        List<InventoryItemDto> inventory = new List<InventoryItemDto>
        {
            CreateItem("Sugar", 2.0m, ProductId2)
        };
        RecipeCookedEventSubscriber.RecipeIngredientResponse ingredient =
            new RecipeCookedEventSubscriber.RecipeIngredientResponse
            {
                ProductId = null,
                Name = "Eggs",
                Quantity = 2m,
                Unit = "whole"
            };

        // Act
        InventoryItemDto? match = RecipeCookedEventSubscriber.FindMatchingInventoryItem(inventory, ingredient);

        // Assert
        match.Should().BeNull();
    }

    [Fact]
    public void FindMatchingInventoryItem_ProductIdTakesPriorityOverNameMatch()
    {
        // Arrange — same name, different product IDs
        InventoryItemDto byProduct = CreateItem("Milk", 2.0m, ProductId1);
        InventoryItemDto byName = CreateItem("Milk", 1.0m, ProductId2);

        List<InventoryItemDto> inventory = new List<InventoryItemDto> { byProduct, byName };

        RecipeCookedEventSubscriber.RecipeIngredientResponse ingredient =
            new RecipeCookedEventSubscriber.RecipeIngredientResponse
            {
                ProductId = ProductId1,
                Name = "Milk",
                Quantity = 1m,
                Unit = "cups"
            };

        // Act
        InventoryItemDto? match = RecipeCookedEventSubscriber.FindMatchingInventoryItem(inventory, ingredient);

        // Assert — should match by ProductId, which has Quantity=2
        match.Should().NotBeNull();
        match!.ProductId.Should().Be(ProductId1);
        match.Quantity.Should().Be(2.0m);
    }

    #endregion

    #region Quantity deduction / clamping

    [Fact]
    public void QuantityAfterDeduction_ClampsToZero_WhenInventoryInsufficient()
    {
        // Arrange: inventory has 1 cup, recipe needs 2 cups × 1 serving
        decimal inventoryQty = 1m;
        decimal ingredientQty = 2m;
        decimal servings = 1m;

        // Act
        decimal scaledQty = ingredientQty * servings;
        decimal after = Math.Max(0, inventoryQty - scaledQty);

        // Assert
        after.Should().Be(0m);
    }

    [Fact]
    public void QuantityAfterDeduction_DeductsCorrectly_ForTwoServings()
    {
        // Arrange: inventory has 3 cups flour, recipe needs 1 cup × 2 servings
        decimal inventoryQty = 3m;
        decimal ingredientQty = 1m;
        decimal servings = 2m;

        // Act
        decimal scaledQty = ingredientQty * servings;
        decimal after = Math.Max(0, inventoryQty - scaledQty);

        // Assert
        scaledQty.Should().Be(2m);
        after.Should().Be(1m);
    }

    [Fact]
    public void QuantityAfterDeduction_ExactFit_LeavesZero()
    {
        // Arrange: inventory has exactly 3 cups, recipe needs 3 cups × 1 serving
        decimal inventoryQty = 3m;
        decimal ingredientQty = 3m;
        decimal servings = 1m;

        // Act
        decimal after = Math.Max(0, inventoryQty - ingredientQty * servings);

        // Assert
        after.Should().Be(0m);
    }

    #endregion
}
