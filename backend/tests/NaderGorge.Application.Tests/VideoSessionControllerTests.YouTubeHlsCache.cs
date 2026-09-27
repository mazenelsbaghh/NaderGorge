using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NaderGorge.API.Controllers;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Services;

namespace NaderGorge.Application.Tests;

public sealed partial class VideoSessionControllerTests
{
    [Fact]
    public async Task YouTubeHlsCache_IndependentBackendInstancesShareExactAndLatestSource()
    {
        await using var db = TestAppDbContextFactory.Create();
        var session = ActiveSession();
        var (material, _) = await SeedYouTubeHlsMaterialAsync(db, session);
        var transport = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var protection = new EphemeralDataProtectionProvider();
        var first = StudentController(session.UserId, db, NullLogger<VideoSessionController>.Instance);
        var second = StudentController(session.UserId, db, NullLogger<VideoSessionController>.Instance);
        var source = JsonSerializer.SerializeToElement(SourceDocument());
        var firstCache = new YouTubeHlsSourceCache(transport, protection);
        var secondCache = new YouTubeHlsSourceCache(transport, protection);

        Assert.IsType<NoContentResult>(await first.PutYouTubeHlsSource(session.Id, source, material,
            new AccessCheckService(db), firstCache, default));
        foreach (var version in new string?[] { null, "abcdefghijklmnop" })
        {
            var response = await second.GetYouTubeHlsSource(session.Id, version, material,
                new AccessCheckService(db), secondCache, default);
            Assert.Equal(source.GetRawText(), Encoding.UTF8.GetString(Assert.IsType<FileContentResult>(response).FileContents));
        }
        var encrypted = await transport.GetAsync("massar:youtube-hls-source:v1:dQw4w9WgXcQ:abcdefghijklmnop");
        Assert.NotNull(encrypted);
        Assert.DoesNotContain("private-source", Encoding.UTF8.GetString(encrypted));
        Assert.IsType<NotFoundResult>(await second.GetYouTubeHlsSource(session.Id, "missingversion00", material,
            new AccessCheckService(db), secondCache, default));
    }

    [Theory]
    [InlineData("video")]
    [InlineData("version")]
    [InlineData("expired")]
    [InlineData("future")]
    [InlineData("expiry-string")]
    [InlineData("oversized")]
    public async Task YouTubeHlsCache_RejectsInvalidPayloadWithoutPublishingLatest(string invalid)
    {
        await using var db = TestAppDbContextFactory.Create();
        var session = ActiveSession();
        var (material, _) = await SeedYouTubeHlsMaterialAsync(db, session);
        var source = SourceDocument();
        if (invalid == "video") source["videoId"] = "aqz-KE-bpKQ";
        if (invalid == "version") source["version"] = "../../other-cache";
        if (invalid == "expired") source["expiresAt"] = DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeMilliseconds();
        if (invalid == "future") source["expiresAt"] = DateTimeOffset.UtcNow.AddHours(25).ToUnixTimeMilliseconds();
        if (invalid == "expiry-string") source["expiresAt"] = "later";
        if (invalid == "oversized") source["payload"] = new string('x', YouTubeHlsSourceCache.MaxSourceBytes);
        var cache = new YouTubeHlsSourceCache(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())), new EphemeralDataProtectionProvider());
        var controller = StudentController(session.UserId, db, NullLogger<VideoSessionController>.Instance);

        var response = await controller.PutYouTubeHlsSource(session.Id, JsonSerializer.SerializeToElement(source), material,
            new AccessCheckService(db), cache, default);

        Assert.IsType<BadRequestObjectResult>(response);
        Assert.Null(await cache.ReadAsync("dQw4w9WgXcQ", null, default));
    }

    [Theory]
    [InlineData("revoked", typeof(ForbidResult))]
    [InlineData("superseded", typeof(ConflictObjectResult))]
    [InlineData("other-user", typeof(NotFoundObjectResult))]
    public async Task YouTubeHlsCache_DeniedSessionCannotReadOrReplaceCachedSource(string denial, Type expected)
    {
        await using var db = TestAppDbContextFactory.Create();
        var session = ActiveSession();
        var (material, grant) = await SeedYouTubeHlsMaterialAsync(db, session);
        var source = JsonSerializer.SerializeToElement(SourceDocument());
        var cache = new YouTubeHlsSourceCache(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())), new EphemeralDataProtectionProvider());
        await cache.StoreAsync("dQw4w9WgXcQ", source, default);
        if (denial == "revoked") grant.IsActive = false;
        if (denial == "superseded") session.IsSuperseded = true;
        await db.SaveChangesAsync();
        var controller = StudentController(denial == "other-user" ? Guid.NewGuid() : session.UserId, db, NullLogger<VideoSessionController>.Instance);

        Assert.IsType(expected, await controller.GetYouTubeHlsSource(session.Id, null, material, new AccessCheckService(db), cache, default));
        Assert.IsType(expected, await controller.PutYouTubeHlsSource(session.Id, source, material, new AccessCheckService(db), cache, default));
    }

    [Fact]
    public async Task YouTubeHlsCache_IframeSessionCannotReadOrPublishSources()
    {
        await using var db = TestAppDbContextFactory.Create();
        var session = ActiveSession();
        var (material, _) = await SeedYouTubeHlsMaterialAsync(db, session);
        var encryption = new VideoEncryptionService();
        session.SessionToken = encryption.EncryptVideoInfo("youtube", "dQw4w9WgXcQ", session.EncryptionKey);
        await db.SaveChangesAsync();
        var controller = StudentController(session.UserId, db, NullLogger<VideoSessionController>.Instance);

        Assert.IsType<ForbidResult>(await controller.GetYouTubeHlsSource(session.Id, null, material, new AccessCheckService(db), null!, default));
        Assert.IsType<ForbidResult>(await controller.PutYouTubeHlsSource(session.Id, JsonSerializer.SerializeToElement(SourceDocument()),
            material, new AccessCheckService(db), null!, default));
    }

    [Fact]
    public async Task YouTubeHlsCache_DistributedCacheFailureReturnsUnavailableWithoutLocalFallback()
    {
        await using var db = TestAppDbContextFactory.Create();
        var session = ActiveSession();
        var (material, _) = await SeedYouTubeHlsMaterialAsync(db, session);
        var cache = new YouTubeHlsSourceCache(new UnavailableDistributedCache(), new EphemeralDataProtectionProvider());
        var controller = StudentController(session.UserId, db, NullLogger<VideoSessionController>.Instance);

        var read = await controller.GetYouTubeHlsSource(session.Id, null, material, new AccessCheckService(db), cache, default);
        var write = await controller.PutYouTubeHlsSource(session.Id, JsonSerializer.SerializeToElement(SourceDocument()), material,
            new AccessCheckService(db), cache, default);

        Assert.Equal(503, Assert.IsType<ObjectResult>(read).StatusCode);
        Assert.Equal(503, Assert.IsType<ObjectResult>(write).StatusCode);
    }

    private static Dictionary<string, object> SourceDocument() => new()
    {
        ["videoId"] = "dQw4w9WgXcQ", ["version"] = "abcdefghijklmnop",
        ["expiresAt"] = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeMilliseconds(),
        ["payload"] = "private-source"
    };

    private static async Task<(VideoSessionMaterialService Material, StudentAccessGrant Grant)> SeedYouTubeHlsMaterialAsync(
        AppDbContext db, VideoPlaybackSession session)
    {
        var (_, grant) = await SeedPlaybackAccessAsync(db, session);
        var video = await db.LessonVideos.FindAsync(session.LessonVideoId);
        video!.YouTubeHlsEnabled = true;
        video.ProviderVideoId = "dQw4w9WgXcQ";
        var encryption = new VideoEncryptionService();
        session.EncryptionKey = encryption.GenerateSessionKey();
        session.SessionToken = encryption.EncryptVideoInfo("youtube-hls", video.ProviderVideoId, session.EncryptionKey);
        db.VideoPlaybackSessions.Add(session);
        await db.SaveChangesAsync();
        return (new VideoSessionMaterialService(db, encryption, new BunnyHlsUrlSigner(),
            new BunnyStreamLibrarySecretProtector(new EphemeralDataProtectionProvider())), grant);
    }

    private sealed class UnavailableDistributedCache : IDistributedCache
    {
        public byte[]? Get(string key) => throw new IOException("Cache unavailable");
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromException<byte[]?>(new IOException("Cache unavailable"));
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw new IOException("Cache unavailable");
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) => Task.FromException(new IOException("Cache unavailable"));
        public void Refresh(string key) => throw new IOException("Cache unavailable");
        public Task RefreshAsync(string key, CancellationToken token = default) => Task.FromException(new IOException("Cache unavailable"));
        public void Remove(string key) => throw new IOException("Cache unavailable");
        public Task RemoveAsync(string key, CancellationToken token = default) => Task.FromException(new IOException("Cache unavailable"));
    }
}
