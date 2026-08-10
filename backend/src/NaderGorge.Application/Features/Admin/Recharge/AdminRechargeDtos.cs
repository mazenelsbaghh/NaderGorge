using System;
using System.Text.Json.Serialization;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Features.Admin.Recharge;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RechargeMatchDiagnosisCode
{
    AwaitingEvidence,
    MissingTeacherScope,
    EligibleWaiting,
    MultipleExactSms,
    CompetingPendingRequests,
    SmsClaimedByAnotherRequest,
    OutsideWindow,
    AmountMismatch,
    PhoneMismatch,
    NoCandidate
}

public record AdminRechargeRequestDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string StudentPhoneNumber { get; set; } = string.Empty;
    public decimal StudentBalance { get; set; }
    public decimal TeacherBalance { get; set; }
    public bool HasPreviousRequest { get; set; }
    public RechargeRequestStatus? PreviousRequestStatus { get; set; }
    public DateTime? PreviousRequestCreatedAt { get; set; }
    public Guid WalletId { get; set; }
    public string WalletLabel { get; set; } = string.Empty;
    public string WalletPhoneNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public Guid? TeacherId { get; set; }
    public string? TeacherName { get; set; }
    public string SenderPhoneNumber { get; set; } = string.Empty;
    public string? OriginalSenderPhoneNumber { get; set; }
    public bool RequiresSenderPhoneConfirmation { get; set; }
    public string? ScreenshotUrl { get; set; }
    public RechargeRequestStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    [JsonIgnore]
    public DateTime MatchingAnchorAt { get; set; }
    public AdminRechargeMatchDiagnosisDto? MatchDiagnosis { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public Guid? ResolvedByUserId { get; set; }
    public string? ResolvedByUserName { get; set; }
    public string? RejectionReason { get; set; }
    public Guid? MatchedSmsLogId { get; set; }
    public DateTime? ReservationExpiresAt { get; set; }
}

public record AdminRechargeMatchDiagnosisDto
{
    public RechargeMatchDiagnosisCode Code { get; set; } = RechargeMatchDiagnosisCode.NoCandidate;
    public int ExactSmsCount { get; set; }
    public int CompetingRequestCount { get; set; }
    public AdminRechargeMatchCandidateDto? Candidate { get; set; }
}

public record AdminRechargeMatchCandidateDto
{
    public Guid SmsLogId { get; set; }
    public Guid WalletId { get; set; }
    public string WalletLabel { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public string SenderPhoneNumber { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; }
    public int TimeOffsetMinutes { get; set; }
    public int OutsideWindowByMinutes { get; set; }
    public int MatchingDigits { get; set; }
    public bool HasSingleDigitMismatchPattern { get; set; }
    public int MatchingDigitsBeforeMismatch { get; set; }
    public int MatchingDigitsAfterMismatch { get; set; }
    public bool AmountMatches { get; set; }
    public bool PhoneMatches { get; set; }
    public bool WithinWindow { get; set; }
    public bool SameWallet { get; set; }
    public bool IsMatched { get; set; }
    public Guid? MatchedRechargeRequestId { get; set; }
}

public record AdminIncomingSmsLogDto
{
    public Guid Id { get; set; }
    public Guid WalletId { get; set; }
    public string WalletLabel { get; set; } = string.Empty;
    public string WalletPhoneNumber { get; set; } = string.Empty;
    public string Sender { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; }
    public decimal? ParsedAmount { get; set; }
    public string? ParsedSenderPhone { get; set; }
    public string? TransferReference { get; set; }
    public bool IsMatched { get; set; }
    public Guid? MatchedRechargeRequestId { get; set; }
    public string? MatchedStudentName { get; set; }
    public string? MatchedStudentPhoneNumber { get; set; }
    public string DeduplicationHash { get; set; } = string.Empty;
}

public record AdminIncomingSmsLogPageDto(IReadOnlyList<AdminIncomingSmsLogDto> Items, int TotalCount, int Page, int PageSize);
