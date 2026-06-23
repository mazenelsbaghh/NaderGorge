using System.Data;
using System.Text;
using Microsoft.EntityFrameworkCore;
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

namespace NaderGorge.Infrastructure.Services;

public sealed class LiveSupportService(IAppDbContext db, ICachedPlatformSettingsReader settings, ILiveSupportPresenceStore? presence = null, ILiveSupportAttachmentStorage? attachmentStorage = null, ILogger<LiveSupportService>? logger = null) : ILiveSupportService, ILiveSupportAssignmentCoordinator
{
    private readonly IAppDbContext _db = db;
    private readonly ICachedPlatformSettingsReader _settings = settings;
    private readonly AppDbContext? _relationalDb = db as AppDbContext;
    private readonly ILiveSupportPresenceStore? _presence = presence;
    private readonly ILiveSupportAttachmentStorage? _attachmentStorage = attachmentStorage;
    private readonly ILogger<LiveSupportService>? _logger = logger;

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
        _db.LiveSupportQueueEntries.Add(new LiveSupportQueueEntry { ConversationId = conversation.Id, EnteredAt = now, Sequence = now.Ticks });
        AddEvent(conversation.Id, LiveSupportEventType.ConversationCreated, participant.StudentUserId, participant.GuestSessionId);
        AddEvent(conversation.Id, LiveSupportEventType.QueueEntered, participant.StudentUserId, participant.GuestSessionId);
        await _db.SaveChangesAsync(ct);
        await AssignOldestWaitingAsync(ct);
        await tx.CommitAsync(ct);
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
        _db.LiveSupportMessages.Add(message); AddEvent(conversation.Id, LiveSupportEventType.MessageSent, userId, guestId, message.Id);
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
        return new LiveSupportConversationDto(c.Id, c.ParticipantType, c.Status, c.CurrentOwnerUserId, c.LinkedStudentUserId, c.Subject, c.CreatedAt, c.QueuedAt, c.AssignedAt, c.ClosedAt, position, c.Version, !IsTerminal(c.Status), c.Status == LiveSupportConversationStatus.Closed && !await _db.LiveSupportRatings.AnyAsync(x => x.ConversationId == c.Id, ct));
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
            c.AssignedAt.HasValue ? (c.AssignedAt.Value - c.CreatedAt).TotalSeconds : null, c.ClosedAt.HasValue && c.AssignedAt.HasValue ? (c.ClosedAt.Value - c.AssignedAt.Value).TotalSeconds : null);
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
}
