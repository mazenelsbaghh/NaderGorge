using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Queries;

public record GetSubjectsQuery(Guid? TeacherId = null) : IRequest<ApiResponse<List<SubjectDto>>>;

public record SubjectDto(Guid Id, string Name, string Description);

public record AcademicSubjectEligibilityDto(
    EducationStage EducationStage,
    GradeLevel GradeLevel,
    Guid SubjectId,
    string SubjectName);

public record GetAcademicSubjectEligibilitiesQuery : IRequest<ApiResponse<List<AcademicSubjectEligibilityDto>>>;

public sealed class GetAcademicSubjectEligibilitiesQueryHandler(IAppDbContext db)
    : IRequestHandler<GetAcademicSubjectEligibilitiesQuery, ApiResponse<List<AcademicSubjectEligibilityDto>>>
{
    public async Task<ApiResponse<List<AcademicSubjectEligibilityDto>>> Handle(GetAcademicSubjectEligibilitiesQuery request, CancellationToken ct)
    {
        var eligibilities = await db.AcademicSubjectEligibilities
            .AsNoTracking()
            .Where(eligibility => eligibility.IsActive)
            .OrderBy(eligibility => eligibility.EducationStage)
            .ThenBy(eligibility => eligibility.GradeLevel)
            .ThenBy(eligibility => eligibility.Subject.Name)
            .Select(eligibility => new AcademicSubjectEligibilityDto(
                eligibility.EducationStage,
                eligibility.GradeLevel,
                eligibility.SubjectId,
                eligibility.Subject.Name))
            .ToListAsync(ct);

        return ApiResponse<List<AcademicSubjectEligibilityDto>>.Ok(eligibilities);
    }
}

public class GetSubjectsQueryHandler : IRequestHandler<GetSubjectsQuery, ApiResponse<List<SubjectDto>>>
{
    private readonly IAppDbContext _db;

    public GetSubjectsQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<List<SubjectDto>>> Handle(GetSubjectsQuery request, CancellationToken ct)
    {
        var query = _db.Subjects.AsQueryable();

        if (request.TeacherId.HasValue)
        {
            query = query.Where(s => s.TeacherSubjects.Any(ts => ts.TeacherId == request.TeacherId.Value || ts.Teacher.UserId == request.TeacherId.Value));
        }

        var subjects = await query
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
