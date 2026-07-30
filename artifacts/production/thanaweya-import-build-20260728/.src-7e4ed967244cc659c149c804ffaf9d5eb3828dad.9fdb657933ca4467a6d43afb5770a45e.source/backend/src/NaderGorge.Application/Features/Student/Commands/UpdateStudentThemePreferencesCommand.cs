using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Student;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Student.Commands;

public record UpdateStudentThemePreferencesCommand(Guid UserId, string LightPaletteId, string DarkPaletteId, string CurrentMode, string? AvatarSlug)
    : IRequest<ApiResponse<StudentThemePreferencesDto>>;

public class UpdateStudentThemePreferencesCommandHandler : IRequestHandler<UpdateStudentThemePreferencesCommand, ApiResponse<StudentThemePreferencesDto>>
{
    private readonly IAppDbContext _db;

    public UpdateStudentThemePreferencesCommandHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<StudentThemePreferencesDto>> Handle(UpdateStudentThemePreferencesCommand request, CancellationToken cancellationToken)
    {
        var trimmedLight = request.LightPaletteId.Trim();
        var trimmedDark = request.DarkPaletteId.Trim();
        var trimmedMode = request.CurrentMode.Trim().ToLowerInvariant();

        if (!StudentThemeCatalog.IsValidLightPalette(trimmedLight))
        {
            return ApiResponse<StudentThemePreferencesDto>.Fail(
                "قيمة ثيم الوضع الفاتح غير صالحة.",
                new List<string> { "INVALID_LIGHT_THEME_PALETTE" }
            );
        }

        if (!StudentThemeCatalog.IsValidDarkPalette(trimmedDark))
        {
            return ApiResponse<StudentThemePreferencesDto>.Fail(
                "قيمة ثيم الوضع الداكن غير صالحة.",
                new List<string> { "INVALID_DARK_THEME_PALETTE" }
            );
        }

        if (trimmedMode is not ("light" or "dark"))
        {
            return ApiResponse<StudentThemePreferencesDto>.Fail(
                "قيمة وضع الثيم الحالية غير صالحة.",
                new List<string> { "INVALID_THEME_MODE" }
            );
        }

        var profile = await _db.StudentProfiles
            .FirstOrDefaultAsync(p => p.UserId == request.UserId, cancellationToken);

        if (profile == null)
        {
            profile = new StudentProfile
            {
                UserId = request.UserId,
                DateOfBirth = DateTime.UtcNow.Date,
                Governorate = string.Empty,
                Address = string.Empty,
                Gender = 0,
                EducationStage = 0,
                GradeLevel = 0,
                CurrentMode = trimmedMode,
            };
            _db.StudentProfiles.Add(profile);
        }

        var oldLight = profile.LightThemePaletteId;
        var oldDark = profile.DarkThemePaletteId;
        var oldMode = profile.CurrentMode;

        var oldAvatar = profile.AvatarSlug;

        profile.LightThemePaletteId = trimmedLight;
        profile.DarkThemePaletteId = trimmedDark;
        profile.CurrentMode = trimmedMode;
        profile.AvatarSlug = request.AvatarSlug;

        _db.AuditLogs.Add(new AuditLog
        {
            Action = "UpdateStudentThemePreferences",
            EntityType = nameof(StudentProfile),
            EntityId = profile.Id,
            PerformedByUserId = request.UserId,
            OldValues = $"LightThemePaletteId={oldLight ?? StudentThemeCatalog.DefaultLightPaletteId};DarkThemePaletteId={oldDark ?? StudentThemeCatalog.DefaultDarkPaletteId};CurrentMode={oldMode};AvatarSlug={oldAvatar}",
            NewValues = $"LightThemePaletteId={trimmedLight};DarkThemePaletteId={trimmedDark};CurrentMode={trimmedMode};AvatarSlug={request.AvatarSlug}",
        });

        await _db.SaveChangesAsync(cancellationToken);

        return ApiResponse<StudentThemePreferencesDto>.Ok(
            StudentThemeCatalog.BuildPreferences(profile.LightThemePaletteId, profile.DarkThemePaletteId, profile.CurrentMode),
            "تم حفظ تفضيلات الثيم بنجاح."
        );
    }
}
