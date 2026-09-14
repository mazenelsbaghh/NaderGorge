# Contract: Phase 1 E2E Auth Surface

## Local Domain Strategy

Use one same-site local domain family for browser E2E:

- Student/app: `http://app.lvh.me:3000`
- Admin: `http://admin.lvh.me:3000`
- Staff/assistant: `http://staff.lvh.me:3000`
- Teacher: `http://teacher.lvh.me:3000`
- Backend API: `http://api.lvh.me:5245/api`
- Refresh cookie domain in E2E: `.lvh.me`

## Required Backend Environment For E2E

- `ASPNETCORE_ENVIRONMENT=E2e`
- `CookieSettings__Domain=.lvh.me`
- `Cors__AllowedOrigins` includes all E2E frontend origins.
- `E2E_TEST_TOKEN` matches Playwright global setup.

## Required Frontend Environment For E2E

- `NEXT_PUBLIC_API_URL=http://api.lvh.me:5245/api`
- `NEXT_PUBLIC_BACKEND_URL=http://api.lvh.me:5245`
- Next dev server listens on port `3000` for Playwright.

## Expected Browser Outcomes

- A login response can set a refresh cookie that is later sent to `/api/auth/refresh`.
- Clearing `localStorage` and `sessionStorage` does not force logout when the refresh cookie is valid.
- Assistant/staff users opening unmapped admin URLs see `/admin/unauthorized` or login denial without protected content.
- Parent report invalid/expired tokens do not reveal student data.
