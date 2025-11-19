using SQLite;

namespace ExpressRecipe.MAUI.Services;

/// <summary>
/// SQLite database service for offline storage
/// </summary>
public interface ISQLiteDatabase
{
    SQLiteAsyncConnection GetConnection();
    Task InitializeAsync();
    Task<int> SaveItemAsync<T>(T item) where T : new();
    Task<int> DeleteItemAsync<T>(T item) where T : new();
    Task<List<T>> GetAllAsync<T>() where T : new();
    Task<T> GetByIdAsync<T>(object primaryKey) where T : new();
}
