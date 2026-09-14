using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Content;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI.Reads;

internal sealed class AdminAIStudentSubscriptionSnapshotReader
{
    private readonly IAppDbContext db;
    private readonly AdminAIStudentSubscriptionTargetLoader targetLoader;

    public AdminAIStudentSubscriptionSnapshotReader(IAppDbContext db)
    {
        this.db = db;
        targetLoader = new(db);
    }

    public async Task<AdminAIStudentSnapshotSection<AdminAIStudentSubscriptionsSection>> LoadAsync(
        AdminAIStudentSnapshotRequest request,
        CancellationToken ct)
    {
        var grantAggregates = await LoadGrantAggregatesAsync(request, ct);
        var recentCandidates = await LoadRecentCandidatesAsync(request, ct);
        var visibleGrants = recentCandidates.Take(request.RecentLimit).ToArray();
        var targets = await targetLoader.LoadAsync(visibleGrants, ct);
        var recentEntitlements = visibleGrants
            .Select(grant => BuildSubscriptionItem(grant, Classify(grant, request.DataAsOf), targets))
            .ToArray();
        var contextEntitlement = await LoadContextTeacherEntitlementAsync(request, ct);
        var subscriptions = BuildSection(grantAggregates, recentEntitlements, contextEntitlement);
        return new(subscriptions, recentCandidates.Count > request.RecentLimit);
    }

    private async Task<IReadOnlyList<GrantAggregate>> LoadGrantAggregatesAsync(
        AdminAIStudentSnapshotRequest request,
        CancellationToken ct)
    {
        var asOf = request.DataAsOf;
        return await db.StudentAccessGrants.AsNoTracking()
            .Where(grant => grant.UserId == request.StudentId && grant.GrantType != CodeType.Balance)
            .GroupBy(grant => grant.GrantType)
            .Select(group => new GrantAggregate(
                group.Key,
                group.Count(),
                group.Count(grant =>
                    !grant.CancelledAt.HasValue &&
                    (!grant.ExpiresAt.HasValue || grant.ExpiresAt > asOf) &&
                    (grant.GrantType != CodeType.Video ||
                     !grant.MaxUses.HasValue ||
                     grant.UsesConsumed < grant.MaxUses.Value) &&
                    grant.IsActive),
                group.Count(grant => grant.CancelledAt.HasValue),
                group.Count(grant =>
                    !grant.CancelledAt.HasValue &&
                    grant.ExpiresAt.HasValue &&
                    grant.ExpiresAt <= asOf),
                group.Count(grant =>
                    !grant.CancelledAt.HasValue &&
                    (!grant.ExpiresAt.HasValue || grant.ExpiresAt > asOf) &&
                    grant.GrantType == CodeType.Video &&
                    grant.MaxUses.HasValue &&
                    grant.UsesConsumed >= grant.MaxUses.Value),
                group.Count(grant =>
                    !grant.CancelledAt.HasValue &&
                    (!grant.ExpiresAt.HasValue || grant.ExpiresAt > asOf) &&
                    (grant.GrantType != CodeType.Video ||
                     !grant.MaxUses.HasValue ||
                     grant.UsesConsumed < grant.MaxUses.Value) &&
                    !grant.IsActive)))
            .ToArrayAsync(ct);
    }

    private async Task<IReadOnlyList<AdminAIStudentAccessGrant>> LoadRecentCandidatesAsync(
        AdminAIStudentSnapshotRequest request,
        CancellationToken ct)
    {
        if (request.RecentLimit == 0)
            return [];

        var asOf = request.DataAsOf;
        return await db.StudentAccessGrants.AsNoTracking()
            .Where(grant => grant.UserId == request.StudentId && grant.GrantType != CodeType.Balance)
            .OrderBy(grant =>
                !grant.CancelledAt.HasValue &&
                (!grant.ExpiresAt.HasValue || grant.ExpiresAt > asOf) &&
                (grant.GrantType != CodeType.Video ||
                 !grant.MaxUses.HasValue ||
                 grant.UsesConsumed < grant.MaxUses.Value) &&
                grant.IsActive ? 0 : 1)
            .ThenByDescending(grant => grant.GrantedAt)
            .ThenByDescending(grant => grant.Id)
            .Take(request.RecentLimit + 1)
            .Select(grant => new AdminAIStudentAccessGrant(
                grant.Id,
                grant.GrantType,
                grant.PackageId,
                grant.TermId,
                grant.ContentSectionId,
                grant.LessonId,
                grant.LessonVideoId,
                grant.VideoTypeId,
                grant.ExamId,
                grant.PublicExamProductId,
                grant.AccessCodeId,
                grant.GiftRecipientId,
                grant.GrantedAt,
                grant.ExpiresAt,
                grant.IsActive,
                grant.CancelledAt,
                grant.MaxUses,
                grant.UsesConsumed))
            .ToArrayAsync(ct);
    }

    private static AdminAIStudentSubscriptionsSection BuildSection(
        IReadOnlyList<GrantAggregate> aggregates,
        IReadOnlyList<AdminAIStudentSubscriptionItem> recentEntitlements,
        AdminAIStudentTeacherEntitlement? contextEntitlement) =>
        new(
            aggregates.Sum(aggregate => aggregate.Total),
            aggregates.Sum(aggregate => aggregate.Active),
            aggregates.Sum(aggregate => aggregate.Cancelled),
            aggregates.Sum(aggregate => aggregate.Expired),
            aggregates.Sum(aggregate => aggregate.Exhausted),
            aggregates.Sum(aggregate => aggregate.Inactive),
            aggregates.OrderBy(aggregate => aggregate.GrantType)
                .Select(aggregate => new AdminAIStudentSubscriptionTypeCount(
                    aggregate.GrantType.ToString(),
                    aggregate.Total))
                .ToArray(),
            recentEntitlements,
            contextEntitlement,
            true,
            "الاشتراك يُثبت من منحة محتوى فعالة فقط؛ رصيد المدرس لا يثبت اشتراكًا. وقد تطبق شاشة المحتوى قيود الإتاحة والأرشفة والمرحلة الدراسية أيضًا.");

    private async Task<AdminAIStudentTeacherEntitlement?> LoadContextTeacherEntitlementAsync(
        AdminAIStudentSnapshotRequest request,
        CancellationToken ct)
    {
        if (!request.SubscriptionContextTeacherId.HasValue)
            return null;

        var teacher = await db.TeacherProfiles.AsNoTracking()
            .Where(profile => profile.Id == request.SubscriptionContextTeacherId.Value && !profile.User.IsDeleted)
            .Select(profile => new { profile.Id, profile.User.FullName })
            .SingleOrDefaultAsync(ct);
        if (teacher is null)
            throw new InvalidOperationException("The context teacher is unavailable.");

        var subscriberFacts = await new TeacherSubscriberFactSource(db)
            .LoadStudentAsync(teacher.Id, request.StudentId, ct);
        var effectiveFacts = subscriberFacts
            .Where(fact => TeacherSubscriberCalculator.IsEffectiveAt(fact, request.DataAsOf))
            .ToArray();
        var countsByType = effectiveFacts
            .GroupBy(fact => fact.GrantType)
            .OrderBy(group => group.Key)
            .Select(group => new AdminAIStudentSubscriptionTypeCount(group.Key.ToString(), group.Count()))
            .ToArray();
        return new(
            teacher.Id,
            AdminAIReadArguments.SafeText(teacher.FullName, 120),
            effectiveFacts.Length > 0,
            effectiveFacts.Length,
            countsByType);
    }

    private static AdminAIStudentSubscriptionItem BuildSubscriptionItem(
        AdminAIStudentAccessGrant grant,
        StudentEntitlementState state,
        IReadOnlyDictionary<AdminAIStudentTargetKey, AdminAIStudentSubscriptionTarget> targets)
    {
        var primaryTarget = FindTarget(targets, PrimaryTargetKey(grant));
        var hierarchyTarget = FindTarget(targets, HierarchyTargetKey(grant));
        var codeTarget = FindTarget(targets, OptionalKey(grant.AccessCodeId, AdminAIStudentTargetKey.AccessCode));
        var giftTarget = FindTarget(targets, OptionalKey(grant.GiftRecipientId, AdminAIStudentTargetKey.Gift));
        var teacherTarget = SelectTeacherTarget(primaryTarget, hierarchyTarget, codeTarget, giftTarget);
        var contentName = SelectContentName(primaryTarget, codeTarget);
        var stateName = StateName(state);

        return new(
            grant.Id,
            grant.GrantType.ToString(),
            primaryTarget?.ContentId,
            AdminAIReadArguments.SafeText(contentName, 160),
            teacherTarget?.TeacherId,
            AdminAIReadArguments.SafeText(teacherTarget?.TeacherName, 120),
            SourceName(grant),
            stateName,
            state == StudentEntitlementState.Active,
            grant.GrantedAt,
            grant.ExpiresAt,
            grant.CancelledAt);
    }

    private static AdminAIStudentSubscriptionTarget? SelectTeacherTarget(
        AdminAIStudentSubscriptionTarget? primary,
        AdminAIStudentSubscriptionTarget? hierarchy,
        AdminAIStudentSubscriptionTarget? code,
        AdminAIStudentSubscriptionTarget? gift) =>
        primary?.TeacherId.HasValue == true ? primary :
        hierarchy?.TeacherId.HasValue == true ? hierarchy :
        code?.TeacherId.HasValue == true ? code : gift;

    private static string SelectContentName(
        AdminAIStudentSubscriptionTarget? primary,
        AdminAIStudentSubscriptionTarget? code)
    {
        if (!string.IsNullOrWhiteSpace(primary?.ContentName))
            return primary.ContentName;
        if (!string.IsNullOrWhiteSpace(code?.ContentName))
            return code.ContentName;
        return "محتوى غير متاح";
    }

    private static string SourceName(AdminAIStudentAccessGrant grant) =>
        grant.GiftRecipientId.HasValue ? "gift" :
        grant.AccessCodeId.HasValue ? "code" : "direct_or_purchase";

    private static StudentEntitlementState Classify(AdminAIStudentAccessGrant grant, DateTime asOf) =>
        grant.CancelledAt.HasValue ? StudentEntitlementState.Cancelled :
        grant.ExpiresAt.HasValue && grant.ExpiresAt <= asOf ? StudentEntitlementState.Expired :
        grant.GrantType == CodeType.Video &&
        grant.MaxUses.HasValue &&
        grant.UsesConsumed >= grant.MaxUses.Value ? StudentEntitlementState.Exhausted :
        !grant.IsActive ? StudentEntitlementState.Inactive : StudentEntitlementState.Active;

    private static string StateName(StudentEntitlementState state) => state switch
    {
        StudentEntitlementState.Active => "active",
        StudentEntitlementState.Cancelled => "cancelled",
        StudentEntitlementState.Expired => "expired",
        StudentEntitlementState.Exhausted => "exhausted",
        StudentEntitlementState.Inactive => "inactive",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
    };

    private static AdminAIStudentSubscriptionTarget? FindTarget(
        IReadOnlyDictionary<AdminAIStudentTargetKey, AdminAIStudentSubscriptionTarget> targets,
        AdminAIStudentTargetKey? key) =>
        key.HasValue && targets.TryGetValue(key.Value, out var target) ? target : null;

    private static AdminAIStudentTargetKey? HierarchyTargetKey(AdminAIStudentAccessGrant grant) =>
        OptionalKey(grant.PackageId, AdminAIStudentTargetKey.Package) ??
        OptionalKey(grant.TermId, AdminAIStudentTargetKey.Term) ??
        OptionalKey(grant.ContentSectionId, AdminAIStudentTargetKey.ContentSection) ??
        OptionalKey(grant.LessonId, AdminAIStudentTargetKey.Lesson);

    private static AdminAIStudentTargetKey? PrimaryTargetKey(AdminAIStudentAccessGrant grant) => grant.GrantType switch
    {
        CodeType.Package when grant.PackageId.HasValue => AdminAIStudentTargetKey.Package(grant.PackageId.Value),
        CodeType.Term when grant.TermId.HasValue => AdminAIStudentTargetKey.Term(grant.TermId.Value),
        CodeType.Month when grant.ContentSectionId.HasValue => AdminAIStudentTargetKey.ContentSection(grant.ContentSectionId.Value),
        CodeType.Lesson when grant.LessonId.HasValue => AdminAIStudentTargetKey.Lesson(grant.LessonId.Value),
        CodeType.Video when grant.LessonVideoId.HasValue => AdminAIStudentTargetKey.Video(grant.LessonVideoId.Value),
        CodeType.Video when grant.VideoTypeId.HasValue => AdminAIStudentTargetKey.VideoType(grant.VideoTypeId.Value),
        CodeType.Exam when grant.PublicExamProductId.HasValue => AdminAIStudentTargetKey.PublicExam(grant.PublicExamProductId.Value),
        CodeType.Exam when grant.ExamId.HasValue => AdminAIStudentTargetKey.Exam(grant.ExamId.Value),
        _ => null
    };

    private static AdminAIStudentTargetKey? OptionalKey(
        Guid? id,
        Func<Guid, AdminAIStudentTargetKey> keyFactory) =>
        id.HasValue ? keyFactory(id.Value) : null;

    private enum StudentEntitlementState
    {
        Active,
        Cancelled,
        Expired,
        Exhausted,
        Inactive
    }

    private sealed record GrantAggregate(
        CodeType GrantType,
        int Total,
        int Active,
        int Cancelled,
        int Expired,
        int Exhausted,
        int Inactive);
}
