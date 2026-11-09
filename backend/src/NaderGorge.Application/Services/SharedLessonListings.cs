using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities.Homework;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Services;

internal sealed class SharedLessonListings(IAppDbContext db)
{
    // This rollout covers only the verified copies in Nader's month and two-month sections.
    private static readonly LessonPair[] Pairs =
    [
        new(new("57725d90-c2d1-4f28-aff1-eb3b76231a05"), new("72fd8393-466e-4391-8667-8bc92803eb56")),
        new(new("a800969b-5444-48b1-bc60-0b76c3aa1d8b"), new("1f188ca4-cd5a-4f6e-abd2-054cbc535c00")),
        new(new("242bd498-e8d2-4e74-b6ce-89e90cc7b0f8"), new("1b09d106-f9f5-43ae-b1c6-2a9391c25d55"))
    ];

    private static readonly Guid[] LessonIds = Pairs.SelectMany(pair => new[] { pair.Original, pair.Copy }).ToArray();

    public static IReadOnlyCollection<Guid> RelatedLessonIds(IReadOnlyCollection<Guid> lessonIds) =>
        LessonIds.Any(lessonIds.Contains) ? LessonIds : [];

    public async Task<HashSet<Guid>> GetHiddenLessonIdsAsync(
        Guid studentId, IReadOnlySet<Guid> accessibleIds, CancellationToken ct)
    {
        var visiblePairs = Pairs.Where(pair =>
            accessibleIds.Contains(pair.Original) && accessibleIds.Contains(pair.Copy)).ToArray();
        if (visiblePairs.Length == 0)
            return [];

        var terms = await ReadTermIds(LessonIds, ct);
        var sameTermPairs = Pairs.Where(pair => SharesTerm(pair, terms)).ToArray();
        visiblePairs = visiblePairs.Where(pair => SharesTerm(pair, terms)).ToArray();
        if (visiblePairs.Length == 0)
            return [];

        var eligibleIds = LessonIds.Where(accessibleIds.Contains).ToArray();
        var coursework = await ReadCoursework(studentId, eligibleIds).Distinct().ToListAsync(ct);

        // Keep the three lessons on one sequence so prerequisite checks use the same coursework.
        var highestOriginal = Array.FindLastIndex(sameTermPairs, pair => coursework.Contains(pair.Original));
        var highestCopy = Array.FindLastIndex(sameTermPairs, pair => coursework.Contains(pair.Copy));
        var useCopy = highestCopy > highestOriginal;

        if (highestCopy == highestOriginal && highestCopy >= 0)
        {
            var pair = sameTermPairs[highestCopy];
            var unfinished = await ReadCoursework(studentId, [pair.Original, pair.Copy], unfinishedOnly: true)
                .Distinct().ToListAsync(ct);
            useCopy = unfinished.Contains(pair.Copy) && !unfinished.Contains(pair.Original);
        }

        return visiblePairs.Select(pair => useCopy ? pair.Original : pair.Copy).ToHashSet();
    }

    public async Task<Guid?> GetOtherLessonWithCourseworkAsync(
        Guid studentId, Guid lessonId, IAccessCheckService access, CancellationToken ct)
    {
        var pair = Pairs.FirstOrDefault(candidate => candidate.Original == lessonId || candidate.Copy == lessonId);
        if (pair is null)
            return null;

        var otherId = lessonId == pair.Original ? pair.Copy : pair.Original;
        var terms = await ReadTermIds([lessonId, otherId], ct);
        if (!SharesTerm(pair, terms))
            return null;

        return await ReadCoursework(studentId, [otherId]).AnyAsync(ct)
            && await access.HasAccessToLessonAsync(studentId, otherId, ct)
            ? otherId
            : null;
    }

    private Task<Dictionary<Guid, Guid>> ReadTermIds(Guid[] lessonIds, CancellationToken ct) =>
        db.Lessons.Where(lesson => lessonIds.Contains(lesson.Id))
            .ToDictionaryAsync(lesson => lesson.Id, lesson => lesson.ContentSection.TermId, ct);

    private static bool SharesTerm(LessonPair pair, IReadOnlyDictionary<Guid, Guid> terms) =>
        terms.TryGetValue(pair.Original, out var originalTerm)
            && terms.TryGetValue(pair.Copy, out var copyTerm)
            && originalTerm == copyTerm;

    private IQueryable<Guid> ReadCoursework(Guid studentId, Guid[] lessonIds, bool unfinishedOnly = false)
    {
        var examLessons = from lesson in db.Lessons
                          join attempt in db.StudentExamAttempts on lesson.ExamId equals (Guid?)attempt.ExamId
                          where lessonIds.Contains(lesson.Id) && attempt.UserId == studentId &&
                              (!unfinishedOnly || (attempt.SubmittedAt == null && !attempt.IsTimeExpired &&
                                  (attempt.Evaluation == null || attempt.Evaluation == "")))
                          select lesson.Id;

        var homeworkLessons = db.HomeworkSubmissions
            .Where(submission => submission.StudentId == studentId && lessonIds.Contains(submission.Homework.LessonId) &&
                (unfinishedOnly ? submission.Status == SubmissionStatus.InProgress : submission.Status != SubmissionStatus.Missed))
            .Select(submission => submission.Homework.LessonId);
        return examLessons.Concat(homeworkLessons);
    }

    private sealed record LessonPair(Guid Original, Guid Copy);
}
