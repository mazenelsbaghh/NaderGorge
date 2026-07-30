using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using NaderGorge.API.Controllers;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Features.Admin.Queries;
using NaderGorge.Application.Features.Admin.VideoTypes.Commands;
using NaderGorge.Application.Features.Admin.VideoTypes.Queries;
using NaderGorge.Application.Interfaces;
using NaderGorge.Application.Features.Content.Queries;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Infrastructure.Data;
using Xunit;

namespace NaderGorge.Application.Tests;

public class ContentIdentityAndVideoTypesTests
{
    [Fact]
    public async Task SaveChanges_AssignsGloballyNamespacedContentCodes()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var videoType = new VideoType
        {
            Name = "شرح",
            NormalizedName = "شرح",
            SortOrder = 10,
            IsActive = true
        };
        var lesson = new Lesson { Title = "Lesson", Summary = "Summary" };
        var video = new LessonVideo
        {
            Title = "Video",
            Provider = "youtube",
            ProviderVideoId = "dQw4w9WgXcQ",
            LessonId = lesson.Id,
            VideoTypeId = videoType.Id
        };
        var exam = new Exam { Title = "Exam", Description = "Description" };

        db.VideoTypes.Add(videoType);
        db.Lessons.Add(lesson);
        db.LessonVideos.Add(video);
        db.Exams.Add(exam);

        await db.SaveChangesAsync();

        Assert.Equal($"LES-{lesson.Id:N}", lesson.InternalCode);
        Assert.Equal($"VID-{video.Id:N}", video.InternalCode);
        Assert.Equal($"EXM-{exam.Id:N}", exam.InternalCode);
        Assert.Equal(3, new[] { lesson.InternalCode, video.InternalCode, exam.InternalCode }.Distinct().Count());
        Assert.Matches("^LES-[0-9a-f]{32}$", lesson.InternalCode);
        Assert.Matches("^VID-[0-9a-f]{32}$", video.InternalCode);
        Assert.Matches("^EXM-[0-9a-f]{32}$", exam.InternalCode);
    }

    [Fact]
    public async Task SaveChanges_RejectsPersistedInternalCodeMutation()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var lesson = new Lesson { Title = "Lesson", Summary = "Summary" };
        db.Lessons.Add(lesson);
        await db.SaveChangesAsync();

        db.Entry(lesson).Property(nameof(Lesson.InternalCode)).CurrentValue = "LES-MUTATED";

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task VideoType_DeactivationPreservesExistingAssignment()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var type = new VideoType
        {
            Name = "مراجعة",
            NormalizedName = "مراجعة",
            SortOrder = 30,
            IsActive = true
        };
        var lesson = new Lesson { Title = "Lesson", Summary = "Summary" };
        var video = new LessonVideo
        {
            Title = "Video",
            Provider = "youtube",
            ProviderVideoId = "dQw4w9WgXcQ",
            LessonId = lesson.Id,
            VideoTypeId = type.Id
        };

        db.AddRange(type, lesson, video);
        await db.SaveChangesAsync();

        type.IsActive = false;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var persisted = await db.LessonVideos.Include(item => item.VideoType).SingleAsync();
        Assert.Equal(type.Id, persisted.VideoTypeId);
        Assert.False(persisted.VideoType.IsActive);
    }

    [Fact]
    public async Task AdminDetailQueries_ReturnPersistedCodesAndVideoType()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var type = new VideoType
        {
            Name = "شرح",
            NormalizedName = "شرح",
            SortOrder = 10,
            IsActive = true
        };
        var lesson = new Lesson { Title = "Lesson", Summary = "Summary" };
        var video = new LessonVideo
        {
            Title = "Video",
            Provider = "youtube",
            ProviderVideoId = "dQw4w9WgXcQ",
            LessonId = lesson.Id,
            VideoTypeId = type.Id
        };
        var exam = new Exam { Title = "Exam", Description = "Description" };
        db.AddRange(type, lesson, video, exam);
        await db.SaveChangesAsync();

        var cockpitHandler = new GetLessonCockpitQueryHandler(db, new TeacherAuthorizationService(db));
        var cockpitResult = await cockpitHandler.Handle(new GetLessonCockpitQuery(lesson.Id), CancellationToken.None);
        var dashboardHandler = new GetExamDashboardQueryHandler(db);
        var dashboardResult = await dashboardHandler.Handle(new GetExamDashboardQuery(exam.Id), CancellationToken.None);

        Assert.True(cockpitResult.Success);
        Assert.Equal(lesson.InternalCode, cockpitResult.Data!.InternalCode);
        var cockpitVideo = Assert.Single(cockpitResult.Data.Videos);
        Assert.Equal(video.InternalCode, cockpitVideo.InternalCode);
        Assert.Equal(type.Id, cockpitVideo.VideoType.Id);
        Assert.Equal("شرح", cockpitVideo.VideoType.Name);
        Assert.True(dashboardResult.Success);
        Assert.Equal(exam.InternalCode, dashboardResult.Data!.InternalCode);
    }

    [Fact]
    public async Task VideoTypeLifecycle_EnforcesNormalizationFilteringAndAssignedDeleteAudit()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var adminId = Guid.NewGuid();
        var createHandler = new CreateVideoTypeCommandHandler(db);
        var created = await createHandler.Handle(
            new CreateVideoTypeCommand("  حل   أسئلة  ", 50, true, adminId),
            CancellationToken.None);

        Assert.True(created.Success);
        Assert.Equal("حل أسئلة", created.Data!.Name);
        var duplicate = await createHandler.Handle(
            new CreateVideoTypeCommand("حل أسئلة", 60, true, adminId),
            CancellationToken.None);
        Assert.False(duplicate.Success);
        Assert.Contains("VIDEO_TYPE_DUPLICATE", duplicate.Errors!);

        var statusHandler = new SetVideoTypeStatusCommandHandler(db);
        var inactive = await statusHandler.Handle(
            new SetVideoTypeStatusCommand(created.Data.Id, false, adminId),
            CancellationToken.None);
        Assert.True(inactive.Success);
        Assert.False(inactive.Data!.IsActive);

        var listHandler = new GetVideoTypesQueryHandler(db);
        var activeOnly = await listHandler.Handle(new GetVideoTypesQuery(), CancellationToken.None);
        Assert.Empty(activeOnly.Data!);
        var all = await listHandler.Handle(new GetVideoTypesQuery(IncludeInactive: true), CancellationToken.None);
        Assert.Single(all.Data!);

        var lesson = new Lesson { Title = "Lesson", Summary = "Summary" };
        var video = new LessonVideo
        {
            Title = "Video",
            Provider = "youtube",
            ProviderVideoId = "dQw4w9WgXcQ",
            LessonId = lesson.Id,
            VideoTypeId = created.Data.Id
        };
        db.AddRange(lesson, video);
        await db.SaveChangesAsync();

        var deleteHandler = new DeleteVideoTypeCommandHandler(db);
        var blocked = await deleteHandler.Handle(
            new DeleteVideoTypeCommand(created.Data.Id, adminId),
            CancellationToken.None);

        Assert.False(blocked.Success);
        Assert.Contains("VIDEO_TYPE_IN_USE", blocked.Errors!);
        Assert.True(await db.VideoTypes.AnyAsync(type => type.Id == created.Data.Id));
        Assert.True(await db.AuditLogs.AnyAsync(log => log.Action == "DELETE_VIDEO_TYPE_BLOCKED"));
    }

    [Fact]
    public async Task VideoTypeLifecycle_AllowsUpdateAndUnusedDelete()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var adminId = Guid.NewGuid();
        var type = new VideoType
        {
            Name = "قديم",
            NormalizedName = "قديم",
            SortOrder = 90,
            IsActive = true
        };
        db.VideoTypes.Add(type);
        await db.SaveChangesAsync();

        var updateHandler = new UpdateVideoTypeCommandHandler(db);
        var updated = await updateHandler.Handle(
            new UpdateVideoTypeCommand(type.Id, "جديد", 15, adminId),
            CancellationToken.None);
        Assert.True(updated.Success);
        Assert.Equal("جديد", updated.Data!.Name);
        Assert.Equal(15, updated.Data.SortOrder);

        var deleteHandler = new DeleteVideoTypeCommandHandler(db);
        var deleted = await deleteHandler.Handle(new DeleteVideoTypeCommand(type.Id, adminId), CancellationToken.None);
        Assert.True(deleted.Success);
        Assert.False(await db.VideoTypes.AnyAsync(item => item.Id == type.Id));
        Assert.True(await db.AuditLogs.AnyAsync(log => log.Action == "DELETE_VIDEO_TYPE"));
    }

    [Fact]
    public void VideoTypeController_SeparatesReadPermissionFromAdminMutations()
    {
        var list = typeof(AdminVideoTypesController).GetMethod(nameof(AdminVideoTypesController.List))!;
        Assert.NotEmpty(list.GetCustomAttributes(typeof(HasPermissionAttribute), inherit: true));

        foreach (var methodName in new[]
                 {
                     nameof(AdminVideoTypesController.Create),
                     nameof(AdminVideoTypesController.Update),
                     nameof(AdminVideoTypesController.SetStatus),
                     nameof(AdminVideoTypesController.Delete)
                 })
        {
            var method = typeof(AdminVideoTypesController).GetMethod(methodName)!;
            var authorize = Assert.Single(method.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true).Cast<AuthorizeAttribute>());
            Assert.Equal("Admin", authorize.Roles);
        }
    }

    [Fact]
    public async Task StandardVideoCommands_RequireActiveReplacementAndPreserveInactiveCurrentType()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var activeType = new VideoType { Name = "شرح", NormalizedName = "شرح", SortOrder = 10, IsActive = true };
        var inactiveType = new VideoType { Name = "قديم", NormalizedName = "قديم", SortOrder = 20, IsActive = false };
        var lesson = new Lesson { Title = "Lesson", Summary = "Summary" };
        db.AddRange(activeType, inactiveType, lesson);
        await db.SaveChangesAsync();

        var auth = new TeacherAuthorizationService(db);
        var createHandler = new CreateVideoCommandHandler(db, Array.Empty<IVideoProvider>(), auth);
        var invalid = await createHandler.Handle(
            new CreateVideoCommand("Video", "youtube", "dQw4w9WgXcQ", 1, 3, lesson.Id, Guid.NewGuid()),
            CancellationToken.None);
        Assert.False(invalid.Success);
        Assert.Contains("VIDEO_TYPE_INVALID", invalid.Errors!);

        var created = await createHandler.Handle(
            new CreateVideoCommand("Video", "youtube", "dQw4w9WgXcQ", 1, 3, lesson.Id, activeType.Id),
            CancellationToken.None);
        Assert.True(created.Success);
        var video = await db.LessonVideos.SingleAsync(item => item.Id == created.Data);
        var originalCode = video.InternalCode;

        activeType.IsActive = false;
        await db.SaveChangesAsync();
        var updateHandler = new UpdateVideoCommandHandler(db, Array.Empty<IVideoProvider>(), auth);
        var unchangedInactive = await updateHandler.Handle(
            new UpdateVideoCommand(video.Id, "Renamed", "youtube", "dQw4w9WgXcQ", 2, 4, activeType.Id),
            CancellationToken.None);
        Assert.True(unchangedInactive.Success);
        Assert.Equal(originalCode, video.InternalCode);

        var inactiveReplacement = await updateHandler.Handle(
            new UpdateVideoCommand(video.Id, "Rejected", "youtube", "dQw4w9WgXcQ", 2, 4, inactiveType.Id),
            CancellationToken.None);
        Assert.False(inactiveReplacement.Success);
        Assert.Contains("VIDEO_TYPE_INVALID", inactiveReplacement.Errors!);
        Assert.Equal(activeType.Id, video.VideoTypeId);
    }

    [Fact]
    public async Task BunnyCreation_RejectsInactiveTypeBeforeCallingExternalProvider()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var adminRole = new Role { Name = "Admin", Type = NaderGorge.Domain.Enums.RoleType.Admin };
        var admin = new User { FullName = "Admin", PhoneNumber = "151-admin", PasswordHash = "hash" };
        var teacherUser = new User { FullName = "Teacher", PhoneNumber = "151-teacher", PasswordHash = "hash" };
        var teacher = new TeacherProfile
        {
            UserId = teacherUser.Id,
            Bio = "Bio",
            Specialization = "Physics",
            ContactInfo = "contact"
        };
        var subject = new Subject { Name = "Physics", NormalizedName = "PHYSICS", Description = "Physics" };
        var package = new Package
        {
            Name = "Package",
            Description = "Description",
            SubjectId = subject.Id,
            TeacherId = teacher.Id,
            TargetGrade = "3rd Secondary"
        };
        var term = new Term { Title = "Term", PackageId = package.Id };
        var section = new ContentSection { Title = "Section", TermId = term.Id };
        var lesson = new Lesson { Title = "Lesson", Summary = "Summary", ContentSectionId = section.Id };
        var activeType = new VideoType { Name = "شرح", NormalizedName = "شرح", SortOrder = 10, IsActive = true };
        var inactiveType = new VideoType { Name = "قديم", NormalizedName = "قديم", SortOrder = 20, IsActive = false };
        db.AddRange(adminRole, admin, teacherUser, teacher, subject, package, term, section, lesson, activeType, inactiveType);
        db.UserRoles.Add(new UserRole { UserId = admin.Id, RoleId = adminRole.Id });
        await db.SaveChangesAsync();

        var bunny = new RecordingBunnyClient();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var tusHandler = new CreateBunnyTusUploadCommandHandler(db, bunny, configuration);
        var invalidTus = await tusHandler.Handle(
            new CreateBunnyTusUploadCommand(null, null, lesson.Id, "Video", 1, 3, inactiveType.Id, "video.mp4", 100, admin.Id),
            CancellationToken.None);
        Assert.False(invalidTus.Success);
        Assert.Contains("VIDEO_TYPE_INVALID", invalidTus.Errors!);
        Assert.Equal(0, bunny.CreateCalls);

        var fetchHandler = new FetchBunnyVideoCommandHandler(db, bunny);
        var invalidFetch = await fetchHandler.Handle(
            new FetchBunnyVideoCommand(null, null, lesson.Id, "Video", 1, 3, inactiveType.Id, "https://example.com/video.mp4", admin.Id),
            CancellationToken.None);
        Assert.False(invalidFetch.Success);
        Assert.Equal(0, bunny.CreateCalls);

        var validTus = await tusHandler.Handle(
            new CreateBunnyTusUploadCommand(null, null, lesson.Id, "Video", 1, 3, activeType.Id, "video.mp4", 100, admin.Id),
            CancellationToken.None);
        Assert.True(validTus.Success);
        Assert.Equal(1, bunny.CreateCalls);
        var createdVideo = await db.LessonVideos.SingleAsync(video => video.Id == validTus.Data!.LessonVideoId);
        Assert.Equal(activeType.Id, createdVideo.VideoTypeId);
    }

    private sealed class RecordingBunnyClient : IBunnyStreamClient
    {
        public int CreateCalls { get; private set; }

        public Task<BunnyStreamVideoDto> CreateVideoAsync(string title, string? collectionId, CancellationToken cancellationToken)
        {
            CreateCalls++;
            return Task.FromResult(new BunnyStreamVideoDto(1, Guid.NewGuid().ToString("N"), title, 0, 0, 0, 0, 0, 0, collectionId, false, true));
        }

        public Task<BunnyFetchVideoResultDto> FetchVideoAsync(string url, string title, string? collectionId, CancellationToken cancellationToken) =>
            Task.FromResult(new BunnyFetchVideoResultDto(true, null, 200));

        public Task<BunnyStreamVideoDto?> GetVideoAsync(string videoGuid, CancellationToken cancellationToken) => Task.FromResult<BunnyStreamVideoDto?>(null);
        public Task<IReadOnlyList<BunnyStreamVideoDto>> ListVideosAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<BunnyStreamVideoDto>>([]);
        public Task<BunnyVideoStorageDto?> GetVideoStorageAsync(string videoGuid, CancellationToken cancellationToken) => Task.FromResult<BunnyVideoStorageDto?>(null);
        public Task<BunnyVideoLibraryDto?> GetVideoLibraryAsync(CancellationToken cancellationToken) => Task.FromResult<BunnyVideoLibraryDto?>(null);
        public BunnyTusUploadSignatureDto CreateTusUploadSignature(string videoGuid, TimeSpan expiresIn) => new(1, videoGuid, "https://video.bunnycdn.com/tusupload", "signature", 1);
        public Task TriggerSmartActionsAsync(string videoGuid, BunnySmartActionsRequest request, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
