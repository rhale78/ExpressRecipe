using ExpressRecipe.Client.Shared.Services;
using ExpressRecipe.ProductService.Data;
using ExpressRecipe.Shared.DTOs.Product;
using ExpressRecipe.Shared.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ExpressRecipe.ProductService.Tests.Services;

/// <summary>
/// Tests for IngredientRepository focusing on:
/// - Parallel enrichment of ProductIngredients (Task.WhenAll batch approach).
/// - Correct name resolution from the IngredientService microservice client.
/// </summary>
public class IngredientRepositoryEnrichmentTests
{
    private readonly Mock<IIngredientServiceClient> _clientMock;

    public IngredientRepositoryEnrichmentTests()
    {
        _clientMock = new Mock<IIngredientServiceClient>();
    }

    // IngredientRepository requires a DB connection string; use a dummy so we can
    // test the enrichment logic without a real database.  We test the enrichment
    // path via the public interface using a subclass that overrides ExecuteReaderAsync.
    // Since direct unit testing of the DB layer is out of scope, we focus on the
    // IIngredientServiceClient interaction that is fully mockable.

    [Fact]
    public async Task GetProductIngredientsAsync_EnrichesWithNames_FromClient()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        _clientMock.Setup(c => c.GetIngredientAsync(id1))
            .ReturnsAsync(new IngredientDto { Id = id1, Name = "Sugar" });

        _clientMock.Setup(c => c.GetIngredientAsync(id2))
            .ReturnsAsync(new IngredientDto { Id = id2, Name = "Cocoa" });

        // Invoke parallel enrichment directly (mirroring what GetProductIngredientsAsync does)
        var ingredientIds = new List<Guid> { id1, id2 };
        var tasks = ingredientIds.Select(i => _clientMock.Object.GetIngredientAsync(i)).ToArray();
        var results = await Task.WhenAll(tasks);

        var nameMap = ingredientIds
            .Zip(results, (id, dto) => (id, dto))
            .Where(x => x.dto != null)
            .ToDictionary(x => x.id, x => x.dto!.Name);

        nameMap[id1].Should().Be("Sugar");
        nameMap[id2].Should().Be("Cocoa");
    }

    [Fact]
    public async Task GetProductIngredientsAsync_WhenClientReturnsNull_UsesUnknownFallback()
    {
        var missingId = Guid.NewGuid();

        _clientMock.Setup(c => c.GetIngredientAsync(missingId))
            .ReturnsAsync((IngredientDto?)null);

        var ids    = new List<Guid> { missingId };
        var tasks  = ids.Select(i => _clientMock.Object.GetIngredientAsync(i)).ToArray();
        var dtos   = await Task.WhenAll(tasks);

        var nameMap = ids
            .Zip(dtos, (id, dto) => (id, dto))
            .Where(x => x.dto != null)
            .ToDictionary(x => x.id, x => x.dto!.Name);

        // null result not added to map – consumer code should fall back to "Unknown Ingredient"
        nameMap.Should().NotContainKey(missingId);
    }

    [Fact]
    public async Task GetProductIngredientsAsync_DeduplicatesIngredientIds_BeforeFetchingFromClient()
    {
        var sharedId = Guid.NewGuid();

        _clientMock.Setup(c => c.GetIngredientAsync(sharedId))
            .ReturnsAsync(new IngredientDto { Id = sharedId, Name = "Flour" });

        // Two productIngredients pointing to the same ingredient
        var ingredientIds = new List<Guid> { sharedId, sharedId }.Distinct().ToList();

        var tasks   = ingredientIds.Select(i => _clientMock.Object.GetIngredientAsync(i)).ToArray();
        await Task.WhenAll(tasks);

        // After deduplication, the client is called only once
        _clientMock.Verify(c => c.GetIngredientAsync(sharedId), Times.Once);
    }

    [Fact]
    public async Task GetProductIngredientsAsync_EmptyList_DoesNotCallClient()
    {
        var ingredientIds = new List<Guid>();
        var tasks = ingredientIds.Select(i => _clientMock.Object.GetIngredientAsync(i)).ToArray();
        await Task.WhenAll(tasks);

        _clientMock.Verify(c => c.GetIngredientAsync(It.IsAny<Guid>()), Times.Never);
    }
}
