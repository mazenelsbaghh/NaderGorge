using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Application.Features.Admin.VideoTypes;

namespace NaderGorge.Application.Features.Admin.Commands;

public record CreatePackageCommand(
    string Name,
    string Description,
    decimal Price,
    Guid SubjectId,
    string TargetGrade,
    Guid? TeacherId = null,
    Guid? CurrentUserId = null,
    IReadOnlyList<AcademicScopeDto>? AcademicScopes = null) : IRequest<ApiResponse<Guid>>
{
    public PackageContentMode ContentMode { get; init; } = PackageContentMode.TermWithSections;
}

public class CreatePackageCommandHandler : IRequestHandler<CreatePackageCommand, ApiResponse<Guid>>
{
    private readonly IAppDbContext _db;
    private readonly TeacherAuthorizationService _auth;

    public CreatePackageCommandHandler(IAppDbContext db, TeacherAuthorizationService auth)
    {
        _db = db;
        _auth = auth;
    }

    public async Task<ApiResponse<Guid>> Handle(CreatePackageCommand request, CancellationToken ct)
    {
        if (request.ContentMode is not (PackageContentMode.TermWithSections
            or PackageContentMode.SectionWithLessons
            or PackageContentMode.LessonsOnly
            or PackageContentMode.SingleLesson))
            return ApiResponse<Guid>.Fail("نوع هيكل الكورس غير صالح.");

        if (request.AcademicScopes is { Count: 0 })
            return ApiResponse<Guid>.Fail("يجب تحديد نطاق أكاديمي واحد على الأقل.", new List<string> { "ACADEMIC_SCOPE_REQUIRED" });

        if (request.SubjectId == Guid.Empty)
        {
            return ApiResponse<Guid>.Fail("Subject is required.");
        }

        var subjectExists = await _db.Subjects.AnyAsync(s => s.Id == request.SubjectId, ct);
        if (!subjectExists)
        {
            return ApiResponse<Guid>.Fail("Subject not found.");
        }

        var teacherId = Guid.Empty;
        if (request.CurrentUserId.HasValue)
        {
            var user = await _db.Users
                .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .Include(u => u.TeacherProfile)
                .FirstOrDefaultAsync(u => u.Id == request.CurrentUserId.Value, ct);

            if (user != null && user.UserRoles.Any(ur => ur.Role.Type == RoleType.Teacher))
            {
                if (user.TeacherProfile == null)
                    return ApiResponse<Guid>.Fail("Teacher profile not onboarded.");
                teacherId = user.TeacherProfile.Id;

                // Verify the teacher can access the subject
                var teachesThisSubject = await _db.TeacherSubjects.AnyAsync(ts => ts.TeacherId == teacherId && ts.SubjectId == request.SubjectId, ct);
                if (!teachesThisSubject)
                    return ApiResponse<Guid>.Fail("Unauthorized access to this subject.");
            }
        }

        if (teacherId == Guid.Empty)
        {
            if (!request.TeacherId.HasValue || request.TeacherId.Value == Guid.Empty)
            {
                return ApiResponse<Guid>.Fail("Teacher is required.");
            }

            var teacherExists = await _db.TeacherProfiles.AnyAsync(tp => tp.Id == request.TeacherId.Value, ct);
            if (!teacherExists)
                return ApiResponse<Guid>.Fail("Selected teacher not found.");

            teacherId = request.TeacherId.Value;
        }

        // Verify that the resolved teacher actually teaches the subject
        var teachesSubject = await _db.TeacherSubjects.AnyAsync(ts => ts.TeacherId == teacherId && ts.SubjectId == request.SubjectId, ct);
        if (!teachesSubject)
        {
            return ApiResponse<Guid>.Fail("Selected teacher does not teach this subject.");
        }

        // Validate the TargetGrade is within the teacher's specialization (grades)
        var teacherProfile = await _db.TeacherProfiles.FirstOrDefaultAsync(tp => tp.Id == teacherId, ct);
        if (teacherProfile == null)
        {
            return ApiResponse<Guid>.Fail("Selected teacher profile not found.");
        }

        var requestedGrades = ContentAcademicScopeValidation.GetTargetGrades(request.AcademicScopes, request.TargetGrade);
        if (requestedGrades.Count == 0)
        {
            return ApiResponse<Guid>.Fail("يجب اختيار صف دراسي واحد على الأقل.");
        }

        var allowedGrades = teacherProfile.Specialization
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(g => {
                if (g == "1st Secondary") return "FirstSecondary";
                if (g == "2nd Secondary") return "SecondSecondary";
                if (g == "3rd Secondary") return "SecondaryGrade3";
                return g;
            })
            .ToList();

        var unsupportedGrades = requestedGrades
            .Where(grade => !allowedGrades.Any(allowed => string.Equals(allowed, grade, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (unsupportedGrades.Count > 0)
        {
            return ApiResponse<Guid>.Fail("المدرس غير مخصص للصف أو الصفوف المختارة.");
        }

        await ContentAcademicScopeValidation.EnsureExactScopeSubjectEligibilityAsync(_db, request.AcademicScopes, ct);
        var scopeValidation = await ContentAcademicScopeValidation.ValidateScopesOrPackageLegacyAsync(_db, request.AcademicScopes, request.TargetGrade, ct);
        if (!scopeValidation.IsEligible)
            return ApiResponse<Guid>.Fail(scopeValidation.Message ?? "نطاق الباقة الأكاديمي غير صالح.", new List<string> { scopeValidation.ErrorCode ?? "ACADEMIC_SCOPE_INVALID" });

        var pkg = new Package
        {
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            SubjectId = request.SubjectId,
            TargetGrade = string.Join(',', requestedGrades),
            TeacherId = teacherId,
            ContentMode = request.ContentMode
        };
        _db.Packages.Add(pkg);

        // Keep the old required Term -> Section -> Lesson relationships intact
        // while making direct sections/lessons appear at package level. These
        // containers are never returned as visible course content.
        if (request.ContentMode is PackageContentMode.SectionWithLessons
            or PackageContentMode.LessonsOnly
            or PackageContentMode.SingleLesson)
        {
            var rootTerm = new Term
            {
                PackageId = pkg.Id,
                Title = "المحتوى المباشر",
                Order = -1,
                Price = request.ContentMode == PackageContentMode.SectionWithLessons ? request.Price : 0,
                IsSystemContainer = true
            };
            pkg.Terms.Add(rootTerm);

            if (request.ContentMode is PackageContentMode.LessonsOnly
                or PackageContentMode.SingleLesson)
            {
                var rootSection = new ContentSection
                {
                    TermId = rootTerm.Id,
                    Title = "الحصص المباشرة",
                    Order = -1,
                    Price = request.ContentMode == PackageContentMode.LessonsOnly ? request.Price : 0,
                    IsSystemContainer = true
                };
                rootTerm.Sections.Add(rootSection);

                if (request.ContentMode == PackageContentMode.SingleLesson)
                {
                    rootSection.Lessons.Add(new Lesson
                    {
                        Title = request.Name,
                        Summary = request.Description,
                        Order = 1,
                        Price = request.Price
                    });
                }
            }
        }

        var outboxEvent = new OutboxEvent
        {
            Type = "PackageCreated",
            TargetGroup = "Role_Student",
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                packageId = pkg.Id,
                name = pkg.Name,
                price = pkg.Price
            })
        };
        _db.OutboxEvents.Add(outboxEvent);

        await _db.SaveChangesAsync(ct);
        await ContentAcademicScopeValidation.SyncScopesOrPackageLegacyAsync(
            _db,
            StudentFacingScopeOwnerType.Package,
            pkg.Id,
            request.AcademicScopes,
            request.TargetGrade,
            request.CurrentUserId,
            ct);

        return ApiResponse<Guid>.Ok(pkg.Id);
    }
}

// --- Terms ---

// --- Toggle Package Visibility ---
public record TogglePackageActiveCommand(Guid PackageId, Guid CurrentUserId) : IRequest<ApiResponse<bool>>;

public class TogglePackageActiveCommandHandler : IRequestHandler<TogglePackageActiveCommand, ApiResponse<bool>>
{
    private readonly IAppDbContext _db;
    private readonly TeacherAuthorizationService _auth;

    public TogglePackageActiveCommandHandler(IAppDbContext db, TeacherAuthorizationService auth)
    {
        _db = db;
        _auth = auth;
    }

    public async Task<ApiResponse<bool>> Handle(TogglePackageActiveCommand request, CancellationToken ct)
    {
        var canAccess = await _auth.CanAccessPackageAsync(request.CurrentUserId, request.PackageId, ct);
        if (!canAccess) return ApiResponse<bool>.Fail("Unauthorized access to this package.");

        var pkg = await _db.Packages.FindAsync(new object[] { request.PackageId }, ct);
        if (pkg == null) return ApiResponse<bool>.Fail("Package not found.");

        pkg.IsActive = !pkg.IsActive;
        await _db.SaveChangesAsync(ct);

        return ApiResponse<bool>.Ok(pkg.IsActive);
    }
}

internal static class ContentAcademicScopeValidation
{
    public static IReadOnlyList<string> GetTargetGrades(
        IReadOnlyList<AcademicScopeDto>? scopes,
        string? fallbackTargetGrade)
    {
        var grades = scopes?
            .Where(scope => scope.GradeLevel.HasValue)
            .Select(scope => scope.GradeLevel!.Value.ToString())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (grades is { Count: > 0 })
            return grades;

        return string.IsNullOrWhiteSpace(fallbackTargetGrade)
            ? []
            : fallbackTargetGrade
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(NormalizeGradeAlias)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
    }

    public static async Task<AcademicScopeCheckResult> ValidateExplicitScopesAsync(
        IAppDbContext db,
        IReadOnlyList<AcademicScopeDto>? scopes,
        CancellationToken ct)
    {
        if (scopes == null)
            return AcademicScopeCheckResult.Eligible();

        return await ValidateScopesAsync(db, scopes, ct);
    }

    public static async Task<AcademicScopeCheckResult> ValidateScopesOrPackageLegacyAsync(
        IAppDbContext db,
        IReadOnlyList<AcademicScopeDto>? scopes,
        string? targetGrade,
        CancellationToken ct)
    {
        return await ValidateScopesAsync(db, scopes ?? BuildPackageLegacyScopes(targetGrade), ct);
    }

    public static async Task EnsureExactScopeSubjectEligibilityAsync(
        IAppDbContext db,
        IReadOnlyList<AcademicScopeDto>? scopes,
        CancellationToken ct)
    {
        if (scopes == null || scopes.Count == 0)
            return;

        foreach (var scope in scopes)
        {
            if (scope.ScopeLevel != AcademicScopeLevel.Exact ||
                !scope.EducationStage.HasValue ||
                !scope.GradeLevel.HasValue ||
                !scope.SubjectId.HasValue)
            {
                continue;
            }

            var subjectExists = await db.Subjects.AnyAsync(subject => subject.Id == scope.SubjectId.Value, ct);
            if (!subjectExists)
                continue;

            var existing = await db.AcademicSubjectEligibilities.FirstOrDefaultAsync(item =>
                item.EducationStage == scope.EducationStage.Value &&
                item.GradeLevel == scope.GradeLevel.Value &&
                item.SubjectId == scope.SubjectId.Value,
                ct);

            if (existing == null)
            {
                db.AcademicSubjectEligibilities.Add(new AcademicSubjectEligibility
                {
                    EducationStage = scope.EducationStage.Value,
                    GradeLevel = scope.GradeLevel.Value,
                    SubjectId = scope.SubjectId.Value,
                    IsActive = true,
                });
            }
            else if (!existing.IsActive)
            {
                existing.IsActive = true;
            }
        }

        await db.SaveChangesAsync(ct);
    }

    public static async Task SyncExplicitScopesAsync(
        IAppDbContext db,
        StudentFacingScopeOwnerType ownerType,
        Guid ownerId,
        IReadOnlyList<AcademicScopeDto>? scopes,
        Guid? actorId,
        CancellationToken ct)
    {
        if (scopes == null)
            return;

        await new AcademicScopeService(db).SyncOwnerScopesAsync(ownerType, ownerId, scopes, actorId, ct);
    }

    public static async Task SyncScopesOrPackageLegacyAsync(
        IAppDbContext db,
        StudentFacingScopeOwnerType ownerType,
        Guid ownerId,
        IReadOnlyList<AcademicScopeDto>? scopes,
        string? targetGrade,
        Guid? actorId,
        CancellationToken ct)
    {
        await new AcademicScopeService(db).SyncOwnerScopesAsync(ownerType, ownerId, scopes ?? BuildPackageLegacyScopes(targetGrade), actorId, ct);
    }

    private static async Task<AcademicScopeCheckResult> ValidateScopesAsync(
        IAppDbContext db,
        IReadOnlyList<AcademicScopeDto> scopes,
        CancellationToken ct)
    {
        var result = await new AcademicScopeService(db).ValidateScopeDtosAsync(scopes, ct);
        return result.IsValid
            ? AcademicScopeCheckResult.Eligible()
            : AcademicScopeCheckResult.Denied(result.ErrorCode ?? "ACADEMIC_SCOPE_INVALID", result.Message ?? "نطاق أكاديمي غير صالح.");
    }

    private static IReadOnlyList<AcademicScopeDto> BuildPackageLegacyScopes(string? targetGrade)
    {
        if (string.Equals(targetGrade?.Trim(), "All", StringComparison.OrdinalIgnoreCase))
            return [new AcademicScopeDto(AcademicScopeLevel.PlatformWide)];

        return targetGrade?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(gradeValue => AcademicScopeService.TryNormalizeGradeAlias(gradeValue, out var grade)
                ? new AcademicScopeDto(AcademicScopeLevel.GradeAllSubjects, InferStageForGrade(grade), grade)
                : null)
            .OfType<AcademicScopeDto>()
            .ToList()
            ?? [];
    }

    private static string NormalizeGradeAlias(string grade)
    {
        return AcademicScopeService.TryNormalizeGradeAlias(grade, out var normalized)
            ? normalized.ToString()
            : grade;
    }

    private static EducationStage InferStageForGrade(GradeLevel grade)
    {
        foreach (EducationStage stage in Enum.GetValues<EducationStage>())
        {
            if (AcademicValidationService.IsGradeValidForStage(stage, grade))
                return stage;
        }

        return EducationStage.Secondary;
    }
}

public record CreateTermCommand(string Title, int Order, Guid PackageId, decimal Price, Guid? CurrentUserId = null, IReadOnlyList<AcademicScopeDto>? AcademicScopes = null) : IRequest<ApiResponse<Guid>>;

public class CreateTermCommandHandler : IRequestHandler<CreateTermCommand, ApiResponse<Guid>>
{
    private readonly IAppDbContext _db;
    private readonly TeacherAuthorizationService _auth;

    public CreateTermCommandHandler(IAppDbContext db, TeacherAuthorizationService auth)
    {
        _db = db;
        _auth = auth;
    }

    public async Task<ApiResponse<Guid>> Handle(CreateTermCommand request, CancellationToken ct)
    {
        var scopeValidation = await ContentAcademicScopeValidation.ValidateExplicitScopesAsync(_db, request.AcademicScopes, ct);
        if (!scopeValidation.IsEligible)
            return ApiResponse<Guid>.Fail(scopeValidation.Message ?? "نطاق الترم الأكاديمي غير صالح.", new List<string> { scopeValidation.ErrorCode ?? "ACADEMIC_SCOPE_INVALID" });

        if (request.CurrentUserId.HasValue)
        {
            var canAccess = await _auth.CanAccessPackageAsync(request.CurrentUserId.Value, request.PackageId, ct);
            if (!canAccess) return ApiResponse<Guid>.Fail("Unauthorized access to this package.");
        }

        var package = await _db.Packages.FirstOrDefaultAsync(item => item.Id == request.PackageId, ct);
        if (package == null)
            return ApiResponse<Guid>.Fail("Package not found");

        if (package.ContentMode != PackageContentMode.TermWithSections)
            return ApiResponse<Guid>.Fail("هذا الكورس لا يستخدم أترامًا ظاهرة؛ أضف المحتوى من هيكل الكورس المحدد.");

        var term = new Term
        {
            Title = request.Title,
            Order = request.Order,
            PackageId = request.PackageId,
            Price = request.Price
        };
        _db.Terms.Add(term);

        var outboxEvent = new OutboxEvent
        {
            Type = "TermCreated",
            TargetGroup = $"Package_{request.PackageId}",
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                termId = term.Id,
                packageId = request.PackageId,
                title = term.Title
            })
        };
        _db.OutboxEvents.Add(outboxEvent);

        var termPublishedEvent = new OutboxEvent
        {
            Type = "TermPublished",
            TargetGroup = $"Package_{request.PackageId}",
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                termId = term.Id,
                packageId = request.PackageId,
                title = term.Title
            })
        };
        _db.OutboxEvents.Add(termPublishedEvent);

        await _db.SaveChangesAsync(ct);
        await ContentAcademicScopeValidation.SyncExplicitScopesAsync(
            _db,
            StudentFacingScopeOwnerType.Term,
            term.Id,
            request.AcademicScopes,
            request.CurrentUserId,
            ct);

        return ApiResponse<Guid>.Ok(term.Id);
    }
}

public record UpdateTermCommand(Guid Id, string Title, int Order, decimal Price, Guid? CurrentUserId = null, IReadOnlyList<AcademicScopeDto>? AcademicScopes = null) : IRequest<ApiResponse>;

public class UpdateTermCommandHandler : IRequestHandler<UpdateTermCommand, ApiResponse>
{
    private readonly IAppDbContext _db;
    private readonly TeacherAuthorizationService _auth;

    public UpdateTermCommandHandler(IAppDbContext db, TeacherAuthorizationService auth)
    {
        _db = db;
        _auth = auth;
    }

    public async Task<ApiResponse> Handle(UpdateTermCommand request, CancellationToken ct)
    {
        var scopeValidation = await ContentAcademicScopeValidation.ValidateExplicitScopesAsync(_db, request.AcademicScopes, ct);
        if (!scopeValidation.IsEligible)
            return ApiResponse.Fail(scopeValidation.Message ?? "نطاق الترم الأكاديمي غير صالح.", new List<string> { scopeValidation.ErrorCode ?? "ACADEMIC_SCOPE_INVALID" });

        if (request.CurrentUserId.HasValue)
        {
            var canAccess = await _auth.CanAccessTermAsync(request.CurrentUserId.Value, request.Id, ct);
            if (!canAccess) return ApiResponse.Fail("Unauthorized access to this term.");
        }

        var term = await _db.Terms.FindAsync(new object[] { request.Id }, ct);
        if (term == null) return ApiResponse.Fail("Term not found");

        term.Title = request.Title;
        term.Order = request.Order;
        term.Price = request.Price;

        var outboxEvent = new OutboxEvent
        {
            Type = "TermUpdated",
            TargetGroup = $"Package_{term.PackageId}",
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                termId = term.Id,
                packageId = term.PackageId,
                title = term.Title
            })
        };
        _db.OutboxEvents.Add(outboxEvent);

        await _db.SaveChangesAsync(ct);
        await ContentAcademicScopeValidation.SyncExplicitScopesAsync(
            _db,
            StudentFacingScopeOwnerType.Term,
            term.Id,
            request.AcademicScopes,
            request.CurrentUserId,
            ct);

        return ApiResponse.Ok();
    }
}

public record DeleteTermCommand(Guid Id, Guid? CurrentUserId = null) : IRequest<ApiResponse>;

public class DeleteTermCommandHandler : IRequestHandler<DeleteTermCommand, ApiResponse>
{
    private readonly IAppDbContext _db;
    private readonly TeacherAuthorizationService _auth;

    public DeleteTermCommandHandler(IAppDbContext db, TeacherAuthorizationService auth)
    {
        _db = db;
        _auth = auth;
    }

    public async Task<ApiResponse> Handle(DeleteTermCommand request, CancellationToken ct)
    {
        if (request.CurrentUserId.HasValue)
        {
            var canAccess = await _auth.CanAccessTermAsync(request.CurrentUserId.Value, request.Id, ct);
            if (!canAccess) return ApiResponse.Fail("Unauthorized access to this term.");
        }

        var term = await _db.Terms.Include(t => t.Sections).FirstOrDefaultAsync(t => t.Id == request.Id, ct);
        if (term == null) return ApiResponse.Fail("Term not found");

        if (term.Sections.Any())
            return ApiResponse.Fail("Cannot delete term because it has sections. Remove sections first.");

        _db.Terms.Remove(term);

        var outboxEvent = new OutboxEvent
        {
            Type = "TermDeleted",
            TargetGroup = $"Package_{term.PackageId}",
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                termId = term.Id,
                packageId = term.PackageId,
                title = term.Title
            })
        };
        _db.OutboxEvents.Add(outboxEvent);

        await _db.SaveChangesAsync(ct);
        return ApiResponse.Ok();
    }
}

// --- Sections ---
public record CreateSectionCommand(string Title, int Order, Guid TermId, decimal Price, Guid? CurrentUserId = null, IReadOnlyList<AcademicScopeDto>? AcademicScopes = null) : IRequest<ApiResponse<Guid>>;

public class CreateSectionCommandHandler : IRequestHandler<CreateSectionCommand, ApiResponse<Guid>>
{
    private readonly IAppDbContext _db;
    private readonly TeacherAuthorizationService _auth;

    public CreateSectionCommandHandler(IAppDbContext db, TeacherAuthorizationService auth)
    {
        _db = db;
        _auth = auth;
    }

    public async Task<ApiResponse<Guid>> Handle(CreateSectionCommand request, CancellationToken ct)
    {
        var scopeValidation = await ContentAcademicScopeValidation.ValidateExplicitScopesAsync(_db, request.AcademicScopes, ct);
        if (!scopeValidation.IsEligible)
            return ApiResponse<Guid>.Fail(scopeValidation.Message ?? "نطاق القسم الأكاديمي غير صالح.", new List<string> { scopeValidation.ErrorCode ?? "ACADEMIC_SCOPE_INVALID" });

        if (request.CurrentUserId.HasValue)
        {
            var canAccess = await _auth.CanAccessTermAsync(request.CurrentUserId.Value, request.TermId, ct);
            if (!canAccess) return ApiResponse<Guid>.Fail("Unauthorized access to this term.");
        }

        var term = await _db.Terms
            .Include(item => item.Package)
            .FirstOrDefaultAsync(item => item.Id == request.TermId, ct);
        if (term == null)
            return ApiResponse<Guid>.Fail("Term not found");

        if (term.IsSystemContainer && term.Package.ContentMode != PackageContentMode.SectionWithLessons)
            return ApiResponse<Guid>.Fail("هذا الكورس لا يسمح بإضافة أقسام في هذا المسار.");

        if (!term.IsSystemContainer && term.Package.ContentMode != PackageContentMode.TermWithSections)
            return ApiResponse<Guid>.Fail("هذا الكورس لا يسمح بإضافة أقسام خارج الترم.");

        var sec = new ContentSection
        {
            Title = request.Title,
            Order = request.Order,
            TermId = request.TermId,
            Price = request.Price
        };
        _db.ContentSections.Add(sec);

        var sectionOutbox = new OutboxEvent
        {
            Type = "SectionCreated",
            TargetGroup = $"Package_{term.PackageId}",
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                sectionId = sec.Id,
                termId = request.TermId,
                packageId = term.PackageId,
                title = sec.Title
            })
        };
        _db.OutboxEvents.Add(sectionOutbox);

        var sectionPublishedOutbox = new OutboxEvent
        {
            Type = "SectionPublished",
            TargetGroup = $"Package_{term.PackageId}",
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                sectionId = sec.Id,
                termId = request.TermId,
                packageId = term.PackageId,
                title = sec.Title
            })
        };
        _db.OutboxEvents.Add(sectionPublishedOutbox);

        await _db.SaveChangesAsync(ct);
        await ContentAcademicScopeValidation.SyncExplicitScopesAsync(
            _db,
            StudentFacingScopeOwnerType.ContentSection,
            sec.Id,
            request.AcademicScopes,
            request.CurrentUserId,
            ct);

        return ApiResponse<Guid>.Ok(sec.Id);
    }
}

// --- Update Section ---
public record UpdateSectionCommand(Guid Id, string Title, int Order, decimal Price, Guid? CurrentUserId = null, IReadOnlyList<AcademicScopeDto>? AcademicScopes = null) : IRequest<ApiResponse>;

public class UpdateSectionCommandHandler : IRequestHandler<UpdateSectionCommand, ApiResponse>
{
    private readonly IAppDbContext _db;
    private readonly TeacherAuthorizationService _auth;

    public UpdateSectionCommandHandler(IAppDbContext db, TeacherAuthorizationService auth)
    {
        _db = db;
        _auth = auth;
    }

    public async Task<ApiResponse> Handle(UpdateSectionCommand request, CancellationToken ct)
    {
        var scopeValidation = await ContentAcademicScopeValidation.ValidateExplicitScopesAsync(_db, request.AcademicScopes, ct);
        if (!scopeValidation.IsEligible)
            return ApiResponse.Fail(scopeValidation.Message ?? "نطاق القسم الأكاديمي غير صالح.", new List<string> { scopeValidation.ErrorCode ?? "ACADEMIC_SCOPE_INVALID" });

        if (request.CurrentUserId.HasValue)
        {
            var canAccess = await _auth.CanAccessSectionAsync(request.CurrentUserId.Value, request.Id, ct);
            if (!canAccess) return ApiResponse.Fail("Unauthorized access to this section.");
        }

        var section = await _db.ContentSections.FindAsync(new object[] { request.Id }, ct);
        if (section == null) return ApiResponse.Fail("Section not found");

        section.Title = request.Title;
        section.Order = request.Order;
        section.Price = request.Price;

        await _db.SaveChangesAsync(ct);
        await ContentAcademicScopeValidation.SyncExplicitScopesAsync(
            _db,
            StudentFacingScopeOwnerType.ContentSection,
            section.Id,
            request.AcademicScopes,
            request.CurrentUserId,
            ct);

        return ApiResponse.Ok();
    }
}

// --- Update Lesson ---
public record UpdateLessonCommand(Guid Id, string Title, string Summary, int Order, decimal Price, Guid? CurrentUserId = null, IReadOnlyList<AcademicScopeDto>? AcademicScopes = null) : IRequest<ApiResponse>;

public class UpdateLessonCommandHandler : IRequestHandler<UpdateLessonCommand, ApiResponse>
{
    private readonly IAppDbContext _db;
    private readonly TeacherAuthorizationService _auth;

    public UpdateLessonCommandHandler(IAppDbContext db, TeacherAuthorizationService auth)
    {
        _db = db;
        _auth = auth;
    }

    public async Task<ApiResponse> Handle(UpdateLessonCommand request, CancellationToken ct)
    {
        var scopeValidation = await ContentAcademicScopeValidation.ValidateExplicitScopesAsync(_db, request.AcademicScopes, ct);
        if (!scopeValidation.IsEligible)
            return ApiResponse.Fail(scopeValidation.Message ?? "نطاق الدرس الأكاديمي غير صالح.", new List<string> { scopeValidation.ErrorCode ?? "ACADEMIC_SCOPE_INVALID" });

        if (request.CurrentUserId.HasValue)
        {
            var canAccess = await _auth.CanAccessLessonAsync(request.CurrentUserId.Value, request.Id, ct);
            if (!canAccess) return ApiResponse.Fail("Unauthorized access to this lesson.");
        }

        var lesson = await _db.Lessons.FindAsync(new object[] { request.Id }, ct);
        if (lesson == null) return ApiResponse.Fail("Lesson not found");

        lesson.Title = request.Title;
        lesson.Summary = request.Summary;
        lesson.Order = request.Order;
        lesson.Price = request.Price;

        await _db.SaveChangesAsync(ct);
        await ContentAcademicScopeValidation.SyncExplicitScopesAsync(
            _db,
            StudentFacingScopeOwnerType.Lesson,
            lesson.Id,
            request.AcademicScopes,
            request.CurrentUserId,
            ct);

        return ApiResponse.Ok();
    }
}

public record CreateLessonCommand(string Title, string Summary, int Order, Guid SectionId, Guid? ExamId, decimal Price, Guid? CurrentUserId = null, IReadOnlyList<AcademicScopeDto>? AcademicScopes = null) : IRequest<ApiResponse<Guid>>;

public class CreateLessonCommandHandler : IRequestHandler<CreateLessonCommand, ApiResponse<Guid>>
{
    private readonly IAppDbContext _db;
    private readonly TeacherAuthorizationService _auth;

    public CreateLessonCommandHandler(IAppDbContext db, TeacherAuthorizationService auth)
    {
        _db = db;
        _auth = auth;
    }

    public async Task<ApiResponse<Guid>> Handle(CreateLessonCommand request, CancellationToken ct)
    {
        var scopeValidation = await ContentAcademicScopeValidation.ValidateExplicitScopesAsync(_db, request.AcademicScopes, ct);
        if (!scopeValidation.IsEligible)
            return ApiResponse<Guid>.Fail(scopeValidation.Message ?? "نطاق الدرس الأكاديمي غير صالح.", new List<string> { scopeValidation.ErrorCode ?? "ACADEMIC_SCOPE_INVALID" });

        if (request.CurrentUserId.HasValue)
        {
            var canAccess = await _auth.CanAccessSectionAsync(request.CurrentUserId.Value, request.SectionId, ct);
            if (!canAccess) return ApiResponse<Guid>.Fail("Unauthorized access to this section.");
        }

        var section = await _db.ContentSections
            .Include(s => s.Term)
                .ThenInclude(t => t.Package)
            .FirstOrDefaultAsync(s => s.Id == request.SectionId, ct);
        if (section == null)
            return ApiResponse<Guid>.Fail("Section not found");

        if (section.IsSystemContainer && section.Term.Package.ContentMode != PackageContentMode.LessonsOnly)
            return ApiResponse<Guid>.Fail("هذا القسم مخصص للحصص المباشرة فقط.");

        if (!section.IsSystemContainer
            && section.Term.IsSystemContainer
            && section.Term.Package.ContentMode != PackageContentMode.SectionWithLessons)
            return ApiResponse<Guid>.Fail("هذا الكورس لا يسمح بإضافة حصص داخل هذا المسار.");

        if (!section.IsSystemContainer
            && !section.Term.IsSystemContainer
            && section.Term.Package.ContentMode != PackageContentMode.TermWithSections)
            return ApiResponse<Guid>.Fail("هذا الكورس لا يسمح بإضافة حصص خارج الترم والأقسام.");

        var lesson = new Lesson
        {
            Title = request.Title,
            Summary = request.Summary,
            Order = request.Order,
            ContentSectionId = request.SectionId,
            ExamId = request.ExamId,
            Price = request.Price
        };
        _db.Lessons.Add(lesson);

        var outboxEvent = new OutboxEvent
        {
            Type = "LessonPublished",
            TargetGroup = $"Package_{section.Term.PackageId}",
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                lessonId = lesson.Id,
                sectionId = lesson.ContentSectionId,
                title = lesson.Title,
                packageId = section.Term.PackageId,
                order = lesson.Order
            })
        };
        _db.OutboxEvents.Add(outboxEvent);

        await _db.SaveChangesAsync(ct);
        await ContentAcademicScopeValidation.SyncExplicitScopesAsync(
            _db,
            StudentFacingScopeOwnerType.Lesson,
            lesson.Id,
            request.AcademicScopes,
            request.CurrentUserId,
            ct);

        return ApiResponse<Guid>.Ok(lesson.Id);
    }
}

public record CreateVideoCommand(string Title, string Provider, string UrlOrEmbedCode, int Order, int Limit, Guid LessonId, Guid VideoTypeId, bool IsActive = true, Guid? CurrentUserId = null) : IRequest<ApiResponse<Guid>>;

public class CreateVideoCommandHandler : IRequestHandler<CreateVideoCommand, ApiResponse<Guid>>
{
    private readonly IAppDbContext _db;
    private readonly IEnumerable<IVideoProvider> _providers;
    private readonly TeacherAuthorizationService _auth;

    public CreateVideoCommandHandler(IAppDbContext db, IEnumerable<IVideoProvider> providers, TeacherAuthorizationService auth)
    {
        _db = db;
        _providers = providers;
        _auth = auth;
    }

    public async Task<ApiResponse<Guid>> Handle(CreateVideoCommand request, CancellationToken ct)
    {
        if (request.CurrentUserId.HasValue)
        {
            var canAccess = await _auth.CanAccessLessonAsync(request.CurrentUserId.Value, request.LessonId, ct);
            if (!canAccess) return ApiResponse<Guid>.Fail("Unauthorized access to this lesson.");
        }

        if (!VideoProviders.IsSupported(request.Provider))
        {
            return ApiResponse<Guid>.Fail("Invalid provider. Supported: youtube, vk, bunny");
        }

        if (!await VideoTypeRules.IsActiveAsync(_db, request.VideoTypeId, ct))
        {
            return ApiResponse<Guid>.Fail("اختر نوع فيديو نشطاً.", ["VIDEO_TYPE_INVALID"]);
        }

        var normalizedProvider = VideoProviders.Normalize(request.Provider);
        var providerImpl = _providers.FirstOrDefault(p => p.Name.Equals(normalizedProvider, StringComparison.OrdinalIgnoreCase));
        string extractedId = request.UrlOrEmbedCode;
        if (providerImpl != null)
        {
            extractedId = providerImpl.ExtractVideoId(request.UrlOrEmbedCode);
        }

        var video = new LessonVideo
        {
            Title = request.Title,
            Provider = normalizedProvider,
            ProviderVideoId = extractedId,
            Order = request.Order,
            MaxWatchCount = request.Limit,
            LessonId = request.LessonId,
            VideoTypeId = request.VideoTypeId,
            IsActive = request.IsActive
        };
        _db.LessonVideos.Add(video);

        var outboxEvent = new OutboxEvent
        {
            Type = "VideoProcessingStarted",
            TargetGroup = $"Lesson_{request.LessonId}",
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                lessonId = request.LessonId,
                videoId = video.Id,
                status = "Started"
            })
        };
        _db.OutboxEvents.Add(outboxEvent);

        await _db.SaveChangesAsync(ct);
        return ApiResponse<Guid>.Ok(video.Id);
    }
}

public record UpdateVideoCommand(Guid Id, string Title, string Provider, string UrlOrEmbedCode, int Order, int Limit, Guid VideoTypeId, Guid? CurrentUserId = null) : IRequest<ApiResponse>;

public class UpdateVideoCommandHandler : IRequestHandler<UpdateVideoCommand, ApiResponse>
{
    private readonly IAppDbContext _db;
    private readonly IEnumerable<IVideoProvider> _providers;
    private readonly TeacherAuthorizationService _auth;

    public UpdateVideoCommandHandler(IAppDbContext db, IEnumerable<IVideoProvider> providers, TeacherAuthorizationService auth)
    {
        _db = db;
        _providers = providers;
        _auth = auth;
    }

    public async Task<ApiResponse> Handle(UpdateVideoCommand request, CancellationToken ct)
    {
        var video = await _db.LessonVideos.FirstOrDefaultAsync(v => v.Id == request.Id, ct);
        if (video == null) return ApiResponse.Fail("Video not found");

        if (request.CurrentUserId.HasValue)
        {
            var canAccess = await _auth.CanAccessLessonAsync(request.CurrentUserId.Value, video.LessonId, ct);
            if (!canAccess) return ApiResponse.Fail("Unauthorized access to this video.");
        }

        if (!VideoProviders.IsSupported(request.Provider))
        {
            return ApiResponse.Fail("Invalid provider. Supported: youtube, vk, bunny");
        }

        if (video.VideoTypeId != request.VideoTypeId && !await VideoTypeRules.IsActiveAsync(_db, request.VideoTypeId, ct))
        {
            return ApiResponse.Fail("اختر نوع فيديو نشطاً.", ["VIDEO_TYPE_INVALID"]);
        }

        var normalizedProvider = VideoProviders.Normalize(request.Provider);
        var providerImpl = _providers.FirstOrDefault(p => p.Name.Equals(normalizedProvider, StringComparison.OrdinalIgnoreCase));
        var extractedId = request.UrlOrEmbedCode;
        if (providerImpl != null)
        {
            extractedId = providerImpl.ExtractVideoId(request.UrlOrEmbedCode);
        }

        video.Title = request.Title;
        video.Provider = normalizedProvider;
        video.ProviderVideoId = extractedId;
        video.Order = request.Order;
        video.MaxWatchCount = request.Limit;
        video.VideoTypeId = request.VideoTypeId;

        var outboxEvent = new OutboxEvent
        {
            Type = "VideoUpdated",
            TargetGroup = $"Lesson_{video.LessonId}",
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                lessonId = video.LessonId,
                videoId = video.Id,
                title = video.Title
            })
        };
        _db.OutboxEvents.Add(outboxEvent);

        await _db.SaveChangesAsync(ct);
        return ApiResponse.Ok();
    }
}

public record DeleteVideoCommand(Guid Id, Guid? CurrentUserId = null) : IRequest<ApiResponse>;

public class DeleteVideoCommandHandler : IRequestHandler<DeleteVideoCommand, ApiResponse>
{
    private readonly IAppDbContext _db;
    private readonly TeacherAuthorizationService _auth;

    public DeleteVideoCommandHandler(IAppDbContext db, TeacherAuthorizationService auth)
    {
        _db = db;
        _auth = auth;
    }

    public async Task<ApiResponse> Handle(DeleteVideoCommand request, CancellationToken ct)
    {
        var video = await _db.LessonVideos.FirstOrDefaultAsync(v => v.Id == request.Id, ct);
        if (video == null) return ApiResponse.Fail("Video not found");

        if (request.CurrentUserId.HasValue)
        {
            var canAccess = await _auth.CanAccessLessonAsync(request.CurrentUserId.Value, video.LessonId, ct);
            if (!canAccess) return ApiResponse.Fail("Unauthorized access to this video.");
        }

        // Cascade-delete all dependent records to avoid FK constraint violations
        var playbackSessions = await _db.VideoPlaybackSessions
            .Where(s => s.LessonVideoId == video.Id).ToListAsync(ct);
        if (playbackSessions.Count > 0) _db.VideoPlaybackSessions.RemoveRange(playbackSessions);

        var chapters = await _db.VideoChapters
            .Where(c => c.LessonVideoId == video.Id).ToListAsync(ct);
        if (chapters.Count > 0) _db.VideoChapters.RemoveRange(chapters);

        var bunnyAssets = await _db.BunnyVideoAssets
            .Where(a => a.LessonVideoId == video.Id).ToListAsync(ct);
        if (bunnyAssets.Count > 0)
        {
            var assetIds = bunnyAssets.Select(a => a.Id).ToList();
            var snapshots = await _db.BunnyUsageSnapshots
                .Where(s => assetIds.Contains(s.BunnyVideoAssetId)).ToListAsync(ct);
            if (snapshots.Count > 0) _db.BunnyUsageSnapshots.RemoveRange(snapshots);
            _db.BunnyVideoAssets.RemoveRange(bunnyAssets);
        }

        var codeTargets = await _db.CodeVideoTargets
            .Where(t => t.LessonVideoId == video.Id).ToListAsync(ct);
        if (codeTargets.Count > 0) _db.CodeVideoTargets.RemoveRange(codeTargets);

        var overrides = await _db.VideoOverrides
            .Where(o => o.LessonVideoId == video.Id).ToListAsync(ct);
        if (overrides.Count > 0) _db.VideoOverrides.RemoveRange(overrides);

        var extraWatchRequests = await _db.ExtraWatchRequests
            .Where(r => r.LessonVideoId == video.Id).ToListAsync(ct);
        if (extraWatchRequests.Count > 0) _db.ExtraWatchRequests.RemoveRange(extraWatchRequests);

        _db.LessonVideos.Remove(video);

        var outboxEvent = new OutboxEvent
        {
            Type = "VideoDeleted",
            TargetGroup = $"Lesson_{video.LessonId}",
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                lessonId = video.LessonId,
                videoId = video.Id
            })
        };
        _db.OutboxEvents.Add(outboxEvent);

        await _db.SaveChangesAsync(ct);
        return ApiResponse.Ok();
    }
}

public record AttachHomeworkCommand(
    Guid LessonId,
    string Title,
    string Instructions,
    bool IsMandatory,
    bool IsRandomized,
    int RequiredPointsToPass,
    decimal TotalScore,
    List<AttachHomeworkQuestionDto> Questions,
    Guid? CurrentUserId = null) : IRequest<ApiResponse<Guid>>;

public record AttachHomeworkOptionDto(string Text, bool IsCorrect);

public record AttachHomeworkQuestionDto(
    string Text,
    int Order,
    decimal Points,
    string Type,
    List<AttachHomeworkOptionDto>? Options = null,
    string? AudioUrl = null,
    string? ImageUrl = null,
    string? WrittenCorrection = null,
    string? HintText = null,
    string? BaseText = null,
    int? MistakeStartIndex = null,
    int? MistakeEndIndex = null
);

public class AttachHomeworkCommandHandler : IRequestHandler<AttachHomeworkCommand, ApiResponse<Guid>>
{
    private readonly IAppDbContext _db;
    private readonly TeacherAuthorizationService _auth;

    public AttachHomeworkCommandHandler(IAppDbContext db, TeacherAuthorizationService auth)
    {
        _db = db;
        _auth = auth;
    }

    public async Task<ApiResponse<Guid>> Handle(AttachHomeworkCommand request, CancellationToken ct)
    {
        if (request.CurrentUserId.HasValue)
        {
            var canAccess = await _auth.CanAccessLessonAsync(request.CurrentUserId.Value, request.LessonId, ct);
            if (!canAccess) return ApiResponse<Guid>.Fail("Unauthorized access to this lesson.");
        }

        var lesson = await _db.Lessons
            .Include(l => l.ContentSection).ThenInclude(s => s.Term)
            .FirstOrDefaultAsync(l => l.Id == request.LessonId, ct);

        if (lesson == null) return ApiResponse<Guid>.Fail("Lesson not found");

        // Load homework WITHOUT including questions to avoid EF tracking issues
        var hw = await _db.Homeworks
            .FirstOrDefaultAsync(h => h.LessonId == request.LessonId, ct);

        if (hw == null)
        {
            hw = new NaderGorge.Domain.Entities.Homework.Homework
            {
                LessonId = lesson.Id,
                Title = request.Title,
                Description = request.Instructions,
                IsMandatory = request.IsMandatory,
                IsRandomized = request.IsRandomized,
                PassingScoreThreshold = request.RequiredPointsToPass,
                TotalScore = request.TotalScore
            };
            _db.Homeworks.Add(hw);
            await _db.SaveChangesAsync(ct);
        }
        else
        {
            hw.Title = request.Title;
            hw.Description = request.Instructions;
            hw.IsMandatory = request.IsMandatory;
            hw.IsRandomized = request.IsRandomized;
            hw.PassingScoreThreshold = request.RequiredPointsToPass;
            hw.TotalScore = request.TotalScore;
            hw.UpdatedAt = DateTime.UtcNow;

            var existingQuestions = await _db.HomeworkQuestions
                .Where(q => q.HomeworkId == hw.Id)
                .ToListAsync(ct);
            _db.HomeworkQuestions.RemoveRange(existingQuestions);
        }

        // Build new questions as standalone entities (not via navigation property)
        var newQuestions = new List<NaderGorge.Domain.Entities.Homework.HomeworkQuestion>();
        foreach (var q in request.Questions)
        {
            var qType = q.Type switch
            {
                "Essay" => NaderGorge.Domain.Entities.Homework.QuestionType.Essay,
                "FindTheMistake" => NaderGorge.Domain.Entities.Homework.QuestionType.FindTheMistake,
                _ => NaderGorge.Domain.Entities.Homework.QuestionType.MCQ
            };

            string[]? possibleAnswers = null;
            string? correctAnswerKey = null;

            if ((qType == NaderGorge.Domain.Entities.Homework.QuestionType.MCQ || qType == NaderGorge.Domain.Entities.Homework.QuestionType.FindTheMistake) && q.Options != null)
            {
                possibleAnswers = q.Options.Select(o => o.Text).ToArray();
                correctAnswerKey = q.Options.FirstOrDefault(o => o.IsCorrect)?.Text;
            }

            newQuestions.Add(new NaderGorge.Domain.Entities.Homework.HomeworkQuestion
            {
                HomeworkId = hw.Id,
                BodyText = q.Text,
                Order = q.Order,
                PointsActive = (int)q.Points,
                QuestionType = qType,
                PossibleAnswers = possibleAnswers,
                CorrectAnswerKey = correctAnswerKey,
                AudioUrl = q.AudioUrl,
                ImageUrl = q.ImageUrl,
                WrittenCorrection = q.WrittenCorrection,
                HintText = q.HintText,
                BaseText = q.BaseText,
                MistakeStartIndex = q.MistakeStartIndex,
                MistakeEndIndex = q.MistakeEndIndex
            });
        }

        // Add all new questions directly via DbSet — clean INSERT, no tracking conflicts
        _db.HomeworkQuestions.AddRange(newQuestions);

        if (lesson.ContentSection?.Term != null)
        {
            var outboxEvent = new OutboxEvent
            {
                Type = "HomeworkPublished",
                TargetGroup = $"Package_{lesson.ContentSection.Term.PackageId}",
                PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    lessonId = lesson.Id,
                    homeworkId = hw.Id,
                    title = hw.Title,
                    packageId = lesson.ContentSection.Term.PackageId
                })
            };
            _db.OutboxEvents.Add(outboxEvent);
        }

        await _db.SaveChangesAsync(ct);
        return ApiResponse<Guid>.Ok(hw.Id);
    }
}

public record CreateLessonResourceCommand(Guid LessonId, string Title, string FileUrl, string ResourceType, Guid? CurrentUserId = null) : IRequest<ApiResponse<Guid>>;

public class CreateLessonResourceCommandHandler : IRequestHandler<CreateLessonResourceCommand, ApiResponse<Guid>>
{
    private readonly IAppDbContext _db;
    private readonly TeacherAuthorizationService _auth;

    public CreateLessonResourceCommandHandler(IAppDbContext db, TeacherAuthorizationService auth)
    {
        _db = db;
        _auth = auth;
    }

    public async Task<ApiResponse<Guid>> Handle(CreateLessonResourceCommand request, CancellationToken ct)
    {
        if (request.CurrentUserId.HasValue)
        {
            var canAccess = await _auth.CanAccessLessonAsync(request.CurrentUserId.Value, request.LessonId, ct);
            if (!canAccess) return ApiResponse<Guid>.Fail("Unauthorized access to this lesson.");
        }

        var resource = new LessonResource
        {
            LessonId = request.LessonId,
            Title = request.Title,
            FileUrl = request.FileUrl,
            ResourceType = request.ResourceType
        };

        var resourceProcessingStartedEvent = new OutboxEvent
        {
            Type = "ResourceProcessingStarted",
            TargetGroup = $"Lesson_{request.LessonId}",
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                lessonId = request.LessonId,
                title = request.Title
            })
        };
        _db.OutboxEvents.Add(resourceProcessingStartedEvent);

        _db.LessonResources.Add(resource);

        var outboxEvent = new OutboxEvent
        {
            Type = "ResourceReady",
            TargetGroup = $"Lesson_{request.LessonId}",
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                lessonId = request.LessonId,
                resourceId = resource.Id,
                title = resource.Title,
                fileUrl = resource.FileUrl
            })
        };
        _db.OutboxEvents.Add(outboxEvent);

        await _db.SaveChangesAsync(ct);

        return ApiResponse<Guid>.Ok(resource.Id);
    }
}

public record LinkLessonExamCommand(Guid LessonId, Guid? ExamId, Guid? CurrentUserId = null) : IRequest<ApiResponse>;

public class LinkLessonExamCommandHandler : IRequestHandler<LinkLessonExamCommand, ApiResponse>
{
    private readonly IAppDbContext _db;
    private readonly TeacherAuthorizationService _auth;

    public LinkLessonExamCommandHandler(IAppDbContext db, TeacherAuthorizationService auth)
    {
        _db = db;
        _auth = auth;
    }

    public async Task<ApiResponse> Handle(LinkLessonExamCommand request, CancellationToken ct)
    {
        if (request.CurrentUserId.HasValue)
        {
            var canAccessLesson = await _auth.CanAccessLessonAsync(request.CurrentUserId.Value, request.LessonId, ct);
            if (!canAccessLesson) return ApiResponse.Fail("Unauthorized access to this lesson.");

            if (request.ExamId.HasValue)
            {
                var canAccessExam = await _auth.CanAccessExamAsync(request.CurrentUserId.Value, request.ExamId.Value, ct);
                if (!canAccessExam) return ApiResponse.Fail("Unauthorized access to this exam.");
            }
        }

        var lesson = await _db.Lessons
            .Include(l => l.ContentSection).ThenInclude(s => s.Term)
            .FirstOrDefaultAsync(l => l.Id == request.LessonId, ct);
        if (lesson == null) return ApiResponse.Fail("Lesson not found");

        lesson.ExamId = request.ExamId;

        if (request.ExamId.HasValue && lesson.ContentSection?.Term != null)
        {
            var outboxEvent = new OutboxEvent
            {
                Type = "ExamPublished",
                TargetGroup = $"Package_{lesson.ContentSection.Term.PackageId}",
                PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    lessonId = lesson.Id,
                    examId = request.ExamId.Value,
                    packageId = lesson.ContentSection.Term.PackageId
                })
            };
            _db.OutboxEvents.Add(outboxEvent);
        }

        await _db.SaveChangesAsync(ct);
        return ApiResponse.Ok();
    }
}

public record LinkVideoExamCommand(Guid VideoId, Guid? ExamId, Guid? CurrentUserId = null) : IRequest<ApiResponse>;

public class LinkVideoExamCommandHandler : IRequestHandler<LinkVideoExamCommand, ApiResponse>
{
    private readonly IAppDbContext _db;
    private readonly TeacherAuthorizationService _auth;

    public LinkVideoExamCommandHandler(IAppDbContext db, TeacherAuthorizationService auth)
    {
        _db = db;
        _auth = auth;
    }

    public async Task<ApiResponse> Handle(LinkVideoExamCommand request, CancellationToken ct)
    {
        if (request.CurrentUserId.HasValue)
        {
            var video = await _db.LessonVideos.FirstOrDefaultAsync(v => v.Id == request.VideoId, ct);
            if (video == null) return ApiResponse.Fail("Video not found");

            var canAccessLesson = await _auth.CanAccessLessonAsync(request.CurrentUserId.Value, video.LessonId, ct);
            if (!canAccessLesson) return ApiResponse.Fail("Unauthorized access to this video.");

            if (request.ExamId.HasValue)
            {
                var canAccessExam = await _auth.CanAccessExamAsync(request.CurrentUserId.Value, request.ExamId.Value, ct);
                if (!canAccessExam) return ApiResponse.Fail("Unauthorized access to this exam.");
            }
        }

        var videoEntity = await _db.LessonVideos.FirstOrDefaultAsync(v => v.Id == request.VideoId, ct);
        if (videoEntity == null) return ApiResponse.Fail("Video not found");

        videoEntity.ExamId = request.ExamId;

        await _db.SaveChangesAsync(ct);
        return ApiResponse.Ok();
    }
}

public record UnlinkVideoExamCommand(Guid VideoId, Guid ExamId, Guid? CurrentUserId = null) : IRequest<ApiResponse>;

public record SetExamActiveStatusCommand(Guid ExamId, bool IsActive, Guid? CurrentUserId = null) : IRequest<ApiResponse>;

public class SetExamActiveStatusCommandHandler : IRequestHandler<SetExamActiveStatusCommand, ApiResponse>
{
    private readonly IAppDbContext _db;
    private readonly TeacherAuthorizationService _auth;

    public SetExamActiveStatusCommandHandler(IAppDbContext db, TeacherAuthorizationService auth) { _db = db; _auth = auth; }

    public async Task<ApiResponse> Handle(SetExamActiveStatusCommand request, CancellationToken ct)
    {
        var exam = await _db.Exams.FirstOrDefaultAsync(x => x.Id == request.ExamId, ct);
        if (exam == null) return ApiResponse.Fail("Exam not found");
        if (request.CurrentUserId.HasValue && !await _auth.CanAccessExamAsync(request.CurrentUserId.Value, request.ExamId, ct))
            return ApiResponse.Fail("Unauthorized access to this exam.");
        exam.IsActive = request.IsActive;
        await _db.SaveChangesAsync(ct);
        return ApiResponse.Ok();
    }
}

public record SetHomeworkActiveStatusCommand(Guid HomeworkId, bool IsActive, Guid? CurrentUserId = null) : IRequest<ApiResponse>;

public class SetHomeworkActiveStatusCommandHandler : IRequestHandler<SetHomeworkActiveStatusCommand, ApiResponse>
{
    private readonly IAppDbContext _db;
    private readonly TeacherAuthorizationService _auth;

    public SetHomeworkActiveStatusCommandHandler(IAppDbContext db, TeacherAuthorizationService auth) { _db = db; _auth = auth; }

    public async Task<ApiResponse> Handle(SetHomeworkActiveStatusCommand request, CancellationToken ct)
    {
        var homework = await _db.Homeworks.FirstOrDefaultAsync(x => x.Id == request.HomeworkId, ct);
        if (homework == null) return ApiResponse.Fail("Homework not found");
        if (request.CurrentUserId.HasValue && !await _auth.CanAccessLessonAsync(request.CurrentUserId.Value, homework.LessonId, ct))
            return ApiResponse.Fail("Unauthorized access to this homework.");
        homework.IsActive = request.IsActive;
        homework.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ApiResponse.Ok();
    }
}

public class UnlinkVideoExamCommandHandler : IRequestHandler<UnlinkVideoExamCommand, ApiResponse>
{
    private readonly IAppDbContext _db;
    private readonly TeacherAuthorizationService _auth;

    public UnlinkVideoExamCommandHandler(IAppDbContext db, TeacherAuthorizationService auth)
    {
        _db = db;
        _auth = auth;
    }

    public async Task<ApiResponse> Handle(UnlinkVideoExamCommand request, CancellationToken ct)
    {
        if (request.CurrentUserId.HasValue)
        {
            var video = await _db.LessonVideos.FirstOrDefaultAsync(v => v.Id == request.VideoId, ct);
            if (video == null) return ApiResponse.Fail("Video not found");

            var canAccess = await _auth.CanAccessLessonAsync(request.CurrentUserId.Value, video.LessonId, ct);
            if (!canAccess) return ApiResponse.Fail("Unauthorized access to this video.");
        }

        var exam = await _db.Exams.FirstOrDefaultAsync(e => e.Id == request.ExamId, ct);
        if (exam != null)
        {
            exam.LessonVideoId = null;
        }

        var videoEntity = await _db.LessonVideos.FirstOrDefaultAsync(v => v.Id == request.VideoId, ct);
        if (videoEntity != null && videoEntity.ExamId == request.ExamId)
        {
            videoEntity.ExamId = null;
        }

        await _db.SaveChangesAsync(ct);
        return ApiResponse.Ok();
    }
}
