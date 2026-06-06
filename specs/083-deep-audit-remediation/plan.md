# Implementation Plan: Deep Technical Audit Remediation

**Branch**: `083-deep-audit-remediation` | **Date**: 2026-06-06 | **Spec**: [spec.md](./spec.md)  
**Input**: Feature specification from `specs/083-deep-audit-remediation/spec.md`

## Summary

Remediate every verified issue in `docs/deep-technical-audit-2026-06-06.md` with a staged, low-risk implementation:

1. Fix broken P0 workflows: worker proxy auth, QR redemption, and homework routes.
2. Add transactional/atomic protections for code redemption, balance purchases, and watch progress.
3. Harden video embed/session handling, internal callbacks, E2E controls, worker logs, token defaults, Redis/Docker setup, and password policy consistency.
4. Strengthen quality gates and touched UI/service code while preserving Massar Academy's existing product design system.

## Technical Context

**Language/Version**: C# 13 / .NET 9 backend, TypeScript 5.x / Next.js 16.2.x / React 19 frontend, Node.js 20 worker, Python 3 tests  
**Primary Dependencies**: ASP.NET Core authorization/filter pipeline, EF Core 9, MediatR, Axios service layer, Next.js App Router route handlers, BullMQ/Express worker, Pytest  
**Storage**: PostgreSQL through EF Core, Redis for worker queues, local/Docker named volumes  
**Testing**: `dotnet test`, `dotnet build`, frontend `npm run lint && npm run build`, worker `npm run build`, endpoint inventory script, Pytest smoke tests  
**Target Platform**: Docker and native local development for landing/student/admin surfaces plus backend/worker services  
**Project Type**: Multi-surface education web platform with API and background worker  
**Performance Goals**: Auth/worker proxy validation adds no extra browser round trip beyond the proxied call; atomic update operations remain single-row or single-transaction operations; endpoint inventory remains sub-second on current controllers  
**Constraints**: Preserve existing App Router paths, keep backend Application layer ORM-specific use within the existing `IAppDbContext` pattern, avoid disruptive frontend redesign, do not change existing brand identity tokens  
**Scale/Scope**: 30 audit findings across frontend, backend, worker, Docker/Ops, tests, and UI quality

## Constitution Check

- **Modular Clean Architecture**: PASS. Backend business fixes remain in MediatR handlers/services; API-only concerns use controllers/filters; Infrastructure provides transaction support behind `IAppDbContext`.
- **Provider Abstraction First**: PASS. Video provider logic remains provider-neutral; embed/session hardening changes the session boundary, not provider implementations.
- **Security & Access Control by Default**: PASS. Worker proxy validates real user auth/roles, internal callbacks become auditable filters, unsafe defaults fail closed, and secrets are removed from browser-visible URLs.
- **Academic Content Integrity**: PASS. Watch tracking becomes more truthful and progression locks trigger at the configured threshold.
- **Frontend Clean Code**: PASS. Worker fetches move into a dedicated service helper; QR flow moves into a focused route/page; touched components keep state responsibilities small.
- **Premium Editorial Design System / UI-UX**: PASS. `impeccable` product register and `ui-ux-pro-max` checks apply: dense admin UI, Arabic-first student UI, 4.5:1 contrast target, visible focus, no new decorative glass/card-heavy patterns, stable responsive controls.

## UI/UX Planning Notes

- Existing Massar `PRODUCT.md`/`DESIGN.md` override generic recommendations. Keep Tajawal/Montserrat and Navy/Teal/Gold tokens.
- Admin AI monitor and lesson video controls remain dense product UI. Add no hero-scale typography, no decorative gradients, no nested cards.
- QR redemption page must be direct and mobile-safe: clear loading/error/success states, 44px touch targets, visible focus, and no horizontal overflow.
- Any added buttons use existing component vocabulary and lucide icons only when an icon materially improves recognition.

## Project Structure

### Documentation (this feature)

```text
specs/083-deep-audit-remediation/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── api-contract.md
│   └── ui-contract.md
└── tasks.md
```

### Source Code (repository root)

```text
backend/src/NaderGorge.API/
├── Configuration/
│   ├── E2eOnlyAttribute.cs
│   └── InternalTokenAuthorizeAttribute.cs
├── Controllers/
│   ├── AuthController.cs
│   ├── E2eTestingController.cs
│   ├── InternalController.cs
│   └── VideoSessionController.cs
└── Program.cs

backend/src/NaderGorge.Application/
├── Common/PasswordPolicy.cs
├── Features/Admin/Commands/AdminResetPasswordCommand.cs
├── Features/Codes/Commands/ActivateCodeCommand.cs
├── Features/Student/Commands/
│   ├── ConsumeVideoSessionCommand.cs
│   ├── CreateVideoSessionCommand.cs
│   ├── PurchaseContentCommand.cs
│   └── TrackWatchProgressCommand.cs
└── Services/BalanceService.cs

backend/src/NaderGorge.Domain/
├── Entities/TrackingEntities.cs
└── Interfaces/IAppDbContext.cs

backend/src/NaderGorge.Infrastructure/
├── Data/AppDbContext.cs
└── Migrations/

frontend/src/
├── app/api/qr/[codeHash]/route.ts
├── app/api/video/embed/route.ts
├── app/api/worker/[...path]/route.ts
├── app/qr/[codeHash]/page.tsx
├── components/admin/LessonVideoList.tsx
├── components/layout/AdminGuard.tsx
├── components/video/SecureVideoPlayer.tsx
├── services/homework-service.ts
├── services/video-session-service.ts
└── services/worker-service.ts

worker/src/
├── index.ts
├── logging.ts
└── services/geminiService.ts

scripts/
└── generate-endpoint-inventory.mjs

tests/
├── requirements.txt
├── test_codes.py
├── test_purchases.py
├── test_video.py
└── test_endpoint_inventory.py

docker-compose.yml
Makefile
```

**Structure Decision**: Use the existing backend/frontend/worker/test layout. Frontend fixes use feature service helpers and focused route components rather than broad package moves because the repo already has surface package boundaries from spec 082.

## Phase 0 Research Summary

See [research.md](./research.md). Key decisions:

- Validate worker proxy users by calling backend auth state, not by trusting browser headers.
- Use client-side QR redemption page as the current auth model stores tokens in browser storage.
- Use EF transactions plus conditional updates for code and balance state.
- Hide video token/key from query strings by using session id in the iframe URL and server-side material lookup.
- Keep UI hardening restrained and product-register aligned.

## Phase 1 Design Artifacts

- [data-model.md](./data-model.md)
- [contracts/api-contract.md](./contracts/api-contract.md)
- [contracts/ui-contract.md](./contracts/ui-contract.md)
- [quickstart.md](./quickstart.md)

## Risk Decisions

- Full refresh-token migration to HttpOnly cookies is a larger auth architecture change. This plan fixes the audited QR breakage using a client redemption page and documents the cookie migration as a follow-up unless implementation can be completed safely within current auth contracts.
- Build-time Next route pruning for surface separation is out of scope for a single remediation pass; this plan closes direct `/api/worker` exposure and keeps surface separation tests/gates active.
- Some UI debt is widespread. This plan only revises touched screens and adds checklist gates, avoiding a broad redesign that would obscure security/data changes.

## Verification

```bash
dotnet build backend/NaderGorge.sln
dotnet test backend/NaderGorge.sln --no-build
cd frontend && npm run lint && npm run build
cd worker && npm run build
python3 -m pip install -r tests/requirements.txt
python3 -m pytest tests/test_endpoint_inventory.py tests/test_codes.py tests/test_purchases.py tests/test_video.py -q
node scripts/generate-endpoint-inventory.mjs --check
docker compose config -q
```

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Additional API filters for internal/E2E auth | Makes custom header security visible and reusable | Inline method checks caused endpoint inventory and review blind spots |
| Video embed material lookup endpoint | Removes browser-visible key/token from iframe URLs | Query string token/key was explicitly audited as sensitive exposure |
