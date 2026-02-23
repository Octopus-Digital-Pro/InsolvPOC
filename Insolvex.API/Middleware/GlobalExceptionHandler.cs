using Microsoft.AspNetCore.Diagnostics;
using Insolvex.Core.DTOs;
using Insolvex.Core.Exceptions;

namespace Insolvex.API.Middleware;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
 {
     _logger = logger;
    }

  public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "An error occurred");

  var response = new ErrorResponse(
            exception is BusinessException ? exception.Message : "An error occurred processing your request",
        exception.GetType().Name
        );

    httpContext.Response.StatusCode = exception is BusinessException
      ? StatusCodes.Status400BadRequest
     : StatusCodes.Status500InternalServerError;

        httpContext.Response.ContentType = "application/json";
        await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);
        return true;
    }
}
