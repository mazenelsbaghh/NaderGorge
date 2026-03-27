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
            Email = string.Empty,
            Phone = user.PhoneNumber,
            ParentPhone = user.StudentProfile?.ParentPhone,
            Grade = user.StudentProfile?.Grade,
            SchoolName = user.StudentProfile?.School,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
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
            AuditTrail = auditLogs
        };
    }
}
