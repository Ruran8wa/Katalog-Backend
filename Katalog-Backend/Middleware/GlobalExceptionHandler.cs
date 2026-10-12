using Katalog_Backend.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Katalog_Backend.Middleware;

public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title) = MapException(exception);

        if (statusCode >= 500)
        {
            logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);
        }
        else
        {
            logger.LogWarning(exception, "Handled exception: {Message}", exception.Message);
        }

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = exception.Message,
            Instance = httpContext.Request.Path
        };

        // Include validation errors for RegistrationException
        if (exception is RegistrationException regEx && regEx.Errors.Any())
        {
            problemDetails.Extensions["errors"] = regEx.Errors;
        }

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }

    private static (int statusCode, string title) MapException(Exception exception) =>
        exception switch
        {
            // 404 Not Found
            ProductNotFoundException
                or VariantNotFoundException
                or VariantImageNotFoundException
                or CollectionNotFoundException
                or OrderNotFoundException
                or KeyNotFoundException       => (StatusCodes.Status404NotFound, "Not Found"),

            // 409 Conflict
            DuplicateSkuException
                or DuplicateCollectionNameException
                or ProductAlreadyInCollectionException
                or InsufficientStockException => (StatusCodes.Status409Conflict, "Conflict"),

            // 400 Bad Request
            RegistrationException
                or InvalidOrderOperationException
                or InvalidVariantOperationException
                or ProductNotInCollectionException
                or ArgumentException          => (StatusCodes.Status400BadRequest, "Bad Request"),

            // 401 Unauthorized
            InvalidCredentialsException      => (StatusCodes.Status401Unauthorized, "Unauthorized"),

            // 423 Locked
            AccountLockedException           => (StatusCodes.Status423Locked, "Account Locked"),

            // 500 fallback for everything else
            _                                => (StatusCodes.Status500InternalServerError, "An unexpected error occurred")
        };
}
