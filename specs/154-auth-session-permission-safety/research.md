# Research: Authentication, Sessions, and Permission Safety

## Decision: Add a durable `SecurityStampVersion` on `User`

**Rationale**: `PasswordResetVersion` already invalidates password-reset sessions, but role changes, account disable, and broad security events need one canonical version claim. A user-level integer version is simple, queryable, migration-friendly, and can be incremented whenever sessions must be invalidated.

**Alternatives considered**:

- Reuse `PasswordResetVersion` for every session invalidation: rejected because it conflates password reset with role/device/security changes and weakens audit meaning.
- Add version to `Role`: rejected because permission JSON and user role assignments can change independently; the session belongs to the user.
- Shorten all access tokens dramatically: rejected because long-lived student tokens are an existing product decision and Phase 1 requires account-state validation rather than removing that decision.

## Decision: Validate access tokens in `JwtBearerEvents.OnTokenValidated`

**Rationale**: Token claims are already generated centrally in `TokenService`. `OnTokenValidated` is the correct API boundary to reject stale/inactive users before controllers and MediatR handlers run. It prevents every protected endpoint from re-implementing account-state checks.

**Alternatives considered**:

- Validate only inside refresh: rejected because long-lived access tokens can continue without refresh.
- Validate only inside route guards: rejected because frontend guards are not authorization boundaries.
- Custom middleware after authorization: rejected because JWT bearer already has the validated claims and failure semantics.

## Decision: Reject inactive refresh and revoke active refresh tokens on security events

**Rationale**: `RefreshTokenCommand` currently atomically revokes the presented refresh token, then loads the user. It must also reject inactive users and inactive devices before minting a new token. Existing reset-password flow already revokes refresh tokens; role changes and device removals must do the same for affected users/devices.

**Alternatives considered**:

- Let access-token validation catch inactive users after refresh: rejected because refresh would still mint new tokens and confuse clients.
- Store global revoked-before timestamp only: rejected because device-level revocation needs per-device behavior.

## Decision: Keep parent report URL token, shorten lifetime, and set strict referrer policy

**Rationale**: The user explicitly chose this Phase 1 tradeoff. `ParentController` already uses signed HMAC tokens with expiration; the main gap is a seven-day default and policy strength. The plan will shorten issued links and add endpoint/page referrer policy so token-bearing URLs do not leak as full referrers.

**Alternatives considered**:

- Exchange URL token to HttpOnly cookie: rejected for this phase by user decision.
- Require parent login: rejected because it changes the product flow.

## Decision: Frontend access token moves to in-memory runtime state with bounded compatibility fallback

**Rationale**: `api-client.ts` and SignalR hooks currently read access tokens from browser storage. A minimal `auth-memory.ts` module lets Axios, stores, and hub factories share runtime access tokens without persistent storage. User data can remain in storage only if needed for non-sensitive UX, but the bearer token should not be persisted after bootstrap/login.

**Alternatives considered**:

- Remove all browser auth storage immediately: rejected as risky for cross-surface compatibility and existing boot behavior.
- Keep local/session access token and only rely on CSP: rejected because P2-1 specifically requires reducing browser storage token exposure.

## Decision: Admin route authorization uses shared route inventory derived from navigation plus explicit extra routes

**Rationale**: `adminMenuItems` already declares most visible route permissions, but direct routes include detail pages and hidden tools. A shared `route-permissions.ts` can export exact route patterns and helper functions used by both navigation filtering and `AdminGuard`. Unknown `/admin/*` routes deny for staff-like users by default; full admins remain allowed.

**Alternatives considered**:

- Manually patch only known missing pages in the guard: rejected because future pages can regress.
- Backend-only controller authorization: required where available, but it does not protect client-rendered page shells and direct URL data fetching in Next.js.

## Decision: Add `ForbiddenException` and map it to 403

**Rationale**: Current middleware maps all `UnauthorizedAccessException` to 401. Some application commands use that exception for "authenticated but not allowed." A dedicated exception gives clear semantics and prevents frontend from clearing sessions on forbidden behavior.

**Alternatives considered**:

- Parse exception messages: rejected as brittle and localization-hostile.
- Convert all handlers immediately: too broad for Phase 1; start with affected high-risk auth/admin/operations paths and document remaining candidates.

## Decision: No worker changes

**Rationale**: Phase 1 auth/session/permission safety does not touch Node worker queues or AI jobs. Worker commands remain outside scope except frontend proxy auth behavior if a staff token is needed.

**Alternatives considered**:

- Add worker auth tests: rejected because worker admin hardening belongs to Phase 3 of the remediation plan.
