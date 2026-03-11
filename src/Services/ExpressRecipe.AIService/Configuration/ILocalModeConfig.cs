namespace ExpressRecipe.AIService.Configuration;

public interface ILocalModeConfig
{
    bool IsLocalMode { get; }
}

public sealed class LocalModeConfig : ILocalModeConfig
{
    public bool IsLocalMode { get; }

    public LocalModeConfig(IConfiguration config)
    {
        string? envValue = config["APP_LOCAL_MODE"]
                           ?? Environment.GetEnvironmentVariable("APP_LOCAL_MODE");
        IsLocalMode = string.Equals(envValue, "true", StringComparison.OrdinalIgnoreCase);
    }
}
