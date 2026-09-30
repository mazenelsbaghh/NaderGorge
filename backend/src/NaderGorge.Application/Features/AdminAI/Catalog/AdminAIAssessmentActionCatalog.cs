using NaderGorge.Application.Features.AdminAI.Interfaces;

namespace NaderGorge.Application.Features.AdminAI.Catalog;

/// <summary>Reviewed moderation candidates held until the full inventory passes.</summary>
public static class AdminAIAssessmentActionCatalog
{
    public static IReadOnlyList<AdminAICapabilityDefinition> CreateCandidates() =>
    [
        new("admin.assessment.community-comment.approve", "candidate-1", "action", "ordinary", "ordinary", """
            {"type":"object","properties":{"commentId":{"type":"string","format":"uuid"}},"required":["commentId"],"additionalProperties":false}
            """, "{\"type\":\"object\"}", 0, 8_192, 5_000, "ApproveCommunityCommentCommand", ["community-comments", "moderation"]),
        new("admin.assessment.community-post.approve", "candidate-1", "action", "ordinary", "ordinary", """
            {"type":"object","properties":{"postId":{"type":"string","format":"uuid"}},"required":["postId"],"additionalProperties":false}
            """, "{\"type\":\"object\"}", 0, 8_192, 5_000, "ApproveCommunityPostCommand", ["community-posts", "moderation"]),
        new("admin.assessment.lesson-comment.approve", "candidate-1", "action", "ordinary", "ordinary", """
            {"type":"object","properties":{"commentId":{"type":"string","format":"uuid"}},"required":["commentId"],"additionalProperties":false}
            """, "{\"type\":\"object\"}", 0, 8_192, 5_000, "ApproveLessonCommentCommand", ["lesson-comments", "moderation"])
    ];
}
