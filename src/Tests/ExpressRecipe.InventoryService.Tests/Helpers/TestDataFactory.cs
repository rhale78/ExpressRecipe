using ExpressRecipe.InventoryService.Data;
using ExpressRecipe.InventoryService.Controllers;

namespace ExpressRecipe.InventoryService.Tests.Helpers;

/// <summary>
/// Factory class for generating test data
/// </summary>
public static class TestDataFactory
{
    public static HouseholdDto CreateHouseholdDto(
        Guid? id = null,
        string name = "Test Household",
        int memberCount = 1,
        int addressCount = 0)
    {
        return new HouseholdDto
        {
            Id = id ?? Guid.NewGuid(),
            Name = name,
            Description = "Test household description",
            CreatedBy = Guid.NewGuid(),
            MemberCount = memberCount,
            AddressCount = addressCount,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static AddressDto CreateAddressDto(
        Guid? id = null,
        Guid? householdId = null,
        string name = "Home",
        string street = "123 Main St")
    {
        return new AddressDto
        {
            Id = id ?? Guid.NewGuid(),
            HouseholdId = householdId ?? Guid.NewGuid(),
            Name = name,
            Street = street,
            City = "Test City",
            State = "TS",
            ZipCode = "12345",
            Country = "Test Country",
            Latitude = 40.7128m,
            Longitude = -74.0060m,
            IsPrimary = true,
            CreatedAt = DateTime.UtcNow,
            StorageLocationCount = 0,
            ItemCount = 0
        };
    }

    public static StorageLocationDto CreateStorageLocationDto(
        Guid? id = null,
        Guid? addressId = null,
        string name = "Kitchen Pantry")
    {
        return new StorageLocationDto
        {
            Id = id ?? Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            AddressId = addressId,
            AddressName = "Main House",
            Name = name,
            Description = "Test storage location",
            Temperature = "Room",
            IsDefault = false,
            CreatedAt = DateTime.UtcNow,
            ItemCount = 0
        };
    }

    public static InventoryItemDto CreateInventoryItemDto(
        Guid? id = null,
        string name = "Test Product",
        decimal quantity = 1.0m,
        DateTime? expirationDate = null)
    {
        return new InventoryItemDto
        {
            Id = id ?? Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            ProductName = name,
            CustomName = name,
            Quantity = quantity,
            Unit = "units",
            ExpirationDate = expirationDate,
            ProductId = Guid.NewGuid(),
            StorageLocationId = Guid.NewGuid(),
            StorageLocationName = "Pantry",
            CreatedAt = DateTime.UtcNow
        };
    }

    public static ScanSessionDto CreateScanSessionDto(
        Guid? id = null,
        string sessionType = "Adding",
        int itemsScanned = 0)
    {
        return new ScanSessionDto
        {
            Id = id ?? Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            SessionType = sessionType,
            ItemsScanned = itemsScanned,
            StartedAt = DateTime.UtcNow,
            EndedAt = null,
            IsActive = true
        };
    }

    public static AllergenDiscoveryDto CreateAllergenDiscoveryDto(
        Guid? id = null,
        string allergenName = "Peanuts",
        string severity = "High")
    {
        return new AllergenDiscoveryDto
        {
            Id = id ?? Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            InventoryHistoryId = Guid.NewGuid(),
            AllergenName = allergenName,
            Severity = severity,
            DiscoveredAt = DateTime.UtcNow,
            AddedToProfile = false,
            ProductName = "Test Product"
        };
    }

    public static CreateHouseholdRequest CreateHouseholdRequest(
        string name = "Test Household",
        string? description = "Test description")
    {
        return new CreateHouseholdRequest
        {
            Name = name,
            Description = description
        };
    }

    public static CreateAddressRequest CreateAddressRequest(
        string name = "Home",
        string street = "123 Main St")
    {
        return new CreateAddressRequest
        {
            Name = name,
            Street = street,
            City = "Test City",
            State = "TS",
            ZipCode = "12345",
            Country = "Test Country",
            Latitude = 40.7128m,
            Longitude = -74.0060m,
            IsPrimary = true
        };
    }

    public static AddMemberRequest CreateAddMemberRequest(
        Guid? userId = null,
        string role = "Member")
    {
        return new AddMemberRequest
        {
            UserId = userId ?? Guid.NewGuid(),
            Role = role
        };
    }

    public static StartScanSessionRequest CreateStartScanSessionRequest(
        string sessionType = "Adding",
        Guid? storageLocationId = null)
    {
        return new StartScanSessionRequest
        {
            SessionType = sessionType,
            StorageLocationId = storageLocationId
        };
    }
}
