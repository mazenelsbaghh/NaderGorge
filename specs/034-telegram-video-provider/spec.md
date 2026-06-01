# Feature Specification: Telegram Video Provider Support

**Feature Branch**: `034-telegram-video-provider`  
**Created**: 2026-03-31  
**Status**: Draft  
**Input**: User description: "عايز اضيف اني احط لينك تلجرام بس محدش يعرف انو من تلجرام بدل اليوتيوب فاهمني يني سيب اليوتيوب و نحط حوار التلجرام و يكون بنفس شغل السيكور و بلاير و كده"

## Overview

Allow admins to attach Telegram-hosted videos to lessons as an alternative to YouTube, while the existing secure video player continues to work identically from the student's perspective. Students must never be able to tell whether a video comes from YouTube or Telegram — the source is hidden behind the same server-side encryption and embed proxy already in use.

---

## Clarifications

### Session 2026-03-31

- Q: هل v1 تدعم القنوات الخاصة أم العامة فقط؟ → A: عام فقط — لينكات CDN مباشرة من قنوات عامة أو un-listed. القنوات الخاصة خارج النطاق.
- Q: ازاي الأدمن يدخل الفيديو؟ → A: يحط رابط post بالفورمات `https://t.me/channelname/postid` والسيرفر يجيب الفيديو عبر Bot API تلقائياً.
- Q: درس فيه YouTube وTelegram معاً ولا مصدر واحد؟ → A: مصدر واحد فقط — الأدمن يختار المورد (YouTube أو Telegram) من قائمة ثم يحط اللينك المناسب.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Admin Sets a Telegram Video on a Lesson (Priority: P1)

An admin opens a lesson's detail page in the admin dashboard, pastes a Telegram video link (public channel post or private file link), saves it, and the system stores it as a new `VideoProvider = Telegram` entry alongside the existing YouTube video or as a replacement.

**Why this priority**: Without this the feature doesn't exist — it's the data-entry entry point.

**Independent Test**: Admin can successfully attach a Telegram video URL to a lesson and see it saved. Delivered value: the platform now stores Telegram videos.

**Acceptance Scenarios**:

1. **Given** an admin is on the lesson detail page, **When** they paste a valid Telegram video URL and save, **Then** the lesson is updated with `provider = "telegram"` and the URL encrypted in storage.
2. **Given** an admin pastes an invalid URL (not a Telegram link), **When** they try to save, **Then** they see a clear validation error and nothing is saved.
3. **Given** a lesson already has a YouTube video, **When** the admin switches to Telegram and saves, **Then** the new provider replaces the old one (or is added as a secondary source, per admin's choice).

---

### User Story 2 — Student Watches a Telegram Video Securely (Priority: P1)

A student opens a lesson that has a Telegram-hosted video. The secure video player loads and plays the video with no visible indication that the source is Telegram — no Telegram logo, no Telegram watermark/overlay, no Telegram URL in DevTools, same custom controls, same watch-tracking behaviour.

**Why this priority**: Core delivery requirement. If the player leaks the source, the privacy contract is broken.

**Independent Test**: Open a lesson with a Telegram video; verify the player looks identical to a YouTube lesson and no Telegram URL appears in browser network/DOM.

**Acceptance Scenarios**:

1. **Given** a lesson has a Telegram video, **When** a student opens that lesson, **Then** the video plays inside the existing custom player with the same controls and overlay design.
2. **Given** the student opens DevTools → Network, **When** they inspect all requests, **Then** no Telegram domain (t.me, cdn.telegram.org, etc.) appears directly — only the internal embed proxy URL `/api/video/embed?t=...`.
3. **Given** the video is playing, **When** the student right-clicks the player, **Then** the context menu is blocked (same as YouTube mode).
4. **Given** the watch-tracking threshold (default 60 s), **When** the student watches past it, **Then** the view is recorded identically to a YouTube lesson.

---

### User Story 3 — Watch-Limit Enforcement for Telegram Videos (Priority: P2)

The existing watch-limit and view-count logic applies equally to Telegram-sourced videos. If a student has exhausted their allowed views, the player shows the locked state.

**Why this priority**: Protects revenue; students cannot bypass limits by using a different-source video.

**Independent Test**: Configure a Telegram video with max_views=1, watch it once past the threshold, try to open it again — locked state appears.

**Acceptance Scenarios**:

1. **Given** a student has reached their view limit on a Telegram video, **When** they try to load it again, **Then** the player shows the "watch limit reached" locked overlay.
2. **Given** a Telegram video with no view limit, **When** a student watches it multiple times, **Then** it always loads successfully.

---

### Edge Cases

- What happens when the Telegram file link has expired or the file was deleted from Telegram? → The embed proxy must return a clean error page and the player shows the standard error state.
- What if a Telegram video requires authentication (private channel)? → خارج النطاق في v1 — عام فقط.
- What if the Telegram video URL format changes (different cdns)? → The admin-side validator must document the accepted URL formats clearly.
- What happens during seek/playback for Telegram videos that don't support range requests? → Player should fall back to non-seekable mode gracefully.

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST support a `telegram` video provider type alongside the existing `youtube` provider in the lesson data model.
- **FR-002**: The admin dashboard MUST allow admins to enter a Telegram video URL when configuring a lesson's video source.
- **FR-003**: The admin input MUST validate that the entered URL is a recognised Telegram video link before saving.
- **FR-004**: The backend MUST encrypt the Telegram video URL server-side before storing it, using the same encryption mechanism as YouTube video IDs.
- **FR-005**: The embed proxy route (`/api/video/embed`) MUST support decrypting and serving Telegram videos, returning an HTML page that plays the video without exposing any Telegram domain to the student's browser.
- **FR-006**: The `SecureVideoPlayer` component MUST work identically for Telegram and YouTube sources — same custom controls, same pause overlay, same watch-tracking, same watch-limit enforcement.
- **FR-007**: The embed HTML for Telegram videos MUST block context menu, drag, and right-click, matching the protections applied to YouTube embeds.
- **FR-008**: No Telegram-identifying information (URLs, logos, headers) MUST be visible in the student's browser DOM, network tab, or JavaScript context.
- **FR-009**: The watch-session service MUST record view progress and enforce view limits for Telegram videos using the same backend logic as YouTube videos.
- **FR-010**: The system MUST handle Telegram video load failures gracefully, displaying the existing player error state.

### Key Entities

- **LessonVideo**: Extended with `provider: "youtube" | "telegram"` and encrypted `videoUrl/videoId` field. For Telegram, the full CDN/file URL is encrypted rather than just a video ID.
- **VideoSession**: Unchanged — provider-agnostic. Session tokens still carry encrypted source data.
- **EmbedProxy** (`/api/video/embed`): Extended to branch on `provider` and produce provider-specific embed HTML.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A student watching a Telegram-hosted lesson cannot identify the video source via the browser UI, DOM, or network requests — verified by manual inspection.
- **SC-002**: A Telegram video lesson loads and begins playing within the same time budget as a YouTube lesson (no additional perceived latency introduced by the proxy).
- **SC-003**: All existing watch-tracking milestones (60 s threshold, view count, limit lock) fire correctly for Telegram videos — verified by automated integration test.
- **SC-004**: Admin can attach a Telegram video to a lesson in under 60 seconds through the existing lesson-edit UI without any new training.
- **SC-005**: Zero Telegram domain requests appear in the student browser network tab during a full playback session.

---

## Assumptions

- Telegram "public" video URLs (direct CDN links, e.g. from public channels or forwarded file links) will be used in v1; private-channel bot-API streaming is out of scope.
- The embed proxy will serve the Telegram video via a `<video>` HTML5 element (not an iframe-within-iframe), since Telegram does not provide an embeddable player iFrame API like YouTube.
- The existing AES-256-GCM encryption used for YouTube video IDs is sufficient for Telegram URLs.
- The backend `LessonVideo` entity already has a `provider` discriminator or can be extended with a migration without breaking existing YouTube lessons.
- Mobile browser support follows the same constraints as the existing player (no additional mobile-specific work in v1).
- The admin UI will reuse the existing video URL input field with a provider selector dropdown added alongside it.
