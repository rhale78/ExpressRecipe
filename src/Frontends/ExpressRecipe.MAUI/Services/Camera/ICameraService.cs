namespace ExpressRecipe.MAUI.Services.Camera;

/// <summary>
/// Service for camera operations
/// </summary>
public interface ICameraService
{
    /// <summary>
    /// Take a photo using the device camera
    /// </summary>
    Task<byte[]?> TakePhotoAsync();

    /// <summary>
    /// Pick a photo from the device gallery
    /// </summary>
    Task<byte[]?> PickPhotoAsync();

    /// <summary>
    /// Check if camera is available
    /// </summary>
    bool IsCameraAvailable();
}
