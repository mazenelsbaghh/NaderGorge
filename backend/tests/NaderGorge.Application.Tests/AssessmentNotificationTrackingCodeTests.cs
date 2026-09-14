using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Assessments;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Services;

namespace NaderGorge.Application.Tests;

public sealed class AssessmentNotificationTrackingCodeTests
{
    [Theory]
    [InlineData("exam", "123456789", -365)]
    [InlineData("homework", "987654321", 0)]
    [InlineData("exam", null, -365)]
    public async Task ResultTemplateUsesStoredTrackingCodeAndRejectsMissingCode(string kind, string? trackingCode, int ageDays)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var student = new User { FullName = "Student", PhoneNumber = "01097000000", PasswordHash = "test", CreatedAt = DateTime.UtcNow.AddDays(ageDays) };
        student.StudentProfile = new StudentProfile { User = student, ParentTrackingCode = trackingCode };
        var template = new LiveSupportWhatsAppTemplate
        {
            MetaTemplateId = "tracking", Name = "result_tracking", Language = "ar", Category = "UTILITY", Status = "APPROVED",
            Fingerprint = new string('a', 64), ComponentsJson = """[{"type":"BODY","text":"الدرجة {{1}} ورقم المتابعة {{2}}"}]"""
        };
        db.AddRange(student, template);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var storedStudent = await db.Users.Include(user => user.StudentProfile).SingleAsync(user => user.Id == student.Id);
        var settings = new AssessmentParentNotificationSettings(true, template.Id, template.Fingerprint, [new("Score"), new("ParentTrackingCode")]);
        Assert.Null(await settings.ValidateAsync(db, CancellationToken.None));
        var result = new AssessmentParentResult(kind, Guid.NewGuid(), Guid.NewGuid(), storedStudent,
            settings, DateTime.UtcNow, "Result", 12, 13, "ممتاز", null);

        var parameters = await AssessmentParentResultReader.ParametersAsync(db, result, CancellationToken.None);

        Assert.Equal("12", parameters[0]);
        Assert.Equal(trackingCode ?? string.Empty, parameters[1]);
        Assert.Equal(trackingCode is not null, WhatsAppDirectTemplatePolicy.Validate(template, parameters) is not null);
    }
}
