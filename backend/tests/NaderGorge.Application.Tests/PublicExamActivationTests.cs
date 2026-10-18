using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.Sales;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Tests;

public sealed class PublicExamActivationTests
{
    [Fact]
    public async Task ActivationSwitchHidesAndRestoresTheExamWithoutChangingSavedAttempts()
    {
        await using var db = TestAppDbContextFactory.Create();
        var actor = await TestAppDbContextFactory.SeedUserAsync(db, "Activation preview", "activation-preview");
        var (exam, _, _, _, _, _, _) = await TestAppDbContextFactory.SeedEssayExamAsync(db);
        var product = new PublicExamProduct { Exam = exam, Slug = "activation-preview", IsPublished = true };
        var attempt = new StudentExamAttempt { Exam = exam, User = actor, ScoreAchieved = 7, Evaluation = "جيد" };
        db.AddRange(product, attempt);
        await db.SaveChangesAsync();
        var handler = new SetPublicExamActivationCommandHandler(db);

        var disabled = await handler.Handle(new(product.Id, false, actor.Id), default);

        Assert.True(disabled.Success, disabled.Message);
        Assert.False(disabled.Data!.IsPublished);
        Assert.NotNull(disabled.Data.DisabledAt);
        var hidden = await new GetPublicExamProductsQueryHandler(db).Handle(new(PublishedOnly: true), default);
        Assert.Empty(hidden.Data!);
        exam.IsActive = false;
        await db.SaveChangesAsync();
        var enabled = await handler.Handle(new(product.Id, true, actor.Id), default);
        Assert.True(enabled.Success, enabled.Message);
        Assert.True(enabled.Data!.IsPublished);
        Assert.True(enabled.Data.IsActive);
        Assert.Null(enabled.Data.DisabledAt);
        var visible = await new GetPublicExamProductsQueryHandler(db).Handle(new(PublishedOnly: true), default);
        Assert.Equal(product.Id, Assert.Single(visible.Data!).Id);
        Assert.Equal(7m, (await db.StudentExamAttempts.SingleAsync()).ScoreAchieved);
        Assert.Equal(2, await db.AuditLogs.CountAsync(a => a.Action == "PublicExamActivationChanged" && a.PerformedByUserId == actor.Id));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExpiredOrArchivedExamCannotBeEnabledUntilItsRestrictionsAreResolved(bool expired)
    {
        await using var db = TestAppDbContextFactory.Create();
        var (exam, _, _, _, _, _, _) = await TestAppDbContextFactory.SeedEssayExamAsync(db);
        var product = new PublicExamProduct { Exam = exam, Slug = "restricted-activation", IsPublished = false };
        if (expired) product.AvailableUntil = DateTime.UtcNow.AddMinutes(-1);
        else exam.ArchiveMode = ContentArchiveMode.HiddenFromEveryone;
        db.PublicExamProducts.Add(product);
        await db.SaveChangesAsync();

        var response = await new SetPublicExamActivationCommandHandler(db).Handle(new(product.Id, true, Guid.NewGuid()), default);

        Assert.False(response.Success);
        Assert.False(product.IsPublished);
        Assert.Empty(await db.AuditLogs.Where(a => a.Action == "PublicExamActivationChanged").ToListAsync());
    }

    [Fact]
    public async Task DisablingRemainsAvailableEvenWhenTheSavedScheduleIsInvalid()
    {
        await using var db = TestAppDbContextFactory.Create();
        var (exam, _, _, _, _, _, _) = await TestAppDbContextFactory.SeedEssayExamAsync(db);
        var product = new PublicExamProduct
        {
            Exam = exam, Slug = "invalid-schedule", IsPublished = true,
            AvailableFrom = DateTime.UtcNow.AddDays(1), AvailableUntil = DateTime.UtcNow.AddDays(-1)
        };
        db.PublicExamProducts.Add(product);
        await db.SaveChangesAsync();

        var disabled = await new SetPublicExamActivationCommandHandler(db).Handle(new(product.Id, false, Guid.NewGuid()), default);

        Assert.True(disabled.Success, disabled.Message);
        Assert.False(disabled.Data!.IsPublished);
        Assert.NotNull(disabled.Data.DisabledAt);
    }
}
