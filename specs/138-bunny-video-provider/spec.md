# Feature Specification: Bunny Video Provider

**Feature Branch**: `[138-bunny-video-provider]`  
**Created**: 2026-06-17  
**Status**: Draft  
**Input**: User description: "Add Bunny.net as a new video source beside YouTube and VK, with in-platform Bunny uploads by teachers/admins, teacher/course/lesson association, cost tracking visible only to admins, and AI video services aligned with available Bunny video capabilities."

## Clarifications

### Session 2026-06-17

- Q: ما مصدر أسعار التخزين والباندويث المستخدمة في حساب تكلفة Bunny؟ → A: المنصة تستخدم أسعار Bunny Stream الرسمية الحالية كقيم افتراضية، ثم يستطيع الأدمن تعديل سعر التخزين وسعر الباندويث من إعدادات المنصة، والتقارير تستخدم القيم المحفوظة في الإعدادات.
- Q: ما طريقة رفع الملفات الكبيرة من الجهاز إلى Bunny؟ → A: يجب دعم رفع Bunny القابل للاستكمال للملفات الكبيرة، مع تقدم واضح للمستخدم وحالة إعادة محاولة عند الفشل.
- Q: ما نطاق تقارير تكلفة Bunny؟ → A: يجب توفير تقارير بتواريخ وفلاتر شهرية، مع حفظ snapshots للتخزين والباندويث والتكلفة.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Choose Bunny As A Video Source (Priority: P1)

Admins and teachers can add or edit lesson videos and choose one of the supported sources: YouTube, VK, or Bunny. Existing YouTube and VK videos continue to play and remain editable without migration.

**Why this priority**: Bunny must be additive, not a replacement. Provider choice is the smallest useful slice and protects current content.

**Independent Test**: Create one video for each provider in the lesson video form, save them, reload the lesson, and verify each provider remains correctly labeled and playable according to its current provider behavior.

**Acceptance Scenarios**:

1. **Given** an admin or teacher is adding a lesson video, **When** they open the source selector, **Then** the options include YouTube, VK, and Bunny.
2. **Given** an existing YouTube or VK video, **When** the lesson is viewed or edited after this feature ships, **Then** its provider and playback behavior remain unchanged.
3. **Given** a Bunny video is linked to a lesson, **When** a student opens the lesson, **Then** the platform uses Bunny playback without exposing admin-only cost data.

---

### User Story 2 - Upload Bunny Videos From The Platform (Priority: P1)

Teachers can upload their own lesson videos to Bunny from inside the platform. Admins can upload a video on behalf of any teacher and must choose teacher, course/package, and lesson before upload.

**Why this priority**: The main business value is avoiding external manual Bunny dashboard work while preserving teacher ownership.

**Independent Test**: As a teacher, upload a local video file to one of the teacher's lessons. As an admin, select a teacher, course, and lesson, then upload both a local file and a source URL; verify each resulting Bunny video is attached to the selected lesson and teacher.

**Acceptance Scenarios**:

1. **Given** a teacher has access to a lesson, **When** they upload a valid video file, **Then** the system uploads it to Bunny with resumable upload behavior, shows progress, creates a Bunny lesson video, and associates it with that teacher and lesson.
2. **Given** an admin uploads a video for another teacher, **When** the admin chooses teacher, course, and lesson, **Then** the video cost and ownership are attributed to the selected teacher, not the admin.
3. **Given** a user provides a remote video URL, **When** the URL is valid and reachable by Bunny, **Then** the system requests Bunny to fetch the video and stores a pending/processing Bunny video link.
4. **Given** an upload fails or Bunny rejects the request, **When** the user views the upload result, **Then** the platform shows a clear failure state and does not create a playable lesson video with broken playback.

---

### User Story 3 - Track Bunny Costs For Admins Only (Priority: P2)

Admins can see Bunny cost data per video, per teacher, per course/package, and for the whole platform. Teachers never see cost, storage usage, bandwidth usage, or platform billing totals.

**Why this priority**: Bunny costs affect operations, but cost visibility is sensitive and must stay admin-only.

**Independent Test**: Generate or sync Bunny usage snapshots for several videos linked to different teachers and courses; verify admin dashboards can filter by month and show totals while teacher dashboards do not contain any cost or usage fields.

**Acceptance Scenarios**:

1. **Given** a Bunny video has storage and bandwidth usage snapshots, **When** an admin opens Bunny cost reporting for a month, **Then** the admin can see that video's cost, storage, and bandwidth for that month.
2. **Given** multiple Bunny videos belong to one teacher, **When** an admin opens teacher cost reporting for a month, **Then** the total cost for that teacher includes all Bunny videos uploaded by either that teacher or an admin on their behalf during the selected reporting window.
3. **Given** multiple Bunny videos belong to one course/package, **When** an admin opens course cost reporting for a month, **Then** the course total includes all linked Bunny videos in that course during the selected reporting window.
4. **Given** a teacher opens any teacher surface, **When** Bunny videos exist under their account, **Then** no cost, bandwidth, storage, or platform total is visible.

---

### User Story 4 - Align AI Video Workflows With Bunny Capabilities (Priority: P3)

The platform's video AI workflows work for Bunny videos using available platform capabilities and Bunny-compatible media access. The system does not remove current AI behavior for YouTube or VK.

**Why this priority**: AI is valuable, but playback and upload support must be stable first.

**Independent Test**: Trigger AI analysis for a Bunny video after Bunny processing completes; verify the existing chapter/mindmap workflow either processes the Bunny media or reports an actionable unsupported/processing state without crashing.

**Acceptance Scenarios**:

1. **Given** a Bunny video is still processing, **When** AI analysis is requested, **Then** the platform blocks or queues the request with a clear processing message.
2. **Given** a Bunny video is ready and media access is available, **When** AI analysis runs, **Then** the platform produces the same chapter/mindmap outputs as existing supported video workflows.
3. **Given** Bunny does not provide a required capability for a specific AI operation, **When** the operation is requested, **Then** the platform records a clear unsupported state and keeps the lesson video usable.

### Edge Cases

- Bunny credentials are missing, invalid, or scoped to the wrong library.
- Bunny accepts an upload but leaves the video processing for a long period.
- A large local upload is interrupted and must resume or retry without forcing the user to recreate all metadata.
- A remote URL is private, expired, unsupported, too large, or not a direct downloadable video.
- An admin selects a teacher/course/lesson combination where the lesson does not belong to the selected teacher's course.
- A teacher attempts to upload a Bunny video into a lesson they do not own.
- A Bunny video is deleted or unavailable in Bunny after it was linked in the platform.
- Usage/cost sync runs before Bunny statistics are available.
- Storage and bandwidth pricing rates change after videos already have historical usage.
- An admin changes pricing settings after historical monthly snapshots already exist; existing snapshots must remain auditable and new snapshots use the active saved rates for their period.

### Manual QA & Docker Acceptance *(mandatory)*

- **Manual QA Role/Flow 1**: Admin surface, lesson profile, add/edit video; verify the source selector includes YouTube, VK, Bunny and old YouTube/VK videos still display and play.
- **Manual QA Role/Flow 2**: Teacher surface, owned lesson profile; upload a valid local video file to Bunny and confirm it appears as a Bunny lesson video without any cost fields.
- **Manual QA Role/Flow 3**: Admin Bunny upload surface; choose teacher, course/package, lesson, then test file upload and URL fetch upload.
- **Manual QA Negative Check**: Teacher attempts to access Bunny cost endpoints or UI; request must be denied or cost data absent.
- **Manual QA Negative Check**: Teacher attempts to upload into an unowned lesson; request must be denied.
- **Docker Acceptance**: API, frontend, worker, PostgreSQL, and Redis containers start; migrations apply; health endpoints pass; Bunny configuration can be injected through environment variables without exposing secrets to the frontend.
- **External Dependencies**: Bunny.net Stream library ID, Stream API key, CDN hostname/pull zone context, and admin-configured storage/bandwidth pricing rates are required for full live validation. If Bunny credentials are unavailable locally, API tests must use mocked Bunny client behavior.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST support Bunny as an additional video provider while preserving existing YouTube and VK behavior and data.
- **FR-002**: The add/edit lesson video UI MUST offer YouTube, VK, and Bunny as provider choices for authorized admins and teachers.
- **FR-003**: The system MUST allow a teacher to upload a Bunny video only for lessons they are authorized to manage.
- **FR-004**: The system MUST allow an admin to upload a Bunny video for any teacher only after selecting the teacher, course/package, and lesson.
- **FR-005**: The system MUST support Bunny upload from a local video file.
- **FR-006**: The system MUST support Bunny upload from a remote video URL that Bunny can fetch.
- **FR-007**: The system MUST associate each Bunny video with a teacher, course/package, lesson, Bunny library, Bunny video identifier, upload method, processing status, and upload actor.
- **FR-008**: The system MUST prevent creation of playable Bunny lesson videos when upload creation fails before Bunny returns a usable video identifier.
- **FR-009**: The system MUST expose Bunny playback to students through the existing protected video viewing experience without exposing Bunny write credentials or admin billing data.
- **FR-010**: The system MUST track Bunny usage and cost per Bunny video.
- **FR-011**: The system MUST aggregate Bunny cost totals by teacher, by course/package, and across the platform.
- **FR-012**: The system MUST attribute Bunny video cost to the teacher associated with the video, regardless of whether the teacher or an admin uploaded it.
- **FR-013**: The system MUST restrict all Bunny cost, storage, bandwidth, and billing reporting to admins only.
- **FR-014**: The system MUST ensure teacher-facing surfaces and teacher API responses omit Bunny cost, storage, bandwidth, and billing fields.
- **FR-015**: The system MUST provide a way to sync or refresh Bunny processing and usage data so reporting does not rely only on local upload metadata.
- **FR-016**: The system MUST record clear status and error details for Bunny uploads, Bunny URL fetches, processing, usage sync, and AI workflow attempts.
- **FR-017**: The existing AI video analysis workflow MUST support Bunny videos when the video is ready and media access needed by the AI workflow is available.
- **FR-018**: The system MUST show an actionable unavailable/processing/unsupported state when Bunny AI analysis cannot run yet or cannot run for a specific video.
- **FR-019**: The system MUST keep Bunny API credentials, pricing configuration, and any signed playback/token details out of frontend bundles and teacher-visible responses.
- **FR-020**: The system MUST maintain auditability for state-changing Bunny operations including upload creation, link creation, status refresh, and cost sync.
- **FR-021**: The system MUST seed Bunny cost settings with current official Bunny Stream defaults: storage from $0.01/GB and CDN/bandwidth from $0.005/GB, while allowing admins to edit the stored rates before reports are calculated.
- **FR-022**: The system MUST support resumable local Bunny uploads for large files, including visible upload progress and recoverable retry behavior for interrupted uploads.
- **FR-023**: The system MUST store dated Bunny usage and cost snapshots and allow admins to filter cost reports by monthly reporting windows.
- **FR-024**: The system MUST preserve the pricing rates used for each saved cost snapshot so historical reports remain auditable after settings change.

### Key Entities *(include if feature involves data)*

- **Video Provider**: The source type for a lesson video. Supported values include YouTube, VK, and Bunny.
- **Bunny Video Asset**: A platform record for a Bunny-hosted video, including Bunny identifiers, status, upload method, linked teacher, linked course/package, linked lesson, and upload actor.
- **Bunny Upload Request**: A user action to upload a local file or request a remote URL fetch into Bunny.
- **Bunny Usage Snapshot**: Usage and cost measurements for one Bunny video over a dated reporting period, including storage, bandwidth, pricing rates used, calculated cost, and sync timestamp.
- **Bunny Cost Aggregate**: Admin-only summary of Bunny cost by video, teacher, course/package, or platform.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of existing YouTube and VK lesson videos remain playable and editable after Bunny provider support is enabled.
- **SC-002**: An authorized teacher can complete a Bunny local file upload flow and see the resulting lesson video in under 5 minutes excluding Bunny transcoding time for large files.
- **SC-003**: An admin can upload a Bunny video for a selected teacher/course/lesson and the resulting cost attribution is assigned to that teacher in 100% of tested cases.
- **SC-004**: Admin cost reports show monthly-filtered per-video, per-teacher, per-course/package, and platform totals with storage and bandwidth fields for synced Bunny videos.
- **SC-005**: Teacher-facing pages and API responses expose 0 Bunny cost, storage, bandwidth, or platform total fields.
- **SC-006**: Bunny upload, status refresh, and cost sync failures produce clear user-facing or admin-facing error states without creating broken playable videos.
- **SC-007**: AI analysis for Bunny videos either completes successfully for ready videos or produces a clear processing/unsupported state with no unhandled errors.

## Assumptions

- Bunny will be integrated through Bunny Stream, not Bunny generic storage, because the requirement is video upload/playback.
- One primary Bunny Stream library is enough for the first version unless planning discovers a strong reason to map libraries per teacher.
- Pricing rates for storage and bandwidth are configurable by admins. The initial defaults are based on Bunny Stream's official pricing page as of 2026-06-17: storage from $0.01/GB and CDN/bandwidth from $0.005/GB. Pricing may vary by replication points, volume tier, region, or account terms, so reports use the admin-saved rates.
- Cost reporting may use periodic snapshots and best-effort estimates rather than real-time billing-grade invoices.
- Remote URL upload requires a direct HTTP/HTTPS URL reachable by Bunny; private sources requiring interactive login are out of scope.
- Teachers can upload only into lessons/courses they are already authorized to manage under existing teacher authorization rules.
- Student playback remains inside the existing secure video player surface.
- Local file upload UX must be designed for large educational videos and unreliable connections, so progress and resume/retry states are part of the first release scope.
- Bunny cost reporting is monthly by default and stores dated snapshots so old reports do not change when pricing settings are edited later.
