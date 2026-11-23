using System.Text.Json;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Features.Admin.Gifts.Models;

namespace NaderGorge.Application.Features.LiveSupport.Interfaces;

public sealed record LiveSupportActionDefinitionDto(string Key, string Category, string LabelAr, string Danger, bool ReasonRequired, string ConfirmationVersion, IReadOnlyList<string> RefreshSections);
public sealed record LiveSupportActionResultDto(Guid ExecutionId, string ActionKey, bool Replayed, IReadOnlyList<string> RefreshSections, string Message);
public sealed record LiveSupportActionRequest(Guid ActorUserId, bool IsAdmin, Guid ConversationId, string ActionKey, string IdempotencyKey, string ConfirmationVersion, JsonElement Payload);

public interface ILiveSupportActionService : ILiveSupportActionExecutor
{
    Task<JsonElement> GetDraftAsync(Guid actorUserId, bool isAdmin, Guid conversationId, string actionKey, CancellationToken ct);
    Task<JsonElement> GetStudentActionContextAsync(Guid actorUserId, bool isAdmin, Guid conversationId, CancellationToken ct);
    Task<IReadOnlyList<LiveSupportPurchaseCourseDto>> GetPurchaseCoursesAsync(Guid actorUserId, bool isAdmin, Guid conversationId, CancellationToken ct);
    Task<LiveSupportPurchasePreviewDto> PreviewCoursePurchaseAsync(Guid actorUserId, bool isAdmin, Guid conversationId, Guid packageId, CancellationToken ct);
    Task<IReadOnlyList<GiftLookupDto>> GetGiftCoursesAsync(Guid actorUserId, bool isAdmin, Guid conversationId, CancellationToken ct);
}
