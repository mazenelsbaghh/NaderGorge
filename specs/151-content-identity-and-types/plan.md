# Implementation Plan: Content Identity and Types

**Branch**: `151-content-identity-and-types` | **Date**: 2026-06-29 | **Spec**: [spec.md](spec.md)
**Input**: Clarified feature specification in `specs/151-content-identity-and-types/spec.md`

## Summary

Add immutable globally unique operational codes to lessons, lesson videos, and exams, then replace optional free-text video classification with a persisted administrator-managed `VideoType` catalog. Existing content is backfilled in one migration without changing primary keys or access relationships. Admin content APIs expose codes and typed video data, a dedicated admin-only catalog page manages types, and existing create/edit forms require an active type with explicit loading, empty, and retry states.

## Technical Context

**Language/Version**: C# 13 on .NET 9; TypeScript 5.9 strict on Next.js 16.2.7 and React 19.2.4
**Primary Dependencies**: ASP.NET Core, MediatR, FluentValidation, EF Core 9.0.6, Npgsql 9.0.4, Next.js App Router, Axios, Zustand, Tailwind CSS, Lucide React
**Storage**: PostgreSQL 16 through EF Core migrations
**Testing**: xUnit with EF Core InMemory/SQLite; Playwright Chromium E2E; TypeScript/Next.js build; ESLint
**Target Platform**: Dockerized Linux web application; RTL admin browser UI at 375px through 1440px
**Project Type**: Existing backend API plus Next.js frontend; worker and mobile clients unchanged
**Performance Goals**: Type list and standard content CRUD remain below the constitution's 500ms p95 target; type list is bounded and returned in one ordered query
**Constraints**: No primary-key changes; no access, playback, exam-attempt, purchase, or provider behavior changes; codes are immutable; all schema changes deploy through one forward migration
**Scale/Scope**: All existing and future `lessons`, `lesson_videos`, and `exams`; one small video-type catalog; three admin detail/list surfaces and one new management route

## Constitution Check

- **Modular architecture**: PASS. Domain entities remain in Domain, commands/queries in Application, mapping/migrations in Infrastructure, HTTP contracts in API, and all browser calls in `admin-service.ts`.
- **Provider abstraction**: PASS. Video-provider selection/extraction is unchanged; every provider including Bunny receives the same `VideoTypeId` requirement.
- **Security and audit**: PASS. Type reads require `content.manage`; catalog mutations require the built-in `Admin` role and write `AuditLog` entries. Content codes are response-only.
- **Academic hierarchy**: PASS. Teacher and subject remain inherited through lesson hierarchy; no duplicate ownership fields are added.
- **Frontend reliability/design**: PASS. Existing admin shell, tokens, RTL conventions, Dropdown/NumberField/Button components, Lucide icons, and service layer are reused. The new surface is dense task UI with loading, empty, error, disabled, keyboard-focus, and responsive states.
- **Layer impact**: Backend, frontend, PostgreSQL migration, backend tests, and Playwright E2E change. Worker, parent/payment mobile apps, video provider integrations, and Docker topology do not change.
- **Phase verification**: Required backend tests, frontend lint/build, targeted Playwright, Docker config, migration, service health checks, and manual QA are listed below. Failed gates block the next spec unless the owner explicitly accepts documented risk.
- **Complexity exceptions**: None.

## Project Structure

### Documentation

```text
specs/151-content-identity-and-types/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── admin-content-identity-api.md
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code

```text
backend/src/
├── NaderGorge.Domain/
│   ├── Entities/ContentEntities.cs
│   ├── Entities/ExamEntities.cs
│   └── Interfaces/IAppDbContext.cs
├── NaderGorge.Application/Features/
│   ├── Admin/Commands/AdminContentCommands.cs
│   ├── Admin/Commands/AdminExamCommands.cs
│   ├── Admin/Commands/BunnyUploadCommands.cs
│   ├── Admin/VideoTypes/Commands/*.cs
│   ├── Admin/VideoTypes/Queries/GetVideoTypesQuery.cs
│   └── Content/Queries/GetLessonCockpitQuery.cs
├── NaderGorge.API/Controllers/
│   ├── AdminController.cs
│   └── AdminVideoTypesController.cs
└── NaderGorge.Infrastructure/
    ├── Data/AppDbContext.cs
    └── Migrations/<timestamp>_AddContentIdentityAndVideoTypes.*

backend/tests/NaderGorge.Application.Tests/
└── ContentIdentityAndVideoTypesTests.cs

frontend/src/
├── app/admin/content/video-types/
│   ├── page.tsx
│   └── VideoTypesPageClient.tsx
├── app/admin/content/AdminContentPageClient.tsx
├── app/admin/content/lessons/[id]/LessonProfilePageClient.tsx
├── app/admin/content/exams/[id]/ExamProfilePageClient.tsx
├── components/admin/
│   ├── AddVideoForm.tsx
│   ├── LessonVideoList.tsx
│   └── VideoTypeSelect.tsx
├── hooks/useVideoTypes.ts
└── services/admin-service.ts

frontend/tests/e2e/admin-content.spec.ts
```

**Structure Decision**: Extend the established Admin/Content CQRS and admin shell boundaries. The type catalog is a focused feature folder in Application and a dedicated admin route in the frontend; existing content commands and cockpit DTOs are modified in place because they own video creation and display.

## Implementation Design

## Phase 0 Research Output

All unknowns and alternatives are resolved in [research.md](research.md), including code generation, cross-kind uniqueness, catalog persistence, legacy mapping, authorization, audit behavior, UI architecture, migration rollout, and verification strategy.

## Phase 1 Design Output

- [data-model.md](data-model.md) defines fields, constraints, relationships, lifecycle, seed data, and migration order.
- [contracts/admin-content-identity-api.md](contracts/admin-content-identity-api.md) defines API authorization, requests, responses, and error behavior.
- [quickstart.md](quickstart.md) defines build, migration, automated, Docker, and manual acceptance flows.

### Internal Codes

- Add required `InternalCode` to `Lesson`, `LessonVideo`, and `Exam`, maximum length 40.
- Canonical format is `LES-{Id:N}`, `VID-{Id:N}`, or `EXM-{Id:N}`. The three disjoint prefixes provide cross-kind uniqueness; each table also has a unique index.
- `AppDbContext.SaveChangesAsync` assigns missing codes for newly added entities and rejects modifications to persisted internal codes. Requests never contain `InternalCode`.
- The migration adds nullable columns, backfills from existing GUID primary keys, validates non-null/uniqueness, then applies required columns and unique indexes.
- Existing IDs remain the only foreign keys. Purchase, access, watch, and exam attempt logic is unchanged.

### Video Types

- Add `VideoType` with `Name`, `NormalizedName`, `SortOrder`, `IsActive`, and timestamps. `NormalizedName` has a unique index.
- Add required `LessonVideo.VideoTypeId` with `Restrict` delete behavior and navigation.
- Seed active Arabic defaults: `شرح`, `واجب`, `مراجعة`, `امتحان`. Seed inactive fallback `غير مصنف` for unmatched or empty legacy tags.
- Migration maps normalized legacy `VideoTag` values to defaults and maps all remaining values to fallback. Keep `VideoTag` for compatibility during this spec; new writes use `VideoTypeId`.
- Catalog create/rename normalizes trimmed names using one application helper; collisions return validation failure. Reorder accepts an explicit integer and listing sorts by `SortOrder`, then name.
- Deactivation is allowed with assignments. Deletion is allowed only for unused non-required rows; assigned rows return a conflict-style failure instructing deactivation.

### Authorization And Audit

- `GET /api/admin/video-types` requires `content.manage` and may include inactive rows for edit forms.
- POST/PUT/PATCH/DELETE catalog operations require the built-in `Admin` role at the controller action and preserve handler-level validation.
- Successful create, rename/reorder, activation changes, deletion, and blocked assigned deletion add `AuditLog` records with actor, target, and serialized before/after state in the same unit of work where persistence occurs.
- Existing content create/edit permissions remain `content.manage` plus `TeacherAuthorizationService` ownership checks.

### UI Architecture

- Add `/admin/content/video-types` using `AdminShellChrome`; only users whose stored roles include `Admin` see the management entry and non-admin route access redirects to `/admin/unauthorized`.
- Use a compact table/list, inline create form, icon actions with tooltips, status text plus color, and explicit save/cancel commands. Avoid nested cards and decorative motion.
- `useVideoTypes(includeInactive)` owns loading/error/retry and exposes ordered data. `VideoTypeSelect` presents accessible labels and disables submission while unavailable.
- Add form requires an active type for manual and Bunny uploads. Edit form includes the current inactive type but only permits replacement with an active type.
- Cockpit DTO returns lesson code plus video code and type summary. Exam dashboard returns exam code. Codes render read-only with a copy icon and tooltip.
- Preserve current Navy/Teal admin tokens, Tajawal typography, RTL layout, 44px mobile controls, focus rings, and 150-250ms state transitions.

## Failure Modes And Rollout

- Migration is transactional. If seed, backfill, FK, or unique-index creation fails, no partial schema is committed.
- Duplicate normalized legacy labels do not create rows; only the fixed defaults plus fallback are seeded.
- No active type means create controls are disabled with retry/navigation guidance; API remains authoritative and rejects invalid IDs.
- Concurrent duplicate type creation is caught by the unique index and returned as a validation error.
- Existing inactive assignment is retained during unrelated video edits; changing the type requires an active target.
- Rollback drops the FK/columns/table and internal-code columns/indexes but cannot restore post-migration catalog edits; database backup remains the operational rollback for production data.

## Phase Closure & Verification Plan

**Automated Tests Required**:

- `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter ContentIdentityAndVideoTypesTests`
- `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj`
- `dotnet build backend/NaderGorge.sln`
- `npm run lint -- --file src/app/admin/content/video-types/VideoTypesPageClient.tsx --file src/components/admin/AddVideoForm.tsx --file src/components/admin/LessonVideoList.tsx --file src/components/admin/VideoTypeSelect.tsx --file src/hooks/useVideoTypes.ts --file src/services/admin-service.ts` from `frontend/`, falling back to full `npm run lint` if file flags are unsupported.
- `npm run build` from `frontend/`.
- `npx playwright test tests/e2e/admin-content.spec.ts --project=chromium` from `frontend/` after Docker is healthy.

Critical coverage: global code uniqueness and immutability, migration-compatible assignment, default/fallback mapping, normalized type uniqueness, active/inactive rules, assigned deletion denial, non-admin mutation denial, required type on manual/Bunny create and edit, read-only code display, and existing content flow regression.

**Docker Gate Required**:

1. `docker compose config -q`
2. `make up`
3. `make migrate`
4. `make ps`
5. `curl -f http://localhost:5245/api/health`
6. `curl -f http://localhost:3001/ui`
7. `curl -f http://localhost:8738`
8. Browser smoke for `/admin/content/video-types` and a representative lesson profile.

**Manual QA Required** (`pending` until performed):

- Admin at `/admin/content/video-types`: create, rename, reorder, deactivate, reactivate, and delete an unused type; verify an assigned type cannot be deleted.
- Admin at a lesson profile: create manual and Bunny videos with a type, copy the generated code, edit title/provider/type, and verify the code remains unchanged.
- Admin at an exam profile: verify the exam code is visible and read-only.
- Non-admin with `content.manage`: use active types in permitted content flows but receive denial for catalog mutation and direct catalog route.
- Existing content sample: verify migrated lesson/video/exam codes, mapped type, playback, lesson access, exam attempt, and code redemption.

**End-of-Phase Report Format**: implemented scope; changed files; migration result; exact test/build/Docker commands and outcomes; failures found and fixed; clean-code-guard result; test-guard result; manual QA checklist with pending/completed state; residual risks; go/no-go for spec 152.

## Complexity Tracking

No constitution violations or complexity exceptions are required.
