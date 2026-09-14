using MediatR;
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

    public AdminTeacherCodeFinanceController(ISender sender) => _sender = sender;

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
            dto.Recipient, dto.AttachmentUrl, dto.DeliveredAt), ct);
        return response.Status == TeacherFinanceCommandStatus.Success
            ? Ok(new { success = true, data = new { id = response.Id, confirmedAt = response.OccurredAt }, alreadyConfirmed = response.AlreadyApplied })
            : ToActionResult(response);
    }

    private IActionResult ToActionResult(TeacherFinanceCommandResult response) => response.Status switch
    {
        TeacherFinanceCommandStatus.Success => Ok(new { success = true }),
        TeacherFinanceCommandStatus.NotFound => NotFound(new { success = false, message = response.Message }),
        TeacherFinanceCommandStatus.Conflict => Conflict(new { success = false, message = response.Message }),
        _ => BadRequest(new { success = false, message = response.Message })
    };
}

public record UpsertCodeGroupFinancialTermsDto(TeacherAgreementTrigger Trigger, Guid? AgreementId, string? Recipient);
public record ConfirmCodeGroupDeliveryDto(string Recipient, string? AttachmentUrl, DateTime? DeliveredAt);
