namespace ExpressRecipe.Shared.Services.FeatureGates;

/// <summary>
/// Indicates whether the application is running in local (offline-first) mode.
/// When <see cref="IsLocalMode"/> is <c>true</c>, feature and tier gate checks are bypassed.
/// </summary>
public interface ILocalModeConfig
{
    /// <summary>
    /// <c>true</c> when the service is running without cloud connectivity
    /// (e.g. a developer workstation with no UserService reachable).
    /// </summary>
    bool IsLocalMode { get; }
}
