namespace ExpressRecipe.Shared.Services;

using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;

/// <summary>
/// HTTP delegating handler that automatically adds JWT tokens to outgoing requests.
/// Injects the token from ITokenProvider into the Authorization header.
/// </summary>
public class AuthenticationDelegatingHandler : DelegatingHandler
{
    private readonly ITokenProvider _tokenProvider;
    private readonly ILogger<AuthenticationDelegatingHandler> _logger;

    public AuthenticationDelegatingHandler(ITokenProvider tokenProvider, ILogger<AuthenticationDelegatingHandler> logger)
    {
        _tokenProvider = tokenProvider;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Get the service token
        var token = await _tokenProvider.GetAccessTokenAsync();

        if (!string.IsNullOrEmpty(token))
        {
            // Add authorization header if token is available
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            _logger.LogDebug("Added authorization header to {Method} {RequestUri}", request.Method, request.RequestUri?.PathAndQuery);
        }
        else
        {
            _logger.LogWarning("No token available for {Method} {RequestUri}", request.Method, request.RequestUri?.PathAndQuery);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
