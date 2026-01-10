using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using System.Security.Claims;

namespace ExpressRecipe.Client.Shared.Services;

public class CustomAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly IAuthService _authService;
    private readonly ITokenProvider _tokenProvider;
    private ClaimsPrincipal _currentUser = new ClaimsPrincipal(new ClaimsIdentity());

    public CustomAuthenticationStateProvider(IAuthService authService, ITokenProvider tokenProvider)
    {
        _authService = authService;
        _tokenProvider = tokenProvider;

        // Subscribe to authentication state changes
        _authService.AuthenticationStateChanged += OnAuthenticationStateChanged;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var token = await _tokenProvider.GetAccessTokenAsync();

            if (string.IsNullOrEmpty(token))
            {
                return new AuthenticationState(_currentUser);
            }

            var userProfile = await _authService.GetCurrentUserAsync();

            if (userProfile == null || !userProfile.IsAuthenticated)
            {
                return new AuthenticationState(_currentUser);
            }

            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userProfile.UserId.ToString()),
                new Claim(ClaimTypes.Name, userProfile.FullName),
                new Claim(ClaimTypes.Email, userProfile.Email),
                new Claim(ClaimTypes.GivenName, userProfile.FirstName),
                new Claim(ClaimTypes.Surname, userProfile.LastName)
            }, "jwt");

            _currentUser = new ClaimsPrincipal(identity);

            return new AuthenticationState(_currentUser);
        }
        catch (JSException)
        {
            // JavaScript interop not available during prerendering
            // Return unauthenticated state
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }
        catch (InvalidOperationException)
        {
            // JavaScript interop not available during prerendering
            // Return unauthenticated state
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }
    }

    public async Task MarkUserAsAuthenticated(string email, string firstName, string lastName, Guid userId)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, $"{firstName} {lastName}"),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.GivenName, firstName),
            new Claim(ClaimTypes.Surname, lastName)
        }, "jwt");

        _currentUser = new ClaimsPrincipal(identity);

        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_currentUser)));
    }

    public void MarkUserAsLoggedOut()
    {
        Console.WriteLine("[AuthStateProvider] MarkUserAsLoggedOut called");
        _currentUser = new ClaimsPrincipal(new ClaimsIdentity());

        Console.WriteLine("[AuthStateProvider] Notifying authentication state changed");
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_currentUser)));
        Console.WriteLine("[AuthStateProvider] User marked as logged out");
    }

    private void OnAuthenticationStateChanged(object? sender, bool isAuthenticated)
    {
        Console.WriteLine($"[AuthStateProvider] OnAuthenticationStateChanged event received: isAuthenticated={isAuthenticated}");
        if (!isAuthenticated)
        {
            MarkUserAsLoggedOut();
        }
    }
}
