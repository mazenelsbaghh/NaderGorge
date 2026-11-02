using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Features.StudentFollowUp;

public sealed partial class StudentFollowUpReader
{
    private Task<Guid[]> CourseLessonIdsAsync(Guid packageId, CancellationToken ct) =>
        db.Lessons.AsNoTracking().Where(lesson => lesson.ContentSection.Term.PackageId == packageId)
            .OrderBy(lesson => lesson.ContentSection.Term.CreatedAt).ThenBy(lesson => lesson.ContentSection.Order)
            .ThenBy(lesson => lesson.Order).ThenBy(lesson => lesson.Id).Select(lesson => lesson.Id).ToArrayAsync(ct);

    public async Task<FollowUpStudentChoices> SearchStudentsAsync(FollowUpStudentSearch search, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(search.Search) || search.Search.Trim().Length is < 2 or > 100)
            throw new ArgumentException("اكتب حرفين على الأقل من اسم الطالب أو الموبايل أو الكود.");
        if (!await db.Packages.AnyAsync(package => package.Id == search.PackageId && package.TeacherId == search.TeacherId, ct))
            throw new ArgumentException("اختر كورسًا تابعًا للمدرس المحدد.");
        var videos = await LoadVideosAsync(await CourseLessonIdsAsync(search.PackageId, ct), ct);
        var filter = new StudentFollowUpFilter(search.TeacherId, search.PackageId, [], Search: search.Search.Trim());
        var grants = await LoadGrantsAsync(filter, videos, ct);
        var choices = new List<FollowUpStudent>();
        var videoIds = videos.Select(video => video.Id).ToArray();
        var eligibility = new Dictionary<(EducationStage, GradeLevel), IReadOnlySet<Guid>>();
        foreach (var student in grants.GroupBy(grant => grant.UserId).OrderBy(group => group.First().Name).ThenBy(group => group.Key))
        {
            var identity = student.First();
            var cohort = (identity.Stage, identity.Grade);
            if (!eligibility.TryGetValue(cohort, out var eligible))
                eligibility[cohort] = eligible = await academicScope.GetEligibleLessonVideoIdsForStudentAsync(videoIds, student.Key, ct);
            if (!videos.Any(video => eligible.Contains(video.Id) && student.Any(grant => GrantsVideo(grant, video, search.TeacherId)))) continue;
            choices.Add(new(student.Key, student.First().Name, student.First().Phone));
            if (choices.Count == 21) break;
        }
        return new(choices.Take(20).ToArray(), choices.Count > 20);
    }
}
