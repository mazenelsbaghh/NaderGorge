using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Queries;

public record GetTeachersQuery : IRequest<ApiResponse<List<TeacherDto>>>;

public record TeacherDto(
    Guid Id,
    Guid UserId,
    string FullName,
    string PhoneNumber,
    string Bio,
    string Specialization,
    decimal CommissionRate,
    string? ProfileImageUrl,
    string ContactInfo,
    List<Guid> SubjectIds,
    List<string> SubjectNames);

public class GetTeachersQueryHandler : IRequestHandler<GetTeachersQuery, ApiResponse<List<TeacherDto>>>
{
    private readonly IAppDbContext _db;

    public GetTeachersQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<List<TeacherDto>>> Handle(GetTeachersQuery request, CancellationToken ct)
    {
        var teachers = await _db.TeacherProfiles
            .Include(tp => tp.User)
            .Include(tp => tp.TeacherSubjects)
                .ThenInclude(ts => ts.Subject)
            .OrderBy(tp => tp.User.FullName)
            .Select(tp => new TeacherDto(
                tp.Id,
                tp.UserId,
                tp.User.FullName,
                tp.User.PhoneNumber,
                tp.Bio,
                tp.Specialization,
                tp.CommissionRate,
                tp.ProfileImageUrl,
                tp.ContactInfo,
                tp.TeacherSubjects.Select(ts => ts.SubjectId).ToList(),
                tp.TeacherSubjects.Select(ts => ts.Subject.Name).ToList()
            ))
            .ToListAsync(ct);

        return ApiResponse<List<TeacherDto>>.Ok(teachers);
    }
}

public record GetTeacherByIdQuery(Guid Id) : IRequest<ApiResponse<TeacherDto>>;

public class GetTeacherByIdQueryHandler : IRequestHandler<GetTeacherByIdQuery, ApiResponse<TeacherDto>>
{
    private readonly IAppDbContext _db;

    public GetTeacherByIdQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<TeacherDto>> Handle(GetTeacherByIdQuery request, CancellationToken ct)
    {
        var teacher = await _db.TeacherProfiles
            .Include(tp => tp.User)
            .Include(tp => tp.TeacherSubjects)
                .ThenInclude(ts => ts.Subject)
            .Where(tp => tp.Id == request.Id)
            .Select(tp => new TeacherDto(
                tp.Id,
                tp.UserId,
                tp.User.FullName,
                tp.User.PhoneNumber,
                tp.Bio,
                tp.Specialization,
                tp.CommissionRate,
                tp.ProfileImageUrl,
                tp.ContactInfo,
                tp.TeacherSubjects.Select(ts => ts.SubjectId).ToList(),
                tp.TeacherSubjects.Select(ts => ts.Subject.Name).ToList()
            ))
            .FirstOrDefaultAsync(ct);

        if (teacher == null)
            return ApiResponse<TeacherDto>.Fail("Teacher profile not found");

        return ApiResponse<TeacherDto>.Ok(teacher);
    }
}
