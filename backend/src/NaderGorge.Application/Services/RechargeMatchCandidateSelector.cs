using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Services;

public sealed record RechargeMatchKey(decimal Amount, string SenderPhoneNumber, DateTime SmsReceivedAt);

public static class RechargeMatchCandidateSelector
{
    public static async Task<RechargeRequest?> UniquePendingRequestAsync(
        IQueryable<RechargeRequest> requests,
        RechargeMatchKey matchKey,
        CancellationToken ct)
    {
        var startTime = matchKey.SmsReceivedAt.AddHours(-2);
        var endTime = matchKey.SmsReceivedAt.AddHours(2);
        var candidates = await requests
            .Where(request => request.Amount == matchKey.Amount
                && request.SenderPhoneNumber == matchKey.SenderPhoneNumber
                && request.ScreenshotUrl != null && request.ScreenshotUrl != ""
                && request.Status == RechargeRequestStatus.Pending
                && (request.UpdatedAt ?? request.CreatedAt) >= startTime
                && (request.UpdatedAt ?? request.CreatedAt) <= endTime)
            .OrderBy(request => request.CreatedAt)
            .Take(2)
            .ToListAsync(ct);

        return candidates.Count == 1 ? candidates[0] : null;
    }
}
