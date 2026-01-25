using ExpressRecipe.Shared.DTOs.Product;
using HighSpeedDAL.Core.InMemoryTable;
using Microsoft.Extensions.Logging;
using ExpressRecipe.ProductService.Entities;

namespace ExpressRecipe.ProductService.Data
{
    /// <summary>
    /// Adapter for complex search operations.
    /// STRICTLY adheres to "No Manual SQL" policy by querying the InMemoryTable directly.
    /// </summary>
    public class ProductSearchAdapter
    {
        private readonly InMemoryTable<ProductEntity> _memoryTable;
        private readonly ILogger<ProductSearchAdapter> _logger;

        public ProductSearchAdapter(
            InMemoryTableManager tableManager,
            ILogger<ProductSearchAdapter> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Resolve the InMemoryTable for ProductEntity
            // Assuming "Product" is the table name from the [Table] attribute
            _memoryTable = tableManager.GetTable<ProductEntity>("Product") 
                ?? throw new InvalidOperationException("InMemoryTable for 'Product' is not registered. Ensure ProductEntityDal is initialized.");
        }

        public Task<List<ProductDto>> SearchAsync(ProductSearchRequest request)
        {
            // Build LINQ predicate based on request
            Func<ProductEntity, bool> predicate = p =>
            {
                if (p.IsDeleted) return false;
                if (request.OnlyApproved == true && p.ApprovalStatus != "Approved") return false;
                if (!string.IsNullOrWhiteSpace(request.Category) && !string.Equals(p.Category, request.Category, StringComparison.OrdinalIgnoreCase)) return false;
                if (!string.IsNullOrWhiteSpace(request.Brand) && !string.Equals(p.Brand, request.Brand, StringComparison.OrdinalIgnoreCase)) return false;
                if (!string.IsNullOrWhiteSpace(request.Barcode) && !string.Equals(p.Barcode, request.Barcode, StringComparison.OrdinalIgnoreCase)) return false;
                
                if (!string.IsNullOrWhiteSpace(request.FirstLetter))
                {
                    if (string.IsNullOrEmpty(p.Name)) return false;
                    char first = char.ToUpperInvariant(p.Name[0]);
                    if (char.IsDigit(request.FirstLetter[0]))
                    {
                        if (!char.IsDigit(first)) return false;
                    }
                    else
                    {
                         if (first != char.ToUpperInvariant(request.FirstLetter[0])) return false;
                    }
                }

                if (!string.IsNullOrWhiteSpace(request.SearchTerm))
                {
                    // Simple contains check (case insensitive)
                    bool nameMatch = p.Name.Contains(request.SearchTerm, StringComparison.OrdinalIgnoreCase);
                    bool brandMatch = p.Brand?.Contains(request.SearchTerm, StringComparison.OrdinalIgnoreCase) ?? false;
                    bool descMatch = p.Description?.Contains(request.SearchTerm, StringComparison.OrdinalIgnoreCase) ?? false;
                    
                    if (!nameMatch && !brandMatch && !descMatch) return false;
                }
                
                return true;
            };

            // Execute Query in Memory (Instant)
            IEnumerable<ProductEntity> query = _memoryTable.Select(predicate);

            // Sorting & Paging
            if (!string.IsNullOrWhiteSpace(request.SortBy))
            {
                query = request.SortBy.ToLowerInvariant() switch
                {
                    "brand" => query.OrderBy(p => p.Brand).ThenBy(p => p.Name),
                    "created" => query.OrderByDescending(p => p.CreatedDate),
                    _ => query.OrderBy(p => p.Name)
                };
            }
            else
            {
                 // Default sort
                 query = query.OrderBy(p => p.Name);
            }

            List<ProductEntity> pagedResults = query
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToList();

            return Task.FromResult(pagedResults.Select(MapEntityToDto).ToList());
        }

        public Task<ProductDto?> GetByBarcodeAsync(string barcode)
        {
            return GetByBarcodeInternalAsync(barcode);
        }

        private async Task<ProductDto?> GetByBarcodeInternalAsync(string barcode)
        {
             // Use the O(1) cache lookup from InMemoryTable
             ProductEntity? entity = await _memoryTable.GetByPropertyAsync(nameof(ProductEntity.Barcode), barcode);
             return entity != null ? MapEntityToDto(entity) : null;
        }

        public async Task<ProductDto?> GetByExternalIdAsync(string source, string externalId)
        {
             // Fallback to LINQ scan (fast enough for memory)
             ProductEntity? entity = _memoryTable.Select(p => 
                string.Equals(p.ExternalSource, source, StringComparison.OrdinalIgnoreCase) && 
                string.Equals(p.ExternalId, externalId, StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault();
                
             return entity != null ? MapEntityToDto(entity) : null;
        }

        public Task<int> GetSearchCountAsync(ProductSearchRequest request)
        {
             Func<ProductEntity, bool> predicate = p =>
            {
                if (p.IsDeleted) return false;
                if (request.OnlyApproved == true && p.ApprovalStatus != "Approved") return false;
                if (!string.IsNullOrWhiteSpace(request.Category) && !string.Equals(p.Category, request.Category, StringComparison.OrdinalIgnoreCase)) return false;
                if (!string.IsNullOrWhiteSpace(request.Brand) && !string.Equals(p.Brand, request.Brand, StringComparison.OrdinalIgnoreCase)) return false;
                if (!string.IsNullOrWhiteSpace(request.Barcode) && !string.Equals(p.Barcode, request.Barcode, StringComparison.OrdinalIgnoreCase)) return false;
                
                if (!string.IsNullOrWhiteSpace(request.SearchTerm))
                {
                    bool nameMatch = p.Name.Contains(request.SearchTerm, StringComparison.OrdinalIgnoreCase);
                    bool brandMatch = p.Brand?.Contains(request.SearchTerm, StringComparison.OrdinalIgnoreCase) ?? false;
                    if (!nameMatch && !brandMatch) return false;
                }
                return true;
            };

            return Task.FromResult(_memoryTable.Select(predicate).Count());
        }

        public Task<Dictionary<string,int>> GetLetterCountsAsync(ProductSearchRequest request)
        {
             var counts = _memoryTable.Select()
                .Where(p => !p.IsDeleted && !string.IsNullOrEmpty(p.Name))
                .GroupBy(p => char.ToUpperInvariant(p.Name[0]).ToString())
                .ToDictionary(g => g.Key, g => g.Count());
                
            return Task.FromResult(counts);
        }

        public Task<IEnumerable<string>> GetExistingBarcodesAsync(IEnumerable<string> barcodes)
        {
            HashSet<string> barcodeSet = new HashSet<string>(barcodes, StringComparer.OrdinalIgnoreCase);
            return GetExistingBarcodesInternalAsync(barcodeSet);
        }

        private async Task<IEnumerable<string>> GetExistingBarcodesInternalAsync(HashSet<string> barcodeSet)
        {
            List<string> found = [];
            foreach(var barcode in barcodeSet)
            {
                var exists = await _memoryTable.GetByPropertyAsync(nameof(ProductEntity.Barcode), barcode);
                if (exists != null)
                {
                    found.Add(barcode);
                }
            }
            return found;
        }

        public Task<Dictionary<string, Guid>> GetProductIdsByBarcodesAsync(IEnumerable<string> barcodes)
        {
             Dictionary<string, Guid> result = new(StringComparer.OrdinalIgnoreCase);
             foreach(var barcode in barcodes)
             {
                 var entity = _memoryTable.GetByPropertyAsync(nameof(ProductEntity.Barcode), barcode).GetAwaiter().GetResult();
                 if (entity != null)
                 {
                     result[barcode] = entity.Id;
                 }
             }
             return Task.FromResult(result);
        }
        
        private static ProductDto MapEntityToDto(ProductEntity entity)
        {
            Guid? approvedBy = null;
            if (Guid.TryParse(entity.ApprovedBy, out Guid parsedApprovedBy))
            {
                approvedBy = parsedApprovedBy;
            }

            Guid? submittedBy = null;
            if (Guid.TryParse(entity.SubmittedBy, out Guid parsedSubmittedBy))
            {
                submittedBy = parsedSubmittedBy;
            }

            return new ProductDto
            {
                Id = entity.Id,
                Name = entity.Name ?? string.Empty,
                Brand = entity.Brand,
                Barcode = entity.Barcode,
                BarcodeType = entity.BarcodeType,
                Description = entity.Description,
                Category = entity.Category,
                ServingSize = entity.ServingSize,
                ServingUnit = entity.ServingUnit,
                ImageUrl = entity.ImageUrl,
                ApprovalStatus = entity.ApprovalStatus ?? "Pending",
                ApprovedBy = approvedBy,
                ApprovedAt = entity.ApprovedAt,
                RejectionReason = entity.RejectionReason,
                SubmittedBy = submittedBy,
                CreatedAt = entity.CreatedDate
            };
        }
    }
}