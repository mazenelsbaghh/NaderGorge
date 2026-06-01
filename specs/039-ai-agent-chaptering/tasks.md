---
description: "Task list for AI Agent Chaptering feature implementation"
---

# Tasks: AI Agent Chaptering Workflow

**Input**: Design documents from `/specs/039-ai-agent-chaptering/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/progress-payload.md

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [ ] T001 Verify BullMQ and Queue configuration in `worker/src/index.ts`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

- [x] T002 Refactor `VideoAnalysisJob` interface locally in `worker/src/jobs/analyzeVideoChapters.ts` to include optional stage state properties (e.g., `audioPath`).

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Multi-Stage Progress Tracking (Priority: P1) 🎯 MVP

**Goal**: Convert monolithic processing state into clear, visible stages (Downloading, Extracting, AI Analysis, Formatting Subtitles).

**Independent Test**: Can be fully tested by submitting a new lesson video and observing the admin dashboard update its state text dynamically through distinct phases.

### Implementation for User Story 1

- [x] T003 [P] [US1] Update the status API endpoint in `worker/src/index.ts` to properly serialize and expose the complex `{ percentage, stage }` progress object.
- [x] T004 [US1] Inject `job.updateProgress({ percentage: 10, stage: 'جاري استخراج وتحضير الصوت من الفيديو...' })` in `worker/src/jobs/analyzeVideoChapters.ts` before extraction.
- [x] T005 [US1] Inject `job.updateProgress({ percentage: 40, stage: 'الذكاء الاصطناعي يقوم بتحليل وتلخيص المحتوى (قد يستغرق دقائق)...' })` in `worker/src/jobs/analyzeVideoChapters.ts` before calling Gemini.
- [x] T006 [US1] Inject `job.updateProgress({ percentage: 85, stage: 'جاري بناء هيكل الفصول وإنشاء الترجمة...' })` in `worker/src/jobs/analyzeVideoChapters.ts` during formatting.
- [x] T007 [US1] Inject `job.updateProgress({ percentage: 95, stage: 'جاري حفظ الفصول وتحديث قواعد البيانات...' })` in `worker/src/jobs/analyzeVideoChapters.ts` during Webhook sync.
- [x] T008 [P] [US1] Update `AIProgressTracker` overlay logic in `frontend/src/app/admin/content/lessons/[id]/page.tsx` (or its child components) to read `progress.percentage` and render `progress.stage` as text in the progress bar.

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently

---

## Phase 4: User Story 2 - Resilient Stage-Level Retries (Priority: P2)

**Goal**: Make the system fault-tolerant at the stage level so that retries skip already completed expensive operations (like 2GB video extraction).

**Independent Test**: Can be fully tested by artificially causing a network timeout, pressing "Retry", and verifying it skips extraction.

### Implementation for User Story 2

- [x] T009 [US2] Implement conditional extraction wrap `if (!job.data.audioPath) { ... job.updateData({ audioPath }) }` in `worker/src/jobs/analyzeVideoChapters.ts`.
- [x] T010 [US2] Update cleanup logic `finally { ... }` in `worker/src/jobs/analyzeVideoChapters.ts` to ONLY delete the temporary `.mp3` file if the entire job succeeds (`isSuccess`), preserving it if an error is thrown.
- [x] T011 [P] [US2] Ensure the frontend "Retry" action triggers a standard enqueue that inherently utilizes the preserved data payload.

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently

---

## Phase N: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [x] T012 Manual end-to-end testing of 1-hour video via dashboard to confirm timings.
- [x] T013 Verify that the Gemini headers timeout (60 minutes) setup in `worker/src/services/geminiService.ts` respects the new staged lifecycle.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: Can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion
- **User Stories (Phase 3+)**: All depend on Foundational phase completion
- **Polish (Final Phase)**: Depends on all user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Independent.
- **User Story 2 (P2)**: Integrates onto US1's structural changes. Should be tested sequentially.

### Parallel Opportunities

- The changes to the worker API (`worker/src/index.ts`) and the frontend React components (`T003` and `T008`) can be implemented in parallel.
- The `geminiService.ts` check can be verified independently.`
