using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Content.Queries;

public sealed record ContentSummaryTeacherDto(
    Guid Id,
    string FullName,
    string? ProfileImageUrl,
    string Specialization,
    IReadOnlyList<Guid> SubjectIds,
    IReadOnlyList<string> SubjectNames,
    int PackagesCount);

public sealed record GetContentSummaryTeachersQuery
    : IRequest<ApiResponse<IReadOnlyList<ContentSummaryTeacherDto>>>;

public sealed class GetContentSummaryTeachersQueryHandler
    : IRequestHandler<GetContentSummaryTeachersQuery, ApiResponse<IReadOnlyList<ContentSummaryTeacherDto>>>
{
    private readonly IAppDbContext _db;

    public GetContentSummaryTeachersQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<IReadOnlyList<ContentSummaryTeacherDto>>> Handle(
        GetContentSummaryTeachersQuery request,
        CancellationToken ct)
    {
        var teachers = await _db.TeacherProfiles
            .AsNoTracking()
            .OrderBy(teacher => teacher.User.FullName)
            .Select(teacher => new ContentSummaryTeacherDto(
                teacher.Id,
                teacher.User.FullName,
                teacher.ProfileImageUrl,
                teacher.Specialization,
                teacher.TeacherSubjects
                    .OrderBy(item => item.Subject.Name)
                    .Select(item => item.SubjectId)
                    .ToList(),
                teacher.TeacherSubjects
                    .OrderBy(item => item.Subject.Name)
                    .Select(item => item.Subject.Name)
                    .ToList(),
                teacher.Packages.Count))
            .ToListAsync(ct);

        return ApiResponse<IReadOnlyList<ContentSummaryTeacherDto>>.Ok(teachers);
    }
}
