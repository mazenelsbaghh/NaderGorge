using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NaderGorge.Application.Features.Admin.Gifts.Commands;
using NaderGorge.Application.Features.Admin.Gifts.Models;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests;

public sealed class StudentAcademicScopeGiftTests
{
    [Fact]
    public async Task IssueGift_DeniesNonMatchingRecipientWithoutCreatingAccessGrant()
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await SeedStudentAsync(db);
        var admin = await TestAppDbContextFactory.SeedUserAsync(db, "Admin", Guid.NewGuid().ToString("N")[..11]);
        var package = await SeedPackageAsync(db);
        db.StudentFacingAcademicScopes.Add(new StudentFacingAcademicScope
        {
            OwnerType = StudentFacingScopeOwnerType.Package,
            OwnerId = package.Id,
            ScopeLevel = AcademicScopeLevel.GradeAllSubjects,
            EducationStage = EducationStage.Secondary,
            GradeLevel = GradeLevel.SecondSecondary
        });
        await db.SaveChangesAsync();

        var academicScope = new AcademicScopeService(db);
        var request = new IssueGiftRequest(
            Guid.NewGuid(),
            GiftTargetType.Package,
            package.Id,
            null,
            null,
            null,
            null,
            [student.Id],
            "Scope test");
        var handler = new IssueGiftCommandHandler(
            db,
            new AccessCheckService(db, academicScope),
            new BalanceService(db, NullLogger<BalanceService>.Instance),
            academicScope);

        var result = await handler.Handle(new IssueGiftCommand(request, admin.Id), CancellationToken.None);

        Assert.True(result.Success, result.Message);
        var recipient = Assert.Single(result.Data!.Recipients);
        Assert.Equal(GiftRecipientStatus.Failed, recipient.Status);
        Assert.Equal("ACADEMIC_SCOPE_DENIED", recipient.OutcomeCode);
        Assert.Empty(db.StudentAccessGrants);
        Assert.Contains(db.AuditLogs, x => x.Action == "AcademicScopeDeniedGiftRecipient");
    }

    private static async Task<User> SeedStudentAsync(AppDbContext db)
    {
        var role = await db.Roles.FirstOrDefaultAsync(x => x.Type == RoleType.Student);
        if (role == null)
        {
            role = new Role { Name = "Student", Type = RoleType.Student };
            db.Roles.Add(role);
        }

        var user = new User
        {
            FullName = $"Gift Student {Guid.NewGuid():N}",
            PhoneNumber = Guid.NewGuid().ToString("N")[..11],
            PasswordHash = "hash",
            IsActive = true
        };
        db.Users.Add(user);
        db.UserRoles.Add(new UserRole { User = user, Role = role });
        db.StudentProfiles.Add(new StudentProfile
        {
            User = user,
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

    private static async Task<Package> SeedPackageAsync(AppDbContext db)
    {
        var subject = new Subject
        {
            Name = $"Gift Subject {Guid.NewGuid():N}",
            NormalizedName = Guid.NewGuid().ToString("N"),
            Description = "Subject"
        };
        var package = new Package
        {
            Name = "Gift package",
            Description = "Package",
            Price = 100m,
            IsActive = true,
            Subject = subject,
            TargetGrade = "FirstSecondary",
            TeacherId = Guid.NewGuid()
        };
        db.AddRange(subject, package);
        await db.SaveChangesAsync();
        return package;
    }
}
