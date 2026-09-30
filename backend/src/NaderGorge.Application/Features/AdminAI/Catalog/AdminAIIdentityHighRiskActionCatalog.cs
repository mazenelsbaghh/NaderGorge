using NaderGorge.Application.Features.AdminAI.Interfaces;

namespace NaderGorge.Application.Features.AdminAI.Catalog;

/// <summary>Reviewed high-risk identity candidates held until the full inventory passes.</summary>
public static class AdminAIIdentityHighRiskActionCatalog
{
    public static IReadOnlyList<AdminAICapabilityDefinition> CreateCandidates() =>
    [
        new("admin.identity.watch-request.approve", "candidate-1", "action", "strong", "strong", """
            {"type":"object","properties":{"requestId":{"type":"string","format":"uuid"},"reason":{"type":"string","maxLength":1000},"addedViews":{"type":"integer","minimum":1,"maximum":1000}},"required":["requestId","addedViews"],"additionalProperties":false}
            """, "{\"type\":\"object\"}", 0, 8_192, 5_000, "ApproveWatchRequestCommand", ["watch-progress", "watch-requests"])
    ];
}
