using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Windows.Networking.Connectivity;
using Windows.Networking.NetworkOperators;

namespace Massar.Hotspot;

internal static class HomeworkHotspot
{
    internal static async Task<HotspotState> Execute(string action)
    {
        if (action is not ("tethering-status" or "tethering-start" or "tethering-stop"))
            throw new InvalidDataException("Unknown tethering action.");
        try
        {
            var tethering = FindManager();
            if (tethering is null)
                return new(false, true, false, "لم يجد ويندوز اتصالًا مناسبًا للهوتسبوت. وصّل الهوست بالواي فاي أو Ethernet وافتح إعدادات نقطة اتصال الهواتف المحمولة.");
            return await Change(tethering, action);
        }
        catch (Exception exception) when (exception is COMException or UnauthorizedAccessException or TimeoutException or NotSupportedException or InvalidOperationException or ArgumentException)
        {
            return new(false, true, false,
                "تعذر تشغيل شبكة الهوست. راجع إعدادات ويندوز ← الشبكة والإنترنت ← نقطة اتصال الهواتف المحمولة، ودعم محول الواي فاي.",
                Status: $"WindowsError:0x{exception.HResult:X8}");
        }
    }

    private static NetworkOperatorTetheringManager? FindManager()
    {
        NetworkOperatorTetheringManager? available = null;
        var profiles = NetworkInformation.GetConnectionProfiles()
            .OrderByDescending(profile => profile.GetNetworkConnectivityLevel() != NetworkConnectivityLevel.None)
            .ThenByDescending(profile => profile.NetworkAdapter?.IanaInterfaceType == 6)
            .ThenByDescending(profile => profile.GetNetworkConnectivityLevel() == NetworkConnectivityLevel.InternetAccess)
            .ThenByDescending(profile => profile.IsWlanConnectionProfile);
        foreach (var profile in profiles)
        {
            try
            {
                var manager = NetworkOperatorTetheringManager.CreateFromConnectionProfile(profile);
                // Keep the existing network and its clients when another uplink appears.
                if (manager.TetheringOperationalState == TetheringOperationalState.On)
                    return manager;
                if (available is null)
                    available = manager;
            }
            catch (Exception exception) when (exception is COMException or ArgumentException or NotSupportedException)
            {
                // One unsupported virtual adapter must not hide a working Wi-Fi uplink.
            }
        }
        return available;
    }

    private static async Task<HotspotState> Change(NetworkOperatorTetheringManager tethering, string action)
    {
        var active = tethering.TetheringOperationalState == TetheringOperationalState.On;
        if (action == "tethering-start" && !active)
        {
            var operation = await tethering.StartTetheringAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(25));
            if (operation.Status != TetheringOperationStatus.Success)
                return Failure(tethering, operation.Status.ToString());
        }
        else if (action == "tethering-stop" && active)
        {
            var operation = await tethering.StopTetheringAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(25));
            if (operation.Status != TetheringOperationStatus.Success)
                return Failure(tethering, operation.Status.ToString());
        }
        return State(tethering, action);
    }

    private static HotspotState Failure(NetworkOperatorTetheringManager tethering, string status) =>
        new(false, true, tethering.TetheringOperationalState == TetheringOperationalState.On,
            $"ويندوز لم يكمل طلب شبكة الهوست ({status}). افتح إعدادات نقطة الاتصال وراجع الاتصال ومحول الواي فاي.",
            Status: status);

    private static HotspotState State(NetworkOperatorTetheringManager tethering, string action)
    {
        var active = tethering.TetheringOperationalState == TetheringOperationalState.On;
        var successful = action == "tethering-status" || (action == "tethering-start" ? active : !active);
        return new(successful, true, active,
            active ? "شبكة الهوست تعمل. وصّل الساكند أو موبايل الموظف بهذه الشبكة." : "شبكة الهوست غير مشغّلة.",
            tethering.GetCurrentAccessPointConfiguration().Ssid,
            tethering.TetheringOperationalState.ToString(), HotspotAddresses());
    }

    private static string[] HotspotAddresses() => NetworkInterface.GetAllNetworkInterfaces()
        .Where(adapter => adapter.OperationalStatus == OperationalStatus.Up &&
            adapter.Description.Contains("Wi-Fi Direct", StringComparison.OrdinalIgnoreCase))
        .SelectMany(adapter => adapter.GetIPProperties().UnicastAddresses)
        .Where(address => address.Address.AddressFamily == AddressFamily.InterNetwork &&
            !address.Address.ToString().StartsWith("169.254.", StringComparison.Ordinal))
        .Select(address => address.Address.ToString()).Distinct().ToArray();
}
