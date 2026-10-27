using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NaderGorge.Application.Features.Admin.Gifts.Commands;
using NaderGorge.Application.Features.Admin.Gifts.Models;
using NaderGorge.Application.Features.Admin.Gifts.Queries;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using Xunit;

namespace NaderGorge.Application.Tests;

public sealed class CenterPackageGiftTests
{
    [Fact]
    public async Task InactiveVisibleCenterPackage_CanBeGiftedAndGrantsLessonAccess()
    {
        await using var db = TestAppDbContextFactory.Create();
        var teacherUser = await TestAppDbContextFactory.SeedUserAsync(db, "Center teacher", "15801");
        var admin = await TestAppDbContextFactory.SeedUserAsync(db, "Center admin", "15802");
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Center student", "15803");
        var role = new Role { Name = "Student", Type = RoleType.Student };
        db.UserRoles.Add(new UserRole { User = student, Role = role });

        var teacher = new TeacherProfile { User = teacherUser };
        var subject = new Subject { Name = "Center subject", NormalizedName = "CENTER_SUBJECT" };
        var package = new Package
        {
            Name = "Center path", Description = "Center lessons", Price = 200,
            Teacher = teacher, Subject = subject, TargetGrade = "SecondSecondary",
            ContentMode = PackageContentMode.LessonsOnly,
            IsActive = false, ShowWhenUnavailable = true
        };
        var term = new Term { Title = "Direct term", Package = package, IsSystemContainer = true };
        var section = new ContentSection { Title = "Direct lessons", Term = term, IsSystemContainer = true };
        var lesson = new Lesson { Title = "Center lesson", ContentSection = section };
        db.AddRange(role, teacher, subject, package, term, section, lesson);
        await db.SaveChangesAsync();

        var lookup = new GetGiftTargetsLookupQueryHandler(db);
        var packages = await lookup.Handle(new GetGiftTargetsLookupQuery(GiftTargetType.Package, teacher.Id), default);
        var terms = await lookup.Handle(new GetGiftTargetsLookupQuery(GiftTargetType.Term, teacher.Id, ParentId: package.Id), default);
        var sections = await lookup.Handle(new GetGiftTargetsLookupQuery(GiftTargetType.ContentSection, teacher.Id, ParentId: term.Id), default);
        var lessons = await lookup.Handle(new GetGiftTargetsLookupQuery(GiftTargetType.Lesson, teacher.Id, ParentId: section.Id), default);

        Assert.Contains(packages.Data!, target => target.Id == package.Id);
        Assert.Contains(terms.Data!, target => target.Id == term.Id);
        Assert.Contains(sections.Data!, target => target.Id == section.Id);
        Assert.Contains(lessons.Data!, target => target.Id == lesson.Id);

        var access = new AccessCheckService(db);
        var issuer = new IssueGiftCommandHandler(db, access, new BalanceService(db, NullLogger<BalanceService>.Instance));
        var request = new IssueGiftRequest(Guid.NewGuid(), GiftTargetType.Package, package.Id,
            null, null, null, null, [student.Id], "هدية السنتر");
        var issued = await issuer.Handle(new IssueGiftCommand(request, admin.Id), default);

        Assert.True(issued.Success, issued.Message);
        Assert.Equal(GiftRecipientStatus.Active, Assert.Single(issued.Data!.Recipients).Status);
        Assert.True(await access.HasAccessToLessonAsync(student.Id, lesson.Id));
        Assert.False(package.IsActive);
        Assert.Empty(await db.BalanceTransactions.ToListAsync());
    }

    [Fact]
    public async Task InactiveHiddenPackage_IsNotAvailableForPackageGifts()
    {
        await using var db = TestAppDbContextFactory.Create();
        var teacherUser = await TestAppDbContextFactory.SeedUserAsync(db, "Hidden teacher", "15804");
        var admin = await TestAppDbContextFactory.SeedUserAsync(db, "Hidden admin", "15805");
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Hidden student", "15806");
        var teacher = new TeacherProfile { User = teacherUser };
        var subject = new Subject { Name = "Hidden subject", NormalizedName = "HIDDEN_SUBJECT" };
        var package = new Package
        {
            Name = "Hidden path", Description = "Hidden lessons", Price = 200,
            Teacher = teacher, Subject = subject, TargetGrade = "SecondSecondary",
            IsActive = false, ShowWhenUnavailable = false
        };
        db.AddRange(teacher, subject, package);
        await db.SaveChangesAsync();

        var lookup = await new GetGiftTargetsLookupQueryHandler(db).Handle(
            new GetGiftTargetsLookupQuery(GiftTargetType.Package, teacher.Id), default);
        var issuer = new IssueGiftCommandHandler(db, new AccessCheckService(db),
            new BalanceService(db, NullLogger<BalanceService>.Instance));
        var request = new IssueGiftRequest(Guid.NewGuid(), GiftTargetType.Package, package.Id,
            null, null, null, null, [student.Id], "هدية مخفية");
        var issued = await issuer.Handle(new IssueGiftCommand(request, admin.Id), default);

        Assert.DoesNotContain(lookup.Data!, target => target.Id == package.Id);
        Assert.False(issued.Success);
        Assert.Empty(db.GiftIssuances);
    }
}
