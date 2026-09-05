using NaderGorge.Application.Features.LiveSupport.Dtos;

namespace NaderGorge.Application.Features.LiveSupport.Interfaces;

public interface IWhatsAppCampaignService
{
    Task<WhatsAppCampaignBootstrapDto> GetBootstrapAsync(int page, int pageSize, CancellationToken ct);
    Task<WhatsAppCampaignPageDto> ListAsync(int page, int pageSize, CancellationToken ct);
    Task<WhatsAppCampaignSpreadsheetInspectionDto> InspectSpreadsheetAsync(Stream stream, string fileName, CancellationToken ct);
    Task<WhatsAppCampaignMediaUploadDto> UploadHeaderImageAsync(Stream stream, string fileName, string contentType, long sizeBytes, CancellationToken ct);
    Task<WhatsAppCampaignPreviewDto> PreviewAsync(WhatsAppCampaignPreviewRequest request, CancellationToken ct);
    Task<WhatsAppCampaignDraftDto> CreateDraftAsync(Guid actorUserId, string idempotencyKey, CreateWhatsAppCampaignDraftRequest request, CancellationToken ct);
    Task<WhatsAppCampaignStateDto> LaunchAsync(Guid actorUserId, Guid campaignId, LaunchWhatsAppCampaignRequest request, CancellationToken ct);
    Task<WhatsAppCampaignStateDto> PauseAsync(Guid actorUserId, Guid campaignId, ChangeWhatsAppCampaignStateRequest request, CancellationToken ct);
    Task<WhatsAppCampaignStateDto> ResumeAsync(Guid actorUserId, Guid campaignId, ChangeWhatsAppCampaignStateRequest request, CancellationToken ct);
    Task<WhatsAppCampaignStateDto> CancelAsync(Guid actorUserId, Guid campaignId, ChangeWhatsAppCampaignStateRequest request, CancellationToken ct);
    Task<WhatsAppContactPreferencePageDto> ListPreferencesAsync(string? search, int page, int pageSize, CancellationToken ct);
    Task<WhatsAppContactCandidatePageDto> SearchContactCandidatesAsync(string search, int page, int pageSize, CancellationToken ct);
    Task<WhatsAppContactPreferenceDto> RecordPreferenceAsync(Guid actorUserId, string idempotencyKey, RecordWhatsAppContactPreferenceRequest request, CancellationToken ct);
    Task RecordInboundOptOutAsync(string whatsAppUserId, string metaMessageId, DateTime providerTimestamp, CancellationToken ct);
    Task<bool> ReconcilePendingReceiptAsync(string metaMessageId, CancellationToken ct);
    Task<bool> ProcessReceiptAsync(string metaMessageId, string? status, DateTime providerTimestamp, string? failureCode, CancellationToken ct);
}

public interface IWhatsAppCampaignDataProtector
{
    byte[] Protect(Guid recipientId, ReadOnlySpan<byte> plaintext);
    byte[] Unprotect(Guid recipientId, ReadOnlySpan<byte> ciphertext, string digest);
    string Digest(Guid recipientId, ReadOnlySpan<byte> ciphertext);
    string DestinationHash(string e164Phone);
    string SecretHash(string purpose, string value);
}
