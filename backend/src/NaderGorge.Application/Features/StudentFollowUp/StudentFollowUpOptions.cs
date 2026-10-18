using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Features.StudentFollowUp;

public sealed partial class StudentFollowUpReader
{
    private async Task<List<FollowUpPackage>> WithPackageGradesAsync(List<FollowUpPackage> packages, CancellationToken ct)
    {
        var ids = packages.Select(package => package.Id).ToArray();
        var scopes = await db.StudentFacingAcademicScopes.AsNoTracking()
            .Where(scope => scope.OwnerType == StudentFacingScopeOwnerType.Package && ids.Contains(scope.OwnerId))
            .ToListAsync(ct);
        var validGrades = (from stage in Enum.GetValues<EducationStage>()
            from grade in Enum.GetValues<GradeLevel>()
            where AcademicValidationService.IsGradeValidForStage(stage, grade)
            select new FollowUpGrade((int)stage, (int)grade)).ToArray();
        var lookup = scopes.ToLookup(scope => scope.OwnerId);
        return packages.Select(package => package with
        {
            Grades = validGrades.Where(grade => lookup[package.Id].Any(scope => ScopeContainsGrade(scope, grade))).ToArray()
        }).ToList();
    }

    private static bool ScopeContainsGrade(StudentFacingAcademicScope scope, FollowUpGrade grade) => scope.ScopeLevel switch
    {
        AcademicScopeLevel.PlatformWide => true,
        AcademicScopeLevel.StageWide => (int?)scope.EducationStage == grade.Stage,
        AcademicScopeLevel.Exact or AcademicScopeLevel.GradeAllSubjects =>
            (int?)scope.EducationStage == grade.Stage && (int?)scope.GradeLevel == grade.Grade,
        _ => false
    };
}
