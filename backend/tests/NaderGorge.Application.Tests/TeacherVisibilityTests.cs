using NaderGorge.Application.Features.Public.Queries;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using Xunit;

namespace NaderGorge.Application.Tests;

public sealed class TeacherVisibilityTests
{
    [Fact]
    public async Task Admin_full_update_persists_identity_visibility_and_non_secret_audit()
    {
        await using var db = TestAppDbContextFactory.Create();
        var admin = await TestAppDbContextFactory.SeedUserAsync(db, "Admin", "01070000005");
        var teacherUser = await TestAppDbContextFactory.SeedUserAsync(db, "Old Teacher", "01070000006");
        var subject = new Subject { Id = Guid.NewGuid(), Name = "Math", NormalizedName = "MATH" };
        var teacher = new TeacherProfile { Id = Guid.NewGuid(), UserId = teacherUser.Id, User = teacherUser };
        db.Subjects.Add(subject);
        db.TeacherProfiles.Add(teacher);
        await db.SaveChangesAsync();

        var response = await new UpdateTeacherProfileCommandHandler(db).Handle(
            new UpdateTeacherProfileCommand(
                teacher.Id, admin.Id, "New Teacher", "01070000007", "new-secret-123", "Bio", "Secondary",
                0.25m, null, "Contact", [subject.Id], null, null, null, null, null, true, false, false),
            CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal("New Teacher", teacherUser.FullName);
        Assert.Equal("01070000007", teacherUser.PhoneNumber);
        Assert.True(BCrypt.Net.BCrypt.Verify("new-secret-123", teacherUser.PasswordHash));
        Assert.False(teacher.IsVisibleToStudents);
        Assert.False(teacher.IsContentVisibleToStudents);
        var audit = Assert.Single(db.AuditLogs);
        Assert.DoesNotContain("new-secret-123", audit.NewValues);
        Assert.Contains("passwordChanged", audit.NewValues);
    }

    [Fact]
    public async Task Hidden_teacher_is_excluded_from_public_teacher_query()
    {
        await using var db = TestAppDbContextFactory.Create();
        var visibleUser = await TestAppDbContextFactory.SeedUserAsync(db, "Visible Teacher", "01070000001");
        var hiddenUser = await TestAppDbContextFactory.SeedUserAsync(db, "Hidden Teacher", "01070000002");

        db.TeacherProfiles.AddRange(
            new TeacherProfile { Id = Guid.NewGuid(), UserId = visibleUser.Id, IsVisibleToStudents = true },
            new TeacherProfile { Id = Guid.NewGuid(), UserId = hiddenUser.Id, IsVisibleToStudents = false });
        await db.SaveChangesAsync();

        var response = await new GetActiveTeachersQueryHandler(db)
            .Handle(new GetActiveTeachersQuery(), CancellationToken.None);

        Assert.True(response.Success);
        Assert.Contains(response.Data!, teacher => teacher.FullName == "Visible Teacher");
        Assert.DoesNotContain(response.Data!, teacher => teacher.FullName == "Hidden Teacher");
    }

    [Fact]
    public async Task Hidden_content_blocks_existing_grant_and_showing_content_restores_access()
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "01070000003");
        var teacherUser = await TestAppDbContextFactory.SeedUserAsync(db, "Teacher", "01070000004");
        var teacher = new TeacherProfile
        {
            Id = Guid.NewGuid(),
            UserId = teacherUser.Id,
            IsVisibleToStudents = true,
            IsContentVisibleToStudents = false
        };
        var package = new Package
        {
            Id = Guid.NewGuid(),
            Name = "Teacher Package",
            Description = "Test",
            Price = 100,
            IsActive = true,
            TargetGrade = "3rd Secondary",
            TeacherId = teacher.Id,
            Teacher = teacher
        };
        db.TeacherProfiles.Add(teacher);
        db.Packages.Add(package);
        db.StudentAccessGrants.Add(new StudentAccessGrant
        {
            Id = Guid.NewGuid(),
            UserId = student.Id,
            GrantType = CodeType.Package,
            PackageId = package.Id,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var access = new AccessCheckService(db);
        Assert.False(await access.HasAccessToPackageAsync(student.Id, package.Id));

        teacher.IsContentVisibleToStudents = true;
        await db.SaveChangesAsync();

        Assert.True(await access.HasAccessToPackageAsync(student.Id, package.Id));
    }
}
