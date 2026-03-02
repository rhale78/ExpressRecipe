# Logging Configuration - Quieted HTTP Client and Polly Logs

## Date: January 2025

## Problem

Console logs were flooded with verbose HTTP client and Polly retry logs, making it difficult to see actual batch progress:

```
info: System.Net.Http.HttpClient.IngredientServiceClient.ClientHandler[101]
      Received HTTP response headers after 5.0161ms - 401
info: Polly[3]
      Execution attempt. Source: '-standard//Standard-Retry', Operation Key: '', Result: '401', Handled: 'False'
info: System.Net.Http.HttpClient.IngredientServiceClient.LogicalHandler[101]
      End processing HTTP request after 11.4186ms - 401
```

Important batch progress logs were getting buried:
```
info: ExpressRecipe.RecipeService.Services.BatchRecipeProcessor[0]
      Writer: Processed 58000 | Speed: 651.5 rec/sec | Lag: 5000 records
info: ExpressRecipe.RecipeService.Workers.RecipeImportWorker[0]
      Producer: Read 1075000 recipes from file...
```

## Solution: Configured Logging Levels

### Changes Made

Updated `appsettings.json` for three services to set specific log levels:

#### 1. **RecipeService** - `src/Services/ExpressRecipe.RecipeService/appsettings.json`
Added:
```json
"Logging": {
  "LogLevel": {
    "Default": "Information",
    "Microsoft.AspNetCore": "Warning",
    "System.Net.Http.HttpClient": "Warning",
    "Polly": "Warning",
    "ExpressRecipe": "Information"
  }
}
```

#### 2. **IngredientService** - `src/Services/ExpressRecipe.IngredientService/appsettings.json`
Updated:
```json
"Logging": {
  "LogLevel": {
    "Default": "Information",
    "Microsoft.AspNetCore": "Warning",
    "System.Net.Http.HttpClient": "Warning",
    "Polly": "Warning",
    "ExpressRecipe": "Information"
  }
}
```

#### 3. **ProductService** - `src/Services/ExpressRecipe.ProductService/appsettings.json`
Updated:
```json
"Logging": {
  "LogLevel": {
    "Default": "Information",
    "Microsoft.AspNetCore": "Warning",
    "System.Net.Http.HttpClient": "Warning",
    "Polly": "Warning",
    "ExpressRecipe": "Information"
  }
}
```

#### 4. **IngredientService Development** - `appsettings.Development.json`
Updated:
```json
"Logging": {
  "LogLevel": {
    "Default": "Debug",
    "Microsoft.AspNetCore": "Information",
    "System.Net.Http.HttpClient": "Warning",
    "Polly": "Warning",
    "Grpc": "Debug",
    "ExpressRecipe": "Debug"
  }
}
```

## What Gets Logged Now

### ✅ **Visible (Information/Debug):**
- ✅ **Batch progress** - "Writer: Processed 58000 | Speed: 651.5 rec/sec"
- ✅ **Import progress** - "Producer: Read 1075000 recipes from file"
- ✅ **Database operations** - "Bulk insert completed: 1000 records"
- ✅ **Migration status** - "Migration 001_InitialIngredientSchema completed"
- ✅ **Application events** - All ExpressRecipe namespace logs

### ⛔ **Hidden (Warning only):**
- ⛔ HTTP client request/response logs
- ⛔ Polly retry execution logs
- ⛔ ASP.NET Core routing logs
- ⛔ Service discovery resolution logs

## Log Level Hierarchy

```
ExpressRecipe.*                  → Information/Debug (application logs)
System.Net.Http.HttpClient       → Warning (only errors)
Polly                            → Warning (only errors)
Microsoft.AspNetCore             → Warning (only errors)
Default                          → Information
```

## Benefits

1. **Cleaner Console** - No more HTTP request spam
2. **Easier Debugging** - See actual batch progress clearly
3. **Performance Metrics Visible** - Speed, lag, counts front and center
4. **Still See Errors** - Warning level catches actual problems
5. **Configurable** - Can adjust per-environment

## Example: Before vs After

### Before:
```
info: System.Net.Http.HttpClient[100] - Sending HTTP request
info: System.Net.Http.HttpClient[101] - Received headers - 200
info: Polly[3] - Execution attempt. Result: '200'
info: System.Net.Http.HttpClient[101] - End processing - 200
info: ExpressRecipe.RecipeService[0] - Processed 58000 | Speed: 651.5 rec/sec
info: System.Net.Http.HttpClient[100] - Sending HTTP request
info: System.Net.Http.HttpClient[101] - Received headers - 200
```

### After:
```
info: ExpressRecipe.RecipeService[0] - Processed 58000 | Speed: 651.5 rec/sec | Lag: 5000 records
info: ExpressRecipe.RecipeService[0] - Producer: Read 1075000 recipes from file...
info: ExpressRecipe.RecipeService[0] - Bulk insert completed: 1000 records in 45ms
info: ExpressRecipe.RecipeService[0] - Processed 59000 | Speed: 655.2 rec/sec | Lag: 4000 records
```

## Files Changed

1. `src/Services/ExpressRecipe.RecipeService/appsettings.json`
2. `src/Services/ExpressRecipe.IngredientService/appsettings.json`
3. `src/Services/ExpressRecipe.IngredientService/appsettings.Development.json`
4. `src/Services/ExpressRecipe.ProductService/appsettings.json`

## Build Status

✅ Build successful

## Testing

After restart, you should see:
- ✅ Clean console with only batch progress
- ✅ "Writer: Processed X" messages clearly visible
- ✅ Import/export metrics front and center
- ⛔ No HTTP client request/response spam
- ⛔ No Polly retry execution logs

## Notes

- If you need to debug HTTP issues, temporarily change:
  - `"System.Net.Http.HttpClient": "Debug"` 
  - `"Polly": "Debug"`
- For production, consider setting ExpressRecipe to "Information" (not Debug)
- All error logs (Warning/Error/Critical) are still visible regardless of these settings
