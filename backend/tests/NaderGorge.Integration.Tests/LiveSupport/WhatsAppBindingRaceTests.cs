using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Application.Features.LiveSupport.Services;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Services;
using NaderGorge.Integration.Tests.AdminAI;
using Npgsql;

namespace NaderGorge.Integration.Tests.LiveSupport;

public sealed class WhatsAppBindingRaceTests
{
    // 2026-10-13: concurrent webhook binding insert returned 409 on ConversationId.
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task ConcurrentConversationBinding_ReusesOnlyMatchingSenderAndAccountAndPreservesTrackedWork(bool sameSender, bool sameAccount)
    {
        await using var fixture = await PostgresAdminAIFixture.CreateAsync();
        await using var seed = fixture.CreateDbContext();
        await seed.Database.MigrateAsync();
        var guest = new LiveSupportGuestSession
        {
            DisplayName = "Synthetic contact", PhoneNumber = "01099999999",
            UserAgentSummary = "WhatsApp Cloud API", SecurityStampHash = new string('a', 64),
            CreatedIpHash = new string('b', 64), ExpiresAt = DateTime.UtcNow.AddYears(1), LastSeenAt = DateTime.UtcNow
        };
        var conversation = new LiveSupportConversation
        {
            ParticipantType = LiveSupportParticipantType.Guest, GuestSessionId = guest.Id,
            Status = LiveSupportConversationStatus.Waiting, QueuedAt = DateTime.UtcNow,
            LastMessageAt = DateTime.UtcNow, Version = 1
        };
        var trackedUser = new User { FullName = "Before webhook", PhoneNumber = "01099999998", PasswordHash = "synthetic" };
        var otherAccount = new LiveSupportWhatsAppAccount
        {
            Name = "Synthetic alternate account", InstanceName = "synthetic-alternate", CreatedByUserId = trackedUser.Id, Version = 1
        };
        seed.AddRange(guest, conversation, trackedUser, otherAccount);
        await seed.SaveChangesAsync();
        var winningInboundAt = DateTime.UtcNow.AddMinutes(5);
        var race = new BindingRace(async () =>
        {
            await using var winner = fixture.CreateDbContext();
            winner.LiveSupportWhatsAppBindings.Add(new LiveSupportWhatsAppBinding
            {
                ConversationId = conversation.Id, GuestSessionId = guest.Id,
                AccountId = sameAccount ? null : otherAccount.Id,
                WhatsAppUserId = sameSender ? "201099999999" : "201088888888",
                PhoneNumber = guest.PhoneNumber, DisplayName = guest.DisplayName,
                LastInboundAt = winningInboundAt, CustomerServiceWindowExpiresAt = winningInboundAt.AddHours(24), Version = 1
            });
            await winner.SaveChangesAsync();
        });
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(fixture.ConnectionString).AddInterceptors(race).Options);
        var retainedUser = await db.Users.SingleAsync(user => user.Id == trackedUser.Id);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["WhatsAppCloudApi:BusinessAccountId"] = "synthetic-business",
            ["WhatsAppCloudApi:PhoneNumberId"] = "synthetic-phone",
            ["WhatsAppCampaigns:HmacKey"] = new string('t', 64)
        }).Build();
        using var http = new HttpClient(new NoNetwork());
        var service = new WhatsAppLiveSupportService(db, new LiveSupportService(db, new Settings()),
            new NoMedia(), new WhatsAppCloudService(http, configuration, NullLogger<WhatsAppCloudService>.Instance),
            new LiveSupportEventWriter(db), configuration,
            new WhatsAppCampaignService(db, new WhatsAppCampaignDataProtector(new EphemeralDataProtectionProvider(), configuration), configuration));
        var payload = JsonSerializer.SerializeToElement(new
        {
            @object = "whatsapp_business_account",
            entry = new[] { new { id = "synthetic-business", changes = new[] { new { value = new
            {
                metadata = new { phone_number_id = "synthetic-phone" },
                messages = new[] { new { id = "wamid.race", from = "201099999999",
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), type = "text",
                    text = new { body = "Synthetic race message" } } }
            } } } } }
        });

        var matchingIdentity = sameSender && sameAccount;
        if (matchingIdentity)
        {
            await service.ProcessWebhookAsync(payload, default);
            await service.ProcessWebhookAsync(payload, default);
            retainedUser.FullName = "After webhook";
            await db.SaveChangesAsync();
        }
        else
        {
            var rejected = await Assert.ThrowsAsync<DbUpdateException>(() => service.ProcessWebhookAsync(payload, default));
            Assert.Equal("IX_live_support_whatsapp_bindings_ConversationId", ((PostgresException)rejected.InnerException!).ConstraintName);
        }

        await using var read = fixture.CreateDbContext();
        var binding = await read.LiveSupportWhatsAppBindings.SingleAsync();
        Assert.Equal(sameSender ? "201099999999" : "201088888888", binding.WhatsAppUserId);
        Assert.Equal(sameAccount ? (Guid?)null : otherAccount.Id, binding.AccountId);
        Assert.Equal(winningInboundAt, binding.LastInboundAt);
        Assert.Equal(winningInboundAt.AddHours(24), binding.CustomerServiceWindowExpiresAt);
        Assert.Equal(matchingIdentity ? 1 : 0, await read.LiveSupportMessages.CountAsync());
        Assert.Equal(matchingIdentity ? 1 : 0, await read.LiveSupportWhatsAppMessages.CountAsync());
        Assert.Equal(matchingIdentity ? "After webhook" : "Before webhook", (await read.Users.SingleAsync(user => user.Id == trackedUser.Id)).FullName);
        Assert.Equal("23505", race.Conflict?.SqlState);
    }

    private sealed class BindingRace(Func<Task> commitWinner) : SaveChangesInterceptor
    {
        private bool _committed;
        public PostgresException? Conflict { get; private set; }
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!_committed && data.Context!.ChangeTracker.Entries<LiveSupportWhatsAppBinding>().Any(e => e.State == EntityState.Added))
            {
                _committed = true;
                await commitWinner();
            }
            return result;
        }
        public override Task SaveChangesFailedAsync(DbContextErrorEventData data, CancellationToken cancellationToken = default)
        {
            Conflict = (data.Exception as DbUpdateException)?.InnerException as PostgresException;
            return Task.CompletedTask;
        }
    }

    private sealed class Settings : ICachedPlatformSettingsReader
    {
        public Task<CachedPlatformSettings> GetAsync(CancellationToken ct) => Task.FromResult(CachedPlatformSettings.Default with { LiveSupportEnabled = true });
        public void Invalidate() { }
    }
    private sealed class NoNetwork : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => throw new InvalidOperationException("Text ingestion must not call an external provider.");
    }
    private sealed class NoMedia : ILiveSupportAttachmentStorage
    {
        public Task<LiveSupportStoredAttachment> SaveAsync(Stream content, string name, string type, long size, CancellationToken ct) => throw new NotSupportedException();
        public Task<Stream> OpenReadAsync(string path, CancellationToken ct) => throw new NotSupportedException();
        public Task DeleteAsync(string path, CancellationToken ct) => throw new NotSupportedException();
    }
}
