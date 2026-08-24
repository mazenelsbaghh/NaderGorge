using System.Data;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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
    private readonly NaderGorge.Application.Features.LiveSupportAI.Interfaces.ILiveSupportAIVerificationService? _aiVerificationService = aiVerificationService;
    private readonly NaderGorge.Application.Features.LiveSupportAI.Interfaces.ILiveSupportAIRegistrationService? _aiRegistrationService = aiRegistrationService;
    private ILiveSupportAIHandoffService? _handoffServiceBacking;
    private ILiveSupportAIHandoffService _handoffService => _handoffServiceBacking ??= new NaderGorge.Infrastructure.Services.LiveSupportAI.LiveSupportAIHandoffService(_db, this);


    public async Task<LiveSupportAvailabilityDto> GetAvailabilityAsync(CancellationToken ct)
    {
        if (!(await _settings.GetAsync(ct)).LiveSupportEnabled)
            return new LiveSupportAvailabilityDto(false, 0, null, LiveSupportErrorCodes.SupportUnavailable, "الدعم المباشر غير مفعّل حاليًا.");
        var businessHours = await GetCurrentBusinessHoursAsync(ct);
        var cairo = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
        var localTime = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, cairo));
        var isOutsideBusinessHours = businessHours.Count == 0 || !businessHours.Any(window => IsWithinBusinessHours(localTime, window));
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
                : "الدعم غير متاح حاليًا، وسيظهر الموعد هنا عند تحديده من الإدارة.",
            businessHours,
            isOutsideBusinessHours);
    }


    public async Task<IReadOnlyList<LiveSupportConversationDto>> ListParticipantConversationsAsync(LiveSupportParticipantIdentity participant, CancellationToken ct)
    {
        var items = await ParticipantQuery(participant).OrderByDescending(x => x.CreatedAt).Take(50).ToListAsync(ct);
        return await MapManyAsync(items, ct);
    }

    public async Task<LiveSupportConversationDto> CreateConversationAsync(LiveSupportParticipantIdentity participant, string? subject, Guid? previousConversationId, CancellationToken ct)
    {
        // Routing is serialized by the PostgreSQL advisory transaction lock below.
        // ReadCommitted avoids taking a stale Serializable snapshot while waiting
        // for that lock, which otherwise produces avoidable 40001 failures under
        // concurrent conversation creation.
        await using var tx = await _db.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await AcquireRoutingLockAsync(ct);
        var availability = await GetAvailabilityAsync(ct);
        if (!availability.IsAvailable && !availability.IsOutsideBusinessHours)
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
            await SendMessageAsync(new PersistMessageRequest(conversation, senderType, participant.StudentUserId, participant.GuestSessionId, clientMessageId, subject, LiveSupportMessageType.Text), ct);
            if (!availability.IsAvailable && availability.IsOutsideBusinessHours)
                await SendAfterHoursReplyAsync(conversation, availability.BusinessHours ?? [], ct);
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
        await AcknowledgeStaffMessagesAsync(conversationId, ct);
        var rows = await _db.LiveSupportMessages.AsNoTracking().Include(x => x.ReplyToMessage).Where(x => x.ConversationId == conversationId)
            .OrderByDescending(x => x.SentAt).Take(Math.Clamp(pageSize, 1, 100)).OrderBy(x => x.SentAt)
            .ToListAsync(ct);
        return await EnrichMessageDtosAsync(rows, ct);
    }

    public async Task<LiveSupportMessagePageDto> GetParticipantMessagePageAsync(LiveSupportParticipantIdentity participant, Guid conversationId, int pageSize, string? cursor, long? afterSequence, CancellationToken ct)
    {
        await RequireParticipantConversationAsync(participant, conversationId, ct);
        await AcknowledgeStaffMessagesAsync(conversationId, ct);
        var take = Math.Clamp(pageSize, 1, 100);
        var query = _db.LiveSupportMessages.AsNoTracking().Include(x => x.ReplyToMessage).Where(x => x.ConversationId == conversationId);
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
        return new(await EnrichMessageDtosAsync(rows, ct), next, lastSequence, events);
    }

    public async Task<LiveSupportAttachmentDto> SaveParticipantAttachmentAsync(LiveSupportParticipantIdentity participant, Guid conversationId, Stream content, string fileName, string contentType, long sizeBytes, CancellationToken ct)
    {
        var conversation = await RequireParticipantConversationAsync(participant, conversationId, ct);
        if (IsTerminal(conversation.Status)) throw new LiveSupportException(LiveSupportErrorCodes.ConversationTerminal, "المحادثة مغلقة.");
        if (sizeBytes is <= 0 or > 10 * 1024 * 1024) throw new LiveSupportException("VALIDATION_ERROR", "نوع الملف غير مدعوم أو حجمه أكبر من 10 ميجابايت.");
        if (_attachmentStorage is null) throw new LiveSupportException("ATTACHMENT_STORAGE_UNAVAILABLE", "رفع الملفات غير متاح مؤقتًا.");
        LiveSupportStoredAttachment stored;
        try
        {
            stored = await _attachmentStorage.SaveAsync(content, fileName, contentType, sizeBytes, ct);
        }
        catch (InvalidUploadContentException)
        {
            throw new LiveSupportException("VALIDATION_ERROR", "نوع الملف غير مدعوم أو لا يطابق محتواه.");
        }
        if (IsAudioAttachment(stored.ContentType))
        {
            await _attachmentStorage.DeleteAsync(stored.StoragePath, ct);
            throw new LiveSupportException(LiveSupportErrorCodes.AudioStaffOnly, "التسجيلات الصوتية متاحة لفريق الدعم فقط.");
        }
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

    public async Task<LiveSupportAttachmentDto> SaveStaffAttachmentAsync(Guid staffUserId, bool isAdmin, Guid conversationId, Stream content, string fileName, string contentType, long sizeBytes, CancellationToken ct)
    {
        var conversation = await RequireStaffConversationAsync(staffUserId, isAdmin, conversationId, ct);
        if (IsTerminal(conversation.Status)) throw new LiveSupportException(LiveSupportErrorCodes.ConversationTerminal, "المحادثة مغلقة.");
        if (_attachmentStorage is null) throw new LiveSupportException("ATTACHMENT_STORAGE_UNAVAILABLE", "رفع المرفقات غير متاح مؤقتًا.");
        LiveSupportStoredAttachment stored;
        try { stored = await _attachmentStorage.SaveAsync(content, fileName, contentType, sizeBytes, ct); }
        catch (InvalidUploadContentException) { throw new LiveSupportException("VALIDATION_ERROR", "المرفق غير مدعوم أو لا يطابق محتواه."); }
        if (!IsImageAttachment(stored.ContentType) && !IsAudioAttachment(stored.ContentType))
        {
            await _attachmentStorage.DeleteAsync(stored.StoragePath, ct);
            throw new LiveSupportException("VALIDATION_ERROR", "مرفقات الموظف يجب أن تكون صورًا أو تسجيلات صوتية.");
        }
        var entity = new LiveSupportAttachment { StoragePath = stored.StoragePath, OriginalFileName = stored.OriginalFileName, ContentType = stored.ContentType, SizeBytes = stored.SizeBytes, Sha256 = stored.Sha256, UploadedByIdentity = staffUserId.ToString("N") };
        _db.LiveSupportAttachments.Add(entity);
        await _db.SaveChangesAsync(ct);
        return new(entity.Id, entity.OriginalFileName, entity.ContentType, entity.SizeBytes, $"/api/live-support/staff/conversations/{conversationId}/attachments/{entity.Id}");
    }

    public async Task<LiveSupportAttachmentDownloadDto> OpenStaffAttachmentAsync(Guid staffUserId, bool isAdmin, Guid conversationId, Guid attachmentId, CancellationToken ct)
    {
        await RequireStaffConversationAsync(staffUserId, isAdmin, conversationId, ct);
        var attachment = await _db.LiveSupportAttachments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == attachmentId && !x.IsBlocked, ct);
        var linked = await _db.LiveSupportMessages.AnyAsync(x => x.ConversationId == conversationId && x.AttachmentId == attachmentId, ct);
        if (attachment is null || !linked) throw new LiveSupportException("NOT_FOUND", "المرفق غير موجود.");
        if (_attachmentStorage is null) throw new LiveSupportException("ATTACHMENT_STORAGE_UNAVAILABLE", "المرفق غير متاح مؤقتًا.");
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
        ValidateParticipantMessageType(type);
        var conversation = await RequireParticipantConversationAsync(participant, conversationId, ct);
        return await SendMessageAsync(new PersistMessageRequest(
            conversation,
            participant.Type == LiveSupportParticipantType.Student ? LiveSupportSenderType.Student : LiveSupportSenderType.Guest,
            participant.StudentUserId,
            participant.GuestSessionId,
            clientMessageId,
            content,
            type), ct);
    }

    public async Task<LiveSupportSendResultDto> IngestExternalMessageAsync(LiveSupportExternalMessage message, CancellationToken ct)
    {
        var conversation = await RequireParticipantConversationAsync(message.Participant, message.ConversationId, ct);
        return await SendMessageAsync(new PersistMessageRequest(
            conversation,
            LiveSupportSenderType.Guest,
            null,
            message.Participant.GuestSessionId,
            message.ClientMessageId,
            message.Content,
            message.Type,
            message.AttachmentId), ct);
    }

    public async Task<LiveSupportSendResultDto> SendParticipantAttachmentMessageAsync(LiveSupportParticipantIdentity participant, Guid conversationId, string clientMessageId, Guid attachmentId, string? caption, LiveSupportMessageType type, CancellationToken ct)
    {
        if (type == LiveSupportMessageType.Audio)
            throw new LiveSupportException(LiveSupportErrorCodes.AudioStaffOnly, "التسجيلات الصوتية متاحة لفريق الدعم فقط.");
        if (type is not (LiveSupportMessageType.Image or LiveSupportMessageType.Pdf))
            throw new LiveSupportException("VALIDATION_ERROR", "نوع المرفق غير مدعوم.");
        var conversation = await RequireParticipantConversationAsync(participant, conversationId, ct);
        var identity = participant.StudentUserId?.ToString("N") ?? participant.GuestSessionId!.Value.ToString("N");
        var attachment = await _db.LiveSupportAttachments.FirstOrDefaultAsync(x => x.Id == attachmentId && x.UploadedByIdentity == identity && !x.IsBlocked, ct)
            ?? throw new LiveSupportException("NOT_FOUND", "الملف غير موجود.");
        var contentMatchesType = type switch
        {
            LiveSupportMessageType.Image => IsImageAttachment(attachment.ContentType),
            LiveSupportMessageType.Pdf => string.Equals(attachment.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
        if (!contentMatchesType)
            throw new LiveSupportException("VALIDATION_ERROR", "نوع المرفق لا يطابق الرسالة.");
        return await SendMessageAsync(new PersistMessageRequest(
            conversation,
            participant.Type == LiveSupportParticipantType.Student ? LiveSupportSenderType.Student : LiveSupportSenderType.Guest,
            participant.StudentUserId,
            participant.GuestSessionId,
            clientMessageId,
            string.IsNullOrWhiteSpace(caption) ? attachment.OriginalFileName : caption.Trim(),
            type,
            attachment.Id), ct);
    }

    public async Task<LiveSupportMessageDto> UpdateParticipantMessageAsync(LiveSupportParticipantIdentity participant, Guid conversationId, Guid messageId, string content, CancellationToken ct)
    {
        await RequireParticipantConversationAsync(participant, conversationId, ct);
        var message = await RequireParticipantOwnedMessageAsync(participant, conversationId, messageId, ct);
        return await UpdateMessageAsync(message, content, participant.StudentUserId, participant.GuestSessionId, ct);
    }

    public async Task<LiveSupportMessageDto> DeleteParticipantMessageAsync(LiveSupportParticipantIdentity participant, Guid conversationId, Guid messageId, CancellationToken ct)
    {
        await RequireParticipantConversationAsync(participant, conversationId, ct);
        var message = await RequireParticipantOwnedMessageAsync(participant, conversationId, messageId, ct);
        return await DeleteMessageAsync(message, participant.StudentUserId, participant.GuestSessionId, ct);
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
        if (!IsTerminal(conversation.Status) || stars is < 1 or > 5 || await _db.LiveSupportRatings.AnyAsync(x => x.ConversationId == conversationId, ct))
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
            await WaitingConversationCountAsync(ct), await MapManyAsync(conversations, ct), await GetRepliesForStaffAsync(staffUserId, ct));
    }

    private Task<int> WaitingConversationCountAsync(CancellationToken ct) =>
        ActiveQueueEntries().CountAsync(ct);

    private IQueryable<LiveSupportQueueEntry> ActiveQueueEntries() =>
        _db.LiveSupportQueueEntries.Where(entry => entry.DequeuedAt == null &&
            _db.LiveSupportConversations.Any(conversation =>
                conversation.Id == entry.ConversationId && conversation.Status == LiveSupportConversationStatus.Waiting));

    public async Task<IReadOnlyList<LiveSupportMessageDto>> GetStaffMessagesAsync(Guid staffUserId, bool isAdmin, Guid conversationId, int pageSize, CancellationToken ct)
    {
        await RequireStaffConversationAsync(staffUserId, isAdmin, conversationId, ct);
        await AcknowledgeParticipantMessagesAsync(conversationId, ct);
        var rows = await _db.LiveSupportMessages.AsNoTracking().Include(x => x.ReplyToMessage).Where(x => x.ConversationId == conversationId)
            .OrderByDescending(x => x.SentAt).Take(Math.Clamp(pageSize, 1, 100)).OrderBy(x => x.SentAt)
            .ToListAsync(ct);
        return await EnrichMessageDtosAsync(rows, ct);
    }

    public async Task<long> GetStaffLastEventSequenceAsync(Guid staffUserId, bool isAdmin, Guid conversationId, CancellationToken ct)
    {
        await RequireStaffConversationAsync(staffUserId, isAdmin, conversationId, ct);
        return await _db.LiveSupportEvents.Where(x => x.ConversationId == conversationId).MaxAsync(x => (long?)x.Sequence, ct) ?? 0;
    }

    public Task AcknowledgeParticipantMessagesAsync(Guid conversationId, CancellationToken ct) =>
        AcknowledgeMessagesAsync(conversationId, [LiveSupportSenderType.Student, LiveSupportSenderType.Guest], ct);

    public Task AcknowledgeStaffMessagesAsync(Guid conversationId, CancellationToken ct) =>
        AcknowledgeMessagesAsync(conversationId, [LiveSupportSenderType.Staff, LiveSupportSenderType.Admin, LiveSupportSenderType.AI, LiveSupportSenderType.System], ct);

    private async Task AcknowledgeMessagesAsync(Guid conversationId, LiveSupportSenderType[] incomingTypes, CancellationToken ct)
    {
        var acknowledgedAt = DateTime.UtcNow;
        await _db.LiveSupportMessages
            .Where(x => x.ConversationId == conversationId && incomingTypes.Contains(x.SenderType) && x.ReadAt == null)
            .ExecuteUpdateAsync(update => update
                .SetProperty(x => x.DeliveredAt, x => x.DeliveredAt ?? acknowledgedAt)
                .SetProperty(x => x.ReadAt, acknowledgedAt), ct);
    }

    public async Task<LiveSupportSendResultDto> SendStaffMessageAsync(Guid staffUserId, bool isAdmin, Guid conversationId, string clientMessageId, string content, Guid? replyToMessageId, CancellationToken ct)
    {
        var conversation = await RequireStaffConversationAsync(staffUserId, isAdmin, conversationId, ct);
        if (!isAdmin && !await IsCheckedInAsync(staffUserId, ct)) throw new LiveSupportException(LiveSupportErrorCodes.Forbidden, "يجب تسجيل الحضور أولًا.");
        var whatsAppBinding = await _db.LiveSupportWhatsAppBindings.AsNoTracking()
            .SingleOrDefaultAsync(x => x.ConversationId == conversationId, ct);
        EnsureWhatsAppWindowOpen(whatsAppBinding);
        var sendResult = await SendMessageAsync(
            new PersistMessageRequest(conversation, isAdmin ? LiveSupportSenderType.Admin : LiveSupportSenderType.Staff, staffUserId, null, clientMessageId, content, LiveSupportMessageType.Text, null, replyToMessageId),
            message => StageStaffMessageSideEffects(
                conversation,
                staffUserId,
                message,
                whatsAppBinding is null ? null : new WhatsAppOutboundDraft("text")),
            ct);
        return WithPendingWhatsAppStatus(sendResult, whatsAppBinding is not null);
    }

    public async Task<LiveSupportSendResultDto> SendStaffWhatsAppTemplateAsync(SendLiveSupportWhatsAppTemplateCommand command, CancellationToken ct)
    {
        var conversation = await RequireStaffConversationAsync(command.StaffUserId, command.IsAdmin, command.ConversationId, ct);
        if (!command.IsAdmin && !await IsCheckedInAsync(command.StaffUserId, ct))
            throw new LiveSupportException(LiveSupportErrorCodes.Forbidden, "يجب تسجيل الحضور أولًا.");
        if (!await _db.LiveSupportWhatsAppBindings.AnyAsync(item => item.ConversationId == conversation.Id, ct))
            throw new LiveSupportException("WHATSAPP_CHANNEL_REQUIRED", "هذه المحادثة ليست محادثة واتساب.");
        var template = await _db.LiveSupportWhatsAppTemplates.AsNoTracking().SingleOrDefaultAsync(item => item.Id == command.Request.TemplateId, ct)
            ?? throw new LiveSupportException("WHATSAPP_TEMPLATE_NOT_FOUND", "قالب واتساب غير موجود.");
        if (!string.Equals(template.Status, "APPROVED", StringComparison.OrdinalIgnoreCase))
            throw new LiveSupportException("WHATSAPP_TEMPLATE_NOT_APPROVED", "قالب واتساب غير معتمد من Meta.");
        if (command.Request.Parameters.Count > 30 || command.Request.Parameters.Any(value => string.IsNullOrWhiteSpace(value) || value.Length > 1000))
            throw new LiveSupportException("VALIDATION_ERROR", "قيم قالب واتساب غير صالحة.");

        var normalizedParameters = command.Request.Parameters.Select(value => value.Trim()).ToArray();
        var serializedParameters = JsonSerializer.Serialize(normalizedParameters);
        var serverPreview = RenderWhatsAppTemplatePreview(template, normalizedParameters);

        var sendResult = await SendMessageAsync(
            new PersistMessageRequest(
                conversation,
                command.IsAdmin ? LiveSupportSenderType.Admin : LiveSupportSenderType.Staff,
                command.StaffUserId,
                null,
                command.Request.ClientMessageId,
                serverPreview,
                LiveSupportMessageType.Text),
            message => StageStaffMessageSideEffects(
                conversation,
                command.StaffUserId,
                message,
                new WhatsAppOutboundDraft(
                    "template",
                    template.Name,
                    template.Language,
                    serializedParameters)),
            ct);

        if (sendResult.Replayed)
        {
            var existingDelivery = await _db.LiveSupportWhatsAppMessages.AsNoTracking()
                .SingleOrDefaultAsync(item => item.LiveSupportMessageId == sendResult.Message.Id, ct);
            if (existingDelivery is null ||
                !string.Equals(existingDelivery.MessageType, "template", StringComparison.Ordinal) ||
                !string.Equals(existingDelivery.TemplateName, template.Name, StringComparison.Ordinal) ||
                !string.Equals(existingDelivery.TemplateLanguage, template.Language, StringComparison.Ordinal) ||
                !string.Equals(existingDelivery.TemplateParametersJson, serializedParameters, StringComparison.Ordinal))
                throw new LiveSupportException(LiveSupportErrorCodes.MessageConflict, "معرّف الرسالة مستخدم لقالب واتساب مختلف.");
        }
        return WithPendingWhatsAppStatus(sendResult, true);
    }

    public async Task<LiveSupportSendResultDto> SendStaffAttachmentMessageAsync(Guid staffUserId, bool isAdmin, Guid conversationId, string clientMessageId, Guid attachmentId, string? caption, LiveSupportMessageType type, CancellationToken ct)
    {
        var conversation = await RequireStaffConversationAsync(staffUserId, isAdmin, conversationId, ct);
        if (!isAdmin && !await IsCheckedInAsync(staffUserId, ct)) throw new LiveSupportException(LiveSupportErrorCodes.Forbidden, "يجب تسجيل الحضور أولًا.");
        var attachment = await _db.LiveSupportAttachments.FirstOrDefaultAsync(x => x.Id == attachmentId && x.UploadedByIdentity == staffUserId.ToString("N") && !x.IsBlocked, ct)
            ?? throw new LiveSupportException("NOT_FOUND", "المرفق غير موجود.");
        var validImage = type == LiveSupportMessageType.Image && IsImageAttachment(attachment.ContentType);
        var validAudio = type == LiveSupportMessageType.Audio && IsAudioAttachment(attachment.ContentType);
        if (!validImage && !validAudio)
            throw new LiveSupportException("VALIDATION_ERROR", "مرفقات الموظف يجب أن تكون صورًا أو تسجيلات صوتية.");
        var whatsAppBinding = await _db.LiveSupportWhatsAppBindings.AsNoTracking()
            .SingleOrDefaultAsync(x => x.ConversationId == conversationId, ct);
        EnsureWhatsAppWindowOpen(whatsAppBinding);
        var sendResult = await SendMessageAsync(
            new PersistMessageRequest(
                conversation,
                isAdmin ? LiveSupportSenderType.Admin : LiveSupportSenderType.Staff,
                staffUserId,
                null,
                clientMessageId,
                string.IsNullOrWhiteSpace(caption) ? attachment.OriginalFileName : caption.Trim(),
                type,
                attachment.Id),
            message => StageStaffMessageSideEffects(
                conversation,
                staffUserId,
                message,
                whatsAppBinding is null ? null : new WhatsAppOutboundDraft(type.ToString().ToLowerInvariant())),
            ct);
        sendResult = WithPendingWhatsAppStatus(sendResult, whatsAppBinding is not null);
        return sendResult with { Message = sendResult.Message with { AttachmentId = attachment.Id } };
    }

    public async Task<LiveSupportMessageDto> UpdateStaffMessageAsync(Guid staffUserId, bool isAdmin, Guid conversationId, Guid messageId, string content, CancellationToken ct)
    {
        await RequireStaffConversationAsync(staffUserId, isAdmin, conversationId, ct);
        var message = await RequireStaffOwnedMessageAsync(staffUserId, conversationId, messageId, ct);
        return await UpdateMessageAsync(message, content, staffUserId, null, ct);
    }

    public async Task<LiveSupportMessageDto> DeleteStaffMessageAsync(Guid staffUserId, bool isAdmin, Guid conversationId, Guid messageId, CancellationToken ct)
    {
        await RequireStaffConversationAsync(staffUserId, isAdmin, conversationId, ct);
        var message = await RequireStaffOwnedMessageAsync(staffUserId, conversationId, messageId, ct);
        return await DeleteMessageAsync(message, staffUserId, null, ct);
    }

    public async Task<LiveSupportConversationDto> CloseAsync(Guid staffUserId, bool isAdmin, Guid conversationId, string? reason, CancellationToken ct)
    {
        var conversation = await RequireStaffConversationAsync(staffUserId, isAdmin, conversationId, ct);
        var closeReason = string.IsNullOrWhiteSpace(reason) ? "أغلقها موظف الدعم" : reason.Trim();
        await FinishConversationAsync(conversation, staffUserId, LiveSupportConversationStatus.Closed, closeReason, LiveSupportAssignmentEndReason.Closed, ct);
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

        await using var tx = await _db.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
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
        return new LiveSupportAdminConfigDto((await _settings.GetAsync(ct)).LiveSupportEnabled, result, await GetCannedRepliesAsync(ct));
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

    public async Task UpdateCannedRepliesAsync(IReadOnlyList<LiveSupportCannedReplyDto> replies, CancellationToken ct)
    {
        if (replies.Count > 30 || replies.Any(x => string.IsNullOrWhiteSpace(x.Id) || string.IsNullOrWhiteSpace(x.Title) || x.Title.Trim().Length > 80 || string.IsNullOrWhiteSpace(x.Content) || x.Content.Trim().Length > 4000))
            throw new LiveSupportException("VALIDATION_ERROR", "الردود الثابتة غير صالحة. الحد الأقصى 30 ردًا، و4000 حرف للنص.");
        var safe = replies.Select(x => new LiveSupportCannedReplyDto(x.Id.Trim(), x.Title.Trim(), x.Content.Trim(), x.SendImmediately)).ToList();
        var setting = await _db.PlatformSettings.FirstOrDefaultAsync(x => x.Key == PlatformSettingKeys.LiveSupportCannedReplies, ct);
        var json = System.Text.Json.JsonSerializer.Serialize(safe);
        if (setting is null) _db.PlatformSettings.Add(new NaderGorge.Domain.Entities.PlatformSetting { Key = PlatformSettingKeys.LiveSupportCannedReplies, Value = json });
        else { setting.Value = json; setting.UpdatedAt = DateTime.UtcNow; }
        await _db.SaveChangesAsync(ct);
    }

    public Task<IReadOnlyList<LiveSupportCannedReplyDto>> GetStaffCannedRepliesAsync(Guid staffUserId, CancellationToken ct) => GetPersonalCannedRepliesAsync(staffUserId, ct);

    public async Task UpdateStaffCannedRepliesAsync(Guid staffUserId, IReadOnlyList<LiveSupportCannedReplyDto> replies, CancellationToken ct)
    {
        if (replies.Count > 30 || replies.Any(x => string.IsNullOrWhiteSpace(x.Id) || string.IsNullOrWhiteSpace(x.Title) || x.Title.Trim().Length > 80 || string.IsNullOrWhiteSpace(x.Content) || x.Content.Trim().Length > 4000)) throw new LiveSupportException("VALIDATION_ERROR", "الردود الثابتة غير صالحة.");
        var key = $"LiveSupportCannedReplies:{staffUserId:N}";
        var json = System.Text.Json.JsonSerializer.Serialize(replies.Select(x => new LiveSupportCannedReplyDto(x.Id.Trim(), x.Title.Trim(), x.Content.Trim(), x.SendImmediately)));
        var setting = await _db.PlatformSettings.FirstOrDefaultAsync(x => x.Key == key, ct);
        if (setting is null) _db.PlatformSettings.Add(new PlatformSetting { Key = key, Value = json }); else { setting.Value = json; setting.UpdatedAt = DateTime.UtcNow; }
        await _db.SaveChangesAsync(ct);
    }

    private async Task<IReadOnlyList<LiveSupportCannedReplyDto>> GetRepliesForStaffAsync(Guid staffUserId, CancellationToken ct)
    {
        var name = await _db.Users.AsNoTracking().Where(x => x.Id == staffUserId).Select(x => x.FullName).FirstOrDefaultAsync(ct) ?? "فريق الدعم";
        return (await GetCannedRepliesAsync(ct)).Concat(await GetPersonalCannedRepliesAsync(staffUserId, ct)).Select(x => x with { Content = x.Content.Replace("{{اسم الموظف}}", name, StringComparison.Ordinal) }).ToList();
    }

    private async Task<IReadOnlyList<LiveSupportCannedReplyDto>> GetPersonalCannedRepliesAsync(Guid staffUserId, CancellationToken ct)
    {
        var json = await _db.PlatformSettings.AsNoTracking().Where(x => x.Key == $"LiveSupportCannedReplies:{staffUserId:N}").Select(x => x.Value).FirstOrDefaultAsync(ct);
        try { return string.IsNullOrWhiteSpace(json) ? [] : System.Text.Json.JsonSerializer.Deserialize<List<LiveSupportCannedReplyDto>>(json) ?? []; } catch { return []; }
    }

    private async Task<IReadOnlyList<LiveSupportCannedReplyDto>> GetCannedRepliesAsync(CancellationToken ct)
    {
        var json = await _db.PlatformSettings.AsNoTracking().Where(x => x.Key == PlatformSettingKeys.LiveSupportCannedReplies).Select(x => x.Value).FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return System.Text.Json.JsonSerializer.Deserialize<List<LiveSupportCannedReplyDto>>(json) ?? []; }
        catch (System.Text.Json.JsonException) { return []; }
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
        await AssignOldestWaitingAsync(ct);
        return await MapStaffConfigAsync(config, ct);
    }

    public async Task ReleaseStaffAssignmentsAsync(Guid staffUserId, LiveSupportAssignmentEndReason reason, CancellationToken ct)
    {
        await using var tx = await _db.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
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

    public async Task<IReadOnlyList<LiveSupportStudentSupportHistoryDto>> GetStudentSupportHistoryAsync(Guid staffUserId, bool isAdmin, Guid conversationId, CancellationToken ct)
    {
        var conversation = await RequireStaffConversationAsync(staffUserId, isAdmin, conversationId, ct);
        var studentId = conversation.LinkedStudentUserId
            ?? throw new LiveSupportException("STUDENT_NOT_LINKED", "اربط المحادثة بطالب أولًا.");

        var summaries = await _db.LiveSupportConversations.AsNoTracking()
            .Where(item => item.LinkedStudentUserId == studentId || item.StudentUserId == studentId ||
                _db.LiveSupportStudentLinkHistories.Any(link => link.ConversationId == item.Id &&
                    (link.PreviousStudentUserId == studentId || link.NewStudentUserId == studentId)))
            .OrderByDescending(item => item.LastMessageAt ?? item.ClosedAt ?? item.CreatedAt)
            .Take(100)
            .Select(item => new
            {
                Conversation = item,
                MessageCount = _db.LiveSupportMessages.Count(message => message.ConversationId == item.Id),
                LastMessage = _db.LiveSupportMessages
                    .Where(message => message.ConversationId == item.Id)
                    .OrderByDescending(message => message.SentAt)
                    .Select(message => new { message.Content, message.SentAt })
                    .FirstOrDefault(),
                LastEvent = _db.LiveSupportEvents
                    .Where(value => value.ConversationId == item.Id)
                    .OrderByDescending(value => value.Sequence)
                    .Select(value => value.Type.ToString())
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        var conversationIds = summaries.Select(x => x.Conversation.Id).ToArray();
        var activityRows = await _db.LiveSupportEvents.AsNoTracking()
            .Where(value =>
                conversationIds.Contains(value.ConversationId) &&
                value.Type != LiveSupportEventType.MessageSent)
            .OrderByDescending(value => value.Sequence)
            .Take(Math.Max(50, conversationIds.Length * 50))
            .Select(value => new
            {
                value.ConversationId,
                value.OccurredAt,
                Type = value.Type.ToString(),
                value.Sequence
            })
            .ToListAsync(ct);
        var activitiesByConversation = activityRows
            .GroupBy(x => x.ConversationId)
            .ToDictionary(
                x => x.Key,
                x => (IReadOnlyList<LiveSupportStudentSupportActivityDto>)x
                    .OrderByDescending(value => value.Sequence)
                    .Take(50)
                    .OrderBy(value => value.Sequence)
                    .Select(value => new LiveSupportStudentSupportActivityDto(value.OccurredAt, value.Type))
                    .ToList());

        var histories = summaries.Select(summary =>
        {
            var item = summary.Conversation;
            var lastActivityAt = summary.LastMessage?.SentAt ?? item.ClosedAt ?? item.CreatedAt;
            return new LiveSupportStudentSupportHistoryDto(
                item.Id,
                item.Status,
                item.Subject,
                item.CreatedAt,
                item.ClosedAt,
                lastActivityAt,
                summary.MessageCount,
                TruncateHistoryPreview(summary.LastMessage?.Content),
                summary.LastEvent,
                activitiesByConversation.GetValueOrDefault(item.Id) ?? []);
        }).ToList();

        return histories.OrderByDescending(item => item.LastActivityAt).ToList();
    }

    public async Task<IReadOnlyList<LiveSupportMessageDto>> GetStudentHistoryMessagesAsync(Guid staffUserId, bool isAdmin, Guid conversationId, Guid historyConversationId, int pageSize, CancellationToken ct)
    {
        var history = await GetStudentSupportHistoryAsync(staffUserId, isAdmin, conversationId, ct);
        if (!history.Any(item => item.ConversationId == historyConversationId))
            throw new LiveSupportException(LiveSupportErrorCodes.Forbidden, "هذه المحادثة ليست ضمن سجل الطالب.");

        var rows = await _db.LiveSupportMessages.AsNoTracking().Include(message => message.ReplyToMessage).Where(message => message.ConversationId == historyConversationId)
            .OrderByDescending(message => message.SentAt).Take(Math.Clamp(pageSize, 1, 100)).OrderBy(message => message.SentAt)
            .ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    private static string? TruncateHistoryPreview(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        const int maximumLength = 160;
        return value.Length <= maximumLength ? value : $"{value[..maximumLength]}…";
    }

    private async Task<JsonElement> GetBasicStudentSectionAsync(Guid studentId, CancellationToken ct)
    {
        var student = await _db.Users.AsNoTracking().Where(user => user.Id == studentId).Select(user => new
        {
            fullName = user.FullName,
            phoneNumber = user.PhoneNumber,
            isActive = user.IsActive,
            studentCode = user.StudentProfile == null ? null : user.StudentProfile.StudentCode,
            governorate = user.StudentProfile == null ? null : user.StudentProfile.Governorate,
            schoolName = user.StudentProfile == null ? null : user.StudentProfile.SchoolName,
            educationStage = user.StudentProfile == null ? null : user.StudentProfile.EducationStage.ToString(),
            gradeLevel = user.StudentProfile == null ? null : user.StudentProfile.GradeLevel.ToString()
        }).SingleOrDefaultAsync(ct) ?? throw new LiveSupportException("NOT_FOUND", "الطالب غير موجود.");
        return JsonSerializer.SerializeToElement(student);
    }

    private async Task<JsonElement> GetStudentMetricsSectionAsync(Guid studentId, CancellationToken ct)
    {
        var balance = await _db.StudentBalances.AsNoTracking().Where(row => row.UserId == studentId).Select(row => (decimal?)row.CurrentBalance).SingleOrDefaultAsync(ct) ?? 0;
        var points = await _db.StudentGamifications.AsNoTracking().Where(row => row.StudentId == studentId).Select(row => (int?)row.TotalPoints).SingleOrDefaultAsync(ct) ?? 0;
        var exams = await _db.StudentExamAttempts.CountAsync(row => row.UserId == studentId, ct);
        var devices = await _db.Devices.CountAsync(row => row.UserId == studentId, ct);
        return JsonSerializer.SerializeToElement(new { balance, points, examAttempts = exams, devicesCount = devices });
    }

    private async Task<JsonElement> GetStudentStudySectionAsync(Guid studentId, CancellationToken ct)
    {
        var grants = await _db.StudentAccessGrants.CountAsync(row => row.UserId == studentId && row.IsActive, ct);
        var watches = await _db.VideoWatchEvents.CountAsync(row => row.UserId == studentId, ct);
        var homework = await _db.HomeworkSubmissions.CountAsync(row => row.StudentId == studentId, ct);
        return JsonSerializer.SerializeToElement(new { activeGrants = grants, watchEvents = watches, homeworkSubmissions = homework });
    }

    private async Task<JsonElement> GetStudentDevicesSectionAsync(Guid studentId, CancellationToken ct)
    {
        var devices = await _db.Devices.AsNoTracking().Where(row => row.UserId == studentId).OrderByDescending(row => row.LastUsedAt)
            .Take(100).Select(row => new { id = row.Id, name = row.DeviceName, type = row.DeviceType, os = row.OsName, browser = row.BrowserName, lastUsedAt = row.LastUsedAt, isActive = row.IsActive }).ToListAsync(ct);
        return JsonSerializer.SerializeToElement(new { devices });
    }

    private async Task<JsonElement> GetStudentNotesSectionAsync(Guid studentId, CancellationToken ct)
    {
        var notes = await _db.StudentNotes.AsNoTracking().Where(row => row.StudentId == studentId).OrderByDescending(row => row.IsPinned).ThenByDescending(row => row.CreatedAt)
            .Take(100).Select(row => new { id = row.Id, content = row.Content, isPinned = row.IsPinned, createdAt = row.CreatedAt }).ToListAsync(ct);
        return JsonSerializer.SerializeToElement(new { notes });
    }

    private async Task<JsonElement> GetStudentCrmSectionAsync(Guid studentId, CancellationToken ct)
    {
        var crm = await _db.CrmStudentStatuses.AsNoTracking().Where(row => row.StudentId == studentId)
            .Select(row => new { status = row.Status.ToString(), priority = row.Priority.ToString() }).SingleOrDefaultAsync(ct);
        return JsonSerializer.SerializeToElement(new { status = crm?.status, priority = crm?.priority });
    }

    public async Task<LiveSupportAdminDashboardDto> GetAdminDashboardAsync(CancellationToken ct)
    {
        var conversations = await _db.LiveSupportConversations.AsNoTracking().OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync(ct);
        var rows = await MapAdminConversationsAsync(conversations, ct);
        var configs = await _db.LiveSupportStaffConfigs.AsNoTracking().ToListAsync(ct);
        var staffIds = configs.Select(x => x.UserId).Distinct().ToArray();
        var staffNames = await _db.Users.AsNoTracking()
            .Where(x => staffIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.FullName, ct);
        var assignmentRows = await _db.LiveSupportAssignments.AsNoTracking()
            .Where(x => staffIds.Contains(x.StaffUserId))
            .Select(x => new { x.StaffUserId, x.ConversationId, x.EndReason })
            .ToListAsync(ct);
        var ratedConversationIds = assignmentRows.Select(x => x.ConversationId).Distinct().ToArray();
        var ratingRows = await _db.LiveSupportRatings.AsNoTracking()
            .Where(x => ratedConversationIds.Contains(x.ConversationId))
            .Select(x => new { x.ConversationId, x.Stars })
            .ToListAsync(ct);
        var ratingsByConversation = ratingRows
            .GroupBy(x => x.ConversationId)
            .ToDictionary(x => x.Key, x => x.Select(value => value.Stars).ToArray());
        var performance = new List<LiveSupportStaffPerformanceDto>(configs.Count);
        foreach (var config in configs)
        {
            var staffAssignments = assignmentRows.Where(x => x.StaffUserId == config.UserId).ToArray();
            var conversationIds = staffAssignments.Select(x => x.ConversationId).Distinct().ToArray();
            var ratings = conversationIds
                .Where(ratingsByConversation.ContainsKey)
                .SelectMany(id => ratingsByConversation[id])
                .ToArray();
            performance.Add(new LiveSupportStaffPerformanceDto(
                config.UserId,
                staffNames.GetValueOrDefault(config.UserId) ?? "موظف",
                conversationIds.Length,
                staffAssignments
                    .Where(x => x.EndReason == LiveSupportAssignmentEndReason.Closed)
                    .Select(x => x.ConversationId)
                    .Distinct()
                    .Count(),
                ratings.Length,
                ratings.Length == 0 ? null : ratings.Average()));
        }
        var cairo = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, cairo);
        var todayStartUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localNow.Date, DateTimeKind.Unspecified), cairo);
        var whatsAppConversationQuery =
            from binding in _db.LiveSupportWhatsAppBindings.AsNoTracking()
            join conversation in _db.LiveSupportConversations.AsNoTracking() on binding.ConversationId equals conversation.Id
            select new { conversation.Status, conversation.ClosedAt };
        var whatsAppConversationCounts = await whatsAppConversationQuery
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Open = group.Count(conversation =>
                    conversation.Status != LiveSupportConversationStatus.Closed &&
                    conversation.Status != LiveSupportConversationStatus.Abandoned),
                Waiting = group.Count(conversation => conversation.Status == LiveSupportConversationStatus.Waiting),
                Active = group.Count(conversation =>
                    conversation.Status == LiveSupportConversationStatus.Assigned ||
                    conversation.Status == LiveSupportConversationStatus.Active),
                ClosedToday = group.Count(conversation =>
                    conversation.Status == LiveSupportConversationStatus.Closed &&
                    conversation.ClosedAt >= todayStartUtc)
            })
            .SingleOrDefaultAsync(ct);
        var whatsAppSummary = new LiveSupportWhatsAppOperationsSummaryDto(
            whatsAppConversationCounts?.Open ?? 0,
            whatsAppConversationCounts?.Waiting ?? 0,
            whatsAppConversationCounts?.Active ?? 0,
            whatsAppConversationCounts?.ClosedToday ?? 0,
            await _db.LiveSupportWhatsAppMessages.AsNoTracking().CountAsync(
                message => message.Direction == "Outbound" && message.Status == "Failed", ct),
            await _db.LiveSupportWhatsAppTemplates.AsNoTracking().CountAsync(
                template => template.Status == "APPROVED", ct),
            await _db.LiveSupportWhatsAppBindings.AsNoTracking().MaxAsync(binding => (DateTime?)binding.LastInboundAt, ct),
            await _db.LiveSupportWhatsAppMessages.AsNoTracking()
                .Where(message => message.Direction == "Outbound")
                .MaxAsync(message => (DateTime?)message.CreatedAt, ct),
            await _db.LiveSupportWhatsAppTemplates.AsNoTracking().MaxAsync(template => (DateTime?)template.LastSyncedAt, ct));
        return new LiveSupportAdminDashboardDto(conversations.Count(x => x.Status == LiveSupportConversationStatus.Waiting), conversations.Count(x => x.Status is LiveSupportConversationStatus.Assigned or LiveSupportConversationStatus.Active),
            await _db.LiveSupportConversations.CountAsync(x => x.Status == LiveSupportConversationStatus.Closed && x.ClosedAt >= todayStartUtc, ct), rows, performance, whatsAppSummary);
    }

    public async Task<IReadOnlyList<LiveSupportRatingDto>> GetAdminRatingsAsync(DateTime? from, DateTime? to, CancellationToken ct)
    {
        var ratingsQuery = _db.LiveSupportRatings.AsNoTracking();
        if (from.HasValue) ratingsQuery = ratingsQuery.Where(rating => rating.SubmittedAt >= from.Value);
        if (to.HasValue) ratingsQuery = ratingsQuery.Where(rating => rating.SubmittedAt <= to.Value);
        var ratings = await ratingsQuery.OrderByDescending(rating => rating.SubmittedAt).Take(500)
            .Select(rating => new { rating.Id, rating.ConversationId, rating.Stars, rating.Comment, rating.SubmittedAt, rating.SubmittedByUserId, rating.SubmittedByGuestSessionId })
            .ToListAsync(ct);
        var userIds = ratings.Where(rating => rating.SubmittedByUserId.HasValue).Select(rating => rating.SubmittedByUserId!.Value).Distinct().ToArray();
        var guestIds = ratings.Where(rating => rating.SubmittedByGuestSessionId.HasValue).Select(rating => rating.SubmittedByGuestSessionId!.Value).Distinct().ToArray();
        var studentNames = await _db.Users.AsNoTracking().Where(user => userIds.Contains(user.Id)).ToDictionaryAsync(user => user.Id, user => user.FullName, ct);
        var guestNames = await _db.LiveSupportGuestSessions.AsNoTracking().Where(guest => guestIds.Contains(guest.Id)).ToDictionaryAsync(guest => guest.Id, guest => guest.DisplayName, ct);
        return ratings.Select(rating => new LiveSupportRatingDto(rating.Id, rating.ConversationId, rating.Stars, rating.Comment, rating.SubmittedAt,
            rating.SubmittedByUserId.HasValue ? studentNames.GetValueOrDefault(rating.SubmittedByUserId.Value) ?? "طالب" : guestNames.GetValueOrDefault(rating.SubmittedByGuestSessionId ?? Guid.Empty) ?? "زائر",
            rating.SubmittedByUserId.HasValue)).ToList();
    }

    public async Task<LiveSupportConversationTimelineDto> GetAdminTimelineAsync(Guid conversationId, CancellationToken ct)
    {
        var conversation = await _db.LiveSupportConversations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == conversationId, ct) ?? throw new LiveSupportException("NOT_FOUND", "المحادثة غير موجودة.");
        var events = await _db.LiveSupportEvents.AsNoTracking().Where(x => x.ConversationId == conversationId).OrderBy(x => x.Sequence).ToListAsync(ct);
        var assignments = await _db.LiveSupportAssignments.AsNoTracking().Where(x => x.ConversationId == conversationId).ToListAsync(ct);
        var messages = await _db.LiveSupportMessages.AsNoTracking().Where(x => x.ConversationId == conversationId).OrderBy(x => x.SentAt).ToListAsync(ct);
        var actions = await _db.LiveSupportActionExecutions.AsNoTracking().Where(x => x.ConversationId == conversationId).ToListAsync(ct);
        var actorUserIds = events.Select(x => x.ActorUserId)
            .Concat(assignments.Select(x => (Guid?)x.StaffUserId))
            .Concat(messages.Select(x => x.SenderUserId))
            .Concat(actions.Select(x => (Guid?)x.StaffUserId))
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToArray();
        var actorGuestIds = events.Select(x => x.ActorGuestSessionId)
            .Concat(messages.Select(x => x.SenderGuestSessionId))
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToArray();
        var actorUserNames = await _db.Users.AsNoTracking()
            .Where(x => actorUserIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.FullName, ct);
        var actorGuestNames = await _db.LiveSupportGuestSessions.AsNoTracking()
            .Where(x => actorGuestIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.DisplayName, ct);
        string? ActorName(Guid? userId, Guid? guestId) =>
            userId.HasValue
                ? actorUserNames.GetValueOrDefault(userId.Value)
                : guestId.HasValue
                    ? actorGuestNames.GetValueOrDefault(guestId.Value)
                    : "النظام";

        var items = new List<LiveSupportTimelineItemDto>(
            events.Count + assignments.Count + messages.Count + actions.Count);
        foreach (var item in events) items.Add(new LiveSupportTimelineItemDto(item.OccurredAt, item.Type.ToString(), ActorName(item.ActorUserId, item.ActorGuestSessionId), EventSummary(item.Type), item.SafeMetadataJson));
        foreach (var item in assignments) items.Add(new LiveSupportTimelineItemDto(item.StartedAt, "Assignment", ActorName(item.StaffUserId, null), "تم إسناد المحادثة للموظف", item.EndedAt.HasValue ? $"انتهى: {item.EndReason} — {item.EndedAt:O}" : "الإسناد الحالي"));
        foreach (var message in messages) items.Add(new LiveSupportTimelineItemDto(message.SentAt, "Message", ActorName(message.SenderUserId, message.SenderGuestSessionId), $"رسالة من {message.SenderType}", message.Content));
        foreach (var item in actions) items.Add(new LiveSupportTimelineItemDto(item.StartedAt, "StudentAction", ActorName(item.StaffUserId, null), $"إجراء على الطالب: {item.ActionKey} — {item.Status}", item.SafeResultJson ?? item.FailureCode));
        var rating = await _db.LiveSupportRatings.AsNoTracking().FirstOrDefaultAsync(x => x.ConversationId == conversationId, ct);
        var mappedConversation = (await MapAdminConversationsAsync([conversation], ct))[0];
        return new LiveSupportConversationTimelineDto(mappedConversation, items.OrderBy(x => x.At).ToList(), rating?.Stars, rating?.Comment);
    }

    private IQueryable<LiveSupportConversation> ParticipantQuery(LiveSupportParticipantIdentity p) => p.Type == LiveSupportParticipantType.Student
        ? _db.LiveSupportConversations.Where(x => x.ParticipantType == p.Type && x.StudentUserId == p.StudentUserId)
        : _db.LiveSupportConversations.Where(x => x.ParticipantType == p.Type && x.GuestSessionId == p.GuestSessionId);

    private IQueryable<LiveSupportStaffConfig> EligibleStaffQuery()
    {
        var today = CurrentCairoDate();
        return _db.LiveSupportStaffConfigs.Where(c => c.IsEnabled && _db.EmployeeProfiles.Any(e =>
            e.UserId == c.UserId &&
            (_db.AttendanceSessions.Any(session => session.EmployeeId == e.Id && session.WorkDate == today && session.State == AttendanceSessionState.Open && session.ClockedOutAt == null && !_db.AttendanceBreaks.Any(breakItem => breakItem.AttendanceSessionId == session.Id && breakItem.EndedAt == null)) ||
             (!_db.AttendanceSessions.Any(session => session.EmployeeId == e.Id && session.WorkDate == today) && _db.AttendanceLogs.Any(log => log.EmployeeId == e.Id && log.ClockOut == null)))));
    }

    // AttendanceSessions is the authoritative current model. A legacy open log is honored only
    // when no session exists today, so an employee who clocked out cannot remain eligible by accident.
    private IQueryable<EmployeeProfile> CheckedInEmployeeQuery(Guid userId)
    {
        var today = CurrentCairoDate();
        return _db.EmployeeProfiles.Where(e =>
            e.UserId == userId &&
            (_db.AttendanceSessions.Any(session => session.EmployeeId == e.Id && session.WorkDate == today && session.State == AttendanceSessionState.Open && session.ClockedOutAt == null && !_db.AttendanceBreaks.Any(breakItem => breakItem.AttendanceSessionId == session.Id && breakItem.EndedAt == null)) ||
             (!_db.AttendanceSessions.Any(session => session.EmployeeId == e.Id && session.WorkDate == today) && _db.AttendanceLogs.Any(log => log.EmployeeId == e.Id && log.ClockOut == null))));
    }

    private static DateOnly CurrentCairoDate()
    {
        try { return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo"))); }
        catch { return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time"))); }
    }

    private Task<bool> IsCheckedInAsync(Guid userId, CancellationToken ct) => CheckedInEmployeeQuery(userId).AnyAsync(ct);

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

    private async Task<IReadOnlyList<LiveSupportScheduleWindowDto>> GetCurrentBusinessHoursAsync(CancellationToken ct)
    {
        var cairo = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
        var dayOfWeek = (int)TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, cairo).DayOfWeek;
        var windows = await _db.LiveSupportScheduleWindows.AsNoTracking()
            .Where(x => x.IsActive && x.DayOfWeek == dayOfWeek && _db.LiveSupportStaffConfigs.Any(c => c.Id == x.StaffConfigId && c.IsEnabled))
            .Select(x => new LiveSupportScheduleWindowDto(x.DayOfWeek, x.StartLocalTime, x.EndLocalTime))
            .ToListAsync(ct);
        return windows
            .DistinctBy(x => (x.StartLocalTime, x.EndLocalTime))
            .OrderBy(x => x.StartLocalTime)
            .ToArray();
    }

    private static bool IsWithinBusinessHours(TimeOnly localTime, LiveSupportScheduleWindowDto window) =>
        window.EndLocalTime >= window.StartLocalTime
            ? localTime >= window.StartLocalTime && localTime < window.EndLocalTime
            : localTime >= window.StartLocalTime || localTime < window.EndLocalTime;

    private async Task SendAfterHoursReplyAsync(LiveSupportConversation conversation, IReadOnlyList<LiveSupportScheduleWindowDto> businessHours, CancellationToken ct)
    {
        var hours = businessHours.Count == 0
            ? "لم تُحدد مواعيد العمل لهذا اليوم بعد"
            : string.Join("، ", businessHours.Select(window => $"من {window.StartLocalTime:HH\\:mm} إلى {window.EndLocalTime:HH\\:mm}"));
        var settings = await _settings.GetAsync(ct);
        var contact = string.IsNullOrWhiteSpace(settings.GuestSupportWhatsAppNumber)
            ? settings.SupportPhoneNumber
            : settings.GuestSupportWhatsAppNumber;
        var content = $"نحن الآن خارج مواعيد العمل الرسمية. مواعيد العمل اليوم: {hours}. للتواصل العاجل راسلنا على {contact}، وسنرد عليك صباحًا.";
        await SendMessageAsync(new PersistMessageRequest(conversation, LiveSupportSenderType.System, null, null, $"after-hours-{conversation.Id:N}", content, LiveSupportMessageType.System), ct);
    }

    private async Task AssignOldestWaitingAsync(CancellationToken ct, Guid? excludedStaffUserId = null)
    {
        if (_relationalDb?.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true &&
            _relationalDb.Database.CurrentTransaction is null)
        {
            await using var tx = await _db.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            await AcquireRoutingLockAsync(ct);
            await AssignOldestWaitingCoreAsync(ct, excludedStaffUserId);
            await tx.CommitAsync(ct);
            return;
        }

        await AssignOldestWaitingCoreAsync(ct, excludedStaffUserId);
    }

    private async Task AssignOldestWaitingCoreAsync(CancellationToken ct, Guid? excludedStaffUserId)
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
            var queue = await _db.LiveSupportQueueEntries
                .Where(x => x.DequeuedAt == null &&
                            _db.LiveSupportConversations.Any(conversation =>
                                conversation.Id == x.ConversationId &&
                                conversation.Status == LiveSupportConversationStatus.Waiting))
                .OrderBy(x => x.Sequence).ThenBy(x => x.Id)
                .FirstOrDefaultAsync(ct);
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

    private Task<LiveSupportSendResultDto> SendMessageAsync(PersistMessageRequest request, CancellationToken ct) =>
        SendMessageAsync(request, null, ct);

    private async Task<LiveSupportSendResultDto> SendMessageAsync(
        PersistMessageRequest request,
        Action<LiveSupportMessage>? stageBeforeSave,
        CancellationToken ct)
    {
        var (conversation, senderType, userId, guestId, clientMessageId, content, type, attachmentId, replyToMessageId) = request;
        if (IsTerminal(conversation.Status)) throw new LiveSupportException(LiveSupportErrorCodes.ConversationTerminal, "المحادثة مغلقة. ابدأ محادثة جديدة.");
        clientMessageId = clientMessageId.Trim(); content = content.Trim();
        if (clientMessageId.Length is < 8 or > 100 || content.Length is < 1 or > 4000) throw new LiveSupportException("VALIDATION_ERROR", "الرسالة غير صالحة.");
        var existing = await _db.LiveSupportMessages.FirstOrDefaultAsync(x => x.ConversationId == conversation.Id && x.ClientMessageId == clientMessageId, ct);
        if (existing is not null)
        {
            if (existing.Content != content || existing.SenderType != senderType || existing.AttachmentId != attachmentId || existing.ReplyToMessageId != replyToMessageId) throw new LiveSupportException(LiveSupportErrorCodes.MessageConflict, "معرّف الرسالة مستخدم لمحتوى مختلف.");
            return new LiveSupportSendResultDto(ToDto(existing), true);
        }
        var repliedMessage = replyToMessageId.HasValue
            ? await _db.LiveSupportMessages.FirstOrDefaultAsync(x => x.Id == replyToMessageId && x.ConversationId == conversation.Id, ct)
                ?? throw new LiveSupportException("NOT_FOUND", "الرسالة المطلوب الرد عليها غير موجودة.")
            : null;
        if (repliedMessage?.DeletedAt.HasValue == true)
            throw new LiveSupportException("VALIDATION_ERROR", "لا يمكن الرد على رسالة محذوفة.");
        var message = new LiveSupportMessage { ConversationId = conversation.Id, SenderType = senderType, SenderUserId = userId, SenderGuestSessionId = guestId, ClientMessageId = clientMessageId, Type = type, Content = content, SentAt = DateTime.UtcNow, AttachmentId = attachmentId, ReplyToMessageId = replyToMessageId, ReplyToMessage = repliedMessage };
        conversation.LastMessageAt = message.SentAt; conversation.Version++;
        _db.LiveSupportMessages.Add(message);
        AddEvent(conversation.Id, LiveSupportEventType.MessageSent, userId, guestId, message.Id, senderType.ToString());
        if (_aiTurnOrchestrator is not null &&
            (senderType == LiveSupportSenderType.Student || senderType == LiveSupportSenderType.Guest))
        {
            await _aiTurnOrchestrator.QueueForParticipantMessageAsync(conversation.Id, message.Id, ct);
        }
        stageBeforeSave?.Invoke(message);
        await _db.SaveChangesAsync(ct);

        return new LiveSupportSendResultDto(ToDto(message), false);
    }

    private static void ValidateParticipantMessageType(LiveSupportMessageType type)
    {
        if (type == LiveSupportMessageType.Audio)
            throw new LiveSupportException(LiveSupportErrorCodes.AudioStaffOnly, "التسجيلات الصوتية متاحة لفريق الدعم فقط.");
        if (type != LiveSupportMessageType.Text)
            throw new LiveSupportException("VALIDATION_ERROR", "نوع الرسالة غير مدعوم بدون مرفق.");
    }

    private static bool IsImageAttachment(string contentType) => contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

    private static bool IsAudioAttachment(string contentType) => contentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase);

    private async Task<LiveSupportMessageDto> UpdateMessageAsync(LiveSupportMessage message, string content, Guid? actorUserId, Guid? actorGuestId, CancellationToken ct)
    {
        await EnsureMessageIsNotWhatsAppAsync(message.Id, ct);
        content = content.Trim();
        if (message.DeletedAt.HasValue) throw new LiveSupportException("MESSAGE_DELETED", "لا يمكن تعديل رسالة محذوفة.");
        if (message.Type != LiveSupportMessageType.Text || message.AttachmentId.HasValue) throw new LiveSupportException("VALIDATION_ERROR", "يمكن تعديل الرسائل النصية فقط.");
        if (content.Length is < 1 or > 4000) throw new LiveSupportException("VALIDATION_ERROR", "نص الرسالة يجب أن يكون بين 1 و4000 حرف.");
        message.Content = content;
        AddEvent(message.ConversationId, LiveSupportEventType.MessageEdited, actorUserId, actorGuestId, message.Id);
        await _db.SaveChangesAsync(ct);
        return ToDto(message);
    }

    private async Task<LiveSupportMessageDto> DeleteMessageAsync(LiveSupportMessage message, Guid? actorUserId, Guid? actorGuestId, CancellationToken ct)
    {
        await EnsureMessageIsNotWhatsAppAsync(message.Id, ct);
        if (message.DeletedAt.HasValue) return ToDto(message);
        message.Content = string.Empty;
        message.AttachmentId = null;
        message.DeletedAt = DateTime.UtcNow;
        AddEvent(message.ConversationId, LiveSupportEventType.MessageDeleted, actorUserId, actorGuestId, message.Id);
        await _db.SaveChangesAsync(ct);
        return ToDto(message);
    }

    private async Task<LiveSupportMessage> RequireParticipantOwnedMessageAsync(LiveSupportParticipantIdentity participant, Guid conversationId, Guid messageId, CancellationToken ct)
    {
        var message = await _db.LiveSupportMessages.FirstOrDefaultAsync(x => x.Id == messageId && x.ConversationId == conversationId, ct)
            ?? throw new LiveSupportException("NOT_FOUND", "الرسالة غير موجودة.");
        var ownedByStudent = participant.StudentUserId.HasValue && message.SenderUserId == participant.StudentUserId;
        var ownedByGuest = participant.GuestSessionId.HasValue && message.SenderGuestSessionId == participant.GuestSessionId;
        if (!ownedByStudent && !ownedByGuest) throw new LiveSupportException(LiveSupportErrorCodes.Forbidden, "لا يمكنك تغيير رسالة شخص آخر.");
        return message;
    }

    private async Task<LiveSupportMessage> RequireStaffOwnedMessageAsync(Guid staffUserId, Guid conversationId, Guid messageId, CancellationToken ct)
    {
        var message = await _db.LiveSupportMessages.FirstOrDefaultAsync(x => x.Id == messageId && x.ConversationId == conversationId, ct)
            ?? throw new LiveSupportException("NOT_FOUND", "الرسالة غير موجودة.");
        if (message.SenderUserId != staffUserId || message.SenderType is not (LiveSupportSenderType.Staff or LiveSupportSenderType.Admin))
            throw new LiveSupportException(LiveSupportErrorCodes.Forbidden, "لا يمكنك تغيير رسالة شخص آخر.");
        return message;
    }

    private sealed record PersistMessageRequest(
        LiveSupportConversation Conversation,
        LiveSupportSenderType SenderType,
        Guid? UserId,
        Guid? GuestId,
        string ClientMessageId,
        string Content,
        LiveSupportMessageType Type,
        Guid? AttachmentId = null,
        Guid? ReplyToMessageId = null);

    private async Task FinishConversationAsync(LiveSupportConversation c, Guid? actor, LiveSupportConversationStatus status, string reason, LiveSupportAssignmentEndReason endReason, CancellationToken ct)
    {
        if (IsTerminal(c.Status)) throw new LiveSupportException(LiveSupportErrorCodes.ConversationTerminal, "المحادثة مغلقة بالفعل.");
        var formerOwnerUserId = c.CurrentOwnerUserId;
        var now = DateTime.UtcNow; c.Status = status; c.ClosedAt = now; c.ClosedByUserId = actor; c.CloseReason = reason.Trim()[..Math.Min(reason.Trim().Length, 500)]; c.CurrentOwnerUserId = null; c.Version++;
        var assignment = await _db.LiveSupportAssignments.FirstOrDefaultAsync(x => x.ConversationId == c.Id && x.EndedAt == null, ct);
        if (assignment is not null) { assignment.EndedAt = now; assignment.EndReason = endReason; }
        var queue = await _db.LiveSupportQueueEntries.FirstOrDefaultAsync(x => x.ConversationId == c.Id && x.DequeuedAt == null, ct);
        if (queue is not null) { queue.DequeuedAt = now; queue.DequeueReason = status.ToString(); }
        AddEvent(c.Id, status == LiveSupportConversationStatus.Closed ? LiveSupportEventType.Closed : LiveSupportEventType.Abandoned, actor, null, staffUserId: formerOwnerUserId);
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
        if (items.Count == 0) return [];

        var conversationIds = items.Select(x => x.Id).ToArray();
        var participantUserIds = items
            .Where(x => x.ParticipantType == LiveSupportParticipantType.Student && x.StudentUserId.HasValue)
            .Select(x => x.StudentUserId!.Value)
            .Distinct()
            .ToArray();
        var participantGuestIds = items
            .Where(x => x.ParticipantType == LiveSupportParticipantType.Guest && x.GuestSessionId.HasValue)
            .Select(x => x.GuestSessionId!.Value)
            .Distinct()
            .ToArray();
        var participantUserNames = await _db.Users.AsNoTracking()
            .Where(x => participantUserIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.FullName, ct);
        var participantGuestNames = await _db.LiveSupportGuestSessions.AsNoTracking()
            .Where(x => participantGuestIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.DisplayName, ct);
        var whatsAppBindings = await _db.LiveSupportWhatsAppBindings.AsNoTracking()
            .Where(x => conversationIds.Contains(x.ConversationId))
            .ToDictionaryAsync(x => x.ConversationId, ct);
        var activeQueue = await ActiveQueueEntries().AsNoTracking()
            .OrderBy(x => x.EnteredAt)
            .Select(x => new { x.ConversationId, x.EnteredAt })
            .ToListAsync(ct);
        var queuePositions = activeQueue
            .Select((row, index) => new { row.ConversationId, Position = index + 1 })
            .ToDictionary(x => x.ConversationId, x => x.Position);
        var states = await _db.LiveSupportAIConversationStates.AsNoTracking()
            .Where(x => conversationIds.Contains(x.ConversationId))
            .ToListAsync(ct);
        var statesByConversation = states.ToDictionary(x => x.ConversationId);
        var policyIds = states.Select(x => x.PolicyVersionId).Distinct().ToArray();
        var policyVersions = await _db.LiveSupportAIPolicyVersions.AsNoTracking()
            .Where(x => policyIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.VersionNumber, ct);
        var typingConversationIds = await _db.LiveSupportAITurns.AsNoTracking()
            .Where(x =>
                conversationIds.Contains(x.ConversationId) &&
                (x.Status == LiveSupportAITurnStatus.Queued ||
                 x.Status == LiveSupportAITurnStatus.Processing))
            .Select(x => x.ConversationId)
            .Distinct()
            .ToListAsync(ct);
        var typingSet = typingConversationIds.ToHashSet();
        var ratedConversationIds = await _db.LiveSupportRatings.AsNoTracking()
            .Where(x => conversationIds.Contains(x.ConversationId))
            .Select(x => x.ConversationId)
            .Distinct()
            .ToListAsync(ct);
        var ratedSet = ratedConversationIds.ToHashSet();
        var unreadParticipantMessageCounts = await _db.LiveSupportMessages.AsNoTracking()
            .Where(x => conversationIds.Contains(x.ConversationId) &&
                        (x.SenderType == LiveSupportSenderType.Student || x.SenderType == LiveSupportSenderType.Guest) &&
                        x.ReadAt == null && x.DeletedAt == null)
            .GroupBy(x => x.ConversationId)
            .Select(group => new { ConversationId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(x => x.ConversationId, x => x.Count, ct);
        var verificationRows = await _db.LiveSupportAIVerificationSessions.AsNoTracking()
            .Where(x => conversationIds.Contains(x.ConversationId))
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new { x.ConversationId, Status = x.Status.ToString(), x.CreatedAt })
            .ToListAsync(ct);
        var verificationByConversation = verificationRows
            .GroupBy(x => x.ConversationId)
            .ToDictionary(x => x.Key, x => x.First().Status);
        var attemptedActionRows = await _db.LiveSupportAIPendingActions.AsNoTracking()
            .Where(x => conversationIds.Contains(x.ConversationId))
            .Select(x => new { x.ConversationId, x.ActionKey })
            .Distinct()
            .ToListAsync(ct);
        var attemptedActionsByConversation = attemptedActionRows
            .GroupBy(x => x.ConversationId)
            .ToDictionary(x => x.Key, x => (IReadOnlyList<string>)x.Select(value => value.ActionKey).ToList());
        var failedTurnRows = await _db.LiveSupportAITurns.AsNoTracking()
            .Where(x =>
                conversationIds.Contains(x.ConversationId) &&
                x.Status == LiveSupportAITurnStatus.Failed &&
                x.FailureCode != null)
            .Select(x => new { x.ConversationId, FailureCode = x.FailureCode! })
            .Distinct()
            .ToListAsync(ct);
        var failedTurnsByConversation = failedTurnRows
            .GroupBy(x => x.ConversationId)
            .ToDictionary(x => x.Key, x => (IReadOnlyList<string>)x.Select(value => value.FailureCode).ToList());

        return items.Select(c =>
        {
            statesByConversation.TryGetValue(c.Id, out var state);
            var isAiActive = state?.Mode == LiveSupportAIMode.AiActive;
            LiveSupportAISummaryDto? aiSummary = null;
            if (state is not null)
            {
                aiSummary = new LiveSupportAISummaryDto(
                    state.HandoffSafeSummary,
                    state.HandoffReasonCode,
                    policyVersions.TryGetValue(state.PolicyVersionId, out var policyVersion)
                        ? policyVersion
                        : null,
                    verificationByConversation.GetValueOrDefault(c.Id),
                    attemptedActionsByConversation.GetValueOrDefault(c.Id) ?? [],
                    failedTurnsByConversation.GetValueOrDefault(c.Id) ?? []);
            }

            whatsAppBindings.TryGetValue(c.Id, out var whatsAppBinding);
            return new LiveSupportConversationDto(
                c.Id,
                c.ParticipantType,
                c.Status,
                c.CurrentOwnerUserId,
                c.LinkedStudentUserId,
                c.ParticipantType == LiveSupportParticipantType.Student && c.StudentUserId.HasValue
                    ? participantUserNames.GetValueOrDefault(c.StudentUserId.Value)
                    : c.GuestSessionId.HasValue
                        ? participantGuestNames.GetValueOrDefault(c.GuestSessionId.Value)
                        : null,
                c.Subject,
                c.CreatedAt,
                c.QueuedAt,
                c.AssignedAt,
                c.ClosedAt,
                c.Status == LiveSupportConversationStatus.Waiting
                    ? queuePositions.GetValueOrDefault(c.Id)
                    : null,
                c.Version,
                !IsTerminal(c.Status),
                IsTerminal(c.Status) && !ratedSet.Contains(c.Id),
                isAiActive,
                isAiActive && typingSet.Contains(c.Id),
                aiSummary,
                unreadParticipantMessageCounts.GetValueOrDefault(c.Id),
                whatsAppBinding is null ? "Web" : "WhatsApp",
                whatsAppBinding?.PhoneNumber,
                whatsAppBinding?.CustomerServiceWindowExpiresAt);
        }).ToList();
    }

    private async Task<LiveSupportConversationDto> MapAsync(LiveSupportConversation c, CancellationToken ct)
    {
        int? position = null;
        if (c.Status == LiveSupportConversationStatus.Waiting && c.QueuedAt.HasValue)
            position = await ActiveQueueEntries().CountAsync(entry => entry.EnteredAt <= c.QueuedAt, ct);
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

        var unreadParticipantMessageCount = await _db.LiveSupportMessages.AsNoTracking()
            .CountAsync(x => x.ConversationId == c.Id &&
                             (x.SenderType == LiveSupportSenderType.Student || x.SenderType == LiveSupportSenderType.Guest) &&
                             x.ReadAt == null && x.DeletedAt == null, ct);
        var participantName = c.ParticipantType == LiveSupportParticipantType.Student && c.StudentUserId.HasValue
            ? await _db.Users.AsNoTracking().Where(x => x.Id == c.StudentUserId.Value).Select(x => x.FullName).FirstOrDefaultAsync(ct)
            : c.GuestSessionId.HasValue
                ? await _db.LiveSupportGuestSessions.AsNoTracking().Where(x => x.Id == c.GuestSessionId.Value).Select(x => x.DisplayName).FirstOrDefaultAsync(ct)
                : null;
        var whatsAppBinding = await _db.LiveSupportWhatsAppBindings.AsNoTracking().FirstOrDefaultAsync(x => x.ConversationId == c.Id, ct);
        return new LiveSupportConversationDto(c.Id, c.ParticipantType, c.Status, c.CurrentOwnerUserId, c.LinkedStudentUserId, participantName, c.Subject, c.CreatedAt, c.QueuedAt, c.AssignedAt, c.ClosedAt, position, c.Version, !IsTerminal(c.Status), IsTerminal(c.Status) && !await _db.LiveSupportRatings.AnyAsync(x => x.ConversationId == c.Id, ct), isAiActive, isAiTyping, aiSummary, unreadParticipantMessageCount, whatsAppBinding is null ? "Web" : "WhatsApp", whatsAppBinding?.PhoneNumber, whatsAppBinding?.CustomerServiceWindowExpiresAt);
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
            var content = "نعتذر، واجه المساعد الذكي مشكلة في معالجة طلبك حالياً. هل تود التحويل إلى أحد موظفي الدعم؟";
            var message = new LiveSupportMessage
            {
                ConversationId = turn.ConversationId,
                SenderType = LiveSupportSenderType.AI,
                ClientMessageId = $"ai-fail-{turn.Id:N}",
                Type = LiveSupportMessageType.Text,
                Content = content,
                SentAt = DateTime.UtcNow
            };
            _db.LiveSupportMessages.Add(message);
            conversation.LastMessageAt = message.SentAt;
            conversation.Version++;

            aiState.HandoffReasonCode = "AI_TURN_FAILED";
            aiState.HandoffSafeSummary = $"تعذر إكمال طلبك تلقائياً ({request.FailureCode})";
            aiState.Version++;

            var policy = await _db.LiveSupportAIPolicyVersions.FirstOrDefaultAsync(x => x.Id == turn.PolicyVersionId, ct);
            var expirySeconds = policy?.PendingActionExpirySeconds > 0 ? policy.PendingActionExpirySeconds : 300;

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

            AddEvent(conversation.Id, LiveSupportEventType.MessageSent, null, null, message.Id);
            AddEvent(conversation.Id, LiveSupportEventType.AIReplySent, null, null, message.Id);
            AddEvent(conversation.Id, LiveSupportEventType.AIHandoffRequested, null, null, turn.Id);
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

    private async Task<IReadOnlyList<LiveSupportAdminConversationDto>> MapAdminConversationsAsync(
        IReadOnlyList<LiveSupportConversation> conversations,
        CancellationToken ct)
    {
        if (conversations.Count == 0) return [];

        var userIds = conversations
            .SelectMany(x => new[] { x.StudentUserId, x.CurrentOwnerUserId })
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToArray();
        var guestIds = conversations
            .Where(x => x.GuestSessionId.HasValue)
            .Select(x => x.GuestSessionId!.Value)
            .Distinct()
            .ToArray();
        var userNames = await _db.Users.AsNoTracking()
            .Where(x => userIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.FullName, ct);
        var guestNames = await _db.LiveSupportGuestSessions.AsNoTracking()
            .Where(x => guestIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.DisplayName, ct);
        var conversationIds = conversations.Select(x => x.Id).ToArray();
        var whatsAppBindings = await _db.LiveSupportWhatsAppBindings.AsNoTracking()
            .Where(x => conversationIds.Contains(x.ConversationId))
            .ToDictionaryAsync(x => x.ConversationId, ct);
        var latestWhatsAppStatuses = await _db.LiveSupportWhatsAppMessages.AsNoTracking()
            .Where(x => conversationIds.Contains(x.ConversationId))
            .GroupBy(x => x.ConversationId)
            .Select(group => group
                .OrderByDescending(message => message.CreatedAt)
                .ThenByDescending(message => message.Id)
                .Select(message => new { message.ConversationId, message.Status })
                .First())
            .ToDictionaryAsync(item => item.ConversationId, item => item.Status, ct);

        return conversations.Select(c =>
        {
            var participantName = c.ParticipantType == LiveSupportParticipantType.Student && c.StudentUserId.HasValue
                ? userNames.GetValueOrDefault(c.StudentUserId.Value)
                : c.GuestSessionId.HasValue
                    ? guestNames.GetValueOrDefault(c.GuestSessionId.Value)
                    : null;
            var ownerName = c.CurrentOwnerUserId.HasValue
                ? userNames.GetValueOrDefault(c.CurrentOwnerUserId.Value)
                : null;
            whatsAppBindings.TryGetValue(c.Id, out var whatsAppBinding);
            return new LiveSupportAdminConversationDto(
                c.Id,
                participantName ?? "غير معروف",
                c.ParticipantType,
                c.Status,
                ownerName,
                c.CreatedAt,
                c.AssignedAt,
                c.FirstStaffResponseAt,
                c.ClosedAt,
                c.AssignedAt.HasValue ? (c.AssignedAt.Value - c.CreatedAt).TotalSeconds : null,
                c.ClosedAt.HasValue && c.AssignedAt.HasValue
                    ? (c.ClosedAt.Value - c.AssignedAt.Value).TotalSeconds
                    : null,
                c.Subject,
                null,
                null,
                whatsAppBinding is null ? "Web" : "WhatsApp",
                whatsAppBinding?.PhoneNumber,
                whatsAppBinding?.CustomerServiceWindowExpiresAt,
                latestWhatsAppStatuses.GetValueOrDefault(c.Id));
        }).ToList();
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
        LiveSupportEventType.WhatsAppDeliveryStatusChanged => "تحديث حالة رسالة واتساب",
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

    private void AddEvent(Guid conversationId, LiveSupportEventType type, Guid? actor, Guid? guest, Guid? relatedId = null, string? senderType = null, Guid? staffUserId = null)
    {
        var eventId = Guid.NewGuid();
        var occurredAt = DateTime.UtcNow;
        var sequence = occurredAt.Ticks;
        var pendingSequence = _db.LiveSupportEvents.Local
            .Where(x => x.ConversationId == conversationId)
            .Select(x => x.Sequence)
            .DefaultIfEmpty(0)
            .Max();
        if (pendingSequence >= sequence) sequence = pendingSequence + 1;
        _db.LiveSupportEvents.Add(new LiveSupportEvent { Id = eventId, ConversationId = conversationId, Type = type, ActorUserId = actor, ActorGuestSessionId = guest, RelatedEntityId = relatedId, OccurredAt = occurredAt, Sequence = sequence });
        var payload = System.Text.Json.JsonSerializer.Serialize(new { eventId, conversationId, sequence, occurredAt, type = type.ToString(), payload = new { relatedId, senderType } });
        _db.OutboxEvents.Add(new OutboxEvent { Type = "LiveSupportEvent", TargetGroup = $"LiveSupport:Conversation:{conversationId:N}", PayloadJson = payload });
        _db.OutboxEvents.Add(new OutboxEvent { Type = "LiveSupportEvent", TargetGroup = "LiveSupport:Admins", PayloadJson = payload });
        var ownerUserId = staffUserId ?? _db.LiveSupportConversations.Local.FirstOrDefault(item => item.Id == conversationId)?.CurrentOwnerUserId;
        if (ownerUserId.HasValue)
            _db.OutboxEvents.Add(new OutboxEvent { Type = "LiveSupportEvent", TargetGroup = $"LiveSupport:Staff:{ownerUserId.Value:N}", PayloadJson = payload });
    }
    private static LiveSupportMessageDto ToDto(LiveSupportMessage message) => new(message.Id, message.ConversationId, message.SenderType, message.ClientMessageId, message.Type, message.Content, message.SentAt, message.AttachmentId, message.DeliveredAt, message.ReadAt, message.UpdatedAt, message.DeletedAt, null, message.ReplyToMessage is null ? null : new LiveSupportReplyDto(message.ReplyToMessage.Id, message.ReplyToMessage.SenderType, message.ReplyToMessage.Type, message.ReplyToMessage.Content, message.ReplyToMessage.DeletedAt.HasValue));

    private void StageStaffMessageSideEffects(
        LiveSupportConversation conversation,
        Guid staffUserId,
        LiveSupportMessage message,
        WhatsAppOutboundDraft? whatsAppOutbound)
    {
        if (whatsAppOutbound is not null)
        {
            _db.LiveSupportWhatsAppMessages.Add(new LiveSupportWhatsAppMessage
            {
                ConversationId = conversation.Id,
                LiveSupportMessageId = message.Id,
                Direction = "Outbound",
                MessageType = whatsAppOutbound.MessageType,
                Status = "Pending",
                TemplateName = whatsAppOutbound.TemplateName,
                TemplateLanguage = whatsAppOutbound.TemplateLanguage,
                TemplateParametersJson = whatsAppOutbound.TemplateParametersJson,
                Version = 1
            });
        }

        if (!conversation.FirstStaffResponseAt.HasValue)
        {
            conversation.FirstStaffResponseAt = DateTime.UtcNow;
            conversation.Status = LiveSupportConversationStatus.Active;
            AddEvent(conversation.Id, LiveSupportEventType.FirstStaffResponse, staffUserId, null);
        }
    }

    private static LiveSupportSendResultDto WithPendingWhatsAppStatus(LiveSupportSendResultDto sendResult, bool isWhatsApp) =>
        isWhatsApp && !sendResult.Replayed
            ? sendResult with { Message = sendResult.Message with { ExternalDeliveryStatus = "Pending" } }
            : sendResult;

    private sealed record WhatsAppOutboundDraft(
        string MessageType,
        string? TemplateName = null,
        string? TemplateLanguage = null,
        string? TemplateParametersJson = null);

    private static string RenderWhatsAppTemplatePreview(
        LiveSupportWhatsAppTemplate template,
        IReadOnlyList<string> parameters)
    {
        try
        {
            using var document = JsonDocument.Parse(template.ComponentsJson);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                throw new LiveSupportException("WHATSAPP_TEMPLATE_INVALID", "بيانات قالب واتساب غير صالحة.");

            var parameterIndex = 0;
            var renderedParts = new List<string>();
            foreach (var component in document.RootElement.EnumerateArray())
            {
                if (!component.TryGetProperty("text", out var textProperty) || textProperty.ValueKind != JsonValueKind.String)
                    continue;
                var text = textProperty.GetString();
                if (string.IsNullOrWhiteSpace(text)) continue;
                renderedParts.Add(Regex.Replace(text, @"\{\{\d+\}\}", _ =>
                {
                    if (parameterIndex >= parameters.Count)
                        throw new LiveSupportException("WHATSAPP_TEMPLATE_PARAMETERS_INVALID", "عدد قيم قالب واتساب غير مطابق للقالب.");
                    return parameters[parameterIndex++];
                }));
            }

            if (parameterIndex != parameters.Count)
                throw new LiveSupportException("WHATSAPP_TEMPLATE_PARAMETERS_INVALID", "عدد قيم قالب واتساب غير مطابق للقالب.");
            var preview = string.Join('\n', renderedParts).Trim();
            return preview.Length > 0 ? preview : $"قالب واتساب: {template.Name}";
        }
        catch (JsonException)
        {
            throw new LiveSupportException("WHATSAPP_TEMPLATE_INVALID", "بيانات قالب واتساب غير صالحة.");
        }
    }

    private async Task EnsureMessageIsNotWhatsAppAsync(Guid messageId, CancellationToken ct)
    {
        if (await _db.LiveSupportWhatsAppMessages.AsNoTracking().AnyAsync(item => item.LiveSupportMessageId == messageId, ct))
            throw new LiveSupportException(LiveSupportErrorCodes.WhatsAppMessageImmutable, "لا يمكن تعديل أو حذف رسالة واتساب بعد تسجيلها للإرسال أو الاستلام.");
    }

    private async Task<IReadOnlyList<LiveSupportMessageDto>> EnrichMessageDtosAsync(
        IReadOnlyList<LiveSupportMessage> messages,
        CancellationToken ct)
    {
        var staffIds = messages
            .Where(x => x.SenderUserId.HasValue && x.SenderType is LiveSupportSenderType.Staff or LiveSupportSenderType.Admin)
            .Select(x => x.SenderUserId!.Value)
            .Distinct()
            .ToArray();
        var names = staffIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await _db.Users.AsNoTracking()
                .Where(x => staffIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.FullName, ct);
        var messageIds = messages.Select(message => message.Id).ToArray();
        var deliveryStatuses = messageIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await _db.LiveSupportWhatsAppMessages.AsNoTracking()
                .Where(delivery => delivery.LiveSupportMessageId.HasValue && messageIds.Contains(delivery.LiveSupportMessageId.Value))
                .ToDictionaryAsync(delivery => delivery.LiveSupportMessageId!.Value, delivery => delivery.Status, ct);

        return messages.Select(message =>
        {
            var dto = ToDto(message);
            if (deliveryStatuses.TryGetValue(message.Id, out var deliveryStatus))
                dto = dto with { ExternalDeliveryStatus = deliveryStatus };
            return message.SenderUserId.HasValue && names.TryGetValue(message.SenderUserId.Value, out var name)
                ? dto with { SenderDisplayName = name }
                : dto;
        }).ToList();
    }
    private static bool IsTerminal(LiveSupportConversationStatus s) => s is LiveSupportConversationStatus.Closed or LiveSupportConversationStatus.Abandoned;
    private static void EnsureWhatsAppWindowOpen(LiveSupportWhatsAppBinding? binding)
    {
        if (binding is not null && binding.CustomerServiceWindowExpiresAt <= DateTime.UtcNow)
            throw new LiveSupportException("WHATSAPP_WINDOW_CLOSED", "انتهت نافذة واتساب لمدة 24 ساعة. استخدم قالبًا معتمدًا لبدء المحادثة من جديد.");
    }
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
            CurrentCairoDate().AddYears(-15).ToDateTime(TimeOnly.MinValue),
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
            DateTime.SpecifyKind(action.ExpiresAt, DateTimeKind.Utc)
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
        var messageRows = await _db.LiveSupportMessages.AsNoTracking().Where(x => x.ConversationId == conversationId)
            .OrderByDescending(x => x.SentAt).ThenByDescending(x => x.Id).Take(50).ToListAsync(ct);
        messageRows.Reverse();
        var messages = (await EnrichMessageDtosAsync(messageRows, ct)).ToList();
        var lastSequence = await _db.LiveSupportEvents.AsNoTracking().Where(x => x.ConversationId == conversationId)
            .Select(x => (long?)x.Sequence).MaxAsync(ct) ?? 0;
        int? queuePosition = null;
        if (conversation.Status == LiveSupportConversationStatus.Waiting && conversation.QueuedAt.HasValue)
            queuePosition = await ActiveQueueEntries().CountAsync(entry => entry.EnteredAt <= conversation.QueuedAt, ct);

        return new LiveSupportAIParticipantSnapshotDto(
            conversation.Id,
            conversation.Status.ToString(),
            state?.Mode,
            lastSequence,
            !IsTerminal(conversation.Status) && (state is null || state.Mode is LiveSupportAIMode.AiActive or LiveSupportAIMode.HumanAssigned),
            turn?.Status.ToString(),
            pending is null ? null : new LiveSupportAIPendingDecisionDto(pending.Id, pending.DecisionKind, pending.ActionKey, pending.SafeProposalJson, pending.Status, DateTime.SpecifyKind(pending.ExpiresAt, DateTimeKind.Utc), pending.FailureCode),
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
