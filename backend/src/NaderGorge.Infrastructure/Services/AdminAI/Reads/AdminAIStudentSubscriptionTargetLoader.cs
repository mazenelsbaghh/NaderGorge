using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI.Reads;

internal sealed class AdminAIStudentSubscriptionTargetLoader(IAppDbContext db)
{
    public async Task<IReadOnlyDictionary<AdminAIStudentTargetKey, AdminAIStudentSubscriptionTarget>> LoadAsync(
        IReadOnlyCollection<AdminAIStudentAccessGrant> grants,
        CancellationToken ct)
    {
        var targets = new Dictionary<AdminAIStudentTargetKey, AdminAIStudentSubscriptionTarget>();
        if (grants.Count == 0)
            return targets;

        if (grants.Any(grant => grant.PackageId.HasValue))
            await LoadPackagesAsync(grants, targets, ct);
        if (grants.Any(grant => grant.TermId.HasValue))
            await LoadTermsAsync(grants, targets, ct);
        if (grants.Any(grant => grant.ContentSectionId.HasValue))
            await LoadContentSectionsAsync(grants, targets, ct);
        if (grants.Any(grant => grant.LessonId.HasValue))
            await LoadLessonsAsync(grants, targets, ct);
        if (grants.Any(grant => grant.LessonVideoId.HasValue))
            await LoadVideosAsync(grants, targets, ct);
        if (grants.Any(grant => grant.VideoTypeId.HasValue))
            await LoadVideoTypesAsync(grants, targets, ct);
        if (grants.Any(grant => grant.ExamId.HasValue))
            await LoadExamsAsync(grants, targets, ct);
        if (grants.Any(grant => grant.PublicExamProductId.HasValue))
            await LoadPublicExamProductsAsync(grants, targets, ct);
        if (grants.Any(grant => grant.AccessCodeId.HasValue))
            await LoadAccessCodesAsync(grants, targets, ct);
        if (grants.Any(grant => grant.GiftRecipientId.HasValue))
            await LoadGiftRecipientsAsync(grants, targets, ct);
        return targets;
    }

    private async Task LoadPackagesAsync(
        IEnumerable<AdminAIStudentAccessGrant> grants,
        IDictionary<AdminAIStudentTargetKey, AdminAIStudentSubscriptionTarget> targets,
        CancellationToken ct)
    {
        var ids = grants.Select(grant => grant.PackageId).OfType<Guid>().Distinct().ToArray();
        var packages = await db.Packages.AsNoTracking()
            .Where(package => ids.Contains(package.Id))
            .Select(package => new
            {
                package.Id,
                package.Name,
                package.TeacherId,
                TeacherName = package.Teacher.User.FullName
            })
            .ToListAsync(ct);
        foreach (var package in packages)
            targets[AdminAIStudentTargetKey.Package(package.Id)] =
                new(package.Id, package.Name, package.TeacherId, package.TeacherName);
    }

    private async Task LoadTermsAsync(
        IEnumerable<AdminAIStudentAccessGrant> grants,
        IDictionary<AdminAIStudentTargetKey, AdminAIStudentSubscriptionTarget> targets,
        CancellationToken ct)
    {
        var ids = grants.Select(grant => grant.TermId).OfType<Guid>().Distinct().ToArray();
        var terms = await db.Terms.AsNoTracking()
            .Where(term => ids.Contains(term.Id))
            .Select(term => new
            {
                term.Id,
                term.Title,
                term.Package.TeacherId,
                TeacherName = term.Package.Teacher.User.FullName
            })
            .ToListAsync(ct);
        foreach (var term in terms)
            targets[AdminAIStudentTargetKey.Term(term.Id)] =
                new(term.Id, term.Title, term.TeacherId, term.TeacherName);
    }

    private async Task LoadContentSectionsAsync(
        IEnumerable<AdminAIStudentAccessGrant> grants,
        IDictionary<AdminAIStudentTargetKey, AdminAIStudentSubscriptionTarget> targets,
        CancellationToken ct)
    {
        var ids = grants.Select(grant => grant.ContentSectionId).OfType<Guid>().Distinct().ToArray();
        var contentSections = await db.ContentSections.AsNoTracking()
            .Where(section => ids.Contains(section.Id))
            .Select(section => new
            {
                section.Id,
                section.Title,
                section.Term.Package.TeacherId,
                TeacherName = section.Term.Package.Teacher.User.FullName
            })
            .ToListAsync(ct);
        foreach (var contentSection in contentSections)
            targets[AdminAIStudentTargetKey.ContentSection(contentSection.Id)] =
                new(contentSection.Id, contentSection.Title, contentSection.TeacherId, contentSection.TeacherName);
    }

    private async Task LoadLessonsAsync(
        IEnumerable<AdminAIStudentAccessGrant> grants,
        IDictionary<AdminAIStudentTargetKey, AdminAIStudentSubscriptionTarget> targets,
        CancellationToken ct)
    {
        var ids = grants.Select(grant => grant.LessonId).OfType<Guid>().Distinct().ToArray();
        var lessons = await db.Lessons.AsNoTracking()
            .Where(lesson => ids.Contains(lesson.Id))
            .Select(lesson => new
            {
                lesson.Id,
                lesson.Title,
                lesson.ContentSection.Term.Package.TeacherId,
                TeacherName = lesson.ContentSection.Term.Package.Teacher.User.FullName
            })
            .ToListAsync(ct);
        foreach (var lesson in lessons)
            targets[AdminAIStudentTargetKey.Lesson(lesson.Id)] =
                new(lesson.Id, lesson.Title, lesson.TeacherId, lesson.TeacherName);
    }

    private async Task LoadVideosAsync(
        IEnumerable<AdminAIStudentAccessGrant> grants,
        IDictionary<AdminAIStudentTargetKey, AdminAIStudentSubscriptionTarget> targets,
        CancellationToken ct)
    {
        var ids = grants.Select(grant => grant.LessonVideoId).OfType<Guid>().Distinct().ToArray();
        var videos = await db.LessonVideos.AsNoTracking()
            .Where(video => ids.Contains(video.Id))
            .Select(video => new
            {
                video.Id,
                video.Title,
                video.Lesson.ContentSection.Term.Package.TeacherId,
                TeacherName = video.Lesson.ContentSection.Term.Package.Teacher.User.FullName
            })
            .ToListAsync(ct);
        foreach (var video in videos)
            targets[AdminAIStudentTargetKey.Video(video.Id)] =
                new(video.Id, video.Title, video.TeacherId, video.TeacherName);
    }

    private async Task LoadVideoTypesAsync(
        IEnumerable<AdminAIStudentAccessGrant> grants,
        IDictionary<AdminAIStudentTargetKey, AdminAIStudentSubscriptionTarget> targets,
        CancellationToken ct)
    {
        var ids = grants.Select(grant => grant.VideoTypeId).OfType<Guid>().Distinct().ToArray();
        var videoTypes = await db.VideoTypes.AsNoTracking()
            .Where(videoType => ids.Contains(videoType.Id))
            .Select(videoType => new { videoType.Id, videoType.Name })
            .ToListAsync(ct);
        foreach (var videoType in videoTypes)
            targets[AdminAIStudentTargetKey.VideoType(videoType.Id)] =
                new(videoType.Id, videoType.Name, null, null);
    }

    private async Task LoadExamsAsync(
        IEnumerable<AdminAIStudentAccessGrant> grants,
        IDictionary<AdminAIStudentTargetKey, AdminAIStudentSubscriptionTarget> targets,
        CancellationToken ct)
    {
        var ids = grants.Select(grant => grant.ExamId).OfType<Guid>().Distinct().ToArray();
        var exams = await db.Exams.AsNoTracking()
            .Where(exam => ids.Contains(exam.Id))
            .Select(exam => new
            {
                exam.Id,
                exam.Title,
                TeacherId = exam.CreatedByTeacherId,
                TeacherName = exam.CreatedByTeacher.User.FullName
            })
            .ToListAsync(ct);
        foreach (var exam in exams)
            targets[AdminAIStudentTargetKey.Exam(exam.Id)] =
                new(exam.Id, exam.Title, exam.TeacherId, exam.TeacherName);
    }

    private async Task LoadPublicExamProductsAsync(
        IEnumerable<AdminAIStudentAccessGrant> grants,
        IDictionary<AdminAIStudentTargetKey, AdminAIStudentSubscriptionTarget> targets,
        CancellationToken ct)
    {
        var ids = grants.Select(grant => grant.PublicExamProductId).OfType<Guid>().Distinct().ToArray();
        var products = await db.PublicExamProducts.AsNoTracking()
            .Where(product => ids.Contains(product.Id))
            .Select(product => new
            {
                product.Id,
                product.Exam.Title,
                TeacherId = product.TeacherId ?? product.Exam.CreatedByTeacherId,
                TeacherName = product.Teacher != null
                    ? product.Teacher.User.FullName
                    : product.Exam.CreatedByTeacher.User.FullName
            })
            .ToListAsync(ct);
        foreach (var product in products)
            targets[AdminAIStudentTargetKey.PublicExam(product.Id)] =
                new(product.Id, product.Title, product.TeacherId, product.TeacherName);
    }

    private async Task LoadAccessCodesAsync(
        IEnumerable<AdminAIStudentAccessGrant> grants,
        IDictionary<AdminAIStudentTargetKey, AdminAIStudentSubscriptionTarget> targets,
        CancellationToken ct)
    {
        var ids = grants.Select(grant => grant.AccessCodeId).OfType<Guid>().Distinct().ToArray();
        var codes = await db.AccessCodes.AsNoTracking()
            .Where(code => ids.Contains(code.Id))
            .Select(code => new
            {
                code.Id,
                code.CodeGroup.Name,
                code.CodeGroup.TeacherId,
                TeacherName = code.CodeGroup.Teacher == null ? null : code.CodeGroup.Teacher.User.FullName
            })
            .ToListAsync(ct);
        foreach (var code in codes)
            targets[AdminAIStudentTargetKey.AccessCode(code.Id)] =
                new(null, code.Name, code.TeacherId, code.TeacherName);
    }

    private async Task LoadGiftRecipientsAsync(
        IEnumerable<AdminAIStudentAccessGrant> grants,
        IDictionary<AdminAIStudentTargetKey, AdminAIStudentSubscriptionTarget> targets,
        CancellationToken ct)
    {
        var ids = grants.Select(grant => grant.GiftRecipientId).OfType<Guid>().Distinct().ToArray();
        var gifts = await db.GiftRecipients.AsNoTracking()
            .Where(recipient => ids.Contains(recipient.Id))
            .Select(recipient => new
            {
                recipient.Id,
                recipient.GiftIssuance.TeacherId,
                TeacherName = recipient.GiftIssuance.Teacher == null
                    ? null
                    : recipient.GiftIssuance.Teacher.User.FullName
            })
            .ToListAsync(ct);
        foreach (var gift in gifts)
            targets[AdminAIStudentTargetKey.Gift(gift.Id)] =
                new(null, string.Empty, gift.TeacherId, gift.TeacherName);
    }
}

internal enum AdminAIStudentTargetKind
{
    Package,
    Term,
    ContentSection,
    Lesson,
    Video,
    VideoType,
    Exam,
    PublicExam,
    AccessCode,
    Gift
}

internal readonly record struct AdminAIStudentTargetKey(AdminAIStudentTargetKind Kind, Guid Id)
{
    public static AdminAIStudentTargetKey Package(Guid id) => new(AdminAIStudentTargetKind.Package, id);
    public static AdminAIStudentTargetKey Term(Guid id) => new(AdminAIStudentTargetKind.Term, id);
    public static AdminAIStudentTargetKey ContentSection(Guid id) => new(AdminAIStudentTargetKind.ContentSection, id);
    public static AdminAIStudentTargetKey Lesson(Guid id) => new(AdminAIStudentTargetKind.Lesson, id);
    public static AdminAIStudentTargetKey Video(Guid id) => new(AdminAIStudentTargetKind.Video, id);
    public static AdminAIStudentTargetKey VideoType(Guid id) => new(AdminAIStudentTargetKind.VideoType, id);
    public static AdminAIStudentTargetKey Exam(Guid id) => new(AdminAIStudentTargetKind.Exam, id);
    public static AdminAIStudentTargetKey PublicExam(Guid id) => new(AdminAIStudentTargetKind.PublicExam, id);
    public static AdminAIStudentTargetKey AccessCode(Guid id) => new(AdminAIStudentTargetKind.AccessCode, id);
    public static AdminAIStudentTargetKey Gift(Guid id) => new(AdminAIStudentTargetKind.Gift, id);
}

internal sealed record AdminAIStudentSubscriptionTarget(
    Guid? ContentId,
    string ContentName,
    Guid? TeacherId,
    string? TeacherName);

internal sealed record AdminAIStudentAccessGrant(
    Guid Id,
    CodeType GrantType,
    Guid? PackageId,
    Guid? TermId,
    Guid? ContentSectionId,
    Guid? LessonId,
    Guid? LessonVideoId,
    Guid? VideoTypeId,
    Guid? ExamId,
    Guid? PublicExamProductId,
    Guid? AccessCodeId,
    Guid? GiftRecipientId,
    DateTime GrantedAt,
    DateTime? ExpiresAt,
    bool IsActive,
    DateTime? CancelledAt,
    int? MaxUses,
    int UsesConsumed);
