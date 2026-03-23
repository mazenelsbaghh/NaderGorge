using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Content.Queries;

public record GetLessonsQuery(Guid SectionId, Guid UserId) : IRequest<ApiResponse<List<LessonSummaryDto>>>;

public record LessonSummaryDto(Guid Id, string Title, string Summary, int Order, bool HasAccess, bool IsCompleted);

public class GetLessonsQueryHandler : IRequestHandler<GetLessonsQuery, ApiResponse<List<LessonSummaryDto>>>
{
    private readonly IAppDbContext _db;
    private readonly IAccessCheckService _access;

    public GetLessonsQueryHandler(IAppDbContext db, IAccessCheckService access)
    {
        _db = db;
        _access = access;
    }

    public async Task<ApiResponse<List<LessonSummaryDto>>> Handle(GetLessonsQuery request, CancellationToken ct)
    {
        var section = await _db.ContentSections
            .Include(cs => cs.Lessons)
            .FirstOrDefaultAsync(cs => cs.Id == request.SectionId, ct);

        if (section == null)
            return ApiResponse<List<LessonSummaryDto>>.Fail("Section not found");

        var progresses = await _db.LessonProgresses
            .Where(lp => lp.UserId == request.UserId && section.Lessons.Select(l => l.Id).Contains(lp.LessonId))
            .ToListAsync(ct);

        var dtos = new List<LessonSummaryDto>();
        foreach (var lesson in section.Lessons.OrderBy(l => l.Order))
        {
            var hasAccess = await _access.HasAccessToLessonAsync(request.UserId, lesson.Id, ct);
            var isCompleted = progresses.Any(p => p.LessonId == lesson.Id && p.IsCompleted);
            
            dtos.Add(new LessonSummaryDto(lesson.Id, lesson.Title, lesson.Summary, lesson.Order, hasAccess, isCompleted));
        }

        return ApiResponse<List<LessonSummaryDto>>.Ok(dtos);
    }
}
