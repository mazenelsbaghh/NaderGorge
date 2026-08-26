using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Infrastructure.Services;

public sealed partial class WhatsAppCampaignService
{
    private const int MaximumAudienceRows = 100_000;
    private const int MaximumVariableLength = 1_024;
    private static readonly string[] ContactRoleWhitelist =
        ["StudentPrimary", "StudentSecondary", "FatherPrimary", "FatherSecondary", "Mother"];

    private sealed record AudienceStudentRow(
        Guid StudentUserId,
        string FullName,
        string PrimaryPhone,
        string? SecondaryPhone,
        string? FatherPhone,
        string? FatherSecondaryPhone,
        string? MotherPhone,
        EducationStage? EducationStage,
        GradeLevel? GradeLevel,
        StudyTrack? StudyTrack,
        string? Governorate,
        string? SchoolName,
        string? ParentTrackingCode);

    private sealed record ExpandedContact(
        AudienceStudentRow Student,
        string ContactRole,
        string? RawPhone);

    internal sealed record FrozenRecipientPayload(
        string Destination,
        IReadOnlyList<WhatsAppCloudService.TemplateComponent> Components);

    internal static string SerializeFrozenRecipientPayload(FrozenRecipientPayload payload) =>
        JsonSerializer.Serialize(payload, JsonOptions);

    internal static FrozenRecipientPayload? DeserializeFrozenRecipientPayload(
        ReadOnlySpan<byte> payload) =>
        JsonSerializer.Deserialize<FrozenRecipientPayload>(payload, JsonOptions);

    private sealed record ResolvedAudienceRecipient(
        Guid StudentUserId,
        string StudentName,
        string ContactRole,
        string DestinationHash,
        string DestinationLast4,
        string PayloadJson,
        string RenderedPreview);

    private sealed record AudienceBuildResult(
        IReadOnlyList<ResolvedAudienceRecipient> Recipients,
        IReadOnlyDictionary<string, int> Exclusions,
        string Fingerprint)
    {
        public int ExcludedCount => Exclusions.Values.Sum();
    }

    public async Task<WhatsAppCampaignPreviewDto> PreviewAsync(
        WhatsAppCampaignPreviewRequest request,
        CancellationToken ct)
    {
        if (request is null) throw Invalid("بيانات معاينة الحملة مطلوبة.");
        var template = await _db.LiveSupportWhatsAppTemplates.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == request.TemplateId, ct)
            ?? throw new WhatsAppCampaignException(
                WhatsAppCampaignErrorCodes.TemplateInvalid, "قالب واتساب غير موجود.", 404);
        var audience = await BuildAudienceAsync(template, request.Filters, request.VariableMappings, ct);
        var samples = audience.Recipients.Take(5).Select(recipient =>
            new WhatsAppCampaignMaskedRecipientDto(
                MaskName(recipient.StudentName),
                $"***{recipient.DestinationLast4}",
                recipient.ContactRole,
                recipient.RenderedPreview)).ToArray();
        return new WhatsAppCampaignPreviewDto(
            audience.Recipients.Count,
            audience.ExcludedCount,
            audience.Exclusions,
            audience.Fingerprint,
            template.Fingerprint,
            DateTime.UtcNow.AddMinutes(15),
            samples);
    }

    public async Task<WhatsAppCampaignDraftDto> CreateDraftAsync(
        Guid actorUserId,
        string idempotencyKey,
        CreateWhatsAppCampaignDraftRequest request,
        CancellationToken ct)
    {
        ValidateIdempotencyKey(idempotencyKey);
        if (request is null) throw Invalid("بيانات الحملة مطلوبة.");
        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length is < 3 or > 160) throw Invalid("اسم الحملة يجب أن يكون بين 3 و160 حرفًا.");
        var createRequestHash = HashJson(new { request, idempotencyKey });
        await using var transaction = await _db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var replay = await _db.WhatsAppCampaigns.AsNoTracking()
            .SingleOrDefaultAsync(item => item.CreatedByUserId == actorUserId &&
                item.CreateIdempotencyKey == idempotencyKey, ct);
        if (replay is not null)
        {
            if (!string.Equals(replay.CreateRequestHash, createRequestHash, StringComparison.Ordinal))
                throw Conflict(WhatsAppCampaignErrorCodes.IdempotencyConflict,
                    "مفتاح تكرار إنشاء الحملة مستخدم بطلب مختلف.");
            var replayTokenBytes = _protector.Unprotect(replay.Id, replay.ProtectedReviewToken,
                replay.ProtectedReviewTokenDigest);
            await transaction.CommitAsync(ct);
            return CampaignDraftDto(replay, Encoding.UTF8.GetString(replayTokenBytes));
        }

        var template = await _db.LiveSupportWhatsAppTemplates.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == request.TemplateId, ct)
            ?? throw new WhatsAppCampaignException(
                WhatsAppCampaignErrorCodes.TemplateInvalid, "قالب واتساب غير موجود.", 404);
        WhatsAppCampaignTemplatePolicy.RequireCampaignTemplate(template);
        var audience = await BuildAudienceAsync(template, request.Filters, request.VariableMappings, ct);
        if (!string.Equals(audience.Fingerprint, request.AudienceFingerprint, StringComparison.Ordinal))
            throw Conflict(WhatsAppCampaignErrorCodes.AudienceChanged,
                "تغير الجمهور منذ المعاينة؛ اعرض المعاينة من جديد.");
        if (audience.Recipients.Count == 0)
            throw Invalid("لا توجد جهات اتصال مؤهلة بعد تطبيق الموافقات والاستبعادات.");
        var maximumRecipients = BoundedConfigurationInt(
            "WhatsAppCampaigns:MaximumRecipients", 25_000, 1, 100_000);
        if (audience.Recipients.Count > maximumRecipients)
            throw Invalid($"حجم الجمهور يتجاوز الحد المسموح ({maximumRecipients}).");

        var campaignId = Guid.NewGuid();
        var reviewToken = Base64Url(RandomNumberGenerator.GetBytes(32));
        var confirmationPhrase = ConfirmationPhrase(audience.Recipients.Count);
        var reviewExpiresAt = DateTime.UtcNow.AddMinutes(BoundedConfigurationInt(
            "WhatsAppCampaigns:ReviewMinutes", 30, 5, 120));
        var protectedReviewToken = _protector.Protect(campaignId, Encoding.UTF8.GetBytes(reviewToken));
        var campaign = new WhatsAppCampaign
        {
            Id = campaignId,
            Name = name,
            TemplateId = template.Id,
            TemplateMetaId = template.MetaTemplateId,
            TemplateName = template.Name,
            TemplateLanguage = template.Language,
            TemplateCategory = template.Category,
            TemplateComponentsJson = template.ComponentsJson,
            TemplateFingerprint = template.Fingerprint,
            AudienceFilterJson = JsonSerializer.Serialize(request.Filters, JsonOptions),
            VariableMappingsJson = JsonSerializer.Serialize(request.VariableMappings, JsonOptions),
            AudienceFingerprint = audience.Fingerprint,
            Status = WhatsAppCampaignStatus.Locked,
            RecipientCount = audience.Recipients.Count,
            ExcludedCount = audience.ExcludedCount,
            ExclusionSummaryJson = JsonSerializer.Serialize(audience.Exclusions, JsonOptions),
            PendingCount = audience.Recipients.Count,
            CreatedByUserId = actorUserId,
            LastChangedByUserId = actorUserId,
            LockedAt = DateTime.UtcNow,
            ReviewTokenHash = _protector.SecretHash($"review:{campaignId:N}", reviewToken),
            ProtectedReviewToken = protectedReviewToken,
            ProtectedReviewTokenDigest = _protector.Digest(campaignId, protectedReviewToken),
            ReviewTokenExpiresAt = reviewExpiresAt,
            ConfirmationPhraseHash = _protector.SecretHash(
                $"confirmation:{campaignId:N}", NormalizePhrase(confirmationPhrase)),
            CreateIdempotencyKey = idempotencyKey,
            CreateRequestHash = createRequestHash,
            Version = 1
        };
        _db.WhatsAppCampaigns.Add(campaign);
        foreach (var resolved in audience.Recipients)
        {
            var recipientId = Guid.NewGuid();
            var protectedPayload = _protector.Protect(recipientId, Encoding.UTF8.GetBytes(resolved.PayloadJson));
            _db.WhatsAppCampaignRecipients.Add(new WhatsAppCampaignRecipient
            {
                Id = recipientId,
                CampaignId = campaign.Id,
                StudentUserId = resolved.StudentUserId,
                ContactRole = resolved.ContactRole,
                DestinationHash = resolved.DestinationHash,
                DestinationLast4 = resolved.DestinationLast4,
                ProtectedPayload = protectedPayload,
                PayloadDigest = _protector.Digest(recipientId, protectedPayload),
                Status = WhatsAppCampaignRecipientStatus.Pending,
                Version = 1
            });
        }
        AppendAudit(campaign.Id, actorUserId, "campaign_locked", new
        {
            campaign.RecipientCount,
            campaign.ExcludedCount,
            campaign.TemplateName,
            campaign.TemplateLanguage,
            campaign.TemplateCategory,
            exclusions = audience.Exclusions
        });
        try
        {
            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return CampaignDraftDto(campaign, reviewToken);
        }
        catch (Exception exception) when (IsPreferenceConcurrencyFailure(exception))
        {
            await transaction.RollbackAsync(CancellationToken.None);
            _db.ClearTrackedChanges();
            var concurrentReplay = await _db.WhatsAppCampaigns.AsNoTracking()
                .SingleOrDefaultAsync(item => item.CreatedByUserId == actorUserId &&
                    item.CreateIdempotencyKey == idempotencyKey, CancellationToken.None);
            if (concurrentReplay is null)
                throw Conflict(WhatsAppCampaignErrorCodes.Conflict,
                    "تزامن إنشاء الحملة مع عملية أخرى؛ أعد المحاولة.");
            if (!string.Equals(concurrentReplay.CreateRequestHash, createRequestHash, StringComparison.Ordinal))
                throw Conflict(WhatsAppCampaignErrorCodes.IdempotencyConflict,
                    "مفتاح تكرار إنشاء الحملة مستخدم بطلب مختلف.");
            var replayTokenBytes = _protector.Unprotect(
                concurrentReplay.Id,
                concurrentReplay.ProtectedReviewToken,
                concurrentReplay.ProtectedReviewTokenDigest);
            return CampaignDraftDto(concurrentReplay, Encoding.UTF8.GetString(replayTokenBytes));
        }
    }

    private async Task<AudienceBuildResult> BuildAudienceAsync(
        LiveSupportWhatsAppTemplate template,
        WhatsAppCampaignAudienceFilterDto filters,
        IReadOnlyList<WhatsAppCampaignVariableMappingDto> mappings,
        CancellationToken ct)
    {
        if (filters is null || mappings is null)
            throw Invalid("شروط الجمهور وتعيينات القالب مطلوبة.");
        var campaignTemplate = WhatsAppCampaignTemplatePolicy.RequireCampaignTemplate(template);
        var canonicalMappings = WhatsAppCampaignTemplatePolicy.ValidateMappings(campaignTemplate, mappings);
        var variableMappings = canonicalMappings.Select(entry => entry.Mapping).ToArray();
        var normalized = NormalizeAndValidateFilters(filters);
        ValidateVariableMappings(variableMappings, normalized, template.Category);
        var needsParentTrackingCode = variableMappings.Any(mapping =>
            string.Equals(mapping.Source.Trim(), "ParentTrackingCode", StringComparison.OrdinalIgnoreCase));
        var targetPackageIds = await ResolveTargetPackageIdsAsync(normalized, ct);
        if (normalized.HasTargetScope && targetPackageIds.Length == 0)
            throw Invalid("نطاق المحتوى المحدد لا يطابق أي باقة أكاديمية نشطة.");
        var query = ApplyAudienceFilters(_db.Users.AsNoTracking()
            .Where(user => user.IsActive && !user.IsDeleted &&
                user.UserRoles.Any(link => link.Role.Type == RoleType.Student)), normalized, targetPackageIds);
        var rows = await query.OrderBy(user => user.Id).Take(MaximumAudienceRows + 1)
            .Select(user => new AudienceStudentRow(
                user.Id,
                user.FullName,
                user.PhoneNumber,
                user.StudentProfile == null ? null : user.StudentProfile.SecondaryPhone,
                user.StudentProfile == null ? null : user.StudentProfile.ParentPhone,
                user.StudentProfile == null ? null : user.StudentProfile.SecondaryParentPhone,
                user.StudentProfile == null ? null : user.StudentProfile.MotherPhone,
                user.StudentProfile == null ? null : user.StudentProfile.EducationStage,
                user.StudentProfile == null ? null : user.StudentProfile.GradeLevel,
                user.StudentProfile == null ? null : user.StudentProfile.StudyTrack,
                user.StudentProfile == null ? null : user.StudentProfile.Governorate,
                user.StudentProfile == null ? null : user.StudentProfile.SchoolName,
                needsParentTrackingCode && user.StudentProfile != null
                    ? user.StudentProfile.ParentTrackingCode
                    : null))
            .ToListAsync(ct);
        if (rows.Count > MaximumAudienceRows)
            throw Invalid("الجمهور واسع جدًا؛ أضف فلاتر أكاديمية أكثر تحديدًا.");

        var contacts = rows.SelectMany(row => normalized.ContactRoles.Select(role =>
            new ExpandedContact(row, role, ContactPhone(row, role)))).ToArray();
        var exclusions = NewExclusionCounts();
        var normalizedContacts = new List<(ExpandedContact Contact, string E164, string Hash)>();
        foreach (var contact in contacts)
        {
            if (string.IsNullOrWhiteSpace(contact.RawPhone))
            {
                Increment(exclusions, "no_phone");
                continue;
            }
            var e164 = NormalizeE164(contact.RawPhone);
            if (e164 is null)
            {
                Increment(exclusions, "invalid_phone");
                continue;
            }
            normalizedContacts.Add((contact, e164, _protector.DestinationHash(e164)));
        }

        var sharedDestinationHashes = normalizedContacts.GroupBy(item => item.Hash)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var shared in normalizedContacts.Where(item =>
                     sharedDestinationHashes.Contains(item.Hash)).GroupBy(item => item.Hash))
            Add(exclusions, "duplicate_or_ambiguous_phone", shared.Count());
        if (sharedDestinationHashes.Count > 0)
            normalizedContacts = normalizedContacts.Where(item =>
                !sharedDestinationHashes.Contains(item.Hash)).ToList();

        var destinationHashes = normalizedContacts.Select(item => item.Hash).Distinct().ToArray();
        var preferenceRows = destinationHashes.Length == 0
            ? []
            : await _db.WhatsAppContactPreferences.AsNoTracking()
                .Where(item => destinationHashes.Contains(item.DestinationHash) &&
                    item.EffectiveAt <= DateTime.UtcNow)
                .ToListAsync(ct);
        var preferenceAuthority = preferenceRows.GroupBy(item => (item.DestinationHash, item.Category))
            .ToDictionary(group => group.Key, group => group
                .OrderByDescending(item => item.EffectiveAt)
                .ThenByDescending(item => item.CreatedAt)
                .ThenByDescending(item => item.State == WhatsAppContactPreferenceState.OptedOut)
                .ThenByDescending(item => item.Id).First());
        var consentCategory = string.Equals(template.Category, "MARKETING", StringComparison.OrdinalIgnoreCase)
            ? WhatsAppContactPreferenceCategory.Marketing
            : WhatsAppContactPreferenceCategory.Utility;
        var referenceValues = await LoadReferenceValuesAsync(variableMappings, ct);
        var purchaseDates = await LoadPurchaseDatesAsync(
            rows.Select(row => row.StudentUserId).ToArray(), variableMappings, normalized, ct);
        var consented = new List<ResolvedAudienceRecipient>();
        foreach (var item in normalizedContacts)
        {
            if (!preferenceAuthority.TryGetValue((item.Hash, consentCategory), out var preference))
            {
                Increment(exclusions, "no_consent");
                continue;
            }
            if (preference.State != WhatsAppContactPreferenceState.OptedIn)
            {
                Increment(exclusions, "opted_out");
                continue;
            }
            if (preferenceAuthority.TryGetValue((item.Hash, WhatsAppContactPreferenceCategory.All), out var global) &&
                global.State == WhatsAppContactPreferenceState.OptedOut &&
                PreferenceIsAtLeastAsRecent(global, preference))
            {
                Increment(exclusions, "opted_out");
                continue;
            }
            IReadOnlyDictionary<WhatsAppTemplateParameterKey, string> resolvedParameters;
            try
            {
                resolvedParameters = ResolveVariableValues(
                    item.Contact.Student, canonicalMappings, referenceValues, purchaseDates);
            }
            catch (MissingCampaignVariableException)
            {
                Increment(exclusions, "missing_variable");
                continue;
            }
            var components = WhatsAppCampaignTemplatePolicy.ProviderComponents(
                campaignTemplate, resolvedParameters);
            var payloadJson = SerializeFrozenRecipientPayload(
                new FrozenRecipientPayload(item.E164, components));
            var maskedPreviewValues = MaskPreviewValues(canonicalMappings, resolvedParameters);
            consented.Add(new ResolvedAudienceRecipient(
                item.Contact.Student.StudentUserId,
                item.Contact.Student.FullName,
                item.Contact.ContactRole,
                item.Hash,
                item.E164[^4..],
                payloadJson,
                WhatsAppCampaignTemplatePolicy.RenderPreview(campaignTemplate, maskedPreviewValues)));
        }

        var deduplicated = new List<ResolvedAudienceRecipient>();
        foreach (var group in consented.GroupBy(item => item.DestinationHash).OrderBy(group => group.Key))
        {
            var grouped = group.ToArray();
            if (grouped.Length > 1)
            {
                // A shared destination is not a safe personalization target, even if two
                // resolved payloads happen to be identical today. Exclude every owner/role.
                Add(exclusions, "duplicate_or_ambiguous_phone", grouped.Length);
                continue;
            }
            deduplicated.Add(grouped[0]);
        }

        var fingerprintMaterial = new
        {
            template = template.Fingerprint,
            filters = normalized.Dto,
            mappings = canonicalMappings.OrderBy(entry => entry.Requirement.Key.ComponentIndex)
                .ThenBy(entry => entry.Requirement.Key.ButtonIndex)
                .ThenBy(entry => entry.Requirement.Key.Position)
                .Select(entry => new
                {
                    entry.Requirement.Key.ComponentType,
                    entry.Requirement.Key.ComponentIndex,
                    entry.Requirement.Key.ButtonIndex,
                    entry.Requirement.Key.Position,
                    entry.Mapping.Source,
                    entry.Mapping.LiteralValue,
                    entry.Mapping.ReferenceId,
                    entry.Mapping.Format
                }),
            recipients = deduplicated.OrderBy(item => item.DestinationHash, StringComparer.Ordinal)
                .Select(item => new { item.DestinationHash, payload = HashText(item.PayloadJson) })
        };
        return new AudienceBuildResult(deduplicated, exclusions, HashJson(fingerprintMaterial));
    }

    private IQueryable<NaderGorge.Domain.Entities.User> ApplyAudienceFilters(
        IQueryable<NaderGorge.Domain.Entities.User> query,
        NormalizedAudienceFilters filters,
        Guid[] targetPackageIds)
    {
        if (filters.Stages.Length > 0)
            query = query.Where(user => user.StudentProfile != null &&
                filters.Stages.Contains(user.StudentProfile.EducationStage));
        if (filters.Grades.Length > 0)
            query = query.Where(user => user.StudentProfile != null &&
                filters.Grades.Contains(user.StudentProfile.GradeLevel));
        if (filters.Tracks.Length > 0)
            query = query.Where(user => user.StudentProfile != null && user.StudentProfile.StudyTrack != null &&
                filters.Tracks.Contains(user.StudentProfile.StudyTrack.Value));
        if (filters.CrmStatuses.Length > 0)
            query = query.Where(user => _db.CrmStudentStatuses.Any(status =>
                status.StudentId == user.Id && filters.CrmStatuses.Contains(status.Status)));

        if (filters.HasTargetScope)
            query = query.Where(user => user.StudentProfile != null &&
                _db.StudentFacingAcademicScopes.Any(scope =>
                    scope.OwnerType == StudentFacingScopeOwnerType.Package &&
                    targetPackageIds.Contains(scope.OwnerId) &&
                    (scope.ScopeLevel == AcademicScopeLevel.PlatformWide ||
                     scope.ScopeLevel == AcademicScopeLevel.StageWide &&
                     scope.EducationStage == user.StudentProfile.EducationStage ||
                     scope.ScopeLevel == AcademicScopeLevel.GradeAllSubjects &&
                     scope.EducationStage == user.StudentProfile.EducationStage &&
                     scope.GradeLevel == user.StudentProfile.GradeLevel ||
                     scope.ScopeLevel == AcademicScopeLevel.Exact &&
                     scope.EducationStage == user.StudentProfile.EducationStage &&
                     scope.GradeLevel == user.StudentProfile.GradeLevel && scope.SubjectId != null &&
                     _db.AcademicSubjectEligibilities.Any(eligibility => eligibility.IsActive &&
                         eligibility.EducationStage == user.StudentProfile.EducationStage &&
                         eligibility.GradeLevel == user.StudentProfile.GradeLevel &&
                         eligibility.SubjectId == scope.SubjectId))));

        if (filters.Dto.HasActiveAccess.HasValue)
        {
            var now = DateTime.UtcNow;
            query = query.Where(user => _db.StudentAccessGrants.Any(grant =>
                grant.UserId == user.Id && grant.IsActive && grant.CancelledAt == null &&
                (grant.ExpiresAt == null || grant.ExpiresAt > now) &&
                (!filters.HasTargetScope ||
                 grant.PackageId != null && targetPackageIds.Contains(grant.PackageId.Value) ||
                 grant.TermId != null && _db.Terms.Any(term => term.Id == grant.TermId.Value &&
                     targetPackageIds.Contains(term.PackageId)) ||
                 grant.ContentSectionId != null && _db.ContentSections.Any(section =>
                     section.Id == grant.ContentSectionId.Value && targetPackageIds.Contains(section.Term.PackageId)) ||
                 grant.LessonId != null && _db.Lessons.Any(lesson => lesson.Id == grant.LessonId.Value &&
                     targetPackageIds.Contains(lesson.ContentSection.Term.PackageId)) ||
                 grant.LessonVideoId != null && _db.LessonVideos.Any(video =>
                     video.Id == grant.LessonVideoId.Value &&
                     targetPackageIds.Contains(video.Lesson.ContentSection.Term.PackageId)) ||
                 grant.ExamId != null && _db.Exams.Any(exam => exam.Id == grant.ExamId.Value &&
                     (exam.LessonVideoId != null && targetPackageIds.Contains(
                          exam.LessonVideo!.Lesson.ContentSection.Term.PackageId) ||
                      _db.Lessons.Any(lesson => lesson.ExamId == exam.Id &&
                          targetPackageIds.Contains(lesson.ContentSection.Term.PackageId)))))
            ) == filters.Dto.HasActiveAccess.Value);
        }

        if (filters.Dto.HasPaidPurchase.HasValue)
        {
            query = query.Where(user => _db.SalesFinancialEffects.Any(effect =>
                effect.StudentId == user.Id && effect.PaidAmount > 0 &&
                (!filters.HasTargetScope ||
                 effect.TargetType == SalesTargetType.Package && targetPackageIds.Contains(effect.TargetId) ||
                 effect.TargetType == SalesTargetType.Term && _db.Terms.Any(term =>
                     term.Id == effect.TargetId && targetPackageIds.Contains(term.PackageId)) ||
                 effect.TargetType == SalesTargetType.ContentSection && _db.ContentSections.Any(section =>
                     section.Id == effect.TargetId && targetPackageIds.Contains(section.Term.PackageId)) ||
                 effect.TargetType == SalesTargetType.Lesson && _db.Lessons.Any(lesson =>
                     lesson.Id == effect.TargetId && targetPackageIds.Contains(lesson.ContentSection.Term.PackageId)) ||
                 effect.TargetType == SalesTargetType.SpecificVideo && _db.LessonVideos.Any(video =>
                     video.Id == effect.TargetId && targetPackageIds.Contains(video.Lesson.ContentSection.Term.PackageId))) &&
                effect.CreatedAt >= filters.Dto.PurchaseFromUtc!.Value &&
                effect.CreatedAt < filters.Dto.PurchaseToUtc!.Value
            ) == filters.Dto.HasPaidPurchase.Value);
        }

        if (filters.Dto.HasWatched.HasValue)
            query = query.Where(user => _db.VideoPlaybackSessions.Any(session =>
                session.UserId == user.Id && session.HasRegisteredView && _db.LessonVideos.Any(video =>
                    video.Id == session.LessonVideoId && filters.LessonIds.Contains(video.LessonId)) &&
                (session.LastProgressAt ?? session.CreatedAt) >= filters.Dto.WatchFromUtc!.Value &&
                (session.LastProgressAt ?? session.CreatedAt) < filters.Dto.WatchToUtc!.Value
            ) == filters.Dto.HasWatched.Value);
        if (filters.Dto.HasExamAttempt.HasValue)
            query = query.Where(user => _db.StudentExamAttempts.Any(attempt =>
                attempt.UserId == user.Id && filters.ExamIds.Contains(attempt.ExamId) &&
                (attempt.StartedAt ?? attempt.CreatedAt) >= filters.Dto.ExamFromUtc!.Value &&
                (attempt.StartedAt ?? attempt.CreatedAt) < filters.Dto.ExamToUtc!.Value
            ) == filters.Dto.HasExamAttempt.Value);
        if (filters.Dto.HasHomeworkSubmission.HasValue)
            query = query.Where(user => _db.HomeworkSubmissions.Any(submission =>
                submission.StudentId == user.Id && filters.HomeworkIds.Contains(submission.HomeworkId) &&
                submission.SubmittedAt != null && submission.SubmittedAt >= filters.Dto.HomeworkFromUtc!.Value &&
                submission.SubmittedAt < filters.Dto.HomeworkToUtc!.Value
            ) == filters.Dto.HasHomeworkSubmission.Value);
        return query;
    }

    private sealed record NormalizedAudienceFilters(
        WhatsAppCampaignAudienceFilterDto Dto,
        string[] ContactRoles,
        EducationStage[] Stages,
        GradeLevel[] Grades,
        StudyTrack[] Tracks,
        CrmStatus[] CrmStatuses,
        Guid[] TeacherIds,
        Guid[] SubjectIds,
        Guid[] PackageIds,
        Guid[] LessonIds,
        Guid[] ExamIds,
        Guid[] HomeworkIds)
    {
        public bool HasTargetScope => TeacherIds.Length + SubjectIds.Length + PackageIds.Length +
            LessonIds.Length + ExamIds.Length + HomeworkIds.Length > 0;
    }

    private static NormalizedAudienceFilters NormalizeAndValidateFilters(WhatsAppCampaignAudienceFilterDto filters)
    {
        var contactRoles = Distinct(filters.ContactRoles ?? ["StudentPrimary"]);
        if (contactRoles.Length == 0) contactRoles = ["StudentPrimary"];
        if (contactRoles.Any(role => !ContactRoleWhitelist.Contains(role, StringComparer.Ordinal)))
            throw Invalid("نوع جهة اتصال غير مدعوم في الجمهور.");
        var stages = ParseEnums<EducationStage>(filters.EducationStages);
        var grades = ParseEnums<GradeLevel>(filters.GradeLevels);
        var tracks = ParseEnums<StudyTrack>(filters.StudyTracks);
        var crm = ParseEnums<CrmStatus>(filters.CrmStatuses);
        var teacherIds = ValidIds(filters.TeacherIds);
        var subjectIds = ValidIds(filters.SubjectIds);
        var packageIds = ValidIds(filters.PackageIds);
        var lessonIds = ValidIds(filters.LessonIds);
        var examIds = ValidIds(filters.ExamIds);
        var homeworkIds = ValidIds(filters.HomeworkIds);
        var hasAcademicBase = stages.Length + grades.Length + tracks.Length + teacherIds.Length +
            subjectIds.Length + packageIds.Length + lessonIds.Length + examIds.Length +
            homeworkIds.Length > 0;
        if (filters.HasActiveAccess == false && !hasAcademicBase)
            throw Invalid("فلتر ليس لديه صلاحية يحتاج جمهورًا أكاديميًا محددًا.");
        ValidateActivity("المشاهدة", filters.HasWatched, lessonIds, filters.WatchFromUtc, filters.WatchToUtc, hasAcademicBase);
        ValidateActivity("الامتحان", filters.HasExamAttempt, examIds, filters.ExamFromUtc, filters.ExamToUtc, hasAcademicBase);
        ValidateActivity("الواجب", filters.HasHomeworkSubmission, homeworkIds, filters.HomeworkFromUtc, filters.HomeworkToUtc, hasAcademicBase);
        if (filters.HasPaidPurchase.HasValue)
        {
            if (!filters.HasPaidPurchase.Value && !hasAcademicBase)
                throw Invalid("فلتر لم يشترِ يحتاج مرحلة أو صفًا أو شعبة أكاديمية محددة.");
            ValidateRange("الشراء", filters.PurchaseFromUtc, filters.PurchaseToUtc, required: true);
        }
        else if (filters.PurchaseFromUtc.HasValue || filters.PurchaseToUtc.HasValue)
            throw Invalid("نطاق تاريخ الشراء يحتاج اختيار حالة الشراء المدفوع.");
        return new NormalizedAudienceFilters(filters, contactRoles, stages, grades, tracks, crm,
            teacherIds, subjectIds, packageIds, lessonIds, examIds, homeworkIds);
    }

    private async Task<Guid[]> ResolveTargetPackageIdsAsync(
        NormalizedAudienceFilters filters,
        CancellationToken ct)
    {
        if (!filters.HasTargetScope) return [];
        var query = _db.Packages.AsNoTracking().Where(package => package.IsActive &&
            package.ArchiveMode != ContentArchiveMode.HiddenFromEveryone &&
            package.Teacher.User.IsActive && !package.Teacher.User.IsDeleted);
        if (filters.PackageIds.Length > 0)
            query = query.Where(package => filters.PackageIds.Contains(package.Id));
        if (filters.TeacherIds.Length > 0)
            query = query.Where(package => filters.TeacherIds.Contains(package.TeacherId));
        if (filters.SubjectIds.Length > 0)
            query = query.Where(package => filters.SubjectIds.Contains(package.SubjectId));
        if (filters.LessonIds.Length > 0)
            query = query.Where(package => _db.Lessons.Any(lesson =>
                filters.LessonIds.Contains(lesson.Id) &&
                lesson.ArchiveMode != ContentArchiveMode.HiddenFromEveryone &&
                lesson.ContentSection.Term.PackageId == package.Id));
        if (filters.ExamIds.Length > 0)
            query = query.Where(package => _db.Exams.Any(exam =>
                filters.ExamIds.Contains(exam.Id) &&
                exam.IsActive && exam.ArchiveMode != ContentArchiveMode.HiddenFromEveryone &&
                (exam.LessonVideoId != null &&
                 exam.LessonVideo!.Lesson.ContentSection.Term.PackageId == package.Id ||
                 _db.Lessons.Any(lesson => lesson.ExamId == exam.Id &&
                     lesson.ContentSection.Term.PackageId == package.Id))));
        if (filters.HomeworkIds.Length > 0)
            query = query.Where(package => _db.Homeworks.Any(homework =>
                filters.HomeworkIds.Contains(homework.Id) &&
                homework.IsActive && homework.ArchiveMode != ContentArchiveMode.HiddenFromEveryone &&
                _db.Lessons.Any(lesson => lesson.Id == homework.LessonId &&
                    lesson.ContentSection.Term.PackageId == package.Id)));
        return await query.Select(package => package.Id).ToArrayAsync(ct);
    }

    private static void ValidateVariableMappings(
        IReadOnlyList<WhatsAppCampaignVariableMappingDto> mappings,
        NormalizedAudienceFilters filters,
        string templateCategory)
    {
        foreach (var mapping in mappings)
        {
            var source = mapping.Source.Trim().ToUpperInvariant();
            switch (source)
            {
                case "LITERAL":
                    if (string.IsNullOrWhiteSpace(mapping.LiteralValue) || mapping.ReferenceId.HasValue)
                        throw Invalid("القيمة الثابتة لمتغير القالب غير صالحة.");
                    break;
                case "STUDENTFIRSTNAME":
                case "STUDENTFULLNAME":
                case "EDUCATIONSTAGE":
                case "GRADELEVEL":
                case "STUDYTRACK":
                case "GOVERNORATE":
                case "SCHOOLNAME":
                    if (mapping.ReferenceId.HasValue || mapping.LiteralValue is not null)
                        throw Invalid("مصدر متغير الطالب لا يقبل قيمة أو مرجعًا إضافيًا.");
                    break;
                case "PARENTTRACKINGCODE":
                    if (!string.Equals(templateCategory, "UTILITY", StringComparison.OrdinalIgnoreCase))
                        throw Invalid("رقم متابعة الطالب متاح لقوالب الخدمة فقط.");
                    if (mapping.ReferenceId.HasValue || mapping.LiteralValue is not null)
                        throw Invalid("مصدر متغير الطالب لا يقبل قيمة أو مرجعًا إضافيًا.");
                    break;
                case "TEACHERNAME":
                case "SUBJECTNAME":
                case "PACKAGENAME":
                case "LESSONNAME":
                    if (!mapping.ReferenceId.HasValue || mapping.LiteralValue is not null)
                        throw Invalid("مصدر المحتوى يحتاج مرجعًا واحدًا صحيحًا.");
                    break;
                case "PURCHASEDATE":
                    if (!mapping.ReferenceId.HasValue || mapping.LiteralValue is not null ||
                        filters.Dto.HasPaidPurchase != true ||
                        filters.PackageIds.Length != 1 ||
                        filters.PackageIds[0] != mapping.ReferenceId.Value)
                        throw Invalid("تاريخ الشراء يحتاج شراءً مدفوعًا وباقة محددة مطابقة.");
                    break;
                default:
                    throw Invalid("مصدر متغير القالب غير مدعوم.");
            }
        }
    }

    private async Task<IReadOnlyDictionary<(string Source, Guid Id), string>> LoadReferenceValuesAsync(
        IReadOnlyList<WhatsAppCampaignVariableMappingDto> mappings,
        CancellationToken ct)
    {
        var referenceValues = new Dictionary<(string Source, Guid Id), string>();
        foreach (var sourceGroup in mappings.Where(mapping => mapping.ReferenceId.HasValue)
                     .GroupBy(mapping => mapping.Source.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            var ids = sourceGroup.Select(mapping => mapping.ReferenceId!.Value).Distinct().ToArray();
            IReadOnlyList<(Guid Id, string Value)> rows = sourceGroup.Key.ToUpperInvariant() switch
            {
                "TEACHERNAME" => await _db.TeacherProfiles.AsNoTracking().Where(item => ids.Contains(item.Id))
                    .Select(item => new ValueTuple<Guid, string>(item.Id, item.User.FullName)).ToListAsync(ct),
                "SUBJECTNAME" => await _db.Subjects.AsNoTracking().Where(item => ids.Contains(item.Id))
                    .Select(item => new ValueTuple<Guid, string>(item.Id, item.Name)).ToListAsync(ct),
                "PACKAGENAME" => await _db.Packages.AsNoTracking().Where(item => ids.Contains(item.Id))
                    .Select(item => new ValueTuple<Guid, string>(item.Id, item.Name)).ToListAsync(ct),
                "LESSONNAME" => await _db.Lessons.AsNoTracking().Where(item => ids.Contains(item.Id))
                    .Select(item => new ValueTuple<Guid, string>(item.Id, item.Title)).ToListAsync(ct),
                "PURCHASEDATE" => [],
                _ => throw Invalid("مصدر متغير القالب غير مدعوم.")
            };
            foreach (var row in rows)
                referenceValues[(sourceGroup.Key.ToUpperInvariant(), row.Id)] = row.Value;
        }
        return referenceValues;
    }

    private async Task<IReadOnlyDictionary<(Guid StudentId, Guid PackageId), DateTime>> LoadPurchaseDatesAsync(
        Guid[] studentIds,
        IReadOnlyList<WhatsAppCampaignVariableMappingDto> mappings,
        NormalizedAudienceFilters filters,
        CancellationToken ct)
    {
        var packageIds = mappings.Where(mapping =>
                string.Equals(mapping.Source, "PurchaseDate", StringComparison.OrdinalIgnoreCase) &&
                mapping.ReferenceId.HasValue)
            .Select(mapping => mapping.ReferenceId!.Value).Distinct().ToArray();
        if (packageIds.Length == 0 || studentIds.Length == 0)
            return new Dictionary<(Guid, Guid), DateTime>();
        var terms = await _db.Terms.AsNoTracking().Where(item => packageIds.Contains(item.PackageId))
            .Select(item => new { item.Id, item.PackageId }).ToListAsync(ct);
        var sections = await _db.ContentSections.AsNoTracking()
            .Where(item => packageIds.Contains(item.Term.PackageId))
            .Select(item => new { item.Id, PackageId = item.Term.PackageId }).ToListAsync(ct);
        var lessons = await _db.Lessons.AsNoTracking()
            .Where(item => packageIds.Contains(item.ContentSection.Term.PackageId))
            .Select(item => new { item.Id, PackageId = item.ContentSection.Term.PackageId }).ToListAsync(ct);
        var videos = await _db.LessonVideos.AsNoTracking()
            .Where(item => packageIds.Contains(item.Lesson.ContentSection.Term.PackageId))
            .Select(item => new { item.Id, PackageId = item.Lesson.ContentSection.Term.PackageId }).ToListAsync(ct);
        var termIds = terms.Select(item => item.Id).ToArray();
        var sectionIds = sections.Select(item => item.Id).ToArray();
        var lessonIds = lessons.Select(item => item.Id).ToArray();
        var videoIds = videos.Select(item => item.Id).ToArray();
        var effects = await _db.SalesFinancialEffects.AsNoTracking()
            .Where(effect => studentIds.Contains(effect.StudentId) && effect.PaidAmount > 0 &&
                effect.CreatedAt >= filters.Dto.PurchaseFromUtc!.Value &&
                effect.CreatedAt < filters.Dto.PurchaseToUtc!.Value &&
                (effect.TargetType == SalesTargetType.Package && packageIds.Contains(effect.TargetId) ||
                 effect.TargetType == SalesTargetType.Term && termIds.Contains(effect.TargetId) ||
                 effect.TargetType == SalesTargetType.ContentSection && sectionIds.Contains(effect.TargetId) ||
                 effect.TargetType == SalesTargetType.Lesson && lessonIds.Contains(effect.TargetId) ||
                 effect.TargetType == SalesTargetType.SpecificVideo && videoIds.Contains(effect.TargetId)))
            .Select(effect => new { effect.StudentId, effect.TargetType, effect.TargetId, effect.CreatedAt })
            .ToListAsync(ct);
        var termPackages = terms.ToDictionary(item => item.Id, item => item.PackageId);
        var sectionPackages = sections.ToDictionary(item => item.Id, item => item.PackageId);
        var lessonPackages = lessons.ToDictionary(item => item.Id, item => item.PackageId);
        var videoPackages = videos.ToDictionary(item => item.Id, item => item.PackageId);
        Guid PackageFor(SalesTargetType type, Guid id) => type switch
        {
            SalesTargetType.Package when packageIds.Contains(id) => id,
            SalesTargetType.Term when termPackages.TryGetValue(id, out var termPackageId) => termPackageId,
            SalesTargetType.ContentSection when sectionPackages.TryGetValue(id, out var sectionPackageId) => sectionPackageId,
            SalesTargetType.Lesson when lessonPackages.TryGetValue(id, out var lessonPackageId) => lessonPackageId,
            SalesTargetType.SpecificVideo when videoPackages.TryGetValue(id, out var videoPackageId) => videoPackageId,
            _ => Guid.Empty
        };
        var rows = effects.Select(effect => new
            {
                effect.StudentId,
                PackageId = PackageFor(effect.TargetType, effect.TargetId),
                effect.CreatedAt
            })
            .Where(item => item.PackageId != Guid.Empty);
        return rows.GroupBy(item => (item.StudentId, item.PackageId))
            .ToDictionary(group => group.Key, group => group.Max(item => item.CreatedAt));
    }

    private static IReadOnlyDictionary<WhatsAppTemplateParameterKey, string> MaskPreviewValues(
        IReadOnlyList<WhatsAppCanonicalVariableMapping> mappings,
        IReadOnlyDictionary<WhatsAppTemplateParameterKey, string> resolved)
    {
        var preview = new Dictionary<WhatsAppTemplateParameterKey, string>(resolved);
        foreach (var canonicalMapping in mappings)
        {
            var mapping = canonicalMapping.Mapping;
            var key = canonicalMapping.Requirement.Key;
            preview[key] = mapping.Source.Trim().ToUpperInvariant() switch
            {
                "STUDENTFIRSTNAME" => "اسم الطالب",
                "STUDENTFULLNAME" => "اسم الطالب المحجوب",
                "EDUCATIONSTAGE" => "المرحلة",
                "GRADELEVEL" => "الصف",
                "STUDYTRACK" => "الشعبة",
                "GOVERNORATE" => "المحافظة",
                "SCHOOLNAME" => "المدرسة",
                "PARENTTRACKINGCODE" => "رقم متابعة محجوب",
                "PURCHASEDATE" => "تاريخ الشراء",
                _ => preview[key]
            };
        }
        return preview;
    }

    private static IReadOnlyDictionary<WhatsAppTemplateParameterKey, string> ResolveVariableValues(
        AudienceStudentRow student,
        IReadOnlyList<WhatsAppCanonicalVariableMapping> mappings,
        IReadOnlyDictionary<(string Source, Guid Id), string> references,
        IReadOnlyDictionary<(Guid StudentId, Guid PackageId), DateTime> purchaseDates)
    {
        var resolvedValues = new Dictionary<WhatsAppTemplateParameterKey, string>();
        foreach (var canonicalMapping in mappings)
        {
            var mapping = canonicalMapping.Mapping;
            var source = mapping.Source.Trim().ToUpperInvariant();
            var value = source switch
            {
                "LITERAL" => mapping.LiteralValue,
                "STUDENTFIRSTNAME" => student.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(),
                "STUDENTFULLNAME" => student.FullName,
                "EDUCATIONSTAGE" => student.EducationStage?.ToString(),
                "GRADELEVEL" => student.GradeLevel?.ToString(),
                "STUDYTRACK" => student.StudyTrack?.ToString(),
                "GOVERNORATE" => student.Governorate,
                "SCHOOLNAME" => student.SchoolName,
                "PARENTTRACKINGCODE" => student.ParentTrackingCode,
                "TEACHERNAME" or "SUBJECTNAME" or "PACKAGENAME" or "LESSONNAME" =>
                    mapping.ReferenceId.HasValue && references.TryGetValue((source, mapping.ReferenceId.Value), out var reference)
                        ? reference : null,
                "PURCHASEDATE" => mapping.ReferenceId.HasValue &&
                    purchaseDates.TryGetValue((student.StudentUserId, mapping.ReferenceId.Value), out var date)
                        ? FormatDate(date, mapping.Format) : null,
                _ => throw Invalid("مصدر متغير القالب غير مدعوم.")
            };
            resolvedValues[canonicalMapping.Requirement.Key] = SafeVariable(value);
        }
        return resolvedValues;
    }

    private static string SafeVariable(string? value)
    {
        var normalized = value?.Normalize(NormalizationForm.FormC).Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > MaximumVariableLength ||
            normalized.Any(char.IsControl) ||
            normalized.Contains("http://", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("https://", StringComparison.OrdinalIgnoreCase))
            throw new MissingCampaignVariableException();
        return normalized;
    }

    private static string FormatDate(DateTime value, string? format) => (format?.Trim()) switch
    {
        null or "" or "dd/MM/yyyy" => CairoTime.ToLocal(value)
            .ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
        "yyyy-MM-dd" => CairoTime.ToLocal(value)
            .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        _ => throw Invalid("صيغة تاريخ متغير القالب غير مدعومة.")
    };

    private static void ValidateActivity(
        string label,
        bool? condition,
        Guid[] scopedIds,
        DateTime? from,
        DateTime? to,
        bool hasAcademicBase)
    {
        if (!condition.HasValue)
        {
            if (scopedIds.Length > 0 || from.HasValue || to.HasValue)
                throw Invalid($"نطاق {label} يحتاج اختيار الحالة.");
            return;
        }
        if (scopedIds.Length == 0) throw Invalid($"فلتر {label} يحتاج عنصرًا محددًا.");
        if (!condition.Value && !hasAcademicBase)
            throw Invalid($"فلتر لم يتم {label} يحتاج جمهورًا أكاديميًا محددًا أولًا.");
        ValidateRange(label, from, to, required: true);
    }

    private static void ValidateRange(string label, DateTime? from, DateTime? to, bool required)
    {
        if (required && (!from.HasValue || !to.HasValue))
            throw Invalid($"فلتر {label} يحتاج تاريخ بداية ونهاية.");
        if (!from.HasValue && !to.HasValue) return;
        if (!from.HasValue || !to.HasValue || from.Value >= to.Value ||
            to.Value - from.Value > TimeSpan.FromDays(366))
            throw Invalid($"نطاق تاريخ {label} غير صالح أو يتجاوز سنة.");
    }

    private static T[] ParseEnums<T>(IReadOnlyList<string>? values) where T : struct, Enum
    {
        if (values is null || values.Count == 0) return [];
        var parsedValues = new List<T>();
        foreach (var value in values.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Enum.TryParse<T>(value, true, out var parsed) || !Enum.IsDefined(parsed))
                throw Invalid("قيمة فلتر أكاديمي غير صالحة.");
            parsedValues.Add(parsed);
        }
        return parsedValues.ToArray();
    }

    private static Guid[] ValidIds(IReadOnlyList<Guid>? values)
    {
        var ids = values?.Distinct().ToArray() ?? [];
        if (ids.Any(id => id == Guid.Empty) || ids.Length > 500)
            throw Invalid("قائمة معرفات الفلتر غير صالحة أو كبيرة جدًا.");
        return ids;
    }

    private static string[] Distinct(IReadOnlyList<string> values) => values
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value.Trim()).Distinct(StringComparer.Ordinal).ToArray();

    private static string? ContactPhone(AudienceStudentRow student, string role) => role switch
    {
        "StudentPrimary" => student.PrimaryPhone,
        "StudentSecondary" => student.SecondaryPhone,
        "FatherPrimary" => student.FatherPhone,
        "FatherSecondary" => student.FatherSecondaryPhone,
        "Mother" => student.MotherPhone,
        _ => null
    };

    private static bool PreferenceIsAtLeastAsRecent(
        WhatsAppContactPreference candidate,
        WhatsAppContactPreference baseline)
    {
        if (candidate.EffectiveAt != baseline.EffectiveAt)
            return candidate.EffectiveAt > baseline.EffectiveAt;
        if (candidate.CreatedAt != baseline.CreatedAt)
            return candidate.CreatedAt > baseline.CreatedAt;
        return candidate.State == WhatsAppContactPreferenceState.OptedOut || candidate.Id.CompareTo(baseline.Id) >= 0;
    }

    private static Dictionary<string, int> NewExclusionCounts() => new(StringComparer.Ordinal)
    {
        ["no_phone"] = 0,
        ["invalid_phone"] = 0,
        ["duplicate_or_ambiguous_phone"] = 0,
        ["opted_out"] = 0,
        ["no_consent"] = 0,
        ["missing_variable"] = 0
    };

    private static void Increment(IDictionary<string, int> counts, string reason) => Add(counts, reason, 1);
    private static void Add(IDictionary<string, int> counts, string reason, int amount)
    {
        if (amount <= 0) return;
        counts.TryGetValue(reason, out var currentCount);
        counts[reason] = currentCount + amount;
    }

    private int BoundedConfigurationInt(string configurationKey, int defaultValue, int minimum, int maximum)
    {
        var configuredText = _configuration[configurationKey];
        var effectiveValue = int.TryParse(
            configuredText,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var configuredValue)
            ? configuredValue
            : defaultValue;
        return Math.Clamp(effectiveValue, minimum, maximum);
    }

    private static string MaskName(string name)
    {
        var trimmed = name.Trim();
        return trimmed.Length == 0 ? "***" : $"{trimmed[0]}***";
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string ConfirmationPhrase(int count) => $"إرسال {count} رسالة واتساب";

    private static WhatsAppCampaignDraftDto CampaignDraftDto(WhatsAppCampaign campaign, string reviewToken)
    {
        using var document = JsonDocument.Parse(campaign.TemplateComponentsJson);
        return new WhatsAppCampaignDraftDto(
            campaign.Id,
            campaign.Version,
            campaign.Status.ToString(),
            campaign.RecipientCount,
            campaign.ExcludedCount,
            new WhatsAppCampaignTemplateSnapshotDto(
                campaign.TemplateId,
                campaign.TemplateName,
                campaign.TemplateLanguage,
                campaign.TemplateCategory,
                campaign.TemplateFingerprint,
                document.RootElement.Clone()),
            reviewToken,
            ConfirmationPhrase(campaign.RecipientCount),
            campaign.ReviewTokenExpiresAt);
    }

    private sealed class MissingCampaignVariableException : Exception;
}
