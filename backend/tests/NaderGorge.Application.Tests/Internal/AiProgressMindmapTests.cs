using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Internal.Commands;
using NaderGorge.Domain.Entities;

namespace NaderGorge.Application.Tests.Internal;

public class AiProgressMindmapTests
{
    [Theory]
    [InlineData("", false, true)]
    [InlineData("_mindmaps", true, false)]
    // Regression: 2026-08-24 a failed lesson analysis broadcast yt-dlp diagnostics.
    public async Task FailedCallback_WithHostileDiagnostics_ClearsMatchingState_AndSanitizesOutbox(
        string jobIdSuffix,
        bool expectedIsProcessingAi,
        bool expectedIsProcessingMindmaps)
    {
        await using var db = TestAppDbContextFactory.Create();
        var teacherUser = await TestAppDbContextFactory.SeedUserAsync(db, "Mindmap Teacher", "01070000001");
        var teacher = new TeacherProfile { Id = Guid.NewGuid(), UserId = teacherUser.Id, User = teacherUser };
        var subject = new Subject { Id = Guid.NewGuid(), Name = "History", NormalizedName = "HISTORY" };
        var package = new Package
        {
            Id = Guid.NewGuid(), Name = "History Package", SubjectId = subject.Id, Subject = subject,
            TeacherId = teacher.Id, Teacher = teacher, TargetGrade = "3rd Secondary",
        };
        var term = new Term { Id = Guid.NewGuid(), Title = "Term", PackageId = package.Id, Package = package };
        var section = new ContentSection { Id = Guid.NewGuid(), Title = "Section", TermId = term.Id, Term = term };
        var lesson = new Lesson { Id = Guid.NewGuid(), Title = "Lesson", ContentSectionId = section.Id, ContentSection = section };
        var video = new LessonVideo
        {
            Id = Guid.NewGuid(), Title = "Video", Provider = "youtube", ProviderVideoId = "video-id",
            LessonId = lesson.Id, Lesson = lesson, IsProcessingAI = true, IsProcessingMindmaps = true,
        };
        db.LessonVideos.Add(video);
        await db.SaveChangesAsync();

        const string rawWorkerMessage =
            "ERROR yt-dlp --cookies /run/secrets/cookies.txt https://video.example/private?id=secret";
        var expectedFailureCode = jobIdSuffix.Length == 0
            ? "AI_VIDEO_ANALYSIS_FAILED"
            : "AI_MINDMAP_GENERATION_FAILED";
        var expectedFailureMessage = jobIdSuffix.Length == 0
            ? AiProgressPublicContract.AnalysisFailureMessage
            : AiProgressPublicContract.MindmapFailureMessage;
        var handler = new AiProgressCommandHandler(db);
        var result = await handler.Handle(
            new AiProgressCommand($"{video.Id}{jobIdSuffix}", 0, "failed", rawWorkerMessage),
            CancellationToken.None);

        var updatedVideo = await db.LessonVideos.SingleAsync(candidate => candidate.Id == video.Id);
        var outboxEvents = await db.OutboxEvents.ToListAsync();
        Assert.True(result.Success);
        Assert.Equal(expectedIsProcessingAi, updatedVideo.IsProcessingAI);
        Assert.Equal(expectedIsProcessingMindmaps, updatedVideo.IsProcessingMindmaps);
        Assert.Contains(outboxEvents, candidate => candidate.Type == "AiJobProgress");
        Assert.Contains(outboxEvents, candidate => candidate.Type == "VideoFailed");
        Assert.Contains(outboxEvents, candidate => candidate.Type == "AiJobFailed");
        Assert.All(outboxEvents, outboxEvent =>
        {
            var payload = outboxEvent.PayloadJson;
            Assert.DoesNotContain("yt-dlp", payload, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("--cookies", payload, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("/run/secrets", payload, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("https://", payload, StringComparison.OrdinalIgnoreCase);
            using var document = System.Text.Json.JsonDocument.Parse(payload);
            var root = document.RootElement;
            var publicMessage = root.TryGetProperty("message", out var messageElement)
                ? messageElement.GetString()
                : root.GetProperty("error").GetString();
            var publicFailure = root.GetProperty("failure");
            Assert.Equal(expectedFailureMessage, publicMessage);
            Assert.Equal(expectedFailureCode, publicFailure.GetProperty("code").GetString());
            Assert.True(publicFailure.GetProperty("retryable").GetBoolean());
        });
    }
}
