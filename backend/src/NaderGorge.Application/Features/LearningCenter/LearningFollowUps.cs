using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Content;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.LearningCenter;

public sealed class LearningFollowUps(IAppDbContext db)
{
    public async Task<List<LearningFollowUpDto>> ReadAsync(Guid actorId, LearningFilter filter, CancellationToken ct)
    {
        LearningCenterScope.Validate(filter);
        var packages = await (await new LearningCenterScope(db).PackagesAsync(actorId, filter, ct))
            .Include(p => p.Teacher).ThenInclude(t => t.User).ToDictionaryAsync(p => p.Id, ct);
        var packageIds = packages.Keys.ToArray();
        var facts = await new ContentGrantFactSource(db).LoadAsync(new(packageIds), ct);
        var roster = ContentAcquisitionCalculator.WhereEffectiveAt(facts, DateTime.UtcNow).GroupBy(f => new { f.UserId, f.PackageId }).ToArray();
        if (roster.Length > 10000) throw new ArgumentException("اختر كورسًا لتقليل حجم قائمة المتابعة.");
        var ids = roster.Select(g => g.Key.UserId).Distinct().ToArray();
        var students = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id) && u.IsActive && !u.IsDeleted).ToDictionaryAsync(u => u.Id, ct);
        var activity = await db.VideoPlaybackSessions.AsNoTracking()
            .Where(s => ids.Contains(s.UserId) && packageIds.Contains(s.LessonVideo.Lesson.ContentSection.Term.PackageId) && s.LastProgressAt != null)
            .GroupBy(s => new { s.UserId, s.LessonVideo.Lesson.ContentSection.Term.PackageId })
            .Select(g => new { g.Key.UserId, g.Key.PackageId, Last = g.Max(s => s.LastProgressAt) }).ToListAsync(ct);
        var examActivity = await db.StudentExamAttempts.AsNoTracking().Where(a => ids.Contains(a.UserId) &&
            a.Exam.LessonVideo != null && packageIds.Contains(a.Exam.LessonVideo.Lesson.ContentSection.Term.PackageId))
            .GroupBy(a => new { a.UserId, a.Exam.LessonVideo!.Lesson.ContentSection.Term.PackageId })
            .Select(g => new { g.Key.UserId, g.Key.PackageId, Last = g.Max(a => a.StartedAt ?? a.CreatedAt) }).ToListAsync(ct);
        var homeworkActivity = await (from submission in db.HomeworkSubmissions.AsNoTracking()
            join lesson in db.Lessons on submission.Homework.LessonId equals lesson.Id
            where ids.Contains(submission.StudentId) && packageIds.Contains(lesson.ContentSection.Term.PackageId)
            group submission by new { submission.StudentId, lesson.ContentSection.Term.PackageId } into grouped
            select new { grouped.Key.StudentId, grouped.Key.PackageId, Last = grouped.Max(h => h.SubmittedAt ?? h.StartedAt) }).ToListAsync(ct);
        var exams = await new LearningEvidenceReader(db).ReadAsync(packageIds, filter.Days, ct);
        var history = await db.LearningFollowUps.AsNoTracking().Include(f => f.PerformedByUser)
            .Where(f => packageIds.Contains(f.PackageId) && ids.Contains(f.StudentId)).ToListAsync(ct);
        var followUps = new List<LearningFollowUpDto>();
        foreach (var member in roster)
        {
            if (!students.TryGetValue(member.Key.UserId, out var student)) continue;
            var package = packages[member.Key.PackageId];
            var attempts = exams.Attempts.Where(a => a.StudentId == student.Id && a.PackageId == package.Id).OrderBy(a => a.At).ToArray();
            var watch = activity.FirstOrDefault(a => a.UserId == student.Id && a.PackageId == package.Id)?.Last;
            var lastActivity = new[] { watch, attempts.LastOrDefault()?.At,
                examActivity.FirstOrDefault(a => a.UserId == student.Id && a.PackageId == package.Id)?.Last,
                homeworkActivity.FirstOrDefault(a => a.StudentId == student.Id && a.PackageId == package.Id)?.Last }.Max();
            var followUp = history.Where(f => f.StudentId == student.Id && f.PackageId == package.Id).OrderByDescending(f => f.CreatedAt).ThenByDescending(f => f.Id).FirstOrDefault();
            var reasons = Reasons(attempts, filter);
            var inactiveSince = lastActivity ?? member.Min(f => f.GrantedAt);
            var inactiveDays = (int)(DateTime.UtcNow - inactiveSince).TotalDays;
            if (inactiveDays >= filter.InactiveDays) reasons.Insert(0, $"لم يسجل نشاط مذاكرة منذ {inactiveDays} يومًا");
            if (reasons.Count == 0 && followUp is null) continue;
            followUps.Add(new(student.Id, student.FullName, package.Id, package.Name, package.Teacher.User.FullName,
                lastActivity, reasons, followUp?.Status ?? "New", followUp?.Note ?? "", followUp?.CreatedAt,
                followUp?.PerformedByUser.FullName, attempts.LastOrDefault()?.Percent, Improvement(attempts, followUp?.CreatedAt)));
        }
        return followUps.OrderByDescending(f => f.Reasons.Count).ThenBy(f => f.LastActivityAt).ToList();
    }

    public async Task SaveAsync(Guid actorId, SaveLearningFollowUp request, CancellationToken ct)
    {
        if (request.Status is not ("New" or "InProgress" or "Completed") || string.IsNullOrWhiteSpace(request.Note) ||
            request.Note.Length > 2000 || string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 2000)
            throw new ArgumentException("أدخل حالة المتابعة والملاحظة والسبب.");
        var packages = await new LearningCenterScope(db).PackagesAsync(actorId, new(PackageId: request.PackageId), ct);
        if (!await packages.AnyAsync(ct)) throw new UnauthorizedAccessException();
        var facts = await new ContentGrantFactSource(db).LoadAsync(new([request.PackageId], StudentId: request.StudentId), ct);
        if (!ContentAcquisitionCalculator.WhereEffectiveAt(facts, DateTime.UtcNow).Any()) throw new UnauthorizedAccessException();
        var followUp = new LearningFollowUp { StudentId = request.StudentId, PackageId = request.PackageId,
            PerformedByUserId = actorId, Status = request.Status, Note = request.Note.Trim(), Reason = request.Reason.Trim() };
        db.LearningFollowUps.Add(followUp);
        db.AuditLogs.Add(new AuditLog { Action = "LearningFollowUpRecorded", EntityType = nameof(LearningFollowUp),
            EntityId = followUp.Id, PerformedByUserId = actorId, NewValues = System.Text.Json.JsonSerializer.Serialize(request) });
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<LearningFollowUpHistory>> HistoryAsync(Guid actorId, FollowUpTarget target, CancellationToken ct)
    {
        var packages = await new LearningCenterScope(db).PackagesAsync(actorId, new(PackageId: target.PackageId), ct);
        if (!await packages.AnyAsync(ct)) throw new UnauthorizedAccessException();
        return await db.LearningFollowUps.AsNoTracking().Where(f => f.PackageId == target.PackageId && f.StudentId == target.StudentId)
            .OrderByDescending(f => f.CreatedAt).Take(100)
            .Select(f => new LearningFollowUpHistory(f.Id, f.Status, f.Note, f.Reason, f.CreatedAt, f.PerformedByUser.FullName)).ToListAsync(ct);
    }

    public static List<string> Reasons(LearningAttemptEvidence[] attempts, LearningFilter filter)
    {
        var reasons = new List<string>();
        foreach (var group in attempts.GroupBy(a => new { a.ExamId, a.Definition }))
        {
            var ordered = group.OrderBy(a => a.At).ToArray();
            if (ordered.Length >= 2 && ordered[^2].Percent - ordered[^1].Percent >= filter.DeclinePoints)
                reasons.Add($"انخفضت نتيجة تقييم قابل للمقارنة من {ordered[^2].Percent}% إلى {ordered[^1].Percent}%");
            var recent = ordered.TakeLast(filter.RepeatedAttempts).ToArray();
            if (recent.Length == filter.RepeatedAttempts && recent[^1].Percent < 60 && recent.Skip(1).All(a => a.Percent <= recent[0].Percent))
                reasons.Add($"كرر نفس التقييم {recent.Length} مرات دون تحسن، وآخر نتيجة {recent[^1].Percent}%");
        }
        return reasons.Distinct().ToList();
    }

    private static decimal? Improvement(LearningAttemptEvidence[] attempts, DateTime? followedAt)
    {
        if (!followedAt.HasValue) return null;
        var after = attempts.LastOrDefault(a => a.At > followedAt.Value);
        if (after is null) return null;
        var before = attempts.LastOrDefault(a => a.At <= followedAt.Value && a.ExamId == after.ExamId && a.Definition == after.Definition);
        return before is null ? null : after.Percent - before.Percent;
    }
}
public sealed record FollowUpTarget(Guid PackageId, Guid StudentId);
public sealed record LearningFollowUpHistory(Guid Id, string Status, string Note, string Reason, DateTime At, string Actor);
