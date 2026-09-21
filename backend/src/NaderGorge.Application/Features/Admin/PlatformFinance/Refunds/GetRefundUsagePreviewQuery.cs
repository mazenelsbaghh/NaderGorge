using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.PlatformFinance.Refunds;

public sealed record GetRefundUsagePreviewQuery(Guid StudentId, Guid AccessGrantId, Guid? PurchaseOperationId)
    : IRequest<RefundUsagePreviewDto?>;

public sealed record RefundUsagePreviewDto(
    decimal PaidAmount,
    decimal PreviouslyRefundedAmount,
    decimal RemainingRefundableAmount,
    string ScopeLabel,
    bool UsageAvailable,
    bool VideosAvailable,
    bool ExamsAvailable,
    string? UnavailableReason,
    int TotalVideos,
    int WatchedVideos,
    int CompletedVideos,
    int UnknownDurationVideos,
    int TotalExams,
    int AttemptedExams,
    int TotalAttempts,
    int SubmittedAttempts,
    string HistoricalUsageNote,
    bool IsHistoricalSource);

public sealed class GetRefundUsagePreviewQueryHandler(IAppDbContext db)
    : IRequestHandler<GetRefundUsagePreviewQuery, RefundUsagePreviewDto?>
{
    public async Task<RefundUsagePreviewDto?> Handle(GetRefundUsagePreviewQuery request, CancellationToken ct)
    {
        var grant = await db.StudentAccessGrants.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == request.AccessGrantId && x.UserId == request.StudentId, ct);
        if (grant is null || !grant.IsActive) return null;

        var source = request.PurchaseOperationId.HasValue
            ? await db.SalesFinancialEffects.AsNoTracking()
                .SingleOrDefaultAsync(x => x.PurchaseOperationId == request.PurchaseOperationId.Value && x.StudentId == request.StudentId, ct)
            : null;
        var isHistoricalSource = source is null && !request.PurchaseOperationId.HasValue;
        if (source is not null && (source.TargetId != TargetId(grant) || source.TargetType != TargetType(grant))) return null;
        if (source is null && !isHistoricalSource) return null;

        var sourceAmount = source?.PaidAmount ?? await ResolveHistoricalSourceAmount(grant, ct);
        if (sourceAmount is null || sourceAmount <= 0m) return null;
        var sourceId = source?.PurchaseOperationId ?? grant.Id;

        var refunded = await db.PlatformRefunds.AsNoTracking()
            .Where(x => x.OriginalSourceId == sourceId
                && x.Status != PlatformRefundStatus.Reversed)
            .SumAsync(x => (decimal?)(x.PlatformAmount + x.TeacherAmount), ct) ?? 0m;

        var remaining = Math.Max(0m, sourceAmount.Value - refunded);
        var lessonIds = await ResolveLessonIds(grant, ct);
        if (lessonIds is null)
            return Empty(sourceAmount.Value, refunded, remaining, ScopeLabel(grant.GrantType),
                grant.GrantType == CodeType.Video ? "نوع منح الفيديو غير مدعوم في معاينة الاستخدام." : "نوع المنحة لا يحتوي محتوى يمكن قياسه.",
                isHistoricalSource);

        var videoIds = grant.GrantType == CodeType.Video && grant.LessonVideoId.HasValue
            ? [grant.LessonVideoId.Value]
            : await db.LessonVideos.AsNoTracking().Where(x => lessonIds.Contains(x.LessonId)).Select(x => x.Id).ToListAsync(ct);
        List<Guid> examIds;
        if (grant.GrantType == CodeType.Exam && grant.ExamId.HasValue) examIds = [grant.ExamId.Value];
        else if (grant.GrantType == CodeType.Video)
            examIds = await db.LessonVideos.AsNoTracking().Where(x => videoIds.Contains(x.Id) && x.ExamId.HasValue)
                .Select(x => x.ExamId!.Value).Distinct().ToListAsync(ct);
        else examIds = await db.Lessons.AsNoTracking().Where(x => lessonIds.Contains(x.Id)).Select(x => x.ExamId)
                .Concat(db.LessonVideos.AsNoTracking().Where(x => videoIds.Contains(x.Id)).Select(x => x.ExamId))
                .Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToListAsync(ct);

        var progress = await StudentWatchProgressReader.ReadAsync(
            new StudentLessonCompletionContext(db, request.StudentId, lessonIds), videoIds, ct);
        var attempts = await db.StudentExamAttempts.AsNoTracking()
            .Where(x => x.UserId == request.StudentId && examIds.Contains(x.ExamId))
            .Select(x => new
            {
                x.Id,
                x.ExamId,
                Submitted = x.Evaluation != null
                    || db.EssaySubmissions.Any(e => e.StudentExamAttemptId == x.Id)
                    || db.StudentAnswers.Any(a => a.StudentExamAttemptId == x.Id
                        && (a.SelectedOptionId != null || (a.SubmittedText != null && a.SubmittedText != "")))
            }).ToListAsync(ct);

        var videosAvailable = grant.GrantType != CodeType.Exam;
        return new(sourceAmount.Value, refunded, remaining, ScopeLabel(grant.GrantType), true, videosAvailable, true, null,
            videoIds.Count, progress.Count(x => x.WatchedSeconds > 0), progress.Count(x => x.IsCompleted), progress.Count(x => x.DurationSeconds is null or <= 0),
            examIds.Count, attempts.Select(x => x.ExamId).Distinct().Count(), attempts.Count, attempts.Count(x => x.Submitted),
            isHistoricalSource
                ? "هذه منحة قديمة بلا عملية شراء مرتبطة؛ سعر المحتوى الحالي هو الحد الأقصى فقط، ويجب إدخال المبلغ المدفوع فعليًا يدويًا."
                : "قد تشمل الأرقام استخدامًا سابقًا لنفس المحتوى؛ سجلات المشاهدة والمحاولات غير مرتبطة بعملية الشراء نفسها.",
            isHistoricalSource);
    }

    private async Task<decimal?> ResolveHistoricalSourceAmount(StudentAccessGrant grant, CancellationToken ct) => grant.GrantType switch
    {
        CodeType.Package when grant.PackageId.HasValue => await db.Packages.AsNoTracking()
            .Where(x => x.Id == grant.PackageId.Value).Select(x => (decimal?)x.Price).SingleOrDefaultAsync(ct),
        CodeType.Term when grant.TermId.HasValue => await db.Terms.AsNoTracking()
            .Where(x => x.Id == grant.TermId.Value).Select(x => (decimal?)x.Price).SingleOrDefaultAsync(ct),
        CodeType.Month when grant.ContentSectionId.HasValue => await db.ContentSections.AsNoTracking()
            .Where(x => x.Id == grant.ContentSectionId.Value).Select(x => (decimal?)x.Price).SingleOrDefaultAsync(ct),
        CodeType.Lesson when grant.LessonId.HasValue => await db.Lessons.AsNoTracking()
            .Where(x => x.Id == grant.LessonId.Value).Select(x => (decimal?)x.Price).SingleOrDefaultAsync(ct),
        CodeType.Exam when grant.PublicExamProductId.HasValue => await db.PublicExamProducts.AsNoTracking()
            .Where(x => x.Id == grant.PublicExamProductId.Value).Select(x => (decimal?)x.Price).SingleOrDefaultAsync(ct),
        CodeType.Exam when grant.ExamId.HasValue => await db.PublicExamProducts.AsNoTracking()
            .Where(x => x.ExamId == grant.ExamId.Value).Select(x => (decimal?)x.Price).SingleOrDefaultAsync(ct),
        _ => null
    };

    private async Task<List<Guid>?> ResolveLessonIds(StudentAccessGrant grant, CancellationToken ct)
    {
        if (grant.GrantType == CodeType.Exam) return [];
        if (grant.GrantType == CodeType.Video && grant.LessonVideoId.HasValue)
            return await db.LessonVideos.AsNoTracking().Where(x => x.Id == grant.LessonVideoId).Select(x => x.LessonId).ToListAsync(ct);
        if (grant.GrantType == CodeType.Video || grant.GrantType == CodeType.Balance) return null;

        var lessons = db.Lessons.AsNoTracking().AsQueryable();
        lessons = grant.GrantType switch
        {
            CodeType.Package => lessons.Where(x => x.ContentSection.Term.PackageId == grant.PackageId),
            CodeType.Term => lessons.Where(x => x.ContentSection.TermId == grant.TermId),
            CodeType.Month => lessons.Where(x => x.ContentSectionId == grant.ContentSectionId),
            CodeType.Lesson => lessons.Where(x => x.Id == grant.LessonId),
            _ => lessons.Where(_ => false)
        };
        return await lessons.Select(x => x.Id).ToListAsync(ct);
    }

    private static Guid? TargetId(StudentAccessGrant grant) => grant.GrantType switch
    {
        CodeType.Package => grant.PackageId,
        CodeType.Term => grant.TermId,
        CodeType.Month => grant.ContentSectionId,
        CodeType.Lesson => grant.LessonId,
        CodeType.Video when grant.LessonVideoId.HasValue => grant.LessonVideoId,
        CodeType.Video => grant.VideoTypeId,
        CodeType.Exam => grant.PublicExamProductId ?? grant.ExamId,
        _ => null
    };

    private static SalesTargetType? TargetType(StudentAccessGrant grant) => grant.GrantType switch
    {
        CodeType.Package => SalesTargetType.Package,
        CodeType.Term => SalesTargetType.Term,
        CodeType.Month => SalesTargetType.ContentSection,
        CodeType.Lesson => SalesTargetType.Lesson,
        CodeType.Video when grant.LessonVideoId.HasValue => SalesTargetType.SpecificVideo,
        CodeType.Video when grant.VideoTypeId.HasValue => SalesTargetType.VideoType,
        CodeType.Exam => SalesTargetType.PublicExam,
        _ => null
    };

    private static string ScopeLabel(CodeType type) => type switch
    {
        CodeType.Package => "الباقة كاملة",
        CodeType.Term => "الترم المحدد",
        CodeType.Month => "الشهر المحدد",
        CodeType.Lesson => "الدرس المحدد",
        CodeType.Video => "الفيديو المحدد",
        CodeType.Exam => "الامتحان المحدد",
        _ => "غير متاح"
    };

    private static RefundUsagePreviewDto Empty(decimal paid, decimal refunded, decimal remaining, string label, string reason, bool isHistoricalSource) =>
        new(paid, refunded, remaining, label, false, false, false, reason, 0, 0, 0, 0, 0, 0, 0, 0,
            "قد تشمل الأرقام استخدامًا سابقًا لنفس المحتوى؛ سجلات الاستخدام غير مرتبطة بعملية الشراء نفسها.", isHistoricalSource);
}
