using System.Text.Json;
using System.Text.Json.Serialization;
using NaderGorge.Domain.Entities.LiveSupport;

namespace NaderGorge.Application.Features.LiveSupport.Dtos;

public static class WhatsAppCampaignPermissions
{
    public const string Manage = "whatsapp_campaigns.manage";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WhatsAppCampaignAudienceFilterDto(
    IReadOnlyList<string>? ContactRoles = null,
    IReadOnlyList<string>? EducationStages = null,
    IReadOnlyList<string>? GradeLevels = null,
    IReadOnlyList<string>? StudyTracks = null,
    IReadOnlyList<Guid>? TeacherIds = null,
    IReadOnlyList<Guid>? SubjectIds = null,
    IReadOnlyList<Guid>? PackageIds = null,
    IReadOnlyList<Guid>? LessonIds = null,
    IReadOnlyList<Guid>? ExamIds = null,
    IReadOnlyList<Guid>? HomeworkIds = null,
    IReadOnlyList<string>? CrmStatuses = null,
    bool? HasActiveAccess = null,
    bool? HasPaidPurchase = null,
    bool? HasWatched = null,
    bool? HasExamAttempt = null,
    bool? HasHomeworkSubmission = null,
    DateTime? PurchaseFromUtc = null,
    DateTime? PurchaseToUtc = null,
    DateTime? WatchFromUtc = null,
    DateTime? WatchToUtc = null,
    DateTime? ExamFromUtc = null,
    DateTime? ExamToUtc = null,
    DateTime? HomeworkFromUtc = null,
    DateTime? HomeworkToUtc = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WhatsAppCampaignVariableMappingDto(
    string ComponentType,
    int Position,
    string Source,
    string? LiteralValue = null,
    Guid? ReferenceId = null,
    string? Format = null,
    int? ComponentIndex = null,
    int? ButtonIndex = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WhatsAppCampaignPreviewRequest(
    Guid TemplateId,
    WhatsAppCampaignAudienceFilterDto Filters,
    IReadOnlyList<WhatsAppCampaignVariableMappingDto> VariableMappings);

public sealed record WhatsAppCampaignMaskedRecipientDto(
    string MaskedName,
    string MaskedPhone,
    string ContactRole,
    string RenderedPreview);

public sealed record WhatsAppCampaignPreviewDto(
    int EligibleCount,
    int ExcludedCount,
    IReadOnlyDictionary<string, int> ExcludedByReason,
    string AudienceFingerprint,
    string TemplateFingerprint,
    DateTime ExpiresAt,
    IReadOnlyList<WhatsAppCampaignMaskedRecipientDto> Samples);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateWhatsAppCampaignDraftRequest(
    string Name,
    Guid TemplateId,
    string AudienceFingerprint,
    WhatsAppCampaignAudienceFilterDto Filters,
    IReadOnlyList<WhatsAppCampaignVariableMappingDto> VariableMappings);

public sealed record WhatsAppCampaignTemplateSnapshotDto(
    Guid Id,
    string Name,
    string Language,
    string Category,
    string Fingerprint,
    JsonElement Components);

public sealed record WhatsAppCampaignDraftDto(
    Guid CampaignId,
    long Version,
    string Status,
    int RecipientCount,
    int ExcludedCount,
    WhatsAppCampaignTemplateSnapshotDto TemplateSnapshot,
    string ReviewToken,
    string ConfirmationPhrase,
    DateTime ReviewExpiresAt);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LaunchWhatsAppCampaignRequest(
    long ExpectedVersion,
    string AudienceFingerprint,
    string ReviewToken,
    string ConfirmationPhrase,
    string IdempotencyKey);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChangeWhatsAppCampaignStateRequest(long ExpectedVersion, string? Reason = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RecordWhatsAppContactPreferenceRequest(
    Guid StudentUserId,
    string ContactRole,
    WhatsAppContactPreferenceCategory Category,
    WhatsAppContactPreferenceState State,
    string Source,
    string EvidenceReference,
    DateTime? EffectiveAt = null,
    Guid? ExpectedLatestPreferenceId = null,
    Guid? ExpectedLatestGlobalPreferenceId = null);

public sealed record WhatsAppContactPreferenceDto(
    Guid Id,
    Guid? StudentUserId,
    string? StudentName,
    string ContactRole,
    string MaskedDestination,
    WhatsAppContactPreferenceCategory Category,
    WhatsAppContactPreferenceState State,
    string Source,
    string EvidenceReference,
    DateTime EffectiveAt,
    DateTime RecordedAt,
    Guid? RecordedByUserId);

public sealed record WhatsAppContactPreferencePageDto(
    IReadOnlyList<WhatsAppContactPreferenceDto> Items,
    int Total,
    int Page,
    int PageSize);

public sealed record WhatsAppContactCategoryStateDto(
    string EffectiveState,
    Guid? LatestPreferenceId,
    DateTime? LatestEffectiveAt,
    bool OverriddenByGlobalOptOut,
    Guid? EffectivePreferenceId);

public sealed record WhatsAppContactCandidateDto(
    Guid StudentUserId,
    string StudentName,
    string ContactRole,
    string MaskedDestination,
    WhatsAppContactCategoryStateDto Marketing,
    WhatsAppContactCategoryStateDto Utility,
    WhatsAppContactCategoryStateDto Global);

public sealed record WhatsAppContactCandidatePageDto(
    IReadOnlyList<WhatsAppContactCandidateDto> Items,
    int Page,
    int PageSize,
    bool HasMore);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SearchWhatsAppContactCandidatesRequest(
    string Search,
    int Page = 1,
    int PageSize = 20);

public sealed record WhatsAppCampaignSummaryDto(
    Guid Id,
    string Name,
    string TemplateName,
    string TemplateLanguage,
    string TemplateCategory,
    string Status,
    int RecipientCount,
    int ExcludedCount,
    int PendingCount,
    int SentCount,
    int DeliveredCount,
    int ReadCount,
    int FailedCount,
    int SkippedCount,
    int UncertainCount,
    long Version,
    DateTime CreatedAt,
    DateTime? LaunchedAt,
    DateTime? CompletedAt,
    string? PauseReason);

public sealed record WhatsAppCampaignPageDto(
    IReadOnlyList<WhatsAppCampaignSummaryDto> Items,
    int Total,
    int Page,
    int PageSize);

public sealed record WhatsAppCampaignFacetItemDto(string Value, string Label, int Count);

public sealed record WhatsAppCampaignFacetsDto(
    IReadOnlyList<WhatsAppCampaignFacetItemDto> EducationStages,
    IReadOnlyList<WhatsAppCampaignFacetItemDto> GradeLevels,
    IReadOnlyList<WhatsAppCampaignFacetItemDto> StudyTracks,
    IReadOnlyList<WhatsAppCampaignFacetItemDto> CrmStatuses,
    IReadOnlyList<WhatsAppCampaignFacetItemDto> Teachers,
    IReadOnlyList<WhatsAppCampaignFacetItemDto> Subjects,
    IReadOnlyList<WhatsAppCampaignFacetItemDto> Packages,
    IReadOnlyList<WhatsAppCampaignFacetItemDto> Lessons,
    IReadOnlyList<WhatsAppCampaignFacetItemDto> Exams,
    IReadOnlyList<WhatsAppCampaignFacetItemDto> Homeworks);

public sealed record WhatsAppCampaignBootstrapDto(
    IReadOnlyList<LiveSupportWhatsAppTemplateDto> Templates,
    WhatsAppCampaignFacetsDto Facets,
    WhatsAppCampaignPageDto Campaigns);

public sealed record WhatsAppCampaignStateDto(Guid CampaignId, string Status, long Version);

public static class WhatsAppCampaignErrorCodes
{
    public const string InvalidRequest = "WHATSAPP_CAMPAIGN_INVALID_REQUEST";
    public const string TemplateInvalid = "WHATSAPP_CAMPAIGN_TEMPLATE_INVALID";
    public const string TemplateChanged = "WHATSAPP_CAMPAIGN_TEMPLATE_CHANGED";
    public const string AudienceChanged = "WHATSAPP_CAMPAIGN_AUDIENCE_CHANGED";
    public const string NotFound = "WHATSAPP_CAMPAIGN_NOT_FOUND";
    public const string Conflict = "WHATSAPP_CAMPAIGN_CONFLICT";
    public const string ConfirmationInvalid = "WHATSAPP_CAMPAIGN_CONFIRMATION_INVALID";
    public const string IdempotencyConflict = "WHATSAPP_CAMPAIGN_IDEMPOTENCY_CONFLICT";
    public const string ConsentRequired = "WHATSAPP_CAMPAIGN_CONSENT_REQUIRED";
}

public sealed class WhatsAppCampaignException(string code, string message, int statusCode = 400)
    : Exception(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
}
