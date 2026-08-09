using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.Application.Common;
using NaderGorge.Infrastructure.Observability;
using StackExchange.Redis;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/admin/system-logs")]
[Authorize(Roles = "Admin")]
public sealed class AdminSystemLogsController(IConnectionMultiplexer redis) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] SystemLogQuery query)
    {
        var limit = Math.Clamp(query.Limit, 1, RedisSystemLogProvider.Capacity);
        var filter = query.ToFilter();
        var logs = (await ReadEntries())
            .Where(entry => Matches(entry.Log, filter))
            .Select(entry => entry.Log)
            .OrderByDescending(entry => entry.Timestamp)
            .ThenByDescending(entry => entry.Id)
            .Take(limit)
            .ToArray();

        return Ok(ApiResponse<SystemLogDto[]>.Ok(logs));
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] SystemLogQuery query)
    {
        var filter = query.ToFilter();
        var logs = (await ReadEntries())
            .Where(entry => Matches(entry.Log, filter))
            .Select(entry => entry.Log)
            .OrderByDescending(entry => entry.Timestamp)
            .ThenByDescending(entry => entry.Id)
            .Select(Format)
            .ToArray();

        var content = string.Join("\n\n────────────────────────\n\n", logs);
        return File(Encoding.UTF8.GetBytes(content), "text/plain; charset=utf-8",
            $"system-logs-{DateTime.UtcNow:yyyy-MM-dd}.txt");
    }

    [HttpPost("delete")]
    public async Task<IActionResult> Delete([FromBody] DeleteSystemLogsRequest request)
    {
        var requestedIds = (request.Ids ?? Array.Empty<Guid>())
            .Take(RedisSystemLogProvider.Capacity)
            .ToHashSet();
        var matches = (await ReadEntries()).Where(entry => requestedIds.Contains(entry.Log.Id)).ToArray();
        var database = redis.GetDatabase();
        foreach (var match in matches)
            await database.ListRemoveAsync(RedisSystemLogProvider.RedisKey, match.RawValue, 0);

        return Ok(ApiResponse<object>.Ok(new { deletedCount = matches.Length }));
    }

    [HttpDelete("all")]
    public async Task<IActionResult> DeleteAll()
    {
        var database = redis.GetDatabase();
        var count = await database.ListLengthAsync(RedisSystemLogProvider.RedisKey);
        await database.KeyDeleteAsync(RedisSystemLogProvider.RedisKey);
        return Ok(ApiResponse<object>.Ok(new { deletedCount = count }));
    }

    private async Task<StoredSystemLog[]> ReadEntries()
    {
        var values = await redis.GetDatabase().ListRangeAsync(
            RedisSystemLogProvider.RedisKey, -RedisSystemLogProvider.Capacity, -1);
        var entries = new List<StoredSystemLog>(values.Length);
        foreach (var value in values)
        {
            var log = Deserialize(value!);
            if (log is not null) entries.Add(new StoredSystemLog(value!, log));
        }
        return entries.ToArray();
    }

    private static SystemLogDto? Deserialize(string json)
    {
        try { return JsonSerializer.Deserialize<SystemLogDto>(json, JsonOptions); }
        catch (JsonException) { return null; }
    }

    private static bool Search(SystemLogDto entry, string search) =>
        entry.Message.Contains(search, StringComparison.OrdinalIgnoreCase) ||
        entry.Category.Contains(search, StringComparison.OrdinalIgnoreCase) ||
        (entry.Exception?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false);

    private static bool Matches(SystemLogDto entry, SystemLogFilter filter) =>
        (string.IsNullOrWhiteSpace(filter.Level) || entry.Level.Equals(filter.Level, StringComparison.OrdinalIgnoreCase)) &&
        (string.IsNullOrWhiteSpace(filter.Source) || entry.Source.Equals(filter.Source, StringComparison.OrdinalIgnoreCase)) &&
        (string.IsNullOrWhiteSpace(filter.Search) || Search(entry, filter.Search)) &&
        (!filter.ErrorsOnly || entry.Level is "error" or "critical") &&
        (!filter.From.HasValue || entry.Timestamp >= filter.From.Value) &&
        (!filter.To.HasValue || entry.Timestamp <= filter.To.Value);

    private static string Format(SystemLogDto entry) =>
        $"[{entry.Timestamp:O}] [{entry.Source}] [{entry.Level}] {entry.Category}\n{entry.Message}"
        + (entry.Exception is null ? string.Empty : $"\n\n{entry.Exception}");

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record StoredSystemLog(string RawValue, SystemLogDto Log);
    public sealed record SystemLogFilter(string? Level, string? Source, string? Search, DateTimeOffset? From, DateTimeOffset? To, bool ErrorsOnly);
}

public sealed class SystemLogQuery
{
    public string? Level { get; init; }
    public string? Source { get; init; }
    public string? Search { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
    public bool ErrorsOnly { get; init; }
    public int Limit { get; init; } = 100;

    public AdminSystemLogsController.SystemLogFilter ToFilter() => new(Level, Source, Search, From, To, ErrorsOnly);
}

public sealed record SystemLogDto(
    Guid Id,
    DateTimeOffset Timestamp,
    string Source,
    string Level,
    string Category,
    string Message,
    string? Exception);

public sealed record DeleteSystemLogsRequest(Guid[]? Ids);
