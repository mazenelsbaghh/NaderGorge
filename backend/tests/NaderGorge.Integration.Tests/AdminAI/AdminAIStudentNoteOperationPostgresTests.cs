using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Domain.Entities;
using NaderGorge.Infrastructure.Services.AdminAI.Actions;
using Npgsql;

namespace NaderGorge.Integration.Tests.AdminAI;

public sealed class AdminAIStudentNoteOperationPostgresTests
{
    [Fact]
    public async Task StudentNoteReceipt_ReplaysAfterDeletion_AndRejectsConflictingPayload()
    {
        await using var fixture = await PostgresAdminAIFixture.CreateAsync();
        await using var seed = fixture.CreateDbContext();
        await seed.Database.MigrateAsync();
        var admin = new User { FullName = "Note admin", PhoneNumber = "01000007801", PasswordHash = "test" };
        var student = new User { FullName = "Note student", PhoneNumber = "01000007802", PasswordHash = "test" };
        seed.Users.AddRange(admin, student);
        await seed.SaveChangesAsync();

        var operationId = Guid.NewGuid().ToString("N");
        var command = new AddStudentNoteCommand(student.Id, "Reviewed note", true, admin.Id)
        {
            OperationId = operationId
        };
        Assert.True((await new AddStudentNoteCommandHandler(seed).Handle(command, default)).Success);
        var note = await seed.StudentNotes.AsNoTracking().SingleAsync();
        Assert.True((await new DeleteStudentNoteCommandHandler(seed)
            .Handle(new DeleteStudentNoteCommand(note.Id, admin.Id), default)).Success);

        await using var replayDb = fixture.CreateDbContext();
        var handler = new AddStudentNoteCommandHandler(replayDb);
        Assert.True((await handler.Handle(command, default)).Success);
        var conflict = await handler.Handle(command with { Content = "Different note" }, default);
        Assert.False(conflict.Success);
        Assert.Contains("IDEMPOTENCY_CONFLICT", conflict.Errors!);

        await using var verify = fixture.CreateDbContext();
        Assert.Empty(await verify.StudentNotes.AsNoTracking().ToListAsync());
        var receipt = await verify.AuthoritativeOperationReceipts.AsNoTracking().SingleAsync();
        Assert.Equal(note.Id, receipt.ResultEntityId);
        Assert.Equal("student-note.create", receipt.Scope);
        Assert.DoesNotContain(command.Content, receipt.RequestHash, StringComparison.Ordinal);
        var resolved = await new AdminAIStudentNoteResultResolver(verify)
            .ResolveAsync(operationId, operationId, default);
        Assert.NotNull(resolved);
        Assert.Null(await new AdminAIStudentNoteResultResolver(verify)
            .ResolveAsync(operationId, "different-execution", default));
    }

    [Fact]
    public async Task ConcurrentSameOperation_CreatesExactlyOneNote_AndCanReplay()
    {
        await using var fixture = await PostgresAdminAIFixture.CreateAsync();
        await using var seed = fixture.CreateDbContext();
        await seed.Database.MigrateAsync();
        var admin = new User { FullName = "Concurrent note admin", PhoneNumber = "01000007803", PasswordHash = "test" };
        var student = new User { FullName = "Concurrent note student", PhoneNumber = "01000007804", PasswordHash = "test" };
        seed.Users.AddRange(admin, student);
        await seed.SaveChangesAsync();

        var command = new AddStudentNoteCommand(student.Id, "Only once", false, admin.Id)
        {
            OperationId = Guid.NewGuid().ToString("N")
        };
        async Task RunAsync()
        {
            await using var db = fixture.CreateDbContext();
            try
            {
                Assert.True((await new AddStudentNoteCommandHandler(db).Handle(command, default)).Success);
            }
            catch (Exception exception) when (IsReceiptRace(exception))
            {
                // A losing transaction is safe because the receipt and note commit atomically.
            }
        }

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => RunAsync()));
        await using var replayDb = fixture.CreateDbContext();
        Assert.True((await new AddStudentNoteCommandHandler(replayDb).Handle(command, default)).Success);
        Assert.Equal(1, await replayDb.StudentNotes.AsNoTracking().CountAsync());
        Assert.Equal(1, await replayDb.AuthoritativeOperationReceipts.AsNoTracking().CountAsync());
    }

    private static bool IsReceiptRace(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
            if (current is PostgresException { SqlState: "40001" or "23505" })
                return true;
        return false;
    }
}
