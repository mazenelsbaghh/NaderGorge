using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using NaderGorge.API.Controllers;
using NaderGorge.Application.Features.Metrics.Commands;
using NaderGorge.Domain.Entities;

namespace NaderGorge.Application.Tests.Metrics;

public sealed class WebVitalsContractTests
{
    private static readonly string[] SafeDimensionProperties =
    [
        "MetricId",
        "MetricName",
        "Value",
        "Rating",
        "RouteTemplate",
        "Surface",
        "DeviceClass",
        "ConnectionClass",
        "NavigationType",
        "ReleaseId",
        "CorrelationId",
    ];

    [Fact]
    public void IngestSchema_ExposesOnlyBoundedLowCardinalityDimensions()
    {
        var commandProperties = typeof(CreateWebVitalsMetricCommand)
            .GetProperties()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.All(SafeDimensionProperties, property => Assert.Contains(property, commandProperties));
        Assert.DoesNotContain("PageUrl", commandProperties);
        Assert.DoesNotContain("UserAgent", commandProperties);
        Assert.DoesNotContain("QueryString", commandProperties);
        Assert.DoesNotContain("Message", commandProperties);
        Assert.DoesNotContain("PhoneNumber", commandProperties);
    }

    [Theory]
    [InlineData("CUSTOM_HIGH_CARDINALITY", 100, "good")]
    [InlineData("LCP", -1, "good")]
    [InlineData("LCP", 100, "excellent")]
    [InlineData("LCP", double.PositiveInfinity, "good")]
    public async Task InvalidMetrics_AreRejectedWithoutPersistence(
        string metricName,
        double value,
        string rating)
    {
        await using var db = TestAppDbContextFactory.Create();
        var handler = new CreateWebVitalsMetricCommandHandler(db);
        var command = DeserializeCommand(new Dictionary<string, object?>
        {
            ["metricId"] = "metric-invalid",
            ["metricName"] = metricName,
            ["value"] = value,
            ["rating"] = rating,
            ["routeTemplate"] = "/student",
            ["surface"] = "student",
            ["deviceClass"] = "mobile",
            ["connectionClass"] = "moderate",
            ["navigationType"] = "client",
            ["releaseId"] = "src-0123456789012345678901234567890123456789",
        });

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Empty(await db.WebVitalsMetrics.ToListAsync());
    }

    [Fact]
    public async Task DynamicRouteAndQuery_AreNormalizedWithoutPersistingPrivacySentinels()
    {
        const string secret = "PRIVACY_SENTINEL_ACCESS_TOKEN";
        var packageId = Guid.NewGuid();
        await using var db = TestAppDbContextFactory.Create();
        var handler = new CreateWebVitalsMetricCommandHandler(db);
        var command = DeserializeCommand(new Dictionary<string, object?>
        {
            ["metricId"] = "metric-normalized",
            ["metricName"] = "LCP",
            ["value"] = 1834.2,
            ["rating"] = "good",
            ["routeTemplate"] = $"/student/packages/{packageId}?access_token={secret}",
            ["surface"] = "student",
            ["deviceClass"] = "mobile",
            ["connectionClass"] = "moderate",
            ["navigationType"] = "client",
            ["releaseId"] = "src-0123456789012345678901234567890123456789",
            ["correlationId"] = "corr-safe-123",
            ["pageUrl"] = $"https://app.example/student/packages/{packageId}?access_token={secret}",
            ["userAgent"] = secret,
            ["message"] = secret,
            ["phoneNumber"] = secret,
        });

        var result = await handler.Handle(command, CancellationToken.None);
        var persisted = await db.WebVitalsMetrics.SingleAsync();

        Assert.True(result.Success);
        Assert.Equal(
            "/student/packages/[packageId]",
            ReadRequiredString(persisted, "RouteTemplate"));
        var serialized = JsonSerializer.Serialize(persisted);
        Assert.DoesNotContain(packageId.ToString(), serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(secret, serialized, StringComparison.Ordinal);
    }

    [Fact]
    public void BrowserIngest_UsesDedicatedRateLimitPolicy()
    {
        var action = typeof(WebVitalsController).GetMethod(
            nameof(WebVitalsController.ReportWebVitals));
        var rateLimit = Assert.Single(
            action!.GetCustomAttributes<EnableRateLimitingAttribute>());

        Assert.Equal("web-vitals", rateLimit.PolicyName);
    }

    private static CreateWebVitalsMetricCommand DeserializeCommand(
        IReadOnlyDictionary<string, object?> payload)
    {
        var options = new JsonSerializerOptions
        {
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
            PropertyNameCaseInsensitive = true,
        };
        return JsonSerializer.Deserialize<CreateWebVitalsMetricCommand>(
            JsonSerializer.Serialize(payload, options),
            options)
            ?? throw new InvalidOperationException("Web Vitals payload did not bind.");
    }

    private static string ReadRequiredString(WebVitalsMetric metric, string propertyName)
    {
        var property = metric.GetType().GetProperty(propertyName);
        Assert.NotNull(property);
        return Assert.IsType<string>(property.GetValue(metric));
    }
}
