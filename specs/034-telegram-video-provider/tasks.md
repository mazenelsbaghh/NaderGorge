# Implementation Tasks: Telegram Video Provider Support

This document outlines the tasks required to implement the Telegram video integration. Each user story is implemented in its own phase so that it can be independently tested and delivered.

---

## 🏗 Phase 1: Foundational Setup (Prerequisites)

**Goal:** Provide the necessary backend abstraction for a new `telegram` provider.

- [x] T001 Implement `TelegramVideoProvider` in `backend/src/NaderGorge.Domain/Services/TelegramVideoProvider.cs`
- [x] T002 Update dependency injection in backend to register `TelegramVideoProvider` (e.g. `backend/src/NaderGorge.API/Extensions/ServiceRegistration.cs` or equivalent).
- [x] T003 Ensure `IVideoProvider` interface has a unified way to process `ProviderVideoId` format (`ExtractVideoId`).
- [x] T004 Add unit tests for `TelegramVideoProvider` covering public and unlisted channel URLs.

---

## 🚀 Phase 2: User Story 1 — Admin Sets a Telegram Video on a Lesson (Priority: P1)

**Goal:** An admin can enter a Telegram video URL (e.g. `https://t.me/channel/postid`) into the lesson creation form, and the system saves it successfully.

### Independent Testing Criteria:
1. Open Admin Dashboard.
2. Edit or Create a Lesson.
3. Select "Telegram" as the video provider instead of YouTube.
4. Input a Telegram post URL.
5. Save the form.
6. Verify in the database that `Provider` is `telegram` and `ProviderVideoId` is exactly the extracted channel/post pattern.

### Implementation Tasks:
- [x] T005 [P] [US1] Update `AddLessonVideoCommand` and `UpdateLessonVideoCommand` validators in `backend/src/NaderGorge.Application/Features/Admin/Commands/` to support `"telegram"`.
- [x] T006 [US1] Update the `ContentForm` / `VideoForm` component in `frontend/src/app/admin/content/[id]/components/` to show a provider `<Select>` dropdown.
- [x] T007 [P] [US1] Update `AdminService.ts` in the frontend (if needed) to ensure the provider is sent efficiently in the API payload.
- [x] T008 [US1] Verify that saving a Telegram lesson successfully updates `ProviderVideoId` using backend `TelegramVideoProvider.ExtractVideoId`.

---

## 🎧 Phase 3: User Story 2 — Student Watches a Telegram Video Securely (Priority: P1)

**Goal:** The student opens the lesson page and sees the video playing securely inside the `SecureVideoPlayer` shell without knowing the source is Telegram.

### Independent Testing Criteria:
1. Login as Student.
2. Open the lesson updated in Phase 2.
3. The video should load natively.
4. Pausing, playing, seeking, and altering volume/speed should work perfectly.
5. Inspecting elements should not show any `t.me` API call or domain on the frontend.
6. Context menu should be blocked.

### Implementation Tasks:
- [x] T009 [US1] Install `cheerio` via `npm install cheerio` in `frontend/`.
- [x] T010 [US1] In `api/video/embed/route.ts`, if `Provider` is `"telegram"`, execute a `fetch` request to `https://t.me/${videoId}?embed=1`. Parse the returned HTML using `cheerio` to locate the `<video src="...">` (class `.tgme_widget_message_video`).
- [x] T011 [US1] If parsing fails, return a secure `404` fallback or minimal error UI.
- [x] T012 [P] [US1] Generate a custom HTML response wrapping the extracted `videoSrc` in a native `<video>` tag with a closed `ShadowDOM`.
- [x] T013 [P] [US1] Add JavaScript to the response wrapper to emulate the YouTube `YT.PlayerState` enum and emit identical `postMessage` events (`ready`, `stateChange`, `timeUpdate`) based on the native `<video>` events (e.g., mapping `play` to `PLAYING`, `ended` to `ENDED`).
- [x] T014 [US1] Secure the `<video>` element by removing `controls`, adding `controlsList="nodownload noplaybackrate"`, and masking right-click menus to prevent direct downloads.
- [x] T015 [US1] Test the student player (`frontend/src/app/(student)/lesson/[id]/...`) to confirm the Telegram video plays securely and watch progress is tracked correctly.

---

## ⏱️ Phase 4: User Story 3 — Watch-Limit Enforcement for Telegram Videos (Priority: P2)

**Goal:** Ensure the watch limits apply flawlessly to the Telegram Provider (just as they do for YouTube).

### Independent Testing Criteria:
1. Ensure the backend marks a watch session as completed after 60 seconds of playback on a Telegram video.
2. Lock out the student if their max watch limits are reached.

### Implementation Tasks:
- [ ] T015 [US3] Test the frontend-backend tracking interactions. Because the tracking signals live in `SecureVideoPlayer`, and US2 adapted Telegram events to YouTube events, `CreateVideoSessionCommand` should automatically work.
- [ ] T016 [US3] Verify `VideoPlaybackSession` entity effectively increments view counts regardless of provider. (QA/Validation Task).
- [ ] T017 [US3] If the embed proxy cannot fetch the video or the video is deleted, ensure it returns a blank or generic "Video Unavailable" error inside the iframe proxy.

---

## ✨ Phase 5: Polish & Cross-Cutting Concerns

**Goal:** Ensure code quality and stability.

- [ ] T018 Run `npm run lint` and `dotnet build` to satisfy all continuous integration checks.
- [ ] T019 Update any testing scripts or cypress commands (if applicable) that hard-coded YouTube testing values to now also test Telegram URLs.
