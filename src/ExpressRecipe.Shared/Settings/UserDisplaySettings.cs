using System.Text.Json;
using System.Text.Json.Serialization;
using ExpressRecipe.Shared.Units;

namespace ExpressRecipe.Shared.Settings;

/// <summary>
/// User display preference settings. Stored as JSON in the UserProfile.DisplaySettingsJson column.
/// </summary>
public sealed class UserDisplaySettings
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true
    };

    public NumberFormat NumberFormat { get; set; } = NumberFormat.Fraction;
    public UnitSystemPreference UnitSystem { get; set; } = UnitSystemPreference.US;

    public static UserDisplaySettings Defaults => new();

    public static UserDisplaySettings FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) { return Defaults; }
        try
        {
            return JsonSerializer.Deserialize<UserDisplaySettings>(json, SerializerOptions) ?? Defaults;
        }
        catch { return Defaults; }
    }

    public string ToJson() => JsonSerializer.Serialize(this, SerializerOptions);
}
