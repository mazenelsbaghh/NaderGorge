using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Features.Admin.TeacherFinanceCenter;
using NaderGorge.Domain.Enums;

namespace NaderGorge.API.Controllers;

/// <summary>Audited code-batch finance controls. These routes are deliberately admin-only.</summary>
[ApiController]
[Route("api/admin/teacher-finance-center/code-groups")]
[Authorize(Roles = "Admin")]
public sealed class AdminTeacherCodeFinanceController : ControllerBase
{
    private readonly ISender _sender;

    private readonly IAppDbContext _db;
    public AdminTeacherCodeFinanceController(ISender sender, IAppDbContext db) => (_sender, _db) = (sender, db);

    [HttpPut("{codeGroupId:guid}/financial-terms")]
    public async Task<IActionResult> SetFinancialTerms(Guid codeGroupId, [FromBody] UpsertCodeGroupFinancialTermsDto dto, CancellationToken ct)
    {
        var response = await _sender.Send(new SetCodeGroupFinancialTermsCommand(User.RequireUserId(), codeGroupId,
            dto.Trigger, dto.AgreementId, dto.Recipient), ct);
        return ToActionResult(response);
    }

    [HttpPost("{codeGroupId:guid}/confirm-delivery")]
    public async Task<IActionResult> ConfirmDelivery(Guid codeGroupId, [FromBody] ConfirmCodeGroupDeliveryDto dto, CancellationToken ct)
    {
        var response = await _sender.Send(new ConfirmCodeGroupDeliveryCommand(User.RequireUserId(), codeGroupId,
            dto.Recipient, dto.AttachmentUrl, dto.DeliveredAt, dto.QuoteKey, dto.Payment), ct);
        return response.Status == TeacherFinanceCommandStatus.Success
            ? Ok(new { success = true, data = new { id = response.Id, confirmedAt = response.OccurredAt }, alreadyConfirmed = response.AlreadyApplied })
            : ToActionResult(response);
    }

    [HttpGet("{codeGroupId:guid}/account")]
    public async Task<IActionResult> GetAccount(Guid codeGroupId, CancellationToken ct)
    {
        var group = await _db.CodeGroups.AsNoTracking().SingleOrDefaultAsync(x => x.Id == codeGroupId, ct);
        if (group == null) return NotFound(new { success = false, message = "دفعة الأكواد غير موجودة" });
        var terms = await _db.CodeGroupFinancialTerms.AsNoTracking().SingleOrDefaultAsync(x => x.CodeGroupId == codeGroupId, ct)
            ?? new CodeGroupFinancialTerms { Trigger = CodeGroupFinancePolicy.Trigger(group, null) };
        var started = await CodeGroupAccountingGuard.HasStartedAsync(_db, group, ct);
        var delivery = await _db.CodeGroupDeliveryConfirmations.AsNoTracking().Include(x => x.Payments)
            .SingleOrDefaultAsync(x => x.CodeGroupId == codeGroupId, ct);
        var quote = !started && group.TeacherId.HasValue && group.CodeType != CodeType.Balance
            ? await CodeGroupFinanceQuote.CalculateAsync(_db, group, terms, DateTime.UtcNow, ct) : null;
        var paid = delivery?.Payments.Sum(x => x.Amount) ?? 0m;
        return Ok(new { success = true, data = new { group.Id, group.TeacherId, group.Name, group.TotalCodes,
            trigger = terms.Trigger, terms.Recipient, started, quote,
            delivery = delivery == null ? null : new { delivery.ConfirmedAt, delivery.Recipient, delivery.PlatformAmountDue,
                delivery.TeacherRetainedAmount, paid, remaining = delivery.PlatformAmountDue - paid,
                payments = delivery.Payments.OrderByDescending(x => x.ReceivedAt).Select(x => new { x.Id, x.Amount, x.Reference, x.ReceivedAt }) } } });
    }

    [HttpPost("{codeGroupId:guid}/payments")]
    public async Task<IActionResult> CollectPayment(Guid codeGroupId, [FromBody] CodeCollectionInput input, CancellationToken ct) =>
        ToActionResult(await _sender.Send(new CollectCodeGroupPaymentCommand(User.RequireUserId(), codeGroupId, input), ct));

    private IActionResult ToActionResult(TeacherFinanceCommandResult response) => response.Status switch
    {
        TeacherFinanceCommandStatus.Success => Ok(new { success = true }),
        TeacherFinanceCommandStatus.NotFound => NotFound(new { success = false, message = response.Message }),
        TeacherFinanceCommandStatus.Conflict => Conflict(new { success = false, message = response.Message }),
        _ => BadRequest(new { success = false, message = response.Message })
    };
}

public record UpsertCodeGroupFinancialTermsDto(TeacherAgreementTrigger Trigger, Guid? AgreementId, string? Recipient);
public record ConfirmCodeGroupDeliveryDto(string Recipient, string? AttachmentUrl, DateTime? DeliveredAt, string QuoteKey, CodeCollectionInput? Payment);
