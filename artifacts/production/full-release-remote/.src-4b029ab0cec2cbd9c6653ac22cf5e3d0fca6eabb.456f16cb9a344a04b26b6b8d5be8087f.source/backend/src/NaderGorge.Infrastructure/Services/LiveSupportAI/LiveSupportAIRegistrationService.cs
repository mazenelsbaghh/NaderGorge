using System.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NaderGorge.Application.Features.Auth.Commands;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Application.Features.LiveSupportAI.Dtos;
using NaderGorge.Application.Features.LiveSupportAI.Interfaces;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.LiveSupportAI;

public sealed class LiveSupportAIRegistrationService(
    IAppDbContext db,
    IMediator mediator,
    IConfiguration configuration) : ILiveSupportAIRegistrationService
{
    public async Task<Guid> RegisterAndLinkAsync(LiveSupportParticipantIdentity participant, Guid conversationId, LiveSupportAISecureRegistrationDto request, CancellationToken cancellationToken)
    {
        if (!participant.GuestSessionId.HasValue || participant.StudentUserId.HasValue)
            throw new LiveSupportException(LiveSupportErrorCodes.Forbidden, "إنشاء الحساب متاح للزائر فقط.");
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var conversation = await db.LiveSupportConversations.SingleOrDefaultAsync(item => item.Id == conversationId && item.GuestSessionId == participant.GuestSessionId, cancellationToken)
            ?? throw new LiveSupportException(LiveSupportErrorCodes.Forbidden, "لا يمكنك الوصول لهذه المحادثة.");
        var decision = await db.LiveSupportAIPendingActions.SingleOrDefaultAsync(item => item.Id == request.DecisionId && item.ConversationId == conversationId, cancellationToken)
            ?? throw new LiveSupportException("NOT_FOUND", "اقتراح إنشاء الحساب غير موجود.");
        if (decision.Status == LiveSupportAIPendingActionStatus.Succeeded && conversation.LinkedStudentUserId.HasValue)
            return conversation.LinkedStudentUserId.Value;
        if (decision.DecisionKind != LiveSupportAIPendingDecisionKind.AccountCreation || decision.Status != LiveSupportAIPendingActionStatus.PendingConfirmation || decision.ExpiresAt <= DateTime.UtcNow)
            throw new LiveSupportException("REGISTRATION_NOT_CONFIRMABLE", "اقتراح إنشاء الحساب لم يعد متاحًا.");
        if (conversation.LinkedStudentUserId.HasValue)
            throw new LiveSupportException("STUDENT_ALREADY_LINKED", "المحادثة مرتبطة بحساب بالفعل.");
        var policy = await db.LiveSupportAIPolicyVersions.SingleOrDefaultAsync(item => item.Id == decision.PolicyVersionId && item.IsEnabled, cancellationToken);
        var allowed = policy is null ? [] : System.Text.Json.JsonSerializer.Deserialize<string[]>(policy.ActionKeysJson) ?? [];
        if (!allowed.Contains("student.create-and-link", StringComparer.Ordinal))
            throw new LiveSupportException("REGISTRATION_REVOKED", "إنشاء الحساب غير متاح حاليًا.");
        if (!Enum.TryParse<Gender>(request.Gender, true, out var gender) ||
            !Enum.TryParse<EducationStage>(request.EducationStage, true, out var stage) ||
            !Enum.TryParse<GradeLevel>(request.GradeLevel, true, out var grade))
            throw new LiveSupportException("VALIDATION_ERROR", "بيانات المرحلة أو النوع غير صحيحة.");

        var result = await mediator.Send(new RegisterCommand(
            request.FullName.Trim(), request.PhoneNumber.Trim(), null, request.Password,
            request.DateOfBirth, gender, null, request.Governorate.Trim(), null, request.Address.Trim(),
            request.ParentPhoneNumber.Trim(), null, null, true, true, null, null,
            request.SchoolName.Trim(), null, stage, grade, null, null), cancellationToken);
        if (!result.Success || result.Data is null)
            throw new LiveSupportException("VALIDATION_ERROR", result.Message ?? "تعذر إنشاء الحساب.");
        var systemActorId = await ResolveSystemActorAsync(cancellationToken);
        conversation.LinkedStudentUserId = result.Data.UserId;
        conversation.Version++;
        db.LiveSupportStudentLinkHistories.Add(new LiveSupportStudentLinkHistory
        {
            ConversationId = conversation.Id,
            NewStudentUserId = result.Data.UserId,
            ChangedByUserId = systemActorId,
            Reason = "AI guest registration",
            ChangedAt = DateTime.UtcNow
        });
        var state = await db.LiveSupportAIConversationStates.SingleAsync(item => item.ConversationId == conversation.Id, cancellationToken);
        state.VerifiedStudentUserId = result.Data.UserId;
        state.Version++;
        decision.Status = LiveSupportAIPendingActionStatus.Succeeded;
        decision.ConfirmedByGuestSessionId = participant.GuestSessionId;
        decision.ConfirmedAt = DateTime.UtcNow;
        decision.CompletedAt = DateTime.UtcNow;
        decision.Version++;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result.Data.UserId;
    }

    private async Task<Guid> ResolveSystemActorAsync(CancellationToken cancellationToken)
    {
        if (Guid.TryParse(configuration["LiveSupportAI:SystemActorUserId"], out var configured) && await db.Users.AnyAsync(item => item.Id == configured && item.IsActive, cancellationToken)) return configured;
        var id = await db.UserRoles.Where(item => item.Role.Type == RoleType.Admin && item.User.IsActive).Select(item => item.UserId).OrderBy(item => item).FirstOrDefaultAsync(cancellationToken);
        return id == Guid.Empty ? throw new InvalidOperationException("AI_SYSTEM_ACTOR_NOT_CONFIGURED") : id;
    }
}
