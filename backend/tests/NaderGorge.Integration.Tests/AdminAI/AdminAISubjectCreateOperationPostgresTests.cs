using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Infrastructure.Services.AdminAI.Actions;

namespace NaderGorge.Integration.Tests.AdminAI;

public sealed class AdminAISubjectCreateOperationPostgresTests
{
    [Fact]
    public async Task SubjectCreation_ReplaysAfterDeletion_AndRecoveryReturnsOriginalIdentity()
    {
        await using var fixture = await PostgresAdminAIFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        await db.Database.MigrateAsync();
        var actor = Guid.NewGuid();
        var operationId = Guid.NewGuid().ToString("N");
        var command = new CreateSubjectCommand("Replay subject", "Original description")
        {
            ActorUserId = actor,
            OperationId = operationId
        };
        var first = await new CreateSubjectCommandHandler(db).Handle(command, default);
        Assert.True(first.Success);
        var subjectId = first.Data;
        Assert.True((await new DeleteSubjectCommandHandler(db)
            .Handle(new DeleteSubjectCommand(subjectId), default)).Success);

        await using var replayDb = fixture.CreateDbContext();
        var handler = new CreateSubjectCommandHandler(replayDb);
        var replay = await handler.Handle(command, default);
        Assert.True(replay.Success);
        Assert.Equal(subjectId, replay.Data);
        var conflict = await handler.Handle(command with { Description = "Changed description" }, default);
        Assert.False(conflict.Success);
        Assert.Contains("IDEMPOTENCY_CONFLICT", conflict.Errors!);

        await using var verify = fixture.CreateDbContext();
        Assert.Equal(0, await verify.Subjects.AsNoTracking().CountAsync(item => item.Id == subjectId));
        Assert.Equal(1, await verify.AuthoritativeOperationReceipts.AsNoTracking()
            .CountAsync(item => item.OperationId == operationId && item.ResultEntityId == subjectId));
        Assert.NotNull(await new AdminAISubjectCreateResultResolver(verify)
            .ResolveAsync(operationId, operationId, default));
        Assert.Null(await new AdminAISubjectCreateResultResolver(verify)
            .ResolveAsync(operationId, "different-execution", default));
    }
}
