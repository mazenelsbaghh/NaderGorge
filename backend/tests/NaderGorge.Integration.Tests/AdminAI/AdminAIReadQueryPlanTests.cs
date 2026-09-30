using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Services.AdminAI.Reads;
using Npgsql;

namespace NaderGorge.Integration.Tests.AdminAI;

public sealed class AdminAIReadQueryPlanTests
{
    [Fact]
    public async Task StudentSearch_StaysBoundedAndUsesNameIndexAsPopulationGrows()
    {
        await using var fixture = await PostgresAdminAIFixture.CreateAsync();
        await using var seedDb = fixture.CreateDbContext();
        await seedDb.Database.MigrateAsync();
        var studentRole = await seedDb.Roles.SingleAsync(role => role.Type == RoleType.Student);

        var target = AddStudent(seedDb, studentRole, 0, "طالب مميز");
        await seedDb.SaveChangesAsync();
        seedDb.ChangeTracker.Clear();
        var small = await SearchAsync(fixture, "مميز");
        Assert.Equal(target, small.StudentId);
        Assert.InRange(small.QueryCount, 1, 2);

        for (var index = 1; index <= 2_000; index++)
            AddStudent(seedDb, studentRole, index, $"طالب تجريبي {index:D4}");
        await seedDb.SaveChangesAsync();
        seedDb.ChangeTracker.Clear();
        await seedDb.Database.ExecuteSqlRawAsync("ANALYZE users");

        var large = await SearchAsync(fixture, "مميز");
        Assert.Equal(target, large.StudentId);
        Assert.Equal(small.QueryCount, large.QueryCount);

        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using (var settings = connection.CreateCommand())
        {
            settings.CommandText = "SET statement_timeout = '5s'; SET enable_seqscan = off;";
            await settings.ExecuteNonQueryAsync();
        }
        await using var planCommand = connection.CreateCommand();
        planCommand.CommandText = """
            EXPLAIN (ANALYZE, BUFFERS)
            SELECT "Id" FROM users
            WHERE "IsDeleted" = FALSE
              AND massar_normalize_arabic("FullName") ILIKE '%مميز%'
            LIMIT 6
            """;
        var planLines = new List<string>();
        await using (var reader = await planCommand.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
                planLines.Add(reader.GetString(0));
        }
        Assert.Contains("IX_users_admin_ai_normalized_name_trgm", string.Join('\n', planLines));
    }

    private static Guid AddStudent(AppDbContext db, Role studentRole, int index, string name)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = name,
            PhoneNumber = $"010{index:D8}",
            PasswordHash = "test",
            IsActive = true
        };
        user.StudentProfile = new StudentProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            StudentCode = $"AI-{index:D6}",
            ParentTrackingCode = index.ToString("D6"),
            DateOfBirth = new DateTime(2008, 1, 1)
        };
        user.UserRoles.Add(new UserRole
        {
            UserId = user.Id,
            User = user,
            RoleId = studentRole.Id
        });
        db.Users.Add(user);
        return user.Id;
    }

    private static async Task<(Guid? StudentId, int QueryCount)> SearchAsync(
        PostgresAdminAIFixture fixture,
        string query)
    {
        var counter = new QueryCounter();
        await using var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(fixture.ConnectionString, options => options.CommandTimeout(5))
                .AddInterceptors(counter)
                .Options);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var projection = await new AdminAIStudentSearchRead(db)
            .ExecuteAsync(Guid.NewGuid(), new { query }, deadline.Token);
        var result = Assert.IsType<AdminAIStudentSearchOutput>(projection.Data);
        Assert.Equal("unique", result.Resolution);
        Assert.Single(result.Candidates);
        return (result.ResolvedStudentId, counter.Count);
    }

    private sealed class QueryCounter : DbCommandInterceptor
    {
        public int Count { get; private set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Count++;
            return ValueTask.FromResult(result);
        }
    }
}
