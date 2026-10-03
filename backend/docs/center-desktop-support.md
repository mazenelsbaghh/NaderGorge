# Desktop support admin API

This source adds a website admin adapter for the separate private desktop support service. It does not replace Flutter/SQLite as the center's authority, create website database tables, deploy a support service, or install desktop updates.

## Server configuration

Set `CenterDesktopSupport:BaseUrl` to the private HTTPS origin (no path, user information, query, or fragment), and `CenterDesktopSupport:AdminToken` to the service's **admin** credential. Environment equivalents are `CenterDesktopSupport__BaseUrl` and `CenterDesktopSupport__AdminToken`. Provision values through the existing private release configuration. Never send this credential to the browser or commit it. The adapter uses normal TLS certificate validation, disables redirects and cookies, and suppresses HTTP client and controller payload logging.

Missing or invalid configuration does not prevent website startup. Status returns `configured:false, available:false` with a safe explanatory message. Other operations return `503`. Availability checks the authenticated upload listing, not merely a public health endpoint.

## Routes

All routes require the existing `Admin` role; staff permissions do not grant access. JSON uses the existing `ApiResponse<T>` shape (`success`, `data`, `message`, `errors`). Responses are not cacheable.

| GET route under `/api/admin/center-desktop` | Result |
|---|---|
| `/status` | `{configured,available,message}` |
| `/uploads?limit=50&after=<UUID>` | `{uploads:[receipt],nextCursor}` |
| `/uploads/{id}/diagnostics?limit=200` | `{receipt,kind,events,total,truncated}` |
| `/uploads/{id}/download` | Streamed JSON attachment; no ApiResponse envelope |
| `/releases` | `{releases:[{platform,role,status,manifest}]}` |

Limits are 1–500; identifiers and cursors must be UUIDv4 values in hyphenated form (uppercase input is normalized for service lookup). There is no user-supplied upstream target. An upload receipt contains `receiptId`, `uploadId`, `centerId`, `sha256`, `bundleSha256`, `receivedAt`, `createdAt`, `size`, and `app:{version,build,role,os}`. It contains no student records or snapshot data.

Diagnostics contain only the typed sanitized event fields: `schema`, `kind`, `id`, `session`, `time`, `version`, `platform`, `operation`, optional `build`/`role`, `errors:[{type,code}]`, and `frames:[{file,frame,line,column}]`. Arbitrary upstream properties, messages, database fields, and error text are not forwarded. The service performs its own event allowlist and bundle integrity verification before responding. Latest events retain source order; `total` is the service's total sanitized event count and `truncated` indicates events outside the requested limit.

Release results cover four platform/role slots: `windows-x64` and `macos-arm64`, each with `host` and `client`. Status is `available`, `missing`, or `invalid`. Only an available slot has a manifest containing `releaseId`, `version`, `build`, `platform`, `role`, `size`, `sha256`, `downloadPath`, and optional `notes`. This endpoint lists metadata; it neither installs nor deploys a release.

The full uploaded logical database is accessible **only** through the explicit admin download route. The adapter streams at most 128 MiB, adds an attachment filename and `nosniff`, and never buffers it into listing/detail responses. A transfer failure after streaming starts aborts the response; a partial file is not a valid complete backup.

## Failure and verification boundaries

Bad queries return `400` without contacting support. Missing files return `404`. Upstream redirects, rejected authentication, malformed/oversized/non-JSON responses, and connection failures produce generic `502` messages; upstream timeouts produce `504`. Upstream response bodies and configuration secrets are not included in errors. JSON reads are capped at 8 MiB and 20 seconds; private downloads have a two-minute overall limit. Caller cancellation reaches upstream reads and transfers.

Focused tests are in `backend/tests/NaderGorge.Integration.Tests/CenterDesktop/CenterDesktopSupportTests.cs`. They use real MVC/JWT/role middleware with only the external support HTTP boundary replaced; no production database or external upload is involved. Deployment and live service availability require the separate release gates.
