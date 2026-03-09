using System.Text.Json;
using System.Text.Json.Nodes;
using ExpressRecipe.Data.Common;
using Microsoft.Data.SqlClient;

namespace ExpressRecipe.UserService.Data;

public interface IUserSettingsRepository
{
    Task<Dictionary<string, object?>> GetAsync(Guid userId, string group, CancellationToken ct = default);
    Task UpsertAsync(Guid userId, string group, Dictionary<string, object?> values, CancellationToken ct = default);
    Task DeleteAsync(Guid userId, string group, CancellationToken ct = default);
    Task<UserSettingsSchemaRow?> GetSchemaAsync(string group, CancellationToken ct = default);
    Task<List<string>> ValidateAsync(string group, Dictionary<string, object?> values, CancellationToken ct = default);
}

public sealed record UserSettingsSchemaRow(string GroupName, string SchemaJson);

public sealed class UserSettingsRepository : SqlHelper, IUserSettingsRepository
{
    public UserSettingsRepository(string connectionString) : base(connectionString) { }

    /// <summary>
    /// Gets settings values for a user and group.
    /// Returns defaults from schema if no settings are stored.
    /// </summary>
    public async Task<Dictionary<string, object?>> GetAsync(Guid userId, string group, CancellationToken ct = default)
    {
        const string sql = "SELECT DisplaySettingsJson FROM UserProfile WHERE Id = @UserId AND IsDeleted = 0";

        List<string?> rows = await ExecuteReaderAsync(
            sql,
            r => GetString(r, "DisplaySettingsJson"),
            CreateParameter("@UserId", userId));

        string? json = rows.FirstOrDefault();
        Dictionary<string, object?> result = ParseGroupFromJson(json, group);

        // Fill in defaults from schema for any missing keys
        UserSettingsSchemaRow? schema = await GetSchemaAsync(group, ct);
        if (schema != null)
        {
            Dictionary<string, object?> defaults = GetDefaultsFromSchema(schema.SchemaJson);
            foreach ((string key, object? value) in defaults)
            {
                result.TryAdd(key, value);
            }
        }

        return result;
    }

    /// <summary>
    /// Upserts settings values for a user and group into DisplaySettingsJson.
    /// </summary>
    public async Task UpsertAsync(Guid userId, string group, Dictionary<string, object?> values, CancellationToken ct = default)
    {
        // Read current JSON
        const string selectSql = "SELECT DisplaySettingsJson FROM UserProfile WHERE Id = @UserId AND IsDeleted = 0";
        List<string?> rows = await ExecuteReaderAsync(
            selectSql,
            r => GetString(r, "DisplaySettingsJson"),
            CreateParameter("@UserId", userId));

        string? currentJson = rows.FirstOrDefault();
        JsonObject root = ParseRootJson(currentJson);

        // Merge new values into the group
        JsonObject groupObject = root[group] as JsonObject ?? new JsonObject();
        foreach ((string key, object? value) in values)
        {
            groupObject[key] = value == null ? null : JsonValue.Create(value.ToString()!);
        }
        root[group] = groupObject;

        string newJson = root.ToJsonString();

        const string updateSql = @"
            UPDATE UserProfile
            SET DisplaySettingsJson = @Json, UpdatedAt = GETUTCDATE()
            WHERE Id = @UserId AND IsDeleted = 0";

        await ExecuteNonQueryAsync(
            updateSql,
            CreateParameter("@Json", newJson),
            CreateParameter("@UserId", userId));
    }

    /// <summary>
    /// Removes the specified group from the user's DisplaySettingsJson.
    /// </summary>
    public async Task DeleteAsync(Guid userId, string group, CancellationToken ct = default)
    {
        const string selectSql = "SELECT DisplaySettingsJson FROM UserProfile WHERE Id = @UserId AND IsDeleted = 0";
        List<string?> rows = await ExecuteReaderAsync(
            selectSql,
            r => GetString(r, "DisplaySettingsJson"),
            CreateParameter("@UserId", userId));

        string? currentJson = rows.FirstOrDefault();
        JsonObject root = ParseRootJson(currentJson);
        root.Remove(group);
        string newJson = root.ToJsonString();

        const string updateSql = @"
            UPDATE UserProfile
            SET DisplaySettingsJson = @Json, UpdatedAt = GETUTCDATE()
            WHERE Id = @UserId AND IsDeleted = 0";

        await ExecuteNonQueryAsync(
            updateSql,
            CreateParameter("@Json", newJson),
            CreateParameter("@UserId", userId));
    }

    /// <summary>
    /// Gets the schema definition for a settings group.
    /// </summary>
    public async Task<UserSettingsSchemaRow?> GetSchemaAsync(string group, CancellationToken ct = default)
    {
        const string sql = "SELECT GroupName, SchemaJson FROM UserSettingsSchema WHERE GroupName = @Group";
        List<UserSettingsSchemaRow> rows = await ExecuteReaderAsync(
            sql,
            r => new UserSettingsSchemaRow(
                GetString(r, "GroupName") ?? group,
                GetString(r, "SchemaJson") ?? "{}"),
            CreateParameter("@Group", group));
        return rows.FirstOrDefault();
    }

    /// <summary>
    /// Validates settings values against the schema.
    /// Returns list of validation error strings (empty list = valid).
    /// Unknown keys (not present in schema) are also rejected.
    /// </summary>
    public async Task<List<string>> ValidateAsync(string group, Dictionary<string, object?> values, CancellationToken ct = default)
    {
        UserSettingsSchemaRow? schemaRow = await GetSchemaAsync(group, ct);
        if (schemaRow is null) { return ["Unknown settings group"]; }

        List<string> errors = [];
        try
        {
            JsonDocument doc = JsonDocument.Parse(schemaRow.SchemaJson);
            if (!doc.RootElement.TryGetProperty("settings", out JsonElement settingsEl)) { return errors; }

            // Build set of known keys from schema
            HashSet<string> knownKeys = new(StringComparer.OrdinalIgnoreCase);
            foreach (JsonElement settingDef in settingsEl.EnumerateArray())
            {
                if (settingDef.TryGetProperty("key", out JsonElement kEl)) { knownKeys.Add(kEl.GetString() ?? ""); }
            }

            // Reject keys not present in schema
            foreach (string key in values.Keys)
            {
                if (!knownKeys.Contains(key))
                {
                    errors.Add($"Unknown setting key '{key}'");
                }
            }

            foreach (JsonElement settingDef in settingsEl.EnumerateArray())
            {
                if (!settingDef.TryGetProperty("key", out JsonElement keyEl)) { continue; }
                string key = keyEl.GetString() ?? "";
                if (!values.TryGetValue(key, out object? rawValue)) { continue; }
                string value = rawValue?.ToString() ?? "";

                // Validate against allowed options for "select" type
                if (settingDef.TryGetProperty("type", out JsonElement typeEl) &&
                    typeEl.GetString() == "select" &&
                    settingDef.TryGetProperty("options", out JsonElement optionsEl))
                {
                    bool found = false;
                    foreach (JsonElement opt in optionsEl.EnumerateArray())
                    {
                        if (opt.TryGetProperty("value", out JsonElement optValue) &&
                            string.Equals(optValue.GetString(), value, StringComparison.OrdinalIgnoreCase))
                        {
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        errors.Add($"Invalid value '{value}' for setting '{key}'");
                    }
                }
                // Validate toggle type
                else if (settingDef.TryGetProperty("type", out JsonElement toggleTypeEl) &&
                         toggleTypeEl.GetString() == "toggle" &&
                         !bool.TryParse(value, out _))
                {
                    errors.Add($"Setting '{key}' must be a boolean value");
                }
            }
        }
        catch (JsonException)
        {
            errors.Add("Invalid settings schema");
        }

        return errors;
    }

    private static Dictionary<string, object?> ParseGroupFromJson(string? json, string group)
    {
        if (string.IsNullOrWhiteSpace(json)) { return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase); }
        try
        {
            JsonDocument doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty(group, out JsonElement groupEl) &&
                groupEl.ValueKind == JsonValueKind.Object)
            {
                Dictionary<string, object?> result = new(StringComparer.OrdinalIgnoreCase);
                foreach (JsonProperty prop in groupEl.EnumerateObject())
                {
                    result[prop.Name] = prop.Value.ValueKind == JsonValueKind.Null
                        ? null
                        : prop.Value.GetString();
                }
                return result;
            }
        }
        catch (JsonException) { }
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, object?> GetDefaultsFromSchema(string schemaJson)
    {
        Dictionary<string, object?> defaults = new(StringComparer.OrdinalIgnoreCase);
        try
        {
            JsonDocument doc = JsonDocument.Parse(schemaJson);
            if (doc.RootElement.TryGetProperty("settings", out JsonElement settingsEl))
            {
                foreach (JsonElement setting in settingsEl.EnumerateArray())
                {
                    if (setting.TryGetProperty("key", out JsonElement keyEl) &&
                        setting.TryGetProperty("default", out JsonElement defaultEl))
                    {
                        defaults[keyEl.GetString() ?? ""] = defaultEl.GetString();
                    }
                }
            }
        }
        catch (JsonException) { }
        return defaults;
    }

    private static JsonObject ParseRootJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) { return new JsonObject(); }
        try
        {
            return JsonNode.Parse(json) as JsonObject ?? new JsonObject();
        }
        catch (JsonException) { return new JsonObject(); }
    }
}
