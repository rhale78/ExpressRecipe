using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;

namespace ExpressRecipe.MAUI.Services;

public class ToastService : IToastService
{
    public async Task ShowToast(string message, ToastDuration duration = ToastDuration.Short)
    {
        var toastDuration = duration == ToastDuration.Short ? CommunityToolkit.Maui.Core.ToastDuration.Short : CommunityToolkit.Maui.Core.ToastDuration.Long;

        var toast = Toast.Make(message, toastDuration);
        await toast.Show();
    }

    public async Task ShowSuccessToast(string message)
    {
        await ShowToast($"✅ {message}");
    }

    public async Task ShowErrorToast(string message)
    {
        await ShowToast($"❌ {message}", ToastDuration.Long);
    }

    public async Task ShowWarningToast(string message)
    {
        await ShowToast($"⚠️ {message}");
    }
}
