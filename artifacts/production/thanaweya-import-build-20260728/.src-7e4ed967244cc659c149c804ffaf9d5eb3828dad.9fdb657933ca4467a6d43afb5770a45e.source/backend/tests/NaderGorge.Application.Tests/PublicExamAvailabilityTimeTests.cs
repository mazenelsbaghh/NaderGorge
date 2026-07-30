using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Admin.Sales;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests;

public sealed class PublicExamAvailabilityTimeTests
{
    [Theory]
    [InlineData("2026-07-22T12:16:00", "2026-07-22T09:16:00Z")]
    [InlineData("2026-12-22T12:16:00", "2026-12-22T10:16:00Z")]
    public void CairoLocalTime_ConvertsToCorrectUtcAcrossDaylightSaving(string localTimestamp, string utcTimestamp)
    {
        var localTime = DateTime.SpecifyKind(DateTime.Parse(localTimestamp), DateTimeKind.Unspecified);

        var utcTime = CairoTime.ToUtc(localTime);

        Assert.Equal(DateTime.Parse(utcTimestamp).ToUniversalTime(), utcTime);
    }

    [Fact]
    public async Task July2026ProductionRegression_CreateExamStoresCairoAvailabilityAsUtc()
    {
        await using var db = TestAppDbContextFactory.Create();
        var (teacher, subject) = await SeedTeacherAndSubjectAsync(db);
        var request = CreateRequest(
            teacher.Id,
            subject.Id,
            (new DateTime(2026, 7, 22, 12, 16, 0, DateTimeKind.Unspecified),
             new DateTime(2026, 7, 22, 14, 16, 0, DateTimeKind.Unspecified)),
            isPublished: false);

        var response = await new CreatePublicExamProductCommandHandler(db)
            .Handle(new CreatePublicExamProductCommand(request, teacher.UserId), default);

        Assert.True(response.Success, response.Message);
        var product = await db.PublicExamProducts.SingleAsync();
        Assert.Equal(new DateTime(2026, 7, 22, 9, 16, 0, DateTimeKind.Utc), product.AvailableFrom);
        Assert.Equal(new DateTime(2026, 7, 22, 11, 16, 0, DateTimeKind.Utc), product.AvailableUntil);
    }

    [Fact]
    public async Task PublishedExamWithExpiredAvailability_IsRejected()
    {
        await using var db = TestAppDbContextFactory.Create();
        var (teacher, subject) = await SeedTeacherAndSubjectAsync(db);
        var request = CreateRequest(
            teacher.Id,
            subject.Id,
            (DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(-1)),
            isPublished: true);

        var response = await new CreatePublicExamProductCommandHandler(db)
            .Handle(new CreatePublicExamProductCommand(request, teacher.UserId), default);

        Assert.False(response.Success);
        Assert.Empty(await db.PublicExamProducts.ToListAsync());
    }

    private static CreatePublicExamRequest CreateRequest(
        Guid teacherId,
        Guid subjectId,
        (DateTime? From, DateTime? Until) availability,
        bool isPublished) => new(
            "امتحان توقيت القاهرة",
            "اختبار انحدار",
            $"cairo-time-{Guid.NewGuid():N}",
            teacherId,
            subjectId,
            "FirstSecondary",
            isPublished,
            false,
            0,
            5,
            10,
            30,
            false,
            availability.From,
            availability.Until,
            [new AcademicScopeDto(AcademicScopeLevel.Exact, EducationStage.Secondary, GradeLevel.FirstSecondary, subjectId)]);

    private static async Task<(TeacherProfile Teacher, Subject Subject)> SeedTeacherAndSubjectAsync(AppDbContext db)
    {
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Public Exam Teacher", Guid.NewGuid().ToString("N")[..11]);
        var subject = new Subject { Name = "تاريخ", NormalizedName = Guid.NewGuid().ToString("N"), Description = "تاريخ" };
        var teacher = new TeacherProfile { UserId = user.Id, Bio = "Bio", Specialization = "History", CommissionRate = 0.2m, ContactInfo = "contact" };
        db.TeacherSubjects.Add(new TeacherSubject { Teacher = teacher, Subject = subject });
        db.AcademicSubjectEligibilities.Add(new AcademicSubjectEligibility
        {
            EducationStage = EducationStage.Secondary,
            GradeLevel = GradeLevel.FirstSecondary,
            Subject = subject,
        });
        await db.SaveChangesAsync();
        return (teacher, subject);
    }
}
