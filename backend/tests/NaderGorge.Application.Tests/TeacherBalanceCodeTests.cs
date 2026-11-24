using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Features.Codes.Commands;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Tests;

public sealed class TeacherBalanceCodeTests
{
    [Theory]
    [InlineData("Teacher", false, false)]
    [InlineData("Platform", true, false)]
    [InlineData("wrong", false, false)]
    [InlineData("Teacher", true, true)]
    [InlineData("Platform", false, true)]
    public async Task Explicit_balance_scope_must_match_the_selected_teacher(string scope, bool selectTeacher, bool succeeds)
    {
        await using var db = TestAppDbContextFactory.Create();
        var admin = new User { FullName = "الإدارة", PhoneNumber = "01011111111", PasswordHash = "test" };
        admin.UserRoles.Add(new UserRole { User = admin, Role = new Role { Name = "Admin", Type = RoleType.Admin } });
        var teacher = new TeacherProfile { User = new User { FullName = "المدرس", PhoneNumber = "01022222222", PasswordHash = "test" } };
        db.AddRange(admin, teacher);
        await db.SaveChangesAsync();
        var response = await new BulkGenerateCodesCommandHandler(db, new TestAudit())
            .Handle(new("رصيد", CodeType.Balance, 1, 12, admin.Id, BalanceAmount: 100,
                TeacherId: selectTeacher ? teacher.Id : null, BalanceScope: scope), default);
        Assert.Equal(succeeds, response.Success);
        if (succeeds) Assert.Equal(selectTeacher ? teacher.Id : (Guid?)null, (await db.CodeGroups.SingleAsync()).TeacherId);
        else Assert.Empty(await db.AccessCodes.ToListAsync());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Activation_credits_the_scope_stored_on_the_code_and_returns_the_matching_destination(bool teacherScoped)
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "الطالب", "01011111111");
        var teacher = new TeacherProfile { User = new User { FullName = "المدرس", PhoneNumber = "01022222222", PasswordHash = "test" } };
        var code = new AccessCode { CodePlaintext = "TEST100CREDIT", CodeHash = "test-hash", SerialNumber = 10000001,
            CodeGroup = new CodeGroup { Name = "رصيد", CodeType = CodeType.Balance, BalanceAmount = 100,
                TeacherId = teacherScoped ? teacher.Id : null, CreatedByUserId = student.Id, TotalCodes = 1 } };
        db.AddRange(teacher, code);
        db.StudentBalances.Add(new StudentBalance { User = student, CurrentBalance = 25 });
        await db.SaveChangesAsync();
        var response = await new ActivateCodeCommandHandler(db, new FakeJobEnqueuer())
            .Handle(new(student.Id, code.CodePlaintext), default);
        Assert.True(response.Success, response.Message);
        db.ChangeTracker.Clear();
        Assert.True((await db.AccessCodes.SingleAsync()).IsConsumed);
        Assert.Equal(teacherScoped ? 25 : 125, (await db.StudentBalances.SingleAsync()).CurrentBalance);
        if (teacherScoped)
        {
            var allocation = await db.PromotionalBalanceAllocations.SingleAsync();
            Assert.Equal(teacher.Id, allocation.TeacherId);
            Assert.Equal(100, allocation.AvailableAmount);
            Assert.Equal($"/student/balance/recommendations/{allocation.Id}", response.Data!.RedirectUrl);
            Assert.Equal(0, await new PromotionalBalanceService(db).GetEligibleAmountAsync(student.Id, Guid.NewGuid()));
        }
        else
        {
            Assert.Empty(await db.PromotionalBalanceAllocations.ToListAsync());
            Assert.Equal("/student/balance", response.Data!.RedirectUrl);
        }
    }

    private sealed class TestAudit : IAuditService
    {
        public Task LogAsync(string action, string entityType, Guid? entityId, Guid? userId, object? oldValues = null,
            object? newValues = null, string? ipAddress = null, string? correlationId = null) => Task.CompletedTask;
    }
}
