using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NaderGorge.Infrastructure.Services;

namespace NaderGorge.API.Filters;

[AttributeUsage(AttributeTargets.Method)]
public class IdempotentAttribute : Attribute, IAsyncActionFilter
{
    private readonly int _ttlSeconds;

    public IdempotentAttribute(int ttlSeconds = 300)
    {
        _ttlSeconds = ttlSeconds;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var request = context.HttpContext.Request;

        if (!request.Headers.TryGetValue("Idempotency-Key", out var keyValues) || string.IsNullOrWhiteSpace(keyValues.ToString()))
        {
            await next();
            return;
        }

        var key = keyValues.ToString();
        var path = request.Path.ToString().ToLowerInvariant();
        var cacheKey = $"idempotency:{path}:{key}";

        var idempotencyService = context.HttpContext.RequestServices.GetRequiredService<IIdempotencyService>();

        var cachedResult = await idempotencyService.GetResultAsync(cacheKey);
        if (cachedResult.HasValue)
        {
            var (statusCode, responseBody) = cachedResult.Value;
            var responseResult = new ContentResult
            {
                StatusCode = statusCode,
                Content = responseBody,
                ContentType = "application/json; charset=utf-8"
            };
            context.Result = responseResult;
            return;
        }

        var ttl = TimeSpan.FromSeconds(_ttlSeconds);
        var locked = await idempotencyService.TryLockAsync(cacheKey, ttl);
        if (!locked)
        {
            context.Result = new ConflictObjectResult(new { message = "Another request with the same Idempotency-Key is currently processing." });
            return;
        }

        var executedContext = await next();

        if (executedContext.Result is ObjectResult objectResult)
        {
            var statusCode = objectResult.StatusCode ?? 200;
            if (statusCode >= 200 && statusCode < 300)
            {
                var json = System.Text.Json.JsonSerializer.Serialize(objectResult.Value);
                await idempotencyService.SaveResultAsync(cacheKey, statusCode, json, ttl);
            }
        }
        else if (executedContext.Result is StatusCodeResult statusCodeResult)
        {
            var statusCode = statusCodeResult.StatusCode;
            if (statusCode >= 200 && statusCode < 300)
            {
                await idempotencyService.SaveResultAsync(cacheKey, statusCode, string.Empty, ttl);
            }
        }
    }
}
