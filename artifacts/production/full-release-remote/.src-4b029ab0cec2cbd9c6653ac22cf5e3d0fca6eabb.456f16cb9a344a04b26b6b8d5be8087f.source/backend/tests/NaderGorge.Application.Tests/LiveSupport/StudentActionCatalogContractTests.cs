using System.Reflection;
using NaderGorge.Infrastructure.Services;

namespace NaderGorge.Application.Tests.LiveSupport;

public sealed class StudentActionCatalogContractTests
{
    public static readonly string[] Expected = ["student.profile.update","student.password.reset","student.account.status.set","student.note.add","student.note.delete","student.device.disconnect","student.devices.disconnect-all","student.package.cancel","student.balance.adjust","student.gamification.adjust","student.video.override.add","student.watch.reset","student.watch.count.set","student.watch-request.approve","student.watch-request.reject","student.lesson.unlock","student.crm.assign","student.crm.call.add","student.create-and-link"];

    [Fact]
    public void ServerCatalogMatchesStableContract()
    {
        var field = typeof(LiveSupportActionService).GetField("Catalog", BindingFlags.NonPublic | BindingFlags.Static)!;
        var values = ((Array)field.GetValue(null)!).Cast<object>().Select(x => (string)x.GetType().GetProperty("Key")!.GetValue(x)!).Order().ToArray();
        Assert.Equal(Expected.Order(), values);
    }
}
