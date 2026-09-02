using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NaderGorge.Application.Features.Teacher;

public record GetTeacherActivityQuery(Guid TeacherUserId) : IRequest<ApiResponse<TeacherActivityDto>>;

public record TeacherActivityDto(
    List<TeacherActiveStudentDto> ActiveStudents,
    List<TeacherMostWatchedVideoDto> MostWatchedVideos,
    List<TeacherInactiveStudentAlertDto> InactiveStudentAlerts
);

public record TeacherActiveStudentDto(
    Guid StudentId,
    string StudentName,
    DateTime? LastActivityAt,
    string LastWatchedVideoTitle,
    string PackageName
);

public record TeacherMostWatchedVideoDto(
    Guid VideoId,
    string VideoTitle,
    string LessonTitle,
    int TotalWatchCount,
    int TotalTimeWatchedSeconds,
    decimal AveragePlaybackRate
);

public record TeacherInactiveStudentAlertDto(
    Guid StudentId,
    string StudentName,
    DateTime? LastActivityAt,
    string PackageName,
    int DaysInactive
);

public class GetTeacherActivityQueryHandler : IRequestHandler<GetTeacherActivityQuery, ApiResponse<TeacherActivityDto>>
{
    private readonly IAppDbContext _db;

    public GetTeacherActivityQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<TeacherActivityDto>> Handle(GetTeacherActivityQuery request, CancellationToken ct)
    {
        var teacherProfile = await _db.TeacherProfiles
            .FirstOrDefaultAsync(tp => tp.UserId == request.TeacherUserId, ct);

        if (teacherProfile == null)
        {
            return ApiResponse<TeacherActivityDto>.Fail("حساب المعلم غير موجود");
        }

        // 1. Get Teacher's Package IDs
        var packageIds = await _db.Packages
            .Where(p => p.TeacherId == teacherProfile.Id)
            .Select(p => p.Id)
            .ToListAsync(ct);

        var scopedWatchEvents = _db.VideoWatchEvents
            .AsNoTracking()
            .Where(v => packageIds.Contains(v.LessonVideo.Lesson.ContentSection.Term.PackageId));

        // A watch event is unique per student/video. Use a correlated anti-join
        // to keep only each student's latest event without EF's unsupported
        // GroupBy + navigation projection shape.
        var latestActivityCandidates = await scopedWatchEvents
            .Where(watchEvent => !scopedWatchEvents.Any(other =>
                other.UserId == watchEvent.UserId
                && (other.UpdatedAt ?? other.CreatedAt) > (watchEvent.UpdatedAt ?? watchEvent.CreatedAt)))
            .OrderByDescending(watchEvent => watchEvent.UpdatedAt ?? watchEvent.CreatedAt)
            .Select(watchEvent => new TeacherActiveStudentDto(
                watchEvent.UserId,
                watchEvent.User.FullName,
                watchEvent.UpdatedAt ?? watchEvent.CreatedAt,
                watchEvent.LessonVideo.Title,
                watchEvent.LessonVideo.Lesson.ContentSection.Term.Package.Name))
            .Take(20)
            .ToListAsync(ct);

        var activeStudents = latestActivityCandidates
            .GroupBy(activity => activity.StudentId)
            .Select(group => group.First())
            .Take(10)
            .ToList();

        // 3. Fetch Most Watched Videos
        var mostWatchedData = await scopedWatchEvents
            .GroupBy(v => v.LessonVideoId)
            .Select(g => new
            {
                VideoId = g.Key,
                TotalWatchCount = g.Sum(v => v.WatchCount),
                TotalTimeWatchedSeconds = g.Sum(v => v.TimeWatchedInSeconds),
                TotalActualWatchedSeconds = g.Sum(v => v.ActualWatchedSeconds)
            })
            .OrderByDescending(w => w.TotalWatchCount)
            .Take(10)
            .ToListAsync(ct);

        var topVideoIds = mostWatchedData.Select(w => w.VideoId).ToList();

        var videoDetails = await _db.LessonVideos
            .AsNoTracking()
            .Include(lv => lv.Lesson)
            .Where(lv => topVideoIds.Contains(lv.Id))
            .ToDictionaryAsync(lv => lv.Id, ct);

        var mostWatched = mostWatchedData
            .Where(w => videoDetails.ContainsKey(w.VideoId))
            .Select(w => {
                var detail = videoDetails[w.VideoId];
                return new TeacherMostWatchedVideoDto(
                    w.VideoId,
                    detail.Title,
                    detail.Lesson.Title,
                    w.TotalWatchCount,
                    w.TotalTimeWatchedSeconds,
                    CalculateAveragePlaybackRate(w.TotalTimeWatchedSeconds, w.TotalActualWatchedSeconds)
                );
            })
            .ToList();

        // 4. Fetch Inactive Student Alerts
        var studentGrants = await _db.StudentAccessGrants
            .AsNoTracking()
            .Where(s => s.GrantType == Domain.Enums.CodeType.Package && s.PackageId != null && s.IsActive && (s.ExpiresAt == null || s.ExpiresAt > DateTime.UtcNow))
            .Where(s => s.PackageId.HasValue && packageIds.Contains(s.PackageId.Value))
            .Select(grant => new
            {
                grant.UserId,
                StudentName = grant.User.FullName,
                PackageName = _db.Packages
                    .Where(package => package.Id == grant.PackageId)
                    .Select(package => package.Name)
                    .FirstOrDefault() ?? string.Empty
            })
            .ToListAsync(ct);

        var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
        var latestActivityByStudent = await scopedWatchEvents
            .GroupBy(watchEvent => watchEvent.UserId)
            .Select(group => new
            {
                StudentId = group.Key,
                LastActivityAt = group.Max(watchEvent => watchEvent.UpdatedAt ?? watchEvent.CreatedAt)
            })
            .ToDictionaryAsync(activity => activity.StudentId, activity => activity.LastActivityAt, ct);

        var now = DateTime.UtcNow;
        var alerts = studentGrants
            .GroupBy(grant => grant.UserId)
            .Select(group => group.First())
            .Select(grant =>
            {
                latestActivityByStudent.TryGetValue(grant.UserId, out var lastActivity);
                return new
                {
                    Grant = grant,
                    LastActivity = lastActivity == default ? (DateTime?)null : lastActivity
                };
            })
            .Where(candidate => candidate.LastActivity is null || candidate.LastActivity < sevenDaysAgo)
            .Select(candidate => new TeacherInactiveStudentAlertDto(
                candidate.Grant.UserId,
                candidate.Grant.StudentName,
                candidate.LastActivity,
                candidate.Grant.PackageName,
                candidate.LastActivity is null ? 30 : (now - candidate.LastActivity.Value).Days))
            .ToList();

        var dto = new TeacherActivityDto(
            activeStudents,
            mostWatched,
            alerts.OrderByDescending(a => a.DaysInactive).Take(15).ToList()
        );

        return ApiResponse<TeacherActivityDto>.Ok(dto);
    }

    private static decimal CalculateAveragePlaybackRate(int trackedSeconds, decimal actualSeconds) =>
        actualSeconds > 0 ? decimal.Round(trackedSeconds / actualSeconds, 2) : 1m;
}
