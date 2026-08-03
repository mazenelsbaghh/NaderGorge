using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Features.CRM.Commands;
using NaderGorge.Application.Features.Exams.Commands;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.Gamification;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using NaderGorge.Application.Features.LiveSupport.Services;

namespace NaderGorge.Infrastructure.Services;

public sealed class LiveSupportActionService(IAppDbContext db, IMediator mediator, ILiveSupportService support, ILiveSupportPresenceStore presence, ILogger<LiveSupportActionService>? logger = null) : ILiveSupportActionService
{
    private readonly IAppDbContext _db = db;
    private readonly IMediator _mediator = mediator;
    private readonly ILiveSupportService _support = support;
    private readonly ILiveSupportPresenceStore _presence = presence;
    private readonly ILogger<LiveSupportActionService>? _logger = logger;

    private static readonly ActionMetadata[] Catalog =
    [
        new("student.profile.update", "Identity", "تعديل بيانات الطالب", "medium", false, ["identity", "academic", "family"]),
        new("student.password.reset", "Account", "إعادة تعيين كلمة السر", "high", false, ["audit"]),
        new("student.account.status.set", "Account", "تغيير حالة الحساب", "high", true, ["account", "audit"]),
        new("student.note.add", "Notes", "إضافة ملاحظة", "low", false, ["notes", "audit"]),
        new("student.note.delete", "Notes", "حذف ملاحظة", "medium", true, ["notes", "audit"]),
        new("student.device.disconnect", "Devices", "فصل جهاز", "high", true, ["devices", "audit"]),
        new("student.devices.disconnect-all", "Devices", "فصل كل الأجهزة", "high", true, ["devices", "audit"]),
        new("student.package.cancel", "Packages", "إلغاء باقة", "financial", true, ["packages", "balance", "audit"]),
        new("student.balance.adjust", "Balance", "تعديل الرصيد", "financial", true, ["balance", "audit"]),
        new("student.gamification.adjust", "Gamification", "تعديل النقاط", "medium", true, ["gamification", "audit"]),
        new("student.video.override.add", "Watch", "إضافة مشاهدات", "high", true, ["watch", "overrides", "audit"]),
        new("student.watch.reset", "Watch", "تصفير المشاهدة", "high", true, ["watch", "audit"]),
        new("student.watch.count.set", "Watch", "تحديد عدد المشاهدات", "high", true, ["watch", "audit"]),
        new("student.watch-request.approve", "Watch", "قبول طلب مشاهدة", "high", false, ["watch", "requests", "audit"]),
        new("student.watch-request.reject", "Watch", "رفض طلب مشاهدة", "medium", true, ["requests", "audit"]),
        new("student.lesson.unlock", "Academic", "فتح درس", "high", true, ["academic", "audit"]),
        new("student.crm.assign", "CRM", "إسناد CRM", "medium", false, ["crm", "audit"]),
        new("student.crm.call.add", "CRM", "تسجيل مكالمة", "low", false, ["crm", "audit"]),
        new("student.create-and-link", "Identity", "إنشاء طالب وربطه", "high", true, ["all"])
    ];

    public async Task<IReadOnlyList<LiveSupportActionDefinitionDto>> GetCatalogAsync(Guid actorUserId, bool isAdmin, Guid conversationId, CancellationToken ct)
    {
        var conversation = await RequireConversationAsync(actorUserId, isAdmin, conversationId, allowUnlinked: true, ct);
        var definitions = new List<LiveSupportActionDefinitionDto>(Catalog.Length);
        foreach (var action in Catalog)
            definitions.Add(new LiveSupportActionDefinitionDto(action.Key, action.Category, action.LabelAr, action.Danger, action.ReasonRequired, await ConfirmationAsync(action.Key, conversation, ct), action.Refresh));
        return definitions;
    }

    public async Task<JsonElement> GetDraftAsync(Guid actorUserId, bool isAdmin, Guid conversationId, string actionKey, CancellationToken ct)
    {
        var conversation = await RequireConversationAsync(actorUserId, isAdmin, conversationId, actionKey == "student.create-and-link", ct);
        if (!Catalog.Any(item => item.Key == actionKey)) throw new LiveSupportException("ACTION_NOT_SUPPORTED", "الإجراء غير مدعوم.");
        return JsonSerializer.SerializeToElement(conversation.LinkedStudentUserId.HasValue
            ? await BuildActionDraftAsync(conversation.LinkedStudentUserId.Value, actionKey, ct)
            : new Dictionary<string, object?>());
    }

    public async Task<JsonElement> GetStudentActionContextAsync(Guid actorUserId, bool isAdmin, Guid conversationId, CancellationToken ct)
    {
        var conversation = await RequireConversationAsync(actorUserId, isAdmin, conversationId, false, ct);
        var studentId = conversation.LinkedStudentUserId ?? throw new LiveSupportException("STUDENT_NOT_LINKED", "اربط المحادثة بطالب أولًا.");
        var now = DateTime.UtcNow;
        var grants = await _db.StudentAccessGrants.AsNoTracking().Where(grant => grant.UserId == studentId && grant.IsActive && (grant.ExpiresAt == null || grant.ExpiresAt > now) && (grant.MaxUses == null || grant.UsesConsumed < grant.MaxUses)).ToListAsync(ct);
        var videos = await _db.LessonVideos.AsNoTracking().Where(video => video.IsActive).Select(video => new
        {
            video.Id, video.VideoTypeId, video.Title, video.MaxWatchCount,
            LessonId = video.LessonId, SectionId = video.Lesson.ContentSectionId, TermId = video.Lesson.ContentSection.TermId, PackageId = video.Lesson.ContentSection.Term.PackageId,
            Teacher = video.Lesson.ContentSection.Term.Package.Teacher.User.FullName, Subject = video.Lesson.ContentSection.Term.Package.Subject.Name, Package = video.Lesson.ContentSection.Term.Package.Name,
            Term = video.Lesson.ContentSection.Term.Title, Course = video.Lesson.ContentSection.Title, Lesson = video.Lesson.Title
        }).ToListAsync(ct);
        var subscribed = videos.Where(video => grants.Any(grant =>
            grant.PackageId == video.PackageId || grant.TermId == video.TermId || grant.ContentSectionId == video.SectionId || grant.LessonId == video.LessonId || grant.LessonVideoId == video.Id ||
            (grant.VideoTypeId == video.VideoTypeId && (grant.PackageId == null || grant.PackageId == video.PackageId) && (grant.TermId == null || grant.TermId == video.TermId) && (grant.ContentSectionId == null || grant.ContentSectionId == video.SectionId) && (grant.LessonId == null || grant.LessonId == video.LessonId)))).ToList();
        var watched = await _db.VideoWatchEvents.AsNoTracking().Where(item => item.UserId == studentId).Select(item => new { item.LessonVideoId, item.WatchCount }).ToDictionaryAsync(item => item.LessonVideoId, item => item.WatchCount, ct);
        var devices = await _db.Devices.AsNoTracking()
            .Where(item => item.UserId == studentId && item.IsActive)
            .OrderByDescending(item => item.LastUsedAt)
            .Select(item => new { item.Id, item.DeviceType, item.OsName, item.BrowserName })
            .ToListAsync(ct);
        var notes = await _db.StudentNotes.AsNoTracking().Where(item => item.StudentId == studentId).OrderByDescending(item => item.IsPinned).ThenByDescending(item => item.CreatedAt).Select(item => new { id = item.Id, label = item.Content }).ToListAsync(ct);
        var displayGrants = await _db.StudentAccessGrants.AsNoTracking().Where(item => item.UserId == studentId && item.IsActive).OrderByDescending(item => item.GrantedAt).Select(item => new { item.Id, item.GrantType, item.PackageId, item.TermId, item.ContentSectionId, item.LessonId, item.LessonVideoId }).ToListAsync(ct);
        var grantedPackageIds = displayGrants.Where(grant => grant.PackageId.HasValue).Select(grant => grant.PackageId!.Value).Distinct().ToList();
        var packageNames = await _db.Packages.AsNoTracking().Where(item => grantedPackageIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, item => item.Name, ct);
        var requests = await _db.ExtraWatchRequests.AsNoTracking().Where(item => item.UserId == studentId && item.Status == RequestStatus.Pending).OrderByDescending(item => item.CreatedAt).Select(item => new { id = item.Id, label = item.LessonVideo.Lesson.Title + " · " + item.LessonVideo.Title }).ToListAsync(ct);
        var staff = await _db.EmployeeProfiles.AsNoTracking().Where(item => item.User != null).OrderBy(item => item.User!.FullName).Select(item => new { id = item.UserId, label = item.User!.FullName }).ToListAsync(ct);
        var balance = await _db.StudentBalances.AsNoTracking().Where(item => item.UserId == studentId).Select(item => (decimal?)item.CurrentBalance).SingleOrDefaultAsync(ct) ?? 0;
        var points = await _db.StudentGamifications.AsNoTracking().Where(item => item.StudentId == studentId).Select(item => (int?)item.TotalPoints).SingleOrDefaultAsync(ct) ?? 0;
        return JsonSerializer.SerializeToElement(new {
            balance,
            points,
            videos = subscribed.Select(video => new { id = video.Id, lessonId = video.LessonId, teacher = video.Teacher, subject = video.Subject, packageName = video.Package, term = video.Term, course = video.Course, lesson = video.Lesson, title = video.Title, watchCount = watched.GetValueOrDefault(video.Id), maxWatchCount = video.MaxWatchCount }),
            devices = devices.Select(item => new
            {
                id = item.Id,
                label = BuildDeviceLabel(item.DeviceType, item.OsName, item.BrowserName)
            }),
            notes = notes.Select(item => new { item.id, label = item.label.Length > 90 ? item.label[..90] + "…" : item.label }),
            grants = displayGrants.Select(item => new { id = item.Id, label = item.PackageId.HasValue && packageNames.TryGetValue(item.PackageId.Value, out var packageName) ? packageName : item.GrantType.ToString() }),
            watchRequests = requests,
            staff
        });
    }

    private static string BuildDeviceLabel(params string?[] parts)
    {
        var label = string.Join(" · ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        return string.IsNullOrWhiteSpace(label) ? "جهاز مسجل" : label;
    }

    private Task<Dictionary<string, object?>> BuildActionDraftAsync(Guid studentId, string actionKey, CancellationToken ct) => actionKey switch
    {
        "student.profile.update" => BuildProfileDraftAsync(studentId, ct),
        "student.account.status.set" => BuildAccountStatusDraftAsync(studentId, ct),
        "student.note.delete" => BuildSingleIdDraftAsync(_db.StudentNotes.Where(x => x.StudentId == studentId).OrderByDescending(x => x.IsPinned).ThenByDescending(x => x.CreatedAt).Select(x => (Guid?)x.Id), "noteId", ct),
        "student.device.disconnect" => BuildSingleIdDraftAsync(_db.Devices.Where(x => x.UserId == studentId && x.IsActive).OrderByDescending(x => x.LastUsedAt).Select(x => (Guid?)x.Id), "deviceId", ct),
        "student.package.cancel" => BuildPackageDraftAsync(studentId, ct),
        "student.video.override.add" or "student.watch.reset" or "student.watch.count.set" => BuildWatchDraftAsync(studentId, actionKey, ct),
        "student.watch-request.approve" or "student.watch-request.reject" => BuildWatchRequestDraftAsync(studentId, actionKey, ct),
        "student.lesson.unlock" => BuildSingleIdDraftAsync(_db.StudentAccessGrants.Where(x => x.UserId == studentId && x.LessonId.HasValue).OrderByDescending(x => x.GrantedAt).Select(x => x.LessonId), "lessonId", ct),
        "student.crm.assign" => BuildCrmDraftAsync(studentId, ct),
        _ => Task.FromResult(new Dictionary<string, object?>())
    };

    private async Task<Dictionary<string, object?>> BuildProfileDraftAsync(Guid studentId, CancellationToken ct)
    {
        var profile = await _db.Users.AsNoTracking().Where(x => x.Id == studentId).Select(x => new { x.FullName, x.PhoneNumber, Governorate = x.StudentProfile!.Governorate, SchoolName = x.StudentProfile!.SchoolName }).SingleAsync(ct);
        return new() { ["fullName"] = profile.FullName, ["phone"] = profile.PhoneNumber, ["governorate"] = profile.Governorate, ["schoolName"] = profile.SchoolName ?? string.Empty };
    }

    private async Task<Dictionary<string, object?>> BuildAccountStatusDraftAsync(Guid studentId, CancellationToken ct) => new() { ["isActive"] = await _db.Users.AsNoTracking().Where(x => x.Id == studentId).Select(x => x.IsActive).SingleAsync(ct) };

    private static async Task<Dictionary<string, object?>> BuildSingleIdDraftAsync(IQueryable<Guid?> query, string fieldName, CancellationToken ct) => new() { [fieldName] = await query.FirstOrDefaultAsync(ct) };

    private async Task<Dictionary<string, object?>> BuildPackageDraftAsync(Guid studentId, CancellationToken ct) => new() { ["accessGrantId"] = await _db.StudentAccessGrants.AsNoTracking().Where(x => x.UserId == studentId && x.IsActive).OrderByDescending(x => x.GrantedAt).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct), ["refundBalance"] = false };

    private async Task<Dictionary<string, object?>> BuildWatchDraftAsync(Guid studentId, string actionKey, CancellationToken ct)
    {
        var watch = await _db.VideoWatchEvents.AsNoTracking().Where(x => x.UserId == studentId).OrderByDescending(x => x.CreatedAt).Select(x => new { x.LessonVideoId, x.WatchCount }).FirstOrDefaultAsync(ct);
        var videoId = watch?.LessonVideoId ?? await _db.StudentAccessGrants.AsNoTracking().Where(x => x.UserId == studentId && x.LessonVideoId.HasValue).OrderByDescending(x => x.GrantedAt).Select(x => x.LessonVideoId).FirstOrDefaultAsync(ct);
        return actionKey == "student.video.override.add" ? new() { ["videoId"] = videoId, ["addedViews"] = 1 } : new() { ["lessonVideoId"] = videoId, ["newWatchCount"] = actionKey == "student.watch.count.set" ? watch?.WatchCount ?? 0 : null };
    }

    private async Task<Dictionary<string, object?>> BuildWatchRequestDraftAsync(Guid studentId, string actionKey, CancellationToken ct) => new() { ["requestId"] = await _db.ExtraWatchRequests.AsNoTracking().Where(x => x.UserId == studentId).OrderByDescending(x => x.CreatedAt).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct), ["addedViews"] = actionKey == "student.watch-request.approve" ? 1 : null };

    private async Task<Dictionary<string, object?>> BuildCrmDraftAsync(Guid studentId, CancellationToken ct) => new() { ["priority"] = await _db.CrmStudentStatuses.AsNoTracking().Where(x => x.StudentId == studentId).Select(x => x.Priority.ToString()).FirstOrDefaultAsync(ct) ?? "Medium" };

    public async Task<LiveSupportActionResultDto> ExecuteAsync(LiveSupportActionRequest request, CancellationToken ct)
    {
        var metadata = Catalog.FirstOrDefault(x => x.Key == request.ActionKey) ?? throw new LiveSupportException("ACTION_NOT_FOUND", "الإجراء غير مدعوم.");
        if (!Guid.TryParse(request.IdempotencyKey, out _)) throw new LiveSupportException("VALIDATION_ERROR", "مفتاح منع التكرار غير صحيح.");
        var conversation = await RequireConversationAsync(request.ActorUserId, request.IsAdmin, request.ConversationId, request.ActionKey == "student.create-and-link", ct);
        var raw = request.Payload.GetRawText();
        var hash = Hash($"{request.ActionKey}|{raw}");
        var previous = await _db.LiveSupportActionExecutions.FirstOrDefaultAsync(x => x.StaffUserId == request.ActorUserId && x.IdempotencyKey == request.IdempotencyKey, ct);
        if (previous is not null)
        {
            if (previous.PayloadHash != hash || previous.ActionKey != request.ActionKey) throw new LiveSupportException("IDEMPOTENCY_CONFLICT", "مفتاح التنفيذ مستخدم لطلب مختلف.");
            return new LiveSupportActionResultDto(previous.Id, request.ActionKey, true, metadata.Refresh, previous.Status == LiveSupportActionStatus.Succeeded ? "تم تنفيذ الإجراء سابقًا." : previous.FailureCode ?? "فشل التنفيذ السابق.");
        }
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(await ConfirmationAsync(request.ActionKey, conversation, ct)), Encoding.UTF8.GetBytes(request.ConfirmationVersion)))
            throw new LiveSupportException("CONFIRMATION_STALE", "تغيرت بيانات الطالب. راجع التأكيد مرة أخرى.");

        var execution = new LiveSupportActionExecution { ConversationId = request.ConversationId, StudentUserId = conversation.LinkedStudentUserId, StaffUserId = request.ActorUserId, ActionKey = request.ActionKey, IdempotencyKey = request.IdempotencyKey, PayloadHash = hash, SafeRequestJson = request.ActionKey is "student.password.reset" or "student.create-and-link" ? "{\"secretRedacted\":true}" : Redact(raw), Status = LiveSupportActionStatus.Pending, StartedAt = DateTime.UtcNow };
        _db.LiveSupportActionExecutions.Add(execution);
        await _db.SaveChangesAsync(ct);
        try
        {
            var message = await DispatchAsync(conversation, request.ActorUserId, request.IsAdmin, request.ActionKey, request.Payload, ct);
            execution.Status = LiveSupportActionStatus.Succeeded; execution.SafeResultJson = JsonSerializer.Serialize(new { message }); execution.CompletedAt = DateTime.UtcNow;
            _db.LiveSupportEvents.Add(new LiveSupportEvent { ConversationId = request.ConversationId, Type = LiveSupportEventType.ActionSucceeded, ActorUserId = request.ActorUserId, RelatedEntityType = "LiveSupportActionExecution", RelatedEntityId = execution.Id, OccurredAt = DateTime.UtcNow, Sequence = DateTime.UtcNow.Ticks, SafeMetadataJson = JsonSerializer.Serialize(new { request.ActionKey }) });
            AddOutbox(request.ConversationId, execution.Id, request.ActionKey, "Succeeded");
            await _db.SaveChangesAsync(ct);
            _logger?.LogInformation("LiveSupport action {ActionKey} execution {ExecutionId} succeeded for conversation {ConversationId} by {ActorUserId}", request.ActionKey, execution.Id, request.ConversationId, request.ActorUserId);
            LiveSupportTelemetry.ActionsSucceeded.Add(1, new KeyValuePair<string, object?>("action_key", request.ActionKey));
            return new LiveSupportActionResultDto(execution.Id, request.ActionKey, false, metadata.Refresh, message);
        }
        catch (Exception ex)
        {
            execution.Status = LiveSupportActionStatus.Failed; execution.FailureCode = ex is LiveSupportException supportError ? supportError.Code : "ACTION_FAILED"; execution.CompletedAt = DateTime.UtcNow;
            _db.LiveSupportEvents.Add(new LiveSupportEvent { ConversationId = request.ConversationId, Type = LiveSupportEventType.ActionFailed, ActorUserId = request.ActorUserId, RelatedEntityId = execution.Id, OccurredAt = DateTime.UtcNow, Sequence = DateTime.UtcNow.Ticks, SafeMetadataJson = JsonSerializer.Serialize(new { request.ActionKey, code = execution.FailureCode }) });
            AddOutbox(request.ConversationId, execution.Id, request.ActionKey, execution.FailureCode ?? "Failed");
            await _db.SaveChangesAsync(ct);
            _logger?.LogWarning("LiveSupport action {ActionKey} execution {ExecutionId} failed with {FailureCode} for conversation {ConversationId} by {ActorUserId}", request.ActionKey, execution.Id, execution.FailureCode, request.ConversationId, request.ActorUserId);
            LiveSupportTelemetry.ActionsFailed.Add(1, new KeyValuePair<string, object?>("action_key", request.ActionKey), new KeyValuePair<string, object?>("failure_code", execution.FailureCode));
            throw;
        }
    }

    private async Task<string> DispatchAsync(LiveSupportConversation conversation, Guid actor, bool isAdmin, string key, JsonElement p, CancellationToken ct)
    {
        var studentId = conversation.LinkedStudentUserId;
        if (key == "student.create-and-link")
        {
            if (studentId.HasValue) throw new LiveSupportException("STUDENT_ALREADY_LINKED", "المحادثة مرتبطة بطالب بالفعل.");
            var created = await _mediator.Send(new AdminCreateUserCommand(Text(p, "fullName"), Text(p, "phoneNumber"), Text(p, "password"), "Student", GuidList(p, "packageIds")), ct);
            if (!created.Success || created.Data is null) throw new LiveSupportException("ACTION_VALIDATION_FAILED", created.Message ?? "تعذر إنشاء الطالب.");
            await _support.ChangeStudentLinkAsync(actor, isAdmin, conversation.Id, created.Data.Id, Text(p, "reason"), conversation.Version, ct);
            return "تم إنشاء الطالب وربطه بالمحادثة.";
        }
        var target = studentId ?? throw new LiveSupportException("STUDENT_NOT_LINKED", "اربط المحادثة بطالب أولًا.");
        ApiResponse result = key switch
        {
            "student.profile.update" => await _mediator.Send(new NaderGorge.Application.Features.Admin.Commands.UpdateStudentProfileCommand(target, OptionalText(p,"fullName"), OptionalText(p,"phone"), OptionalText(p,"parentPhone"), OptionalText(p,"secondaryPhone"), OptionalText(p,"motherPhone"), OptionalText(p,"secondaryParentPhone"), OptionalText(p,"nationality"), OptionalText(p,"governorate"), OptionalText(p,"district"), OptionalText(p,"address"), OptionalText(p,"schoolName"), OptionalText(p,"dateOfBirth"), OptionalText(p,"fatherDateOfBirth"), OptionalText(p,"motherDateOfBirth"), OptionalText(p,"gender"), OptionalText(p,"educationStage"), OptionalText(p,"gradeLevel"), OptionalText(p,"studyTrack"), OptionalText(p,"schoolType"), OptionalText(p,"studentCode"), OptionalBool(p,"isFatherAlive"), OptionalBool(p,"isMotherAlive"), actor), ct),
            "student.password.reset" => await _mediator.Send(new AdminResetPasswordCommand(target, Text(p,"newPassword"), actor), ct),
            "student.account.status.set" => await _mediator.Send(new UpdateUserStatusCommand(target, Bool(p,"isActive") ? "Active" : "Disabled", actor), ct),
            "student.note.add" => await _mediator.Send(new AddStudentNoteCommand(target, Text(p,"content"), OptionalBool(p,"isPinned") ?? false, actor), ct),
            "student.note.delete" => await DeleteNoteAsync(target, actor, p, ct),
            "student.device.disconnect" => await DisconnectDeviceAsync(target, actor, p, ct),
            "student.package.cancel" => await CancelPackageAsync(target, actor, p, ct),
            "student.balance.adjust" => await _mediator.Send(new AdjustBalanceCommand(target, DecimalValue(p,"amount"), Text(p,"reason"), actor), ct),
            "student.video.override.add" => await _mediator.Send(new OverrideVideoLimitCommand(target, GuidValue(p,"videoId"), IntValue(p,"addedViews"), Text(p,"reason"), actor), ct),
            "student.watch.reset" => await _mediator.Send(new ResetWatchLimitCommand(GuidValue(p,"lessonVideoId"), target, actor), ct),
            "student.watch.count.set" => await _mediator.Send(new SetWatchCountCommand(GuidValue(p,"lessonVideoId"), target, IntValue(p,"newWatchCount"), actor), ct),
            "student.lesson.unlock" => await _mediator.Send(new ManualUnlockCommand(GuidValue(p,"lessonId"), target, actor), ct),
            "student.crm.assign" => await _mediator.Send(new AssignStudentToAgentCommand(target, OptionalGuid(p,"assignedAgentId"), EnumValue<CrmPriority>(p,"priority"), OptionalText(p,"notes"), actor), ct),
            "student.crm.call.add" => await _mediator.Send(new LogCrmCallCommand(target, actor, EnumValue<CallOutcome>(p,"outcome"), OptionalText(p,"notes"), OptionalDate(p,"nextFollowUpDate")), ct),
            "student.devices.disconnect-all" => await DisconnectAllAsync(target, actor, ct),
            "student.gamification.adjust" => await AdjustPointsAsync(target, actor, IntValue(p,"points"), Text(p,"reason"), ct),
            _ => ApiResponse.Fail("GENERIC_RESPONSE_REQUIRED")
        };
        if (key == "student.watch-request.approve") return await ApproveWatchRequestAsync(target, actor, p, ct);
        if (key == "student.watch-request.reject") return await RejectWatchRequestAsync(target, p, ct);
        if (!result.Success) throw new LiveSupportException("ACTION_VALIDATION_FAILED", result.Message ?? "تعذر تنفيذ الإجراء.");
        return result.Message ?? "تم تنفيذ الإجراء.";
    }

    private async Task<LiveSupportConversation> RequireConversationAsync(Guid actor, bool isAdmin, Guid conversationId, bool allowUnlinked, CancellationToken ct)
    {
        var conversation = await _db.LiveSupportConversations.FirstOrDefaultAsync(x => x.Id == conversationId, ct) ?? throw new LiveSupportException("NOT_FOUND", "المحادثة غير موجودة.");
        if (!isAdmin && conversation.CurrentOwnerUserId != actor) throw new LiveSupportException(LiveSupportErrorCodes.Forbidden, "المحادثة مملوكة لموظف آخر.");
        if (conversation.Status is LiveSupportConversationStatus.Closed or LiveSupportConversationStatus.Abandoned) throw new LiveSupportException(LiveSupportErrorCodes.ConversationTerminal, "المحادثة مغلقة.");
        if (!isAdmin && !(await _support.GetStaffBootstrapAsync(actor, false, ct)).IsCheckedIn) throw new LiveSupportException(LiveSupportErrorCodes.Forbidden, "يجب تسجيل الحضور أولًا.");
        if (!isAdmin && !await _presence.IsConnectedAsync(actor)) throw new LiveSupportException(LiveSupportErrorCodes.Forbidden, "اتصال الموظف غير نشط.");
        if (!allowUnlinked && !conversation.LinkedStudentUserId.HasValue) throw new LiveSupportException("STUDENT_NOT_LINKED", "اربط المحادثة بطالب أولًا.");
        return conversation;
    }

    private async Task<ApiResponse> DisconnectAllAsync(Guid student, Guid actor, CancellationToken ct) { foreach (var id in await _db.Devices.Where(x => x.UserId == student && x.IsActive).Select(x => x.Id).ToListAsync(ct)) { var r = await _mediator.Send(new RemoveDeviceCommand(id, actor), ct); if (!r.Success) return r; } return ApiResponse.Ok("تم فصل كل الأجهزة."); }
    private async Task<ApiResponse> DeleteNoteAsync(Guid student, Guid actor, JsonElement payload, CancellationToken ct) { var id = GuidValue(payload, "noteId"); if (!await _db.StudentNotes.AnyAsync(x => x.Id == id && x.StudentId == student, ct)) return ApiResponse.Fail("الملاحظة لا تخص الطالب المرتبط."); return await _mediator.Send(new DeleteStudentNoteCommand(id, actor), ct); }
    private async Task<ApiResponse> DisconnectDeviceAsync(Guid student, Guid actor, JsonElement payload, CancellationToken ct) { var id = GuidValue(payload, "deviceId"); if (!await _db.Devices.AnyAsync(x => x.Id == id && x.UserId == student, ct)) return ApiResponse.Fail("الجهاز لا يخص الطالب المرتبط."); return await _mediator.Send(new RemoveDeviceCommand(id, actor), ct); }
    private async Task<ApiResponse> CancelPackageAsync(Guid student, Guid actor, JsonElement payload, CancellationToken ct) { var id = GuidValue(payload, "accessGrantId"); if (!await _db.StudentAccessGrants.AnyAsync(x => x.Id == id && x.UserId == student, ct)) return ApiResponse.Fail("الباقة لا تخص الطالب المرتبط."); return await _mediator.Send(new CancelPackageGrantCommand(id, Bool(payload,"refundBalance"), actor, Text(payload,"reason")), ct); }
    private async Task<string> ApproveWatchRequestAsync(Guid student, Guid actor, JsonElement payload, CancellationToken ct) { var id = GuidValue(payload, "requestId"); if (!await _db.ExtraWatchRequests.AnyAsync(x => x.Id == id && x.UserId == student, ct)) throw new LiveSupportException("ACTION_TARGET_MISMATCH", "الطلب لا يخص الطالب المرتبط."); var response = await _mediator.Send(new ApproveWatchRequestCommand(id, actor, OptionalText(payload,"reason"), OptionalInt(payload,"addedViews") ?? 1), ct); if (!response.Success) throw new LiveSupportException("ACTION_VALIDATION_FAILED", response.Message ?? "فشل الإجراء."); return response.Message ?? "تم التنفيذ."; }
    private async Task<string> RejectWatchRequestAsync(Guid student, JsonElement payload, CancellationToken ct) { var id = GuidValue(payload, "requestId"); if (!await _db.ExtraWatchRequests.AnyAsync(x => x.Id == id && x.UserId == student, ct)) throw new LiveSupportException("ACTION_TARGET_MISMATCH", "الطلب لا يخص الطالب المرتبط."); var response = await _mediator.Send(new RejectWatchRequestCommand(id, Text(payload,"reason")), ct); if (!response.Success) throw new LiveSupportException("ACTION_VALIDATION_FAILED", response.Message ?? "فشل الإجراء."); return response.Message ?? "تم التنفيذ."; }
    private async Task<ApiResponse> AdjustPointsAsync(Guid student, Guid actor, int points, string reason, CancellationToken ct) { var state = await _db.StudentGamifications.FirstOrDefaultAsync(x => x.StudentId == student, ct); if (state is null) { state = new StudentGamification { StudentId = student }; _db.StudentGamifications.Add(state); } if (state.TotalPoints + points < 0) return ApiResponse.Fail("لا يمكن أن يصبح رصيد النقاط سالبًا."); state.TotalPoints += points; _db.AuditLogs.Add(new AuditLog { Action = "LiveSupportAdjustGamification", EntityType = "StudentGamification", EntityId = student, PerformedByUserId = actor, NewValues = JsonSerializer.Serialize(new { points, reason, resultingPoints = state.TotalPoints }), IpAddress = "LiveSupport" }); await _db.SaveChangesAsync(ct); return ApiResponse.Ok("تم تعديل النقاط."); }
    private async Task<string> ConfirmationAsync(string key, LiveSupportConversation conversation, CancellationToken ct)
    {
        var student = conversation.LinkedStudentUserId;
        if (!student.HasValue) return Hash($"{key}|{conversation.Id:N}|unlinked|{conversation.Version}");
        if (key == "student.profile.update")
        {
            var profileState = await _db.Users
                .Where(x => x.Id == student)
                .Select(x => $"{x.FullName}|{x.PhoneNumber}|{x.UpdatedAt}|{x.StudentProfile!.UpdatedAt}")
                .SingleAsync(ct);
            return Hash($"{key}|{conversation.Id:N}|{student:N}|{profileState}");
        }
        var state = key switch
        {
            "student.balance.adjust" or "student.package.cancel" => await _db.StudentBalances.Where(x => x.UserId == student).Select(x => x.CurrentBalance.ToString()).FirstOrDefaultAsync(ct) ?? "0",
            "student.gamification.adjust" => await _db.StudentGamifications.Where(x => x.StudentId == student).Select(x => x.TotalPoints.ToString()).FirstOrDefaultAsync(ct) ?? "0",
            "student.account.status.set" or "student.password.reset" => await _db.Users.Where(x => x.Id == student).Select(x => $"{x.IsActive}:{x.PasswordResetVersion}:{x.UpdatedAt}").FirstAsync(ct),
            "student.device.disconnect" or "student.devices.disconnect-all" => $"{await _db.Devices.CountAsync(x => x.UserId == student && x.IsActive, ct)}:{await _db.Devices.Where(x => x.UserId == student).MaxAsync(x => (DateTime?)x.LastUsedAt, ct)}",
            "student.note.add" or "student.note.delete" => $"{await _db.StudentNotes.CountAsync(x => x.StudentId == student, ct)}:{await _db.StudentNotes.Where(x => x.StudentId == student).MaxAsync(x => (DateTime?)x.CreatedAt, ct)}",
            "student.watch.reset" or "student.watch.count.set" or "student.video.override.add" => $"{await _db.VideoWatchEvents.Where(x => x.UserId == student).SumAsync(x => x.WatchCount, ct)}:{await _db.VideoWatchEvents.CountAsync(x => x.UserId == student, ct)}",
            _ => conversation.Version.ToString()
        };
        return Hash($"{key}|{conversation.Id:N}|{student:N}|{conversation.Version}|{state}");
    }
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string Redact(string raw) => raw.Length > 4000 ? raw[..4000] : raw;
    private void AddOutbox(Guid conversationId, Guid executionId, string actionKey, string status)
    {
        var payload = JsonSerializer.Serialize(new { eventId = Guid.NewGuid(), conversationId, occurredAt = DateTime.UtcNow, type = "StudentActionChanged", payload = new { executionId, actionKey, status } });
        _db.OutboxEvents.Add(new OutboxEvent { Type = "LiveSupportEvent", TargetGroup = $"LiveSupport:Conversation:{conversationId:N}", PayloadJson = payload });
        _db.OutboxEvents.Add(new OutboxEvent { Type = "LiveSupportEvent", TargetGroup = "LiveSupport:Admins", PayloadJson = payload });
    }
    private static string Text(JsonElement p, string key) => p.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString()) ? v.GetString()!.Trim() : throw new LiveSupportException("VALIDATION_ERROR", $"الحقل {key} مطلوب.");
    private static string? OptionalText(JsonElement p, string key) => p.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    private static bool Bool(JsonElement p, string key) => p.TryGetProperty(key, out var v) && (v.ValueKind is JsonValueKind.True or JsonValueKind.False) ? v.GetBoolean() : throw new LiveSupportException("VALIDATION_ERROR", $"الحقل {key} مطلوب.");
    private static bool? OptionalBool(JsonElement p, string key) => p.TryGetProperty(key, out var v) && (v.ValueKind is JsonValueKind.True or JsonValueKind.False) ? v.GetBoolean() : null;
    private static Guid GuidValue(JsonElement p, string key) => Guid.TryParse(Text(p,key), out var id) ? id : throw new LiveSupportException("VALIDATION_ERROR", $"الحقل {key} غير صحيح.");
    private static Guid? OptionalGuid(JsonElement p, string key) => p.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String && Guid.TryParse(v.GetString(), out var id) ? id : null;
    private static int IntValue(JsonElement p, string key) => p.TryGetProperty(key, out var v) && v.TryGetInt32(out var value) ? value : throw new LiveSupportException("VALIDATION_ERROR", $"الحقل {key} غير صحيح.");
    private static int? OptionalInt(JsonElement p, string key) => p.TryGetProperty(key, out var v) && v.TryGetInt32(out var value) ? value : null;
    private static decimal DecimalValue(JsonElement p, string key) => p.TryGetProperty(key, out var v) && v.TryGetDecimal(out var value) ? value : throw new LiveSupportException("VALIDATION_ERROR", $"الحقل {key} غير صحيح.");
    private static DateTime? OptionalDate(JsonElement p, string key) => p.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String && DateTime.TryParse(v.GetString(), out var value) ? value : null;
    private static T EnumValue<T>(JsonElement p, string key) where T : struct, Enum => Enum.TryParse<T>(Text(p,key), true, out var value) ? value : throw new LiveSupportException("VALIDATION_ERROR", $"الحقل {key} غير صحيح.");
    private static List<Guid>? GuidList(JsonElement p, string key) => p.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Array ? v.EnumerateArray().Select(x => Guid.Parse(x.GetString()!)).ToList() : null;
    private sealed record ActionMetadata(string Key, string Category, string LabelAr, string Danger, bool ReasonRequired, string[] Refresh);
}
