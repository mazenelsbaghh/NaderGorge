using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities;

public class AcademicSubjectEligibility : BaseEntity
{
    public EducationStage EducationStage { get; set; }
    public GradeLevel GradeLevel { get; set; }
    public Guid SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;
    public bool IsActive { get; set; } = true;
}

public class StudentFacingAcademicScope : BaseEntity
{
    public StudentFacingScopeOwnerType OwnerType { get; set; }
    public Guid OwnerId { get; set; }
    public AcademicScopeLevel ScopeLevel { get; set; }
    public EducationStage? EducationStage { get; set; }
    public GradeLevel? GradeLevel { get; set; }
    public Guid? SubjectId { get; set; }
    public Subject? Subject { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
}
