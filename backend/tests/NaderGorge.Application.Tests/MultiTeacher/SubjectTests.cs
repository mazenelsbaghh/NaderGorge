using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Features.Admin.Queries;
using NaderGorge.Domain.Entities;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests.MultiTeacher;

public class SubjectTests
{
    [Fact]
    public async Task CreateSubject_SavesToDatabase()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var handler = new CreateSubjectCommandHandler(db);

        var result = await handler.Handle(
            new CreateSubjectCommand("Mathematics", "Math description"),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotEqual(Guid.Empty, result.Data);

        var subject = await db.Subjects.FirstOrDefaultAsync(s => s.Id == result.Data);
        Assert.NotNull(subject);
        Assert.Equal("Mathematics", subject!.Name);
        Assert.Equal("MATHEMATICS", subject!.NormalizedName);
        Assert.Equal("Math description", subject!.Description);
    }

    [Fact]
    public async Task UpdateSubject_UpdatesSuccessfully()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var subject = new Subject { Id = Guid.NewGuid(), Name = "Math", NormalizedName = "MATH", Description = "Old" };
        db.Subjects.Add(subject);
        await db.SaveChangesAsync();

        var handler = new UpdateSubjectCommandHandler(db);
        var result = await handler.Handle(
            new UpdateSubjectCommand(subject.Id, "Physics", "New"),
            CancellationToken.None);

        Assert.True(result.Success);

        var updated = await db.Subjects.FindAsync(subject.Id);
        Assert.NotNull(updated);
        Assert.Equal("Physics", updated!.Name);
        Assert.Equal("PHYSICS", updated!.NormalizedName);
        Assert.Equal("New", updated!.Description);
    }

    [Fact]
    public async Task DeleteSubject_RemovesSuccessfully()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var subject = new Subject { Id = Guid.NewGuid(), Name = "Math", NormalizedName = "MATH", Description = "Old" };
        db.Subjects.Add(subject);
        await db.SaveChangesAsync();

        var handler = new DeleteSubjectCommandHandler(db);
        var result = await handler.Handle(
            new DeleteSubjectCommand(subject.Id),
            CancellationToken.None);

        Assert.True(result.Success);

        var deleted = await db.Subjects.FindAsync(subject.Id);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task DeleteSubject_FailsIfLinkedToProgram()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var subject = new Subject { Id = Guid.NewGuid(), Name = "Math", NormalizedName = "MATH" };
        var program = new Program { Id = Guid.NewGuid(), Name = "Grade 10", SubjectId = subject.Id };
        
        db.Subjects.Add(subject);
        db.Programs.Add(program);
        await db.SaveChangesAsync();

        var handler = new DeleteSubjectCommandHandler(db);
        var result = await handler.Handle(
            new DeleteSubjectCommand(subject.Id),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.NotNull(result.Message);
        Assert.Contains("program", result.Message.ToLower());

        var exists = await db.Subjects.FindAsync(subject.Id);
        Assert.NotNull(exists);
    }
}
