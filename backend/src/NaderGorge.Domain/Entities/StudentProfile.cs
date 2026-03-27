using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities;

public class StudentProfile : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    // --- Personal data ---
    public string StudentCode { get; set; } = string.Empty;   // Dostab student code
    public DateTime DateOfBirth { get; set; }
    public Gender Gender { get; set; }
    public string Governorate { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;

    // --- Parent data ---
    public string? ParentPhone { get; set; }
    public bool IsFatherAlive { get; set; } = true;
    public bool IsMotherAlive { get; set; } = true;

    // --- Academic data (conditional) ---
    public EducationStage EducationStage { get; set; }
    public GradeLevel GradeLevel { get; set; }

    /// <summary>
    /// Required only for SecondSecondary (Arts/Science) and
    /// SecondBaccalaureate (Medicine/Engineering/Business/ArtsAndHumanities).
    /// Null for FirstSecondary and FirstBaccalaureate.
    /// </summary>
    public StudyTrack? StudyTrack { get; set; }
}
