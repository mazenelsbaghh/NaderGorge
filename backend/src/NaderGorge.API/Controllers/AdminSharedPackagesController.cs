using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Admin.SharedPackages;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;
using SixLabors.ImageSharp;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/admin/shared-packages")]
[Authorize(Roles = "Admin,Supervisor")]
public class AdminSharedPackagesController : ControllerBase
{
    private readonly IAppDbContext _db;
    private readonly ISender? _sender;

    public AdminSharedPackagesController(IAppDbContext db, ISender? sender = null)
    {
        _db = db;
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var items = await _db.SharedTeacherPackages
            .Include(x => x.Teachers).ThenInclude(x => x.Teacher).ThenInclude(x => x.User)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Slug,
                x.ImageUrl,
                x.Price,
                x.IsPublished,
                x.DistributionMode,
                educationStage = x.EducationStage.HasValue ? x.EducationStage.ToString() : null,
                gradeLevel = x.GradeLevel.HasValue ? x.GradeLevel.ToString() : null,
                teacherCount = x.Teachers.Count,
                teachers = x.Teachers.Select(t => new
                {
                    t.TeacherId,
                    teacherName = t.Teacher.User.FullName,
                    t.SubjectId,
                    subjectName = t.Subject != null ? t.Subject.Name : null,
                    t.AllocationMode,
                    t.AllocationValue
                })
            })
            .ToListAsync(ct);

        return Ok(new { success = true, data = items });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail([FromRoute] Guid id, CancellationToken ct)
    {
        var item = await _db.SharedTeacherPackages
            .Include(x => x.Teachers).ThenInclude(x => x.Teacher).ThenInclude(x => x.User)
            .Include(x => x.Teachers).ThenInclude(x => x.Subject)
            .Include(x => x.Items).ThenInclude(x => x.Subject)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        return item == null
            ? NotFound(new { success = false, message = "الباكدج المشترك غير موجود" })
            : Ok(new { success = true, data = item });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveSharedPackageDto dto, CancellationToken ct)
    {
        // The one-argument constructor remains available to legacy validation tests; runtime DI always supplies ISender.
        var actorUserId = _sender is null ? Guid.Empty : User.RequireUserId();
        var command = new CreateSharedPackageCommand(actorUserId, dto.Name, dto.Slug,
            dto.Description, dto.ImageUrl, dto.Price, dto.DistributionMode, dto.IsPublished, dto.EducationStage,
            dto.GradeLevel, dto.AvailableFrom, dto.AvailableUntil,
            dto.Teachers.Select(x => new SharedPackageTeacherInput(x.TeacherId, x.SubjectId, x.AllocationMode, x.AllocationValue, x.DisplayOrder)).ToList(),
            dto.Items.Select(x => new SharedPackageItemInput(x.TeacherId, x.SubjectId, x.ContentType, x.ContentId, x.Price)).ToList(),
            dto.AcademicScopes);
        var result = _sender is null
            ? await new CreateSharedPackageCommandHandler(_db).Handle(command, ct)
            : await _sender.Send(command, ct);
        return result.Status == SharedPackageCommandStatus.Success
            ? Ok(new { success = true, data = new { id = result.Id }, message = result.Message })
            : BadRequest(new { success = false, message = result.Message, errors = result.ErrorCode is null ? null : new[] { result.ErrorCode } });
    }

    [HttpPost("{id:guid}/image")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> UploadImage([FromRoute] Guid id, IFormFile image, CancellationToken ct)
    {
        if (image.Length == 0 || image.Length > 10 * 1024 * 1024)
        {
            return BadRequest(ApiResponse.Fail("Image must be between 1 byte and 10 MB"));
        }

        await using var imageStream = image.OpenReadStream(); using var memoryStream = new MemoryStream();
        await imageStream.CopyToAsync(memoryStream, ct);

        try
        {
            var result = await RequireSender().Send(new UploadSharedPackageImageCommand(User.RequireUserId(), id,
                memoryStream.ToArray(), image.FileName, image.ContentType), ct);
            return result.Status switch
            {
                SharedPackageCommandStatus.Success => Ok(ApiResponse<string>.Ok(result.Value!, result.Message)),
                SharedPackageCommandStatus.NotFound => NotFound(ApiResponse.Fail(result.Message!)),
                _ => BadRequest(ApiResponse.Fail(result.Message!))
            };
        }
        catch (InvalidUploadContentException) { return BadRequest(ApiResponse.Fail("Uploaded file is not a supported image")); }
        catch (UnknownImageFormatException) { return BadRequest(ApiResponse.Fail("Uploaded file is not a supported image")); }
        catch (InvalidImageContentException) { return BadRequest(ApiResponse.Fail("Uploaded image is invalid or too large")); }
    }

    [HttpPost("{id:guid}/publish")]
    public async Task<IActionResult> Publish([FromRoute] Guid id, CancellationToken ct)
    {
        var command = new PublishSharedPackageCommand(User.RequireUserId(), id);
        var result = _sender is null
            ? await new PublishSharedPackageCommandHandler(_db).Handle(command, ct)
            : await _sender.Send(command, ct);
        return result.Status switch
        {
            SharedPackageCommandStatus.Success => Ok(new { success = true, message = result.Message }),
            SharedPackageCommandStatus.NotFound => NotFound(new { success = false, message = result.Message }),
            _ => BadRequest(new { success = false, message = result.Message, errors = result.ErrorCode is null ? null : new[] { result.ErrorCode } })
        };
    }

    private ISender RequireSender() => _sender ?? throw new InvalidOperationException("MediatR sender is required for shared-package image uploads.");

    private static string? ValidateDistribution(SaveSharedPackageDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name)) return "اسم الباكدج مطلوب";
        if (dto.Price <= 0) return "سعر الباكدج يجب أن يكون أكبر من صفر";
        if (dto.Teachers.Count == 0) return "يجب اختيار مدرس واحد على الأقل";
        if (dto.Items.Count == 0) return "يجب اختيار محتوى واحد على الأقل";
        if (dto.AcademicScopes == null && !dto.EducationStage.HasValue) return "يجب اختيار المرحلة";
        if (dto.AcademicScopes == null && !dto.GradeLevel.HasValue) return "يجب اختيار الصف";

        var duplicatedTeacherSubject = dto.Teachers
            .GroupBy(x => new { x.TeacherId, x.SubjectId })
            .FirstOrDefault(x => x.Count() > 1);
        if (duplicatedTeacherSubject != null) return "لا يمكن تكرار نفس المدرس لنفس المادة داخل الباكدج";

        var teacherSubjects = dto.Teachers
            .Select(x => new { x.TeacherId, x.SubjectId })
            .ToHashSet();
        if (dto.Items.Any(x => !teacherSubjects.Contains(new { x.TeacherId, x.SubjectId })))
        {
            return "كل محتوى يجب أن يكون مرتبطاً بمدرس ومادة تم اختيارهم داخل الباكدج";
        }

        if (dto.Items.Any(x => x.Price <= 0)) return "سعر كل اختيار يجب أن يكون أكبر من صفر";

        var itemPriceByTeacherSubject = dto.Items
            .GroupBy(x => new { x.TeacherId, x.SubjectId })
            .ToDictionary(x => x.Key, x => x.Sum(item => item.Price));

        foreach (var teacher in dto.Teachers)
        {
            if (teacher.AllocationValue < 0) return "قيمة نصيب المدرس لا يمكن أن تكون سالبة";

            if (teacher.AllocationMode == TeacherAllocationMode.Percentage && teacher.AllocationValue > 100m)
            {
                return "نسبة المدرس لا يمكن أن تتجاوز 100% من سعر الاختيار";
            }

            if (teacher.AllocationMode == TeacherAllocationMode.FixedAmount
                && itemPriceByTeacherSubject.TryGetValue(new { teacher.TeacherId, teacher.SubjectId }, out var teacherItemPrice)
                && teacher.AllocationValue > teacherItemPrice)
            {
                return "المبلغ الثابت للمدرس لا يمكن أن يتجاوز سعر اختياره";
            }
        }

        var groupedItemPrices = dto.Items
            .GroupBy(x => x.SubjectId ?? x.TeacherId)
            .Select(group => new
            {
                Key = group.Key,
                Prices = group.Select(x => x.Price).Distinct().ToList()
            })
            .ToList();

        if (groupedItemPrices.Any(x => x.Prices.Count > 1))
        {
            return "كل بدائل نفس المادة يجب أن يكون لها نفس سعر الاختيار";
        }

        var selectedPathTotal = groupedItemPrices.Sum(x => x.Prices[0]);
        if (Math.Abs(selectedPathTotal - dto.Price) > 0.01m)
        {
            return "مجموع أسعار الاختيارات يجب أن يساوي سعر الباكدج الأساسي";
        }

        return null;
    }

    private static IReadOnlyList<AcademicScopeDto> ResolveAcademicScopes(SaveSharedPackageDto dto)
    {
        if (dto.AcademicScopes != null)
            return dto.AcademicScopes;

        return dto.EducationStage.HasValue && dto.GradeLevel.HasValue
            ? [new AcademicScopeDto(AcademicScopeLevel.GradeAllSubjects, dto.EducationStage.Value, dto.GradeLevel.Value)]
            : [];
    }

    private async Task<string?> ValidateTeacherSubjectAndContentAsync(SaveSharedPackageDto dto, CancellationToken ct)
    {
        var teacherSubjectPairs = dto.Teachers
            .Where(x => x.SubjectId.HasValue)
            .Select(x => new { x.TeacherId, SubjectId = x.SubjectId!.Value })
            .Distinct()
            .ToList();

        foreach (var pair in teacherSubjectPairs)
        {
            var exists = await _db.TeacherSubjects
                .AnyAsync(x => x.TeacherId == pair.TeacherId && x.SubjectId == pair.SubjectId, ct);
            if (!exists)
            {
                return "المدرس المختار غير مرتبط بالمادة المحددة";
            }
        }

        foreach (var item in dto.Items)
        {
            if (!item.SubjectId.HasValue)
            {
                return "يجب تحديد مادة لكل محتوى داخل الباكدج";
            }

            var gradeAliases = ResolveAllowedGradeAliases(dto);
            var isGradeUnrestricted = gradeAliases == null;
            var allowedGradeAliases = gradeAliases ?? Array.Empty<string>();
            var matches = item.ContentType switch
            {
                SalesTargetType.Package => await _db.Packages.AnyAsync(x =>
                    x.Id == item.ContentId
                    && x.TeacherId == item.TeacherId
                    && x.SubjectId == item.SubjectId.Value
                    && (isGradeUnrestricted || string.IsNullOrWhiteSpace(x.TargetGrade) || x.TargetGrade == "All" || allowedGradeAliases.Contains(x.TargetGrade)), ct),
                SalesTargetType.Term => await _db.Terms
                    .Include(x => x.Package)
                    .AnyAsync(x =>
                        x.Id == item.ContentId
                        && x.Package.TeacherId == item.TeacherId
                        && x.Package.SubjectId == item.SubjectId.Value
                        && (isGradeUnrestricted || string.IsNullOrWhiteSpace(x.Package.TargetGrade) || x.Package.TargetGrade == "All" || allowedGradeAliases.Contains(x.Package.TargetGrade)), ct),
                SalesTargetType.ContentSection => await _db.ContentSections
                    .Include(x => x.Term).ThenInclude(x => x.Package)
                    .AnyAsync(x =>
                        x.Id == item.ContentId
                        && x.Term.Package.TeacherId == item.TeacherId
                        && x.Term.Package.SubjectId == item.SubjectId.Value
                        && (isGradeUnrestricted || string.IsNullOrWhiteSpace(x.Term.Package.TargetGrade) || x.Term.Package.TargetGrade == "All" || allowedGradeAliases.Contains(x.Term.Package.TargetGrade)), ct),
                SalesTargetType.Lesson => await _db.Lessons
                    .Include(x => x.ContentSection).ThenInclude(x => x.Term).ThenInclude(x => x.Package)
                    .AnyAsync(x =>
                        x.Id == item.ContentId
                        && x.ContentSection.Term.Package.TeacherId == item.TeacherId
                        && x.ContentSection.Term.Package.SubjectId == item.SubjectId.Value
                        && (isGradeUnrestricted || string.IsNullOrWhiteSpace(x.ContentSection.Term.Package.TargetGrade) || x.ContentSection.Term.Package.TargetGrade == "All" || allowedGradeAliases.Contains(x.ContentSection.Term.Package.TargetGrade)), ct),
                _ => false
            };

            if (!matches)
            {
                return "المحتوى المختار لا يطابق المدرس أو المادة أو الصف";
            }
        }

        return null;
    }

    private static IReadOnlyCollection<string>? ResolveAllowedGradeAliases(SaveSharedPackageDto dto)
    {
        var scopes = dto.AcademicScopes;
        if (scopes?.Any(x => x.ScopeLevel is AcademicScopeLevel.PlatformWide or AcademicScopeLevel.StageWide) == true)
        {
            return null;
        }

        var grades = scopes?
            .Where(x => x.GradeLevel.HasValue)
            .Select(x => x.GradeLevel!.Value)
            .Distinct()
            .ToList();

        if (grades is not { Count: > 0 } && dto.GradeLevel.HasValue)
        {
            grades = [dto.GradeLevel.Value];
        }

        return grades is { Count: > 0 }
            ? grades.SelectMany(grade => GetGradeAliases(grade)).Distinct().ToList()
            : Array.Empty<string>();
    }

    private static IReadOnlyCollection<string> GetGradeAliases(GradeLevel? gradeLevel)
    {
        if (!gradeLevel.HasValue) return Array.Empty<string>();

        return gradeLevel.Value switch
        {
            GradeLevel.FirstSecondary => new[]
            {
                "FirstSecondary",
                "1st Secondary",
                "الأول الثانوي",
                "الاول الثانوي",
                "الأول الثانوى",
                "اولى ثانوي"
            },
            GradeLevel.SecondSecondary => new[]
            {
                "SecondSecondary",
                "2nd Secondary",
                "الثاني الثانوي",
                "الثانى الثانوي",
                "الثاني الثانوى",
                "تانية ثانوي"
            },
            GradeLevel.SecondaryGrade3 => new[]
            {
                "SecondaryGrade3",
                "ThirdSecondary",
                "3rd Secondary",
                "الثالث الثانوي",
                "الثالث الثانوى",
                "ثالثة ثانوي"
            },
            _ => new[] { gradeLevel.Value.ToString() }
        };
    }
}

public record SaveSharedPackageDto(
    string Name,
    string? Slug,
    string? Description,
    string? ImageUrl,
    decimal Price,
    SharedPackageDistributionMode DistributionMode,
    bool IsPublished,
    EducationStage? EducationStage,
    GradeLevel? GradeLevel,
    DateTime? AvailableFrom,
    DateTime? AvailableUntil,
    List<SharedPackageTeacherDto> Teachers,
    List<SharedPackageItemDto> Items,
    IReadOnlyList<AcademicScopeDto>? AcademicScopes = null
);

public record SharedPackageTeacherDto(
    Guid TeacherId,
    Guid? SubjectId,
    TeacherAllocationMode AllocationMode,
    decimal AllocationValue,
    int DisplayOrder
);

public record SharedPackageItemDto(
    Guid TeacherId,
    Guid? SubjectId,
    SalesTargetType ContentType,
    Guid ContentId,
    decimal Price
);
