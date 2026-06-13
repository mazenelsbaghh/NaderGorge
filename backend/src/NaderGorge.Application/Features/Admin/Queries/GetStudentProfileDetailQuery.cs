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

        var packages = await _context.StudentAccessGrants
            .Where(g => g.UserId == request.UserId && g.PackageId.HasValue)
            .Include(g => g.CancelledByUser)
            .Join(
                _context.Packages,
                grant => grant.PackageId!.Value,
                package => package.Id,
                (grant, package) => new StudentPackageDto
                {
                    Id = package.Id,
                    AccessGrantId = grant.Id,
                    Name = package.Name,
                    EnrolledAt = grant.CreatedAt,
                    ExpiresAt = grant.ExpiresAt,
                    Progress = 0,
                    IsActive = grant.IsActive,
                    PurchaseMethod = grant.AccessCodeId.HasValue ? "Code" : "Balance",
                    Price = package.Price,
                    GrantType = grant.GrantType.ToString(),
                    CancelledByName = grant.CancelledByUser != null ? grant.CancelledByUser.FullName : null,
                    CancelledAt = grant.CancelledAt,
                    CancellationReason = grant.CancellationReason
                })
            .OrderByDescending(p => p.EnrolledAt)
            .ToListAsync(cancellationToken);

        var overrides = await _context.VideoOverrides
            .Include(o => o.LessonVideo)
            .Include(o => o.PerformedByUser)
            .Where(o => o.UserId == request.UserId)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new VideoOverrideDto
            {
                Id = o.Id,
                VideoId = o.LessonVideoId,
                VideoTitle = o.LessonVideo.Title,
                OriginalLimit = o.OriginalLimit,
                NewLimit = o.NewLimit,
                AddedViews = o.AddedViews,
                Reason = o.Reason,
                OverrideBy = o.PerformedByUser.FullName,
                CreatedAt = o.CreatedAt
            })
            .ToListAsync(cancellationToken);

        // Map devices
        var devices = user.Devices.Select(d => new StudentDeviceDto
        {
            Id = d.Id,
            DeviceName = d.DeviceName ?? d.DeviceFingerprint,
            IpAddress = d.IpAddress,
            OsName = d.OsName,
            BrowserName = d.BrowserName,
            DeviceType = d.DeviceType,
            LastActiveAt = d.LastUsedAt,
            IsActive = d.IsActive
        }).OrderByDescending(d => d.LastActiveAt).ToList();

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
                TermTitle = v.LessonVideo.Lesson.ContentSection.Term.Title,
                WatchCount = v.WatchCount,
                MaxWatchCount = v.CustomMaxWatchCount ?? v.LessonVideo.MaxWatchCount,
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
                Details = a.NewValues ?? string.Empty,
                EntityType = a.EntityType,
                EntityId = a.EntityId,
                OldValues = a.OldValues,
                NewValues = a.NewValues,
                IpAddress = a.IpAddress
            })
            .ToListAsync(cancellationToken);

        var balance = await _context.StudentBalances
            .FirstOrDefaultAsync(b => b.UserId == request.UserId, cancellationToken);

        var balanceTransactions = new List<StudentBalanceTransactionDto>();
        if (balance != null)
        {
            balanceTransactions = await _context.BalanceTransactions
                .Include(t => t.PerformedByUser)
                .Where(t => t.StudentBalanceId == balance.Id)
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new StudentBalanceTransactionDto
                {
                    Id = t.Id,
                    Amount = t.Amount,
                    BalanceAfter = t.BalanceAfter,
                    TransactionType = t.TransactionType,
                    Description = t.Description,
                    CreatedAt = t.CreatedAt,
                    AdminName = t.PerformedByUser != null ? t.PerformedByUser.FullName : (t.TransactionType == "AdminAdjustment" ? "مدير النظام" : "النظام")
                })
                .ToListAsync(cancellationToken);
        }

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
            CurrentBalance = balance?.CurrentBalance ?? 0m,
            BalanceTransactions = balanceTransactions,
            AuditTrail = auditLogs,
            Notes = await _context.StudentNotes
                .Include(n => n.Admin)
                .Where(n => n.StudentId == request.UserId)
                .OrderByDescending(n => n.IsPinned)
                .ThenByDescending(n => n.CreatedAt)
                .Select(n => new StudentNoteDto
                {
                    Id = n.Id,
                    Content = n.Content,
                    AdminName = n.Admin.FullName,
                    IsPinned = n.IsPinned,
                    CreatedAt = n.CreatedAt
                })
                .ToListAsync(cancellationToken)
        };
    }
}
