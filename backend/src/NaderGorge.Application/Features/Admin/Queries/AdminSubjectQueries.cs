using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Queries;

public record GetSubjectsQuery : IRequest<ApiResponse<List<SubjectDto>>>;

public record SubjectDto(Guid Id, string Name, string Description);

public class GetSubjectsQueryHandler : IRequestHandler<GetSubjectsQuery, ApiResponse<List<SubjectDto>>>
{
    private readonly IAppDbContext _db;

    public GetSubjectsQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<List<SubjectDto>>> Handle(GetSubjectsQuery request, CancellationToken ct)
    {
        var subjects = await _db.Subjects
            .OrderBy(s => s.Name)
            .Select(s => new SubjectDto(s.Id, s.Name, s.Description))
            .ToListAsync(ct);

        return ApiResponse<List<SubjectDto>>.Ok(subjects);
    }
}

public record GetSubjectByIdQuery(Guid Id) : IRequest<ApiResponse<SubjectDto>>;

public class GetSubjectByIdQueryHandler : IRequestHandler<GetSubjectByIdQuery, ApiResponse<SubjectDto>>
{
    private readonly IAppDbContext _db;

    public GetSubjectByIdQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<SubjectDto>> Handle(GetSubjectByIdQuery request, CancellationToken ct)
    {
        var subject = await _db.Subjects
            .Where(s => s.Id == request.Id)
            .Select(s => new SubjectDto(s.Id, s.Name, s.Description))
            .FirstOrDefaultAsync(ct);

        if (subject == null)
            return ApiResponse<SubjectDto>.Fail("Subject not found");

        return ApiResponse<SubjectDto>.Ok(subject);
    }
}
