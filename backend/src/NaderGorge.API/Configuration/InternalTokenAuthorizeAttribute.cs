using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace NaderGorge.API.Configuration;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class InternalTokenAuthorizeAttribute : TypeFilterAttribute
{
    public InternalTokenAuthorizeAttribute(params string[] tokenConfigurationKeys)
        : base(typeof(InternalTokenAuthorizeFilter))
    {
        Arguments = new object[]
        {
            tokenConfigurationKeys.Length == 0
                ? new[] { "API_CALLBACK_SECRET" }
                : tokenConfigurationKeys
        };
    }
}

public sealed class InternalTokenAuthorizeFilter : IAsyncActionFilter
{
    private readonly IConfiguration _configuration;
    private readonly string[] _tokenConfigurationKeys;

    public InternalTokenAuthorizeFilter(
        IConfiguration configuration,
        string[] tokenConfigurationKeys)
    {
        _configuration = configuration;
        _tokenConfigurationKeys = tokenConfigurationKeys;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var suppliedToken = context.HttpContext.Request.Headers["X-Internal-Token"].FirstOrDefault();
        var configuredTokens = _tokenConfigurationKeys
            .Select(key => _configuration[key])
            .ToArray();

        if (!ServiceTokenValidator.IsValid(suppliedToken, configuredTokens))
        {
            context.Result = new UnauthorizedObjectResult("Invalid internal token.");
            return;
        }

        await next();
    }
}
