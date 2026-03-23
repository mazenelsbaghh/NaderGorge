# Tasks: Phase 0 — Discovery, Planning, and Product Blueprint

**Input**: Design documents from `/specs/001-phase0-discovery-blueprint/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, quickstart.md

**Tests**: Not applicable — Phase 0 is a documentation phase. Validation is manual review against spec acceptance scenarios.

**Organization**: Tasks are grouped by user story to enable independent writing and review of each deliverable set.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2)
- Include exact file paths in descriptions

---

## Phase 1: Setup

**Purpose**: Create deliverables directory and establish document structure

- [x] T001 Create deliverables directory at `specs/001-phase0-discovery-blueprint/deliverables/`
- [x] T002 [P] Create placeholder files for all 10 deliverables with headers and required section structure from `data-model.md`

**Checkpoint**: Directory and file structure ready for content writing

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Write the Product Requirements Document (PRD) — all other deliverables depend on the platform definition established here

**⚠️ CRITICAL**: No user story deliverable can be finalized until the PRD is reviewed

- [x] T003 Write PRD platform identity section: define as full academic control system (not content site) in `deliverables/01-product-requirements.md`
- [x] T004 Write PRD target audience section: enumerate all segments (4 student grades, parents, assistants, admin) with descriptions in `deliverables/01-product-requirements.md`
- [x] T005 Write PRD brand identity section: document teacher style (youthful, simple, energetic, story-based, maps/tables, motivating) in `deliverables/01-product-requirements.md`
- [x] T006 Write PRD value proposition section: differentiate from generic course platforms in `deliverables/01-product-requirements.md`
- [x] T007 Write PRD content hierarchy overview: Package → Content Section → Lesson → components in `deliverables/01-product-requirements.md`
- [x] T008 Write PRD academic flow section: Exam → Lesson → Homework cycle in `deliverables/01-product-requirements.md`

**Checkpoint**: PRD complete — user story deliverables can now begin in parallel

---

## Phase 3: User Story 1 — Product Identity & Academic Structure (Priority: P1) 🎯 MVP

**Goal**: Produce the Content Blueprint that defines the full content hierarchy and lesson components

**Independent Test**: Review Content Blueprint and confirm every element of the academic flow appears in the hierarchy

### Implementation for User Story 1

- [x] T009 [US1] Write content hierarchy definition: Package → Content Section → Lesson in `deliverables/02-content-blueprint.md`
- [x] T010 [US1] Write lesson components section: Videos (multiple), written summary, short questions, homework, short quiz, downloadable file, mind map, revision content in `deliverables/02-content-blueprint.md`
- [x] T011 [US1] Write Content Section terminology note: canonical term documentation, "months" as internal alias only (not in API/UI/data model) in `deliverables/02-content-blueprint.md`
- [x] T012 [US1] Write package structure section: multiple Content Sections per package, multiple Lessons per Content Section in `deliverables/02-content-blueprint.md`

**Checkpoint**: Content Blueprint complete and independently reviewable

---

## Phase 4: User Story 2 — Access Model & Code System (Priority: P1)

**Goal**: Produce the Access Blueprint and Data Blueprint documenting the code-based monetization strategy and student data requirements

**Independent Test**: Walk through 5 code activation scenarios (lesson, package, term, stacked, expired) against the Access Blueprint and confirm deterministic outcomes

### Implementation for User Story 2

- [x] T013 [P] [US2] Write code types section: lesson code, package code, term code, promotional (future), referral (future) with scope/duration/stacking per type in `deliverables/03-access-blueprint.md`
- [x] T014 [P] [US2] Write student data fields section: full name, phone, parent phone, grade, study track, governorate, city/district, school in `deliverables/04-data-blueprint.md`
- [x] T015 [US2] Write code behaviors section: single-use, code groups/batches, content-based logic, duration-based logic in `deliverables/03-access-blueprint.md`
- [x] T016 [US2] Write activation flow section: Entry → Validation → Confirmation → Selected date → Access grant in `deliverables/03-access-blueprint.md`
- [x] T017 [US2] Write stacking rules matrix: per-type stacking combinations in `deliverables/03-access-blueprint.md`
- [x] T018 [US2] Write expiration rules section: pre-usage expiration, post-activation duration limits in `deliverables/03-access-blueprint.md`
- [x] T019 [US2] Write conflict resolution section: overlapping content access from multiple code types in `deliverables/03-access-blueprint.md`
- [x] T020 [US2] Write engagement data section: watch time, lesson completion, homework completion, exam performance, inactivity in `deliverables/04-data-blueprint.md`
- [x] T021 [US2] Write history data section: package history, code history, activation logs in `deliverables/04-data-blueprint.md`
- [x] T022 [US2] Write parent data section: contact info, linked student(s) in `deliverables/04-data-blueprint.md`

**Checkpoint**: Access Blueprint and Data Blueprint complete — 5 code scenarios walkable

---

## Phase 5: User Story 3 — Technical Architecture & System Blueprint (Priority: P2)

**Goal**: Produce the Technical Architecture Document, Data Model Draft, and System Blueprint documenting the full tech stack, all entities, and deployment structure

**Independent Test**: A developer can read these documents and set up the repository structure with all dependencies without additional guidance

### Implementation for User Story 3

- [x] T023 [P] [US3] Write frontend stack section: Next.js, TypeScript, Tailwind CSS, React Query, Zustand, Shadcn/UI, Framer Motion in `deliverables/06-technical-architecture.md`
- [x] T024 [P] [US3] Write User & Identity domain entities: User, Role, Permission, UserSession, Device, ParentContact in `deliverables/09-data-model-draft.md`
- [x] T025 [US3] Write backend stack section: .NET Web API, C#, Clean Architecture, CQRS, Entity Framework Core in `deliverables/06-technical-architecture.md`
- [x] T026 [US3] Write database section: PostgreSQL rationale, schema domains overview in `deliverables/06-technical-architecture.md`
- [x] T027 [US3] Write cache layer section: Redis — caching, rate limiting, sessions, leaderboard, notification buffering in `deliverables/06-technical-architecture.md`
- [x] T028 [US3] Write background jobs section: BullMQ (Node.js) + Redis broker + .NET writes; hybrid architecture diagram in `deliverables/06-technical-architecture.md`
- [x] T029 [US3] Write video strategy section: YouTube initially, VideoProviderAbstraction with all required fields in `deliverables/06-technical-architecture.md`
- [x] T030 [US3] Write communication patterns section: Frontend ↔ Backend (REST), Backend ↔ Redis, Backend ↔ Worker (queues) in `deliverables/06-technical-architecture.md`
- [x] T031 [US3] Write provider abstractions section: Video, Notification, AI — all behind interfaces in `deliverables/06-technical-architecture.md`
- [x] T032 [US3] Write Student domain entities: StudentProfile, StudentStatus, StudentProgress, StudentLeaderboard, StudentNotificationPreference in `deliverables/09-data-model-draft.md`
- [x] T033 [US3] Write Content domain entities: Program, Package, ContentSection, Lesson, LessonVideo, LessonSummary, LessonResource, MindMap, RevisionBlock in `deliverables/09-data-model-draft.md`
- [x] T034 [US3] Write Assessment domain entities: Exam, ExamQuestion, QuestionBankItem, StudentExamAttempt, StudentAnswer, Homework, HomeworkSubmission in `deliverables/09-data-model-draft.md`
- [x] T035 [US3] Write Access domain entities: CodeGroup, AccessCode, CodeActivation, StudentAccessGrant in `deliverables/09-data-model-draft.md`
- [x] T036 [US3] Write Tracking domain entities: VideoWatchEvent, LessonProgress, EngagementMetric, WarningEvent, NotificationEvent, AuditLog in `deliverables/09-data-model-draft.md`
- [x] T037 [US3] Write AI domain entities: AITask, AIAnalysisResult, EssayReviewResult, WeaknessInsight, RecommendationItem in `deliverables/09-data-model-draft.md`
- [x] T038 [US3] Write entity relationships and cross-domain references in `deliverables/09-data-model-draft.md`
- [x] T039 [US3] Write services section: Frontend (Next.js), Backend API (.NET), Node Worker (BullMQ), PostgreSQL, Redis, Reverse Proxy, Monitoring in `deliverables/10-system-blueprint.md`
- [x] T040 [US3] Write environments section: Development, Staging, Production configurations in `deliverables/10-system-blueprint.md`
- [x] T041 [US3] Write secrets management section: env vars / secrets manager, never in source control in `deliverables/10-system-blueprint.md`
- [x] T042 [US3] Write performance targets section: API <500ms p95, Video page <3s, Code redemption <2s in `deliverables/10-system-blueprint.md`

**Checkpoint**: Technical Architecture, Data Model Draft, and System Blueprint complete — developer can set up repo

---

## Phase 6: User Story 4 — User Roles & Permissions Matrix (Priority: P2)

**Goal**: Produce the User Roles Matrix documenting all roles, sub-roles, permissions, and multi-role assignment model

**Independent Test**: Take any platform feature and confirm the matrix explicitly states which roles can perform it

### Implementation for User Story 4

- [x] T043 [US4] Write role definitions section: Student, Parent, Teacher, Admin with descriptions in `deliverables/05-user-roles-matrix.md`
- [x] T044 [US4] Write assistant sub-roles section: Academic, Homework reviewer, Follow-up, Support — with per-sub-role permission scopes in `deliverables/05-user-roles-matrix.md`
- [x] T045 [US4] Write multi-role assignment model: users can hold multiple roles, assignment rules in `deliverables/05-user-roles-matrix.md`
- [x] T046 [US4] Write permission matrix table: Role × Feature → Access Level (full/read-only/none) for all platform features in `deliverables/05-user-roles-matrix.md`
- [x] T047 [US4] Write role extension procedure: process for adding new roles during later phases in `deliverables/05-user-roles-matrix.md`

**Checkpoint**: Roles Matrix complete — every role-feature combination has explicit access level

---

## Phase 7: User Story 5 — UX Direction & Sitemap (Priority: P3)

**Goal**: Produce the UX Direction document covering UX philosophy, registration flow, navigation principles, and full sitemap

**Independent Test**: Present to a non-technical stakeholder; they can explain the intended student experience

### Implementation for User Story 5

- [x] T048 [US5] Write UX principles section: simple, organized, motivating, clear path, controlled not oppressive in `deliverables/08-ux-direction.md`
- [x] T049 [US5] Write two-step registration flow: Step 1 (name, phone, grade, track) → Step 2 (parent phone, governorate, city, school) in `deliverables/08-ux-direction.md`
- [x] T050 [US5] Write student dashboard design: available packages, latest lesson, upcoming exams, progress, codes, notifications, resume in `deliverables/08-ux-direction.md`
- [x] T051 [US5] Write sitemap: Public site, Student portal, Parent layer, Teacher panel, Assistant panel, Admin panel — with page hierarchy in `deliverables/08-ux-direction.md`
- [x] T052 [US5] Write navigation rules: max 3 clicks from dashboard to lesson content, breadcrumbs strategy in `deliverables/08-ux-direction.md`

**Checkpoint**: UX Direction complete — designer can begin wireframing

---

## Phase 8: User Story 6 — AI Scope & Business Rules (Priority: P3)

**Goal**: Produce the Business Rules Document covering watch control, exams, homework, gamification, student behavior, and AI scope boundaries

**Independent Test**: Every business rule from plan sections 4.1–4.7 is represented; AI has explicit in-scope/out-of-scope lists

### Implementation for User Story 6

- [x] T053 [P] [US6] Write watch control rules: max minutes, max replays, allowed speeds, skip policy, completion threshold in `deliverables/07-business-rules.md`
- [x] T054 [P] [US6] Write AI scope definition: in-scope features (question gen, weak-point analysis, essay support, controlled assistant) in `deliverables/07-business-rules.md`
- [x] T055 [US6] Write exam rules: MCQ, Essay, mixed; question bank classification; pass thresholds; instant grading; attempt tracking in `deliverables/07-business-rules.md`
- [x] T056 [US6] Write homework rules: MCQ, Essay, mixed; due dates; submission states; review pipeline in `deliverables/07-business-rules.md`
- [x] T057 [US6] Write student behavior classification: committed / average / at-risk; criteria and thresholds in `deliverables/07-business-rules.md`
- [x] T058 [US6] Write gamification rules: points, badges, levels, ranking, challenges — motivating not toxic in `deliverables/07-business-rules.md`
- [x] T059 [US6] Write progression rules: exam/homework required before next; pass threshold gating; code-type-based paths in `deliverables/07-business-rules.md`
- [x] T060 [US6] Write AI boundary section: explicit out-of-scope list (no open-web, no non-platform content, suggestions only) in `deliverables/07-business-rules.md`

**Checkpoint**: Business Rules and AI Scope complete — all plan sections 4.1–4.7 covered

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: Cross-validation, consistency checks, and final review preparation

- [x] T061 Validate cross-references: every entity in `09-data-model-draft.md` traces to a requirement in another deliverable
- [x] T062 Validate code-rule alignment: every code type in `03-access-blueprint.md` has matching rules in `07-business-rules.md`
- [x] T063 Validate role-sitemap alignment: every role in `05-user-roles-matrix.md` appears in `08-ux-direction.md` sitemap
- [x] T064 Validate provider abstractions: `06-technical-architecture.md` references all constitution-required abstractions (Video, Notification, AI)
- [x] T065 Validate terminology consistency: "Content Section" used canonically across all 10 deliverables (no "months" in non-contexts)
- [x] T066 Final review preparation: generate a review checklist mapping each deliverable to its spec acceptance scenarios from `spec.md`
- [x] T067 Present all deliverables to project owner for approval against success criteria SC-001 through SC-008

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Setup — PRD BLOCKS all user stories
- **US1 (Phase 3)**: Depends on PRD — Content Blueprint
- **US2 (Phase 4)**: Depends on PRD — Access Blueprint + Data Blueprint
- **US3 (Phase 5)**: Depends on PRD — Technical Architecture + Data Model + System Blueprint
- **US4 (Phase 6)**: Depends on PRD — User Roles Matrix
- **US5 (Phase 7)**: Depends on US4 (roles needed for sitemap) — UX Direction
- **US6 (Phase 8)**: Depends on US1 (content hierarchy needed for watch/exam rules) — Business Rules + AI Scope
- **Polish (Phase 9)**: Depends on ALL user stories being complete

### User Story Dependencies

- **US1 (P1)**: Can start after PRD (Phase 2) — No dependencies on other stories
- **US2 (P1)**: Can start after PRD (Phase 2) — Independent of US1
- **US3 (P2)**: Can start after PRD (Phase 2) — Independent of US1/US2
- **US4 (P2)**: Can start after PRD (Phase 2) — Independent of US1/US2/US3
- **US5 (P3)**: Depends on US4 (roles needed for sitemap) — Can start after US4
- **US6 (P3)**: Depends on US1 (content hierarchy for business rules) — Can start after US1

### Parallel Opportunities

- **Phase 2**: T003–T008 are sequential within the PRD (same file)
- **After PRD**: US1, US2, US3, US4 can all start in parallel (different deliverable files)
- **Within US2**: T013 + T014 can run in parallel (different files)
- **Within US3**: T023 + T024 can run in parallel (different files)
- **Within US6**: T053 + T054 can run in parallel (same file but different sections)

---

## Parallel Example: After PRD Completion

```bash
# All four P1/P2 stories can launch in parallel:
Story US1: "Write Content Blueprint in deliverables/02-content-blueprint.md"
Story US2: "Write Access Blueprint + Data Blueprint in deliverables/03-*.md + 04-*.md"
Story US3: "Write Tech Architecture + Data Model + System Blueprint in deliverables/06-*.md + 09-*.md + 10-*.md"
Story US4: "Write User Roles Matrix in deliverables/05-user-roles-matrix.md"
```

---

## Implementation Strategy

### MVP First (PRD + Content Blueprint Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: PRD (Foundational)
3. Complete Phase 3: US1 — Content Blueprint
4. **STOP and VALIDATE**: Review PRD + Content Blueprint independently
5. Deliver first increment for stakeholder feedback

### Incremental Delivery

1. PRD → Content Blueprint (US1) → **Validate**
2. Add Access Blueprint + Data Blueprint (US2) → **Validate**
3. Add Technical Architecture + Data Model + System Blueprint (US3) → **Validate**
4. Add User Roles Matrix (US4) → **Validate**
5. Add UX Direction (US5) → **Validate**
6. Add Business Rules + AI Scope (US6) → **Validate**
7. Cross-validation polish (Phase 9) → **Final approval**

### Parallel Team Strategy

With multiple contributors:

1. One person writes PRD (blocks everyone)
2. Once PRD done:
   - Contributor A: US1 (Content Blueprint) + US6 (Business Rules)
   - Contributor B: US2 (Access Blueprint + Data Blueprint)
   - Contributor C: US3 (Tech Architecture + Data Model + System Blueprint)
   - Contributor D: US4 (User Roles Matrix) + US5 (UX Direction)
3. All complete → Cross-validation (Phase 9)

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- All deliverables are Markdown files in `specs/001-phase0-discovery-blueprint/deliverables/`
- "Content Section" is the canonical term — never use "months" in deliverables except as documented alias
- Multi-role assignment model: users can hold multiple roles (documented in US4)
- No code is produced in Phase 0 — all tasks are documentation tasks
- Commit after each deliverable or logical group
- Stop at any checkpoint to validate deliverable independently
