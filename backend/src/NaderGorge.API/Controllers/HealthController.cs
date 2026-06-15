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

    public HealthController(IAppDbContext db, IConnectionMultiplexer redis)
    {
        _db = db;
        _redis = redis;
    }

    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            version = "1.0.0-phase1"
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
                redis = redisReady ? "healthy" : "unhealthy"
            });
        }

        return Ok(new
        {
            status = "healthy",
            database = "healthy",
            redis = "healthy",
            timestamp = DateTime.UtcNow
        });
    }
}
