# Tasks: Secure Video Player with YouTube Link Protection

**Feature**: 005-secure-video-player
**Spec**: [spec.md](./spec.md)
**Plan**: [plan.md](./plan.md)
**Generated**: 2026-03-25
**Total Tasks**: 22

---

## Phase 1: Setup

> Project initialization and shared infrastructure for video encryption.

- [x] T001 Create `VideoPlaybackSession` entity in `backend/src/NaderGorge.Domain/Entities/VideoPlaybackSession.cs`
- [x] T002 Add `DbSet<VideoPlaybackSession> VideoPlaybackSessions` to `backend/src/NaderGorge.Domain/Interfaces/IAppDbContext.cs`
- [x] T003 Add `DbSet<VideoPlaybackSession> VideoPlaybackSessions` to `backend/src/NaderGorge.Infrastructure/Persistence/AppDbContext.cs`
- [x] T004 Create EF Core migration by running `dotnet ef migrations add AddVideoPlaybackSession` in `backend/src/NaderGorge.API`

---

## Phase 2: Foundational — Encryption & Session Service

> Blocking prerequisites: encryption service and video session backend logic.

- [x] T005 Create `IVideoEncryptionService` interface in `backend/src/NaderGorge.Application/Common/IVideoEncryptionService.cs` with methods: `EncryptVideoId(videoId, key)`, `DecryptVideoId(token, key)`, `GenerateSessionKey()`
- [x] T006 Implement `VideoEncryptionService` using AES-256-GCM in `backend/src/NaderGorge.Infrastructure/Services/VideoEncryptionService.cs`
- [x] T007 Register `IVideoEncryptionService` in DI container in `backend/src/NaderGorge.API/Program.cs`

---

## Phase 3: US-1 — Student Watches Protected Video (P1)

> **Goal**: Student can play lesson videos through a custom secure player.
>
> **Independent Test**: A student with an active package can navigate to a lesson, request a video session, and see the custom player load and play the video.

### Backend

- [x] T008 [US1] Create `CreateVideoSessionCommand` and handler in `backend/src/NaderGorge.Application/Features/Student/Commands/CreateVideoSessionCommand.cs` — validates auth, access, watch limits, generates encrypted token
- [x] T009 [US1] Create `ConsumeVideoSessionCommand` and handler in `backend/src/NaderGorge.Application/Features/Student/Commands/ConsumeVideoSessionCommand.cs` — marks session token as consumed
- [x] T010 [US1] Create `VideoSessionController` with POST endpoints in `backend/src/NaderGorge.API/Controllers/VideoSessionController.cs`

### Frontend

- [x] T011 [P] [US1] Create `video-session-service.ts` in `frontend/src/services/video-session-service.ts` with `createSession()` and `consumeSession()` methods
- [x] T012 [P] [US1] Create `video-crypto.ts` decryption utility using Web Crypto API in `frontend/src/utils/video-crypto.ts`
- [x] T013 [US1] Create `SecureVideoPlayer.tsx` component in `frontend/src/components/video/SecureVideoPlayer.tsx` — fetches encrypted token, decrypts video ID, injects YouTube IFrame via Shadow DOM
- [x] T014 [US1] Create `PlayerControls.tsx` pharaonic-themed control bar in `frontend/src/components/video/PlayerControls.tsx` — play/pause, seek, volume, speed, fullscreen, watch count
- [x] T015 [US1] Create lesson video page in `frontend/src/app/student/packages/[packageId]/lessons/[lessonId]/page.tsx` — renders SecureVideoPlayer with lesson detail

---

## Phase 4: US-2 — Video URL Cannot Be Extracted (P1)

> **Goal**: YouTube video ID is not discoverable via browser DevTools, page source, or network inspection.
>
> **Independent Test**: Open DevTools while video plays; confirm no YouTube URL/ID visible in Elements panel, page source, or human-readable network requests.

- [x] T016 [US2] Create `dom-shield.ts` anti-inspection utilities in `frontend/src/utils/dom-shield.ts` — disableContextMenu, obfuscateIframeSrc, createMutationGuard, preventDrag
- [x] T017 [US2] Integrate DOM shield into `SecureVideoPlayer.tsx` — apply all protections after YouTube IFrame loads in `frontend/src/components/video/SecureVideoPlayer.tsx`
- [x] T018 [US2] Modify `GetLessonDetailQuery` to remove `embedUrl` from `VideoDto` response in `backend/src/NaderGorge.Application/Features/Content/Queries/GetLessonDetailQuery.cs`

---

## Phase 5: US-3 — Automatic Protection for Admin-Added Videos (P2)

> **Goal**: Protection is applied to all videos by default with zero admin effort.
>
> **Independent Test**: Admin adds a new video via the content panel; student views it and sees the secure player with no YouTube URL exposed.

- [x] T019 [US3] Verify and update `CreateVideoCommand` in `backend/src/NaderGorge.Application/Features/Admin/Commands/AdminContentCommands.cs` — ensure no embed URL is returned in creation response
- [x] T020 [US3] Update any admin video preview components to use SecureVideoPlayer instead of raw embed in `frontend/src/app/admin/` (if applicable)

---

## Phase 6: Polish & Cross-Cutting

> Final integration, responsive design, and error handling.

- [x] T021 Add rate limiting (10 requests/min) to video session endpoint in `backend/src/NaderGorge.API/Controllers/VideoSessionController.cs`
- [x] T022 Add error handling and Arabic user-facing messages to `SecureVideoPlayer.tsx` — session expired, network error, watch limit reached, unauthorized in `frontend/src/components/video/SecureVideoPlayer.tsx`

---

## Dependency Graph

```
Phase 1 (Setup)
  T001 → T002 → T003 → T004
    │
    ▼
Phase 2 (Foundational)
  T005 → T006 → T007
    │
    ▼
Phase 3 (US-1: Play Video)         
  T008 → T009 → T010 ─────────┐
  T011 ─┐ (parallel)          │
  T012 ─┤                     │
        ▼                     ▼
  T013 → T014 → T015
    │
    ▼
Phase 4 (US-2: Hide URL)
  T016 → T017
  T018 (parallel, backend-only)
    │
    ▼
Phase 5 (US-3: Auto Protection)
  T019 → T020
    │
    ▼
Phase 6 (Polish)
  T021 (parallel)
  T022 (parallel)
```

---

## Parallel Execution Opportunities

### Within Phase 3 (US-1)
- **T011 + T012**: Both are independent utility modules (service client + crypto)
- After T010 completes: T011/T012 can feed into T013

### Within Phase 4 (US-2)
- **T016 + T018**: Frontend DOM shield and backend response change are independent

### Within Phase 6 (Polish)
- **T021 + T022**: Rate limiting (backend) and error handling (frontend) are independent

---

## Implementation Strategy

### MVP (Minimum Viable Product)
**Scope**: Phase 1 + Phase 2 + Phase 3 (US-1)

This delivers:
- ✅ Student can watch videos through the secure player
- ✅ Video URL is encrypted and not in page HTML
- ✅ Custom pharaonic-themed controls
- ✅ Watch limit enforcement

### Increment 2
**Scope**: Phase 4 (US-2)

Adds:
- ✅ DOM-level protection (right-click disable, src obfuscation, mutation guards)
- ✅ embedUrl removed from API response

### Increment 3
**Scope**: Phase 5 + Phase 6

Adds:
- ✅ Admin flow verified
- ✅ Rate limiting, error handling, polish

---

## Summary

| Metric | Value |
|--------|-------|
| **Total Tasks** | 22 |
| **US-1 Tasks** | 8 (T008-T015) |
| **US-2 Tasks** | 3 (T016-T018) |
| **US-3 Tasks** | 2 (T019-T020) |
| **Setup/Foundation** | 7 (T001-T007) |
| **Polish** | 2 (T021-T022) |
| **Parallelizable** | 6 tasks marked [P] |
| **MVP Scope** | Phases 1-3 (15 tasks) |
| **Estimated Effort** | 13-18 hours |
