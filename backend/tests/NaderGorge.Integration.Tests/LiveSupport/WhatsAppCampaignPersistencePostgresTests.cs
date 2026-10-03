using System.Data.Common;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Services;
using Npgsql;

namespace NaderGorge.Integration.Tests.LiveSupport;

public sealed class WhatsAppCampaignPersistencePostgresTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public async Task Incident20261003_AcceptedMessageWithCommitConflicts_IsSavedWithoutResending(int conflicts)
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var configuration = Configuration();
        var protector = new WhatsAppCampaignDataProtector(new EphemeralDataProtectionProvider(), configuration);
        var recipient = await SeedCampaignAsync(fixture.Db, protector);
        var fault = new AcceptedMessageCommitConflict(conflicts);
        using var http = new AcceptedMessageHandler(() => fault.Accepted = true);
        using var client = new HttpClient(http);
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(fixture.ConnectionString).AddInterceptors(fault));
        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());
        services.AddSingleton<IWhatsAppCampaignDataProtector>(protector);
        services.AddScoped(provider => new WhatsAppCampaignService(
            provider.GetRequiredService<IAppDbContext>(), protector, configuration));
        services.AddScoped(_ => new WhatsAppCloudService(client, configuration, NullLogger<WhatsAppCloudService>.Instance));
        await using var provider = services.BuildServiceProvider();
        var dispatcher = new WhatsAppCampaignDispatcher(provider.GetRequiredService<IServiceScopeFactory>(),
            configuration, NullLogger<WhatsAppCampaignDispatcher>.Instance);

        await dispatcher.DispatchBatchAsync(CancellationToken.None);

        fixture.Db.ChangeTracker.Clear();
        var saved = await fixture.Db.WhatsAppCampaignRecipients.SingleAsync(item => item.Id == recipient.Id);
        Assert.Equal(1, http.SendCount);
        Assert.Equal(conflicts, fault.InjectedConflicts);
        Assert.Equal(http.MetaMessageId, saved.MetaMessageId);
        Assert.Equal(WhatsAppCampaignRecipientStatus.Sent, saved.Status);
        Assert.Equal(1, saved.AttemptCount);
        Assert.Null(saved.FailureCode);
        var campaign = await fixture.Db.WhatsAppCampaigns.SingleAsync(item => item.Id == recipient.CampaignId);
        Assert.Equal(1, campaign.SentCount);
        Assert.Equal(0, campaign.UncertainCount);
    }

    [Fact]
    public async Task Incident20261003_ConcurrentCampaignReceipts_PreserveFinalStatusAndCounters()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var configuration = Configuration();
        var protector = new WhatsAppCampaignDataProtector(new EphemeralDataProtectionProvider(), configuration);
        var recipient = await SeedCampaignAsync(fixture.Db, protector);
        recipient.Status = WhatsAppCampaignRecipientStatus.Sent;
        recipient.MetaMessageId = "wamid.concurrent-receipts";
        await fixture.Db.SaveChangesAsync();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var statuses = Enumerable.Range(0, 24).Select(index => new[] { "sent", "delivered", "read" }[index % 3]);
        var receipts = statuses.Select(async status =>
        {
            await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(fixture.ConnectionString).Options);
            var campaigns = new WhatsAppCampaignService(db, protector, configuration);
            await start.Task;
            Assert.True(await campaigns.ProcessReceiptAsync(recipient.MetaMessageId, status,
                DateTime.UtcNow, null, CancellationToken.None));
        }).ToArray();
        start.SetResult();

        await Task.WhenAll(receipts);

        fixture.Db.ChangeTracker.Clear();
        var saved = await fixture.Db.WhatsAppCampaignRecipients.SingleAsync(item => item.Id == recipient.Id);
        var campaign = await fixture.Db.WhatsAppCampaigns.SingleAsync(item => item.Id == recipient.CampaignId);
        Assert.Equal(WhatsAppCampaignRecipientStatus.Read, saved.Status);
        Assert.NotNull(saved.DeliveredAt);
        Assert.NotNull(saved.ReadAt);
        Assert.Equal(1, campaign.ReadCount);
        Assert.Equal(0, campaign.PendingCount);
        Assert.Equal(WhatsAppCampaignStatus.Completed, campaign.Status);
    }

    private static IConfiguration Configuration() => new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?>
        {
            ["WhatsAppCampaigns:HmacKey"] = Convert.ToBase64String(new byte[32]),
            ["WhatsAppCloudApi:AccessToken"] = "test-only-token",
            ["WhatsAppCloudApi:PhoneNumberId"] = "test-phone-id"
        }).Build();

    private static async Task<WhatsAppCampaignRecipient> SeedCampaignAsync(
        AppDbContext db, IWhatsAppCampaignDataProtector protector)
    {
        var owner = new User { FullName = "Persistence regression", PasswordHash = "unused",
            PhoneNumber = $"01{Random.Shared.NextInt64(100000000, 999999999)}" };
        var template = new LiveSupportWhatsAppTemplate
        {
            MetaTemplateId = Guid.NewGuid().ToString("N"), Name = $"regression_{Guid.NewGuid():N}",
            Language = "ar", Category = "UTILITY", Status = "APPROVED",
            ComponentsJson = """[{"type":"BODY","text":"Test message"}]""", Fingerprint = new string('a', 64)
        };
        var campaign = new WhatsAppCampaign
        {
            Name = "Persistence regression", TemplateId = template.Id, TemplateName = template.Name,
            TemplateLanguage = template.Language, TemplateCategory = template.Category,
            TemplateFingerprint = template.Fingerprint, CreatedByUserId = owner.Id,
            RecipientCount = 1, PendingCount = 1, Status = WhatsAppCampaignStatus.Running, Version = 1,
            CreateIdempotencyKey = Guid.NewGuid().ToString("N")
        };
        var recipient = new WhatsAppCampaignRecipient
        {
            CampaignId = campaign.Id, DestinationHash = protector.DestinationHash("201012345678"), Version = 1
        };
        var payload = WhatsAppCampaignService.SerializeFrozenRecipientPayload(new("201012345678", []));
        recipient.ProtectedPayload = protector.Protect(recipient.Id, Encoding.UTF8.GetBytes(payload));
        recipient.PayloadDigest = protector.Digest(recipient.Id, recipient.ProtectedPayload);
        db.AddRange(owner, template, campaign, recipient);
        await db.SaveChangesAsync();
        return recipient;
    }

    private sealed class AcceptedMessageCommitConflict(int conflicts) : DbTransactionInterceptor
    {
        public bool Accepted { get; set; }
        public int InjectedConflicts { get; private set; }

        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
            TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            if (Accepted && InjectedConflicts < conflicts)
            {
                InjectedConflicts++;
                throw new PostgresException("Regression: commit conflict after provider acceptance", "ERROR", "ERROR", "40001");
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class AcceptedMessageHandler(Action accepted) : HttpMessageHandler
    {
        public int SendCount { get; private set; }
        public string MetaMessageId { get; } = $"wamid.accepted-{Guid.NewGuid():N}";

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            SendCount++;
            accepted();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($$$"""{"messages":[{"id":"{{{MetaMessageId}}}"}]}""", Encoding.UTF8, "application/json")
            });
        }
    }
}
