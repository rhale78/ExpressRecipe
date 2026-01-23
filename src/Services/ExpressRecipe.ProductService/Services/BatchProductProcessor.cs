using System.Threading.Tasks.Dataflow;
using ExpressRecipe.ProductService.Data;
using ExpressRecipe.Shared.DTOs.Product;
using Microsoft.Extensions.Logging;

namespace ExpressRecipe.ProductService.Services;

/// <summary>
/// Batch processor for products and ingredients using TPL Dataflow for efficient parallel processing
/// </summary>
public class BatchProductProcessor
{
    private readonly ILogger<BatchProductProcessor> _logger;
    private readonly IIngredientListParser _ingredientListParser;
    private readonly int _maxDegreeOfParallelism;
    private readonly int _batchSize;
    private readonly int _bufferSize;

    public BatchProductProcessor(
        ILogger<BatchProductProcessor> logger,
        IIngredientListParser ingredientListParser,
        int maxDegreeOfParallelism = 4,
        int batchSize = 100,
        int bufferSize = 500)
    {
        _logger = logger;
        _ingredientListParser = ingredientListParser;
        _maxDegreeOfParallelism = maxDegreeOfParallelism;
        _batchSize = batchSize;
        _bufferSize = bufferSize;
    }

    /// <summary>
    /// Process staged products using dataflow pipeline
    /// </summary>
    public async Task<ProcessingResult> ProcessStagedProductsAsync(
        IProductStagingRepository stagingRepo,
        IProductRepository productRepo,
        IIngredientRepository ingredientRepo,
        IProductImageRepository productImageRepo,
        CancellationToken cancellationToken = default)
    {
        var result = new ProcessingResult();

        // Get total pending count for progress tracking
        int pendingCount = await stagingRepo.GetPendingCountAsync();
        var progressTracker = new ProgressTracker(_logger, pendingCount);

        _logger.LogInformation("Starting batch processing of {Count} staged products", pendingCount);

        // Stage 1: Fetch pending products in batches
        var fetchBlock = new TransformManyBlock<int, StagedProduct>(
            async batchNumber =>
            {
                try
                {
                    var products = await stagingRepo.GetPendingProductsAsync(_batchSize);
                    _logger.LogDebug("Batch {BatchNumber}: Fetched {Count} products", batchNumber, products.Count);
                    return products;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to fetch batch {BatchNumber}", batchNumber);
                    return Enumerable.Empty<StagedProduct>();
                }
            },
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = 1, // Sequential fetching
                BoundedCapacity = 2
            });

        // Stage 2: Batch products for ingredient pre-creation
        var batchBlock = new BatchBlock<StagedProduct>(_batchSize, new GroupingDataflowBlockOptions
        {
            BoundedCapacity = _bufferSize
        });

        // Stage 3: Pre-create unique ingredients for each batch
        var preCreateIngredientsBlock = new TransformBlock<StagedProduct[], StagedProduct[]>(
            async batch =>
            {
                try
                {
                    await PreCreateBatchIngredientsAsync(batch, ingredientRepo, cancellationToken);
                    return batch;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to pre-create ingredients for batch");
                    return batch; // Continue processing even if pre-creation fails
                }
            },
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = 1,
                BoundedCapacity = _bufferSize
            });

        // Stage 4: Process entire batch at once using bulk operations
        var processBatchBlock = new ActionBlock<StagedProduct[]>(
            async batch =>
            {
                try
                {
                    var batchResult = await ProcessProductBatchAsync(batch, productRepo, ingredientRepo, productImageRepo, stagingRepo, cancellationToken);

                    Interlocked.Add(ref result.SuccessCount, batchResult.Success);
                    Interlocked.Add(ref result.FailureCount, batchResult.Failure);

                    // Update progress tracker with current counts
                    progressTracker.Update(result.SuccessCount, result.FailureCount);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process batch of {Count} products", batch.Length);
                    Interlocked.Add(ref result.FailureCount, batch.Length);

                    // Update progress tracker with failure
                    progressTracker.Update(result.SuccessCount, result.FailureCount);

                    // Mark all as failed
                    foreach (var product in batch)
                    {
                        try
                        {
                            await stagingRepo.UpdateProcessingStatusAsync(product.Id, "Failed", ex.Message);
                        }
                        catch { }
                    }
                }
            },
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = 2, // Process 2 batches concurrently
                BoundedCapacity = _bufferSize
            });

        // Link the pipeline
        var linkOptions = new DataflowLinkOptions { PropagateCompletion = true };
        fetchBlock.LinkTo(batchBlock, linkOptions);
        batchBlock.LinkTo(preCreateIngredientsBlock, linkOptions);
        preCreateIngredientsBlock.LinkTo(processBatchBlock, linkOptions);

        // Start feeding data
        int batchNumber = 0;

        while (pendingCount > 0 && !cancellationToken.IsCancellationRequested)
        {
            await fetchBlock.SendAsync(batchNumber++, cancellationToken);
            
            // Check if we need to continue
            await Task.Delay(100, cancellationToken);
            var currentPending = await stagingRepo.GetPendingCountAsync();
            
            if (currentPending == pendingCount)
            {
                // No progress, stop fetching
                break;
            }
            
            pendingCount = currentPending;
        }

        // Signal completion and wait for pipeline to finish
        fetchBlock.Complete();
        await processBatchBlock.Completion;

        // Log completion with final metrics
        progressTracker.LogCompletion();

        return result;
    }

    /// <summary>
    /// Process a batch of products using bulk database operations
    /// </summary>
    private async Task<(int Success, int Failure)> ProcessProductBatchAsync(
        StagedProduct[] batch,
        IProductRepository productRepo,
        IIngredientRepository ingredientRepo,
        IProductImageRepository productImageRepo,
        IProductStagingRepository stagingRepo,
        CancellationToken cancellationToken)
    {
        int success = 0;
        int failure = 0;

        // PERFORMANCE: Pre-fetch all ingredients for the entire batch upfront to avoid repeated lookups
        var batchIngredientCache = await PreFetchBatchIngredientsAsync(batch, ingredientRepo, cancellationToken);

        // Step 1: Check which barcodes already exist (batch lookup)
        var barcodes = batch
            .Where(p => !string.IsNullOrWhiteSpace(p.Barcode))
            .Select(p => p.Barcode!)
            .Distinct()
            .ToList();

        var existingProducts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (barcodes.Any())
        {
            var existingBarcodes = await productRepo.GetExistingBarcodesAsync(barcodes);
            existingProducts = new HashSet<string>(existingBarcodes, StringComparer.OrdinalIgnoreCase);
        }

        // Step 2: Filter to only new products and pre-generate IDs
        var newProducts = batch
            .Where(p => string.IsNullOrWhiteSpace(p.Barcode) || !existingProducts.Contains(p.Barcode!))
            .Select(p => new ProductWithId
            {
                ProductId = Guid.NewGuid(),
                StagedProduct = p,
                Request = new Shared.DTOs.Product.CreateProductRequest
                {
                    Name = p.ProductName ?? "Unknown Product",
                    Brand = p.Brands,
                    Barcode = p.Barcode,
                    BarcodeType = DetermineBarcodeType(p.Barcode),
                    Description = p.GenericName,
                    Category = ExtractPrimaryCategory(p.Categories),
                    ImageUrl = p.ImageUrl
                }
            })
            .ToList();

        if (!newProducts.Any())
        {
            // All products already exist - mark as completed in bulk
            var existingIds = batch.Select(p => p.Id).ToList();
            await stagingRepo.BulkUpdateProcessingStatusAsync(existingIds, "Completed");
            success = batch.Length;
            return (success, failure);
        }

        _logger.LogInformation("Bulk processing {NewCount} new products (skipping {ExistingCount} existing)",
            newProducts.Count, batch.Length - newProducts.Count);

        // Step 3: Bulk insert products
        var productRequests = newProducts.Select(p => p.Request).ToList();
        var insertedCount = await productRepo.BulkCreateAsync(productRequests);
        _logger.LogInformation("Bulk inserted {Count} products", insertedCount);

        // Step 4: Get the created product IDs by barcode lookup
        var createdBarcodes = newProducts
            .Where(p => !string.IsNullOrWhiteSpace(p.Request.Barcode))
            .Select(p => p.Request.Barcode!)
            .ToList();

        Dictionary<string, Guid> barcodeToId = new(StringComparer.OrdinalIgnoreCase);
        if (createdBarcodes.Any())
        {
            barcodeToId = await productRepo.GetProductIdsByBarcodesAsync(createdBarcodes);
        }

        // Map back to our products
        foreach (var product in newProducts)
        {
            if (!string.IsNullOrWhiteSpace(product.Request.Barcode) &&
                barcodeToId.TryGetValue(product.Request.Barcode, out var id))
            {
                product.ProductId = id;
            }
        }

        // Step 5: Bulk approve products (simplified - just update status)
        // For now, we'll do individual approvals since bulk approve isn't implemented
        // TODO: Add BulkApproveAsync to IProductRepository

        // Step 6: Bulk insert images
        var imageRequests = new List<ProductImageRequest>();
        foreach (var product in newProducts)
        {
            if (!string.IsNullOrWhiteSpace(product.StagedProduct.ImageUrl))
            {
                imageRequests.Add(new ProductImageRequest
                {
                    ProductId = product.ProductId,
                    ImageType = DetermineImageType(product.StagedProduct.ImageUrl),
                    ImageUrl = product.StagedProduct.ImageUrl,
                    IsPrimary = true,
                    DisplayOrder = 0,
                    SourceSystem = "OpenFoodFacts",
                    SourceId = product.StagedProduct.Barcode
                });
            }

            if (!string.IsNullOrWhiteSpace(product.StagedProduct.ImageSmallUrl) &&
                product.StagedProduct.ImageSmallUrl != product.StagedProduct.ImageUrl)
            {
                imageRequests.Add(new ProductImageRequest
                {
                    ProductId = product.ProductId,
                    ImageType = DetermineImageType(product.StagedProduct.ImageSmallUrl),
                    ImageUrl = product.StagedProduct.ImageSmallUrl,
                    IsPrimary = false,
                    DisplayOrder = 1,
                    SourceSystem = "OpenFoodFacts",
                    SourceId = product.StagedProduct.Barcode
                });
            }
        }

        if (imageRequests.Any())
        {
            await productImageRepo.BulkAddImagesAsync(imageRequests);
            _logger.LogInformation("Bulk inserted {Count} product images", imageRequests.Count);
        }

        // Step 7: Bulk insert product-ingredient links (using cached ingredients from batch)
        var ingredientLinks = new List<(Guid ProductId, Guid IngredientId, int OrderIndex)>();
        foreach (var product in newProducts)
        {
            var ingredientsText = product.StagedProduct.IngredientsTextEn ?? product.StagedProduct.IngredientsText;
            if (!string.IsNullOrWhiteSpace(ingredientsText))
            {
                var parsedIngredients = ParseIngredientsQuick(ingredientsText).Take(50).ToList();

                // PERFORMANCE: Use cached batch ingredients instead of another lookup
                int orderIndex = 0;
                foreach (var ingredientName in parsedIngredients)
                {
                    if (batchIngredientCache.TryGetValue(ingredientName, out var ingredientId))
                    {
                        ingredientLinks.Add((product.ProductId, ingredientId, orderIndex++));
                    }
                }
            }
        }

        if (ingredientLinks.Any())
        {
            await ingredientRepo.BulkAddProductIngredientsAsync(ingredientLinks);
            _logger.LogInformation("Bulk inserted {Count} product-ingredient links", ingredientLinks.Count);
        }

        // Step 8: Bulk insert external links
        var externalLinks = newProducts
            .Where(p => !string.IsNullOrWhiteSpace(p.StagedProduct.ExternalId))
            .Select(p => (p.ProductId, Source: "OpenFoodFacts", ExternalId: p.StagedProduct.ExternalId))
            .ToList();

        if (externalLinks.Any())
        {
            await productRepo.BulkAddExternalLinksAsync(externalLinks);
            _logger.LogInformation("Bulk inserted {Count} external links", externalLinks.Count);
        }

        // Step 9: Mark staging records as completed
        var completedIds = newProducts.Select(p => p.StagedProduct.Id).ToList();
        var skippedIds = batch
            .Where(p => !string.IsNullOrWhiteSpace(p.Barcode) && existingProducts.Contains(p.Barcode!))
            .Select(p => p.Id)
            .ToList();

        await stagingRepo.BulkUpdateProcessingStatusAsync(completedIds, "Completed");
        if (skippedIds.Any())
        {
            await stagingRepo.BulkUpdateProcessingStatusAsync(skippedIds, "Completed"); // Already existed
        }

        success = batch.Length;
        return (success, failure);
    }

    private class ProductWithId
    {
        public Guid ProductId { get; set; }
        public StagedProduct StagedProduct { get; set; } = null!;
        public Shared.DTOs.Product.CreateProductRequest Request { get; set; } = null!;
    }

    /// <summary>
    /// Pre-fetch all unique ingredients from a batch and return as cached dictionary.
    /// This avoids repeated GetAllAsync calls when processing individual products.
    /// </summary>
    private async Task<Dictionary<string, Guid>> PreFetchBatchIngredientsAsync(
        StagedProduct[] batch,
        IIngredientRepository ingredientRepo,
        CancellationToken cancellationToken)
    {
        // Collect all unique ingredient names from the batch
        var allIngredientNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var product in batch)
        {
            var ingredientsText = product.IngredientsTextEn ?? product.IngredientsText;
            if (!string.IsNullOrWhiteSpace(ingredientsText))
            {
                var ingredients = ParseIngredientsQuick(ingredientsText).Take(50).ToList();
                foreach (var ingredient in ingredients)
                {
                    if (!string.IsNullOrWhiteSpace(ingredient))
                    {
                        allIngredientNames.Add(ingredient);
                    }
                }
            }
        }

        if (!allIngredientNames.Any())
        {
            _logger.LogDebug("No ingredients found in batch");
            return new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        }

        _logger.LogDebug("Batch contains {Count} unique ingredients - pre-fetching once for entire batch", allIngredientNames.Count);

        // PERFORMANCE: Single lookup for all ingredients in the batch - cached for all products
        var ingredientIds = await ingredientRepo.GetIngredientIdsByNamesAsync(allIngredientNames);

        _logger.LogDebug("Pre-fetched {Count} ingredient IDs - will reuse for all {ProductCount} products in batch",
            ingredientIds.Count, batch.Length);

        return ingredientIds;
    }

    private async Task PreCreateBatchIngredientsAsync(
        StagedProduct[] stagedProducts,
        IIngredientRepository ingredientRepo,
        CancellationToken cancellationToken)
    {
        // Collect all unique ingredient names from the batch
        var allIngredientNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var product in stagedProducts)
        {
            var ingredientsText = product.IngredientsTextEn ?? product.IngredientsText;
            if (!string.IsNullOrWhiteSpace(ingredientsText))
            {
                var ingredients = ParseIngredientsQuick(ingredientsText);
                foreach (var ingredient in ingredients)
                {
                    if (!string.IsNullOrWhiteSpace(ingredient))
                    {
                        allIngredientNames.Add(ingredient);
                    }
                }
            }
        }

        if (!allIngredientNames.Any())
            return;

        _logger.LogDebug("Batch contains {Count} unique ingredients to check/create", allIngredientNames.Count);

        // PERFORMANCE FIX: Use bulk lookup instead of N individual queries
        var existingIngredients = await ingredientRepo.GetIngredientIdsByNamesAsync(allIngredientNames);

        // Create new ingredients in bulk
        var newIngredients = allIngredientNames
            .Except(existingIngredients.Keys, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (newIngredients.Any())
        {
            _logger.LogInformation("Bulk creating {Count} new ingredients", newIngredients.Count);

            // PERFORMANCE FIX: Use bulk create instead of individual inserts
            await ingredientRepo.BulkCreateIngredientsAsync(newIngredients);
        }
    }

    private async Task ProcessSingleProductAsync(
        StagedProduct stagedProduct,
        IProductRepository productRepo,
        IIngredientRepository ingredientRepo,
        IProductImageRepository productImageRepo,
        CancellationToken cancellationToken)
    {
        // Check if product already exists
        if (!string.IsNullOrWhiteSpace(stagedProduct.Barcode))
        {
            var existing = await productRepo.GetByBarcodeAsync(stagedProduct.Barcode);
            if (existing != null)
            {
                _logger.LogDebug("Product with barcode {Barcode} already exists, skipping", stagedProduct.Barcode);
                return;
            }
        }

        // Create product
        var productRequest = new Shared.DTOs.Product.CreateProductRequest
        {
            Name = stagedProduct.ProductName ?? "Unknown Product",
            Brand = stagedProduct.Brands,
            Barcode = stagedProduct.Barcode,
            BarcodeType = DetermineBarcodeType(stagedProduct.Barcode),
            Description = stagedProduct.GenericName,
            Category = ExtractPrimaryCategory(stagedProduct.Categories),
            ImageUrl = stagedProduct.ImageUrl
        };

        var productId = await productRepo.CreateAsync(productRequest);
        await productRepo.ApproveAsync(productId, true, Guid.Empty);

        // Save product images from staging table
        await SaveImagesFromStagingAsync(productId, stagedProduct, productImageRepo, cancellationToken);

        // Parse and link ingredients
        var ingredientsText = stagedProduct.IngredientsTextEn ?? stagedProduct.IngredientsText;
        if (!string.IsNullOrWhiteSpace(ingredientsText))
        {
            var parsedIngredients = ParseIngredientsQuick(ingredientsText).Take(50).ToList(); // Limit to 50 ingredients

            // PERFORMANCE FIX: Bulk lookup ingredient IDs instead of N queries
            var ingredientIds = await ingredientRepo.GetIngredientIdsByNamesAsync(parsedIngredients);

            int orderIndex = 0;
            foreach (var ingredientName in parsedIngredients)
            {
                if (cancellationToken.IsCancellationRequested) break;

                if (ingredientIds.TryGetValue(ingredientName, out var ingredientId))
                {
                    try
                    {
                        var addIngredientRequest = new AddProductIngredientRequest
                        {
                            IngredientId = ingredientId,
                            OrderIndex = orderIndex++,
                            IngredientListString = ingredientName
                        };
                        await ingredientRepo.AddProductIngredientAsync(productId, addIngredientRequest);
                    }
                    catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
                    {
                        // Duplicate constraint, skip
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to link ingredient '{Ingredient}' to product {ProductId}",
                            ingredientName, productId);
                    }
                }
            }
        }

        // Add allergens
        if (!string.IsNullOrWhiteSpace(stagedProduct.Allergens))
        {
            var allergens = stagedProduct.Allergens.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var allergen in allergens.Take(20))
            {
                try
                {
                    await productRepo.AddAllergenToProductAsync(productId, allergen.Trim());
                }
                catch { }
            }
        }

        // Store metadata
        if (!string.IsNullOrWhiteSpace(stagedProduct.NutritionData))
        {
            try
            {
                await productRepo.UpdateProductMetadataAsync(productId, "nutrition_json", stagedProduct.NutritionData);
            }
            catch { }
        }

        // Store external link
        await productRepo.AddExternalLinkAsync(productId, "OpenFoodFacts", stagedProduct.ExternalId);
    }

    private List<string> ParseIngredientsQuick(string ingredientsText)
    {
        // Use the injected advanced parser for consistent, high-quality parsing
        return _ingredientListParser.ParseIngredients(ingredientsText);
    }

    private static string? DetermineBarcodeType(string? barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode))
            return null;

        return barcode.Length switch
        {
            8 => "EAN-8",
            12 => "UPC-A",
            13 => "EAN-13",
            14 => "ITF-14",
            _ => "Unknown"
        };
    }

    private static string? ExtractPrimaryCategory(string? categories)
    {
        if (string.IsNullOrWhiteSpace(categories))
            return "General";

        var parts = categories.Split(',', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0].Trim() : "General";
    }

    /// <summary>
    /// Save images from staging table to ProductImage table
    /// Automatically detects image type (Front, Nutrition, Ingredients, Packaging) from URL pattern
    /// Saves both display (400px) and small (200px) variants when available
    /// </summary>
    private async Task SaveImagesFromStagingAsync(
        Guid productId,
        StagedProduct stagedProduct,
        IProductImageRepository productImageRepo,
        CancellationToken cancellationToken)
    {
        var displayOrder = 0;
        var imagesSaved = 0;

        try
        {
            // Determine image type from URL pattern (Front, Nutrition, Ingredients, Packaging)
            var imageType = DetermineImageType(stagedProduct.ImageUrl);

            // Save main image URL (display size, typically 400px)
            if (!string.IsNullOrWhiteSpace(stagedProduct.ImageUrl))
            {
                var isPrimary = imageType == "Front"; // Only front images are primary

                await productImageRepo.AddImageAsync(
                    productId: productId,
                    imageType: imageType,
                    imageUrl: stagedProduct.ImageUrl,
                    localFilePath: null,
                    fileName: null,
                    fileSize: null,
                    mimeType: "image/jpeg",
                    width: null,
                    height: null,
                    isPrimary: isPrimary,
                    displayOrder: displayOrder++,
                    isUserUploaded: false,
                    sourceSystem: "OpenFoodFacts",
                    sourceId: stagedProduct.Barcode,
                    userId: null
                );
                imagesSaved++;
                _logger.LogDebug("Saved {ImageType} image (display, primary={IsPrimary}) for product {ProductId} from staging",
                    imageType, isPrimary, productId);
            }

            // Save small image URL if different from main (thumbnail size, typically 200px)
            if (!string.IsNullOrWhiteSpace(stagedProduct.ImageSmallUrl) &&
                stagedProduct.ImageSmallUrl != stagedProduct.ImageUrl)
            {
                await productImageRepo.AddImageAsync(
                    productId: productId,
                    imageType: imageType,
                    imageUrl: stagedProduct.ImageSmallUrl,
                    localFilePath: null,
                    fileName: null,
                    fileSize: null,
                    mimeType: "image/jpeg",
                    width: null,
                    height: null,
                    isPrimary: false,
                    displayOrder: displayOrder++,
                    isUserUploaded: false,
                    sourceSystem: "OpenFoodFacts",
                    sourceId: stagedProduct.Barcode,
                    userId: null
                );
                imagesSaved++;
                _logger.LogDebug("Saved {ImageType} image (small) for product {ProductId} from staging",
                    imageType, productId);
            }

            if (imagesSaved > 0)
            {
                _logger.LogDebug("Saved {Count} image(s) for product {ProductId} from staging table",
                    imagesSaved, productId);
            }
        }
        catch (Exception ex)
        {
           _logger.LogWarning(ex, "Failed to save images for product {ProductId} from staging", productId);
            // Don't throw - continue processing the product even if image save fails
        }
    }

    /// <summary>
    /// Determine image type from URL pattern
    /// OpenFoodFacts URLs contain indicators like: front_en.123.400.jpg, nutrition_fr.456.200.jpg
    /// </summary>
    private static string DetermineImageType(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return "Front";

        var urlLower = imageUrl.ToLowerInvariant();

        if (urlLower.Contains("nutrition"))
            return "Nutrition";
        if (urlLower.Contains("ingredient"))
            return "Ingredients";
        if (urlLower.Contains("packaging"))
            return "Packaging";

        // Default to Front for unrecognized or "front" URLs
        return "Front";
    }
}

public class ProcessingResult
{
    public int SuccessCount;
    public int FailureCount;
}
