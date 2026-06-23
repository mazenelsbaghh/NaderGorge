# Implementation Plan: Live Support AI Refinements and Performance Dashboard

**Branch**: `144-ai-live-support-refinements` | **Date**: 2026-06-23 | **Spec**: [spec.md](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/specs/144-ai-live-support-refinements/spec.md)
**Input**: Feature specification from `/specs/144-ai-live-support-refinements/spec.md`

## Summary

This plan outlines the design and implementation for repairing the database 500 error on policy publish, adding play/stop controls for the active AI policy version, introducing a visual glowing/pulsating activity indicator, and developing a statistics panel for AI support performance with predefined time range filters.

## Technical Context

- **Language/Version**: C# 13 (.NET 9), TypeScript (Next.js 16.2.1 / React 19)
- **Primary Dependencies**: ASP.NET Core Web API, Entity Framework Core 9.0.6, Axios, Lucide React, Cairo font
- **Storage**: PostgreSQL (existing tables: `live_support_ai_policy_versions`, `live_support_ai_conversation_states`, `live_support_messages`, `live_support_ai_pending_actions`)
- **Testing**: xUnit (`dotnet test`)
- **Target Platform**: Linux Docker / Local development
- **Project Type**: Web application (backend service + Next.js frontend proxy/page)
- **Performance Goals**: Stats retrieval < 1.5s, UI toggles < 1s
- **Constraints**: No database schema migrations required; adhere to RTL-first Cairo typography.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Layer impact**:
  - Backend: `LiveSupportAIAdminController`, `LiveSupportAIAdminService`, `ILiveSupportAIAdminService`, DTOs.
  - Frontend: `live-support-ai-service`, `AdminAISupportPageClient`.
  - Database & Docker: No schema changes, no migration additions.
- **Automated tests**: Backend tests under `backend/tests/NaderGorge.Application.Tests/LiveSupportAI/` to verify endpoint authorization and stats logic.
- **Manual QA flows**: Toggling active state, publishing policies, date range dropdown filtering, visual glow verification.
- **Docker gate commands**: `docker compose config -q`, `make up`.

## Project Structure

### Documentation (this feature)

```text
specs/144-ai-live-support-refinements/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── checklists/
│   └── requirements.md  # Spec quality checklist
└── contracts/
    └── api.md           # API Contracts
```

### Source Code (repository root)

```text
backend/
├── src/
│   ├── NaderGorge.API/
│   │   └── Controllers/
│   │       └── LiveSupportAIAdminController.cs
│   ├── NaderGorge.Application/
│   │   └── Features/LiveSupportAI/
│   │       ├── Dtos/
│   │       │   └── LiveSupportAIDtos.cs
│   │       └── Interfaces/
│   │           └── ILiveSupportAIAdminService.cs
│   └── NaderGorge.Infrastructure/
│       └── Services/
│           └── LiveSupportAIAdminService.cs
└── tests/
    └── NaderGorge.Application.Tests/
        └── LiveSupportAI/
            └── AIAdminAuthorizationTests.cs

frontend/
├── src/
│   ├── app/
│   │   └── admin/
│   │       └── live-support/
│   │           └── ai/
│   │               └── AdminAISupportPageClient.tsx
│   └── services/
│       └── live-support-ai-service.ts
```

**Structure Decision**: Standard C# / Next.js web application structure. Changes are localized to the Live Support AI feature folders.

## Phase Closure & Verification Plan

**Automated Tests Required**:
- `dotnet test backend/tests/NaderGorge.Application.Tests/LiveSupportAI/` covering authorization, policy enabling, and stats query logic.

**Docker Gate Required**:
- `docker compose config -q`
- Run `make up` to verify container orchestration.

**Manual QA Required**:
- Admin goes to `/admin/live-support/ai`.
- Publishes policy → verifies success (no 500 error).
- Disables policy → verifies gray status.
- Enables policy → verifies green pulsating indicator.
- Clicks "الإحصائيات والأداء" → verifies 5 stats metrics.
- Changes period preset to "آخر 7 أيام" → verifies stats update.

**End-of-Phase Report Format**:
- Summary of fixes and features.
- Verification commands run and test output.
- Screenshots of UI tabs and active state pulse.
- Validation checklists complete.
