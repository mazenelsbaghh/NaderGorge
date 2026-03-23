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
    List<VideoDto> Videos,
    List<ResourceDto> Resources
);

public record VideoDto(Guid Id, string Title, string Provider, string EmbedUrl, int Order, int Limit, int Watched, bool IsLocked);
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
            var embedUrl = _video.GetEmbedUrl(v.ProviderVideoId);
            var watchEvent = watchEvents.FirstOrDefault(we => we.LessonVideoId == v.Id);
            
            return new VideoDto(
                v.Id, 
                v.Title, 
                v.Provider, 
                embedUrl, 
                v.Order, 
                v.MaxWatchCount, 
                watchEvent?.WatchCount ?? 0, 
                watchEvent?.IsLocked ?? false
            );
        }).ToList();

        var resourceDtos = lesson.Resources.Select(r => new ResourceDto(r.Id, r.Title, r.FileUrl, r.ResourceType)).ToList();

        var detail = new LessonDetailDto(lesson.Id, lesson.Title, lesson.Summary, lesson.ExamId, videoDtos, resourceDtos);
        
        return ApiResponse<LessonDetailDto>.Ok(detail);
    }
}
