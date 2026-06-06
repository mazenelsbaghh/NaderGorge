# Research: Deep Technical Audit Remediation

## Decision: Worker Proxy Authorization

- **Decision**: The Next.js worker proxy will validate the browser's bearer token against the backend and require `Admin` or `Teacher` before using the server-side worker admin token.
- **Rationale**: Next.js route handlers are not the source of truth for user roles. Calling the backend preserves existing JWT validation, role names, expiry checks, and account-state logic.
- **Alternatives considered**: Decode JWT directly in Next.js using the shared secret. Rejected because it duplicates token validation and risks drift.

## Decision: QR Redemption

- **Decision**: Keep `/api/qr/[codeHash]` as a compatibility redirect, but perform redemption on a client page `/qr/[codeHash]` using the current auth store and code service.
- **Rationale**: The app stores access tokens in browser storage. A server route cannot read that state, so server-side auto-redeem will keep failing without a broader HttpOnly cookie migration.
- **Alternatives considered**: Move all auth to cookies now. Deferred as a larger auth architecture change.

## Decision: Code and Balance Race Protection

- **Decision**: Use explicit EF transactions and conditional database updates for one-time code consumption and balance debit.
- **Rationale**: Read-modify-save is not sufficient for paid access state. Conditional updates give a clear affected-row check under concurrency.
- **Alternatives considered**: Only add optimistic row version columns. Useful long-term, but conditional updates are lower-risk and directly address current race paths.

## Decision: Watch Progress Trust Model

- **Decision**: Accept only plausible watch deltas based on server-known last progress time, cap single updates, and lock at `>= MaxWatchCount`.
- **Rationale**: The client can be modified. Server-side elapsed validation prevents rapid forged unlocks while allowing normal delayed sync.
- **Alternatives considered**: Require every progress update to include a playback session id. Stronger, but requires broader frontend/backend contract change and is kept as a future strengthening step.

## Decision: Video Embed Secret Exposure

- **Decision**: Iframe URLs will carry only an opaque session id. The Next route will fetch encrypted playback material server-side from the backend using an internal token.
- **Rationale**: Query strings are copied into logs/history/referrers. Session id exposure is still sensitive, but it is short-lived and not directly decryptable.
- **Alternatives considered**: Store token/key in cookies. Rejected because it complicates multi-video playback and still exposes material to the browser context.

## Decision: Internal Callback and E2E Visibility

- **Decision**: Convert custom token checks to reusable filters/attributes and update endpoint inventory classification.
- **Rationale**: Security tooling can recognize reusable attributes more easily than hidden method-level checks.
- **Alternatives considered**: Keep inline checks and add comments. Rejected because the audit explicitly flags tooling blind spots.

## Decision: UI/UX Remediation Scope

- **Decision**: Apply product-register hardening to touched screens only: QR redemption, admin worker controls, and video player messaging.
- **Rationale**: The task is technical remediation. Broad visual redesign would add risk and make verification harder.
- **Alternatives considered**: Full normalize/arrange pass. Deferred because the audit lists it as P3 and not all card/glass debt is in touched files.
