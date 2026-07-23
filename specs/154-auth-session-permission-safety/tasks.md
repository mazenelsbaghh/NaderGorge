# Tasks: Authentication, Sessions, and Permission Safety

**Input**: Design documents from `specs/154-auth-session-permission-safety/`  
**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/`, `quickstart.md`

**Tests**: Mandatory because this phase changes authentication, authorization, data persistence, frontend session behavior, and public report security.

## Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`speckit-specify`) completed in `specs/154-auth-session-permission-safety/spec.md`
- [x] Phase 2: Arabic Clarification (`speckit-clarify`) completed with no additional critical questions in `specs/154-auth-session-permission-safety/spec.md`
- [x] Phase 3: Technical Planning (`speckit-plan`) completed in `specs/154-auth-session-permission-safety/plan.md`
- [x] Phase 4: Detailed Task Breakdown (`speckit-tasks`) completed in `specs/154-auth-session-permission-safety/tasks.md`

## Phase 1: Setup and Existing Completed Surface

**Purpose**: Identify already-completed prerequisites and prepare exact test surfaces before behavior changes.

- [x] T001 Confirm existing password reset session claim exists in `backend/src/NaderGorge.Infrastructure/Services/TokenService.cs` as `passwordResetVersion`
- [x] T002 Confirm existing reset-password refresh-token revocation exists in `backend/src/NaderGorge.Application/Features/Auth/Commands/ResetPasswordCommand.cs`
- [x] T003 Confirm parent report links already use signed HMAC payloads in `backend/src/NaderGorge.API/Controllers/ParentController.cs`
- [x] T004 [P] Create backend auth/session tests file `backend/tests/NaderGorge.Application.Tests/Auth/AuthSessionSafetyTests.cs` with failing tests for disabled refresh, stale password version, stale security version, and device-revoked refresh
- [x] T005 [P] Create frontend auth hydration test coverage in `frontend/tests/e2e/auth.spec.ts` for empty browser token storage plus valid refresh cookie expected result: user reaches the intended protected surface without login loop
- [x] T006 [P] Extend parent report E2E coverage in `frontend/tests/e2e/parent-report.spec.ts` for expired token denial and referrer policy expected result: no student data appears after expiration

---

## Phase 2: Foundational Backend Session Versioning

**Purpose**: Add durable account-state versioning and token validation that blocks every user story until complete.

- [x] T007 Add `public int SecurityStampVersion { get; set; } = 0;` to `backend/src/NaderGorge.Domain/Entities/User.cs`
- [x] T008 Add EF mapping for `SecurityStampVersion` in `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs` with default value `0`
- [x] T009 Create EF migration in `backend/src/NaderGorge.Infrastructure/Migrations/` adding non-null `SecurityStampVersion` integer to users with default `0`
- [x] T010 Update `backend/src/NaderGorge.Domain/Interfaces/ITokenService.cs` contract comments or signature usage notes so generated access tokens include both `passwordResetVersion` and `securityStampVersion`
- [x] T011 Update `backend/src/NaderGorge.Infrastructure/Services/TokenService.cs` to add `securityStampVersion` claim beside `passwordResetVersion`
- [x] T012 Add `backend/src/NaderGorge.Application/Common/ForbiddenException.cs` with message constructor and no dependency on ASP.NET Core
- [x] T013 Update `backend/src/NaderGorge.API/Middleware/ExceptionHandlingMiddleware.cs` to map `ForbiddenException` to HTTP 403 using `ApiResponse.Fail(ex.Message)`

**Checkpoint**: Backend can persist and issue security version claims, and forbidden failures have a 403 type.

---

## Phase 3: User Story 1 - Stop Invalid Long-Lived Sessions (Priority: P1)

**Goal**: Old access and refresh sessions stop after inactive account, password reset, role/security change, or device revocation.

**Independent Test**: Run `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter "FullyQualifiedName~AuthSessionSafetyTests"` and expect disabled refresh, stale version, and device-revoked cases to pass.

### Tests for User Story 1

- [x] T014 [P] [US1] Implement `DisabledUser_CannotRefresh` test in `backend/tests/NaderGorge.Application.Tests/Auth/AuthSessionSafetyTests.cs` expected result: `RefreshTokenCommandHandler` throws `UnauthorizedAccessException` and no replacement token is stored
- [x] T015 [P] [US1] Implement `AccessToken_Fails_WhenPasswordResetVersionChanged` test in `backend/tests/NaderGorge.Application.Tests/Auth/AuthSessionSafetyTests.cs` expected result: validation helper rejects token with old `passwordResetVersion`
- [x] T016 [P] [US1] Implement `AccessToken_Fails_WhenSecurityStampVersionChanged` test in `backend/tests/NaderGorge.Application.Tests/Auth/AuthSessionSafetyTests.cs` expected result: validation helper rejects token with old `securityStampVersion`
- [x] T017 [P] [US1] Implement `DeviceRevocation_RevokesMatchingRefreshTokens` test in `backend/tests/NaderGorge.Application.Tests/Auth/AuthSessionSafetyTests.cs` expected result: refresh tokens with the removed fingerprint are revoked

### Implementation for User Story 1

- [x] T018 [US1] Add scoped token validation service or local validation helper in `backend/src/NaderGorge.API/Program.cs` `JwtBearerEvents.OnTokenValidated` that loads the user by claim id with current `IsActive`, `PasswordResetVersion`, and `SecurityStampVersion`
- [x] T019 [US1] Update `backend/src/NaderGorge.API/Program.cs` token validation to call `context.Fail(...)` when user is missing, inactive, missing version claims, or version mismatched
- [x] T020 [US1] Update `backend/src/NaderGorge.Application/Features/Auth/Commands/RefreshTokenCommand.cs` to reject `storedToken.User.IsActive == false` before creating `newAccessToken`
- [x] T021 [US1] Update `backend/src/NaderGorge.Application/Features/Auth/Commands/RefreshTokenCommand.cs` to reject refresh when `DeviceFingerprint` is present and an existing matching `Device` row has `IsActive == false` or is missing after revocation
- [x] T022 [US1] Update `backend/src/NaderGorge.Application/Features/Auth/Commands/ResetPasswordCommand.cs` to increment `SecurityStampVersion` when password reset succeeds, while preserving the existing `PasswordResetVersion += 1`
- [x] T023 [US1] Update `backend/src/NaderGorge.Application/Features/Admin/Commands/UpdateUserRoleCommand.cs` to increment `user.SecurityStampVersion` and revoke all active refresh tokens for the changed user after role replacement
- [x] T024 [US1] Update `backend/src/NaderGorge.Application/Features/Admin/Commands/RemoveDeviceCommand.cs` to revoke active `RefreshToken` rows for the removed user's `DeviceFingerprint` before or during device removal

**Checkpoint**: User Story 1 passes independently when focused backend auth tests pass.

---

## Phase 4: User Story 2 - Preserve Cross-Surface Sessions Without Persistent Access Tokens (Priority: P1)

**Goal**: Frontend restores session from refresh cookie when access-token storage is empty and avoids long-lived persistent bearer token storage.

**Independent Test**: Run `cd frontend && npx playwright test tests/e2e/auth.spec.ts` expected result: empty storage plus valid refresh cookie hydrates user and 403 does not logout.

### Tests for User Story 2

- [x] T025 [P] [US2] Add Playwright scenario in `frontend/tests/e2e/auth.spec.ts` that clears `localStorage` and `sessionStorage`, keeps refresh cookie, opens protected admin/student surface, and expects authenticated content or correct dashboard redirect
- [x] T026 [P] [US2] Add Playwright or unit-style scenario in `frontend/tests/e2e/auth.spec.ts` that simulates 403 API response and expects auth state/session to remain present

### Implementation for User Story 2

- [x] T027 [US2] Create `frontend/src/lib/auth-memory.ts` exporting `getAccessToken`, `setAccessToken`, and `clearAccessToken` for runtime-only bearer token storage
- [x] T028 [US2] Update `frontend/src/stores/auth-store.ts` so `setAuth` writes access token to `auth-memory.ts` and persists only user/non-sensitive compatibility data through `frontend/src/lib/auth-storage.ts`
- [x] T029 [US2] Update `frontend/src/lib/auth-storage.ts` so `persistAuthSession` no longer writes `accessToken` to local/session storage and `readStoredAuth` tolerates user-only legacy payloads
- [x] T030 [US2] Update `frontend/src/services/api-client.ts` request interceptor to read bearer token from `auth-memory.ts` first and only use storage fallback for legacy one-time migration
- [x] T031 [US2] Update `frontend/src/services/api-client.ts` refresh success path to call `setAccessToken(token)` and update Zustand without `replaceStoredTokens(token)`
- [x] T032 [US2] Update `frontend/src/components/layout/AuthBootstrap.tsx` and `frontend/src/stores/auth-store.ts` so empty storage triggers `authService.refresh()` with credentials before setting `isLoading=false`
- [x] T033 [US2] Update `frontend/src/hooks/useLiveSupportHub.ts` and `frontend/src/services/worker-service.ts` to read access tokens from `auth-memory.ts` instead of persistent storage
- [x] T034 [US2] Update `frontend/src/services/api-client.ts` response handling so HTTP 403 shows the existing error toast but never calls `clearAuth()` or redirects to `/login`

**Checkpoint**: User Story 2 passes independently when frontend auth E2E and build pass.

---

## Phase 5: User Story 3 - Deny Unknown or Unauthorized Admin Routes (Priority: P1)

**Goal**: Staff-like users can only open admin routes explicitly allowed by shared permission inventory; unknown routes deny by default.

**Independent Test**: Run `cd frontend && npx playwright test tests/e2e/admin-users.spec.ts` expected result: limited staff cannot open unmapped admin URLs and full admin still can.

### Tests for User Story 3

- [x] T035 [P] [US3] Extend `frontend/tests/e2e/admin-users.spec.ts` with limited assistant direct URL checks for `/admin/finance`, `/admin/reports`, `/admin/hr`, `/admin/operations`, and `/admin/media` expected result: unauthorized page or redirect without protected content
- [x] T036 [P] [US3] Add route inventory unit coverage if frontend test runner supports it, or Playwright smoke in `frontend/tests/e2e/admin-users.spec.ts`, proving navigation-visible route and direct URL use the same permission

### Implementation for User Story 3

- [x] T037 [US3] Create `frontend/src/packages/admin/route-permissions.ts` exporting route patterns and `canAccessAdminRoute(pathname, user)` according to `contracts/admin-route-permissions.md`
- [x] T038 [US3] Update `frontend/src/packages/admin/navigation.tsx` so `adminMenuItems` permission metadata is imported or mirrored by `route-permissions.ts` without divergent strings
- [x] T039 [US3] Update `frontend/src/components/layout/AdminGuard.tsx` to use `usePathname()` and `canAccessAdminRoute` after authentication, redirecting forbidden users to `/admin/unauthorized`
- [x] T040 [US3] Update `frontend/src/app/admin/unauthorized/UnauthorizedPageClient.tsx` only if needed to present a stable forbidden state without login wording

**Checkpoint**: User Story 3 passes independently when limited staff direct URL checks pass.

---

## Phase 6: User Story 4 - Return Correct Authorization Failures (Priority: P2)

**Goal**: Application-level forbidden actions return 403 while unauthenticated failures remain 401.

**Independent Test**: Run `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter "FullyQualifiedName~TaskTests"` expected result: forbidden operations assert `ForbiddenException` or controller 403 mapping.

### Tests for User Story 4

- [x] T041 [P] [US4] Update `backend/tests/NaderGorge.Application.Tests/Operations/TaskTests.cs` forbidden cases to expect `ForbiddenException` for authenticated users lacking permission
- [x] T042 [P] [US4] Add middleware mapping test in `backend/tests/NaderGorge.Application.Tests/Auth/AuthSessionSafetyTests.cs` expected result: `ForbiddenException` produces HTTP 403 response body compatible with `ApiResponse.Fail`

### Implementation for User Story 4

- [x] T043 [US4] Replace forbidden `UnauthorizedAccessException` throws in `backend/src/NaderGorge.Application/Features/Operations/Commands/UpdateTaskStatusCommand.cs` with `ForbiddenException`
- [x] T044 [US4] Replace forbidden `UnauthorizedAccessException` throws in `backend/src/NaderGorge.Application/Features/Operations/Commands/AdminResolveApprovalCommand.cs` with `ForbiddenException`
- [x] T045 [US4] Replace forbidden `UnauthorizedAccessException` throws in `backend/src/NaderGorge.Application/Features/Operations/Commands/AddTaskCommentCommand.cs` with `ForbiddenException`
- [x] T046 [US4] Replace forbidden `UnauthorizedAccessException` throws in `backend/src/NaderGorge.Application/Features/Operations/Queries/GetTaskDetailsQuery.cs` with `ForbiddenException`

**Checkpoint**: User Story 4 passes independently when operations forbidden tests and middleware mapping tests pass.

---

## Phase 7: User Story 5 - Reduce Parent Report Link Leakage (Priority: P2)

**Goal**: Parent report public links remain token-in-URL but become short-lived and use strict referrer policy.

**Independent Test**: Run `cd frontend && npx playwright test tests/e2e/parent-report.spec.ts` expected result: valid token works, expired token denies, referrer policy is strict enough to avoid full URL leakage.

### Tests for User Story 5

- [x] T047 [P] [US5] Extend `backend/tests/NaderGorge.Application.Tests/Parent/GetDetailsTests.cs` or create `backend/tests/NaderGorge.Application.Tests/Parent/ParentReportLinkTests.cs` expected result: generated admin link expiration is measured in hours not days
- [x] T048 [P] [US5] Extend parent report coverage in `backend/tests/NaderGorge.Application.Tests/Parent/GetDetailsTests.cs` and `frontend/tests/e2e/parent-report.spec.ts` expected result: expired or invalid URL token shows denial and no report fields
- [x] T049 [P] [US5] Extend `frontend/tests/e2e/parent-report.spec.ts` expected result: parent report document or response has `Referrer-Policy` set to `no-referrer`

### Implementation for User Story 5

- [x] T050 [US5] Update `backend/src/NaderGorge.API/Controllers/ParentController.cs` `CreateParentReportLink` to issue token expiration from config key `ParentReports:PublicLinkExpirationHours` defaulting to `24` hours
- [x] T051 [US5] Update `backend/src/NaderGorge.API/Controllers/ParentController.cs` response payload from `expiresInDays` to include `expiresAt` and `expiresInHours` while preserving backward-compatible fields only if frontend depends on them
- [x] T052 [US5] Update `backend/src/NaderGorge.API/Controllers/ParentController.cs` `GetSummaryReport` to set response header `Referrer-Policy: no-referrer`
- [x] T053 [US5] Update `frontend/src/app/parent-report/[studentId]/page.tsx` or route metadata mechanism to set `Referrer-Policy` equivalent when served by Next.js, if backend header alone does not cover the rendered page
- [x] T054 [US5] Update `frontend/src/components/admin/CopyParentLinkButton.tsx` to show/copy any changed expiration text without exposing extra token data in visible UI

**Checkpoint**: User Story 5 passes independently when parent report backend/frontend tests pass.

---

## Phase 8: Polish and Cross-Cutting Verification Tasks

**Purpose**: Complete required guard rails and make the feature ready for final phase gates.

- [x] T055 [P] Update `specs/154-auth-session-permission-safety/quickstart.md` only if actual commands differ from planned commands
- [x] T056 [P] Record implementation evidence and any newly discovered warnings in `achievements.md`
- [x] T057 Run `dotnet build backend/src/NaderGorge.API/NaderGorge.API.csproj` expected result: build passes with no new warnings from this feature
- [x] T058 Run `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter "FullyQualifiedName~AuthSessionSafetyTests|FullyQualifiedName~ParentReport|FullyQualifiedName~TaskTests"` expected result: focused backend tests pass
- [x] T059 Run `cd frontend && npm run lint` expected result: lint passes with no new warnings from this feature
- [x] T060 Run `cd frontend && npm run typecheck` if script exists; otherwise record absence in `achievements.md`
- [x] T061 Run `cd frontend && npm run build` expected result: Next.js build passes
- [x] T062 Run `cd frontend && npx playwright test tests/e2e/auth.spec.ts tests/e2e/admin-users.spec.ts tests/e2e/parent-report.spec.ts` expected result: Phase 1 E2E smoke passes or environment blocker is recorded
- [x] T063 Run `docker compose config -q` expected result: compose config validates

---

## Phase 9: Required Final Quality Gates

**Purpose**: Enforce the exact final order required by `speckit-all`.

- [x] T064 Perform deep critique fixes against `specs/154-auth-session-permission-safety/spec.md`, `specs/154-auth-session-permission-safety/plan.md`, and the changed production/test files
- [x] T065 Run `clean-code-guard` against changed production files after deep critique fixes
- [x] T066 Run `test-guard` against changed test files after `clean-code-guard`
- [x] T067 Run feature tests from the final feature test matrix after `test-guard`
- [x] T068 Run final build verification after feature tests: `dotnet build backend/src/NaderGorge.API/NaderGorge.API.csproj`, `cd frontend && npm run build`, and `docker compose config -q`
- [x] T069 Update `achievements.md` with feature tests, guard results, failures fixed, and final readiness

## Dependencies & Execution Order

- Phase 1 setup/test scaffold can start immediately.
- Phase 2 blocks all backend token/session stories.
- US1 blocks reliable US2 refresh-cookie hydration because frontend bootstrap needs trustworthy refresh semantics.
- US3 can proceed after Phase 2 and in parallel with US2 if files do not overlap.
- US4 can proceed after `ForbiddenException` exists in Phase 2.
- US5 can proceed independently after setup because parent report link hardening is isolated.
- Phase 8 requires all selected user stories complete.
- Phase 9 must run in exact order: deep critique fixes, `clean-code-guard`, `test-guard`, feature tests, final build verification.

## Parallel Opportunities

- T004, T005, and T006 can be created in parallel.
- T014, T015, T016, and T017 are independent test cases in one file but should be coordinated to avoid edit conflicts.
- T025 and T026 can be written before US2 implementation.
- T035 and T036 can be written before US3 implementation.
- T047, T048, and T049 can be written before US5 implementation.

## Implementation Strategy

1. Deliver US1 first as the MVP security slice: stale sessions and refresh invalidation.
2. Add US2 to restore cross-surface UX without persistent bearer storage.
3. Add US3 to close direct admin route gaps.
4. Add US4 to correct 401/403 semantics.
5. Add US5 to reduce public parent report leakage.
6. Run Phase 8 verification commands.
7. Run Phase 9 final quality gates in the required order.
