---
description: "Task list template for feature implementation"
---

# Tasks: Telegram Direct Stream

**Input**: Design documents from `/specs/051-telegram-direct-stream/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), data-model.md

**Tests**: Not explicitly requested, testing is manual per spec.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- **Web app**: `backend/src/`, `frontend/src/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Preparing for the new provider

- [ ] T001 Initialize branch and set workspace correctly.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure removal of the old provider

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T002 Delete Okru Video Provider logic completely from `backend/src/NaderGorge.Infrastructure/Providers/OkVideoProvider.cs`
- [X] T003 Remove Okru iframe helper from `frontend/src/app/api/video/embed/route.ts`

**Checkpoint**: Foundation ready - OK.ru is fully removed.

---

## Phase 3: User Story 3 - Redirect Service to Bypass Link Expiration (Priority: P1)

**Goal**: As a system, I need an endpoint that fetches fresh Telegram direct `.mp4` URLs on the fly to bypass token expiration via a 302 Redirect. Note: Elevated to P1 because the frontend player depends on this proxy endpoint.

**Independent Test**: Hit `/api/video/stream-proxy?t=<token>` and observe a 302 to the CDN.

### Implementation for User Story 3

- [X] T004 [US3] Create proxy route in `frontend/src/app/api/video/stream-proxy/route.ts`
- [X] T005 [US3] Implement dynamic fetch and 302 Redirect logic to `Location` header.

**Checkpoint**: At this point, the streaming proxy is up and running.

---

## Phase 4: User Story 1 - Headless Video Playback via Telegram (Priority: P2)

**Goal**: As a student, I want to watch educational videos within a fully customized, branded video player powered natively by HTML5 tracking.

**Independent Test**: Load a lesson with a telegram provider and verify the HTML5 video plays directly via the proxy URL.

### Implementation for User Story 1

- [X] T006 [US1] Update `frontend/src/components/video/SecureVideoPlayer.tsx` to conditionally render an HTML5 `<video>` element for provider === "telegram"
- [X] T007 [US1] Link the `<video>` src to `/api/video/stream-proxy?t=<videoToken>`
- [X] T008 [US1] Bind React player controls (Play, Pause, Progress) to the HTML5 video `ref`.

**Checkpoint**: Student player now plays native MP4s securely.

---

## Phase 5: User Story 2 - Administrator Providing Telegram Videos (Priority: P3)

**Goal**: As an admin, I want to upload a large video to Telegram and provide its bot direct link in the system.

**Independent Test**: Admin Add Video dropdown should show "Telegram Direct" instead of "OK.ru".

### Implementation for User Story 2

- [X] T009 [US2] Update `frontend/src/components/admin/AddVideoForm.tsx` to replace `okru` with `telegram` in dropdown.
- [X] T010 [US2] Ensure backend `backend/src/NaderGorge.Application/Features/Content/Queries/GetLessonDetailQuery.cs` logic handles `telegram` provider cleanly without OK.ru legacy logic.

**Checkpoint**: Admins can now add direct Telegram streams.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [X] T011 Update `backend/src/NaderGorge.Infrastructure/DependencyInjection.cs` to remove any lingering `OkVideoProvider` injections.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3+)**: All depend on Foundational phase completion
- **Polish (Final Phase)**: Depends on all desired user stories being complete

### Parallel Opportunities

- T004 and T009 can be executed in parallel.
- Foundational Backend deletions and Frontend `SecureVideoPlayer.tsx` rewrites can be done simultaneously.

---

## Implementation Strategy

### MVP First (User Story 1 & 3 Only)

1. Remove OK.ru
2. Create redirect proxy
3. Map frontend player to proxy.
4. **STOP and VALIDATE**: Video plays manually mapped.

### Incremental Delivery

5. Add Admin flow (US2).
6. Polish.
