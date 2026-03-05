using Microsoft.AspNetCore.Diagnostics;
using Insolvio.Core.DTOs;
using Insolvio.Core.Exceptions;

namespace Insolvio.API.Middleware;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "An error occurred: {ExceptionType} - {Message}", exception.GetType().Name, exception.Message);

        var (statusCode, message) = exception switch
        {
            NotFoundException nfe => (StatusCodes.Status404NotFound, nfe.Message),
            ForbiddenException fe => (StatusCodes.Status403Forbidden, fe.Message),
            BusinessException be => (StatusCodes.Status400BadRequest, be.Message),
            _ => (StatusCodes.Status500InternalServerError, "An error occurred processing your request"),
        };

        var response = new ErrorResponse(message, exception.GetType().Name);

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/json";
        await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);
        return true;
    }
}
