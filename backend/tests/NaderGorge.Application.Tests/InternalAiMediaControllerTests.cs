using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NaderGorge.API.Controllers;
using NaderGorge.Application.Common;
using NaderGorge.Application.Interfaces;
using NaderGorge.Domain.Entities;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests;

public sealed class InternalAiMediaControllerTests
{
    [Fact]
    public async Task GetBunnyOriginal_RejectsStaleRunBeforeResolvingLibraryOrMedia()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"internal-ai-media-{Guid.NewGuid():N}")
            .Options);
        var currentRun = Guid.NewGuid();
        var video = new LessonVideo
        {
            Provider = VideoProviders.Bunny,
            ProviderVideoId = "12345678-abcd-1234-abcd-123456789abc",
            BunnyStreamLibraryId = Guid.NewGuid(),
            IsProcessingAI = true,
            CurrentAiAnalysisRunId = currentRun,
            LessonId = Guid.NewGuid(),
            VideoTypeId = Guid.NewGuid()
        };
        db.LessonVideos.Add(video);
        await db.SaveChangesAsync();

        var controller = new InternalAiMediaController(
            db,
            new ThrowingLibraryAccessService(),
            new ThrowingClientFactory(),
            new ThrowingOriginalMediaReader());

        var result = await controller.GetBunnyOriginal(video.Id, Guid.NewGuid(), CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var body = JsonSerializer.Serialize(conflict.Value);
        Assert.Contains("BUNNY_ANALYSIS_RUN_STALE", body, StringComparison.Ordinal);
    }

    private sealed class ThrowingLibraryAccessService : IBunnyStreamLibraryAccessService
    {
        public Task<BunnyStreamLibraryAccessResult> ResolveAsync(Guid libraryId, bool requireActive, CancellationToken cancellationToken) =>
            throw new Xunit.Sdk.XunitException("The stale run must not resolve a library.");

        public Task<BunnyStreamLibraryAccessResult> ResolveByExternalIdAsync(long externalLibraryId, bool requireActive, CancellationToken cancellationToken) =>
            throw new Xunit.Sdk.XunitException("The stale run must not resolve a library.");
    }

    private sealed class ThrowingClientFactory : IBunnyStreamClientFactory
    {
        public IBunnyStreamClient Create(long libraryId, string apiKey) =>
            throw new Xunit.Sdk.XunitException("The stale run must not contact Bunny.");
    }

    private sealed class ThrowingOriginalMediaReader : IBunnyOriginalMediaReader
    {
        public Task<BunnyOriginalMediaStream> OpenAsync(BunnyStreamLibraryAccess library, string videoGuid, CancellationToken cancellationToken) =>
            throw new Xunit.Sdk.XunitException("The stale run must not open media.");
    }
}
