using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.LiveSupport.Interfaces;

namespace NaderGorge.Application.Tests.LiveSupport;

internal static class LiveSupportTestData
{
    public static readonly Guid StudentId = Guid.Parse("14200000-0000-0000-0000-000000000001");
    public static readonly Guid GuestId = Guid.Parse("14200000-0000-0000-0000-000000000002");
    public static readonly Guid AdminId = Guid.Parse("14200000-0000-0000-0000-000000000003");
    public static readonly Guid StaffAId = Guid.Parse("14200000-0000-0000-0000-000000000004");
    public static readonly Guid StaffBId = Guid.Parse("14200000-0000-0000-0000-000000000005");

    public static User User(Guid id, string name, string phone) => new() { Id = id, FullName = name, PhoneNumber = phone, PasswordHash = "test-only" };
    public static LiveSupportGuestSession Guest() => new() { Id = GuestId, DisplayName = "زائر الاختبار", PhoneNumber = "01099999999", SecurityStampHash = new string('A', 64), ExpiresAt = DateTime.UtcNow.AddDays(1), LastSeenAt = DateTime.UtcNow };
    public static LiveSupportConversation Conversation(Guid? owner = null) => new() { Id = Guid.Parse("14200000-0000-0000-0000-000000000010"), ParticipantType = LiveSupportParticipantType.Student, StudentUserId = StudentId, LinkedStudentUserId = StudentId, CurrentOwnerUserId = owner, Status = owner.HasValue ? LiveSupportConversationStatus.Assigned : LiveSupportConversationStatus.Waiting, QueuedAt = DateTime.UtcNow, Version = 1 };
}

internal sealed class LiveSupportEnabledSettings : ICachedPlatformSettingsReader
{
    public Task<CachedPlatformSettings> GetAsync(CancellationToken cancellationToken) => Task.FromResult(CachedPlatformSettings.Default with { LiveSupportEnabled = true });
    public void Invalidate() { }
}

internal sealed class LiveSupportConnectedPresence : ILiveSupportPresenceStore
{
    public Task ConnectedAsync(Guid staffUserId, string connectionId) => Task.CompletedTask;
    public Task DisconnectedAsync(Guid staffUserId, string connectionId) => Task.CompletedTask;
    public Task HeartbeatAsync(Guid staffUserId) => Task.CompletedTask;
    public Task<bool> IsConnectedAsync(Guid staffUserId) => Task.FromResult(true);
    public Task<IReadOnlyList<Guid>> ClaimExpiredDisconnectsAsync(DateTime utcNow) => Task.FromResult<IReadOnlyList<Guid>>([]);
}
