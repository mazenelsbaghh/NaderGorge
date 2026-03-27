# Research: Registration, Code System & Content Hierarchy Overhaul

**Branch**: `014-registration-codes-hierarchy`
**Date**: 2026-03-27

## R1: Content Hierarchy Migration Strategy

**Decision**: Add a `Term` entity between `Package` and `ContentSection`. Existing `ContentSection` records get a foreign key to a default "Term 1" created per package via a data migration.

**Rationale**: The current model is `Package > ContentSection > Lesson`. Adding Term requires inserting a new level. A data migration is the safest approach — create a `Term` row per existing package, then re-point all `ContentSection.PackageId` to `ContentSection.TermId`.

**Alternatives considered**:
- Rename `Package` to `Year` and create a new `Package` wrapper → too invasive, breaks all existing references (access grants, code groups, student access).
- Add Term as optional → creates two valid hierarchies which complicates all queries.

## R2: Code Type Implementation

**Decision**: Add a `CodeType` enum to `CodeGroup` and expand `StudentAccessGrant` with nullable FKs to `Term`, `ContentSection`, `LessonVideo`, and `Exam`. Add a separate `StudentBalance` entity for credit codes.

**Rationale**: The current CodeGroup only has `PackageId` and `LessonId`. Expanding with a `CodeType` enum (Package, Term, Month, Lesson, Video, Exam, Balance) and corresponding nullable FKs follows the existing pattern without breaking current codes.

**Alternatives considered**:
- Polymorphic code subtypes (e.g., `TermCode`, `VideoCode` classes) → over-engineering for 6 types that differ only in target scope.
- Generic `TargetEntityType` + `TargetEntityId` pattern → loses strong typing and FK constraints.

## R3: QR Code Generation

**Decision**: Use `QRCoder` NuGet package (MIT license) for server-side QR generation. Generate QR containing a URL: `{baseUrl}/qr/{codeHash}` that triggers auto-redeem when scanned.

**Rationale**: QRCoder is the most popular .NET QR library (30M+ downloads), generates PNG/SVG, and has zero external dependencies. The URL-based QR approach allows both in-app scanning and any external QR reader to work.

**Alternatives considered**:
- Client-side QR generation (JS library) → works for display but doesn't support server-side batch generation for printing.
- Embedding raw code string in QR → requires the student to have the app open and use in-app scanner. URL approach is more flexible.

## R4: Registration Form UX

**Decision**: Single-page form with sections that progressively reveal based on selections. Use Framer Motion for smooth section transitions. Academic fields (grade, track) render conditionally based on stage selection.

**Rationale**: Single-page with conditional reveal is faster than multi-step wizard (fewer clicks, no back/forward navigation), and the total field count (~14 fields) is manageable on one page with good sectioning.

**Alternatives considered**:
- Multi-step wizard (step 1: personal, step 2: academic) → adds navigation complexity, user might lose progress.
- Accordion sections → less discoverable, can confuse users about what's required.

## R5: Balance System Design

**Decision**: Simple `StudentBalance` entity with `CurrentBalance` (decimal) and a `BalanceTransaction` log table. Balance is always >= 0. Deductions are atomic (transaction + balance update in same DB transaction).

**Rationale**: The balance system is intentionally simple — it's for code-based credit, not a full payment system. No payment gateway needed. Atomic deductions prevent race conditions.

**Alternatives considered**:
- Event-sourced balance (calculate from transaction history) → simpler to audit but slower reads. The hybrid approach (cached balance + transaction log) gives both.
- Redis-based balance → risks data loss. PostgreSQL is the source of truth.

## R6: Student Conditional Academic Fields

**Decision**: Implement stage/grade/track as three related enums with a validation matrix enforced in both frontend (conditional rendering) and backend (validation service).

**Validation matrix**:
| Stage | Grades Available | Tracks Available |
|-------|------------------|------------------|
| Secondary | First Secondary, Second Secondary | Second Secondary only: Arts, Science |
| Baccalaureate | First Baccalaureate, Second Baccalaureate | Second Baccalaureate only: Medicine & Life Sciences, Engineering & CS, Business, Arts & Humanities |

**Rationale**: Enum-based approach with a validation matrix is the clearest way to enforce the conditional logic. Frontend uses it for rendering, backend for validation. Single source of truth.

**Alternatives considered**:
- Free-text grade/track fields → no validation, data quality disaster.
- Database-driven grade/track lookup tables → over-engineering for 4 grades and 6 tracks that are unlikely to change.
