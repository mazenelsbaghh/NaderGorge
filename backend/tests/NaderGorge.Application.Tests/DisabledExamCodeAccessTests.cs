using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Codes.Commands;
using NaderGorge.Application.Features.Codes.Queries;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests;

public sealed class DisabledExamCodeAccessTests
{
    [Fact]
    public async Task DisabledDirectExamCode_IsRejectedWithoutRevealingTitleOrChangingState()
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await TestAppDbContextFactory.SeedUserAsync(
            db,
            "Disabled exam student",
            Guid.NewGuid().ToString("N")[..11]);
        var exam = CreateExam(isActive: false);
        var accessCode = CreateAccessCode(student.Id, exam);
        db.AddRange(exam, accessCode.CodeGroup, accessCode);
        await db.SaveChangesAsync();

        var validation = await new ValidateCodeQueryHandler(db)
            .Handle(new ValidateCodeQuery(accessCode.CodePlaintext, student.Id), CancellationToken.None);
        var activation = await new ActivateCodeCommandHandler(db, new FakeJobEnqueuer())
            .Handle(new ActivateCodeCommand(student.Id, accessCode.CodePlaintext), CancellationToken.None);

        Assert.False(validation.Success);
        Assert.Null(validation.Data);
        Assert.Contains("EXAM_UNAVAILABLE", validation.Errors ?? []);
        Assert.DoesNotContain(exam.Title, validation.Message ?? string.Empty);
        Assert.False(activation.Success);
        Assert.Contains("EXAM_UNAVAILABLE", activation.Errors ?? []);
        Assert.DoesNotContain(exam.Title, activation.Message ?? string.Empty);
        Assert.False(await db.AccessCodes.AsNoTracking()
            .Where(x => x.Id == accessCode.Id)
            .Select(x => x.IsConsumed)
            .SingleAsync());
        Assert.Empty(await db.StudentAccessGrants.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task AvailablePublicExamCodes_RemainRedeemable()
    {
        await using var db = TestAppDbContextFactory.Create();
        var fixture = await SeedPublicExamCodesAsync(db, PublicExamAvailabilityState.Available);
        var printableCodeStudent = await TestAppDbContextFactory.SeedUserAsync(
            db,
            "Available public exam printable student",
            Guid.NewGuid().ToString("N")[..11]);

        var validation = await new ValidateCodeQueryHandler(db)
            .Handle(new ValidateCodeQuery(fixture.AccessCode.CodePlaintext, fixture.Student.Id), CancellationToken.None);
        var activation = await new ActivateCodeCommandHandler(db, new FakeJobEnqueuer())
            .Handle(new ActivateCodeCommand(fixture.Student.Id, fixture.AccessCode.CodePlaintext), CancellationToken.None);
        var redemption = await new SalesRedemptionService(db).RedeemPrintableCodeAsync(
            printableCodeStudent.Id,
            Guid.NewGuid(),
            fixture.PrintableCode.CodePlaintext!,
            CancellationToken.None);

        Assert.True(validation.Success, validation.Message);
        Assert.Equal(fixture.Exam.Title, validation.Data?.TargetName);
        Assert.True(activation.Success, activation.Message);
        Assert.True(redemption.Success, redemption.Message);
        Assert.True(await db.AccessCodes.AsNoTracking()
            .Where(x => x.Id == fixture.AccessCode.Id)
            .Select(x => x.IsConsumed)
            .SingleAsync());
        Assert.Equal(1, await db.PrintableSalesCodes.AsNoTracking()
            .Where(x => x.Id == fixture.PrintableCode.Id)
            .Select(x => x.UsedCount)
            .SingleAsync());
        Assert.Equal(2, await db.StudentAccessGrants.AsNoTracking().CountAsync());
    }

    [Theory]
    [InlineData(PublicExamAvailabilityState.ExamInactive)]
    [InlineData(PublicExamAvailabilityState.Unpublished)]
    [InlineData(PublicExamAvailabilityState.Disabled)]
    [InlineData(PublicExamAvailabilityState.NotStarted)]
    [InlineData(PublicExamAvailabilityState.Ended)]
    public async Task UnavailablePublicExamCodes_AreRejectedWithoutConsumptionOrGrant(
        PublicExamAvailabilityState state)
    {
        await using var db = TestAppDbContextFactory.Create();
        var fixture = await SeedPublicExamCodesAsync(db, state);

        var validation = await new ValidateCodeQueryHandler(db)
            .Handle(new ValidateCodeQuery(fixture.AccessCode.CodePlaintext, fixture.Student.Id), CancellationToken.None);
        var activation = await new ActivateCodeCommandHandler(db, new FakeJobEnqueuer())
            .Handle(new ActivateCodeCommand(fixture.Student.Id, fixture.AccessCode.CodePlaintext), CancellationToken.None);
        var redemption = await new SalesRedemptionService(db).RedeemPrintableCodeAsync(
            fixture.Student.Id,
            Guid.NewGuid(),
            fixture.PrintableCode.CodePlaintext!,
            CancellationToken.None);

        Assert.False(validation.Success);
        Assert.Null(validation.Data);
        Assert.Contains("EXAM_UNAVAILABLE", validation.Errors ?? []);
        Assert.DoesNotContain(fixture.Exam.Title, validation.Message ?? string.Empty);
        Assert.False(activation.Success);
        Assert.Contains("EXAM_UNAVAILABLE", activation.Errors ?? []);
        Assert.DoesNotContain(fixture.Exam.Title, activation.Message ?? string.Empty);
        Assert.False(redemption.Success);
        Assert.DoesNotContain(fixture.Exam.Title, redemption.Message);

        Assert.False(await db.AccessCodes.AsNoTracking()
            .Where(x => x.Id == fixture.AccessCode.Id)
            .Select(x => x.IsConsumed)
            .SingleAsync());
        Assert.Equal(0, await db.PrintableSalesCodes.AsNoTracking()
            .Where(x => x.Id == fixture.PrintableCode.Id)
            .Select(x => x.UsedCount)
            .SingleAsync());
        Assert.Equal(SalesStatus.Active, await db.PrintableSalesCodes.AsNoTracking()
            .Where(x => x.Id == fixture.PrintableCode.Id)
            .Select(x => x.Status)
            .SingleAsync());
        Assert.Equal(0, await db.PrintableCodeBatches.AsNoTracking()
            .Where(x => x.Id == fixture.PrintableCode.BatchId)
            .Select(x => x.UsedCount)
            .SingleAsync());
        Assert.Empty(await db.StudentAccessGrants.AsNoTracking().ToListAsync());
        Assert.Empty(await db.PrintableCodeRedemptions.AsNoTracking().ToListAsync());
    }

    private static Exam CreateExam(bool isActive = true) => new()
    {
        Title = $"Hidden exam title {Guid.NewGuid():N}",
        Description = "Exam availability regression",
        PassingScore = 50,
        TotalScore = 100,
        IsActive = isActive,
        CreatedByTeacherId = Guid.NewGuid()
    };

    private static AccessCode CreateAccessCode(
        Guid createdByUserId,
        Exam exam,
        PublicExamProduct? publicExamProduct = null)
    {
        var group = new CodeGroup
        {
            Name = "Exam code availability regression",
            TotalCodes = 1,
            CodeType = CodeType.Exam,
            ExamId = exam.Id,
            PublicExamProductId = publicExamProduct?.Id,
            CreatedByUserId = createdByUserId
        };

        return new AccessCode
        {
            CodePlaintext = $"EXAM{Guid.NewGuid():N}"[..16],
            CodeHash = Guid.NewGuid().ToString("N"),
            CodeGroup = group,
            SerialNumber = 1
        };
    }

    private static async Task<PublicExamCodeFixture> SeedPublicExamCodesAsync(
        AppDbContext db,
        PublicExamAvailabilityState state)
    {
        var now = DateTime.UtcNow;
        var student = await TestAppDbContextFactory.SeedUserAsync(
            db,
            $"Public exam student {state}",
            Guid.NewGuid().ToString("N")[..11]);
        var exam = CreateExam(isActive: state != PublicExamAvailabilityState.ExamInactive);
        var product = new PublicExamProduct
        {
            ExamId = exam.Id,
            Exam = exam,
            Slug = $"unavailable-exam-{Guid.NewGuid():N}",
            IsPublished = state != PublicExamAvailabilityState.Unpublished,
            IsPaid = true,
            Price = 100,
            DisabledAt = state == PublicExamAvailabilityState.Disabled ? now.AddMinutes(-1) : null,
            AvailableFrom = state == PublicExamAvailabilityState.NotStarted ? now.AddHours(1) : now.AddHours(-1),
            AvailableUntil = state == PublicExamAvailabilityState.Ended ? now.AddMinutes(-1) : now.AddHours(2),
            CreatedByUserId = student.Id
        };
        var accessCode = CreateAccessCode(student.Id, exam, product);
        var printableCode = new PrintableSalesCode
        {
            CodePlaintext = $"PRINT{Guid.NewGuid():N}"[..16],
            SerialNumber = 1,
            QrPayload = "public-exam-code",
            UsageLimit = 1,
            Status = SalesStatus.Active
        };
        printableCode.CodeHash = DiscountEngine.HashCode(printableCode.CodePlaintext);
        printableCode.Batch = new PrintableCodeBatch
        {
            Name = "Unavailable public exam direct access",
            Behavior = PrintableCodeBehavior.DirectAccess,
            TargetType = SalesTargetType.PublicExam,
            TargetId = product.Id,
            OwnerType = SalesOwnerType.Platform,
            TotalCodes = 1,
            Status = SalesStatus.Active,
            CreatedByUserId = student.Id
        };

        db.AddRange(exam, product, accessCode.CodeGroup, accessCode, printableCode.Batch, printableCode);
        await db.SaveChangesAsync();
        return new PublicExamCodeFixture(student, exam, accessCode, printableCode);
    }

    public enum PublicExamAvailabilityState
    {
        Available,
        ExamInactive,
        Unpublished,
        Disabled,
        NotStarted,
        Ended
    }

    private sealed record PublicExamCodeFixture(
        User Student,
        Exam Exam,
        AccessCode AccessCode,
        PrintableSalesCode PrintableCode);
}
