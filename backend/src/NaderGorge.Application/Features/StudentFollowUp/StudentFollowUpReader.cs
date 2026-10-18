using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.StudentFollowUp;

public sealed partial class StudentFollowUpReader(IAppDbContext db, IAcademicScopeService academicScope,
    TimeProvider? clock = null) : IStudentFollowUpReader
{
    private const int ExportLimit = 10_000;
    private readonly TimeProvider time = clock ?? TimeProvider.System;

    public async Task<StudentFollowUpOptions> OptionsAsync(Guid? teacherId, Guid? packageId,
        bool includeTeachers, CancellationToken ct)
    {
        var teachers = includeTeachers
            ? await db.TeacherProfiles.AsNoTracking().OrderBy(t => t.User.FullName)
                .Select(t => new FollowUpTeacher(t.Id, t.User.FullName)).ToListAsync(ct) : [];
        if (teacherId is null) return new(teachers, [], []);
        var packages = await db.Packages.AsNoTracking().Where(p => p.TeacherId == teacherId)
            .OrderBy(p => p.Name).Select(p => new FollowUpPackage(p.Id, p.Name)).ToListAsync(ct);
        var lessons = packageId is null ? [] : await db.Lessons.AsNoTracking()
            .Where(l => l.ContentSection.Term.PackageId == packageId
                && l.ContentSection.Term.Package.TeacherId == teacherId && l.Videos.Any(v => v.IsActive))
            .OrderBy(l => l.ContentSection.Term.CreatedAt).ThenBy(l => l.ContentSection.Order).ThenBy(l => l.Order).ThenBy(l => l.Id)
            .Select(l => new FollowUpLesson(l.Id, l.Title, l.ContentSection.Term.Title, l.Order)).ToListAsync(ct);
        return new(teachers, packages, lessons);
    }

    public async Task<StudentFollowUpReport> ReadAsync(StudentFollowUpFilter filter, bool export, CancellationToken ct)
    {
        Validate(filter);
        if (!await db.Packages.AnyAsync(p => p.Id == filter.PackageId && p.TeacherId == filter.TeacherId, ct))
            throw new ArgumentException("اختر كورسًا تابعًا للمدرس المحدد.");
        var lessonIds = filter.LessonIds.Distinct().ToArray();
        var ownedIds = await db.Lessons.Where(l => lessonIds.Contains(l.Id)
                && l.ContentSection.Term.PackageId == filter.PackageId)
            .Select(l => l.Id).ToListAsync(ct);
        if (ownedIds.Count != lessonIds.Length) throw new ArgumentException("إحدى الحصص لا تنتمي للكورس المحدد.");
        var videos = await LoadVideosAsync(lessonIds, ct);
        var grants = await LoadGrantsAsync(filter, videos, ct);
        var rows = await BuildRowsAsync(filter, videos, grants, ct);
        var unknown = rows.Count(row => row.AvailableSinceUtc is null);
        var playbackDates = rows.Count(row => row.AvailabilitySource == "RecordedPlayback");
        var filtered = rows.Where(row => MatchesFilter(row, filter))
            .OrderByDescending(row => row.DaysWaiting).ThenBy(row => row.StudentName)
            .ThenBy(row => row.StudentId).ThenBy(row => Array.IndexOf(lessonIds, row.LessonId)).ToArray();
        if (export && filtered.Length > ExportLimit)
            throw new ArgumentException("النتيجة تتجاوز 10000 سجل. حدد حصصًا أو صفًا أضيق قبل تنزيل الإكسيل.");
        var pageRows = export ? filtered : filtered.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToArray();
        return new(pageRows, filtered.Length, filtered.Select(r => r.StudentId).Distinct().Count(),
            filtered.Count(r => r.State == "Completed"), filtered.Count(r => r.State == "Started"),
            filtered.Count(r => r.State == "NotStarted"), filtered.Count(r => r.State != "Completed" && r.DaysWaiting >= 7),
            unknown, playbackDates, export ? 1 : filter.Page, export ? filtered.Length : filter.PageSize,
            time.GetUtcNow().UtcDateTime);
    }

    private static void Validate(StudentFollowUpFilter filter)
    {
        if (filter.TeacherId == Guid.Empty || filter.PackageId == Guid.Empty)
            throw new ArgumentException("اختر المدرس والكورس أولًا.");
        if (filter.LessonIds is null || filter.LessonIds.Length is < 1 or > 20)
            throw new ArgumentException("اختر من حصة إلى 20 حصة في الكشف الواحد.");
        if (filter.Page < 1 || filter.Page > 1_000_000 || filter.PageSize is < 1 or > 100)
            throw new ArgumentException("رقم الصفحة أو حجمها غير صالح.");
        if (filter.MinimumDays is < 0 or > 3650 || filter.Search?.Length > 100)
            throw new ArgumentException("مدة التأخير أو نص البحث غير صالح.");
        if (filter.State is not ("All" or "Incomplete" or "NotStarted" or "Started" or "Completed"))
            throw new ArgumentException("اختر حالة مشاهدة صحيحة.");
    }

    private static bool MatchesFilter(StudentFollowUpRow row, StudentFollowUpFilter filter) =>
        (filter.State == "All" || (filter.State == "Incomplete" ? row.State != "Completed" : row.State == filter.State))
        && (filter.MinimumDays == 0 || (row.State != "Completed" && row.DaysWaiting >= filter.MinimumDays));

    private async Task<List<VideoRow>> LoadVideosAsync(Guid[] lessonIds, CancellationToken ct) =>
        await db.LessonVideos.AsNoTracking().Where(v => lessonIds.Contains(v.LessonId) && v.IsActive
            && v.Lesson.ContentSection.Term.Package.IsActive && v.Lesson.ContentSection.Term.Package.Teacher.IsContentVisibleToStudents
            && v.ArchiveMode != ContentArchiveMode.HiddenFromEveryone && v.Lesson.ArchiveMode != ContentArchiveMode.HiddenFromEveryone
            && v.Lesson.ContentSection.ArchiveMode != ContentArchiveMode.HiddenFromEveryone
            && v.Lesson.ContentSection.Term.ArchiveMode != ContentArchiveMode.HiddenFromEveryone
            && v.Lesson.ContentSection.Term.Package.ArchiveMode != ContentArchiveMode.HiddenFromEveryone)
            .Select(v => new VideoRow(v.Id, v.LessonId, v.Lesson.Title, v.Lesson.ContentSection.Term.PackageId,
                v.Lesson.ContentSection.Term.Package.Name, v.Lesson.ContentSection.TermId, v.Lesson.ContentSectionId,
                v.VideoTypeId, v.StudentOpenedAtUtc,
                db.VideoPlaybackSessions.Where(s => s.LessonVideoId == v.Id && s.HasRegisteredView
                    && s.User.StudentProfile != null).Min(s => (DateTime?)s.CreatedAt),
                db.BunnyVideoAssets.Where(a => a.LessonVideoId == v.Id && a.SourceState == BunnyVideoAssetSourceState.Current
                    && a.DurationSeconds > 0).Select(a => a.DurationSeconds).FirstOrDefault()))
            .ToListAsync(ct);

    private async Task<List<GrantRow>> LoadGrantsAsync(StudentFollowUpFilter filter, List<VideoRow> videos, CancellationToken ct)
    {
        if (videos.Count == 0) return [];
        var now = time.GetUtcNow().UtcDateTime;
        var terms = videos.Select(v => v.TermId).Distinct().ToArray();
        var sections = videos.Select(v => v.SectionId).Distinct().ToArray();
        var lessons = videos.Select(v => v.LessonId).Distinct().ToArray();
        var ids = videos.Select(v => v.Id).ToArray();
        var types = videos.Select(v => v.TypeId).Distinct().ToArray();
        var query = db.StudentAccessGrants.AsNoTracking().Where(g => g.IsActive && g.CancelledAt == null
            && (g.ExpiresAt == null || g.ExpiresAt > now) && g.GrantedAt <= now && g.User.StudentProfile != null
            && ((g.GrantType == CodeType.Package && g.PackageId == filter.PackageId)
                || (g.GrantType == CodeType.Term && terms.Contains(g.TermId ?? Guid.Empty))
                || (g.GrantType == CodeType.Month && sections.Contains(g.ContentSectionId ?? Guid.Empty))
                || (g.GrantType == CodeType.Lesson && lessons.Contains(g.LessonId ?? Guid.Empty))
                || (g.GrantType == CodeType.Video && (g.MaxUses == null || g.UsesConsumed < g.MaxUses)
                    && (ids.Contains(g.LessonVideoId ?? Guid.Empty) || types.Contains(g.VideoTypeId ?? Guid.Empty)))));
        if (filter.Stage.HasValue) query = query.Where(g => g.User.StudentProfile!.EducationStage == filter.Stage);
        if (filter.Grade.HasValue) query = query.Where(g => g.User.StudentProfile!.GradeLevel == filter.Grade);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            query = query.Where(g => g.User.FullName.Contains(search) || g.User.PhoneNumber.Contains(search)
                || (g.User.StudentProfile!.StudentCode != null && g.User.StudentProfile.StudentCode.Contains(search)));
        }
        return await query.Select(g => new GrantRow(g.UserId, g.User.FullName, g.User.PhoneNumber,
            g.User.StudentProfile!.ParentPhone, g.User.StudentProfile.EducationStage, g.User.StudentProfile.GradeLevel,
            g.GrantType, g.PackageId, g.TermId, g.ContentSectionId, g.LessonId, g.LessonVideoId, g.VideoTypeId,
            g.AccessCode == null ? null : g.AccessCode.CodeGroup.TeacherId, g.GrantedAt)).ToListAsync(ct);
    }

    private sealed record VideoRow(Guid Id, Guid LessonId, string LessonTitle, Guid PackageId, string PackageName,
        Guid TermId, Guid SectionId, Guid TypeId, DateTime? OpenedAt, DateTime? FirstPlaybackAt, int? Duration);
    private sealed record GrantRow(Guid UserId, string Name, string Phone, string? ParentPhone,
        EducationStage Stage, GradeLevel Grade, CodeType Type, Guid? PackageId, Guid? TermId, Guid? SectionId,
        Guid? LessonId, Guid? VideoId, Guid? VideoTypeId, Guid? CodeTeacherId, DateTime GrantedAt);
}
