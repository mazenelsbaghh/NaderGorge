using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services;

internal static class LiveSupportConversationSearch
{
    public static IQueryable<LiveSupportConversation> Apply(IAppDbContext db, LiveSupportConversationSearchDto request)
    {
        if (request.From > request.To)
            throw new LiveSupportException("VALIDATION_ERROR", "بداية الفترة يجب أن تسبق نهايتها.");
        var query = db.LiveSupportConversations.AsNoTracking();
        if (request.From.HasValue) query = query.Where(conversation => conversation.CreatedAt >= request.From.Value);
        if (request.To.HasValue) query = query.Where(conversation => conversation.CreatedAt <= request.To.Value);
        if (request.Status.HasValue) query = query.Where(conversation => conversation.Status == request.Status.Value);
        if (request.WithComment) query = query.Where(conversation => db.LiveSupportRatings.Any(rating =>
            rating.ConversationId == conversation.Id && rating.Comment != null && rating.Comment.Trim() != ""));
        query = FilterChannel(db, query, request);
        return FilterText(db, query, request.Search);
    }

    private static IQueryable<LiveSupportConversation> FilterChannel(IAppDbContext db, IQueryable<LiveSupportConversation> query, LiveSupportConversationSearchDto request)
    {
        if (request.Channel is not (null or "Web" or "WhatsApp" or "Messenger")
            || request.Window is not (null or "open" or "expired") || request.Activity is not (null or "ai" or "failed"))
            throw new LiveSupportException("VALIDATION_ERROR", "اختر مرشحات محادثة صالحة.");
        if (request.Channel == "WhatsApp") query = query.Where(conversation => db.LiveSupportWhatsAppBindings.Any(binding => binding.ConversationId == conversation.Id));
        if (request.Channel == "Messenger") query = query.Where(conversation => db.LiveSupportMessengerBindings.Any(binding => binding.ConversationId == conversation.Id));
        if (request.Channel == "Web") query = query.Where(conversation => !db.LiveSupportWhatsAppBindings.Any(binding => binding.ConversationId == conversation.Id)
            && !db.LiveSupportMessengerBindings.Any(binding => binding.ConversationId == conversation.Id));
        if (!string.IsNullOrWhiteSpace(request.PageId)) query = query.Where(conversation => db.LiveSupportMessengerBindings.Any(binding =>
            binding.ConversationId == conversation.Id && (binding.PageId == request.PageId || binding.PageName == request.PageId)));
        if (request.Activity == "ai") query = query.Where(conversation => db.LiveSupportAITurns.Any(turn => turn.ConversationId == conversation.Id));
        if (request.Activity == "failed") query = query.Where(conversation => db.LiveSupportAITurns.Any(turn => turn.ConversationId == conversation.Id && turn.Status == LiveSupportAITurnStatus.Failed));
        return FilterWindow(db, query, request.Window);
    }

    private static IQueryable<LiveSupportConversation> FilterWindow(IAppDbContext db, IQueryable<LiveSupportConversation> query, string? window)
    {
        if (window is null) return query;
        var now = DateTime.UtcNow;
        var open = window == "open";
        return query.Where(conversation => db.LiveSupportWhatsAppBindings.Any(binding => binding.ConversationId == conversation.Id
            && (binding.CustomerServiceWindowExpiresAt > now) == open)
            || db.LiveSupportMessengerBindings.Any(binding => binding.ConversationId == conversation.Id
                && (binding.ReplyWindowExpiresAt > now) == open));
    }

    private static IQueryable<LiveSupportConversation> FilterText(IAppDbContext db, IQueryable<LiveSupportConversation> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        var text = search.Trim().ToLowerInvariant();
        var phone = text.Replace(" ", "").Replace("+", "").Replace("-", "").Replace("(", "").Replace(")", "");
        if (phone.Length == 0) phone = text;
        return query.Where(conversation => (conversation.Subject != null && conversation.Subject.ToLower().Contains(text))
            || db.Users.Any(user => (user.Id == conversation.StudentUserId || user.Id == conversation.LinkedStudentUserId
                || user.Id == conversation.CurrentOwnerUserId || db.LiveSupportAssignments.Any(assignment => assignment.ConversationId == conversation.Id && assignment.StaffUserId == user.Id))
                && (user.FullName.ToLower().Contains(text) || user.PhoneNumber.Contains(phone)))
            || db.LiveSupportGuestSessions.Any(guest => guest.Id == conversation.GuestSessionId
                && (guest.DisplayName.ToLower().Contains(text) || (guest.PhoneNumber != null && guest.PhoneNumber.Contains(phone))))
            || db.LiveSupportWhatsAppBindings.Any(binding => binding.ConversationId == conversation.Id
                && (binding.PhoneNumber.Contains(phone) || binding.DisplayName.ToLower().Contains(text)))
            || db.LiveSupportMessengerBindings.Any(binding => binding.ConversationId == conversation.Id
                && (binding.PageName.ToLower().Contains(text) || binding.DisplayName.ToLower().Contains(text)))
            || db.LiveSupportMessages.Any(message => message.ConversationId == conversation.Id && message.DeletedAt == null && message.Content.ToLower().Contains(text))
            || db.LiveSupportRatings.Any(rating => rating.ConversationId == conversation.Id && rating.Comment != null && rating.Comment.ToLower().Contains(text)));
    }
}
