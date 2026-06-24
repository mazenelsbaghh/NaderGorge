using System.Data;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MediatR;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Infrastructure.Data;
using Microsoft.Extensions.Logging;
using NaderGorge.Application.Features.LiveSupport.Services;
using NaderGorge.Application.Features.Exams.Commands;
using NaderGorge.Application.Features.Admin.Commands;

using NaderGorge.Application.Interfaces;
using NaderGorge.Application.Features.LiveSupportAI.Interfaces;
using NaderGorge.Application.Features.LiveSupportAI.Commands;
using NaderGorge.Application.Features.LiveSupportAI.Dtos;

namespace NaderGorge.Infrastructure.Services;

public sealed class LiveSupportService(
    IAppDbContext db,
    ICachedPlatformSettingsReader settings,
    ILiveSupportPresenceStore? presence = null,
    ILiveSupportAttachmentStorage? attachmentStorage = null,
    ILogger<LiveSupportService>? logger = null,
    IJobEnqueuer? jobEnqueuer = null,
    IMediator? mediator = null,
    ILiveSupportAITurnOrchestrator? aiTurnOrchestrator = null,
    ILiveSupportAIHandoffService? handoffService = null,
    NaderGorge.Application.Features.LiveSupportAI.Interfaces.ILiveSupportAIVerificationService? aiVerificationService = null,
    NaderGorge.Application.Features.LiveSupportAI.Interfaces.ILiveSupportAIRegistrationService? aiRegistrationService = null) : ILiveSupportService, ILiveSupportAssignmentCoordinator
{
    private readonly IAppDbContext _db = db;
    private readonly ICachedPlatformSettingsReader _settings = settings;
    private readonly AppDbContext? _relationalDb = db as AppDbContext;
    public Task AssignWaitingAsync(CancellationToken ct) => AssignOldestWaitingAsync(ct);
    private readonly ILiveSupportPresenceStore? _presence = presence;
    private readonly ILiveSupportAttachmentStorage? _attachmentStorage = attachmentStorage;
    private readonly ILogger<LiveSupportService>? _logger = logger;
    private readonly IJobEnqueuer? _jobEnqueuer = jobEnqueuer;
    private readonly IMediator? _mediator = mediator;
    private readonly ILiveSupportAITurnOrchestrator? _aiTurnOrchestrator = aiTurnOrchestrator;
    private readonly ILiveSupportAIHandoffService? _handoffServiceInput = handoffService;
    private readonly NaderGorge.Application.Features.LiveSupportAI.Interfaces.ILiveSupportAIVerificationService? _aiVerificationService = aiVerificationService;
    private readonly NaderGorge.Application.Features.LiveSupportAI.Interfaces.ILiveSupportAIRegistrationService? _aiRegistrationService = aiRegistrationService;
    private ILiveSupportAIHandoffService? _handoffServiceBacking;
    private ILiveSupportAIHandoffService _handoffService => _handoffServiceBacking ??= (_handoffServiceInput ?? new NaderGorge.Infrastructure.Services.LiveSupportAI.LiveSupportAIHandoffService(_db, this));


    public async Task<LiveSupportAvailabilityDto> GetAvailabilityAsync(CancellationToken ct)
    {
        if (!(await _settings.GetAsync(ct)).LiveSupportEnabled)
            return new LiveSupportAvailabilityDto(false, 0, null, LiveSupportErrorCodes.SupportUnavailable, "الدعم المباشر غير مفعّل حاليًا.");
        var staffIds = await EligibleStaffQuery().Select(x => x.UserId).ToListAsync(ct);
        var staff = 0;
        foreach (var id in staffIds) if (_presence is null || await _presence.IsConnectedAsync(id)) staff++;
        var aiActive = await _db.LiveSupportAIPolicyVersions.AnyAsync(x => x.Status == LiveSupportAIPolicyStatus.Published && x.IsEnabled, ct);
        var isAvailable = staff > 0 || aiActive;
        var next = isAvailable ? null : await GetNextScheduleAsync(ct);
        return new LiveSupportAvailabilityDto(
            isAvailable,
            staff,
            next,
            isAvailable ? "AVAILABLE" : LiveSupportErrorCodes.SupportUnavailable,
            isAvailable ? "الدعم متاح الآن" : next.HasValue
                ? $"الدعم غير متاح الآن. الموعد القادم {next.Value:yyyy-MM-dd HH:mm}"
                : "الدعم غير متاح حاليًا، وسيظهر الموعد هنا عند تحديده من الإدارة.");
    }


    public async Task<IReadOnlyList<LiveSupportConversationDto>> ListParticipantConversationsAsync(LiveSupportParticipantIdentity participant, CancellationToken ct)
    {
        var items = await ParticipantQuery(participant).OrderByDescending(x => x.CreatedAt).Take(50).ToListAsync(ct);
        return await MapManyAsync(items, ct);
    }

    public async Task<LiveSupportConversationDto> CreateConversationAsync(LiveSupportParticipantIdentity participant, string? subject, Guid? previousConversationId, CancellationToken ct)
    {
        await using var tx = await _db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await AcquireRoutingLockAsync(ct);
        var availability = await GetAvailabilityAsync(ct);
        if (!availability.IsAvailable)
            throw new LiveSupportException(LiveSupportErrorCodes.SupportUnavailable, availability.Message);
        if (await ParticipantQuery(participant).AnyAsync(x => x.Status == LiveSupportConversationStatus.Waiting || x.Status == LiveSupportConversationStatus.Assigned || x.Status == LiveSupportConversationStatus.Active, ct))
            throw new LiveSupportException(LiveSupportErrorCodes.OpenConversationExists, "لديك محادثة مفتوحة بالفعل.");

        var now = DateTime.UtcNow;
        var conversation = new LiveSupportConversation
        {
            ParticipantType = participant.Type,
            StudentUserId = participant.StudentUserId,
            GuestSessionId = participant.GuestSessionId,
            LinkedStudentUserId = participant.StudentUserId,
            PreviousConversationId = previousConversationId,
            Status = LiveSupportConversationStatus.Waiting,
            Subject = string.IsNullOrWhiteSpace(subject) ? null : subject.Trim()[..Math.Min(subject.Trim().Length, 200)],
            QueuedAt = now,
            LastMessageAt = now,
            Version = 1
        };
        _db.LiveSupportConversations.Add(conversation);

        var aiPolicy = await _db.LiveSupportAIPolicyVersions.FirstOrDefaultAsync(x => x.Status == LiveSupportAIPolicyStatus.Published && x.IsEnabled, ct);
        var aiActive = aiPolicy is not null;

        if (aiActive)
        {
            var aiState = new LiveSupportAIConversationState
            {
                ConversationId = conversation.Id,
                Mode = LiveSupportAIMode.AiActive,
                PolicyVersionId = aiPolicy!.Id,
                LastParticipantActivityAt = now,
                Version = 1
            };
            _db.LiveSupportAIConversationStates.Add(aiState);
            AddEvent(conversation.Id, LiveSupportEventType.ConversationCreated, participant.StudentUserId, participant.GuestSessionId);
            await _db.SaveChangesAsync(ct);
        }
        else
        {
            _db.LiveSupportQueueEntries.Add(new LiveSupportQueueEntry { ConversationId = conversation.Id, EnteredAt = now, Sequence = now.Ticks });
            AddEvent(conversation.Id, LiveSupportEventType.ConversationCreated, participant.StudentUserId, participant.GuestSessionId);
            AddEvent(conversation.Id, LiveSupportEventType.QueueEntered, participant.StudentUserId, participant.GuestSessionId);
            await _db.SaveChangesAsync(ct);
            await AssignOldestWaitingAsync(ct);
        }

        await tx.CommitAsync(ct);

        if (!string.IsNullOrWhiteSpace(subject))
        {
            var senderType = participant.Type == LiveSupportParticipantType.Student ? LiveSupportSenderType.Student : LiveSupportSenderType.Guest;
            var clientMessageId = $"init-{Guid.NewGuid():N}";
            await SendMessageAsync(conversation, senderType, participant.StudentUserId, participant.GuestSessionId, clientMessageId, subject, LiveSupportMessageType.Text, ct);
        }
        _logger?.LogInformation("LiveSupport conversation {ConversationId} routed status {Status} owner {OwnerUserId}", conversation.Id, conversation.Status, conversation.CurrentOwnerUserId);
        LiveSupportTelemetry.ConversationsCreated.Add(1, new KeyValuePair<string, object?>("status", conversation.Status.ToString()));
        return await MapAsync(conversation, ct);
    }

    public async Task<LiveSupportConversationDto?> GetParticipantConversationAsync(LiveSupportParticipantIdentity participant, Guid conversationId, CancellationToken ct)
    {
        var item = await ParticipantQuery(participant).FirstOrDefaultAsync(x => x.Id == conversationId, ct);
        return item is null ? null : await MapAsync(item, ct);
    }

    public async Task<IReadOnlyList<LiveSupportMessageDto>> GetParticipantMessagesAsync(LiveSupportParticipantIdentity participant, Guid conversationId, int pageSize, CancellationToken ct)
    {
        await RequireParticipantConversationAsync(participant, conversationId, ct);
        return await _db.LiveSupportMessages.Where(x => x.ConversationId == conversationId)
            .OrderByDescending(x => x.SentAt).Take(Math.Clamp(pageSize, 1, 100)).OrderBy(x => x.SentAt)
            .Select(x => ToDto(x)).ToListAsync(ct);
    }

    public async Task<LiveSupportMessagePageDto> GetParticipantMessagePageAsync(LiveSupportParticipantIdentity participant, Guid conversationId, int pageSize, string? cursor, long? afterSequence, CancellationToken ct)
    {
        await RequireParticipantConversationAsync(participant, conversationId, ct);
        var take = Math.Clamp(pageSize, 1, 100);
        var query = _db.LiveSupportMessages.AsNoTracking().Where(x => x.ConversationId == conversationId);
        if (TryDecodeCursor(cursor, out var sentAt, out var id)) query = query.Where(x => x.SentAt < sentAt || x.SentAt == sentAt && x.Id.CompareTo(id) < 0);
        var rows = await query.OrderByDescending(x => x.SentAt).ThenByDescending(x => x.Id).Take(take + 1).ToListAsync(ct);
        var hasMore = rows.Count > take;
        if (hasMore) rows.RemoveAt(rows.Count - 1);
        var next = hasMore && rows.Count > 0 ? EncodeCursor(rows[^1].SentAt, rows[^1].Id) : null;
        var eventQuery = _db.LiveSupportEvents.AsNoTracking().Where(x => x.ConversationId == conversationId);
        if (afterSequence.HasValue) eventQuery = eventQuery.Where(x => x.Sequence > afterSequence.Value);
        var events = await eventQuery.OrderBy(x => x.Sequence).Take(250).Select(x => new LiveSupportTimelineItemDto(x.OccurredAt, x.Type.ToString(), null, x.Type.ToString(), x.SafeMetadataJson)).ToListAsync(ct);
        var lastSequence = await _db.LiveSupportEvents.Where(x => x.ConversationId == conversationId).MaxAsync(x => (long?)x.Sequence, ct) ?? 0;
        rows.Reverse();
        return new(rows.Select(ToDto).ToList(), next, lastSequence, events);
    }

    public async Task<LiveSupportAttachmentDto> SaveParticipantAttachmentAsync(LiveSupportParticipantIdentity participant, Guid conversationId, Stream content, string fileName, string contentType, long sizeBytes, CancellationToken ct)
    {
        var conversation = await RequireParticipantConversationAsync(participant, conversationId, ct);
        if (IsTerminal(conversation.Status)) throw new LiveSupportException(LiveSupportErrorCodes.ConversationTerminal, "المحادثة مغلقة.");
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/webp", "application/pdf", "audio/mpeg", "audio/mp4", "audio/ogg" };
        if (!allowed.Contains(contentType) || sizeBytes is <= 0 or > 10 * 1024 * 1024) throw new LiveSupportException("VALIDATION_ERROR", "نوع الملف غير مدعوم أو حجمه أكبر من 10 ميجابايت.");
        if (_attachmentStorage is null) throw new LiveSupportException("ATTACHMENT_STORAGE_UNAVAILABLE", "رفع الملفات غير متاح مؤقتًا.");
        var stored = await _attachmentStorage.SaveAsync(content, fileName, contentType, sizeBytes, ct);
        var entity = new LiveSupportAttachment { StoragePath = stored.StoragePath, OriginalFileName = stored.OriginalFileName, ContentType = stored.ContentType, SizeBytes = stored.SizeBytes, Sha256 = stored.Sha256, UploadedByIdentity = participant.StudentUserId?.ToString("N") ?? participant.GuestSessionId!.Value.ToString("N") };
        _db.LiveSupportAttachments.Add(entity);
        await _db.SaveChangesAsync(ct);
        _logger?.LogInformation("LiveSupport attachment {AttachmentId} stored for conversation {ConversationId}; bytes {SizeBytes}; content type {ContentType}", entity.Id, conversationId, entity.SizeBytes, entity.ContentType);
        LiveSupportTelemetry.AttachmentBytes.Record(entity.SizeBytes, new KeyValuePair<string, object?>("content_type", entity.ContentType));
        return new(entity.Id, entity.OriginalFileName, entity.ContentType, entity.SizeBytes, $"/api/live-support/participant/conversations/{conversationId}/attachments/{entity.Id}");
    }

    public async Task<LiveSupportAttachmentDownloadDto> OpenParticipantAttachmentAsync(LiveSupportParticipantIdentity participant, Guid conversationId, Guid attachmentId, CancellationToken ct)
    {
        await RequireParticipantConversationAsync(participant, conversationId, ct);
        var identity = participant.StudentUserId?.ToString("N") ?? participant.GuestSessionId!.Value.ToString("N");
        var attachment = await _db.LiveSupportAttachments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == attachmentId && !x.IsBlocked, ct);
        var linked = await _db.LiveSupportMessages.AnyAsync(x => x.ConversationId == conversationId && x.AttachmentId == attachmentId, ct);
        if (attachment is null || (!linked && attachment.UploadedByIdentity != identity)) throw new LiveSupportException("NOT_FOUND", "الملف غير موجود.");
        if (_attachmentStorage is null) throw new LiveSupportException("ATTACHMENT_STORAGE_UNAVAILABLE", "الملف غير متاح مؤقتًا.");
        return new(await _attachmentStorage.OpenReadAsync(attachment.StoragePath, ct), attachment.OriginalFileName, attachment.ContentType, attachment.SizeBytes);
    }

    public async Task<LiveSupportConversationDto> AdminInterveneAsync(Guid adminUserId, Guid conversationId, string operation, Guid? targetStaffUserId, string reason, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 3) throw new LiveSupportException("VALIDATION_ERROR", "سبب تدخل الإدارة مطلوب.");
        return operation.Trim().ToLowerInvariant() switch
        {
            "close" => await CloseAsync(adminUserId, true, conversationId, $"[ADMIN] {reason.Trim()}", ct),
            "reassign" or "transfer" => await TransferAsync(adminUserId, true, conversationId, targetStaffUserId, $"[ADMIN] {reason.Trim()}", ct),
            "queue" => await TransferAsync(adminUserId, true, conversationId, null, $"[ADMIN] {reason.Trim()}", ct),
            "abandon" => await AdminAbandonAsync(adminUserId, conversationId, reason, ct),
            _ => throw new LiveSupportException("VALIDATION_ERROR", "نوع التدخل غير صحيح.")
        };
    }

    private async Task<LiveSupportConversationDto> AdminAbandonAsync(Guid actor, Guid conversationId, string reason, CancellationToken ct)
    {
        var conversation = await RequireStaffConversationAsync(actor, true, conversationId, ct);
        await FinishConversationAsync(conversation, actor, LiveSupportConversationStatus.Abandoned, $"[ADMIN] {reason.Trim()}", LiveSupportAssignmentEndReason.AdminReassignment, ct);
        return await MapAsync(conversation, ct);
    }

    public async Task<LiveSupportSendResultDto> SendParticipantMessageAsync(LiveSupportParticipantIdentity participant, Guid conversationId, string clientMessageId, string content, LiveSupportMessageType type, CancellationToken ct)
    {
        var conversation = await RequireParticipantConversationAsync(participant, conversationId, ct);
        return await SendMessageAsync(conversation, participant.Type == LiveSupportParticipantType.Student ? LiveSupportSenderType.Student : LiveSupportSenderType.Guest,
            participant.StudentUserId, participant.GuestSessionId, clientMessageId, content, type, ct);
    }

    public async Task<LiveSupportSendResultDto> SendParticipantAttachmentMessageAsync(LiveSupportParticipantIdentity participant, Guid conversationId, string clientMessageId, Guid attachmentId, string? caption, LiveSupportMessageType type, CancellationToken ct)
    {
        var conversation = await RequireParticipantConversationAsync(participant, conversationId, ct);
        var identity = participant.StudentUserId?.ToString("N") ?? participant.GuestSessionId!.Value.ToString("N");
        var attachment = await _db.LiveSupportAttachments.FirstOrDefaultAsync(x => x.Id == attachmentId && x.UploadedByIdentity == identity && !x.IsBlocked, ct)
            ?? throw new LiveSupportException("NOT_FOUND", "الملف غير موجود.");
        var result = await SendMessageAsync(conversation, participant.Type == LiveSupportParticipantType.Student ? LiveSupportSenderType.Student : LiveSupportSenderType.Guest, participant.StudentUserId, participant.GuestSessionId, clientMessageId, string.IsNullOrWhiteSpace(caption) ? attachment.OriginalFileName : caption.Trim(), type, ct);
        var message = await _db.LiveSupportMessages.FirstAsync(x => x.Id == result.Message.Id, ct);
        message.AttachmentId = attachment.Id;
        await _db.SaveChangesAsync(ct);
        return result;
    }

    public async Task<LiveSupportConversationDto> AbandonAsync(LiveSupportParticipantIdentity participant, Guid conversationId, CancellationToken ct)
    {
        var conversation = await RequireParticipantConversationAsync(participant, conversationId, ct);
        if (IsTerminal(conversation.Status)) throw new LiveSupportException(LiveSupportErrorCodes.ConversationTerminal, "المحادثة مغلقة بالفعل.");
        await FinishConversationAsync(conversation, null, LiveSupportConversationStatus.Abandoned, "أغلقها صاحب المحادثة", LiveSupportAssignmentEndReason.Closed, ct);
        return await MapAsync(conversation, ct);
    }

    public async Task SubmitRatingAsync(LiveSupportParticipantIdentity participant, Guid conversationId, int stars, string? comment, CancellationToken ct)
    {
        var conversation = await RequireParticipantConversationAsync(participant, conversationId, ct);
        if (conversation.Status != LiveSupportConversationStatus.Closed || stars is < 1 or > 5 || await _db.LiveSupportRatings.AnyAsync(x => x.ConversationId == conversationId, ct))
            throw new LiveSupportException(LiveSupportErrorCodes.RatingConflict, "التقييم متاح مرة واحدة بعد إغلاق المحادثة.");
        _db.LiveSupportRatings.Add(new LiveSupportRating { ConversationId = conversationId, Stars = stars, Comment = comment?.Trim(), SubmittedByUserId = participant.StudentUserId, SubmittedByGuestSessionId = participant.GuestSessionId, SubmittedAt = DateTime.UtcNow });
        AddEvent(conversationId, LiveSupportEventType.RatingSubmitted, participant.StudentUserId, participant.GuestSessionId);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<LiveSupportStaffBootstrapDto> GetStaffBootstrapAsync(Guid staffUserId, bool isAdmin, CancellationToken ct)
    {
        var config = await _db.LiveSupportStaffConfigs.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == staffUserId && x.IsEnabled, ct);
        if (!isAdmin && config is null) throw new LiveSupportException(
            LiveSupportErrorCodes.Forbidden,
            "صلاحية الدور لا تكفي لاستقبال المحادثات. يجب على الأدمن تفعيل «يستقبل محادثات» لهذا الموظف من إدارة الدعم المباشر وتحديد سعته.");
        var checkedIn = isAdmin || await IsCheckedInAsync(staffUserId, ct);
        var conversations = await _db.LiveSupportConversations.Where(x =>
            x.Status != LiveSupportConversationStatus.Closed && x.Status != LiveSupportConversationStatus.Abandoned &&
            (isAdmin || x.CurrentOwnerUserId == staffUserId)).OrderByDescending(x => x.LastMessageAt).ToListAsync(ct);
        return new LiveSupportStaffBootstrapDto(config?.IsEnabled ?? isAdmin, checkedIn, conversations.Count, config?.MaxActiveConversations ?? 50,
            await _db.LiveSupportQueueEntries.CountAsync(x => x.DequeuedAt == null, ct), await MapManyAsync(conversations, ct));
    }

    public async Task<IReadOnlyList<LiveSupportMessageDto>> GetStaffMessagesAsync(Guid staffUserId, bool isAdmin, Guid conversationId, int pageSize, CancellationToken ct)
    {
        await RequireStaffConversationAsync(staffUserId, isAdmin, conversationId, ct);
        return await _db.LiveSupportMessages.Where(x => x.ConversationId == conversationId)
            .OrderByDescending(x => x.SentAt).Take(Math.Clamp(pageSize, 1, 100)).OrderBy(x => x.SentAt)
            .Select(x => ToDto(x)).ToListAsync(ct);
    }

    public async Task<long> GetStaffLastEventSequenceAsync(Guid staffUserId, bool isAdmin, Guid conversationId, CancellationToken ct)
    {
        await RequireStaffConversationAsync(staffUserId, isAdmin, conversationId, ct);
        return await _db.LiveSupportEvents.Where(x => x.ConversationId == conversationId).MaxAsync(x => (long?)x.Sequence, ct) ?? 0;
    }

    public async Task<LiveSupportSendResultDto> SendStaffMessageAsync(Guid staffUserId, bool isAdmin, Guid conversationId, string clientMessageId, string content, CancellationToken ct)
    {
        var conversation = await RequireStaffConversationAsync(staffUserId, isAdmin, conversationId, ct);
        if (!isAdmin && !await IsCheckedInAsync(staffUserId, ct)) throw new LiveSupportException(LiveSupportErrorCodes.Forbidden, "يجب تسجيل الحضور أولًا.");
        var result = await SendMessageAsync(conversation, isAdmin ? LiveSupportSenderType.Admin : LiveSupportSenderType.Staff, staffUserId, null, clientMessageId, content, LiveSupportMessageType.Text, ct);
        if (!conversation.FirstStaffResponseAt.HasValue)
        {
            conversation.FirstStaffResponseAt = DateTime.UtcNow;
            conversation.Status = LiveSupportConversationStatus.Active;
            AddEvent(conversation.Id, LiveSupportEventType.FirstStaffResponse, staffUserId, null);
            await _db.SaveChangesAsync(ct);
        }
        return result;
    }

    public async Task<LiveSupportConversationDto> CloseAsync(Guid staffUserId, bool isAdmin, Guid conversationId, string reason, CancellationToken ct)
    {
        var conversation = await RequireStaffConversationAsync(staffUserId, isAdmin, conversationId, ct);
        await FinishConversationAsync(conversation, staffUserId, LiveSupportConversationStatus.Closed, reason, LiveSupportAssignmentEndReason.Closed, ct);
        return await MapAsync(conversation, ct);
    }

    public async Task<LiveSupportConversationDto> TransferAsync(Guid staffUserId, bool isAdmin, Guid conversationId, Guid? targetStaffUserId, string reason, CancellationToken ct)
    {
        if (reason.Trim().Length is < 3 or > 500) throw new LiveSupportException("VALIDATION_ERROR", "سبب التحويل مطلوب.");
        
        var aiState = await _db.LiveSupportAIConversationStates.FirstOrDefaultAsync(x => x.ConversationId == conversationId, ct);
        if (aiState != null && aiState.Mode == LiveSupportAIMode.AiActive)
        {
            if (_handoffService == null) throw new InvalidOperationException("Handoff service is not available.");
            await _handoffService.HandoffAsync(
                conversationId,
                participant: null,
                actorUserId: staffUserId,
                reasonCode: "ADMIN_INTERVENTION",
                safeSummary: $"تدخل الإدارة: {reason.Trim()}",
                forced: true,
                idempotencyKey: $"admin-transfer-{conversationId}-{DateTime.UtcNow.Ticks}",
                cancellationToken: ct);
        }

        await using var tx = await _db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await AcquireRoutingLockAsync(ct);
        var conversation = await RequireStaffConversationAsync(staffUserId, isAdmin, conversationId, ct);
        if (IsTerminal(conversation.Status)) throw new LiveSupportException(LiveSupportErrorCodes.ConversationTerminal, "المحادثة مغلقة.");
        var active = await _db.LiveSupportAssignments.FirstOrDefaultAsync(x => x.ConversationId == conversationId && x.EndedAt == null, ct);
        if (active is not null) { active.EndedAt = DateTime.UtcNow; active.EndReason = LiveSupportAssignmentEndReason.ManualTransfer; active.TransferReason = reason.Trim(); }
        conversation.CurrentOwnerUserId = null; conversation.AssignedAt = null; conversation.Status = LiveSupportConversationStatus.Waiting; conversation.QueuedAt = DateTime.UtcNow; conversation.Version++;
        _db.LiveSupportQueueEntries.Add(new LiveSupportQueueEntry { ConversationId = conversationId, EnteredAt = DateTime.UtcNow, Sequence = DateTime.UtcNow.Ticks });
        AddEvent(conversationId, LiveSupportEventType.TransferRequested, staffUserId, null);
        await _db.SaveChangesAsync(ct);
        if (targetStaffUserId.HasValue)
        {
            var target = await EligibleStaffQuery().FirstOrDefaultAsync(x => x.UserId == targetStaffUserId, ct);
            var load = await _db.LiveSupportAssignments.CountAsync(x => x.StaffUserId == targetStaffUserId && x.EndedAt == null, ct);
            if (target is null || load >= target.MaxActiveConversations || (_presence is not null && !await _presence.IsConnectedAsync(targetStaffUserId.Value)))
                throw new LiveSupportException("TARGET_UNAVAILABLE", "الموظف المطلوب غير متاح أو وصل للحد الأقصى.");
            await AssignConversationAsync(conversation, target, ct);
        }
        else await AssignOldestWaitingAsync(ct, staffUserId);
        AddEvent(conversationId, LiveSupportEventType.Transferred, staffUserId, null, conversation.CurrentOwnerUserId);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        _logger?.LogInformation("LiveSupport conversation {ConversationId} transferred by {ActorUserId} to {OwnerUserId}", conversationId, staffUserId, conversation.CurrentOwnerUserId);
        return await MapAsync(conversation, ct);
    }

    public async Task<LiveSupportAdminConfigDto> GetAdminConfigAsync(CancellationToken ct)
    {
        var employees = await _db.EmployeeProfiles.AsNoTracking().Select(x => x.UserId).Distinct().ToListAsync(ct);
        var configs = await _db.LiveSupportStaffConfigs.AsNoTracking().Where(x => employees.Contains(x.UserId)).ToDictionaryAsync(x => x.UserId, ct);
        var result = new List<LiveSupportStaffConfigDto>(employees.Count);
        foreach (var userId in employees)
        {
            if (configs.TryGetValue(userId, out var config)) result.Add(await MapStaffConfigAsync(config, ct));
            else
            {
                var name = await _db.Users.Where(x => x.Id == userId).Select(x => x.FullName).FirstOrDefaultAsync(ct) ?? "موظف";
                result.Add(new LiveSupportStaffConfigDto(userId, name, false, 1, 0, await IsCheckedInAsync(userId, ct), 0, []));
            }
        }
        result.Sort((a, b) => string.Compare(a.StaffName, b.StaffName, StringComparison.CurrentCulture));
        return new LiveSupportAdminConfigDto((await _settings.GetAsync(ct)).LiveSupportEnabled, result);
    }

    public async Task SetFeatureEnabledAsync(bool enabled, CancellationToken ct)
    {
        var setting = await _db.PlatformSettings.FirstOrDefaultAsync(x => x.Key == PlatformSettingKeys.LiveSupportEnabled, ct);
        if (setting is null)
        {
            setting = new NaderGorge.Domain.Entities.PlatformSetting { Key = PlatformSettingKeys.LiveSupportEnabled, Value = enabled.ToString() };
            _db.PlatformSettings.Add(setting);
        }
        else { setting.Value = enabled.ToString(); setting.UpdatedAt = DateTime.UtcNow; }
        await _db.SaveChangesAsync(ct);
        _settings.Invalidate();
    }

    public async Task<LiveSupportStaffConfigDto> UpdateStaffConfigAsync(Guid actorUserId, Guid staffUserId, bool enabled, int capacity, long? expectedVersion, IReadOnlyList<LiveSupportScheduleWindowDto> schedule, CancellationToken ct)
    {
        if (capacity is < 1 or > 50) throw new LiveSupportException("VALIDATION_ERROR", "الحد الأقصى يجب أن يكون من 1 إلى 50.");
        if (!await _db.EmployeeProfiles.AnyAsync(x => x.UserId == staffUserId, ct)) throw new LiveSupportException("NOT_FOUND", "الموظف غير موجود.");
        ValidateSchedule(schedule);
        var config = await _db.LiveSupportStaffConfigs.FirstOrDefaultAsync(x => x.UserId == staffUserId, ct);
        if (config is null)
        {
            config = new LiveSupportStaffConfig { UserId = staffUserId, IsEnabled = enabled, MaxActiveConversations = capacity, ConfiguredByUserId = actorUserId, Version = 1 };
            _db.LiveSupportStaffConfigs.Add(config);
        }
        else
        {
            if (expectedVersion.HasValue && config.Version != expectedVersion.Value) throw new LiveSupportException("VERSION_CONFLICT", "تم تعديل الإعدادات بواسطة مستخدم آخر. حدّث الصفحة.");
            config.IsEnabled = enabled; config.MaxActiveConversations = capacity; config.ConfiguredByUserId = actorUserId; config.Version++; config.UpdatedAt = DateTime.UtcNow;
            var oldWindows = await _db.LiveSupportScheduleWindows.Where(x => x.StaffConfigId == config.Id).ToListAsync(ct);
            _db.LiveSupportScheduleWindows.RemoveRange(oldWindows);
        }
        foreach (var window in schedule) _db.LiveSupportScheduleWindows.Add(new LiveSupportScheduleWindow { StaffConfigId = config.Id, DayOfWeek = window.DayOfWeek, StartLocalTime = window.StartLocalTime, EndLocalTime = window.EndLocalTime });
        await _db.SaveChangesAsync(ct);
        return await MapStaffConfigAsync(config, ct);
    }

    public async Task ReleaseStaffAssignmentsAsync(Guid staffUserId, LiveSupportAssignmentEndReason reason, CancellationToken ct)
    {
        await using var tx = await _db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await AcquireRoutingLockAsync(ct);
        var assignments = await _db.LiveSupportAssignments.Where(x => x.StaffUserId == staffUserId && x.EndedAt == null).ToListAsync(ct);
        var now = DateTime.UtcNow;
        foreach (var assignment in assignments)
        {
            assignment.EndedAt = now;
            assignment.EndReason = reason;
            var conversation = await _db.LiveSupportConversations.FirstAsync(x => x.Id == assignment.ConversationId, ct);
            conversation.CurrentOwnerUserId = null; conversation.AssignedAt = null; conversation.Status = LiveSupportConversationStatus.Waiting; conversation.QueuedAt = now; conversation.Version++;
            _db.LiveSupportQueueEntries.Add(new LiveSupportQueueEntry { ConversationId = conversation.Id, EnteredAt = now, Sequence = now.Ticks });
            AddEvent(conversation.Id, reason == LiveSupportAssignmentEndReason.DisconnectTimeout ? LiveSupportEventType.StaffDisconnected : LiveSupportEventType.AttendanceCheckedOut, staffUserId, null);
            AddEvent(conversation.Id, LiveSupportEventType.QueueEntered, staffUserId, null);
        }
        await _db.SaveChangesAsync(ct);
        await AssignOldestWaitingAsync(ct);
        await tx.CommitAsync(ct);
        _logger?.LogInformation("LiveSupport assignments released for staff {StaffUserId}; reason {Reason}; count {Count}", staffUserId, reason, assignments.Count);
        LiveSupportTelemetry.AssignmentsReleased.Add(assignments.Count, new KeyValuePair<string, object?>("reason", reason.ToString()));
    }

    public async Task<IReadOnlyList<LiveSupportStudentSearchDto>> SearchStudentsAsync(Guid staffUserId, bool isAdmin, Guid conversationId, string query, CancellationToken ct)
    {
        await RequireStaffConversationAsync(staffUserId, isAdmin, conversationId, ct);
        query = query.Trim();
        if (query.Length < 3) throw new LiveSupportException("VALIDATION_ERROR", "اكتب 3 حروف أو أرقام على الأقل.");
        var users = await _db.Users.AsNoTracking().Where(x => x.FullName.Contains(query) || x.PhoneNumber.Contains(query) || (x.StudentProfile != null && x.StudentProfile.StudentCode != null && x.StudentProfile.StudentCode.Contains(query)))
            .OrderBy(x => x.FullName).Take(10).Select(x => new { x.Id, x.FullName, x.PhoneNumber, Code = x.StudentProfile == null ? null : x.StudentProfile.StudentCode }).ToListAsync(ct);
        return users.Select(x => new LiveSupportStudentSearchDto(x.Id, x.FullName, MaskPhone(x.PhoneNumber), x.Code)).ToList();
    }

    public async Task<LiveSupportConversationDto> ChangeStudentLinkAsync(Guid staffUserId, bool isAdmin, Guid conversationId, Guid? studentUserId, string reason, long expectedVersion, CancellationToken ct)
    {
        var conversation = await RequireStaffConversationAsync(staffUserId, isAdmin, conversationId, ct);
        if (conversation.Version != expectedVersion) throw new LiveSupportException("VERSION_CONFLICT", "تغيرت المحادثة. حدّث البيانات ثم حاول مرة أخرى.");
        if (studentUserId.HasValue && !await _db.StudentProfiles.AnyAsync(x => x.UserId == studentUserId, ct)) throw new LiveSupportException("NOT_FOUND", "حساب الطالب غير موجود.");
        if (reason.Trim().Length is < 3 or > 500) throw new LiveSupportException("VALIDATION_ERROR", "سبب الربط أو الإلغاء مطلوب.");
        var previous = conversation.LinkedStudentUserId;
        conversation.LinkedStudentUserId = studentUserId; conversation.Version++;
        _db.LiveSupportStudentLinkHistories.Add(new LiveSupportStudentLinkHistory { ConversationId = conversationId, PreviousStudentUserId = previous, NewStudentUserId = studentUserId, ChangedByUserId = staffUserId, Reason = reason.Trim(), ChangedAt = DateTime.UtcNow });
        var eventType = studentUserId is null ? LiveSupportEventType.StudentUnlinked : previous is null ? LiveSupportEventType.StudentLinked : LiveSupportEventType.StudentLinkReplaced;
        AddEvent(conversationId, eventType, staffUserId, null, studentUserId);
        await _db.SaveChangesAsync(ct);
        return await MapAsync(conversation, ct);
    }

    public async Task<LiveSupportStudentContextDto> GetStudentContextAsync(Guid staffUserId, bool isAdmin, Guid conversationId, CancellationToken ct)
    {
        var conversation = await RequireStaffConversationAsync(staffUserId, isAdmin, conversationId, ct);
        var studentId = conversation.LinkedStudentUserId ?? throw new LiveSupportException("STUDENT_NOT_LINKED", "اربط المحادثة بطالب أولًا.");
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == studentId, ct) ?? throw new LiveSupportException("NOT_FOUND", "الطالب غير موجود.");
        var profile = await _db.StudentProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == studentId, ct);
        var balance = await _db.StudentBalances.AsNoTracking().Where(x => x.UserId == studentId).Select(x => (decimal?)x.CurrentBalance).FirstOrDefaultAsync(ct) ?? 0;
        var game = await _db.StudentGamifications.AsNoTracking().FirstOrDefaultAsync(x => x.StudentId == studentId, ct);
        var crm = await _db.CrmStudentStatuses.AsNoTracking().FirstOrDefaultAsync(x => x.StudentId == studentId, ct);
        var devices = await _db.Devices.AsNoTracking().Where(x => x.UserId == studentId).OrderByDescending(x => x.LastUsedAt).Select(x => new LiveSupportDeviceDto(x.Id, x.DeviceName, x.DeviceType, x.OsName, x.BrowserName, x.LastUsedAt, x.IsActive)).ToListAsync(ct);
        var grants = await _db.StudentAccessGrants.AsNoTracking().Where(x => x.UserId == studentId).OrderByDescending(x => x.GrantedAt).Take(100).Select(x => new LiveSupportGrantDto(x.Id, x.GrantType.ToString(), x.PackageId, x.GrantedAt, x.ExpiresAt, x.IsActive)).ToListAsync(ct);
        var notes = await _db.StudentNotes.AsNoTracking().Where(x => x.StudentId == studentId).OrderByDescending(x => x.IsPinned).ThenByDescending(x => x.CreatedAt).Take(100).Select(x => new LiveSupportNoteDto(x.Id, x.Content, x.IsPinned, x.CreatedAt)).ToListAsync(ct);
        return new LiveSupportStudentContextDto(studentId, user.FullName, user.PhoneNumber, user.IsActive, profile?.StudentCode, profile?.Governorate, profile?.SchoolName, profile?.EducationStage.ToString(), profile?.GradeLevel.ToString(), balance, game?.TotalPoints ?? 0, game?.LevelName, crm?.Status.ToString(), crm?.Priority.ToString(), devices, grants, notes,
            await _db.VideoWatchEvents.CountAsync(x => x.UserId == studentId, ct), await _db.StudentExamAttempts.CountAsync(x => x.UserId == studentId, ct), await _db.HomeworkSubmissions.CountAsync(x => x.StudentId == studentId, ct));
    }

    public async Task<LiveSupportStudentContextSectionDto> GetStudentContextSectionAsync(
        Guid staffUserId, bool isAdmin, Guid conversationId, string section, CancellationToken ct)
    {
        var conversation = await RequireStaffConversationAsync(staffUserId, isAdmin, conversationId, ct);
        var studentId = conversation.LinkedStudentUserId
            ?? throw new LiveSupportException("STUDENT_NOT_LINKED", "اربط المحادثة بطالب أولًا.");
        var sectionData = section switch
        {
            "basic" => await GetBasicStudentSectionAsync(studentId, ct),
            "metrics" => await GetStudentMetricsSectionAsync(studentId, ct),
            "study" => await GetStudentStudySectionAsync(studentId, ct),
            "devices" => await GetStudentDevicesSectionAsync(studentId, ct),
            "notes" => await GetStudentNotesSectionAsync(studentId, ct),
            "crm" => await GetStudentCrmSectionAsync(studentId, ct),
            _ => throw new LiveSupportException("VALIDATION_ERROR", "قسم بيانات الطالب غير مدعوم.")
        };
        return new LiveSupportStudentContextSectionDto(section, sectionData);
    }

    private async Task<JsonElement> GetBasicStudentSectionAsync(Guid studentId, CancellationToken ct)
    {
        var student = await _db.Users.AsNoTracking().Where(user => user.Id == studentId).Select(user => new
        {
            user.FullName, user.PhoneNumber, user.IsActive,
            StudentCode = user.StudentProfile == null ? null : user.StudentProfile.StudentCode,
            Governorate = user.StudentProfile == null ? null : user.StudentProfile.Governorate,
            SchoolName = user.StudentProfile == null ? null : user.StudentProfile.SchoolName,
            EducationStage = user.StudentProfile == null ? null : user.StudentProfile.EducationStage.ToString(),
            GradeLevel = user.StudentProfile == null ? null : user.StudentProfile.GradeLevel.ToString()
        }).SingleOrDefaultAsync(ct) ?? throw new LiveSupportException("NOT_FOUND", "الطالب غير موجود.");
        return JsonSerializer.SerializeToElement(student);
    }

    private async Task<JsonElement> GetStudentMetricsSectionAsync(Guid studentId, CancellationToken ct)
    {
        var balance = await _db.StudentBalances.AsNoTracking().Where(row => row.UserId == studentId).Select(row => (decimal?)row.CurrentBalance).SingleOrDefaultAsync(ct) ?? 0;
        var points = await _db.StudentGamifications.AsNoTracking().Where(row => row.StudentId == studentId).Select(row => (int?)row.TotalPoints).SingleOrDefaultAsync(ct) ?? 0;
        var exams = await _db.StudentExamAttempts.CountAsync(row => row.UserId == studentId, ct);
        var devices = await _db.Devices.CountAsync(row => row.UserId == studentId, ct);
        return JsonSerializer.SerializeToElement(new { Balance = balance, Points = points, ExamAttempts = exams, DevicesCount = devices });
    }

    private async Task<JsonElement> GetStudentStudySectionAsync(Guid studentId, CancellationToken ct)
    {
        var grants = await _db.StudentAccessGrants.CountAsync(row => row.UserId == studentId && row.IsActive, ct);
        var watches = await _db.VideoWatchEvents.CountAsync(row => row.UserId == studentId, ct);
        var homework = await _db.HomeworkSubmissions.CountAsync(row => row.StudentId == studentId, ct);
        return JsonSerializer.SerializeToElement(new { ActiveGrants = grants, WatchEvents = watches, HomeworkSubmissions = homework });
    }

    private async Task<JsonElement> GetStudentDevicesSectionAsync(Guid studentId, CancellationToken ct)
    {
        var devices = await _db.Devices.AsNoTracking().Where(row => row.UserId == studentId).OrderByDescending(row => row.LastUsedAt)
            .Take(100).Select(row => new LiveSupportDeviceDto(row.Id, row.DeviceName, row.DeviceType, row.OsName, row.BrowserName, row.LastUsedAt, row.IsActive)).ToListAsync(ct);
        return JsonSerializer.SerializeToElement(new { Devices = devices });
    }

    private async Task<JsonElement> GetStudentNotesSectionAsync(Guid studentId, CancellationToken ct)
    {
        var notes = await _db.StudentNotes.AsNoTracking().Where(row => row.StudentId == studentId).OrderByDescending(row => row.IsPinned).ThenByDescending(row => row.CreatedAt)
            .Take(100).Select(row => new LiveSupportNoteDto(row.Id, row.Content, row.IsPinned, row.CreatedAt)).ToListAsync(ct);
        return JsonSerializer.SerializeToElement(new { Notes = notes });
    }

    private async Task<JsonElement> GetStudentCrmSectionAsync(Guid studentId, CancellationToken ct)
    {
        var crm = await _db.CrmStudentStatuses.AsNoTracking().Where(row => row.StudentId == studentId)
            .Select(row => new { Status = row.Status.ToString(), Priority = row.Priority.ToString() }).SingleOrDefaultAsync(ct);
        return JsonSerializer.SerializeToElement(new { Status = crm?.Status, Priority = crm?.Priority });
    }

    public async Task<LiveSupportAdminDashboardDto> GetAdminDashboardAsync(CancellationToken ct)
    {
        var conversations = await _db.LiveSupportConversations.AsNoTracking().OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync(ct);
        var rows = new List<LiveSupportAdminConversationDto>(conversations.Count);
        foreach (var conversation in conversations) rows.Add(await MapAdminConversationAsync(conversation, ct));
        var configs = await _db.LiveSupportStaffConfigs.AsNoTracking().ToListAsync(ct);
        var performance = new List<LiveSupportStaffPerformanceDto>(configs.Count);
        foreach (var config in configs)
        {
            var conversationIds = await _db.LiveSupportAssignments.AsNoTracking().Where(x => x.StaffUserId == config.UserId).Select(x => x.ConversationId).Distinct().ToListAsync(ct);
            var ratings = await _db.LiveSupportRatings.AsNoTracking().Where(x => conversationIds.Contains(x.ConversationId)).Select(x => x.Stars).ToListAsync(ct);
            performance.Add(new LiveSupportStaffPerformanceDto(config.UserId, await _db.Users.Where(x => x.Id == config.UserId).Select(x => x.FullName).FirstOrDefaultAsync(ct) ?? "موظف", conversationIds.Count,
                await _db.LiveSupportAssignments.Where(x => x.StaffUserId == config.UserId && x.EndReason == LiveSupportAssignmentEndReason.Closed).Select(x => x.ConversationId).Distinct().CountAsync(ct), ratings.Count, ratings.Count == 0 ? null : ratings.Average()));
        }
        var today = DateTime.UtcNow.Date;
        return new LiveSupportAdminDashboardDto(conversations.Count(x => x.Status == LiveSupportConversationStatus.Waiting), conversations.Count(x => x.Status is LiveSupportConversationStatus.Assigned or LiveSupportConversationStatus.Active),
            await _db.LiveSupportConversations.CountAsync(x => x.Status == LiveSupportConversationStatus.Closed && x.ClosedAt >= today, ct), rows, performance);
    }

    public async Task<LiveSupportConversationTimelineDto> GetAdminTimelineAsync(Guid conversationId, CancellationToken ct)
    {
        var conversation = await _db.LiveSupportConversations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == conversationId, ct) ?? throw new LiveSupportException("NOT_FOUND", "المحادثة غير موجودة.");
        var items = new List<LiveSupportTimelineItemDto>();
        var events = await _db.LiveSupportEvents.AsNoTracking().Where(x => x.ConversationId == conversationId).OrderBy(x => x.Sequence).ToListAsync(ct);
        foreach (var item in events) items.Add(new LiveSupportTimelineItemDto(item.OccurredAt, item.Type.ToString(), await ActorNameAsync(item.ActorUserId, item.ActorGuestSessionId, ct), EventSummary(item.Type), item.SafeMetadataJson));
        var assignments = await _db.LiveSupportAssignments.AsNoTracking().Where(x => x.ConversationId == conversationId).ToListAsync(ct);
        foreach (var item in assignments) items.Add(new LiveSupportTimelineItemDto(item.StartedAt, "Assignment", await ActorNameAsync(item.StaffUserId, null, ct), "تم إسناد المحادثة للموظف", item.EndedAt.HasValue ? $"انتهى: {item.EndReason} — {item.EndedAt:O}" : "الإسناد الحالي"));
        var messages = await _db.LiveSupportMessages.AsNoTracking().Where(x => x.ConversationId == conversationId).OrderBy(x => x.SentAt).ToListAsync(ct);
        foreach (var message in messages) items.Add(new LiveSupportTimelineItemDto(message.SentAt, "Message", await ActorNameAsync(message.SenderUserId, message.SenderGuestSessionId, ct), $"رسالة من {message.SenderType}", message.Content));
        var actions = await _db.LiveSupportActionExecutions.AsNoTracking().Where(x => x.ConversationId == conversationId).ToListAsync(ct);
        foreach (var item in actions) items.Add(new LiveSupportTimelineItemDto(item.StartedAt, "StudentAction", await ActorNameAsync(item.StaffUserId, null, ct), $"إجراء على الطالب: {item.ActionKey} — {item.Status}", item.SafeResultJson ?? item.FailureCode));
        var rating = await _db.LiveSupportRatings.AsNoTracking().FirstOrDefaultAsync(x => x.ConversationId == conversationId, ct);
        return new LiveSupportConversationTimelineDto(await MapAdminConversationAsync(conversation, ct), items.OrderBy(x => x.At).ToList(), rating?.Stars, rating?.Comment);
    }

    private IQueryable<LiveSupportConversation> ParticipantQuery(LiveSupportParticipantIdentity p) => p.Type == LiveSupportParticipantType.Student
        ? _db.LiveSupportConversations.Where(x => x.ParticipantType == p.Type && x.StudentUserId == p.StudentUserId)
        : _db.LiveSupportConversations.Where(x => x.ParticipantType == p.Type && x.GuestSessionId == p.GuestSessionId);

    private IQueryable<LiveSupportStaffConfig> EligibleStaffQuery() =>
        _db.LiveSupportStaffConfigs.Where(c => c.IsEnabled && _db.EmployeeProfiles.Any(e => e.UserId == c.UserId && _db.AttendanceLogs.Any(a => a.EmployeeId == e.Id && a.ClockOut == null)));

    private async Task<bool> IsCheckedInAsync(Guid userId, CancellationToken ct) => await _db.EmployeeProfiles.AnyAsync(e => e.UserId == userId && _db.AttendanceLogs.Any(a => a.EmployeeId == e.Id && a.ClockOut == null), ct);

    private async Task<DateTime?> GetNextScheduleAsync(CancellationToken ct)
    {
        var windows = await _db.LiveSupportScheduleWindows.AsNoTracking().Where(x => x.IsActive && _db.LiveSupportStaffConfigs.Any(c => c.Id == x.StaffConfigId && c.IsEnabled)).ToListAsync(ct);
        var cairo = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, cairo);
        return windows.SelectMany(w => Enumerable.Range(0, 8).Select(offset => (w, date: DateOnly.FromDateTime(localNow).AddDays(offset))))
            .Where(x => (int)x.date.DayOfWeek == x.w.DayOfWeek)
            .Select(x => TimeZoneInfo.ConvertTimeToUtc(x.date.ToDateTime(x.w.StartLocalTime), cairo))
            .Where(x => x > DateTime.UtcNow).OrderBy(x => x).Cast<DateTime?>().FirstOrDefault();
    }

    private async Task AssignOldestWaitingAsync(CancellationToken ct, Guid? excludedStaffUserId = null)
    {
        while (true)
        {
            var candidates = await EligibleStaffQuery().Where(c => !excludedStaffUserId.HasValue || c.UserId != excludedStaffUserId.Value).Select(c => new { Config = c, Load = _db.LiveSupportAssignments.Count(a => a.StaffUserId == c.UserId && a.EndedAt == null) })
                .Where(x => x.Load < x.Config.MaxActiveConversations).OrderBy(x => x.Load).ThenBy(x => x.Config.LastAssignedAt).ThenBy(x => x.Config.UserId).ToListAsync(ct);
            var staff = candidates.FirstOrDefault();
            if (_presence is not null)
            {
                staff = null;
                foreach (var candidate in candidates) if (await _presence.IsConnectedAsync(candidate.Config.UserId)) { staff = candidate; break; }
            }
            var queue = await _db.LiveSupportQueueEntries.Where(x => x.DequeuedAt == null).OrderBy(x => x.Sequence).ThenBy(x => x.Id).FirstOrDefaultAsync(ct);
            if (staff is null || queue is null) return;
            var conversation = await _db.LiveSupportConversations.FirstAsync(x => x.Id == queue.ConversationId, ct);
            await AssignConversationAsync(conversation, staff.Config, ct, queue);
        }
    }

    private async Task AssignConversationAsync(LiveSupportConversation conversation, LiveSupportStaffConfig staff, CancellationToken ct, LiveSupportQueueEntry? queue = null)
    {
        queue ??= await _db.LiveSupportQueueEntries.FirstAsync(x => x.ConversationId == conversation.Id && x.DequeuedAt == null, ct);
        var now = DateTime.UtcNow;
        conversation.Status = LiveSupportConversationStatus.Assigned; conversation.CurrentOwnerUserId = staff.UserId; conversation.AssignedAt = now; conversation.Version++;
        queue.DequeuedAt = now; queue.DequeueReason = "Assigned"; staff.LastAssignedAt = now;
        _db.LiveSupportAssignments.Add(new LiveSupportAssignment { ConversationId = conversation.Id, StaffUserId = staff.UserId, StartedAt = now, AssignmentSequence = await _db.LiveSupportAssignments.CountAsync(x => x.ConversationId == conversation.Id, ct) + 1 });
        AddEvent(conversation.Id, LiveSupportEventType.Assigned, staff.UserId, null);
        await _db.SaveChangesAsync(ct);
    }

    private async Task<LiveSupportSendResultDto> SendMessageAsync(LiveSupportConversation conversation, LiveSupportSenderType senderType, Guid? userId, Guid? guestId, string clientMessageId, string content, LiveSupportMessageType type, CancellationToken ct)
    {
        if (IsTerminal(conversation.Status)) throw new LiveSupportException(LiveSupportErrorCodes.ConversationTerminal, "المحادثة مغلقة. ابدأ محادثة جديدة.");
        clientMessageId = clientMessageId.Trim(); content = content.Trim();
        if (clientMessageId.Length is < 8 or > 100 || content.Length is < 1 or > 4000) throw new LiveSupportException("VALIDATION_ERROR", "الرسالة غير صالحة.");
        var existing = await _db.LiveSupportMessages.FirstOrDefaultAsync(x => x.ConversationId == conversation.Id && x.ClientMessageId == clientMessageId, ct);
        if (existing is not null)
        {
            if (existing.Content != content || existing.SenderType != senderType) throw new LiveSupportException(LiveSupportErrorCodes.MessageConflict, "معرّف الرسالة مستخدم لمحتوى مختلف.");
            return new LiveSupportSendResultDto(ToDto(existing), true);
        }
        var message = new LiveSupportMessage { ConversationId = conversation.Id, SenderType = senderType, SenderUserId = userId, SenderGuestSessionId = guestId, ClientMessageId = clientMessageId, Type = type, Content = content, SentAt = DateTime.UtcNow };
        conversation.LastMessageAt = message.SentAt; conversation.Version++;
        _db.LiveSupportMessages.Add(message);
        AddEvent(conversation.Id, LiveSupportEventType.MessageSent, userId, guestId, message.Id);
        if (_aiTurnOrchestrator is not null &&
            (senderType == LiveSupportSenderType.Student || senderType == LiveSupportSenderType.Guest))
        {
            await _aiTurnOrchestrator.QueueForParticipantMessageAsync(conversation.Id, message.Id, ct);
        }
        await _db.SaveChangesAsync(ct);

        return new LiveSupportSendResultDto(ToDto(message), false);
    }

    private async Task FinishConversationAsync(LiveSupportConversation c, Guid? actor, LiveSupportConversationStatus status, string reason, LiveSupportAssignmentEndReason endReason, CancellationToken ct)
    {
        if (IsTerminal(c.Status)) throw new LiveSupportException(LiveSupportErrorCodes.ConversationTerminal, "المحادثة مغلقة بالفعل.");
        var now = DateTime.UtcNow; c.Status = status; c.ClosedAt = now; c.ClosedByUserId = actor; c.CloseReason = reason.Trim()[..Math.Min(reason.Trim().Length, 500)]; c.CurrentOwnerUserId = null; c.Version++;
        var assignment = await _db.LiveSupportAssignments.FirstOrDefaultAsync(x => x.ConversationId == c.Id && x.EndedAt == null, ct);
        if (assignment is not null) { assignment.EndedAt = now; assignment.EndReason = endReason; }
        var queue = await _db.LiveSupportQueueEntries.FirstOrDefaultAsync(x => x.ConversationId == c.Id && x.DequeuedAt == null, ct);
        if (queue is not null) { queue.DequeuedAt = now; queue.DequeueReason = status.ToString(); }
        AddEvent(c.Id, status == LiveSupportConversationStatus.Closed ? LiveSupportEventType.Closed : LiveSupportEventType.Abandoned, actor, null);
        await _db.SaveChangesAsync(ct); await AssignOldestWaitingAsync(ct);
    }

    private async Task<LiveSupportConversation> RequireParticipantConversationAsync(LiveSupportParticipantIdentity p, Guid id, CancellationToken ct) =>
        await ParticipantQuery(p).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new LiveSupportException(LiveSupportErrorCodes.Forbidden, "لا يمكنك الوصول لهذه المحادثة.");

    private async Task<LiveSupportConversation> RequireStaffConversationAsync(Guid userId, bool admin, Guid id, CancellationToken ct)
    {
        var c = await _db.LiveSupportConversations.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new LiveSupportException("NOT_FOUND", "المحادثة غير موجودة.");
        if (!admin && c.CurrentOwnerUserId != userId) throw new LiveSupportException(LiveSupportErrorCodes.Forbidden, "المحادثة مملوكة لموظف آخر.");
        return c;
    }

    private async Task<IReadOnlyList<LiveSupportConversationDto>> MapManyAsync(IReadOnlyList<LiveSupportConversation> items, CancellationToken ct)
    {
        var result = new List<LiveSupportConversationDto>(items.Count);
        foreach (var item in items) result.Add(await MapAsync(item, ct));
        return result;
    }

    private async Task<LiveSupportConversationDto> MapAsync(LiveSupportConversation c, CancellationToken ct)
    {
        int? position = null;
        if (c.Status == LiveSupportConversationStatus.Waiting && c.QueuedAt.HasValue)
            position = await _db.LiveSupportQueueEntries.CountAsync(x => x.DequeuedAt == null && x.EnteredAt <= c.QueuedAt, ct);
        var isAiActive = await _db.LiveSupportAIConversationStates.AnyAsync(x => x.ConversationId == c.Id && x.Mode == LiveSupportAIMode.AiActive, ct);
        var isAiTyping = isAiActive && await _db.LiveSupportAITurns.AnyAsync(x => x.ConversationId == c.Id && (x.Status == LiveSupportAITurnStatus.Queued || x.Status == LiveSupportAITurnStatus.Processing), ct);
        
        LiveSupportAISummaryDto? aiSummary = null;
        var state = await _db.LiveSupportAIConversationStates.AsNoTracking().FirstOrDefaultAsync(x => x.ConversationId == c.Id, ct);
        if (state != null)
        {
            var policyVersion = await _db.LiveSupportAIPolicyVersions.AsNoTracking()
                .Where(x => x.Id == state.PolicyVersionId)
                .Select(x => (long?)x.VersionNumber)
                .FirstOrDefaultAsync(ct);

            var verificationSession = await _db.LiveSupportAIVerificationSessions.AsNoTracking()
                .Where(x => x.ConversationId == c.Id)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => x.Status.ToString())
                .FirstOrDefaultAsync(ct);

            var attemptedActions = await _db.LiveSupportAIPendingActions.AsNoTracking()
                .Where(x => x.ConversationId == c.Id)
                .Select(x => x.ActionKey)
                .Distinct()
                .ToListAsync(ct);

            var failedTurnErrors = await _db.LiveSupportAITurns.AsNoTracking()
                .Where(x => x.ConversationId == c.Id && x.Status == LiveSupportAITurnStatus.Failed && x.FailureCode != null)
                .Select(x => x.FailureCode!)
                .Distinct()
                .ToListAsync(ct);

            aiSummary = new LiveSupportAISummaryDto(
                state.HandoffSafeSummary,
                state.HandoffReasonCode,
                policyVersion,
                verificationSession,
                attemptedActions,
                failedTurnErrors
            );
        }

        return new LiveSupportConversationDto(c.Id, c.ParticipantType, c.Status, c.CurrentOwnerUserId, c.LinkedStudentUserId, c.Subject, c.CreatedAt, c.QueuedAt, c.AssignedAt, c.ClosedAt, position, c.Version, !IsTerminal(c.Status), c.Status == LiveSupportConversationStatus.Closed && !await _db.LiveSupportRatings.AnyAsync(x => x.ConversationId == c.Id, ct), isAiActive, isAiTyping, aiSummary);
    }

    public async Task<LiveSupportAITurnContextDto?> ClaimAITurnAsync(Guid turnId, CancellationToken ct)
    {
        var turn = await _db.LiveSupportAITurns.FirstOrDefaultAsync(x => x.Id == turnId, ct);
        if (turn is null) return null;

        turn.Status = LiveSupportAITurnStatus.Processing;
        turn.StartedAt = DateTime.UtcNow;
        turn.Version++;
        await _db.SaveChangesAsync(ct);

        var conversation = await _db.LiveSupportConversations.FirstOrDefaultAsync(x => x.Id == turn.ConversationId, ct);
        if (conversation is null) throw new LiveSupportException("NOT_FOUND", "Conversation not found.");

        var policy = await _db.LiveSupportAIPolicyVersions.FirstOrDefaultAsync(x => x.Id == turn.PolicyVersionId, ct);
        if (policy is null) throw new LiveSupportException("NOT_FOUND", "AI Policy version not found.");

        var knowledgeRevisionIds = await _db.LiveSupportAIPolicyKnowledgeRevisions
            .Where(x => x.PolicyVersionId == turn.PolicyVersionId)
            .Select(x => x.KnowledgeRevisionId)
            .ToListAsync(ct);

        var knowledgeDocs = await _db.LiveSupportAIKnowledgeRevisions
            .Where(x => knowledgeRevisionIds.Contains(x.Id) && x.IsPublished)
            .Select(x => x.Content)
            .ToListAsync(ct);

        // Inject dynamic student profile context if linked
        if (conversation.LinkedStudentUserId.HasValue)
        {
            var readableKeys = System.Text.Json.JsonSerializer.Deserialize<List<string>>(policy.ReadableDataKeysJson) ?? new List<string>();
            var studentContext = await BuildStudentContextAsync(conversation.LinkedStudentUserId.Value, readableKeys, ct);
            if (!string.IsNullOrEmpty(studentContext))
            {
                knowledgeDocs.Add(studentContext);
            }
        }

        // Dynamically build system instructions by appending action instructions and schemas
        var systemInstructions = policy.SystemInstructions;
        var actionKeys = System.Text.Json.JsonSerializer.Deserialize<List<string>>(policy.ActionKeysJson) ?? new List<string>();
        if (actionKeys.Any())
        {
            var instructionsBuilder = new System.Text.StringBuilder(systemInstructions);
            instructionsBuilder.AppendLine("\n\n--- ALLOWED ACTIONS ---");
            instructionsBuilder.AppendLine("You are permitted to propose the following administrative actions. When a student requests one, use the `propose_action` type, passing the exact action key, arguments, and an Arabic description of the effect.");
            foreach (var key in actionKeys)
            {
                if (NaderGorge.Application.Features.LiveSupportAI.Services.LiveSupportAICatalog.Actions.TryGetValue(key, out var actionDto))
                {
                    instructionsBuilder.AppendLine($"- Action Key: \"{key}\"");
                    instructionsBuilder.AppendLine($"  Description: {actionDto.Description}");
                    var argsSchema = GetActionArgumentsSchema(key);
                    instructionsBuilder.AppendLine($"  Arguments Schema: {argsSchema}");
                }
            }
            systemInstructions = instructionsBuilder.ToString();
        }

        var messages = await _db.LiveSupportMessages
            .Where(x => x.ConversationId == turn.ConversationId)
            .OrderBy(x => x.SentAt)
            .Select(x => ToDto(x))
            .ToListAsync(ct);

        return new LiveSupportAITurnContextDto(
            turn.Id,
            turn.ConversationId,
            turn.PolicyVersionId,
            turn.ExpectedConversationVersion,
            systemInstructions,
            knowledgeDocs,
            messages,
            conversation.ParticipantType.ToString()
        );
    }

    public async Task CompleteAITurnAsync(Guid turnId, LiveSupportAITurnCompleteRequest request, CancellationToken ct)
    {
        var turn = await _db.LiveSupportAITurns.FirstOrDefaultAsync(x => x.Id == turnId, ct);
        if (turn is null) throw new LiveSupportException("NOT_FOUND", "AI Turn not found.");

        if (turn.Status == LiveSupportAITurnStatus.Completed || turn.Status == LiveSupportAITurnStatus.DiscardedAfterHandoff)
        {
            return;
        }

        var conversation = await _db.LiveSupportConversations.FirstOrDefaultAsync(x => x.Id == turn.ConversationId, ct);
        if (conversation is null) throw new LiveSupportException("NOT_FOUND", "Conversation not found.");

        if (conversation.Version != request.ExpectedConversationVersion)
        {
            turn.Status = LiveSupportAITurnStatus.DiscardedAfterHandoff;
            turn.FailureCode = "CONVERSATION_VERSION_MISMATCH";
            turn.SafeFailureDetail = $"Expected version {request.ExpectedConversationVersion} but got {conversation.Version}.";
            await _db.SaveChangesAsync(ct);
            return;
        }

        var aiState = await _db.LiveSupportAIConversationStates.FirstOrDefaultAsync(x => x.ConversationId == turn.ConversationId, ct);
        if (aiState is null || aiState.Mode != LiveSupportAIMode.AiActive)
        {
            turn.Status = LiveSupportAITurnStatus.DiscardedAfterHandoff;
            turn.FailureCode = "AI_INACTIVE";
            turn.SafeFailureDetail = "AI is no longer active on this conversation.";
            await _db.SaveChangesAsync(ct);
            return;
        }

        var policy = await _db.LiveSupportAIPolicyVersions.FirstOrDefaultAsync(x => x.Id == turn.PolicyVersionId, ct);
        if (policy is null) throw new LiveSupportException("NOT_FOUND", "AI Policy version not found.");

        await using var tx = await _db.BeginTransactionAsync(IsolationLevel.Serializable, ct);


        if (request.Decision.Type == "reply")
        {
            var content = request.Decision.MessageAr ?? string.Empty;
            var message = new LiveSupportMessage
            {
                ConversationId = turn.ConversationId,
                SenderType = LiveSupportSenderType.AI,
                ClientMessageId = $"ai-{turn.Id:N}",
                Type = LiveSupportMessageType.Text,
                Content = content.Trim(),
                SentAt = DateTime.UtcNow
            };
            _db.LiveSupportMessages.Add(message);
            conversation.LastMessageAt = message.SentAt;
            conversation.Version++;

            turn.Status = LiveSupportAITurnStatus.Completed;
            turn.DecisionType = LiveSupportAIDecisionType.Reply;
            turn.OutputMessageId = message.Id;
            turn.Provider = request.Provider;
            turn.Model = request.Model;
            turn.ProviderResponseId = request.ProviderResponseId;
            turn.InputTokenCount = request.InputTokenCount;
            turn.OutputTokenCount = request.OutputTokenCount;
            turn.LatencyMs = request.LatencyMs;
            turn.CompletedAt = DateTime.UtcNow;
            turn.Version++;

            AddEvent(conversation.Id, LiveSupportEventType.MessageSent, null, null, message.Id);
            AddEvent(conversation.Id, LiveSupportEventType.AIReplySent, null, null, message.Id);
            AddEvent(conversation.Id, LiveSupportEventType.AITurnCompleted, null, null, turn.Id);

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        else if (request.Decision.Type == "handoff")
        {
            if (!string.IsNullOrWhiteSpace(request.Decision.MessageAr))
            {
                var message = new LiveSupportMessage
                {
                    ConversationId = conversation.Id,
                    SenderType = LiveSupportSenderType.AI,
                    ClientMessageId = $"ai-{turn.Id:N}-handoff",
                    Type = LiveSupportMessageType.Text,
                    Content = request.Decision.MessageAr.Trim(),
                    SentAt = DateTime.UtcNow
                };
                _db.LiveSupportMessages.Add(message);
                conversation.LastMessageAt = message.SentAt;
                conversation.Version++;
                turn.OutputMessageId = message.Id;

                AddEvent(conversation.Id, LiveSupportEventType.MessageSent, null, null, message.Id);
                AddEvent(conversation.Id, LiveSupportEventType.AIReplySent, null, null, message.Id);
            }

            aiState.HandoffReasonCode = request.Decision.Handoff?.ReasonCode ?? "USER_REQUEST";
            aiState.HandoffSafeSummary = request.Decision.Handoff?.SafeSummaryAr ?? "طلب التحويل لموظف بشري";
            aiState.Version++;

            var expirySeconds = policy.PendingActionExpirySeconds > 0 ? policy.PendingActionExpirySeconds : 300;
            var pendingAction = new LiveSupportAIPendingAction
            {
                ConversationId = turn.ConversationId,
                TurnId = turn.Id,
                StudentUserId = conversation.LinkedStudentUserId ?? Guid.Empty,
                PolicyVersionId = turn.PolicyVersionId,
                ActionKey = "system.handoff",
                SafeProposalJson = System.Text.Json.JsonSerializer.Serialize(new {
                    reasonCode = aiState.HandoffReasonCode,
                    safeSummaryAr = aiState.HandoffSafeSummary
                }),
                Status = LiveSupportAIPendingActionStatus.PendingConfirmation,
                ExpiresAt = DateTime.UtcNow.AddSeconds(expirySeconds),
                IdempotencyKey = Guid.NewGuid(),
                Version = 1
            };
            _db.LiveSupportAIPendingActions.Add(pendingAction);

            turn.Status = LiveSupportAITurnStatus.Completed;
            turn.DecisionType = LiveSupportAIDecisionType.Handoff;
            turn.Provider = request.Provider;
            turn.Model = request.Model;
            turn.ProviderResponseId = request.ProviderResponseId;
            turn.InputTokenCount = request.InputTokenCount;
            turn.OutputTokenCount = request.OutputTokenCount;
            turn.LatencyMs = request.LatencyMs;
            turn.CompletedAt = DateTime.UtcNow;
            turn.Version++;

            AddEvent(conversation.Id, LiveSupportEventType.AIHandoffRequested, null, null, turn.Id);
            AddEvent(conversation.Id, LiveSupportEventType.AITurnCompleted, null, null, turn.Id);

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        else if (request.Decision.Type == "propose_action")
        {
            if (request.Decision.Action == null) throw new LiveSupportException("VALIDATION_ERROR", "Action payload is required.");

            var actionKeys = System.Text.Json.JsonSerializer.Deserialize<List<string>>(policy.ActionKeysJson) ?? new List<string>();
            if (!actionKeys.Contains(request.Decision.Action.Key))
                throw new LiveSupportException("VALIDATION_ERROR", $"Action key '{request.Decision.Action.Key}' is not allowed by active policy.");

            if (!string.IsNullOrWhiteSpace(request.Decision.MessageAr))
            {
                var message = new LiveSupportMessage
                {
                    ConversationId = conversation.Id,
                    SenderType = LiveSupportSenderType.AI,
                    ClientMessageId = $"ai-{turn.Id:N}-propose",
                    Type = LiveSupportMessageType.Text,
                    Content = request.Decision.MessageAr.Trim(),
                    SentAt = DateTime.UtcNow
                };
                _db.LiveSupportMessages.Add(message);
                conversation.LastMessageAt = message.SentAt;
                conversation.Version++;
                turn.OutputMessageId = message.Id;

                AddEvent(conversation.Id, LiveSupportEventType.MessageSent, null, null, message.Id);
                AddEvent(conversation.Id, LiveSupportEventType.AIReplySent, null, null, message.Id);
            }

            var expirySeconds = policy.PendingActionExpirySeconds > 0 ? policy.PendingActionExpirySeconds : 300;
            var argsJson = request.Decision.Action.Arguments != null ? request.Decision.Action.Arguments.ToString() : "{}";
            var pendingAction = new LiveSupportAIPendingAction
            {
                ConversationId = turn.ConversationId,
                TurnId = turn.Id,
                StudentUserId = conversation.LinkedStudentUserId ?? Guid.Empty,
                PolicyVersionId = turn.PolicyVersionId,
                ActionKey = request.Decision.Action.Key,
                SafeProposalJson = System.Text.Json.JsonSerializer.Serialize(request.Decision.Action),
                EncryptedPayload = System.Text.Encoding.UTF8.GetBytes(argsJson),
                Status = LiveSupportAIPendingActionStatus.PendingConfirmation,
                ExpiresAt = DateTime.UtcNow.AddSeconds(expirySeconds),
                IdempotencyKey = Guid.NewGuid(),
                Version = 1
            };
            _db.LiveSupportAIPendingActions.Add(pendingAction);

            turn.Status = LiveSupportAITurnStatus.Completed;
            turn.DecisionType = LiveSupportAIDecisionType.ProposeAction;
            turn.Provider = request.Provider;
            turn.Model = request.Model;
            turn.ProviderResponseId = request.ProviderResponseId;
            turn.InputTokenCount = request.InputTokenCount;
            turn.OutputTokenCount = request.OutputTokenCount;
            turn.LatencyMs = request.LatencyMs;
            turn.CompletedAt = DateTime.UtcNow;
            turn.Version++;

            AddEvent(conversation.Id, LiveSupportEventType.AIActionProposed, null, null, pendingAction.Id);
            AddEvent(conversation.Id, LiveSupportEventType.AITurnCompleted, null, null, turn.Id);

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        else if (request.Decision.Type == "request_verification")
        {
            if (!string.IsNullOrWhiteSpace(request.Decision.MessageAr))
            {
                var message = new LiveSupportMessage
                {
                    ConversationId = conversation.Id,
                    SenderType = LiveSupportSenderType.AI,
                    ClientMessageId = $"ai-{turn.Id:N}-verify",
                    Type = LiveSupportMessageType.Text,
                    Content = request.Decision.MessageAr.Trim(),
                    SentAt = DateTime.UtcNow
                };
                _db.LiveSupportMessages.Add(message);
                conversation.LastMessageAt = message.SentAt;
                conversation.Version++;
                turn.OutputMessageId = message.Id;

                AddEvent(conversation.Id, LiveSupportEventType.MessageSent, null, null, message.Id);
                AddEvent(conversation.Id, LiveSupportEventType.AIReplySent, null, null, message.Id);
            }

            var expirySeconds = policy.PendingActionExpirySeconds > 0 ? policy.PendingActionExpirySeconds : 300;
            var pendingAction = new LiveSupportAIPendingAction
            {
                ConversationId = turn.ConversationId,
                TurnId = turn.Id,
                StudentUserId = conversation.LinkedStudentUserId ?? Guid.Empty,
                PolicyVersionId = turn.PolicyVersionId,
                ActionKey = "system.verification",
                SafeProposalJson = "{}",
                Status = LiveSupportAIPendingActionStatus.PendingConfirmation,
                ExpiresAt = DateTime.UtcNow.AddSeconds(expirySeconds),
                IdempotencyKey = Guid.NewGuid(),
                Version = 1
            };
            _db.LiveSupportAIPendingActions.Add(pendingAction);

            turn.Status = LiveSupportAITurnStatus.Completed;
            turn.DecisionType = LiveSupportAIDecisionType.RequestVerification;
            turn.Provider = request.Provider;
            turn.Model = request.Model;
            turn.ProviderResponseId = request.ProviderResponseId;
            turn.InputTokenCount = request.InputTokenCount;
            turn.OutputTokenCount = request.OutputTokenCount;
            turn.LatencyMs = request.LatencyMs;
            turn.CompletedAt = DateTime.UtcNow;
            turn.Version++;

            AddEvent(conversation.Id, LiveSupportEventType.AIActionProposed, null, null, pendingAction.Id);
            AddEvent(conversation.Id, LiveSupportEventType.AITurnCompleted, null, null, turn.Id);

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        else if (request.Decision.Type == "propose_account_creation")
        {
            if (!string.IsNullOrWhiteSpace(request.Decision.MessageAr))
            {
                var message = new LiveSupportMessage
                {
                    ConversationId = conversation.Id,
                    SenderType = LiveSupportSenderType.AI,
                    ClientMessageId = $"ai-{turn.Id:N}-register",
                    Type = LiveSupportMessageType.Text,
                    Content = request.Decision.MessageAr.Trim(),
                    SentAt = DateTime.UtcNow
                };
                _db.LiveSupportMessages.Add(message);
                conversation.LastMessageAt = message.SentAt;
                conversation.Version++;
                turn.OutputMessageId = message.Id;

                AddEvent(conversation.Id, LiveSupportEventType.MessageSent, null, null, message.Id);
                AddEvent(conversation.Id, LiveSupportEventType.AIReplySent, null, null, message.Id);
            }

            var expirySeconds = policy.PendingActionExpirySeconds > 0 ? policy.PendingActionExpirySeconds : 300;
            var pendingAction = new LiveSupportAIPendingAction
            {
                ConversationId = turn.ConversationId,
                TurnId = turn.Id,
                StudentUserId = conversation.LinkedStudentUserId ?? Guid.Empty,
                PolicyVersionId = turn.PolicyVersionId,
                ActionKey = "system.registration",
                SafeProposalJson = "{}",
                Status = LiveSupportAIPendingActionStatus.PendingConfirmation,
                ExpiresAt = DateTime.UtcNow.AddSeconds(expirySeconds),
                IdempotencyKey = Guid.NewGuid(),
                Version = 1
            };
            _db.LiveSupportAIPendingActions.Add(pendingAction);

            turn.Status = LiveSupportAITurnStatus.Completed;
            turn.DecisionType = LiveSupportAIDecisionType.ProposeAccountCreation;
            turn.Provider = request.Provider;
            turn.Model = request.Model;
            turn.ProviderResponseId = request.ProviderResponseId;
            turn.InputTokenCount = request.InputTokenCount;
            turn.OutputTokenCount = request.OutputTokenCount;
            turn.LatencyMs = request.LatencyMs;
            turn.CompletedAt = DateTime.UtcNow;
            turn.Version++;

            AddEvent(conversation.Id, LiveSupportEventType.AIActionProposed, null, null, pendingAction.Id);
            AddEvent(conversation.Id, LiveSupportEventType.AITurnCompleted, null, null, turn.Id);

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        else
        {
            throw new LiveSupportException("VALIDATION_ERROR", $"Unsupported decision type: {request.Decision.Type}");
        }
    }

    public async Task FailAITurnAsync(Guid turnId, LiveSupportAITurnFailRequest request, CancellationToken ct)
    {
        var turn = await _db.LiveSupportAITurns.FirstOrDefaultAsync(x => x.Id == turnId, ct);
        if (turn is null) throw new LiveSupportException("NOT_FOUND", "AI Turn not found.");

        if (turn.Status == LiveSupportAITurnStatus.Completed || turn.Status == LiveSupportAITurnStatus.Failed || turn.Status == LiveSupportAITurnStatus.DiscardedAfterHandoff)
        {
            return;
        }

        var conversation = await _db.LiveSupportConversations.FirstOrDefaultAsync(x => x.Id == turn.ConversationId, ct);
        if (conversation is null) throw new LiveSupportException("NOT_FOUND", "Conversation not found.");

        turn.Status = LiveSupportAITurnStatus.Failed;
        turn.FailureCode = request.FailureCode;
        turn.SafeFailureDetail = request.SafeFailureDetail;
        turn.Provider = request.Provider;
        turn.Model = request.Model;
        turn.LatencyMs = request.LatencyMs;
        turn.CompletedAt = DateTime.UtcNow;
        turn.Version++;

        var aiState = await _db.LiveSupportAIConversationStates.FirstOrDefaultAsync(x => x.ConversationId == turn.ConversationId, ct);
        if (aiState is not null && aiState.Mode == LiveSupportAIMode.AiActive)
        {
            if (_handoffService == null) throw new InvalidOperationException("Handoff service is not available.");
            await _handoffService.HandoffAsync(
                turn.ConversationId,
                participant: null,
                actorUserId: null,
                reasonCode: "AI_TURN_FAILED",
                safeSummary: $"فشل المساعد الذكي في معالجة الطلب: {request.FailureCode}",
                forced: true,
                idempotencyKey: $"turn-fail-{turnId}",
                cancellationToken: ct);

            AddEvent(conversation.Id, LiveSupportEventType.AITurnFailed, null, null, turn.Id);
            await _db.SaveChangesAsync(ct);
        }
        else
        {
            AddEvent(conversation.Id, LiveSupportEventType.AITurnFailed, null, null, turn.Id);
            await _db.SaveChangesAsync(ct);
        }
    }

    private async Task<LiveSupportStaffConfigDto> MapStaffConfigAsync(LiveSupportStaffConfig config, CancellationToken ct)
    {
        var name = await _db.Users.Where(x => x.Id == config.UserId).Select(x => x.FullName).FirstOrDefaultAsync(ct) ?? "موظف";
        var schedule = await _db.LiveSupportScheduleWindows.AsNoTracking().Where(x => x.StaffConfigId == config.Id && x.IsActive).OrderBy(x => x.DayOfWeek).ThenBy(x => x.StartLocalTime).Select(x => new LiveSupportScheduleWindowDto(x.DayOfWeek, x.StartLocalTime, x.EndLocalTime)).ToListAsync(ct);
        return new LiveSupportStaffConfigDto(config.UserId, name, config.IsEnabled, config.MaxActiveConversations,
            await _db.LiveSupportAssignments.CountAsync(x => x.StaffUserId == config.UserId && x.EndedAt == null, ct), await IsCheckedInAsync(config.UserId, ct), config.Version, schedule);
    }

    private async Task<LiveSupportAdminConversationDto> MapAdminConversationAsync(LiveSupportConversation c, CancellationToken ct)
    {
        var participantName = c.ParticipantType == LiveSupportParticipantType.Student
            ? await _db.Users.Where(x => x.Id == c.StudentUserId).Select(x => x.FullName).FirstOrDefaultAsync(ct)
            : await _db.LiveSupportGuestSessions.Where(x => x.Id == c.GuestSessionId).Select(x => x.DisplayName).FirstOrDefaultAsync(ct);
        var ownerName = c.CurrentOwnerUserId.HasValue ? await _db.Users.Where(x => x.Id == c.CurrentOwnerUserId).Select(x => x.FullName).FirstOrDefaultAsync(ct) : null;
        return new LiveSupportAdminConversationDto(c.Id, participantName ?? "غير معروف", c.ParticipantType, c.Status, ownerName, c.CreatedAt, c.AssignedAt, c.FirstStaffResponseAt, c.ClosedAt,
            c.AssignedAt.HasValue ? (c.AssignedAt.Value - c.CreatedAt).TotalSeconds : null, c.ClosedAt.HasValue && c.AssignedAt.HasValue ? (c.ClosedAt.Value - c.AssignedAt.Value).TotalSeconds : null, c.Subject, null, null);
    }

    private async Task<string?> ActorNameAsync(Guid? userId, Guid? guestId, CancellationToken ct)
    {
        if (userId.HasValue) return await _db.Users.Where(x => x.Id == userId).Select(x => x.FullName).FirstOrDefaultAsync(ct);
        if (guestId.HasValue) return await _db.LiveSupportGuestSessions.Where(x => x.Id == guestId).Select(x => x.DisplayName).FirstOrDefaultAsync(ct);
        return "النظام";
    }

    private static string EventSummary(LiveSupportEventType type) => type switch
    {
        LiveSupportEventType.ConversationCreated => "تم إنشاء المحادثة",
        LiveSupportEventType.QueueEntered => "دخلت المحادثة الطابور",
        LiveSupportEventType.Assigned => "تم إسناد المحادثة",
        LiveSupportEventType.MessageSent => "تم إرسال رسالة",
        LiveSupportEventType.FirstStaffResponse => "أول رد من الموظف",
        LiveSupportEventType.Closed => "تم إغلاق المحادثة",
        LiveSupportEventType.RatingSubmitted => "أرسل المستخدم التقييم",
        LiveSupportEventType.StudentLinked => "تم ربط الطالب",
        LiveSupportEventType.StudentUnlinked => "تم إلغاء ربط الطالب",
        LiveSupportEventType.StudentLinkReplaced => "تم استبدال الطالب المرتبط",
        LiveSupportEventType.StaffDisconnected => "انقطع الموظف وأعيدت المحادثة للتوزيع",
        _ => type.ToString()
    };

    private static void ValidateSchedule(IReadOnlyList<LiveSupportScheduleWindowDto> schedule)
    {
        if (schedule.Any(x => x.DayOfWeek is < 0 or > 6 || x.EndLocalTime <= x.StartLocalTime)) throw new LiveSupportException("VALIDATION_ERROR", "فترة الدعم غير صحيحة.");
        foreach (var day in schedule.GroupBy(x => x.DayOfWeek))
        {
            var sorted = day.OrderBy(x => x.StartLocalTime).ToArray();
            for (var i = 1; i < sorted.Length; i++) if (sorted[i].StartLocalTime < sorted[i - 1].EndLocalTime) throw new LiveSupportException("VALIDATION_ERROR", "فترات الدعم متداخلة.");
        }
    }

    private void AddEvent(Guid conversationId, LiveSupportEventType type, Guid? actor, Guid? guest, Guid? relatedId = null)
    {
        var eventId = Guid.NewGuid();
        var occurredAt = DateTime.UtcNow;
        var sequence = occurredAt.Ticks;
        _db.LiveSupportEvents.Add(new LiveSupportEvent { Id = eventId, ConversationId = conversationId, Type = type, ActorUserId = actor, ActorGuestSessionId = guest, RelatedEntityId = relatedId, OccurredAt = occurredAt, Sequence = sequence });
        var payload = System.Text.Json.JsonSerializer.Serialize(new { eventId, conversationId, sequence, occurredAt, type = type.ToString(), payload = new { relatedId } });
        _db.OutboxEvents.Add(new OutboxEvent { Type = "LiveSupportEvent", TargetGroup = $"LiveSupport:Conversation:{conversationId:N}", PayloadJson = payload });
        _db.OutboxEvents.Add(new OutboxEvent { Type = "LiveSupportEvent", TargetGroup = "LiveSupport:Admins", PayloadJson = payload });
    }
    private static LiveSupportMessageDto ToDto(LiveSupportMessage x) => new(x.Id, x.ConversationId, x.SenderType, x.ClientMessageId, x.Type, x.Content, x.SentAt);
    private static bool IsTerminal(LiveSupportConversationStatus s) => s is LiveSupportConversationStatus.Closed or LiveSupportConversationStatus.Abandoned;
    private static string MaskPhone(string phone) => phone.Length <= 4 ? "****" : $"{phone[..2]}******{phone[^2..]}";
    private static string EncodeCursor(DateTime sentAt, Guid id) => Convert.ToBase64String(Encoding.UTF8.GetBytes($"{sentAt.Ticks}|{id:N}"));
    private static bool TryDecodeCursor(string? cursor, out DateTime sentAt, out Guid id)
    {
        sentAt = default;
        id = default;
        if (string.IsNullOrWhiteSpace(cursor)) return false;
        try
        {
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(cursor)).Split('|', 2);
            return parts.Length == 2 && long.TryParse(parts[0], out var ticks) && ticks > 0 && Guid.TryParseExact(parts[1], "N", out id) && (sentAt = new DateTime(ticks, DateTimeKind.Utc)) != default;
        }
        catch (FormatException) { return false; }
        catch (ArgumentOutOfRangeException) { return false; }
    }

    private async Task AcquireRoutingLockAsync(CancellationToken ct)
    {
        if (_relationalDb?.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
            await _relationalDb.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(14220260621)", ct);
    }

    public async Task ConfirmPendingActionAsync(LiveSupportParticipantIdentity participant, Guid conversationId, Guid proposalId, CancellationToken ct)
    {
        if (_mediator is null) throw new InvalidOperationException("Mediator is required.");
        try
        {
            await _mediator.Send(new ConfirmLiveSupportAIActionCommand(participant, conversationId, proposalId, proposalId.ToString("N")), ct);
        }
        catch (LiveSupportException ex)
        {
            if (ex.Code is "CONFIRMATION_EXPIRED" or "ACTION_REVOKED" or "DECISION_NOT_CONFIRMABLE")
                throw new LiveSupportException("CONFLICT", ex.Message);
            throw;
        }
    }

    public async Task CancelPendingActionAsync(LiveSupportParticipantIdentity participant, Guid conversationId, Guid proposalId, CancellationToken ct)
    {
        if (_mediator is null) throw new InvalidOperationException("Mediator is required.");
        try
        {
            await _mediator.Send(new CancelLiveSupportAIDecisionCommand(participant, conversationId, proposalId, proposalId.ToString("N")), ct);
        }
        catch (LiveSupportException ex)
        {
            if (ex.Code is "DECISION_NOT_CANCELLABLE")
                throw new LiveSupportException("CONFLICT", ex.Message);
            throw;
        }
    }

    public async Task ConfirmHandoffAsync(LiveSupportParticipantIdentity participant, Guid conversationId, CancellationToken ct)
    {
        var conversation = await RequireParticipantConversationAsync(participant, conversationId, ct);

        var aiState = await _db.LiveSupportAIConversationStates.FirstOrDefaultAsync(x => x.ConversationId == conversationId, ct);
        if (aiState == null || aiState.Mode != LiveSupportAIMode.AiActive)
            throw new LiveSupportException("CONFLICT", "Conversation is not in an active AI support state.");

        var action = await _db.LiveSupportAIPendingActions
            .FirstOrDefaultAsync(x => x.ConversationId == conversationId && x.ActionKey == "system.handoff" && x.Status == LiveSupportAIPendingActionStatus.PendingConfirmation, ct);
        if (action == null) throw new LiveSupportException("NOT_FOUND", "No pending handoff proposal found.");

        if (_handoffService == null) throw new InvalidOperationException("Handoff service is not available.");

        await _handoffService.HandoffAsync(
            conversationId,
            participant,
            actorUserId: null,
            reasonCode: aiState.HandoffReasonCode ?? "USER_REQUEST",
            safeSummary: aiState.HandoffSafeSummary ?? "طلب التحويل لموظف بشري",
            forced: false,
            idempotencyKey: $"confirm-{conversationId}",
            cancellationToken: ct);
    }

    public async Task CancelHandoffAsync(LiveSupportParticipantIdentity participant, Guid conversationId, CancellationToken ct)
    {
        var conversation = await RequireParticipantConversationAsync(participant, conversationId, ct);

        var aiState = await _db.LiveSupportAIConversationStates.FirstOrDefaultAsync(x => x.ConversationId == conversationId, ct);
        if (aiState == null || aiState.Mode != LiveSupportAIMode.AiActive)
            throw new LiveSupportException("CONFLICT", "Conversation is not in an active AI support state.");

        var action = await _db.LiveSupportAIPendingActions
            .FirstOrDefaultAsync(x => x.ConversationId == conversationId && x.ActionKey == "system.handoff" && x.Status == LiveSupportAIPendingActionStatus.PendingConfirmation, ct);
        if (action == null) throw new LiveSupportException("NOT_FOUND", "No pending handoff proposal found.");

        await using var tx = await _db.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        action.Status = LiveSupportAIPendingActionStatus.Cancelled;
        action.CompletedAt = DateTime.UtcNow;
        action.Version++;

        var message = new LiveSupportMessage
        {
            ConversationId = conversationId,
            SenderType = LiveSupportSenderType.System,
            ClientMessageId = $"sys-handoff-cancel-{Guid.NewGuid():N}",
            Type = LiveSupportMessageType.Text,
            Content = "[System] رفض الطالب التحويل للدعم البشري ويريد الاستمرار في التحدث معك.",
            SentAt = DateTime.UtcNow
        };
        _db.LiveSupportMessages.Add(message);
        conversation.LastMessageAt = message.SentAt;
        conversation.Version++;

        AddEvent(conversationId, LiveSupportEventType.MessageSent, null, null, message.Id);

        var turn = new LiveSupportAITurn
        {
            ConversationId = conversationId,
            SourceMessageId = message.Id,
            PolicyVersionId = aiState.PolicyVersionId,
            ExpectedConversationVersion = conversation.Version,
            Status = LiveSupportAITurnStatus.Queued,
            QueuedAt = DateTime.UtcNow,
            Version = 1
        };
        _db.LiveSupportAITurns.Add(turn);
        await _db.SaveChangesAsync(ct);

        if (_jobEnqueuer is not null)
        {
            await _jobEnqueuer.EnqueueJobAsync("ai-live-support-turns", "respond", new { turnId = turn.Id, conversationId = conversationId });
        }

        await tx.CommitAsync(ct);
    }

    public async Task<LiveSupportAIVerificationSessionDto> StartVerificationLookupAsync(LiveSupportParticipantIdentity participant, Guid conversationId, LiveSupportLookupRequestDto request, CancellationToken ct)
    {
        if (_aiVerificationService is null) throw new InvalidOperationException("Verification service is not available.");
        var lookupDto = new NaderGorge.Application.Features.LiveSupportAI.Dtos.LiveSupportAIVerificationLookupCommandDto(request.LookupKey, request.Value, Guid.NewGuid().ToString("N"));
        var result = await _aiVerificationService.StartLookupAsync(participant, conversationId, lookupDto, ct);
        return new LiveSupportAIVerificationSessionDto(
            result.SessionId,
            result.Status.ToString(),
            result.PromptText != null ? "profile.governorate" : null,
            result.PromptText,
            result.AttemptCount,
            result.MaxAttempts
        );
    }

    public async Task<LiveSupportAIVerificationSessionDto> SubmitVerificationChallengeAsync(LiveSupportParticipantIdentity participant, Guid conversationId, LiveSupportAnswerChallengeDto request, CancellationToken ct)
    {
        if (_aiVerificationService is null) throw new InvalidOperationException("Verification service is not available.");
        var active = await GetActiveVerificationSessionAsync(participant, conversationId, ct);
        if (active is null) throw new LiveSupportException("NOT_FOUND", "Active verification session not found.");
        
        var answerDto = new NaderGorge.Application.Features.LiveSupportAI.Dtos.LiveSupportAIVerificationAnswerCommandDto(active.SessionId, request.Answer, active.SessionId.ToString("N"));
        var result = await _aiVerificationService.SubmitAnswerAsync(participant, conversationId, answerDto, ct);
        return new LiveSupportAIVerificationSessionDto(
            result.SessionId,
            result.Status.ToString(),
            result.PromptText != null ? "profile.governorate" : null,
            result.PromptText,
            result.AttemptCount,
            result.MaxAttempts
        );
    }

    public async Task ConfirmRegistrationProposalAsync(LiveSupportParticipantIdentity participant, Guid conversationId, LiveSupportRegisterGuestDto request, CancellationToken ct)
    {
        if (_aiRegistrationService is null) throw new InvalidOperationException("Registration service is not available.");
        
        var decision = await _db.LiveSupportAIPendingActions
            .Where(x => x.ConversationId == conversationId && x.DecisionKind == LiveSupportAIPendingDecisionKind.AccountCreation && x.Status == LiveSupportAIPendingActionStatus.PendingConfirmation)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (decision is null) throw new LiveSupportException("NOT_FOUND", "Account creation proposal not found.");

        var dto = new NaderGorge.Application.Features.LiveSupportAI.Dtos.LiveSupportAISecureRegistrationDto(
            decision.Id,
            decision.Id.ToString("N"),
            request.FullName,
            request.PhoneNumber,
            request.Password,
            DateTime.UtcNow.Date.AddYears(-15),
            "Male",
            request.Governorate,
            "Address",
            request.EducationStage,
            request.GradeLevel,
            request.SchoolName,
            request.ParentPhoneNumber
        );

        await _aiRegistrationService.RegisterAndLinkAsync(participant, conversationId, dto, ct);
    }

    public async Task<LiveSupportAIPendingActionDto?> GetActivePendingActionAsync(LiveSupportParticipantIdentity participant, Guid conversationId, CancellationToken ct)
    {
        await RequireParticipantConversationAsync(participant, conversationId, ct);
        var action = await _db.LiveSupportAIPendingActions
            .AsNoTracking()
            .Where(x => x.ConversationId == conversationId && x.Status == LiveSupportAIPendingActionStatus.PendingConfirmation && x.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (action == null) return null;

        return new LiveSupportAIPendingActionDto(
            action.Id,
            action.ActionKey,
            action.SafeProposalJson,
            action.Status.ToString(),
            action.ExpiresAt
        );
    }

    public async Task<LiveSupportAIVerificationSessionDto?> GetActiveVerificationSessionAsync(LiveSupportParticipantIdentity participant, Guid conversationId, CancellationToken ct)
    {
        await RequireParticipantConversationAsync(participant, conversationId, ct);
        var session = await _db.LiveSupportAIVerificationSessions
            .AsNoTracking()
            .Where(x => x.ConversationId == conversationId && x.Status == LiveSupportAIVerificationStatus.Challenging && x.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (session == null) return null;

        var selectedKeys = System.Text.Json.JsonSerializer.Deserialize<List<string>>(session.SelectedQuestionKeysJson) ?? new List<string>();
        var currentQuestionIndex = session.CorrectCount;
        
        string? nextQuestionKey = null;
        string? promptText = null;
        
        if (currentQuestionIndex < selectedKeys.Count)
        {
            nextQuestionKey = selectedKeys[currentQuestionIndex];
            promptText = GetVerificationQuestionPrompt(nextQuestionKey);
        }

        return new LiveSupportAIVerificationSessionDto(
            session.Id,
            session.Status.ToString(),
            nextQuestionKey,
            promptText,
            session.AttemptCount,
            session.MaxAttempts
        );
    }

    public async Task<LiveSupportAIParticipantSnapshotDto> GetParticipantAISnapshotAsync(LiveSupportParticipantIdentity participant, Guid conversationId, CancellationToken ct)
    {
        var conversation = await RequireParticipantConversationAsync(participant, conversationId, ct);
        var state = await _db.LiveSupportAIConversationStates.AsNoTracking().SingleOrDefaultAsync(x => x.ConversationId == conversationId, ct);
        var turn = await _db.LiveSupportAITurns.AsNoTracking().Where(x => x.ConversationId == conversationId)
            .OrderByDescending(x => x.QueuedAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        var pending = await _db.LiveSupportAIPendingActions.AsNoTracking()
            .Where(x => x.ConversationId == conversationId && x.Status == LiveSupportAIPendingActionStatus.PendingConfirmation && x.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        var verification = await _db.LiveSupportAIVerificationSessions.AsNoTracking()
            .Where(x => x.ConversationId == conversationId &&
                (x.Status == LiveSupportAIVerificationStatus.AwaitingLookup || x.Status == LiveSupportAIVerificationStatus.Challenging) &&
                x.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        var messages = await _db.LiveSupportMessages.AsNoTracking().Where(x => x.ConversationId == conversationId)
            .OrderByDescending(x => x.SentAt).ThenByDescending(x => x.Id).Take(50).Select(x => ToDto(x)).ToListAsync(ct);
        messages.Reverse();
        var lastSequence = await _db.LiveSupportEvents.AsNoTracking().Where(x => x.ConversationId == conversationId)
            .Select(x => (long?)x.Sequence).MaxAsync(ct) ?? 0;
        int? queuePosition = null;
        if (conversation.Status == LiveSupportConversationStatus.Waiting && conversation.QueuedAt.HasValue)
            queuePosition = await _db.LiveSupportQueueEntries.CountAsync(x => x.DequeuedAt == null && x.EnteredAt <= conversation.QueuedAt, ct);

        return new LiveSupportAIParticipantSnapshotDto(
            conversation.Id,
            conversation.Status.ToString(),
            state?.Mode,
            lastSequence,
            !IsTerminal(conversation.Status) && (state is null || state.Mode is LiveSupportAIMode.AiActive or LiveSupportAIMode.HumanAssigned),
            turn?.Status.ToString(),
            pending is null ? null : new LiveSupportAIPendingDecisionDto(pending.Id, pending.DecisionKind, pending.ActionKey, pending.SafeProposalJson, pending.Status, pending.ExpiresAt, pending.FailureCode),
            verification is null ? null : new LiveSupportAIVerificationStateDto(verification.Id, verification.Status, null, verification.AttemptCount, verification.MaxAttempts),
            queuePosition,
            messages.Cast<object>().ToList());
    }

    private async Task<string> BuildStudentContextAsync(Guid studentUserId, List<string> readableKeys, CancellationToken ct)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("--- STUDENT PROFILE CONTEXT ---");

        var user = await _db.Users
            .Include(x => x.StudentProfile)
            .FirstOrDefaultAsync(x => x.Id == studentUserId, ct);

        if (user == null)
        {
            sb.AppendLine("No linked student profile found.");
            return sb.ToString();
        }

        foreach (var key in readableKeys)
        {
            switch (key)
            {
                case "identity.basic":
                    sb.AppendLine("[identity.basic]");
                    sb.AppendLine($"- Student ID: {user.Id}");
                    sb.AppendLine($"- Full Name: {user.FullName}");
                    if (user.StudentProfile != null)
                    {
                        sb.AppendLine($"- Student Code: {user.StudentProfile.StudentCode}");
                    }
                    break;

                case "identity.contact":
                    sb.AppendLine("[identity.contact]");
                    sb.AppendLine($"- Phone Number: {user.PhoneNumber}");
                    if (user.StudentProfile != null)
                    {
                        sb.AppendLine($"- Parent Phone: {user.StudentProfile.ParentPhone}");
                        sb.AppendLine($"- Secondary Phone: {user.StudentProfile.SecondaryPhone}");
                    }
                    break;

                case "account.status":
                    sb.AppendLine("[account.status]");
                    sb.AppendLine($"- Is Active: {user.IsActive}");
                    sb.AppendLine($"- Is Profile Complete: {user.IsProfileComplete}");
                    if (!string.IsNullOrWhiteSpace(user.SuspensionReason))
                    {
                        sb.AppendLine($"- Suspension Reason: {user.SuspensionReason}");
                    }
                    break;

                case "education.profile":
                    if (user.StudentProfile != null)
                    {
                        sb.AppendLine("[education.profile]");
                        sb.AppendLine($"- Education Stage: {user.StudentProfile.EducationStage}");
                        sb.AppendLine($"- Grade Level: {user.StudentProfile.GradeLevel}");
                        sb.AppendLine($"- Governorate: {user.StudentProfile.Governorate}");
                        sb.AppendLine($"- School Name: {user.StudentProfile.SchoolName}");
                    }
                    break;

                case "packages.active":
                    sb.AppendLine("[packages.active]");
                    var activePackageGrants = await _db.StudentAccessGrants
                        .Where(x => x.UserId == studentUserId && x.PackageId != null && x.IsActive && (x.ExpiresAt == null || x.ExpiresAt > DateTime.UtcNow))
                        .ToListAsync(ct);
                    if (activePackageGrants.Any())
                    {
                        var packageIds = activePackageGrants.Select(x => x.PackageId!.Value).ToList();
                        var packages = await _db.Packages.Where(x => packageIds.Contains(x.Id)).ToListAsync(ct);
                        foreach (var grant in activePackageGrants)
                        {
                            var pkg = packages.FirstOrDefault(x => x.Id == grant.PackageId);
                            sb.AppendLine($"- Package: {(pkg != null ? pkg.Name : grant.PackageId.ToString())} (Granted: {grant.GrantedAt:yyyy-MM-dd}, Expires: {(grant.ExpiresAt.HasValue ? grant.ExpiresAt.Value.ToString("yyyy-MM-dd") : "Never")})");
                        }
                    }
                    else
                    {
                        sb.AppendLine("- No active packages.");
                    }
                    break;

                case "access.grants":
                    sb.AppendLine("[access.grants]");
                    var activeGrants = await _db.StudentAccessGrants
                        .Where(x => x.UserId == studentUserId && x.IsActive && (x.ExpiresAt == null || x.ExpiresAt > DateTime.UtcNow))
                        .ToListAsync(ct);
                    if (activeGrants.Any())
                    {
                        foreach (var grant in activeGrants)
                        {
                            sb.AppendLine($"- Grant Type: {grant.GrantType}, Target ID: {grant.PackageId ?? grant.LessonId ?? grant.LessonVideoId ?? grant.ExamId} (Granted: {grant.GrantedAt:yyyy-MM-dd})");
                        }
                    }
                    else
                    {
                        sb.AppendLine("- No active access grants.");
                    }
                    break;

                case "devices.summary":
                    sb.AppendLine("[devices.summary]");
                    var devices = await _db.Devices
                        .Where(x => x.UserId == studentUserId && x.IsActive)
                        .ToListAsync(ct);
                    if (devices.Any())
                    {
                        foreach (var dev in devices)
                        {
                            sb.AppendLine($"- Device: {dev.DeviceType} - {dev.OsName} {dev.BrowserName} (IP: {dev.IpAddress}, Fingerprint: {dev.DeviceFingerprint}, Last Used: {dev.LastUsedAt:yyyy-MM-dd HH:mm:ss})");
                        }
                    }
                    else
                    {
                        sb.AppendLine("- No registered active devices.");
                    }
                    break;

                case "balance.summary":
                    sb.AppendLine("[balance.summary]");
                    var balanceObj = await _db.StudentBalances
                        .FirstOrDefaultAsync(x => x.UserId == studentUserId, ct);
                    sb.AppendLine($"- Balance Amount: {(balanceObj != null ? balanceObj.CurrentBalance : 0)} EGP");
                    break;

                case "watch.summary":
                    sb.AppendLine("[watch.summary]");
                    var watchEvents = await _db.VideoWatchEvents
                        .Include(x => x.LessonVideo)
                        .Where(x => x.UserId == studentUserId)
                        .ToListAsync(ct);
                    if (watchEvents.Any())
                    {
                        foreach (var we in watchEvents)
                        {
                            sb.AppendLine($"- Video: {(we.LessonVideo != null ? we.LessonVideo.Title : we.LessonVideoId.ToString())} (Watch Count: {we.WatchCount}, Locked: {we.IsLocked}, Custom Max: {we.CustomMaxWatchCount})");
                        }
                    }
                    else
                    {
                        sb.AppendLine("- No video watch events recorded.");
                    }
                    break;

                case "exams.summary":
                    sb.AppendLine("[exams.summary]");
                    var examAttempts = await _db.StudentExamAttempts
                        .Include(x => x.Exam)
                        .Where(x => x.UserId == studentUserId)
                        .OrderByDescending(x => x.StartedAt)
                        .Take(10)
                        .ToListAsync(ct);
                    if (examAttempts.Any())
                    {
                        foreach (var attempt in examAttempts)
                        {
                            sb.AppendLine($"- Exam: {attempt.Exam.Title} (Score: {attempt.ScoreAchieved}, Passed: {attempt.IsPassed}, Evaluation: {attempt.Evaluation}, Started: {attempt.StartedAt})");
                        }
                    }
                    else
                    {
                        sb.AppendLine("- No exam attempts recorded.");
                    }
                    break;

                case "requests.summary":
                    sb.AppendLine("[requests.summary]");
                    var watchRequests = await _db.ExtraWatchRequests
                        .Include(x => x.LessonVideo)
                        .Where(x => x.UserId == studentUserId)
                        .OrderByDescending(x => x.CreatedAt)
                        .Take(10)
                        .ToListAsync(ct);
                    if (watchRequests.Any())
                    {
                        foreach (var req in watchRequests)
                        {
                            sb.AppendLine($"- Extra Watch Request for Video: {req.LessonVideo.Title} (Status: {req.Status}, Resolved At: {req.ResolvedAt})");
                        }
                    }
                    else
                    {
                        sb.AppendLine("- No extra watch requests found.");
                    }
                    break;

                case "homework.summary":
                    sb.AppendLine("[homework.summary]");
                    var homeworks = await _db.Homeworks.ToListAsync(ct);
                    sb.AppendLine($"- Total Homeworks on Platform: {homeworks.Count}");
                    break;
            }
        }

        return sb.ToString();
    }

    private string GetActionArgumentsSchema(string key) => key switch
    {
        "student.lesson.unlock" => "{\"lessonId\": \"Guid\"}",
        "student.devices.disconnect-all" => "{}",
        "student.device.disconnect" => "{\"deviceId\": \"Guid\"}",
        "student.watch.reset" => "{\"lessonVideoId\": \"Guid\"}",
        "student.watch-request.approve" => "{\"requestId\": \"Guid\"}",
        "student.create-and-link" => "{\"fullName\": \"string\", \"phoneNumber\": \"string\", \"password\": \"string\", \"governorate\": \"string\", \"educationStage\": \"string\", \"gradeLevel\": \"string\", \"schoolName\": \"string\", \"parentPhoneNumber\": \"string\"}",
        _ => "{}"
    };

    private string GetVerificationQuestionPrompt(string key) => key switch
    {
        "profile.full_name" => "ما هو اسمك بالكامل المسجل في المنصة؟",
        "profile.birth_date" => "ما هو تاريخ ميلادك؟ (مثال: 2008-05-15)",
        "profile.governorate" => "ما هي المحافظة المسجلة بحسابك؟",
        "profile.school_name" => "ما هو اسم المدرسة المسجل بحسابك؟",
        "contact.parent_phone_last4" => "اكتب آخر 4 أرقام من هاتف ولي الأمر المسجل.",
        _ => "أجب على سؤال التحقق التالي لتأكيد هويتك."
    };

    private bool ValidateVerificationAnswer(User student, string key, string answer)
    {
        if (string.IsNullOrWhiteSpace(answer)) return false;

        var cleanAnswer = NormalizeText(answer);

        switch (key)
        {
            case "profile.full_name":
                return NormalizeText(student.FullName) == cleanAnswer;

            case "profile.birth_date":
                if (student.StudentProfile == null) return false;
                var birthDateStr = student.StudentProfile.DateOfBirth.ToString("yyyy-MM-dd");
                return birthDateStr == cleanAnswer;

            case "profile.governorate":
                if (student.StudentProfile == null) return false;
                return NormalizeText(student.StudentProfile.Governorate) == cleanAnswer;

            case "profile.school_name":
                if (student.StudentProfile == null) return false;
                return NormalizeText(student.StudentProfile.SchoolName ?? string.Empty) == cleanAnswer;

            case "contact.parent_phone_last4":
                if (student.StudentProfile == null || string.IsNullOrEmpty(student.StudentProfile.ParentPhone)) return false;
                var parentPhone = student.StudentProfile.ParentPhone.Trim();
                if (parentPhone.Length < 4) return false;
                var last4 = parentPhone[^4..];
                return last4 == cleanAnswer;

            default:
                return false;
        }
    }

    private string NormalizeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var sb = new StringBuilder();
        foreach (var c in text.Trim().ToLowerInvariant())
        {
            if (c == 'أ' || c == 'إ' || c == 'آ') sb.Append('ا');
            else if (c == 'ة') sb.Append('ه');
            else if (c == 'ى') sb.Append('ي');
            else if (char.IsLetterOrDigit(c)) sb.Append(c);
        }
        return sb.ToString();
    }

    private async Task<string> ExecuteActionPayloadAsync(Guid conversationId, string actionKey, string argumentsJson, Guid studentUserId, Guid actorUserId, CancellationToken ct)
    {
        if (_mediator == null) throw new LiveSupportException("INTERNAL_ERROR", "Mediator is not available.");

        using var document = System.Text.Json.JsonDocument.Parse(argumentsJson);
        var root = document.RootElement;

        switch (actionKey)
        {
            case "student.lesson.unlock":
                {
                    if (!root.TryGetProperty("lessonId", out var prop) || !Guid.TryParse(prop.GetString(), out var lessonId))
                        throw new LiveSupportException("VALIDATION_ERROR", "Invalid or missing lessonId.");
                    var res = await _mediator.Send(new ManualUnlockCommand(lessonId, studentUserId, actorUserId), ct);
                    if (!res.Success) throw new LiveSupportException("ACTION_FAILED", res.Message ?? "Failed to unlock lesson.");
                    return res.Message ?? "Lesson unlocked successfully.";
                }

            case "student.devices.disconnect-all":
                {
                    var devices = await _db.Devices.Where(x => x.UserId == studentUserId && x.IsActive).ToListAsync(ct);
                    foreach (var dev in devices)
                    {
                        var res = await _mediator.Send(new RemoveDeviceCommand(dev.Id, actorUserId), ct);
                        if (!res.Success) throw new LiveSupportException("ACTION_FAILED", res.Message ?? "Failed to disconnect device.");
                    }
                    return "All devices disconnected successfully.";
                }

            case "student.device.disconnect":
                {
                    Guid deviceId = Guid.Empty;
                    if (root.TryGetProperty("deviceId", out var prop) && Guid.TryParse(prop.GetString(), out var id))
                    {
                        deviceId = id;
                    }
                    else if (root.TryGetProperty("deviceFingerprint", out var fpProp))
                    {
                        var fp = fpProp.GetString();
                        var dev = await _db.Devices.FirstOrDefaultAsync(x => x.UserId == studentUserId && x.DeviceFingerprint == fp && x.IsActive, ct);
                        if (dev == null) throw new LiveSupportException("NOT_FOUND", "Active device not found for fingerprint.");
                        deviceId = dev.Id;
                    }
                    else
                    {
                        throw new LiveSupportException("VALIDATION_ERROR", "Invalid or missing deviceId / deviceFingerprint.");
                    }
                    var res = await _mediator.Send(new RemoveDeviceCommand(deviceId, actorUserId), ct);
                    if (!res.Success) throw new LiveSupportException("ACTION_FAILED", res.Message ?? "Failed to disconnect device.");
                    return res.Message ?? "Device disconnected successfully.";
                }

            case "student.watch.reset":
                {
                    if (!root.TryGetProperty("lessonVideoId", out var prop) || !Guid.TryParse(prop.GetString(), out var videoId))
                        throw new LiveSupportException("VALIDATION_ERROR", "Invalid or missing lessonVideoId.");
                    var res = await _mediator.Send(new ResetWatchLimitCommand(videoId, studentUserId, actorUserId), ct);
                    if (!res.Success) throw new LiveSupportException("ACTION_FAILED", res.Message ?? "Failed to reset watch limit.");
                    return res.Message ?? "Watch limit reset successfully.";
                }

            case "student.watch-request.approve":
                {
                    Guid requestId;
                    if (root.TryGetProperty("requestId", out var prop) && Guid.TryParse(prop.GetString(), out var reqId))
                    {
                        requestId = reqId;
                    }
                    else if (root.TryGetProperty("lessonVideoId", out var videoProp) && Guid.TryParse(videoProp.GetString(), out var videoId))
                    {
                        var req = await _db.ExtraWatchRequests.FirstOrDefaultAsync(x => x.UserId == studentUserId && x.LessonVideoId == videoId && x.Status == RequestStatus.Pending, ct);
                        if (req == null) throw new LiveSupportException("NOT_FOUND", "No pending watch request found for this video.");
                        requestId = req.Id;
                    }
                    else
                    {
                        throw new LiveSupportException("VALIDATION_ERROR", "Invalid or missing requestId / lessonVideoId.");
                    }

                    var reason = root.TryGetProperty("reason", out var reasonProp) ? reasonProp.GetString() : "Approved by AI";
                    var addedViews = root.TryGetProperty("addedViews", out var viewsProp) && viewsProp.TryGetInt32(out var val) ? val : 1;
                    
                    var res = await _mediator.Send(new ApproveWatchRequestCommand(requestId, actorUserId, reason, addedViews), ct);
                    if (!res.Success) throw new LiveSupportException("ACTION_FAILED", res.Message ?? "Failed to approve request.");
                    return res.Message ?? "Watch request approved successfully.";
                }

            default:
                throw new LiveSupportException("VALIDATION_ERROR", $"Unsupported action: {actionKey}");
        }
    }

    private static string Hash(string value)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
