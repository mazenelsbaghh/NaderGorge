using NaderGorge.Application.Features.AdminAI.Interfaces;

namespace NaderGorge.Application.Features.AdminAI.Catalog;

/// <summary>Reviewed operations actions, held as candidates until the full mutation inventory passes.</summary>
public static class AdminAIOperationsActionCatalog
{
    public static IReadOnlyList<AdminAICapabilityDefinition> CreateCandidates() =>
    [
        new("admin.operations.task.approval.resolve", "candidate-1", "action", "ordinary", "ordinary", """
            {"type":"object","properties":{"taskId":{"type":"string","format":"uuid"},"approve":{"type":"boolean"},"rejectionReason":{"type":"string","maxLength":1000}},"required":["taskId","approve"],"additionalProperties":false}
            """, "{\"type\":\"object\"}", 0, 8_192, 5_000, "AdminResolveApprovalCommand", ["operations-tasks", "media-pipelines", "task-comments"]),
        new("admin.operations.task.status.update", "candidate-1", "action", "ordinary", "ordinary", """
            {"type":"object","properties":{"taskId":{"type":"string","format":"uuid"},"status":{"type":"integer","minimum":1,"maximum":6}},"required":["taskId","status"],"additionalProperties":false}
            """, "{\"type\":\"object\"}", 0, 8_192, 5_000, "UpdateTaskStatusCommand", ["operations-tasks"]),
        new("admin.operations.task-comment.create", "candidate-1", "action", "ordinary", "ordinary", """
            {"type":"object","properties":{"taskId":{"type":"string","format":"uuid"},"content":{"type":"string","minLength":1,"maxLength":4000},"attachmentUrl":{"type":"string","maxLength":2048}},"required":["taskId","content"],"additionalProperties":false}
            """, "{\"type\":\"object\"}", 0, 8_192, 5_000, "AddTaskCommentCommand", ["operations-tasks", "task-comments"])
    ];
}
