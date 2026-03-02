using System.Text.Json;
using System.Text.Json.Serialization;

namespace ExpressRecipe.RecipeService.Services;

public class RecipeJsonDto
{
    [JsonPropertyName("id")]
    public JsonElement Id { get; set; } // Can be int or string

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("ingredients")]
    public JsonElement Ingredients { get; set; }

    [JsonPropertyName("ingredients_csv")]
    public JsonElement IngredientsCsv { get; set; }

    [JsonPropertyName("directions")]
    public JsonElement Directions { get; set; }

    [JsonPropertyName("directions_csv")]
    public JsonElement DirectionsCsv { get; set; }

    [JsonPropertyName("ner")]
    public JsonElement Ner { get; set; }

    [JsonPropertyName("link")]
    public string? Link { get; set; }

    [JsonPropertyName("source")]
    public JsonElement Source { get; set; }

    [JsonPropertyName("cooking_time")]
    public JsonElement CookingTime { get; set; }

    [JsonPropertyName("servings")]
    public JsonElement Servings { get; set; }

    [JsonPropertyName("ratings")]
    public JsonElement Ratings { get; set; }

    [JsonPropertyName("tags")]
    public JsonElement Tags { get; set; }

    [JsonPropertyName("publish_date")]
    public string? PublishDate { get; set; }

    [JsonPropertyName("image")]
    public string? Image { get; set; }

    [JsonIgnore]
    public string? RawJsonText { get; set; }
}

public class RecipeRatingsDto
{
    [JsonPropertyName("rating")]
    public decimal? Rating { get; set; }

    [JsonPropertyName("count")]
    public int? Count { get; set; }
}
