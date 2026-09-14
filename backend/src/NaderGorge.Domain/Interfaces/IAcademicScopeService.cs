using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Interfaces;

public sealed record AcademicScopeCheckResult(bool IsEligible, string? ErrorCode = null, string? Message = null)
{
    public static AcademicScopeCheckResult Eligible() => new(true);
    public static AcademicScopeCheckResult Denied(string code, string message) => new(false, code, message);
}

public interface IAcademicScopeService
{
    Task<StudentProfile?> GetStudentProfileAsync(Guid studentId, CancellationToken ct = default);
    Task<IReadOnlySet<Guid>> GetAllowedSubjectIdsAsync(EducationStage stage, GradeLevel grade, CancellationToken ct = default);
    Task<IReadOnlySet<Guid>> GetEligiblePackageIdsForStudentAsync(IReadOnlyCollection<Guid> packageIds, Guid studentId, CancellationToken ct = default);
    Task<IReadOnlySet<Guid>> GetEligibleLessonIdsForStudentAsync(IReadOnlyCollection<Guid> lessonIds, Guid studentId, CancellationToken ct = default);
    Task<IReadOnlySet<Guid>> GetEligibleLessonVideoIdsForStudentAsync(IReadOnlyCollection<Guid> lessonVideoIds, Guid studentId, CancellationToken ct = default);
    Task<bool> IsOwnerEligibleForStudentAsync(StudentFacingScopeOwnerType ownerType, Guid ownerId, Guid studentId, CancellationToken ct = default);
    Task<AcademicScopeCheckResult> ValidateTargetHasScopeAsync(StudentFacingScopeOwnerType ownerType, Guid ownerId, CancellationToken ct = default);
    Task<AcademicScopeCheckResult> ValidateStudentCanUseTargetAsync(StudentFacingScopeOwnerType ownerType, Guid ownerId, Guid studentId, CancellationToken ct = default);
    Task<IReadOnlyList<StudentFacingAcademicScope>> ResolveEffectiveScopesAsync(StudentFacingScopeOwnerType ownerType, Guid ownerId, CancellationToken ct = default);
}
