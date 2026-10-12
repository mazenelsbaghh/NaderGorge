# Windows direct exam network

`massar-exam-network.exe` is the Windows network companion bundled with both center roles. It replaces the Mac's direct-network DHCP/DNS job on **one explicitly selected physical Ethernet interface**. The exam engine remains a separate user process; center financial data continues to belong to the center host.

The public command interface reads one UTF-8 JSON object from stdin, then EOF, and writes one JSON object to stdout. Staff tokens and hotspot passwords must never be placed in arguments or log messages.

| action | additional input | result |
| --- | --- | --- |
| `list-adapters` | none | physical adapter GUID, index, name, state, IPv4 addresses |
| `status` | none | configured/actual address, DHCP heartbeat, DNS, HTTP, student service, HTTPS status |
| `configure` | `adapterId`, `adminPort:8766`, `adminToken` | UAC prompt, direct-network setup or failure |
| `restore` | `adminPort:8766`, `adminToken` | UAC prompt, restores this helper's original DHCP/profile settings |
| `portal-configure` | maintenance fields, `mode:legacy` or `mode:https`, and for HTTPS `hostname`, `certificatePath`, `privateKeyPath` | validates certificate first, applies HTTP/CAPPORT mode on the existing exam adapter |
| `hotspot-start` | `ssid`, `password` | starts a per-user Windows Wi-Fi Direct legacy group owner |
| `hotspot-status` / `hotspot-stop` | none | inspects/stops only the current user's owned hotspot |

The helper obtains a short server-side network maintenance reservation before any network mutation. The elevated phase receives only adapter/portal selections and its deadline. A persistent `ProgramData\Massar\ExamNetwork\setup.lock` blocks exam opening if an installer outlives its caller. Unconfirmed rollback keeps that lock and requires administrator inspection; it is not automatically removed on reboot. Inspect the retained `adapter-backup.json`, `network.json` and `rollback-*` snapshot before removing a stale lock. Never remove it while a setup process is still running.

## Direct connection

- Exam Ethernet: `10.77.0.1/22`; no default gateway, bridge, NAT, Internet Connection Sharing or DNS-client changes.
- DHCP only answers packets whose Windows packet information identifies the selected adapter index. The GUID and index are verified again at runtime startup. The UDP socket's outgoing interface is pinned to the same index.
- Student leases: `10.77.0.20` through `10.77.3.250`, one hour. Existing Omada MAC reservation retains `10.77.0.2`. Offers/acknowledgements are persisted before sending; duplicate/corrupt reservations fail closed.
- DNS on the exam address answers A records locally and returns NODATA for other types. UDP and TCP DNS are supported; no upstream resolver is used.
- HTTP portal binds `10.77.0.1:80` and forwards only the student service at `127.0.0.1:8765`. Optional HTTPS binds `10.77.0.1:443` with DHCP option 114. Administration stays on `127.0.0.1:8766`.
- Firewall rules are limited to this program, interface, private profile, exam address/subnet (DHCP necessarily permits unassigned client source addresses). Other adapters and the center host connection are not changed.
- `status.ready` requires the configured address, fresh DHCP heartbeat, DNS resolution, redirect, and the actual lounge endpoint. HTTPS mode additionally requires a trusted TLS/CAPPORT/lounge probe. This cannot prove a phone received a DHCP offer or that the AP radio settings are correct.

The setup rejects existing unrelated static addresses/default routes, a subnet already present on another adapter, foreign DHCP/DNS/portal port owners, and changes to another previously configured adapter. It saves the prior DHCP and network profile, protects its state with Windows ACLs, and restores previous owned binaries/configuration on an ordinary failed update. Certificate keys and rollback snapshots are readable only by SYSTEM/administrators. DHCP leases are local runtime data and never belong in a release archive.

`run` and `apply` are internal lifecycle commands, not UI commands. The scheduled task runs the network companion as SYSTEM at Windows startup. The application must remain open for the exam engine to serve answers; the network service alone does not host an exam.

## Validation

Protocol tests exercise DHCP persistence/restart, duplicate prevention, declines/expiry, foreign-server and malformed-packet rejection, write failure, CAPPORT discovery, DNS framing and real UDP transport. Go helpers are cross-compiled for Windows. PowerShell scripts receive parser validation without being executed against live adapters. The Wi-Fi Direct helper is compiled against the Windows SDK.

These checks do **not** certify physical Windows behavior. Before deployment, validate UAC cancellation, setup failure/rollback, two adapters, cable removal/reconnect, restart, real Omada DHCP offers, Android/iPhone portal behavior, HTTPS expiry/fallback, sleep prevention, and Windows Wi-Fi Direct adapter support. Do not publish a claim that an actual Windows machine or student phones have passed until that has been observed.
