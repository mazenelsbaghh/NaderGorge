using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Queries;

public record GetAdminProgramsQuery(Guid? CurrentUserId = null) : IRequest<ApiResponse<List<AdminProgramDto>>>;

public record AdminProgramDto(Guid Id, string Name, string Description, string TargetGrade, Guid SubjectId, string SubjectName);

public class GetAdminProgramsQueryHandler : IRequestHandler<GetAdminProgramsQuery, ApiResponse<List<AdminProgramDto>>>
{
    private readonly IAppDbContext _context;

    public GetAdminProgramsQueryHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<List<AdminProgramDto>>> Handle(GetAdminProgramsQuery request, CancellationToken cancellationToken)
    {
        Guid? teacherId = null;
        if (request.CurrentUserId.HasValue)
        {
            var user = await _context.Users
                .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .Include(u => u.TeacherProfile)
                .FirstOrDefaultAsync(u => u.Id == request.CurrentUserId.Value, cancellationToken);

            if (user != null && user.UserRoles.Any(ur => ur.Role.Type == RoleType.Teacher))
            {
                teacherId = user.TeacherProfile?.Id;
            }
        }

        var query = _context.Programs
            .Include(p => p.Subject)
            .AsQueryable();

        if (teacherId.HasValue)
        {
            var taughtSubjectIds = await _context.TeacherSubjects
                .Where(ts => ts.TeacherId == teacherId.Value)
                .Select(ts => ts.SubjectId)
                .ToListAsync(cancellationToken);

            query = query.Where(p => taughtSubjectIds.Contains(p.SubjectId));
        }

        var programs = await query
            .OrderBy(p => p.Name)
            .Select(p => new AdminProgramDto(
                p.Id,
                p.Name,
                p.Description,
                p.TargetGrade,
                p.SubjectId,
                p.Subject != null ? p.Subject.Name : string.Empty
            ))
            .ToListAsync(cancellationToken);

        return ApiResponse<List<AdminProgramDto>>.Ok(programs);
    }
}
