using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.API.Controllers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Assessments;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests;

public sealed class AssessmentNotificationAuthorizationTests
{
    [Theory]
    [InlineData(RoleType.Admin, true)]
    [InlineData(RoleType.Teacher, false)]
    [InlineData(RoleType.Student, false)]
    public async Task OnlyAdminCanEnableDisableOrChangeAssessmentWhatsApp(RoleType roleType, bool allowed)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var actor = new User { FullName = "Notification Editor", PhoneNumber = "01096000000", PasswordHash = "test" };
        db.Add(new UserRole { User = actor, Role = new Role { Name = roleType.ToString(), Type = roleType } });
        await db.SaveChangesAsync();
        var controller = new AssessmentReviewController(null!)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actor.Id.ToString())], "test"))
                }
            }
        };
        var examTemplates = await controller.ExamNotificationTemplates(db, CancellationToken.None);
        var homeworkTemplates = await controller.HomeworkNotificationTemplates(db, CancellationToken.None);
        Assert.Equal(allowed, examTemplates is OkObjectResult);
        Assert.Equal(allowed, homeworkTemplates is OkObjectResult);
        if (!allowed)
        {
            Assert.IsType<ForbidResult>(examTemplates);
            Assert.IsType<ForbidResult>(homeworkTemplates);
        }
        var disabled = AssessmentParentNotificationSettings.Disabled;
        var enabled = new AssessmentParentNotificationSettings(true, Guid.NewGuid(), "fingerprint", [new("StudentName")]);
        var changed = enabled with { Parameters = [new("ParentName")] };

        foreach (var (current, proposed) in new[] { (disabled, enabled), (enabled, disabled), (enabled, changed) })
        {
            var error = await proposed.AuthorizeChangeAsync(db, actor.Id, current, CancellationToken.None);
            Assert.Equal(allowed, error is null);
        }
        var unchanged = enabled with { Parameters = [new("StudentName")] };
        Assert.Null(await unchanged.AuthorizeChangeAsync(db, actor.Id, enabled, CancellationToken.None));
        Assert.Null(await disabled.AuthorizeChangeAsync(db, actor.Id, disabled, CancellationToken.None));
        Assert.NotNull(await enabled.AuthorizeChangeAsync(db, null, disabled, CancellationToken.None));
        actor.IsActive = false;
        await db.SaveChangesAsync();
        Assert.NotNull(await enabled.AuthorizeChangeAsync(db, actor.Id, disabled, CancellationToken.None));
    }
}
