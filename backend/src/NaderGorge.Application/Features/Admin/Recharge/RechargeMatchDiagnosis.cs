using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Recharge;

public static class RechargeMatchDiagnosis
{
    private static readonly TimeSpan EvidenceLookaround = TimeSpan.FromHours(48);

    public static async Task AttachAsync(
        IAppDbContext db,
        IReadOnlyList<AdminRechargeRequestDto> requests,
        CancellationToken ct)
    {
        var pending = requests
            .Where(request => request.Status == RechargeRequestStatus.Pending)
            .ToArray();
        if (pending.Length == 0)
            return;

        var diagnosable = pending
            .Where(CanSearchForEvidence)
            .ToArray();

        foreach (var request in pending.Where(request => !CanSearchForEvidence(request)))
            request.MatchDiagnosis = DiagnoseEvidence(request, pending, []);

        if (diagnosable.Length == 0)
            return;

        var evidence = await LoadRelevantEvidenceAsync(db, diagnosable, ct);
        foreach (var request in diagnosable)
            request.MatchDiagnosis = DiagnoseEvidence(request, pending, evidence);
    }

    private static bool CanSearchForEvidence(AdminRechargeRequestDto request) =>
        !string.IsNullOrWhiteSpace(request.ScreenshotUrl)
        && !string.IsNullOrWhiteSpace(request.SenderPhoneNumber)
        && request.TeacherId.HasValue;

    private static async Task<List<RechargeMatchEvidence>> LoadRelevantEvidenceAsync(
        IAppDbContext db,
        IReadOnlyCollection<AdminRechargeRequestDto> pending,
        CancellationToken ct)
    {
        var earliestAnchor = pending.Min(request => request.MatchingAnchorAt);
        var latestAnchor = pending.Max(request => request.MatchingAnchorAt);
        var from = earliestAnchor - EvidenceLookaround;
        var to = latestAnchor + EvidenceLookaround;
        var phoneNumbers = pending
            .Select(request => request.SenderPhoneNumber)
            .Distinct()
            .ToArray();
        var amounts = pending.Select(request => request.Amount).Distinct().ToArray();

        var rows = await db.IncomingSmsLogs
            .AsNoTracking()
            .Where(log => log.ReceivedAt >= from
                && log.ReceivedAt <= to
                && log.ParsedAmount.HasValue
                && log.ParsedSenderPhone != null
                && (phoneNumbers.Contains(log.ParsedSenderPhone) || amounts.Contains(log.ParsedAmount.Value)))
            .Select(log => new RechargeMatchEvidenceRow
            {
                Id = log.Id,
                WalletId = log.WalletId,
                WalletLabel = log.Wallet.Label,
                Body = log.Body,
                ReceivedAt = log.ReceivedAt,
                Amount = log.ParsedAmount!.Value,
                SenderPhoneNumber = log.ParsedSenderPhone!,
                IsMatched = log.IsMatched,
                MatchedRechargeRequestId = log.MatchedRechargeRequestId
            })
            .ToListAsync(ct);

        // Direction is not persisted on legacy SMS rows. Keep the body only in this
        // short-lived projection so outgoing transfers can never become recharge evidence.
        return rows
            .Where(row => !SmsParser.IsOutgoingTransfer(row.Body))
            .Select(row => row.ToEvidence())
            .ToList();
    }

    public static AdminRechargeMatchDiagnosisDto Diagnose(
        AdminRechargeRequestDto request,
        IReadOnlyList<AdminRechargeRequestDto> pendingRequests,
        IReadOnlyList<IncomingSmsLog> smsLogs)
    {
        var evidence = smsLogs
            .Where(sms => sms.ParsedAmount.HasValue
                && !string.IsNullOrWhiteSpace(sms.ParsedSenderPhone)
                && !SmsParser.IsOutgoingTransfer(sms.Body))
            .Select(sms => new RechargeMatchEvidence(
                sms.Id,
                sms.WalletId,
                sms.Wallet?.Label ?? string.Empty,
                sms.ReceivedAt,
                sms.ParsedAmount!.Value,
                sms.ParsedSenderPhone!,
                sms.IsMatched,
                sms.MatchedRechargeRequestId))
            .ToArray();

        return DiagnoseEvidence(request, pendingRequests, evidence);
    }

    private static AdminRechargeMatchDiagnosisDto DiagnoseEvidence(
        AdminRechargeRequestDto request,
        IReadOnlyList<AdminRechargeRequestDto> pendingRequests,
        IReadOnlyList<RechargeMatchEvidence> evidence)
    {
        if (string.IsNullOrWhiteSpace(request.ScreenshotUrl) || string.IsNullOrWhiteSpace(request.SenderPhoneNumber))
            return CreateDiagnosis(RechargeMatchDiagnosisCode.AwaitingEvidence);

        if (!request.TeacherId.HasValue)
            return CreateDiagnosis(RechargeMatchDiagnosisCode.MissingTeacherScope);

        var exactUnmatchedInWindow = evidence
            .Where(sms => !sms.IsMatched
                && sms.Amount == request.Amount
                && sms.SenderPhoneNumber == request.SenderPhoneNumber
                && IsWithinRequestWindow(request, sms.ReceivedAt))
            .OrderBy(sms => sms.ReceivedAt)
            .ToArray();

        if (exactUnmatchedInWindow.Length > 1)
            return CreateDiagnosis(
                RechargeMatchDiagnosisCode.MultipleExactSms,
                exactSmsCount: exactUnmatchedInWindow.Length,
                candidate: CreateCandidate(request, exactUnmatchedInWindow[0]));

        if (exactUnmatchedInWindow.Length == 1)
            return DiagnoseExactSms(request, pendingRequests, exactUnmatchedInWindow[0]);

        return DiagnoseWithoutExactSms(request, evidence);
    }

    private static AdminRechargeMatchDiagnosisDto DiagnoseExactSms(
        AdminRechargeRequestDto request,
        IReadOnlyList<AdminRechargeRequestDto> pendingRequests,
        RechargeMatchEvidence sms)
    {
        var competingRequests = pendingRequests.Count(candidate =>
            candidate.Status == RechargeRequestStatus.Pending
            && !string.IsNullOrWhiteSpace(candidate.ScreenshotUrl)
            && candidate.Amount == sms.Amount
            && candidate.SenderPhoneNumber == sms.SenderPhoneNumber
            && IsWithinRequestWindow(candidate, sms.ReceivedAt));
        var code = competingRequests > 1
            ? RechargeMatchDiagnosisCode.CompetingPendingRequests
            : RechargeMatchDiagnosisCode.EligibleWaiting;
        return CreateDiagnosis(
            code,
            exactSmsCount: 1,
            competingRequestCount: competingRequests,
            candidate: CreateCandidate(request, sms));
    }

    private static AdminRechargeMatchDiagnosisDto DiagnoseWithoutExactSms(
        AdminRechargeRequestDto request,
        IReadOnlyList<RechargeMatchEvidence> evidence)
    {
        var claimed = FindClaimedSms(request, evidence);
        if (claimed is not null)
            return CreateDiagnosis(RechargeMatchDiagnosisCode.SmsClaimedByAnotherRequest, candidate: CreateCandidate(request, claimed));

        var outsideWindow = FindExactSmsOutsideWindow(request, evidence);
        if (outsideWindow is not null)
            return CreateDiagnosis(RechargeMatchDiagnosisCode.OutsideWindow, candidate: CreateCandidate(request, outsideWindow));

        var amountMismatch = FindAmountMismatch(request, evidence);
        if (amountMismatch is not null)
            return CreateDiagnosis(RechargeMatchDiagnosisCode.AmountMismatch, candidate: CreateCandidate(request, amountMismatch));

        var phoneMismatch = FindPhoneMismatch(request, evidence);
        return phoneMismatch is null
            ? CreateDiagnosis(RechargeMatchDiagnosisCode.NoCandidate)
            : CreateDiagnosis(RechargeMatchDiagnosisCode.PhoneMismatch, candidate: CreateCandidate(request, phoneMismatch));
    }

    private static RechargeMatchEvidence? FindClaimedSms(
        AdminRechargeRequestDto request,
        IEnumerable<RechargeMatchEvidence> evidence) =>
        evidence
            .Where(sms => sms.IsMatched
                && sms.MatchedRechargeRequestId != request.Id
                && sms.Amount == request.Amount
                && sms.SenderPhoneNumber == request.SenderPhoneNumber
                && IsWithinRequestWindow(request, sms.ReceivedAt))
            .OrderBy(sms => DistanceFromAnchor(request, sms))
            .FirstOrDefault();

    private static RechargeMatchEvidence? FindExactSmsOutsideWindow(
        AdminRechargeRequestDto request,
        IEnumerable<RechargeMatchEvidence> evidence) =>
        evidence
            .Where(sms => !sms.IsMatched
                && sms.Amount == request.Amount
                && sms.SenderPhoneNumber == request.SenderPhoneNumber
                && !IsWithinRequestWindow(request, sms.ReceivedAt))
            .OrderBy(sms => DistanceFromAnchor(request, sms))
            .FirstOrDefault();

    private static RechargeMatchEvidence? FindAmountMismatch(
        AdminRechargeRequestDto request,
        IEnumerable<RechargeMatchEvidence> evidence) =>
        evidence
            .Where(sms => !sms.IsMatched
                && sms.SenderPhoneNumber == request.SenderPhoneNumber
                && sms.Amount != request.Amount
                && IsWithinRequestWindow(request, sms.ReceivedAt))
            .OrderBy(sms => DistanceFromAnchor(request, sms))
            .FirstOrDefault();

    private static RechargeMatchEvidence? FindPhoneMismatch(
        AdminRechargeRequestDto request,
        IEnumerable<RechargeMatchEvidence> evidence) =>
        evidence
            .Where(sms => !sms.IsMatched
                && sms.Amount == request.Amount
                && IsWithinRequestWindow(request, sms.ReceivedAt))
            .Select(sms => new
            {
                Sms = sms,
                Similarity = RechargePhoneSimilarity.Analyze(
                    request.SenderPhoneNumber,
                    sms.SenderPhoneNumber)
            })
            .Where(candidate => candidate.Similarity.RequiresConfirmation)
            .OrderByDescending(candidate => candidate.Similarity.HasSingleDigitMismatchPattern)
            .ThenByDescending(candidate => candidate.Similarity.AlignedMatchingDigits)
            .ThenByDescending(candidate => candidate.Similarity.LongestCommonDigitSequence)
            .ThenBy(candidate => DistanceFromAnchor(request, candidate.Sms))
            .Select(candidate => candidate.Sms)
            .FirstOrDefault();

    private static AdminRechargeMatchDiagnosisDto CreateDiagnosis(
        RechargeMatchDiagnosisCode code,
        int exactSmsCount = 0,
        int competingRequestCount = 0,
        AdminRechargeMatchCandidateDto? candidate = null) => new()
    {
        Code = code,
        ExactSmsCount = exactSmsCount,
        CompetingRequestCount = competingRequestCount,
        Candidate = candidate
    };

    private static AdminRechargeMatchCandidateDto CreateCandidate(
        AdminRechargeRequestDto request,
        RechargeMatchEvidence sms)
    {
        var totalOffsetMinutes = (sms.ReceivedAt - request.MatchingAnchorAt).TotalMinutes;
        var timeOffsetMinutes = totalOffsetMinutes >= 0
            ? (int)Math.Ceiling(totalOffsetMinutes)
            : (int)Math.Floor(totalOffsetMinutes);
        var phoneSimilarity = RechargePhoneSimilarity.Analyze(
            request.SenderPhoneNumber,
            sms.SenderPhoneNumber);

        return new AdminRechargeMatchCandidateDto
        {
            SmsLogId = sms.Id,
            WalletId = sms.WalletId,
            WalletLabel = sms.WalletLabel,
            Amount = sms.Amount,
            SenderPhoneNumber = sms.SenderPhoneNumber,
            ReceivedAt = sms.ReceivedAt,
            TimeOffsetMinutes = timeOffsetMinutes,
            OutsideWindowByMinutes = Math.Max(0, Math.Abs(timeOffsetMinutes) - RechargeMatchRules.WindowMinutes),
            MatchingDigits = phoneSimilarity.LongestCommonDigitSequence,
            HasSingleDigitMismatchPattern = phoneSimilarity.HasSingleDigitMismatchPattern,
            MatchingDigitsBeforeMismatch = phoneSimilarity.MatchingDigitsBeforeMismatch,
            MatchingDigitsAfterMismatch = phoneSimilarity.MatchingDigitsAfterMismatch,
            AmountMatches = sms.Amount == request.Amount,
            PhoneMatches = sms.SenderPhoneNumber == request.SenderPhoneNumber,
            WithinWindow = IsWithinRequestWindow(request, sms.ReceivedAt),
            SameWallet = sms.WalletId == request.WalletId,
            IsMatched = sms.IsMatched,
            MatchedRechargeRequestId = sms.MatchedRechargeRequestId
        };
    }

    private static bool IsWithinRequestWindow(AdminRechargeRequestDto request, DateTime timestamp) =>
        RechargeMatchRules.IsWithinWindow(timestamp, request.MatchingAnchorAt);

    private static double DistanceFromAnchor(AdminRechargeRequestDto request, RechargeMatchEvidence sms) =>
        Math.Abs((sms.ReceivedAt - request.MatchingAnchorAt).TotalMinutes);

    private sealed record RechargeMatchEvidence(
        Guid Id,
        Guid WalletId,
        string WalletLabel,
        DateTime ReceivedAt,
        decimal Amount,
        string SenderPhoneNumber,
        bool IsMatched,
        Guid? MatchedRechargeRequestId);

    private sealed record RechargeMatchEvidenceRow
    {
        public Guid Id { get; init; }
        public Guid WalletId { get; init; }
        public string WalletLabel { get; init; } = string.Empty;
        public string Body { get; init; } = string.Empty;
        public DateTime ReceivedAt { get; init; }
        public decimal Amount { get; init; }
        public string SenderPhoneNumber { get; init; } = string.Empty;
        public bool IsMatched { get; init; }
        public Guid? MatchedRechargeRequestId { get; init; }

        public RechargeMatchEvidence ToEvidence() => new(
            Id,
            WalletId,
            WalletLabel,
            ReceivedAt,
            Amount,
            SenderPhoneNumber,
            IsMatched,
            MatchedRechargeRequestId);
    }
}
