using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace NaderGorge.API.Configuration;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class E2eOnlyAttribute : TypeFilterAttribute
{
    public E2eOnlyAttribute() : base(typeof(E2eOnlyFilter))
    {
    }
}

public sealed class E2eOnlyFilter : IAsyncActionFilter
{
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;

    public E2eOnlyFilter(IWebHostEnvironment environment, IConfiguration configuration)
    {
        _environment = environment;
        _configuration = configuration;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!string.Equals(_environment.EnvironmentName, "E2e", System.StringComparison.OrdinalIgnoreCase))
        {
            context.Result = new NotFoundObjectResult($"E2E endpoints only available in E2E environment. Current environment: {_environment.EnvironmentName}");
            return;
        }

        var suppliedToken = context.HttpContext.Request.Headers["X-E2E-Token"].FirstOrDefault();
        var configuredToken = _configuration["E2E_TEST_TOKEN"];
        if (!ServiceTokenValidator.IsValid(suppliedToken, configuredToken))
        {
            context.Result = new UnauthorizedObjectResult("Invalid E2E token.");
            return;
        }

        await next();
    }
}
