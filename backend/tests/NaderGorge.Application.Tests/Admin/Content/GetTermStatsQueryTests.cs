using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.Content.Queries;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests.Admin.Content;

public sealed class GetTermStatsQueryTests
{
    [Fact]
    public async Task EnrolledStudents_CountsDistinctEffectivePackageAndTermAccess()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();

        var teacherUser = NewUser("Teacher", 0);
        var teacher = new TeacherProfile { Id = Guid.NewGuid(), User = teacherUser };
        var subject = new Subject { Id = Guid.NewGuid(), Name = "Physics", NormalizedName = "PHYSICS" };
        var package = new Package { Id = Guid.NewGuid(), Name = "Grade 12", Subject = subject, Teacher = teacher };
        var term = new Term { Id = Guid.NewGuid(), Title = "Term 1", Package = package };
        var otherTerm = new Term { Id = Guid.NewGuid(), Title = "Term 2", Package = package };
        var students = Enumerable.Range(1, 6)
            .Select(index => NewUser($"Student {index}", index))
            .ToArray();
        var now = DateTime.UtcNow;
        var expiredGrant = TermGrant(students[2], term.Id);
        expiredGrant.ExpiresAt = now.AddMinutes(-1);
        var cancelledGrant = PackageGrant(students[3], package.Id);
        cancelledGrant.CancelledAt = now;
        var inactiveGrant = TermGrant(students[4], term.Id);
        inactiveGrant.IsActive = false;

        db.AddRange(teacher, subject, package, term, otherTerm);
        db.StudentAccessGrants.AddRange(
            TermGrant(students[0], term.Id),
            PackageGrant(students[0], package.Id),
            PackageGrant(students[1], package.Id),
            expiredGrant,
            cancelledGrant,
            inactiveGrant,
            TermGrant(students[5], otherTerm.Id));
        await db.SaveChangesAsync();

        var response = await new GetTermStatsQueryHandler(db)
            .Handle(new GetTermStatsQuery(term.Id), CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(2, response.Data!.EnrolledStudentsCount);
    }

    private static User NewUser(string fullName, int phoneSuffix) => new()
    {
        Id = Guid.NewGuid(),
        FullName = fullName,
        PhoneNumber = $"01089{phoneSuffix:000000}",
        PasswordHash = "test-hash"
    };

    private static StudentAccessGrant PackageGrant(User student, Guid packageId) => new()
    {
        Id = Guid.NewGuid(),
        User = student,
        GrantType = CodeType.Package,
        PackageId = packageId,
        IsActive = true
    };

    private static StudentAccessGrant TermGrant(User student, Guid termId) => new()
    {
        Id = Guid.NewGuid(),
        User = student,
        GrantType = CodeType.Term,
        TermId = termId,
        IsActive = true
    };
}
