using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Interfaces;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.SharedPackages;

public enum SharedPackageCommandStatus { Success, Invalid, NotFound }

public sealed record SharedPackageCommandResult(
    SharedPackageCommandStatus Status,
    Guid? Id = null,
    string? Value = null,
    string? Message = null,
    string? ErrorCode = null)
{
    public static SharedPackageCommandResult Invalid(string message, string? code = null) =>
        new(SharedPackageCommandStatus.Invalid, Message: message, ErrorCode: code);
}

public sealed record SharedPackageTeacherInput(Guid TeacherId, Guid? SubjectId,
    TeacherAllocationMode AllocationMode, decimal AllocationValue, int DisplayOrder);
public sealed record SharedPackageItemInput(Guid TeacherId, Guid? SubjectId,
    SalesTargetType ContentType, Guid ContentId, decimal Price);

public sealed record CreateSharedPackageCommand(
    Guid ActorUserId, string Name, string? Slug, string? Description, string? ImageUrl,
    decimal Price, SharedPackageDistributionMode DistributionMode, bool IsPublished,
    EducationStage? EducationStage, GradeLevel? GradeLevel, DateTime? AvailableFrom,
    DateTime? AvailableUntil, IReadOnlyList<SharedPackageTeacherInput> Teachers,
    IReadOnlyList<SharedPackageItemInput> Items, IReadOnlyList<AcademicScopeDto>? AcademicScopes)
    : IRequest<SharedPackageCommandResult>;

public sealed record UploadSharedPackageImageCommand(Guid ActorUserId, Guid PackageId,
    byte[] Content, string FileName, string? ContentType) : IRequest<SharedPackageCommandResult>;

public sealed record PublishSharedPackageCommand(Guid ActorUserId, Guid PackageId)
    : IRequest<SharedPackageCommandResult>;

public sealed class CreateSharedPackageCommandHandler(IAppDbContext db)
    : IRequestHandler<CreateSharedPackageCommand, SharedPackageCommandResult>
{
    public async Task<SharedPackageCommandResult> Handle(CreateSharedPackageCommand request, CancellationToken ct)
    {
        var basicError = ValidateDistribution(request);
        if (basicError is not null) return SharedPackageCommandResult.Invalid(basicError);
        var relationError = await ValidateRelationsAsync(request, ct);
        if (relationError is not null) return SharedPackageCommandResult.Invalid(relationError);

        var scopes = ResolveScopes(request);
        var scopeValidation = await new AcademicScopeService(db).ValidateScopeDtosAsync(scopes, ct);
        if (!scopeValidation.IsValid)
            return SharedPackageCommandResult.Invalid(
                scopeValidation.Message ?? "نطاق الباكدج المشترك الأكاديمي غير صالح",
                scopeValidation.ErrorCode ?? "ACADEMIC_SCOPE_INVALID");

        var package = new SharedTeacherPackage
        {
            Id = Guid.NewGuid(), Name = request.Name.Trim(),
            Slug = string.IsNullOrWhiteSpace(request.Slug) ? request.Name.Trim().ToLowerInvariant().Replace(' ', '-') : request.Slug.Trim(),
            Description = request.Description ?? string.Empty, ImageUrl = request.ImageUrl, Price = request.Price,
            DistributionMode = request.DistributionMode, IsPublished = request.IsPublished,
            EducationStage = request.EducationStage, GradeLevel = request.GradeLevel,
            AvailableFrom = request.AvailableFrom, AvailableUntil = request.AvailableUntil,
            CreatedByUserId = request.ActorUserId
        };
        foreach (var teacher in request.Teachers)
            package.Teachers.Add(new SharedTeacherPackageTeacher { Id = Guid.NewGuid(), TeacherId = teacher.TeacherId,
                SubjectId = teacher.SubjectId, AllocationMode = teacher.AllocationMode,
                AllocationValue = teacher.AllocationValue, DisplayOrder = teacher.DisplayOrder });
        foreach (var item in request.Items)
            package.Items.Add(new SharedTeacherPackageItem { Id = Guid.NewGuid(), TeacherId = item.TeacherId,
                SubjectId = item.SubjectId, ContentType = item.ContentType, ContentId = item.ContentId,
                Price = item.Price, IsIncluded = true });

        db.SharedTeacherPackages.Add(package);
        await db.SaveChangesAsync(ct);
        await new AcademicScopeService(db).SyncOwnerScopesAsync(StudentFacingScopeOwnerType.SharedTeacherPackage,
            package.Id, scopes, request.ActorUserId, ct);
        return new SharedPackageCommandResult(SharedPackageCommandStatus.Success, package.Id,
            Message: "تم حفظ الباكدج المشترك");
    }

    private static string? ValidateDistribution(CreateSharedPackageCommand r)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) return "اسم الباكدج مطلوب";
        if (r.Price <= 0) return "سعر الباكدج يجب أن يكون أكبر من صفر";
        if (r.Teachers.Count == 0) return "يجب اختيار مدرس واحد على الأقل";
        if (r.Items.Count == 0) return "يجب اختيار محتوى واحد على الأقل";
        if (r.AcademicScopes is null && !r.EducationStage.HasValue) return "يجب اختيار المرحلة";
        if (r.AcademicScopes is null && !r.GradeLevel.HasValue) return "يجب اختيار الصف";
        if (r.Teachers.GroupBy(x => new { x.TeacherId, x.SubjectId }).Any(x => x.Count() > 1))
            return "لا يمكن تكرار نفس المدرس لنفس المادة داخل الباكدج";
        var pairs = r.Teachers.Select(x => (x.TeacherId, x.SubjectId)).ToHashSet();
        if (r.Items.Any(x => !pairs.Contains((x.TeacherId, x.SubjectId))))
            return "كل محتوى يجب أن يكون مرتبطاً بمدرس ومادة تم اختيارهم داخل الباكدج";
        if (r.Items.Any(x => x.Price <= 0)) return "سعر كل اختيار يجب أن يكون أكبر من صفر";
        var totals = r.Items.GroupBy(x => (x.TeacherId, x.SubjectId)).ToDictionary(x => x.Key, x => x.Sum(i => i.Price));
        foreach (var teacher in r.Teachers)
        {
            if (teacher.AllocationValue < 0) return "قيمة نصيب المدرس لا يمكن أن تكون سالبة";
            if (teacher.AllocationMode == TeacherAllocationMode.Percentage && teacher.AllocationValue > 100m)
                return "نسبة المدرس لا يمكن أن تتجاوز 100% من سعر الاختيار";
            if (teacher.AllocationMode == TeacherAllocationMode.FixedAmount && totals.TryGetValue((teacher.TeacherId, teacher.SubjectId), out var total) && teacher.AllocationValue > total)
                return "المبلغ الثابت للمدرس لا يمكن أن يتجاوز سعر اختياره";
        }
        var paths = r.Items.GroupBy(x => x.SubjectId ?? x.TeacherId).Select(g => g.Select(x => x.Price).Distinct().ToList()).ToList();
        if (paths.Any(x => x.Count > 1)) return "كل بدائل نفس المادة يجب أن يكون لها نفس سعر الاختيار";
        return Math.Abs(paths.Sum(x => x[0]) - r.Price) > 0.01m
            ? "مجموع أسعار الاختيارات يجب أن يساوي سعر الباكدج الأساسي" : null;
    }

    private async Task<string?> ValidateRelationsAsync(CreateSharedPackageCommand r, CancellationToken ct)
    {
        foreach (var pair in r.Teachers.Where(x => x.SubjectId.HasValue).Select(x => (x.TeacherId, x.SubjectId!.Value)).Distinct())
            if (!await db.TeacherSubjects.AnyAsync(x => x.TeacherId == pair.TeacherId && x.SubjectId == pair.Value, ct))
                return "المدرس المختار غير مرتبط بالمادة المحددة";
        var aliases = ResolveAllowedGradeAliases(r);
        foreach (var item in r.Items)
        {
            if (!item.SubjectId.HasValue) return "يجب تحديد مادة لكل محتوى داخل الباكدج";
            var unrestricted = aliases is null;
            var allowed = aliases ?? [];
            var matches = item.ContentType switch
            {
                SalesTargetType.Package => await db.Packages.AnyAsync(x => x.Id == item.ContentId && x.TeacherId == item.TeacherId && x.SubjectId == item.SubjectId && (unrestricted || string.IsNullOrWhiteSpace(x.TargetGrade) || x.TargetGrade == "All" || allowed.Contains(x.TargetGrade)), ct),
                SalesTargetType.Term => await db.Terms.AnyAsync(x => x.Id == item.ContentId && x.Package.TeacherId == item.TeacherId && x.Package.SubjectId == item.SubjectId && (unrestricted || string.IsNullOrWhiteSpace(x.Package.TargetGrade) || x.Package.TargetGrade == "All" || allowed.Contains(x.Package.TargetGrade)), ct),
                SalesTargetType.ContentSection => await db.ContentSections.AnyAsync(x => x.Id == item.ContentId && x.Term.Package.TeacherId == item.TeacherId && x.Term.Package.SubjectId == item.SubjectId && (unrestricted || string.IsNullOrWhiteSpace(x.Term.Package.TargetGrade) || x.Term.Package.TargetGrade == "All" || allowed.Contains(x.Term.Package.TargetGrade)), ct),
                SalesTargetType.Lesson => await db.Lessons.AnyAsync(x => x.Id == item.ContentId && x.ContentSection.Term.Package.TeacherId == item.TeacherId && x.ContentSection.Term.Package.SubjectId == item.SubjectId && (unrestricted || string.IsNullOrWhiteSpace(x.ContentSection.Term.Package.TargetGrade) || x.ContentSection.Term.Package.TargetGrade == "All" || allowed.Contains(x.ContentSection.Term.Package.TargetGrade)), ct),
                _ => false
            };
            if (!matches) return "المحتوى المختار لا يطابق المدرس أو المادة أو الصف";
        }
        return null;
    }

    private static IReadOnlyList<AcademicScopeDto> ResolveScopes(CreateSharedPackageCommand r) => r.AcademicScopes
        ?? (r.EducationStage.HasValue && r.GradeLevel.HasValue
            ? [new AcademicScopeDto(AcademicScopeLevel.GradeAllSubjects, r.EducationStage.Value, r.GradeLevel.Value)] : []);

    private static IReadOnlyCollection<string>? ResolveAllowedGradeAliases(CreateSharedPackageCommand r)
    {
        if (r.AcademicScopes?.Any(x => x.ScopeLevel is AcademicScopeLevel.PlatformWide or AcademicScopeLevel.StageWide) == true) return null;
        var grades = r.AcademicScopes?.Where(x => x.GradeLevel.HasValue).Select(x => x.GradeLevel!.Value).Distinct().ToList();
        if (grades is not { Count: > 0 } && r.GradeLevel.HasValue) grades = [r.GradeLevel.Value];
        return grades is { Count: > 0 } ? grades.SelectMany(GetGradeAliases).Distinct().ToList() : [];
    }

    private static IReadOnlyCollection<string> GetGradeAliases(GradeLevel grade) => grade switch
    {
        GradeLevel.FirstSecondary => ["FirstSecondary", "1st Secondary", "الأول الثانوي", "الاول الثانوي", "الأول الثانوى", "اولى ثانوي"],
        GradeLevel.SecondSecondary => ["SecondSecondary", "2nd Secondary", "الثاني الثانوي", "الثانى الثانوي", "الثاني الثانوى", "تانية ثانوي"],
        GradeLevel.SecondaryGrade3 => ["SecondaryGrade3", "ThirdSecondary", "3rd Secondary", "الثالث الثانوي", "الثالث الثانوى", "ثالثة ثانوي"],
        _ => [grade.ToString()]
    };
}

public sealed class UploadSharedPackageImageCommandHandler(IAppDbContext db, IContentImageStorage imageStorage)
    : IRequestHandler<UploadSharedPackageImageCommand, SharedPackageCommandResult>
{
    public async Task<SharedPackageCommandResult> Handle(UploadSharedPackageImageCommand request, CancellationToken ct)
    {
        var package = await db.SharedTeacherPackages.FirstOrDefaultAsync(x => x.Id == request.PackageId, ct);
        if (package is null) return new(SharedPackageCommandStatus.NotFound, Message: "الباكدج المشترك غير موجود");
        if (request.Content.Length is 0 or > 10 * 1024 * 1024) return SharedPackageCommandResult.Invalid("Image must be between 1 byte and 10 MB");
        UploadFileSafety.Validate(request.Content, request.FileName, request.ContentType, SafeUploadKind.PublicImage);
        await using var stream = new MemoryStream(request.Content, writable: false);
        var url = await imageStorage.SaveAsWebpAsync(stream, "shared-packages", ct);
        package.ImageUrl = url; package.UpdatedByUserId = request.ActorUserId; package.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return new(SharedPackageCommandStatus.Success, request.PackageId, url, "Shared package image uploaded successfully");
    }
}

public sealed class PublishSharedPackageCommandHandler(IAppDbContext db)
    : IRequestHandler<PublishSharedPackageCommand, SharedPackageCommandResult>
{
    public async Task<SharedPackageCommandResult> Handle(PublishSharedPackageCommand request, CancellationToken ct)
    {
        var package = await db.SharedTeacherPackages.FirstOrDefaultAsync(x => x.Id == request.PackageId, ct);
        if (package is null) return new(SharedPackageCommandStatus.NotFound, Message: "الباكدج المشترك غير موجود");
        var validation = await new AcademicScopeService(db).ValidateTargetHasScopeAsync(StudentFacingScopeOwnerType.SharedTeacherPackage, package.Id, ct);
        if (!validation.IsEligible) return SharedPackageCommandResult.Invalid(validation.Message ?? "الباكدج المشترك يجب أن يكون مربوطا بنطاق أكاديمي صالح قبل النشر", validation.ErrorCode ?? "ACADEMIC_SCOPE_TARGET_UNSCOPED");
        package.IsPublished = true; package.UpdatedByUserId = request.ActorUserId; package.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return new(SharedPackageCommandStatus.Success, package.Id, Message: "تم نشر الباكدج المشترك");
    }
}
