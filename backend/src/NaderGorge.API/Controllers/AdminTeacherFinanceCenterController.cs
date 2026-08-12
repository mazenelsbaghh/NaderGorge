using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MediatR;
using System.Data;
using System.Text.Json;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Features.Admin.Queries;
using NaderGorge.Application.Features.Admin.TeacherFinanceCenter.SharedPackages;
using NaderGorge.Application.Features.Admin.TeacherFinanceCenter;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.API.Controllers;

/// <summary>Admin-only source-of-truth surface for teacher finance. It intentionally does not expose teacher self-service routes.</summary>
[ApiController]
[Route("api/admin/teacher-finance-center")]
[Authorize(Roles = "Admin")]
public class AdminTeacherFinanceCenterController : ControllerBase
{
    private readonly IAppDbContext _db;
    private readonly IMediator _mediator;
    private readonly TeacherSettlementAuthorityService _settlements;

    public AdminTeacherFinanceCenterController(IAppDbContext db, IMediator mediator)
    {
        _db = db;
        _mediator = mediator;
        _settlements = new TeacherSettlementAuthorityService(db);
    }

    private Guid ActorId() => User.RequireUserId();

    [HttpPost("shared-packages/{id:guid}/allocation-preview")]
    public async Task<IActionResult> PreviewSharedPackageAllocation(Guid id, [FromBody] SharedPackageAllocationPreviewRequestDto? dto, CancellationToken ct)
    {
        var package = await _db.SharedTeacherPackages.AsNoTracking()
            .Include(x => x.Teachers).ThenInclude(x => x.Teacher).ThenInclude(x => x.User)
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (package is null) return NotFound(new { success = false, message = "الباكدج المشترك غير موجود" });

        var selection = ResolvePreviewSelections(package, dto?.Selections ?? []);
        if (selection.Error is not null) return BadRequest(new { success = false, message = selection.Error });
        var teachers = package.Teachers.Where(x => selection.TeacherIds.Contains(x.TeacherId)).ToList();
        var items = package.Items.Where(item => selection.TeacherBySubject.TryGetValue(item.SubjectId ?? Guid.Empty, out var teacherId)
            ? item.TeacherId == teacherId : selection.TeacherIds.Contains(item.TeacherId)).ToList();
        if (items.Count == 0) return BadRequest(new { success = false, message = "لا يوجد محتوى للاختيارات المحددة" });
        if (Math.Abs(items.Sum(x => x.Price) - package.Price) > 0.01m)
            return BadRequest(new { success = false, message = "أسعار اختيارات الباكدج غير متوافقة مع السعر الأساسي" });

        var preview = SharedPackageAllocationPreviewService.Calculate(package.Price, teachers.Select(teacher =>
            new SharedPackageAllocationCandidate(teacher.TeacherId, teacher.Teacher.User.FullName, teacher.SubjectId,
                items.Where(item => item.TeacherId == teacher.TeacherId && item.SubjectId == teacher.SubjectId).Sum(item => item.Price),
                teacher.AllocationMode, teacher.AllocationValue)));
        return Ok(new { success = true, data = preview });
    }

    private static (Dictionary<Guid, Guid> TeacherBySubject, HashSet<Guid> TeacherIds, string? Error) ResolvePreviewSelections(
        SharedTeacherPackage package, IReadOnlyCollection<SharedPackageAllocationPreviewSelectionDto> selections)
    {
        var grouped = package.Teachers.GroupBy(x => x.SubjectId ?? Guid.Empty).ToDictionary(x => x.Key, x => x.ToList());
        var chosen = new Dictionary<Guid, Guid>();
        foreach (var selection in selections.Where(x => x.SubjectId.HasValue && x.TeacherId.HasValue))
        {
            if (!chosen.TryAdd(selection.SubjectId!.Value, selection.TeacherId!.Value) || !grouped.TryGetValue(selection.SubjectId.Value, out var teachers)
                || !teachers.Any(x => x.TeacherId == selection.TeacherId.Value))
                return (chosen, [], "اختيار المدرس لا يطابق المادة داخل الباكدج");
        }
        foreach (var subject in grouped)
        {
            if (chosen.ContainsKey(subject.Key)) continue;
            if (subject.Value.Count != 1) return (chosen, [], "اختر مدرساً واحداً لكل مادة لها أكثر من مدرس");
            chosen[subject.Key] = subject.Value[0].TeacherId;
        }
        return (chosen, chosen.Values.ToHashSet(), null);
    }

    [HttpGet("teachers/{teacherId:guid}/agreements")]
    public async Task<IActionResult> ListAgreements(Guid teacherId, CancellationToken ct)
    {
        var items = await _db.TeacherFinancialAgreements.AsNoTracking()
            .Where(x => x.TeacherId == teacherId)
            .OrderByDescending(x => x.EffectiveFrom)
            .Select(x => new TeacherAgreementDto(x.Id, x.TeacherId, x.ScopeType, x.ScopeId, x.Trigger, x.AllocationMode,
                x.AllocationValue, x.PriceBasis, x.EffectiveFrom, x.EffectiveTo, x.IsActive, x.Reason))
            .ToListAsync(ct);
        return Ok(new { success = true, data = items });
    }

    [HttpPost("teachers/{teacherId:guid}/agreements")]
    public async Task<IActionResult> CreateAgreement(Guid teacherId, [FromBody] UpsertTeacherAgreementDto dto, CancellationToken ct)
    {
        var response = await _mediator.Send(new CreateTeacherAgreementCommand(ActorId(), teacherId, ToTerms(dto)), ct);
        return response.Status == TeacherFinanceCommandStatus.Success
            ? CreatedAtAction(nameof(ListAgreements), new { teacherId }, new { success = true, data = response.Id })
            : AgreementError(response);
    }

    [HttpPut("agreements/{agreementId:guid}")]
    public async Task<IActionResult> ReplaceAgreement(Guid agreementId, [FromBody] UpsertTeacherAgreementDto dto, CancellationToken ct)
    {
        var response = await _mediator.Send(new ReplaceTeacherAgreementCommand(ActorId(), agreementId, ToTerms(dto)), ct);
        return response.Status == TeacherFinanceCommandStatus.Success ? Ok(new { success = true }) : AgreementError(response);
    }

    [HttpGet("teachers/{teacherId:guid}/summary")]
    public async Task<IActionResult> GetTeacherSummary(Guid teacherId, CancellationToken ct)
    {
        var account = await _db.TeacherAccounts.AsNoTracking().FirstOrDefaultAsync(x => x.TeacherId == teacherId, ct);
        var debt = await _db.TeacherPayoutAdjustments.AsNoTracking()
            .Where(x => x.TeacherId == teacherId && x.Status == TeacherPayoutAdjustmentStatus.Open && x.Amount < 0m)
            .SumAsync(x => (decimal?)-x.Amount, ct) ?? 0m;
        var paid = await _db.TeacherFinancialAllocations.AsNoTracking()
            .Where(x => x.TeacherId == teacherId && x.PayoutStatus == TeacherFinancialPayoutStatus.Paid)
            .SumAsync(x => (decimal?)x.TeacherShareAmount, ct) ?? 0m;
        return Ok(new
        {
            success = true,
            data = new { teacherId, totalEarned = account?.TotalEarnings ?? 0m, available = account?.CurrentBalance ?? 0m,
                reserved = account?.ReservedBalance ?? 0m, paid, debt, netPayable = Math.Max(0m, (account?.CurrentBalance ?? 0m) - debt) }
        });
    }

    [HttpGet("teachers/{teacherId:guid}/ledger")]
    public async Task<IActionResult> GetLedger(Guid teacherId, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] TeacherFinancialPayoutStatus? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var query = _db.TeacherFinancialAllocations.AsNoTracking().Include(x => x.TeacherFinancialEvent)
            .Where(x => x.TeacherId == teacherId);
        if (from.HasValue) query = query.Where(x => x.TeacherFinancialEvent.OccurredAt >= from.Value);
        if (to.HasValue) query = query.Where(x => x.TeacherFinancialEvent.OccurredAt <= to.Value);
        if (status.HasValue) query = query.Where(x => x.PayoutStatus == status.Value);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.TeacherFinancialEvent.OccurredAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.Id, x.TeacherFinancialEventId, x.ContentNameSnapshot, x.TeacherShareAmount, x.PlatformShareAmount,
                x.PayoutStatus, x.ReviewStatus, x.ReversedAmount, x.AgreementId, x.TeacherFinancialEvent.OccurredAt,
                sourceType = x.TeacherFinancialEvent.SourceType.ToString(), x.TeacherFinancialEvent.GrossAmount, x.TeacherFinancialEvent.DiscountAmount,
                x.TeacherFinancialEvent.PlatformDiscountAmount, x.TeacherFinancialEvent.TeacherDiscountAmount })
            .ToListAsync(ct);
        return Ok(new { success = true, data = new { items, total, page, pageSize } });
    }

    [HttpPost("settlements/preview")]
    public async Task<IActionResult> PreviewSettlement([FromBody] CreateSettlementDto dto, CancellationToken ct)
    {
        var preview = await BuildSettlementPreview(dto, ct);
        return preview.Error is null
            ? Ok(new { success = true, data = preview })
            : BadRequest(new { success = false, message = preview.Error });
    }

    [HttpPost("settlements")]
    public async Task<IActionResult> CreateSettlement([FromBody] CreateSettlementDto dto, CancellationToken ct)
    {
        var response = await _settlements.CreateAsync(ActorId(), new(dto.TeacherId, dto.PeriodFrom, dto.PeriodTo, dto.Note, dto.AllocationIds), ct);
        return response.Status == TeacherFinanceCommandStatus.Success
            ? CreatedAtAction(nameof(GetSettlement), new { id = response.Id }, new { success = true, data = new { id = response.Id } })
            : FinanceError(response);
    }

    [HttpGet("settlements/{id:guid}")]
    public async Task<IActionResult> GetSettlement(Guid id, CancellationToken ct)
    {
        var settlement = await _db.TeacherSettlements.AsNoTracking().Include(x => x.Lines).Include(x => x.Payments)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        return settlement is null ? NotFound(new { success = false, message = "التسوية غير موجودة" }) : Ok(new { success = true, data = settlement });
    }

    [HttpPost("settlements/{id:guid}/review")]
    public Task<IActionResult> ReviewSettlement(Guid id, CancellationToken ct) => TransitionSettlement(id, TeacherSettlementStatus.Draft, TeacherSettlementStatus.Reviewed, ct);

    [HttpPost("settlements/{id:guid}/approve")]
    public Task<IActionResult> ApproveSettlement(Guid id, CancellationToken ct) => TransitionSettlement(id, TeacherSettlementStatus.Reviewed, TeacherSettlementStatus.Approved, ct);

    [HttpPost("settlements/{id:guid}/pay")]
    public async Task<IActionResult> PaySettlement(Guid id, [FromBody] PaySettlementDto dto, CancellationToken ct)
    {
        var response = await _settlements.PayAsync(ActorId(), id, new(dto.PaymentMethod, dto.TransferReference, dto.AttachmentUrl, dto.Amount), ct);
        return response.Status == TeacherFinanceCommandStatus.Success ? Ok(new { success = true }) : FinanceError(response);
    }

    [HttpPost("settlements/{id:guid}/cancel")]
    public async Task<IActionResult> CancelSettlement(Guid id, CancellationToken ct)
    {
        var response = await _settlements.CancelAsync(id, ct);
        return response.Status == TeacherFinanceCommandStatus.Success ? Ok(new { success = true }) : FinanceError(response);
    }

    [HttpPost("reversals")]
    public async Task<IActionResult> ReverseSelectedLines([FromBody] CreateReversalDto dto, CancellationToken ct)
    {
        var response = await _settlements.ReverseAsync(new(dto.Lines.Select(x => new ReversalLineInput(x.AllocationId, x.Amount)).ToList(),
            dto.Reason, dto.Disposition, dto.IdempotencyKey), ct);
        return response.Status == TeacherFinanceCommandStatus.Success
            ? Ok(new { success = true, data = new { id = response.Id, duplicate = response.AlreadyApplied } })
            : FinanceError(response);
    }

    [HttpPost("invoices/{id:guid}/attachments")]
    public async Task<IActionResult> AttachInvoiceDocument(Guid id, [FromBody] AttachInvoiceDto dto, CancellationToken ct)
    {
        var response = await _settlements.AttachInvoiceAsync(id, dto.AttachmentUrl, ct);
        return response.Status == TeacherFinanceCommandStatus.Success ? Ok(new { success = true }) : FinanceError(response);
    }

    [HttpGet("bunny/cost-report")]
    public async Task<IActionResult> GetBunnyCostReport([FromQuery] string month, [FromQuery] Guid? teacherId,
        [FromQuery] Guid? packageId, CancellationToken ct)
    {
        if (!DateTime.TryParse($"{month}-01", out var parsedMonth))
            return BadRequest(new { success = false, message = "صيغة الشهر يجب أن تكون yyyy-MM" });

        var periodStart = DateTime.SpecifyKind(parsedMonth.Date, DateTimeKind.Utc);
        var response = await _mediator.Send(new GetBunnyCostReportQuery(periodStart, periodStart.AddMonths(1), teacherId, packageId), ct);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPost("bunny/usage/sync")]
    public async Task<IActionResult> SyncBunnyUsage([FromBody] SyncTeacherFinanceBunnyUsageDto dto, CancellationToken ct)
    {
        if (dto.PeriodEnd <= dto.PeriodStart)
            return BadRequest(new { success = false, message = "فترة التقرير غير صالحة" });

        var response = await _mediator.Send(new SyncBunnyUsageCommand(
            dto.PeriodStart, dto.PeriodEnd, dto.TeacherId, dto.PackageId, dto.ForceRefresh, ActorId()), ct);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    private static TeacherAgreementTerms ToTerms(UpsertTeacherAgreementDto dto) => new(dto.ScopeType, dto.ScopeId,
        dto.Trigger, dto.AllocationMode, dto.AllocationValue, dto.PriceBasis, dto.EffectiveFrom, dto.EffectiveTo, dto.Reason);

    private IActionResult AgreementError(TeacherFinanceCommandResult response) => response.Status switch
    {
        TeacherFinanceCommandStatus.NotFound => NotFound(new { success = false, message = response.Message }),
        TeacherFinanceCommandStatus.Conflict => Conflict(new { success = false, message = response.Message }),
        _ => BadRequest(new { success = false, message = response.Message })
    };

    private async Task<SettlementPreview> BuildSettlementPreview(CreateSettlementDto dto, CancellationToken ct)
    {
        if (dto.TeacherId == Guid.Empty || dto.PeriodTo < dto.PeriodFrom ||
            !await _db.TeacherProfiles.AnyAsync(x => x.Id == dto.TeacherId, ct))
            return SettlementPreview.Invalid("بيانات التسوية أو المدرس غير صالحة");

        var requestedIds = dto.AllocationIds?.Distinct().ToList();
        if (dto.AllocationIds is not null && requestedIds!.Count != dto.AllocationIds.Count)
            return SettlementPreview.Invalid("لا يمكن اختيار بند مرتين");
        var allocationQuery = _db.TeacherFinancialAllocations
            .Include(x => x.TeacherFinancialEvent)
            .Where(x => x.TeacherId == dto.TeacherId && x.TeacherShareAmount > x.ReversedAmount
                && x.PayoutStatus == TeacherFinancialPayoutStatus.Unpaid
                && (x.ReviewStatus == TeacherFinancialReviewStatus.AutoApproved || x.ReviewStatus == TeacherFinancialReviewStatus.Approved)
                && x.TeacherFinancialEvent.OccurredAt >= dto.PeriodFrom && x.TeacherFinancialEvent.OccurredAt <= dto.PeriodTo);
        if (requestedIds is not null && requestedIds.Count > 0) allocationQuery = allocationQuery.Where(x => requestedIds.Contains(x.Id));
        var allocations = await allocationQuery.OrderBy(x => x.TeacherFinancialEvent.OccurredAt).ToListAsync(ct);
        if (requestedIds is not null && requestedIds.Count > 0 && allocations.Count != requestedIds.Count)
            return SettlementPreview.Invalid("بعض البنود غير مؤهلة أو تم حجزها في تسوية أخرى");
        var gross = allocations.Sum(x => x.TeacherShareAmount - x.ReversedAmount);

        // Adjustments are consumed whole only. This prevents a single debt line from being accidentally
        // marked paid in two settlements when the remaining payable amount is smaller than that debt.
        var openAdjustments = await _db.TeacherPayoutAdjustments
            .Where(x => x.TeacherId == dto.TeacherId && x.Status == TeacherPayoutAdjustmentStatus.Open && x.Amount < 0m)
            .OrderBy(x => x.CreatedAt).ToListAsync(ct);
        var adjustments = new List<TeacherPayoutAdjustment>();
        var debt = 0m;
        foreach (var adjustment in openAdjustments)
        {
            var amount = -adjustment.Amount;
            if (debt + amount > gross) break;
            debt += amount;
            adjustments.Add(adjustment);
        }
        return new SettlementPreview(null, allocations, adjustments, gross, debt, gross - debt);
    }

    private async Task<IActionResult> TransitionSettlement(Guid id, TeacherSettlementStatus expected, TeacherSettlementStatus next, CancellationToken ct)
    {
        var response = await _settlements.TransitionAsync(ActorId(), id, expected, next, ct);
        return response.Status == TeacherFinanceCommandStatus.Success ? Ok(new { success = true }) : FinanceError(response);
    }

    private IActionResult FinanceError(TeacherFinanceCommandResult response) => response.Status switch
    {
        TeacherFinanceCommandStatus.NotFound => NotFound(new { success = false, message = response.Message }),
        TeacherFinanceCommandStatus.Conflict => Conflict(new { success = false, message = response.Message }),
        _ => BadRequest(new { success = false, message = response.Message })
    };
}

public record UpsertTeacherAgreementDto(TeacherAgreementScopeType ScopeType, Guid? ScopeId, TeacherAgreementTrigger Trigger,
    TeacherAgreementAllocationMode AllocationMode, decimal AllocationValue, TeacherPriceBasis PriceBasis,
    DateTime EffectiveFrom, DateTime? EffectiveTo, string Reason);

public record TeacherAgreementDto(Guid Id, Guid TeacherId, TeacherAgreementScopeType ScopeType, Guid? ScopeId,
    TeacherAgreementTrigger Trigger, TeacherAgreementAllocationMode AllocationMode, decimal AllocationValue,
    TeacherPriceBasis PriceBasis, DateTime EffectiveFrom, DateTime? EffectiveTo, bool IsActive, string Reason);

public record CreateSettlementDto(Guid TeacherId, DateTime PeriodFrom, DateTime PeriodTo, string? Note, IReadOnlyList<Guid>? AllocationIds = null);
public record PaySettlementDto(string PaymentMethod, string TransferReference, string? AttachmentUrl, decimal? Amount = null);
public record AttachInvoiceDto(string AttachmentUrl);
public record ReversalLineDto(Guid AllocationId, decimal Amount);
public record CreateReversalDto(IReadOnlyList<ReversalLineDto> Lines, string Reason, TeacherReversalDisposition Disposition, string IdempotencyKey);
public record SyncTeacherFinanceBunnyUsageDto(DateTime PeriodStart, DateTime PeriodEnd, Guid? TeacherId, Guid? PackageId, bool ForceRefresh = false);
public record SharedPackageAllocationPreviewRequestDto(IReadOnlyList<SharedPackageAllocationPreviewSelectionDto>? Selections = null);
public record SharedPackageAllocationPreviewSelectionDto(Guid? SubjectId, Guid? TeacherId);

public sealed record SettlementPreview(string? Error, List<TeacherFinancialAllocation> Allocations, List<TeacherPayoutAdjustment> Adjustments,
    decimal GrossDueAmount, decimal DebtDeductionAmount, decimal NetPayableAmount)
{
    public static SettlementPreview Invalid(string error) => new(error, [], [], 0m, 0m, 0m);
}
