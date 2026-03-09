using System.Text.Json;
using ExpressRecipe.Client.Shared.Models.User;

namespace ExpressRecipe.Client.Shared.Services;

/// <summary>
/// API client for user settings management.
/// </summary>
public interface IUserSettingsApiClient
{
    Task<Dictionary<string, object?>> GetSettingsAsync(string group);
    Task<bool> UpdateSettingsAsync(string group, Dictionary<string, object?> values);
    Task<bool> ResetSettingsAsync(string group);
    Task<List<UserSettingsDto>> GetUserSettingsAsync();
    Task<bool> UpdateUserSettingsAsync(string group, Dictionary<string, object?> values);
}

public class UserSettingsApiClient : ApiClientBase, IUserSettingsApiClient
{
    // Known groups with their schema labels and setting definitions
    private static readonly UserSettingsSchemaDto DisplaySchema = new()
    {
        Group = "display",
        Label = "Display Preferences",
        Settings =
        [
            new SettingDefinition
            {
                Key = "numberFormat",
                Label = "Number Format",
                Type = "select",
                Default = "Fraction",
                Options =
                [
                    new SettingOption { Value = "Fraction", Label = "Fractions (1\u00bd)" },
                    new SettingOption { Value = "Decimal", Label = "Decimals (1.5)" }
                ]
            },
            new SettingDefinition
            {
                Key = "unitSystem",
                Label = "Unit System",
                Type = "select",
                Default = "US",
                Options =
                [
                    new SettingOption { Value = "US", Label = "US" },
                    new SettingOption { Value = "Metric", Label = "Metric" },
                    new SettingOption { Value = "UK", Label = "UK" }
                ]
            }
        ]
    };

    private static readonly UserSettingsSchemaDto[] KnownSchemas = [DisplaySchema];

    public UserSettingsApiClient(HttpClient httpClient, ITokenProvider tokenProvider)
        : base(httpClient, tokenProvider) { }

    public async Task<Dictionary<string, object?>> GetSettingsAsync(string group)
    {
        Dictionary<string, object?>? result =
            await GetAsync<Dictionary<string, object?>>($"/api/users/settings/{group}");
        return result ?? new Dictionary<string, object?>();
    }

    public async Task<bool> UpdateSettingsAsync(string group, Dictionary<string, object?> values)
        => await PutAsync($"/api/users/settings/{group}", values);

    public async Task<bool> ResetSettingsAsync(string group)
        => await DeleteAsync($"/api/users/settings/{group}");

    public async Task<List<UserSettingsDto>> GetUserSettingsAsync()
    {
        List<UserSettingsDto> result = [];
        foreach (UserSettingsSchemaDto schema in KnownSchemas)
        {
            Dictionary<string, object?> values = await GetSettingsAsync(schema.Group);
            result.Add(new UserSettingsDto { Schema = schema, Values = values });
        }
        return result;
    }

    public async Task<bool> UpdateUserSettingsAsync(string group, Dictionary<string, object?> values)
        => await UpdateSettingsAsync(group, values);
}
