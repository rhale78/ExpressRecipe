# USDA Recall Import - Final Solution

## Problem Statement
The USDA FSIS recall import was failing with 404 errors. Investigation revealed that:
1. The old RSS feed (`https://www.fsis.usda.gov/rss/fsis-recalls.xml`) has been discontinued
2. No public USDA FSIS API exists at `https://api.fsis.usda.gov`
3. USDA recall data is only available through their website (not via API)

## Solution: Use FDA API for Meat/Poultry Recalls

The FDA's openFDA API includes recalls for USDA-regulated products (meat, poultry, eggs) because:
- FDA's Food Enforcement database aggregates all food recalls
- USDA FSIS reports recalls to FDA
- FDA provides a legitimate, maintained public API

## Implementation Details

### 1. New Method: `ImportMeatPoultryRecallsFromFDAAsync()`

**Location:** `FDARecallImportService.cs`

```csharp
public async Task<RecallImportResult> ImportMeatPoultryRecallsFromFDAAsync(int limit = 50)
```

**Features:**
- Searches FDA API for meat/poultry/egg products using keyword filter
- Tags recalls with `Source = "USDA-MEAT"` to distinguish from general FDA recalls
- Uses existing FDA HttpClient (no separate configuration needed)
- Returns standard `RecallImportResult`

**FDA API Query:**
```
food/enforcement.json?search=product_description:(meat OR poultry OR chicken OR beef OR pork OR turkey OR egg)&limit=50&sort=report_date:desc
```

### 2. Updated: `ImportUSDARecallsAsync()`

**Status:** Now returns informational error message

```csharp
result.ErrorMessage = "USDA FSIS no longer provides a public API or RSS feed. " +
                     "Alternative data sources may be needed...";
```

**Behavior:**
- Logs warning about unavailable data source
- Returns empty result with descriptive error message
- Includes TODO comments for future alternatives

### 3. Deprecated: `ProcessUSDARecallFromApiAsync()`

```csharp
[Obsolete("USDA FSIS Public API is no longer available")]
private async Task ProcessUSDARecallFromApiAsync(...)
```

Marked as obsolete for future removal.

### 4. New API Endpoint

**Location:** `AdminController.cs`

```
POST /api/admin/import/meat-poultry-recalls?limit=50
```

**Response:**
```json
{
  "importId": "guid",
  "source": "USDA-MEAT",
  "status": "InProgress",
  "startedAt": "2024-01-15T10:00:00Z"
}
```

### 5. Updated Background Worker

**Location:** `RecallMonitorWorker.cs`

Now calls both:
1. `ImportRecentRecallsAsync()` - General FDA recalls
2. `ImportMeatPoultryRecallsFromFDAAsync()` - Meat/poultry recalls

Runs every hour to check for new recalls.

### 6. Configuration Cleanup

**Location:** `Program.cs`

- Removed USDA HttpClient configuration (no longer needed)
- Only FDA HttpClient is configured
- Simplified resilience policies

## Database Impact

### Recall Table - Source Field Values

| Source Value | Description |
|-------------|-------------|
| `FDA` | General FDA food recalls |
| `USDA-MEAT` | Meat/poultry/egg recalls from FDA API (USDA-regulated) |
| ~~`USDA`~~ | Legacy value (no longer created) |

### Query Examples

```sql
-- Get all meat/poultry recalls
SELECT * FROM Recall WHERE Source = 'USDA-MEAT'

-- Get all recalls (FDA + meat/poultry)
SELECT * FROM Recall WHERE Source IN ('FDA', 'USDA-MEAT')

-- Get recent USDA-regulated product recalls
SELECT * FROM Recall 
WHERE Source = 'USDA-MEAT' 
AND RecallDate >= DATEADD(day, -30, GETUTCDATE())
ORDER BY RecallDate DESC
```

## API Usage

### Import General FDA Recalls
```bash
POST http://localhost:5005/api/admin/import/fda-recalls?limit=100
```

### Import Meat/Poultry Recalls
```bash
POST http://localhost:5005/api/admin/import/meat-poultry-recalls?limit=50
```

### Check Import Status
```bash
GET http://localhost:5005/api/admin/import/{importId}
```

### Legacy USDA Endpoint (Returns Error)
```bash
POST http://localhost:5005/api/admin/import/usda-recalls
# Returns: "USDA FSIS no longer provides a public API or RSS feed..."
```

## Testing Checklist

- [x] Build successful
- [x] FDA HttpClient configuration correct
- [x] New meat/poultry import method compiles
- [x] Admin controller endpoints updated
- [x] Background worker updated
- [ ] Test meat/poultry import via API endpoint
- [ ] Verify `USDA-MEAT` source tag in database
- [ ] Confirm duplicate prevention works
- [ ] Test background worker execution

## Alternative Data Sources (Future)

If FDA API doesn't provide sufficient USDA recall coverage:

### Option 1: Web Scraping
- **URL:** https://www.fsis.usda.gov/recalls
- **Pros:** Most complete USDA data
- **Cons:** May violate ToS, brittle, requires HTML parsing

### Option 2: Data.gov
- **URL:** https://catalog.data.gov/dataset?q=usda+recall
- **Pros:** Official government data
- **Cons:** May be delayed, requires download/import workflow

### Option 3: Email Alerts
- **URL:** Subscribe to USDA FSIS recall alerts
- **Pros:** Real-time notifications
- **Cons:** Manual data entry required

### Option 4: Commercial APIs
- Third-party food safety data providers
- May aggregate multiple sources
- Typically requires subscription/payment

## Recommendation

**Continue using the FDA API approach** (`ImportMeatPoultryRecallsFromFDAAsync`) because:
1. ? Legitimate public API access
2. ? Maintained by government agency
3. ? Includes USDA-regulated products
4. ? Reliable JSON format
5. ? No Terms of Service concerns
6. ? Integrated with existing resilience policies

## Migration Notes

### For Existing Deployments

1. **No database migration needed** - Same schema
2. **Update API calls** - Use new `/meat-poultry-recalls` endpoint
3. **Remove USDA-specific monitoring** - No longer applicable
4. **Update documentation** - Reference new endpoint

### For Developers

1. **Use `ImportMeatPoultryRecallsFromFDAAsync()`** for meat/poultry imports
2. **Ignore `ImportUSDARecallsAsync()`** - Returns error message only
3. **Check `Source` field** - Use `"USDA-MEAT"` to filter meat/poultry recalls
4. **Monitor FDA API limits** - Same rate limits apply to all FDA endpoints

## Documentation Updates

- [x] Code comments updated
- [x] XML documentation added
- [x] API endpoint documented
- [x] Background worker updated
- [x] USDA_API_MIGRATION.md created
- [x] This summary document created

## Support

For questions or issues:
1. Check FDA openFDA documentation: https://open.fda.gov/apis/food/enforcement/
2. Review USDA_API_MIGRATION.md for detailed technical info
3. Test meat/poultry endpoint: `POST /api/admin/import/meat-poultry-recalls`
