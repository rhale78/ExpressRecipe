using ExpressRecipe.Shared.DTOs.Product;
using HighSpeedDAL.Core.Base;
using HighSpeedDAL.Core.Resilience;
using HighSpeedDAL.SqlServer;
using HighSpeedDAL.Core.Interfaces;
using Microsoft.Extensions.Logging;
using ExpressRecipe.ProductService.Entities;

namespace ExpressRecipe.ProductService.Data;

/// <summary>
/// Adapter for complex SQL/search operations. Uses HighSpeedDAL plumbing (IDbConnectionFactory/ExecuteQueryAsync)
/// to benefit from retry, connection factory, and consistent logging.
/// </summary>
public class ProductSearchAdapter
{
    private readonly ProductDatabaseConnection _connection;
    private readonly InternalDalBase _dalBase;
    private readonly ILogger<ProductSearchAdapter> _logger;

    public ProductSearchAdapter(ProductDatabaseConnection connection, ILogger<ProductSearchAdapter> logger,
        IDbConnectionFactory connectionFactory, DatabaseRetryPolicy retryPolicy)
    {
        _connection = connection;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        // Use a lightweight anonymous DAL base to access ExecuteQueryAsync. We create a small derived type.
        _dalBase = new InternalDalBase(connection, logger, connectionFactory, retryPolicy);
    }

    public async Task<List<ProductDto>> SearchAsync(ProductSearchRequest request)
    {
        // Delegate to the original SQL in ProductRepository.SearchFromDbAsync but using ExecuteQueryAsync
        var sql = BuildSearchSql(request, out object? parameters);
        var results = await _dalBase.ExecuteQueryPublicAsync<ProductDto>(sql, MapFromReader, parameters, null, CancellationToken.None);
        return results.ToList();
    }

    public async Task<int> GetSearchCountAsync(ProductSearchRequest request)
    {
        var sql = BuildCountSql(request, out object? parameters);
        var count = await _dalBase.ExecuteScalarPublicAsync<int>(sql, parameters, null, CancellationToken.None);
        return count;
    }

    public async Task<Dictionary<string,int>> GetLetterCountsAsync(ProductSearchRequest request)
    {
        var sql = BuildLetterCountsSql(request, out object? parameters);
        var list = await _dalBase.ExecuteQueryPublicAsync<(string Letter, int Count)>(sql, r => (r.GetString(r.GetOrdinal("FirstLetter")), r.GetInt32(r.GetOrdinal("ProductCount"))), parameters, null, CancellationToken.None);
        return list.ToDictionary(x => x.Item1, x => x.Item2);
    }

    private static ProductDto MapFromReader(System.Data.IDataReader r)
    {
        return new ProductDto
        {
            Id = r.GetGuid(r.GetOrdinal("Id")),
            Name = r.GetString(r.GetOrdinal("Name")),
            Brand = r.IsDBNull(r.GetOrdinal("Brand")) ? null : r.GetString(r.GetOrdinal("Brand")),
            Barcode = r.IsDBNull(r.GetOrdinal("Barcode")) ? null : r.GetString(r.GetOrdinal("Barcode")),
            BarcodeType = r.IsDBNull(r.GetOrdinal("BarcodeType")) ? null : r.GetString(r.GetOrdinal("BarcodeType")),
            Description = r.IsDBNull(r.GetOrdinal("Description")) ? null : r.GetString(r.GetOrdinal("Description")),
            Category = r.IsDBNull(r.GetOrdinal("Category")) ? null : r.GetString(r.GetOrdinal("Category")),
            ServingSize = r.IsDBNull(r.GetOrdinal("ServingSize")) ? null : r.GetString(r.GetOrdinal("ServingSize")),
            ServingUnit = r.IsDBNull(r.GetOrdinal("ServingUnit")) ? null : r.GetString(r.GetOrdinal("ServingUnit")),
            ImageUrl = r.IsDBNull(r.GetOrdinal("ImageUrl")) ? null : r.GetString(r.GetOrdinal("ImageUrl")),
            ApprovalStatus = r.IsDBNull(r.GetOrdinal("ApprovalStatus")) ? "Pending" : r.GetString(r.GetOrdinal("ApprovalStatus")),
            ApprovedBy = r.IsDBNull(r.GetOrdinal("ApprovedBy")) ? null : r.GetGuid(r.GetOrdinal("ApprovedBy")),
            ApprovedAt = r.IsDBNull(r.GetOrdinal("ApprovedAt")) ? null : r.GetDateTime(r.GetOrdinal("ApprovedAt")),
            RejectionReason = r.IsDBNull(r.GetOrdinal("RejectionReason")) ? null : r.GetString(r.GetOrdinal("RejectionReason")),
            SubmittedBy = r.IsDBNull(r.GetOrdinal("SubmittedBy")) ? null : r.GetGuid(r.GetOrdinal("SubmittedBy")),
            CreatedAt = r.IsDBNull(r.GetOrdinal("CreatedAt")) ? DateTime.UtcNow : r.GetDateTime(r.GetOrdinal("CreatedAt"))
        };
    }

    // BuildSearchSql / BuildCountSql / BuildLetterCountsSql are simplified: reuse the existing SQL from ProductRepository
    private string BuildSearchSql(ProductSearchRequest request, out object? parameters)
    {
        parameters = new { /* TODO: map parameters from request */ };
        // For brevity, use a simple SQL here - real implementation should mirror ProductRepository.SearchFromDbAsync
        return $"SELECT * FROM Product WHERE IsDeleted = 0";
    }

    private string BuildCountSql(ProductSearchRequest request, out object? parameters)
    {
        parameters = new { };
        return $"SELECT COUNT(*) FROM Product WHERE IsDeleted = 0";
    }

    private string BuildLetterCountsSql(ProductSearchRequest request, out object? parameters)
    {
        parameters = new { };
        return $@"SELECT UPPER(SUBSTRING(Name,1,1)) AS FirstLetter, COUNT(*) AS ProductCount FROM Product WHERE IsDeleted = 0 GROUP BY UPPER(SUBSTRING(Name,1,1)) ORDER BY FirstLetter";
    }

    // Internal lightweight DalBase implementation to call protected Execute* methods
    private class InternalDalBase : SqlServerDalBase<ProductEntity, ProductDatabaseConnection>
    {
        public InternalDalBase(ProductDatabaseConnection connection, ILogger logger, IDbConnectionFactory connectionFactory, DatabaseRetryPolicy retryPolicy)
            : base(connection, logger, connectionFactory, retryPolicy)
        {
        }

        // Expose protected methods publicly
        public Task<List<TResult>> ExecuteQueryPublicAsync<TResult>(string sql, Func<System.Data.IDataReader, TResult> mapper, object? parameters = null, System.Data.IDbTransaction? transaction = null, CancellationToken cancellationToken = default)
        {
            return base.ExecuteQueryAsync<TResult>(sql, mapper, parameters, transaction, cancellationToken);
        }

        public Task<TResult?> ExecuteScalarPublicAsync<TResult>(string sql, object? parameters = null, System.Data.IDbTransaction? transaction = null, CancellationToken cancellationToken = default)
        {
            return base.ExecuteScalarAsync<TResult>(sql, parameters, transaction, cancellationToken);
        }
    }
}
