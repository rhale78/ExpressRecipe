using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ExpressRecipe.Shared.DTOs.Auth;

/// <summary>
/// Request to register a new user.
/// </summary>
public class RegisterRequest
{
    [Required]
    [EmailAddress]
    [StringLength(256)]
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 8)]
    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;

    [Required]
    [Compare(nameof(Password))]
    [JsonPropertyName("confirmPassword")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [StringLength(100)]
    [JsonPropertyName("firstName")]
    public string? FirstName { get; set; }

    [StringLength(100)]
    [JsonPropertyName("lastName")]
    public string? LastName { get; set; }
}

/// <summary>
/// Request to login.
/// </summary>
public class LoginRequest
{
    [Required]
    [EmailAddress]
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [Required]
    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;

    [JsonPropertyName("rememberMe")]
    public bool RememberMe { get; set; }
}

/// <summary>
/// Response after successful authentication.
/// </summary>
public class AuthResponse
{
    [JsonPropertyName("accessToken")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("refreshToken")]
    public string RefreshToken { get; set; } = string.Empty;

    [JsonPropertyName("expiresAt")]
    public DateTime ExpiresAt { get; set; }

    [JsonPropertyName("user")]
    public UserDto User { get; set; } = null!;
}

/// <summary>
/// User DTO.
/// </summary>
public class UserDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("emailConfirmed")]
    public bool EmailConfirmed { get; set; }

    [JsonPropertyName("phoneNumber")]
    public string? PhoneNumber { get; set; }

    [JsonPropertyName("twoFactorEnabled")]
    public bool TwoFactorEnabled { get; set; }
}

/// <summary>
/// Request to refresh access token.
/// </summary>
public class RefreshTokenRequest
{
    [Required]
    [JsonPropertyName("refreshToken")]
    public string RefreshToken { get; set; } = string.Empty;
}
