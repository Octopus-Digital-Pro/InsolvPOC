using System.Diagnostics;
using Insolvex.Core.Abstractions;

namespace Insolvex.API.Middleware;

public class AuditMiddleware
{
    private readonly RequestDelegate _next;

    public AuditMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IAuditService auditService)
    {
        var method = context.Request.Method;
        var path = context.Request.Path.Value;
        var sw = Diagnostics.IsStateChanging(method) ? Stopwatch.StartNew() : null;

        await _next(context);

        // Audit state-changing operations
        if (sw != null)
        {
            sw.Stop();
            var statusCode = context.Response.StatusCode;

            // Always log, even failures — failures are warnings
            var severity = statusCode >= 400 ? "Warning" : "Info";

            await auditService.LogAsync(new AuditEntry
            {
                Action = $"{method} {path}",
                Category = InferCategory(path),
                Severity = severity,
                NewValues = new
                {
                    requestMethod = method,
                    requestPath = path,
                    responseStatusCode = statusCode,
                    durationMs = sw.ElapsedMilliseconds,
                },
            });
        }
    }

    private static string InferCategory(string? path)
    {
        if (string.IsNullOrEmpty(path)) return "System";
        var lower = path.ToLowerInvariant();
        return lower switch
        {
            _ when lower.Contains("/auth") => "Auth",
            _ when lower.Contains("/cases") => "Case",
            _ when lower.Contains("/documents") || lower.Contains("/upload") => "Document",
            _ when lower.Contains("/tasks") => "Task",
            _ when lower.Contains("/parties") => "Party",
            _ when lower.Contains("/phases") || lower.Contains("/workflow") || lower.Contains("/stage") => "Workflow",
            _ when lower.Contains("/signing") => "Signing",
            _ when lower.Contains("/meeting") || lower.Contains("/calendar") => "Meeting",
            _ when lower.Contains("/settings") || lower.Contains("/firm") || lower.Contains("/config") || lower.Contains("/tenant") => "Settings",
            _ when lower.Contains("/users") => "User",
            _ when lower.Contains("/mailmerge") || lower.Contains("/template") => "Document",
            _ => "System",
        };
    }

    private static class Diagnostics
    {
        public static bool IsStateChanging(string method) =>
            method is "POST" or "PUT" or "DELETE" or "PATCH";
    }
}
