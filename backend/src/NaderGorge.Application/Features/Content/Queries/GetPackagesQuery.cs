using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Content.Queries;

public record GetPackagesQuery(Guid UserId) : IRequest<ApiResponse<List<PackageDto>>>;

public record PackageDto(
    Guid Id, 
    string Name, 
    string Description, 
    decimal Price, 
    Guid ProgramId, 
    bool IsEnrolled, 
    Guid TeacherId, 
    Guid SubjectId,
    string TeacherName,
    string? TeacherProfileImageUrl,
    string SubjectName,
    string? TeacherBio,
    string? TeacherSpecialization
);

public class GetPackagesQueryHandler : IRequestHandler<GetPackagesQuery, ApiResponse<List<PackageDto>>>
{
    private readonly IAppDbContext _db;
    private readonly IAccessCheckService _access;

    public GetPackagesQueryHandler(IAppDbContext db, IAccessCheckService access)
    {
        _db = db;
        _access = access;
    }

    public async Task<ApiResponse<List<PackageDto>>> Handle(GetPackagesQuery request, CancellationToken ct)
    {
        Guid? teacherId = null;
        var user = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.TeacherProfile)
            .FirstOrDefaultAsync(u => u.Id == request.UserId, ct);

        if (user != null && user.UserRoles.Any(ur => ur.Role.Type == RoleType.Teacher))
        {
            teacherId = user.TeacherProfile?.Id;
        }

        var query = _db.Packages
            .Include(p => p.Program).ThenInclude(pr => pr.Subject)
            .Include(p => p.Teacher).ThenInclude(t => t.User)
            .AsQueryable();

        if (teacherId.HasValue)
        {
            query = query.Where(p => p.TeacherId == teacherId.Value);
        }

        var packages = await query.ToListAsync(ct);

        var dtos = new List<PackageDto>();
        foreach (var pk in packages)
        {
            var isEnrolled = await _access.HasAccessToPackageAsync(request.UserId, pk.Id, ct);
            dtos.Add(new PackageDto(
                pk.Id, 
                pk.Name, 
                pk.Description, 
                pk.Price, 
                pk.ProgramId, 
                isEnrolled, 
                pk.TeacherId, 
                pk.Program != null ? pk.Program.SubjectId : Guid.Empty,
                pk.Teacher?.User?.FullName ?? "Unknown",
                pk.Teacher?.ProfileImageUrl,
                pk.Program?.Subject?.Name ?? "Unknown",
                pk.Teacher?.Bio,
                pk.Teacher?.Specialization
            ));
        }

        return ApiResponse<List<PackageDto>>.Ok(dtos);
    }
}
