using Microsoft.Extensions.Configuration;

namespace ExpressRecipe.Shared.Services;

/// <summary>
/// Checked by feature gate — when true, ALL gates are bypassed (local dev/desktop install).
/// </summary>
public interface ILocalModeConfig
{
    bool IsLocalMode { get; }
}

/// <summary>
/// Reads APP_LOCAL_MODE from configuration/environment.
/// Set APP_LOCAL_MODE=true in .env.local or Aspire launchSettings for local development.
/// </summary>
public sealed class LocalModeConfig : ILocalModeConfig
{
    public bool IsLocalMode { get; }

    public LocalModeConfig(IConfiguration config)
    {
        string? envVal = config["APP_LOCAL_MODE"];
        IsLocalMode = string.Equals(envVal, "true", StringComparison.OrdinalIgnoreCase);
    }
}
