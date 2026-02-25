using System.Security.Claims;
using Insolvex.API.Data;
using Insolvex.Domain.Entities;

namespace Insolvex.API.Middleware;

public class ErrorLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorLoggingMiddleware> _logger;

    public ErrorLoggingMiddleware(RequestDelegate next, ILogger<ErrorLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ApplicationDbContext dbContext)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred");
            await LogErrorToDb(ex, context, dbContext);
            throw;
        }
    }

    private async Task LogErrorToDb(Exception ex, HttpContext context, ApplicationDbContext dbContext)
    {
        try
        {
            var errorLog = new ErrorLog
            {
                Id = Guid.NewGuid(),
                Message = ex.Message,
                StackTrace = ex.StackTrace,
                Source = ex.Source,
                RequestPath = context.Request.Path,
                RequestMethod = context.Request.Method,
                UserId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                UserEmail = context.User?.FindFirst(ClaimTypes.Email)?.Value,
                Timestamp = DateTime.UtcNow,
                CreatedOn = DateTime.UtcNow,
                CreatedBy = "System"
            };

            dbContext.ErrorLogs.Add(errorLog);
            await dbContext.SaveChangesAsync();
        }
        catch (Exception logEx)
        {
            _logger.LogError(logEx, "Failed to log error to database");
        }
    }
}
