using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Infrastructure.Data;
using Xunit;

namespace NaderGorge.Application.Tests;

public class LessonOutboxTests
{
    [Fact]
    public async Task CreateLesson_ShouldEnqueueLessonPublishedOutboxEvent()
    {
        // Arrange
        await using AppDbContext db = TestAppDbContextFactory.Create();
        
        var (packageId, subjectId) = await TestAppDbContextFactory.SeedPackageAsync(db, "Test Package");
        
        var term = new Term
        {
            Id = Guid.NewGuid(),
            Title = "Test Term",
            PackageId = packageId
        };
        db.Terms.Add(term);
        
        var section = new ContentSection
        {
            Id = Guid.NewGuid(),
            Title = "Test Section",
            TermId = term.Id
        };
        db.ContentSections.Add(section);
        await db.SaveChangesAsync();

        var authService = new TeacherAuthorizationService(db);
        var handler = new CreateLessonCommandHandler(db, authService);

        var command = new CreateLessonCommand(
            Title: "Test Lesson",
            Summary: "Lesson Summary",
            Order: 1,
            SectionId: section.Id,
            ExamId: null,
            Price: 0m,
            CurrentUserId: null
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        
        var outboxEvents = await db.OutboxEvents.ToListAsync();
        Assert.Single(outboxEvents);

        var @event = outboxEvents[0];
        Assert.Equal("LessonPublished", @event.Type);
        Assert.Equal($"Package_{packageId}", @event.TargetGroup);
        Assert.Contains(result.Data.ToString(), @event.PayloadJson);
        Assert.Contains("Test Lesson", @event.PayloadJson);
        Assert.Null(@event.ProcessedAt);
    }
}
