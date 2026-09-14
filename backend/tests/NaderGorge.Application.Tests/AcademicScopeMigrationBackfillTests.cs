using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using NaderGorge.Infrastructure.Migrations;

namespace NaderGorge.Application.Tests;

public sealed class AcademicScopeMigrationBackfillTests
{
    [Fact]
    public void AddStudentAcademicScope_BackfillUsesLegacyGradeAliasesWithoutBroadUnknownFallback()
    {
        var sql = GetMigrationSql();

        Assert.Contains("lower(trim(p.\"TargetGrade\")) = ga.alias_value", sql);
        Assert.Contains("'firstsecondary', 0, 0", sql);
        Assert.Contains("'secondarygrade3', 0, 31", sql);
        Assert.Contains("lower(trim(p.\"TargetGrade\")) IN ('all', 'جميع الصفوف الدراسية', 'كل الصفوف')", sql);
        Assert.DoesNotContain("p.\"TargetGrade\" IS NULL", sql);
        Assert.DoesNotContain("coalesce(p.\"TargetGrade\"", sql);
    }

    [Fact]
    public void AddStudentAcademicScope_BackfillCoversPublicExamsSharedPackagesAndTeacherSubjectScopes()
    {
        var sql = GetMigrationSql();

        Assert.Contains("FROM public_exam_products pep", sql);
        Assert.Contains("pep.\"IsPlatformWide\" = TRUE", sql);
        Assert.Contains("FROM shared_teacher_packages stp", sql);
        Assert.Contains("FROM teacher_subjects ts", sql);
        Assert.Contains("JOIN academic_subject_eligibilities ase", sql);
        Assert.Contains("ON CONFLICT (\"EducationStage\", \"GradeLevel\", \"SubjectId\") DO NOTHING", sql);
    }

    private static string GetMigrationSql()
    {
        var migration = new AddStudentAcademicScope();
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        typeof(AddStudentAcademicScope)
            .GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);

        return string.Join("\n", builder.Operations.OfType<SqlOperation>().Select(operation => operation.Sql));
    }
}
