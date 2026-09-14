using MediatR;
using NaderGorge.Application.Features.AdminAI.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI.Actions;

public static class AdminAIActionCapabilityRegistration
{
    public static IReadOnlyList<IAdminAIActionCapability> CreateImplementedOrdinaryAdapters(
        IMediator mediator,
        IAdminAIActionPreviewSource preview) =>
        Array.AsReadOnly<IAdminAIActionCapability>(
        [
            new AdminAIAddStudentNoteAction(mediator, preview),
            new AdminAICreateSubjectAction(mediator, preview),
            new AdminAIUpdateSubjectAction(mediator, preview),
            new AdminAICreateVideoTypeAction(mediator, preview),
            new AdminAIUpdateVideoTypeAction(mediator, preview),
            new AdminAIApproveLessonCommentAction(mediator, preview),
            new AdminAIApproveCommunityPostAction(mediator, preview),
            new AdminAICreateFormAction(mediator, preview),
            new AdminAIUpdateFormAction(mediator, preview),
            new AdminAICreateTaskAction(mediator, preview),
            new AdminAIUpdateTaskStatusAction(mediator, preview),
            new AdminAIAddTaskCommentAction(mediator, preview),
            new AdminAICreateMediaPipelineAction(mediator, preview),
            new AdminAICreateSocialPlanAction(mediator, preview)
        ]);

    public static IReadOnlyList<IAdminAIActionCapability> ValidateOrdinaryCoverage(
        IAdminAICapabilityRegistry catalog,
        IEnumerable<IAdminAIActionCapability> adapters)
    {
        var materialized = adapters.ToArray();
        var ordinary = catalog.All.Where(x => x.Kind == "action" && x.Risk == "ordinary").ToDictionary(x => x.Key, StringComparer.Ordinal);
        if (ordinary.Count == 0)
            throw new InvalidOperationException("Ordinary Admin AI coverage cannot be validated against an empty catalog.");
        if (materialized.Length == 0)
            throw new InvalidOperationException("Ordinary Admin AI coverage requires at least one authoritative adapter.");

        var duplicates = materialized.GroupBy(x => x.Key, StringComparer.Ordinal).Where(x => x.Count() != 1).Select(x => x.Key).Order(StringComparer.Ordinal).ToArray();
        if (duplicates.Length > 0) throw new InvalidOperationException($"Duplicate ordinary Admin AI adapters: {string.Join(", ", duplicates)}");

        var unknown = materialized.Select(x => x.Key).Where(x => !ordinary.ContainsKey(x)).Order(StringComparer.Ordinal).ToArray();
        if (unknown.Length > 0) throw new InvalidOperationException($"Unlisted or non-ordinary Admin AI adapters: {string.Join(", ", unknown)}");

        var missing = ordinary.Keys.Except(materialized.Select(x => x.Key), StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (missing.Length > 0) throw new InvalidOperationException($"Missing ordinary Admin AI adapters: {string.Join(", ", missing)}");
        return Array.AsReadOnly(materialized.OrderBy(x => x.Key, StringComparer.Ordinal).ToArray());
    }

    public static IReadOnlyList<IAdminAIActionCapability> ValidateHighRiskCoverage(
        IAdminAICapabilityRegistry catalog,
        IEnumerable<IAdminAIActionCapability> adapters)
    {
        var materialized = adapters.ToArray();
        var duplicates = materialized.GroupBy(x => x.Key, StringComparer.Ordinal).Where(x => x.Count() != 1)
            .Select(x => x.Key).Order(StringComparer.Ordinal).ToArray();
        if (duplicates.Length > 0) throw new InvalidOperationException($"Duplicate high-risk Admin AI adapters: {string.Join(", ", duplicates)}");

        var strong = catalog.All.Where(x => x.Kind == "action" && x.Risk == "strong" && x.Confirmation == "strong")
            .ToDictionary(x => x.Key, StringComparer.Ordinal);
        var unknown = materialized.Select(x => x.Key).Where(x => !strong.ContainsKey(x)).Order(StringComparer.Ordinal).ToArray();
        if (unknown.Length > 0) throw new InvalidOperationException($"Unlisted or non-strong Admin AI adapters: {string.Join(", ", unknown)}");

        var missing = strong.Keys.Except(materialized.Select(x => x.Key), StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (missing.Length > 0) throw new InvalidOperationException($"Missing high-risk Admin AI adapters: {string.Join(", ", missing)}");
        return Array.AsReadOnly(materialized.OrderBy(x => x.Key, StringComparer.Ordinal).ToArray());
    }
}
