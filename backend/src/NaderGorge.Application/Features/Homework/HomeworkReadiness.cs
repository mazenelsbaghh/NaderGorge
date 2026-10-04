using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;
using HomeworkEntity = NaderGorge.Domain.Entities.Homework.Homework;

namespace NaderGorge.Application.Features.Homework;

/// <summary>
/// Defines the single student-facing readiness rule for homework. A saved shell
/// without questions is a draft even if an older row still has IsActive=true.
/// </summary>
public static class HomeworkReadiness
{
    public static IQueryable<HomeworkEntity> ReadyForStudents(
        this IQueryable<HomeworkEntity> source) =>
        source.Where(homework => homework.IsActive && homework.Questions.Any(question => !question.IsRetired));

    public static async Task<HomeworkEntity?> FirstAccessibleToStudentAsync(
        this IQueryable<HomeworkEntity> source,
        Guid studentId,
        IAccessCheckService access,
        IContentArchiveAccessService archiveAccess,
        CancellationToken cancellationToken)
    {
        var readyHomeworks = await source.ReadyForStudents().ToListAsync(cancellationToken);
        var accessByLesson = new Dictionary<Guid, bool>();
        foreach (var homework in readyHomeworks)
        {
            if (!accessByLesson.TryGetValue(homework.LessonId, out var hasLessonAccess))
            {
                hasLessonAccess = await access.HasAccessToLessonAsync(
                    studentId,
                    homework.LessonId,
                    cancellationToken);
                accessByLesson.Add(homework.LessonId, hasLessonAccess);
            }

            if (!hasLessonAccess)
            {
                continue;
            }

            if (await archiveAccess.CanViewAsync(
                    studentId,
                    ContentArchiveTargetType.Homework,
                    homework.Id,
                    cancellationToken))
            {
                return homework;
            }
        }

        return null;
    }
}
