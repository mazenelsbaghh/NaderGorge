using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Content;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Services.AdminAI.Reads;
using Npgsql;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace NaderGorge.Integration.Tests.AdminAI;

public sealed class AdminAIEntityReadPostgresTests
{
    [Fact]
    public async Task NormalizedEntitySearchAndSubscriberAggregate_RunOnPostgresIndexes()
    {
        await using var fixture = await PostgresAdminAIFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        await db.Database.MigrateAsync();

        var teacherUser = new User
        {
            Id = Guid.NewGuid(),
            FullName = "أَحـمَد نَادِر",
            PhoneNumber = "01000000001",
            PasswordHash = "test",
            IsActive = true
        };
        var teacher = new TeacherProfile
        {
            Id = Guid.NewGuid(),
            UserId = teacherUser.Id,
            User = teacherUser,
            Specialization = "فيزياء"
        };
        teacherUser.TeacherProfile = teacher;

        var studentRole = await db.Roles.SingleAsync(role => role.Type == RoleType.Student);
        var student = new User
        {
            Id = Guid.NewGuid(),
            FullName = "مُحَمَّـد عَلِى",
            PhoneNumber = "01000000002",
            PasswordHash = "test",
            IsActive = true,
            IsProfileComplete = true
        };
        var studentProfile = new StudentProfile
        {
            Id = Guid.NewGuid(),
            UserId = student.Id,
            User = student,
            StudentCode = "STـ-42",
            DateOfBirth = new DateTime(2008, 1, 1)
        };
        var studentUserRole = new UserRole
        {
            UserId = student.Id,
            User = student,
            RoleId = studentRole.Id,
            Role = studentRole
        };
        student.StudentProfile = studentProfile;
        student.UserRoles.Add(studentUserRole);

        var subject = new Subject
        {
            Id = Guid.NewGuid(),
            Name = "فيزياء",
            NormalizedName = "فيزياء"
        };
        var package = new Package
        {
            Id = Guid.NewGuid(),
            Name = "باكدج",
            SubjectId = subject.Id,
            Subject = subject,
            TeacherId = teacher.Id,
            Teacher = teacher
        };
        var grant = new StudentAccessGrant
        {
            Id = Guid.NewGuid(),
            UserId = student.Id,
            User = student,
            GrantType = CodeType.Package,
            PackageId = package.Id,
            IsActive = true,
            GrantedAt = DateTime.UtcNow
        };
        db.AddRange(teacherUser, teacher, student, studentProfile, studentUserRole, subject, package, grant);
        await db.SaveChangesAsync();

        var unrelatedTeacherUser = new User
        {
            Id = Guid.NewGuid(),
            FullName = "مدرس غير متعلق",
            PhoneNumber = "01000000003",
            PasswordHash = "test",
            IsActive = true
        };
        var unrelatedTeacher = new TeacherProfile
        {
            Id = Guid.NewGuid(),
            UserId = unrelatedTeacherUser.Id,
            User = unrelatedTeacherUser,
            Specialization = "رياضيات"
        };
        unrelatedTeacherUser.TeacherProfile = unrelatedTeacher;
        var unrelatedPackage = new Package
        {
            Id = Guid.NewGuid(),
            Name = "محتوى غير متعلق",
            SubjectId = subject.Id,
            Subject = subject,
            TeacherId = unrelatedTeacher.Id,
            Teacher = unrelatedTeacher
        };
        db.AddRange(unrelatedTeacherUser, unrelatedTeacher, unrelatedPackage);
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO student_access_grants
                ("Id", "UserId", "PackageId", "GrantType", "GrantedAt", "IsActive", "CreatedAt")
            SELECT gen_random_uuid(), {student.Id}, {unrelatedPackage.Id}, 0,
                   CURRENT_TIMESTAMP::timestamp, FALSE, CURRENT_TIMESTAMP::timestamp
            FROM generate_series(1, 20000)
            """);
        await db.Database.ExecuteSqlRawAsync("ANALYZE student_access_grants");

        var teacherProjection = await new AdminAITeacherSearchRead(db)
            .ExecuteAsync(Guid.NewGuid(), new { query = "مستر احمد ناد" }, default);
        var teacherSearch = Assert.IsType<AdminAITeacherSearchOutput>(teacherProjection.Data);
        Assert.Equal("unique", teacherSearch.Resolution);
        Assert.Equal(teacher.Id, teacherSearch.ResolvedTeacherId);

        var studentProjection = await new AdminAIStudentSearchRead(db)
            .ExecuteAsync(Guid.NewGuid(), new { query = "محمد علي" }, default);
        var studentSearch = Assert.IsType<AdminAIStudentSearchOutput>(studentProjection.Data);
        Assert.Equal("unique", studentSearch.Resolution);
        Assert.Equal(student.Id, studentSearch.ResolvedStudentId);

        var codeProjection = await new AdminAIStudentSearchRead(db)
            .ExecuteAsync(Guid.NewGuid(), new { query = "st-42" }, default);
        Assert.Equal(student.Id, Assert.IsType<AdminAIStudentSearchOutput>(codeProjection.Data).ResolvedStudentId);

        var summary = await new TeacherSubscriberFactSource(db)
            .SummarizeAsync(teacher.Id, DateTime.UtcNow, default);
        Assert.Equal(1, summary.Overall.Active.Total);
        Assert.Equal(1, summary.PackageHierarchy.Active.NonGift);

        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT count(*)
            FROM pg_indexes
            WHERE schemaname = 'public'
              AND indexname = ANY (ARRAY[
                'IX_users_admin_ai_normalized_name_trgm',
                'IX_student_profiles_admin_ai_normalized_code_trgm',
                'IX_sag_admin_ai_package_subscribers',
                'IX_sag_admin_ai_term_subscribers',
                'IX_sag_admin_ai_section_subscribers',
                'IX_sag_admin_ai_lesson_subscribers',
                'IX_sag_admin_ai_video_subscribers',
                'IX_sag_admin_ai_video_code_subscribers',
                'IX_sag_admin_ai_exam_subscribers',
                'IX_sag_admin_ai_public_exam_subscribers'
              ])
            """;
        Assert.Equal(10L, Convert.ToInt64(await command.ExecuteScalarAsync()));

        var asOf = DateTime.UtcNow;
        var summarySql = new TeacherSubscriberFactSource(db)
            .BuildSummaryQuery(teacher.Id, asOf)
            .ToQueryString();
        var plan = await ExplainAsync(connection, summarySql, teacher.Id, asOf);
        Assert.DoesNotContain(
            "Seq Scan on student_access_grants",
            plan,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IX_sag_admin_ai_", plan, StringComparison.Ordinal);
    }

    private static async Task<string> ExplainAsync(
        NpgsqlConnection connection,
        string querySql,
        Guid teacherId,
        DateTime asOf)
    {
        await using (var settings = connection.CreateCommand())
        {
            settings.CommandText = "SET statement_timeout = '5s'; SET enable_seqscan = off;";
            await settings.ExecuteNonQueryAsync();
        }

        var executableSql = string.Join(
            Environment.NewLine,
            querySql.Split('\n').Where(line => !line.TrimStart().StartsWith("--", StringComparison.Ordinal)));
        var declaredValues = Regex.Matches(
                querySql,
                "(?m)^-- @(?<name>__[A-Za-z0-9_]+)='(?<value>[^']*)'")
            .ToDictionary(
                match => match.Groups["name"].Value,
                match => match.Groups["value"].Value,
                StringComparer.Ordinal);
        await using var command = connection.CreateCommand();
        command.CommandText = $"EXPLAIN (ANALYZE, BUFFERS, FORMAT TEXT) {executableSql}";
        foreach (var parameterName in Regex.Matches(executableSql, "@(?<name>__[A-Za-z0-9_]+)")
                     .Select(match => match.Groups["name"].Value)
                     .Distinct(StringComparer.Ordinal))
        {
            if (parameterName.Contains("teacherId", StringComparison.OrdinalIgnoreCase))
                command.Parameters.AddWithValue(parameterName, teacherId);
            else if (parameterName.Contains("asOfUtc", StringComparison.OrdinalIgnoreCase))
                command.Parameters.AddWithValue(parameterName, asOf);
            else if (declaredValues.TryGetValue(parameterName, out var declared) &&
                     bool.TryParse(declared, out var boolean))
                command.Parameters.AddWithValue(parameterName, boolean);
            else if (declaredValues.TryGetValue(parameterName, out declared) &&
                     int.TryParse(declared, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
                command.Parameters.AddWithValue(parameterName, integer);
            else
                throw new InvalidOperationException($"Unexpected query parameter '{parameterName}'.");
        }

        var plan = new StringBuilder();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            plan.AppendLine(reader.GetString(0));
        return plan.ToString();
    }
}
