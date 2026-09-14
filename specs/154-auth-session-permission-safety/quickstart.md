# Quickstart: Authentication, Sessions, and Permission Safety

## 1. Baseline

```bash
git status --short
docker compose config -q
```

Record unrelated dirty files before implementation. Do not revert existing user changes.

## 2. Backend Verification

```bash
dotnet build backend/src/NaderGorge.API/NaderGorge.API.csproj
dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter "FullyQualifiedName~AuthSessionSafetyTests|FullyQualifiedName~ParentReport|FullyQualifiedName~TaskTests"
```

Expected:

- Disabled user refresh is rejected.
- Old token after password reset/security version change is rejected.
- Role change revokes refresh or invalidates old access token.
- Device revocation blocks refresh for that device.
- Forbidden application failure maps to 403.
- Parent report expired/invalid token returns no student data.

## 3. Frontend Verification

```bash
cd frontend
npm run lint
npm run build
```

`npm run typecheck` is not defined in the current `frontend/package.json`; rely on `npm run build` for TypeScript compile verification unless the script is added later.

Expected:

- Auth bootstrap uses refresh-cookie hydration when persistent access token storage is empty.
- Axios does not clear auth state on 403.
- Admin guard denies unmapped routes for staff-like users.

## 4. E2E Smoke

```bash
cd frontend
npx playwright test tests/e2e/auth.spec.ts tests/e2e/admin-users.spec.ts tests/e2e/parent-report.spec.ts
```

Run only when local backend/frontend fixtures are available. Otherwise document the blocker in `achievements.md`.

Current local note: Playwright global setup can seed the E2E backend, but the frontend dev server must be running on `localhost:3000` for `app.localhost`, `admin.localhost`, `teacher.localhost`, and `staff.localhost` before this command can pass.

## 5. Manual QA

- Disable a student, refresh the page, and verify the session cannot renew.
- Reset password for a user and verify old session fails.
- Change a staff user's role/permissions and verify direct admin URLs are denied unless explicitly allowed.
- Clear browser token storage and verify refresh-cookie bootstrap restores session.
- Open valid and expired parent report links and inspect referrer policy.
