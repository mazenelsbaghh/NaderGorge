using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Common;

public sealed record AcademicScopeDto(
    AcademicScopeLevel ScopeLevel,
    EducationStage? EducationStage = null,
    GradeLevel? GradeLevel = null,
    Guid? SubjectId = null);

public sealed record AcademicScopeValidationResult(
    bool IsValid,
    string? ErrorCode = null,
    string? Message = null)
{
    public static AcademicScopeValidationResult Valid() => new(true);
    public static AcademicScopeValidationResult Invalid(string errorCode, string message) => new(false, errorCode, message);
}

public sealed record AcademicScopeSummaryDto(
    AcademicScopeLevel ScopeLevel,
    EducationStage? EducationStage,
    GradeLevel? GradeLevel,
    Guid? SubjectId,
    string Label);
