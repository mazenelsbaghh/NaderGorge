# Research: Teacher Profile & Content Visibility

## Decision 1: Keep visibility on `TeacherProfile`

- **Decision**: Add two explicit boolean states to `TeacherProfile`: one for teacher discovery/public visibility and one for teacher-owned content visibility.
- **Evidence**: `TeacherProfile` already owns public profile fields (`IsPublicProfileEnabled`, `ShowOnLanding`) and is the ownership root for packages, exams, community posts, and shared content.
- **Rationale**: A teacher-level content switch can consistently cover inherited content without adding visibility columns to every content table or deleting grants. Existing rows default to visible.
- **Alternatives considered**: Reusing `User.IsActive` was rejected because it disables login/account behavior and is already used for account state. Reusing `Package.IsActive` was rejected because it changes individual content lifecycle and cannot cover all teacher-owned content.

## Decision 2: Enforce hiding in backend query and access layers

- **Decision**: Filter public/student projections and deny protected access in Application/backend code; frontend hiding is only presentation.
- **Evidence**: Public teacher routes are in `PublicTeachersController`, student services call `/api/public/teachers`, and `AccessCheckService` is the shared protected-content gate.
- **Rationale**: Prevents leaks through direct IDs, stale UI, nested projections, caches, and previous-purchaser grants.
- **Alternatives considered**: Frontend-only filtering was rejected because it leaks data and cannot protect direct content URLs. Deactivating all grants was rejected because it mutates historical state and complicates restoration.

## Decision 3: Preserve purchases/grants and evaluate visibility at request time

- **Decision**: Leave purchase, financial, grant, academic, and audit rows unchanged; hidden content is denied by the same effective access check until shown.
- **Rationale**: Meets the confirmed product rule while preserving history and making show reversible/idempotent.
- **Risk**: Every access path must use the shared predicate; tests must cover direct and nested paths.

## Decision 4: Extend existing Admin teacher command instead of a parallel CRUD system

- **Decision**: Extend `UpdateTeacherProfileCommand`, `TeacherDto`, `AdminController`, and `teacherService` to include linked User fields, visibility fields, and write-only password replacement.
- **Evidence**: Existing Admin routes already use `[HasPermission("users.manage")]` and existing teacher update already synchronizes profile subjects.
- **Rationale**: Keeps one management workflow and avoids divergent update semantics.
- **Security**: Never serialize `PasswordHash`; if password changes, increment the existing password/security versions and revoke active refresh tokens as the current auth model requires.

## Decision 5: Use existing audit and staff-refresh infrastructure

- **Decision**: Record before/after user/profile/visibility values using `AuditLog` while omitting secrets, and rely on `StaffRealtimeChangeDetector`/outbox scopes for Admin staff refresh. Explicitly invalidate teacher/public/content frontend keys after successful mutation.
- **Evidence**: `AppDbContext.SaveChangesAsync` detects `TeacherProfile` and `User` changes; `StaffRealtimeChangeDetector` maps them to `users`, `subjects`, and content-related scopes through existing outbox processing.
- **Rationale**: Consistent with current realtime remediation and prevents stale Admin forms.

## Decision 6: Do not change the worker or Docker topology

- **Decision**: No Node worker, Redis queue, or new external provider change is needed.
- **Rationale**: This is a relational authorization/publication rule; the worker does not own teacher discovery or access grants.

## Decision 7: Validation and migration safety

- **Decision**: Add FluentValidation rules for required identity fields, normalized unique phone/login identifier, optional password strength, commission range, URL/length limits, and subject IDs; apply one atomic save/transaction.
- **Migration**: Add nullable-safe/defaulted boolean columns and indexes only if query plans require them. Existing teachers remain visible after migration.
- **Deployment risk**: Run migration in the existing migrator service, verify schema, then rebuild backend/frontend. If a pre-existing table/schema mismatch appears, use the repository's idempotent migration pattern and stop before service restart until verified.
