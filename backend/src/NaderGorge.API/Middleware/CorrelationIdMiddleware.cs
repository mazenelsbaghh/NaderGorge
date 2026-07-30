namespace NaderGorge.API.Middleware;

public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private const string CorrelationIdHeader = "X-Correlation-Id";
    internal const string CorrelationIdItem = "CorrelationId";

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var suppliedCorrelationId =
            context.Request.Headers[CorrelationIdHeader].FirstOrDefault();
        var correlationId = IsSafeCorrelationId(suppliedCorrelationId)
            ? suppliedCorrelationId!
            : Guid.NewGuid().ToString("N");

        context.Items[CorrelationIdItem] = correlationId;
        context.Response.Headers[CorrelationIdHeader] = correlationId;

        using var scope = context.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("CorrelationId")
            .BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId });

        await _next(context);
    }

    private static bool IsSafeCorrelationId(string? correlationId) =>
        correlationId is { Length: >= 8 and <= 64 } &&
        correlationId.All(character =>
            character is >= 'a' and <= 'z' ||
            character is >= '0' and <= '9' ||
            character == '-');
}
