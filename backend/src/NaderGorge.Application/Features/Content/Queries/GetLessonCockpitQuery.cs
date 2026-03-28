using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Content.Queries;

public record LessonCockpitVideoDto(Guid Id, string Title, string Provider, string Url, int Order, int MaxWatchCount);
public record LessonCockpitResourceDto(Guid Id, string Title, string FileUrl, string ResourceType);
public record LessonCockpitHomeworkDto(Guid Id, string Title, bool IsMandatory, decimal? PassingScoreThreshold);

public record LessonCockpitDto(
    Guid LessonId,
    string Title,
    string Summary,
    Guid? ExamId,
    List<LessonCockpitVideoDto> Videos,
    List<LessonCockpitResourceDto> Resources,
    List<LessonCockpitHomeworkDto> Homework
);

public record GetLessonCockpitQuery(Guid LessonId) : IRequest<ApiResponse<LessonCockpitDto>>;

public class GetLessonCockpitQueryHandler : IRequestHandler<GetLessonCockpitQuery, ApiResponse<LessonCockpitDto>>
{
    private readonly IAppDbContext _db;

    public GetLessonCockpitQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<LessonCockpitDto>> Handle(GetLessonCockpitQuery request, CancellationToken ct)
    {
        var lesson = await _db.Lessons
            .Include(l => l.Videos)
            .Include(l => l.Resources)
            .FirstOrDefaultAsync(l => l.Id == request.LessonId, ct);

        if (lesson == null)
            return ApiResponse<LessonCockpitDto>.Fail("Lesson not found");

        // Fetch homework separately as it's not a direct navigation property on the same aggregate root if not configured,
        // Wait, Homework has LessonId. We can query it.
        var homeworks = await _db.Homeworks
            .Where(h => h.LessonId == request.LessonId)
            .Select(h => new LessonCockpitHomeworkDto(h.Id, h.Title, h.IsMandatory, h.PassingScoreThreshold))
            .ToListAsync(ct);

        var dto = new LessonCockpitDto(
            lesson.Id,
            lesson.Title,
            lesson.Summary,
            lesson.ExamId,
            lesson.Videos.OrderBy(v => v.Order).Select(v => new LessonCockpitVideoDto(v.Id, v.Title, v.Provider, v.ProviderVideoId, v.Order, v.MaxWatchCount)).ToList(),
            lesson.Resources.Select(r => new LessonCockpitResourceDto(r.Id, r.Title, r.FileUrl, r.ResourceType)).ToList(),
            homeworks
        );

        return ApiResponse<LessonCockpitDto>.Ok(dto);
    }
}
