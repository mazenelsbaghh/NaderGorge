using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NaderGorge.Application.Features.Public.Queries;

public record PublicTeacherDto(
    Guid Id,
    string FullName,
    string Bio,
    string Specialization,
    string? ProfileImageUrl,
    List<string> SubjectNames
);

public record GetActiveTeachersQuery() : IRequest<ApiResponse<List<PublicTeacherDto>>>;

public class GetActiveTeachersQueryHandler : IRequestHandler<GetActiveTeachersQuery, ApiResponse<List<PublicTeacherDto>>>
{
    private readonly IAppDbContext _db;

    public GetActiveTeachersQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<List<PublicTeacherDto>>> Handle(GetActiveTeachersQuery request, CancellationToken ct)
    {
        var teachers = await _db.TeacherProfiles
            .Include(tp => tp.User)
            .Include(tp => tp.TeacherSubjects)
                .ThenInclude(ts => ts.Subject)
            .OrderBy(tp => tp.User.FullName)
            .Select(tp => new PublicTeacherDto(
                tp.Id,
                tp.User.FullName,
                tp.Bio,
                tp.Specialization,
                tp.ProfileImageUrl,
                tp.TeacherSubjects.Select(ts => ts.Subject.Name).ToList()
            ))
            .ToListAsync(ct);

        return ApiResponse<List<PublicTeacherDto>>.Ok(teachers);
    }
}
