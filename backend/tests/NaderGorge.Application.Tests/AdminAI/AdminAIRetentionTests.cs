using NaderGorge.Domain.Entities.AdminAI;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Services.AdminAI;

namespace NaderGorge.Application.Tests.AdminAI;

public sealed class AdminAIRetentionTests
{
    [Fact]
    public async Task Recovery_PurgesExpiredProtectedReadAndSecureBytesButKeepsSafeEvidence()
    {
        await using var db = AdminAIStrongConfirmationTests.CreateDb();
        var read = new AdminAIReadInvocation { InvocationSequence = 1, CapabilityKey = "safe.read", CapabilityVersion = "1", InputHash = new string('a', 64), ProtectedResult = [1, 2, 3], ProtectedResultHash = new string('b', 64), ProtectedResultExpiresAt = DateTime.UtcNow.AddSeconds(-1), SafeEvidenceJson = "{\"count\":1}", TraceId = "trace" };
        var grant = new AdminAISecureInputGrant { InputKind = "Password", TokenDigest = new string('c', 64), ProtectedPayload = [4, 5, 6], PayloadHash = new string('d', 64), Status = AdminAISecureInputGrantStatus.Submitted, ExpiresAt = DateTime.UtcNow.AddSeconds(-1) };
        db.AddRange(read, grant); await db.SaveChangesAsync();
        Assert.Equal(2, await new AdminAIRecoveryService(db).ReconcileAsync(10, default));
        Assert.Null(read.ProtectedResult); Assert.Null(read.ProtectedResultHash); Assert.Equal("{\"count\":1}", read.SafeEvidenceJson);
        Assert.Null(grant.ProtectedPayload); Assert.Null(grant.PayloadHash); Assert.NotNull(grant.PurgedAt); Assert.Equal(AdminAISecureInputGrantStatus.Expired, grant.Status);
    }

    [Fact]
    public async Task UnexpiredProtectedRead_IsRetained()
    {
        await using var db = AdminAIStrongConfirmationTests.CreateDb(); var read = new AdminAIReadInvocation { InvocationSequence = 1, CapabilityKey = "safe.read", CapabilityVersion = "1", InputHash = new string('a', 64), ProtectedResult = [1], ProtectedResultHash = new string('b', 64), ProtectedResultExpiresAt = DateTime.UtcNow.AddHours(1), TraceId = "trace" }; db.Add(read); await db.SaveChangesAsync();
        Assert.Equal(0, await new AdminAIRecoveryService(db).ReconcileAsync(10, default)); Assert.NotNull(read.ProtectedResult);
    }
}
