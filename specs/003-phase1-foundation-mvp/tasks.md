# Tasks: Phase 1 — Foundation and MVP Launch

**Input**: Design documents from `/specs/003-phase1-foundation-mvp/`
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/ ✅

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3, US4)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Initialize the monorepo, tooling, and Docker dev environment

- [X] T001 Create monorepo directory structure: `backend/`, `frontend/`, `worker/`, `docker/` per plan.md project layout
- [X] T002 Initialize .NET solution: `dotnet new sln -n NaderGorge` and create four projects: `NaderGorge.Domain`, `NaderGorge.Application`, `NaderGorge.Infrastructure`, `NaderGorge.API` in `backend/src/`
- [X] T003 [P] Initialize Next.js 14 (App Router) project with TypeScript in `frontend/` using `npx create-next-app@latest`
- [X] T004 [P] Initialize BullMQ worker Node.js project in `worker/` with `package.json` and `tsconfig.json`
- [X] T005 [P] Create `docker/docker-compose.yml` with PostgreSQL 16 and Redis 7 services
- [X] T006 [P] Configure Tailwind CSS, Shadcn/UI, and Framer Motion in `frontend/`
- [X] T007 [P] Configure ESLint + Prettier for `frontend/` and .NET analyzers for `backend/`
- [X] T008 [P] Create `.env.example` files for backend, frontend, and worker with all required environment variables from quickstart.md

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can begin

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T009 Create base domain entities: `User`, `Role`, `UserRole` in `backend/src/NaderGorge.Domain/Entities/`
- [X] T010 [P] Configure Entity Framework Core DbContext with PostgreSQL connection in `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs`
- [X] T011 [P] Setup MediatR pipeline with validation behaviors in `backend/src/NaderGorge.Application/Common/`
- [X] T012 Create initial EF Core migration for `users`, `roles`, `user_roles` tables: `dotnet ef migrations add InitialCreate`
- [X] T013 [P] Implement JWT token generation service (access + refresh tokens) in `backend/src/NaderGorge.Infrastructure/Services/TokenService.cs`
- [X] T014 [P] Implement JWT authentication middleware and RBAC authorization policy in `backend/src/NaderGorge.API/Middleware/`
- [X] T015 [P] Create global error handling middleware with consistent error response format in `backend/src/NaderGorge.API/Middleware/ExceptionHandlingMiddleware.cs`
- [X] T016 [P] Create structured logging configuration with correlation IDs in `backend/src/NaderGorge.API/Configuration/LoggingConfig.cs`
- [X] T017 [P] Create health check endpoint at `/api/health` in `backend/src/NaderGorge.API/Controllers/HealthController.cs`
- [X] T018 [P] Setup Redis connection with StackExchange.Redis in `backend/src/NaderGorge.Infrastructure/Cache/RedisConnectionFactory.cs`
- [X] T019 [P] Create rate limiting middleware for auth and code routes in `backend/src/NaderGorge.API/Middleware/RateLimitingMiddleware.cs`
- [X] T020 Create database seed command for default roles (Admin, Teacher, Assistant, Student) and initial admin user in `backend/src/NaderGorge.Infrastructure/Data/Seeder.cs`
- [X] T021 [P] Setup Axios/fetch API client wrapper with JWT interceptor in `frontend/src/services/api-client.ts`
- [X] T022 [P] Create auth Zustand store for managing tokens and user state in `frontend/src/stores/auth-store.ts`
- [X] T023 [P] Create base layout components: Navigation, Sidebar, Breadcrumbs, Footer in `frontend/src/components/layout/`
- [X] T024 [P] Setup Next.js App Router layouts: `(public)/layout.tsx`, `student/layout.tsx`, `admin/layout.tsx` in `frontend/src/app/`
- [X] T025 Create `AuditLog` entity in `backend/src/NaderGorge.Domain/Entities/AuditLog.cs` and audit logging service in `backend/src/NaderGorge.Application/Services/AuditService.cs`
- [X] T026 Add EF Core migration for `audit_logs` table

**Checkpoint**: Foundation ready — user story implementation can now begin in parallel

---

## Phase 3: User Story 1 — Student Onboarding & Code Activation (Priority: P1) 🎯 MVP

**Goal**: A new student can register (Step 1), log in, complete profile (Step 2), and activate an access code to gain content access.

**Independent Test**: Register a new student → log in → attempt code activation → complete Step 2 modal → redeem code → verify Package/Lesson access is granted.

### Implementation for User Story 1

- [X] T027 [P] [US1] Create `StudentProfile` entity in `backend/src/NaderGorge.Domain/Entities/StudentProfile.cs`
- [X] T028 [P] [US1] Create `Device` and `RefreshToken` entities in `backend/src/NaderGorge.Domain/Entities/`
- [X] T029 [P] [US1] Create `CodeGroup`, `AccessCode`, `StudentAccessGrant` entities in `backend/src/NaderGorge.Domain/Entities/`
- [X] T030 [US1] Add EF Core migration for `student_profiles`, `devices`, `refresh_tokens`, `code_groups`, `access_codes`, `student_access_grants` tables
- [X] T031 [P] [US1] Implement RegisterCommand handler (Step 1: name, phone, password, grade, track) in `backend/src/NaderGorge.Application/Features/Auth/Commands/RegisterCommand.cs`
- [X] T032 [P] [US1] Implement LoginCommand handler with device fingerprint check and device limit enforcement in `backend/src/NaderGorge.Application/Features/Auth/Commands/LoginCommand.cs`
- [X] T033 [US1] Implement RefreshTokenCommand handler in `backend/src/NaderGorge.Application/Features/Auth/Commands/RefreshTokenCommand.cs`
- [X] T034 [US1] Implement CompleteProfileCommand handler (Step 2: parent phone, governorate, city, school) in `backend/src/NaderGorge.Application/Features/Auth/Commands/CompleteProfileCommand.cs`
- [X] T035 [US1] Create AuthController with register, login, refresh, logout, complete-profile endpoints in `backend/src/NaderGorge.API/Controllers/AuthController.cs`
- [X] T036 [US1] Implement ActivateCodeCommand handler: validate code, check profile completion (trigger Step 2 if needed), consume code, create StudentAccessGrant, log activation in `backend/src/NaderGorge.Application/Features/Codes/Commands/ActivateCodeCommand.cs`
- [X] T037 [US1] Create CodesController with `/api/codes/activate` endpoint in `backend/src/NaderGorge.API/Controllers/CodesController.cs`
- [X] T038 [P] [US1] Create Registration Form (Step 1) component in `frontend/src/components/forms/RegistrationForm.tsx`
- [X] T039 [P] [US1] Create Login Form component in `frontend/src/components/forms/LoginForm.tsx`
- [X] T040 [P] [US1] Create Profile Completion Modal (Step 2) component in `frontend/src/components/forms/ProfileCompletionModal.tsx`
- [X] T041 [P] [US1] Create Code Activation Form component in `frontend/src/components/forms/CodeActivationForm.tsx`
- [X] T042 [US1] Create Register page at `frontend/src/app/(public)/register/page.tsx`
- [X] T043 [US1] Create Login page at `frontend/src/app/(public)/login/page.tsx`
- [X] T044 [US1] Create Code Redemption page at `frontend/src/app/student/code-redemption/page.tsx` with Step 2 modal integration
- [X] T045 [US1] Wire auth API service layer: register, login, refresh, completeProfile, activateCode in `frontend/src/services/auth-service.ts`

**Checkpoint**: A student can fully register, log in (with device enforcement), complete their profile, and redeem an access code.

---

## Phase 4: User Story 2 — Content Consumption & Video Tracking (Priority: P2)

**Goal**: An enrolled student can browse unlocked packages, drill into lessons, watch videos with real-time tracking, and experience hard-lock enforcement when limits are reached.

**Independent Test**: Log in as a student with active access → navigate to a Package → open a Lesson → play a video → verify watch events are logged → exhaust watch limit → verify playback is locked.

### Implementation for User Story 2

- [X] T046 [P] [US2] Create `Program`, `Package`, `ContentSection`, `Lesson`, `LessonVideo`, `LessonResource` entities in `backend/src/NaderGorge.Domain/Entities/`
- [X] T047 [P] [US2] Create `VideoWatchEvent`, `LessonProgress` entities in `backend/src/NaderGorge.Domain/Entities/`
- [X] T048 [US2] Add EF Core migration for `programs`, `packages`, `content_sections`, `lessons`, `lesson_videos`, `lesson_resources`, `video_watch_events`, `lesson_progress` tables
- [X] T049 [P] [US2] Implement `IVideoProvider` interface in `backend/src/NaderGorge.Domain/Interfaces/IVideoProvider.cs` and `YouTubeVideoProvider` in `backend/src/NaderGorge.Infrastructure/Providers/YouTubeVideoProvider.cs`
- [X] T050 [US2] Implement GetPackagesQuery, GetSectionsQuery, GetLessonsQuery, GetLessonDetailQuery handlers in `backend/src/NaderGorge.Application/Features/Content/Queries/`
- [X] T051 [US2] Implement access-check logic: verify StudentAccessGrant before returning lesson content in `backend/src/NaderGorge.Application/Services/AccessCheckService.cs`
- [X] T052 [US2] Create ContentController with `/api/packages`, `/api/sections/:id/lessons`, `/api/lessons/:id` endpoints in `backend/src/NaderGorge.API/Controllers/ContentController.cs`
- [X] T053 [US2] Implement RecordVideoEventCommand handler with cumulative watch tracking and hard-lock enforcement in `backend/src/NaderGorge.Application/Features/Tracking/Commands/RecordVideoEventCommand.cs`
- [X] T054 [US2] Create TrackingController with `/api/tracking/video-event` endpoint in `backend/src/NaderGorge.API/Controllers/TrackingController.cs`
- [X] T055 [P] [US2] Create VideoPlayer component with provider abstraction, speed lock, and event heartbeat in `frontend/src/components/content/VideoPlayer.tsx`
- [X] T056 [P] [US2] Create LessonViewer component (summary, video, resources, exam link) in `frontend/src/components/content/LessonViewer.tsx`
- [X] T057 [P] [US2] Create PackageCard and SectionList components in `frontend/src/components/content/`
- [X] T058 [US2] Create Packages listing page at `frontend/src/app/student/packages/page.tsx`
- [X] T059 [US2] Create Section listing page at `frontend/src/app/student/packages/[packageId]/page.tsx`
- [X] T060 [US2] Create Lesson detail page at `frontend/src/app/student/packages/[packageId]/lessons/[lessonId]/page.tsx`
- [X] T061 [US2] Wire content API service layer: getPackages, getSections, getLessons, getLessonDetail, recordVideoEvent in `frontend/src/services/content-service.ts`

**Checkpoint**: A student can browse content, watch videos with heartbeat tracking, and is hard-locked when watch limits are exceeded.

---

## Phase 5: User Story 3 — Basic Exam Completion & Gating (Priority: P3)

**Goal**: A student can take MCQ exams attached to lessons, receive instant grades, and is blocked from progressing to the next lesson if they fail (with Teacher/Admin manual override).

**Independent Test**: Open an exam → submit answers → verify instant score → fail on purpose → verify next lesson is locked → admin unlocks → verify student can proceed.

### Implementation for User Story 3

- [X] T062 [P] [US3] Create `Exam`, `QuestionBankItem`, `QuestionOption`, `ExamQuestion`, `StudentExamAttempt`, `StudentAnswer` entities in `backend/src/NaderGorge.Domain/Entities/`
- [X] T063 [US3] Add EF Core migration for `exams`, `question_bank_items`, `question_options`, `exam_questions`, `student_exam_attempts`, `student_answers` tables
- [X] T064 [US3] Implement StartExamQuery handler (load questions with shuffled options) in `backend/src/NaderGorge.Application/Features/Exams/Queries/StartExamQuery.cs`
- [X] T065 [US3] Implement SubmitExamCommand handler with auto-grading, pass/fail check, and LessonProgress update (BLOCKED if fail) in `backend/src/NaderGorge.Application/Features/Exams/Commands/SubmitExamCommand.cs`
- [X] T066 [US3] Implement ManualUnlockCommand handler (Teacher/Admin/Assistant unlocks next lesson, creates audit log) in `backend/src/NaderGorge.Application/Features/Exams/Commands/ManualUnlockCommand.cs`
- [X] T067 [US3] Create ExamsController with `/api/exams/:id/start`, `/api/exams/:id/submit`, `/api/admin/lessons/:id/students/:id/unlock` endpoints in `backend/src/NaderGorge.API/Controllers/ExamsController.cs`
- [X] T068 [P] [US3] Create ExamViewer component (question display, option selection, timer, submit) in `frontend/src/components/exams/ExamViewer.tsx`
- [X] T069 [P] [US3] Create Exam attempt page at `frontend/src/app/student/exams/[examId]/page.tsx`
- [X] T070 [US3] Wire exam API service layer in `frontend/src/services/exam-service.ts`
- [X] T071 [US3] Update LessonViewer to link to Exam page with `?packageId=XXX`
- [X] T072 [US3] Add gating indicator to LessonViewer: show locked state with "Pass the exam first" or "Contact your teacher" message in `frontend/src/components/content/LessonViewer.tsx`

**Checkpoint**: Students can take MCQ exams, receive instant scores, and progression is gated based on pass/fail with manual override support.

---

## Phase 6: User Story 4 — Admin Panel & Code Management (Priority: P4)

**Goal**: Admin can manage users (list, disable, manage devices), create and manage content (Packages → Sections → Lessons → Videos), generate code batches via BullMQ, and view question bank.

**Independent Test**: Log in as Admin → create a full content hierarchy → generate 100 codes → export codes → disable a student → remove a student's device → verify audit logs.

### Implementation for User Story 4

- [X] T073 [US4] Implement Admin CRUD handlers for Users: ListUsersQuery, UpdateUserStatusCommand, GetUserDevicesQuery, RemoveDeviceCommand in `backend/src/NaderGorge.Application/Features/Admin/`
- [X] T074 [US4] Implement Admin CRUD handlers for Content: CreatePackageCommand, CreateSectionCommand, CreateLessonCommand, CreateVideoCommand, UpdateX, DeleteX in `backend/src/NaderGorge.Application/Features/Admin/`
- [X] T075 [US4] Implement BulkGenerateCodesCommand: push job to Redis queue for worker in `backend/src/NaderGorge.Application/Features/Admin/Commands/BulkGenerateCodesCommand.cs`
- [X] T076 [US4] Implement ListCodeGroupsQuery, GetCodeGroupCodesQuery (with CSV export) in `backend/src/NaderGorge.Application/Features/Admin/Queries/`
- [X] T077 [US4] Implement basic Question Bank CRUD: CreateQuestionCommand, ListQuestionsQuery in `backend/src/NaderGorge.Application/Features/Admin/`
- [X] T078 [US4] Create AdminController with all admin endpoints for users, content, codes, questions in `backend/src/NaderGorge.API/Controllers/AdminController.cs`
- [X] T079 [US4] Implement code generation job processor in `worker/src/index.ts`
- [X] T080 [US4] Setup queue definitions and worker entry point in `worker/src/index.ts`
- [X] T081 [P] [US4] Create Admin Users management page (list, disable, device management) at `frontend/src/app/admin/users/page.tsx`
- [X] T082 [P] [US4] Create Admin Content management pages (Packages, Sections, Lessons, Videos CRUD) at `frontend/src/app/admin/content/`
- [X] T083 [P] [US4] Create Admin Code Groups page (generate batch, view codes, export CSV) at `frontend/src/app/admin/codes/page.tsx`
- [X] T084 [P] [US4] Create Admin Question Bank page (create/edit MCQ questions) at `frontend/src/app/admin/questions/page.tsx`
- [X] T085 [US4] Create Admin Watch Limit Reset and Manual Lesson Unlock pages at `frontend/src/app/admin/overrides/page.tsx`
- [X] T086 [US4] Wire admin API service layer: all admin CRUD endpoints in `frontend/src/services/admin-service.ts`

**Checkpoint**: Admin has full operational control of users, content, codes, question bank, and student overrides.

---

## Phase 7: User Story 5 — Student Dashboard & Public Website (Priority: P5)

**Goal**: Students see a personalized dashboard with progress tracking. Public visitors see a polished landing page with package overview and FAQ.

**Independent Test**: Visit public site → see landing page → navigate to About/FAQ → register → log in → see dashboard with active packages, resume button, progress, and recent codes.

### Implementation for User Story 5

- [X] T087 [US5] Implement GetDashboardQuery handler (aggregate active packages, current lesson, upcoming exams, progress %, recent codes) in `backend/src/NaderGorge.Application/Features/Student/Queries/GetDashboardQuery.cs`
- [X] T088 [US5] Implement GetProgressQuery handler in `backend/src/NaderGorge.Application/Features/Student/Queries/GetProgressQuery.cs`
- [X] T089 [US5] Create StudentController with `/api/student/dashboard`, `/api/student/progress` endpoints in `backend/src/NaderGorge.API/Controllers/StudentController.cs`
- [X] T090 [P] [US5] Create public Landing page with hero, features, package overview, testimonials at `frontend/src/app/page.tsx`
- [X] T091 [P] [US5] Create About Teacher page at `frontend/src/app/about/page.tsx`
- [X] T092 [P] [US5] Create FAQ page at `frontend/src/app/faq/page.tsx`
- [X] T093 [US5] Create Student Dashboard page with DashboardCard components (resume, packages, exams, progress, codes) at `frontend/src/app/student/page.tsx`
- [X] T094 [US5] Wire student API service layer: getDashboard, getProgress in `frontend/src/services/student-service.ts`

**Checkpoint**: Public site is polished and student dashboard provides a complete operational overview.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Quality improvements affecting all user stories

- [X] T095 [P] Add responsive mobile CSS across all pages and components in `frontend/src/styles/`
- [X] T096 [P] Add Framer Motion page transitions and micro-animations in `frontend/src/components/layout/PageTransition.tsx`
- [X] T097 [P] Add loading skeletons and empty states for all data-fetching pages in `frontend/src/components/ui/`
- [X] T098 Add consistent error toast notifications across all pages in `frontend/src/components/ui/Toast.tsx`
- [X] T099 Security review: ensure all admin endpoints check RBAC, all inputs validated both frontend and backend
- [X] T100 Run quickstart.md validation: follow all steps from scratch and verify smoke test passes
- [X] T101 [P] Add SEO meta tags (title, description) to all public pages in `frontend/src/app/(public)/`
- [X] T102 Performance audit: ensure API < 500ms p95, Video page < 3s TTI, Code gen < 5s for 1000 codes

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion — BLOCKS all user stories
- **User Stories (Phase 3–7)**: All depend on Foundational phase completion
  - US1 (Auth/Codes) can start immediately after Foundational
  - US2 (Content) can start after Foundational (independent of US1)
  - US3 (Exams) depends on US2 content entities being available
  - US4 (Admin) depends on US1 + US2 entities but can start backend work in parallel
  - US5 (Dashboard/Public) depends on US1 + US2 for data aggregation
- **Polish (Phase 8)**: Depends on all user stories being substantially complete

### User Story Dependencies

- **US1 (P1)**: Can start after Foundational — No other story dependency ✅
- **US2 (P2)**: Can start after Foundational — No dependency on US1 ✅
- **US3 (P3)**: Depends on US2 content entities (Lesson, Exam FK) — Start after T048
- **US4 (P4)**: Depends on US1 + US2 entities — Start after T030 + T048, but backend handlers can parallelize
- **US5 (P5)**: Depends on US1 + US2 for dashboard aggregation — Frontend pages can start in parallel

### Parallel Opportunities

- **Phase 1**: T003, T004, T005, T006, T007, T008 are all parallelizable
- **Phase 2**: T010, T011, T013, T014, T15, T016, T017, T018, T019 are parallelizable
- **US1 + US2**: Backend models (T027–T029 and T046–T047) can start in parallel after Foundational
- **Within each US**: Frontend components marked [P] can be built simultaneously

---

## Parallel Example: User Story 1

```bash
# Launch all models together:
Task: "Create StudentProfile entity in backend/src/NaderGorge.Domain/Entities/StudentProfile.cs"
Task: "Create Device and RefreshToken entities in backend/src/NaderGorge.Domain/Entities/"
Task: "Create CodeGroup, AccessCode, StudentAccessGrant entities in backend/src/NaderGorge.Domain/Entities/"

# Launch all frontend forms together:
Task: "Create Registration Form in frontend/src/components/forms/RegistrationForm.tsx"
Task: "Create Login Form in frontend/src/components/forms/LoginForm.tsx"
Task: "Create Profile Completion Modal in frontend/src/components/forms/ProfileCompletionModal.tsx"
Task: "Create Code Activation Form in frontend/src/components/forms/CodeActivationForm.tsx"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL — blocks all stories)
3. Complete Phase 3: User Story 1 (Auth + Codes)
4. **STOP and VALIDATE**: Test US1 independently
5. Deploy/demo if ready

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. US1 (Auth + Codes) → Test → Deploy (MVP!)
3. US2 (Content + Video) → Test → Deploy
4. US3 (Exams + Gating) → Test → Deploy
5. US4 (Admin) → Test → Deploy
6. US5 (Dashboard + Public) → Test → Deploy
7. Polish → Final Deploy

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
