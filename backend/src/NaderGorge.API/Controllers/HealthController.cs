using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using NaderGorge.Domain.Interfaces;
using System.Threading.Tasks;
using System;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly IAppDbContext _db;
    private readonly IConnectionMultiplexer _redis;
    private readonly IConfiguration _configuration;

    public HealthController(IAppDbContext db, IConnectionMultiplexer redis, IConfiguration configuration)
    {
        _db = db;
        _redis = redis;
        _configuration = configuration;
    }

    [HttpGet("ready/ai-live-support")]
    public async Task<IActionResult> GetAILiveSupportReady(CancellationToken cancellationToken)
    {
        var callbackSecret = _configuration["AI_CALLBACK_SECRET"];
        var secretReady = !string.IsNullOrWhiteSpace(callbackSecret) && callbackSecret.Length >= 32;
        var redisReady = _redis.IsConnected;
        var workerReady = false;
        if (redisReady)
        {
            try { workerReady = await _redis.GetDatabase().KeyExistsAsync("live-support-worker:ready"); }
            catch { workerReady = false; }
        }
        var policyReady = await _db.LiveSupportAIPolicyVersions.AnyAsync(item => item.Status == NaderGorge.Domain.Enums.LiveSupportAIPolicyStatus.Published && item.IsEnabled, cancellationToken);
        var ready = secretReady && redisReady && workerReady && policyReady;
        return StatusCode(ready ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable, new
        {
            status = ready ? "healthy" : "unhealthy",
            callbackAuthentication = secretReady ? "healthy" : "unhealthy",
            redis = redisReady ? "healthy" : "unhealthy",
            worker = workerReady ? "healthy" : "unhealthy",
            policy = policyReady ? "healthy" : "unhealthy"
        });
    }

    [HttpGet("ready/admin-ai")]
    public async Task<IActionResult> GetAdminAIReady(CancellationToken cancellationToken)
    {
        var enabled = _configuration.GetValue("AdminAI:Enabled", false);
        if (!enabled)
        {
            return Ok(new
            {
                status = "healthy",
                feature = "disabled",
                baseline = "not-required",
                policy = "not-required",
                queue = "not-required",
                callbackAuthentication = "not-required"
            });
        }

        var callbackSecret = _configuration["AdminAI:CallbackSecret"];
        var callbackReady = !string.IsNullOrWhiteSpace(callbackSecret) && callbackSecret.Length >= 32;
        var queueReady = false;
        if (_redis.IsConnected)
        {
            try { queueReady = await _redis.GetDatabase().KeyExistsAsync("admin-ai-worker:ready"); }
            catch { queueReady = false; }
        }

        var baselineReady = false;
        var policyReady = false;
        try
        {
            baselineReady = await _db.AdminAICapabilityBaselines.AnyAsync(
                item => item.Status == NaderGorge.Domain.Enums.AdminAICapabilityBaselineStatus.Active,
                cancellationToken);
            policyReady = await _db.AdminAISensitiveDataPolicyVersions.AnyAsync(
                item => item.Status == NaderGorge.Domain.Enums.AdminAISensitiveDataPolicyStatus.Active,
                cancellationToken);
        }
        catch
        {
            baselineReady = false;
            policyReady = false;
        }

        var ready = baselineReady && policyReady && queueReady && callbackReady;
        return StatusCode(ready ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable, new
        {
            status = ready ? "healthy" : "unhealthy",
            feature = "enabled",
            baseline = baselineReady ? "healthy" : "unhealthy",
            policy = policyReady ? "healthy" : "unhealthy",
            queue = queueReady ? "healthy" : "unhealthy",
            callbackAuthentication = callbackReady ? "healthy" : "unhealthy"
        });
    }

    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            version = "1.0.0-phase1",
            nodeId = _configuration["Cluster:NodeId"] ?? "unknown",
            releaseId = _configuration["Cluster:ReleaseId"] ?? "unknown"
        });
    }

    [HttpGet("live")]
    public IActionResult GetLive()
    {
        return Ok(new
        {
            status = "healthy",
            nodeId = _configuration["Cluster:NodeId"] ?? "unknown",
            releaseId = _configuration["Cluster:ReleaseId"] ?? "unknown",
            timestamp = DateTime.UtcNow
        });
    }

    [HttpGet("ready")]
    public async Task<IActionResult> GetReady()
    {
        bool dbReady = false;
        try
        {
            if (_db is DbContext dbContext)
            {
                dbReady = await dbContext.Database.CanConnectAsync();
            }
            else
            {
                dbReady = await _db.Users.AnyAsync();
            }
        }
        catch
        {
            dbReady = false;
        }

        bool redisReady = _redis.IsConnected;

        if (!dbReady || !redisReady)
        {
            return StatusCode(503, new
            {
                status = "unhealthy",
                database = dbReady ? "healthy" : "unhealthy",
                redis = redisReady ? "healthy" : "unhealthy",
                nodeId = _configuration["Cluster:NodeId"] ?? "unknown",
                releaseId = _configuration["Cluster:ReleaseId"] ?? "unknown"
            });
        }

        return Ok(new
        {
            status = "healthy",
            database = "healthy",
            redis = "healthy",
            nodeId = _configuration["Cluster:NodeId"] ?? "unknown",
            releaseId = _configuration["Cluster:ReleaseId"] ?? "unknown",
            timestamp = DateTime.UtcNow
        });
    }
}
