using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;

using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Queries;

public class GetStudentProfileDetailQuery : IRequest<StudentProfileExtendedDto>
{
    public Guid UserId { get; set; }

    public GetStudentProfileDetailQuery(Guid userId)
    {
        UserId = userId;
    }
}

public class GetStudentProfileDetailQueryHandler : IRequestHandler<GetStudentProfileDetailQuery, StudentProfileExtendedDto>
{
    private readonly IAppDbContext _context;

    public GetStudentProfileDetailQueryHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<StudentProfileExtendedDto> Handle(GetStudentProfileDetailQuery request, CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .Include(u => u.StudentProfile)
            .Include(u => u.Devices)
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (user == null)
        {
            throw new KeyNotFoundException($"Student profile for user {request.UserId} not found.");
        }

        var gamification = await _context.StudentGamifications
            .FirstOrDefaultAsync(g => g.StudentId == request.UserId, cancellationToken);
            
        var rankPosition = gamification != null ? await _context.StudentGamifications
            .CountAsync(g => g.TotalPoints > gamification.TotalPoints, cancellationToken) + 1 : 0;

        var packageIds = await _context.StudentAccessGrants
            .Where(g => g.UserId == request.UserId && g.PackageId.HasValue)
            .Select(g => new { g.PackageId, g.CreatedAt, g.ExpiresAt })
            .ToListAsync(cancellationToken);

        var packages = new List<StudentPackageDto>();
        foreach (var grant in packageIds)
        {
            var p = await _context.Packages.FindAsync(new object[] { grant.PackageId!.Value }, cancellationToken);
            packages.Add(new StudentPackageDto
            {
                Id = grant.PackageId.Value,
                Name = p != null ? p.Name : "Unknown",
                EnrolledAt = grant.CreatedAt,
                ExpiresAt = grant.ExpiresAt,
                Progress = 0
            });
        }

        // Map devices
        var devices = user.Devices.Select(d => new StudentDeviceDto
        {
            Id = d.Id,
            DeviceName = d.DeviceFingerprint,
            LastActiveAt = d.LastUsedAt,
            IsActive = d.IsActive
        }).ToList();

        // Overrides
        // We will need VideoOverrides table if it exists. Reverting to empty for now if entity lacks it, 
        // to avoid compiler error.
        var overrides = new List<VideoOverrideDto>(); // Placeholder for US2 when we build the entity.

        var watchActivities = await _context.VideoWatchEvents
            .Where(v => v.UserId == request.UserId)
            .Include(v => v.LessonVideo)
                .ThenInclude(video => video.Lesson)
                    .ThenInclude(lesson => lesson.ContentSection)
                        .ThenInclude(section => section.Term)
                            .ThenInclude(term => term.Package)
            .OrderByDescending(v => v.UpdatedAt)
            .Select(v => new StudentVideoWatchActivityDto
            {
                LessonVideoId = v.LessonVideoId,
                VideoTitle = v.LessonVideo.Title,
                LessonId = v.LessonVideo.LessonId,
                LessonTitle = v.LessonVideo.Lesson.Title,
                PackageName = v.LessonVideo.Lesson.ContentSection.Term.Package.Name,
                WatchCount = v.WatchCount,
                MaxWatchCount = v.LessonVideo.MaxWatchCount,
                WatchedSeconds = v.TimeWatchedInSeconds,
                IsLocked = v.IsLocked,
                LastWatchedAt = v.UpdatedAt ?? v.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var auditLogs = await _context.AuditLogs
            .Include(a => a.PerformedByUser)
            .Where(a => a.EntityType == "User" && a.EntityId == request.UserId)
            .OrderByDescending(a => a.CreatedAt)
            .Take(50)
            .Select(a => new AuditLogDto
            {
                Id = a.Id,
                Action = a.Action,
                AdminName = a.PerformedByUser != null ? a.PerformedByUser.FullName : "System",
                Date = a.CreatedAt,
                Details = a.NewValues ?? string.Empty
            })
            .ToListAsync(cancellationToken);

        return new StudentProfileExtendedDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Phone = user.PhoneNumber,
            ParentPhone = user.StudentProfile?.ParentPhone,
            SecondaryPhone = user.StudentProfile?.SecondaryPhone,
            SecondaryParentPhone = user.StudentProfile?.SecondaryParentPhone,
            District = user.StudentProfile?.District,
            Grade = user.StudentProfile?.GradeLevel.ToString(),
            SchoolName = user.StudentProfile?.SchoolName,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,

            // ── Personal fields ─────────────────────────────────────────
            DateOfBirth = user.StudentProfile?.DateOfBirth,
            Gender = user.StudentProfile?.Gender.ToString(),
            Governorate = user.StudentProfile?.Governorate,
            Address = user.StudentProfile?.Address,
            StudentCode = user.StudentProfile?.StudentCode,
            IsProfileComplete = user.IsProfileComplete,

            // ── Academic fields ──────────────────────────────────────────
            EducationStage = user.StudentProfile?.EducationStage.ToString(),
            StudyTrack = user.StudentProfile?.StudyTrack?.ToString(),

            // ── Student Profile V2 fields ─────────────────────────────────
            Nationality = user.StudentProfile?.Nationality,
            MotherPhone = user.StudentProfile?.MotherPhone,
            FatherDateOfBirth = user.StudentProfile?.FatherDateOfBirth,
            MotherDateOfBirth = user.StudentProfile?.MotherDateOfBirth,
            SchoolType = user.StudentProfile?.SchoolType?.ToString(),
            IsFatherAlive = user.StudentProfile?.IsFatherAlive ?? true,
            IsMotherAlive = user.StudentProfile?.IsMotherAlive ?? true,

            Gamification = gamification != null ? new GamificationSummaryDto
            {
                TotalPoints = gamification.TotalPoints,
                GlobalRank = rankPosition,
                Level = 0,
                Title = gamification.LevelName ?? string.Empty,
                RecentBadges = new List<string>()
            } : null,
            Packages = packages,
            Devices = devices,
            Overrides = overrides,
            WatchTracking = new WatchTrackingSummaryDto
            {
                TotalWatchedSeconds = watchActivities.Sum(activity => activity.WatchedSeconds),
                WatchedVideosCount = watchActivities.Count,
                Activities = watchActivities
            },
            AuditTrail = auditLogs
        };
    }
}
