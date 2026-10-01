using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Integration.Tests.Finance;

public sealed class RefundHttpAuthorizationTests
{
    [Theory]
    [InlineData(null, "", 401, 401)]
    [InlineData("Student", "", 403, 403)]
    [InlineData("Staff", "", 403, 403)]
    [InlineData("Staff", "finance.refunds.view", 200, 403)]
    [InlineData("Staff", "finance.refunds.create", 403, 200)]
    [InlineData("Staff", "finance.manage", 200, 200)]
    [InlineData("Admin", "", 200, 200)]
    public async Task RefundEndpoints_EnforceReadAndCreatePermissionsWithRealJwt(
        string? roleName, string permission, int readStatus, int createStatus)
    {
        await using var scenario = await RefundTestScenario.CreateAsync();
        if (roleName is not null)
        {
            var role = await scenario.Db.Roles.SingleOrDefaultAsync(candidate => candidate.Name == roleName)
                ?? new Role { Name = roleName, Type = roleName == "Student" ? RoleType.Student : RoleType.Assistant };
            role.PermissionsJson = JsonSerializer.Serialize(string.IsNullOrEmpty(permission) ? Array.Empty<string>() : new[] { permission });
            scenario.Db.UserRoles.Add(new UserRole { User = scenario.Actor, Role = role });
            await scenario.Db.SaveChangesAsync();
        }
        await using var factory = new RefundApiFactory(scenario.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        if (roleName is not null)
        {
            using var scope = factory.Services.CreateScope();
            var token = scope.ServiceProvider.GetRequiredService<ITokenService>()
                .GenerateAccessToken(scenario.Actor, [roleName]);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        using var read = await client.GetAsync("/api/admin/platform-finance/refunds");
        using var create = await client.PostAsJsonAsync("/api/admin/platform-finance/refunds/external-package", scenario.CashRequest());

        Assert.Equal((HttpStatusCode)readStatus, read.StatusCode);
        Assert.Equal((HttpStatusCode)createStatus, create.StatusCode);
        await using var verifyDb = scenario.CreateDbContext();
        if (createStatus == 200)
        {
            Assert.False((await verifyDb.StudentAccessGrants.SingleAsync()).IsActive);
            Assert.Equal(290m, (await verifyDb.PlatformRefunds.SingleAsync()).TotalAmount);
            Assert.Equal(scenario.Actor.Id, (await verifyDb.JournalEntries.SingleAsync()).ActorUserId);
        }
        else
        {
            Assert.True((await verifyDb.StudentAccessGrants.SingleAsync()).IsActive);
            Assert.Empty(await verifyDb.PlatformRefunds.ToListAsync());
            Assert.Empty(await verifyDb.JournalEntries.ToListAsync());
        }
    }

    private sealed class RefundApiFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("E2e");
            builder.UseSetting("Security:RequireHttps", "false");
            builder.UseSetting("ConnectionStrings:DefaultConnection", connectionString);
            var redis = Environment.GetEnvironmentVariable("REFUND_TEST_REDIS")
                ?? throw new InvalidOperationException("Real refund HTTP tests require REFUND_TEST_REDIS.");
            builder.UseSetting("ConnectionStrings:Redis", redis);
            builder.UseSetting("Redis:ConnectionString", redis);
            builder.UseSetting("AdminAI:HmacKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
            builder.UseSetting("JwtSettings:Secret", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
            builder.UseSetting("AiMediaRelay:Secret", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<ILoggerProvider>();
            });
        }
    }
}
