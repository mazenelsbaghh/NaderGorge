using NaderGorge.Application.Features.AdminAI.Interfaces;

namespace NaderGorge.Application.Features.AdminAI.Catalog;

/// <summary>A reviewed candidate; production keeps action capabilities disabled until inventory gates pass.</summary>
public static class AdminAITeacherFinancialReviewActionCatalog
{
    public static IReadOnlyList<AdminAICapabilityDefinition> CreateCandidates() =>
    [
        new("admin.finance.teacher-event.review", "candidate-1", "action", "strong", "strong", """
            {"type":"object","properties":{"allocationId":{"type":"string","format":"uuid"},"status":{"type":"integer","enum":[2,3]},"note":{"type":"string","maxLength":1000}},"required":["allocationId","status"],"additionalProperties":false}
            """, "{\"type\":\"object\"}", 0, 8_192, 5_000,
            "ReviewTeacherFinancialAllocationCommand", ["teacher-finance", "finance"])
    ];
}
