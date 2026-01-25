# HighSpeedDAL Refactor Plan: ProductService

## 1. Objectives
Refactor `ProductService` to strictly adhere to `HighSpeedDAL` best practices, maximizing performance through correct use of `InMemoryTable`, `StagingTable`, and Source Generation, while eliminating manual SQL.

## 2. The "Perfect" Pattern

### A. Entity Configuration
The `ProductEntity` will be the "Gold Standard" implementation.

```csharp
[DalEntity]
[Table("Product", PrimaryKeyType = PrimaryKeyType.Guid)]
[Cache(CacheStrategy.TwoLayer, MaxSize = 50000, ExpirationSeconds = 3600)] // L2 (Redis) + L1 (Short term)
[InMemoryTable(
    FlushIntervalSeconds = 30,
    MaxRowCount = 1000000,
    EnforceConstraints = true,
    ValidateOnWrite = true,
    TrackOperations = true)]
[StagingTable(
    SyncIntervalSeconds = 60,
    UseMerge = true)] // Bulk Merge from InMemory -> Staging -> Primary
[AutoAudit(
    CreatedBy = "CreatedBy",
    CreatedDate = "CreatedDate",
    ModifiedBy = "ModifiedBy",
    ModifiedDate = "ModifiedDate")]
[SoftDelete(
    Column = "IsDeleted",
    DateColumn = "DeletedAt",
    UserColumn = "DeletedBy")]
[NamedQuery("ByBarcode", nameof(Barcode), IsSingle = true)]
[NamedQuery("ByCategory", nameof(Category))]
public partial class ProductEntity
{
    // ... Properties
}
```

### B. Data Flow Architecture
1.  **Reads:** 100% served from `InMemoryTable`.
    *   *Latency:* Microsecond.
    *   *Logic:* generated DAL checks memory -> returns result. No SQL.
2.  **Writes:** 100% written to `InMemoryTable` first.
    *   *Latency:* Microsecond (memory update).
    *   *Durability:*
        *   Immediate: Memory.
        *   Async (30s): Flushed to `StagingTable` (SQL).
        *   Async (60s): Merged from `StagingTable` to `Primary` (SQL).
3.  **Startup / Sync:**
    *   On boot, `InMemoryTable` hydrates from `StagingTable`.
    *   If `StagingTable` is empty/stale, it pulls from `Primary`.
4.  **Bulk Operations:**
    *   Use `BulkInsertAsync` / `BulkUpdateAsync` on the DAL.
    *   These flow through the same pipeline (Memory -> Staging -> Primary).

### C. No Manual SQL
*   **Repo Adapters:** Will be thin wrappers around the generated DAL.
*   **Search:** Complex search (e.g. wildcards, multiple filters) that cannot be handled by simple `InMemoryTable` LINQ lookups will be the *only* exception, but should be optimized to query the *In-Memory* collection directly using LINQ if possible, falling back to SQL only for complex full-text scenarios not fit for memory.

## 3. Implementation Steps

### Step 1: Update Entity Definitions
*   Modify `ProductEntity.cs` to include all attributes (`[StagingTable]`, `[AutoAudit]`, etc.).
*   Ensure `IngredientEntity.cs` follows the same pattern if used in high-volume paths.

### Step 2: Fix `ProductRepositoryAdapter`
*   **Remove** manual SQL strings.
*   **Remove** `InternalDalBase` hack.
*   **Refactor `GetByBarcodeAsync`** to use `_dal.GetByBarcodeAsync()` (generated).
*   **Refactor `SearchAsync`**:
    *   Attempt to query `_dal.MemoryTable.Where(...)` first.
    *   If query is too complex, standard generated `_dal.ExecuteQueryAsync` is preferred over manual parsing.

### Step 3: Verify Bulk Processing
*   `BatchProductProcessor` is already using `BulkCreateAsync` (good).
*   Ensure `ProductRepositoryAdapter.BulkCreateAsync` delegates correctly to `_dal.BulkInsertAsync`.

### Step 4: Staging & Sync Configuration
*   Ensure `Program.cs` registers the DALs as `Singleton` (already done, but verify).
*   Ensure `InMemoryTableManager` is configured to handle the background flushing.

## 4. Migration Strategy
1.  Apply Entity changes.
2.  Regenerate DAL (simulated by updating usages since we are "the generator").
3.  Update Adapter code.
4.  Test with `ProductProcessingWorker`.

## 5. Verification Checklist
*   [ ] Reads hit memory (check logs/metrics).
*   [ ] Writes hit memory first.
*   [ ] Staging table fills up every ~30s.
*   [ ] Primary table fills up every ~60s.
*   [ ] Bulk imports use 1 DB call per batch (via Staging flush).
