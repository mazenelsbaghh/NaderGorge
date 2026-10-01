using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NaderGorge.API.Controllers;
using NaderGorge.Application.Common;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Services.Finance;
using NaderGorge.Integration.Tests.AdminAI;

namespace NaderGorge.Integration.Tests.Finance;

internal sealed class RefundTestScenario : IAsyncDisposable
{
    private readonly PostgresAdminAIFixture _fixture;
    private ServiceProvider? _services;

    private RefundTestScenario(PostgresAdminAIFixture fixture)
    {
        _fixture = fixture;
        Db = fixture.CreateDbContext();
    }

    public AppDbContext Db { get; }
    public string ConnectionString => _fixture.ConnectionString;
    public User Student { get; } = new() { FullName = "Refund safety student", PhoneNumber = "01000000061", PasswordHash = "test", IsActive = true };
    public User Actor { get; } = new() { FullName = "Refund safety operator", PhoneNumber = "01000000062", PasswordHash = "test", IsActive = true };
    public TeacherProfile Teacher { get; private set; } = null!;
    public StudentAccessGrant Grant { get; private set; } = null!;
    public TreasuryAccount Cashbox { get; private set; } = null!;
    public Guid PurchaseId { get; } = Guid.NewGuid();
    public AppDbContext CreateDbContext() => _fixture.CreateDbContext();

    public static async Task<RefundTestScenario> CreateAsync()
    {
        var scenario = new RefundTestScenario(await PostgresAdminAIFixture.CreateAsync());
        try
        {
            await scenario.Db.Database.MigrateAsync();
            await scenario.SeedAsync();
            return scenario;
        }
        catch
        {
            await scenario.DisposeAsync();
            throw;
        }
    }

    private async Task SeedAsync()
    {
        var teacherUser = new User { FullName = "Refund safety teacher", PhoneNumber = "01000000063", PasswordHash = "test" };
        Teacher = new TeacherProfile { User = teacherUser };
        var subject = new Subject { Name = "Refund safety", NormalizedName = "refund safety" };
        var package = new Package { Name = "Refund safety package", Description = "Refund safety fixture", Price = 490m, Subject = subject, Teacher = Teacher, TargetGrade = "SecondSecondary" };
        Grant = new StudentAccessGrant { User = Student, GrantType = CodeType.Package, PackageId = package.Id, IsActive = true };
        var cash = new FinancialAccount { Code = "1000", Name = "Cash", Type = FinancialAccountType.Asset, NormalSide = FinancialNormalSide.Debit, Role = FinancialAccountRole.Treasury };
        Cashbox = new TreasuryAccount { Name = "Refund cashbox", Type = TreasuryAccountType.Cashbox, FinancialAccountId = cash.Id };
        Db.AddRange(Student, Actor, teacherUser, Teacher, subject, package, Grant, cash, Cashbox,
            new StudentBalance { UserId = Student.Id, CurrentBalance = 20m },
            new FinancialAccount { Code = "1100", Name = "Balance", Type = FinancialAccountType.Liability, NormalSide = FinancialNormalSide.Credit, Role = FinancialAccountRole.GeneralStudentLiability },
            new FinancialAccount { Code = "2000", Name = "Teacher payable", Type = FinancialAccountType.Liability, NormalSide = FinancialNormalSide.Credit, Role = FinancialAccountRole.TeacherPayable },
            new FinancialAccount { Code = "4100", Name = "Refunds", Type = FinancialAccountType.ContraRevenue, NormalSide = FinancialNormalSide.Debit, Role = FinancialAccountRole.Refunds },
            new SalesFinancialEffect { PurchaseOperationId = PurchaseId, StudentId = Student.Id, TeacherId = Teacher.Id, TargetType = SalesTargetType.Package, TargetId = package.Id, PaidAmount = 490m, GrossAmount = 490m, TeacherShareImpact = 120m, PlatformShareImpact = 370m });
        await Db.SaveChangesAsync();
    }

    public ExternalPackageRefundBody CashRequest(decimal amount = 290m) =>
        new(Grant.Id, PurchaseId, Student.Id, Teacher.Id, amount, 0m, Cashbox.Id, "طلب رد المبلغ", "REFUND-SAFETY");

    public PlatformFinanceOperationsService Operations() => new(Db, new FinancialPostingService(Db),
        new BalanceService(Db, NullLogger<BalanceService>.Instance));

    public AdminPlatformFinanceController Controller()
    {
        _services ??= new ServiceCollection().AddSingleton<IAppDbContext>(Db)
            .AddSingleton<TeacherAccountingService>()
            .AddMediatR(config => config.RegisterServicesFromAssembly(typeof(ApiResponse).Assembly))
            .BuildServiceProvider();
        return new(null!, null!, Operations(), null!, null!, null!, Db, null!, null!, null!, null!, _services.GetRequiredService<IMediator>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Actor.Id.ToString())], "test"))
                }
            }
        };
    }

    public async ValueTask DisposeAsync()
    {
        _services?.Dispose();
        await Db.DisposeAsync();
        await _fixture.DisposeAsync();
    }
}
