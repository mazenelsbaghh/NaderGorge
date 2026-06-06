# Implementation Plan: Frontend Surface Packages, Register Branding, and Endpoint Audit

**Branch**: `082-frontend-surface-packages-endpoint-audit` | **Date**: 2026-06-06 | **Spec**: [spec.md](./spec.md)

## Summary

Create explicit frontend surface package boundaries for landing, admin, and student without destabilizing the current dirty worktree; centralize the Masar logo in a reusable shared component and replace hardcoded register/login icon marks; add a source-driven endpoint inventory generator plus committed JSON/Markdown reports and Pytest stale-inventory protection.

## Technical Context

**Language/Version**: TypeScript 5.x / Next.js 16.2.x / React 19, C# 13 / .NET 9, Python 3 / Pytest, Node.js 20  
**Primary Dependencies**: Next.js App Router, lucide-react, next/image, ASP.NET Core controller attributes, Pytest  
**Storage**: N/A for this feature  
**Testing**: `npm run lint`, endpoint inventory generator, `pytest tests/test_endpoint_inventory.py`, backend build if available  
**Target Platform**: Local development and Docker-separated Masar surfaces  
**Project Type**: Multi-surface web platform  
**Performance Goals**: No additional client bundle dependency beyond existing `next/image` and existing components; endpoint inventory generation completes in under one second on current controllers  
**Constraints**: Do not revert existing user changes; avoid mass file moves in dirty worktree; preserve existing App Router paths and Docker runtime separation  
**Scale/Scope**: Three frontend packages, one shared logo component, 20+ controller files and all discovered endpoints

## Constitution Check

- **Modular Clean Architecture**: PASS. Frontend boundaries become package entry points; backend endpoint parsing is test/documentation only.
- **Security & Access Control**: PASS. Endpoint inventory records authorization classification and does not expose secrets.
- **Premium Editorial Design System**: PASS. Shared logo uses existing Masar assets, Cairo/RTL context, and restrained product UI treatment. Impeccable product register is applied to admin/student; brand register is applied to landing.
- **Provider Abstraction First**: PASS. No provider code is changed.
- **Phased Delivery with MVP Discipline**: PASS. Incremental package boundaries avoid unsafe large rewrites.

## UI/UX Planning Notes

- **Impeccable**: Product surfaces remain task-focused and familiar. No new decorative cards or animation systems are introduced. The register logo becomes a stable brand mark with accessible alt text.
- **ui-ux-pro-max**: Applied Arabic education platform recommendations: visible focus states, 4.5:1 text contrast target, mobile responsiveness at 375/768/1024/1440, SVG/image logo instead of emoji-like glyphs, and hover transitions within 150-300ms.
- Existing PRODUCT/DESIGN context overrides generic recommendations where they conflict: keep Cairo and current Masar assets instead of adding new fonts.

## Project Structure

```text
frontend/src/packages/
├── landing/
│   ├── home.tsx
│   └── index.ts
├── admin/
│   ├── navigation.ts
│   └── index.ts
├── student/
│   ├── dashboard.ts
│   └── index.ts
└── brand/
    ├── platform-identity.ts
    └── index.ts

frontend/src/components/shared/
└── PlatformLogo.tsx

scripts/
└── generate-endpoint-inventory.mjs

tests/
├── endpoint_inventory.json
├── endpoint_inventory.md
└── test_endpoint_inventory.py
```

## Implementation Strategy

1. Add brand identity constants and a shared `PlatformLogo` component with `mark` and `full` variants.
2. Replace register hardcoded `𓂀` mark and public nav Sphinx login mark with `PlatformLogo`.
3. Add landing package entry point and update `frontend/src/app/page.tsx` to render through it.
4. Add admin package navigation/root link definitions and update admin layout/root page imports.
5. Add student package dashboard barrel and update student dashboard imports.
6. Add endpoint inventory generator that parses controller-level `[Route]`, method attributes, action names, and auth attributes.
7. Generate committed JSON and Markdown inventories.
8. Add Pytest that compares current parsed endpoints with the committed JSON and validates key fields.

## Verification

- `node scripts/generate-endpoint-inventory.mjs --check`
- `pytest tests/test_endpoint_inventory.py`
- `cd frontend && npm run lint`
- Backend build if local SDK is available and time permits: `dotnet build backend/NaderGorge.sln`

## Risk Notes

- Regex parsing C# attributes is sufficient for current controllers because route attributes use simple string literals. If controllers switch to constants/interpolation, the test will fail and the parser should move to a Roslyn-based tool.
- Existing UI files contain pre-existing design debt such as decorative glow classes; this feature only touches requested logo and package boundaries.
