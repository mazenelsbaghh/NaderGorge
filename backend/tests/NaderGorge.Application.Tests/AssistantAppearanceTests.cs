using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NaderGorge.API.Controllers;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests;

public sealed class AssistantAppearanceTests
{
    [Fact]
    public async Task AppearanceIsSavedForTheCurrentAssistantAndRejectsInvalidPalettes()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();

        var role = new Role { Name = "Custom course assistant", Type = RoleType.Staff, AllowedDomain = "assistant" };
        var assistant = Account("01010000001", role);
        var otherAssistant = Account("01010000002", role);
        var student = Account("01010000003", new Role { Name = "Student", Type = RoleType.Student, AllowedDomain = "student" });
        db.Users.AddRange(assistant, otherAssistant, student);
        await db.SaveChangesAsync();

        var endpoint = ForUser(db, assistant);
        Assert.Equal(new AssistantAppearanceController.AssistantAppearance(null, "massar-light", "massar-dark"),
            Read(await endpoint.Get(default)));

        var update = new AssistantAppearanceController.UpdateAppearance("oasis-light", "ember-dark", "light");
        Assert.IsType<OkObjectResult>(await endpoint.Update(update, default));
        Assert.IsType<OkObjectResult>(await endpoint.Update(update with { CurrentMode = "dark" }, default));
        Assert.IsType<BadRequestObjectResult>(await endpoint.Update(update with { LightPaletteId = "ember-dark" }, default));

        db.ChangeTracker.Clear();
        Assert.Equal(new AssistantAppearanceController.AssistantAppearance("dark", "oasis-light", "ember-dark"),
            Read(await ForUser(db, assistant).Get(default)));
        Assert.Null(Read(await ForUser(db, otherAssistant).Get(default)).CurrentMode);
        Assert.IsType<ForbidResult>(await ForUser(db, student).Update(update, default));
    }

    private static User Account(string phone, Role role) => new()
    {
        PhoneNumber = phone, FullName = "Test account", PasswordHash = "test",
        UserRoles = [new UserRole { Role = role }]
    };

    private static AssistantAppearanceController ForUser(AppDbContext db, User user) => new(db)
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())], "test"))
            }
        }
    };

    private static AssistantAppearanceController.AssistantAppearance Read(IActionResult response) =>
        Assert.IsType<ApiResponse<AssistantAppearanceController.AssistantAppearance>>(Assert.IsType<OkObjectResult>(response).Value).Data!;
}
