namespace NaderGorge.Application.Common;

internal static class AiGenerationRunContract
{
    public static bool IsCurrent(Guid? currentRunId, Guid? callbackRunId, bool isActive) =>
        currentRunId.HasValue
            ? currentRunId == callbackRunId
            : !callbackRunId.HasValue && isActive;
}
