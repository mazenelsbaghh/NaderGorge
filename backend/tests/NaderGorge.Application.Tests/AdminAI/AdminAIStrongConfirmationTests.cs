using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NaderGorge.Domain.Entities.AdminAI;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Services.AdminAI;

namespace NaderGorge.Application.Tests.AdminAI;

public sealed class AdminAIStrongConfirmationTests
{
    [Fact]
    public async Task Phrase_IsRandomExactAndBoundToOneProposal()
    {
        await using var db = CreateDb(); var actor = Guid.NewGuid(); var first = Proposal(actor); var second = Proposal(actor); db.AddRange(first, second); await db.SaveChangesAsync();
        var service = new AdminAIConfirmationChallengeService(db, Protector());
        var normalization = Protector(); Assert.NotEqual(normalization.NormalizeConfirmationPhrase("123"), normalization.NormalizeConfirmationPhrase("١٢٣"));
        var phrase1 = await service.IssueAsync(actor, first.Id, "تعطيل الحساب", default);
        var phrase2 = await service.IssueAsync(actor, second.Id, "تعطيل الحساب", default);
        Assert.StartsWith("أؤكد تنفيذ danger — ", phrase1); Assert.NotEqual(phrase1, phrase2);
        Assert.Equal(phrase1, await service.PhraseAsync(actor, first.Id, default));
        Assert.DoesNotContain(phrase1, db.AdminAIConfirmationChallenges.Single(x => x.ProposalId == first.Id).PhraseDigest, StringComparison.Ordinal);
        Assert.False(await service.VerifyAsync(actor, first.Id, phrase1.ToLowerInvariant(), default));
        Assert.False(await service.VerifyAsync(actor, first.Id, phrase1.Replace("—", "-", StringComparison.Ordinal), default));
        Assert.False(await service.VerifyAsync(actor, first.Id, phrase1[..^1] + (phrase1[^1] == 'A' ? 'B' : 'A'), default));
        Assert.True(await service.VerifyAsync(actor, first.Id, "  " + phrase1.Replace(" ", "   ") + "  ", default));
        Assert.False(await service.VerifyAsync(actor, second.Id, phrase1, default));
    }

    [Fact]
    public async Task FiveWrongAttempts_LockAndInvalidateProposal()
    {
        await using var db = CreateDb(); var actor = Guid.NewGuid(); var proposal = Proposal(actor); db.Add(proposal); await db.SaveChangesAsync();
        var service = new AdminAIConfirmationChallengeService(db, Protector()); await service.IssueAsync(actor, proposal.Id, "حذف", default);
        for (var attempt = 0; attempt < 5; attempt++) Assert.False(await service.VerifyAsync(actor, proposal.Id, $"wrong-{attempt}", default));
        Assert.Equal(AdminAIChallengeStatus.Locked, db.AdminAIConfirmationChallenges.Single().Status);
        Assert.Equal(AdminAIProposalStatus.Invalidated, proposal.Status);
    }

    [Fact]
    public async Task ExpiredProposal_ExpiresChallengeWithZeroAcceptance()
    {
        await using var db = CreateDb(); var actor = Guid.NewGuid(); var proposal = Proposal(actor); db.Add(proposal); await db.SaveChangesAsync();
        var service = new AdminAIConfirmationChallengeService(db, Protector()); var phrase = await service.IssueAsync(actor, proposal.Id, "حذف", default);
        proposal.ExpiresAt = DateTime.UtcNow.AddSeconds(-1); db.AdminAIConfirmationChallenges.Single().ExpiresAt = proposal.ExpiresAt; await db.SaveChangesAsync();
        Assert.False(await service.VerifyAsync(actor, proposal.Id, phrase, default));
        Assert.Equal(AdminAIProposalStatus.Expired, proposal.Status); Assert.Equal(AdminAIChallengeStatus.Expired, db.AdminAIConfirmationChallenges.Single().Status);
    }

    internal static AppDbContext CreateDb() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"admin-ai-confirm-{Guid.NewGuid()}").Options);
    internal static AdminAIDataProtector Protector() => new(new EphemeralDataProtectionProvider(), new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["AdminAI:HmacKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) }).Build());
    internal static AdminAIActionProposal Proposal(Guid actor) => new() { ActorAdminUserId = actor, ConfirmationType = AdminAIConfirmationType.TypedStrong, Status = AdminAIProposalStatus.PendingConfirmation, ExpiresAt = DateTime.UtcNow.AddMinutes(5), CapabilityKey = "danger", CapabilityVersion = "1", PayloadHash = new string('a', 64), StateFingerprint = new string('b', 64) };
}
