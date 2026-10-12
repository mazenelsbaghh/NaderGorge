using System.Diagnostics;
using System.IO.Pipes;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Windows.Devices.WiFiDirect;
using Windows.Security.Credentials;

namespace Massar.Hotspot;

internal sealed record HotspotRequest(string Action, string? Ssid, string? Password);
internal sealed record HotspotState(bool Ok, bool Supported, bool Active, string Message,
    string? Ssid = null, string? Status = null, string[]? Addresses = null,
    bool PhysicalWindowsVerified = false);

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };
    private static string PipeName => "Massar.Center.Hotspot." + WindowsIdentity.GetCurrent().User!.Value;

    private static async Task<int> Main(string[] arguments)
    {
        // The native launcher sends UTF-8 JSON regardless of the Windows OEM code page.
        Console.InputEncoding = new UTF8Encoding(false);
        Console.OutputEncoding = new UTF8Encoding(false);
        try
        {
            var line = await Console.In.ReadLineAsync();
            if (line is null || line.Length > 4096) throw new InvalidDataException();
            var request = JsonSerializer.Deserialize<HotspotRequest>(line, JsonOptions) ?? throw new InvalidDataException();
            if (arguments is ["serve"]) return await Serve(request);
            var existing = await PipeRequest(request);
            if (existing is not null) { Print(existing); return 0; }
            if (request.Action == "status" || request.Action == "stop")
            {
                Print(new HotspotState(true, true, false, "شبكة الهوست غير مشغّلة.")); return 0;
            }
            if (request.Action != "start") throw new InvalidDataException();
            Validate(request);
            var start = new ProcessStartInfo(Environment.ProcessPath!, "serve")
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
                StandardInputEncoding = new UTF8Encoding(false),
                StandardOutputEncoding = new UTF8Encoding(false),
                StandardErrorEncoding = new UTF8Encoding(false)
            };
            using var worker = Process.Start(start) ?? throw new IOException();
            await worker.StandardInput.WriteLineAsync(JsonSerializer.Serialize(request, JsonOptions));
            worker.StandardInput.Close();
            var response = await worker.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(20));
            if (response is null) throw new IOException();
            // The worker owns Wi-Fi Direct after this launcher exits, until an explicit stop.
            Console.WriteLine(response);
            return 0;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            Print(new HotspotState(false, true, false, "تعذر تشغيل Wi-Fi Direct؛ افحص محول الواي فاي وأغلق Mobile Hotspot إن كان مشغّلًا."));
            return 1;
        }
    }

    private static void Validate(HotspotRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Ssid) || Encoding.UTF8.GetByteCount(request.Ssid) > 32 || request.Ssid.Any(char.IsControl) ||
            request.Password is null || request.Password.Length is < 8 or > 63 || request.Password.Any(character => character < 32 || character > 126))
            throw new InvalidDataException("Invalid SSID or WPA2 password.");
    }

    private static async Task<HotspotState?> PipeRequest(HotspotRequest request)
    {
        await using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        try { await pipe.ConnectAsync(700); }
        catch (TimeoutException) { return null; }
        using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
        using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);
        await writer.WriteLineAsync(JsonSerializer.Serialize(request, JsonOptions));
        var line = await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5));
        if (line is null || line.Length > 8192) throw new IOException();
        return JsonSerializer.Deserialize<HotspotState>(line, JsonOptions);
    }

    private static async Task<int> Serve(HotspotRequest request)
    {
        Validate(request);
        using var mutex = new Mutex(false, "Local\\" + PipeName, out var acquired);
        if (!acquired) { Print(new HotspotState(false, true, false, "هناك عملية أخرى تشغّل شبكة الهوست؛ افحص حالتها.")); return 1; }
        await using var server = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        using var awake = new KeepAwake();
        using var radio = new DirectRadio(request.Ssid!, request.Password!);
        var started = await radio.Start();
        Print(started);
        // Close inherited stdout so the calling native helper can finish while this worker runs.
        Console.Out.Close(); Console.Error.Close();
        if (!started.Active) return 1;
        while (true)
        {
            await server.WaitForConnectionAsync();
            var stop = false;
            try
            {
                using var reader = new StreamReader(server, Encoding.UTF8, leaveOpen: true);
                using var writer = new StreamWriter(server, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
                var line = await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5));
                if (line is null || line.Length > 4096) throw new InvalidDataException();
                var command = JsonSerializer.Deserialize<HotspotRequest>(line, JsonOptions) ?? throw new InvalidDataException();
                HotspotState result;
                if (command.Action == "stop") { radio.Stop(); stop = true; result = radio.Status(); }
                else if (command.Action == "start" && (command.Ssid != request.Ssid || command.Password != request.Password))
                    result = new HotspotState(false, true, true, "أوقف شبكة الهوست الحالية قبل تغيير اسمها.", request.Ssid);
                else if (command.Action is "status" or "start") result = radio.Status();
                else result = new HotspotState(false, true, true, "أمر الشبكة غير معروف.");
                await writer.WriteLineAsync(JsonSerializer.Serialize(result, JsonOptions));
            }
            catch (Exception exception) when (exception is IOException or TimeoutException or JsonException or InvalidDataException) { }
            finally { server.Disconnect(); }
            if (stop) return 0;
        }
    }

    private static void Print(HotspotState state) => Console.WriteLine(JsonSerializer.Serialize(state, JsonOptions));
}

internal sealed class DirectRadio : IDisposable
{
    private readonly WiFiDirectAdvertisementPublisher _publisher = new();
    private readonly WiFiDirectConnectionListener _listener = new();
    private readonly List<WiFiDirectDevice> _connections = [];
    private readonly HashSet<string> _addressesBefore;
    private readonly string _ssid;
    private readonly TaskCompletionSource<bool> _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private WiFiDirectAdvertisementPublisherStatus _status;

    internal DirectRadio(string ssid, string password)
    {
        _ssid = ssid;
        _addressesBefore = Addresses().ToHashSet(StringComparer.Ordinal);
        _publisher.Advertisement.IsAutonomousGroupOwnerEnabled = true;
        _publisher.Advertisement.LegacySettings.IsEnabled = true;
        _publisher.Advertisement.LegacySettings.Ssid = ssid;
        _publisher.Advertisement.LegacySettings.Passphrase = new PasswordCredential { Password = password };
        _publisher.StatusChanged += Changed;
        _listener.ConnectionRequested += ConnectionRequested;
    }

    internal async Task<HotspotState> Start()
    {
        _publisher.Start();
        try { await _started.Task.WaitAsync(TimeSpan.FromSeconds(12)); }
        catch (TimeoutException) { _publisher.Stop(); return new HotspotState(false, true, false, "لم يؤكد ويندوز بدء الشبكة؛ راجع دعم Wi-Fi Direct بالمحوّل."); }
        return Status();
    }

    private void Changed(WiFiDirectAdvertisementPublisher publisher, WiFiDirectAdvertisementPublisherStatusChangedEventArgs change)
    {
        _status = change.Status;
        if (_status is WiFiDirectAdvertisementPublisherStatus.Started or WiFiDirectAdvertisementPublisherStatus.Aborted)
            _started.TrySetResult(_status == WiFiDirectAdvertisementPublisherStatus.Started);
    }

    private async void ConnectionRequested(WiFiDirectConnectionListener listener, WiFiDirectConnectionRequestedEventArgs arguments)
    {
        using var request = arguments.GetConnectionRequest();
        try
        {
            var device = await WiFiDirectDevice.FromIdAsync(request.DeviceInformation.Id);
            if (device is not null) lock (_connections) _connections.Add(device);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException) { /* Windows rejected the peer; no session was created. */ }
    }

    internal HotspotState Status()
    {
        var active = _status == WiFiDirectAdvertisementPublisherStatus.Started;
        return new HotspotState(active || _status == WiFiDirectAdvertisementPublisherStatus.Stopped, true, active,
            active ? "شبكة الهوست تعمل؛ وصّل الساكند بها ثم استخدم ربط أجهزة السنتر." : "شبكة الهوست متوقفة أو غير مدعومة بالمحوّل.",
            _ssid, _status.ToString(), Addresses().Where(address => !_addressesBefore.Contains(address)).ToArray());
    }

    private static string[] Addresses() => NetworkInterface.GetAllNetworkInterfaces()
        .Where(adapter => adapter.OperationalStatus == OperationalStatus.Up)
        .SelectMany(adapter => adapter.GetIPProperties().UnicastAddresses)
        .Where(address => address.Address.AddressFamily == AddressFamily.InterNetwork && !address.Address.ToString().StartsWith("169.254.") && !address.Address.Equals(System.Net.IPAddress.Loopback))
        .Select(address => address.Address.ToString()).Distinct().ToArray();

    internal void Stop() { _publisher.Stop(); _status = WiFiDirectAdvertisementPublisherStatus.Stopped; }
    public void Dispose()
    {
        _publisher.StatusChanged -= Changed; _listener.ConnectionRequested -= ConnectionRequested;
        _publisher.Stop();
        lock (_connections) { foreach (var peer in _connections) peer.Dispose(); _connections.Clear(); }
    }
}

internal sealed class KeepAwake : IDisposable
{
    private readonly ManualResetEvent _stop = new(false);
    private readonly Thread _thread;
    [DllImport("kernel32.dll")]
    private static extern uint SetThreadExecutionState(uint flags);
    internal KeepAwake()
    {
        _thread = new Thread(() =>
        {
            SetThreadExecutionState(0x80000001);
            _stop.WaitOne();
            SetThreadExecutionState(0x80000000);
        }) { IsBackground = true, Name = "Massar host network awake" };
        _thread.Start();
    }
    public void Dispose() { _stop.Set(); _thread.Join(); _stop.Dispose(); }
}
