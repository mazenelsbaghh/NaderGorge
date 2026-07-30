# Implementation Plan: مركز حسابات المدرسين والمالية

**Branch**: `165-teacher-finance-center` | **Date**: 2026-07-24 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `specs/165-teacher-finance-center/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

Extend the existing teacher financial ledger into an admin-only finance center. Add effective-dated teacher agreements, discount-burden snapshots, delivery-confirmed code-batch accounting, settlement/invoice line allocation, selected-line reversals/debt handling, shared-package loss acknowledgement, and unified reporting that keeps EGP revenue separate from Bunny USD cost. Reuse the existing idempotent `TeacherAccountingService`, `TeacherFinancialEvent`, `TeacherFinancialAllocation`, `TeacherPayout`, `TeacherPayoutAdjustment`, shared-package models, and Bunny usage snapshots; do not introduce a parallel ledger.

## Technical Context

<!--
  ACTION REQUIRED: Replace the content in this section with the technical details
  for the project. The structure here is presented in advisory capacity to guide
  the iteration process.
-->

**Language/Version**: C# 13/.NET 9; TypeScript 5.x, Next.js 16.2.7, React 19.2.4  
**Primary Dependencies**: ASP.NET Core, MediatR, FluentValidation, EF Core 9/Npgsql, Axios service layer, Zustand, Tailwind, Lucide  
**Storage**: PostgreSQL 16; existing attachment storage; Bunny Stream API/snapshots  
**Testing**: xUnit application/integration tests, Playwright admin flows, frontend lint/build  
**Target Platform**: Dockerized API and admin Next.js surface  
**Project Type**: Full-stack web application  
**Performance Goals**: paginated ledger/report endpoints, indexed date/teacher/source queries, no N+1 dashboard aggregation, idempotent financial writes  
**Constraints**: immutable financial history; admin-only center; EGP sales and USD Bunny cost never mixed; single transaction and database uniqueness for settlement allocation  
**Scale/Scope**: Admin dashboard, teacher workspaces, agreements, code delivery, shared packages, settlement/invoice lifecycle, Bunny rollups, migration of current finance data

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Layer impact**: Domain adds agreement/settlement/invoice/debt models and enums; Application adds resolver, commands, queries and validators; Infrastructure adds mappings/indexes/migration; API adds admin-only contracts; frontend adds admin finance center feature modules and service contracts. Worker remains unchanged. Docker runtime remains unchanged but migrations/health are mandatory gates.
- **Security**: Every new API endpoint uses Admin authorization in addition to existing finance permission checks. Every mutation writes an audit record with actor, reason and source references.
- **Financial integrity**: Agreement and discount fields are snapshotted on allocations; settlement lines reserve exact eligible allocations; unique constraints and serializable transactions prevent duplicate payment; corrections use reversible entries only.
- **UI quality**: Preserve Massar admin tokens/Tajawal and use data-dense drill-down workspaces, skeleton/empty/error states, keyboard-accessible tables and forms, responsive desktop-to-mobile panels. Do not adopt the generic design-system palette or font recommendations that conflict with the current product brand.
- **No-next-phase gate**: No implementation wave closes without focused tests, builds, Docker gate, documented Bunny dependency state and listed manual QA.

## Project Structure

### Documentation (this feature)

```text
specs/[###-feature]/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)
<!--
  ACTION REQUIRED: Replace the placeholder tree below with the concrete layout
  for this feature. Delete unused options and expand the chosen structure with
  real paths (e.g., apps/admin, packages/something). The delivered plan must
  not include Option labels.
-->

```text
backend/
├── src/NaderGorge.Domain/Entities/{TeacherFinancialEvent,TeacherAccount,TeacherPayout,TeacherFinanceAgreement}.cs
├── src/NaderGorge.Domain/Enums/TeacherAccountingEnums.cs
├── src/NaderGorge.Application/Services/{TeacherAccountingService,TeacherAgreementResolver}.cs
├── src/NaderGorge.Application/Features/Admin/TeacherFinanceCenter/
├── src/NaderGorge.Application/Features/{Student,Codes,Admin}/
├── src/NaderGorge.Infrastructure/{Data/AppDbContext.cs,Migrations/}
├── src/NaderGorge.API/Controllers/AdminTeacherFinanceCenterController.cs
└── tests/NaderGorge.Application.Tests/Finance/
frontend/
├── src/app/admin/finance/
├── src/features/teacher-finance-center/
├── src/components/admin/{BunnyCostReports,AdminDataTable,AdminStatCard}.tsx
└── src/services/finance-service.ts
```

**Structure Decision**: Extend the existing finance ledger and admin finance surface with focused feature modules, preserving current content, wallet, code, shared-package, Bunny, audit and attachment patterns.

## Phase Closure & Verification Plan

<!--
  ACTION REQUIRED: Replace placeholders with the concrete close-out plan for
  this feature/phase. A phase is not complete without evidence here.
-->

**Automated Tests Required**: Agreement precedence/effective date, discount burden, wallet recharge isolation, code delivery/activation idempotency, shared package loss acknowledgement, settlement reservation/state lifecycle, manual partial reversals, debt disposition, Bunny actual/estimated/missing costs, and admin authorization. Run focused xUnit tests, full backend build, frontend lint/build, and finance Playwright smoke.

**Docker Gate Required**: `docker compose config -q`; `make up`; `make migrate`; `docker compose ps`; health check backend/admin surfaces. Document unavailable Bunny credentials without marking live Bunny verification passed.

**Manual QA Required**: Admin creates teacher agreement; purchases and discounts representative content; confirms code delivery; sells shared package with/without loss acknowledgement; creates/reviews/pays/cancels settlement; records selected-line refund; views Bunny USD rollups; verifies non-admin denial.

**End-of-Phase Report Format**: implemented scope, migrations, exact commands/results, Docker result, manual QA checklist, external dependency status, risks and go/no-go.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| None | Existing layered architecture and ledger can be extended without an additional project or bypassing boundaries. | N/A |
