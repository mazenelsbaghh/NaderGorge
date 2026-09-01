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
        source.Where(homework => homework.IsActive && homework.Questions.Any());

    public static async Task<HomeworkEntity?> FirstAccessibleToStudentAsync(
        this IQueryable<HomeworkEntity> source,
        Guid studentId,
        IAccessCheckService access,
        IContentArchiveAccessService archiveAccess,
        CancellationToken cancellationToken)
    {
        var readyHomeworks = await source.ReadyForStudents().ToListAsync(cancellationToken);
        foreach (var homework in readyHomeworks)
        {
            if (!await access.HasAccessToLessonAsync(
                    studentId,
                    homework.LessonId,
                    cancellationToken))
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
