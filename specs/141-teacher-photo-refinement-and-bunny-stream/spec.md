# Feature Specification: Teacher Photo Refinement and Bunny Stream

**Feature Branch**: `141-teacher-photo-refinement-and-bunny-stream`  
**Created**: 2026-06-20  
**Status**: Draft  
**Input**: User description: "الصور اللي هنا المفروض تظهر الصور اللي انا رفعهتها للمدرس وانا بعملوا فاهمني و المفروض لمي اعمل الصور يعملها علي الصور بتاعت المدرس ع طول علشان انا اصلا جوه المحتوي بتاعوا فاهمني. وعايز ف bunny نشوف الطريقه علشان ننزل السوند و نعملوا التلخيص لانها غير اليوتيوب. ف تنزيل الفيديو"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Active AI Photo Preview in Admin Portal (Priority: P1)

As an Admin editing or viewing a teacher's details, I want to see the currently active AI reference photo in the upload/edit preview box, so that I don't see an empty box and know which photo is active.

**Why this priority**: Crucial for usability and preventing admins from repeatedly uploading photos because they think it did not save.

**Independent Test**:
Can be fully tested by opening a teacher's edit modal in `/admin/teachers` or choosing a teacher in `/admin/ai-monitor` and asserting that their existing active AI photo is loaded and displayed.

**Acceptance Scenarios**:

1. **Given** a teacher has an active AI reference photo uploaded, **When** the admin opens their edit modal on `/admin/teachers`, **Then** the active AI photo is displayed in the "صورة التحليل للذكاء الاصطناعي (AI)" preview container.
2. **Given** the admin is on `/admin/ai-monitor`, **When** the admin selects a teacher from the dropdown (or inherits a teacher ID from the URL query parameter), **Then** the active AI reference photo for that teacher is retrieved and shown in the preview box.

---

### User Story 2 - Contextual Teacher Photo Upload (Priority: P1)

As an Admin on the AI Monitor page, when I upload and save an AI reference photo, I want it to be associated directly with the teacher currently selected or in-context, rather than defaulting to the logged-in administrator's user ID.

**Why this priority**: Essential to ensure that AI reference photos are mapped to the correct teachers who own the lessons and videos, rather than the admin who uploads them.

**Independent Test**:
Can be tested by selecting a teacher in `/admin/ai-monitor`, uploading a new photo, clicking "حفظ واستخدام", and verifying that the database record maps the new photo to that teacher's `TeacherId`.

**Acceptance Scenarios**:

1. **Given** an admin is on `/admin/ai-monitor` and selects Teacher A, **When** they upload a photo and click "حفظ واستخدام", **Then** the photo is saved with `TeacherId` equal to Teacher A's user ID.
2. **Given** an admin triggers AI analysis on a video belonging to Teacher A, **When** the backend schedules the analysis job, **Then** it fetches the active AI reference photo associated with Teacher A to use as the character avatar generator reference.

---

### User Story 3 - Bunny Stream Video Audio Extraction (Priority: P1)

As the system worker, I want to extract and download the audio track from Bunny Stream videos using the video GUID, so that I can perform transcription and chapter summary generation.

**Why this priority**: Critical to enable the core AI features (transcription, chaptering, mindmaps) for Bunny Stream video provider, which has a different stream delivery mechanism than YouTube.

**Independent Test**:
Can be tested by running the worker's audio extraction logic on a Bunny Stream video GUID and verifying that it successfully produces a valid local MP3 audio file.

**Acceptance Scenarios**:

1. **Given** a video uses provider `"bunny"` with a GUID, **When** the worker processes the AI chaptering job, **Then** it downloads the video/audio using the Bunny Stream iframe embed endpoint and the official Referer header `https://admin.massar-academy.net/` via `yt-dlp` (or via API MP4 fallbacks).
2. **Given** the worker downloads the media, **When** it completes, **Then** it produces a standard 16kHz mono MP3 file at the expected local path for speech-to-text processing.

---

### Edge Cases

- **Teacher has no active photo**: If a teacher has no active AI reference photo, the preview displays the default upload state. During AI analysis, the system will fall back gracefully (e.g. queue the job without a teacher photo or use a placeholder) without crashing.
- **Bunny Stream Video does not have MP4 fallback enabled**: If direct MP4 URLs are not available, `yt-dlp` must download the HLS stream from `iframe.mediadelivery.net` using the admin referer.
- **Bunny API/Download fails**: The worker must handle download timeouts and rate limits gracefully, updating the job status to `failed` with a descriptive message.

### Manual QA & Docker Acceptance *(mandatory)*

- **Manual QA Role/Flow 1**: Admin goes to `/admin/teachers`, clicks edit on a teacher who already has an AI photo. Expect to see their AI photo in the upload box.
- **Manual QA Role/Flow 2**: Admin goes to `/admin/ai-monitor?teacherId=<id>`. Expect the teacher to be selected and their photo displayed.
- **Manual QA Negative Check**: Admin selects Teacher A, uploads a photo. Inspect the database and ensure the photo is NOT saved under the admin's user ID.
- **Docker Acceptance**: `make up` and `make migrate` run successfully. Worker container gets `BUNNY_STREAM_LIBRARY_ID` and `BUNNY_STREAM_API_KEY` from docker-compose.
- **External Dependencies**: Bunny Stream API key and library ID must be configured in the `.env` file.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The C# backend MUST expose an API endpoint `GET /api/admin/teachers/{teacherId}/active-photo` that returns the active AI reference photo URL for the specified teacher.
- **FR-002**: The C# backend MUST expose an API endpoint `GET /api/teacher/profile/active-photo` for the logged-in teacher to retrieve their own active AI reference photo.
- **FR-003**: The admin video analysis commands (chaptering, mindmap generation, single mindmap regeneration) MUST fetch the active `TeacherPhoto` of the teacher who owns the lesson's package, instead of using a global fallback.
- **FR-004**: The React frontend MUST display the active AI reference photo on mount or teacher selection in both `/admin/teachers` edit modal and `/admin/ai-monitor`.
- **FR-005**: The React frontend `/admin/ai-monitor` page MUST allow the admin to select a teacher from a dropdown list to view and manage their AI reference photo.
- **FR-006**: The worker service MUST support Bunny Stream video downloads using `yt-dlp` by constructing the iframe URL `https://iframe.mediadelivery.net/embed/{libraryId}/{videoId}` with the referer `https://admin.massar-academy.net/`.
- **FR-007**: The worker service's environment variables MUST include `BUNNY_STREAM_LIBRARY_ID` and `BUNNY_STREAM_API_KEY` mapped from `docker-compose.yml`.

### Key Entities *(include if feature involves data)*

- **TeacherPhoto**: Represents reference photos for teachers.
  - `TeacherId` (Guid, references User)
  - `FileUrl` (string)
  - `IsActive` (bool)
  - `UploadedAt` (DateTime)
- **LessonVideo**: Represents the video linked to a lesson.
  - `Provider` (string, e.g. "bunny", "youtube", "vk")
  - `ProviderVideoId` (string, stores the Bunny video GUID or YouTube video ID)

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Admins can view the correct active AI reference photo for any teacher within 1 second of opening the edit modal or selecting them in the dropdown.
- **SC-002**: 100% of uploaded teacher photos from the AI Monitor page are mapped to the selected teacher instead of the admin user.
- **SC-003**: The worker successfully extracts audio tracks from 100% of valid Bunny Stream videos for which analysis is triggered.

## Assumptions

- The administrator has populated the `BUNNY_STREAM_LIBRARY_ID` and `BUNNY_STREAM_API_KEY` in the environment variables.
- The Bunny Stream library has security referrers configured to allow `admin.massar-academy.net`.
