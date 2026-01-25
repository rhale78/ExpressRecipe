# ProductService HighSpeedDAL Audit

## Overview
This document details areas in the `ProductService` microservice that violate established `HighSpeedDAL` patterns or use the framework inefficiently.

## 1. Major Violations in `ProductSearchAdapter.cs`

### A. "Fighting the Framework" with `InternalDalBase`
**Violation:** The adapter defines a private inner class `InternalDalBase` that inherits from `SqlServerDalBase` solely to expose protected methods (`ExecuteQueryAsync`, `ExecuteScalarAsync`) as public.
**Why it's a problem:** This bypasses the intended encapsulation of the DAL framework. It suggests that the standard `ProductEntityDal` is not being extended properly or that the framework lacks a supported way to execute custom queries.
**Location:** `src/Services/ExpressRecipe.ProductService/Data/ProductSearchAdapter.cs` (lines 142-164)

### B. Ignoring `[NamedQuery]` Attributes
**Violation:** The `GetByBarcodeAsync` method manually constructs and executes a raw SQL query (`SELECT * FROM Product WHERE Barcode = ...`).
**Why it's a problem:** `ProductEntity` is decorated with `[NamedQuery("ByBarcode", nameof(Barcode), IsSingle = true)]`. The generated `ProductEntityDal` should contain an optimized method for this (e.g., `GetByBarcodeAsync`). Re-implementing it manually:
1.  Misses potential caching strategies defined on the entity.
2.  Duplicates logic.
3.  Increases maintenance burden.
**Location:** `src/Services/ExpressRecipe.ProductService/Data/ProductSearchAdapter.cs` (lines 37-43)

## 2. Inefficiencies in `ProductRepositoryAdapter.cs`

### A. Suboptimal Bulk Read Operations
**Violation:** Methods like `GetExistingBarcodesAsync` and `GetProductIdsByBarcodesAsync` delegate to `ProductSearchAdapter`, which constructs SQL with large `IN (...)` clauses and manually generated parameters (`@b0`, `@b1`...).
**Correction:** 
1.  If `ProductEntity` is an `[InMemoryTable]`, these lookups should be performed against the in-memory cache for microsecond performance, completely avoiding the database roundtrip.
2.  If not in-memory, `HighSpeedDAL` usually provides optimized batch read capabilities that should be preferred over manual `IN` clauses.
**Location:** `src/Services/ExpressRecipe.ProductService/Data/ProductRepositoryAdapter.cs` (lines 430-474)

### B. Manual Object Mapping
**Observation:** `MapEntityToDto` manually maps entity properties to DTOs.
**Improvement:** While not a strict violation, this boilerplate could be reduced. If `HighSpeedDAL` or the project conventions support a mapper (like AutoMapper or Mapster), it should be used to reduce human error and code volume.

## 3. Legacy Code Confusion

**Observation:** The project contains multiple "Repository" implementations that appear to be dead code or legacy versions:
*   `src/Services/ExpressRecipe.ProductService/Data/ProductRepository.cs` (Manual SQL implementation)
*   `src/Services/ExpressRecipe.ProductService/Data/HighSpeedProductRepository.cs` (Manual BulkCopy implementation)

**Why it's a problem:** `Program.cs` registers `ProductRepositoryAdapter` as the active `IProductRepository`. The presence of these other files creates confusion for developers about which pattern to follow. `HighSpeedProductRepository` specifically contains manual `SqlBulkCopy` logic that conflicts with `HighSpeedDAL`'s built-in `BulkInsertAsync`.

## 4. Framework Setup / Usage Issues

### A. Custom Query Support
The existence of the `InternalDalBase` hack suggests that developers found it difficult to add custom complex queries (like Search) to the generated `ProductEntityDal`.
**Recommendation:** Instead of a separate Adapter with a hacked DAL, the project should likely use `partial class ProductEntityDal` to add these custom query methods. This would allow access to the protected `ExecuteQueryAsync` methods legitimately within the DAL class itself.

### B. Missing `InMemoryTable` Leverage
`ProductEntity` is marked with `[InMemoryTable]`. `ProductRepositoryAdapter` should check if the DAL supports direct memory access for single-record lookups (e.g., `GetById`) before even calling the async `GetByIdAsync` method, or trust that the generated DAL handles this check efficiently (which it seems to do). However, the "Search" adapter completely ignores the in-memory table, meaning all searches hit the database even if the data is sitting in memory.
