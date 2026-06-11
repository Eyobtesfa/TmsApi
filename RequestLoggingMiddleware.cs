using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Threading.Tasks;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 1. Generate a short, unique 8-character correlation ID
        string correlationId = System.Guid.NewGuid().ToString("N")[..8];

        // 2. STAMP the header early onto the response before downstream work happens
        context.Response.Headers["X-Correlation-Id"] = correlationId;

        // 3. Log the incoming entry line
        _logger.LogInformation(
            "Request Started: {Method} {Path} [Correlation ID: {CorrelationId}]",
            context.Request.Method,
            context.Request.Path,
            correlationId);

        // 4. Start a stopwatch to measure execution duration
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // 5. Pass control downstream to the next middleware or endpoint
            await _next(context);
        }
        finally
        {
            // 6. Stop timing once the downstream work finishes and execution flows back up
            stopwatch.Stop();

            // 7. Log the exiting line with the status code and duration
            _logger.LogInformation(
                "Request Finished: {StatusCode} in {ElapsedMs}ms [Correlation ID: {CorrelationId}]",
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                correlationId);
        }
    }
}