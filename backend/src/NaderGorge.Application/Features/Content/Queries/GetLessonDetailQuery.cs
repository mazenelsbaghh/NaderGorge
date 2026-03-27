using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Content.Queries;

public record GetLessonDetailQuery(Guid LessonId, Guid UserId) : IRequest<ApiResponse<LessonDetailDto>>;

public record LessonDetailDto(
    Guid Id, 
    string Title, 
    string Summary, 
    Guid? ExamId,
    LessonHomeworkDto? Homework,
    List<VideoDto> Videos,
    List<ResourceDto> Resources
);

public record LessonHomeworkDto(Guid Id, string Title, string Instructions, bool IsMandatory, decimal? RequiredPointsToPass, List<LessonHomeworkQuestionDto> Questions);
public record LessonHomeworkQuestionDto(Guid Id, string Text, int Order, int MaxPoints);

public record VideoDto(Guid Id, string Title, string Provider, int Order, int Limit, int Watched, bool IsLocked);
public record ResourceDto(Guid Id, string Title, string FileUrl, string Type);

public class GetLessonDetailQueryHandler : IRequestHandler<GetLessonDetailQuery, ApiResponse<LessonDetailDto>>
{
    private readonly IAppDbContext _db;
    private readonly IAccessCheckService _access;
    private readonly IVideoProvider _video;

    public GetLessonDetailQueryHandler(IAppDbContext db, IAccessCheckService access, IVideoProvider video)
    {
        _db = db;
        _access = access;
        _video = video;
    }

    public async Task<ApiResponse<LessonDetailDto>> Handle(GetLessonDetailQuery request, CancellationToken ct)
    {
        var hasAccess = await _access.HasAccessToLessonAsync(request.UserId, request.LessonId, ct);
        if (!hasAccess)
            return ApiResponse<LessonDetailDto>.Fail("You do not have access to this lesson.");

        var lesson = await _db.Lessons
            .Include(l => l.Videos)
            .Include(l => l.Resources)
            .FirstOrDefaultAsync(l => l.Id == request.LessonId, ct);

        if (lesson == null)
            return ApiResponse<LessonDetailDto>.Fail("Lesson not found");

        var watchEvents = await _db.VideoWatchEvents
            .Where(v => v.UserId == request.UserId && lesson.Videos.Select(x => x.Id).Contains(v.LessonVideoId))
            .ToListAsync(ct);

        var videoDtos = lesson.Videos.OrderBy(v => v.Order).Select(v => 
        {
            var watchEvent = watchEvents.FirstOrDefault(we => we.LessonVideoId == v.Id);
            
            return new VideoDto(
                v.Id, 
                v.Title, 
                v.Provider, 
                v.Order, 
                v.MaxWatchCount, 
                watchEvent?.WatchCount ?? 0, 
                watchEvent?.IsLocked ?? false
            );
        }).ToList();

        var resourceDtos = lesson.Resources.Select(r => new ResourceDto(r.Id, r.Title, r.FileUrl, r.ResourceType)).ToList();

        var hw = await _db.Homeworks
            .Include(h => h.Questions)
            .FirstOrDefaultAsync(h => h.LessonId == request.LessonId, ct);
            
        LessonHomeworkDto? homeworkDto = null;
        if (hw != null)
        {
            var hwQuestions = hw.Questions.OrderBy(q => q.Order).Select(q => 
                new LessonHomeworkQuestionDto(q.Id, q.BodyText, q.Order, q.PointsActive)
            ).ToList();
            
            homeworkDto = new LessonHomeworkDto(hw.Id, hw.Title, hw.Description ?? "", hw.IsMandatory, hw.PassingScoreThreshold, hwQuestions);
        }

        var detail = new LessonDetailDto(lesson.Id, lesson.Title, lesson.Summary, lesson.ExamId, homeworkDto, videoDtos, resourceDtos);
        
        return ApiResponse<LessonDetailDto>.Ok(detail);
    }
}
