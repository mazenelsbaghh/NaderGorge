# Implementation Plan: Content Pricing and Currency Update

**Branch**: `019-content-pricing-currency` | **Date**: 2026-03-28 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/019-content-pricing-currency/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

Expand the content hierarchy (Terms, Sections, Lessons) to support individual pricing by adding a `Price` column to their respective database entities. Update all layers (Entity Framework, Commands/Queries, Controllers, Services, and React UI) to accept and display this price. Additionally, perform a global replacement of the currency text from Kuwaiti Dinar "دك" to Egyptian Pound "جنيها".

## Technical Context

**Language/Version**: C# 12 (.NET 8), TypeScript 5.x
**Primary Dependencies**: EF Core 8, MediatR, Next.js App Router, Axios
**Storage**: PostgreSQL (via EF Core Code-First Migrations)
**Testing**: Manual UI verification and backend compilation checks.
**Target Platform**: Web application (Next.js frontend, ASP.NET Core API).
**Project Type**: Full-stack Web MVP Expansion
**Performance Goals**: N/A (Simple CRUD addition)
**Constraints**: Price must default to 0 and cannot be negative.
**Scale/Scope**: Affects 3 tables and approximately ~10 frontend files.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Modular Clean Architecture**: PASS. The new `Price` property will be correctly passed through DTOs from the API layer to the Application layer, and finally mapped to the Domain entities.
- **III. Security & Access Control by Default**: PASS. Pricing manipulation will remain restricted to `[Authorize(Roles = "Admin")]` endpoints.
- **VII. Observability & Operational Readiness**: PASS. Database schema changes will be introduced via standard EF Core versioned migrations.
- **VIII. Premium Editorial Design System**: PASS. Frontend inputs will utilize the existing `.admin-input` utility class to maintain consistent styling.

## Project Structure

### Documentation (this feature)

```text
specs/019-content-pricing-currency/
├── plan.md              
├── research.md          
├── data-model.md        
└── contracts/api.md             
```

### Source Code (repository root)

```text
backend/
├── src/
│   ├── NaderGorge.Domain/
│   ├── NaderGorge.Application/
│   └── NaderGorge.API/

frontend/
├── src/
│   ├── components/admin/
│   ├── services/
│   └── app/admin/content/
```

**Structure Decision**: Option 2 (Web application with Next.js frontend and .NET backend).

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

*(No violations exist. Following standard CQRS and EF Core patterns.)*
