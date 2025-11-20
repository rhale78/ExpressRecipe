namespace ExpressRecipe.Client.Shared.Services;

public interface IToastService
{
    event Action<ToastMessage>? OnShow;
    void ShowSuccess(string message, string? title = null, int durationMs = 5000);
    void ShowError(string message, string? title = null, int durationMs = 7000);
    void ShowWarning(string message, string? title = null, int durationMs = 6000);
    void ShowInfo(string message, string? title = null, int durationMs = 5000);
}

public class ToastService : IToastService
{
    public event Action<ToastMessage>? OnShow;

    public void ShowSuccess(string message, string? title = null, int durationMs = 5000)
    {
        Show(new ToastMessage
        {
            Type = ToastType.Success,
            Title = title ?? "Success",
            Message = message,
            DurationMs = durationMs
        });
    }

    public void ShowError(string message, string? title = null, int durationMs = 7000)
    {
        Show(new ToastMessage
        {
            Type = ToastType.Error,
            Title = title ?? "Error",
            Message = message,
            DurationMs = durationMs
        });
    }

    public void ShowWarning(string message, string? title = null, int durationMs = 6000)
    {
        Show(new ToastMessage
        {
            Type = ToastType.Warning,
            Title = title ?? "Warning",
            Message = message,
            DurationMs = durationMs
        });
    }

    public void ShowInfo(string message, string? title = null, int durationMs = 5000)
    {
        Show(new ToastMessage
        {
            Type = ToastType.Info,
            Title = title ?? "Info",
            Message = message,
            DurationMs = durationMs
        });
    }

    private void Show(ToastMessage message)
    {
        OnShow?.Invoke(message);
    }
}

public class ToastMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public ToastType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int DurationMs { get; set; } = 5000;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum ToastType
{
    Success,
    Error,
    Warning,
    Info
}
