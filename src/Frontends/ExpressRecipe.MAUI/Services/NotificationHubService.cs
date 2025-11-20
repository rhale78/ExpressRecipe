using Microsoft.AspNetCore.SignalR.Client;

namespace ExpressRecipe.MAUI.Services;

public class NotificationHubService : INotificationHubService
{
    private HubConnection? _hubConnection;
    private readonly IConfiguration _configuration;
    private readonly ITokenProvider _tokenProvider;
    private readonly ILogger<NotificationHubService> _logger;

    public event Action<string>? OnNotificationReceived;

    public NotificationHubService(
        IConfiguration configuration,
        ITokenProvider tokenProvider,
        ILogger<NotificationHubService> logger)
    {
        _configuration = configuration;
        _tokenProvider = tokenProvider;
        _logger = logger;
    }

    public async Task StartAsync()
    {
        try
        {
            var hubUrl = _configuration["ApiBaseUrl"] + "/hubs/notifications";
            var token = await _tokenProvider.GetTokenAsync();

            _hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUrl, options =>
                {
                    if (!string.IsNullOrEmpty(token))
                    {
                        options.AccessTokenProvider = () => Task.FromResult(token)!;
                    }
                })
                .WithAutomaticReconnect()
                .Build();

            _hubConnection.On<string>("ReceiveNotification", (message) =>
            {
                OnNotificationReceived?.Invoke(message);
            });

            await _hubConnection.StartAsync();
            _logger.LogInformation("NotificationHub connected");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error connecting to NotificationHub");
        }
    }

    public async Task StopAsync()
    {
        if (_hubConnection != null)
        {
            await _hubConnection.StopAsync();
            await _hubConnection.DisposeAsync();
        }
    }
}

public class SyncHubService : ISyncHubService
{
    private HubConnection? _hubConnection;
    private readonly IConfiguration _configuration;
    private readonly ITokenProvider _tokenProvider;
    private readonly ILogger<SyncHubService> _logger;

    public event Action<int>? OnSyncProgress;

    public SyncHubService(
        IConfiguration configuration,
        ITokenProvider tokenProvider,
        ILogger<SyncHubService> logger)
    {
        _configuration = configuration;
        _tokenProvider = tokenProvider;
        _logger = logger;
    }

    public async Task StartAsync()
    {
        try
        {
            var hubUrl = _configuration["ApiBaseUrl"] + "/hubs/sync";
            var token = await _tokenProvider.GetTokenAsync();

            _hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUrl, options =>
                {
                    if (!string.IsNullOrEmpty(token))
                    {
                        options.AccessTokenProvider = () => Task.FromResult(token)!;
                    }
                })
                .WithAutomaticReconnect()
                .Build();

            _hubConnection.On<int>("SyncProgress", (progress) =>
            {
                OnSyncProgress?.Invoke(progress);
            });

            await _hubConnection.StartAsync();
            _logger.LogInformation("SyncHub connected");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error connecting to SyncHub");
        }
    }

    public async Task StopAsync()
    {
        if (_hubConnection != null)
        {
            await _hubConnection.StopAsync();
            await _hubConnection.DisposeAsync();
        }
    }
}
