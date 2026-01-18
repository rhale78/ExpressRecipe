# Column Name Mapping Fix - Database vs DTO Properties

**Date**: 2026-01-14
**Status**: 🔧 In Progress

## The Mapping Problem

After database migration to HighSpeedDAL naming conventions, we have:

### Database Schema (after migration 012)
```sql
- CreatedDate   (not CreatedAt)
- CreatedBy
- ModifiedDate  (not UpdatedAt)
- ModifiedBy    (not UpdatedBy)
- DeletedDate   (not DeletedAt)
- DeletedBy
- IsDeleted
```

### DTO Properties (unchanged - public API)
```csharp
public class ProductDto
{
    public DateTime CreatedAt { get; set; }  // NOT CreatedDate!
}

public class ProductImageModel
{
    public DateTime CreatedAt { get; set; }  // NOT CreatedDate!
}
```

### The Fix Pattern

**SQL SELECT (use database column names)**:
```sql
SELECT Id, Name, CreatedDate, ModifiedDate FROM Product
                ^^^^^^^^^^^  ^^^^^^^^^^^^
                database column names
```

**DTO Mapping (read from DB column, assign to DTO property)**:
```csharp
new ProductDto
{
    Id = reader.GetGuid("Id"),
    Name = reader.GetString("Name"),
    CreatedAt = reader.GetDateTime("CreatedDate"),  // ← Read from CreatedDate, assign to CreatedAt
    //^^^^^^^^^                      ^^^^^^^^^^^
    //DTO property                   DB column
}
```

## Files Requiring Mapping Fixes

All repository files that map from database reader to DTOs:

1. ProductRepository.cs - ProductDto mappings
2. ProductImageRepository.cs - ProductImageModel mappings
3. RestaurantRepository.cs - RestaurantDto, UserRestaurantRatingDto mappings
4. MenuItemRepository.cs - MenuItemDto, UserMenuItemRatingDto mappings
5. CouponRepository.cs - CouponDto mappings
6. BaseIngredientRepository.cs - BaseIngredientDto mappings
7. ProductStagingRepository.cs - StagedProduct mappings
8. ProductRepositoryAdapter.cs - ProductImageDto mappings
9. AllergenRepositoryAdapter.cs - ProductDto mappings

## DTO Properties That Need Special Handling

| DTO Type | Property | Database Column |
|----------|----------|-----------------|
| ProductDto | CreatedAt | CreatedDate |
| ProductImageDto | CreatedAt | CreatedDate |
| ProductImageModel | CreatedAt | CreatedDate |
| RestaurantDto | CreatedAt | CreatedDate |
| MenuItemDto | CreatedAt | CreatedDate |
| CouponDto | CreatedAt | CreatedDate |
| BaseIngredientDto | CreatedAt | CreatedDate |
| StagedProduct | CreatedAt | CreatedDate |
| StagedProduct | ModifiedDate | ModifiedDate |
| UserRestaurantRatingDto | CreatedAt | CreatedDate |
| UserMenuItemRatingDto | CreatedAt | CreatedDate |

## Action Items

- [ ] Fix all DTO property assignments to map from CreatedDate → CreatedAt
- [ ] Fix StagedProduct.ModifiedDate (if DTO has UpdatedAt property)
- [ ] Verify build succeeds
- [ ] Run ProductService and verify no runtime SQL errors

## Important Notes

- **DTOs are the public API** - their property names should NOT change
- **Database columns** use HighSpeedDAL conventions (CreatedDate, ModifiedDate)
- **Mapping layer** translates between the two
- SQL column names in SELECT are correct as-is
- Only the C# property assignments need fixing
