using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ExpressRecipe.ShoppingService.Controllers;
using ExpressRecipe.ShoppingService.Data;
using ExpressRecipe.ShoppingService.Tests.Helpers;

namespace ExpressRecipe.ShoppingService.Tests.Controllers;

public class ShoppingControllerTests
{
    private readonly Mock<IShoppingRepository> _mockRepository;
    private readonly Mock<ILogger<ShoppingController>> _mockLogger;
    private readonly ShoppingController _controller;
    private readonly Guid _testUserId;

    public ShoppingControllerTests()
    {
        _mockRepository = new Mock<IShoppingRepository>();
        _mockLogger = new Mock<ILogger<ShoppingController>>();
        _controller = new ShoppingController(_mockLogger.Object, _mockRepository.Object);
        _testUserId = Guid.NewGuid();
        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_testUserId);
    }

    #region GetLists Tests

    [Fact]
    public async Task GetLists_WhenAuthenticated_ReturnsUserLists()
    {
        // Arrange
        var lists = new List<ShoppingListDto>
        {
            new ShoppingListDto { Id = Guid.NewGuid(), Name = "Weekly Groceries", UserId = _testUserId },
            new ShoppingListDto { Id = Guid.NewGuid(), Name = "Party Supplies", UserId = _testUserId }
        };

        _mockRepository
            .Setup(r => r.GetUserListsAsync(_testUserId))
            .ReturnsAsync(lists);

        // Act
        var result = await _controller.GetLists();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(lists);

        _mockRepository.Verify(r => r.GetUserListsAsync(_testUserId), Times.Once);
    }

    [Fact]
    public async Task GetLists_WhenNoLists_ReturnsEmptyList()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.GetUserListsAsync(_testUserId))
            .ReturnsAsync(new List<ShoppingListDto>());

        // Act
        var result = await _controller.GetLists();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        (okResult!.Value as List<ShoppingListDto>).Should().BeEmpty();
    }

    [Fact]
    public async Task GetLists_UsesCorrectUserId()
    {
        // Arrange
        var otherUserId = Guid.NewGuid();
        _mockRepository
            .Setup(r => r.GetUserListsAsync(_testUserId))
            .ReturnsAsync(new List<ShoppingListDto>());
        _mockRepository
            .Setup(r => r.GetUserListsAsync(otherUserId))
            .ReturnsAsync(new List<ShoppingListDto> { new ShoppingListDto { Name = "Other user list" } });

        // Act
        var result = await _controller.GetLists();

        // Assert
        _mockRepository.Verify(r => r.GetUserListsAsync(_testUserId), Times.Once);
        _mockRepository.Verify(r => r.GetUserListsAsync(otherUserId), Times.Never);
    }

    #endregion

    #region CreateList Tests

    [Fact]
    public async Task CreateList_WithValidRequest_ReturnsCreatedWithList()
    {
        // Arrange
        var request = new CreateListRequest { Name = "Weekly Groceries", Description = "For the week" };
        var listId = Guid.NewGuid();
        var listDto = new ShoppingListDto { Id = listId, Name = request.Name, Description = request.Description, UserId = _testUserId };

        _mockRepository
            .Setup(r => r.CreateShoppingListAsync(_testUserId, null, request.Name, request.Description, "Standard", null))
            .ReturnsAsync(listId);
        _mockRepository
            .Setup(r => r.GetShoppingListAsync(listId, _testUserId))
            .ReturnsAsync(listDto);

        // Act
        var result = await _controller.CreateList(request);

        // Assert
        result.Should().BeOfType<CreatedAtActionResult>();
        var createdResult = result as CreatedAtActionResult;
        createdResult!.ActionName.Should().Be(nameof(ShoppingController.GetList));
        createdResult.Value.Should().BeEquivalentTo(listDto);

        _mockRepository.Verify(r => r.CreateShoppingListAsync(_testUserId, null, request.Name, request.Description, "Standard", null), Times.Once);
        _mockRepository.Verify(r => r.GetShoppingListAsync(listId, _testUserId), Times.Once);
    }

    [Fact]
    public async Task CreateList_SetsCorrectUserId()
    {
        // Arrange
        var request = new CreateListRequest { Name = "My List" };
        var listId = Guid.NewGuid();

        _mockRepository
            .Setup(r => r.CreateShoppingListAsync(_testUserId, null, request.Name, null, "Standard", null))
            .ReturnsAsync(listId);
        _mockRepository
            .Setup(r => r.GetShoppingListAsync(listId, _testUserId))
            .ReturnsAsync(new ShoppingListDto { Id = listId, UserId = _testUserId });

        // Act
        await _controller.CreateList(request);

        // Assert
        _mockRepository.Verify(r => r.CreateShoppingListAsync(_testUserId, null, request.Name, null, "Standard", null), Times.Once);
    }

    [Fact]
    public async Task CreateList_ReturnsLocationHeaderWithNewListId()
    {
        // Arrange
        var request = new CreateListRequest { Name = "Holiday Shopping" };
        var listId = Guid.NewGuid();

        _mockRepository
            .Setup(r => r.CreateShoppingListAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<Guid?>()))
            .ReturnsAsync(listId);
        _mockRepository
            .Setup(r => r.GetShoppingListAsync(listId, _testUserId))
            .ReturnsAsync(new ShoppingListDto { Id = listId });

        // Act
        var result = await _controller.CreateList(request);

        // Assert
        var createdResult = result as CreatedAtActionResult;
        createdResult!.RouteValues.Should().ContainKey("id");
        createdResult.RouteValues!["id"].Should().Be(listId);
    }

    #endregion

    #region GetList Tests

    [Fact]
    public async Task GetList_ExistingId_ReturnsList()
    {
        // Arrange
        var listId = Guid.NewGuid();
        var listDto = new ShoppingListDto { Id = listId, Name = "Groceries", UserId = _testUserId };

        _mockRepository
            .Setup(r => r.GetShoppingListAsync(listId, _testUserId))
            .ReturnsAsync(listDto);

        // Act
        var result = await _controller.GetList(listId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(listDto);
    }

    [Fact]
    public async Task GetList_NotFound_ReturnsNotFound()
    {
        // Arrange
        var listId = Guid.NewGuid();

        _mockRepository
            .Setup(r => r.GetShoppingListAsync(listId, _testUserId))
            .ReturnsAsync((ShoppingListDto?)null);

        // Act
        var result = await _controller.GetList(listId);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region UpdateList Tests

    [Fact]
    public async Task UpdateList_ValidRequest_ReturnsNoContent()
    {
        // Arrange
        var listId = Guid.NewGuid();
        var request = new UpdateListRequest { Name = "Updated Name", Description = "Updated desc" };

        // Ownership check: return a list so the user is treated as the owner
        _mockRepository
            .Setup(r => r.GetShoppingListAsync(listId, _testUserId))
            .ReturnsAsync(new ShoppingListDto { Id = listId, UserId = _testUserId, Name = "Old Name" });
        _mockRepository
            .Setup(r => r.UpdateShoppingListAsync(listId, request.Name, request.Description, null))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.UpdateList(listId, request);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task UpdateList_CallsRepositoryWithCorrectArgs()
    {
        // Arrange
        var listId = Guid.NewGuid();
        var request = new UpdateListRequest { Name = "New Name", Description = "New Description" };

        // Ownership check: return a list so the user is treated as the owner
        _mockRepository
            .Setup(r => r.GetShoppingListAsync(listId, _testUserId))
            .ReturnsAsync(new ShoppingListDto { Id = listId, UserId = _testUserId, Name = "Old Name" });
        _mockRepository
            .Setup(r => r.UpdateShoppingListAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<Guid?>()))
            .Returns(Task.CompletedTask);

        // Act
        await _controller.UpdateList(listId, request);

        // Assert
        _mockRepository.Verify(r => r.UpdateShoppingListAsync(listId, request.Name, request.Description, null), Times.Once);
    }

    [Fact]
    public async Task UpdateList_WhenListNotOwnedByUser_ReturnsNotFound()
    {
        // Arrange
        var listId = Guid.NewGuid();
        var request = new UpdateListRequest { Name = "Name", Description = null };

        // Ownership check: list not found for this user
        _mockRepository
            .Setup(r => r.GetShoppingListAsync(listId, _testUserId))
            .ReturnsAsync((ShoppingListDto?)null);

        // Act
        var result = await _controller.UpdateList(listId, request);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
        _mockRepository.Verify(r => r.UpdateShoppingListAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<Guid?>()), Times.Never);
    }

    #endregion

    #region DeleteList Tests

    [Fact]
    public async Task DeleteList_ValidRequest_ReturnsNoContent()
    {
        // Arrange
        var listId = Guid.NewGuid();

        _mockRepository
            .Setup(r => r.DeleteShoppingListAsync(listId, _testUserId))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.DeleteList(listId);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeleteList_CallsRepositoryWithCorrectArgs()
    {
        // Arrange
        var listId = Guid.NewGuid();

        _mockRepository
            .Setup(r => r.DeleteShoppingListAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);

        // Act
        await _controller.DeleteList(listId);

        // Assert
        _mockRepository.Verify(r => r.DeleteShoppingListAsync(listId, _testUserId), Times.Once);
    }

    #endregion

    #region GetListItems Tests

    [Fact]
    public async Task GetListItems_ValidList_ReturnsItems()
    {
        // Arrange
        var listId = Guid.NewGuid();
        var items = new List<ShoppingListItemDto>
        {
            new ShoppingListItemDto { Id = Guid.NewGuid(), CustomName = "Milk", Quantity = 2, Unit = "liters" },
            new ShoppingListItemDto { Id = Guid.NewGuid(), CustomName = "Bread", Quantity = 1 }
        };

        _mockRepository
            .Setup(r => r.GetListItemsAsync(listId, _testUserId))
            .ReturnsAsync(items);

        // Act
        var result = await _controller.GetListItems(listId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(items);

        _mockRepository.Verify(r => r.GetListItemsAsync(listId, _testUserId), Times.Once);
    }

    [Fact]
    public async Task GetListItems_EmptyList_ReturnsEmptyList()
    {
        // Arrange
        var listId = Guid.NewGuid();

        _mockRepository
            .Setup(r => r.GetListItemsAsync(listId, _testUserId))
            .ReturnsAsync(new List<ShoppingListItemDto>());

        // Act
        var result = await _controller.GetListItems(listId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        (okResult!.Value as List<ShoppingListItemDto>).Should().BeEmpty();
    }

    #endregion

    #region AddItem Tests

    [Fact]
    public async Task AddItem_WithProductId_ReturnsCreatedItem()
    {
        // Arrange
        var listId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var request = new AddItemRequest
        {
            ProductId = productId,
            Quantity = 2,
            Unit = "kg",
            Category = "Produce"
        };

        _mockRepository
            .Setup(r => r.AddItemToListAsync(listId, _testUserId, productId, null, 2, "kg", "Produce", false, false, null, null))
            .ReturnsAsync(itemId);

        // Act
        var result = await _controller.AddItem(listId, request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(new { id = itemId });

        _mockRepository.Verify(r => r.AddItemToListAsync(listId, _testUserId, productId, null, 2, "kg", "Produce", false, false, null, null), Times.Once);
    }

    [Fact]
    public async Task AddItem_WithCustomName_ReturnsCreatedItem()
    {
        // Arrange
        var listId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var request = new AddItemRequest
        {
            CustomName = "Organic Almond Milk",
            Quantity = 1,
            Unit = "carton",
            Category = "Dairy"
        };

        _mockRepository
            .Setup(r => r.AddItemToListAsync(listId, _testUserId, null, "Organic Almond Milk", 1, "carton", "Dairy", false, false, null, null))
            .ReturnsAsync(itemId);

        // Act
        var result = await _controller.AddItem(listId, request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(new { id = itemId });
    }

    [Fact]
    public async Task AddItem_WithAllFields_PassesAllFieldsToRepository()
    {
        // Arrange
        var listId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var request = new AddItemRequest
        {
            ProductId = productId,
            CustomName = "Premium Cheese",
            Quantity = 500,
            Unit = "g",
            Category = "Dairy"
        };

        _mockRepository
            .Setup(r => r.AddItemToListAsync(listId, _testUserId, productId, "Premium Cheese", 500, "g", "Dairy", false, false, null, null))
            .ReturnsAsync(itemId);

        // Act
        await _controller.AddItem(listId, request);

        // Assert
        _mockRepository.Verify(r => r.AddItemToListAsync(
            listId, _testUserId, productId, "Premium Cheese", 500, "g", "Dairy", false, false, null, null), Times.Once);
    }

    [Fact]
    public async Task AddItem_ReturnsCorrectItemId()
    {
        // Arrange
        var listId = Guid.NewGuid();
        var expectedItemId = Guid.NewGuid();
        var request = new AddItemRequest { CustomName = "Eggs", Quantity = 12 };

        _mockRepository
            .Setup(r => r.AddItemToListAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
                It.IsAny<decimal>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<Guid?>()))
            .ReturnsAsync(expectedItemId);

        // Act
        var result = await _controller.AddItem(listId, request);

        // Assert
        var okResult = result as OkObjectResult;
        var value = okResult!.Value;
        value.Should().BeEquivalentTo(new { id = expectedItemId });
    }

    #endregion

    #region ToggleItem Tests

    [Fact]
    public async Task ToggleItem_ExistingItem_ReturnsNoContent()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var listId = Guid.NewGuid();

        _mockRepository
            .Setup(r => r.GetShoppingListItemAsync(itemId))
            .ReturnsAsync(new ShoppingListItemDto { Id = itemId, ShoppingListId = listId });
        _mockRepository
            .Setup(r => r.GetShoppingListAsync(listId, _testUserId))
            .ReturnsAsync(new ShoppingListDto { Id = listId, UserId = _testUserId, Name = "List" });
        _mockRepository
            .Setup(r => r.ToggleItemCheckedAsync(itemId))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.ToggleChecked(itemId);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        _mockRepository.Verify(r => r.ToggleItemCheckedAsync(itemId), Times.Once);
    }

    [Fact]
    public async Task ToggleItem_CallsRepositoryWithCorrectItemId()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var otherId = Guid.NewGuid();

        _mockRepository
            .Setup(r => r.GetShoppingListItemAsync(itemId))
            .ReturnsAsync(new ShoppingListItemDto { Id = itemId, ShoppingListId = listId });
        _mockRepository
            .Setup(r => r.GetShoppingListAsync(listId, _testUserId))
            .ReturnsAsync(new ShoppingListDto { Id = listId, UserId = _testUserId, Name = "List" });
        _mockRepository
            .Setup(r => r.ToggleItemCheckedAsync(It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);

        // Act
        await _controller.ToggleChecked(itemId);

        // Assert
        _mockRepository.Verify(r => r.ToggleItemCheckedAsync(itemId), Times.Once);
        _mockRepository.Verify(r => r.ToggleItemCheckedAsync(otherId), Times.Never);
    }

    #endregion

    #region RemoveItem Tests

    [Fact]
    public async Task RemoveItem_ExistingItem_ReturnsNoContent()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var listId = Guid.NewGuid();

        _mockRepository
            .Setup(r => r.GetShoppingListItemAsync(itemId))
            .ReturnsAsync(new ShoppingListItemDto { Id = itemId, ShoppingListId = listId });
        _mockRepository
            .Setup(r => r.GetShoppingListAsync(listId, _testUserId))
            .ReturnsAsync(new ShoppingListDto { Id = listId, UserId = _testUserId, Name = "List" });
        _mockRepository
            .Setup(r => r.RemoveItemFromListAsync(itemId))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.RemoveItem(itemId);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        _mockRepository.Verify(r => r.RemoveItemFromListAsync(itemId), Times.Once);
    }

    [Fact]
    public async Task RemoveItem_CallsRepositoryWithCorrectItemId()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var listId = Guid.NewGuid();

        _mockRepository
            .Setup(r => r.GetShoppingListItemAsync(itemId))
            .ReturnsAsync(new ShoppingListItemDto { Id = itemId, ShoppingListId = listId });
        _mockRepository
            .Setup(r => r.GetShoppingListAsync(listId, _testUserId))
            .ReturnsAsync(new ShoppingListDto { Id = listId, UserId = _testUserId, Name = "List" });
        _mockRepository
            .Setup(r => r.RemoveItemFromListAsync(It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);

        // Act
        await _controller.RemoveItem(itemId);

        // Assert
        _mockRepository.Verify(r => r.RemoveItemFromListAsync(itemId), Times.Once);
    }

    #endregion

    #region UpdateItemQuantity Tests

    [Fact]
    public async Task UpdateItemQuantity_ValidRequest_ReturnsNoContent()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var request = new UpdateQuantityRequest { Quantity = 5.0m };

        _mockRepository
            .Setup(r => r.GetShoppingListItemAsync(itemId))
            .ReturnsAsync(new ShoppingListItemDto { Id = itemId, ShoppingListId = listId });
        _mockRepository
            .Setup(r => r.GetShoppingListAsync(listId, _testUserId))
            .ReturnsAsync(new ShoppingListDto { Id = listId, UserId = _testUserId, Name = "List" });
        _mockRepository
            .Setup(r => r.UpdateItemQuantityAsync(itemId, request.Quantity))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.UpdateQuantity(itemId, request);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task UpdateItemQuantity_CallsRepositoryWithCorrectItemId()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var request = new UpdateQuantityRequest { Quantity = 3.5m };

        _mockRepository
            .Setup(r => r.GetShoppingListItemAsync(itemId))
            .ReturnsAsync(new ShoppingListItemDto { Id = itemId, ShoppingListId = listId });
        _mockRepository
            .Setup(r => r.GetShoppingListAsync(listId, _testUserId))
            .ReturnsAsync(new ShoppingListDto { Id = listId, UserId = _testUserId, Name = "List" });
        _mockRepository
            .Setup(r => r.UpdateItemQuantityAsync(It.IsAny<Guid>(), It.IsAny<decimal>()))
            .Returns(Task.CompletedTask);

        // Act
        await _controller.UpdateQuantity(itemId, request);

        // Assert
        _mockRepository.Verify(r => r.UpdateItemQuantityAsync(itemId, 3.5m), Times.Once);
    }

    #endregion

    #region ShareList Tests

    [Fact]
    public async Task ShareList_WithValidRequest_ReturnsShareId()
    {
        // Arrange
        var listId = Guid.NewGuid();
        var sharedWithUserId = Guid.NewGuid();
        var shareId = Guid.NewGuid();
        var request = new ShareListRequest { SharedWithUserId = sharedWithUserId, Permission = "Edit" };

        _mockRepository
            .Setup(r => r.ShareListAsync(listId, _testUserId, sharedWithUserId, "Edit"))
            .ReturnsAsync(shareId);

        // Act
        var result = await _controller.ShareList(listId, request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(new { id = shareId });

        _mockRepository.Verify(r => r.ShareListAsync(listId, _testUserId, sharedWithUserId, "Edit"), Times.Once);
    }

    [Fact]
    public async Task ShareList_UsesCorrectOwnerUserId()
    {
        // Arrange
        var listId = Guid.NewGuid();
        var sharedWithUserId = Guid.NewGuid();
        var request = new ShareListRequest { SharedWithUserId = sharedWithUserId, Permission = "View" };

        _mockRepository
            .Setup(r => r.ShareListAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>()))
            .ReturnsAsync(Guid.NewGuid());

        // Act
        await _controller.ShareList(listId, request);

        // Assert
        _mockRepository.Verify(r => r.ShareListAsync(listId, _testUserId, sharedWithUserId, "View"), Times.Once);
    }

    #endregion

    #region GetSharedLists Tests

    [Fact]
    public async Task GetSharedLists_ReturnsSharedLists()
    {
        // Arrange
        var sharedLists = new List<ShoppingListDto>
        {
            new ShoppingListDto { Id = Guid.NewGuid(), Name = "Family Groceries" },
            new ShoppingListDto { Id = Guid.NewGuid(), Name = "Office Supplies" }
        };

        _mockRepository
            .Setup(r => r.GetSharedListsAsync(_testUserId))
            .ReturnsAsync(sharedLists);

        // Act
        var result = await _controller.GetSharedLists();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(sharedLists);

        _mockRepository.Verify(r => r.GetSharedListsAsync(_testUserId), Times.Once);
    }

    [Fact]
    public async Task GetSharedLists_WhenNoSharedLists_ReturnsEmptyList()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.GetSharedListsAsync(_testUserId))
            .ReturnsAsync(new List<ShoppingListDto>());

        // Act
        var result = await _controller.GetSharedLists();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        (okResult!.Value as List<ShoppingListDto>).Should().BeEmpty();
    }

    #endregion

    #region GetStores Tests

    [Fact]
    public async Task GetStores_WhenAuthenticated_ReturnsUserStores()
    {
        // Arrange
        var stores = new List<StoreDto>
        {
            new StoreDto { Id = Guid.NewGuid(), Name = "Whole Foods", City = "Seattle" },
            new StoreDto { Id = Guid.NewGuid(), Name = "Trader Joe's", City = "Bellevue" }
        };

        _mockRepository
            .Setup(r => r.GetUserStoresAsync(_testUserId))
            .ReturnsAsync(stores);

        // Act
        var result = await _controller.GetStores();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(stores);

        _mockRepository.Verify(r => r.GetUserStoresAsync(_testUserId), Times.Once);
    }

    [Fact]
    public async Task GetStores_WhenNoStores_ReturnsEmptyList()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.GetUserStoresAsync(_testUserId))
            .ReturnsAsync(new List<StoreDto>());

        // Act
        var result = await _controller.GetStores();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        (okResult!.Value as List<StoreDto>).Should().BeEmpty();
    }

    #endregion

    #region End-to-End Flow Tests

    [Fact]
    public async Task EndToEnd_CreateListAddItemsAndCheckItems_CompleteShoppingFlow()
    {
        // Arrange
        var listId = Guid.NewGuid();
        var item1Id = Guid.NewGuid();
        var item2Id = Guid.NewGuid();

        var createRequest = new CreateListRequest { Name = "Weekly Shop", Description = "Regular weekly shopping" };
        var listDto = new ShoppingListDto { Id = listId, Name = "Weekly Shop", UserId = _testUserId };

        var addItem1Request = new AddItemRequest { CustomName = "Milk", Quantity = 2, Unit = "liters", Category = "Dairy" };
        var addItem2Request = new AddItemRequest { CustomName = "Bread", Quantity = 1, Unit = "loaf", Category = "Bakery" };

        var items = new List<ShoppingListItemDto>
        {
            new ShoppingListItemDto { Id = item1Id, CustomName = "Milk", Quantity = 2 },
            new ShoppingListItemDto { Id = item2Id, CustomName = "Bread", Quantity = 1 }
        };

        _mockRepository
            .Setup(r => r.CreateShoppingListAsync(_testUserId, null, createRequest.Name, createRequest.Description, "Standard", null))
            .ReturnsAsync(listId);
        _mockRepository
            .Setup(r => r.GetShoppingListAsync(listId, _testUserId))
            .ReturnsAsync(listDto);
        _mockRepository
            .Setup(r => r.AddItemToListAsync(listId, _testUserId, null, "Milk", 2, "liters", "Dairy", false, false, null, null))
            .ReturnsAsync(item1Id);
        _mockRepository
            .Setup(r => r.AddItemToListAsync(listId, _testUserId, null, "Bread", 1, "loaf", "Bakery", false, false, null, null))
            .ReturnsAsync(item2Id);
        // Ownership checks for item mutations
        _mockRepository
            .Setup(r => r.GetShoppingListItemAsync(item1Id))
            .ReturnsAsync(new ShoppingListItemDto { Id = item1Id, ShoppingListId = listId });
        _mockRepository
            .Setup(r => r.GetShoppingListItemAsync(item2Id))
            .ReturnsAsync(new ShoppingListItemDto { Id = item2Id, ShoppingListId = listId });
        _mockRepository
            .Setup(r => r.ToggleItemCheckedAsync(item1Id))
            .Returns(Task.CompletedTask);
        _mockRepository
            .Setup(r => r.GetListItemsAsync(listId, _testUserId))
            .ReturnsAsync(items);
        _mockRepository
            .Setup(r => r.RemoveItemFromListAsync(item2Id))
            .Returns(Task.CompletedTask);

        // Act - Step 1: Create list
        var createResult = await _controller.CreateList(createRequest);
        createResult.Should().BeOfType<CreatedAtActionResult>();

        // Act - Step 2: Add two items
        var addItem1Result = await _controller.AddItem(listId, addItem1Request);
        addItem1Result.Should().BeOfType<OkObjectResult>();

        var addItem2Result = await _controller.AddItem(listId, addItem2Request);
        addItem2Result.Should().BeOfType<OkObjectResult>();

        // Act - Step 3: Toggle first item checked
        var toggleResult = await _controller.ToggleChecked(item1Id);
        toggleResult.Should().BeOfType<NoContentResult>();

        // Act - Step 4: Get items - verify 2 items exist
        var getItemsResult = await _controller.GetListItems(listId);
        getItemsResult.Should().BeOfType<OkObjectResult>();
        var getItemsOk = getItemsResult as OkObjectResult;
        (getItemsOk!.Value as List<ShoppingListItemDto>).Should().HaveCount(2);

        // Act - Step 5: Delete second item
        var removeResult = await _controller.RemoveItem(item2Id);
        removeResult.Should().BeOfType<NoContentResult>();

        // Assert - Verify all repository methods called with correct args
        _mockRepository.Verify(r => r.CreateShoppingListAsync(_testUserId, null, createRequest.Name, createRequest.Description, "Standard", null), Times.Once);
        // GetShoppingListAsync is called for list creation return value plus item ownership checks
        _mockRepository.Verify(r => r.GetShoppingListAsync(listId, _testUserId), Times.AtLeast(1));
        _mockRepository.Verify(r => r.AddItemToListAsync(listId, _testUserId, null, "Milk", 2, "liters", "Dairy", false, false, null, null), Times.Once);
        _mockRepository.Verify(r => r.AddItemToListAsync(listId, _testUserId, null, "Bread", 1, "loaf", "Bakery", false, false, null, null), Times.Once);
        _mockRepository.Verify(r => r.ToggleItemCheckedAsync(item1Id), Times.Once);
        _mockRepository.Verify(r => r.GetListItemsAsync(listId, _testUserId), Times.Once);
        _mockRepository.Verify(r => r.RemoveItemFromListAsync(item2Id), Times.Once);
    }

    [Fact]
    public async Task EndToEnd_CreateListShareWithAnotherUser_SharingFlow()
    {
        // Arrange
        var listId = Guid.NewGuid();
        var shareId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        var createRequest = new CreateListRequest { Name = "Family Groceries" };
        var listDto = new ShoppingListDto { Id = listId, Name = "Family Groceries", UserId = _testUserId };
        var sharedLists = new List<ShoppingListDto> { listDto };

        _mockRepository
            .Setup(r => r.CreateShoppingListAsync(_testUserId, null, createRequest.Name, null, "Standard", null))
            .ReturnsAsync(listId);
        _mockRepository
            .Setup(r => r.GetShoppingListAsync(listId, _testUserId))
            .ReturnsAsync(listDto);
        _mockRepository
            .Setup(r => r.ShareListAsync(listId, _testUserId, otherUserId, "View"))
            .ReturnsAsync(shareId);

        // Setup shared lists for the other user context
        _mockRepository
            .Setup(r => r.GetSharedListsAsync(otherUserId))
            .ReturnsAsync(sharedLists);

        // Act - Step 1: Create a list
        var createResult = await _controller.CreateList(createRequest);
        createResult.Should().BeOfType<CreatedAtActionResult>();

        // Act - Step 2: Share list with another user
        var shareRequest = new ShareListRequest { SharedWithUserId = otherUserId, Permission = "View" };
        var shareResult = await _controller.ShareList(listId, shareRequest);
        shareResult.Should().BeOfType<OkObjectResult>();
        var shareOk = shareResult as OkObjectResult;
        shareOk!.Value.Should().BeEquivalentTo(new { id = shareId });

        // Act - Step 3: Get shared lists for other user (simulate other user context)
        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(otherUserId);
        var sharedListsResult = await _controller.GetSharedLists();
        sharedListsResult.Should().BeOfType<OkObjectResult>();
        var sharedListsOk = sharedListsResult as OkObjectResult;
        (sharedListsOk!.Value as List<ShoppingListDto>).Should().HaveCount(1);

        // Assert - Verify proper repository calls
        _mockRepository.Verify(r => r.CreateShoppingListAsync(_testUserId, null, createRequest.Name, null, "Standard", null), Times.Once);
        _mockRepository.Verify(r => r.ShareListAsync(listId, _testUserId, otherUserId, "View"), Times.Once);
        _mockRepository.Verify(r => r.GetSharedListsAsync(otherUserId), Times.Once);
    }

    #endregion
}
