# Research: Content Identity and Types

## Decision 1: Derive Operational Codes From Existing GUIDs

**Decision**: Persist `LES-{Id:N}`, `VID-{Id:N}`, and `EXM-{Id:N}` in each owning table.

**Rationale**: Existing GUIDs are already stable and unique. A disjoint prefix identifies the kind and guarantees cross-kind separation, while a table-level unique index protects each namespace. Backfill is deterministic, idempotent, and requires no collision retry or sequence coordination.

**Alternatives considered**:

- Random short codes: rejected because collision handling and retries add unnecessary operational risk.
- One polymorphic identity registry: rejected because it adds a table and application-level referential integrity for only three established aggregates.
- Expose raw GUIDs only: rejected because staff need kind-aware operational identifiers and the requirement calls for explicit internal codes.

## Decision 2: Enforce Assignment And Immutability Centrally

**Decision**: `AppDbContext.SaveChangesAsync` assigns missing codes on added supported entities and rejects changes to persisted codes. Requests omit the field.

**Rationale**: Videos and exams are created through several paths, including standard admin commands, Bunny upload commands, E2E setup, and test seeding. Central enforcement prevents one path from producing empty codes and makes immutability independent of UI behavior.

**Alternatives considered**:

- Set codes only in individual handlers: rejected because current source has multiple `new LessonVideo` and `new Exam` paths.
- Database triggers: rejected because logic would be less visible to tests and inconsistent with the repository's EF migration patterns.
- Computed columns: rejected because provider-specific generated-column expression support complicates SQLite/InMemory tests and rollback.

## Decision 3: Persist A First-Class Video Type Catalog

**Decision**: Add `VideoType` and a required FK from `LessonVideo`; retain legacy `VideoTag` temporarily for compatibility.

**Rationale**: Free text cannot support administrator lifecycle, normalized uniqueness, active choices, reliable purchase targeting, or referential deletion rules. Keeping `VideoTag` avoids an unrelated compatibility cleanup in this spec.

**Alternatives considered**:

- Enum: rejected because administrators must add types without deployment.
- Continue free text with suggestions: rejected because it cannot enforce integrity.
- Remove `VideoTag` immediately: rejected because unknown external/report consumers have not been fully audited and removal is unnecessary for the accepted outcome.

## Decision 4: Seed Four Defaults Plus An Inactive Fallback

**Decision**: Seed active `شرح`, `واجب`, `مراجعة`, and `امتحان`, plus inactive `غير مصنف` for legacy values that cannot be mapped.

**Rationale**: The four labels come directly from the approved brief. A named inactive fallback makes migration total and reviewable without silently guessing an academic meaning.

**Alternatives considered**:

- Map every unknown to `شرح`: rejected because it changes meaning.
- Create one type for every historical tag: rejected because uncontrolled legacy spelling would pollute the managed catalog.
- Leave FK nullable: rejected because it perpetuates invalid classification and weakens later sales rules.

## Decision 5: Normalize Names In Application And Database

**Decision**: Trim, collapse surrounding whitespace, and use invariant uppercase normalization in application commands; enforce uniqueness on `NormalizedName` in PostgreSQL.

**Rationale**: Application validation provides clear Arabic errors, while the unique index closes concurrent-write races.

**Alternatives considered**:

- Case-sensitive names: rejected because visually identical labels would be confusing.
- Database-only error handling: rejected because raw constraint failures are poor UX.

## Decision 6: Separate Read Permission From Catalog Mutation

**Decision**: Any user with `content.manage` may list types for content forms; only built-in `Admin` role users may mutate the catalog.

**Rationale**: Teachers or delegated content managers may already create videos, but the user explicitly assigned catalog extension to admin. This preserves current content ownership while protecting the global taxonomy.

**Alternatives considered**:

- `content.manage` for all actions: rejected because delegated users could change platform-wide classification.
- Admin-only reads: rejected because non-admin authorized content editors could not satisfy the required video type field.

## Decision 7: Use Existing CQRS And Audit Patterns

**Decision**: Add focused type commands/queries and append `AuditLog` entities in the same application unit of work.

**Rationale**: This matches existing Admin commands and keeps state plus audit evidence atomic. No new repository abstraction is needed.

**Alternatives considered**:

- Direct controller database access: rejected by clean architecture.
- New audit event service: rejected as unnecessary scope because current features already persist `AuditLog` through `IAppDbContext`.

## Decision 8: Extend Existing DTOs Instead Of Adding Parallel Content APIs

**Decision**: Add codes and video type summaries to `GetLessonCockpitQuery`; add exam code to `GetExamDashboardQuery`.

**Rationale**: Those APIs already power the exact admin detail surfaces. Extending them avoids extra requests and duplicate authorization logic.

**Alternatives considered**:

- Dedicated code lookup endpoint: deferred until a later search/report feature requires it.
- Client-side code construction from IDs: rejected because API contracts should expose authoritative values.

## Decision 9: Dedicated Type Page Plus Shared Select Hook

**Decision**: Add `/admin/content/video-types`, `useVideoTypes`, and `VideoTypeSelect`, then reuse them in create/edit video forms.

**Rationale**: The catalog deserves a stable management surface, while shared data/error behavior prevents duplicate request logic across forms.

**Alternatives considered**:

- Modal inside the content page: rejected because lifecycle management, ordering, and assigned-count feedback outgrow a transient modal.
- Hard-coded dropdown options: rejected because it contradicts administrator-managed types.

## Decision 10: Preserve Existing Visual Identity

**Decision**: Use current AdminShellChrome, Navy/Teal tokens, Tajawal, moderate radii, Lucide icons, and compact tables/forms. Ignore the generic purple palette returned by the UI search tool.

**Rationale**: `PRODUCT.md` and `DESIGN.md` are authoritative. The interface is an operational admin tool, so consistency, density, WCAG AA contrast, and explicit states matter more than visual novelty.

**Alternatives considered**:

- New palette or page-level style: rejected as design drift.
- Card-heavy catalog: rejected because a compact ordered table is the established and more scannable affordance.

## Decision 11: Transactional Migration And Deployment

**Decision**: One EF migration creates/seeds types, adds nullable fields, backfills deterministic values and type mappings, then makes fields required and adds indexes/FK.

**Rationale**: PostgreSQL executes the migration transactionally, preventing partially typed content. Existing IDs and relationships do not change.

**Alternatives considered**:

- Background backfill after deploy: rejected because new required behavior could observe mixed state.
- Multi-release expand/contract: not needed for the current data shape and deterministic local transformation; production backup and clean-database migration tests remain mandatory.

## Decision 12: Verification Strategy

**Decision**: Add focused xUnit behavior tests and extend the established admin-content Playwright flow, then run full backend/frontend builds plus Docker migration and health gates.

**Rationale**: Unit/integration tests cover domain and command rules; Playwright covers the actual admin flow and readonly code rendering; Docker validates the real PostgreSQL migration.

**Alternatives considered**:

- Build-only verification: rejected because authorization, persistence, and UI behavior changed.
- Manual-only testing: rejected by the constitution and not reproducible.
