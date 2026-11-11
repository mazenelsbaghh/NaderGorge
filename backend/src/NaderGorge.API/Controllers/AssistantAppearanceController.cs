using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Student;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/assistant/theme-preferences")]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AssistantAppearanceController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var userId = User.RequireUserId();
        if (!await CanCustomizeAsync(userId, ct)) return Forbid();

        var preference = await db.AssistantAppearancePreferences.AsNoTracking()
            .SingleOrDefaultAsync(row => row.UserId == userId, ct);
        var colors = StudentThemeCatalog.BuildPreferences(preference?.LightPaletteId,
            preference?.DarkPaletteId, preference?.CurrentMode, avatarSlug: null);
        return AppearanceResponse(preference is null ? null : colors.CurrentMode,
            colors.SelectedLightPaletteId, colors.SelectedDarkPaletteId);
    }

    [HttpPut]
    public async Task<IActionResult> Update(UpdateAppearance request, CancellationToken ct)
    {
        if (request.CurrentMode is not ("light" or "dark") ||
            !StudentThemeCatalog.IsValidLightPalette(request.LightPaletteId) ||
            !StudentThemeCatalog.IsValidDarkPalette(request.DarkPaletteId))
            return BadRequest(ApiResponse<AssistantAppearance>.Fail("اختيارات الألوان غير صالحة."));

        var userId = User.RequireUserId();
        if (!await CanCustomizeAsync(userId, ct)) return Forbid();

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO assistant_appearance_preferences ("UserId", "LightPaletteId", "DarkPaletteId", "CurrentMode")
            VALUES ({userId}, {request.LightPaletteId}, {request.DarkPaletteId}, {request.CurrentMode})
            ON CONFLICT ("UserId") DO UPDATE SET
                "LightPaletteId" = excluded."LightPaletteId",
                "DarkPaletteId" = excluded."DarkPaletteId",
                "CurrentMode" = excluded."CurrentMode"
            """, ct);
        return AppearanceResponse(request.CurrentMode, request.LightPaletteId, request.DarkPaletteId);
    }

    private Task<bool> CanCustomizeAsync(Guid userId, CancellationToken ct) =>
        db.Users.AnyAsync(user => user.Id == userId && user.IsActive && !user.IsDeleted &&
            user.UserRoles.Any(membership => membership.Role.AllowedDomain == "assistant" ||
                (membership.Role.AllowedDomain == "all" && membership.Role.Type != RoleType.Student &&
                    membership.Role.Type != RoleType.Teacher)), ct);

    private OkObjectResult AppearanceResponse(string? mode, string lightPaletteId, string darkPaletteId) =>
        Ok(ApiResponse<AssistantAppearance>.Ok(new(mode, lightPaletteId, darkPaletteId)));

    public sealed record UpdateAppearance(string LightPaletteId, string DarkPaletteId, string CurrentMode);
    public sealed record AssistantAppearance(string? CurrentMode, string SelectedLightPaletteId, string SelectedDarkPaletteId);
}
