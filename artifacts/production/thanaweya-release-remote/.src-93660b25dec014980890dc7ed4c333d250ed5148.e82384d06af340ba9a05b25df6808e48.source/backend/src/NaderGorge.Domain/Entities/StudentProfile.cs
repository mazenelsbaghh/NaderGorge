using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities;

public class StudentProfile : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    // ── Personal data ────────────────────────────────────────────────────
    public string? StudentCode { get; set; }                   // Was required, now optional
    public DateTime DateOfBirth { get; set; }
    public Gender Gender { get; set; }
    public string? Nationality { get; set; }                   // NEW: e.g. "مصري", "سعودي"
    public string Governorate { get; set; } = string.Empty;
    public string? District { get; set; }                      // Neighborhood/area within governorate
    public string Address { get; set; } = string.Empty;
    public string? SecondaryPhone { get; set; }                // Student's 2nd phone

    // ── Parent data ──────────────────────────────────────────────────────
    public string? ParentPhone { get; set; }                   // Father's phone (primary contact)
    public string? SecondaryParentPhone { get; set; }          // Parent's 2nd phone
    public string? MotherPhone { get; set; }                   // NEW: Mother's phone (separate from ParentPhone)
    public bool IsFatherAlive { get; set; } = true;
    public bool IsMotherAlive { get; set; } = true;
    public DateTime? FatherDateOfBirth { get; set; }           // NEW: Father's date of birth
    public DateTime? MotherDateOfBirth { get; set; }           // NEW: Mother's date of birth

    // ── School data ──────────────────────────────────────────────────────
    public string? SchoolName { get; set; }                    // NEW: School name (free text)
    public SchoolType? SchoolType { get; set; }                // NEW: School type enum

    // ── Academic data (conditional) ──────────────────────────────────────
    public EducationStage EducationStage { get; set; }
    public GradeLevel GradeLevel { get; set; }
    public string? LightThemePaletteId { get; set; }
    public string? DarkThemePaletteId { get; set; }
    public string CurrentMode { get; set; } = "light";

    /// <summary>
    /// Required only for SecondSecondary (Arts/Science) and
    /// SecondBaccalaureate (Medicine/Engineering/Business/ArtsAndHumanities).
    /// Null for all other grade levels.
    /// </summary>
    public StudyTrack? StudyTrack { get; set; }
    public string? AvatarSlug { get; set; }

    // ── Parent Tracking ──────────────────────────────────────────────────
    public string? ParentTrackingCode { get; set; }
    public bool HasSeenTrackingCodePopup { get; set; } = false;

    public StudentProfile()
    {
        ParentTrackingCode = GenerateRandomCode();
    }

    private static string GenerateRandomCode()
    {
        const string chars = "0123456789";
        var random = new Random();
        var result = new char[6];
        for (int i = 0; i < 6; i++)
        {
            result[i] = chars[random.Next(chars.Length)];
        }
        return new string(result);
    }
}
