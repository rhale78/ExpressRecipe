# Architecture Clarification - Cloud-First with Offline Support

**Updated:** 2025-11-19

## Data Storage Strategy (Revised)

### Primary Architecture: Cloud-First with Local Caching

**Cloud Storage:**
- **Primary source of truth** for all user data
- SQL Server databases per microservice
- User data synchronized to cloud automatically
- Enables cross-device access
- Family sharing capabilities
- Data backup and recovery

**Local Storage (SQLite):**
- **Cache layer** for offline functionality
- Downloaded user data for offline access
- Enables shopping/scanning without connectivity
- Auto-sync when connection available
- Optimistic updates queued for sync

### Why Cloud-First?

1. **Cross-Device Access**: User data available on web, Windows, and mobile
2. **Data Safety**: Automatic backup, no data loss if device fails
3. **Family Sharing**: Multiple family members access shared data
4. **Scalability**: Centralized data for analytics and recommendations
5. **Updates**: Push recipe updates, recall alerts, price changes
6. **Community**: User contributions benefit all users

### Offline Capabilities

**What Works Offline:**
- View saved recipes
- Access shopping lists
- Scan products (with cached product database)
- Check personal inventory
- View meal plans
- Record usage/purchases (synced later)

**What Requires Connection:**
- New recipe discovery
- Real-time price comparisons
- Recall alerts (latest updates)
- Community reviews
- Product submissions
- New product lookups

### Sync Strategy

**Auto-Sync Triggers:**
- App startup (if online)
- After user action (debounced 5-10 seconds)
- Periodic background sync (every 5 minutes when online)
- Manual "Sync Now" button

**Sync Priority:**
1. **Critical**: Allergen settings, dietary restrictions
2. **High**: Shopping lists, inventory updates
3. **Medium**: Recipe saves, meal plans
4. **Low**: Preferences, history

**Smart Sync:**
```
User Opens App
    ↓
Check Connectivity
    ↓
If Online:
    - Fetch latest user data
    - Download recent recipes
    - Check for recalls
    - Update product database cache
    - Sync pending local changes
    ↓
If Offline:
    - Use cached data
    - Queue changes for later
    - Show "Offline Mode" indicator
    ↓
User Makes Changes
    ↓
Save Locally Immediately
    ↓
If Online:
    - Sync to cloud (background)
If Offline:
    - Queue for sync
    - Show "Pending sync" indicator
```

### Data Download Strategy

**Initial Setup:**
- User registers/logs in
- Download user profile and preferences
- Download saved recipes
- Download shopping lists
- Download inventory
- Cache common products (top 1000)

**Ongoing Updates:**
- Delta sync (only changed data)
- Incremental product cache updates
- New recipes based on preferences
- Recall alerts

**Storage Limits:**
- **Mobile**: Cache up to 500 MB
- **Desktop**: Cache up to 2 GB
- **Web**: Use IndexedDB (browser limits)

**Cache Expiration:**
- User data: Never (always fresh from server)
- Product data: 30 days
- Recipe data: 90 days
- Price data: 7 days

### Connection States

**Online:**
- Full functionality
- Real-time updates
- Immediate sync
- Green indicator

**Offline:**
- Limited to cached data
- Optimistic updates
- Yellow indicator
- "Pending sync: X changes"

**Slow Connection:**
- Deferred sync
- Essential data only
- Orange indicator

**Sync Conflict:**
- Automatic resolution (last-write-wins for most)
- User prompt for critical data
- Red indicator with action required

### Development Environment

**Local Development:**
- Can run fully local (no cloud needed)
- Use local SQL Server or SQLite for all services
- Aspire orchestrates everything locally
- No Azure subscription required for dev

**Testing:**
- Mock offline scenarios
- Simulate slow connections
- Test sync conflicts
- Test data recovery

### Production Deployment

**Cloud Resources:**
- Azure SQL Database (per service)
- Azure Container Apps (services)
- Azure Cache for Redis
- Azure Service Bus
- Azure Blob Storage (images)
- Azure CDN

**Cost Optimization:**
- Cache aggressively
- Minimize cloud reads
- Batch sync operations
- Compress data transfers

## Updated Service Responsibilities

### Sync Service

**Enhanced Responsibilities:**
- Manage sync queue priority
- Handle conflict resolution
- Monitor connection state
- Optimize data transfer
- Track sync status per entity
- Provide sync analytics

**API Endpoints:**
```
POST /sync/download       - Download user data to device
POST /sync/upload         - Upload pending changes
GET  /sync/status         - Get sync status
POST /sync/resolve        - Resolve conflict
GET  /sync/pending        - Get pending changes count
POST /sync/priority       - Change sync priority
```

### Offline Detection Service

**New Responsibilities:**
- Monitor network connectivity
- Detect connection quality
- Notify app of state changes
- Queue operations when offline
- Retry failed operations

## Implementation Notes

### Client-Side (Blazor/MAUI)

```csharp
public class ConnectivityService
{
    private bool _isOnline;

    public bool IsOnline => _isOnline;

    public event EventHandler<bool> ConnectivityChanged;

    public async Task<bool> CheckConnectivity()
    {
        try
        {
            // Ping health endpoint
            var response = await httpClient.GetAsync("/health");
            _isOnline = response.IsSuccessStatusCode;
        }
        catch
        {
            _isOnline = false;
        }

        ConnectivityChanged?.Invoke(this, _isOnline);
        return _isOnline;
    }
}

public class DataService
{
    public async Task<Product> GetProduct(Guid id)
    {
        // Try cloud first
        if (connectivityService.IsOnline)
        {
            try
            {
                var product = await apiClient.GetProductAsync(id);
                await localDb.UpsertProductAsync(product);
                return product;
            }
            catch (HttpRequestException)
            {
                // Fall through to local
            }
        }

        // Use local cache
        return await localDb.GetProductAsync(id);
    }

    public async Task UpdateInventory(InventoryItem item)
    {
        // Save locally immediately
        await localDb.UpdateInventoryAsync(item);

        // Sync to cloud if online
        if (connectivityService.IsOnline)
        {
            await syncService.SyncInventoryAsync(item);
        }
        else
        {
            await syncQueue.EnqueueAsync("inventory", item.Id, item);
        }
    }
}
```

### Background Sync Worker

```csharp
public class BackgroundSyncWorker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (connectivityService.IsOnline)
            {
                await syncService.ProcessQueueAsync();
            }

            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}
```

## Summary

**Key Changes from Initial Plan:**
- Cloud storage is PRIMARY, not optional
- Local storage is CACHE, not primary source
- Offline mode is temporary state, not permanent option
- Auto-sync is default behavior
- User data syncs across devices automatically

**Benefits:**
- No data loss
- Cross-device access
- Family sharing possible
- Better user experience
- Enables community features
- Supports analytics and recommendations

**User Control:**
- Can disable auto-sync (manual mode)
- Can clear local cache
- Can export data
- Can delete account and all cloud data
