<!--
  Sync Impact Report
  ───────────────────
  Version change: 3.0.0 → 3.1.0 (MINOR — Added mandatory phase verification,
  per-phase automated/manual QA, Docker gates, and end-of-phase reporting for
  platform expansion work. Updated on 2026-06-07)

  Modified principles:
    - IV. Phased Delivery with MVP Discipline — Expanded with explicit
      phase-close gates and no-next-phase-until-verified rule
    - VII. Observability & Operational Readiness — Expanded by requiring Docker
      health verification at the end of every implementation phase
    - Development Workflow & Quality Gates / Definition of Done — Expanded with
      per-phase report, automated test evidence, Docker evidence, and manual QA

  Added sections:
    - XV. Phase Verification, Docker Gates & Manual QA

  Removed sections: None

  Templates requiring updates:
    - .specify/templates/plan-template.md       ✅ updated — Constitution Check
      now requires phase tests, Docker gate, and manual QA plan
    - .specify/templates/spec-template.md        ✅ updated — User Scenarios now
      capture manual QA and Docker acceptance expectations
    - .specify/templates/tasks-template.md       ✅ updated — Tests are mandatory
      for phase work; every phase includes an end-of-phase Docker/report gate
    - .specify/templates/commands/*.md           ✅ reviewed — directory not
      present in this project
    - AGENTS.md                                  ✅ reviewed — no change needed
    - PRODUCT.md                                 ✅ updated — removed legacy
      visible brand strings so surface verification can pass

  Follow-up TODOs: None
-->

# Nader George Educational Platform Constitution

## Core Principles

### I. Modular Clean Architecture (NON-NEGOTIABLE)

The system MUST follow a layered, modular architecture at all times:

- **Backend** — 4 C# projects (all targeting `net9.0`, C# 13) enforcing strict
  layer separation:
  - `NaderGorge.API` — 21 controllers, middleware pipeline
    (CorrelationId → ExceptionHandling → StaticFiles → CORS → RateLimiter →
    Auth → Authorization → Controllers), Swagger docs, health checks.
  - `NaderGorge.Application` — 116+ MediatR handlers across 16 feature modules
    (Admin, Assistant, Auth, Codes, Community, Content, Exams, Gamification,
    Homework, Internal, Public, Reports, Student, Tracking, Warnings, Webhooks).
    FluentValidation pipeline behavior. Service interfaces.
  - `NaderGorge.Domain` — 40+ entities, 11 enums, domain interfaces
    (`IAppDbContext`, `ITokenService`, `IVideoProvider`, `IAccessCheckService`,
    `IAuditRepository`).
  - `NaderGorge.Infrastructure` — `AppDbContext` with 40+ DbSets, 27 EF Core
    migrations, Redis cache, JWT `TokenService`, `VideoEncryptionService`,
    `QrCodeService`, `RedisJobEnqueuer`, `AuditRepository`, video providers
    (YouTube, VK).
- **Frontend** — Next.js 16.2.1 App Router with 130+ components across 17
  directories:
  - Route Groups: `(public)` (login, register), `admin/`, `student/`,
    `assistant/`, `parent-report/`, plus 3 API route handlers.
  - Architecture: Pages → Components → 14 Service files → 5 Hooks → 2 Zustand
    Stores → 7 Lib utils → 5 Utils → 3 Static data files.
- **Worker** — Node.js Express server (port 3001) with BullMQ queue processing:
  - 4 BullMQ queues: `ai-video-chapters`, `generate-chapter-mindmaps`,
    `notifications`, `ai-essay-grading`.
  - 3 Redis BRPOP bridge loops (.NET pushes to Redis lists → worker converts to
    BullMQ jobs).
  - 5 job processors: `analyzeVideoChapters`, `generateChapterMindmaps`,
    `evaluateEssay`, `commitment-engine`, `notification-sender`.
  - Legacy bulk code generation via `processJob()`.
  - Bull Board UI dashboard for visual job monitoring.
- Cross-module communication MUST happen through well-defined interfaces (MediatR
  for backend CQRS, Axios service layer for frontend, Redis lists for
  backend→worker). Never through direct internal access.
- No circular dependencies between modules. Dependency graph:
  `API → Application, Infrastructure | Infrastructure → Application, Domain |
  Application → Domain`.
- Database access MUST be confined to the Infrastructure Layer; Domain and
  Application layers MUST NOT reference ORM-specific types directly.

**Rationale**: The platform spans 64 feature specifications and 16 backend
feature modules. Without strict modularity the codebase will become
unmaintainable as features scale.

### II. Provider Abstraction First

Every external integration MUST be built behind an abstraction layer from day
one:

- **Video providers**: The platform implements `IVideoProvider` with 7 providers:
  YouTube, Telegram (embed via Cheerio scraping + direct HLS streaming), Google
  Drive (proxy endpoint), VK/Vkontakte (custom player via VK JS API + backend
  `VkVideoProvider`), Rutube (embed), OK/Odnoklassniki (embed). Each
  `LessonVideo` record stores: `Provider`, `ProviderVideoId`, `Title`, `Order`,
  `MaxWatchCount`, `VideoTag`, `SubtitleUrl`, `IsProcessingAI`,
  `IsProcessingMindmaps`, `LessonId`, `ExamId`. Adding a new provider MUST NOT
  require changes outside the provider layer and the Next.js proxy endpoint.
- **Notification channels**: In-platform notifications (`NotificationEvent`
  entity with `InApp`/`SMS` channels) and WhatsApp (via Evolution API at
  `evo.n8n-mazen.online`) are implemented. The `notification-sender` worker job
  is currently a stub ready for real SMS gateway integration. All channels MUST
  go through a unified interface.
- **AI services**: All AI features MUST go through the Node.js worker's
  `@google/genai` SDK (Google Gemini). Models used:
  `gemini-2.5-flash` (chapters, essays), `gemini-3-pro-image-preview`
  (mindmaps). Never couple directly to a specific LLM vendor in .NET code. The
  .NET backend communicates with AI exclusively via Redis LPUSH → Node.js
  BRPOP → BullMQ → Gemini → Webhook callback.
- **Payment/code systems**: Code redemption and access grant logic MUST be
  separated from specific code type implementations. The system supports 7 code
  types via `CodeType` enum: `Package=0`, `Term=1`, `Month=2`, `Lesson=3`,
  `Video=4`, `Exam=5`, `Balance=6`. Each handled through unified
  `CodeGroup` → `AccessCode` → `StudentAccessGrant` entities.

**Rationale**: The platform has already migrated from YouTube-only to 7 video
providers. Abstraction from day one prevented costly rewrites and MUST be
maintained for all future integrations.

### III. Security & Access Control by Default

Security is a foundational concern, not a bolt-on feature:

- **Authentication**: JWT-based with refresh token flow:
  - Token generation: `TokenService` using `System.IdentityModel.Tokens.Jwt
    8.3.0` + `Microsoft.AspNetCore.Authentication.JwtBearer 8.0.11`.
  - Settings: `Issuer=NaderGorgeAPI`, `Audience=NaderGorgeClients`,
    `ExpirationMinutes=60`, `RefreshExpirationDays=30`.
  - `RefreshToken` entity stores token, expiry, revocation status, and
    `DeviceFingerprint`.
  - Phone-based registration with OTP verification.
- **Password Hashing**: BCrypt (`BCrypt.Net-Next 4.0.3`).
- **Authorization**: Role-based access control via `RoleType` enum:
  `Admin=1`, `Teacher=2`, `Assistant=3`, `Student=4`,
  `AssistantReviewer=5`, `AssistantAcademic=6`. Custom policies:
  `RequireAssistantReviewer` (Admin, Assistant, AssistantReviewer),
  `RequireAcademicAssistant` (Admin, Teacher, AssistantAcademic).
- **Default Seeds**: 4 roles seeded + default admin user
  (`01000000000`/`Admin@123`) + default student user.
- **Audit logging**: Every state-changing operation MUST produce an `AuditLog`
  entry (Action, EntityType, EntityId, PerformedByUserId, OldValues, NewValues,
  IpAddress, CorrelationId).
- **Session control**: `Device` entity tracks fingerprint, name, IP, last use,
  active status. `MAX_DEVICES_PER_STUDENT` configurable (default: 2). Admin can
  delete specific or all devices.
- **Rate limiting** (enforced in middleware):
  - `auth` — 10 requests/minute per IP.
  - `codes` — 5 requests/minute per user.
  - `video-session` — 10 requests/minute per user.
  - Global — 300 requests/minute per IP.
- **Request tracing**: `CorrelationIdMiddleware` adds/propagates
  `X-Correlation-Id` header on every request.
- **Error handling**: `ExceptionHandlingMiddleware` maps:
  `ValidationException→400`, `UnauthorizedAccessException→401`,
  `KeyNotFoundException→404`, `InvalidOperationException→400`, Generic→500.
  Unhandled exceptions MUST be caught and logged, never exposed to clients.
- **Data validation**: All user input MUST be validated at both frontend (Zod
  schemas) and backend (`FluentValidation 11.11.0` with
  `ValidationBehavior<TRequest, TResponse>` MediatR pipeline behavior) layers.
- **Video content protection**: Anti-download protection, dynamic watermarking,
  DevTools/iframe hiding, Shadow DOM shields, and `VideoEncryptionService` for
  session-based playback tokens.

**Rationale**: The platform handles student data, financial transactions
(code-based access), and academic records. A security breach would be
catastrophic for trust and legal compliance.

### IV. Phased Delivery with MVP Discipline

Development MUST follow a strict phased approach:

- **Phase 0**: Discovery, planning, and product blueprint — no code production.
- **Phase 1**: Foundation and MVP launch — working auth, content, codes, exams,
  admin, student dashboard.
- **Phase 2**: Structured learning and academic operations (homework, parent
  layer, gamification, notifications, commitment engine, assistant task queue).
- **Phase 2.5**: Video security and content protection (server-side embed,
  Shadow DOM, DOM shields, session-based video access).
- **Phase 3**: Registration, code system, and content hierarchy overhaul.
- **Phase 4–7**: Incremental feature delivery (question bank, AI, watch control,
  analytics, revenue optimization).
- Each phase MUST deliver independently usable, testable functionality.
- Features from later phases MUST NOT leak into earlier phase implementations.
- Schema fields for future features MAY be added early (e.g., watch limit fields
  in Phase 1), but business logic MUST NOT be implemented ahead of schedule.
- Risk avoidance: Do NOT build too much too early (Section 9.1 of the plan).
- The project has delivered 64 feature specs (`specs/001-*` through
  `specs/064-*`) tracking all work from Phase 0 discovery through full Docker
  setup.

**Rationale**: The plan explicitly identifies "building too much too early" as a
major risk. MVP discipline ensures a launchable product before expanding scope.

### V. Academic Content Integrity

All academic features MUST respect the teacher's content authority:

- **AI boundaries**: AI MUST stay tied to approved academic content. No open-web
  generic assistant behavior. AI video chapter analysis derives from actual
  video transcripts (SRT files generated via `fluent-ffmpeg 2.1.3` +
  `youtube-dl-exec 3.1.4` audio extraction → `gemini-2.5-flash` transcription).
  Mind-map generation uses `gemini-3-pro-image-preview` with optional teacher
  photo reference for 3D Pixar-style visuals in 16:9 landscape with Arabic text.
- **Target Boundaries**: Exams can target either an entire `Lesson`
  (via `Lesson.ExamId`) or a specific `Video` (via `LessonVideo.ExamId`).
  Supports MCQ, Essay, and FindTheMistake (`FindTheMistakeQuestion` entity with
  `BaseText`, `MistakeStartIndex`, `MistakeEndIndex`). Exam features include
  50/50 lifeline, question swap, and `DisplayQuestionCount` for randomized
  subsets.
- **Content Hierarchy**: `Program` → `Package` → `Term` → `ContentSection`
  (Month/Unit) → `Lesson` → Materials (`LessonVideo`, `LessonResource`,
  `Homework`, `Exam`). This hierarchy is non-negotiable and MUST be preserved
  in all data models and UIs.
- **Question bank integrity**: `QuestionBankItem` classified by `Type`
  (MCQ/Essay/FindTheMistake), `Tags`, `AudioUrl`, `HintText`,
  `WrittenCorrection`, `DefaultPoints`. Questions linked to exams via
  `ExamQuestion` junction entity with `Order` and `Points`.
- **Progression rules**: `LessonProgress` tracks `IsCompleted` and
  `IsManuallyUnlocked`. `VideoWatchEvent` tracks `TimeWatchedInSeconds`,
  `WatchCount`, `IsLocked`. Admin can unlock via
  `POST admin/lessons/{lessonId}/students/{studentId}/unlock`.
- **Gamification ethics**: `StudentGamification` tracks `TotalPoints`,
  `CurrentStreakCount`, `LongestStreakCount`, `LevelName`.
  `GamificationActionLog` records events: `HomeworkSubmittedOnTime`,
  `PerfectExam`, `StreakMaintained`, `EarlyBird`. `StudentBadge` for
  achievements. Gamification MUST be motivating, not toxic.
- **Essay evaluation**: `EssaySubmission` entity with status lifecycle:
  `WaitAI=0` → `AIScored=1` → `WaitTeacher=2` → `TeacherGraded=3`. AI
  evaluation via `evaluateEssay` worker job compares against model answers.
  AI feedback is in Arabic and MUST NOT reveal the correct answer.
- **Student behavior tracking**: `StudentStatusTracker` classifies students as
  `Committed`, `Average`, or `AtRisk`. `WarningEvent` with `Low`/`Medium`/
  `Critical` severity. Commitment engine runs hourly to detect 7+ day
  inactivity.

**Rationale**: This is an educational platform for a specific teacher's brand.
Academic quality and content control are the core value proposition.

### VI. Single-Flow Registration & UX Simplicity

The platform experience MUST be simple, organized, motivating, and clear in its
study path:

- **Registration**: MUST collect all required data in a single registration flow
  (`RegistrationForm.tsx` — 51KB, multi-step wizard):
  - Personal data: full name (four-part), student phone (Dostab), student code
    (Dostab), date of birth, gender, governorate, address, parent phone, parent
    status (father alive/not, mother alive/not).
  - Academic data with conditional logic via `EducationStage` enum:
    `Secondary=0`, `Baccalaureate=1`, `Primary=2`, `Preparatory=3`, `Azhari=4`,
    `American=5` → `GradeLevel` → `StudyTrack` (only for Second Secondary and
    Second Baccalaureate).
  - `StudyTrack` options: Arts, Science, Medicine and Life Sciences, Engineering
    and Computer Science, Business, Arts and Humanities.
  - `SchoolType` enum for school classification.
  - `StudentProfile` entity stores all fields including `Nationality`,
    `District`, `SecondaryPhone`, `SecondaryParentPhone`, `MotherPhone`,
    `FatherDateOfBirth`, `MotherDateOfBirth`, `SchoolName`.
- **Real-Time Verification**: WhatsApp number verification via
  `WhatsAppVerificationService` calling Evolution API
  (`POST api/whatsapp/check`). Manual verification steps MUST be avoided.
- **Data Integrity Constraints**: Registration fields MUST enforce strict literal
  boundaries at both the HTML layer (`max` attributes) and schema validation
  layer (`zod`).
- **Progressive Full-Context Previews**: Derived information (age calculation,
  birthday countdown) MUST be offloaded to a "Preview Panel".
- **Student dashboard** (`StudentHero`, `StatsStrip`, `ProgressRing`,
  `PackageGrid`, `ContinueLearningCard`, `QuickAccessPanel`,
  `UpcomingExamsPanel`, `GamificationWidget`, `StudentMomentumRail`,
  `StudentGettingStartedPanel`, `StudentDestinationsPanel`): MUST surface
  available packages, latest lesson, upcoming exams, progress, used codes,
  notifications, and quick resume-study access.
- **Navigation**: `StudentShellChrome` (16KB) provides shell with dot-grid
  background, sidebar, `StudentBottomNav` for mobile, and theme support.
  Students MUST always know where they are.
- **Control balance**: The platform MUST guide and control students, not
  suffocate them.
- **Responsive design**: Mobile-first for students (`StudentBottomNav` for
  one-handed phone use), desktop-first for admin (`AdminShellChrome`).

**Rationale**: All student data is collected upfront to avoid incomplete profiles.
Real-time background verification prevents unreachable accounts.

### VII. Observability & Operational Readiness

The system MUST be observable and operationally ready from Phase 1:

- **Structured logging**: `CorrelationIdMiddleware` propagates
  `X-Correlation-Id` for request tracing across all services.
- **Error handling**: `ExceptionHandlingMiddleware` returns consistent error
  response formats via `ApiResponse<T>` wrapper. Unhandled exceptions MUST be
  caught and logged, never exposed to clients.
- **Health checks**: Each deployable service exposes a health check:
  - Backend: `GET /api/health` (`HealthController`) — `curl -f
    http://localhost:5245/api/health`
  - Worker: `GET /ui` (Bull Board dashboard) — `curl -f
    http://localhost:3001/ui`
  - Frontend: HTTP 200 on port 8738 — `curl -f http://localhost:8738`
  - PostgreSQL: `pg_isready -U postgres` (5s interval, 10 retries)
  - Redis: `redis-cli ping` (5s interval, 10 retries)
- **Environment separation**: `appsettings.Development.json`,
  `appsettings.E2e.json`, `appsettings.json` + Docker environment. `make dev`
  runs natively with `ASPNETCORE_ENVIRONMENT=E2e`. `make up` runs Docker with
  `ASPNETCORE_ENVIRONMENT=Docker`.
- **Database migrations**: 27 EF Core code-first migrations in
  `NaderGorge.Infrastructure/Migrations/` (from `InitialCreate` through
  `AddExamDisplayQuestionCount`). Direct schema modifications in production are
  FORBIDDEN. Use `make migrate` (runs `nadergorge_migrator` container) and
  `make migrate-add NAME=X` (scaffolds via temporary SDK container).
- **Background job monitoring**: Bull Board UI at `http://localhost:3001/ui`
  tracks all 4 BullMQ queues with status, progress percentages (Arabic stage
  labels), retry count, and failure logs. Worker Express API provides
  `GET /api/status/:id`, `DELETE /api/status/:id`, `POST /api/status/:id/retry`.

**Rationale**: A platform serving students during exam periods cannot afford
unexplained downtime. Observability is the foundation of reliable operations.

### VIII. Premium Editorial Design System (The "Curated Archive")

The platform MUST adhere to the design system documented in `DESIGN.md` and
`.impeccable.md`:

- **No-Line Rule**: Borders (e.g., 1px solid lines) are strictly prohibited for
  defining layout sections. Boundaries MUST use background tonal shifts and
  negative space.
- **Glass & Gradient**: Glassmorphism (`surface_variant` at 60% opacity, 24px
  blur) for floating navigation/modals. Signature gradients
  (`primary-strong` #7f5427 → `primary` #9a6933 at 135°) for main CTAs.
- **Typography**: Complete reliance on **Cairo** (Google Fonts) — geometric
  clarity, excellent Arabic support, weights 300–900. Semantic tiers: Display,
  Headline, Title, Body, Label. Generous letter-spacing on labels.
- **Elevation & Depth**: Layering via nested `surface-container` tiers.
  Shadows MUST be warm and ambient (umber-tinted, never pure black). Ghost
  borders: `outline_variant` at 15% opacity.
- **Tonal Palette — Light Mode (Gold Identity)**:
  - `--background`: #faf2e6 (sand)
  - `--card`: #fcf6ea (papyrus)
  - `--muted`: #f0e4ce
  - `--secondary`: #f2dfbc
  - `--sidebar`: #f7ecda
  - `--primary` (gold): #9a6933
  - `--primary-strong`: #7f5427
  - `--primary-container`: #c5a059
  - `--primary-foreground`: #fffaf1
  - `--on-surface`: #2c1708 (warm near-black, NEVER pure #000000)
  - `--muted-text`: #7a644d
  - `--tertiary` (links): #485e8b
- **Dark Mode (Premium Calm)**: Separate restful experience using oklch color
  space. Not gold-heavy. Quiet, professional, refined. Comfortable for night
  studying. Initialized via blocking `<script>` to prevent FOUC.
- **Student Theme Palettes**: 9 palettes stored in `student-theme-palettes.ts`:
  - Light (5): Scholar (gold), Oasis (teal), Ruby (copper-rose), Blossom
    (pink), Winter Sky (slate).
  - Dark (4): Scholar (gold), Midnight Teal, Ember (warm amber), Rainy Night
    (slate).
  - Persisted to DB via `StudentProfile.LightThemePaletteId`,
    `DarkThemePaletteId`, `CurrentMode`.
- **RTL-First**: `dir="rtl"`, `lang="ar"` on `<html>`. All components MUST
  support RTL natively. Arabic-first copy throughout.
- **View Transitions**: CSS `@supports (view-transition-name: none)` with
  crossfade animations. Page transitions via Framer Motion `template.tsx`.
- **Admin Design System**: 30+ dedicated `--admin-*` CSS variables for bg, text,
  cards, buttons, borders, shadows, status colors. Custom CSS classes:
  `.admin-input`, `.admin-btn-primary`, `.admin-btn-ghost`, `.admin-panel`,
  `.admin-badge`, `.admin-btn-icon`. Centralized shared components:
  `AdminShellChrome` (12KB), `AdminDataTable`, `AdminStatCard`, `AdminModal`,
  `AdminSearchToolbar`, `AdminTabBar`, `AdminBreadcrumbs`, `AdminBackButton`,
  `AdminPageSkeleton`. Writing ad-hoc HTML tables or custom layout structures
  for standard admin views is FORBIDDEN.
- **Landing-specific tokens**: `--landing-*` CSS variables with custom classes
  `.landing-page`, `.landing-panel`, `.landing-chip`.
- **IDM Annihilator**: CSS rules to hide Internet Download Manager browser
  extension overlays on video pages.
- **Accessibility**: WCAG AA compliance. Minimum 4.5:1 contrast ratio. 44px
  touch targets. Min 14px body text. Full `prefers-reduced-motion` support with
  graceful animation degradation.

**Rationale**: The platform's "Modern Egyptian" aesthetic — subtle pharaonic
identity without kitsch — rejects sterile SaaS aesthetics in favor of a curated,
academic journal feel. The brand's mantra: "دي مش منصة دروس… دي نظام بيدير
مذاكرتي" — "This isn't a lessons platform... this is a system managing my
studying."

### IX. Assessment & Time Integrity

The platform's examination module MUST be technically resilient against tampering:

- **Server-Side Truth**: All assessment timers (per-exam via
  `Exam.DurationMinutes`, per-question) MUST compute elapsed time based on
  absolute server timestamps (`StudentExamAttempt.StartedAt` and offset limits).
- **Client Resilience**: Client-side countdowns (`ExamTimer.tsx`,
  `CountdownTimer.tsx`) are strictly visual indicators. If a student refreshes,
  closes the browser, or disconnects, the server-side time continues.
  `IsTimeExpired` flag on `StudentExamAttempt` enforces server truth.
- **Enforced Submissions**: `POST api/exams/{id}/submit/{attemptId}` handles
  expired exams intelligently. `GET attempts/{attemptId}/grading-status` tracks
  async essay grading.
- **Exam Features**: `IsRandomized` for question shuffling,
  `DisplayQuestionCount` for showing a subset, 50/50 lifeline
  (`GET fifty-fifty`), question swap (`POST swap`), `IsMandatory` flag,
  `PassingScore` threshold.
- **Essay Submissions**: `EssaySubmission` entity with `AudioUrl` support.
  AI-assisted evaluation via worker's `evaluateEssay` job. Status lifecycle
  ensures teacher has final override over AI scoring.
- **Answer Tracking**: `StudentAnswer` stores `SelectedOptionId` (MCQ),
  `SubmittedText` (essay), `HintUsed` flag, `IsCorrect`, `PointsAwarded`.

**Rationale**: Accurate assessment is the core of academic accountability.
Timers reliant on client-side mechanisms compromise evaluation integrity.

### X. Pricing & Currency Localization

Billing and gamification MUST remain starkly demarcated:

- **Fiat vs Gamification**: Fiat currency (EGP) tracked via `StudentBalance`
  → `BalanceTransaction` (Amount, BalanceAfter, TransactionType, ReferenceId,
  Description). Gamification tracked separately via `StudentGamification` →
  `GamificationActionLog` → `StudentBadge`. These MUST never be conflated.
- **Granular Content Pricing**: `Price` field exists on `Package`, `Term`,
  `ContentSection`, and `Lesson` entities. If omitted, defaults to `0` (Free).
  `POST api/student/balance/purchase` handles content purchase via balance.
- **Code-Based Access**: `CodeGroup` supports `BalanceAmount` (for credit codes)
  and `DiscountPercentage` (for discount codes). `ExpiresAt` on both
  `CodeGroup` and `AccessCode` levels.
- **Localized Display**: All monetary values MUST use the localized Egyptian
  Pound suffix ("جنيها"). Legacy references (like "دك") are strictly
  prohibited.
- **Negative Rejection**: The system MUST prevent negative pricing inputs via
  form validation.

**Rationale**: Granular pricing supports flexible micro-transactions. Accurate
localization prevents customer confusion.

### XI. Frontend Reliability & Rendering Strictness

The frontend MUST maintain rendering reliability across all user-facing views:

- **React Compiler**: Next.js 16 React Compiler (`reactCompiler: true` in
  `next.config.ts`) is enabled. All components MUST be compatible with automatic
  memoization.
- **Standalone Output**: `output: 'standalone'` for minimal Docker images
  (multi-stage Dockerfile with production-only files).
- **Strict TypeScript**: Frontend code MUST compile with strict TypeScript
  (`"strict": true` in `tsconfig.json`). Target: ES2017, Module: ESNext,
  Module Resolution: bundler. Path alias: `@/*` → `./src/*`.
- **Service Layer Isolation**: All API calls MUST go through the 14 service files
  in `src/services/`:
  - `api-client.ts` — Centralized Axios client with JWT interceptor,
    auto-refresh on 401, rate limiting toast on 429.
  - `admin-service.ts` (21KB) — Full admin CRUD.
  - `content-service.ts` — Content hierarchy + in-memory package cache (10s TTL).
  - `auth-service.ts` — Register, login, refresh, complete-profile.
  - `student-service.ts` — Dashboard, progress, mistakes, theme preferences.
  - `exam-service.ts` — Start, submit, 50/50, swap, grading status.
  - `community-service.ts` — Posts, comments, likes, polls.
  - `code-service.ts` — Code group management, redemption.
  - `balance-service.ts` — Balance retrieval, content purchase.
  - `video-session-service.ts` — Session create/consume, progress tracking.
  - `homework-service.ts` — Pending homework, submission.
  - `report-service.ts` — Parent report (anonymous).
  - `assistant-service.ts` — Pending tasks, resolution.
  - `whatsapp-service.ts` — WhatsApp number validation.
  Direct `fetch` or `axios` calls from components are FORBIDDEN.
- **State Management**: Zustand 5.0.12 for local app state:
  - `auth-store.ts` — user, tokens, isAuthenticated, persisted to
    localStorage/sessionStorage.
  - `lesson-focus-store.ts` — focus mode toggle.
  No Redux, no Context API for global state.
- **Client-Side Auth Only**: No Next.js middleware. Auth is handled via
  `AuthBootstrap` component (loads from storage on mount), `AdminGuard` (role
  check), and API client interceptor (auto-refresh). Device fingerprint
  generated via `crypto.randomUUID()`.
- **Animation Budget**: Framer Motion (`^12.38.0`) + GSAP (`^3.14.2`) +
  Three.js (`^0.183.2`) + OGL (`^1.0.11`). Three.js/OGL MUST be used only for
  landing page effects, never in student dashboard.

**Rationale**: A reliable, predictable frontend ensures students can focus on
studying without encountering rendering bugs or stale state.

### XII. Multi-Provider Video Architecture & Content Protection

The platform's video delivery MUST support multiple providers with unified
protection:

- **Implemented Providers** (7 total):
  - YouTube — Standard embed via `YouTubeVideoProvider` (Infrastructure).
  - Telegram Embed — Cheerio scraping of embed page (Next.js proxy at
    `api/video/embed`).
  - Telegram Direct Stream — HLS streaming via proxy at `api/video/stream-proxy`.
  - Google Drive — Proxy endpoint for direct playback.
  - VK/Vkontakte — Custom player via VK JS API (`videoplayer.js`) +
    `VkVideoProvider` (Infrastructure).
  - Rutube — Standard embed.
  - OK/Odnoklassniki — Standard embed.
- **Custom Player**: `SecureVideoPlayer.tsx` (38KB — largest component) provides
  unified player across all providers with: `PlayerControls.tsx` (13KB —
  play/pause, scrub, fullscreen, chapters, speed), `ChapterList.tsx` (AI
  chapters), `LessonMindmapDisplay.tsx` (mind-maps), `WatchStatusBar.tsx`
  (watch count/limit).
- **Session-Based Access**: `VideoPlaybackSession` entity stores `SessionToken`,
  `EncryptionKey`, `ExpiresAt`, `IsConsumed`, `IpAddress`. Created via
  `POST api/student/video-session`, consumed via
  `POST api/student/video-session/{sessionId}/consume`. Rate limited to
  10 req/min/user.
- **Anti-Download Protection**: DOM event interception, right-click blocking,
  keyboard shortcut interception, DevTools detection (`DevToolsIframeHider`),
  iframe concealment, CSS IDM Annihilator rules. All implemented in
  `src/utils/` (DOM shield, video crypto utilities).
- **Dynamic Watermarking**: Student-specific watermark overlays rendered at
  runtime to deter screen recording.
- **Chapter Intelligence**: `VideoChapter` entity (Title, StartTime, EndTime,
  SummaryText, MindmapImageUrl, Order). AI-generated via `analyzeVideoChapters`
  worker job. Mind-maps via `generateChapterMindmaps` job (supports both batch
  and single-chapter regeneration). SRT files saved to
  `backend/wwwroot/subtitles/{videoId}.srt`. Mind-map PNGs saved to
  `backend/wwwroot/mindmaps/`.
- **Watch Tracking**: `VideoWatchEvent` tracks per-user per-video
  `TimeWatchedInSeconds`, `WatchCount`, `IsLocked`. `ExtraWatchRequest` allows
  students to request additional watches (Pending → Approved/Rejected by admin).
- **Provider Addition Protocol**: (1) Add provider string to `LessonVideo.
  Provider`, (2) Create Next.js proxy route in `src/app/api/video/`, (3)
  Implement embed/player component in `SecureVideoPlayer.tsx`, (4) Optionally
  add `IVideoProvider` implementation in Infrastructure.

**Rationale**: The platform evolved from YouTube-only to 7 providers due to
content protection requirements. This architecture MUST be maintained and
extended predictably.

### XIII. AI Worker Orchestration & Background Job Integrity

The Node.js BullMQ worker MUST maintain strict job processing integrity:

- **Architecture**: Single Node.js process (`worker/src/index.ts`, 348 lines)
  running Express server + 4 BullMQ workers + 3 Redis BRPOP bridge loops +
  commitment engine cron + legacy code generator.
- **BullMQ Queues and Jobs**:
  - `ai-video-chapters` queue → `analyzeVideoChapters` job:
    Pipeline: yt-dlp audio extraction → ffmpeg conversion (16kHz mono, 48kbps)
    → Gemini File API upload → Call A: SRT transcription (text/plain) →
    Call B: Chapter analysis (JSON, 5–15 chapters) → Save SRT → Webhook to
    `/api/v1/internal/callbacks/ai-analysis-completed` → Cleanup temp files.
    Progress stages (Arabic): 10%→40%→85%→95%→100%.
  - `generate-chapter-mindmaps` queue → `generateChapterMindmaps` job:
    Two modes (batch all chapters / single chapter). Uses
    `gemini-3-pro-image-preview` for 3D Pixar-style mind-map PNGs. Webhooks:
    batch → `/callbacks/mindmaps-completed`, single →
    `/callbacks/single-mindmap-completed`.
  - `ai-essay-grading` queue → `evaluateEssay` job:
    Sends student answer + expected answer to `gemini-2.5-flash`. Returns
    `{ isCorrect, feedback }` (Arabic). Prompt MUST NOT reveal correct answer.
    Webhook → `/callbacks/essay-graded`.
  - `notifications` queue → `notification-sender` job:
    Currently a stub (500ms delay simulation). Ready for real SMS gateway.
- **Redis BRPOP Bridge**: .NET backend pushes to Redis lists (`LPUSH`):
  `ai-video-queue`, `ai-mindmaps-queue`, `ai-essay-queue`. Worker polls via
  `BRPOP` (blocking pop) and enqueues into BullMQ with deduplication via
  job IDs (`lessonVideoId`, `{videoId}_mindmaps`, `essaySubmissionId`).
- **Commitment Engine**: `setInterval` every 1 hour. Queries PostgreSQL for
  students inactive >7 days not in `student_status_trackers`. Creates
  `warning_events` entries with Medium severity.
- **Legacy Code Generation**: `processJob()` handles bulk access code generation
  via direct SQL INSERT into `access_codes` and `code_groups` (batch size:
  5000).
- **Callback Security**: Worker→backend webhook callbacks authenticate via
  `X-Internal-Token` header matching `API_CALLBACK_SECRET`. Received by
  `InternalController` (no standard auth, token-only).
- **Gemini Service** (`geminiService.ts`, 289 lines): Uses `@google/genai` SDK
  with **60-minute timeouts** via `undici` global dispatcher. Uploads audio to
  Gemini File API, uses for multiple calls, then deletes. Mind-map generation
  supports teacher photo reference (placed FIRST in parts for likeness).
- **Audio Extraction** (`audioExtractor.ts`, 153 lines): Downloads via `yt-dlp`
  with `--extract-audio --audio-format mp3`. Handles YouTube ID normalization.
  Falls back to ffmpeg conversion if needed. Uses
  `--js-runtimes node:{execPath}` for yt-dlp 2026+ compatibility.
- **Temporary Files**: Audio files in `worker/.tmp/{videoId}.mp3`. Cleaned up
  on success only (preserved on failure for retry debugging).

**Rationale**: AI jobs are resource-intensive and long-running. Without strict
orchestration, they can exhaust system resources, produce duplicate results, or
leave orphaned temporary files.

### XIV. Community, Comments & Social Features

Social features MUST maintain strict moderation boundaries:

- **Lesson Comments**: `LessonComment` entity (Body max 2000 chars). Status
  lifecycle via `LessonCommentStatus` enum: Pending → Approved/Rejected.
  Students create via `POST api/content/lessons/{lessonId}/comments`. Admin
  moderates via `AdminLessonCommentsController` (approve/reject endpoints).
  Students can view their own comments via `GET .../comments/mine`.
- **Community Posts**: `CommunityPost` entity (Body max 4000 chars).
  `CommunityPostStatus` enum: Pending → Approved/Rejected. Students create
  via `POST api/community/posts`, view their own via `GET .../posts/mine`.
  Admin moderates via `AdminCommunityController`.
- **Community Comments**: `CommunityPostComment` entity (Body max 2000 chars).
  `CommunityCommentStatus` enum with `RejectionReason`. Students comment on
  approved posts via `POST api/community/posts/{postId}/comments`.
- **Likes**: `CommunityPostLike` entity with unique constraint on
  (PostId, UserId). Toggle via `POST api/community/posts/{postId}/likes/toggle`.
- **Polls**: Poll voting via
  `POST api/community/posts/{postId}/polls/{optionId}/vote`.
- **Moderation-First**: No user-generated content MUST be visible to other
  users without admin approval. This is NON-NEGOTIABLE.
- **Frontend Components**: `CommunityFeed.tsx`, `CommunityPostComposer.tsx`
  (6KB), `CommunityPostComments.tsx`, `CommunityPostLikeButton.tsx`,
  `CommunityPostPoll.tsx`, `MyCommunityPostsPanel.tsx`. Admin:
  `CommunityPostsModerationTable.tsx` (11KB),
  `CommunityCommentsModerationTable.tsx`.

**Rationale**: An educational platform for minors MUST maintain strict content
moderation. Unmoderated social features would expose students to inappropriate
content and create legal liability.

### XV. Phase Verification, Docker Gates & Manual QA

Every implementation phase MUST close with evidence that it works in the real
project structure, not only in isolated code:

- **Phase Scope**: Each phase MUST define its backend, frontend, worker,
  database, and Docker impact before implementation starts. Work outside that
  scope MUST be deferred to a later phase or documented as an explicit
  exception.
- **Automated Tests**: Each phase MUST include automated tests for its critical
  paths. Backend changes require `dotnet test` coverage where business logic is
  introduced. Frontend user journeys require Playwright or equivalent E2E
  coverage. Worker changes require at least build verification and job-level
  tests or documented stubs. Python smoke/API tests MUST be added when the
  phase introduces cross-service workflows.
- **Existing Regression**: Existing relevant tests MUST pass before the phase is
  marked complete. Failing tests cannot be waived without a documented reason
  and a follow-up issue tied to the phase.
- **Docker Gate**: Every phase MUST run `docker compose config -q`, start the
  Docker stack with `make up`, apply migrations with `make migrate` when schema
  changes exist, and verify service health (`backend`, `worker`, and each
  frontend surface). If a required external secret is unavailable locally, the
  blocked check MUST be documented and the remaining Docker checks MUST still
  run.
- **Manual QA**: Each phase MUST list the exact flows the product owner must
  test manually, including user role, URL/surface, action, expected result, and
  any negative permission test.
- **End-of-Phase Report**: Each phase MUST end with a report containing:
  implemented scope, commands run, automated test results, Docker gate result,
  manual QA checklist, known risks, and whether the next phase may start.
- **No Hidden Failure Rule**: A failed phase gate MUST be fixed inside the same
  phase. Later phases MUST NOT be used to hide, skip, or normalize unresolved
  failures from earlier phases.

**Rationale**: The platform is now large enough that feature work can appear
complete while breaking Docker, role boundaries, or cross-service workflows.
Phase-close evidence prevents silent regressions before the next scope begins.

## Technology Stack & Constraints

### Mandatory Stack (Verified from Source — 2026-06-01)

| Layer              | Technology                            | Verified Version             | Source File                          |
|--------------------|---------------------------------------|------------------------------|--------------------------------------|
| Backend Framework  | ASP.NET Core Web API (C#)             | .NET 9.0 (`net9.0`)         | All 4 `.csproj` files               |
| C# Language        | C# 13                                 | Default for net9.0           | No `LangVersion` override           |
| ORM                | Entity Framework Core                 | 9.0.6                        | All `.csproj` PackageReference       |
| DB Provider        | Npgsql.EntityFrameworkCore.PostgreSQL  | 9.0.4                        | Infrastructure.csproj                |
| CQRS/Mediator      | MediatR                               | 12.4.1                       | Application.csproj                   |
| Validation         | FluentValidation                      | 11.11.0                      | Application.csproj                   |
| Auth Tokens        | System.IdentityModel.Tokens.Jwt       | 8.3.0                        | Infrastructure.csproj                |
| JWT Bearer         | Microsoft.AspNetCore.Authentication   | 9.0.6                        | Infrastructure.csproj                |
| Password Hash      | BCrypt.Net-Next                       | 4.0.3                        | Application + Infrastructure.csproj  |
| Redis Client       | StackExchange.Redis                   | 2.12.4                       | Application + Infrastructure.csproj  |
| Redis Cache        | Microsoft.Extensions.Caching.SE.Redis | 10.0.5                       | API.csproj                           |
| API Docs           | Swashbuckle.AspNetCore                | 6.6.2                        | API.csproj                           |
| QR Generation      | QRCoder                               | 1.7.0                        | Infrastructure.csproj                |
| Frontend Framework | Next.js                               | 16.2.1                       | frontend/package.json                |
| React              | React + React DOM                     | 19.2.4                       | frontend/package.json                |
| TypeScript         | TypeScript                            | ^5                           | frontend/package.json                |
| Styling            | Tailwind CSS                          | ^4 (`@tailwindcss/postcss`)  | frontend/package.json devDependencies|
| UI Components      | Shadcn                                | 4.1.0                        | frontend/package.json                |
| Radix              | @radix-ui/react-slot                  | 1.2.4                        | frontend/package.json                |
| Base UI            | @base-ui/react                        | 1.3.0                        | frontend/package.json                |
| CVA                | class-variance-authority              | 0.7.1                        | frontend/package.json                |
| Classnames         | clsx + tailwind-merge                 | 2.1.1 / 3.5.0               | frontend/package.json                |
| Animations         | framer-motion / motion                | 12.38.0                      | frontend/package.json                |
| Animations (Adv)   | GSAP + @gsap/react                    | 3.14.2 / 2.1.2              | frontend/package.json                |
| 3D/WebGL           | Three.js + OGL                        | 0.183.2 / 1.0.11            | frontend/package.json                |
| Icons              | lucide-react                          | 1.7.0                        | frontend/package.json                |
| HTTP Client        | Axios                                 | 1.13.6                       | frontend/package.json                |
| State Management   | Zustand                               | 5.0.12                       | frontend/package.json                |
| Toast              | react-hot-toast                       | 2.6.0                        | frontend/package.json                |
| Rich Text Editor   | react-quill-new                       | 3.8.3                        | frontend/package.json                |
| QR Scan/Gen        | qrcode.react + react-qr-scanner      | 4.2.0 / 2.5.1               | frontend/package.json                |
| HTML Parser        | Cheerio                               | 1.2.0                        | frontend/package.json                |
| React Compiler     | babel-plugin-react-compiler           | 1.0.0                        | frontend/package.json devDependencies|
| E2E Testing        | @playwright/test                      | 1.58.2                       | frontend/package.json devDependencies|
| Linting            | ESLint + eslint-config-next           | ^9 / 16.2.1                 | frontend/package.json devDependencies|
| Formatting         | Prettier                              | 3.8.1                        | frontend/package.json devDependencies|
| Database           | PostgreSQL                            | 16 (Alpine)                  | docker-compose.yml                   |
| Cache/Queue Broker | Redis                                 | 7 (Alpine)                   | docker-compose.yml                   |
| Worker Runtime     | Node.js                               | 20 (Slim)                    | worker/Dockerfile                    |
| Job Queue          | BullMQ                                | 5.71.1                       | worker/package.json                  |
| Worker HTTP        | Express                               | 5.2.1                        | worker/package.json                  |
| Job Dashboard      | @bull-board/express + api             | 6.20.6                       | worker/package.json                  |
| AI SDK             | @google/genai (Gemini)                | 1.47.0                       | worker/package.json                  |
| Audio Processing   | fluent-ffmpeg                         | 2.1.3                        | worker/package.json                  |
| Video Download     | youtube-dl-exec (yt-dlp)              | 3.1.4                        | worker/package.json                  |
| HTTP (Worker)      | undici                                | 7.24.6                       | worker/package.json                  |
| Redis (Worker)     | ioredis                               | 5.10.1                       | worker/package.json                  |
| DB (Worker)        | pg (node-postgres)                    | 8.20.0                       | worker/package.json                  |
| Worker TS          | TypeScript                            | 5.9.3                        | worker/package.json                  |

### Deployment Architecture

- **6 Docker services** (5 always-on + 1 on-demand migrator):
  - `nadergorge_frontend` — Next.js standalone (port **8738**)
  - `nadergorge_backend` — ASP.NET Core (port **5245**,
    Swagger at `/swagger`)
  - `nadergorge_worker` — Node.js BullMQ + Express (port **3001**,
    Bull Board at `/ui`)
  - `nadergorge_db` — PostgreSQL 16 Alpine (port **5432**,
    volume: `pgdata`)
  - `nadergorge_redis` — Redis 7 Alpine (port **6379**,
    volume: `redisdata`)
  - `nadergorge_migrator` — EF Core migration runner (on-demand,
    `--profile migration`)
- **Infra-Only Compose** (`docker/docker-compose.infra-only.yml`): db + redis +
  `telegram-bot-api` (aiogram/telegram-bot-api, port **8081**, volume:
  `tgdata` — local Telegram Bot API server, up to 2GB file support).
- **Network**: All services on `nadergorge_net` bridge network.
- **Make Targets** (20 targets):
  - Docker: `up`, `down`, `build`, `build-frontend/backend/worker`, `restart`,
    `ps`, `clean` (⚠️ destroys volumes).
  - Logs: `logs`, `logs-frontend/backend/worker/db/redis`.
  - Shells: `shell-frontend/backend/worker/db`.
  - Migrations: `migrate`, `migrate-add NAME=X`.
  - Native: `dev` (all services), `frontend`, `backend`, `stop`.
- **Environment Variables** (17 variables in `.env.example`):
  - Required: `JWT_SECRET` (32+ chars), `API_CALLBACK_SECRET`, `GEMINI_API_KEY`.
  - Optional: `EVOLUTION_API_BASE_URL`, `EVOLUTION_API_KEY`,
    `EVOLUTION_API_INSTANCE`.
  - Defaults: `POSTGRES_USER=postgres`, `POSTGRES_DB=nadergorge`,
    `JWT_ISSUER=NaderGorgeAPI`, `JWT_AUDIENCE=NaderGorgeClients`,
    `JWT_EXPIRY_MINUTES=60`, `JWT_REFRESH_DAYS=30`,
    `MAX_DEVICES_PER_STUDENT=2`, `CORS_ALLOWED_ORIGINS=http://localhost:8738`.

### Performance Expectations

- API response time: < 500ms p95 for standard CRUD operations.
- Video page load: < 3 seconds to first content render.
- Code redemption: < 2 seconds end-to-end including confirmation.
- Concurrent users: System MUST handle the expected student base per academic
  year without degradation.
- Platform settings cache: 10-minute TTL via `CachedPlatformSettingsReader`.
- Frontend package cache: 10-second in-memory TTL in `content-service.ts`.

## Development Workflow & Quality Gates

### Code Quality

- **TypeScript strict mode**: Frontend code MUST compile with strict TypeScript
  configuration (`"strict": true`).
- **C# nullable reference types**: All 4 backend projects enable
  `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>`.
- **Linting**: ESLint 9 with `eslint-config-next 16.2.1` + `eslint-config-
  prettier 10.1.8` + `eslint-plugin-prettier 5.5.5` for frontend. .NET
  analyzers for backend.
- **Formatting**: Prettier 3.8.1 (`.prettierrc` + `.prettierignore`).
- **Code review**: All changes MUST be reviewed before merging.

### Testing Strategy

- **E2E tests**: Playwright `^1.58.2` with 10 test suites:
  `auth`, `admin-users`, `admin-content`, `assistant-dashboard`, `codes`,
  `codes-wallet`, `package-code-profiles`, `parent-report`,
  `student-academic`, `student-journey`.
  Config: Chromium only, 1 worker (sequential for shared DB), 30s timeout,
  2 retries on CI.
- **Test Infrastructure**: `E2eTestingController` (12KB) provides test data
  seeding and cleanup. Global setup via `tests/fixtures/global-setup.ts`.
  Auth helpers via `tests/fixtures/auth-helpers.ts`.
- **Backend unit tests**: Test projects in `backend/tests/`.
- **Database migration tests**: Migrations MUST be tested against a clean
  database before deployment.

### Branch & Release Strategy

- **Feature branches**: Format `###-feature-name` (e.g., `064-full-docker-
  setup`). Never work directly on main.
- **Semantic versioning**: Releases MUST follow MAJOR.MINOR.PATCH versioning.
- **Spec-Kit workflow**: `/speckit-specify` → `/speckit-plan` →
  `/speckit-tasks` → `/speckit-implement`. Spec-Kit version: 0.3.1. 64 feature
  specs in `specs/001-*` through `specs/064-*`. Templates in
  `.specify/templates/` (6 files). Scripts in `.specify/scripts/bash/` (5
  files: `check-prerequisites.sh`, `common.sh`, `create-new-feature.sh`,
  `setup-plan.sh`, `update-agent-context.sh`).
- **Changelog**: Every release MUST include a changelog entry.

### Definition of Done

A feature is considered "done" when:

1. Code is written, reviewed, and merged.
2. All existing tests pass (`npm run lint` + Playwright suite).
3. New tests cover the feature's critical paths.
4. API documentation is updated (Swagger auto-generated).
5. Database migrations are included (if applicable).
6. The feature works in Docker environment (`make up && make migrate`).
7. The phase report documents automated tests, Docker gate results, and manual
   QA still required from the product owner.
8. The next phase is not started until the current phase's failed gates are
   fixed or explicitly documented with owner-approved risk.

## Governance

- This constitution supersedes all ad-hoc decisions and informal agreements
  regarding architecture, technology choices, and development practices.
- **Amendments**: Any change to this constitution MUST be documented with a
  version bump, rationale, and impact assessment. MAJOR changes (principle
  removal or redefinition) require explicit stakeholder approval.
- **Compliance review**: At the start of each new phase (Phase 1, 2, 3, etc.),
  the constitution MUST be reviewed to ensure principles still align with
  evolving requirements.
- **Versioning policy**: Constitution versions follow semantic versioning —
  MAJOR for backward-incompatible governance changes, MINOR for new principles
  or expanded guidance, PATCH for clarifications and typo fixes.
- **Conflict resolution**: When a technical decision conflicts with a
  constitution principle, the principle takes precedence unless an explicit
  exception is documented in the Complexity Tracking section of the relevant
  plan.md.
- Use `.specify/memory/constitution.md` as the single source of truth for
  governance.

**Version**: 3.1.0 | **Ratified**: 2026-03-19 | **Last Amended**: 2026-06-07
