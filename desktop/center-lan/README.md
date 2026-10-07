# Massar LAN gateway

The Flutter host owns business rules and SQLite. This Go standard-library service terminates LAN TLS, pairs devices, and proxies requests to the host's loopback HTTP bridge. It neither reads SQLite nor chooses an employee identity.

The module declares Go **1.22.0 or newer**; method-aware HTTP routing requires Go 1.22. Verification in this workspace used Go **1.27.1 on macOS ARM64**. Windows and Intel Mac artifacts are crosscompiled; this alone does not verify their native runtime behavior.

## Parent process contract

Launch `massar-lan-host` (`massar-lan-host.exe` on Windows) without secret command-line arguments or environment variables. Write one UTF-8 JSON configuration object to stdin, followed by a newline, and **flush but keep stdin open** for the lifetime of the Flutter host. Closing the pipe, including when the parent crashes, shuts down both listeners. SIGTERM or an interrupt also initiates shutdown.

| Configuration field | Required value |
| --- | --- |
| `upstream` | HTTP loopback IP origin with explicit port, e.g. `http://127.0.0.1:12345`; DNS names, remote IPs, credentials, query, fragment, and non-root paths are rejected |
| `upstreamSecret` | 32–512 bytes, without CR/LF; generated and held by the owning Flutter process |
| `dataDir` | Dedicated persistent LAN configuration directory |
| `name` | Trimmed, nonempty host name, at most 120 Unicode characters, without CR/LF/NUL |
| `port` | Optional HTTPS port; default `43873`; explicit `0` selects an ephemeral port for tests |
| `discoveryPort` | Optional UDP discovery port; default `43874`; explicit `0` selects an ephemeral port for tests |

Configuration is capped at 64 KiB and unknown configuration fields are rejected. On startup stdout emits one JSON ready object with `ready`, `protocol`, `hostId`, `name`, the actual HTTPS `port`, `certificateSha256`, `pairingCode`, and `pairingExpiresAt`. Dates use RFC3339 UTC. Treat the code as private owner information, not diagnostic output. A startup failure emits `{"ready":false,"error":"gateway_start_failed"}` and exits unsuccessfully. Normal stdout has no HTTP, credential, or payload logs.

`identity.json` persists the stable host ID, certificate, and private key. New certificates use ECDSA P-256 for Flutter TLS compatibility. `devices.json` stores device bindings and SHA-256 token hashes, not plaintext bearer tokens. Both files are written through synchronized temporary files and rename, with file mode `0600`. Existing identity files are preserved; do not silently replace a trusted certificate to repair a client pin mismatch.

## Discovery and transport

Send UDP JSON `{"kind":"massar-discover","protocol":1}` to the discovery port. Matching requests from private, loopback, or link-local unicast addresses receive:

- `kind`: `massar-host`
- `protocol`: `1`
- `hostId`, `name`, HTTPS `port`
- `certificateSha256`: lowercase SHA-256 hex of the certificate DER

`GET /health` returns the same public identity over HTTPS without authentication. Discovery does not return a pairing code or any student/staff data. Responses are capped at 50 per second; malformed, oversized, wrong-kind, or wrong-protocol queries receive no response.

TLS requires version 1.2 or newer. The client must verify the chosen certificate fingerprint, including on reconnect. A different fingerprint is a trust failure, not permission to accept an arbitrary certificate.

## Device pairing

`POST /pair` accepts only `code`, `deviceId`, and `name`. Device IDs match `[A-Za-z0-9._-]{1,100}` and names follow the host-name limits. A successful response contains `token`, `deviceId`, `hostId`, and `protocol`.

The code is cryptographically random, six digits, and valid for ten minutes. Five wrong attempts from one IP, or thirty wrong attempts overall, block further pairing until the owner rotates the code or the service restarts. The blocked response is HTTP 429 with `pairing_rate_limited`; an invalid or expired code returns HTTP 403 with `pairing_rejected`. Malformed JSON returns HTTP 400 with `invalid_request`.

Tokens contain 32 random bytes, encoded as unpadded base64url. Re-pairing an existing device ID replaces its token; the previous token immediately stops authenticating new requests. A failed registry write returns HTTP 500 and preserves the previous in-memory credential. There are at most 100 stored device IDs, including revoked records.

## Proxy boundary

All `/api/*` paths and their query strings are forwarded unchanged. Requests require `Authorization: Bearer <paired-device-token>`. Employee authentication remains independent: the client supplies the opaque `X-Massar-Session` issued by the Flutter bridge.

The gateway removes the pair authorization and every supplied `X-Massar-*` header except `X-Massar-Session`, `X-Massar-State-Version`, and `X-Massar-State-Patch`, then injects `X-Massar-Bridge-Secret` and the verified `X-Massar-Device-ID`. The state headers are opaque synchronization hints, never authorization. Employee permissions and command idempotency belong to Flutter. Supplying an actor ID cannot establish staff authority.

The Flutter bridge can return a partial snapshot when `X-Massar-State-Patch: 1` accompanies a known state version. It sends version-bound changes to records and fields; clients without this capability, or whose base version is unavailable, receive the complete snapshot. The gateway forwards this response without applying it or retaining application state. A malformed or mismatched partial snapshot triggers a full read on the client; it does not retry the mutation. An unresolved committed response retains the existing pending command identity for reconciliation.

The gateway buffers at most 2 MiB of request body before forwarding, so an oversized request cannot partially reach the bridge. It rejects upgrades and duplicate staff-session or state-hint headers. Header values are capped at 4096 bytes for the staff session, 128 for the state version, and 16 for the patch capability. Pair/control JSON bodies are capped at 4096 bytes and unknown fields or trailing JSON are rejected.

Unpaired or revoked devices receive HTTP 401 with `device_not_paired`. Oversized API payloads receive HTTP 413 with `request_too_large`. Upstream connection failures and redirects return HTTP 502 with `host_unavailable`; redirects are never followed with credentials. The upstream bridge secret is removed from response headers. The proxy uses a five-second dial timeout, thirty-second response-header timeout, and bounded host connections. The HTTPS server also bounds headers and read/write/idle durations.

## Owner controls

These HTTPS endpoints require both an actual loopback peer and `Authorization: Bearer <upstreamSecret>`. A device bearer token, or a forged forwarding header, cannot authorize them.

| Endpoint | Request / response |
| --- | --- |
| `POST /control/pairing` | Returns `pairingCode` and `expiresAt`; resets pairing attempt limits |
| `GET /control/devices` | Returns `devices`: each entry has `deviceId`, `name`, `pairedAt`, and `revoked`; token hashes are omitted |
| `POST /control/devices/revoke` | Accepts `{"deviceId":"..."}`; returns `revoked: true` and `deviceId`; missing devices return HTTP 404 |

Revocation is persisted before the active registry changes. Requests already accepted before revocation may finish; subsequent requests require an active token.

## Verification and builds

From this directory:

```sh
go test ./...
go test -race ./...
go vet ./...
```

The tests use real temporary files, HTTPS/UDP sockets, and a real loopback upstream. The lifecycle regression builds and starts the actual executable, checks pinned health, closes the parent pipe, and proves both ports are reusable. It uses an `.exe` filename when the tests run on Windows. The race check requires the platform's Go race-detector support.

Build artifacts belong in ignored `bin/`:

```sh
mkdir -p bin
CGO_ENABLED=0 GOOS=darwin GOARCH=arm64 go build -trimpath -ldflags='-s -w' -o bin/massar-lan-host-darwin-arm64 .
CGO_ENABLED=0 GOOS=darwin GOARCH=amd64 go build -trimpath -ldflags='-s -w' -o bin/massar-lan-host-darwin-amd64 .
CGO_ENABLED=0 GOOS=windows GOARCH=amd64 go build -trimpath -ldflags='-s -w' -o bin/massar-lan-host-windows-amd64.exe .
```

The packaging layer selects the matching artifact and names the bundled executable `massar-lan-host` or `massar-lan-host.exe`.
