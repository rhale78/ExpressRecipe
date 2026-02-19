using ExpressRecipe.Client.Shared.Models.Recipe;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ExpressRecipe.BlazorWeb.Services;

/// <summary>
/// Service for exporting/importing recipes to/from disk in Markdown format
/// Handles auto-save, auto-load, and sync functionality
/// </summary>
public class RecipeFileService
{
    private readonly string _baseExportPath;
    private readonly ILogger<RecipeFileService> _logger;

    public RecipeFileService(IConfiguration config, ILogger<RecipeFileService> logger)
    {
        _logger = logger;
        
        // Get base path from configuration or use default
        var configPath = config["RecipeExport:BasePath"];
        if (!string.IsNullOrEmpty(configPath))
        {
            _baseExportPath = configPath;
        }
        else
        {
            _baseExportPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ExpressRecipe",
                "RecipeExports"
            );
        }
        
        EnsureDirectoryExists(_baseExportPath);
        _logger.LogInformation("RecipeFileService initialized with base path: {BasePath}", _baseExportPath);
    }

    /// <summary>
    /// Export a recipe to disk in Markdown format
    /// </summary>
    public async Task<string> ExportRecipeAsync(RecipeDto recipe, string userIdentifier)
    {
        try
        {
            var userDir = GetUserDirectory(userIdentifier);
            EnsureDirectoryExists(userDir);

            var fileName = SanitizeFileName($"{recipe.Title}_{recipe.Id}.md");
            var filePath = Path.Combine(userDir, fileName);

            var markdown = ConvertRecipeToMarkdown(recipe);
            await File.WriteAllTextAsync(filePath, markdown);

            _logger.LogInformation("Exported recipe {RecipeId} ({Title}) to {FilePath}", 
                recipe.Id, recipe.Title, filePath);
            
            return filePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting recipe {RecipeId}", recipe.Id);
            throw;
        }
    }

    /// <summary>
    /// Import a recipe from a markdown file on disk
    /// </summary>
    public async Task<RecipeDto?> ImportRecipeFromDiskAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("File not found: {FilePath}", filePath);
                return null;
            }

            var markdown = await File.ReadAllTextAsync(filePath);
            var recipe = ParseMarkdownToRecipe(markdown);
            
            if (recipe != null)
            {
                _logger.LogInformation("Imported recipe from {FilePath}", filePath);
            }
            
            return recipe;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing recipe from {FilePath}", filePath);
            return null;
        }
    }

    /// <summary>
    /// Scan user's directory for all recipe files
    /// </summary>
    public async Task<List<string>> ScanUserRecipesAsync(string userIdentifier)
    {
        var userDir = GetUserDirectory(userIdentifier);
        if (!Directory.Exists(userDir))
        {
            return new List<string>();
        }

        try
        {
            var files = Directory.GetFiles(userDir, "*.md", SearchOption.TopDirectoryOnly)
                .Where(f => !f.Contains("_deleted"))
                .ToList();
            
            _logger.LogInformation("Found {Count} recipe files for user {User}", files.Count, userIdentifier);
            return files;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning recipes for user {User}", userIdentifier);
            return new List<string>();
        }
    }

    /// <summary>
    /// Delete a recipe file (moves to _deleted subfolder)
    /// </summary>
    public async Task DeleteRecipeFileAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return;
            }

            // Move to _deleted subfolder with timestamp
            var directory = Path.GetDirectoryName(filePath);
            if (directory == null) return;
            
            var deletedDir = Path.Combine(directory, "_deleted");
            EnsureDirectoryExists(deletedDir);

            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var fileName = Path.GetFileName(filePath);
            var deletedPath = Path.Combine(deletedDir, $"{timestamp}_{fileName}");

            File.Move(filePath, deletedPath);
            _logger.LogInformation("Moved deleted recipe to {DeletedPath}", deletedPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting recipe file {FilePath}", filePath);
        }
    }

    /// <summary>
    /// Export all recipes for a user
    /// </summary>
    public async Task<int> ExportAllRecipesAsync(List<RecipeDto> recipes, string userIdentifier)
    {
        int successCount = 0;
        foreach (var recipe in recipes)
        {
            try
            {
                await ExportRecipeAsync(recipe, userIdentifier);
                successCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export recipe {RecipeId}", recipe.Id);
            }
        }
        
        _logger.LogInformation("Exported {Success}/{Total} recipes for user {User}", 
            successCount, recipes.Count, userIdentifier);
        
        return successCount;
    }

    /// <summary>
    /// Get the export path for a user
    /// </summary>
    public string GetUserExportPath(string userIdentifier)
    {
        return GetUserDirectory(userIdentifier);
    }

    #region Private Helper Methods

    private string SanitizeFileName(string fileName)
    {
        // Remove or replace invalid characters
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Select(c => invalid.Contains(c) ? '_' : c).ToArray());

        // Additional replacements for problematic characters
        sanitized = sanitized
            .Replace(':', '_')
            .Replace('*', '_')
            .Replace('?', '_')
            .Replace('"', '_')
            .Replace('<', '_')
            .Replace('>', '_')
            .Replace('|', '_')
            .Replace('/', '_')
            .Replace('\\', '_');

        // Limit length (leave room for extension)
        if (sanitized.Length > 200)
        {
            sanitized = sanitized.Substring(0, 200);
        }

        return sanitized;
    }

    private string GetUserDirectory(string userIdentifier)
    {
        var sanitizedUser = SanitizeFileName(userIdentifier);
        return Path.Combine(_baseExportPath, sanitizedUser);
    }

    private void EnsureDirectoryExists(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }

    private string ConvertRecipeToMarkdown(RecipeDto recipe)
    {
        var sb = new StringBuilder();
        
        // YAML frontmatter
        sb.AppendLine("---");
        sb.AppendLine($"id: {recipe.Id}");
        sb.AppendLine($"title: \"{EscapeYaml(recipe.Title)}\"");
        sb.AppendLine($"created: {recipe.CreatedAt:O}");
        if (recipe.UpdatedAt.HasValue)
            sb.AppendLine($"updated: {recipe.UpdatedAt:O}");
        if (!string.IsNullOrEmpty(recipe.Difficulty))
            sb.AppendLine($"difficulty: {recipe.Difficulty}");
        if (recipe.Servings > 0)
            sb.AppendLine($"servings: {recipe.Servings}");
        if (recipe.PrepTimeMinutes > 0)
            sb.AppendLine($"prep_time: {recipe.PrepTimeMinutes}");
        if (recipe.CookTimeMinutes > 0)
            sb.AppendLine($"cook_time: {recipe.CookTimeMinutes}");
        if (recipe.Tags?.Any() == true)
            sb.AppendLine($"tags: [{string.Join(", ", recipe.Tags.Select(t => $"\"{EscapeYaml(t)}\""))}]");
        sb.AppendLine("---");
        sb.AppendLine();

        // Title
        sb.AppendLine($"# {recipe.Title}");
        sb.AppendLine();

        // Description
        if (!string.IsNullOrEmpty(recipe.Description))
        {
            sb.AppendLine(recipe.Description);
            sb.AppendLine();
        }

        // Ingredients
        if (recipe.Ingredients?.Any() == true)
        {
            sb.AppendLine("## Ingredients");
            sb.AppendLine();
            
            // Group by section if available
            var grouped = recipe.Ingredients.GroupBy(i => i.GroupName ?? "");
            foreach (var group in grouped.OrderBy(g => g.Key))
            {
                if (!string.IsNullOrEmpty(group.Key))
                {
                    sb.AppendLine($"### {group.Key}");
                    sb.AppendLine();
                }
                
                foreach (var ingredient in group.OrderBy(i => i.OrderIndex))
                {
                    var quantity = ingredient.Quantity > 0 ? ingredient.Quantity.ToString("0.##") : "";
                    var unit = ingredient.Unit ?? "";
                    var name = ingredient.Name ?? "";
                    var notes = ingredient.Notes ?? "";
                    var optional = ingredient.IsOptional ? " (optional)" : "";
                    
                    var line = $"- {quantity} {unit} {name}".Trim();
                    if (!string.IsNullOrEmpty(notes))
                    {
                        line += $", {notes}";
                    }
                    line += optional;
                    
                    sb.AppendLine(line);
                }
                sb.AppendLine();
            }
        }

        // Instructions
        if (recipe.Steps?.Any() == true)
        {
            sb.AppendLine("## Instructions");
            sb.AppendLine();
            foreach (var step in recipe.Steps.OrderBy(s => s.OrderIndex))
            {
                sb.AppendLine($"{step.OrderIndex}. {step.Instruction}");
                if (step.DurationMinutes.HasValue && step.DurationMinutes > 0)
                {
                    sb.AppendLine($"   *({step.DurationMinutes} minutes)*");
                }
                sb.AppendLine();
            }
        }

        // Nutrition
        if (recipe.Nutrition != null)
        {
            sb.AppendLine("## Nutrition (per serving)");
            sb.AppendLine();
            if (recipe.Nutrition.Calories > 0)
                sb.AppendLine($"- **Calories**: {recipe.Nutrition.Calories}");
            if (recipe.Nutrition.Protein > 0)
                sb.AppendLine($"- **Protein**: {recipe.Nutrition.Protein}g");
            if (recipe.Nutrition.Carbohydrates > 0)
                sb.AppendLine($"- **Carbohydrates**: {recipe.Nutrition.Carbohydrates}g");
            if (recipe.Nutrition.Fat > 0)
                sb.AppendLine($"- **Fat**: {recipe.Nutrition.Fat}g");
            if (recipe.Nutrition.Fiber > 0)
                sb.AppendLine($"- **Fiber**: {recipe.Nutrition.Fiber}g");
            if (recipe.Nutrition.Sugar > 0)
                sb.AppendLine($"- **Sugar**: {recipe.Nutrition.Sugar}g");
            if (recipe.Nutrition.Sodium > 0)
                sb.AppendLine($"- **Sodium**: {recipe.Nutrition.Sodium}mg");
            sb.AppendLine();
        }

        // Source
        if (!string.IsNullOrEmpty(recipe.SourceUrl))
        {
            sb.AppendLine("## Source");
            sb.AppendLine();
            sb.AppendLine($"[{recipe.Source ?? "Original Recipe"}]({recipe.SourceUrl})");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private RecipeDto? ParseMarkdownToRecipe(string markdown)
    {
        try
        {
            var recipe = new RecipeDto();
            
            // Extract YAML frontmatter
            var frontmatterMatch = Regex.Match(markdown, @"^---\s*\n(.*?)\n---", RegexOptions.Singleline);
            if (frontmatterMatch.Success)
            {
                var frontmatter = frontmatterMatch.Groups[1].Value;
                var yamlLines = frontmatter.Split('\n');
                
                foreach (var line in yamlLines)
                {
                    var parts = line.Split(new[] { ':' }, 2);
                    if (parts.Length != 2) continue;
                    
                    var key = parts[0].Trim();
                    var value = parts[1].Trim().Trim('"');
                    
                    switch (key.ToLower())
                    {
                        case "id":
                            if (Guid.TryParse(value, out var id))
                                recipe.Id = id;
                            break;
                        case "title":
                            recipe.Title = value;
                            break;
                        case "difficulty":
                            recipe.Difficulty = value;
                            break;
                        case "servings":
                            if (int.TryParse(value, out var servings))
                                recipe.Servings = servings;
                            break;
                        case "prep_time":
                            if (int.TryParse(value, out var prepTime))
                                recipe.PrepTimeMinutes = prepTime;
                            break;
                        case "cook_time":
                            if (int.TryParse(value, out var cookTime))
                                recipe.CookTimeMinutes = cookTime;
                            break;
                        case "created":
                            if (DateTime.TryParse(value, out var created))
                                recipe.CreatedAt = created;
                            break;
                        case "updated":
                            if (DateTime.TryParse(value, out var updated))
                                recipe.UpdatedAt = updated;
                            break;
                    }
                }
                
                // Remove frontmatter from markdown for further parsing
                markdown = markdown.Substring(frontmatterMatch.Length).Trim();
            }
            
            // Parse content sections (basic implementation - can be enhanced)
            var descriptionMatch = Regex.Match(markdown, @"^#\s+.*?\n\n(.*?)(?=\n##|\z)", RegexOptions.Singleline);
            if (descriptionMatch.Success)
            {
                recipe.Description = descriptionMatch.Groups[1].Value.Trim();
            }
            
            return recipe;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing markdown to recipe");
            return null;
        }
    }

    private string EscapeYaml(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Replace("\"", "\\\"");
    }

    #endregion
}
