using System.Text.Json;
using NaderGorge.Application.Features.LiveSupport.Dtos;

namespace NaderGorge.Application.Features.LiveSupport.Interfaces;

public sealed record LiveSupportActionDefinitionDto(string Key, string Category, string LabelAr, string Danger, bool ReasonRequired, string ConfirmationVersion, IReadOnlyList<string> RefreshSections);
public sealed record LiveSupportActionResultDto(Guid ExecutionId, string ActionKey, bool Replayed, IReadOnlyList<string> RefreshSections, string Message);
public sealed record LiveSupportActionRequest(Guid ActorUserId, bool IsAdmin, Guid ConversationId, string ActionKey, string IdempotencyKey, string ConfirmationVersion, JsonElement Payload);

public interface ILiveSupportActionService : ILiveSupportActionExecutor
{
}
