# Massar private support service

This source-only Go 1.22 service accepts voluntary support uploads and serves pre-authored desktop updates. It does not synchronize operational data, run migrations, execute commands, or replace the offline database. The source has passed `go test -race ./...` and `go vet ./...` locally using synthetic fixtures, temporary private directories, and loopback TLS only. It has not been deployed or exercised with actual center data.

## Configuration and isolation

All configuration is required except the listen address and upload size. Invalid or missing configuration prevents startup.

| Environment variable | Value |
| --- | --- |
| `MASSAR_SUPPORT_LISTEN` | Loopback IP and port; default `127.0.0.1:43880` |
| `MASSAR_SUPPORT_STORAGE_DIR` | Absolute private upload directory, outside the webroot |
| `MASSAR_SUPPORT_UPDATES_DIR` | Absolute read-only release directory, separate from upload storage |
| `MASSAR_SUPPORT_DEVICES_JSON` | JSON object mapping each center ID to its device Bearer token's lowercase SHA-256 hash |
| `MASSAR_SUPPORT_ADMIN_TOKEN_SHA256` | Separate administrator Bearer token's lowercase SHA-256 hash |
| `MASSAR_SUPPORT_MAX_UPLOAD_BYTES` | Optional limit; default `134217728` bytes (128 MiB), maximum 1 GiB |

Center IDs use 1–64 ASCII letters, digits, `_` or `-`, beginning with a letter or digit. Hashes must be distinct, nonzero, and 64 lowercase hexadecimal characters. Provision random tokens privately; configure only their hashes on the server. Never commit plaintext tokens or a populated environment file. The same center credential may initially be provisioned to its devices; the API identifies the center, not an individual device.

Run under a dedicated unprivileged Linux account. Upload directories are `0700`; bundle and receipt files are `0600`. Only that account and authorized support operators should access the storage. The release directory must contain only publishable release ZIPs and manifests, be readable by the service, and be writable only by the release administrator. Do not put databases, diagnostic bundles, or credentials in it. Keep both directories outside any proxy static-file location. The service rejects overlapping directories and symlink escapes.

A local reverse proxy must terminate TLS, overwrite `X-Forwarded-Proto` with `https`, preserve `Authorization`, and forward exclusively to the loopback listener. Plain HTTP is rejected. Examples are in `deploy/`; they contain no usable credentials. Use your normal certificate provisioning and secrets management. Request/access-body logging must remain disabled.

## Upload contract

Device Bearer token: `POST /v1/uploads`, `Content-Type: application/json`.

```json
{
  "format": "massar-support-upload-v1",
  "kind": "database",
  "uploadId": "UUID-v4",
  "centerId": "provisioned-center-id",
  "createdAt": "2026-10-03T12:00:00Z",
  "app": {"version":"1.0.0+1","build":"16-lowercase-hex","role":"host","os":"windows"},
  "data": {},
  "diagnostics": "sanitized ProblemLog JSONL text"
}
```

IDs must be lowercase UUID v4; timestamps must be UTC RFC3339 with `Z`. `data` is the full logical backup object, deliberately retained without record redaction so authorized support can restore and inspect it. It can contain student contact details, financial history, and password credential hashes. Uploads therefore require explicit support access protection, encrypted disks and backups, and an operator-defined retention policy. Nothing is published or executable.

The client secondary uses `kind:"diagnostics"`, `app.role:"client"`, and `data:null`; client database uploads are rejected. `kind` is optional for backwards compatibility and inferred from `app.role`. A host may also send diagnostics only with `data:null`. The desktop client must never create or retain a database merely to upload support data. Role is supplied by the authenticated desktop; the shared center credential does not independently attest the device role.

Diagnostics are limited to 6 MiB and reconstructed on the server from the explicit ProblemLog whitelist: event/session UUIDs and UTC timestamps, safe app version/build/role/platform, known operation/type identifiers, bounded numeric codes, slow-operation durations, fixed phase names and numeric counts, and known app filenames with bounded line numbers. Messages, arbitrary paths, function strings, unknown properties and invalid lines are discarded. The input export header is omitted. The service does not log request bodies, tokens, raw exceptions, or filesystem paths.

First accepted upload returns `201`; an identical logical retry returns `200` with the original durable receipt:

```json
{
  "receiptId":"UUID-v4", "uploadId":"UUID-v4", "centerId":"center-id",
  "sha256":"64-lowercase-hex", "bundleSha256":"64-lowercase-hex",
  "receivedAt":"2026-10-03T12:00:01Z", "createdAt":"2026-10-03T12:00:00Z",
  "size":1234,
  "app":{"version":"1.0.0+1","build":"16-lowercase-hex","role":"host","os":"windows"}
}
```

`sha256` identifies canonical input JSON before diagnostics sanitization. It is not necessarily the hash of the desktop's UTF-8 request bytes. `bundleSha256` verifies the actual saved sanitized bundle. Financial integers are preserved exactly with `json.Number`. A repeated upload ID with different content or center returns `409 upload_id_conflict`. Retry an uncertain response with the same immutable envelope and ID; never treat HTTP success without a matching valid receipt as acknowledgement.

Publication writes and syncs a private temporary directory, then atomically renames its complete bundle and receipt into the UUID directory and syncs the destination directory before acknowledgement. Concurrent publishers cannot replace an existing complete upload. Storage failure returns `503`; upload admission is bounded to two concurrent requests. The HTTP header limit is 8 KiB, header timeout 10 seconds, full read timeout 3 minutes, and write timeout 5 minutes. Configure a storage quota and monitor free space; there is no automatic deletion of support evidence. Abandoned `.staging` directories can be cleaned while the service is stopped.

## Private support retrieval

These routes require the separate administrator Bearer token; device tokens are rejected:

- `GET /v1/uploads?limit=100&after=<UUID>` returns `{uploads:[receipt...],nextCursor:""}`. Limit 1–500; pagination is by UUID, not creation date.
- `GET /v1/uploads/<UUID>` returns the saved JSON bundle after its size and SHA-256 are verified. Range requests are supported.
- `GET /v1/uploads/<UUID>/diagnostics?limit=200` returns `{receipt,kind,events,total,truncated}`. Limit is 1–500; `events` contains the latest valid session/error/performance events in their original log order, `total` counts all valid sanitized events, and `truncated` indicates omitted older events. The saved bundle is verified before projection; database values are streamed past without retaining a database map. Diagnostics are sanitized again on read, including older saved evidence. No database, student records, raw messages, paths, or raw diagnostic text appear in this response.
- `GET /v1/admin/releases` returns `{releases:[{platform,role,status,manifest?}]}` with exactly four slots: Windows x64 and macOS arm64, each host/client. `status` is `available`, `missing`, or `invalid`. Only an available slot includes the validated manifest; ZIP size and SHA-256 are verified. A missing manifest is distinct from a manifest whose archive is missing or damaged. No credentials, storage paths, raw malformed manifests, or internal failure text are returned.


The administrator credential is not accepted as a device credential for upload or update endpoints.

## Desktop updates

Device Bearer token is required for both manifests and downloads.

- `GET /v1/updates/windows-x64/host` (also `client`, and platform `macos-arm64`).
- `GET /v1/releases/<safe-basename>.zip`.

Publish manifests at `UPDATES_DIR/manifests/<platform>/<role>.json`, and immutable release ZIPs directly in `UPDATES_DIR`:

```json
{
  "releaseId":"release-1-0-1",
  "version":"1.0.1+2",
  "build":"0123456789abcdef",
  "platform":"windows-x64",
  "role":"host",
  "size":123456,
  "sha256":"64-lowercase-hex",
  "downloadPath":"/v1/releases/massar-host-1.0.1.zip",
  "notes":"Release notes"
}
```

A missing manifest returns `204`. Invalid content, platform/role mismatch, missing ZIP, or size/hash mismatch returns `503 update_unavailable`; there is no fallback to another platform or role. Manifests are at most 64 KiB, notes at most 2048 Unicode code points, and ZIPs at most 2 GiB. Only ZIP basenames with ASCII letters, digits, `.`, `_`, `-` are accepted, starting with a letter/digit. No arbitrary paths or redirects are used. Release ZIPs must remain immutable; publish the ZIP before atomically replacing its corresponding manifest.

Versions use `major.minor.patch+numericBuild` with an optional prerelease suffix; build identity is 16 lowercase hex characters. The desktop updater decides whether a version is newer. There is no inferred ordering of content hashes or server-side installation. Use a new, increasing version/build number for every offered release; same-version content changes are rejected by the desktop.

## Verification

The Go suite covers behavioral test functions with table-driven cases: durable/restarted and concurrent idempotence, center binding and admin/device capability separation, client diagnostics-only enforcement, full logical data preservation with independently sanitized diagnostics, storage failure and corrupt evidence, known/chunked request and diagnostics bounds, TLS proxy trust, provisioning and directory isolation, exact platform/role offers, ZIP transfer/ranges, tampered/malformed manifests, private-file/symlink escape rejection, admin-only diagnostics projection with re-sanitization and bounded event history, evidence tamper rejection, and verified four-slot release inventory. Tests use no real database or external upload destination.
