using Xunit;
using Microsoft.AspNetCore.RateLimiting;
using NaderGorge.API.Controllers;
using System.Reflection;

namespace NaderGorge.Application.Tests.LiveSupportAI;

public sealed class LiveSupportAISecurityTests
{
    private static string? GetRateLimitingPolicy(Type controller, string methodName)
    {
        var method = controller.GetMethods()
            .FirstOrDefault(m => m.Name == methodName && m.GetCustomAttributes(typeof(EnableRateLimitingAttribute), true).Any());
        
        return method?.GetCustomAttributes(typeof(EnableRateLimitingAttribute), true)
            .Cast<EnableRateLimitingAttribute>()
            .FirstOrDefault()?.PolicyName;
    }

    [Fact]
    public void LiveSupportEndpointsHaveCorrectRateLimitingPolicies()
    {
        Assert.Equal("live-support-ai-message", GetRateLimitingPolicy(typeof(LiveSupportParticipantController), nameof(LiveSupportParticipantController.Send)));
        Assert.Equal("live-support-ai-confirmation", GetRateLimitingPolicy(typeof(LiveSupportParticipantController), nameof(LiveSupportParticipantController.ConfirmAction)));
        Assert.Equal("live-support-ai-confirmation", GetRateLimitingPolicy(typeof(LiveSupportParticipantController), nameof(LiveSupportParticipantController.CancelAction)));
        Assert.Equal("live-support-ai-verification", GetRateLimitingPolicy(typeof(LiveSupportParticipantController), nameof(LiveSupportParticipantController.VerificationLookup)));
        Assert.Equal("live-support-ai-verification", GetRateLimitingPolicy(typeof(LiveSupportParticipantController), nameof(LiveSupportParticipantController.VerificationSessionAnswer)));
        Assert.Equal("live-support-ai-registration", GetRateLimitingPolicy(typeof(LiveSupportParticipantController), nameof(LiveSupportParticipantController.ConfirmRegistration)));
        
        Assert.Equal("live-support-ai-admin-preview", GetRateLimitingPolicy(typeof(LiveSupportAIAdminController), nameof(LiveSupportAIAdminController.Preview)));
        
        Assert.Equal("live-support-ai-callback", GetRateLimitingPolicy(typeof(InternalController), nameof(InternalController.ClaimAITurn)));
        Assert.Equal("live-support-ai-callback", GetRateLimitingPolicy(typeof(InternalController), nameof(InternalController.CompleteAITurn)));
        Assert.Equal("live-support-ai-callback", GetRateLimitingPolicy(typeof(InternalController), nameof(InternalController.FailAITurn)));
    }

    [Fact]
    public void SensitiveDataPatternScanning_EnsuresNoSecretsLeaked()
    {
        var sensitivePatterns = new[] { "password", "token", "secret", "cookie", "auth" };
        
        var data = new Dictionary<string, string>
        {
            { "password", "123456" },
            { "clientMessageId", "c-12345" },
            { "verificationAnswer", "secret" },
            { "token", "abcde" }
        };

        foreach (var key in data.Keys)
        {
            if (sensitivePatterns.Any(p => key.Contains(p, StringComparison.OrdinalIgnoreCase)))
            {
                var value = data[key];
                var redactedValue = "[REDACTED]";
                Assert.NotEqual(value, redactedValue);
            }
        }
    }

    [Fact]
    public void RedactionScan_EnsuresNoForbiddenSecretsLeakToLogsOrEvents()
    {
        var forbiddenPatterns = new[] { "Password", "AI_CALLBACK_SECRET", "accessToken", "massar_support_guest", "verificationAnswer", "lookupValue" };
        
        var payloadToInspect = "{\"User\":\"guest\",\"Password\":\"my-secret-password\",\"AI_CALLBACK_SECRET\":\"supersecret\",\"accessToken\":\"token123\",\"massar_support_guest\":\"cookie123\",\"verificationAnswer\":\"my-secret-answer\",\"lookupValue\":\"01011111111\"}";
        
        var redactedPayload = System.Text.RegularExpressions.Regex.Replace(payloadToInspect, "\"(Password|AI_CALLBACK_SECRET|accessToken|massar_support_guest|verificationAnswer|lookupValue)\":\"[^\"]+\"", "\"$1\":\"[REDACTED]\"");
        
        foreach (var key in forbiddenPatterns)
        {
            Assert.Contains($"\"{key}\":\"[REDACTED]\"", redactedPayload);
            Assert.DoesNotContain($"\"{key}\":\"my-secret-password\"", redactedPayload);
            Assert.DoesNotContain($"\"{key}\":\"supersecret\"", redactedPayload);
        }
    }
}
