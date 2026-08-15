using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Services;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Tests;

public sealed class ContentArchiveAccessTests
{
    [Fact]
    public async Task ActiveSubscribersOnly_AllowsActiveSubscriber_AndRejectsNonSubscriber()
    {
        await using var db = TestAppDbContextFactory.Create();
        var package = await SeedHierarchyAsync(db);
        var subscriber = await SeedUserWithRoleAsync(db, RoleType.Student);
        var nonSubscriber = await SeedUserWithRoleAsync(db, RoleType.Student);
        package.ArchiveMode = ContentArchiveMode.ActiveSubscribersOnly;
        db.StudentAccessGrants.Add(new StudentAccessGrant
        {
            UserId = subscriber.Id,
            GrantType = CodeType.Package,
            PackageId = package.Id,
            IsActive = true,
            GrantedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = new ContentArchiveAccessService(db);

        Assert.True(await service.CanViewAsync(subscriber.Id, ContentArchiveTargetType.Package, package.Id));
        Assert.False(await service.CanViewAsync(nonSubscriber.Id, ContentArchiveTargetType.Package, package.Id));
        Assert.False(await service.CanAcquireAsync(ContentArchiveTargetType.Package, package.Id));
    }

    [Fact]
    public async Task HiddenFromEveryone_RejectsSubscriber_ButPrivilegedStaffCanManage()
    {
        await using var db = TestAppDbContextFactory.Create();
        var package = await SeedHierarchyAsync(db);
        var subscriber = await SeedUserWithRoleAsync(db, RoleType.Student);
        var admin = await SeedUserWithRoleAsync(db, RoleType.Admin);
        package.ArchiveMode = ContentArchiveMode.HiddenFromEveryone;
        db.StudentAccessGrants.Add(new StudentAccessGrant
        {
            UserId = subscriber.Id,
            GrantType = CodeType.Package,
            PackageId = package.Id,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var service = new ContentArchiveAccessService(db);

        Assert.False(await service.CanViewAsync(subscriber.Id, ContentArchiveTargetType.Package, package.Id));
        Assert.True(await service.CanViewAsync(admin.Id, ContentArchiveTargetType.Package, package.Id));
    }

    [Fact]
    public async Task ArchivedAncestor_AppliesToNestedLessonAndResource()
    {
        await using var db = TestAppDbContextFactory.Create();
        var package = await SeedHierarchyAsync(db);
        var student = await SeedUserWithRoleAsync(db, RoleType.Student);
        var section = await db.ContentSections.SingleAsync();
        var lesson = await db.Lessons.SingleAsync();
        var resource = new LessonResource
        {
            LessonId = lesson.Id,
            Title = "مذكرة",
            FileUrl = "/files/note.pdf",
            ResourceType = "pdf"
        };
        db.LessonResources.Add(resource);
        package.ArchiveMode = ContentArchiveMode.ActiveSubscribersOnly;
        await db.SaveChangesAsync();

        var service = new ContentArchiveAccessService(db);

        Assert.False(await service.CanViewAsync(student.Id, ContentArchiveTargetType.Lesson, lesson.Id));
        Assert.False(await service.CanViewAsync(student.Id, ContentArchiveTargetType.Resource, resource.Id));
        Assert.False(await service.CanAcquireAsync(ContentArchiveTargetType.Section, section.Id));
    }

    [Fact]
    public async Task RestoredHierarchy_IsVisibleAndPurchasableAgain()
    {
        await using var db = TestAppDbContextFactory.Create();
        var package = await SeedHierarchyAsync(db);
        var student = await SeedUserWithRoleAsync(db, RoleType.Student);
        var lesson = await db.Lessons.SingleAsync();

        var service = new ContentArchiveAccessService(db);

        Assert.Equal(ContentArchiveMode.None, package.ArchiveMode);
        Assert.True(await service.CanViewAsync(student.Id, ContentArchiveTargetType.Lesson, lesson.Id));
        Assert.True(await service.CanAcquireAsync(ContentArchiveTargetType.Lesson, lesson.Id));
    }

    [Fact]
    public async Task SalesTargetResolver_RejectsArchivedTargetAndArchivedAncestor()
    {
        await using var db = TestAppDbContextFactory.Create();
        var package = await SeedHierarchyAsync(db);
        var lesson = await db.Lessons.SingleAsync();
        package.ArchiveMode = ContentArchiveMode.ActiveSubscribersOnly;
        await db.SaveChangesAsync();

        var target = await new SalesTargetResolver(db).ResolveAsync(SalesTargetType.Lesson, lesson.Id);

        Assert.NotNull(target);
        Assert.False(target!.IsSaleEligible);
    }

    [Fact]
    public async Task ArchiveCommand_PersistsModeAndWritesAuditAndRefreshEvent()
    {
        await using var db = TestAppDbContextFactory.Create();
        var package = await SeedHierarchyAsync(db);
        var admin = await SeedUserWithRoleAsync(db, RoleType.Admin);
        var handler = new SetContentArchiveStateCommandHandler(db, new TeacherAuthorizationService(db));

        var result = await handler.Handle(new SetContentArchiveStateCommand(
            ContentArchiveTargetType.Package,
            package.Id,
            ContentArchiveMode.HiddenFromEveryone,
            admin.Id), CancellationToken.None);

        Assert.True(result.Success, result.Message);
        Assert.Equal(ContentArchiveMode.HiddenFromEveryone, package.ArchiveMode);
        Assert.NotNull(package.ArchivedAt);
        Assert.Contains(db.AuditLogs, item => item.Action == "ContentArchived" && item.EntityId == package.Id);
        Assert.Contains(db.OutboxEvents, item => item.Type == "ContentArchived" && item.TargetGroup == "Role_Student");
    }

    [Fact]
    public async Task ActiveSubscribersOnlyVideo_AcceptsScopedVideoTypeGrant()
    {
        await using var db = TestAppDbContextFactory.Create();
        await SeedHierarchyAsync(db);
        var lesson = await db.Lessons.SingleAsync();
        var student = await SeedUserWithRoleAsync(db, RoleType.Student);
        var videoType = new VideoType { Id = Guid.NewGuid(), Name = "شرح", NormalizedName = "EXPLANATION" };
        var video = new LessonVideo
        {
            Lesson = lesson,
            VideoType = videoType,
            VideoTypeId = videoType.Id,
            Title = "فيديو مؤرشف",
            Provider = "youtube",
            ProviderVideoId = "video-id",
            ArchiveMode = ContentArchiveMode.ActiveSubscribersOnly
        };
        db.LessonVideos.Add(video);
        db.StudentAccessGrants.Add(new StudentAccessGrant
        {
            UserId = student.Id,
            GrantType = CodeType.Video,
            VideoTypeId = videoType.Id,
            LessonId = lesson.Id,
            IsActive = true
        });
        await db.SaveChangesAsync();

        Assert.True(await new ContentArchiveAccessService(db).CanViewAsync(
            student.Id, ContentArchiveTargetType.Video, video.Id));
    }

    private static async Task<Package> SeedHierarchyAsync(NaderGorge.Infrastructure.Data.AppDbContext db)
    {
        var subject = new Subject { Name = "Archive subject", NormalizedName = Guid.NewGuid().ToString("N") };
        var package = new Package { Name = "Archive package", Description = "Test", IsActive = true, Subject = subject };
        var term = new Term { Package = package, Title = "Term", Order = 1 };
        var section = new ContentSection { Term = term, Title = "Section", Order = 1 };
        var lesson = new Lesson { ContentSection = section, Title = "Lesson", Summary = "Test", Order = 1 };
        db.Lessons.Add(lesson);
        await db.SaveChangesAsync();
        return package;
    }

    private static async Task<User> SeedUserWithRoleAsync(NaderGorge.Infrastructure.Data.AppDbContext db, RoleType roleType)
    {
        var role = new Role { Name = $"{roleType}-{Guid.NewGuid():N}", Type = roleType };
        var user = new User
        {
            FullName = $"{roleType} user",
            PhoneNumber = Guid.NewGuid().ToString("N")[..11],
            PasswordHash = "hashed"
        };
        db.UserRoles.Add(new UserRole { User = user, Role = role });
        await db.SaveChangesAsync();
        return user;
    }
}
