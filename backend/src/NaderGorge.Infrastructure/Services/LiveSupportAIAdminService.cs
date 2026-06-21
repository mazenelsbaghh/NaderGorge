using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.LiveSupportAI.Dtos;
using NaderGorge.Application.Features.LiveSupportAI.Interfaces;
using NaderGorge.Application.Features.LiveSupportAI.Services;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services;

public sealed class LiveSupportAIAdminService(IAppDbContext db) : ILiveSupportAIAdminService
{
    public LiveSupportAICatalogsDto GetCatalogs() => LiveSupportAICatalog.Snapshot();

    public async Task<LiveSupportAIConfigDto> GetConfigAsync(CancellationToken ct)
    {
        var draft = await db.LiveSupportAIPolicyVersions.AsNoTracking().Where(x => x.Status == LiveSupportAIPolicyStatus.Draft)
            .OrderByDescending(x => x.VersionNumber).FirstOrDefaultAsync(ct);
        var published = await db.LiveSupportAIPolicyVersions.AsNoTracking().Where(x => x.Status == LiveSupportAIPolicyStatus.Published)
            .OrderByDescending(x => x.VersionNumber).FirstOrDefaultAsync(ct);
        return new(ToDtoOrNull(draft), ToDtoOrNull(published), GetCatalogs());
    }

    public async Task<LiveSupportAIPolicyDto> SaveDraftAsync(Guid adminUserId, SaveLiveSupportAIDraftRequest request, CancellationToken ct)
    {
        Validate(request);
        var draft = await db.LiveSupportAIPolicyVersions.Where(x => x.Status == LiveSupportAIPolicyStatus.Draft)
            .OrderByDescending(x => x.VersionNumber).FirstOrDefaultAsync(ct);
        if (draft is not null && request.ExpectedVersion != draft.Version)
            throw new LiveSupportAIAdminException("VERSION_CONFLICT", "تم تعديل المسودة في جلسة أخرى.");

        if (draft is null)
        {
            var nextVersion = (await db.LiveSupportAIPolicyVersions.MaxAsync(x => (long?)x.VersionNumber, ct) ?? 0) + 1;
            draft = new LiveSupportAIPolicyVersion { VersionNumber = nextVersion, Status = LiveSupportAIPolicyStatus.Draft, CreatedByUserId = adminUserId };
            db.LiveSupportAIPolicyVersions.Add(draft);
        }

        Apply(draft, request);
        draft.Version++;
        await db.SaveChangesAsync(ct);
        return ToDto(draft);
    }

    public async Task<LiveSupportAIPolicyDto> PublishAsync(Guid adminUserId, long expectedVersion, CancellationToken ct)
    {
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var draft = await db.LiveSupportAIPolicyVersions.SingleOrDefaultAsync(x => x.Status == LiveSupportAIPolicyStatus.Draft, ct)
            ?? throw new LiveSupportAIAdminException("DRAFT_NOT_FOUND", "لا توجد مسودة للنشر.");
        if (draft.Version != expectedVersion)
            throw new LiveSupportAIAdminException("VERSION_CONFLICT", "المسودة قديمة، أعد تحميلها.");
        Validate(ToRequest(draft));

        var current = await db.LiveSupportAIPolicyVersions.SingleOrDefaultAsync(x => x.Status == LiveSupportAIPolicyStatus.Published, ct);
        if (current is not null)
        {
            current.Status = LiveSupportAIPolicyStatus.Superseded;
            current.IsEnabled = false;
            current.Version++;
        }

        draft.Status = LiveSupportAIPolicyStatus.Published;
        draft.IsEnabled = true;
        draft.PublishedByUserId = adminUserId;
        draft.PublishedAt = DateTime.UtcNow;
        draft.Version++;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ToDto(draft);
    }

    public async Task DisableAsync(Guid adminUserId, CancellationToken ct)
    {
        _ = adminUserId;
        var published = await db.LiveSupportAIPolicyVersions.SingleOrDefaultAsync(x => x.Status == LiveSupportAIPolicyStatus.Published && x.IsEnabled, ct);
        if (published is null) return;
        published.IsEnabled = false;
        published.Version++;
        await db.SaveChangesAsync(ct);
    }

    private static void Validate(SaveLiveSupportAIDraftRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SystemInstructions) || request.SystemInstructions.Length > 20_000)
            throw new LiveSupportAIAdminException("INVALID_INSTRUCTIONS", "تعليمات الـAI مطلوبة وبحد أقصى 20000 حرف.");
        ValidateKeys(request.ReadableDataKeys, LiveSupportAICatalog.ReadableData, "readable data");
        ValidateKeys(request.ActionKeys, LiveSupportAICatalog.Actions, "actions");
        ValidateKeys(request.LookupKeys, LiveSupportAICatalog.LookupKeys, "lookup");
        ValidateKeys(request.VerificationQuestionKeys, LiveSupportAICatalog.VerificationQuestions, "verification");
        if (request.VerificationRequiredCorrect < 1 || request.VerificationRequiredCorrect > request.VerificationQuestionKeys.Count)
            throw new LiveSupportAIAdminException("INVALID_VERIFICATION", "عدد إجابات التحقق المطلوبة غير صالح.");
        if (request.VerificationMaxAttempts is < 1 or > 10 || request.PendingActionExpirySeconds is < 30 or > 900 ||
            request.InactivityMinutes is < 5 or > 1440 || request.InactivityWarningGraceSeconds is < 30 or > 600)
            throw new LiveSupportAIAdminException("INVALID_LIMITS", "حدود المحاولات أو المدد غير صالحة.");
    }

    private static void ValidateKeys(IReadOnlyList<string> keys, IReadOnlyDictionary<string, LiveSupportAICatalogItemDto> catalog, string name)
    {
        if (keys.Count > 100 || keys.Distinct(StringComparer.Ordinal).Count() != keys.Count || keys.Any(key => !catalog.ContainsKey(key)))
            throw new LiveSupportAIAdminException("INVALID_CATALOG_KEY", $"توجد قيمة غير مسموحة في {name}.");
    }

    private static void Apply(LiveSupportAIPolicyVersion entity, SaveLiveSupportAIDraftRequest request)
    {
        entity.SystemInstructions = request.SystemInstructions.Trim();
        entity.ReadableDataKeysJson = JsonSerializer.Serialize(request.ReadableDataKeys);
        entity.ActionKeysJson = JsonSerializer.Serialize(request.ActionKeys);
        entity.LookupKeysJson = JsonSerializer.Serialize(request.LookupKeys);
        entity.VerificationQuestionKeysJson = JsonSerializer.Serialize(request.VerificationQuestionKeys);
        entity.VerificationRequiredCorrect = request.VerificationRequiredCorrect;
        entity.VerificationMaxAttempts = request.VerificationMaxAttempts;
        entity.PendingActionExpirySeconds = request.PendingActionExpirySeconds;
        entity.InactivityMinutes = request.InactivityMinutes;
        entity.InactivityWarningGraceSeconds = request.InactivityWarningGraceSeconds;
    }

    private static SaveLiveSupportAIDraftRequest ToRequest(LiveSupportAIPolicyVersion entity) => new(entity.SystemInstructions,
        Parse(entity.ReadableDataKeysJson), Parse(entity.ActionKeysJson), Parse(entity.LookupKeysJson), Parse(entity.VerificationQuestionKeysJson),
        entity.VerificationRequiredCorrect, entity.VerificationMaxAttempts, entity.PendingActionExpirySeconds, entity.InactivityMinutes,
        entity.InactivityWarningGraceSeconds, entity.Version);

    private static LiveSupportAIPolicyDto? ToDtoOrNull(LiveSupportAIPolicyVersion? entity) => entity is null ? null : ToDto(entity);
    private static LiveSupportAIPolicyDto ToDto(LiveSupportAIPolicyVersion entity) => new(entity.Id, entity.VersionNumber, entity.Status.ToString(), entity.IsEnabled,
        entity.SystemInstructions, Parse(entity.ReadableDataKeysJson), Parse(entity.ActionKeysJson), Parse(entity.LookupKeysJson), Parse(entity.VerificationQuestionKeysJson),
        entity.VerificationRequiredCorrect, entity.VerificationMaxAttempts, entity.PendingActionExpirySeconds, entity.InactivityMinutes,
        entity.InactivityWarningGraceSeconds, entity.Version, entity.PublishedAt);
    private static string[] Parse(string json) => JsonSerializer.Deserialize<string[]>(json) ?? [];
}
