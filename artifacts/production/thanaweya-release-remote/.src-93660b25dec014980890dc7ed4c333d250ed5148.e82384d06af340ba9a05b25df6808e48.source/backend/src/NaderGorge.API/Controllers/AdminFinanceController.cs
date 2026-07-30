using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Features.Admin.Finance.Commands;
using NaderGorge.Application.Features.Admin.Finance.Queries;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/admin/finance")]
[Authorize]
[HasPermission("finance.manage")]
public class AdminFinanceController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IAppDbContext _db;
    private readonly TeacherAccountingService _teacherAccounting;

    public AdminFinanceController(IMediator mediator, IAppDbContext db, TeacherAccountingService teacherAccounting)
    {
        _mediator = mediator;
        _db = db;
        _teacherAccounting = teacherAccounting;
    }

    private Guid GetUserId() => User.RequireUserId();

    [HttpGet("payouts")]
    public async Task<IActionResult> GetPayouts([FromQuery] PayoutStatus? status = null)
    {
        var result = await _mediator.Send(new GetPayoutsQuery(status));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("payouts/{id:guid}/resolve")]
    public async Task<IActionResult> ResolvePayout([FromRoute] Guid id, [FromBody] ResolvePayoutDto dto)
    {
        var result = await _mediator.Send(new ResolvePayoutCommand(id, dto.Status, dto.RejectionReason, GetUserId()));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("code-accounting")]
    public async Task<IActionResult> GetCodeAccounting(
        [FromQuery] Guid? teacherId = null,
        [FromQuery] Guid? packageId = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetCodeAccountingQuery(teacherId, packageId, startDate, endDate, page, pageSize));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("teacher-events")]
    public async Task<IActionResult> GetTeacherEvents(
        [FromQuery] TeacherFinancialReviewStatus? status = null,
        [FromQuery] Guid? teacherId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.TeacherFinancialAllocations
            .Include(a => a.Teacher).ThenInclude(t => t.User)
            .Include(a => a.TeacherFinancialEvent).ThenInclude(e => e.Student)
            .AsNoTracking()
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(a => a.ReviewStatus == status.Value);
        }

        if (teacherId.HasValue)
        {
            query = query.Where(a => a.TeacherId == teacherId.Value);
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(a => a.TeacherFinancialEvent.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                allocationId = a.Id,
                eventId = a.TeacherFinancialEventId,
                teacherId = a.TeacherId,
                teacherName = a.Teacher.User.FullName,
                studentName = a.StudentNameSnapshot ?? a.TeacherFinancialEvent.Student!.FullName,
                studentPhone = a.StudentPhoneSnapshot ?? a.TeacherFinancialEvent.Student!.PhoneNumber,
                a.ContentNameSnapshot,
                a.CodeSerialNumber,
                sourceType = a.TeacherFinancialEvent.SourceType.ToString(),
                targetType = a.TeacherFinancialEvent.TargetType.ToString(),
                a.TeacherFinancialEvent.GrossAmount,
                a.TeacherFinancialEvent.DiscountAmount,
                a.TeacherFinancialEvent.PaidAmount,
                a.TeacherFinancialEvent.PromotionalAmount,
                a.TeacherShareAmount,
                a.PlatformShareAmount,
                reviewStatus = a.ReviewStatus.ToString(),
                payoutStatus = a.PayoutStatus.ToString(),
                a.TeacherFinancialEvent.OccurredAt
            })
            .ToListAsync(ct);

        return Ok(new { success = true, data = new { items, total, page, pageSize } });
    }

    [HttpPost("teacher-events/{allocationId:guid}/review")]
    public async Task<IActionResult> ReviewTeacherEvent(
        [FromRoute] Guid allocationId,
        [FromBody] ReviewTeacherEventDto dto,
        CancellationToken ct)
    {
        if (dto.Status is not (TeacherFinancialReviewStatus.Approved or TeacherFinancialReviewStatus.Rejected))
        {
            return BadRequest(new { success = false, message = "حالة المراجعة يجب أن تكون Approved أو Rejected" });
        }

        var allocation = await _db.TeacherFinancialAllocations
            .Include(a => a.TeacherFinancialEvent)
            .FirstOrDefaultAsync(a => a.Id == allocationId, ct);

        if (allocation == null)
        {
            return NotFound(new { success = false, message = "البند المالي غير موجود" });
        }

        if (allocation.ReviewStatus != TeacherFinancialReviewStatus.PendingReview)
        {
            return BadRequest(new { success = false, message = "يمكن مراجعة البنود المعلقة فقط" });
        }

        allocation.ReviewStatus = dto.Status;
        allocation.UpdatedAt = DateTime.UtcNow;
        allocation.PayoutStatus = dto.Status == TeacherFinancialReviewStatus.Rejected || allocation.TeacherShareAmount <= 0m
            ? TeacherFinancialPayoutStatus.NotEligible
            : TeacherFinancialPayoutStatus.Unpaid;

        var eventAllocations = await _db.TeacherFinancialAllocations
            .Where(a => a.TeacherFinancialEventId == allocation.TeacherFinancialEventId)
            .ToListAsync(ct);

        allocation.TeacherFinancialEvent.ReviewStatus = eventAllocations.Any(a => a.Id != allocation.Id && a.ReviewStatus == TeacherFinancialReviewStatus.PendingReview)
            ? TeacherFinancialReviewStatus.PendingReview
            : eventAllocations.Any(a => a.Id != allocation.Id && a.ReviewStatus == TeacherFinancialReviewStatus.Approved) || dto.Status == TeacherFinancialReviewStatus.Approved
                ? TeacherFinancialReviewStatus.Approved
                : TeacherFinancialReviewStatus.Rejected;
        allocation.TeacherFinancialEvent.UpdatedAt = DateTime.UtcNow;

        if (dto.Status == TeacherFinancialReviewStatus.Approved && allocation.TeacherShareAmount > 0m)
        {
            await CreditTeacherAccount(allocation.TeacherId, allocation.TeacherShareAmount, ct);
        }

        await _db.SaveChangesAsync(ct);
        return Ok(new { success = true, data = true });
    }

    [HttpPost("teacher-events/manual-compensation")]
    public async Task<IActionResult> CreateManualCompensation([FromBody] ManualTeacherCompensationDto dto, CancellationToken ct)
    {
        if (dto.TeacherId == Guid.Empty || dto.Amount <= 0m)
        {
            return BadRequest(new { success = false, message = "حدد المدرس وقيمة تعويض أكبر من صفر" });
        }

        var teacherExists = await _db.TeacherProfiles.AnyAsync(t => t.Id == dto.TeacherId, ct);
        if (!teacherExists)
        {
            return NotFound(new { success = false, message = "المدرس غير موجود" });
        }

        var sourceId = Guid.NewGuid();
        var reason = string.IsNullOrWhiteSpace(dto.Reason) ? "تعويض مالي يدوي" : dto.Reason.Trim();
        var evt = await _teacherAccounting.RecordEventAsync(new TeacherFinancialEventInput(
            TeacherFinancialSourceType.ManualCompensation,
            sourceId,
            null,
            SalesTargetType.Teacher,
            dto.TeacherId,
            dto.Amount,
            0m,
            dto.Amount,
            0m,
            0m,
            $"manual-compensation:{sourceId}",
            System.Text.Json.JsonSerializer.Serialize(new { reason }),
            DateTime.UtcNow,
            TeacherFinancialReviewStatus.Approved,
            new[]
            {
                new TeacherFinancialAllocationInput(
                    dto.TeacherId,
                    TeacherAllocationMode.ManualCompensation,
                    dto.Amount,
                    dto.Amount,
                    dto.Amount,
                    0m,
                    null,
                    null,
                    reason,
                    null,
                    TeacherFinancialReviewStatus.Approved)
            }), ct);

        return Ok(new { success = true, data = new { evt.Id } });
    }

    private async Task CreditTeacherAccount(Guid teacherId, decimal amount, CancellationToken ct)
    {
        var account = await _db.TeacherAccounts.FirstOrDefaultAsync(a => a.TeacherId == teacherId, ct);
        if (account == null)
        {
            var teacher = await _db.TeacherProfiles.FirstOrDefaultAsync(t => t.Id == teacherId, ct);
            account = new TeacherAccount
            {
                Id = Guid.NewGuid(),
                TeacherId = teacherId,
                CommissionRate = teacher?.CommissionRate ?? 0m,
                TotalEarnings = 0m,
                CurrentBalance = 0m,
                ReservedBalance = 0m
            };
            _db.TeacherAccounts.Add(account);
        }

        account.TotalEarnings += amount;
        account.CurrentBalance += amount;
        account.UpdatedAt = DateTime.UtcNow;
    }
}

public class GeneratePayrollDto
{
    public int Month { get; set; }
    public int Year { get; set; }
}

public class AddAdjustmentDto
{
    public PayrollAdjustmentType Type { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = null!;
}

public class ResolvePayoutDto
{
    public PayoutStatus Status { get; set; }
    public string? RejectionReason { get; set; }
}

public class ReviewTeacherEventDto
{
    public TeacherFinancialReviewStatus Status { get; set; }
    public string? Note { get; set; }
}

public class ManualTeacherCompensationDto
{
    public Guid TeacherId { get; set; }
    public decimal Amount { get; set; }
    public string? Reason { get; set; }
}
