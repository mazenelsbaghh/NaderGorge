using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.Application.Common;
using StackExchange.Redis;

namespace NaderGorge.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/ai-provider-status")]
public sealed class AIProviderStatusController(IConnectionMultiplexer redis) : ControllerBase
{
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var json = await redis.GetDatabase().StringGetAsync("ai:gemini:provider-status").WaitAsync(ct);
        var status = json.IsNullOrEmpty ? null : JsonSerializer.Deserialize<AIProviderStatus>(json.ToString(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (status is null || status.State is not ("healthy" or "balance-exhausted" or "quota-exhausted"))
            status = new("unknown", null, null);
        return Ok(ApiResponse<AIProviderStatus>.Ok(status));
    }
}

public sealed record AIProviderStatus(string State, string? IncidentId, DateTimeOffset? UpdatedAt);
