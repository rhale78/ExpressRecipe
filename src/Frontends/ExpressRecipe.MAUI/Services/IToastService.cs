namespace ExpressRecipe.MAUI.Services;

public interface IToastService
{
    Task ShowToast(string message, ToastDuration duration = ToastDuration.Short);
    Task ShowSuccessToast(string message);
    Task ShowErrorToast(string message);
    Task ShowWarningToast(string message);
}

public enum ToastDuration
{
    Short,
    Long
}
