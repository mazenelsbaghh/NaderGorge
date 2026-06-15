# Backend Master Plan

**Last Updated**: 2026-06-15

---

## Active Plans

### Comprehensive Audit Remediation (2026-06-15)
- [x] Restrict teacher profile queries to own profile IDs and deny teacher code generation API routes.
- [x] Restrict manual lesson unlock endpoint to users with `watch_requests.manage` permission.
- [x] Require explicit `codes.manage` permission in MediatR handler for bulk code generation.
- [x] Enforce lesson access authorization check inside exam startup command.
- [x] Enforce single-use refresh token rotation utilizing atomic conditional DB updates in a transaction.
- [x] Implement token revocation and cookie cleanup at `POST /api/auth/logout`.
- [x] Enforce password reset version validation on tokens and reset commands.
- [x] Wrap balance adjustments, ledger, and outbox logs in atomic database transactions.
- [x] Intercept homework database constraint violations to ensure idempotent submissions.
- [x] Publish background jobs to Redis Streams (`job-stream`) instead of lists.
- [x] Expose `GET /api/health/ready` checking PostgreSQL and Redis connection health.

### Real-time Platform Speed & Sync (2026-06-11)
- [x] Implement database schema migration `AddOutboxEvents` and transactional entity `OutboxEvent` to capture balance and notification updates.
- [x] Implement SignalR unified `PlatformHub` at `/hubs/platform` with automatic group mapping for connections (UserId, Role, and dynamic Package/Lesson groups).
- [x] Create `OutboxProcessorBackgroundService` to process and dispatch pending outbox events every 2 seconds via SignalR.
- [x] Integrate `BalanceChanged` outbox event in `BalanceService` and intercept `NotificationCreated` events globally in `AppDbContext.SaveChangesAsync()`.
- [x] Publish `LessonPublished` outbox events upon new lesson creation inside `CreateLessonCommandHandler`.
- [x] Publish `VideoReady` outbox events inside `AiAnalysisCompletedCommandHandler` on processing completions.
- [x] Expose `ai-progress` endpoint in `InternalController` to enqueue `AiJobProgress` events for active monitoring.
- [x] Implement Redis-backed `IIdempotencyService` and `[Idempotent]` action filter to enforce 100% request deduplication on critical POST endpoints (purchase, code activation).

### Teacher Image WebP Conversion (2026-06-11)
- [x] Enforce `.webp` extension based on base64 prefix mime-type in `UploadTeacherProfileImageCommand.cs`.
- [x] Enforce `.webp` extension based on base64 prefix mime-type in `UploadTeacherPhotoCommand.cs`.

### Performance Audit Remediation (2026-06-11)
- [x] Implement `GET /api/student/shell-bootstrap` endpoint and MediatR query `GetShellBootstrapQuery` to retrieve notifications, balance, gamification points/streak in a single DB roundtrip.
- [x] Optimize `GetDashboardQuery.cs` to use `.AsNoTracking()`, flat SQL aggregates, and `.Select()` projections instead of full entity includes and memory loops.
- [x] Optimize `ListCodeGroupsQuery.cs` to query access code count directly in SQL projection.
- [x] Add pagination and direct projections to `GetMistakesQuery.cs` and `GetProgressQuery.cs`.
- [x] Add Brotli/Gzip response compression in `Program.cs`.
- [x] Add output caching for public GET endpoints in `Program.cs`.


### Role Pages and Permissions Completion (2026-06-09)
- [x] Create MediatR queries/DTOs for teacher dashboard analytics in `TeacherActivity.cs`.
- [x] Expose `GET /api/teacher/activity` inside `TeacherController.cs`.
- [x] Create profile and notification Handlers (Queries/Commands) under `Features/Student/` and register them in `StudentController.cs`.
- [x] Explicitly protect assistant tasks endpoints under `my/*` in `AssistantController.cs` using role authorize attributes.

### Update Default Role Permissions (2026-06-09)
- [x] Create EF Core DB migration to update default roles (Supervisor, Staff, Assistant) with predefined permissions in the database.
- [x] Update Seeder.cs to register these default roles with prefilled permissions.

### Assistant Surface Security & Task Ownership Checks (2026-06-09)
- [x] Secure GetTaskDetailsQuery to validate if user is Assignee, Creator, Admin, or Supervisor.
- [x] Secure AddTaskCommentCommand to enforce task ownership access before adding comment.
- [x] Secure UpdateTaskStatusCommand to block regular assistants from completing tasks without supervisor approval, and restrict modifying other users' tasks.

### Custom Forms API Payload Alignment (2026-06-07)
- [x] Ensure and document that backend `PUT` endpoints (`PUT /api/admin/forms/{id}` and `PUT /api/admin/forms/submissions/{submissionId}/status`) successfully receive matched IDs from the body payload as well.

### Student Forgot Password Endpoints (2026-06-04)
- [x] Add `VerifyResetFieldsCommand` to authenticate student details via student phone, date of birth, governorate, and district.
- [x] Issue a 10-minute temporary JWT token containing a `PasswordReset` claim/role upon successful verification.
- [x] Add `ResetPasswordCommand` to validate the reset token and update the user's password hash in the database using BCrypt.
- [x] Expose endpoints in `AuthController.cs`:
  - `POST /api/auth/verify-reset-fields`
  - `POST /api/auth/reset-password`
- [x] Write FluentValidation rules and unit test commands.

---

## History
- **2026-06-15**: Completed backend security audit changes: authorization logic, database transactions, idempotency checks, token rotations, and background streams.
- **2026-06-11**: Added outbox dispatcher service and real-time synchronization updates.
- - Initialized backend master plan directory.
