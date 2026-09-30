using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Operations.Commands;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Services.AdminAI.Actions;

namespace NaderGorge.Integration.Tests.AdminAI;

public sealed class AdminAITaskCommentOperationPostgresTests
{
    [Fact]
    public async Task RetryAfterCommentDeletion_ReturnsOriginalIdWithoutCreatingAnotherComment()
    {
        await using var fixture = await PostgresAdminAIFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        await db.Database.MigrateAsync();
        var user = new User
        {
            FullName = "Task comment admin", PhoneNumber = "01000007921", PasswordHash = "test",
            UserRoles = [new UserRole { Role = new Role { Name = "Task comment admin role", Type = RoleType.Admin } }]
        };
        var task = new TaskItem
        {
            Title = "Review", Description = "Review content", AssigneeId = user.Id, CreatedById = user.Id
        };
        db.AddRange(user, task);
        await db.SaveChangesAsync();

        var operationId = Guid.NewGuid().ToString("N");
        var command = new AddTaskCommentCommand(task.Id, user.Id, "First comment")
        { OperationId = operationId };
        var created = await new AddTaskCommentCommandHandler(db).Handle(command, default);
        Assert.True(created.Success);
        db.TaskComments.Remove(await db.TaskComments.SingleAsync(item => item.Id == created.Data));
        await db.SaveChangesAsync();

        await using var retryDb = fixture.CreateDbContext();
        var handler = new AddTaskCommentCommandHandler(retryDb);
        var replay = await handler.Handle(command, default);
        Assert.True(replay.Success);
        Assert.Equal(created.Data, replay.Data);
        var conflict = await handler.Handle(command with { Content = "Changed comment" }, default);
        Assert.False(conflict.Success);
        Assert.Contains("IDEMPOTENCY_CONFLICT", conflict.Errors!);

        await using var verifyDb = fixture.CreateDbContext();
        Assert.Equal(0, await verifyDb.TaskComments.CountAsync());
        Assert.Equal(1, await verifyDb.AuthoritativeOperationReceipts.CountAsync(item => item.OperationId == operationId));
        var resolver = new AdminAITaskCommentResultResolver(verifyDb);
        var recovered = await resolver.ResolveAsync(operationId, operationId, default);
        var safeResult = JsonSerializer.SerializeToElement(recovered?.SafeResult);
        Assert.Equal(created.Data, safeResult.GetProperty("commentId").GetGuid());
        Assert.Null(await resolver.ResolveAsync(operationId, "wrong-execution", default));
    }
}
