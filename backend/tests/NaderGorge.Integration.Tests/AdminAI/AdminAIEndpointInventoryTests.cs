using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Reflection;
using System.Text.Json;
using NaderGorge.API.Controllers;
using NaderGorge.API.Extensions;

namespace NaderGorge.Integration.Tests.AdminAI;

/// <summary>
/// The runtime route table is the authoritative backend half of the AdminAI
/// capability baseline. Source parsing remains a diagnostic only.
/// </summary>
public sealed class AdminAIEndpointInventoryTests : IClassFixture<AdminAIEndpointInventoryTests.ApiFactory>
{
    private readonly ApiFactory _factory;

    public AdminAIEndpointInventoryTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public void RuntimeInventory_ContainsResolvedControllerRoutesAndAuthorizationMetadata()
    {
        var endpointSource = _factory.Services.GetRequiredService<EndpointDataSource>();
        var endpoints = endpointSource.Endpoints
            .Select(endpoint => new
            {
                Endpoint = endpoint,
                Route = endpoint as RouteEndpoint,
                Action = endpoint.Metadata.GetMetadata<ControllerActionDescriptor>(),
                Methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? Array.Empty<string>(),
                Authorization = endpoint.Metadata.OfType<IAuthorizeData>().ToArray(),
                AllowsAnonymous = endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null,
                Permissions = endpoint.Metadata.OfType<HasPermissionAttribute>()
                    .Select(attribute => attribute.Arguments?.FirstOrDefault()?.ToString())
                    .Where(permission => !string.IsNullOrWhiteSpace(permission))
                    .ToArray(),
            })
            .Where(candidate => candidate.Action is not null)
            .ToList();

        Assert.NotEmpty(endpoints);
        Assert.All(endpoints, candidate =>
        {
            Assert.NotNull(candidate.Action);
            Assert.NotEmpty(candidate.Methods);
            Assert.NotNull(candidate.Route);
            Assert.StartsWith("api/", candidate.Route!.RoutePattern.RawText ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            Assert.False(string.IsNullOrWhiteSpace(candidate.Action!.ControllerName));
            Assert.False(string.IsNullOrWhiteSpace(candidate.Action.ActionName));
        });

        var adminEndpoints = endpoints
            .Where(candidate => candidate.Action!.ControllerName.StartsWith("Admin", StringComparison.Ordinal))
            .ToList();
        Assert.NotEmpty(adminEndpoints);
        Assert.Contains(endpoints, endpoint => endpoint.Authorization.Length > 0 && !endpoint.AllowsAnonymous);
        Assert.Contains(endpoints, endpoint => endpoint.Permissions.Length > 0);
    }

    [Fact]
    public void RuntimeInventory_ExportsCanonicalSnapshot_WhenExplicitlyRequested()
    {
        var destination = Environment.GetEnvironmentVariable("ADMIN_AI_RUNTIME_INVENTORY_PATH");
        if (string.IsNullOrWhiteSpace(destination)) return;

        var endpointSource = _factory.Services.GetRequiredService<EndpointDataSource>();
        var snapshot = endpointSource.Endpoints
            .Select(endpoint => new
            {
                Route = (endpoint as RouteEndpoint)?.RoutePattern.RawText,
                Action = endpoint.Metadata.GetMetadata<ControllerActionDescriptor>(),
                Methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Order().ToArray() ?? Array.Empty<string>(),
                Authorization = endpoint.Metadata.OfType<IAuthorizeData>()
                    .Select(attribute => new { attribute.Policy, attribute.Roles, attribute.AuthenticationSchemes })
                    .OrderBy(attribute => attribute.Policy).ToArray(),
                AllowsAnonymous = endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null,
                Permissions = endpoint.Metadata.OfType<HasPermissionAttribute>()
                    .Select(attribute => attribute.Arguments?.FirstOrDefault()?.ToString()).Where(value => value is not null).Order().ToArray(),
            })
            .Where(item => item.Action is not null && item.Route is not null)
            .Select(item => new
            {
                route = item.Route,
                controller = item.Action!.ControllerName,
                action = item.Action.ActionName,
                methods = item.Methods,
                authorization = item.Authorization,
                item.AllowsAnonymous,
                permissions = item.Permissions,
            })
            .OrderBy(item => item.route).ThenBy(item => item.controller).ThenBy(item => item.action).ToArray();

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.WriteAllText(destination, JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
        Assert.NotEmpty(snapshot);
    }

    [Fact]
    public void ProductionRegression_2026_08_13_AdminAIAgentController_CanBeActivatedFromRegisteredServices()
    {
        using var scope = _factory.Services.CreateScope();

        var controller = ActivatorUtilities.CreateInstance<AdminAIAgentController>(scope.ServiceProvider);

        Assert.NotNull(controller);
    }

    public sealed class ApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("E2e");
            builder.UseSetting("Security:RequireHttps", "false");
            builder.UseSetting("AdminAI:HmacKey", Convert.ToBase64String(new byte[32]));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<ILoggerProvider>();
                services.RemoveAll<IConnectionMultiplexer>();
                services.AddSingleton<IConnectionMultiplexer>(
                    DispatchProxy.Create<IConnectionMultiplexer, NoRedisProxy>());
            });
        }
    }

    private class NoRedisProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IConnectionMultiplexer.GetDatabase))
            {
                return DispatchProxy.Create<IDatabase, NoRedisDatabaseProxy>();
            }

            throw new NotSupportedException("Redis is intentionally unavailable to the endpoint inventory test.");
        }
    }

    private class NoRedisDatabaseProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            throw new NotSupportedException("The endpoint inventory test must not access Redis data.");
    }
}
