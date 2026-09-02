using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NaderGorge.API.Controllers;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Admin.BunnyLibraries;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Features.Content.Queries;
using NaderGorge.Application.Features.Student.Commands;
using NaderGorge.Application.Interfaces;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Services;
using Xunit;

namespace NaderGorge.Application.Tests;

public sealed class BunnyStreamLibrariesTests
{
    private const string VideoGuid = "432b90d4-8c28-40bb-bcab-285be673f122";

    [Theory]
    [InlineData(nameof(AdminController.GetBunnyStreamLibraries), "settings.manage")]
    [InlineData(nameof(AdminController.CreateBunnyStreamLibrary), "settings.manage")]
    [InlineData(nameof(AdminController.UpdateBunnyStreamLibrary), "settings.manage")]
    [InlineData(nameof(AdminController.SetBunnyStreamLibraryStatus), "settings.manage")]
    [InlineData(nameof(AdminController.DeleteBunnyStreamLibrary), "settings.manage")]
    [InlineData(nameof(AdminController.GetAvailableBunnyStreamLibraries), "content.manage")]
    [InlineData(nameof(AdminController.CancelBunnyVideoReplacement), "content.manage")]
    public void BunnyManagementEndpoints_RequireTheirIntendedPermission(string methodName, string expectedPermission)
    {
        var endpoint = typeof(AdminController).GetMethod(methodName);
        var permission = Assert.Single(endpoint!.GetCustomAttributes(typeof(HasPermissionAttribute), inherit: true));
        var attribute = Assert.IsType<HasPermissionAttribute>(permission);

        Assert.Equal(expectedPermission, Assert.Single(attribute.Arguments!));
    }

    [Theory]
    [InlineData(nameof(AdminController.CreateBunnyStreamLibrary))]
    [InlineData(nameof(AdminController.UpdateBunnyStreamLibrary))]
    public void ApiKeyEndpoints_DisableRequestBodyHttpLogging(string methodName)
    {
        var endpoint = typeof(AdminController).GetMethod(methodName);
        var logging = Assert.Single(endpoint!.GetCustomAttributes(typeof(HttpLoggingAttribute), inherit: true));

        Assert.Equal(HttpLoggingFields.None, Assert.IsType<HttpLoggingAttribute>(logging).LoggingFields);
    }

    [Fact]
    public void LibrarySecretProtector_EncryptsAtRestAndScopesCiphertextToOneLibrary()
    {
        var protector = new BunnyStreamLibrarySecretProtector(new EphemeralDataProtectionProvider());
        var libraryId = Guid.NewGuid();
        const string apiKey = "private-library-key";

        var ciphertext = protector.Protect(libraryId, apiKey);

        Assert.False(Encoding.UTF8.GetBytes(apiKey).SequenceEqual(ciphertext));
        Assert.Equal(apiKey, protector.Unprotect(libraryId, ciphertext));
        Assert.Throws<CryptographicException>(() => protector.Unprotect(Guid.NewGuid(), ciphertext));
    }

    [Theory]
    [InlineData("https://player.mediadelivery.net/play/740733/432b90d4-8c28-40bb-bcab-285be673f122")]
    [InlineData("https://iframe.mediadelivery.net/embed/740733/432b90d4-8c28-40bb-bcab-285be673f122?autoplay=true")]
    public void ReferenceParser_AcceptsSupportedFullUrls(string value)
    {
        var parsed = BunnyVideoReferenceParser.TryParse(value, out var reference);

        Assert.True(parsed);
        Assert.NotNull(reference);
        Assert.Equal(740733L, reference.ExternalLibraryId);
        Assert.Equal(VideoGuid, reference.VideoGuid);
    }

    [Fact]
    public void ReferenceParser_AcceptsBareGuidAndCanonicalizesIt()
    {
        var parsed = BunnyVideoReferenceParser.TryParse(
            VideoGuid.ToUpperInvariant(),
            out var reference);

        Assert.True(parsed);
        Assert.NotNull(reference);
        Assert.Null(reference.ExternalLibraryId);
        Assert.Equal(VideoGuid, reference.VideoGuid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-bunny-reference")]
    [InlineData("http://player.mediadelivery.net/play/740733/432b90d4-8c28-40bb-bcab-285be673f122")]
    [InlineData("https://example.com/play/740733/432b90d4-8c28-40bb-bcab-285be673f122")]
    [InlineData("https://player.mediadelivery.net/play/not-a-library/432b90d4-8c28-40bb-bcab-285be673f122")]
    [InlineData("https://player.mediadelivery.net/play/740733/not-a-guid")]
    [InlineData("https://player.mediadelivery.net/play/740733/432b90d4-8c28-40bb-bcab-285be673f122/extra")]
    public void ReferenceParser_RejectsInvalidOrUntrustedReferences(string? value)
    {
        var parsed = BunnyVideoReferenceParser.TryParse(value, out var reference);

        Assert.False(parsed);
        Assert.Null(reference);
    }

    [Theory]
    [InlineData("  أولى  ", "999999", "BUNNY_LIBRARY_NAME_EXISTS")]
    [InlineData("ثانية", "740733", "BUNNY_LIBRARY_ID_EXISTS")]
    public async Task CreateLibrary_RejectsDuplicateNameOrExternalId(
        string name,
        string externalLibraryId,
        string expectedError)
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        db.BunnyStreamLibraries.Add(CreateLibrary("أولى", 740733));
        await db.SaveChangesAsync();

        var client = new FakeBunnyStreamClient(740733);
        var factory = new FakeBunnyStreamClientFactory(client);
        var handler = new CreateBunnyStreamLibraryCommandHandler(db, factory, new FakeSecretProtector());

        var result = await handler.Handle(
            new CreateBunnyStreamLibraryCommand(name, externalLibraryId, "new-secret", true, Guid.NewGuid()),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(expectedError, result.Errors!);
        Assert.Equal(1, await db.BunnyStreamLibraries.CountAsync());
    }

    [Fact]
    public async Task CreateAndListLibrary_NeverExposePlaintextKeyInDtoOrAudit()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        const string apiKey = "top-secret-bunny-api-key";
        var actorId = Guid.NewGuid();
        var client = new FakeBunnyStreamClient(740737, apiKey);
        var factory = new FakeBunnyStreamClientFactory(client);
        var protector = new FakeSecretProtector();
        var handler = new CreateBunnyStreamLibraryCommandHandler(db, factory, protector);

        var result = await handler.Handle(
            new CreateBunnyStreamLibraryCommand("  ثانية  ", "740737", apiKey, true, actorId),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("ثانية", result.Data.Name);
        Assert.True(result.Data.ApiKeyConfigured);

        var listResult = await new GetBunnyStreamLibrariesQueryHandler(db).Handle(
            new GetBunnyStreamLibrariesQuery(),
            CancellationToken.None);
        Assert.True(listResult.Success);
        Assert.Single(listResult.Data!);

        var availableResult = await new GetAvailableBunnyStreamLibrariesQueryHandler(db).Handle(
            new GetAvailableBunnyStreamLibrariesQuery(),
            CancellationToken.None);
        Assert.True(availableResult.Success);
        Assert.Single(availableResult.Data!);

        var dtoPayload = JsonSerializer.Serialize(new
        {
            Created = result.Data,
            Listed = listResult.Data,
            Available = availableResult.Data
        });
        Assert.DoesNotContain(apiKey, dtoPayload);
        var dtoPropertyNames = typeof(BunnyStreamLibraryDto)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();
        Assert.DoesNotContain("ApiKey", dtoPropertyNames);
        Assert.DoesNotContain("ApiKeyCiphertext", dtoPropertyNames);

        var audit = await db.AuditLogs.SingleAsync(log => log.Action == "BunnyStreamLibrary.Create");
        Assert.Equal(actorId, audit.PerformedByUserId);
        Assert.DoesNotContain(apiKey, audit.NewValues!);
        using var auditJson = JsonDocument.Parse(audit.NewValues!);
        var auditPropertyNames = auditJson.RootElement
            .EnumerateObject()
            .Select(property => property.Name)
            .ToArray();
        Assert.DoesNotContain("ApiKey", auditPropertyNames);
        Assert.DoesNotContain("ApiKeyCiphertext", auditPropertyNames);
        Assert.True(auditJson.RootElement.GetProperty("ApiKeyChanged").GetBoolean());

        var stored = await db.BunnyStreamLibraries.SingleAsync();
        Assert.NotNull(stored.ApiKeyCiphertext);
        Assert.Equal(FakeSecretProtector.Ciphertext, stored.ApiKeyCiphertext);
    }

    [Fact]
    public void AvailableLibrarySelector_2026_09_01_NpgsqlQueryTranslates()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=translation_only;Username=none;Password=none")
            .Options);

        var selectorSql = new GetAvailableBunnyStreamLibrariesQueryHandler(db).BuildQuery().ToQueryString();

        Assert.Contains("bunny_stream_libraries", selectorSql, StringComparison.Ordinal);
    }

    [Fact]
    public void LibraryManagement_2026_09_01_NpgsqlQueryTranslates()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=translation_only;Username=none;Password=none")
            .Options);

        var managementSql = new GetBunnyStreamLibrariesQueryHandler(db).BuildQuery().ToQueryString();

        Assert.Contains("bunny_stream_libraries", managementSql, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RejectedBunnyCredentials_DoNotPersistLibraryCiphertextOrAudit()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        const string rejectedApiKey = "rejected-secret";
        var client = new FakeBunnyStreamClient(740733, rejectedApiKey)
        {
            ValidationResult = new BunnyStreamValidationResult(
                false,
                "BUNNY_CREDENTIALS_REJECTED",
                "Bunny rejected these credentials.")
        };
        var handler = new CreateBunnyStreamLibraryCommandHandler(
            db,
            new FakeBunnyStreamClientFactory(client),
            new FakeSecretProtector());

        var result = await handler.Handle(
            new CreateBunnyStreamLibraryCommand(
                "مكتبة مرفوضة",
                "740733",
                rejectedApiKey,
                true,
                Guid.NewGuid()),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("BUNNY_CREDENTIALS_REJECTED", result.Errors!);
        Assert.Empty(await db.BunnyStreamLibraries.AsNoTracking().ToListAsync());
        Assert.Empty(await db.AuditLogs.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task UsedLibrary_CannotBeDeleted_ButCanBeDisabledWithoutUnlinkingVideos()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var library = CreateLibrary("أولى", 740733);
        var videoType = new VideoType
        {
            Name = "شرح",
            NormalizedName = "شرح",
            SortOrder = 1,
            IsActive = true
        };
        var lesson = new Lesson { Title = "Lesson", Summary = "Summary" };
        var video = new LessonVideo
        {
            Title = "Bunny video",
            Provider = VideoProviders.Bunny,
            ProviderVideoId = VideoGuid,
            LessonId = lesson.Id,
            VideoTypeId = videoType.Id,
            BunnyStreamLibraryId = library.Id,
            IsActive = true
        };
        db.AddRange(library, videoType, lesson, video);
        await db.SaveChangesAsync();

        var deleteResult = await new DeleteBunnyStreamLibraryCommandHandler(db).Handle(
            new DeleteBunnyStreamLibraryCommand(library.Id, Guid.NewGuid()),
            CancellationToken.None);

        Assert.False(deleteResult.Success);
        Assert.Contains("BUNNY_LIBRARY_IN_USE", deleteResult.Errors!);
        Assert.True(await db.BunnyStreamLibraries.AnyAsync(item => item.Id == library.Id));

        var access = new FakeLibraryAccessService(
            new BunnyStreamLibraryAccess(library.Id, library.Name, library.ExternalLibraryId, "api-key", true));
        var factory = new FakeBunnyStreamClientFactory(new FakeBunnyStreamClient(library.ExternalLibraryId));
        var statusResult = await new SetBunnyStreamLibraryStatusCommandHandler(db, access, factory).Handle(
            new SetBunnyStreamLibraryStatusCommand(library.Id, false, Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(statusResult.Success);
        Assert.False(statusResult.Data!.IsActive);
        var persistedVideo = await db.LessonVideos.AsNoTracking().SingleAsync(item => item.Id == video.Id);
        Assert.Equal(library.Id, persistedVideo.BunnyStreamLibraryId);
        Assert.True(persistedVideo.IsActive);
    }

    [Fact]
    public async Task DisabledLibrary_StillProducesPlaybackTokenScopedToItsOriginalLibrary()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var (admin, lesson, videoType) = await SeedUploadGraphAsync(db);
        var library = CreateLibrary("أولى", 740733);
        var video = new LessonVideo
        {
            Title = "Existing Bunny video",
            Provider = VideoProviders.Bunny,
            ProviderVideoId = VideoGuid,
            LessonId = lesson.Id,
            VideoTypeId = videoType.Id,
            BunnyStreamLibraryId = library.Id,
            IsActive = true
        };
        db.AddRange(library, video);
        await db.SaveChangesAsync();

        var access = new FakeLibraryAccessService(
            new BunnyStreamLibraryAccess(
                library.Id,
                library.Name,
                library.ExternalLibraryId,
                "api-key",
                true));
        var disabled = await new SetBunnyStreamLibraryStatusCommandHandler(
                db,
                access,
                new FakeBunnyStreamClientFactory(new FakeBunnyStreamClient(library.ExternalLibraryId)))
            .Handle(
                new SetBunnyStreamLibraryStatusCommand(library.Id, false, admin.Id),
                CancellationToken.None);

        Assert.True(disabled.Success);
        Assert.False(disabled.Data!.IsActive);

        var encryption = new VideoEncryptionService();
        var sessionResult = await new CreateVideoSessionCommandHandler(
                db,
                new AccessCheckService(db),
                encryption)
            .Handle(
                new CreateVideoSessionCommand(video.Id, admin.Id),
                CancellationToken.None);

        Assert.True(sessionResult.Success, sessionResult.Message);
        var session = await db.VideoPlaybackSessions.AsNoTracking().SingleAsync();
        var token = encryption.DecryptVideoInfo(session.SessionToken, session.EncryptionKey);
        Assert.Equal(VideoProviders.Bunny, token.ProviderName);
        Assert.Equal($"{library.ExternalLibraryId}/{VideoGuid}", token.ProviderVideoId);
    }

    [Fact]
    public async Task ManualFullUrl_RejectsSelectedLibraryMismatchBeforeCallingBunny()
    {
        var actualLibraryId = Guid.NewGuid();
        var selectedLibraryId = Guid.NewGuid();
        var access = new FakeLibraryAccessService(
            new BunnyStreamLibraryAccess(actualLibraryId, "أولى", 740733, "api-key", true));
        var factory = new FakeBunnyStreamClientFactory(new FakeBunnyStreamClient(740733));

        var result = await BunnyManualVideoResolver.ResolveAsync(
            $"https://player.mediadelivery.net/play/740733/{VideoGuid}",
            selectedLibraryId,
            access,
            factory,
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("BUNNY_LIBRARY_MISMATCH", result.ErrorCode);
    }

    [Fact]
    public async Task ManualBareGuid_RequiresExplicitLibrarySelection()
    {
        var access = new FakeLibraryAccessService(
            new BunnyStreamLibraryAccess(Guid.NewGuid(), "أولى", 740733, "api-key", true));
        var factory = new FakeBunnyStreamClientFactory(new FakeBunnyStreamClient(740733));

        var result = await BunnyManualVideoResolver.ResolveAsync(
            VideoGuid,
            selectedLibraryId: null,
            access,
            factory,
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("BUNNY_LIBRARY_REQUIRED", result.ErrorCode);
    }

    [Theory]
    [InlineData(0, "BUNNY_VIDEO_NOT_READY")]
    [InlineData(1, "BUNNY_VIDEO_NOT_READY")]
    [InlineData(2, "BUNNY_VIDEO_NOT_READY")]
    [InlineData(3, "BUNNY_VIDEO_NOT_READY")]
    [InlineData(5, "BUNNY_VIDEO_FAILED")]
    [InlineData(6, "BUNNY_VIDEO_FAILED")]
    [InlineData(7, "BUNNY_VIDEO_NOT_READY")]
    [InlineData(8, "BUNNY_VIDEO_NOT_READY")]
    public async Task ManualNonPlayableVideo_IsRejectedWithStatusSpecificError(
        int bunnyStatus,
        string expectedErrorCode)
    {
        var libraryId = Guid.NewGuid();
        var access = new FakeLibraryAccessService(
            new BunnyStreamLibraryAccess(libraryId, "أولى", 740733, "api-key", true));
        var client = new FakeBunnyStreamClient(740733) { VideoStatus = bunnyStatus };
        var factory = new FakeBunnyStreamClientFactory(client);

        var result = await BunnyManualVideoResolver.ResolveAsync(
            VideoGuid,
            libraryId,
            access,
            factory,
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(expectedErrorCode, result.ErrorCode);
    }

    [Theory]
    [InlineData(4)]
    public async Task ManualFinishedVideo_IsAccepted(int bunnyStatus)
    {
        var libraryId = Guid.NewGuid();
        var access = new FakeLibraryAccessService(
            new BunnyStreamLibraryAccess(libraryId, "أولى", 740733, "api-key", true));
        var client = new FakeBunnyStreamClient(740733) { VideoStatus = bunnyStatus };
        var factory = new FakeBunnyStreamClientFactory(client);

        var result = await BunnyManualVideoResolver.ResolveAsync(
            VideoGuid,
            libraryId,
            access,
            factory,
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(libraryId, result.LibraryId);
        Assert.Equal(VideoGuid, result.VideoGuid);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public async Task ProductionReferenceFrom20260901_WithFinishedStatus_IsAccepted()
    {
        const long externalLibraryId = 740737;
        var libraryId = Guid.NewGuid();
        var access = new FakeLibraryAccessService(
            new BunnyStreamLibraryAccess(libraryId, "ثانية", externalLibraryId, "api-key", true));
        var client = new FakeBunnyStreamClient(externalLibraryId) { VideoStatus = 4 };

        var result = await BunnyManualVideoResolver.ResolveAsync(
            "https://player.mediadelivery.net/play/740737/bf782c91-a093-4d59-8d20-daa579657041",
            libraryId,
            access,
            new FakeBunnyStreamClientFactory(client),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(libraryId, result.LibraryId);
        Assert.Equal("bf782c91-a093-4d59-8d20-daa579657041", result.VideoGuid);
    }

    [Fact]
    public async Task ManagedBunnyVideo_UpdateToYoutube_PreservesIdentityAndRetiresAssetHistory()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var seeded = await SeedManagedBunnyVideoAsync(db);
        var originalId = seeded.Video.Id;
        var originalInternalCode = seeded.Video.InternalCode;

        var result = await new UpdateVideoCommandHandler(
                db,
                Array.Empty<IVideoProvider>(),
                new TeacherAuthorizationService(db),
                new FakeLibraryAccessService(),
                new FakeBunnyStreamClientFactory())
            .Handle(
                new UpdateVideoCommand(
                    seeded.Video.Id,
                    "YouTube replacement",
                    VideoProviders.YouTube,
                    "new-youtube-id",
                    7,
                    0,
                    seeded.VideoType.Id,
                    IsActive: false),
                CancellationToken.None);

        Assert.True(result.Success, result.Message);
        db.ChangeTracker.Clear();

        var video = await db.LessonVideos.AsNoTracking().SingleAsync(item => item.Id == originalId);
        var retiredAsset = await db.BunnyVideoAssets
            .AsNoTracking()
            .Include(item => item.UsageSnapshots)
            .SingleAsync(item => item.Id == seeded.Asset.Id);

        Assert.Equal(originalId, video.Id);
        Assert.Equal(originalInternalCode, video.InternalCode);
        Assert.Equal(VideoProviders.YouTube, video.Provider);
        Assert.Equal("new-youtube-id", video.ProviderVideoId);
        Assert.Equal(7, video.Order);
        Assert.Equal(0, video.MaxWatchCount);
        Assert.False(video.IsActive);
        Assert.Null(video.BunnyStreamLibraryId);
        Assert.Equal(BunnyVideoAssetSourceState.Retired, retiredAsset.SourceState);
        Assert.NotNull(retiredAsset.RetiredAtUtc);
        Assert.Equal(seeded.Library.Id, retiredAsset.BunnyStreamLibraryRecordId);
        Assert.Single(retiredAsset.UsageSnapshots);
    }

    [Fact]
    public async Task TusReplacement_ReadyCandidateSwapsSameLessonVideoAndRetiresPriorAsset()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var seeded = await SeedManagedBunnyVideoAsync(db);
        var replacementType = new VideoType
        {
            Name = "مراجعة",
            NormalizedName = "مراجعة",
            SortOrder = 2,
            IsActive = true
        };
        seeded.Video.IsProcessingAI = true;
        seeded.Video.IsProcessingMindmaps = true;
        seeded.Video.CurrentAiAnalysisRunId = Guid.NewGuid();
        seeded.Video.CurrentMindmapGenerationRunId = Guid.NewGuid();
        seeded.Video.SubtitleUrl = "https://example.com/old.srt";
        db.AddRange(
            replacementType,
            new VideoChapter
            {
                LessonVideoId = seeded.Video.Id,
                Title = "Old chapter",
                StartTime = 0,
                EndTime = 20,
                SummaryText = "Old"
            },
            new VideoPlaybackSession
            {
                UserId = seeded.Admin.Id,
                LessonVideoId = seeded.Video.Id,
                SessionToken = "old-session",
                EncryptionKey = "key",
                ExpiresAt = DateTime.UtcNow.AddMinutes(5),
                IsSuperseded = false
            });
        await db.SaveChangesAsync();

        var libraries = new FakeLibraryAccessService(
            new BunnyStreamLibraryAccess(
                seeded.Library.Id,
                seeded.Library.Name,
                seeded.Library.ExternalLibraryId,
                "api-key",
                true));
        var client = new FakeBunnyStreamClient(seeded.Library.ExternalLibraryId);
        var createHandler = new CreateBunnyTusUploadCommandHandler(
            db,
            libraries,
            new FakeBunnyStreamClientFactory(client),
            new ConfigurationBuilder().AddInMemoryCollection().Build(),
            NullLogger<CreateBunnyTusUploadCommandHandler>.Instance);

        var created = await createHandler.Handle(
            new CreateBunnyTusUploadCommand(
                null,
                null,
                seeded.Lesson.Id,
                "Replacement upload",
                9,
                0,
                replacementType.Id,
                seeded.Library.Id,
                true,
                "replacement.mp4",
                2048,
                seeded.Admin.Id,
                seeded.Video.Id),
            CancellationToken.None);

        Assert.True(created.Success, created.Message);
        Assert.Equal(seeded.Video.Id, created.Data!.LessonVideoId);
        db.ChangeTracker.Clear();

        var beforeReady = await db.LessonVideos.AsNoTracking().SingleAsync(item => item.Id == seeded.Video.Id);
        var pending = await db.BunnyVideoAssets.AsNoTracking().SingleAsync(item => item.Id == created.Data.BunnyVideoAssetId);
        Assert.Equal("Managed Bunny", beforeReady.Title);
        Assert.Equal(VideoGuid, beforeReady.ProviderVideoId);
        Assert.True(beforeReady.IsActive);
        Assert.Equal(BunnyVideoAssetSourceState.PendingReplacement, pending.SourceState);
        Assert.Equal(seeded.Library.Id, pending.BunnyStreamLibraryRecordId);
        Assert.Equal(9, pending.TargetOrder);
        Assert.Equal(0, pending.TargetMaxWatchCount);
        Assert.Equal(replacementType.Id, pending.TargetVideoTypeId);
        Assert.True(pending.TargetIsActive);
        Assert.Equal(beforeReady.SourceRevision, pending.TargetSourceRevision);

        client.SetVideoStatus(created.Data.BunnyVideoGuid, 4);
        var refreshed = await new RefreshBunnyVideoStatusCommandHandler(
                db,
                libraries,
                new FakeBunnyStreamClientFactory(client))
            .Handle(new RefreshBunnyVideoStatusCommand(pending.Id, seeded.Admin.Id), CancellationToken.None);

        Assert.True(refreshed.Success, refreshed.Message);
        Assert.Equal("Ready", refreshed.Data!.Status);
        db.ChangeTracker.Clear();

        var video = await db.LessonVideos.AsNoTracking().SingleAsync(item => item.Id == seeded.Video.Id);
        var assets = await db.BunnyVideoAssets
            .AsNoTracking()
            .Include(item => item.UsageSnapshots)
            .Where(item => item.LessonVideoId == seeded.Video.Id)
            .ToDictionaryAsync(item => item.Id);
        var oldAsset = assets[seeded.Asset.Id];
        var currentAsset = assets[pending.Id];

        Assert.Equal(seeded.Video.Id, video.Id);
        Assert.Equal(seeded.Video.InternalCode, video.InternalCode);
        Assert.Equal(VideoProviders.Bunny, video.Provider);
        Assert.Equal(created.Data.BunnyVideoGuid, video.ProviderVideoId);
        Assert.Equal("Replacement upload", video.Title);
        Assert.Equal(9, video.Order);
        Assert.Equal(0, video.MaxWatchCount);
        Assert.Equal(replacementType.Id, video.VideoTypeId);
        Assert.Equal(seeded.Library.Id, video.BunnyStreamLibraryId);
        Assert.True(video.IsActive);
        Assert.False(video.IsProcessingAI);
        Assert.False(video.IsProcessingMindmaps);
        Assert.Null(video.CurrentAiAnalysisRunId);
        Assert.Null(video.CurrentMindmapGenerationRunId);
        Assert.Null(video.SubtitleUrl);
        Assert.Equal(BunnyVideoAssetSourceState.Retired, oldAsset.SourceState);
        Assert.Single(oldAsset.UsageSnapshots);
        Assert.Equal(BunnyVideoAssetSourceState.Current, currentAsset.SourceState);
        Assert.Equal("Ready", currentAsset.Status);
        Assert.Empty(await db.VideoChapters.AsNoTracking().Where(item => item.LessonVideoId == seeded.Video.Id).ToListAsync());
        Assert.True(await db.VideoPlaybackSessions.AsNoTracking()
            .Where(item => item.LessonVideoId == seeded.Video.Id)
            .AllAsync(item => item.IsSuperseded));
        Assert.Empty(client.DeletedVideoGuids);
    }

    [Fact]
    public async Task YoutubeTusReplacement_RetainsIdentityUntilReadyAndAllowsCurrentInactiveVideoType()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var (admin, lesson, videoType) = await SeedUploadGraphAsync(db);
        var library = CreateLibrary("مكتبة YouTube البديلة", 749998);
        var video = new LessonVideo
        {
            Title = "YouTube original",
            Provider = VideoProviders.YouTube,
            ProviderVideoId = "youtube-original-id",
            LessonId = lesson.Id,
            VideoTypeId = videoType.Id,
            Order = 4,
            MaxWatchCount = 6,
            IsActive = true
        };
        db.AddRange(library, video);
        await db.SaveChangesAsync();
        var originalId = video.Id;
        var originalInternalCode = video.InternalCode;

        // Existing content remains editable after its type is deactivated.
        videoType.IsActive = false;
        await db.SaveChangesAsync();

        var libraries = new FakeLibraryAccessService(
            new BunnyStreamLibraryAccess(
                library.Id,
                library.Name,
                library.ExternalLibraryId,
                "api-key",
                true));
        var client = new FakeBunnyStreamClient(library.ExternalLibraryId);
        var created = await new CreateBunnyTusUploadCommandHandler(
                db,
                libraries,
                new FakeBunnyStreamClientFactory(client),
                new ConfigurationBuilder().AddInMemoryCollection().Build(),
                NullLogger<CreateBunnyTusUploadCommandHandler>.Instance)
            .Handle(
                new CreateBunnyTusUploadCommand(
                    null,
                    null,
                    lesson.Id,
                    "Bunny replacement",
                    8,
                    0,
                    videoType.Id,
                    library.Id,
                    false,
                    "replacement.mp4",
                    2048,
                    admin.Id,
                    originalId),
                CancellationToken.None);

        Assert.True(created.Success, created.Message);
        Assert.Equal(originalId, created.Data!.LessonVideoId);
        db.ChangeTracker.Clear();

        var beforeReady = await db.LessonVideos.AsNoTracking().SingleAsync(item => item.Id == originalId);
        var candidate = await db.BunnyVideoAssets.AsNoTracking()
            .SingleAsync(item => item.Id == created.Data.BunnyVideoAssetId);
        Assert.Equal(originalId, beforeReady.Id);
        Assert.Equal(originalInternalCode, beforeReady.InternalCode);
        Assert.Equal(VideoProviders.YouTube, beforeReady.Provider);
        Assert.Equal("youtube-original-id", beforeReady.ProviderVideoId);
        Assert.Equal("YouTube original", beforeReady.Title);
        Assert.Equal(4, beforeReady.Order);
        Assert.Equal(6, beforeReady.MaxWatchCount);
        Assert.True(beforeReady.IsActive);
        Assert.Null(beforeReady.BunnyStreamLibraryId);
        Assert.Equal(BunnyVideoAssetSourceState.PendingReplacement, candidate.SourceState);

        client.SetVideoStatus(created.Data.BunnyVideoGuid, 4);
        var refreshed = await new RefreshBunnyVideoStatusCommandHandler(
                db,
                libraries,
                new FakeBunnyStreamClientFactory(client))
            .Handle(
                new RefreshBunnyVideoStatusCommand(candidate.Id, admin.Id),
                CancellationToken.None);

        Assert.True(refreshed.Success, refreshed.Message);
        Assert.Equal("Ready", refreshed.Data!.Status);
        db.ChangeTracker.Clear();

        var afterReady = await db.LessonVideos.AsNoTracking().SingleAsync(item => item.Id == originalId);
        var currentAsset = await db.BunnyVideoAssets.AsNoTracking().SingleAsync(item => item.Id == candidate.Id);
        Assert.Equal(originalId, afterReady.Id);
        Assert.Equal(originalInternalCode, afterReady.InternalCode);
        Assert.Equal(VideoProviders.Bunny, afterReady.Provider);
        Assert.Equal(created.Data.BunnyVideoGuid, afterReady.ProviderVideoId);
        Assert.Equal("Bunny replacement", afterReady.Title);
        Assert.Equal(8, afterReady.Order);
        Assert.Equal(0, afterReady.MaxWatchCount);
        Assert.Equal(videoType.Id, afterReady.VideoTypeId);
        Assert.Equal(library.Id, afterReady.BunnyStreamLibraryId);
        Assert.False(afterReady.IsActive);
        Assert.Equal(BunnyVideoAssetSourceState.Current, currentAsset.SourceState);
        Assert.Equal("Ready", currentAsset.Status);
    }

    [Fact]
    public async Task TusReplacement_ReadyCandidateDoesNotOverwriteNewerExternalSourceEdit()
    {
        // Regression: an external source edit can begin while no candidate exists,
        // then commit after the delayed Bunny candidate was persisted. The ready
        // candidate must be retired as superseded rather than take the source back.
        var connectionString = $"Data Source=bunny-source-revision-race-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        await using var keeper = new SqliteConnection(connectionString);
        await keeper.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connectionString)
            .Options;

        Guid adminId;
        Guid lessonId;
        Guid videoTypeId;
        Guid libraryId;
        Guid lessonVideoId;
        string internalCode;
        long libraryExternalId;
        await using (var setup = new AppDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
            var (admin, lesson, videoType) = await SeedUploadGraphAsync(setup);
            var library = CreateLibrary("مكتبة سباق المصدر", 749997);
            var video = new LessonVideo
            {
                Title = "YouTube original",
                Provider = VideoProviders.YouTube,
                ProviderVideoId = "youtube-original-id",
                LessonId = lesson.Id,
                VideoTypeId = videoType.Id,
                Order = 4,
                MaxWatchCount = 6,
                IsActive = true
            };
            setup.AddRange(library, video);
            await setup.SaveChangesAsync();

            adminId = admin.Id;
            lessonId = lesson.Id;
            videoTypeId = videoType.Id;
            libraryId = library.Id;
            lessonVideoId = video.Id;
            internalCode = video.InternalCode;
            libraryExternalId = library.ExternalLibraryId;
        }

        await using var sourceEditor = new AppDbContext(options);
        var staleEditorVideo = await sourceEditor.LessonVideos
            .Include(item => item.BunnyVideoAssets)
            .SingleAsync(item => item.Id == lessonVideoId);
        Assert.Empty(staleEditorVideo.BunnyVideoAssets);

        var libraries = new FakeLibraryAccessService(
            new BunnyStreamLibraryAccess(libraryId, "مكتبة سباق المصدر", libraryExternalId, "api-key", true));
        var client = new FakeBunnyStreamClient(libraryExternalId);
        BunnyTusUploadSessionDto session;
        await using (var candidateCreator = new AppDbContext(options))
        {
            var created = await new CreateBunnyTusUploadCommandHandler(
                    candidateCreator,
                    libraries,
                    new FakeBunnyStreamClientFactory(client),
                    new ConfigurationBuilder().AddInMemoryCollection().Build(),
                    NullLogger<CreateBunnyTusUploadCommandHandler>.Instance)
                .Handle(
                    new CreateBunnyTusUploadCommand(
                        null,
                        null,
                        lessonId,
                        "Delayed Bunny replacement",
                        8,
                        0,
                        videoTypeId,
                        libraryId,
                        true,
                        "replacement.mp4",
                        2048,
                        adminId,
                        lessonVideoId),
                    CancellationToken.None);

            Assert.True(created.Success, created.Message);
            session = created.Data!;
            var pending = await candidateCreator.BunnyVideoAssets.AsNoTracking()
                .SingleAsync(item => item.Id == session.BunnyVideoAssetId);
            Assert.Equal(0, pending.TargetSourceRevision);
        }

        // This context represents UpdateVideo having read no pending candidate before
        // the upload setup committed. It may therefore save its already-planned
        // source change after the candidate becomes pending.
        staleEditorVideo.Title = "VK edit won the race";
        staleEditorVideo.Provider = VideoProviders.Vk;
        staleEditorVideo.ProviderVideoId = "vk-new-id";
        staleEditorVideo.BunnyStreamLibraryId = null;
        checked
        {
            staleEditorVideo.SourceRevision++;
        }
        await sourceEditor.SaveChangesAsync();

        client.SetVideoStatus(session.BunnyVideoGuid, 4);
        await using (var refresher = new AppDbContext(options))
        {
            var refreshed = await new RefreshBunnyVideoStatusCommandHandler(
                    refresher,
                    libraries,
                    new FakeBunnyStreamClientFactory(client))
                .Handle(new RefreshBunnyVideoStatusCommand(session.BunnyVideoAssetId, adminId), CancellationToken.None);

            Assert.True(refreshed.Success, refreshed.Message);
            Assert.Equal("Failed", refreshed.Data!.Status);
        }

        await using var verifier = new AppDbContext(options);
        var videoAfterRace = await verifier.LessonVideos.AsNoTracking()
            .SingleAsync(item => item.Id == lessonVideoId);
        var candidateAfterRace = await verifier.BunnyVideoAssets.AsNoTracking()
            .SingleAsync(item => item.Id == session.BunnyVideoAssetId);

        Assert.Equal(lessonVideoId, videoAfterRace.Id);
        Assert.Equal(internalCode, videoAfterRace.InternalCode);
        Assert.Equal(VideoProviders.Vk, videoAfterRace.Provider);
        Assert.Equal("vk-new-id", videoAfterRace.ProviderVideoId);
        Assert.Equal("VK edit won the race", videoAfterRace.Title);
        Assert.Equal(1, videoAfterRace.SourceRevision);
        Assert.Null(videoAfterRace.BunnyStreamLibraryId);
        Assert.Equal(BunnyVideoAssetSourceState.Retired, candidateAfterRace.SourceState);
        Assert.Equal("Failed", candidateAfterRace.Status);
        Assert.NotNull(candidateAfterRace.OutcomeSupersededAtUtc);
        Assert.False(await verifier.BunnyVideoAssets.AnyAsync(item =>
            item.LessonVideoId == lessonVideoId
            && item.SourceState == BunnyVideoAssetSourceState.Current));
        Assert.Empty(client.DeletedVideoGuids);
    }

    [Fact]
    public async Task UrlFetchReplacement_FailedCandidateKeepsCurrentSourceAndRetiresCandidate()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var seeded = await SeedManagedBunnyVideoAsync(db);
        var originalVideoId = seeded.Video.Id;
        var originalInternalCode = seeded.Video.InternalCode;
        var libraries = new FakeLibraryAccessService(
            new BunnyStreamLibraryAccess(
                seeded.Library.Id,
                seeded.Library.Name,
                seeded.Library.ExternalLibraryId,
                "api-key",
                true));
        var client = new FakeBunnyStreamClient(seeded.Library.ExternalLibraryId) { VideoStatus = 5 };
        var factory = new FakeBunnyStreamClientFactory(client);

        var started = await new FetchBunnyVideoCommandHandler(
                db,
                libraries,
                factory,
                NullLogger<FetchBunnyVideoCommandHandler>.Instance)
            .Handle(
                new FetchBunnyVideoCommand(
                    null,
                    null,
                    seeded.Lesson.Id,
                    "Failed replacement",
                    3,
                    5,
                    seeded.VideoType.Id,
                    seeded.Library.Id,
                    false,
                    "https://example.com/replacement.mp4",
                    seeded.Admin.Id,
                    seeded.Video.Id),
                CancellationToken.None);

        Assert.True(started.Success, started.Message);
        Assert.Equal(originalVideoId, started.Data!.LessonVideoId);
        db.ChangeTracker.Clear();

        var refreshed = await new RefreshBunnyVideoStatusCommandHandler(db, libraries, factory)
            .Handle(new RefreshBunnyVideoStatusCommand(started.Data.AssetId, seeded.Admin.Id), CancellationToken.None);

        Assert.True(refreshed.Success, refreshed.Message);
        Assert.Equal("Failed", refreshed.Data!.Status);
        db.ChangeTracker.Clear();

        var video = await db.LessonVideos.AsNoTracking().SingleAsync(item => item.Id == originalVideoId);
        var assets = await db.BunnyVideoAssets.AsNoTracking()
            .Where(item => item.LessonVideoId == originalVideoId)
            .ToDictionaryAsync(item => item.Id);
        var currentAsset = assets[seeded.Asset.Id];
        var failedCandidate = assets[started.Data.AssetId];

        Assert.Equal(originalVideoId, video.Id);
        Assert.Equal(originalInternalCode, video.InternalCode);
        Assert.Equal("Managed Bunny", video.Title);
        Assert.Equal(VideoProviders.Bunny, video.Provider);
        Assert.Equal(VideoGuid, video.ProviderVideoId);
        Assert.Equal(1, video.Order);
        Assert.Equal(3, video.MaxWatchCount);
        Assert.True(video.IsActive);
        Assert.Equal(BunnyVideoAssetSourceState.Current, currentAsset.SourceState);
        Assert.Equal("Ready", currentAsset.Status);
        Assert.Equal(BunnyVideoAssetSourceState.Retired, failedCandidate.SourceState);
        Assert.Equal("Failed", failedCandidate.Status);
        Assert.NotNull(failedCandidate.RetiredAtUtc);
        Assert.Empty(client.DeletedVideoGuids);
    }

    [Fact]
    public async Task PendingReplacement_CancelRetiresOnlyCandidateAndKeepsCurrentSource()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var seeded = await SeedManagedBunnyVideoAsync(db);
        var candidate = await AddPendingReplacementAsync(db, seeded);

        var result = await new CancelBunnyVideoReplacementCommandHandler(db)
            .Handle(
                new CancelBunnyVideoReplacementCommand(candidate.Id, seeded.Admin.Id),
                CancellationToken.None);

        Assert.True(result.Success, result.Message);
        Assert.Equal("Cancelled", result.Data!.Status);
        db.ChangeTracker.Clear();

        var video = await db.LessonVideos.AsNoTracking().SingleAsync(item => item.Id == seeded.Video.Id);
        var persistedCandidate = await db.BunnyVideoAssets.AsNoTracking().SingleAsync(item => item.Id == candidate.Id);
        var currentAsset = await db.BunnyVideoAssets.AsNoTracking().SingleAsync(item => item.Id == seeded.Asset.Id);

        Assert.Equal(VideoGuid, video.ProviderVideoId);
        Assert.True(video.IsActive);
        Assert.Equal(BunnyVideoAssetSourceState.Current, currentAsset.SourceState);
        Assert.Equal(BunnyVideoAssetSourceState.Retired, persistedCandidate.SourceState);
        Assert.Equal(seeded.Admin.Id, persistedCandidate.RetiredByUserId);
        Assert.NotNull(persistedCandidate.RetiredAtUtc);
    }

    [Theory]
    [InlineData("Ready")]
    [InlineData("Processing")]
    public async Task Refresh_CannotResurrectCandidateCancelledByAnotherRequest(string refreshedStatus)
    {
        var connectionString = $"Data Source=bunny-replacement-race-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        await using var keeper = new SqliteConnection(connectionString);
        await keeper.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connectionString)
            .Options;

        Guid adminId;
        Guid lessonVideoId;
        Guid originalAssetId;
        Guid candidateId;
        await using (var setup = new AppDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
            var seeded = await SeedManagedBunnyVideoAsync(setup);
            var candidate = await AddPendingReplacementAsync(setup, seeded);
            adminId = seeded.Admin.Id;
            lessonVideoId = seeded.Video.Id;
            originalAssetId = seeded.Asset.Id;
            candidateId = candidate.Id;
        }

        await using var refresher = new AppDbContext(options);
        var staleReadyCandidate = await refresher.BunnyVideoAssets
            .Include(item => item.LessonVideo)
            .SingleAsync(item => item.Id == candidateId);
        // This is the local state after a Bunny status request, but before that
        // refresher has persisted or promoted the candidate.
        staleReadyCandidate.Status = refreshedStatus;
        staleReadyCandidate.LastStatusSyncedAtUtc = DateTime.UtcNow;

        await using (var canceller = new AppDbContext(options))
        {
            var cancelled = await new CancelBunnyVideoReplacementCommandHandler(canceller)
                .Handle(new CancelBunnyVideoReplacementCommand(candidateId, adminId), CancellationToken.None);
            Assert.True(cancelled.Success, cancelled.Message);
        }

        var applied = await BunnyVideoReplacementLifecycle.FinalizeIfNeededAsync(
            refresher,
            staleReadyCandidate,
            CancellationToken.None);
        Assert.False(applied);
        var persistedRefresh = await BunnyVideoReplacementLifecycle.TrySaveAssetStateAsync(
            refresher,
            staleReadyCandidate,
            CancellationToken.None);
        Assert.Equal(refreshedStatus == "Ready", persistedRefresh);

        await using var verifier = new AppDbContext(options);
        var persistedCandidate = await verifier.BunnyVideoAssets.AsNoTracking()
            .SingleAsync(item => item.Id == candidateId);
        var originalAsset = await verifier.BunnyVideoAssets.AsNoTracking()
            .SingleAsync(item => item.Id == originalAssetId);
        var video = await verifier.LessonVideos.AsNoTracking()
            .SingleAsync(item => item.Id == lessonVideoId);

        Assert.Equal(BunnyVideoAssetSourceState.Retired, persistedCandidate.SourceState);
        Assert.Equal("Cancelled", persistedCandidate.Status);
        Assert.Equal(BunnyVideoAssetSourceState.Current, originalAsset.SourceState);
        Assert.Equal(VideoGuid, video.ProviderVideoId);
        Assert.True(video.IsActive);
    }

    [Fact]
    public async Task Cockpit_HidesFailedReplacementOutcomeAfterALaterCandidateBecomesCurrent()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var seeded = await SeedManagedBunnyVideoAsync(db);
        var failure = await AddPendingReplacementAsync(db, seeded);
        var failedAt = DateTime.UtcNow.AddMinutes(-2);
        failure.Status = "Failed";
        failure.SourceState = BunnyVideoAssetSourceState.Retired;
        failure.RetiredAtUtc = failedAt;

        // This models a later successful candidate. The one-current/one-pending
        // constraint means its creation is necessarily after the failed candidate.
        seeded.Asset.SourceState = BunnyVideoAssetSourceState.Retired;
        seeded.Asset.RetiredAtUtc = failedAt.AddMinutes(1);
        var successfulReplacement = await AddPendingReplacementAsync(db, seeded);
        successfulReplacement.Status = "Ready";
        successfulReplacement.SourceState = BunnyVideoAssetSourceState.Current;
        successfulReplacement.CreatedAt = failedAt.AddMinutes(2);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var cockpit = await new GetLessonCockpitQueryHandler(
                db,
                new TeacherAuthorizationService(db))
            .Handle(new GetLessonCockpitQuery(seeded.Lesson.Id), CancellationToken.None);

        Assert.True(cockpit.Success, cockpit.Message);
        Assert.Null(Assert.Single(cockpit.Data!.Videos).LastBunnyReplacementOutcome);
    }

    [Theory]
    [InlineData("Failed")]
    [InlineData("Unknown")]
    public async Task Cockpit_SupersedesTerminalReplacementOutcomeAfterExternalSourceChange(string terminalStatus)
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var (admin, lesson, videoType) = await SeedUploadGraphAsync(db);
        var ownership = await db.Lessons
            .Where(item => item.Id == lesson.Id)
            .Select(item => new
            {
                PackageId = item.ContentSection.Term.PackageId,
                TeacherId = item.ContentSection.Term.Package.TeacherId
            })
            .SingleAsync();
        var library = CreateLibrary("مكتبة الاستبدال", 749999);
        var video = new LessonVideo
        {
            Title = "YouTube original",
            Provider = VideoProviders.YouTube,
            ProviderVideoId = "youtube-original-id",
            LessonId = lesson.Id,
            VideoTypeId = videoType.Id,
            Order = 1,
            MaxWatchCount = 3,
            IsActive = true
        };
        var failedCandidate = new BunnyVideoAsset
        {
            LessonVideo = video,
            TeacherId = ownership.TeacherId,
            PackageId = ownership.PackageId,
            LessonId = lesson.Id,
            UploadedByUserId = admin.Id,
            BunnyLibraryId = library.ExternalLibraryId,
            BunnyStreamLibraryRecordId = library.Id,
            BunnyVideoGuid = Guid.NewGuid().ToString("D"),
            Title = "Unusable Bunny candidate",
            UploadMethod = "TusFile",
            Status = terminalStatus,
            ErrorMessage = "Bunny processing did not complete.",
            SourceState = BunnyVideoAssetSourceState.Retired,
            RetiredAtUtc = DateTime.UtcNow
        };
        db.AddRange(library, video, failedCandidate);
        await db.SaveChangesAsync();

        var cockpitHandler = new GetLessonCockpitQueryHandler(
            db,
            new TeacherAuthorizationService(db));
        var beforeSourceChange = await cockpitHandler.Handle(
            new GetLessonCockpitQuery(lesson.Id),
            CancellationToken.None);
        Assert.Equal(terminalStatus, Assert.Single(beforeSourceChange.Data!.Videos).LastBunnyReplacementOutcome?.Status);

        var updated = await new UpdateVideoCommandHandler(
                db,
                Array.Empty<IVideoProvider>(),
                new TeacherAuthorizationService(db),
                new FakeLibraryAccessService(),
                new FakeBunnyStreamClientFactory())
            .Handle(
                new UpdateVideoCommand(
                    video.Id,
                    "VK replacement",
                    VideoProviders.Vk,
                    "vk-new-id",
                    2,
                    0,
                    videoType.Id,
                    admin.Id,
                    IsActive: true),
                CancellationToken.None);

        Assert.True(updated.Success, updated.Message);
        db.ChangeTracker.Clear();

        var persistedCandidate = await db.BunnyVideoAssets.AsNoTracking()
            .SingleAsync(item => item.Id == failedCandidate.Id);
        var afterSourceChange = await new GetLessonCockpitQueryHandler(
                db,
                new TeacherAuthorizationService(db))
            .Handle(new GetLessonCockpitQuery(lesson.Id), CancellationToken.None);
        var cockpitVideo = Assert.Single(afterSourceChange.Data!.Videos);

        Assert.Equal(VideoProviders.Vk, cockpitVideo.Provider);
        Assert.Equal("vk-new-id", cockpitVideo.Url);
        Assert.NotNull(persistedCandidate.OutcomeSupersededAtUtc);
        Assert.Null(cockpitVideo.LastBunnyReplacementOutcome);
    }

    [Theory]
    [InlineData("Failed")]
    [InlineData("Unknown")]
    public async Task ExternalSourceChange_SuppressesTerminalCurrentBunnyOutcome(string terminalStatus)
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var seeded = await SeedManagedBunnyVideoAsync(db);
        seeded.Asset.Status = terminalStatus;
        seeded.Asset.ErrorMessage = "The managed Bunny source is no longer usable.";
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var updated = await new UpdateVideoCommandHandler(
                db,
                Array.Empty<IVideoProvider>(),
                new TeacherAuthorizationService(db),
                new FakeLibraryAccessService(),
                new FakeBunnyStreamClientFactory())
            .Handle(
                new UpdateVideoCommand(
                    seeded.Video.Id,
                    "VK replacement",
                    VideoProviders.Vk,
                    "vk-new-id",
                    2,
                    0,
                    seeded.VideoType.Id,
                    seeded.Admin.Id,
                    IsActive: true),
                CancellationToken.None);

        Assert.True(updated.Success, updated.Message);
        db.ChangeTracker.Clear();

        var retiredAsset = await db.BunnyVideoAssets.AsNoTracking()
            .SingleAsync(item => item.Id == seeded.Asset.Id);
        var cockpit = await new GetLessonCockpitQueryHandler(
                db,
                new TeacherAuthorizationService(db))
            .Handle(new GetLessonCockpitQuery(seeded.Lesson.Id), CancellationToken.None);

        Assert.True(cockpit.Success, cockpit.Message);
        Assert.Equal(BunnyVideoAssetSourceState.Retired, retiredAsset.SourceState);
        Assert.NotNull(retiredAsset.OutcomeSupersededAtUtc);
        Assert.Null(Assert.Single(cockpit.Data!.Videos).LastBunnyReplacementOutcome);
    }

    [Fact]
    public async Task AbandonedPendingReplacement_ExpiresBeforeMetadataEditAndNoLongerBlocksVideo()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var seeded = await SeedManagedBunnyVideoAsync(db);
        var candidate = await AddPendingReplacementAsync(db, seeded);
        candidate.CreatedAt = DateTime.UtcNow - BunnyVideoReplacementLifecycle.PendingReplacementExpiry - TimeSpan.FromMinutes(1);
        await db.SaveChangesAsync();

        var result = await new UpdateVideoCommandHandler(
                db,
                Array.Empty<IVideoProvider>(),
                new TeacherAuthorizationService(db),
                new FakeLibraryAccessService(),
                new FakeBunnyStreamClientFactory())
            .Handle(
                new UpdateVideoCommand(
                    seeded.Video.Id,
                    "Renamed after expiry",
                    VideoProviders.Bunny,
                    VideoGuid,
                    2,
                    3,
                    seeded.VideoType.Id,
                    BunnyStreamLibraryId: seeded.Library.Id,
                    IsActive: true),
                CancellationToken.None);

        Assert.True(result.Success, result.Message);
        db.ChangeTracker.Clear();

        var video = await db.LessonVideos.AsNoTracking().SingleAsync(item => item.Id == seeded.Video.Id);
        var expiredCandidate = await db.BunnyVideoAssets.AsNoTracking().SingleAsync(item => item.Id == candidate.Id);

        Assert.Equal("Renamed after expiry", video.Title);
        Assert.Equal(VideoGuid, video.ProviderVideoId);
        Assert.True(video.IsActive);
        Assert.Equal(BunnyVideoAssetSourceState.Retired, expiredCandidate.SourceState);
        Assert.Equal("Expired", expiredCandidate.Status);
        Assert.NotNull(expiredCandidate.RetiredAtUtc);
    }

    [Fact]
    public async Task TusUpload_PersistsSelectedLibraryAndActivatesOnlyAfterBunnyIsReady()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var (admin, lesson, videoType) = await SeedUploadGraphAsync(db);
        var library = CreateLibrary("مسار", 740801);
        db.BunnyStreamLibraries.Add(library);
        await db.SaveChangesAsync();

        var access = new FakeLibraryAccessService(
            new BunnyStreamLibraryAccess(library.Id, library.Name, library.ExternalLibraryId, "api-key", true));
        var client = new FakeBunnyStreamClient(library.ExternalLibraryId);
        var factory = new FakeBunnyStreamClientFactory(client);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BunnyStream:TusUploadExpiryMinutes"] = "30"
            })
            .Build();
        var createHandler = new CreateBunnyTusUploadCommandHandler(
            db,
            access,
            factory,
            configuration,
            NullLogger<CreateBunnyTusUploadCommandHandler>.Instance);

        var created = await createHandler.Handle(
            new CreateBunnyTusUploadCommand(
                null,
                null,
                lesson.Id,
                "Upload",
                1,
                3,
                videoType.Id,
                library.Id,
                true,
                "video.mp4",
                1024,
                admin.Id),
            CancellationToken.None);

        Assert.True(created.Success);
        Assert.Equal(library.ExternalLibraryId, created.Data!.LibraryId);
        var uploadedVideo = await db.LessonVideos.AsNoTracking()
            .SingleAsync(item => item.Id == created.Data.LessonVideoId);
        Assert.Equal(library.Id, uploadedVideo.BunnyStreamLibraryId);
        Assert.False(uploadedVideo.IsActive);
        var uploadedAsset = await db.BunnyVideoAssets.AsNoTracking()
            .SingleAsync(item => item.Id == created.Data.BunnyVideoAssetId);
        Assert.Equal(library.ExternalLibraryId, uploadedAsset.BunnyLibraryId);
        Assert.True(uploadedAsset.ActivateWhenReady);

        var refreshHandler = new RefreshBunnyVideoStatusCommandHandler(db, access, factory);
        client.VideoStatus = 2;
        var processing = await refreshHandler.Handle(
            new RefreshBunnyVideoStatusCommand(uploadedAsset.Id, admin.Id),
            CancellationToken.None);
        Assert.True(processing.Success);
        Assert.Equal("Processing", processing.Data!.Status);
        Assert.False((await db.LessonVideos.AsNoTracking().SingleAsync(item => item.Id == uploadedVideo.Id)).IsActive);

        db.ChangeTracker.Clear();
        client.VideoStatus = 3;
        var transcoding = await refreshHandler.Handle(
            new RefreshBunnyVideoStatusCommand(uploadedAsset.Id, admin.Id),
            CancellationToken.None);
        Assert.True(transcoding.Success);
        Assert.Equal("Processing", transcoding.Data!.Status);
        Assert.False((await db.LessonVideos.AsNoTracking().SingleAsync(item => item.Id == uploadedVideo.Id)).IsActive);

        db.ChangeTracker.Clear();
        client.VideoStatus = 4;
        var ready = await refreshHandler.Handle(
            new RefreshBunnyVideoStatusCommand(uploadedAsset.Id, admin.Id),
            CancellationToken.None);
        Assert.True(ready.Success);
        Assert.Equal("Ready", ready.Data!.Status);
        Assert.True((await db.LessonVideos.AsNoTracking().SingleAsync(item => item.Id == uploadedVideo.Id)).IsActive);
    }

    [Theory]
    [InlineData(0, "Processing", false)]
    [InlineData(1, "Processing", false)]
    [InlineData(2, "Processing", false)]
    [InlineData(3, "Processing", false)]
    [InlineData(4, "Ready", true)]
    [InlineData(5, "Failed", false)]
    [InlineData(6, "Failed", false)]
    [InlineData(7, "Processing", false)]
    [InlineData(8, "Processing", false)]
    [InlineData(9, "Unknown", false)]
    [InlineData(10, "Unknown", false)]
    public async Task UploadRefresh_MapsOfficialBunnyStatusToObservableAssetState(
        int bunnyStatus,
        string expectedStatus,
        bool expectedActive)
    {
        var lessonVideo = new LessonVideo { IsActive = false };
        var asset = new BunnyVideoAsset
        {
            BunnyVideoGuid = VideoGuid,
            ActivateWhenReady = true,
            LessonVideo = lessonVideo
        };
        var client = new FakeBunnyStreamClient(740733) { VideoStatus = bunnyStatus };

        await BunnyUploadStatusUpdater.RefreshAsync(client, asset, CancellationToken.None);

        Assert.Equal(expectedStatus, asset.Status);
        Assert.Equal(expectedActive, lessonVideo.IsActive);
    }

    [Fact]
    public async Task UrlFetch_CreatesOneAssetWithTheSelectedLibraryAndFetchesIntoTheCreatedGuid()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var (admin, lesson, videoType) = await SeedUploadGraphAsync(db);
        var selectedLibrary = CreateLibrary("مسار", 740801);
        var otherLibrary = CreateLibrary("أولى", 740733);
        db.BunnyStreamLibraries.AddRange(selectedLibrary, otherLibrary);
        await db.SaveChangesAsync();

        var libraries = new FakeLibraryAccessService(
            new BunnyStreamLibraryAccess(
                selectedLibrary.Id,
                selectedLibrary.Name,
                selectedLibrary.ExternalLibraryId,
                "selected-key",
                true),
            new BunnyStreamLibraryAccess(
                otherLibrary.Id,
                otherLibrary.Name,
                otherLibrary.ExternalLibraryId,
                "other-key",
                true));
        var selectedClient = new FakeBunnyStreamClient(
            selectedLibrary.ExternalLibraryId,
            "selected-key")
        {
            RequireFetchBeforeStatusLookup = true,
            VideoStatus = 4
        };
        var factory = new FakeBunnyStreamClientFactory(
            new FakeBunnyStreamClient(otherLibrary.ExternalLibraryId, "other-key"),
            selectedClient);
        var handler = new FetchBunnyVideoCommandHandler(
            db,
            libraries,
            factory,
            NullLogger<FetchBunnyVideoCommandHandler>.Instance);

        var fetched = await handler.Handle(
            new FetchBunnyVideoCommand(
                null,
                null,
                lesson.Id,
                "Fetched video",
                1,
                3,
                videoType.Id,
                selectedLibrary.Id,
                true,
                "https://example.com/source.mp4",
                admin.Id),
            CancellationToken.None);

        Assert.True(fetched.Success, fetched.Message);
        var video = await db.LessonVideos.AsNoTracking().SingleAsync();
        var asset = await db.BunnyVideoAssets.AsNoTracking().SingleAsync();
        Assert.Equal(selectedLibrary.Id, video.BunnyStreamLibraryId);
        Assert.Equal(selectedLibrary.ExternalLibraryId, asset.BunnyLibraryId);
        Assert.Equal(video.ProviderVideoId, asset.BunnyVideoGuid);
        Assert.Equal(fetched.Data!.LessonVideoId, video.Id);
        Assert.Equal(fetched.Data.AssetId, asset.Id);

        var refreshed = await new RefreshBunnyVideoStatusCommandHandler(db, libraries, factory)
            .Handle(
                new RefreshBunnyVideoStatusCommand(asset.Id, admin.Id),
                CancellationToken.None);

        Assert.True(refreshed.Success, refreshed.Message);
        Assert.Equal("Ready", refreshed.Data!.Status);
    }

    [Fact]
    public async Task UrlFetch_WhenBunnyRejectsFetch_DeletesTheRemotePlaceholder()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var (admin, lesson, videoType) = await SeedUploadGraphAsync(db);
        var library = CreateLibrary("مسار", 740801);
        db.BunnyStreamLibraries.Add(library);
        await db.SaveChangesAsync();

        var libraries = new FakeLibraryAccessService(
            new BunnyStreamLibraryAccess(library.Id, library.Name, library.ExternalLibraryId, "api-key", true));
        var client = new FakeBunnyStreamClient(library.ExternalLibraryId)
        {
            FetchResult = new BunnyFetchVideoResultDto(false, "Rejected", 422)
        };
        var handler = new FetchBunnyVideoCommandHandler(
            db,
            libraries,
            new FakeBunnyStreamClientFactory(client),
            NullLogger<FetchBunnyVideoCommandHandler>.Instance);

        var result = await handler.Handle(
            new FetchBunnyVideoCommand(
                null,
                null,
                lesson.Id,
                "Rejected fetch",
                1,
                3,
                videoType.Id,
                library.Id,
                true,
                "https://example.com/rejected.mp4",
                admin.Id),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Single(client.DeletedVideoGuids);
        Assert.Empty(await db.LessonVideos.AsNoTracking().ToListAsync());
        Assert.Empty(await db.BunnyVideoAssets.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task TusUpload_WhenPersistenceFails_DeletesTheRemoteVideoAndRethrows()
    {
        await using AppDbContext db = CreateDbThatFailsOnSave(saveCall: 3);
        var (admin, lesson, videoType) = await SeedUploadGraphAsync(db);
        var library = CreateLibrary("مسار", 740801);
        db.BunnyStreamLibraries.Add(library);
        await db.SaveChangesAsync();

        var libraries = new FakeLibraryAccessService(
            new BunnyStreamLibraryAccess(library.Id, library.Name, library.ExternalLibraryId, "api-key", true));
        var client = new FakeBunnyStreamClient(library.ExternalLibraryId);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var handler = new CreateBunnyTusUploadCommandHandler(
            db,
            libraries,
            new FakeBunnyStreamClientFactory(client),
            configuration,
            NullLogger<CreateBunnyTusUploadCommandHandler>.Instance);

        await Assert.ThrowsAsync<SimulatedPersistenceException>(() => handler.Handle(
            new CreateBunnyTusUploadCommand(
                null,
                null,
                lesson.Id,
                "Persistence failure",
                1,
                3,
                videoType.Id,
                library.Id,
                true,
                "video.mp4",
                1024,
                admin.Id),
            CancellationToken.None));

        Assert.Single(client.DeletedVideoGuids);
    }

    [Fact]
    public async Task UrlFetch_WhenPersistenceFails_DeletesTheRemoteVideoAndRethrows()
    {
        await using AppDbContext db = CreateDbThatFailsOnSave(saveCall: 3);
        var (admin, lesson, videoType) = await SeedUploadGraphAsync(db);
        var library = CreateLibrary("مسار", 740801);
        db.BunnyStreamLibraries.Add(library);
        await db.SaveChangesAsync();

        var libraries = new FakeLibraryAccessService(
            new BunnyStreamLibraryAccess(library.Id, library.Name, library.ExternalLibraryId, "api-key", true));
        var client = new FakeBunnyStreamClient(library.ExternalLibraryId);
        var handler = new FetchBunnyVideoCommandHandler(
            db,
            libraries,
            new FakeBunnyStreamClientFactory(client),
            NullLogger<FetchBunnyVideoCommandHandler>.Instance);

        await Assert.ThrowsAsync<SimulatedPersistenceException>(() => handler.Handle(
            new FetchBunnyVideoCommand(
                null,
                null,
                lesson.Id,
                "Persistence failure",
                1,
                3,
                videoType.Id,
                library.Id,
                true,
                "https://example.com/source.mp4",
                admin.Id),
            CancellationToken.None));

        Assert.Single(client.DeletedVideoGuids);
    }

    [Fact]
    public async Task PendingRefresh_RetriesRecentMissingVideo_ButStopsAfterTheGraceWindow()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();

        var (admin, lesson, videoType) = await SeedUploadGraphAsync(db);
        var ownership = await db.Lessons
            .Where(item => item.Id == lesson.Id)
            .Select(item => new
            {
                PackageId = item.ContentSection.Term.PackageId,
                TeacherId = item.ContentSection.Term.Package.TeacherId
            })
            .SingleAsync();
        var library = CreateLibrary("مكتبة retry", 749998);
        var recentVideo = CreatePendingVideo(lesson.Id, videoType.Id, library.Id, "Recent missing video");
        var expiredVideo = CreatePendingVideo(lesson.Id, videoType.Id, library.Id, "Expired missing video");
        var recentAsset = CreatePendingAsset(
            recentVideo,
            ownership.TeacherId,
            ownership.PackageId,
            admin.Id,
            library.ExternalLibraryId,
            "Processing",
            activateWhenReady: true);
        var expiredAsset = CreatePendingAsset(
            expiredVideo,
            ownership.TeacherId,
            ownership.PackageId,
            admin.Id,
            library.ExternalLibraryId,
            "Processing",
            activateWhenReady: true);
        expiredAsset.CreatedAt = DateTime.UtcNow - BunnyUploadStatusUpdater.MissingVideoRetryWindow - TimeSpan.FromMinutes(1);
        db.AddRange(library, recentVideo, expiredVideo, recentAsset, expiredAsset);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var access = new FakeLibraryAccessService(
            new BunnyStreamLibraryAccess(library.Id, library.Name, library.ExternalLibraryId, "api-key", true));
        var client = new FakeBunnyStreamClient(library.ExternalLibraryId);
        client.ReturnMissingVideo(recentAsset.BunnyVideoGuid);
        client.ReturnMissingVideo(expiredAsset.BunnyVideoGuid);
        var handler = new RefreshPendingBunnyVideosCommandHandler(
            db,
            access,
            new FakeBunnyStreamClientFactory(client));

        var missingResult = await handler.Handle(new RefreshPendingBunnyVideosCommand(10), CancellationToken.None);

        Assert.Equal(2, missingResult.Attempted);
        Assert.Equal(2, missingResult.Refreshed);
        db.ChangeTracker.Clear();
        var missingAssets = await db.BunnyVideoAssets
            .AsNoTracking()
            .Where(item => item.Id == recentAsset.Id || item.Id == expiredAsset.Id)
            .ToDictionaryAsync(item => item.Id);
        Assert.Equal("Processing", missingAssets[recentAsset.Id].Status);
        Assert.Equal("Unknown", missingAssets[expiredAsset.Id].Status);

        client.ReturnVideo(recentAsset.BunnyVideoGuid);
        client.SetVideoStatus(recentAsset.BunnyVideoGuid, 4);
        var retryAsset = await db.BunnyVideoAssets.SingleAsync(item => item.Id == recentAsset.Id);
        retryAsset.LastStatusSyncedAtUtc = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var retryResult = await handler.Handle(new RefreshPendingBunnyVideosCommand(10), CancellationToken.None);

        Assert.Equal(1, retryResult.Attempted);
        Assert.Equal(1, retryResult.Refreshed);
        var finalAssets = await db.BunnyVideoAssets
            .AsNoTracking()
            .Where(item => item.Id == recentAsset.Id || item.Id == expiredAsset.Id)
            .ToDictionaryAsync(item => item.Id);
        Assert.Equal("Ready", finalAssets[recentAsset.Id].Status);
        Assert.Equal("Unknown", finalAssets[expiredAsset.Id].Status);
    }

    [Fact]
    public async Task PendingRefresh_ClaimsPendingAssets_UsesInactiveLibrary_AndActivatesOnlyOptedInVideos()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();

        var (admin, lesson, videoType) = await SeedUploadGraphAsync(db);
        var ownership = await db.Lessons
            .Where(item => item.Id == lesson.Id)
            .Select(item => new
            {
                PackageId = item.ContentSection.Term.PackageId,
                TeacherId = item.ContentSection.Term.Package.TeacherId
            })
            .SingleAsync();
        var library = CreateLibrary("مكتبة معطلة", 749999);
        library.IsActive = false;

        var activateVideo = CreatePendingVideo(lesson.Id, videoType.Id, library.Id, "Activate when ready");
        var remainInactiveVideo = CreatePendingVideo(lesson.Id, videoType.Id, library.Id, "Remain inactive");
        var failedVideo = CreatePendingVideo(lesson.Id, videoType.Id, library.Id, "Claimed failure");
        var activateAsset = CreatePendingAsset(
            activateVideo,
            ownership.TeacherId,
            ownership.PackageId,
            admin.Id,
            library.ExternalLibraryId,
            "Created",
            activateWhenReady: true);
        var remainInactiveAsset = CreatePendingAsset(
            remainInactiveVideo,
            ownership.TeacherId,
            ownership.PackageId,
            admin.Id,
            library.ExternalLibraryId,
            "Uploaded",
            activateWhenReady: false);
        var failedAsset = CreatePendingAsset(
            failedVideo,
            ownership.TeacherId,
            ownership.PackageId,
            admin.Id,
            library.ExternalLibraryId,
            "Processing",
            activateWhenReady: true);
        db.AddRange(
            library,
            activateVideo,
            remainInactiveVideo,
            failedVideo,
            activateAsset,
            remainInactiveAsset,
            failedAsset);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var access = new FakeLibraryAccessService(
            new BunnyStreamLibraryAccess(library.Id, library.Name, library.ExternalLibraryId, "api-key", false));
        var client = new FakeBunnyStreamClient(library.ExternalLibraryId);
        client.SetVideoStatus(activateAsset.BunnyVideoGuid, 4);
        client.SetVideoStatus(remainInactiveAsset.BunnyVideoGuid, 4);
        client.FailStatusLookup(failedAsset.BunnyVideoGuid);
        var factory = new FakeBunnyStreamClientFactory(client);
        var handler = new RefreshPendingBunnyVideosCommandHandler(db, access, factory);

        var result = await handler.Handle(new RefreshPendingBunnyVideosCommand(10), CancellationToken.None);

        Assert.Equal(3, result.Attempted);
        Assert.Equal(2, result.Refreshed);
        Assert.Equal(1, result.Failed);

        db.ChangeTracker.Clear();
        var persistedAssets = await db.BunnyVideoAssets
            .AsNoTracking()
            .Include(item => item.LessonVideo)
            .Where(item => item.Id == activateAsset.Id
                || item.Id == remainInactiveAsset.Id
                || item.Id == failedAsset.Id)
            .ToDictionaryAsync(item => item.Id);

        Assert.Equal("Ready", persistedAssets[activateAsset.Id].Status);
        Assert.True(persistedAssets[activateAsset.Id].LessonVideo.IsActive);
        Assert.Equal("Ready", persistedAssets[remainInactiveAsset.Id].Status);
        Assert.False(persistedAssets[remainInactiveAsset.Id].LessonVideo.IsActive);
        Assert.Equal("Processing", persistedAssets[failedAsset.Id].Status);
        Assert.False(persistedAssets[failedAsset.Id].LessonVideo.IsActive);
        Assert.NotNull(persistedAssets[failedAsset.Id].LastStatusSyncedAtUtc);
        Assert.False((await db.BunnyStreamLibraries.AsNoTracking().SingleAsync(item => item.Id == library.Id)).IsActive);
    }

    private sealed record ManagedBunnyVideoSeed(
        User Admin,
        Lesson Lesson,
        VideoType VideoType,
        BunnyStreamLibrary Library,
        LessonVideo Video,
        BunnyVideoAsset Asset);

    [Fact]
    public async Task ManagedBunnyVideo_SessionCoversKnownVideoDurationAndPlaybackMargin()
    {
        await using var db = TestAppDbContextFactory.Create();
        var seeded = await SeedManagedBunnyVideoAsync(db);
        seeded.Asset.DurationSeconds = 4 * 60 * 60;
        await db.SaveChangesAsync();
        var beforeCreation = DateTime.UtcNow;

        var sessionResult = await new CreateVideoSessionCommandHandler(
                db,
                new AccessCheckService(db),
                new VideoEncryptionService())
            .Handle(
                new CreateVideoSessionCommand(seeded.Video.Id, seeded.Admin.Id),
                CancellationToken.None);

        Assert.True(sessionResult.Success, sessionResult.Message);
        Assert.True(sessionResult.Data!.ExpiresAt >= beforeCreation.AddHours(4.5));
        Assert.True(sessionResult.Data.ExpiresAt <= DateTime.UtcNow.AddHours(4.5));
    }

    private static async Task<ManagedBunnyVideoSeed> SeedManagedBunnyVideoAsync(AppDbContext db)
    {
        var (admin, lesson, videoType) = await SeedUploadGraphAsync(db);
        var ownership = await db.Lessons
            .Where(item => item.Id == lesson.Id)
            .Select(item => new
            {
                PackageId = item.ContentSection.Term.PackageId,
                TeacherId = item.ContentSection.Term.Package.TeacherId
            })
            .SingleAsync();
        var library = CreateLibrary("مكتبة الفيديو", 749997);
        var video = new LessonVideo
        {
            Title = "Managed Bunny",
            Provider = VideoProviders.Bunny,
            ProviderVideoId = VideoGuid,
            LessonId = lesson.Id,
            VideoTypeId = videoType.Id,
            BunnyStreamLibraryId = library.Id,
            Order = 1,
            MaxWatchCount = 3,
            IsActive = true
        };
        var asset = new BunnyVideoAsset
        {
            LessonVideo = video,
            TeacherId = ownership.TeacherId,
            PackageId = ownership.PackageId,
            LessonId = lesson.Id,
            UploadedByUserId = admin.Id,
            BunnyLibraryId = library.ExternalLibraryId,
            BunnyStreamLibraryRecordId = library.Id,
            BunnyVideoGuid = VideoGuid,
            Title = video.Title,
            UploadMethod = "TusFile",
            Status = "Ready",
            ActivateWhenReady = true,
            SourceState = BunnyVideoAssetSourceState.Current
        };
        var snapshot = new BunnyUsageSnapshot
        {
            BunnyVideoAsset = asset,
            TeacherId = ownership.TeacherId,
            PackageId = ownership.PackageId,
            LessonId = lesson.Id,
            PeriodStartUtc = DateTime.UtcNow.AddDays(-1),
            PeriodEndUtc = DateTime.UtcNow,
            StorageBytes = 1024,
            BandwidthBytes = 2048,
            BandwidthSource = "Bunny",
            SyncedAtUtc = DateTime.UtcNow
        };

        db.AddRange(library, video, asset, snapshot);
        await db.SaveChangesAsync();
        return new ManagedBunnyVideoSeed(admin, lesson, videoType, library, video, asset);
    }

    private static async Task<BunnyVideoAsset> AddPendingReplacementAsync(
        AppDbContext db,
        ManagedBunnyVideoSeed seeded)
    {
        var ownership = await db.Lessons
            .Where(item => item.Id == seeded.Lesson.Id)
            .Select(item => new
            {
                PackageId = item.ContentSection.Term.PackageId,
                TeacherId = item.ContentSection.Term.Package.TeacherId
            })
            .SingleAsync();
        var candidate = new BunnyVideoAsset
        {
            LessonVideo = seeded.Video,
            TeacherId = ownership.TeacherId,
            PackageId = ownership.PackageId,
            LessonId = seeded.Lesson.Id,
            UploadedByUserId = seeded.Admin.Id,
            BunnyLibraryId = seeded.Library.ExternalLibraryId,
            BunnyStreamLibraryRecordId = seeded.Library.Id,
            BunnyVideoGuid = Guid.NewGuid().ToString("D"),
            Title = "Pending replacement",
            UploadMethod = "TusFile",
            Status = "Processing",
            ActivateWhenReady = true,
            SourceState = BunnyVideoAssetSourceState.PendingReplacement,
            TargetOrder = 2,
            TargetMaxWatchCount = 3,
            TargetVideoTypeId = seeded.VideoType.Id,
            TargetIsActive = true,
            TargetSourceRevision = seeded.Video.SourceRevision
        };
        db.BunnyVideoAssets.Add(candidate);
        await db.SaveChangesAsync();
        return candidate;
    }

    private static BunnyStreamLibrary CreateLibrary(string name, long externalLibraryId) => new()
    {
        Name = name,
        NormalizedName = name.Trim().ToUpperInvariant(),
        ExternalLibraryId = externalLibraryId,
        ApiKeyCiphertext = FakeSecretProtector.Ciphertext.ToArray(),
        IsActive = true
    };

    private static AppDbContext CreateDbThatFailsOnSave(int saveCall)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"bunny-compensation-{Guid.NewGuid():N}")
            .AddInterceptors(new FailOnSaveChangesInterceptor(saveCall))
            .Options;
        return new AppDbContext(options);
    }

    private static LessonVideo CreatePendingVideo(
        Guid lessonId,
        Guid videoTypeId,
        Guid libraryId,
        string title) => new()
    {
        Title = title,
        Provider = VideoProviders.Bunny,
        ProviderVideoId = Guid.NewGuid().ToString("D"),
        LessonId = lessonId,
        VideoTypeId = videoTypeId,
        BunnyStreamLibraryId = libraryId,
        IsActive = false
    };

    private static BunnyVideoAsset CreatePendingAsset(
        LessonVideo video,
        Guid teacherId,
        Guid packageId,
        Guid uploadedByUserId,
        long externalLibraryId,
        string status,
        bool activateWhenReady) => new()
    {
        LessonVideo = video,
        TeacherId = teacherId,
        PackageId = packageId,
        LessonId = video.LessonId,
        UploadedByUserId = uploadedByUserId,
        BunnyLibraryId = externalLibraryId,
        BunnyVideoGuid = video.ProviderVideoId,
        Title = video.Title,
        UploadMethod = "TusFile",
        Status = status,
        ActivateWhenReady = activateWhenReady
    };

    private static async Task<(User Admin, Lesson Lesson, VideoType VideoType)> SeedUploadGraphAsync(AppDbContext db)
    {
        var adminRole = new Role { Name = "Admin", Type = RoleType.Admin };
        var admin = new User { FullName = "Admin", PhoneNumber = $"bunny-admin-{Guid.NewGuid():N}", PasswordHash = "hash" };
        var teacherUser = new User { FullName = "Teacher", PhoneNumber = $"bunny-teacher-{Guid.NewGuid():N}", PasswordHash = "hash" };
        var teacher = new TeacherProfile
        {
            UserId = teacherUser.Id,
            Bio = "Bio",
            Specialization = "Physics",
            ContactInfo = "contact"
        };
        var subject = new Subject { Name = "Physics", NormalizedName = $"PHYSICS-{Guid.NewGuid():N}", Description = "Physics" };
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
        var videoType = new VideoType { Name = "شرح", NormalizedName = "شرح", SortOrder = 1, IsActive = true };
        db.AddRange(adminRole, admin, teacherUser, teacher, subject, package, term, section, lesson, videoType);
        db.UserRoles.Add(new UserRole { UserId = admin.Id, RoleId = adminRole.Id });
        await db.SaveChangesAsync();
        return (admin, lesson, videoType);
    }

    private sealed class FakeLibraryAccessService(params BunnyStreamLibraryAccess[] libraries)
        : IBunnyStreamLibraryAccessService
    {
        public Task<BunnyStreamLibraryAccessResult> ResolveAsync(
            Guid libraryId,
            bool requireActive,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Resolve(libraries.SingleOrDefault(item => item.Id == libraryId), requireActive));
        }

        public Task<BunnyStreamLibraryAccessResult> ResolveByExternalIdAsync(
            long externalLibraryId,
            bool requireActive,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Resolve(
                libraries.SingleOrDefault(item => item.ExternalLibraryId == externalLibraryId),
                requireActive));
        }

        private static BunnyStreamLibraryAccessResult Resolve(BunnyStreamLibraryAccess? access, bool requireActive)
        {
            if (access is null)
            {
                return BunnyStreamLibraryAccessResult.Fail("BUNNY_LIBRARY_NOT_FOUND", "Bunny library not found.");
            }

            return requireActive && !access.IsActive
                ? BunnyStreamLibraryAccessResult.Fail("BUNNY_LIBRARY_INACTIVE", "Bunny library inactive.")
                : BunnyStreamLibraryAccessResult.Ok(access);
        }
    }

    private sealed class FailOnSaveChangesInterceptor(int failOnCall) : SaveChangesInterceptor
    {
        private int _saveCalls;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            _saveCalls++;
            if (_saveCalls == failOnCall)
            {
                throw new SimulatedPersistenceException();
            }

            return ValueTask.FromResult(result);
        }
    }

    private sealed class SimulatedPersistenceException : Exception;

    private sealed class FakeBunnyStreamClientFactory(params FakeBunnyStreamClient[] clients)
        : IBunnyStreamClientFactory
    {
        private readonly IReadOnlyDictionary<(long LibraryId, string ApiKey), FakeBunnyStreamClient> _clients =
            clients.ToDictionary(client => (client.LibraryId, client.ApiKey));

        public IBunnyStreamClient Create(long libraryId, string apiKey)
        {
            return _clients.TryGetValue((libraryId, apiKey), out var client)
                ? client
                : throw new InvalidOperationException("No fake Bunny client is configured for this library credential pair.");
        }
    }

    private sealed class FakeSecretProtector : IBunnyStreamLibrarySecretProtector
    {
        public static readonly byte[] Ciphertext = [0xC1, 0x50, 0x48, 0x45, 0x52];

        private readonly Dictionary<Guid, string> _plaintextByLibrary = [];

        public byte[] Protect(Guid libraryId, string apiKey)
        {
            _plaintextByLibrary[libraryId] = apiKey;
            return Ciphertext.ToArray();
        }

        public string Unprotect(Guid libraryId, ReadOnlySpan<byte> ciphertext) =>
            _plaintextByLibrary.TryGetValue(libraryId, out var apiKey)
                ? apiKey
                : throw new InvalidOperationException("No API key was protected for this library.");
    }

    private sealed class FakeBunnyStreamClient(long libraryId, string apiKey = "api-key")
        : IBunnyStreamClient
    {
        private readonly Dictionary<string, int> _videoStatuses = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _failedStatusLookups = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _missingVideoGuids = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _fetchedVideoGuids = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _deletedVideoGuids = new(StringComparer.OrdinalIgnoreCase);
        private string? _createdVideoGuid;

        public long LibraryId { get; } = libraryId;
        public string ApiKey { get; } = apiKey;
        public int VideoStatus { get; set; }
        public bool RequireFetchBeforeStatusLookup { get; init; }
        public BunnyFetchVideoResultDto FetchResult { get; set; } = new(true, null, 200);
        public IReadOnlyCollection<string> DeletedVideoGuids => _deletedVideoGuids;
        public BunnyStreamValidationResult ValidationResult { get; init; } =
            new(true, null, null);

        public void SetVideoStatus(string videoGuid, int status) => _videoStatuses[videoGuid] = status;

        public void FailStatusLookup(string videoGuid) => _failedStatusLookups.Add(videoGuid);

        public void ReturnMissingVideo(string videoGuid) => _missingVideoGuids.Add(videoGuid);

        public void ReturnVideo(string videoGuid) => _missingVideoGuids.Remove(videoGuid);

        public Task<BunnyStreamValidationResult> ValidateLibraryAccessAsync(CancellationToken cancellationToken) =>
            Task.FromResult(ValidationResult);

        public Task<BunnyStreamVideoDto> CreateVideoAsync(
            string title,
            string? collectionId,
            CancellationToken cancellationToken)
        {
            if (_createdVideoGuid is not null)
            {
                throw new InvalidOperationException("The fake Bunny library allows only one video creation per scenario.");
            }

            _createdVideoGuid = Guid.NewGuid().ToString("D");
            return Task.FromResult(ToVideo(_createdVideoGuid, title, collectionId));
        }

        public Task<BunnyFetchVideoResultDto> FetchVideoAsync(
            string videoGuid,
            string url,
            CancellationToken cancellationToken)
        {
            if (!string.Equals(videoGuid, _createdVideoGuid, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new BunnyFetchVideoResultDto(
                    false,
                    "Fetch did not target the video created for this scenario.",
                    409));
            }

            if (FetchResult.Success)
            {
                _fetchedVideoGuids.Add(videoGuid);
            }
            return Task.FromResult(FetchResult);
        }

        public Task DeleteVideoAsync(string videoGuid, CancellationToken cancellationToken)
        {
            _deletedVideoGuids.Add(videoGuid);
            return Task.CompletedTask;
        }

        public Task<BunnyStreamVideoDto?> GetVideoAsync(string videoGuid, CancellationToken cancellationToken)
        {
            if (_failedStatusLookups.Contains(videoGuid))
            {
                throw new HttpRequestException("Simulated Bunny status failure.");
            }

            if (_missingVideoGuids.Contains(videoGuid))
            {
                return Task.FromResult<BunnyStreamVideoDto?>(null);
            }

            if (RequireFetchBeforeStatusLookup && !_fetchedVideoGuids.Contains(videoGuid))
            {
                return Task.FromResult<BunnyStreamVideoDto?>(null);
            }

            var status = _videoStatuses.GetValueOrDefault(videoGuid, VideoStatus);
            return Task.FromResult<BunnyStreamVideoDto?>(ToVideo(videoGuid, "Video", null, status));
        }

        public Task<IReadOnlyList<BunnyStreamVideoDto>> ListVideosAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<BunnyStreamVideoDto>>([]);

        public Task<BunnyVideoStorageDto?> GetVideoStorageAsync(string videoGuid, CancellationToken cancellationToken) =>
            Task.FromResult<BunnyVideoStorageDto?>(null);

        public Task<BunnyVideoLibraryDto?> GetVideoLibraryAsync(CancellationToken cancellationToken) =>
            Task.FromResult<BunnyVideoLibraryDto?>(null);

        public BunnyTusUploadSignatureDto CreateTusUploadSignature(string videoGuid, TimeSpan expiresIn) =>
            new(LibraryId, videoGuid, "https://video.bunnycdn.com/tusupload", "signature", 1234567890);

        public Task TriggerSmartActionsAsync(
            string videoGuid,
            BunnySmartActionsRequest request,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        private BunnyStreamVideoDto ToVideo(
            string guid,
            string title,
            string? collectionId,
            int? statusOverride = null) =>
            new(
                LibraryId,
                guid,
                title,
                statusOverride ?? VideoStatus,
                (statusOverride ?? VideoStatus) == 4 ? 100 : 50,
                2048,
                60,
                0,
                0,
                collectionId,
                false,
                true);
    }
}
