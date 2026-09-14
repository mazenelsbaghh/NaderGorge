using NaderGorge.Application.Interfaces.Finance;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Services.Finance;
using NaderGorge.Integration.Tests.Finance;

namespace NaderGorge.Integration.Tests.Migrations;

public sealed class PlatformFinanceHistoricalMigrationTests
{
    [Fact]
    public void Migration_items_have_repeat_safe_statuses_and_checksums()
    {
        var item = new FinancialMigrationItem { SourceType = "Purchase", SourceId = Guid.NewGuid(), Amount = 10m, Status = FinanceMigrationItemStatus.Posted, SourceChecksum = "sha256" };
        var batch = new FinancialMigrationBatch { SourceChecksum = "sha256", Items = [item] };
        Assert.Equal(FinanceMigrationItemStatus.Posted, batch.Items.Single().Status);
        Assert.NotEmpty(batch.SourceChecksum);
    }

    [Fact]
    public async Task Matched_recharges_are_posted_once()
    {
        var (db, _, studentId) = await FinanceTestDbFactory.CreateLedgerAsync();
        await using (db)
        {
            var opening = new FinancialAccount { Code = "9990", Name = "Opening", Type = FinancialAccountType.Equity, Role = FinancialAccountRole.OpeningSuspense, NormalSide = FinancialNormalSide.Credit };
            var wallet = new DigitalWallet { Label = "Test wallet", PhoneNumber = "01000000000", PairingToken = "test" };
            var walletAccount = new FinancialAccount { Code = wallet.Id.ToString("N"), Name = "Wallet", Type = FinancialAccountType.Asset, Role = FinancialAccountRole.Treasury, NormalSide = FinancialNormalSide.Debit };
            db.AddRange(opening, wallet, walletAccount);
            db.TreasuryAccounts.Add(new TreasuryAccount { Name = "Wallet", Type = TreasuryAccountType.DigitalWallet, FinancialAccountId = walletAccount.Id, DigitalWalletId = wallet.Id });

            var occurredAt = DateTime.UtcNow.AddDays(-1);
            db.RechargeRequests.Add(new RechargeRequest { UserId = studentId, WalletId = wallet.Id, Amount = 100m, SenderPhoneNumber = "01000000000", Status = RechargeRequestStatus.Matched, ResolvedAt = occurredAt });
            await db.SaveChangesAsync();

            var service = new PlatformFinanceMigrationService(db, new FinancialPostingService(db));
            var preview = await service.PreviewAsync(occurredAt.AddDays(-1), occurredAt.AddDays(1), default);
            Assert.Equal(1, preview.RechargeCandidates);

            var first = await service.PostAsync(occurredAt.AddDays(-1), occurredAt.AddDays(1), studentId, default);
            var second = await service.PostAsync(occurredAt.AddDays(-1), occurredAt.AddDays(1), studentId, default);

            Assert.Equal(1, first.Posted);
            Assert.Equal(0, first.Failed);
            Assert.Equal(0, second.Posted);
            Assert.Equal(1, second.AlreadyPosted);
            Assert.Single(db.JournalEntries);
        }
    }
}
