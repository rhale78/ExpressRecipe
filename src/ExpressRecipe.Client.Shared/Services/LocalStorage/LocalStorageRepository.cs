using Blazored.LocalStorage;

namespace ExpressRecipe.Client.Shared.Services.LocalStorage
{
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
            List<T> items = await GetAllAsync();
            return items.FirstOrDefault(x => x.Id == id && !x.IsDeleted);
        }

        public async Task<List<T>> GetAllAsync()
        {
            try
            {
                List<T>? items = await _localStorage.GetItemAsync<List<T>>(_storageKey);
                return items?.Where(x => !x.IsDeleted).ToList() ?? [];
            }
            catch
            {
                return [];
            }
        }

        public async Task<bool> SaveAsync(T entity)
        {
            try
            {
                List<T> items = await GetAllInternalAsync();

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
                List<T> items = await GetAllInternalAsync();
                T? existing = items.FirstOrDefault(x => x.Id == entity.Id);

                if (existing == null)
                {
                    return false;
                }

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
                List<T> items = await GetAllInternalAsync();
                T? existing = items.FirstOrDefault(x => x.Id == id);

                if (existing == null)
                {
                    return false;
                }

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
            List<T> items = await GetAllAsync();
            return items.Count;
        }

        /// <summary>
        /// Get all items including deleted (for internal use)
        /// </summary>
        private async Task<List<T>> GetAllInternalAsync()
        {
            try
            {
                List<T>? items = await _localStorage.GetItemAsync<List<T>>(_storageKey);
                return items ?? [];
            }
            catch
            {
                return [];
            }
        }

        /// <summary>
        /// Get unsynced items for sync queue
        /// </summary>
        public async Task<List<T>> GetUnsyncedAsync()
        {
            List<T> items = await GetAllInternalAsync();
            return items.Where(x => !x.IsSynced).ToList();
        }

        /// <summary>
        /// Mark item as synced
        /// </summary>
        public async Task<bool> MarkAsSyncedAsync(Guid id)
        {
            try
            {
                List<T> items = await GetAllInternalAsync();
                T? item = items.FirstOrDefault(x => x.Id == id);

                if (item == null)
                {
                    return false;
                }

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
}
