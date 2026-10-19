using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NaderGorge.API.BackgroundServices;
using NaderGorge.API.Hubs;
using NaderGorge.Application.Features.Exams;
using NaderGorge.Application.Features.Gamification.Commands;
using NaderGorge.Application.Interfaces;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.Gamification;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Infrastructure.Background;
using NaderGorge.Integration.Tests.AdminAI;

namespace NaderGorge.Integration.Tests.Exams;

public sealed class ExamSubmissionConsumerCompatibilityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BridgeConsumer_HandlesFutureEffectAndRedeliveryWithoutDuplicatingItsIdentity(bool academicReward)
    {
        await using var fixture = await PostgresAdminAIFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        await db.Database.MigrateAsync();
        var student = new User { FullName = "Bridge consumer student", PhoneNumber = "01000000981", PasswordHash = "test" };
        var effect = new OutboxEvent
        {
            Type = academicReward ? ExamSubmissionEffects.AcademicRewardEventType : ExamSubmissionEffects.ParentPushEventType,
            TargetUserId = academicReward ? null : student.Id.ToString(),
            CreatedAt = DateTime.UtcNow.AddMinutes(-1)
        };
        effect.PayloadJson = academicReward
            ? JsonSerializer.Serialize(new AcademicTaskCompletedEvent(student.Id, GamificationEventType.PerfectExam, 120, effect.Id))
            : JsonSerializer.Serialize(new ExamParentPushPayload(student.Id, "امتحان تجريبي"));
        db.AddRange(student, effect);
        await db.SaveChangesAsync();
        var effects = new ExternalEffects();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSignalR();
        services.AddScoped<IAppDbContext>(_ => fixture.CreateDbContext());
        services.AddSingleton<IJobEnqueuer>(effects);
        await using var provider = services.BuildServiceProvider();
        using var processor = new OutboxProcessorBackgroundService(provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<IHubContext<PlatformHub>>(), NullLogger<OutboxProcessorBackgroundService>.Instance,
            new ConfigurationBuilder().Build());

        await DispatchOneAsync(processor);
        db.ChangeTracker.Clear();
        Assert.NotNull((await db.OutboxEvents.SingleAsync(e => e.Id == effect.Id)).ProcessedAt);
        await db.OutboxEvents.Where(e => e.Id == effect.Id)
            .ExecuteUpdateAsync(update => update.SetProperty(e => e.ProcessedAt, (DateTime?)null));
        await DispatchOneAsync(processor);

        db.ChangeTracker.Clear();
        Assert.NotNull((await db.OutboxEvents.SingleAsync(e => e.Id == effect.Id)).ProcessedAt);
        if (academicReward)
        {
            Assert.Equal(120, (await db.StudentGamifications.SingleAsync()).TotalPoints);
            Assert.Equal(effect.Id, (await db.GamificationActionLogs.SingleAsync()).Id);
            Assert.Empty(effects.ParentJobs);
        }
        else
        {
            Assert.Equal(2, effects.ParentJobs.Count);
            Assert.All(effects.ParentJobs, payload => Assert.Equal(effect.Id.ToString(),
                RedisJobEnqueuer.ResolveStableJobId("notifications", payload)));
            using var delivered = JsonDocument.Parse(effects.ParentJobs.Last());
            Assert.Equal(student.Id, delivered.RootElement.GetProperty("StudentId").GetGuid());
            Assert.Contains("امتحان تجريبي", delivered.RootElement.GetProperty("Body").GetString());
            Assert.Empty(await db.GamificationActionLogs.ToListAsync());
        }
    }

    private static async Task DispatchOneAsync(OutboxProcessorBackgroundService processor)
    {
        var dispatch = typeof(OutboxProcessorBackgroundService)
            .GetMethod("ProcessOutboxEventsAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await (Task<bool>)dispatch.Invoke(processor, [CancellationToken.None])!;
    }

    private sealed class ExternalEffects : IJobEnqueuer
    {
        public List<string> ParentJobs { get; } = [];
        public Task EnqueueJobAsync<T>(string queueName, string jobName, T data)
        {
            ParentJobs.Add(JsonSerializer.Serialize(data));
            return Task.CompletedTask;
        }
    }
}
