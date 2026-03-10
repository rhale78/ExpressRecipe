using ExpressRecipe.Shared.Services.FeatureGates;
using Microsoft.Extensions.Configuration;

namespace ExpressRecipe.ServiceDefaults.FeatureGates;

/// <summary>
/// Configuration-backed implementation of <see cref="ILocalModeConfig"/>.
/// Reads <c>LocalMode</c> from app configuration. Defaults to <c>false</c>.
/// </summary>
public sealed class LocalModeConfig : ILocalModeConfig
{
    public bool IsLocalMode { get; }

    public LocalModeConfig(IConfiguration configuration)
    {
        IsLocalMode = configuration.GetValue<bool>("LocalMode", false);
    }
}
