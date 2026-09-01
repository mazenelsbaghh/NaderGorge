using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace NaderGorge.Application.Features.Admin.Queries;

public record AdminWatchRequestDto(
    Guid Id,
    Guid UserId,
    string StudentName,
    string StudentPhone,
    Guid LessonVideoId,
    string VideoTitle,
    string TeacherName,
    string PackageName,
    string TermTitle,
    string SectionTitle,
    string LessonTitle,
    string StudentReason,
    int Status,
    DateTime CreatedAt,
    DateTime? ResolvedAt,
    string? Reason,
    int CurrentWatchCount,
    int MaxWatchCount,
    bool ReachedLimit,
    int BaseWatchCount,
    int? VideoDurationSeconds,
    bool HasPreviousRequest
);

public record GetWatchRequestsQuery() : IRequest<ApiResponse<List<AdminWatchRequestDto>>>;

public class GetWatchRequestsQueryHandler : IRequestHandler<GetWatchRequestsQuery, ApiResponse<List<AdminWatchRequestDto>>>
{
    private readonly IAppDbContext _context;

    public GetWatchRequestsQueryHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<List<AdminWatchRequestDto>>> Handle(GetWatchRequestsQuery request, CancellationToken cancellationToken)
    {
        var requests = await _context.ExtraWatchRequests
            .Include(r => r.User)
            .Include(r => r.LessonVideo)
            .OrderBy(r => r.Status == NaderGorge.Domain.Enums.RequestStatus.Pending ? 0 : 1)
            .ThenByDescending(r => r.CreatedAt)
            .Select(r => new {
                r.Id,
                r.UserId,
                StudentName = r.User != null ? r.User.FullName : string.Empty,
                StudentPhone = r.User != null ? r.User.PhoneNumber : string.Empty,
                r.LessonVideoId,
                VideoTitle = r.LessonVideo != null ? r.LessonVideo.Title : string.Empty,
                TeacherName = r.LessonVideo != null && r.LessonVideo.Lesson.ContentSection.Term.Package.Teacher.User != null
                    ? r.LessonVideo.Lesson.ContentSection.Term.Package.Teacher.User.FullName
                    : string.Empty,
                PackageName = r.LessonVideo != null ? r.LessonVideo.Lesson.ContentSection.Term.Package.Name : string.Empty,
                TermTitle = r.LessonVideo != null ? r.LessonVideo.Lesson.ContentSection.Term.Title : string.Empty,
                SectionTitle = r.LessonVideo != null ? r.LessonVideo.Lesson.ContentSection.Title : string.Empty,
                LessonTitle = r.LessonVideo != null ? r.LessonVideo.Lesson.Title : string.Empty,
                StudentReason = r.RequestReason,
                Status = (int)r.Status,
                r.CreatedAt,
                r.ResolvedAt,
                Reason = r.RejectionReason,
                WatchEvent = _context.VideoWatchEvents
                    .Where(w => w.UserId == r.UserId && w.LessonVideoId == r.LessonVideoId)
                    .Select(w => new { w.WatchCount, MaxLimit = w.CustomMaxWatchCount })
                    .FirstOrDefault(),
                VideoMaxLimit = r.LessonVideo != null ? r.LessonVideo.MaxWatchCount : 0,
                VideoDurationSeconds = r.LessonVideo != null
                    ? (_context.BunnyVideoAssets
                        .Where(asset => asset.LessonVideoId == r.LessonVideoId
                            && asset.SourceState == BunnyVideoAssetSourceState.Current)
                        .Select(asset => asset.DurationSeconds)
                        .FirstOrDefault()
                        ?? r.LessonVideo.VideoChapters.Select(chapter => (int?)chapter.EndTime).Max())
                    : null,
                HasPreviousRequest = _context.ExtraWatchRequests.Any(previous =>
                    previous.UserId == r.UserId
                    && previous.LessonVideoId == r.LessonVideoId
                    && previous.Id != r.Id)
            })
            .ToListAsync(cancellationToken);

        var dtos = requests.Select(r => {
            int currentCount = r.WatchEvent?.WatchCount ?? 0;
            int maxCount = r.WatchEvent?.MaxLimit ?? r.VideoMaxLimit;
            bool reachedLimit = maxCount > 0 && currentCount >= maxCount;
            return new AdminWatchRequestDto(
                r.Id,
                r.UserId,
                r.StudentName,
                r.StudentPhone,
                r.LessonVideoId,
                r.VideoTitle,
                r.TeacherName,
                r.PackageName,
                r.TermTitle,
                r.SectionTitle,
                r.LessonTitle,
                r.StudentReason,
                r.Status,
                r.CreatedAt,
                r.ResolvedAt,
                r.Reason,
                currentCount,
                maxCount,
                reachedLimit,
                r.VideoMaxLimit,
                r.VideoDurationSeconds,
                r.HasPreviousRequest
            );
        }).ToList();

        return ApiResponse<List<AdminWatchRequestDto>>.Ok(dtos);
    }
}
