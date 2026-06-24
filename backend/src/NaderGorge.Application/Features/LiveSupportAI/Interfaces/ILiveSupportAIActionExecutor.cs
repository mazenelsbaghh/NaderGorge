namespace NaderGorge.Application.Features.LiveSupportAI.Interfaces;

public interface ILiveSupportAIActionExecutor
{
    Task<Guid> ExecuteAsync(
        Guid conversationId,
        Guid studentUserId,
        Guid pendingDecisionId,
        string actionKey,
        IReadOnlyDictionary<string, object?> payload,
        string idempotencyKey,
        CancellationToken cancellationToken);
}

public interface ILiveSupportAIDataProtector
{
    byte[] Protect(ReadOnlySpan<byte> plaintext);
    byte[] Unprotect(ReadOnlySpan<byte> protectedPayload);
    string ComputeKeyedDigest(string purpose, ReadOnlySpan<byte> value);
}
