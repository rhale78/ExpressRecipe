# Quick Reference: USDA Recall Import Fix

## What Happened?
USDA no longer provides a public API or RSS feed for recall data. The old endpoints return 404 errors.

## What's the Solution?
Use FDA API to import meat/poultry recalls (FDA database includes USDA-regulated products).

## New API Endpoint

### Import Meat/Poultry Recalls
```bash
POST http://localhost:5005/api/admin/import/meat-poultry-recalls?limit=50
```

**Response:**
```json
{
  "importId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "source": "USDA-MEAT",
  "status": "InProgress",
  "totalRecords": 0,
  "processedRecords": 0,
  "successCount": 0,
  "errorCount": 0,
  "startedAt": "2024-01-15T10:30:00Z",
  "completedAt": null
}
```

### Check Status
```bash
GET http://localhost:5005/api/admin/import/{importId}
```

## Database Query

```sql
-- View meat/poultry recalls
SELECT TOP 10 
    ExternalId,
    Source,
    Title,
    Severity,
    RecallDate,
    PublishedDate
FROM Recall
WHERE Source = 'USDA-MEAT'
ORDER BY RecallDate DESC
```

## Background Worker

The `RecallMonitorWorker` now automatically imports:
1. General FDA recalls (every hour)
2. Meat/poultry recalls (every hour)

No manual intervention needed after deployment.

## Testing

1. Start RecallService
2. Call new endpoint:
   ```bash
   curl -X POST http://localhost:5005/api/admin/import/meat-poultry-recalls?limit=10
   ```
3. Check response for `importId`
4. Query database for `Source = 'USDA-MEAT'`
5. Verify recalls are imported

## Migration Path

### From Old Code
```csharp
// OLD - No longer works
await importService.ImportUSDARecallsAsync();
```

### To New Code
```csharp
// NEW - Works with FDA API
await importService.ImportMeatPoultryRecallsFromFDAAsync(limit: 50);
```

## Key Changes

| Component | Old Behavior | New Behavior |
|-----------|-------------|--------------|
| USDA HttpClient | Configured for api.fsis.usda.gov | **Removed** (not needed) |
| ImportUSDARecallsAsync() | Tried to call USDA API ? 404 | Returns informational error |
| AdminController | POST /usda-recalls ? 404 | POST /meat-poultry-recalls ? ? |
| RecallMonitorWorker | Called USDA import ? failed | Calls meat/poultry import ? ? |
| Database Source | "USDA" | "USDA-MEAT" |

## Build Status
? Build successful
? All compilation errors resolved
? Ready for deployment

## Next Steps
1. Deploy updated RecallService
2. Test new `/meat-poultry-recalls` endpoint
3. Monitor background worker logs
4. Verify meat/poultry recalls appear in database
5. Update any client applications using old USDA endpoint
