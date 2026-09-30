using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.VideoTypes;
using NaderGorge.Application.Features.Admin.VideoTypes.Commands;
using NaderGorge.Domain.Entities;
using NaderGorge.Infrastructure.Services.AdminAI.Actions;

namespace NaderGorge.Integration.Tests.AdminAI;

public sealed class AdminAIVideoTypeOperationPostgresTests
{
    [Fact]
    public async Task CreateReplayAfterDeletion_ReturnsOriginalSafeResultWithoutNewAudit()
    {
        await using var fixture = await PostgresAdminAIFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        await db.Database.MigrateAsync();
        var admin = new User
        {
            FullName = "Video type admin", PhoneNumber = "01000007901", PasswordHash = "test"
        };
        db.Users.Add(admin);
        await db.SaveChangesAsync();
        var actor = admin.Id;
        var operationId = Guid.NewGuid().ToString("N");
        var command = new CreateVideoTypeCommand("Interactive", 7, true, actor)
        {
            OperationId = operationId
        };
        var created = await new CreateVideoTypeCommandHandler(db).Handle(command, default);
        Assert.True(created.Success);
        Assert.True((await new DeleteVideoTypeCommandHandler(db).Handle(
            new DeleteVideoTypeCommand(created.Data!.Id, actor), default)).Success);

        await using var replayDb = fixture.CreateDbContext();
        var handler = new CreateVideoTypeCommandHandler(replayDb);
        var replay = await handler.Handle(command, default);
        Assert.True(replay.Success);
        Assert.Equal(created.Data, replay.Data);
        var conflict = await handler.Handle(command with { SortOrder = 8 }, default);
        Assert.False(conflict.Success);
        Assert.Contains("IDEMPOTENCY_CONFLICT", conflict.Errors!);

        await using var verify = fixture.CreateDbContext();
        Assert.Equal(0, await verify.VideoTypes.CountAsync(item => item.Id == created.Data.Id));
        Assert.Equal(1, await verify.AuditLogs.CountAsync(item => item.Action == "CREATE_VIDEO_TYPE"));
        var resolver = new AdminAIVideoTypeResultResolver(verify,
            "admin.content.video-type.create", "video-type.create");
        var resolved = await resolver.ResolveAsync(operationId, operationId, default);
        Assert.Equal(created.Data, Assert.IsType<VideoTypeDto>(resolved?.SafeResult));
        Assert.Null(await resolver.ResolveAsync(operationId, "different-execution", default));
    }

    [Fact]
    public async Task UpdateReplay_LeavesLaterEditIntactAndReturnsOriginalSafeResult()
    {
        await using var fixture = await PostgresAdminAIFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        await db.Database.MigrateAsync();
        var type = new VideoType
        {
            Name = "Original", NormalizedName = "ORIGINAL", SortOrder = 1, IsActive = true
        };
        var admin = new User
        {
            FullName = "Video type update admin", PhoneNumber = "01000007902", PasswordHash = "test"
        };
        db.AddRange(type, admin);
        await db.SaveChangesAsync();
        var actor = admin.Id;
        var operationId = Guid.NewGuid().ToString("N");
        var command = new UpdateVideoTypeCommand(type.Id, "Reviewed", 2, actor)
        {
            OperationId = operationId
        };
        var updated = await new UpdateVideoTypeCommandHandler(db).Handle(command, default);
        Assert.True(updated.Success);
        Assert.True((await new UpdateVideoTypeCommandHandler(db).Handle(
            new UpdateVideoTypeCommand(type.Id, "Later", 3, actor), default)).Success);

        await using var replayDb = fixture.CreateDbContext();
        var handler = new UpdateVideoTypeCommandHandler(replayDb);
        var replay = await handler.Handle(command, default);
        Assert.True(replay.Success);
        Assert.Equal(updated.Data, replay.Data);
        var conflict = await handler.Handle(command with { SortOrder = 4 }, default);
        Assert.False(conflict.Success);
        Assert.Contains("IDEMPOTENCY_CONFLICT", conflict.Errors!);

        await using var verify = fixture.CreateDbContext();
        var persisted = await verify.VideoTypes.AsNoTracking().SingleAsync(item => item.Id == type.Id);
        Assert.Equal("Later", persisted.Name);
        Assert.Equal(3, persisted.SortOrder);
        Assert.Equal(2, await verify.AuditLogs.CountAsync(item => item.Action == "UPDATE_VIDEO_TYPE"));
        var resolver = new AdminAIVideoTypeResultResolver(verify,
            "admin.content.video-type.update", "video-type.update");
        var resolved = await resolver.ResolveAsync(operationId, operationId, default);
        Assert.Equal(updated.Data, Assert.IsType<VideoTypeDto>(resolved?.SafeResult));
        Assert.Null(await resolver.ResolveAsync(operationId, "different-execution", default));
    }
}
