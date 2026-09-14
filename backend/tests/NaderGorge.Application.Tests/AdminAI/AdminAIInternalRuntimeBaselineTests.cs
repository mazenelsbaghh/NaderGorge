using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NaderGorge.API.Controllers;
using NaderGorge.Application.Features.AdminAI.Catalog;
using NaderGorge.Domain.Entities.AdminAI;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests.AdminAI;

public sealed class AdminAIInternalRuntimeBaselineTests
{
    [Fact]
    public async Task Readiness_FailsClosedUntilTheActiveBaselineMatchesTheLocalRuntimeCatalog()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"admin-ai-runtime-baseline-{Guid.NewGuid()}")
            .Options);
        var registry = AdminAICapabilityRegistry.CreateProductionReadRegistry();
        var baseline = new AdminAICapabilityBaseline
        {
            Version = "read-old",
            ManifestHash = new string('a', 64),
            SafeManifestJson = "{}",
            SourceRevision = "test",
            RuntimeInventoryHash = new string('b', 64),
            FrontendInventoryHash = new string('b', 64),
            SupportedReadCount = registry.All.Count,
            Status = AdminAICapabilityBaselineStatus.Active
        };
        db.AdminAICapabilityBaselines.Add(baseline);
        await db.SaveChangesAsync();

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["AdminAI:Enabled"] = "true",
                ["AdminAI:CallbackSecret"] = "test-secret"
            }).Build();
        var controller = new AdminAIInternalController(
            configuration,
            db,
            registry,
            null!,
            null!,
            null!)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.Request.Headers["X-Internal-Token"] = "test-secret";

        var mismatch = Assert.IsType<ObjectResult>(await controller.Ready(default));
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, mismatch.StatusCode);

        baseline.RuntimeInventoryHash = registry.BaselineHash;
        await db.SaveChangesAsync();

        Assert.IsType<OkObjectResult>(await controller.Ready(default));
    }
}
