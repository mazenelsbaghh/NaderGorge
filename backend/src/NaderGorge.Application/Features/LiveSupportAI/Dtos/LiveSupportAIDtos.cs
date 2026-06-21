namespace NaderGorge.Application.Features.LiveSupportAI.Dtos;

public sealed record LiveSupportAICatalogItemDto(string Key, string Label, string Description, bool RequiresVerification = false);
public sealed record LiveSupportAICatalogsDto(
    IReadOnlyList<LiveSupportAICatalogItemDto> ReadableData,
    IReadOnlyList<LiveSupportAICatalogItemDto> Actions,
    IReadOnlyList<LiveSupportAICatalogItemDto> LookupKeys,
    IReadOnlyList<LiveSupportAICatalogItemDto> VerificationQuestions);

public sealed record LiveSupportAIPolicyDto(
    Guid Id,
    long VersionNumber,
    string Status,
    bool IsEnabled,
    string SystemInstructions,
    IReadOnlyList<string> ReadableDataKeys,
    IReadOnlyList<string> ActionKeys,
    IReadOnlyList<string> LookupKeys,
    IReadOnlyList<string> VerificationQuestionKeys,
    int VerificationRequiredCorrect,
    int VerificationMaxAttempts,
    int PendingActionExpirySeconds,
    int InactivityMinutes,
    int InactivityWarningGraceSeconds,
    long Version,
    DateTime? PublishedAt);

public sealed record LiveSupportAIConfigDto(LiveSupportAIPolicyDto? Draft, LiveSupportAIPolicyDto? Published, LiveSupportAICatalogsDto Catalogs);

public sealed record SaveLiveSupportAIDraftRequest(
    string SystemInstructions,
    IReadOnlyList<string> ReadableDataKeys,
    IReadOnlyList<string> ActionKeys,
    IReadOnlyList<string> LookupKeys,
    IReadOnlyList<string> VerificationQuestionKeys,
    int VerificationRequiredCorrect,
    int VerificationMaxAttempts,
    int PendingActionExpirySeconds,
    int InactivityMinutes,
    int InactivityWarningGraceSeconds,
    long? ExpectedVersion);
