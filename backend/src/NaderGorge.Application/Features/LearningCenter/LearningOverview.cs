using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.LearningCenter;

public sealed class LearningOverview(IAppDbContext db)
{
    public const int MinimumStudents = 5;

    public async Task<LearningOverviewDto> ReadAsync(Guid actorId, LearningFilter filter, CancellationToken ct)
    {
        LearningCenterScope.Validate(filter);
        var packages = await (await new LearningCenterScope(db).PackagesAsync(actorId, filter, ct)).ToDictionaryAsync(p => p.Id, ct);
        var packageIds = packages.Keys.ToArray();
        var lessons = await db.Lessons.AsNoTracking().Where(l => packageIds.Contains(l.ContentSection.Term.PackageId))
            .Select(l => new LearningLessonDto(l.Id, l.Title, l.ContentSection.Term.PackageId)).ToListAsync(ct);
        var lessonIds = lessons.Select(l => l.Id).ToArray();
        var catalog = await db.QuestionBankItems.AsNoTracking().Where(q => q.LearningLessonId.HasValue && lessonIds.Contains(q.LearningLessonId.Value))
            .Select(q => new { q.LearningLessonId, q.LearningConcept }).Distinct().ToListAsync(ct);
        var evidence = await new LearningEvidenceReader(db).ReadAsync(packageIds, filter.Days, ct);
        evidence.Attempts.AddRange(await new NaderGorge.Application.Features.VideoLearning.VideoLearningEvidence(db).ReadAsync(packageIds, filter.Days, ct));
        var answers = evidence.Attempts.SelectMany(a => a.Answers.Select(answer => new AnswerRow(a, answer))).ToArray();
        catalog.AddRange(answers.Where(a => !string.IsNullOrWhiteSpace(a.Answer.Concept))
            .Select(a => new { LearningLessonId = (Guid?)a.Answer.LessonId, LearningConcept = a.Answer.Concept }).Distinct()
            .Where(c => !catalog.Any(existing => existing.LearningLessonId == c.LearningLessonId && existing.LearningConcept == c.LearningConcept)));
        var concepts = catalog.Where(q => !string.IsNullOrWhiteSpace(q.LearningConcept)).Select(q =>
        {
            var lesson = lessons.Single(l => l.Id == q.LearningLessonId);
            var rows = answers.Where(a => a.Answer.LessonId == lesson.Id && a.Answer.Concept == q.LearningConcept).ToArray();
            return Summarize(lesson, packages[lesson.PackageId].Name, q.LearningConcept, rows);
        }).OrderBy(c => c.CorrectPercent ?? 101).ThenBy(c => c.Lesson).ToList();
        var teacherIds = packages.Values.Select(p => p.TeacherId).Distinct().ToArray();
        var unclassified = await db.QuestionBankItems.CountAsync(q => teacherIds.Contains(q.CreatedByTeacherId) &&
            q.SupersededByQuestionId == null && (q.LearningLessonId == null || q.LearningConcept == ""), ct);
        return new(concepts, unclassified, evidence.Excluded, MinimumStudents);
    }

    private static LearningConceptDto Summarize(LearningLessonDto lesson, string package, string concept, AnswerRow[] rows)
    {
        var latest = LatestAnswers(rows);
        var students = latest.Select(a => a.Attempt.StudentId).Distinct().Count();
        var scores = latest.GroupBy(a => a.Attempt.StudentId).Select(g => new LearningStudentScore(g.Key,
            g.First().Attempt.StudentName, Percent(g.Count(a => a.Answer.Correct), g.Count()))).OrderBy(s => s.CorrectPercent).ToList();
        return new(lesson.Id, lesson.Title, lesson.PackageId, package, concept, students, rows.Select(a => a.Attempt.Id).Distinct().Count(),
            students < MinimumStudents ? null : Percent(latest.Count(a => a.Answer.Correct), latest.Length),
            rows.GroupBy(a => a.Answer.QuestionId).Select(QuestionStats).OrderBy(q => q.CorrectPercent ?? 101).ToList(),
            scores.Where(s => s.CorrectPercent < 60).ToList(),
            rows.GroupBy(a => NaderGorge.Application.Common.CairoTime.ToDate(a.Attempt.At).ToString("yyyy-MM-dd")).OrderBy(g => g.Key).Select(g => Trend(g.Key, g.ToArray())).ToList());
    }

    private static LearningQuestionStats QuestionStats(IGrouping<Guid, AnswerRow> group)
    {
        var latest = LatestAnswers(group.ToArray());
        var wrong = latest.Where(a => a.Answer.WrongOption is not null).GroupBy(a => a.Answer.WrongOption)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key).FirstOrDefault();
        var ordered = latest.OrderBy(a => a.Attempt.Percent).ToArray();
        var band = Math.Max(1, ordered.Length / 3);
        var discrimination = ordered.Length >= 10 && ordered[0].Attempt.Percent < ordered[^1].Attempt.Percent
            ? Percent(ordered.TakeLast(band).Count(a => a.Answer.Correct), band) - Percent(ordered.Take(band).Count(a => a.Answer.Correct), band)
            : (decimal?)null;
        return new(group.Key, group.OrderByDescending(a => a.Attempt.At).First().Answer.Text, latest.Length, group.Count(),
            latest.Length < MinimumStudents ? null : Percent(latest.Count(a => a.Answer.Correct), latest.Length),
            wrong?.Key, wrong?.Count() ?? 0, discrimination);
    }

    private static LearningTrendPoint Trend(string date, AnswerRow[] rows)
    {
        var latest = LatestAnswers(rows);
        var students = latest.Select(a => a.Attempt.StudentId).Distinct().Count();
        return new(date, students, students < MinimumStudents ? null : Percent(latest.Count(a => a.Answer.Correct), latest.Length));
    }

    private static AnswerRow[] LatestAnswers(AnswerRow[] rows) => rows.GroupBy(a => new { a.Attempt.StudentId, a.Answer.QuestionId })
        .Select(g => g.OrderByDescending(a => a.Attempt.At).ThenBy(a => a.Attempt.Id).First()).ToArray();
    private static decimal Percent(int correct, int total) => total == 0 ? 0 : Math.Round(100m * correct / total, 1);
    private sealed record AnswerRow(LearningAttemptEvidence Attempt, LearningAnswerEvidence Answer);
}
