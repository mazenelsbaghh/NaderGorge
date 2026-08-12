using NaderGorge.API.Controllers;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Features.Content.Queries;
using NaderGorge.Domain.Entities;

namespace NaderGorge.Application.Tests;

public sealed class ContentSummaryTeacherTests
{
    [Fact]
    public void Catalog_endpoint_requires_content_permission_instead_of_user_management()
    {
        var endpoint = typeof(AdminController).GetMethod(nameof(AdminController.GetContentSummaryTeachers));
        var permission = Assert.Single(endpoint!.GetCustomAttributes(typeof(HasPermissionAttribute), inherit: true));

        var attribute = Assert.IsType<HasPermissionAttribute>(permission);
        Assert.NotNull(attribute.Arguments);
        Assert.Equal("content.manage", Assert.Single(attribute.Arguments!));
    }

    [Fact]
    public async Task Catalog_returns_only_content_identity_with_subjects_and_package_count()
    {
        await using var db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Teacher", "01088000001");
        var teacher = new TeacherProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            Specialization = "Secondary",
            ProfileImageUrl = "/teachers/profile.webp"
        };
        var subject = new Subject
        {
            Id = Guid.NewGuid(),
            Name = "Physics",
            NormalizedName = "PHYSICS"
        };
        teacher.TeacherSubjects.Add(new TeacherSubject
        {
            TeacherId = teacher.Id,
            Teacher = teacher,
            SubjectId = subject.Id,
            Subject = subject
        });
        teacher.Packages.Add(new Package
        {
            Id = Guid.NewGuid(),
            Name = "Grade 12",
            TeacherId = teacher.Id,
            Teacher = teacher,
            SubjectId = subject.Id,
            Subject = subject
        });
        db.AddRange(teacher, subject);
        await db.SaveChangesAsync();

        var response = await new GetContentSummaryTeachersQueryHandler(db)
            .Handle(new GetContentSummaryTeachersQuery(), CancellationToken.None);

        Assert.True(response.Success);
        var item = Assert.Single(response.Data!);
        Assert.Equal(teacher.Id, item.Id);
        Assert.Equal("Teacher", item.FullName);
        Assert.Equal("Secondary", item.Specialization);
        Assert.Equal([subject.Id], item.SubjectIds);
        Assert.Equal(["Physics"], item.SubjectNames);
        Assert.Equal(1, item.PackagesCount);
    }
}
