using System.Net;
using System.Text.Json;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Polly.Timeout;
using ExpressRecipe.Shared.Models;
using ExpressRecipe.ServiceDefaults.Logging;

namespace ExpressRecipe.Shared.Middleware;

/// <summary>
/// Global exception handling middleware to standardize error responses across all microservices.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client disconnected — not an application error
            _logger.LogExceptionHandled(nameof(OperationCanceledException), 499);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

        var response = context.Response;
        response.ContentType = "application/json";

        var errorResponse = new ErrorResponse
        {
            TraceId = traceId
        };

        switch (exception)
        {
            case SecurityTokenSignatureKeyNotFoundException:
                response.StatusCode = (int)HttpStatusCode.Unauthorized;
                errorResponse.Message = "Authentication failed";
                errorResponse.Code = "AUTHENTICATION_FAILED";
                _logger.LogSecurityTokenFailure(nameof(SecurityTokenSignatureKeyNotFoundException), response.StatusCode, exception);
                break;

            case SecurityTokenException:
                response.StatusCode = (int)HttpStatusCode.Unauthorized;
                errorResponse.Message = "Authentication failed";
                errorResponse.Code = "AUTHENTICATION_FAILED";
                _logger.LogUnhandledException(context.Request.Path, exception);
                break;

            case UnauthorizedAccessException:
                response.StatusCode = (int)HttpStatusCode.Unauthorized;
                errorResponse.Message = "Unauthorized access";
                errorResponse.Code = "UNAUTHORIZED";
                _logger.LogExceptionHandled(nameof(UnauthorizedAccessException), response.StatusCode);
                break;

            case ArgumentException or InvalidOperationException:
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                errorResponse.Message = exception.Message;
                errorResponse.Code = "BAD_REQUEST";
                _logger.LogValidationError(context.Request.Path, exception.Message);
                break;

            case TimeoutRejectedException:
                response.StatusCode = (int)HttpStatusCode.GatewayTimeout;
                errorResponse.Message = "A downstream service did not respond in time. Please try again later.";
                errorResponse.Code = "UPSTREAM_TIMEOUT";
                _logger.LogExceptionHandled(nameof(TimeoutRejectedException), response.StatusCode);
                break;

            case HttpRequestException:
                response.StatusCode = (int)HttpStatusCode.BadGateway;
                errorResponse.Message = "A downstream service returned an unexpected response.";
                errorResponse.Code = "UPSTREAM_ERROR";
                _logger.LogExceptionHandled(nameof(HttpRequestException), response.StatusCode);
                break;

            case KeyNotFoundException:
                response.StatusCode = (int)HttpStatusCode.NotFound;
                errorResponse.Message = "The requested resource was not found";
                errorResponse.Code = "NOT_FOUND";
                _logger.LogExceptionHandled(nameof(KeyNotFoundException), response.StatusCode);
                break;

            default:
                response.StatusCode = (int)HttpStatusCode.InternalServerError;
                errorResponse.Message = "An unexpected error occurred. Please try again later.";
                errorResponse.Code = "INTERNAL_SERVER_ERROR";
                _logger.LogUnhandledException(context.Request.Path, exception);
                break;
        }

        var result = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await response.WriteAsync(result);
    }
}
