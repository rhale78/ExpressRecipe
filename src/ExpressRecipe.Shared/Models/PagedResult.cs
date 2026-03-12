namespace ExpressRecipe.Shared.Models;

/// <summary>
/// Generic paged result envelope returned by paginated API endpoints.
/// </summary>
/// <typeparam name="T">The item type in the page.</typeparam>
public sealed record PagedResult<T>(
    List<T> Items,
    int Total,
    int Page,
    int PageSize)
{
    /// <summary>Total number of pages given the current page size.</summary>
    public int TotalPages => (int)Math.Ceiling(Total / (double)PageSize);

    /// <summary>Whether a next page exists.</summary>
    public bool HasNextPage => Page < TotalPages;

    /// <summary>Whether a previous page exists.</summary>
    public bool HasPreviousPage => Page > 1;
}
