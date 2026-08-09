using Microsoft.AspNetCore.Mvc;
using NaderGorge.API.Controllers;
using NaderGorge.Domain.Entities;

namespace NaderGorge.Application.Tests;

public sealed class VideoSessionControllerTests
{
    [Fact]
    public async Task GetEmbedMaterial_ConsumedButActiveSession_ReturnsMaterial()
    {
        await using var db = TestAppDbContextFactory.Create();
        var session = ActiveSession();
        session.IsConsumed = true;
        db.VideoPlaybackSessions.Add(session);
        await db.SaveChangesAsync();

        var controller = new VideoSessionController(null!, db);

        var response = await controller.GetEmbedMaterial(session.Id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response);
        var material = Assert.IsType<VideoEmbedMaterialResponse>(ok.Value);
        Assert.Equal(session.SessionToken, material.Token);
        Assert.Equal(session.EncryptionKey, material.Key);
    }

    [Fact]
    public async Task GetEmbedMaterial_SupersededSession_ReturnsNotFound()
    {
        await using var db = TestAppDbContextFactory.Create();
        var session = ActiveSession();
        session.IsSuperseded = true;
        db.VideoPlaybackSessions.Add(session);
        await db.SaveChangesAsync();

        var controller = new VideoSessionController(null!, db);

        var response = await controller.GetEmbedMaterial(session.Id, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(response);
    }

    private static VideoPlaybackSession ActiveSession() => new()
    {
        Id = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        LessonVideoId = Guid.NewGuid(),
        SessionToken = "encrypted-token",
        EncryptionKey = "encryption-key",
        CreatedAt = DateTime.UtcNow,
        ExpiresAt = DateTime.UtcNow.AddMinutes(5)
    };
}
