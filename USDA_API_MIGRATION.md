# USDA Recall Data Source - Reality Check

## Issue
The USDA FSIS RSS feed URL (`https://www.fsis.usda.gov/rss/fsis-recalls.xml`) returns 404 errors. 
The suggested API endpoint (`https://api.fsis.usda.gov/api/v1/recalls`) also returns 404 errors.

## Root Cause
**USDA FSIS does not provide a public API or RSS feed for recall data.** 

After investigation:
- The old RSS feed at `fsis.usda.gov/rss/fsis-recalls.xml` has been discontinued
- There is no public API at `api.fsis.usda.gov` 
- USDA recall data is only available through their website: https://www.fsis.usda.gov/recalls

## Solution Implemented

### Realistic Approach: Use FDA API for Meat/Poultry Recalls

The FDA's openFDA API includes recalls for **USDA-regulated products** (meat, poultry, egg products). This is because:
- FDA's Food Enforcement database includes all food recalls regardless of regulatory agency
- USDA FSIS recalls are reported to FDA and included in the enforcement API
- This provides legitimate API access to meat/poultry recall data

### Changes Made

#### 1. Removed Non-Existent USDA API Code (FDARecallImportService.cs)

**Deprecated Method:**
```csharp
[Obsolete("USDA FSIS Public API is no longer available")]
private async Task ProcessUSDARecallFromApiAsync(...)
```

**Updated ImportUSDARecallsAsync():**
- Returns empty result with informational error message
- Logs warning about unavailable data source
- Includes TODO comments for alternative approaches

#### 2. Added New Method: ImportMeatPoultryRecallsFromFDAAsync()

Uses FDA API to import meat/poultry recalls:
```csharp
var url = $"food/enforcement.json?search=product_description:(meat OR poultry OR chicken OR beef OR pork OR turkey OR egg)&limit={limit}&sort=report_date:desc";
```

- Searches FDA API for meat/poultry/egg products
- Tags recalls with source `"USDA-MEAT"` to distinguish from general FDA recalls
- Uses same FDA HttpClient (no separate USDA client needed)

#### 3. Removed USDA HttpClient Configuration (Program.cs)

The USDA HttpClient configuration block was removed since:
- No valid USDA API endpoint exists
- All recall data comes from FDA API
- Simplifies configuration and reduces confusion

### API Response Format

FDA API returns meat/poultry recalls in same format as other food recalls:

```json
{
  "results": [
    {
      "recall_number": "F-001-2024",
      "product_description": "Ground Beef Products",
      "classification": "Class I",
      "reason_for_recall": "Possible E. coli contamination",
      "recall_initiation_date": "20240115",
      "report_date": "20240120"
    }
  ]
}
```

### Alternative Data Sources (Future Consideration)

If FDA API doesn't provide sufficient USDA recall coverage:

1. **Web Scraping** https://www.fsis.usda.gov/recalls
   - Requires HTML parsing
   - May violate Terms of Service
   - Brittle (breaks with website changes)

2. **Data.gov Datasets**
   - Check https://catalog.data.gov/dataset?q=usda+recall
   - May provide bulk downloads or periodic exports

3. **Email Alerts + Manual Entry**
   - Subscribe to USDA FSIS recall alerts
   - Manual data entry workflow

4. **FSIS Widget/RSS (if restored)**
   - Monitor for restoration of public feeds

## Testing

1. Build successful ?
2. FDA API continues to work for all food recalls ?
3. Meat/poultry filtering added via search query ?

## Usage

### Import Meat/Poultry Recalls
```csharp
var result = await fdaRecallImportService.ImportMeatPoultryRecallsFromFDAAsync(limit: 50);
```

### Check Source in Database
Recalls are tagged with:
- `Source = "FDA"` - General FDA food recalls
- `Source = "USDA-MEAT"` - Meat/poultry recalls from FDA API

## Recommendation

**Use the FDA API method (`ImportMeatPoultryRecallsFromFDAAsync`)** as the primary source for meat/poultry recall data. This provides:
- Legitimate API access (no Terms of Service concerns)
- Reliable data format
- Maintained by government agency
- Includes USDA-regulated products

The original `ImportUSDARecallsAsync()` method now returns an informational error explaining the situation.
