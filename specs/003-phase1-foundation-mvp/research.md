# Research: Phase 1 — Foundation and MVP Launch

**Date**: 2026-03-22 | **Spec**: [spec.md](spec.md)

## R1: Authentication Strategy (JWT + Device Binding)

- **Decision**: JWT access tokens (short-lived, 15 min) + Refresh tokens (long-lived, 7 days, stored in HttpOnly cookie). Device fingerprinting via a unique `DeviceId` generated client-side and stored in a database `Device` table linked to the user.
- **Rationale**: JWT is stateless and scales well. Refresh tokens in HttpOnly cookies prevent XSS attacks on the token itself. Device tracking is separate from sessions to allow admin-managed device removal.
- **Alternatives Considered**:
  - Session-based auth: Rejected — requires sticky sessions or shared session store; doesn't scale as cleanly with the .NET + Redis architecture.
  - OAuth2 / OpenID Connect: Overkill for a phone-based platform with no 3rd-party identity provider needs at MVP stage.

## R2: Device Limit Enforcement

- **Decision**: Store device fingerprints in a `Device` table (UserId, Fingerprint, FriendlyName, CreatedAt). On login, check count. If at max, reject login with a message directing the student to contact admin. Admin panel exposes a "Manage Devices" view per student to remove old devices.
- **Rationale**: Users' clarification demanded strict enforcement. Admins need a simple removal UI to prevent support bottlenecks.
- **Alternatives Considered**:
  - IP-based detection: Too unreliable (shared Wi-Fi, VPNs).
  - Soft alerts only: Explicitly rejected by user in clarification session.

## R3: Video Provider Abstraction (YouTube)

- **Decision**: Create an `IVideoProvider` interface with methods: `GetVideoMetadata(externalId)`, `GetEmbedUrl(externalId)`, `GetThumbnailUrl(externalId)`. Implement `YouTubeVideoProvider` as the initial concrete class. The frontend video player component receives an embed URL from the API; it never constructs YouTube URLs directly.
- **Rationale**: Constitution Principle II mandates provider abstraction from day one. The frontend and backend must never "speak YouTube natively."
- **Alternatives Considered**:
  - Direct YouTube embed in frontend: Violates constitution.

## R4: Exam Gating with Manual Override

- **Decision**: Each Exam entity has a `PassThreshold` (percentage set by Teacher/Assistant). After auto-grading, if the student's score < threshold, a `LessonProgressionBlock` record is created or the LessonProgress state is set to `BLOCKED`. The student can retake (new `StudentExamAttempt`). Teacher/Admin/Assistant can issue a `ManualUnlock` action that clears the block and allows the student into the next lesson.
- **Rationale**: User clarification specified Teacher/Assistant-controlled gating with a manual override path. This creates a clear audit trail via the existing AuditLog for every manual unlock.
- **Alternatives Considered**:
  - Soft flags only: Rejected by user.
  - No override — retake required always: Too rigid; Teachers need flexibility.

## R5: Video Watch Limit with Hard Lock

- **Decision**: Each `LessonVideo` has `MaxWatchMinutes` and `MaxReplays`. The backend tracks cumulative `VideoWatchEvent` data. When the limit is exceeded, the API returns a `VIDEO_LOCKED` status code. The frontend disables the video player and shows a "Contact your instructor" message. Admin/Assistant panel exposes a "Reset Watch Limit" button per student per video.
- **Rationale**: User clarification specified hard lock with manual override. This protects intellectual property while giving operational staff a relief valve.
- **Alternatives Considered**:
  - Frontend-only enforcement: Easily bypassed; backend must be the enforcer.
  - Automatic reset after a cooldown: Not requested; admins want full control.

## R6: Two-Step Registration UX

- **Decision**: Step 1 (immediate): Name, Phone, Password, Grade, Track. Creates User + StudentProfile. Issues JWT. Step 2 (deferred modal): On first code activation or restricted content access, a full-screen modal collects Parent Phone, Governorate, City/District, School. Until Step 2 is complete, the student can browse but cannot redeem codes.
- **Rationale**: Reduces friction at initial signup (Constitution Principle VI). Step 2 data is operationally required for code management and academic tracking.
- **Alternatives Considered**:
  - Single long form: Higher drop-off rate.
  - Step 2 optional forever: Loses critical operational data.

## R7: BullMQ Worker Integration Pattern

- **Decision**: The .NET backend writes job payloads to Redis queues using StackExchange.Redis. The Node.js BullMQ worker consumes from these queues. Job types for Phase 1: `BULK_CODE_GENERATION` (heavy batch job), `SEND_NOTIFICATION` (placeholder). Jobs are JSON-serialized with a `jobType` discriminator and typed payload.
- **Rationale**: Constitution mandates BullMQ for background jobs. .NET → Redis → Node is the established communication pattern from Phase 0 architecture.
- **Alternatives Considered**:
  - .NET native background services (Hangfire): Rejected by constitution's mandatory stack.
  - Direct HTTP calls from .NET to Node: Forbidden by architecture blueprint.

## R8: Admin Panel Scope (MVP)

- **Decision**: Admin panel supports CRUD for: Users (list, create, disable, manage devices), Content (Packages, Sections, Lessons, Videos), Code Groups (create batch, view logs, export), basic Question Bank (create/edit/tag MCQ questions), and Exam results (read-only view). No analytics dashboards in Phase 1.
- **Rationale**: MVP discipline — deliver the minimum operational tooling for the teacher's center to function.
- **Alternatives Considered**:
  - Full reporting dashboard: Deferred to Phase 2+.
  - CLI-based administration: Inadequate for non-technical center staff.
