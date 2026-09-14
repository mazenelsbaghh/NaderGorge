using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NaderGorge.API.Controllers;

namespace NaderGorge.Application.Tests.AdminAI;

public sealed class AdminAISecureInputApiTests
{
    [Fact]
    public void SubmitEndpoint_SuppressesBodyLoggingAndBoundsRequest()
    {
        var method = typeof(AdminAIAgentController).GetMethod(nameof(AdminAIAgentController.SubmitSecureInput))!;
        Assert.Equal(HttpLoggingFields.None, method.GetCustomAttributes(typeof(HttpLoggingAttribute), false).Cast<HttpLoggingAttribute>().Single().LoggingFields);
        Assert.Single(method.GetCustomAttributes(typeof(RequestSizeLimitAttribute), false));
        Assert.Equal("admin-ai-secure-input", method.GetCustomAttributes(typeof(EnableRateLimitingAttribute), false).Cast<EnableRateLimitingAttribute>().Single().PolicyName);
    }
}
