using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Features.Admin.Commands.MindmapOps;
using NaderGorge.Application.Features.Internal.Commands;
using NaderGorge.Application.Interfaces;
using NaderGorge.Domain.Entities;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests;

public sealed class AiGenerationRunFencingTests
{
    [Fact]
    public async Task LegacyAnalysisCompletion_AfterTerminalStateIsIgnored()
    {
        await using var fixture = await RelationalAiFixture.CreateAsync();
        var handler = new AiAnalysisCompletedCommandHandler(
            fixture.Db,
            NullLogger<AiAnalysisCompletedCommandHandler>.Instance);

        var response = await handler.Handle(new AiAnalysisCompletedCommand(
            fixture.VideoId,
            "/subtitles/late.srt",
            [new ChapterDto { Title = "Late chapter", SummaryText = "Late summary" }]),
            CancellationToken.None);

        Assert.True(response.Success);
        Assert.False(response.Data!.Accepted);
        fixture.Db.ChangeTracker.Clear();
        Assert.Null((await fixture.VideoAsync()).SubtitleUrl);
        Assert.Equal("Chapter", (await fixture.ChapterAsync()).Title);
    }

    [Fact]
    public async Task MismatchedAnalysisCompletion_DoesNotReplaceResultsOrReleaseCurrentRun()
    {
        await using var fixture = await RelationalAiFixture.CreateAsync();
        var currentRunId = Guid.NewGuid();
        var video = await fixture.VideoAsync();
        video.IsProcessingAI = true;
        video.CurrentAiAnalysisRunId = currentRunId;
        video.SubtitleUrl = "/subtitles/stale.srt";
        await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear();
        var handler = new AiAnalysisCompletedCommandHandler(
            fixture.Db,
            NullLogger<AiAnalysisCompletedCommandHandler>.Instance);

        var response = await handler.Handle(new AiAnalysisCompletedCommand(
            fixture.VideoId,
            "/subtitles/stale.srt",
            [new ChapterDto { Title = "Stale chapter", SummaryText = "Stale summary" }],
            GenerationRunId: Guid.NewGuid()), CancellationToken.None);

        Assert.True(response.Success);
        Assert.False(response.Data!.Accepted);
        fixture.Db.ChangeTracker.Clear();
        video = await fixture.VideoAsync();
        Assert.True(video.IsProcessingAI);
        Assert.Equal(currentRunId, video.CurrentAiAnalysisRunId);
        Assert.Equal("/subtitles/stale.srt", video.SubtitleUrl);
        Assert.Equal("Chapter", (await fixture.ChapterAsync()).Title);
    }

    [Fact]
    public async Task CurrentAnalysisCompletion_AndLostResponseDuplicateReturnAcceptedReceipt()
    {
        await using var fixture = await RelationalAiFixture.CreateAsync();
        var runId = Guid.NewGuid();
        var video = await fixture.VideoAsync();
        video.IsProcessingAI = true;
        video.CurrentAiAnalysisRunId = runId;
        await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear();
        var handler = new AiAnalysisCompletedCommandHandler(
            fixture.Db,
            NullLogger<AiAnalysisCompletedCommandHandler>.Instance);
        var command = new AiAnalysisCompletedCommand(
            fixture.VideoId,
            "/subtitles/current.srt",
            [new ChapterDto { Title = "Current chapter", SummaryText = "Current summary", Order = 1 }],
            GenerationRunId: runId);

        var accepted = await handler.Handle(command, CancellationToken.None);
        var duplicate = await handler.Handle(command, CancellationToken.None);

        Assert.True(accepted.Success);
        Assert.True(accepted.Data!.Accepted);
        Assert.True(duplicate.Success);
        Assert.True(duplicate.Data!.Accepted);
        fixture.Db.ChangeTracker.Clear();
        video = await fixture.VideoAsync();
        Assert.False(video.IsProcessingAI);
        Assert.Null(video.CurrentAiAnalysisRunId);
        Assert.Equal("/subtitles/current.srt", video.SubtitleUrl);
        Assert.Equal(
            "Current chapter",
            await fixture.Db.VideoChapters
                .Where(chapter => chapter.LessonVideoId == fixture.VideoId)
                .Select(chapter => chapter.Title)
                .SingleAsync());
    }

    [Fact]
    public async Task MismatchedSingleCompletion_DoesNotReplaceImageOrReleaseLocks()
    {
        await using var fixture = await RelationalAiFixture.CreateAsync();
        var currentRunId = await fixture.ActivateSingleRunAsync();
        var chapter = await fixture.ChapterAsync();
        chapter.MindmapImageUrl = "/mindmaps/stale.webp";
        await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear();
        var handler = new SingleMindmapCompletedCommandHandler(fixture.Db);

        var response = await handler.Handle(new SingleMindmapCompletedCommand(
            fixture.ChapterId,
            "/mindmaps/stale.webp",
            Guid.NewGuid()), CancellationToken.None);

        Assert.True(response.Success);
        Assert.False(response.Data!.Accepted);
        fixture.Db.ChangeTracker.Clear();
        var video = await fixture.VideoAsync();
        chapter = await fixture.ChapterAsync();
        Assert.True(video.IsProcessingMindmaps);
        Assert.True(chapter.IsRegeneratingMindmap);
        Assert.Equal(currentRunId, video.CurrentMindmapGenerationRunId);
        Assert.Equal(currentRunId, chapter.CurrentMindmapGenerationRunId);
        Assert.Equal("/mindmaps/stale.webp", chapter.MindmapImageUrl);
    }

    [Fact]
    public async Task MismatchedSingleFailure_DoesNotReplaceImageOrReleaseLocks()
    {
        await using var fixture = await RelationalAiFixture.CreateAsync();
        var currentRunId = await fixture.ActivateSingleRunAsync();
        var handler = new SingleMindmapFailedCommandHandler(fixture.Db);

        var response = await handler.Handle(
            new SingleMindmapFailedCommand(fixture.ChapterId, Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(response.Success);
        await AssertSingleRunUnchangedAsync(fixture, currentRunId);
    }

    [Fact]
    public async Task AnalysisStart_WhenMindmapIsRunningDoesNotQueue()
    {
        await using var fixture = await RelationalAiFixture.CreateAsync();
        var video = await fixture.VideoAsync();
        video.IsProcessingMindmaps = true;
        video.CurrentMindmapGenerationRunId = Guid.NewGuid();
        await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear();
        var jobs = new RecordingJobEnqueuer();
        var handler = new AnalyzeVideoAICommandHandler(
            fixture.Db,
            jobs,
            new NoOpAiJobCancellationStore());

        var response = await handler.Handle(
            new AnalyzeVideoAICommand(fixture.VideoId, Guid.NewGuid()),
            CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(0, jobs.EnqueueCount);
    }

    [Fact]
    public async Task BatchMindmapStart_WhenAnalysisIsRunningDoesNotQueue()
    {
        await using var fixture = await RelationalAiFixture.CreateAsync();
        var video = await fixture.VideoAsync();
        video.IsProcessingAI = true;
        video.CurrentAiAnalysisRunId = Guid.NewGuid();
        await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear();
        var jobs = new RecordingJobEnqueuer();
        var handler = new GenerateChapterMindmapsCommandHandler(
            fixture.Db,
            jobs,
            new NoOpAiJobCancellationStore());

        var response = await handler.Handle(
            new GenerateChapterMindmapsCommand(fixture.VideoId),
            CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(0, jobs.EnqueueCount);
    }

    [Fact]
    public async Task BatchMindmapStart_QueuesStableChapterIdentityAndOrder()
    {
        await using var fixture = await RelationalAiFixture.CreateAsync();
        var jobs = new RecordingJobEnqueuer();
        var handler = new GenerateChapterMindmapsCommandHandler(
            fixture.Db,
            jobs,
            new NoOpAiJobCancellationStore());

        var response = await handler.Handle(
            new GenerateChapterMindmapsCommand(fixture.VideoId),
            CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(1, jobs.EnqueueCount);
        var chapterPayload = jobs.LastPayload!.Value
            .GetProperty("chapters")
            .EnumerateArray()
            .Single();
        Assert.Equal(fixture.ChapterId, chapterPayload.GetProperty("chapterId").GetGuid());
        Assert.Equal(1, chapterPayload.GetProperty("order").GetInt32());
    }

    [Fact]
    public async Task SingleMindmapStart_AllowsOnlyOneRunPerVideo()
    {
        await using var fixture = await RelationalAiFixture.CreateAsync();
        var jobs = new RecordingJobEnqueuer();
        var handler = new RegenerateChapterMindmapCommandHandler(
            fixture.Db,
            jobs,
            new NoOpAiJobCancellationStore());

        var first = await handler.Handle(
            new RegenerateChapterMindmapCommand(fixture.ChapterId),
            CancellationToken.None);
        var second = await handler.Handle(
            new RegenerateChapterMindmapCommand(fixture.ChapterId),
            CancellationToken.None);

        Assert.True(first.Success);
        Assert.False(second.Success);
        Assert.Equal(1, jobs.EnqueueCount);
    }

    [Fact]
    public async Task AnalysisPreparationFailure_ReleasesAcquiredVideoLock()
    {
        await using var fixture = await RelationalAiFixture.CreateAsync();
        var handler = new AnalyzeVideoAICommandHandler(
            fixture.Db,
            new RecordingJobEnqueuer(),
            new ThrowingAnalysisClearCancellationStore());

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new AnalyzeVideoAICommand(fixture.VideoId, Guid.NewGuid()),
            CancellationToken.None));

        fixture.Db.ChangeTracker.Clear();
        var video = await fixture.VideoAsync();
        Assert.False(video.IsProcessingAI);
        Assert.Null(video.CurrentAiAnalysisRunId);
    }

    [Fact]
    public async Task BatchPreparationFailure_ReleasesAcquiredVideoLock()
    {
        await using var fixture = await RelationalAiFixture.CreateAsync();
        var handler = new GenerateChapterMindmapsCommandHandler(
            fixture.Db,
            new RecordingJobEnqueuer(),
            new ThrowingMindmapClearCancellationStore());

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new GenerateChapterMindmapsCommand(fixture.VideoId),
            CancellationToken.None));

        fixture.Db.ChangeTracker.Clear();
        var video = await fixture.VideoAsync();
        Assert.False(video.IsProcessingMindmaps);
        Assert.Null(video.CurrentMindmapGenerationRunId);
    }

    [Fact]
    public async Task SingleChapterLockFailure_ReleasesPreviouslyAcquiredVideoLock()
    {
        await using var fixture = await RelationalAiFixture.CreateAsync(new ThrowOnChapterLockInterceptor());
        var handler = new RegenerateChapterMindmapCommandHandler(
            fixture.Db,
            new RecordingJobEnqueuer(),
            new NoOpAiJobCancellationStore());

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new RegenerateChapterMindmapCommand(fixture.ChapterId),
            CancellationToken.None));

        fixture.Db.ChangeTracker.Clear();
        var video = await fixture.VideoAsync();
        Assert.False(video.IsProcessingMindmaps);
        Assert.Null(video.CurrentMindmapGenerationRunId);
    }

    [Fact]
    public async Task SingleCancellationClearFailure_ReleasesVideoAndChapterLocks()
    {
        await using var fixture = await RelationalAiFixture.CreateAsync();
        var handler = new RegenerateChapterMindmapCommandHandler(
            fixture.Db,
            new RecordingJobEnqueuer(),
            new ThrowingMindmapClearCancellationStore());

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new RegenerateChapterMindmapCommand(fixture.ChapterId),
            CancellationToken.None));

        fixture.Db.ChangeTracker.Clear();
        var video = await fixture.VideoAsync();
        var chapter = await fixture.ChapterAsync();
        Assert.False(video.IsProcessingMindmaps);
        Assert.False(chapter.IsRegeneratingMindmap);
        Assert.Null(video.CurrentMindmapGenerationRunId);
        Assert.Null(chapter.CurrentMindmapGenerationRunId);
    }

    [Fact]
    public async Task MindmapCancel_ClearsVideoAndSingleChapterRun()
    {
        await using var fixture = await RelationalAiFixture.CreateAsync();
        await fixture.ActivateSingleRunAsync();
        var handler = new CancelAnalyzeVideoAICommandHandler(
            fixture.Db,
            new NoOpAiJobCancellationStore());

        var cancelled = await handler.Handle(
            new CancelAnalyzeVideoAICommand(fixture.VideoId, Guid.NewGuid(), IsMindmapOnly: true),
            CancellationToken.None);

        Assert.True(cancelled);
        fixture.Db.ChangeTracker.Clear();
        var video = await fixture.VideoAsync();
        var chapter = await fixture.ChapterAsync();
        Assert.False(video.IsProcessingMindmaps);
        Assert.Null(video.CurrentMindmapGenerationRunId);
        Assert.False(chapter.IsRegeneratingMindmap);
        Assert.Null(chapter.CurrentMindmapGenerationRunId);
    }

    [Fact]
    public async Task Cancel_WhenVideoIsIdleLeavesSubtitleAndChaptersUnchangedForBothModes()
    {
        await using var fixture = await RelationalAiFixture.CreateAsync();
        var video = await fixture.VideoAsync();
        video.SubtitleUrl = "/subtitles/retained.srt";
        await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear();
        var cancellations = new RecordingCancellationStore();
        var handler = new CancelAnalyzeVideoAICommandHandler(fixture.Db, cancellations);

        var mindmapOnly = await handler.Handle(
            new CancelAnalyzeVideoAICommand(fixture.VideoId, Guid.NewGuid(), IsMindmapOnly: true),
            CancellationToken.None);
        var full = await handler.Handle(
            new CancelAnalyzeVideoAICommand(fixture.VideoId, Guid.NewGuid()),
            CancellationToken.None);

        Assert.False(mindmapOnly);
        Assert.False(full);
        Assert.Equal(0, cancellations.VideoAnalysisRequestCount);
        Assert.Equal(0, cancellations.MindmapRequestCount);
        fixture.Db.ChangeTracker.Clear();
        video = await fixture.VideoAsync();
        Assert.Equal("/subtitles/retained.srt", video.SubtitleUrl);
        Assert.Equal("Chapter", (await fixture.ChapterAsync()).Title);
        Assert.False(await fixture.Db.OutboxEvents.AnyAsync(
            outboxEvent => outboxEvent.Type == "AiJobCancelled"));
    }

    [Fact]
    public async Task MindmapCancel_WhenMarkerRequestFailsRollsBackStateAndOutbox()
    {
        await using var fixture = await RelationalAiFixture.CreateAsync();
        var runId = await fixture.ActivateSingleRunAsync();
        var handler = new CancelAnalyzeVideoAICommandHandler(
            fixture.Db,
            new ThrowingMindmapRequestCancellationStore());

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new CancelAnalyzeVideoAICommand(fixture.VideoId, Guid.NewGuid(), IsMindmapOnly: true),
            CancellationToken.None));

        fixture.Db.ChangeTracker.Clear();
        var video = await fixture.VideoAsync();
        var chapter = await fixture.ChapterAsync();
        Assert.True(video.IsProcessingMindmaps);
        Assert.True(chapter.IsRegeneratingMindmap);
        Assert.Equal(runId, video.CurrentMindmapGenerationRunId);
        Assert.Equal(runId, chapter.CurrentMindmapGenerationRunId);
        Assert.False(await fixture.Db.OutboxEvents.AnyAsync(
            outboxEvent => outboxEvent.Type == "AiJobCancelled"));
    }

    [Fact]
    public async Task MindmapCancel_BlocksInterleavingRun_AndNextRunClearsMarkerBeforeEnqueue()
    {
        await using var fixture = await RelationalAiFixture.CreateAsync();
        await fixture.ActivateSingleRunAsync();
        await using var siblingDb = fixture.CreateSiblingContext();
        var cancellationStore = new InterleavingMindmapCancellationStore(
            siblingDb,
            fixture.VideoId);
        var handler = new CancelAnalyzeVideoAICommandHandler(
            fixture.Db,
            cancellationStore);

        var cancelled = await handler.Handle(
            new CancelAnalyzeVideoAICommand(fixture.VideoId, Guid.NewGuid(), IsMindmapOnly: true),
            CancellationToken.None);

        Assert.True(cancelled);
        Assert.True(cancellationStore.MarkerActive);
        Assert.True(cancellationStore.InterleavingWriteWasBlocked);
        fixture.Db.ChangeTracker.Clear();
        var video = await fixture.VideoAsync();
        var chapter = await fixture.ChapterAsync();
        Assert.False(video.IsProcessingMindmaps);
        Assert.False(chapter.IsRegeneratingMindmap);
        Assert.Null(video.CurrentMindmapGenerationRunId);
        Assert.Null(chapter.CurrentMindmapGenerationRunId);
        Assert.Equal(2, await fixture.Db.OutboxEvents.CountAsync(
            outboxEvent => outboxEvent.Type == "AiJobCancelled"));

        var jobs = new MarkerAwareJobEnqueuer(() => cancellationStore.MarkerActive);
        var restart = await new RegenerateChapterMindmapCommandHandler(
            fixture.Db,
            jobs,
            cancellationStore).Handle(
                new RegenerateChapterMindmapCommand(fixture.ChapterId),
                CancellationToken.None);

        Assert.True(restart.Success);
        Assert.False(cancellationStore.MarkerActive);
        Assert.Equal(1, jobs.EnqueueCount);
        Assert.False(jobs.MarkerWasActiveWhenQueued);
        fixture.Db.ChangeTracker.Clear();
        video = await fixture.VideoAsync();
        chapter = await fixture.ChapterAsync();
        Assert.True(video.IsProcessingMindmaps);
        Assert.True(chapter.IsRegeneratingMindmap);
        Assert.NotNull(video.CurrentMindmapGenerationRunId);
        Assert.Equal(video.CurrentMindmapGenerationRunId, chapter.CurrentMindmapGenerationRunId);
    }

    [Fact]
    public async Task LegacySingleCompletion_ClearsChapterWithoutRequiringOrChangingVideoLock()
    {
        await using var fixture = await RelationalAiFixture.CreateAsync();
        var chapter = await fixture.ChapterAsync();
        chapter.IsRegeneratingMindmap = true;
        await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear();
        var handler = new SingleMindmapCompletedCommandHandler(fixture.Db);

        var response = await handler.Handle(new SingleMindmapCompletedCommand(
            fixture.ChapterId,
            "/mindmaps/legacy.webp"), CancellationToken.None);

        Assert.True(response.Success);
        Assert.True(response.Data!.Accepted);
        fixture.Db.ChangeTracker.Clear();
        var video = await fixture.VideoAsync();
        chapter = await fixture.ChapterAsync();
        Assert.False(video.IsProcessingMindmaps);
        Assert.Null(video.CurrentMindmapGenerationRunId);
        Assert.False(chapter.IsRegeneratingMindmap);
        Assert.Null(chapter.CurrentMindmapGenerationRunId);
        Assert.Equal("/mindmaps/legacy.webp", chapter.MindmapImageUrl);
    }

    [Fact]
    public async Task LegacySingleFailure_ClearsChapterWithoutRequiringOrChangingVideoLock()
    {
        await using var fixture = await RelationalAiFixture.CreateAsync();
        var chapter = await fixture.ChapterAsync();
        chapter.IsRegeneratingMindmap = true;
        await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear();
        var handler = new SingleMindmapFailedCommandHandler(fixture.Db);

        var response = await handler.Handle(
            new SingleMindmapFailedCommand(fixture.ChapterId),
            CancellationToken.None);

        Assert.True(response.Success);
        fixture.Db.ChangeTracker.Clear();
        var video = await fixture.VideoAsync();
        chapter = await fixture.ChapterAsync();
        Assert.False(video.IsProcessingMindmaps);
        Assert.False(chapter.IsRegeneratingMindmap);
        Assert.Null(video.CurrentMindmapGenerationRunId);
        Assert.Null(chapter.CurrentMindmapGenerationRunId);
    }

    [Fact]
    public async Task CurrentSingleCompletion_UpdatesImageAndReleasesBothLocks()
    {
        await using var fixture = await RelationalAiFixture.CreateAsync();
        var runId = await fixture.ActivateSingleRunAsync();
        var handler = new SingleMindmapCompletedCommandHandler(fixture.Db);

        var response = await handler.Handle(new SingleMindmapCompletedCommand(
            fixture.ChapterId,
            "/mindmaps/current.webp",
            runId), CancellationToken.None);

        Assert.True(response.Success);
        Assert.True(response.Data!.Accepted);
        fixture.Db.ChangeTracker.Clear();
        var video = await fixture.VideoAsync();
        var chapter = await fixture.ChapterAsync();
        Assert.Equal("/mindmaps/current.webp", chapter.MindmapImageUrl);
        Assert.False(video.IsProcessingMindmaps);
        Assert.False(chapter.IsRegeneratingMindmap);
        Assert.Null(video.CurrentMindmapGenerationRunId);
        Assert.Null(chapter.CurrentMindmapGenerationRunId);
        var completedEvents = await fixture.Db.OutboxEvents
            .Where(outboxEvent => outboxEvent.Type == "AiJobCompleted")
            .ToListAsync();
        Assert.Equal(2, completedEvents.Count);
        Assert.Contains(completedEvents, outboxEvent => outboxEvent.TargetGroup == "Role_Admin");
        Assert.Contains(completedEvents, outboxEvent => outboxEvent.TargetUserId != null);
        Assert.All(completedEvents, outboxEvent =>
            Assert.Contains($"{fixture.VideoId}_mindmaps", outboxEvent.PayloadJson));

        var duplicate = await handler.Handle(new SingleMindmapCompletedCommand(
            fixture.ChapterId,
            "/mindmaps/current.webp",
            runId), CancellationToken.None);
        Assert.True(duplicate.Success);
        Assert.True(duplicate.Data!.Accepted);
    }

    [Fact]
    public async Task CurrentSingleFailure_ReleasesBothLocksWithoutReplacingImage()
    {
        await using var fixture = await RelationalAiFixture.CreateAsync();
        var runId = await fixture.ActivateSingleRunAsync();
        var handler = new SingleMindmapFailedCommandHandler(fixture.Db);

        var response = await handler.Handle(
            new SingleMindmapFailedCommand(fixture.ChapterId, runId),
            CancellationToken.None);

        Assert.True(response.Success);
        fixture.Db.ChangeTracker.Clear();
        var video = await fixture.VideoAsync();
        var chapter = await fixture.ChapterAsync();
        Assert.Null(chapter.MindmapImageUrl);
        Assert.False(video.IsProcessingMindmaps);
        Assert.False(chapter.IsRegeneratingMindmap);
        Assert.Null(video.CurrentMindmapGenerationRunId);
        Assert.Null(chapter.CurrentMindmapGenerationRunId);
    }

    [Fact]
    public async Task MismatchedBatchCompletion_ReturnsRejectedReceiptWithoutReleasingCurrentRun()
    {
        await using var fixture = await RelationalAiFixture.CreateAsync();
        var currentRunId = Guid.NewGuid();
        var video = await fixture.VideoAsync();
        var chapter = await fixture.ChapterAsync();
        video.IsProcessingMindmaps = true;
        video.CurrentMindmapGenerationRunId = currentRunId;
        chapter.MindmapImageUrl = "/mindmaps/stale-batch.webp";
        await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear();
        var handler = new MindmapsCompletedCommandHandler(fixture.Db);

        var response = await handler.Handle(new MindmapsCompletedCommand(
            fixture.VideoId,
            [new MindmapDto("Chapter", "/mindmaps/stale-batch.webp")],
            Guid.NewGuid()), CancellationToken.None);

        Assert.True(response.Success);
        Assert.False(response.Data!.Accepted);
        fixture.Db.ChangeTracker.Clear();
        video = await fixture.VideoAsync();
        Assert.True(video.IsProcessingMindmaps);
        Assert.Equal(currentRunId, video.CurrentMindmapGenerationRunId);
        Assert.Equal("/mindmaps/stale-batch.webp", (await fixture.ChapterAsync()).MindmapImageUrl);
        Assert.False(await fixture.Db.OutboxEvents.AnyAsync(
            outboxEvent => outboxEvent.Type == "AiJobCompleted"));
    }

    [Fact]
    public async Task CurrentBatchCompletion_AndLostResponseDuplicateReturnAcceptedReceipt()
    {
        await using var fixture = await RelationalAiFixture.CreateAsync();
        var runId = Guid.NewGuid();
        var video = await fixture.VideoAsync();
        video.IsProcessingMindmaps = true;
        video.CurrentMindmapGenerationRunId = runId;
        await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear();
        var handler = new MindmapsCompletedCommandHandler(fixture.Db);

        var response = await handler.Handle(new MindmapsCompletedCommand(
            fixture.VideoId,
            [new MindmapDto("Chapter", "/mindmaps/batch.webp", fixture.ChapterId, 1)],
            runId), CancellationToken.None);

        Assert.True(response.Success);
        Assert.True(response.Data!.Accepted);
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal("/mindmaps/batch.webp", (await fixture.ChapterAsync()).MindmapImageUrl);
        video = await fixture.VideoAsync();
        Assert.False(video.IsProcessingMindmaps);
        Assert.Null(video.CurrentMindmapGenerationRunId);
        var completedEvents = await fixture.Db.OutboxEvents
            .Where(outboxEvent => outboxEvent.Type == "AiJobCompleted")
            .ToListAsync();
        Assert.Equal(2, completedEvents.Count);
        Assert.All(completedEvents, outboxEvent =>
            Assert.Contains($"{fixture.VideoId}_mindmaps", outboxEvent.PayloadJson));

        var duplicate = await handler.Handle(new MindmapsCompletedCommand(
            fixture.VideoId,
            [new MindmapDto("Chapter", "/mindmaps/batch.webp", fixture.ChapterId, 1)],
            runId), CancellationToken.None);
        Assert.True(duplicate.Success);
        Assert.True(duplicate.Data!.Accepted);
    }

    [Fact]
    public async Task CurrentBatchCompletion_UsesChapterIdentityWhenTitlesWereEditedToDuplicates()
    {
        await using var fixture = await RelationalAiFixture.CreateAsync();
        var video = await fixture.VideoAsync();
        var firstChapter = await fixture.ChapterAsync();
        var secondChapter = new VideoChapter
        {
            Title = "Edited duplicate",
            SummaryText = "Second summary",
            Order = 2,
            LessonVideoId = fixture.VideoId
        };
        firstChapter.Title = "Edited duplicate";
        video.IsProcessingMindmaps = true;
        var runId = Guid.NewGuid();
        video.CurrentMindmapGenerationRunId = runId;
        fixture.Db.VideoChapters.Add(secondChapter);
        await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear();
        var handler = new MindmapsCompletedCommandHandler(fixture.Db);

        var response = await handler.Handle(new MindmapsCompletedCommand(
            fixture.VideoId,
            [
                new MindmapDto("Original duplicate", "/mindmaps/first.webp", fixture.ChapterId, 1),
                new MindmapDto("Original duplicate", "/mindmaps/second.webp", secondChapter.Id, 2)
            ],
            runId), CancellationToken.None);

        Assert.True(response.Success);
        Assert.True(response.Data!.Accepted);
        fixture.Db.ChangeTracker.Clear();
        var retainedImages = await fixture.Db.VideoChapters
            .Where(chapter => chapter.LessonVideoId == fixture.VideoId)
            .OrderBy(chapter => chapter.Order)
            .Select(chapter => chapter.MindmapImageUrl)
            .ToListAsync();
        Assert.Equal(
            new string?[] { "/mindmaps/first.webp", "/mindmaps/second.webp" },
            retainedImages);
    }

    [Fact]
    public async Task CurrentBatchCompletion_WithDuplicateIdentityOrOrderKeepsRunLocked()
    {
        await using var fixture = await RelationalAiFixture.CreateAsync();
        var runId = Guid.NewGuid();
        var video = await fixture.VideoAsync();
        var secondChapter = new VideoChapter
        {
            Title = "Second chapter",
            SummaryText = "Second summary",
            Order = 2,
            LessonVideoId = fixture.VideoId
        };
        video.IsProcessingMindmaps = true;
        video.CurrentMindmapGenerationRunId = runId;
        fixture.Db.VideoChapters.Add(secondChapter);
        await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear();
        var handler = new MindmapsCompletedCommandHandler(fixture.Db);

        var invalidPayloads = new List<MindmapDto>[]
        {
            new()
            {
                new MindmapDto("Chapter", "/mindmaps/first.webp", fixture.ChapterId, 1),
                new MindmapDto("Chapter", "/mindmaps/second.webp", fixture.ChapterId, 2)
            },
            new()
            {
                new MindmapDto("Chapter", "/mindmaps/first.webp", fixture.ChapterId, 1),
                new MindmapDto("Second chapter", "/mindmaps/second.webp", secondChapter.Id, 1)
            }
        };
        foreach (var payload in invalidPayloads)
        {
            var response = await handler.Handle(
                new MindmapsCompletedCommand(fixture.VideoId, payload, runId),
                CancellationToken.None);
            Assert.False(response.Success);
        }

        fixture.Db.ChangeTracker.Clear();
        video = await fixture.VideoAsync();
        Assert.True(video.IsProcessingMindmaps);
        Assert.Equal(runId, video.CurrentMindmapGenerationRunId);
        Assert.All(
            await fixture.Db.VideoChapters
                .Where(chapter => chapter.LessonVideoId == fixture.VideoId)
                .ToListAsync(),
            chapter => Assert.Null(chapter.MindmapImageUrl));
    }

    [Fact]
    public async Task CurrentBatchFailureProgress_ReleasesVideoLock()
    {
        await using var fixture = await RelationalAiFixture.CreateAsync();
        var runId = Guid.NewGuid();
        var video = await fixture.VideoAsync();
        video.IsProcessingMindmaps = true;
        video.CurrentMindmapGenerationRunId = runId;
        await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear();
        var handler = new AiProgressCommandHandler(fixture.Db);

        var response = await handler.Handle(new AiProgressCommand(
            $"{fixture.VideoId}_mindmaps",
            0,
            "failed",
            "failure",
            runId), CancellationToken.None);

        Assert.True(response.Success);
        fixture.Db.ChangeTracker.Clear();
        video = await fixture.VideoAsync();
        Assert.False(video.IsProcessingMindmaps);
        Assert.Null(video.CurrentMindmapGenerationRunId);
    }

    [Fact]
    public async Task StaleSingleProgress_WhenVideoHasNewerRunDoesNotPublishOrTouchVideo()
    {
        await using var fixture = await RelationalAiFixture.CreateAsync();
        var staleSingleRunId = Guid.NewGuid();
        var newerVideoRunId = Guid.NewGuid();
        var video = await fixture.VideoAsync();
        var chapter = await fixture.ChapterAsync();
        video.IsProcessingMindmaps = true;
        video.CurrentMindmapGenerationRunId = newerVideoRunId;
        chapter.IsRegeneratingMindmap = true;
        chapter.CurrentMindmapGenerationRunId = staleSingleRunId;
        await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear();
        var updatedAtBeforeCallback = (await fixture.VideoAsync()).UpdatedAt;
        var outboxCountBeforeCallback = await fixture.Db.OutboxEvents.CountAsync();
        fixture.Db.ChangeTracker.Clear();
        var handler = new AiProgressCommandHandler(fixture.Db);

        var response = await handler.Handle(new AiProgressCommand(
            $"{fixture.VideoId}_mindmap_{fixture.ChapterId}",
            50,
            "active",
            "stale progress",
            staleSingleRunId), CancellationToken.None);

        Assert.True(response.Success);
        fixture.Db.ChangeTracker.Clear();
        video = await fixture.VideoAsync();
        chapter = await fixture.ChapterAsync();
        Assert.Equal(updatedAtBeforeCallback, video.UpdatedAt);
        Assert.True(video.IsProcessingMindmaps);
        Assert.Equal(newerVideoRunId, video.CurrentMindmapGenerationRunId);
        Assert.True(chapter.IsRegeneratingMindmap);
        Assert.Equal(staleSingleRunId, chapter.CurrentMindmapGenerationRunId);
        Assert.Equal(outboxCountBeforeCallback, await fixture.Db.OutboxEvents.CountAsync());
    }

    private static async Task AssertSingleRunUnchangedAsync(
        RelationalAiFixture fixture,
        Guid currentRunId)
    {
        fixture.Db.ChangeTracker.Clear();
        var video = await fixture.VideoAsync();
        var chapter = await fixture.ChapterAsync();
        Assert.True(video.IsProcessingMindmaps);
        Assert.True(chapter.IsRegeneratingMindmap);
        Assert.Equal(currentRunId, video.CurrentMindmapGenerationRunId);
        Assert.Equal(currentRunId, chapter.CurrentMindmapGenerationRunId);
        Assert.Null(chapter.MindmapImageUrl);
    }

    private sealed class RecordingJobEnqueuer : IJobEnqueuer
    {
        public int EnqueueCount { get; private set; }
        public System.Text.Json.JsonElement? LastPayload { get; private set; }

        public Task EnqueueJobAsync<T>(string queueName, string jobName, T payload)
        {
            EnqueueCount++;
            LastPayload = System.Text.Json.JsonSerializer.SerializeToElement(payload);
            return Task.CompletedTask;
        }
    }

    private sealed class MarkerAwareJobEnqueuer : IJobEnqueuer
    {
        private readonly Func<bool> _isMarkerActive;

        public MarkerAwareJobEnqueuer(Func<bool> isMarkerActive)
        {
            _isMarkerActive = isMarkerActive;
        }

        public int EnqueueCount { get; private set; }
        public bool MarkerWasActiveWhenQueued { get; private set; }

        public Task EnqueueJobAsync<T>(string queueName, string jobName, T payload)
        {
            EnqueueCount++;
            MarkerWasActiveWhenQueued = _isMarkerActive();
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpAiJobCancellationStore : IAiJobCancellationStore
    {
        public Task RequestVideoAnalysisCancellationAsync(Guid videoId) => Task.CompletedTask;
        public Task RequestMindmapCancellationAsync(Guid videoId) => Task.CompletedTask;
        public Task ClearVideoAnalysisCancellationAsync(Guid videoId) => Task.CompletedTask;
        public Task ClearMindmapCancellationAsync(Guid videoId) => Task.CompletedTask;
    }

    private sealed class RecordingCancellationStore : IAiJobCancellationStore
    {
        public int VideoAnalysisRequestCount { get; private set; }
        public int MindmapRequestCount { get; private set; }

        public Task RequestVideoAnalysisCancellationAsync(Guid videoId)
        {
            VideoAnalysisRequestCount++;
            return Task.CompletedTask;
        }

        public Task RequestMindmapCancellationAsync(Guid videoId)
        {
            MindmapRequestCount++;
            return Task.CompletedTask;
        }

        public Task ClearVideoAnalysisCancellationAsync(Guid videoId) => Task.CompletedTask;
        public Task ClearMindmapCancellationAsync(Guid videoId) => Task.CompletedTask;
    }

    private sealed class InterleavingMindmapCancellationStore : IAiJobCancellationStore
    {
        private readonly AppDbContext _db;
        private readonly Guid _videoId;

        public InterleavingMindmapCancellationStore(
            AppDbContext db,
            Guid videoId)
        {
            _db = db;
            _videoId = videoId;
        }

        public bool MarkerActive { get; private set; }
        public bool InterleavingWriteWasBlocked { get; private set; }

        public Task RequestVideoAnalysisCancellationAsync(Guid videoId) => Task.CompletedTask;

        public async Task RequestMindmapCancellationAsync(Guid videoId)
        {
            MarkerActive = true;
            try
            {
                var newerRunId = Guid.NewGuid();
                await _db.LessonVideos
                    .Where(candidate => candidate.Id == _videoId)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(candidate => candidate.IsProcessingMindmaps, true)
                        .SetProperty(candidate => candidate.CurrentMindmapGenerationRunId, newerRunId));
            }
            catch (SqliteException exception) when (exception.SqliteErrorCode is 5 or 6)
            {
                InterleavingWriteWasBlocked = true;
            }
        }

        public Task ClearVideoAnalysisCancellationAsync(Guid videoId) => Task.CompletedTask;

        public Task ClearMindmapCancellationAsync(Guid videoId)
        {
            MarkerActive = false;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingAnalysisClearCancellationStore : IAiJobCancellationStore
    {
        public Task RequestVideoAnalysisCancellationAsync(Guid videoId) => Task.CompletedTask;
        public Task RequestMindmapCancellationAsync(Guid videoId) => Task.CompletedTask;
        public Task ClearVideoAnalysisCancellationAsync(Guid videoId) =>
            Task.FromException(new InvalidOperationException("analysis cancellation cleanup failed"));
        public Task ClearMindmapCancellationAsync(Guid videoId) => Task.CompletedTask;
    }

    private sealed class ThrowingMindmapClearCancellationStore : IAiJobCancellationStore
    {
        public Task RequestVideoAnalysisCancellationAsync(Guid videoId) => Task.CompletedTask;
        public Task RequestMindmapCancellationAsync(Guid videoId) => Task.CompletedTask;
        public Task ClearVideoAnalysisCancellationAsync(Guid videoId) => Task.CompletedTask;
        public Task ClearMindmapCancellationAsync(Guid videoId) =>
            Task.FromException(new InvalidOperationException("mindmap cancellation cleanup failed"));
    }

    private sealed class ThrowingMindmapRequestCancellationStore : IAiJobCancellationStore
    {
        public Task RequestVideoAnalysisCancellationAsync(Guid videoId) => Task.CompletedTask;
        public Task RequestMindmapCancellationAsync(Guid videoId) =>
            Task.FromException(new InvalidOperationException("mindmap cancellation request failed"));
        public Task ClearVideoAnalysisCancellationAsync(Guid videoId) => Task.CompletedTask;
        public Task ClearMindmapCancellationAsync(Guid videoId) => Task.CompletedTask;
    }

    private sealed class ThrowOnChapterLockInterceptor : DbCommandInterceptor
    {
        private bool _hasThrown;

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            ThrowOnceForChapterLock(command.CommandText);
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            ThrowOnceForChapterLock(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        private void ThrowOnceForChapterLock(string commandText)
        {
            if (_hasThrown ||
                !commandText.Contains("UPDATE \"video_chapters\"", StringComparison.OrdinalIgnoreCase) ||
                !commandText.Contains("IsRegeneratingMindmap", StringComparison.Ordinal))
                return;

            _hasThrown = true;
            throw new InvalidOperationException("chapter lock failed");
        }
    }

    private sealed class RelationalAiFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private RelationalAiFixture(SqliteConnection connection, AppDbContext db, Guid videoId, Guid chapterId)
        {
            _connection = connection;
            Db = db;
            VideoId = videoId;
            ChapterId = chapterId;
        }

        public AppDbContext Db { get; }
        public Guid VideoId { get; }
        public Guid ChapterId { get; }

        public static async Task<RelationalAiFixture> CreateAsync(params IInterceptor[] interceptors)
        {
            var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = $"ai-generation-runs-{Guid.NewGuid():N}",
                Mode = SqliteOpenMode.Memory,
                Cache = SqliteCacheMode.Shared,
                DefaultTimeout = 0
            }.ToString());
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .AddInterceptors(interceptors)
                .Options;
            var db = new AppDbContext(options);
            await db.Database.EnsureCreatedAsync();

            var teacherUser = new User
            {
                FullName = "AI Teacher",
                PhoneNumber = $"8{Guid.NewGuid():N}"[..11],
                PasswordHash = "hashed"
            };
            var teacher = new TeacherProfile
            {
                User = teacherUser,
                Specialization = "FirstSecondary",
                ContactInfo = "contact"
            };
            var subject = new Subject
            {
                Name = "Science",
                NormalizedName = Guid.NewGuid().ToString("N")
            };
            var package = new Package
            {
                Name = "Science Package",
                Subject = subject,
                Teacher = teacher,
                TargetGrade = "FirstSecondary"
            };
            var term = new Term { Title = "Term", Package = package };
            var section = new ContentSection { Title = "Section", Term = term };
            var lesson = new Lesson { Title = "Lesson", ContentSection = section };
            var videoType = new VideoType
            {
                Name = "شرح",
                NormalizedName = Guid.NewGuid().ToString("N")
            };
            var video = new LessonVideo
            {
                Title = "Video",
                Provider = "youtube",
                ProviderVideoId = "video-id",
                Lesson = lesson,
                VideoType = videoType
            };
            var chapter = new VideoChapter
            {
                Title = "Chapter",
                SummaryText = "Summary",
                Order = 1,
                LessonVideo = video
            };
            var teacherPhoto = new TeacherPhoto
            {
                Teacher = teacherUser,
                FileUrl = "/teacher-photos/teacher.webp",
                IsActive = true
            };

            db.AddRange(video, chapter, teacherPhoto);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            return new RelationalAiFixture(connection, db, video.Id, chapter.Id);
        }

        public Task<LessonVideo> VideoAsync() =>
            Db.LessonVideos.SingleAsync(video => video.Id == VideoId);

        public Task<VideoChapter> ChapterAsync() =>
            Db.VideoChapters.SingleAsync(chapter => chapter.Id == ChapterId);

        public AppDbContext CreateSiblingContext() =>
            new(new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(new SqliteConnectionStringBuilder(_connection.ConnectionString)
                {
                    DefaultTimeout = 1
                }.ToString())
                .Options);

        public async Task<Guid> ActivateSingleRunAsync()
        {
            var runId = Guid.NewGuid();
            var video = await VideoAsync();
            var chapter = await ChapterAsync();
            video.IsProcessingMindmaps = true;
            video.CurrentMindmapGenerationRunId = runId;
            chapter.IsRegeneratingMindmap = true;
            chapter.CurrentMindmapGenerationRunId = runId;
            await Db.SaveChangesAsync();
            Db.ChangeTracker.Clear();
            return runId;
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
