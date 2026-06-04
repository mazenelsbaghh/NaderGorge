# Implementation Plan: Critical Security Hardening

**Branch**: `075-critical-security-hardening` | **Date**: 2026-06-04 | **Spec**: `specs/075-critical-security-hardening/spec.md`

## Summary

Implement the full Phase 1 critical security remediation from `docs/audit-remediation-phase-1-security-critical.md` with a tightly scoped, low-risk approach:

1. Backend startup safety: required secret validation, gated seeding, and `RequireStudent` policy.
2. Backend internal callbacks: no default secrets, constant-time token comparison.
3. Worker service protection: no open CORS in production, token-protected status/cancel/retry/Bull Board.
4. Frontend worker proxy: explicit route allowlist and bearer-token forwarding for worker service authorization.
5. Parent reports: reject raw `studentId` access; require signed expiring token tied to the student.
6. XSS hardening: shared rich-text sanitizer for exam HTML plus text-safe watermark injection.
7. Password reset: revoke all existing refresh tokens after password reset.
8. E2E endpoints: require E2E environment, configured E2E secret, safe test DB marker.

## Technical Context

**Backend**: C# 13 / .NET 9, MediatR, EF Core, PostgreSQL  
**Frontend**: TypeScript strict, Next.js 16.2.1, React 19  
**Worker**: Node.js v20+, Express, BullMQ  
**Security Constraints**: No tracked live secrets, fail closed for unsafe config, no frontend-only authorization boundary.

## Constitution Check

- **Modular Clean Architecture**: Backend helpers remain in API for startup/controller security concerns; Application handlers keep business logic.
- **Security by Default**: All defaults that could become production secrets are removed or rejected.
- **AI Worker Integrity**: Worker control surfaces require service authorization.
- **Frontend Clean Code**: Sanitization is centralized in `frontend/src/lib/sanitize-html.ts`; components call a shared helper rather than duplicating logic.
- **UI/UX References**: This phase does not redesign UI. The relevant impeccable/ui-ux principle is safety and resilience: rich text is preserved only through a strict allowlist, without layout changes.

## Source Code Modifications

```text
backend/src/NaderGorge.API/
├── Configuration/
│   ├── SecurityConfigurationValidator.cs [NEW]
│   └── ServiceTokenValidator.cs [NEW]
├── Controllers/
│   ├── E2eTestingController.cs [MODIFY]
│   ├── InternalController.cs [MODIFY]
│   └── ParentController.cs [MODIFY]
└── Program.cs [MODIFY]

backend/src/NaderGorge.Application/
└── Features/Auth/Commands/ResetPasswordCommand.cs [MODIFY]

frontend/src/
├── app/api/video/embed/route.ts [MODIFY]
├── app/api/worker/[...path]/route.ts [MODIFY]
├── app/admin/content/exams/[id]/dashboard/page.tsx [MODIFY]
├── app/student/mistakes/page.tsx [MODIFY]
├── components/exams/ExamViewer.tsx [MODIFY]
├── components/admin/CopyParentLinkButton.tsx [MODIFY]
├── services/report-service.ts [MODIFY]
└── lib/sanitize-html.ts [NEW]

worker/src/
├── security.ts [NEW]
├── index.ts [MODIFY]
└── jobs/*.ts callback secret fallbacks [MODIFY]

docker-compose.yml [MODIFY]
worker/.env.example [MODIFY]
```

## Implementation Notes

- Parent report token format will be stateless: `base64url(payload).base64url(hmac)`, where payload contains `studentId`, `purpose`, `exp`.
- The API validates parent tokens using `ParentReports:SigningSecret`, which is required outside development.
- E2E controller will require `X-E2E-Token` and a connection string containing an E2E/test database marker before destructive operations.
- Worker service token will use `WORKER_ADMIN_TOKEN`; frontend proxy forwards this as `Authorization: Bearer ...` server-side only.
- Internal callback token will support both `AI_CALLBACK_SECRET` and `API_CALLBACK_SECRET` config keys, but neither can fall back to a default.

## Verification

```bash
dotnet test backend/NaderGorge.sln --no-restore
cd frontend && npm run build && npm run lint
cd worker && npm run build
rg -n "secretxyz|dangerouslySetInnerHTML.*questionText|watermark.innerHTML" backend frontend worker docker-compose.yml worker/.env.example
```
