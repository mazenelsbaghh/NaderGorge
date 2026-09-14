using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace NaderGorge.API.AutoRepair;

public sealed class RepairRunnerAuth(IConfiguration configuration) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var expected = configuration["AutoRepair:RunnerToken"] ?? "";
        var supplied = context.HttpContext.Request.Headers["X-Repair-Token"].ToString();
        if (expected.Length < 32 || !CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(Encoding.UTF8.GetBytes(expected)), SHA256.HashData(Encoding.UTF8.GetBytes(supplied))))
        {
            context.Result = new UnauthorizedResult();
            return;
        }
        await next();
    }
}
