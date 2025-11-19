namespace ExpressRecipe.Client.Shared.Services.LocalStorage;

/// <summary>
/// Generic interface for local storage repositories
/// </summary>
public interface ILocalStorageRepository<T> where T : class
{
    Task<T?> GetByIdAsync(Guid id);
    Task<List<T>> GetAllAsync();
    Task<bool> SaveAsync(T entity);
    Task<bool> UpdateAsync(T entity);
    Task<bool> DeleteAsync(Guid id);
    Task<bool> ClearAllAsync();
    Task<int> GetCountAsync();
}

/// <summary>
/// Base entity for local storage
/// </summary>
public abstract class LocalStorageEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsSynced { get; set; }
    public DateTime? LastSyncedAt { get; set; }
    public bool IsDeleted { get; set; }
}
