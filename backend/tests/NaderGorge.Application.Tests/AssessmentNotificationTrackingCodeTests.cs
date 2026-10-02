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
    [InlineData("StudentName")]
    [InlineData("ParentName")]
    public async Task September30ResultUsesFirstTwoNamesAndPreservesScore(string headerSource)
    {
        await using var db = TestAppDbContextFactory.Create();
        const string fullName = "أحمد محمد محمود عبد الرحمن إبراهيم مصطفى حسن علي";
        var template = new LiveSupportWhatsAppTemplate
        {
            Status = "APPROVED", Fingerprint = new string('a', 64),
            ComponentsJson = """[{"type":"HEADER","format":"TEXT","text":"مساء الخير ي ولي امر الطالب/الطالبة الطالب {{1}}"},{"type":"BODY","text":"الطالب {{1}} درجته {{2}} من {{3}}"}]"""
        };
        var settings = new AssessmentParentNotificationSettings(true, template.Id, template.Fingerprint,
            [new(headerSource), new("StudentName"), new("Score"), new("TotalScore")]);
        var result = new AssessmentParentResult("exam", Guid.NewGuid(), Guid.NewGuid(),
            new User { FullName = fullName }, settings, DateTime.UtcNow, "امتحان", 35, 40, "ممتاز", null);

        var parameters = await AssessmentParentResultReader.ParametersAsync(db, result, default, template);
        var validated = WhatsAppDirectTemplatePolicy.Validate(template, parameters);

        Assert.NotNull(validated);
        Assert.StartsWith(headerSource == "ParentName" ? "ولي أمر أحمد" : "أحمد", parameters[0]);
        Assert.Equal("أحمد محمد", parameters[1]);
        Assert.Equal("35", parameters[2]);
        Assert.Equal("40", parameters[3]);
        Assert.Contains("أحمد محمد", validated.Preview);
        Assert.DoesNotContain("محمود", validated.Preview);
    }

    [Theory]
    [InlineData(60, true)]
    [InlineData(61, false)]
    public void HydratedHeaderEnforcesMetaLimit(int length, bool accepted)
    {
        var template = new LiveSupportWhatsAppTemplate
        {
            Status = "APPROVED", Fingerprint = new string('a', 64),
            ComponentsJson = """[{"type":"HEADER","format":"TEXT","text":"{{1}}"},{"type":"BODY","text":"Result"}]"""
        };
        Assert.Equal(accepted, WhatsAppDirectTemplatePolicy.Validate(template, [new string('a', length)]) is not null);
    }

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
