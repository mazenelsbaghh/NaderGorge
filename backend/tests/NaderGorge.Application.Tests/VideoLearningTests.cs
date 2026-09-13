using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.VideoLearning;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;
using Xunit;

namespace NaderGorge.Application.Tests;

public sealed class VideoLearningTests
{
    private static LearningActivity Question() => new(Guid.NewGuid(), "question", "moment", 30, 30,
        "ما ناتج ٢ + ٢؟", "", "نجمع العددين فيكون الناتج ٤", "جمع", ["٣", "٤"], 1, true);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Student_document_hides_answer_keys_but_keeps_reveal_cards()
    {
        var question = Question();
        var card = question with { Id = Guid.NewGuid(), Kind = "card", Answer = "تعريف الجمع" };
        var doc = LearningRules.ForStudent(new(new(Questions: true, Cards: true), [question, card]));
        Assert.Null(doc.Activities[0].CorrectOption);
        Assert.Empty(doc.Activities[0].Answer);
        Assert.Equal("تعريف الجمع", doc.Activities[1].Answer);
    }

    [Fact]
    public async Task Answer_is_server_graded_persisted_and_idempotent_without_second_attempt()
    {
        await using var db = TestAppDbContextFactory.Create();
        var (student, video, config, question) = await Seed(db);
        var service = new VideoLearningService(db, new Access(student.Id), new Ai());
        var request = new LearningEntryRequest(Guid.NewGuid(), config.Version, "answer", 30, "0", ActivityId: question.Id);
        var wrong = await service.RecordAsync(student.Id, video.Id, request, default);
        Assert.False(wrong.Entry.Correct);
        Assert.Equal(question.Answer, wrong.Explanation);
        var replay = await service.RecordAsync(student.Id, video.Id, request, default);
        Assert.Equal(wrong.Entry.Id, replay.Entry.Id);
        var second = await service.RecordAsync(student.Id, video.Id, request with { Id = Guid.NewGuid(), Text = "1" }, default);
        Assert.False(second.Entry.Correct);
        Assert.Equal(1, await db.VideoLearningEntries.CountAsync());
        var snapshot = await service.SnapshotAsync(student.Id, video.Id, false, default);
        Assert.Single(snapshot.Entries);
        Assert.Null(snapshot.Document.Activities[0].CorrectOption);
    }

    [Fact]
    public async Task Notes_are_private_and_another_student_cannot_delete_them()
    {
        await using var db = TestAppDbContextFactory.Create();
        var (student, video, config, _) = await Seed(db);
        var other = new User { FullName = "طالب آخر", PhoneNumber = "01000000002", IsActive = true };
        db.Users.Add(other); await db.SaveChangesAsync();
        var service = new VideoLearningService(db, new Access(student.Id, other.Id), new Ai());
        var note = await service.RecordAsync(student.Id, video.Id, new(Guid.NewGuid(), config.Version, "note", 12, "ملاحظتي الخاصة"), default);
        Assert.Empty((await service.SnapshotAsync(other.Id, video.Id, false, default)).Entries);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.DeleteAsync(other.Id, video.Id, note.Entry.Id, default));
    }

    [Fact]
    public async Task Stale_source_suspends_activities_and_rejects_writes()
    {
        await using var db = TestAppDbContextFactory.Create();
        var (student, video, config, _) = await Seed(db);
        video.SourceRevision++; await db.SaveChangesAsync();
        var service = new VideoLearningService(db, new Access(student.Id), new Ai());
        var snapshot = await service.SnapshotAsync(student.Id, video.Id, false, default);
        Assert.True(snapshot.Stale);
        Assert.Empty(snapshot.Document.Activities);
        await Assert.ThrowsAsync<LearningConflictException>(() => service.RecordAsync(student.Id, video.Id,
            new(Guid.NewGuid(), config.Version, "note", 1, "note"), default));
    }

    [Fact]
    public async Task Student_cannot_use_author_mode_to_read_keys_or_generate_content()
    {
        await using var db = TestAppDbContextFactory.Create();
        var (student, video, config, _) = await Seed(db);
        var service = new VideoLearningService(db, new Access(student.Id), new Ai());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SnapshotAsync(student.Id, video.Id, true, default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.AiAsync(student.Id, video.Id,
            new(Guid.NewGuid(), config.Version, "author", 0, ""), default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new VideoLearningService(db, new Access(), new Ai()).SnapshotAsync(student.Id, video.Id, false, default));
    }

    [Fact]
    public async Task Questions_use_existing_moderated_comments_and_keep_timestamp()
    {
        await using var db = TestAppDbContextFactory.Create();
        var (student, video, config, _) = await Seed(db);
        var service = new VideoLearningService(db, new Access(student.Id), new Ai());
        var result = await service.RecordAsync(student.Id, video.Id, new(Guid.NewGuid(), config.Version, "ask", 42, "ممكن مثال؟"), default);
        var comment = await db.LessonComments.SingleAsync();
        Assert.Equal(result.Entry.CommentId, comment.Id);
        Assert.Equal(LessonCommentStatus.Pending, comment.Status);
        Assert.Contains("00:00:42", comment.Body);
        Assert.Equal(video.LessonId, comment.LessonId);
    }

    [Theory]
    [InlineData(-1, 10, "linear")]
    [InlineData(30, 29, "linear")]
    [InlineData(30, 30, "unknown")]
    public void Invalid_timing_or_experiment_template_cannot_publish(int start, int end, string template)
    {
        var activity = Question() with { Kind = "experiment", Seconds = start, EndSeconds = end, Experiment = template };
        Assert.Throws<ArgumentException>(() => LearningRules.Validate(new(new(), [activity]), 100));
    }

    [Fact]
    public async Task Ai_quota_is_reserved_and_successful_request_replay_does_not_charge_again()
    {
        await using var db = TestAppDbContextFactory.Create();
        var (student, video, config, question) = await Seed(db);
        config.DocumentJson = JsonSerializer.Serialize(new LearningDocument(new(AiTutor: true, AiDailyLimit: 1), [question]), Json);
        db.VideoChapters.Add(new VideoChapter { LessonVideoId = video.Id, Title = "الجمع", StartTime = 0, EndTime = 60, SummaryText = "جمع ٢ و٢ يساوي ٤" });
        await db.SaveChangesAsync();
        var ai = new Ai(); var service = new VideoLearningService(db, new Access(student.Id), ai);
        var request = new LearningAiRequest(Guid.NewGuid(), config.Version, "simplify", 30, "");
        var response = await service.AiAsync(student.Id, video.Id, request, default);
        var replay = await service.AiAsync(student.Id, video.Id, request, default);
        Assert.Equal(response.Text, replay.Text);
        Assert.Equal(response.Activities, replay.Activities);
        await Assert.ThrowsAsync<ArgumentException>(() => service.AiAsync(student.Id, video.Id, request with { Id = Guid.NewGuid() }, default));
        Assert.Equal(1, ai.Calls);
    }

    [Fact]
    public async Task Unconfigured_video_has_every_tool_disabled_and_rejects_notes()
    {
        await using var db = TestAppDbContextFactory.Create();
        var (student, video, config, _) = await Seed(db);
        db.VideoLearningConfigurations.Remove(config);
        await db.SaveChangesAsync();
        var service = new VideoLearningService(db, new Access(student.Id), new Ai());
        var snapshot = await service.SnapshotAsync(student.Id, video.Id, false, default);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(snapshot.Document.Tools, Json));
        Assert.All(document.RootElement.EnumerateObject().Where(p => p.Value.ValueKind == JsonValueKind.False || p.Value.ValueKind == JsonValueKind.True), p => Assert.False(p.Value.GetBoolean()));
        Assert.Empty(snapshot.Document.Activities);
        await Assert.ThrowsAsync<ArgumentException>(() => service.RecordAsync(student.Id, video.Id,
            new(Guid.NewGuid(), Guid.Empty, "note", 0, "ملاحظة"), default));
    }

    [Theory]
    [InlineData(RoleType.Teacher)]
    [InlineData(RoleType.Student)]
    public async Task Non_admin_cannot_publish_or_read_author_content_or_generate_drafts(RoleType type)
    {
        await using var db = TestAppDbContextFactory.Create();
        var (user, video, config, question) = await Seed(db);
        var role = new Role { Type = type, Name = type.ToString() };
        db.UserRoles.Add(new UserRole { UserId = user.Id, Role = role });
        await db.SaveChangesAsync();
        var service = new VideoLearningService(db, new Access(user.Id), new Ai());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SaveAsync(user.Id, video.Id,
            new(config.Version, video.SourceRevision, new(new(Notes: true), [question])), default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SnapshotAsync(user.Id, video.Id, true, default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.AiAsync(user.Id, video.Id,
            new(Guid.NewGuid(), config.Version, "author", 0, ""), default));
    }

    [Fact]
    public async Task Admin_can_enable_a_tool_and_disable_it_again()
    {
        await using var db = TestAppDbContextFactory.Create();
        var (user, video, config, _) = await Seed(db);
        db.UserRoles.Add(new UserRole { UserId = user.Id, Role = new Role { Name = "Admin", Type = RoleType.Admin } });
        await db.SaveChangesAsync();
        var service = new VideoLearningService(db, new Access(user.Id), new Ai());
        var enabled = await service.SaveAsync(user.Id, video.Id, new(config.Version, video.SourceRevision, new(new(Notes: true), [])), default);
        Assert.True(enabled.Document.Tools.Notes);
        var disabled = await service.SaveAsync(user.Id, video.Id, new(enabled.Version, video.SourceRevision, new(new(), [])), default);
        Assert.False(disabled.Document.Tools.Notes);
        await Assert.ThrowsAsync<ArgumentException>(() => service.RecordAsync(user.Id, video.Id,
            new(Guid.NewGuid(), disabled.Version, "note", 0, "ملاحظة"), default));
    }

    private static async Task<(User, LessonVideo, VideoLearningConfiguration, LearningActivity)> Seed(NaderGorge.Infrastructure.Data.AppDbContext db)
    {
        var student = new User { FullName = "طالب", PhoneNumber = "01000000001", IsActive = true };
        var video = new LessonVideo { Title = "الجمع", LessonId = Guid.NewGuid(), Provider = "youtube", IsActive = true };
        var question = Question();
        var config = new VideoLearningConfiguration { LessonVideoId = video.Id, DocumentJson = JsonSerializer.Serialize(
            new LearningDocument(new(Questions: true, AskTeacher: true, Understanding: true, Notes: true), [question]), Json) };
        db.Users.Add(student); db.LessonVideos.Add(video); db.VideoLearningConfigurations.Add(config); await db.SaveChangesAsync();
        return (student, video, config, question);
    }

    private sealed class Ai : IVideoLearningAi
    {
        public int Calls { get; private set; }
        public Task<LearningAiResult> GenerateAsync(string mode, string question, string context, CancellationToken ct)
        { Calls++; return Task.FromResult(new LearningAiResult("نجمع العددين", [])); }
    }
    private sealed class Access(params Guid[] students) : IAccessCheckService
    {
        public Task<bool> HasAccessToPackageAsync(Guid userId, Guid packageId, CancellationToken ct = default) => Task.FromResult(students.Contains(userId));
        public Task<bool> HasAccessToLessonAsync(Guid userId, Guid lessonId, CancellationToken ct = default) => Task.FromResult(students.Contains(userId));
        public Task<bool> HasAccessToVideoAsync(Guid userId, Guid lessonVideoId, CancellationToken ct = default) => Task.FromResult(students.Contains(userId));
        public Task<bool> HasAccessToExamAsync(Guid userId, Guid examId, CancellationToken ct = default) => Task.FromResult(students.Contains(userId));
    }
}
