using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Application.Features.Student.Commands;
using NaderGorge.Application.Interfaces.Finance;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Services;
using NaderGorge.Infrastructure.Services.Finance;
using NaderGorge.Integration.Tests.AdminAI;

namespace NaderGorge.Integration.Tests.LiveSupport;

public sealed class SupportCourseActionsPostgresTests
{
    [Theory]
    [InlineData(PackageContentMode.TermWithSections, CodeType.Package)]
    [InlineData(PackageContentMode.SectionWithLessons, CodeType.Term)]
    [InlineData(PackageContentMode.LessonsOnly, CodeType.Month)]
    [InlineData(PackageContentMode.SingleLesson, CodeType.Lesson)]
    public async Task Staff_purchase_uses_the_student_course_target_and_records_one_balanced_sale(PackageContentMode mode, CodeType expectedType)
    {
        await using var scenario = await Scenario.CreateAsync(mode);
        var preview = await scenario.Actions.PreviewCoursePurchaseAsync(scenario.Staff.Id, false, scenario.Conversation.Id, scenario.Package.Id, default);
        Assert.True(preview.Funding.IsSufficient);
        var request = scenario.PurchaseRequest(preview);

        var first = await scenario.Actions.ExecuteAsync(request, default);
        var replay = await scenario.Actions.ExecuteAsync(request, default);

        Assert.False(first.Replayed);
        Assert.True(replay.Replayed);
        await using var verify = scenario.Fixture.CreateDbContext();
        Assert.Equal(150m, (await verify.StudentBalances.SingleAsync()).CurrentBalance);
        Assert.Equal(expectedType, (await verify.StudentAccessGrants.SingleAsync()).GrantType);
        Assert.Equal(100m, (await verify.SalesFinancialEffects.SingleAsync()).PaidAmount);
        Assert.Equal(scenario.Staff.Id, (await verify.BalanceTransactions.SingleAsync()).PerformedByUserId);
        var journal = await verify.JournalEntries.Include(entry => entry.Lines).SingleAsync();
        Assert.Equal(scenario.Staff.Id, journal.ActorUserId);
        Assert.Equal(100m, journal.Lines.Sum(line => line.Debit));
        Assert.Equal(100m, journal.Lines.Sum(line => line.Credit));
        var execution = await verify.LiveSupportActionExecutions.SingleAsync();
        Assert.Equal(scenario.Staff.Id, execution.StaffUserId);
        Assert.Equal(scenario.Student.Id, execution.StudentUserId);
        Assert.Equal(LiveSupportActionStatus.Succeeded, execution.Status);
        Assert.DoesNotContain(await scenario.Actions.GetPurchaseCoursesAsync(scenario.Staff.Id, false, scenario.Conversation.Id, default), course => course.PackageId == scenario.Package.Id);
    }

    [Theory]
    [InlineData("price")]
    [InlineData("balance")]
    [InlineData("closed")]
    [InlineData("scope")]
    public async Task Changed_purchase_terms_require_a_new_review_without_debit(string change)
    {
        await using var scenario = await Scenario.CreateAsync();
        var preview = await scenario.Actions.PreviewCoursePurchaseAsync(scenario.Staff.Id, false, scenario.Conversation.Id, scenario.Package.Id, default);
        switch (change)
        {
            case "price": scenario.Package.Price = 120m; break;
            case "balance": (await scenario.Db.StudentBalances.SingleAsync()).CurrentBalance = 90m; break;
            case "closed": scenario.Package.AllowFullPackagePurchase = false; break;
            case "scope": (await scenario.Db.StudentProfiles.SingleAsync()).GradeLevel = GradeLevel.SecondaryGrade3; break;
        }
        await scenario.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<LiveSupportException>(() => scenario.Actions.ExecuteAsync(scenario.PurchaseRequest(preview), default));

        await scenario.AssertNoSaleAsync(change == "balance" ? 90m : 250m);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Only_balance_eligible_for_the_selected_teacher_can_fund_a_purchase(bool matchingTeacher)
    {
        await using var scenario = await Scenario.CreateAsync();
        (await scenario.Db.StudentBalances.SingleAsync()).CurrentBalance = 30m;
        var otherTeacher = new TeacherProfile { User = new User { FullName = "Other teacher", PhoneNumber = "01044444444", PasswordHash = "test" } };
        scenario.Db.Add(otherTeacher);
        await scenario.Db.SaveChangesAsync();
        await scenario.Services.GetRequiredService<BalanceService>().AddTeacherCredit(scenario.Student.Id,
            matchingTeacher ? scenario.Package.TeacherId : otherTeacher.Id, 70m, "Paid teacher credit", scenario.Staff.Id);
        var preview = await scenario.Actions.PreviewCoursePurchaseAsync(scenario.Staff.Id, false, scenario.Conversation.Id, scenario.Package.Id, default);

        Assert.Equal(matchingTeacher, preview.Funding.IsSufficient);
        if (matchingTeacher)
        {
            await scenario.Actions.ExecuteAsync(scenario.PurchaseRequest(preview), default);
            await using var verify = scenario.Fixture.CreateDbContext();
            Assert.Equal(0m, (await verify.StudentBalances.SingleAsync()).CurrentBalance);
            Assert.Equal(100m, (await verify.SalesFinancialEffects.SingleAsync()).PaidAmount);
            Assert.Equal(70m, (await verify.PromotionalBalanceUsages.SingleAsync()).Amount);
        }
        else
        {
            var failure = await Assert.ThrowsAsync<LiveSupportException>(() => scenario.Actions.ExecuteAsync(scenario.PurchaseRequest(preview), default));
            Assert.Equal("INSUFFICIENT_BALANCE", failure.Code);
            await scenario.AssertNoSaleAsync(30m);
        }
    }

    [Fact]
    public async Task Accounting_failure_rolls_back_the_sale_before_the_support_failure_receipt_is_saved()
    {
        await using var scenario = await Scenario.CreateAsync();
        scenario.Db.FinancialAccounts.Remove(await scenario.Db.FinancialAccounts.SingleAsync(account => account.Code == "4000"));
        await scenario.Db.SaveChangesAsync();
        var preview = await scenario.Actions.PreviewCoursePurchaseAsync(scenario.Staff.Id, false, scenario.Conversation.Id, scenario.Package.Id, default);

        await Assert.ThrowsAsync<InvalidOperationException>(() => scenario.Actions.ExecuteAsync(scenario.PurchaseRequest(preview), default));

        await scenario.AssertNoSaleAsync(250m);
        await using var verify = scenario.Fixture.CreateDbContext();
        Assert.Empty(await verify.TeacherFinancialEvents.ToArrayAsync());
        Assert.Equal(LiveSupportActionStatus.Failed, (await verify.LiveSupportActionExecutions.SingleAsync()).Status);
    }

    [Fact]
    public async Task Purchase_command_rejects_a_price_change_inside_its_own_transaction()
    {
        await using var scenario = await Scenario.CreateAsync();
        var result = await scenario.Services.GetRequiredService<IMediator>().Send(new PurchaseContentCommand(
            scenario.Student.Id, CodeType.Package, scenario.Package.Id, ExpectedPrice: 90m));
        Assert.Contains("PURCHASE_PRICE_CHANGED", result.Errors!);
        await scenario.AssertNoSaleAsync(250m);
    }

    [Fact]
    public async Task Staff_can_gift_a_sale_closed_compensation_course_once_without_changing_balance()
    {
        await using var scenario = await Scenario.CreateAsync();
        scenario.Package.IsActive = false;
        scenario.Package.AllowFullPackagePurchase = false;
        await scenario.Db.SaveChangesAsync();
        Assert.Contains(await scenario.Actions.GetGiftCoursesAsync(scenario.Staff.Id, false, scenario.Conversation.Id, default), course => course.Id == scenario.Package.Id);
        Assert.Empty(await scenario.Actions.GetPurchaseCoursesAsync(scenario.Staff.Id, false, scenario.Conversation.Id, default));
        var definition = (await scenario.Actions.GetCatalogAsync(scenario.Staff.Id, false, scenario.Conversation.Id, default)).Single(action => action.Key == "student.course.gift");
        var request = scenario.Request(definition.Key, definition.ConfirmationVersion, new { packageId = scenario.Package.Id, reason = "تعويض الطالب" });

        await scenario.Actions.ExecuteAsync(request, default);
        Assert.True((await scenario.Actions.ExecuteAsync(request, default)).Replayed);

        await using var verify = scenario.Fixture.CreateDbContext();
        Assert.Equal(250m, (await verify.StudentBalances.SingleAsync()).CurrentBalance);
        Assert.Single(await verify.StudentAccessGrants.ToArrayAsync());
        Assert.Equal(scenario.Staff.Id, (await verify.GiftIssuances.SingleAsync()).IssuedByUserId);
        Assert.Empty(await verify.SalesFinancialEffects.ToArrayAsync());
        Assert.Empty(await verify.BalanceTransactions.ToArrayAsync());
        Assert.False((await verify.Packages.SingleAsync()).IsActive);
        Assert.False((await verify.Packages.SingleAsync()).AllowFullPackagePurchase);
    }

    [Theory]
    [InlineData("scope")]
    [InlineData("archive")]
    [InlineData("owner")]
    [InlineData("unlinked")]
    [InlineData("checked-out")]
    public async Task Staff_gift_respects_student_scope_archiving_and_conversation_access(string denial)
    {
        await using var scenario = await Scenario.CreateAsync();
        var definition = (await scenario.Actions.GetCatalogAsync(scenario.Staff.Id, false, scenario.Conversation.Id, default)).Single(action => action.Key == "student.course.gift");
        switch (denial)
        {
            case "scope": (await scenario.Db.StudentProfiles.SingleAsync()).GradeLevel = GradeLevel.SecondaryGrade3; break;
            case "archive": scenario.Package.ArchiveMode = ContentArchiveMode.ActiveSubscribersOnly; break;
            case "owner": scenario.Conversation.CurrentOwnerUserId = scenario.Package.Teacher.UserId; break;
            case "unlinked": scenario.Conversation.LinkedStudentUserId = null; break;
            case "checked-out": (await scenario.Db.AttendanceLogs.SingleAsync()).ClockOut = DateTime.UtcNow; break;
        }
        await scenario.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<LiveSupportException>(() => scenario.Actions.ExecuteAsync(scenario.Request(definition.Key,
            definition.ConfirmationVersion, new { packageId = scenario.Package.Id, reason = "طلب هدية" }), default));

        await scenario.AssertNoSaleAsync(250m);
        await using var verify = scenario.Fixture.CreateDbContext();
        Assert.Empty(await verify.GiftIssuances.ToArrayAsync());
    }

    [Fact]
    public async Task Concurrent_staff_purchase_attempts_cannot_debit_the_student_twice()
    {
        await using var scenario = await Scenario.CreateAsync();
        await using var otherDb = scenario.Fixture.CreateDbContext();
        await using var otherServices = Scenario.BuildServices(otherDb);
        var presence = new ConnectedPresence();
        var otherActions = new LiveSupportActionService(otherDb, otherServices.GetRequiredService<IMediator>(),
            new LiveSupportService(otherDb, new EnabledSettings(), presence), presence);
        var first = await scenario.Actions.PreviewCoursePurchaseAsync(scenario.Staff.Id, false, scenario.Conversation.Id, scenario.Package.Id, default);
        var second = await otherActions.PreviewCoursePurchaseAsync(scenario.Staff.Id, false, scenario.Conversation.Id, scenario.Package.Id, default);

        var results = await Task.WhenAll(Attempt(scenario.Actions, scenario.PurchaseRequest(first)), Attempt(otherActions, scenario.PurchaseRequest(second)));

        Assert.Single(results, succeeded => succeeded);
        await using var verify = scenario.Fixture.CreateDbContext();
        Assert.Equal(150m, (await verify.StudentBalances.SingleAsync()).CurrentBalance);
        Assert.Single(await verify.StudentAccessGrants.ToArrayAsync());
        Assert.Single(await verify.BalanceTransactions.ToArrayAsync());
        Assert.Single(await verify.SalesFinancialEffects.ToArrayAsync());
        Assert.Single(await verify.JournalEntries.ToArrayAsync());

        static async Task<bool> Attempt(LiveSupportActionService actions, LiveSupportActionRequest request)
        {
            try { await actions.ExecuteAsync(request, default); return true; }
            catch (LiveSupportException) { return false; }
        }
    }

    private sealed class Scenario : IAsyncDisposable
    {
        public required PostgresAdminAIFixture Fixture { get; init; }
        public required AppDbContext Db { get; init; }
        public required ServiceProvider Services { get; init; }
        public required LiveSupportActionService Actions { get; init; }
        public required User Staff { get; init; }
        public required User Student { get; init; }
        public required Package Package { get; init; }
        public required LiveSupportConversation Conversation { get; init; }

        public static async Task<Scenario> CreateAsync(PackageContentMode mode = PackageContentMode.TermWithSections)
        {
            var fixture = await PostgresAdminAIFixture.CreateAsync();
            var db = fixture.CreateDbContext();
            try
            {
                await db.Database.MigrateAsync();
                var staff = new User { FullName = "Support operator", PhoneNumber = "01011111111", PasswordHash = "test", IsActive = true };
                var student = new User { FullName = "Course buyer", PhoneNumber = "01022222222", PasswordHash = "test", IsActive = true };
                var teacher = new TeacherProfile { User = new User { FullName = "Course teacher", PhoneNumber = "01033333333", PasswordHash = "test" }, IsContentVisibleToStudents = true, CommissionRate = 25m };
                var subject = new Subject { Name = "History", NormalizedName = "HISTORY" };
                var package = new Package { Name = "Support course", Description = "Synthetic course", Teacher = teacher, Subject = subject, Price = 100m, ContentMode = mode, TargetGrade = "SecondSecondary" };
                var term = new Term { Package = package, Title = "Term", IsSystemContainer = true, Price = 100m };
                var section = new ContentSection { Term = term, Title = "Month", IsSystemContainer = true, Price = 100m };
                var employee = new EmployeeProfile { User = staff, BasicSalary = 1m };
                var conversation = new LiveSupportConversation { ParticipantType = LiveSupportParticipantType.Student, StudentUserId = student.Id,
                    LinkedStudentUserId = student.Id, CurrentOwnerUserId = staff.Id, Status = LiveSupportConversationStatus.Active };
                db.AddRange(staff, student, teacher, subject, package, term, section, employee, conversation,
                    new Lesson { ContentSection = section, Title = "Root lesson", Price = 100m },
                    new UserRole { User = student, Role = await db.Roles.SingleAsync(role => role.Name == "Student") },
                    new StudentProfile { User = student, EducationStage = EducationStage.Secondary, GradeLevel = GradeLevel.SecondSecondary },
                    new AcademicSubjectEligibility { Subject = subject, EducationStage = EducationStage.Secondary, GradeLevel = GradeLevel.SecondSecondary, IsActive = true },
                    new StudentFacingAcademicScope { OwnerType = StudentFacingScopeOwnerType.Package, OwnerId = package.Id, ScopeLevel = AcademicScopeLevel.GradeAllSubjects, EducationStage = EducationStage.Secondary, GradeLevel = GradeLevel.SecondSecondary },
                    new StudentBalance { User = student, CurrentBalance = 250m },
                    new LiveSupportStaffConfig { UserId = staff.Id, IsEnabled = true, MaxActiveConversations = 5, ConfiguredByUserId = staff.Id },
                    new AttendanceLog { Employee = employee, ClockIn = DateTime.UtcNow, Date = DateOnly.FromDateTime(DateTime.UtcNow), Status = AttendanceStatus.Present, IpAddress = "test", UserAgent = "test" });
                foreach (var code in new[] { "1100", "1110", "2000", "4000" })
                    db.FinancialAccounts.Add(new FinancialAccount { Code = code, Name = code, Type = code == "4000" ? FinancialAccountType.Revenue : FinancialAccountType.Liability, NormalSide = FinancialNormalSide.Credit });
                await db.SaveChangesAsync();
                var services = BuildServices(db);
                var presence = new ConnectedPresence();
                var support = new LiveSupportService(db, new EnabledSettings(), presence);
                return new() { Fixture = fixture, Db = db, Services = services, Staff = staff, Student = student, Package = package,
                    Conversation = conversation, Actions = new(db, services.GetRequiredService<IMediator>(), support, presence) };
            }
            catch
            {
                await db.DisposeAsync();
                await fixture.DisposeAsync();
                throw;
            }
        }

        public static ServiceProvider BuildServices(AppDbContext db) =>
            new ServiceCollection().AddLogging().AddSingleton<IAppDbContext>(db)
                    .AddSingleton<BalanceService>().AddSingleton<IPromotionalBalanceService, PromotionalBalanceService>()
                    .AddSingleton<ISalesTargetResolver, SalesTargetResolver>().AddSingleton<IDiscountEngine, DiscountEngine>()
                    .AddSingleton<IAccessCheckService, AccessCheckService>().AddSingleton<IAcademicScopeService, AcademicScopeService>()
                    .AddSingleton<IFinancialPostingService, FinancialPostingService>()
                    .AddMediatR(config => config.RegisterServicesFromAssembly(typeof(ApiResponse).Assembly)).BuildServiceProvider();

        public LiveSupportActionRequest PurchaseRequest(LiveSupportPurchasePreviewDto preview)
            => Request("student.course.purchase", preview.ConfirmationVersion, new { packageId = Package.Id, quoteVersion = preview.QuoteVersion, reason = "طلب الطالب" });

        public LiveSupportActionRequest Request(string key, string version, object payload)
            => new(Staff.Id, false, Conversation.Id, key, Guid.NewGuid().ToString(), version, JsonSerializer.SerializeToElement(payload));

        public async Task AssertNoSaleAsync(decimal balance)
        {
            await using var verify = Fixture.CreateDbContext();
            Assert.Equal(balance, (await verify.StudentBalances.SingleAsync()).CurrentBalance);
            Assert.Empty(await verify.StudentAccessGrants.ToArrayAsync());
            Assert.Empty(await verify.SalesFinancialEffects.ToArrayAsync());
            Assert.Empty(await verify.BalanceTransactions.ToArrayAsync());
            Assert.Empty(await verify.JournalEntries.ToArrayAsync());
        }

        public async ValueTask DisposeAsync() { await Services.DisposeAsync(); await Db.DisposeAsync(); await Fixture.DisposeAsync(); }
    }

    private sealed class EnabledSettings : ICachedPlatformSettingsReader
    {
        public Task<CachedPlatformSettings> GetAsync(CancellationToken ct) => Task.FromResult(CachedPlatformSettings.Default with { LiveSupportEnabled = true });
        public void Invalidate() { }
    }

    private sealed class ConnectedPresence : ILiveSupportPresenceStore
    {
        public Task ConnectedAsync(Guid staffUserId, string connectionId) => Task.CompletedTask;
        public Task DisconnectedAsync(Guid staffUserId, string connectionId) => Task.CompletedTask;
        public Task HeartbeatAsync(Guid staffUserId) => Task.CompletedTask;
        public Task<bool> IsConnectedAsync(Guid staffUserId) => Task.FromResult(true);
        public Task<IReadOnlyList<Guid>> ClaimExpiredDisconnectsAsync(DateTime utcNow) => Task.FromResult<IReadOnlyList<Guid>>([]);
    }
}
