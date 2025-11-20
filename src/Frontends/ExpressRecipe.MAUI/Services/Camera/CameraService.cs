namespace ExpressRecipe.MAUI.Services.Camera;

/// <summary>
/// Implementation of camera service using MAUI MediaPicker
/// </summary>
public class CameraService : ICameraService
{
    private readonly ILogger<CameraService> _logger;

    public CameraService(ILogger<CameraService> logger)
    {
        _logger = logger;
    }

    public async Task<byte[]?> TakePhotoAsync()
    {
        try
        {
            if (!MediaPicker.Default.IsCaptureSupported)
            {
                _logger.LogWarning("Camera capture not supported on this device");
                return null;
            }

            var photo = await MediaPicker.Default.CapturePhotoAsync();

            if (photo == null)
            {
                _logger.LogInformation("User cancelled photo capture");
                return null;
            }

            return await LoadPhotoAsync(photo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error taking photo");
            return null;
        }
    }

    public async Task<byte[]?> PickPhotoAsync()
    {
        try
        {
            var photo = await MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions
            {
                Title = "Select a product photo"
            });

            if (photo == null)
            {
                _logger.LogInformation("User cancelled photo pick");
                return null;
            }

            return await LoadPhotoAsync(photo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error picking photo");
            return null;
        }
    }

    public bool IsCameraAvailable()
    {
        return MediaPicker.Default.IsCaptureSupported;
    }

    private async Task<byte[]> LoadPhotoAsync(FileResult photo)
    {
        using var stream = await photo.OpenReadAsync();
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);
        return memoryStream.ToArray();
    }
}
