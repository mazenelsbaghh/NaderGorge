using System.Text.Json;
using System.Text.Json.Nodes;
using NaderGorge.Domain.Entities;

namespace NaderGorge.Infrastructure.Services.MimStudio;

internal static class MimVideoOutcome
{
    private static JsonObject Read(string json) => JsonNode.Parse(json) switch
    {
        JsonObject outcome => outcome,
        JsonArray urls => new JsonObject { ["urls"] = urls },
        _ => throw new JsonException("Invalid saved video outcome.")
    };

    public static string? Error(string json) => Read(json)["error"]?.GetValue<string>();
    public static string[] Urls(string json) => Read(json)["urls"]?.Deserialize<string[]>() ?? [];

    public static string WithError(string json, string? error)
    {
        var outcome = Read(json);
        outcome["error"] = error;
        return outcome.ToJsonString();
    }

    public static string WithUrls(string json, string[] urls)
    {
        var outcome = Read(json);
        outcome["urls"] = JsonSerializer.SerializeToNode(urls);
        return outcome.ToJsonString();
    }

    public static string Reviewed(MimSceneVideo row)
    {
        var outcome = Read(row.ResultJson);
        var attempts = outcome["reviewedAttempts"]?.AsArray() ?? new JsonArray();
        if (attempts.Count >= 100) throw new HiggsfieldMcpException("وصل هذا المشهد إلى حد المراجعات. تواصل مع مسؤول المنصة.");
        attempts.Add(new JsonObject {
            ["version"] = row.Version.ToString(), ["reviewedBy"] = row.AdminUserId.ToString(),
            ["reviewedAt"] = DateTime.UtcNow, ["error"] = Error(row.ResultJson),
            ["assertion"] = "no_generation_and_no_charge", ["parameters"] = JsonNode.Parse(row.ParametersJson)
        });
        if (outcome["reviewedAttempts"] is null) outcome["reviewedAttempts"] = attempts;
        return outcome.ToJsonString();
    }
}
