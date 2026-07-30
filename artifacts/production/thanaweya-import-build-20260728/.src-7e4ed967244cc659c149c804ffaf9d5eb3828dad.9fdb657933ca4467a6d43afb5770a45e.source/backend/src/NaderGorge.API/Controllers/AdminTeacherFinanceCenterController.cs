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

    public AdminTeacherFinanceCenterController(IAppDbContext db, IMediator mediator)
    {
        _db = db;
        _mediator = mediator;
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
        var validation = await ValidateAgreement(teacherId, dto, null, ct);
        if (validation != null) return validation;

        var agreement = new TeacherFinancialAgreement
        {
            Id = Guid.NewGuid(), TeacherId = teacherId, ScopeType = dto.ScopeType, ScopeId = dto.ScopeId,
            Trigger = dto.Trigger, AllocationMode = dto.AllocationMode, AllocationValue = dto.AllocationValue,
            PriceBasis = dto.PriceBasis, EffectiveFrom = dto.EffectiveFrom, EffectiveTo = dto.EffectiveTo,
            Reason = dto.Reason.Trim(), CreatedByUserId = ActorId()
        };
        _db.TeacherFinancialAgreements.Add(agreement);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(ListAgreements), new { teacherId }, new { success = true, data = agreement.Id });
    }

    [HttpPut("agreements/{agreementId:guid}")]
    public async Task<IActionResult> ReplaceAgreement(Guid agreementId, [FromBody] UpsertTeacherAgreementDto dto, CancellationToken ct)
    {
        var current = await _db.TeacherFinancialAgreements.FirstOrDefaultAsync(x => x.Id == agreementId, ct);
        if (current == null) return NotFound(new { success = false, message = "الاتفاق غير موجود" });
        var validation = await ValidateAgreement(current.TeacherId, dto, agreementId, ct);
        if (validation != null) return validation;

        // Historic ledger rows reference the old terms; replacing creates a new effective version.
        current.IsActive = false;
        current.EffectiveTo = current.EffectiveTo is null || current.EffectiveTo > DateTime.UtcNow
            ? DateTime.UtcNow
            : current.EffectiveTo;
        current.UpdatedAt = DateTime.UtcNow;
        current.UpdatedByUserId = ActorId();
        _db.TeacherFinancialAgreements.Add(new TeacherFinancialAgreement
        {
            Id = Guid.NewGuid(), TeacherId = current.TeacherId, ScopeType = dto.ScopeType, ScopeId = dto.ScopeId,
            Trigger = dto.Trigger, AllocationMode = dto.AllocationMode, AllocationValue = dto.AllocationValue,
            PriceBasis = dto.PriceBasis, EffectiveFrom = dto.EffectiveFrom, EffectiveTo = dto.EffectiveTo,
            Reason = dto.Reason.Trim(), CreatedByUserId = ActorId()
        });
        await _db.SaveChangesAsync(ct);
        return Ok(new { success = true });
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
        if (dto.TeacherId == Guid.Empty || dto.PeriodTo < dto.PeriodFrom)
            return BadRequest(new { success = false, message = "بيانات فترة التسوية غير صالحة" });

        await using var transaction = await _db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var preview = await BuildSettlementPreview(dto, ct);
        if (preview.Error is not null)
            return BadRequest(new { success = false, message = preview.Error });
        if (preview.Allocations.Count == 0)
            return BadRequest(new { success = false, message = "لا توجد مستحقات مؤهلة لإنشاء تسوية" });

        var account = await _db.TeacherAccounts.FirstOrDefaultAsync(x => x.TeacherId == dto.TeacherId, ct);
        if (account == null || account.CurrentBalance - account.ReservedBalance < preview.GrossDueAmount)
            return Conflict(new { success = false, message = "رصيد المعلم المتاح تغير؛ أعد معاينة التسوية" });

        var settlement = new TeacherSettlement
        {
            Id = Guid.NewGuid(), TeacherId = dto.TeacherId, PeriodFrom = dto.PeriodFrom, PeriodTo = dto.PeriodTo,
            Currency = "EGP", Status = TeacherSettlementStatus.Draft, GrossDueAmount = preview.GrossDueAmount,
            DebtDeductionAmount = preview.DebtDeductionAmount, NetPayableAmount = preview.NetPayableAmount,
            Note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim(), CreatedByUserId = ActorId()
        };
        foreach (var allocation in preview.Allocations)
        {
            var line = new TeacherSettlementLine
            {
                Id = Guid.NewGuid(), TeacherSettlementId = settlement.Id, AllocationId = allocation.Id,
                Amount = allocation.TeacherShareAmount - allocation.ReversedAmount,
                DescriptionSnapshot = allocation.ContentNameSnapshot
            };
            settlement.Lines.Add(line);
            allocation.SettlementLineId = line.Id;
            allocation.PayoutStatus = TeacherFinancialPayoutStatus.Reserved;
        }
        foreach (var adjustment in preview.Adjustments)
        {
            settlement.Lines.Add(new TeacherSettlementLine
            {
                Id = Guid.NewGuid(), TeacherSettlementId = settlement.Id, AdjustmentId = adjustment.Id,
                Amount = adjustment.Amount, DescriptionSnapshot = $"خصم مديونية: {adjustment.Reason}"
            });
        }
        account.ReservedBalance += preview.GrossDueAmount;
        account.UpdatedAt = DateTime.UtcNow;
        _db.TeacherSettlements.Add(settlement);
        _db.FinancialInvoices.Add(new FinancialInvoice
        {
            Id = Guid.NewGuid(), Type = FinancialInvoiceType.TeacherSettlement, Status = FinancialInvoiceStatus.Draft,
            DocumentNumber = $"TS-{DateTime.UtcNow:yyyyMMdd}-{settlement.Id.ToString("N")[..8].ToUpperInvariant()}",
            Currency = settlement.Currency, Amount = settlement.NetPayableAmount, TeacherId = settlement.TeacherId,
            TeacherSettlementId = settlement.Id, Description = $"تسوية مستحقات مدرس للفترة {dto.PeriodFrom:yyyy-MM-dd} إلى {dto.PeriodTo:yyyy-MM-dd}",
            CreatedByUserId = ActorId()
        });
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return CreatedAtAction(nameof(GetSettlement), new { id = settlement.Id }, new { success = true, data = new { settlement.Id } });
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
        if (string.IsNullOrWhiteSpace(dto.PaymentMethod) || string.IsNullOrWhiteSpace(dto.TransferReference))
            return BadRequest(new { success = false, message = "طريقة الدفع والمرجع مطلوبان" });
        await using var transaction = await _db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var settlement = await _db.TeacherSettlements.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (settlement is null) return NotFound(new { success = false, message = "التسوية غير موجودة" });
        if (settlement.Status != TeacherSettlementStatus.Approved)
            return Conflict(new { success = false, message = "يجب اعتماد التسوية قبل تسجيل الدفع" });
        if (dto.Amount is not null && dto.Amount.Value != settlement.NetPayableAmount)
            return BadRequest(new { success = false, message = "يجب أن يطابق مبلغ الدفع صافي التسوية" });
        var account = await _db.TeacherAccounts.FirstOrDefaultAsync(x => x.TeacherId == settlement.TeacherId, ct);
        if (account is null || account.ReservedBalance < settlement.GrossDueAmount || account.CurrentBalance < settlement.GrossDueAmount)
            return Conflict(new { success = false, message = "رصيد التسوية المحجوز غير متاح" });
        var allocationIds = settlement.Lines.Where(x => x.AllocationId.HasValue).Select(x => x.AllocationId!.Value).ToList();
        var allocations = await _db.TeacherFinancialAllocations.Where(x => allocationIds.Contains(x.Id)).ToListAsync(ct);
        if (allocations.Count != allocationIds.Count || allocations.Any(x => x.PayoutStatus != TeacherFinancialPayoutStatus.Reserved || x.SettlementLineId == null))
            return Conflict(new { success = false, message = "تغيرت حالة بنود التسوية؛ لا يمكن الدفع" });
        foreach (var allocation in allocations) allocation.PayoutStatus = TeacherFinancialPayoutStatus.Paid;
        var adjustmentIds = settlement.Lines.Where(x => x.AdjustmentId.HasValue).Select(x => x.AdjustmentId!.Value).ToList();
        if (adjustmentIds.Count > 0)
        {
            var adjustments = await _db.TeacherPayoutAdjustments.Where(x => adjustmentIds.Contains(x.Id)).ToListAsync(ct);
            if (adjustments.Count != adjustmentIds.Count || adjustments.Any(x => x.Status != TeacherPayoutAdjustmentStatus.Open))
                return Conflict(new { success = false, message = "تغيرت حالة مديونية التسوية؛ لا يمكن الدفع" });
            foreach (var adjustment in adjustments) adjustment.Status = TeacherPayoutAdjustmentStatus.Applied;
        }
        account.CurrentBalance -= settlement.GrossDueAmount;
        account.ReservedBalance -= settlement.GrossDueAmount;
        account.UpdatedAt = DateTime.UtcNow;
        settlement.Status = TeacherSettlementStatus.Paid; settlement.PaidByUserId = ActorId(); settlement.PaidAt = DateTime.UtcNow;
        _db.TeacherSettlementPayments.Add(new TeacherSettlementPayment
        {
            Id = Guid.NewGuid(), TeacherSettlementId = settlement.Id, Amount = settlement.NetPayableAmount,
            PaymentMethod = dto.PaymentMethod.Trim(), TransferReference = dto.TransferReference.Trim(), AttachmentUrl = dto.AttachmentUrl,
            PaidByUserId = ActorId()
        });
        var invoice = await _db.FinancialInvoices.FirstOrDefaultAsync(x => x.TeacherSettlementId == settlement.Id, ct);
        if (invoice != null) { invoice.Status = FinancialInvoiceStatus.Paid; invoice.PaymentReference = dto.TransferReference.Trim(); invoice.AttachmentUrl = dto.AttachmentUrl; }
        await _db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return Ok(new { success = true });
    }

    [HttpPost("settlements/{id:guid}/cancel")]
    public async Task<IActionResult> CancelSettlement(Guid id, CancellationToken ct)
    {
        await using var transaction = await _db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var settlement = await _db.TeacherSettlements.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (settlement is null) return NotFound(new { success = false, message = "التسوية غير موجودة" });
        if (settlement.Status == TeacherSettlementStatus.Paid) return Conflict(new { success = false, message = "لا يمكن إلغاء تسوية مدفوعة" });
        if (settlement.Status == TeacherSettlementStatus.Cancelled) return Ok(new { success = true });
        var account = await _db.TeacherAccounts.FirstOrDefaultAsync(x => x.TeacherId == settlement.TeacherId, ct);
        if (account is null || account.ReservedBalance < settlement.GrossDueAmount) return Conflict(new { success = false, message = "الرصيد المحجوز غير متاح" });
        var allocationIds = settlement.Lines.Where(x => x.AllocationId.HasValue).Select(x => x.AllocationId!.Value).ToList();
        var allocations = await _db.TeacherFinancialAllocations.Where(x => allocationIds.Contains(x.Id)).ToListAsync(ct);
        if (allocations.Any(x => x.PayoutStatus != TeacherFinancialPayoutStatus.Reserved)) return Conflict(new { success = false, message = "بعض البنود لم تعد قابلة للتحرير" });
        foreach (var allocation in allocations) { allocation.PayoutStatus = TeacherFinancialPayoutStatus.Unpaid; allocation.SettlementLineId = null; }
        account.ReservedBalance -= settlement.GrossDueAmount; account.UpdatedAt = DateTime.UtcNow;
        settlement.Status = TeacherSettlementStatus.Cancelled;
        var invoice = await _db.FinancialInvoices.FirstOrDefaultAsync(x => x.TeacherSettlementId == settlement.Id, ct);
        if (invoice != null) invoice.Status = FinancialInvoiceStatus.Cancelled;
        await _db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return Ok(new { success = true });
    }

    [HttpPost("reversals")]
    public async Task<IActionResult> ReverseSelectedLines([FromBody] CreateReversalDto dto, CancellationToken ct)
    {
        if (dto.Lines is null || dto.Lines.Count == 0 || string.IsNullOrWhiteSpace(dto.Reason) || string.IsNullOrWhiteSpace(dto.IdempotencyKey) ||
            dto.Lines.Any(x => x.AllocationId == Guid.Empty || x.Amount <= 0m))
            return BadRequest(new { success = false, message = "بيانات المرتجع غير صالحة" });
        if (dto.Disposition == TeacherReversalDisposition.ReverseAvailableBalance)
            return BadRequest(new { success = false, message = "اختر مديونية المدرس أو خصم التسوية القادمة" });
        await using var transaction = await _db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var duplicate = await _db.TeacherFinancialEvents.FirstOrDefaultAsync(x => x.IdempotencyKey == dto.IdempotencyKey, ct);
        if (duplicate != null) return Ok(new { success = true, data = new { duplicate.Id, duplicate = true } });
        var ids = dto.Lines.Select(x => x.AllocationId).Distinct().ToList();
        if (ids.Count != dto.Lines.Count) return BadRequest(new { success = false, message = "لا يمكن تكرار بند في نفس المرتجع" });
        var allocations = await _db.TeacherFinancialAllocations.Include(x => x.TeacherFinancialEvent).Where(x => ids.Contains(x.Id)).ToListAsync(ct);
        if (allocations.Count != ids.Count || allocations.Any(x => x.TeacherShareAmount <= 0m || x.PayoutStatus is TeacherFinancialPayoutStatus.Reversed or TeacherFinancialPayoutStatus.Debt or TeacherFinancialPayoutStatus.Reserved))
            return Conflict(new { success = false, message = "أحد بنود المرتجع غير متاح" });
        if (allocations.Select(x => x.TeacherId).Distinct().Count() != 1)
            return BadRequest(new { success = false, message = "يجب أن تخص البنود مدرساً واحداً" });
        var amounts = dto.Lines.ToDictionary(x => x.AllocationId, x => x.Amount);
        if (allocations.Any(x => amounts[x.Id] > x.TeacherShareAmount - x.ReversedAmount))
            return Conflict(new { success = false, message = "قيمة المرتجع تتجاوز الرصيد القابل للعكس" });
        var teacherId = allocations[0].TeacherId;
        var eventId = Guid.NewGuid();
        var reversal = new TeacherFinancialEvent
        {
            Id = eventId, SourceType = TeacherFinancialSourceType.Refund, SourceId = eventId, TargetType = allocations[0].TeacherFinancialEvent.TargetType,
            TargetId = allocations[0].TeacherFinancialEvent.TargetId, GrossAmount = -amounts.Values.Sum(), PlatformShareAmount = 0m,
            IdempotencyKey = dto.IdempotencyKey.Trim(), DetailsJson = JsonSerializer.Serialize(new { dto.Reason, dto.Disposition, allocationIds = ids }),
            OccurredAt = DateTime.UtcNow, ReviewStatus = TeacherFinancialReviewStatus.Reversed, PayoutStatus = TeacherFinancialPayoutStatus.Reversed
        };
        var availableReversal = allocations.Where(x => x.PayoutStatus != TeacherFinancialPayoutStatus.Paid).Sum(x => amounts[x.Id]);
        var account = availableReversal == 0m ? null : await _db.TeacherAccounts.FirstOrDefaultAsync(x => x.TeacherId == teacherId, ct);
        if (availableReversal > 0m && (account is null || account.CurrentBalance - account.ReservedBalance < availableReversal))
            return Conflict(new { success = false, message = "الرصيد المتاح تغير؛ أعد العملية" });
        foreach (var allocation in allocations)
        {
            var amount = amounts[allocation.Id];
            allocation.ReversedAmount += amount;
            var wasPaid = allocation.PayoutStatus == TeacherFinancialPayoutStatus.Paid;
            if (!wasPaid)
            {
                account!.CurrentBalance -= amount; account.TotalEarnings = Math.Max(0m, account.TotalEarnings - amount); account.UpdatedAt = DateTime.UtcNow;
                if (allocation.ReversedAmount == allocation.TeacherShareAmount) allocation.PayoutStatus = TeacherFinancialPayoutStatus.Reversed;
            }
            else
            {
                _db.TeacherPayoutAdjustments.Add(new TeacherPayoutAdjustment { Id = Guid.NewGuid(), TeacherId = teacherId,
                    RelatedFinancialEventId = allocation.TeacherFinancialEventId, RelatedPayoutId = allocation.PayoutId,
                    Amount = -amount, Reason = $"[{dto.Disposition}] {dto.Reason.Trim()}", Status = TeacherPayoutAdjustmentStatus.Open });
                if (allocation.ReversedAmount == allocation.TeacherShareAmount) allocation.PayoutStatus = TeacherFinancialPayoutStatus.Debt;
            }
            reversal.Allocations.Add(new TeacherFinancialAllocation { Id = Guid.NewGuid(), TeacherId = teacherId, AllocationMode = TeacherAllocationMode.Reversal,
                AllocationValue = amount, GrossBasisAmount = -amount, TeacherShareAmount = -amount, PlatformShareAmount = 0m,
                StudentNameSnapshot = allocation.StudentNameSnapshot, StudentPhoneSnapshot = allocation.StudentPhoneSnapshot,
                ContentNameSnapshot = allocation.ContentNameSnapshot, ReviewStatus = TeacherFinancialReviewStatus.Reversed,
                PayoutStatus = wasPaid ? TeacherFinancialPayoutStatus.Debt : TeacherFinancialPayoutStatus.Reversed });
        }
        _db.TeacherFinancialEvents.Add(reversal);
        await _db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return Ok(new { success = true, data = new { reversal.Id } });
    }

    [HttpPost("invoices/{id:guid}/attachments")]
    public async Task<IActionResult> AttachInvoiceDocument(Guid id, [FromBody] AttachInvoiceDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.AttachmentUrl))
            return BadRequest(new { success = false, message = "رابط المرفق مطلوب" });
        var invoice = await _db.FinancialInvoices.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (invoice is null) return NotFound(new { success = false, message = "الفاتورة غير موجودة" });
        if (invoice.Status == FinancialInvoiceStatus.Cancelled)
            return Conflict(new { success = false, message = "لا يمكن تعديل فاتورة ملغاة" });
        invoice.AttachmentUrl = dto.AttachmentUrl.Trim();
        await _db.SaveChangesAsync(ct);
        return Ok(new { success = true });
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

    private async Task<IActionResult?> ValidateAgreement(Guid teacherId, UpsertTeacherAgreementDto dto, Guid? ignoredId, CancellationToken ct)
    {
        if (teacherId == Guid.Empty || !await _db.TeacherProfiles.AnyAsync(x => x.Id == teacherId, ct))
            return NotFound(new { success = false, message = "المدرس غير موجود" });
        if (string.IsNullOrWhiteSpace(dto.Reason) || dto.AllocationValue < 0m ||
            (dto.AllocationMode == TeacherAgreementAllocationMode.Percentage && dto.AllocationValue > 100m) ||
            (dto.EffectiveTo.HasValue && dto.EffectiveTo < dto.EffectiveFrom) ||
            (dto.ScopeType == TeacherAgreementScopeType.Default && dto.ScopeId != null))
            return BadRequest(new { success = false, message = "بيانات الاتفاق غير صالحة" });
        var overlaps = await _db.TeacherFinancialAgreements.AnyAsync(x => x.Id != ignoredId && x.TeacherId == teacherId && x.IsActive
            && x.ScopeType == dto.ScopeType && x.ScopeId == dto.ScopeId && x.Trigger == dto.Trigger
            && x.EffectiveFrom <= (dto.EffectiveTo ?? DateTime.MaxValue)
            && (x.EffectiveTo == null || x.EffectiveTo >= dto.EffectiveFrom), ct);
        return overlaps ? Conflict(new { success = false, message = "يوجد اتفاق نشط متداخل لنفس النطاق والتوقيت" }) : null;
    }

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
        await using var transaction = await _db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var settlement = await _db.TeacherSettlements.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (settlement is null) return NotFound(new { success = false, message = "التسوية غير موجودة" });
        if (settlement.Status != expected)
            return Conflict(new { success = false, message = "انتقال حالة التسوية غير صالح" });
        settlement.Status = next;
        if (next == TeacherSettlementStatus.Reviewed) { settlement.ReviewedByUserId = ActorId(); settlement.ReviewedAt = DateTime.UtcNow; }
        if (next == TeacherSettlementStatus.Approved) { settlement.ApprovedByUserId = ActorId(); settlement.ApprovedAt = DateTime.UtcNow; }
        var invoice = await _db.FinancialInvoices.FirstOrDefaultAsync(x => x.TeacherSettlementId == id, ct);
        if (invoice != null) invoice.Status = next == TeacherSettlementStatus.Reviewed ? FinancialInvoiceStatus.Reviewed : FinancialInvoiceStatus.Approved;
        await _db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return Ok(new { success = true });
    }
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
