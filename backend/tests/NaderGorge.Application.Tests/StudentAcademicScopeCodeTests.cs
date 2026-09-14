using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Codes.Commands;
using NaderGorge.Application.Features.Codes.Queries;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests;

public sealed class StudentAcademicScopeCodeTests
{
    [Fact]
    public async Task ValidateCode_DeniesNonMatchingTargetForCurrentStudent()
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await SeedStudentAsync(db);
        var (code, _) = await SeedPackageCodeAsync(db, student.Id);
        var academicScope = new AcademicScopeService(db);
        var handler = new ValidateCodeQueryHandler(db, academicScope);

        var result = await handler.Handle(new ValidateCodeQuery(code.CodePlaintext, student.Id), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("ACADEMIC_SCOPE_DENIED", result.Errors ?? []);
    }

    [Fact]
    public async Task ActivateCode_DeniesNonMatchingTargetBeforeConsumingCodeOrCreatingGrant()
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await SeedStudentAsync(db);
        var (code, package) = await SeedPackageCodeAsync(db, student.Id);
        var academicScope = new AcademicScopeService(db);
        var handler = new ActivateCodeCommandHandler(db, new FakeJobEnqueuer(), academicScope: academicScope);

        var result = await handler.Handle(new ActivateCodeCommand(student.Id, code.CodePlaintext), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("ACADEMIC_SCOPE_DENIED", result.Errors ?? []);
        Assert.False(await db.AccessCodes.Where(x => x.Id == code.Id).Select(x => x.IsConsumed).SingleAsync());
        Assert.Empty(db.StudentAccessGrants);
        Assert.Contains(db.AuditLogs, x =>
            x.Action == "AcademicScopeDeniedCodeActivation" &&
            x.EntityId == code.Id);
        Assert.True(await db.StudentFacingAcademicScopes.AnyAsync(x => x.OwnerId == package.Id));
    }

    private static async Task<User> SeedStudentAsync(AppDbContext db)
    {
        var user = await TestAppDbContextFactory.SeedUserAsync(db, $"Student {Guid.NewGuid():N}", Guid.NewGuid().ToString("N")[..11]);
        db.StudentProfiles.Add(new StudentProfile
        {
            UserId = user.Id,
            DateOfBirth = DateTime.UtcNow.AddYears(-16),
            Gender = Gender.Male,
            Governorate = "Cairo",
            Address = "Address",
            EducationStage = EducationStage.Secondary,
            GradeLevel = GradeLevel.FirstSecondary
        });
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task<(AccessCode Code, Package Package)> SeedPackageCodeAsync(AppDbContext db, Guid createdByUserId)
    {
        var subject = new Subject
        {
            Name = $"Code Subject {Guid.NewGuid():N}",
            NormalizedName = Guid.NewGuid().ToString("N"),
            Description = "Subject"
        };
        var package = new Package
        {
            Name = "Code package",
            Description = "Package",
            Price = 100m,
            IsActive = true,
            Subject = subject,
            TargetGrade = "FirstSecondary",
            TeacherId = Guid.NewGuid()
        };
        var group = new CodeGroup
        {
            Name = "Denied code group",
            CodeType = CodeType.Package,
            PackageId = package.Id,
            TotalCodes = 1,
            CreatedByUserId = createdByUserId
        };
        var code = new AccessCode
        {
            CodePlaintext = $"SCOPE{Guid.NewGuid():N}"[..12],
            CodeHash = "hash",
            CodeGroup = group,
            SerialNumber = 1
        };

        db.AddRange(subject, package, group, code);
        db.StudentFacingAcademicScopes.Add(new StudentFacingAcademicScope
        {
            OwnerType = StudentFacingScopeOwnerType.Package,
            OwnerId = package.Id,
            ScopeLevel = AcademicScopeLevel.GradeAllSubjects,
            EducationStage = EducationStage.Secondary,
            GradeLevel = GradeLevel.SecondSecondary
        });
        await db.SaveChangesAsync();
        return (code, package);
    }
}
