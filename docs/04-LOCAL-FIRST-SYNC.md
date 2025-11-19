# ExpressRecipe - Local-First Synchronization Strategy

## Overview

ExpressRecipe uses a **local-first** architecture where:
1. All data is stored locally on the device
2. The app works fully offline
3. Cloud sync is an enhancement, not a requirement
4. User owns and controls their data

## Why Local-First?

### Benefits
- **Privacy**: Sensitive health data stays on device by default
- **Performance**: Instant response, no network latency
- **Reliability**: Works without internet connection
- **Cost**: Reduced cloud storage and bandwidth costs
- **User Control**: Data export, no vendor lock-in

### Challenges
- Data synchronization complexity
- Conflict resolution
- Storage limitations on devices
- Cross-device consistency

## Architecture

### Client Storage (SQLite)

Each client maintains a complete local database:

```
Local SQLite Database
├── User Profile & Preferences (always local)
├── Dietary Restrictions (always local)
├── Recipes (downloaded + personal)
├── Inventory (local + synced)
├── Shopping Lists (local + synced)
├── Scans History (local only)
├── Product Cache (downloaded)
└── Sync Queue (pending changes)
```

### Cloud Storage (SQL Server)

Cloud stores:
- Shared product database (read-mostly)
- Shared recipe database (read-mostly)
- User data (opt-in sync)
- Community contributions
- Price data
- Recall information

## Sync Strategy

### Sync Modes

**1. Never Sync (Local-Only)**
- User chooses to keep all data local
- Medical conditions can be local-only
- Personal recipes can be local-only
- Scan history is local-only by default

**2. Selective Sync**
- User chooses what to sync
- "Sync shopping lists but not inventory"
- "Sync saved recipes but not meal plans"

**3. Full Sync**
- All eligible data syncs to cloud
- Enables cross-device usage
- Required for family sharing

### Sync Frequency

**Manual Sync:**
- User triggers "Sync Now"
- Guaranteed immediate sync

**Automatic Sync:**
- On app startup (if online)
- After data changes (debounced 30s)
- Periodic background (every 15 min when online)
- Before app close

**Push Notifications:**
- Trigger sync when server has updates
- Use SignalR or Firebase Cloud Messaging

## Data Synchronization Patterns

### 1. Last-Write-Wins (LWW)

**For:** User preferences, settings, profile data

**Algorithm:**
```csharp
if (server.UpdatedAt > client.UpdatedAt) {
    client.Data = server.Data;
    client.UpdatedAt = server.UpdatedAt;
} else if (client.UpdatedAt > server.UpdatedAt) {
    server.Data = client.Data;
    server.UpdatedAt = client.UpdatedAt;
}
```

**Pros:** Simple, no conflicts
**Cons:** Data loss possible if concurrent edits

### 2. Append-Only (Event Sourcing)

**For:** Inventory usage, scan history, shopping events

**Algorithm:**
```csharp
// Always append new events
// Never modify/delete existing events
events.Add(new InventoryUsed {
    ProductId = productId,
    Quantity = quantity,
    Timestamp = DateTime.UtcNow
});

// Current state is computed from all events
var currentQuantity = events
    .Where(e => e.ProductId == productId)
    .Sum(e => e.Type == "Added" ? e.Quantity : -e.Quantity);
```

**Pros:** No data loss, full history
**Cons:** Storage overhead

### 3. Operational Transform (OT)

**For:** Shared shopping lists, collaborative editing

**Algorithm:**
```csharp
// Transform operations based on concurrent edits
// Example: Two users adding to same list
Operation op1 = new AddItem { Index = 3, Item = "Milk" };
Operation op2 = new AddItem { Index = 3, Item = "Eggs" };

// Transform op2 against op1
var transformedOp2 = op2.Transform(op1);
// Result: op2 now inserts at Index = 4
```

**Pros:** True collaborative editing
**Cons:** Complex implementation

### 4. Conflict Detection & Resolution

**For:** Recipes, meal plans

**Detection:**
```csharp
public class SyncEntity {
    public DateTime UpdatedAt { get; set; }
    public long ChangeVector { get; set; } // Incrementing version
    public byte[] RowVersion { get; set; } // Database version
}

// Conflict occurs when:
bool IsConflict =
    server.ChangeVector != client.LastKnownServerVersion &&
    client.ChangeVector != server.LastKnownClientVersion;
```

**Resolution Strategies:**

```csharp
public enum ConflictResolution {
    ServerWins,      // Use server version
    ClientWins,      // Use client version
    Newest,          // Use most recent UpdatedAt
    Manual,          // Prompt user
    Merge            // Attempt automatic merge
}
```

**User Prompt Example:**
```
Conflict Detected: Recipe "Chicken Soup"

Your Version (edited 2 hours ago):
- 2 cups chicken broth
- 1 lb chicken
- Salt and pepper

Server Version (edited 1 hour ago):
- 3 cups chicken broth
- 1 lb chicken
- Salt, pepper, and thyme

Choose: [Keep Mine] [Use Server] [View Diff]
```

## Sync Protocol

### Pull Sync (Client ← Server)

**Step 1: Get Server Changes**
```http
POST /sync/pull
Content-Type: application/json

{
  "userId": "user-guid",
  "deviceId": "device-guid",
  "lastSyncVector": {
    "products": 12345,
    "recipes": 67890,
    "inventory": 11111
  }
}

Response:
{
  "changeVector": {
    "products": 12500,
    "recipes": 68000,
    "inventory": 11200
  },
  "changes": {
    "products": [...],
    "recipes": [...],
    "inventory": [...]
  },
  "deletions": {
    "recipes": ["recipe-id-1", "recipe-id-2"]
  }
}
```

**Step 2: Apply Changes Locally**
```csharp
foreach (var change in serverChanges) {
    var local = db.Find(change.Id);

    if (local == null) {
        // New from server
        db.Insert(change);
    } else if (local.UpdatedAt < change.UpdatedAt) {
        // Server newer
        if (local.HasLocalChanges) {
            // Conflict!
            await ResolveConflict(local, change);
        } else {
            db.Update(change);
        }
    }
    // Else: local is newer, keep local
}
```

### Push Sync (Client → Server)

**Step 1: Collect Local Changes**
```csharp
var changes = db.Query(@"
    SELECT * FROM SyncQueue
    WHERE IsSynced = 0
    ORDER BY CreatedAt
");
```

**Step 2: Push to Server**
```http
POST /sync/push
Content-Type: application/json

{
  "userId": "user-guid",
  "deviceId": "device-guid",
  "changes": [
    {
      "entityType": "inventory",
      "entityId": "item-guid",
      "operation": "update",
      "data": {...},
      "clientVector": 123,
      "timestamp": "2025-11-19T10:30:00Z"
    }
  ]
}

Response:
{
  "accepted": ["item-guid-1", "item-guid-2"],
  "rejected": [
    {
      "entityId": "item-guid-3",
      "reason": "Conflict",
      "serverData": {...}
    }
  ]
}
```

**Step 3: Mark as Synced**
```csharp
foreach (var acceptedId in response.Accepted) {
    db.Execute(@"
        UPDATE SyncQueue
        SET IsSynced = 1, SyncedAt = @now
        WHERE EntityId = @id
    ", new { now = DateTime.UtcNow, id = acceptedId });
}

foreach (var rejected in response.Rejected) {
    await HandleRejection(rejected);
}
```

## Sync Queue Management

### Queue Table
```sql
CREATE TABLE SyncQueue (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    EntityType TEXT NOT NULL,
    EntityId TEXT NOT NULL,
    Operation TEXT NOT NULL, -- Insert, Update, Delete
    Data TEXT NOT NULL, -- JSON serialized entity
    CreatedAt INTEGER NOT NULL,
    IsSynced INTEGER NOT NULL DEFAULT 0,
    SyncedAt INTEGER NULL,
    RetryCount INTEGER NOT NULL DEFAULT 0,
    LastError TEXT NULL
);
```

### Queueing Changes
```csharp
public async Task UpdateInventoryItem(InventoryItem item) {
    // Update local database
    await db.UpdateAsync(item);

    // Queue for sync
    await db.InsertAsync(new SyncQueueItem {
        EntityType = "inventory",
        EntityId = item.Id.ToString(),
        Operation = "Update",
        Data = JsonSerializer.Serialize(item),
        CreatedAt = DateTime.UtcNow
    });
}
```

### Processing Queue
```csharp
public async Task ProcessSyncQueue() {
    var pending = await db.QueryAsync<SyncQueueItem>(
        "SELECT * FROM SyncQueue WHERE IsSynced = 0 LIMIT 100");

    if (!pending.Any()) return;

    var result = await httpClient.PostAsync("/sync/push", pending);

    foreach (var accepted in result.Accepted) {
        await db.ExecuteAsync(
            "UPDATE SyncQueue SET IsSynced = 1 WHERE EntityId = ?",
            accepted);
    }
}
```

## Handling Offline Edits

### Optimistic UI Updates
```csharp
// Immediately update UI
item.Quantity -= usedQuantity;
OnPropertyChanged(nameof(Inventory));

// Queue for background sync
await syncQueue.Enqueue(new InventoryUsed {
    ItemId = item.Id,
    Quantity = usedQuantity,
    Timestamp = DateTime.UtcNow
});

// Sync in background (fire and forget)
_ = Task.Run(async () => {
    try {
        await syncService.Sync();
    } catch (Exception ex) {
        logger.LogError("Sync failed: {Error}", ex.Message);
        // Will retry later
    }
});
```

### Retry Logic
```csharp
public async Task<bool> SyncWithRetry(int maxRetries = 3) {
    for (int attempt = 1; attempt <= maxRetries; attempt++) {
        try {
            await Sync();
            return true;
        } catch (HttpRequestException) {
            if (attempt == maxRetries) {
                return false;
            }
            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
        }
    }
    return false;
}
```

## Data Partitioning

### By User
- Each user's data is independent
- No cross-user conflicts
- User ID is partition key

### By Device
- Track which device made changes
- Useful for debugging
- Device ID in sync metadata

### By Data Type
- Sync recipes separately from inventory
- Prioritize critical data (allergens, restrictions)
- Allow selective sync

## Network Optimization

### Delta Sync
Only send changed fields:
```json
{
  "entityId": "item-123",
  "delta": {
    "quantity": 5,      // Changed
    "updatedAt": "..." // Changed
    // Name, productId, etc. unchanged
  }
}
```

### Compression
```csharp
var json = JsonSerializer.Serialize(changes);
var compressed = await Gzip.CompressAsync(json);
// 70-90% size reduction typical
```

### Batching
```csharp
// Don't sync every keystroke
var debouncer = new Debouncer(TimeSpan.FromSeconds(30));
debouncer.Debounce(async () => await syncService.Sync());
```

### Partial Sync
```csharp
// Only sync what's needed
await syncService.SyncInventory();  // High priority
await syncService.SyncShoppingLists(); // High priority
// Skip recipe sync if low bandwidth
```

## Sync Status UI

### Status Indicators
```
[✓] Synced - All changes uploaded
[⟳] Syncing - Sync in progress
[!] Conflicts - Needs attention
[⚠] Offline - Waiting for network
[✗] Error - Sync failed, will retry
```

### Sync Details
```
Last synced: 2 minutes ago
Pending changes: 3
- Shopping list updated
- 2 inventory items used
- Recipe saved

[Sync Now] [View Conflicts]
```

## Testing Sync

### Unit Tests
```csharp
[Fact]
public async Task WhenServerNewer_ClientShouldUpdate() {
    var server = new Item { Id = 1, Name = "Server", UpdatedAt = DateTime.UtcNow };
    var client = new Item { Id = 1, Name = "Client", UpdatedAt = DateTime.UtcNow.AddHours(-1) };

    var result = await syncService.Merge(client, server);

    Assert.Equal("Server", result.Name);
}
```

### Integration Tests
```csharp
[Fact]
public async Task FullSyncRoundTrip() {
    // 1. Create item on client
    var item = await clientDb.CreateInventoryItem(...);

    // 2. Sync to server
    await syncService.Push();

    // 3. Clear client cache
    await clientDb.Clear();

    // 4. Sync from server
    await syncService.Pull();

    // 5. Verify item restored
    var restored = await clientDb.GetInventoryItem(item.Id);
    Assert.NotNull(restored);
}
```

### Conflict Simulation
```csharp
[Fact]
public async Task ConcurrentEdits_ShouldDetectConflict() {
    var itemId = Guid.NewGuid();

    // Client A updates
    await clientA.UpdateItem(itemId, "Version A");

    // Client B updates (offline, doesn't know about A)
    await clientB.UpdateItem(itemId, "Version B");

    // Client A syncs (succeeds)
    await clientA.Sync();

    // Client B syncs (conflict!)
    var result = await clientB.Sync();

    Assert.True(result.HasConflicts);
    Assert.Single(result.Conflicts);
}
```

## Next Steps
See frontend architecture in `05-FRONTEND-ARCHITECTURE.md`
