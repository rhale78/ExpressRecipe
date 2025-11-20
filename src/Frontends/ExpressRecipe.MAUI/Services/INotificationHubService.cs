namespace ExpressRecipe.MAUI.Services;

public interface INotificationHubService
{
    Task StartAsync();
    Task StopAsync();
    event Action<string>? OnNotificationReceived;
}

public interface ISyncHubService
{
    Task StartAsync();
    Task StopAsync();
    event Action<int>? OnSyncProgress;
}
