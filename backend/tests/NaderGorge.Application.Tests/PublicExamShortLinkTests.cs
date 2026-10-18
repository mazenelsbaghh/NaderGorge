using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.Sales;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests;

public sealed class PublicExamShortLinkTests
{
    [Fact]
    public async Task ShortSlugResolvesOnlyItsPublishedExamForAnEligibleAccount()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var owner = new User { FullName = "Teacher", PhoneNumber = "72001", PasswordHash = "test" };
        var student = new User { FullName = "Student", PhoneNumber = "72002", PasswordHash = "test" };
        var teacher = new TeacherProfile { User = owner, Specialization = "History", ContactInfo = "test" };
        var exam = new Exam { CreatedByTeacher = teacher, Title = "Short link exam" };
        var product = new PublicExamProduct { Exam = exam, Slug = "shamel1", IsPublished = true, CreatedByUser = owner };
        var profile = new StudentProfile { User = student, EducationStage = EducationStage.Secondary, GradeLevel = GradeLevel.FirstSecondary };
        db.AddRange(product, profile, new StudentFacingAcademicScope
        {
            OwnerType = StudentFacingScopeOwnerType.PublicExamProduct, OwnerId = product.Id,
            ScopeLevel = AcademicScopeLevel.GradeAllSubjects, EducationStage = EducationStage.Secondary,
            GradeLevel = GradeLevel.FirstSecondary
        });
        await db.SaveChangesAsync();
        var handler = new GetPublicExamProductsQueryHandler(db, new AcademicScopeService(db));
        var request = new GetPublicExamProductsQuery(PublishedOnly: true, StudentId: student.Id, Slug: "shamel1");

        Assert.Equal(exam.Id, Assert.Single((await handler.Handle(request, default)).Data!).ExamId);
        Assert.Empty((await handler.Handle(request with { Slug = "missing" }, default)).Data!);
        profile.GradeLevel = GradeLevel.SecondSecondary;
        await db.SaveChangesAsync();
        Assert.Empty((await handler.Handle(request, default)).Data!);
        profile.GradeLevel = GradeLevel.FirstSecondary;
        product.IsPublished = false;
        await db.SaveChangesAsync();
        Assert.Empty((await handler.Handle(request, default)).Data!);
        product.IsPublished = true;
        product.DisabledAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        Assert.Empty((await handler.Handle(request, default)).Data!);
        product.DisabledAt = null;
        product.AvailableUntil = DateTime.UtcNow.AddHours(-1);
        await db.SaveChangesAsync();
        Assert.Empty((await handler.Handle(request, default)).Data!);
        Assert.Empty(await db.StudentExamAttempts.ToListAsync());
        Assert.Empty(await db.StudentAccessGrants.ToListAsync());
    }
}
