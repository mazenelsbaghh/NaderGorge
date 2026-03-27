using System;
using System.Collections.Generic;

namespace NaderGorge.Application.Features.Admin.Queries;

public class StudentProfileExtendedDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? ParentPhone { get; set; }
    public string? Grade { get; set; }
    public string? SchoolName { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }

    public GamificationSummaryDto? Gamification { get; set; }
    public List<StudentPackageDto> Packages { get; set; } = new();
    public List<StudentDeviceDto> Devices { get; set; } = new();
    public List<VideoOverrideDto> Overrides { get; set; } = new();
    public List<AuditLogDto> AuditTrail { get; set; } = new();
}

public class GamificationSummaryDto
{
    public decimal TotalPoints { get; set; }
    public int GlobalRank { get; set; }
    public int Level { get; set; }
    public string Title { get; set; } = string.Empty;
    public List<string> RecentBadges { get; set; } = new();
}

public class StudentPackageDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime EnrolledAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public decimal Progress { get; set; }
}

public class StudentDeviceDto
{
    public Guid Id { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public DateTime LastActiveAt { get; set; }
    public bool IsActive { get; set; }
}

public class VideoOverrideDto
{
    public Guid Id { get; set; }
    public Guid VideoId { get; set; }
    public string VideoTitle { get; set; } = string.Empty;
    public int OriginalLimit { get; set; }
    public int NewLimit { get; set; }
    public int CurrentViews { get; set; }
    public string OverrideBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class AuditLogDto
{
    public Guid Id { get; set; }
    public string AdminName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Details { get; set; } = string.Empty;
}
