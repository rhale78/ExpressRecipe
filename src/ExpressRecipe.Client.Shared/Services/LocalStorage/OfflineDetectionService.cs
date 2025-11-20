using Microsoft.JSInterop;

namespace ExpressRecipe.Client.Shared.Services.LocalStorage;

/// <summary>
/// Service for detecting online/offline status
/// </summary>
public class OfflineDetectionService : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private DotNetObjectReference<OfflineDetectionService>? _objectReference;
    private IJSObjectReference? _module;

    public event Action<bool>? OnlineStatusChanged;

    public bool IsOnline { get; private set; } = true;

    public OfflineDetectionService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <summary>
    /// Initialize offline detection
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            _objectReference = DotNetObjectReference.Create(this);

            // Load JavaScript module for offline detection
            _module = await _jsRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./js/offline-detection.js");

            // Initialize and get current status
            IsOnline = await _module.InvokeAsync<bool>("initialize", _objectReference);
        }
        catch
        {
            // Assume online if detection fails
            IsOnline = true;
        }
    }

    /// <summary>
    /// Called from JavaScript when online status changes
    /// </summary>
    [JSInvokable]
    public void UpdateOnlineStatus(bool isOnline)
    {
        if (IsOnline != isOnline)
        {
            IsOnline = isOnline;
            OnlineStatusChanged?.Invoke(isOnline);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_module != null)
        {
            await _module.DisposeAsync();
        }

        _objectReference?.Dispose();
    }
}
