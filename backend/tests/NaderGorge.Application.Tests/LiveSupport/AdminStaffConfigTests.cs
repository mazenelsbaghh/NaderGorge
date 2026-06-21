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
}
