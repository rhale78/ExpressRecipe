namespace ExpressRecipe.Client.Shared.Models.User;

/// <summary>
/// Schema definition for a settings group returned by the UserSettingsSchema table.
/// </summary>
public class UserSettingsSchemaDto
{
    public string Group { get; set; } = "";
    public string Label { get; set; } = "";
    public List<SettingDefinition> Settings { get; set; } = [];
}

/// <summary>
/// A single setting definition within a settings group.
/// </summary>
public class SettingDefinition
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public string Type { get; set; } = "select";
    public string? Default { get; set; }
    public List<SettingOption>? Options { get; set; }
    public SettingDependsOn? DependsOn { get; set; }
}

/// <summary>
/// An option for a select-type setting.
/// </summary>
public class SettingOption
{
    public string Value { get; set; } = "";
    public string Label { get; set; } = "";
}

/// <summary>
/// A conditional dependency: this setting is only shown when another setting has a specific value.
/// </summary>
public class SettingDependsOn
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}

/// <summary>
/// A combined DTO holding the schema and current values for a settings group.
/// </summary>
public class UserSettingsDto
{
    public UserSettingsSchemaDto Schema { get; set; } = new();
    public Dictionary<string, object?> Values { get; set; } = new();
}
