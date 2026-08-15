using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Services;
using NaderGorge.Application.Features.Admin.TeacherFinanceCenter.SharedPackages;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;
using System.Text.Json;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/student/shared-packages")]
[Authorize(Roles = "Student")]
public class StudentSharedPackagesController : ControllerBase
{
    private readonly IAppDbContext _db;
    private readonly BalanceService _balanceService;
    private readonly TeacherAccountingService _teacherAccounting;
    private readonly IAcademicScopeService _academicScope;
    private readonly IContentArchiveAccessService _archiveAccess;

    public StudentSharedPackagesController(
        IAppDbContext db,
        BalanceService balanceService,
        TeacherAccountingService teacherAccounting,
        IAcademicScopeService academicScope,
        IContentArchiveAccessService? archiveAccess = null)
    {
        _db = db;
        _balanceService = balanceService;
        _teacherAccounting = teacherAccounting;
        _academicScope = academicScope;
        _archiveAccess = archiveAccess ?? new ContentArchiveAccessService(db);
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var studentId = User.RequireUserId();
        var packages = await _db.SharedTeacherPackages
            .Where(x => x.IsPublished
                && x.Teachers.Any()
                // A package is only purchasable when every advertised teacher
                // can actually expose their selected content to students.
                && x.Teachers.All(t => t.Teacher.IsContentVisibleToStudents)
                && (!x.AvailableFrom.HasValue || x.AvailableFrom.Value <= now)
                && (!x.AvailableUntil.HasValue || x.AvailableUntil.Value > now))
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Slug,
                x.Description,
                x.ImageUrl,
                x.Price,
                educationStage = x.EducationStage.HasValue ? x.EducationStage.ToString() : null,
                gradeLevel = x.GradeLevel.HasValue ? x.GradeLevel.ToString() : null
            })
            .ToListAsync(ct);

        var eligiblePackageIds = new List<Guid>();
        foreach (var package in packages)
        {
            if (await _academicScope.IsOwnerEligibleForStudentAsync(
                    StudentFacingScopeOwnerType.SharedTeacherPackage,
                    package.Id,
                    studentId,
                    ct))
            {
                eligiblePackageIds.Add(package.Id);
            }
        }

        packages = packages.Where(x => eligiblePackageIds.Contains(x.Id)).ToList();

        return Ok(new { success = true, data = packages });
    }

    [HttpGet("purchased")]
    public async Task<IActionResult> Purchased(CancellationToken ct)
    {
        var studentId = User.RequireUserId();
        var events = await _db.TeacherFinancialEvents
            .Include(x => x.Allocations).ThenInclude(x => x.Teacher).ThenInclude(x => x.User)
            .Where(x => x.StudentId == studentId
                && x.SourceType == TeacherFinancialSourceType.SharedPackagePurchase
                && x.TargetType == SalesTargetType.Package)
            .OrderByDescending(x => x.OccurredAt)
            .ToListAsync(ct);

        if (events.Count == 0)
        {
            return Ok(new { success = true, data = Array.Empty<object>() });
        }

        var sharedPackageIds = events.Select(x => x.TargetId).Distinct().ToList();
        var packages = await _db.SharedTeacherPackages
            .Include(x => x.Items).ThenInclude(x => x.Subject)
            .Include(x => x.Teachers).ThenInclude(x => x.Subject)
            .Where(x => sharedPackageIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct);

        var selectedItems = events
            .Where(x => packages.ContainsKey(x.TargetId))
            .SelectMany(x =>
            {
                var selectedTeacherIds = ResolvePurchasedTeacherIds(x);
                return packages[x.TargetId].Items.Where(item => selectedTeacherIds.Contains(item.TeacherId));
            })
            .DistinctBy(x => x.Id)
            .ToList();

        var contentLookup = await BuildPurchasedContentLookupAsync(selectedItems, ct);

        var data = events
            .Where(x => packages.ContainsKey(x.TargetId))
            .Select(evt =>
            {
                var package = packages[evt.TargetId];
                var selectedTeacherIds = ResolvePurchasedTeacherIds(evt);
                var selectedPackageItems = package.Items
                    .Where(item => selectedTeacherIds.Contains(item.TeacherId))
                    .ToList();

                var teachers = selectedPackageItems
                    .GroupBy(item => new { item.TeacherId, item.SubjectId })
                    .Select(group =>
                    {
                        var allocation = evt.Allocations.FirstOrDefault(x => x.TeacherId == group.Key.TeacherId);
                        var teacher = allocation?.Teacher;
                        var firstItem = group.First();
                        var content = contentLookup.GetValueOrDefault(firstItem.Id);

                        return new
                        {
                            teacherId = group.Key.TeacherId,
                            teacherName = teacher?.User?.FullName ?? "المدرس",
                            teacherProfileImageUrl = teacher?.ProfileImageUrl,
                            subjectId = group.Key.SubjectId,
                            subjectName = firstItem.Subject?.Name,
                            contentCount = group.Count(),
                            contentName = content?.Name ?? "المحتوى",
                            contentUrl = content?.Url ?? "/student/packages",
                        };
                    })
                    .OrderBy(x => x.subjectName)
                    .ThenBy(x => x.teacherName)
                    .ToList();

                return new
                {
                    id = evt.SourceId,
                    sharedPackageId = package.Id,
                    package.Name,
                    package.Description,
                    package.ImageUrl,
                    price = evt.PaidAmount,
                    purchasedAt = evt.OccurredAt,
                    teachers
                };
            })
            .ToList();

        return Ok(new { success = true, data });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail([FromRoute] Guid id, CancellationToken ct)
    {
        var package = await _db.SharedTeacherPackages
            .Include(x => x.Teachers).ThenInclude(x => x.Teacher).ThenInclude(x => x.User)
            .Include(x => x.Teachers).ThenInclude(x => x.Subject)
            .Include(x => x.Items)
            .ThenInclude(x => x.Subject)
            .FirstOrDefaultAsync(x => x.Id == id && x.IsPublished, ct);

        if (package == null)
        {
            return NotFound(new { success = false, message = "الباكدج المشترك غير موجود" });
        }

        if (package.Teachers.Any(teacher => !teacher.Teacher.IsContentVisibleToStudents))
        {
            return BadRequest(new { success = false, message = "هذا الباكدج يحتوي محتوى مدرس غير متاح للطلاب حالياً" });
        }

        var studentId = User.RequireUserId();
        if (!await _academicScope.IsOwnerEligibleForStudentAsync(
                StudentFacingScopeOwnerType.SharedTeacherPackage,
                package.Id,
                studentId,
                ct))
        {
            return NotFound(new { success = false, message = "الباكدج المشترك غير متاح لنطاقك الدراسي الحالي" });
        }

        var eligibleItems = new List<SharedTeacherPackageItem>();
        foreach (var item in package.Items)
        {
            if (await IsSharedPackageItemEligibleAsync(item, studentId, ct))
                eligibleItems.Add(item);
        }

        var packageIds = package.Items.Where(x => x.ContentType == SalesTargetType.Package).Select(x => x.ContentId).ToList();
        var termIds = package.Items.Where(x => x.ContentType == SalesTargetType.Term).Select(x => x.ContentId).ToList();
        var sectionIds = package.Items.Where(x => x.ContentType == SalesTargetType.ContentSection).Select(x => x.ContentId).ToList();
        var lessonIds = package.Items.Where(x => x.ContentType == SalesTargetType.Lesson).Select(x => x.ContentId).ToList();
        var examIds = package.Items.Where(x => x.ContentType == SalesTargetType.PublicExam).Select(x => x.ContentId).ToList();

        var packageNames = await _db.Packages.Where(x => packageIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var termNames = await _db.Terms.Where(x => termIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Title, ct);
        var sectionNames = await _db.ContentSections.Where(x => sectionIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Title, ct);
        var lessonNames = await _db.Lessons.Where(x => lessonIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Title, ct);
        var examNames = await _db.PublicExamProducts
            .Include(x => x.Exam)
            .Where(x => examIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Exam.Title, ct);

        string ResolveName(SharedTeacherPackageItem item) => item.ContentType switch
        {
            SalesTargetType.Package => packageNames.GetValueOrDefault(item.ContentId, "باكدج"),
            SalesTargetType.Term => termNames.GetValueOrDefault(item.ContentId, "ترم"),
            SalesTargetType.ContentSection => sectionNames.GetValueOrDefault(item.ContentId, "قسم"),
            SalesTargetType.Lesson => lessonNames.GetValueOrDefault(item.ContentId, "حصة"),
            SalesTargetType.PublicExam => examNames.GetValueOrDefault(item.ContentId, "امتحان عام"),
            _ => "محتوى"
        };

        var dto = new
        {
            package.Id,
            package.Name,
            package.Slug,
            package.Description,
            package.ImageUrl,
            package.Price,
            educationStage = package.EducationStage.HasValue ? package.EducationStage.ToString() : null,
            gradeLevel = package.GradeLevel.HasValue ? package.GradeLevel.ToString() : null,
            teachers = package.Teachers
                .OrderBy(x => x.DisplayOrder)
                .Select(x => new
                {
                    x.TeacherId,
                    teacherName = x.Teacher.User.FullName,
                    teacherProfileImageUrl = x.Teacher.ProfileImageUrl,
                    subjectId = x.SubjectId,
                    subjectName = x.Subject != null ? x.Subject.Name : null,
                    x.AllocationMode,
                    x.AllocationValue
                }),
            items = eligibleItems.Select(x => new
            {
                x.Id,
                x.TeacherId,
                teacherName = package.Teachers.FirstOrDefault(t => t.TeacherId == x.TeacherId)?.Teacher.User.FullName,
                subjectId = x.SubjectId,
                subjectName = x.Subject != null ? x.Subject.Name : null,
                contentType = x.ContentType.ToString(),
                contentTypeValue = (int)x.ContentType,
                x.ContentId,
                x.Price,
                contentName = ResolveName(x)
            })
        };

        return Ok(new { success = true, data = dto });
    }

    [HttpPost("{id:guid}/purchase")]
    public async Task<IActionResult> Purchase([FromRoute] Guid id, [FromBody] PurchaseSharedPackageDto? dto, CancellationToken ct)
    {
        var studentId = User.RequireUserId();
        var package = await _db.SharedTeacherPackages
            .Include(x => x.Teachers).ThenInclude(x => x.Teacher).ThenInclude(x => x.User)
            .Include(x => x.Teachers).ThenInclude(x => x.Subject)
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id && x.IsPublished, ct);

        if (package == null)
        {
            return NotFound(new { success = false, message = "الباكدج المشترك غير موجود" });
        }

        if (package.Teachers.Any(teacher => !teacher.Teacher.IsContentVisibleToStudents))
        {
            return BadRequest(new { success = false, message = "هذا الباكدج يحتوي محتوى مدرس غير متاح للطلاب حالياً" });
        }

        if (!await _academicScope.IsOwnerEligibleForStudentAsync(
                StudentFacingScopeOwnerType.SharedTeacherPackage,
                package.Id,
                studentId,
                ct))
        {
            return BadRequest(new { success = false, message = "الباكدج المشترك غير متاح لنطاقك الدراسي الحالي", errors = new[] { "ACADEMIC_SCOPE_DENIED" } });
        }

        var selectionResult = ResolveSelections(package, dto?.Selections ?? []);
        if (selectionResult.Error != null)
        {
            return BadRequest(new { success = false, message = selectionResult.Error });
        }

        var selectedItems = package.Items
            .Where(item => selectionResult.SelectedTeacherBySubject.TryGetValue(item.SubjectId ?? Guid.Empty, out var teacherId)
                ? item.TeacherId == teacherId
                : selectionResult.SelectedTeacherIds.Contains(item.TeacherId))
            .ToList();

        if (selectedItems.Count == 0)
        {
            return BadRequest(new { success = false, message = "لا يوجد محتوى متاح للاختيارات المحددة" });
        }

        var purchaseIdempotencyKey = $"shared-package:{package.Id}:{studentId}";
        var existingPurchase = await _db.TeacherFinancialEvents
            .AnyAsync(x => x.IdempotencyKey == purchaseIdempotencyKey, ct);
        if (existingPurchase)
        {
            // Repeated clicks or a retry after the client lost the response must not
            // deduct the balance a second time.
            return Ok(new
            {
                success = true,
                data = new { sharedPackageId = package.Id, alreadyPurchased = true },
                message = "الباكدج مفعّل بالفعل على حسابك"
            });
        }

        foreach (var item in selectedItems)
        {
            if (!await IsSharedPackageItemEligibleAsync(item, studentId, ct))
            {
                return BadRequest(new { success = false, message = "تحتوي الاختيارات على محتوى غير متاح لنطاقك الدراسي الحالي", errors = new[] { "ACADEMIC_SCOPE_DENIED" } });
            }
        }

        var selectedItemsTotal = selectedItems.Sum(item => item.Price);
        if (Math.Abs(selectedItemsTotal - package.Price) > 0.01m)
        {
            return BadRequest(new { success = false, message = "أسعار اختيارات الباكدج غير متوافقة مع السعر الأساسي" });
        }

        var selectedTeachers = package.Teachers
            .Where(teacher => selectionResult.SelectedTeacherIds.Contains(teacher.TeacherId))
            .ToList();
        var allocationPreview = BuildAllocationPreview(package, selectedItems, selectedTeachers);
        if (allocationPreview.RequiresLossAcknowledgement && dto?.ConfirmLoss != true)
        {
            return Conflict(new
            {
                success = false,
                code = "FINANCE_LOSS_CONFIRMATION_REQUIRED",
                message = "إجمالي مستحقات المدرسين يتجاوز قيمة البيع؛ يلزم تأكيد الخسارة قبل إتمام الشراء",
                data = allocationPreview
            });
        }

        // The wallet debit, access grants, and teacher accounting are one purchase.
        // Keeping them in one transaction prevents the wallet from being charged if
        // a later save hits a concurrency or persistence error.
        await using var purchaseTransaction = await _db.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        try
        {
            await _balanceService.DeductBalance(studentId, package.Price, $"شراء باكدج مشترك: {package.Name}", package.Id, ct);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }

        var newlyGrantedItemsCount = 0;
        foreach (var contentItem in selectedItems)
        {
            // Shared packages can contain a course the student already owns from
            // an individual purchase. The grant table intentionally prevents a
            // duplicate user/content grant, so retain the existing access instead
            // of failing the whole package purchase after the wallet debit.
            if (await HasExistingGrantAsync(studentId, contentItem, ct))
            {
                continue;
            }

            _db.StudentAccessGrants.Add(CreateGrant(studentId, contentItem));
            newlyGrantedItemsCount++;
        }

        var student = await _db.Users.FirstOrDefaultAsync(x => x.Id == studentId, ct);
        var allocations = allocationPreview.Allocations
            .Select((allocation, index) => new TeacherFinancialAllocationInput(
                allocation.TeacherId,
                allocation.AllocationMode,
                allocation.AllocationValue,
                allocation.BasisAmount,
                allocation.TeacherShareAmount,
                index == 0 ? allocationPreview.PlatformShareAmount : 0m,
                student?.FullName,
                student?.PhoneNumber,
                package.Name))
            .ToList();

        if (allocationPreview.RequiresLossAcknowledgement)
        {
            _db.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                Action = "ConfirmSharedPackageFinanceLoss",
                EntityType = nameof(SharedTeacherPackage),
                EntityId = package.Id,
                PerformedByUserId = studentId,
                ActorType = "Student",
                Reason = "Student explicitly confirmed shared-package platform loss",
                NewValues = JsonSerializer.Serialize(new
                {
                    package.Id,
                    allocationPreview.SaleBasisAmount,
                    allocationPreview.TotalTeacherShareAmount,
                    allocationPreview.PlatformShareAmount,
                    allocations = allocationPreview.Allocations
                })
            });
        }

        var purchaseOperationId = Guid.NewGuid();
        await _teacherAccounting.RecordEventAsync(new TeacherFinancialEventInput(
            TeacherFinancialSourceType.SharedPackagePurchase,
            purchaseOperationId,
            studentId,
            SalesTargetType.Package,
            package.Id,
            package.Price,
            0m,
            package.Price,
            0m,
            allocationPreview.PlatformShareAmount,
            purchaseIdempotencyKey,
            System.Text.Json.JsonSerializer.Serialize(new
            {
                package.Id,
                package.Name,
                selections = selectionResult.SelectedTeacherBySubject.Select(x => new { subjectId = x.Key, teacherId = x.Value })
            }),
            DateTime.UtcNow,
            TeacherFinancialReviewStatus.AutoApproved,
            allocations), ct);

        await _db.SaveChangesAsync(ct);
        await purchaseTransaction.CommitAsync(ct);

        return Ok(new
        {
            success = true,
            data = new
            {
                purchaseOperationId,
                sharedPackageId = package.Id,
                paidAmount = package.Price,
                platformShareAmount = allocationPreview.PlatformShareAmount,
                grantedItemsCount = newlyGrantedItemsCount,
                teacherAllocations = allocations.Select(x => new { x.TeacherId, x.TeacherShareAmount })
            },
            message = "تم شراء الباكدج المشترك بنجاح"
        });
    }

    private static (Dictionary<Guid, Guid> SelectedTeacherBySubject, HashSet<Guid> SelectedTeacherIds, string? Error) ResolveSelections(
        SharedTeacherPackage package,
        IReadOnlyCollection<SharedPackageTeacherSelectionDto> requestedSelections)
    {
        var subjects = package.Teachers
            .GroupBy(x => x.SubjectId ?? Guid.Empty)
            .ToDictionary(x => x.Key, x => x.ToList());

        var selectedBySubject = new Dictionary<Guid, Guid>();

        foreach (var selection in requestedSelections.Where(x => x.SubjectId.HasValue && x.TeacherId.HasValue))
        {
            var subjectKey = selection.SubjectId!.Value;
            if (selectedBySubject.ContainsKey(subjectKey))
            {
                return (selectedBySubject, [], "لا يمكن اختيار أكثر من مدرس لنفس المادة");
            }

            var teacherInSubject = subjects.TryGetValue(subjectKey, out var subjectTeachers)
                && subjectTeachers.Any(x => x.TeacherId == selection.TeacherId!.Value);
            if (!teacherInSubject)
            {
                return (selectedBySubject, [], "اختيار المدرس لا يطابق المادة داخل الباكدج");
            }

            selectedBySubject[subjectKey] = selection.TeacherId!.Value;
        }

        foreach (var subject in subjects)
        {
            if (selectedBySubject.ContainsKey(subject.Key))
            {
                continue;
            }

            if (subject.Value.Count == 1)
            {
                selectedBySubject[subject.Key] = subject.Value[0].TeacherId;
                continue;
            }

            var subjectName = subject.Value[0].Subject?.Name ?? "المادة";
            return (selectedBySubject, [], $"اختر مدرساً واحداً لمادة {subjectName}");
        }

        return (selectedBySubject, selectedBySubject.Values.ToHashSet(), null);
    }

    private static SharedPackageAllocationPreview BuildAllocationPreview(
        SharedTeacherPackage package,
        IReadOnlyCollection<SharedTeacherPackageItem> selectedItems,
        IReadOnlyCollection<SharedTeacherPackageTeacher> selectedTeachers) =>
        SharedPackageAllocationPreviewService.Calculate(package.Price, selectedTeachers.Select(teacher =>
            new SharedPackageAllocationCandidate(
                teacher.TeacherId,
                teacher.Teacher.User.FullName,
                teacher.SubjectId,
                selectedItems.Where(item => item.TeacherId == teacher.TeacherId && item.SubjectId == teacher.SubjectId)
                    .Sum(item => item.Price),
                teacher.AllocationMode,
                teacher.AllocationValue)));

    private static StudentAccessGrant CreateGrant(Guid studentId, SharedTeacherPackageItem item)
    {
        var grant = new StudentAccessGrant
        {
            Id = Guid.NewGuid(),
            UserId = studentId,
            GrantedAt = DateTime.UtcNow,
            IsActive = true,
            GrantType = item.ContentType switch
            {
                SalesTargetType.Package => CodeType.Package,
                SalesTargetType.Term => CodeType.Term,
                SalesTargetType.ContentSection => CodeType.Month,
                SalesTargetType.Lesson => CodeType.Lesson,
                SalesTargetType.PublicExam => CodeType.Exam,
                _ => CodeType.Package
            }
        };

        switch (item.ContentType)
        {
            case SalesTargetType.Package:
                grant.PackageId = item.ContentId;
                break;
            case SalesTargetType.Term:
                grant.TermId = item.ContentId;
                break;
            case SalesTargetType.ContentSection:
                grant.ContentSectionId = item.ContentId;
                break;
            case SalesTargetType.Lesson:
                grant.LessonId = item.ContentId;
                break;
            case SalesTargetType.PublicExam:
                grant.PublicExamProductId = item.ContentId;
                break;
        }

        return grant;
    }

    private Task<bool> HasExistingGrantAsync(Guid studentId, SharedTeacherPackageItem item, CancellationToken ct) =>
        item.ContentType switch
        {
            SalesTargetType.Package => _db.StudentAccessGrants.AnyAsync(grant =>
                grant.UserId == studentId && grant.PackageId == item.ContentId, ct),
            SalesTargetType.Term => _db.StudentAccessGrants.AnyAsync(grant =>
                grant.UserId == studentId && grant.TermId == item.ContentId, ct),
            SalesTargetType.ContentSection => _db.StudentAccessGrants.AnyAsync(grant =>
                grant.UserId == studentId && grant.ContentSectionId == item.ContentId, ct),
            SalesTargetType.Lesson => _db.StudentAccessGrants.AnyAsync(grant =>
                grant.UserId == studentId && grant.LessonId == item.ContentId, ct),
            SalesTargetType.PublicExam => _db.StudentAccessGrants.AnyAsync(grant =>
                grant.UserId == studentId && grant.PublicExamProductId == item.ContentId, ct),
            _ => Task.FromResult(false)
        };

    private static HashSet<Guid> ResolvePurchasedTeacherIds(TeacherFinancialEvent evt)
    {
        var ids = evt.Allocations.Select(x => x.TeacherId).ToHashSet();
        if (ids.Count > 0 || string.IsNullOrWhiteSpace(evt.DetailsJson))
        {
            return ids;
        }

        try
        {
            using var document = JsonDocument.Parse(evt.DetailsJson);
            if (!document.RootElement.TryGetProperty("selections", out var selections) || selections.ValueKind != JsonValueKind.Array)
            {
                return ids;
            }

            foreach (var selection in selections.EnumerateArray())
            {
                if (selection.TryGetProperty("teacherId", out var teacherIdElement)
                    && teacherIdElement.TryGetGuid(out var teacherId))
                {
                    ids.Add(teacherId);
                }
            }
        }
        catch (JsonException)
        {
            return ids;
        }

        return ids;
    }

    private sealed record PurchasedContentLink(string Name, string Url);

    private async Task<Dictionary<Guid, PurchasedContentLink>> BuildPurchasedContentLookupAsync(
        IReadOnlyCollection<SharedTeacherPackageItem> items,
        CancellationToken ct)
    {
        var lookup = new Dictionary<Guid, PurchasedContentLink>();

        var packageIds = items.Where(x => x.ContentType == SalesTargetType.Package).Select(x => x.ContentId).Distinct().ToList();
        var termIds = items.Where(x => x.ContentType == SalesTargetType.Term).Select(x => x.ContentId).Distinct().ToList();
        var sectionIds = items.Where(x => x.ContentType == SalesTargetType.ContentSection).Select(x => x.ContentId).Distinct().ToList();
        var lessonIds = items.Where(x => x.ContentType == SalesTargetType.Lesson).Select(x => x.ContentId).Distinct().ToList();

        var packages = await _db.Packages
            .Where(x => packageIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Name })
            .ToDictionaryAsync(x => x.Id, ct);
        var terms = await _db.Terms
            .Where(x => termIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Title, x.PackageId })
            .ToDictionaryAsync(x => x.Id, ct);
        var sections = await _db.ContentSections
            .Where(x => sectionIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Title, x.TermId, PackageId = x.Term.PackageId })
            .ToDictionaryAsync(x => x.Id, ct);
        var lessons = await _db.Lessons
            .Where(x => lessonIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Title, PackageId = x.ContentSection.Term.PackageId })
            .ToDictionaryAsync(x => x.Id, ct);

        foreach (var item in items)
        {
            var link = item.ContentType switch
            {
                SalesTargetType.Package when packages.TryGetValue(item.ContentId, out var package) =>
                    new PurchasedContentLink(package.Name, $"/student/packages/{package.Id}"),
                SalesTargetType.Term when terms.TryGetValue(item.ContentId, out var term) =>
                    new PurchasedContentLink(term.Title, $"/student/packages/{term.PackageId}/terms/{term.Id}"),
                SalesTargetType.ContentSection when sections.TryGetValue(item.ContentId, out var section) =>
                    new PurchasedContentLink(section.Title, $"/student/packages/{section.PackageId}/terms/{section.TermId}/sections/{section.Id}"),
                SalesTargetType.Lesson when lessons.TryGetValue(item.ContentId, out var lesson) =>
                    new PurchasedContentLink(lesson.Title, $"/student/packages/{lesson.PackageId}/lessons/{lesson.Id}"),
                _ => null
            };

            if (link != null)
            {
                lookup[item.Id] = link;
            }
        }

        return lookup;
    }

    private async Task<bool> IsSharedPackageItemEligibleAsync(SharedTeacherPackageItem item, Guid studentId, CancellationToken ct)
    {
        if (!await _db.TeacherProfiles.AnyAsync(
                teacher => teacher.Id == item.TeacherId && teacher.IsContentVisibleToStudents,
                ct))
        {
            return false;
        }

        var archiveTarget = item.ContentType switch
        {
            SalesTargetType.Package => (ContentArchiveTargetType.Package, item.ContentId),
            SalesTargetType.Term => (ContentArchiveTargetType.Term, item.ContentId),
            SalesTargetType.ContentSection => (ContentArchiveTargetType.Section, item.ContentId),
            SalesTargetType.Lesson => (ContentArchiveTargetType.Lesson, item.ContentId),
            _ => ((ContentArchiveTargetType TargetType, Guid TargetId)?)null
        };
        if (item.ContentType == SalesTargetType.PublicExam)
        {
            var examId = await _db.PublicExamProducts
                .Where(product => product.Id == item.ContentId)
                .Select(product => (Guid?)product.ExamId)
                .FirstOrDefaultAsync(ct);
            archiveTarget = examId.HasValue ? (ContentArchiveTargetType.Exam, examId.Value) : null;
        }
        if (archiveTarget.HasValue && !await _archiveAccess.CanAcquireAsync(archiveTarget.Value.TargetType, archiveTarget.Value.TargetId, ct))
        {
            return false;
        }

        if (!await _academicScope.IsOwnerEligibleForStudentAsync(
                StudentFacingScopeOwnerType.SharedTeacherPackageItem,
                item.Id,
                studentId,
                ct))
        {
            return false;
        }

        var targetOwnerType = item.ContentType switch
        {
            SalesTargetType.Package => StudentFacingScopeOwnerType.Package,
            SalesTargetType.Term => StudentFacingScopeOwnerType.Term,
            SalesTargetType.ContentSection => StudentFacingScopeOwnerType.ContentSection,
            SalesTargetType.Lesson => StudentFacingScopeOwnerType.Lesson,
            SalesTargetType.PublicExam => StudentFacingScopeOwnerType.PublicExamProduct,
            _ => (StudentFacingScopeOwnerType?)null
        };

        return targetOwnerType == null ||
            await _academicScope.IsOwnerEligibleForStudentAsync(targetOwnerType.Value, item.ContentId, studentId, ct);
    }
}

public record PurchaseSharedPackageDto(List<SharedPackageTeacherSelectionDto> Selections, bool ConfirmLoss = false);

public record SharedPackageTeacherSelectionDto(Guid? SubjectId, Guid? TeacherId);
