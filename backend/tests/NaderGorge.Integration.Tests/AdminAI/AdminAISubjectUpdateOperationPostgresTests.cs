using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Domain.Entities;
using NaderGorge.Infrastructure.Services.AdminAI.Actions;

namespace NaderGorge.Integration.Tests.AdminAI;

public sealed class AdminAISubjectUpdateOperationPostgresTests
{
    [Fact]
    public async Task ReplayingSubjectUpdate_DoesNotOverwriteLaterEdit()
    {
        await using var fixture = await PostgresAdminAIFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        await db.Database.MigrateAsync();
        var subject = new Subject
        {
            Name = "Initial subject", NormalizedName = "INITIAL SUBJECT", Description = "Initial"
        };
        db.Subjects.Add(subject);
        await db.SaveChangesAsync();

        var actor = Guid.NewGuid();
        var operationId = Guid.NewGuid().ToString("N");
        var firstUpdate = new UpdateSubjectCommand(subject.Id, "First edit", "First")
        {
            ActorUserId = actor,
            OperationId = operationId
        };
        Assert.True((await new UpdateSubjectCommandHandler(db).Handle(firstUpdate, default)).Success);
        Assert.True((await new UpdateSubjectCommandHandler(db).Handle(
            new UpdateSubjectCommand(subject.Id, "Later edit", "Later"), default)).Success);

        await using var replayDb = fixture.CreateDbContext();
        var handler = new UpdateSubjectCommandHandler(replayDb);
        Assert.True((await handler.Handle(firstUpdate, default)).Success);
        var conflict = await handler.Handle(firstUpdate with { Description = "Changed request" }, default);
        Assert.False(conflict.Success);
        Assert.Contains("IDEMPOTENCY_CONFLICT", conflict.Errors!);

        await using var verify = fixture.CreateDbContext();
        var persisted = await verify.Subjects.AsNoTracking().SingleAsync(item => item.Id == subject.Id);
        Assert.Equal("Later edit", persisted.Name);
        Assert.Equal("Later", persisted.Description);
        Assert.Equal(1, await verify.AuthoritativeOperationReceipts.AsNoTracking()
            .CountAsync(item => item.OperationId == operationId && item.ResultEntityId == subject.Id));
        Assert.NotNull(await new AdminAISubjectUpdateResultResolver(verify)
            .ResolveAsync(operationId, operationId, default));
    }
}
