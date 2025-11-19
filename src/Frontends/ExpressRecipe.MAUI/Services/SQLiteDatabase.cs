using SQLite;

namespace ExpressRecipe.MAUI.Services;

/// <summary>
/// Implementation of SQLite database for offline data storage
/// </summary>
public class SQLiteDatabase : ISQLiteDatabase
{
    private readonly SQLiteAsyncConnection _database;

    public SQLiteDatabase(string dbPath)
    {
        _database = new SQLiteAsyncConnection(dbPath);
    }

    public SQLiteAsyncConnection GetConnection()
    {
        return _database;
    }

    public async Task InitializeAsync()
    {
        // Create tables for offline storage
        await _database.CreateTableAsync<OfflineProduct>();
        await _database.CreateTableAsync<OfflineRecipe>();
        await _database.CreateTableAsync<OfflineInventoryItem>();
        await _database.CreateTableAsync<OfflineShoppingItem>();
        await _database.CreateTableAsync<OfflineSyncQueue>();
    }

    public async Task<int> SaveItemAsync<T>(T item) where T : new()
    {
        return await _database.InsertOrReplaceAsync(item);
    }

    public async Task<int> DeleteItemAsync<T>(T item) where T : new()
    {
        return await _database.DeleteAsync(item);
    }

    public async Task<List<T>> GetAllAsync<T>() where T : new()
    {
        return await _database.Table<T>().ToListAsync();
    }

    public async Task<T> GetByIdAsync<T>(object primaryKey) where T : new()
    {
        return await _database.GetAsync<T>(primaryKey);
    }
}

// Offline data models
[Table("Products")]
public class OfflineProduct
{
    [PrimaryKey]
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string? UPC { get; set; }
    public string? Category { get; set; }
    public DateTime LastSynced { get; set; }
}

[Table("Recipes")]
public class OfflineRecipe
{
    [PrimaryKey]
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime LastSynced { get; set; }
}

[Table("Inventory")]
public class OfflineInventoryItem
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public Guid ServerId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public bool IsSynced { get; set; }
    public DateTime LastModified { get; set; }
}

[Table("Shopping")]
public class OfflineShoppingItem
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public Guid ServerId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsSynced { get; set; }
    public DateTime LastModified { get; set; }
}

[Table("SyncQueue")]
public class OfflineSyncQueue
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty; // Create, Update, Delete
    public string Data { get; set; } = string.Empty; // JSON
    public bool IsSynced { get; set; }
    public DateTime CreatedAt { get; set; }
    public int RetryCount { get; set; }
}
