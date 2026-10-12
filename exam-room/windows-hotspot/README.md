# Windows host Wi-Fi Direct companion

This small self-contained .NET helper uses `Windows.Devices.WiFiDirect.WiFiDirectAdvertisementPublisher` with autonomous group owner and legacy settings. It does not call the deprecated `netsh wlan hostednetwork` path and does not configure Internet Connection Sharing. It follows the [Microsoft classic WiFiDirectLegacyAP sample](https://github.com/microsoft/Windows-classic-samples/tree/main/Samples/WiFiDirectLegacyAP).

`Massar.Hotspot.exe` reads one JSON line from stdin: `{ "action": "start", "ssid": "...", "password": "..." }`, or `status`/`stop`. The launcher returns one status JSON object. A per-user worker keeps the advertisement alive and accepts status/stop over a local named pipe restricted to the same user. The password is transient process memory only: no settings file, command-line argument, response, diagnostic event, or release asset contains it. Changing credentials requires stopping the owned advertisement first.

A dedicated thread holds a Windows idle-sleep assertion while this worker is alive. The worker stops on an explicit stop or Windows logout/shutdown. Closing the center UI alone does not stop the hotspot; the UI should issue stop when the owner requests it. The reported IPv4 addresses are newly observed local addresses, not proof of the secondary device's connectivity. Center pairing still uses the center gateway's existing trusted device workflow.

The publisher's `Started` event is used as the activation result. Unsupported hardware, Wi-Fi radio/driver failures, or a conflicting Windows Mobile Hotspot produce a failure response. An actual Windows adapter and client connection must still be tested; cross-compilation cannot verify Wi-Fi hardware support.

Build with `dotnet publish -c Release -r win-x64 --self-contained true`. The center Windows packaging script includes the output directory and preserves the existing PDF worker. Edge WebView2 must already be installed on the target computer for the embedded exam administration view; no online dependency installation is triggered on exam day.
