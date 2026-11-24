using System.Security.Claims;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NaderGorge.API.Controllers;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Content.Queries;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests;

public sealed class TeacherContentTitleTests
{
    [Fact]
    public async Task Teacher_with_legacy_content_permission_cannot_use_full_admin_edit_or_source_endpoints()
    {
        var controller = new AdminController(null!, null!)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([
                    new Claim(ClaimTypes.Role, "Teacher"), new Claim("permission", "content.manage")], "test"))
            } }
        };
        Assert.IsType<ForbidResult>(await controller.UpdateLesson(Guid.NewGuid(), new("غير مسموح", "ملخص", 1, 0)));
        Assert.IsType<ForbidResult>(await controller.UpdateVideo(Guid.NewGuid(), null!));
        Assert.IsType<ForbidResult>(await controller.GetLessonCockpit(Guid.NewGuid()));
    }

    [Theory]
    [InlineData("lesson")]
    [InlineData("video")]
    public async Task Owner_can_rename_only_the_title_and_audit_the_change(string target)
    {
        await using var fixture = await TitleFixture.Create();
        var request = JsonSerializer.Deserialize<TeacherController.ContentTitleRequest>(
            """{"title":"  الاسم الجديد  ","provider":"bunny","urlOrEmbedCode":"changed","price":0,"order":99,"isActive":true,"limit":0}""",
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var response = await fixture.Rename(fixture.OwnerId, target, request);
        Assert.IsType<OkObjectResult>(response);
        fixture.Db.ChangeTracker.Clear();
        var lesson = await fixture.Db.Lessons.SingleAsync();
        var video = await fixture.Db.LessonVideos.SingleAsync();
        Assert.Equal(target == "lesson" ? "الاسم الجديد" : "الحصة الأصلية", lesson.Title);
        Assert.Equal(target == "video" ? "الاسم الجديد" : "الفيديو الأصلي", video.Title);
        Assert.Equal(150, lesson.Price);
        Assert.Equal("الملخص الأصلي", lesson.Summary);
        Assert.Equal(3, lesson.Order);
        Assert.Equal("youtube", video.Provider);
        Assert.Equal("original-video-id", video.ProviderVideoId);
        Assert.Equal(4, video.Order);
        Assert.Equal(2, video.MaxWatchCount);
        Assert.Equal(7, video.SourceRevision);
        Assert.False(video.IsActive);
        Assert.True(video.YouTubeQualityEnabled);
        Assert.True(video.YouTubeHlsEnabled);
        Assert.Single(await fixture.Db.VideoChapters.ToListAsync());
        var audit = await fixture.Db.AuditLogs.SingleAsync(x => x.Action == "TeacherContentTitleUpdated");
        Assert.Equal(fixture.OwnerId, audit.PerformedByUserId);
        Assert.Equal(target == "lesson" ? lesson.Id : video.Id, audit.EntityId);
        Assert.Equal("الاسم الجديد", JsonDocument.Parse(audit.NewValues!).RootElement.GetProperty("Title").GetString());
    }

    [Theory]
    [InlineData("lesson", "foreign", 404)]
    [InlineData("video", "foreign", 404)]
    [InlineData("lesson", "no-content", 403)]
    [InlineData("video", "no-content", 403)]
    [InlineData("lesson", "inactive-staff", 403)]
    [InlineData("video", "inactive-staff", 403)]
    public async Task Foreign_or_unpermitted_accounts_cannot_rename_content(string target, string actor, int status)
    {
        await using var fixture = await TitleFixture.Create();
        var response = await fixture.Rename(fixture.ActorIds[actor], target, new("غير مصرح"));
        if (status == 404) Assert.IsType<NotFoundResult>(response);
        else Assert.IsType<ForbidResult>(response);
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal("الحصة الأصلية", (await fixture.Db.Lessons.SingleAsync()).Title);
        Assert.Equal("الفيديو الأصلي", (await fixture.Db.LessonVideos.SingleAsync()).Title);
        Assert.Empty(await fixture.Db.AuditLogs.ToListAsync());
    }

    [Theory]
    [InlineData("lesson", "")]
    [InlineData("video", "   ")]
    [InlineData("lesson", "too-long")]
    [InlineData("video", "too-long")]
    public async Task Invalid_title_is_rejected_without_writing(string target, string title)
    {
        await using var fixture = await TitleFixture.Create();
        var request = new TeacherController.ContentTitleRequest(title == "too-long" ? new string('س', 201) : title);
        Assert.IsType<BadRequestObjectResult>(await fixture.Rename(fixture.OwnerId, target, request));
        Assert.Empty(await fixture.Db.AuditLogs.ToListAsync());
    }

    [Theory]
    [InlineData("lesson")]
    [InlineData("video")]
    public async Task Content_staff_can_rename_and_repeating_the_same_name_is_a_noop(string target)
    {
        await using var fixture = await TitleFixture.Create();
        var actor = fixture.ActorIds["content"];
        Assert.IsType<OkObjectResult>(await fixture.Rename(actor, target, new("عنوان محفوظ")));
        Assert.IsType<OkObjectResult>(await fixture.Rename(actor, target, new("عنوان محفوظ")));
        var audit = Assert.Single(await fixture.Db.AuditLogs.ToListAsync());
        Assert.Equal(actor, audit.PerformedByUserId);
    }

    [Fact]
    public async Task Teacher_cockpit_hides_source_but_admin_cockpit_keeps_it()
    {
        await using var fixture = await TitleFixture.Create();
        var controller = fixture.Controller(fixture.OwnerId);
        var response = Assert.IsType<OkObjectResult>(await controller.GetTeacherLessonCockpit(fixture.LessonId, default));
        var teacherLesson = Assert.IsType<ApiResponse<LessonCockpitDto>>(response.Value).Data!;
        var video = Assert.Single(teacherLesson.Videos);
        Assert.Equal("الفيديو الأصلي", video.Title);
        Assert.Empty(video.Provider);
        Assert.Empty(video.Url);
        Assert.False(video.YouTubeQualityEnabled);
        Assert.False(video.YouTubeHlsEnabled);
        Assert.Null(video.BunnyLibrary);
        Assert.Null(video.PendingBunnyReplacement);
        var adminLesson = await fixture.Mediator.Send(new GetLessonCockpitQuery(fixture.LessonId));
        Assert.Equal("youtube", Assert.Single(adminLesson.Data!.Videos).Provider);
        Assert.Equal("original-video-id", Assert.Single(adminLesson.Data.Videos).Url);
        Assert.IsType<NotFoundResult>(await fixture.Controller(fixture.ActorIds["foreign"]).GetTeacherLessonCockpit(fixture.LessonId, default));
        Assert.IsType<ForbidResult>(await fixture.Controller(fixture.ActorIds["no-content"]).GetTeacherLessonCockpit(fixture.LessonId, default));
    }

    private sealed class TitleFixture : IAsyncDisposable
    {
        public AppDbContext Db { get; } = TestAppDbContextFactory.Create();
        private ServiceProvider Services { get; set; } = null!;
        public IMediator Mediator => Services.GetRequiredService<IMediator>();
        public Guid OwnerId { get; private set; }
        public Guid LessonId { get; private set; }
        private Guid VideoId { get; set; }
        public Dictionary<string, Guid> ActorIds { get; } = [];

        public static async Task<TitleFixture> Create()
        {
            var fixture = new TitleFixture();
            await fixture.Seed();
            fixture.Services = new ServiceCollection().AddSingleton<IAppDbContext>(fixture.Db)
                .AddTransient<TeacherAuthorizationService>()
                .AddMediatR(config => config.RegisterServicesFromAssemblyContaining<GetLessonCockpitQuery>())
                .BuildServiceProvider();
            return fixture;
        }

        private async Task Seed()
        {
            var role = new Role { Name = "Teacher", Type = RoleType.Teacher };
            var owner = new User { FullName = "المدرس", PhoneNumber = "01000000001", PasswordHash = "test" };
            owner.UserRoles.Add(new UserRole { Role = role, User = owner });
            var teacher = new TeacherProfile { User = owner };
            OwnerId = owner.Id;
            foreach (var actor in new[] { "foreign", "no-content", "content", "inactive-staff" })
            {
                var user = new User { FullName = actor, PhoneNumber = $"0100000000{ActorIds.Count + 2}", PasswordHash = "test" };
                user.UserRoles.Add(new UserRole { Role = role, User = user });
                ActorIds[actor] = user.Id;
                if (actor == "foreign") Db.TeacherProfiles.Add(new TeacherProfile { User = user });
                else Db.TeacherStaffMembers.Add(new TeacherStaffMember
                {
                    User = user, Teacher = teacher, CreatedByTeacherUser = owner,
                    IsActive = actor != "inactive-staff", PermissionKeys = actor == "no-content" ? "students" : "content"
                });
            }
            var lesson = new Lesson
            {
                Title = "الحصة الأصلية", Summary = "الملخص الأصلي", Price = 150, Order = 3,
                ContentSection = new ContentSection { Title = "الشهر", Term = new Term
                {
                    Title = "الترم", Package = new Package { Name = "الباقة", Teacher = teacher,
                        Subject = new Subject { Name = "التاريخ", NormalizedName = "HISTORY" } }
                } }
            };
            var video = new LessonVideo
            {
                Title = "الفيديو الأصلي", Lesson = lesson, Provider = "youtube", ProviderVideoId = "original-video-id",
                VideoType = new VideoType { Name = "شرح", NormalizedName = "EXPLANATION" },
                Order = 4, MaxWatchCount = 2, SourceRevision = 7, IsActive = false,
                YouTubeQualityEnabled = true, YouTubeHlsEnabled = true,
                VideoChapters = [new VideoChapter { Title = "الفصل الأصلي", SummaryText = "ملخص", StartTime = 0, EndTime = 10 }]
            };
            Db.Add(video);
            await Db.SaveChangesAsync();
            LessonId = lesson.Id;
            VideoId = video.Id;
        }

        public TeacherController Controller(Guid actorId) => new(Mediator, Db)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([
                    new Claim(ClaimTypes.NameIdentifier, actorId.ToString()), new Claim(ClaimTypes.Role, "Teacher")], "test"))
            } }
        };

        public Task<IActionResult> Rename(Guid actorId, string target, TeacherController.ContentTitleRequest request) =>
            target == "lesson" ? Controller(actorId).RenameLesson(LessonId, request, default)
                : Controller(actorId).RenameVideo(VideoId, request, default);

        public async ValueTask DisposeAsync()
        {
            await Services.DisposeAsync();
            await Db.DisposeAsync();
        }
    }
}
