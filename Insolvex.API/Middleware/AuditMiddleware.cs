using Insolvex.Core.Abstractions;

namespace Insolvex.API.Middleware;

/// <summary>
/// Lightweight safety-net middleware: logs only failed requests (4xx/5xx).
/// All successful mutations are already logged with human-readable descriptions
/// by the service layer, so we skip those here to avoid "POST /api/..." noise.
/// </summary>
public class AuditMiddleware
{
    private readonly RequestDelegate _next;

    public AuditMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, IAuditService auditService)
    {
        await _next(context);

        var statusCode = context.Response.StatusCode;

        // Only log HTTP failures -- successful mutations are covered by services
        if (statusCode >= 400 && IsStateChanging(context.Request.Method))
        {
            var method = context.Request.Method;
            var path = context.Request.Path.Value;
            var severity = statusCode >= 500 ? "Critical" : "Warning";

            await auditService.LogAsync(new AuditEntry
            {
                Action = $"Request Failed: {method} {path}",
                Category = InferCategory(path),
                Severity = severity,
                NewValues = new { requestMethod = method, requestPath = path, responseStatusCode = statusCode },
            });
        }
    }

    private static bool IsStateChanging(string method) =>
        method == "POST" || method == "PUT" || method == "DELETE" || method == "PATCH";

    private static string InferCategory(string? path)
    {
        if (string.IsNullOrEmpty(path)) return "System";
        var lower = path.ToLowerInvariant();
        if (lower.Contains("/auth")) return "Auth";
        if (lower.Contains("/cases")) return "Case";
        if (lower.Contains("/documents") || lower.Contains("/upload")) return "Document";
        if (lower.Contains("/tasks")) return "Task";
        if (lower.Contains("/parties")) return "Party";
        if (lower.Contains("/phases") || lower.Contains("/stage")) return "Workflow";
        if (lower.Contains("/signing")) return "Signing";
        if (lower.Contains("/meeting") || lower.Contains("/calendar")) return "Meeting";
        if (lower.Contains("/settings") || lower.Contains("/firm") || lower.Contains("/tenant")) return "Settings";
        if (lower.Contains("/users")) return "User";
        return "System";
    }
}
