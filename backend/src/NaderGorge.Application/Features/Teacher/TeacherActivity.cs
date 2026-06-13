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
    int TotalTimeWatchedSeconds
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

        // 2. Fetch Active Students (recently watched a video belonging to the teacher's packages)
        var activeStudents = await _db.VideoWatchEvents
            .AsNoTracking()
            .Include(v => v.User)
            .Include(v => v.LessonVideo)
                .ThenInclude(lv => lv.Lesson)
                    .ThenInclude(l => l.ContentSection)
                        .ThenInclude(cs => cs.Term)
            .Where(v => packageIds.Contains(v.LessonVideo.Lesson.ContentSection.Term.PackageId))
            .OrderByDescending(v => v.UpdatedAt ?? v.CreatedAt)
            .Take(10)
            .Select(v => new TeacherActiveStudentDto(
                v.UserId,
                v.User.FullName,
                v.UpdatedAt ?? v.CreatedAt,
                v.LessonVideo.Title,
                _db.Packages.Where(p => p.Id == v.LessonVideo.Lesson.ContentSection.Term.PackageId).Select(p => p.Name).FirstOrDefault() ?? ""
            ))
            .ToListAsync(ct);

        // 3. Fetch Most Watched Videos
        var mostWatchedData = await _db.VideoWatchEvents
            .AsNoTracking()
            .Where(v => packageIds.Contains(v.LessonVideo.Lesson.ContentSection.Term.PackageId))
            .GroupBy(v => v.LessonVideoId)
            .Select(g => new
            {
                VideoId = g.Key,
                TotalWatchCount = g.Sum(v => v.WatchCount),
                TotalTimeWatchedSeconds = g.Sum(v => v.TimeWatchedInSeconds)
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
                    w.TotalTimeWatchedSeconds
                );
            })
            .ToList();

        // 4. Fetch Inactive Student Alerts
        var studentGrants = await _db.StudentAccessGrants
            .AsNoTracking()
            .Include(s => s.User)
            .Where(s => s.GrantType == Domain.Enums.CodeType.Package && s.PackageId != null && s.IsActive && (s.ExpiresAt == null || s.ExpiresAt > DateTime.UtcNow))
            .Where(s => s.PackageId.HasValue && packageIds.Contains(s.PackageId.Value))
            .ToListAsync(ct);

        var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
        var alerts = new List<TeacherInactiveStudentAlertDto>();

        foreach (var grant in studentGrants)
        {
            var latestEvent = await _db.VideoWatchEvents
                .AsNoTracking()
                .Where(v => v.UserId == grant.UserId && _db.LessonVideos.Any(lv => lv.Id == v.LessonVideoId && _db.Lessons.Any(l => l.Id == lv.LessonId && l.ContentSection.Term.PackageId == grant.PackageId)))
                .OrderByDescending(v => v.UpdatedAt ?? v.CreatedAt)
                .FirstOrDefaultAsync(ct);

            var lastActivity = latestEvent?.UpdatedAt ?? latestEvent?.CreatedAt;

            if (lastActivity == null || lastActivity < sevenDaysAgo)
            {
                var daysInactive = lastActivity == null ? 30 : (DateTime.UtcNow - lastActivity.Value).Days;
                var packageName = await _db.Packages.Where(p => p.Id == grant.PackageId).Select(p => p.Name).FirstOrDefaultAsync(ct) ?? "";
                
                // Add unique student alert
                if (!alerts.Any(a => a.StudentId == grant.UserId))
                {
                    alerts.Add(new TeacherInactiveStudentAlertDto(
                        grant.UserId,
                        grant.User.FullName,
                        lastActivity,
                        packageName,
                        daysInactive
                    ));
                }
            }
        }

        var dto = new TeacherActivityDto(
            activeStudents,
            mostWatched,
            alerts.OrderByDescending(a => a.DaysInactive).Take(15).ToList()
        );

        return ApiResponse<TeacherActivityDto>.Ok(dto);
    }
}
