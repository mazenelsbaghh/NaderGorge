using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services;

public sealed partial class WhatsAppCampaignService : IWhatsAppCampaignService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IAppDbContext _db;
    private readonly IWhatsAppCampaignDataProtector _protector;
    private readonly IConfiguration _configuration;

    public WhatsAppCampaignService(
        IAppDbContext db,
        IWhatsAppCampaignDataProtector protector,
        IConfiguration configuration)
    {
        _db = db;
        _protector = protector;
        _configuration = configuration;
    }

    public async Task<WhatsAppCampaignBootstrapDto> GetBootstrapAsync(
        int page,
        int pageSize,
        CancellationToken ct)
    {
        var templates = await _db.LiveSupportWhatsAppTemplates.AsNoTracking()
            .Where(template => template.Status == "APPROVED")
            .OrderBy(template => template.Name).ThenBy(template => template.Language)
            .ToListAsync(ct);
        var templateDtos = new List<LiveSupportWhatsAppTemplateDto>(templates.Count);
        foreach (var template in templates)
        {
            try
            {
                WhatsAppCampaignTemplatePolicy.RequireCampaignTemplate(template);
            }
            catch (WhatsAppCampaignException)
            {
                continue;
            }
            using var document = JsonDocument.Parse(template.ComponentsJson);
            templateDtos.Add(new LiveSupportWhatsAppTemplateDto(
                template.Id, template.Name, template.Language, template.Category, template.Status,
                document.RootElement.Clone(), template.LastSyncedAt, template.Version, template.Fingerprint));
        }

        var facets = await BuildFacetsAsync(ct);
        var campaigns = await ListAsync(page, pageSize, ct);
        return new WhatsAppCampaignBootstrapDto(templateDtos, facets, campaigns);
    }

    public async Task<WhatsAppCampaignPageDto> ListAsync(int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = _db.WhatsAppCampaigns.AsNoTracking();
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(campaign => campaign.CreatedAt)
            .ThenByDescending(campaign => campaign.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);
        return new WhatsAppCampaignPageDto(rows.Select(ToSummary).ToArray(), total, page, pageSize);
    }

    public async Task<WhatsAppCampaignStateDto> LaunchAsync(
        Guid actorUserId,
        Guid campaignId,
        LaunchWhatsAppCampaignRequest request,
        CancellationToken ct)
    {
        if (request is null) throw Invalid("بيانات إطلاق الحملة مطلوبة.");
        ValidateIdempotencyKey(request.IdempotencyKey);
        await using var transaction = await _db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var campaign = await _db.WhatsAppCampaigns.SingleOrDefaultAsync(item => item.Id == campaignId, ct)
            ?? throw NotFound();
        var requestHash = HashJson(new
        {
            campaignId,
            request.ExpectedVersion,
            request.AudienceFingerprint,
            request.ReviewToken,
            request.ConfirmationPhrase
        });
        if (string.Equals(campaign.LaunchIdempotencyKey, request.IdempotencyKey, StringComparison.Ordinal))
        {
            if (!string.Equals(campaign.LaunchRequestHash, requestHash, StringComparison.Ordinal))
                throw Conflict(WhatsAppCampaignErrorCodes.IdempotencyConflict,
                    "مفتاح تكرار الإطلاق مستخدم بطلب مختلف.");
            await transaction.CommitAsync(ct);
            return ToState(campaign);
        }
        if (campaign.Status != WhatsAppCampaignStatus.Locked || campaign.Version != request.ExpectedVersion)
            throw Conflict(WhatsAppCampaignErrorCodes.Conflict, "تغيرت الحملة؛ راجعها مرة أخرى قبل الإرسال.");
        if (!string.Equals(campaign.AudienceFingerprint, request.AudienceFingerprint, StringComparison.Ordinal))
            throw Conflict(WhatsAppCampaignErrorCodes.AudienceChanged, "تغيرت بصمة الجمهور؛ أنشئ مراجعة جديدة.");
        if (campaign.ReviewTokenExpiresAt <= DateTime.UtcNow ||
            !FixedHashEquals(campaign.ReviewTokenHash,
                _protector.SecretHash($"review:{campaign.Id:N}", request.ReviewToken)) ||
            !FixedHashEquals(campaign.ConfirmationPhraseHash,
                _protector.SecretHash($"confirmation:{campaign.Id:N}", NormalizePhrase(request.ConfirmationPhrase))))
            throw Conflict(WhatsAppCampaignErrorCodes.ConfirmationInvalid,
                "انتهت أو لم تطابق بيانات تأكيد الحملة.");

        var template = await _db.LiveSupportWhatsAppTemplates.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == campaign.TemplateId, ct)
            ?? throw Conflict(WhatsAppCampaignErrorCodes.TemplateChanged, "القالب لم يعد متاحًا.");
        WhatsAppCampaignTemplatePolicy.RequireCampaignTemplate(template);
        if (!string.Equals(template.Fingerprint, campaign.TemplateFingerprint, StringComparison.Ordinal))
            throw Conflict(WhatsAppCampaignErrorCodes.TemplateChanged,
                "تغير القالب بعد المراجعة؛ أعد إنشاء الحملة.");

        var filters = JsonSerializer.Deserialize<WhatsAppCampaignAudienceFilterDto>(campaign.AudienceFilterJson, JsonOptions)
            ?? throw Conflict(WhatsAppCampaignErrorCodes.Conflict, "تعذر استعادة شروط الحملة.");
        var mappings = JsonSerializer.Deserialize<List<WhatsAppCampaignVariableMappingDto>>(campaign.VariableMappingsJson, JsonOptions)
            ?? throw Conflict(WhatsAppCampaignErrorCodes.Conflict, "تعذر استعادة متغيرات الحملة.");
        var rebuilt = await BuildAudienceAsync(template, filters, mappings, ct);
        if (!string.Equals(rebuilt.Fingerprint, campaign.AudienceFingerprint, StringComparison.Ordinal))
            throw Conflict(WhatsAppCampaignErrorCodes.AudienceChanged,
                "تغير الجمهور أو بيانات الرسالة بعد المراجعة؛ أنشئ حملة جديدة.");

        var now = DateTime.UtcNow;
        campaign.Status = WhatsAppCampaignStatus.Running;
        campaign.LaunchedAt = now;
        campaign.LastChangedByUserId = actorUserId;
        campaign.LaunchIdempotencyKey = request.IdempotencyKey;
        campaign.LaunchRequestHash = requestHash;
        campaign.UpdatedAt = now;
        campaign.Version++;
        AppendAudit(campaign.Id, actorUserId, "campaign_launched", new
        {
            campaign.RecipientCount,
            campaign.TemplateName,
            campaign.TemplateLanguage
        });
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ToState(campaign);
    }

    public Task<WhatsAppCampaignStateDto> PauseAsync(
        Guid actorUserId, Guid campaignId, ChangeWhatsAppCampaignStateRequest request, CancellationToken ct) =>
        ChangeStateAsync(actorUserId, campaignId, request, WhatsAppCampaignStatus.Running,
            WhatsAppCampaignStatus.Paused, "campaign_paused", ct);

    public async Task<WhatsAppCampaignStateDto> ResumeAsync(
        Guid actorUserId, Guid campaignId, ChangeWhatsAppCampaignStateRequest request, CancellationToken ct)
    {
        var campaign = await _db.WhatsAppCampaigns.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == campaignId, ct) ?? throw NotFound();
        var template = await _db.LiveSupportWhatsAppTemplates.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == campaign.TemplateId, ct);
        if (template is null || !string.Equals(template.Fingerprint, campaign.TemplateFingerprint, StringComparison.Ordinal) ||
            !string.Equals(template.Status, "APPROVED", StringComparison.OrdinalIgnoreCase))
            throw Conflict(WhatsAppCampaignErrorCodes.TemplateChanged,
                "لا يمكن استكمال الحملة لأن القالب تغير أو لم يعد معتمدًا.");
        return await ChangeStateAsync(actorUserId, campaignId, request,
            WhatsAppCampaignStatus.Paused, WhatsAppCampaignStatus.Running, "campaign_resumed", ct);
    }

    public async Task<WhatsAppCampaignStateDto> CancelAsync(
        Guid actorUserId,
        Guid campaignId,
        ChangeWhatsAppCampaignStateRequest request,
        CancellationToken ct)
    {
        await using var transaction = await _db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var campaign = await _db.WhatsAppCampaigns.SingleOrDefaultAsync(item => item.Id == campaignId, ct)
            ?? throw NotFound();
        if (campaign.Version != request.ExpectedVersion ||
            campaign.Status is WhatsAppCampaignStatus.Completed or WhatsAppCampaignStatus.Cancelled or WhatsAppCampaignStatus.Failed)
            throw Conflict(WhatsAppCampaignErrorCodes.Conflict, "لا يمكن إلغاء الحملة في حالتها الحالية.");
        var now = DateTime.UtcNow;
        await _db.WhatsAppCampaignRecipients
            .Where(item => item.CampaignId == campaignId && item.Status == WhatsAppCampaignRecipientStatus.Pending)
            .ExecuteUpdateAsync(update => update
                .SetProperty(item => item.Status, WhatsAppCampaignRecipientStatus.Skipped)
                .SetProperty(item => item.FailureCode, "WHATSAPP_CAMPAIGN_CANCELLED")
                .SetProperty(item => item.UpdatedAt, now)
                .SetProperty(item => item.Version, item => item.Version + 1), ct);
        campaign.Status = WhatsAppCampaignStatus.Cancelled;
        campaign.CancelledAt = now;
        campaign.LastChangedByUserId = actorUserId;
        campaign.PauseReason = BoundedReason(request.Reason);
        campaign.UpdatedAt = now;
        campaign.Version++;
        await RefreshCountersAsync(campaign, ct);
        AppendAudit(campaign.Id, actorUserId, "campaign_cancelled", new { reason = campaign.PauseReason });
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ToState(campaign);
    }

    public async Task<WhatsAppContactPreferencePageDto> ListPreferencesAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        // This endpoint is the append-only evidence audit. Effective authority for a
        // destination is returned by the candidate endpoint and evaluated at send time.
        var query = _db.WhatsAppContactPreferences.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(item =>
                item.DestinationLast4.Contains(term) ||
                item.StudentUserId != null && _db.Users.Any(user =>
                    user.Id == item.StudentUserId && EF.Functions.ILike(user.FullName, $"%{term}%")));
        }
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(item => item.EffectiveAt)
            .ThenByDescending(item => item.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(item => new
            {
                Preference = item,
                StudentName = item.StudentUserId == null
                    ? null
                    : _db.Users.Where(user => user.Id == item.StudentUserId)
                        .Select(user => user.FullName).FirstOrDefault()
            }).ToListAsync(ct);
        return new WhatsAppContactPreferencePageDto(rows.Select(row => ToPreferenceDto(
            row.Preference, row.StudentName)).ToArray(), total, page, pageSize);
    }

    public async Task<WhatsAppContactPreferenceDto> RecordPreferenceAsync(
        Guid actorUserId,
        string idempotencyKey,
        RecordWhatsAppContactPreferenceRequest request,
        CancellationToken ct)
    {
        if (request is null) throw Invalid("بيانات تفضيل الاتصال مطلوبة.");
        ValidateIdempotencyKey(idempotencyKey);
        ValidateAdminPreference(request);
        await using var transaction = await _db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var replay = await _db.WhatsAppContactPreferences.AsNoTracking()
            .SingleOrDefaultAsync(item => item.RecordedByUserId == actorUserId &&
                item.IdempotencyKey == idempotencyKey, ct);
        if (replay is not null)
        {
            var replayHash = HashJson(new { request, destinationHash = replay.DestinationHash });
            if (!string.Equals(replay.RequestHash, replayHash, StringComparison.Ordinal))
                throw Conflict(WhatsAppCampaignErrorCodes.IdempotencyConflict,
                    "مفتاح التكرار مستخدم لتفضيل مختلف.");
            var replayName = replay.StudentUserId.HasValue
                ? await _db.Users.AsNoTracking().Where(user => user.Id == replay.StudentUserId.Value)
                    .Select(user => user.FullName).SingleOrDefaultAsync(ct)
                : null;
            await transaction.CommitAsync(ct);
            return ToPreferenceDto(replay, replayName);
        }

        var contact = await ResolveAdminContactAsync(request.StudentUserId, request.ContactRole, ct);
        var e164 = NormalizeE164(contact.Phone);
        if (e164 is null)
            throw Invalid("رقم جهة الاتصال غير صالح لإرسال واتساب.");
        var destinationHash = _protector.DestinationHash(e164);
        var requestHash = HashJson(new { request, destinationHash });

        var latest = await LatestPreferenceAsync(destinationHash, request.Category, ct);
        if (latest?.Id != request.ExpectedLatestPreferenceId ||
            latest is null && request.ExpectedLatestPreferenceId.HasValue)
            throw Conflict(WhatsAppCampaignErrorCodes.Conflict,
                "تغير تفضيل الاتصال؛ حدّث القائمة ثم أعد المحاولة.");
        var effectiveAt = request.EffectiveAt?.ToUniversalTime() ?? DateTime.UtcNow;
        if (effectiveAt > DateTime.UtcNow)
            throw Invalid("وقت التفضيل لا يمكن أن يكون في المستقبل.");
        if (latest is not null && effectiveAt < latest.EffectiveAt)
            throw Invalid("لا يمكن إضافة تفضيل إداري بتاريخ أقدم من آخر حالة مسجلة.");
        if (request.Category != WhatsAppContactPreferenceCategory.All &&
            request.State == WhatsAppContactPreferenceState.OptedIn)
        {
            var latestGlobal = await LatestPreferenceAsync(
                destinationHash, WhatsAppContactPreferenceCategory.All, ct);
            if (latestGlobal?.Id != request.ExpectedLatestGlobalPreferenceId ||
                latestGlobal is null && request.ExpectedLatestGlobalPreferenceId.HasValue)
                throw Conflict(WhatsAppCampaignErrorCodes.Conflict,
                    "تغير إلغاء الاشتراك العام؛ حدّث جهة الاتصال قبل تسجيل الموافقة.");
        }
        var preference = new WhatsAppContactPreference
        {
            StudentUserId = request.StudentUserId,
            ContactRole = NormalizeContactRole(request.ContactRole),
            DestinationHash = destinationHash,
            DestinationLast4 = e164[^4..],
            Category = request.Category,
            State = request.State,
            Source = request.Source.Trim(),
            EvidenceReference = request.EvidenceReference.Trim(),
            EffectiveAt = effectiveAt,
            RecordedByUserId = actorUserId,
            SupersedesPreferenceId = latest?.Id,
            IdempotencyKey = idempotencyKey,
            RequestHash = requestHash
        };
        _db.WhatsAppContactPreferences.Add(preference);
        AppendAudit(null, actorUserId, "contact_preference_recorded", new
        {
            preference.Category,
            preference.State,
            preference.ContactRole,
            destination = $"***{preference.DestinationLast4}"
        });
        try
        {
            await _db.SaveChangesAsync(ct);
            if (request.State == WhatsAppContactPreferenceState.OptedOut)
            {
                await SuppressPendingDestinationAsync(destinationHash, "WHATSAPP_CONTACT_OPTED_OUT", ct);
                await _db.SaveChangesAsync(ct);
            }
            await transaction.CommitAsync(ct);
            return ToPreferenceDto(preference, contact.StudentName);
        }
        catch (Exception exception) when (IsPreferenceConcurrencyFailure(exception))
        {
            await transaction.RollbackAsync(CancellationToken.None);
            _db.ClearTrackedChanges();
            var concurrentReplay = await _db.WhatsAppContactPreferences.AsNoTracking()
                .SingleOrDefaultAsync(item => item.RecordedByUserId == actorUserId &&
                    item.IdempotencyKey == idempotencyKey, CancellationToken.None);
            if (concurrentReplay is not null)
            {
                if (!string.Equals(concurrentReplay.RequestHash, requestHash, StringComparison.Ordinal))
                    throw Conflict(WhatsAppCampaignErrorCodes.IdempotencyConflict,
                        "مفتاح التكرار مستخدم لتفضيل مختلف.");
                return ToPreferenceDto(concurrentReplay, contact.StudentName);
            }
            throw Conflict(WhatsAppCampaignErrorCodes.Conflict,
                "تغير تفضيل الاتصال بالتزامن؛ حدّث القائمة ثم أعد المحاولة.");
        }
    }

    public async Task RecordInboundOptOutAsync(
        string whatsAppUserId,
        string metaMessageId,
        DateTime providerTimestamp,
        CancellationToken ct)
    {
        var e164 = NormalizeE164(whatsAppUserId);
        if (e164 is null || string.IsNullOrWhiteSpace(metaMessageId)) return;
        var destinationHash = _protector.DestinationHash(e164);
        var sourceMessageId = metaMessageId.Length <= 200 ? metaMessageId : HashText(metaMessageId);
        await using var transaction = await _db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (await _db.WhatsAppContactPreferences.AnyAsync(item => item.SourceMessageId == sourceMessageId, ct))
        {
            await transaction.CommitAsync(ct);
            return;
        }
        var receivedAt = DateTime.UtcNow;
        var effectiveAt = providerTimestamp.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(providerTimestamp, DateTimeKind.Utc)
            : providerTimestamp.ToUniversalTime();
        if (effectiveAt > receivedAt) effectiveAt = receivedAt;
        var latest = await LatestPreferenceAsync(destinationHash, WhatsAppContactPreferenceCategory.All, ct);
        var preference = new WhatsAppContactPreference
        {
            ContactRole = "External",
            DestinationHash = destinationHash,
            DestinationLast4 = e164[^4..],
            Category = WhatsAppContactPreferenceCategory.All,
            State = WhatsAppContactPreferenceState.OptedOut,
            Source = "whatsapp_keyword",
            EvidenceReference = "Inbound opt-out keyword",
            EffectiveAt = effectiveAt,
            // Delayed provider evidence must be retained without branching the
            // authoritative successor chain or overriding later explicit consent.
            SupersedesPreferenceId = latest is null || effectiveAt >= latest.EffectiveAt ? latest?.Id : null,
            IdempotencyKey = metaMessageId.Length <= 100 ? metaMessageId : HashText(metaMessageId),
            RequestHash = HashText($"inbound-optout:{destinationHash}:{providerTimestamp:O}"),
            SourceMessageId = sourceMessageId
        };
        _db.WhatsAppContactPreferences.Add(preference);
        try
        {
            await _db.SaveChangesAsync(ct);
            await SuppressPendingDestinationAsync(destinationHash, "WHATSAPP_CONTACT_OPTED_OUT", ct);
            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "IX_whatsapp_contact_preferences_SourceMessageId"
            })
        {
            await transaction.RollbackAsync(CancellationToken.None);
            // A failed insert remains Added in EF's tracker after rollback. Detach it so
            // the enclosing webhook ingestion cannot accidentally retry it on its save.
            _db.Entry(preference).State = EntityState.Detached;
        }
    }

    private async Task<WhatsAppCampaignStateDto> ChangeStateAsync(
        Guid actorUserId,
        Guid campaignId,
        ChangeWhatsAppCampaignStateRequest request,
        WhatsAppCampaignStatus expectedStatus,
        WhatsAppCampaignStatus targetStatus,
        string auditAction,
        CancellationToken ct)
    {
        var campaign = await _db.WhatsAppCampaigns.SingleOrDefaultAsync(item => item.Id == campaignId, ct)
            ?? throw NotFound();
        if (campaign.Status != expectedStatus || campaign.Version != request.ExpectedVersion)
            throw Conflict(WhatsAppCampaignErrorCodes.Conflict, "تغيرت حالة الحملة؛ حدّث الصفحة وأعد المحاولة.");
        var now = DateTime.UtcNow;
        campaign.Status = targetStatus;
        campaign.LastChangedByUserId = actorUserId;
        campaign.UpdatedAt = now;
        campaign.Version++;
        if (targetStatus == WhatsAppCampaignStatus.Paused)
        {
            campaign.PausedAt = now;
            campaign.PauseReason = BoundedReason(request.Reason);
        }
        else
        {
            campaign.PausedAt = null;
            campaign.PauseReason = null;
        }
        AppendAudit(campaign.Id, actorUserId, auditAction, new { reason = BoundedReason(request.Reason) });
        await _db.SaveChangesAsync(ct);
        return ToState(campaign);
    }

    private async Task SuppressPendingDestinationAsync(
        string destinationHash,
        string failureCode,
        CancellationToken ct)
    {
        var candidateCampaigns = await _db.WhatsAppCampaignRecipients.AsNoTracking()
            .Where(item => item.DestinationHash == destinationHash &&
                item.Status == WhatsAppCampaignRecipientStatus.Pending)
            .Select(item => new
            {
                item.CampaignId,
                Category = _db.WhatsAppCampaigns.Where(campaign => campaign.Id == item.CampaignId)
                    .Select(campaign => campaign.TemplateCategory).Single()
            }).Distinct().ToListAsync(ct);
        var preferenceRows = await _db.WhatsAppContactPreferences.AsNoTracking()
            .Where(item => item.DestinationHash == destinationHash && item.EffectiveAt <= DateTime.UtcNow)
            .ToListAsync(ct);
        var campaignIds = candidateCampaigns
            .Where(candidate => !IsDestinationConsented(preferenceRows, candidate.Category))
            .Select(candidate => candidate.CampaignId)
            .Distinct()
            .ToArray();
        var now = DateTime.UtcNow;
        await _db.WhatsAppCampaignRecipients
            .Where(item => item.DestinationHash == destinationHash &&
                item.Status == WhatsAppCampaignRecipientStatus.Pending &&
                campaignIds.Contains(item.CampaignId))
            .ExecuteUpdateAsync(update => update
                .SetProperty(item => item.Status, WhatsAppCampaignRecipientStatus.Skipped)
                .SetProperty(item => item.FailureCode, failureCode)
                .SetProperty(item => item.UpdatedAt, now)
                .SetProperty(item => item.Version, item => item.Version + 1), ct);
        var affectedCampaigns = await _db.WhatsAppCampaigns
            .Where(item => campaignIds.Contains(item.Id))
            .ToListAsync(ct);
        await RefreshCountersAsync(affectedCampaigns, ct);
        await CompleteCampaignsIfTerminalAsync(affectedCampaigns, ct);
    }

    internal async Task<bool> IsDestinationConsentedAsync(
        string destinationHash,
        string templateCategory,
        CancellationToken ct)
    {
        var category = string.Equals(templateCategory, "MARKETING", StringComparison.OrdinalIgnoreCase)
            ? WhatsAppContactPreferenceCategory.Marketing
            : WhatsAppContactPreferenceCategory.Utility;
        var rows = await _db.WhatsAppContactPreferences.AsNoTracking()
            .Where(item => item.DestinationHash == destinationHash &&
                (item.Category == category || item.Category == WhatsAppContactPreferenceCategory.All) &&
                item.EffectiveAt <= DateTime.UtcNow)
            .ToListAsync(ct);
        return IsDestinationConsented(rows, templateCategory);
    }

    private static bool IsDestinationConsented(
        IReadOnlyList<WhatsAppContactPreference> rows,
        string templateCategory)
    {
        var category = string.Equals(templateCategory, "MARKETING", StringComparison.OrdinalIgnoreCase)
            ? WhatsAppContactPreferenceCategory.Marketing
            : WhatsAppContactPreferenceCategory.Utility;
        WhatsAppContactPreference? Latest(WhatsAppContactPreferenceCategory target) => rows
            .Where(item => item.Category == target)
            .OrderByDescending(item => item.EffectiveAt)
            .ThenByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.State == WhatsAppContactPreferenceState.OptedOut)
            .ThenByDescending(item => item.Id).FirstOrDefault();
        var categoryPreference = Latest(category);
        if (categoryPreference?.State != WhatsAppContactPreferenceState.OptedIn) return false;
        var global = Latest(WhatsAppContactPreferenceCategory.All);
        return global is null || global.State != WhatsAppContactPreferenceState.OptedOut ||
            !PreferenceAtLeastAsRecent(global, categoryPreference);
    }

    internal async Task<bool> CurrentRecipientDestinationMatchesAsync(
        WhatsAppCampaignRecipient recipient,
        CancellationToken ct)
    {
        try
        {
            var contact = await ResolveAdminContactAsync(
                recipient.StudentUserId, recipient.ContactRole, ct);
            var e164 = NormalizeE164(contact.Phone);
            return e164 is not null && FixedHashEquals(
                recipient.DestinationHash, _protector.DestinationHash(e164));
        }
        catch (WhatsAppCampaignException)
        {
            return false;
        }
    }

    private static bool PreferenceAtLeastAsRecent(
        WhatsAppContactPreference candidate,
        WhatsAppContactPreference baseline)
    {
        if (candidate.EffectiveAt != baseline.EffectiveAt)
            return candidate.EffectiveAt > baseline.EffectiveAt;
        if (candidate.CreatedAt != baseline.CreatedAt)
            return candidate.CreatedAt > baseline.CreatedAt;
        return candidate.State == WhatsAppContactPreferenceState.OptedOut ||
            candidate.Id.CompareTo(baseline.Id) >= 0;
    }

    private static bool IsPreferenceConcurrencyFailure(Exception exception) => exception switch
    {
        DbUpdateConcurrencyException => true,
        PostgresException { SqlState: PostgresErrorCodes.SerializationFailure } => true,
        DbUpdateException
        {
            InnerException: PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.SerializationFailure
            }
        } => true,
        _ => false
    };

    internal Task RefreshCountersAsync(WhatsAppCampaign campaign, CancellationToken ct) =>
        RefreshCountersAsync([campaign], ct);

    private async Task RefreshCountersAsync(
        IReadOnlyList<WhatsAppCampaign> campaigns,
        CancellationToken ct)
    {
        if (campaigns.Count == 0) return;
        var campaignIds = campaigns.Select(campaign => campaign.Id).ToArray();
        var countRows = await _db.WhatsAppCampaignRecipients.AsNoTracking()
            .Where(item => campaignIds.Contains(item.CampaignId))
            .GroupBy(item => new { item.CampaignId, item.Status })
            .Select(group => new { group.Key.CampaignId, group.Key.Status, Count = group.Count() })
            .ToListAsync(ct);
        var counts = countRows.ToDictionary(row => (row.CampaignId, row.Status), row => row.Count);
        foreach (var campaign in campaigns) ApplyCounters(campaign, counts);
    }

    private static void ApplyCounters(
        WhatsAppCampaign campaign,
        IReadOnlyDictionary<(Guid CampaignId, WhatsAppCampaignRecipientStatus Status), int> counts)
    {
        var before = (campaign.PendingCount, campaign.SentCount, campaign.DeliveredCount,
            campaign.ReadCount, campaign.FailedCount, campaign.SkippedCount, campaign.UncertainCount);
        int Count(WhatsAppCampaignRecipientStatus status) =>
            counts.GetValueOrDefault((campaign.Id, status));
        campaign.PendingCount = Count(WhatsAppCampaignRecipientStatus.Pending) +
            Count(WhatsAppCampaignRecipientStatus.Sending);
        campaign.SentCount = Count(WhatsAppCampaignRecipientStatus.Sent) +
            Count(WhatsAppCampaignRecipientStatus.Delivered) + Count(WhatsAppCampaignRecipientStatus.Read);
        campaign.DeliveredCount = Count(WhatsAppCampaignRecipientStatus.Delivered) +
            Count(WhatsAppCampaignRecipientStatus.Read);
        campaign.ReadCount = Count(WhatsAppCampaignRecipientStatus.Read);
        campaign.FailedCount = Count(WhatsAppCampaignRecipientStatus.Failed);
        campaign.SkippedCount = Count(WhatsAppCampaignRecipientStatus.Skipped);
        campaign.UncertainCount = Count(WhatsAppCampaignRecipientStatus.Uncertain);
        var after = (campaign.PendingCount, campaign.SentCount, campaign.DeliveredCount,
            campaign.ReadCount, campaign.FailedCount, campaign.SkippedCount, campaign.UncertainCount);
        if (before != after)
        {
            campaign.UpdatedAt = DateTime.UtcNow;
        }
    }

    private async Task<WhatsAppCampaignFacetsDto> BuildFacetsAsync(CancellationToken ct)
    {
        var stages = await _db.StudentProfiles.AsNoTracking()
            .Where(profile => profile.User.IsActive && !profile.User.IsDeleted)
            .GroupBy(profile => profile.EducationStage)
            .Select(group => new { Value = group.Key, Count = group.Count() }).ToListAsync(ct);
        var grades = await _db.StudentProfiles.AsNoTracking()
            .Where(profile => profile.User.IsActive && !profile.User.IsDeleted)
            .GroupBy(profile => profile.GradeLevel)
            .Select(group => new { Value = group.Key, Count = group.Count() }).ToListAsync(ct);
        var tracks = await _db.StudentProfiles.AsNoTracking()
            .Where(profile => profile.User.IsActive && !profile.User.IsDeleted && profile.StudyTrack != null)
            .GroupBy(profile => profile.StudyTrack!.Value)
            .Select(group => new { Value = group.Key, Count = group.Count() }).ToListAsync(ct);
        var crm = await _db.CrmStudentStatuses.AsNoTracking().GroupBy(item => item.Status)
            .Select(group => new { Value = group.Key, Count = group.Count() }).ToListAsync(ct);
        var teachers = await _db.TeacherProfiles.AsNoTracking()
            .Where(item => item.User.IsActive && !item.User.IsDeleted &&
                item.Packages.Any(package => package.IsActive &&
                    package.ArchiveMode != NaderGorge.Domain.Enums.ContentArchiveMode.HiddenFromEveryone))
            .OrderBy(item => item.User.FullName)
            .Select(item => new WhatsAppCampaignFacetItemDto(item.Id.ToString(), item.User.FullName,
                item.Packages.Count(package => package.IsActive &&
                    package.ArchiveMode != NaderGorge.Domain.Enums.ContentArchiveMode.HiddenFromEveryone)))
            .ToListAsync(ct);
        var subjects = await _db.Subjects.AsNoTracking()
            .Where(item => item.Packages.Any(package => package.IsActive &&
                package.ArchiveMode != NaderGorge.Domain.Enums.ContentArchiveMode.HiddenFromEveryone))
            .OrderBy(item => item.Name)
            .Select(item => new WhatsAppCampaignFacetItemDto(item.Id.ToString(), item.Name,
                item.Packages.Count(package => package.IsActive &&
                    package.ArchiveMode != NaderGorge.Domain.Enums.ContentArchiveMode.HiddenFromEveryone)))
            .ToListAsync(ct);
        var packages = await _db.Packages.AsNoTracking()
            .Where(item => item.IsActive &&
                item.ArchiveMode != NaderGorge.Domain.Enums.ContentArchiveMode.HiddenFromEveryone &&
                item.Teacher.User.IsActive && !item.Teacher.User.IsDeleted)
            .OrderBy(item => item.Name)
            .Select(item => new WhatsAppCampaignFacetItemDto(item.Id.ToString(), item.Name, 0)).ToListAsync(ct);
        var lessons = await _db.Lessons.AsNoTracking()
            .Where(item => item.ArchiveMode != NaderGorge.Domain.Enums.ContentArchiveMode.HiddenFromEveryone &&
                item.ContentSection.ArchiveMode != NaderGorge.Domain.Enums.ContentArchiveMode.HiddenFromEveryone &&
                item.ContentSection.Term.ArchiveMode != NaderGorge.Domain.Enums.ContentArchiveMode.HiddenFromEveryone &&
                item.ContentSection.Term.Package.IsActive &&
                item.ContentSection.Term.Package.ArchiveMode != NaderGorge.Domain.Enums.ContentArchiveMode.HiddenFromEveryone)
            .OrderBy(item => item.Title)
            .Select(item => new WhatsAppCampaignFacetItemDto(item.Id.ToString(),
                item.Title + " — " + item.ContentSection.Term.Package.Subject.Name + " — " +
                item.ContentSection.Term.Package.Teacher.User.FullName, 0)).ToListAsync(ct);
        var exams = await _db.Exams.AsNoTracking()
            .Where(item => item.IsActive &&
                item.ArchiveMode != NaderGorge.Domain.Enums.ContentArchiveMode.HiddenFromEveryone &&
                (item.LessonVideoId != null || _db.Lessons.Any(lesson => lesson.ExamId == item.Id)))
            .OrderBy(item => item.Title)
            .Select(item => new WhatsAppCampaignFacetItemDto(item.Id.ToString(), item.Title, item.Attempts.Count))
            .ToListAsync(ct);
        var homeworks = await _db.Homeworks.AsNoTracking()
            .Where(item => item.IsActive &&
                item.ArchiveMode != NaderGorge.Domain.Enums.ContentArchiveMode.HiddenFromEveryone &&
                _db.Lessons.Any(lesson => lesson.Id == item.LessonId &&
                    lesson.ContentSection.Term.Package.IsActive &&
                    lesson.ContentSection.Term.Package.ArchiveMode !=
                        NaderGorge.Domain.Enums.ContentArchiveMode.HiddenFromEveryone))
            .OrderBy(item => item.Title)
            .Select(item => new WhatsAppCampaignFacetItemDto(item.Id.ToString(), item.Title, item.Submissions.Count))
            .ToListAsync(ct);
        return new WhatsAppCampaignFacetsDto(
            stages.Select(item => Facet(item.Value.ToString(), item.Count)).ToArray(),
            grades.Select(item => Facet(item.Value.ToString(), item.Count)).ToArray(),
            tracks.Select(item => Facet(item.Value.ToString(), item.Count)).ToArray(),
            crm.Select(item => Facet(item.Value.ToString(), item.Count)).ToArray(),
            teachers, subjects, packages, lessons, exams, homeworks);
    }

    private async Task<(string StudentName, string Phone)> ResolveAdminContactAsync(
        Guid studentUserId,
        string contactRole,
        CancellationToken ct)
    {
        var role = NormalizeContactRole(contactRole);
        var row = await _db.Users.AsNoTracking().Where(user => user.Id == studentUserId &&
                user.IsActive && !user.IsDeleted && user.StudentProfile != null &&
                user.UserRoles.Any(link => link.Role.Type == NaderGorge.Domain.Enums.RoleType.Student))
            .Select(user => new
            {
                user.FullName,
                Primary = user.PhoneNumber,
                Secondary = user.StudentProfile == null ? null : user.StudentProfile.SecondaryPhone,
                Father = user.StudentProfile == null ? null : user.StudentProfile.ParentPhone,
                FatherSecondary = user.StudentProfile == null ? null : user.StudentProfile.SecondaryParentPhone,
                Mother = user.StudentProfile == null ? null : user.StudentProfile.MotherPhone
            }).SingleOrDefaultAsync(ct) ?? throw Invalid("الطالب غير موجود أو غير نشط.");
        var phone = role switch
        {
            "StudentPrimary" => row.Primary,
            "StudentSecondary" => row.Secondary,
            "FatherPrimary" => row.Father,
            "FatherSecondary" => row.FatherSecondary,
            "Mother" => row.Mother,
            _ => null
        };
        if (string.IsNullOrWhiteSpace(phone)) throw Invalid("جهة الاتصال المختارة لا تملك رقمًا مسجلاً.");
        return (row.FullName, phone);
    }

    private async Task<WhatsAppContactPreference?> LatestPreferenceAsync(
        string destinationHash,
        WhatsAppContactPreferenceCategory category,
        CancellationToken ct) =>
        await _db.WhatsAppContactPreferences
            .Where(item => item.DestinationHash == destinationHash && item.Category == category)
            .OrderByDescending(item => item.EffectiveAt)
            .ThenByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.State == WhatsAppContactPreferenceState.OptedOut)
            .ThenByDescending(item => item.Id)
            .FirstOrDefaultAsync(ct);

    private void AppendAudit(Guid? campaignId, Guid actorUserId, string action, object metadata) =>
        _db.WhatsAppCampaignAuditEvents.Add(new WhatsAppCampaignAuditEvent
        {
            CampaignId = campaignId,
            ActorUserId = actorUserId,
            Action = action,
            SafeMetadataJson = JsonSerializer.Serialize(metadata, JsonOptions)
        });

    private static WhatsAppCampaignSummaryDto ToSummary(WhatsAppCampaign item) => new(
        item.Id, item.Name, item.TemplateName, item.TemplateLanguage, item.TemplateCategory,
        item.Status.ToString(), item.RecipientCount, item.ExcludedCount, item.PendingCount,
        item.SentCount, item.DeliveredCount, item.ReadCount, item.FailedCount, item.SkippedCount,
        item.UncertainCount, item.Version, item.CreatedAt, item.LaunchedAt, item.CompletedAt,
        item.PauseReason);

    private static WhatsAppCampaignStateDto ToState(WhatsAppCampaign item) =>
        new(item.Id, item.Status.ToString(), item.Version);

    private static WhatsAppContactPreferenceDto ToPreferenceDto(
        WhatsAppContactPreference item,
        string? studentName) => new(
        item.Id, item.StudentUserId, studentName, item.ContactRole, $"***{item.DestinationLast4}",
        item.Category, item.State, item.Source, item.EvidenceReference, item.EffectiveAt,
        item.CreatedAt, item.RecordedByUserId);

    private static WhatsAppCampaignFacetItemDto Facet(string value, int count) =>
        new(value, value, count);

    internal static string? NormalizeE164(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();
        if (value.Length > 32 || value.Count(character => character == '+') > 1 ||
            value.Contains('+') && value[0] != '+' ||
            value.Any(character => !(character is >= '0' and <= '9') &&
            character is not ('+' or '-' or '(' or ')') && !char.IsWhiteSpace(character))) return null;
        var digits = new string(value.Where(character => character is >= '0' and <= '9').ToArray());
        if (digits.StartsWith("00", StringComparison.Ordinal)) digits = digits[2..];
        if (digits.Length == 11 && digits[0] == '0' && digits[1] == '1' && "0125".Contains(digits[2]))
            digits = $"20{digits[1..]}";
        return digits.Length == 12 && digits.StartsWith("201", StringComparison.Ordinal) &&
            "0125".Contains(digits[3]) ? digits : null;
    }

    private static string NormalizeContactRole(string value)
    {
        var role = value?.Trim() ?? string.Empty;
        return role is "StudentPrimary" or "StudentSecondary" or "FatherPrimary" or
            "FatherSecondary" or "Mother"
            ? role
            : throw Invalid("نوع جهة الاتصال غير مدعوم.");
    }

    private static void ValidateAdminPreference(RecordWhatsAppContactPreferenceRequest request)
    {
        string[] allowedSources =
            ["web_consent_form", "signed_document", "recorded_call", "inbound_request", "legacy_import"];
        if (request.StudentUserId == Guid.Empty) throw Invalid("معرف الطالب مطلوب.");
        _ = NormalizeContactRole(request.ContactRole);
        if (!Enum.IsDefined(request.Category) || !Enum.IsDefined(request.State))
            throw Invalid("فئة أو حالة تفضيل الاتصال غير مدعومة.");
        if (request.Category == WhatsAppContactPreferenceCategory.All &&
            request.State != WhatsAppContactPreferenceState.OptedOut)
            throw Invalid("فئة جميع الرسائل متاحة لإلغاء الاشتراك فقط.");
        if (string.IsNullOrWhiteSpace(request.Source) ||
            !allowedSources.Contains(request.Source.Trim(), StringComparer.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(request.EvidenceReference) || request.EvidenceReference.Trim().Length > 500)
            throw Invalid("مصدر ومُستند الموافقة أو الرفض مطلوبان.");
    }

    private static void ValidateIdempotencyKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length is < 8 or > 100)
            throw Invalid("مفتاح تكرار صالح مطلوب.");
    }

    private static string NormalizePhrase(string value) =>
        string.Join(' ', (value ?? string.Empty).Normalize(NormalizationForm.FormC)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string? BoundedReason(string? reason) =>
        string.IsNullOrWhiteSpace(reason) ? null : reason.Trim()[..Math.Min(200, reason.Trim().Length)];

    private static string HashJson(object value) =>
        HashText(JsonSerializer.Serialize(value, JsonOptions));

    internal static string HashText(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static bool FixedHashEquals(string expected, string supplied) =>
        expected.Length == supplied.Length && CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(supplied));

    private static WhatsAppCampaignException Invalid(string message) =>
        new(WhatsAppCampaignErrorCodes.InvalidRequest, message, 400);

    private static WhatsAppCampaignException NotFound() =>
        new(WhatsAppCampaignErrorCodes.NotFound, "حملة واتساب غير موجودة.", 404);

    private static WhatsAppCampaignException Conflict(string code, string message) =>
        new(code, message, 409);
}
