# Operations Master Plan

**Last Updated**: 2026-06-04

---

## Active Plans

### Student Forgot Password Deployment (2026-06-04)
- [x] No database migrations required (uses existing `User` and `StudentProfile` schemas).
- [x] No new environment variables required (reuses existing JWT configuration).
- [x] Verify build and tests via:
  - Frontend: `npm run lint` and `npm run build`
  - Backend: `dotnet build` and `dotnet test`

---

## History
- Initialized Ops master plan directory.
