using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Services;

public sealed class AcademicScopeService : IAcademicScopeService
{
    private const string DeniedMessage = "هذا المحتوى غير متاح لمرحلتك أو صفك الحالي.";
    private readonly IAppDbContext _db;

    public AcademicScopeService(IAppDbContext db)
    {
        _db = db;
    }

    public Task<StudentProfile?> GetStudentProfileAsync(Guid studentId, CancellationToken ct = default)
    {
        return _db.StudentProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == studentId, ct);
    }

    public async Task<IReadOnlySet<Guid>> GetAllowedSubjectIdsAsync(EducationStage stage, GradeLevel grade, CancellationToken ct = default)
    {
        var ids = await _db.AcademicSubjectEligibilities
            .AsNoTracking()
            .Where(x => x.IsActive && x.EducationStage == stage && x.GradeLevel == grade)
            .Select(x => x.SubjectId)
            .ToListAsync(ct);

        return ids.ToHashSet();
    }

    public async Task<IReadOnlySet<Guid>> GetEligiblePackageIdsForStudentAsync(
        IReadOnlyCollection<Guid> packageIds,
        Guid studentId,
        CancellationToken ct = default)
    {
        if (packageIds.Count == 0)
            return new HashSet<Guid>();

        var profile = await GetStudentProfileAsync(studentId, ct);
        if (profile is null || !AcademicValidationService.IsGradeValidForStage(profile.EducationStage, profile.GradeLevel))
            return new HashSet<Guid>();

        var allowedSubjects = await GetAllowedSubjectIdsAsync(profile.EducationStage, profile.GradeLevel, ct);
        var scopes = await _db.StudentFacingAcademicScopes
            .AsNoTracking()
            .Where(x => x.OwnerType == StudentFacingScopeOwnerType.Package && packageIds.Contains(x.OwnerId))
            .ToListAsync(ct);

        return scopes
            .Where(scope => Matches(scope, profile, allowedSubjects))
            .Select(scope => scope.OwnerId)
            .ToHashSet();
    }

    public async Task<IReadOnlySet<Guid>> GetEligibleLessonVideoIdsForStudentAsync(
        IReadOnlyCollection<Guid> lessonVideoIds,
        Guid studentId,
        CancellationToken ct = default)
    {
        var distinctVideoIds = lessonVideoIds.Distinct().ToArray();
        if (distinctVideoIds.Length == 0)
            return new HashSet<Guid>();

        var profile = await GetStudentProfileAsync(studentId, ct);
        if (profile is null
            || !AcademicValidationService.IsGradeValidForStage(
                profile.EducationStage,
                profile.GradeLevel))
        {
            return new HashSet<Guid>();
        }

        var paths = await _db.LessonVideos
            .AsNoTracking()
            .Where(video => distinctVideoIds.Contains(video.Id))
            .Select(video => new LessonVideoAcademicPath(
                video.Id,
                video.LessonId,
                video.Lesson.ContentSectionId,
                video.Lesson.ContentSection.TermId,
                video.Lesson.ContentSection.Term.PackageId))
            .ToListAsync(ct);
        if (paths.Count == 0)
            return new HashSet<Guid>();

        var videoIds = paths.Select(path => path.VideoId).Distinct().ToArray();
        var lessonIds = paths.Select(path => path.LessonId).Distinct().ToArray();
        var sectionIds = paths.Select(path => path.SectionId).Distinct().ToArray();
        var termIds = paths.Select(path => path.TermId).Distinct().ToArray();
        var packageIds = paths.Select(path => path.PackageId).Distinct().ToArray();
        var scopes = await _db.StudentFacingAcademicScopes
            .AsNoTracking()
            .Where(scope =>
                (scope.OwnerType == StudentFacingScopeOwnerType.LessonVideo
                    && videoIds.Contains(scope.OwnerId))
                || (scope.OwnerType == StudentFacingScopeOwnerType.Lesson
                    && lessonIds.Contains(scope.OwnerId))
                || (scope.OwnerType == StudentFacingScopeOwnerType.ContentSection
                    && sectionIds.Contains(scope.OwnerId))
                || (scope.OwnerType == StudentFacingScopeOwnerType.Term
                    && termIds.Contains(scope.OwnerId))
                || (scope.OwnerType == StudentFacingScopeOwnerType.Package
                    && packageIds.Contains(scope.OwnerId)))
            .ToListAsync(ct);

        IReadOnlySet<Guid> allowedSubjects = scopes.Any(scope => scope.ScopeLevel == AcademicScopeLevel.Exact)
            ? await GetAllowedSubjectIdsAsync(profile.EducationStage, profile.GradeLevel, ct)
            : new HashSet<Guid>();
        var scopeLookup = scopes.ToLookup(scope => new AcademicScopeOwner(scope.OwnerType, scope.OwnerId));
        var eligibleVideoIds = new HashSet<Guid>();
        foreach (var path in paths)
        {
            var effectiveScopes = GetEffectiveScopes(path, scopeLookup);
            if (effectiveScopes.Any(scope => Matches(scope, profile, allowedSubjects)))
                eligibleVideoIds.Add(path.VideoId);
        }

        return eligibleVideoIds;
    }

    public async Task<IReadOnlySet<Guid>> GetEligibleLessonIdsForStudentAsync(
        IReadOnlyCollection<Guid> lessonIds,
        Guid studentId,
        CancellationToken ct = default)
    {
        var distinctLessonIds = lessonIds.Distinct().ToArray();
        if (distinctLessonIds.Length == 0)
            return new HashSet<Guid>();

        var profile = await GetStudentProfileAsync(studentId, ct);
        if (profile is null
            || !AcademicValidationService.IsGradeValidForStage(
                profile.EducationStage,
                profile.GradeLevel))
        {
            return new HashSet<Guid>();
        }

        var paths = await _db.Lessons
            .AsNoTracking()
            .Where(lesson => distinctLessonIds.Contains(lesson.Id))
            .Select(lesson => new LessonAcademicPath(
                lesson.Id,
                lesson.ContentSectionId,
                lesson.ContentSection.TermId,
                lesson.ContentSection.Term.PackageId))
            .ToListAsync(ct);
        if (paths.Count == 0)
            return new HashSet<Guid>();

        var resolvedLessonIds = paths.Select(path => path.LessonId).Distinct().ToArray();
        var sectionIds = paths.Select(path => path.SectionId).Distinct().ToArray();
        var termIds = paths.Select(path => path.TermId).Distinct().ToArray();
        var packageIds = paths.Select(path => path.PackageId).Distinct().ToArray();
        var scopes = await _db.StudentFacingAcademicScopes
            .AsNoTracking()
            .Where(scope =>
                (scope.OwnerType == StudentFacingScopeOwnerType.Lesson
                    && resolvedLessonIds.Contains(scope.OwnerId))
                || (scope.OwnerType == StudentFacingScopeOwnerType.ContentSection
                    && sectionIds.Contains(scope.OwnerId))
                || (scope.OwnerType == StudentFacingScopeOwnerType.Term
                    && termIds.Contains(scope.OwnerId))
                || (scope.OwnerType == StudentFacingScopeOwnerType.Package
                    && packageIds.Contains(scope.OwnerId)))
            .ToListAsync(ct);

        IReadOnlySet<Guid> allowedSubjects = scopes.Any(scope => scope.ScopeLevel == AcademicScopeLevel.Exact)
            ? await GetAllowedSubjectIdsAsync(profile.EducationStage, profile.GradeLevel, ct)
            : new HashSet<Guid>();
        var scopeLookup = scopes.ToLookup(scope => new AcademicScopeOwner(scope.OwnerType, scope.OwnerId));
        return paths
            .Where(path => GetEffectiveScopes(path, scopeLookup)
                .Any(scope => Matches(scope, profile, allowedSubjects)))
            .Select(path => path.LessonId)
            .ToHashSet();
    }

    public async Task<bool> IsOwnerEligibleForStudentAsync(StudentFacingScopeOwnerType ownerType, Guid ownerId, Guid studentId, CancellationToken ct = default)
    {
        if (ownerType == StudentFacingScopeOwnerType.LessonVideo)
        {
            var eligibleVideoIds = await GetEligibleLessonVideoIdsForStudentAsync(
                [ownerId],
                studentId,
                ct);
            return eligibleVideoIds.Contains(ownerId);
        }

        var result = await ValidateStudentCanUseTargetAsync(ownerType, ownerId, studentId, ct);
        return result.IsEligible;
    }

    public async Task<AcademicScopeCheckResult> ValidateTargetHasScopeAsync(StudentFacingScopeOwnerType ownerType, Guid ownerId, CancellationToken ct = default)
    {
        var scopes = await ResolveEffectiveScopesAsync(ownerType, ownerId, ct);
        return scopes.Count == 0
            ? AcademicScopeCheckResult.Denied("ACADEMIC_SCOPE_TARGET_UNSCOPED", "هدف المحتوى يجب أن يكون مربوطا بنطاق أكاديمي صالح أو نطاق عام صريح.")
            : AcademicScopeCheckResult.Eligible();
    }

    public async Task<AcademicScopeCheckResult> ValidateStudentCanUseTargetAsync(StudentFacingScopeOwnerType ownerType, Guid ownerId, Guid studentId, CancellationToken ct = default)
    {
        var profile = await GetStudentProfileAsync(studentId, ct);
        if (profile is null)
            return AcademicScopeCheckResult.Denied("STUDENT_PROFILE_REQUIRED", "ملف الطالب الأكاديمي غير مكتمل.");

        if (!AcademicValidationService.IsGradeValidForStage(profile.EducationStage, profile.GradeLevel))
            return AcademicScopeCheckResult.Denied("STUDENT_PROFILE_REQUIRED", "بيانات المرحلة أو الصف غير صالحة.");

        var scopes = await ResolveEffectiveScopesAsync(ownerType, ownerId, ct);
        if (scopes.Count == 0)
            return AcademicScopeCheckResult.Denied("ACADEMIC_SCOPE_TARGET_UNSCOPED", DeniedMessage);

        var allowedSubjects = await GetAllowedSubjectIdsAsync(profile.EducationStage, profile.GradeLevel, ct);
        return scopes.Any(scope => Matches(scope, profile, allowedSubjects))
            ? AcademicScopeCheckResult.Eligible()
            : AcademicScopeCheckResult.Denied("ACADEMIC_SCOPE_DENIED", DeniedMessage);
    }

    public async Task<IReadOnlyList<StudentFacingAcademicScope>> ResolveEffectiveScopesAsync(StudentFacingScopeOwnerType ownerType, Guid ownerId, CancellationToken ct = default)
    {
        var explicitScopes = await LoadScopesAsync(ownerType, ownerId, ct);
        if (explicitScopes.Count > 0)
            return explicitScopes;

        var parent = await ResolveParentAsync(ownerType, ownerId, ct);
        if (parent is null)
            return explicitScopes;

        return await ResolveEffectiveScopesAsync(parent.Value.OwnerType, parent.Value.OwnerId, ct);
    }

    public async Task<AcademicScopeValidationResult> ValidateScopeDtosAsync(IReadOnlyList<AcademicScopeDto>? scopes, CancellationToken ct = default)
    {
        if (scopes == null || scopes.Count == 0)
            return AcademicScopeValidationResult.Invalid("ACADEMIC_SCOPE_REQUIRED", "يجب تحديد نطاق أكاديمي واحد على الأقل.");

        foreach (var scope in scopes)
        {
            var result = await ValidateScopeDtoAsync(scope, ct);
            if (!result.IsValid)
                return result;
        }

        return AcademicScopeValidationResult.Valid();
    }

    public async Task<AcademicScopeValidationResult> SyncOwnerScopesAsync(
        StudentFacingScopeOwnerType ownerType,
        Guid ownerId,
        IReadOnlyList<AcademicScopeDto>? scopes,
        Guid? createdByUserId = null,
        CancellationToken ct = default)
    {
        var validation = await ValidateScopeDtosAsync(scopes, ct);
        if (!validation.IsValid)
            return validation;

        var existing = await _db.StudentFacingAcademicScopes
            .Where(x => x.OwnerType == ownerType && x.OwnerId == ownerId)
            .ToListAsync(ct);
        _db.StudentFacingAcademicScopes.RemoveRange(existing);

        foreach (var scope in scopes!)
        {
            _db.StudentFacingAcademicScopes.Add(new StudentFacingAcademicScope
            {
                OwnerType = ownerType,
                OwnerId = ownerId,
                ScopeLevel = scope.ScopeLevel,
                EducationStage = scope.EducationStage,
                GradeLevel = scope.GradeLevel,
                SubjectId = scope.SubjectId,
                CreatedByUserId = createdByUserId
            });
        }

        await _db.SaveChangesAsync(ct);
        return AcademicScopeValidationResult.Valid();
    }

    public static IReadOnlyList<AcademicScopeSummaryDto> ToScopeSummaries(IEnumerable<StudentFacingAcademicScope> scopes)
    {
        return scopes
            .Select(scope => new AcademicScopeSummaryDto(
                scope.ScopeLevel,
                scope.EducationStage,
                scope.GradeLevel,
                scope.SubjectId,
                BuildScopeLabel(scope.ScopeLevel, scope.EducationStage, scope.GradeLevel, scope.SubjectId)))
            .ToList();
    }

    public static bool TryNormalizeGradeAlias(string? value, out GradeLevel grade)
    {
        grade = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = value.Trim().Replace("-", " ", StringComparison.Ordinal).Replace("_", " ", StringComparison.Ordinal);
        var compact = normalized.Replace(" ", "", StringComparison.Ordinal).ToLowerInvariant();

        var aliases = new Dictionary<string, GradeLevel>
        {
            ["firstsecondary"] = GradeLevel.FirstSecondary,
            ["1stsecondary"] = GradeLevel.FirstSecondary,
            ["firstsec"] = GradeLevel.FirstSecondary,
            ["secondsecondary"] = GradeLevel.SecondSecondary,
            ["2ndsecondary"] = GradeLevel.SecondSecondary,
            ["secondsec"] = GradeLevel.SecondSecondary,
            ["thirdsecondary"] = GradeLevel.SecondaryGrade3,
            ["3rdsecondary"] = GradeLevel.SecondaryGrade3,
            ["secondarygrade3"] = GradeLevel.SecondaryGrade3
        };

        if (aliases.TryGetValue(compact, out grade))
            return true;

        return Enum.TryParse(value, ignoreCase: true, out grade);
    }

    private async Task<AcademicScopeValidationResult> ValidateScopeDtoAsync(AcademicScopeDto scope, CancellationToken ct)
    {
        return scope.ScopeLevel switch
        {
            AcademicScopeLevel.PlatformWide => ValidatePlatformWide(scope),
            AcademicScopeLevel.StageWide => ValidateStageWide(scope),
            AcademicScopeLevel.GradeAllSubjects => ValidateGradeAllSubjects(scope),
            AcademicScopeLevel.Exact => await ValidateExactScopeAsync(scope, ct),
            _ => AcademicScopeValidationResult.Invalid("ACADEMIC_SCOPE_INVALID_LEVEL", "مستوى النطاق الأكاديمي غير صالح.")
        };
    }

    private static AcademicScopeValidationResult ValidatePlatformWide(AcademicScopeDto scope)
    {
        return scope.EducationStage.HasValue || scope.GradeLevel.HasValue || scope.SubjectId.HasValue
            ? AcademicScopeValidationResult.Invalid("ACADEMIC_SCOPE_INVALID_PLATFORM_WIDE", "النطاق العام للمنصة لا يقبل مرحلة أو صفا أو مادة.")
            : AcademicScopeValidationResult.Valid();
    }

    private static AcademicScopeValidationResult ValidateStageWide(AcademicScopeDto scope)
    {
        if (!scope.EducationStage.HasValue)
            return AcademicScopeValidationResult.Invalid("ACADEMIC_SCOPE_STAGE_REQUIRED", "نطاق المرحلة يتطلب تحديد المرحلة.");

        return scope.GradeLevel.HasValue || scope.SubjectId.HasValue
            ? AcademicScopeValidationResult.Invalid("ACADEMIC_SCOPE_INVALID_STAGE_WIDE", "نطاق المرحلة لا يقبل صفا أو مادة.")
            : AcademicScopeValidationResult.Valid();
    }

    private static AcademicScopeValidationResult ValidateGradeAllSubjects(AcademicScopeDto scope)
    {
        if (!scope.EducationStage.HasValue || !scope.GradeLevel.HasValue)
            return AcademicScopeValidationResult.Invalid("ACADEMIC_SCOPE_GRADE_REQUIRED", "نطاق كل مواد الصف يتطلب المرحلة والصف.");

        if (!AcademicValidationService.IsGradeValidForStage(scope.EducationStage.Value, scope.GradeLevel.Value))
            return AcademicScopeValidationResult.Invalid("ACADEMIC_SCOPE_INVALID_GRADE", "الصف لا يتبع المرحلة المحددة.");

        return scope.SubjectId.HasValue
            ? AcademicScopeValidationResult.Invalid("ACADEMIC_SCOPE_INVALID_GRADE_ALL_SUBJECTS", "نطاق كل مواد الصف لا يقبل مادة محددة.")
            : AcademicScopeValidationResult.Valid();
    }

    private async Task<AcademicScopeValidationResult> ValidateExactScopeAsync(AcademicScopeDto scope, CancellationToken ct)
    {
        if (!scope.EducationStage.HasValue || !scope.GradeLevel.HasValue || !scope.SubjectId.HasValue)
            return AcademicScopeValidationResult.Invalid("ACADEMIC_SCOPE_EXACT_REQUIRED", "النطاق المحدد يتطلب المرحلة والصف والمادة.");

        if (!AcademicValidationService.IsGradeValidForStage(scope.EducationStage.Value, scope.GradeLevel.Value))
            return AcademicScopeValidationResult.Invalid("ACADEMIC_SCOPE_INVALID_GRADE", "الصف لا يتبع المرحلة المحددة.");

        var isAllowedSubject = await _db.AcademicSubjectEligibilities
            .AsNoTracking()
            .AnyAsync(x =>
                x.IsActive &&
                x.EducationStage == scope.EducationStage.Value &&
                x.GradeLevel == scope.GradeLevel.Value &&
                x.SubjectId == scope.SubjectId.Value,
                ct);

        return isAllowedSubject
            ? AcademicScopeValidationResult.Valid()
            : AcademicScopeValidationResult.Invalid("ACADEMIC_SCOPE_INVALID_SUBJECT", "المادة غير مفعلة لهذا الصف والمرحلة.");
    }

    private static string BuildScopeLabel(AcademicScopeLevel level, EducationStage? stage, GradeLevel? grade, Guid? subjectId)
    {
        return level switch
        {
            AcademicScopeLevel.PlatformWide => "عام للمنصة",
            AcademicScopeLevel.StageWide => stage.HasValue ? $"عام لمرحلة {stage.Value}" : "عام لمرحلة غير محددة",
            AcademicScopeLevel.GradeAllSubjects => stage.HasValue && grade.HasValue ? $"عام لكل مواد {stage.Value} / {grade.Value}" : "عام لكل مواد صف غير محدد",
            AcademicScopeLevel.Exact => stage.HasValue && grade.HasValue && subjectId.HasValue ? $"{stage.Value} / {grade.Value} / مادة محددة" : "نطاق مادة غير مكتمل",
            _ => "نطاق غير معروف"
        };
    }

    private async Task<List<StudentFacingAcademicScope>> LoadScopesAsync(StudentFacingScopeOwnerType ownerType, Guid ownerId, CancellationToken ct)
    {
        return await _db.StudentFacingAcademicScopes
            .AsNoTracking()
            .Where(x => x.OwnerType == ownerType && x.OwnerId == ownerId)
            .ToListAsync(ct);
    }

    private static bool Matches(StudentFacingAcademicScope scope, StudentProfile profile, IReadOnlySet<Guid> allowedSubjects)
    {
        return scope.ScopeLevel switch
        {
            AcademicScopeLevel.PlatformWide => true,
            AcademicScopeLevel.StageWide => scope.EducationStage == profile.EducationStage,
            AcademicScopeLevel.GradeAllSubjects => scope.EducationStage == profile.EducationStage && scope.GradeLevel == profile.GradeLevel,
            AcademicScopeLevel.Exact => scope.EducationStage == profile.EducationStage &&
                                        scope.GradeLevel == profile.GradeLevel &&
                                        scope.SubjectId.HasValue &&
                                        allowedSubjects.Contains(scope.SubjectId.Value),
            _ => false
        };
    }

    private static IEnumerable<StudentFacingAcademicScope> GetEffectiveScopes(
        LessonVideoAcademicPath path,
        ILookup<AcademicScopeOwner, StudentFacingAcademicScope> scopeLookup)
    {
        var owners = new[]
        {
            new AcademicScopeOwner(StudentFacingScopeOwnerType.LessonVideo, path.VideoId),
            new AcademicScopeOwner(StudentFacingScopeOwnerType.Lesson, path.LessonId),
            new AcademicScopeOwner(StudentFacingScopeOwnerType.ContentSection, path.SectionId),
            new AcademicScopeOwner(StudentFacingScopeOwnerType.Term, path.TermId),
            new AcademicScopeOwner(StudentFacingScopeOwnerType.Package, path.PackageId)
        };

        foreach (var owner in owners)
        {
            var scopes = scopeLookup[owner];
            if (scopes.Any())
                return scopes;
        }

        return [];
    }

    private static IEnumerable<StudentFacingAcademicScope> GetEffectiveScopes(
        LessonAcademicPath path,
        ILookup<AcademicScopeOwner, StudentFacingAcademicScope> scopeLookup)
    {
        var owners = new[]
        {
            new AcademicScopeOwner(StudentFacingScopeOwnerType.Lesson, path.LessonId),
            new AcademicScopeOwner(StudentFacingScopeOwnerType.ContentSection, path.SectionId),
            new AcademicScopeOwner(StudentFacingScopeOwnerType.Term, path.TermId),
            new AcademicScopeOwner(StudentFacingScopeOwnerType.Package, path.PackageId)
        };

        foreach (var owner in owners)
        {
            var scopes = scopeLookup[owner];
            if (scopes.Any())
                return scopes;
        }

        return [];
    }

    private async Task<(StudentFacingScopeOwnerType OwnerType, Guid OwnerId)?> ResolveParentAsync(StudentFacingScopeOwnerType ownerType, Guid ownerId, CancellationToken ct)
    {
        Guid? parentId;

        switch (ownerType)
        {
            case StudentFacingScopeOwnerType.Term:
                parentId = await _db.Terms
                    .AsNoTracking()
                    .Where(x => x.Id == ownerId)
                    .Select(x => (Guid?)x.PackageId)
                    .FirstOrDefaultAsync(ct);
                return parentId.HasValue ? (StudentFacingScopeOwnerType.Package, parentId.Value) : null;

            case StudentFacingScopeOwnerType.ContentSection:
                parentId = await _db.ContentSections
                    .AsNoTracking()
                    .Where(x => x.Id == ownerId)
                    .Select(x => (Guid?)x.TermId)
                    .FirstOrDefaultAsync(ct);
                return parentId.HasValue ? (StudentFacingScopeOwnerType.Term, parentId.Value) : null;

            case StudentFacingScopeOwnerType.Lesson:
                parentId = await _db.Lessons
                    .AsNoTracking()
                    .Where(x => x.Id == ownerId)
                    .Select(x => (Guid?)x.ContentSectionId)
                    .FirstOrDefaultAsync(ct);
                return parentId.HasValue ? (StudentFacingScopeOwnerType.ContentSection, parentId.Value) : null;

            case StudentFacingScopeOwnerType.LessonVideo:
                parentId = await _db.LessonVideos
                    .AsNoTracking()
                    .Where(x => x.Id == ownerId)
                    .Select(x => (Guid?)x.LessonId)
                    .FirstOrDefaultAsync(ct);
                return parentId.HasValue ? (StudentFacingScopeOwnerType.Lesson, parentId.Value) : null;

            case StudentFacingScopeOwnerType.Exam:
                return await ResolveExamParentAsync(ownerId, ct);

            case StudentFacingScopeOwnerType.CommunityPost:
                parentId = await _db.CommunityPosts
                    .AsNoTracking()
                    .Where(x => x.Id == ownerId)
                    .Select(x => x.TeacherId)
                    .FirstOrDefaultAsync(ct);
                return parentId.HasValue ? (StudentFacingScopeOwnerType.Teacher, parentId.Value) : null;

            case StudentFacingScopeOwnerType.SharedTeacherPackageItem:
                parentId = await _db.SharedTeacherPackageItems
                    .AsNoTracking()
                    .Where(x => x.Id == ownerId)
                    .Select(x => (Guid?)x.SharedTeacherPackageId)
                    .FirstOrDefaultAsync(ct);
                return parentId.HasValue ? (StudentFacingScopeOwnerType.SharedTeacherPackage, parentId.Value) : null;

            default:
                return null;
        }
    }

    private async Task<(StudentFacingScopeOwnerType OwnerType, Guid OwnerId)?> ResolveExamParentAsync(Guid examId, CancellationToken ct)
    {
        var lessonId = await _db.Lessons
            .AsNoTracking()
            .Where(x => x.ExamId == examId)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(ct);
        if (lessonId.HasValue)
            return (StudentFacingScopeOwnerType.Lesson, lessonId.Value);

        var videoId = await _db.LessonVideos
            .AsNoTracking()
            .Where(x => x.ExamId == examId)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(ct);
        if (videoId.HasValue)
            return (StudentFacingScopeOwnerType.LessonVideo, videoId.Value);

        var linkedVideoId = await _db.Exams
            .AsNoTracking()
            .Where(x => x.Id == examId && x.LessonVideoId != null)
            .Select(x => x.LessonVideoId)
            .FirstOrDefaultAsync(ct);
        return linkedVideoId.HasValue ? (StudentFacingScopeOwnerType.LessonVideo, linkedVideoId.Value) : null;
    }

    private sealed record LessonVideoAcademicPath(
        Guid VideoId,
        Guid LessonId,
        Guid SectionId,
        Guid TermId,
        Guid PackageId);

    private sealed record LessonAcademicPath(
        Guid LessonId,
        Guid SectionId,
        Guid TermId,
        Guid PackageId);

    private sealed record AcademicScopeOwner(
        StudentFacingScopeOwnerType OwnerType,
        Guid OwnerId);
}
