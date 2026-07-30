using FluentValidation;
using NaderGorge.Application.Features.LiveSupportAI.Dtos;

namespace NaderGorge.Application.Features.LiveSupportAI.Validation;

public static class LiveSupportAICompletionErrorCodes
{
    public const string InvalidDecision = "DECISION_SCHEMA_INVALID";
    public const string ConfirmationExpired = "CONFIRMATION_EXPIRED";
    public const string IdempotencyConflict = "IDEMPOTENCY_PAYLOAD_CONFLICT";
}

public sealed class LiveSupportAIWorkerCompletionValidator : AbstractValidator<LiveSupportAIWorkerCompletionDto>
{
    private static readonly string[] DecisionTypes =
    [
        "reply", "propose_action", "request_verification",
        "propose_account_creation", "request_resolution", "handoff"
    ];

    public LiveSupportAIWorkerCompletionValidator()
    {
        RuleFor(x => x.SchemaVersion).Equal("1");
        RuleFor(x => x.ExpectedConversationVersion).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ExpectedPolicyVersionId).NotEmpty();
        RuleFor(x => x.DecisionHash).Matches("^[a-f0-9]{64}$");
        RuleFor(x => x.CallbackIdempotencyKey).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Provider).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Model).NotEmpty().MaximumLength(150);
        RuleFor(x => x.LatencyMs).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Decision.SchemaVersion).Equal("1");
        RuleFor(x => x.Decision.Type).Must(DecisionTypes.Contains);
        RuleFor(x => x.Decision.MessageAr).MaximumLength(LiveSupportAIContractLimits.MaxMessageLength);
    }
}

public sealed class LiveSupportAIVerificationLookupValidator : AbstractValidator<LiveSupportAIVerificationLookupCommandDto>
{
    public LiveSupportAIVerificationLookupValidator()
    {
        RuleFor(x => x.LookupKey).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Value).NotEmpty().MaximumLength(200);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(100);
    }
}

public sealed class LiveSupportAIConfirmationValidator : AbstractValidator<LiveSupportAIConfirmationCommandDto>
{
    public LiveSupportAIConfirmationValidator()
    {
        RuleFor(x => x.DecisionId).NotEmpty();
        RuleFor(x => x.IdempotencyKey).NotEmpty().Length(16, 100);
    }
}

public sealed class LiveSupportAIPreviewValidator : AbstractValidator<LiveSupportAIPreviewRequestDto>
{
    public LiveSupportAIPreviewValidator()
    {
        RuleFor(x => x.PolicyVersionId).NotEqual(Guid.Empty).When(x => x.PolicyVersionId.HasValue);
        RuleFor(x => x.Message).NotEmpty().MaximumLength(LiveSupportAIContractLimits.MaxMessageLength);
    }
}

public sealed class LiveSupportAIVerificationAnswerValidator : AbstractValidator<LiveSupportAIVerificationAnswerCommandDto>
{
    public LiveSupportAIVerificationAnswerValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.Answer).NotEmpty().MaximumLength(300);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(100);
    }
}

public sealed class LiveSupportAISecureRegistrationValidator : AbstractValidator<LiveSupportAISecureRegistrationDto>
{
    public LiveSupportAISecureRegistrationValidator()
    {
        RuleFor(x => x.DecisionId).NotEmpty();
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(100);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200).Must(value => value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 4);
        RuleFor(x => x.PhoneNumber).NotEmpty().Matches("^01[0125]\\d{8}$");
        RuleFor(x => x.Password).NotEmpty().Length(8, 128);
        RuleFor(x => x.DateOfBirth).LessThan(DateTime.UtcNow.Date);
        RuleFor(x => x.Gender).Must(value => value is "Male" or "Female");
        RuleFor(x => x.Governorate).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Address).NotEmpty().MaximumLength(300);
        RuleFor(x => x.EducationStage).NotEmpty().MaximumLength(100);
        RuleFor(x => x.GradeLevel).NotEmpty().MaximumLength(100);
        RuleFor(x => x.SchoolName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ParentPhoneNumber).NotEmpty().Matches("^01[0125]\\d{8}$").NotEqual(x => x.PhoneNumber);
    }
}
