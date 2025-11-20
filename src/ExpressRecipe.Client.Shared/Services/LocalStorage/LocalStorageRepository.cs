using Blazored.LocalStorage;

namespace ExpressRecipe.Client.Shared.Services.LocalStorage;

/// <summary>
/// Base implementation of local storage repository using Blazored.LocalStorage (IndexedDB)
/// </summary>
public class LocalStorageRepository<T> : ILocalStorageRepository<T> where T : LocalStorageEntity
{
    private readonly ILocalStorageService _localStorage;
    private readonly string _storageKey;

    public LocalStorageRepository(ILocalStorageService localStorage, string storageKey)
    {
        _localStorage = localStorage;
        _storageKey = storageKey;
    }

    public async Task<T?> GetByIdAsync(Guid id)
    {
        var items = await GetAllAsync();
        return items.FirstOrDefault(x => x.Id == id && !x.IsDeleted);
    }

    public async Task<List<T>> GetAllAsync()
    {
        try
        {
            var items = await _localStorage.GetItemAsync<List<T>>(_storageKey);
            return items?.Where(x => !x.IsDeleted).ToList() ?? new List<T>();
        }
        catch
        {
            return new List<T>();
        }
    }

    public async Task<bool> SaveAsync(T entity)
    {
        try
        {
            var items = await GetAllInternalAsync();

            entity.CreatedAt = DateTime.UtcNow;
            entity.IsSynced = false;

            items.Add(entity);
            await _localStorage.SetItemAsync(_storageKey, items);

            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> UpdateAsync(T entity)
    {
        try
        {
            var items = await GetAllInternalAsync();
            var existing = items.FirstOrDefault(x => x.Id == entity.Id);

            if (existing == null)
                return false;

            var index = items.IndexOf(existing);
            entity.UpdatedAt = DateTime.UtcNow;
            entity.IsSynced = false;
            items[index] = entity;

            await _localStorage.SetItemAsync(_storageKey, items);

            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        try
        {
            var items = await GetAllInternalAsync();
            var existing = items.FirstOrDefault(x => x.Id == id);

            if (existing == null)
                return false;

            // Soft delete
            existing.IsDeleted = true;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.IsSynced = false;

            await _localStorage.SetItemAsync(_storageKey, items);

            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> ClearAllAsync()
    {
        try
        {
            await _localStorage.RemoveItemAsync(_storageKey);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<int> GetCountAsync()
    {
        var items = await GetAllAsync();
        return items.Count;
    }

    /// <summary>
    /// Get all items including deleted (for internal use)
    /// </summary>
    private async Task<List<T>> GetAllInternalAsync()
    {
        try
        {
            var items = await _localStorage.GetItemAsync<List<T>>(_storageKey);
            return items ?? new List<T>();
        }
        catch
        {
            return new List<T>();
        }
    }

    /// <summary>
    /// Get unsynced items for sync queue
    /// </summary>
    public async Task<List<T>> GetUnsyncedAsync()
    {
        var items = await GetAllInternalAsync();
        return items.Where(x => !x.IsSynced).ToList();
    }

    /// <summary>
    /// Mark item as synced
    /// </summary>
    public async Task<bool> MarkAsSyncedAsync(Guid id)
    {
        try
        {
            var items = await GetAllInternalAsync();
            var item = items.FirstOrDefault(x => x.Id == id);

            if (item == null)
                return false;

            item.IsSynced = true;
            item.LastSyncedAt = DateTime.UtcNow;

            await _localStorage.SetItemAsync(_storageKey, items);

            return true;
        }
        catch
        {
            return false;
        }
    }
}
