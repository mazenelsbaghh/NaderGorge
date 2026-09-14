using Microsoft.Extensions.Configuration;
using NaderGorge.Infrastructure.Services.AdminAI;
using NaderGorge.Domain.Enums;
using NaderGorge.Application.Features.AdminAI.Interfaces;

namespace NaderGorge.Application.Tests.AdminAI;

public sealed class AdminAISecureInputTests
{
    [Fact]
    public async Task Grant_IsActorBoundEncryptedOneTimeAndPurged()
    {
        await using var db = AdminAIStrongConfirmationTests.CreateDb(); var actor = Guid.NewGuid(); var proposal = AdminAIStrongConfirmationTests.Proposal(actor); proposal.Status = AdminAIProposalStatus.PendingSecureInput; db.Add(proposal); await db.SaveChangesAsync();
        var protector = AdminAIStrongConfirmationTests.Protector();
        var service = new AdminAISecureInputService(db, new AdminAIConversationTests.AllowAccess(actor), protector, new ConfigurationBuilder().AddInMemoryCollection().Build());
        var issued = await service.IssueAsync(actor, proposal.Id, "Password", proposal.Version, default); Assert.NotNull(issued.Token);
        Assert.Equal(issued.Token, (await service.IssueAsync(actor, proposal.Id, "Password", proposal.Version, default)).Token);
        Assert.DoesNotContain(issued.Token!, db.AdminAISecureInputGrants.Single().TokenDigest, StringComparison.Ordinal);
        var submitted = await service.SubmitAsync(actor, issued.Id, issued.Token!, "Password", "P0-PRIVATE"u8.ToArray(), default); Assert.Null(submitted.Token);
        Assert.Equal(submitted, await service.SubmitAsync(actor, issued.Id, issued.Token!, "Password", "P0-PRIVATE"u8.ToArray(), default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SubmitAsync(actor, issued.Id, issued.Token!, "Password", "different"u8.ToArray(), default));
        Assert.NotEqual("P0-PRIVATE"u8.ToArray(), db.AdminAISecureInputGrants.Single().ProtectedPayload!);
        var protectedValue = await service.ConsumeAsync(actor, proposal.Id, default);
        Assert.Equal("P0-PRIVATE", System.Text.Encoding.UTF8.GetString(protector.Unprotect("secure-input:Password", protectedValue)));
        Assert.Null(db.AdminAISecureInputGrants.Single().ProtectedPayload); Assert.NotNull(db.AdminAISecureInputGrants.Single().PurgedAt);
        Assert.DoesNotContain("P0-PRIVATE", db.AdminAISecureInputGrants.Single().SafeMetadataJson, StringComparison.Ordinal);
        await Assert.ThrowsAsync<AdminAISecureInputGoneException>(() => service.ConsumeAsync(actor, proposal.Id, default));
    }

    [Fact]
    public async Task WrongActorTokenTypeAndOversize_FailClosed()
    {
        await using var db = AdminAIStrongConfirmationTests.CreateDb(); var actor = Guid.NewGuid(); var proposal = AdminAIStrongConfirmationTests.Proposal(actor); proposal.Status = AdminAIProposalStatus.PendingSecureInput; db.Add(proposal); await db.SaveChangesAsync();
        var service = new AdminAISecureInputService(db, new AdminAIConversationTests.AllowAccess(actor), AdminAIStrongConfirmationTests.Protector(), new ConfigurationBuilder().Build());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.IssueAsync(Guid.NewGuid(), proposal.Id, "ProtectedToken", proposal.Version, default));
        await Assert.ThrowsAsync<ArgumentException>(() => service.IssueAsync(actor, proposal.Id, "RawToken", proposal.Version, default));
        var grant = await service.IssueAsync(actor, proposal.Id, "ProtectedToken", proposal.Version, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SubmitAsync(actor, grant.Id, "wrong", "ProtectedToken", new byte[1], default));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.SubmitAsync(actor, grant.Id, grant.Token!, "ProtectedToken", new byte[4097], default));
    }

    [Fact]
    public async Task ExpiredSubmittedGrant_IsPurgedAndNeverReturned()
    {
        await using var db = AdminAIStrongConfirmationTests.CreateDb(); var actor = Guid.NewGuid(); var proposal = AdminAIStrongConfirmationTests.Proposal(actor); proposal.Status = AdminAIProposalStatus.PendingSecureInput; db.Add(proposal); await db.SaveChangesAsync();
        var service = new AdminAISecureInputService(db, new AdminAIConversationTests.AllowAccess(actor), AdminAIStrongConfirmationTests.Protector(), new ConfigurationBuilder().Build());
        var grant = await service.IssueAsync(actor, proposal.Id, "VerificationAnswer", proposal.Version, default);
        await service.SubmitAsync(actor, grant.Id, grant.Token!, "VerificationAnswer", "P0-EXPIRED"u8.ToArray(), default);
        db.AdminAISecureInputGrants.Single().ExpiresAt = DateTime.UtcNow.AddSeconds(-1); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<AdminAISecureInputGoneException>(() => service.ConsumeAsync(actor, proposal.Id, default));
        var persisted = db.AdminAISecureInputGrants.Single(); Assert.Equal(AdminAISecureInputGrantStatus.Expired, persisted.Status); Assert.Null(persisted.ProtectedPayload); Assert.Null(persisted.PayloadHash);
    }

    [Fact]
    public async Task PrivateFile_AcceptsOnlyOpaquePrivateReference()
    {
        await using var db = AdminAIStrongConfirmationTests.CreateDb(); var actor = Guid.NewGuid(); var proposal = AdminAIStrongConfirmationTests.Proposal(actor); proposal.Status = AdminAIProposalStatus.PendingSecureInput; db.Add(proposal); await db.SaveChangesAsync();
        var service = new AdminAISecureInputService(db, new AdminAIConversationTests.AllowAccess(actor), AdminAIStrongConfirmationTests.Protector(), new ConfigurationBuilder().Build());
        var grant = await service.IssueAsync(actor, proposal.Id, "PrivateFile", proposal.Version, default);
        await Assert.ThrowsAsync<ArgumentException>(() => service.SubmitAsync(actor, grant.Id, grant.Token!, "PrivateFile", "https://public/file"u8.ToArray(), default));
        var submitted = await service.SubmitAsync(actor, grant.Id, grant.Token!, "PrivateFile", "private:object-123"u8.ToArray(), default);
        Assert.Equal(AdminAISecureInputGrantStatus.Submitted, submitted.Status);
    }
}
