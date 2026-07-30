using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.API.Controllers;

/// <summary>Audited code-batch finance controls. These routes are deliberately admin-only.</summary>
[ApiController]
[Route("api/admin/teacher-finance-center/code-groups")]
[Authorize(Roles = "Admin")]
public sealed class AdminTeacherCodeFinanceController : ControllerBase
{
    private readonly IAppDbContext _db;
    private readonly CodeGroupFinancialAccountingService _deliveryAccounting;

    public AdminTeacherCodeFinanceController(IAppDbContext db, CodeGroupFinancialAccountingService deliveryAccounting)
        => (_db, _deliveryAccounting) = (db, deliveryAccounting);

    [HttpPut("{codeGroupId:guid}/financial-terms")]
    public async Task<IActionResult> SetFinancialTerms(Guid codeGroupId, [FromBody] UpsertCodeGroupFinancialTermsDto dto, CancellationToken ct)
    {
        if (dto.Trigger is not (TeacherAgreementTrigger.CodeDelivery or TeacherAgreementTrigger.CodeActivation))
            return BadRequest(new { success = false, message = "توقيت الحساب غير صالح" });
        var group = await _db.CodeGroups.FirstOrDefaultAsync(x => x.Id == codeGroupId, ct);
        if (group == null) return NotFound(new { success = false, message = "دفعة الأكواد غير موجودة" });
        if (group.CodeType == CodeType.Balance) return BadRequest(new { success = false, message = "أكواد الرصيد لا تنشئ استحقاق مدرس" });
        if (group.AccountingRecordedAt != null)
            return Conflict(new { success = false, message = "لا يمكن تغيير شروط دفعة تم احتسابها بالفعل" });

        if (dto.AgreementId.HasValue)
        {
            var validAgreement = group.TeacherId.HasValue && await _db.TeacherFinancialAgreements.AnyAsync(x =>
                x.Id == dto.AgreementId.Value && x.TeacherId == group.TeacherId.Value && x.IsActive && x.Trigger == dto.Trigger, ct);
            if (!validAgreement) return BadRequest(new { success = false, message = "الاتفاق المحدد لا يخص مدرس الدفعة أو توقيتها" });
        }

        var terms = await _db.CodeGroupFinancialTerms.FirstOrDefaultAsync(x => x.CodeGroupId == codeGroupId, ct);
        if (terms == null)
        {
            terms = new CodeGroupFinancialTerms { Id = Guid.NewGuid(), CodeGroupId = codeGroupId };
            _db.CodeGroupFinancialTerms.Add(terms);
        }
        terms.Trigger = dto.Trigger;
        terms.AgreementId = dto.AgreementId;
        terms.Recipient = dto.Recipient?.Trim();
        terms.UpdatedByUserId = User.RequireUserId();
        terms.UpdatedAt = DateTime.UtcNow;
        // Preserve legacy consumers while preventing the legacy immediate generation path.
        group.AccountingTiming = dto.Trigger == TeacherAgreementTrigger.CodeDelivery
            ? CodeAccountingTiming.Immediate : CodeAccountingTiming.OnActivation;
        await _db.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    [HttpPost("{codeGroupId:guid}/confirm-delivery")]
    public async Task<IActionResult> ConfirmDelivery(Guid codeGroupId, [FromBody] ConfirmCodeGroupDeliveryDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Recipient))
            return BadRequest(new { success = false, message = "يجب إدخال مستلم دفعة الأكواد" });
        await using var transaction = await _db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var group = await _db.CodeGroups.FirstOrDefaultAsync(x => x.Id == codeGroupId, ct);
        if (group == null) return NotFound(new { success = false, message = "دفعة الأكواد غير موجودة" });
        if (group.CodeType == CodeType.Balance || !group.TeacherId.HasValue)
            return BadRequest(new { success = false, message = "هذه الدفعة لا تحتوي على استحقاق مدرس للتأكيد" });
        var terms = await _db.CodeGroupFinancialTerms.FirstOrDefaultAsync(x => x.CodeGroupId == codeGroupId, ct);
        if (terms?.Trigger != TeacherAgreementTrigger.CodeDelivery)
            return Conflict(new { success = false, message = "هذه الدفعة مضبوطة للحساب عند تفعيل كل كود" });

        var existing = await _db.CodeGroupDeliveryConfirmations.FirstOrDefaultAsync(x => x.CodeGroupId == codeGroupId, ct);
        if (existing != null)
        {
            await transaction.CommitAsync(ct);
            return Ok(new { success = true, data = new { existing.Id, existing.ConfirmedAt }, alreadyConfirmed = true });
        }

        var occurredAt = dto.DeliveredAt?.ToUniversalTime() ?? DateTime.UtcNow;
        var confirmation = new CodeGroupDeliveryConfirmation
        {
            Id = Guid.NewGuid(), CodeGroupId = codeGroupId, Recipient = dto.Recipient.Trim(),
            AttachmentUrl = string.IsNullOrWhiteSpace(dto.AttachmentUrl) ? null : dto.AttachmentUrl.Trim(),
            ConfirmedByUserId = User.RequireUserId(), ConfirmedAt = occurredAt,
            IdempotencyKey = $"code-delivery:{codeGroupId}"
        };
        _db.CodeGroupDeliveryConfirmations.Add(confirmation);
        await _deliveryAccounting.RecordDeliveryAsync(group, terms, occurredAt, ct);
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Ok(new { success = true, data = new { confirmation.Id, confirmation.ConfirmedAt }, alreadyConfirmed = false });
    }
}

public record UpsertCodeGroupFinancialTermsDto(TeacherAgreementTrigger Trigger, Guid? AgreementId, string? Recipient);
public record ConfirmCodeGroupDeliveryDto(string Recipient, string? AttachmentUrl, DateTime? DeliveredAt);
