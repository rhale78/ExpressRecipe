using System.Security.Cryptography;
using System.Text;

namespace ExpressRecipe.RecipeService.Services;

/// <summary>
/// Service for downloading and storing recipe images
/// </summary>
public class ImageDownloadService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ImageDownloadService> _logger;
    private readonly string _imageStoragePath;
    private readonly bool _downloadEnabled;

    public ImageDownloadService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<ImageDownloadService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        // Get configuration
        _imageStoragePath = configuration["ImageStorage:Path"] ?? "images/recipes";
        _downloadEnabled = configuration.GetValue<bool>("ImageStorage:DownloadEnabled", false);

        // Ensure directory exists
        if (_downloadEnabled && !Directory.Exists(_imageStoragePath))
        {
            Directory.CreateDirectory(_imageStoragePath);
        }
    }

    /// <summary>
    /// Download image from URL and save locally
    /// Returns the local file path or original URL if download failed/disabled
    /// </summary>
    public async Task<string?> DownloadImageAsync(string? imageUrl, Guid recipeId)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return null;

        // If download is disabled, just return the original URL
        if (!_downloadEnabled)
        {
            _logger.LogDebug("Image download disabled, using original URL: {Url}", imageUrl);
            return imageUrl;
        }

        try
        {
            // Validate URL
            if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri))
            {
                _logger.LogWarning("Invalid image URL: {Url}", imageUrl);
                return imageUrl;
            }

            // Only download from HTTP/HTTPS
            if (uri.Scheme != "http" && uri.Scheme != "https")
            {
                _logger.LogWarning("Unsupported URL scheme: {Scheme}", uri.Scheme);
                return imageUrl;
            }

            _logger.LogInformation("Downloading image for recipe {RecipeId} from {Url}", recipeId, imageUrl);

            // Download image
            var response = await _httpClient.GetAsync(uri);
            response.EnsureSuccessStatusCode();

            // Get content type to determine extension
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
            var extension = GetImageExtension(contentType);

            // Generate filename
            var filename = $"{recipeId}_{GenerateHashFromUrl(imageUrl)}{extension}";
            var filePath = Path.Combine(_imageStoragePath, filename);

            // Save to disk
            using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await response.Content.CopyToAsync(fileStream);
            }

            _logger.LogInformation("Image downloaded successfully: {FilePath}", filePath);

            // Return the local file path (or relative path for serving)
            return $"/images/recipes/{filename}";
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to download image from {Url}", imageUrl);
            return imageUrl; // Fall back to original URL
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading image for recipe {RecipeId}", recipeId);
            return imageUrl; // Fall back to original URL
        }
    }

    /// <summary>
    /// Download multiple images in parallel
    /// </summary>
    public async Task<List<string?>> DownloadImagesAsync(List<string?> imageUrls, Guid recipeId)
    {
        var tasks = imageUrls.Select((url, index) =>
            DownloadImageAsync(url, recipeId));

        var results = await Task.WhenAll(tasks);
        return results.ToList();
    }

    /// <summary>
    /// Get image extension from content type
    /// </summary>
    private string GetImageExtension(string contentType)
    {
        return contentType.ToLower() switch
        {
            "image/jpeg" => ".jpg",
            "image/jpg" => ".jpg",
            "image/png" => ".png",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            "image/svg+xml" => ".svg",
            _ => ".jpg" // Default to JPEG
        };
    }

    /// <summary>
    /// Generate a short hash from URL for unique filenames
    /// </summary>
    private string GenerateHashFromUrl(string url)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(url));
        var hash = BitConverter.ToString(hashBytes).Replace("-", "").Substring(0, 16);
        return hash.ToLower();
    }

    /// <summary>
    /// Delete image file
    /// </summary>
    public async Task<bool> DeleteImageAsync(string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return false;

        try
        {
            // Only delete if it's a local file path
            if (imagePath.StartsWith("/images/recipes/"))
            {
                var filename = Path.GetFileName(imagePath);
                var filePath = Path.Combine(_imageStoragePath, filename);

                if (File.Exists(filePath))
                {
                    await Task.Run(() => File.Delete(filePath));
                    _logger.LogInformation("Deleted image: {FilePath}", filePath);
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting image: {ImagePath}", imagePath);
            return false;
        }
    }

    /// <summary>
    /// Validate if image URL is accessible
    /// </summary>
    public async Task<bool> ValidateImageUrlAsync(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return false;

        try
        {
            if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri))
                return false;

            var response = await _httpClient.SendAsync(
                new HttpRequestMessage(HttpMethod.Head, uri));

            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
