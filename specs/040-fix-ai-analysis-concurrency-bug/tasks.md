---
description: "Task list for fixing AI analysis concurrency bug"
---

# Tasks: Fix AI Analysis Concurrency Bug

**Input**: Design documents from `/specs/040-fix-ai-analysis-concurrency-bug/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, quickstart.md

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1)
- Include exact file paths in descriptions

## Path Conventions

- **Web app**: `backend/src/NaderGorge.Application/`

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

*(No new infrastructure setup required for this bug fix).*

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

*(No foundational prerequisites required for this bug fix).*

---

## Phase 3: User Story 1 - Reliable AI Analysis Completion (Priority: P1) 🎯 MVP

**Goal**: The system must successfully save the results of the AI video chapter analysis to the database idempotently without throwing a DbUpdateConcurrencyException.

**Independent Test**: Trigger the AI video analysis job and observe backend logs to ensure no `DbUpdateConcurrencyException` is thrown, and view the video on the frontend to manually witness generated chapters.

### Implementation for User Story 1

- [X] T001 [US1] Refactor `AiAnalysisCompletedCommandHandler` in `backend/src/NaderGorge.Application/Features/Internal/Commands/AiAnalysisCompletedCommand.cs` to catch `DbUpdateConcurrencyException` gracefully, allowing idempotent webhook processing without failing the background job.

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently.

---

## Phase N: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [X] T002 Verify compilation strictly across the backend project.
- [ ] T003 Run quickstart.md validation locally to manually test the Node worker -> .NET webhook cycle.

---

## Dependencies & Execution Order

### Phase Dependencies

- **User Story 1**: Can start immediately.
- **Polish (Final Phase)**: Depends on User Story 1 being complete.

### User Story Dependencies

- **User Story 1 (P1)**: Independent execution.

### Parallel Opportunities

- Due to the nature of this bug fix, there are no independent parallel implementation layers (it involves refactoring a single file).

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Skip Setup/Foundational phases as they are satisfied.
2. Complete Phase 3: User Story 1 (Refactor the command handler).
3. **STOP and VALIDATE**: Test User Story 1 independently using quickstart validation.
4. Deploy the backend adjustments to staging/production.
