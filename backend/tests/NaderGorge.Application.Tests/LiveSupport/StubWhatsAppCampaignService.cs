using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Features.LiveSupport.Interfaces;

namespace NaderGorge.Application.Tests.LiveSupport;

internal class StubWhatsAppCampaignService : IWhatsAppCampaignService
{
    public Task RecordInboundOptOutAsync(string whatsAppUserId, string metaMessageId,
        DateTime providerTimestamp, CancellationToken ct) => Task.CompletedTask;
    public Task<bool> ReconcilePendingReceiptAsync(string metaMessageId, CancellationToken ct) =>
        Task.FromResult(false);
    public Task<bool> ProcessReceiptAsync(string metaMessageId, string? status,
        DateTime providerTimestamp, string? failureCode, CancellationToken ct) => Task.FromResult(false);

    public Task<WhatsAppCampaignBootstrapDto> GetBootstrapAsync(int page, int pageSize, CancellationToken ct) =>
        throw new NotSupportedException();
    public Task<WhatsAppCampaignPageDto> ListAsync(int page, int pageSize, CancellationToken ct) =>
        throw new NotSupportedException();
    public Task<WhatsAppCampaignSpreadsheetInspectionDto> InspectSpreadsheetAsync(
        Stream stream, string fileName, CancellationToken ct) => throw new NotSupportedException();
    public Task<WhatsAppCampaignMediaUploadDto> UploadHeaderImageAsync(
        Stream stream, string fileName, string contentType, long sizeBytes, CancellationToken ct) =>
        throw new NotSupportedException();
    public Task<WhatsAppCampaignPreviewDto> PreviewAsync(WhatsAppCampaignPreviewRequest request, CancellationToken ct) =>
        throw new NotSupportedException();
    public Task<WhatsAppCampaignDraftDto> CreateDraftAsync(Guid actorUserId, string idempotencyKey,
        CreateWhatsAppCampaignDraftRequest request, CancellationToken ct) => throw new NotSupportedException();
    public Task<WhatsAppCampaignStateDto> LaunchAsync(Guid actorUserId, Guid campaignId,
        LaunchWhatsAppCampaignRequest request, CancellationToken ct) => throw new NotSupportedException();
    public Task<WhatsAppCampaignStateDto> PauseAsync(Guid actorUserId, Guid campaignId,
        ChangeWhatsAppCampaignStateRequest request, CancellationToken ct) => throw new NotSupportedException();
    public Task<WhatsAppCampaignStateDto> ResumeAsync(Guid actorUserId, Guid campaignId,
        ChangeWhatsAppCampaignStateRequest request, CancellationToken ct) => throw new NotSupportedException();
    public Task<WhatsAppCampaignStateDto> CancelAsync(Guid actorUserId, Guid campaignId,
        ChangeWhatsAppCampaignStateRequest request, CancellationToken ct) => throw new NotSupportedException();
    public Task<WhatsAppContactPreferencePageDto> ListPreferencesAsync(string? search, int page, int pageSize,
        CancellationToken ct) => throw new NotSupportedException();
    public Task<WhatsAppContactCandidatePageDto> SearchContactCandidatesAsync(string search, int page, int pageSize,
        CancellationToken ct) => throw new NotSupportedException();
    public Task<WhatsAppContactPreferenceDto> RecordPreferenceAsync(Guid actorUserId, string idempotencyKey,
        RecordWhatsAppContactPreferenceRequest request, CancellationToken ct) => throw new NotSupportedException();
}
