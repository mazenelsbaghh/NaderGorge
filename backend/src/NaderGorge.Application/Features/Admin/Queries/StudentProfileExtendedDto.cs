using System;
using System.Collections.Generic;

namespace NaderGorge.Application.Features.Admin.Queries;

public class StudentProfileExtendedDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? ParentPhone { get; set; }
    public string? SecondaryPhone { get; set; }
    public string? SecondaryParentPhone { get; set; }
    public string? District { get; set; }
    public string? Grade { get; set; }
    public string? SchoolName { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }

    // ── Personal fields ─────────────────────────────────────────────────
    public DateTime? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? Governorate { get; set; }
    public string? Address { get; set; }
    public string? StudentCode { get; set; }
    public bool IsProfileComplete { get; set; }

    // ── Academic fields ─────────────────────────────────────────────────
    public string? EducationStage { get; set; }
    public string? StudyTrack { get; set; }

    // ── Parent/Family fields (Student Profile V2) ─────────────────────
    public string? Nationality { get; set; }                   // e.g. "مصري"
    public string? MotherPhone { get; set; }                   // Mother's phone number
    public DateTime? FatherDateOfBirth { get; set; }           // Father's date of birth
    public DateTime? MotherDateOfBirth { get; set; }           // Mother's date of birth
    public string? SchoolType { get; set; }                    // e.g. "Language" → mapped to label in query
    public bool IsFatherAlive { get; set; }
    public bool IsMotherAlive { get; set; }

    public GamificationSummaryDto? Gamification { get; set; }
    public List<StudentPackageDto> Packages { get; set; } = new();
    public List<StudentDeviceDto> Devices { get; set; } = new();
    public List<VideoOverrideDto> Overrides { get; set; } = new();
    public WatchTrackingSummaryDto WatchTracking { get; set; } = new();
    public decimal CurrentBalance { get; set; }
    public List<StudentBalanceTransactionDto> BalanceTransactions { get; set; } = new();
    public List<AuditLogDto> AuditTrail { get; set; } = new();
    public List<StudentNoteDto> Notes { get; set; } = new();
}

public class StudentBalanceTransactionDto
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string AdminName { get; set; } = string.Empty;
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
    public Guid AccessGrantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime EnrolledAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public decimal Progress { get; set; }
    public bool IsActive { get; set; }
    public string PurchaseMethod { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string GrantType { get; set; } = "Package";
    public string? CancelledByName { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
}

public class StudentDeviceDto
{
    public Guid Id { get; set; }
    public string DeviceName { get; set; } = string.Empty;   // Raw User-Agent
    public string? IpAddress { get; set; }
    public string? OsName { get; set; }
    public string? BrowserName { get; set; }
    public string? DeviceType { get; set; }
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
    public int AddedViews { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string OverrideBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class WatchTrackingSummaryDto
{
    public int TotalWatchedSeconds { get; set; }
    public int WatchedVideosCount { get; set; }
    public List<StudentVideoWatchActivityDto> Activities { get; set; } = new();
}

public class StudentVideoWatchActivityDto
{
    public Guid LessonVideoId { get; set; }
    public string VideoTitle { get; set; } = string.Empty;
    public Guid LessonId { get; set; }
    public string LessonTitle { get; set; } = string.Empty;
    public string? PackageName { get; set; }
    public string? TermTitle { get; set; }
    public int WatchCount { get; set; }
    public int MaxWatchCount { get; set; }
    public int WatchedSeconds { get; set; }
    public bool IsLocked { get; set; }
    public DateTime LastWatchedAt { get; set; }
}

public class AuditLogDto
{
    public Guid Id { get; set; }
    public string AdminName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Details { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? IpAddress { get; set; }
}

public class StudentNoteDto
{
    public Guid Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public string AdminName { get; set; } = string.Empty;
    public bool IsPinned { get; set; }
    public DateTime CreatedAt { get; set; }
}
