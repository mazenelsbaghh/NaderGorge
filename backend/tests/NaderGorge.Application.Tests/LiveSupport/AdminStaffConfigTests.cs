using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Infrastructure.Services;

namespace NaderGorge.Application.Tests.LiveSupport;

public sealed class AdminStaffConfigTests
{
    [Fact]
    public async Task CapacityAndOverlappingScheduleAreValidated()
    {
        await using var fixture = await LiveSupportTestDb.CreateSeededAsync();
        var service = new LiveSupportService(fixture.Db, new LiveSupportEnabledSettings(), new LiveSupportConnectedPresence());
        var windows = new[] { new LiveSupportScheduleWindowDto(1, new(9,0), new(12,0)), new LiveSupportScheduleWindowDto(1, new(11,0), new(13,0)) };
        await Assert.ThrowsAsync<LiveSupportException>(() => service.UpdateStaffConfigAsync(LiveSupportTestData.AdminId, LiveSupportTestData.StaffAId, true, 0, 1, [], CancellationToken.None));
        await Assert.ThrowsAsync<LiveSupportException>(() => service.UpdateStaffConfigAsync(LiveSupportTestData.AdminId, LiveSupportTestData.StaffAId, true, 2, 1, windows, CancellationToken.None));
    }

    [Fact]
    public async Task OvernightSchedule_IsAcceptedAndRemainsActiveAfterMidnight()
    {
        await using var fixture = await LiveSupportTestDb.CreateSeededAsync();
        var service = new LiveSupportService(fixture.Db, new LiveSupportEnabledSettings(), new LiveSupportConnectedPresence());
        var overnight = new LiveSupportScheduleWindowDto(1, new(22, 0), new(2, 0));

        var result = await service.UpdateStaffConfigAsync(
            LiveSupportTestData.AdminId,
            LiveSupportTestData.StaffAId,
            true,
            2,
            1,
            [overnight],
            CancellationToken.None);

        Assert.Contains(result.Schedule, window => window == overnight);
        Assert.True(LiveSupportScheduleRules.Contains(new DateTime(2026, 9, 7, 23, 0, 0), overnight));
        Assert.True(LiveSupportScheduleRules.Contains(new DateTime(2026, 9, 8, 1, 59, 0), overnight));
        Assert.False(LiveSupportScheduleRules.Contains(new DateTime(2026, 9, 8, 2, 0, 0), overnight));
    }

    [Fact]
    public void OvernightSchedule_RejectsCrossDayOverlap()
    {
        var windows = new[]
        {
            new LiveSupportScheduleWindowDto(1, new(22, 0), new(2, 0)),
            new LiveSupportScheduleWindowDto(2, new(1, 0), new(3, 0))
        };

        var error = Assert.Throws<LiveSupportException>(() => LiveSupportScheduleRules.Validate(windows));

        Assert.Equal("VALIDATION_ERROR", error.Code);
    }
}
