using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Student;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Student.Queries;

public record GetStudentThemePreferencesQuery(Guid UserId) : IRequest<ApiResponse<StudentThemePreferencesDto>>;

public class GetStudentThemePreferencesQueryHandler : IRequestHandler<GetStudentThemePreferencesQuery, ApiResponse<StudentThemePreferencesDto>>
{
    private readonly IAppDbContext _db;

    public GetStudentThemePreferencesQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<StudentThemePreferencesDto>> Handle(GetStudentThemePreferencesQuery request, CancellationToken cancellationToken)
    {
        var profile = await _db.StudentProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == request.UserId, cancellationToken);

        var dto = StudentThemeCatalog.BuildPreferences(
            profile?.LightThemePaletteId,
            profile?.DarkThemePaletteId,
            profile?.CurrentMode
        );

        return ApiResponse<StudentThemePreferencesDto>.Ok(dto);
    }
}
