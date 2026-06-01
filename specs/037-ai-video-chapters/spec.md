# Feature Specification: AI Video Transcription and Chapters

**Feature Branch**: `037-ai-video-chapters`  
**Created**: 2026-03-31  
**Status**: Draft  
**Input**: User description: "عايز يطلع كل كلام بتاع الحصه يعني يعمل srt للصوت كلو بعدها يعملهم فصول و يقسمهم زي شبتار ف الفيديو بلاير زي اليوتيوب كده و بعدها يعمل ملخص لكل فصل لوحدوا ويعمل بجيمناي فلاش ٢.٥ وده api key: AIzaSyB..."

## Clarifications

### Session 2026-03-31
- Q: How will the system handle long AI processing without hitting HTTP timeouts? → A: Background Job (Queue / BullMQ)
- Q: How will the SRT subtitle transcript be stored? → A: Physical file in cloud/local storage, storing only the URL in the database.
- Q: How will the generated chapters and summaries be stored? → A: In a separate relational table (`VideoChapter`) with a foreign key to the `LessonVideo`.
- Q: How will audio be processed for long videos (e.g., 1+ hours) for free? → A: Compress the audio into a small format (e.g. MP3/M4A) via FFmpeg, upload it via the **Gemini File API**, and prompt Gemini Flash 2.5 to generate the transcript, chapters, and summaries in a single step.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Automated Lesson Analysis (Priority: P1)

As a platform admin, I want to extract audio from lesson videos and process it via AI (Gemini Flash 2.5) to automatically generate SRT subtitles, chapter timelines, and chapter summaries, so that educational content is highly structured without manual effort.

**Why this priority**: Automating content structuring saves immense administrative time and builds the foundation (metadata) required for the student-facing features.

**Independent Test**: Can be tested by invoking the generation process for a specific video in the admin dashboard and verifying that the database is populated with valid SRT data, chapter timestamps, and summaries.

**Acceptance Scenarios**:

1. **Given** an uploaded or registered lesson video, **When** the admin triggers "Generate AI Chapters", **Then** the system sends the audio/metadata to Gemini Flash 2.5.
2. **Given** a successful AI processing response, **When** the data is returned, **Then** the system saves the generated SRT file, chapter list (timestamps & titles), and chapter summaries to the database.

---

### User Story 2 - Student Video Player Chapters (Priority: P2)

As a student watching a lesson, I want to see interactive chapter markers on the video player and a list of chapter summaries, so that I can easily navigate to specific topics of interest like I do on YouTube.

**Why this priority**: Enhances the student's learning experience by providing structured, easily navigable content. It relies on P1 being completed.

**Independent Test**: Can be tested by rendering a video player with synthetic/mocked chapter data and ensuring timeline markers and summary side-panels update and navigate correctly.

**Acceptance Scenarios**:

1. **Given** a video that has AI-generated chapters, **When** the student opens the lesson page, **Then** the video player displays segmented timeline markers.
2. **Given** the chapter list panel, **When** the student views it, **Then** they can read a brief AI-generated summary of what is covered in each chapter.
3. **Given** a chapter item in the list or timeline, **When** the student clicks it, **Then** the video player immediately seeks to that specific timestamp.

---

### Edge Cases

- What happens if the video audio is too corrupted or unclear for the AI to process?
- How does the system handle lesson videos that are extremely short (under 1 minute) or have no talking?
- What occurs if the AI processing service takes longer than expected or times out?
- How are overlapping chapters or incorrectly parsed transcript timestamps resolved?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST integrate directly with the Google Gemini API (File API + Flash 2.5) as a unified free and fast solution for audio-video analysis.
- **FR-002**: The system MUST extract the audio locally via FFmpeg (to heavily reduce file size to ~20-30MB) and upload it securely to Gemini's File API.
- **FR-003**: The system MUST prompt Gemini to analyze the uploaded audio and return three distinct components in one structured JSON output: a localized subtitle SRT transcript, a linear list of Chapters (Start Time, End Time, Title), and a Summary paragraph for each chapter.
- **FR-004**: The video processing MUST run asynchronously via a background job queue (e.g., BullMQ/Hangfire) to prevent HTTP timeouts, and the admin interface MUST reflect the processing status (e.g., "Processing...", "Ready").
- **FR-005**: The student-facing video player MUST display interactive chapter markers on its progress bar.
- **FR-006**: The UI MUST display a supplementary side/bottom section showing the chronological chapter titles and their corresponding AI summaries.
- **FR-007**: The system MUST allow administrators to review, manually edit, or regenerate the AI-generated chapters and summaries before publishing them to the students.

### Key Entities

- **LessonVideo**: The core lecture entity, extended to link to Chapters and Subtitles.
- **VideoChapter**: A separate relational table representing a defined segment of a video (Contains: LessonVideoId, Title, Start Time, End Time, Summary Text).
- **VideoSubtitle**: Represents the precise timed transcript data, stored as a file URL pointing to an external storage bucket or local path.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of newly processed videos successfully yield a subtitle file, at least 2 distinct chapters, and corresponding summaries without manual typing.
- **SC-002**: The AI integration processes a standard 1-hour lesson and returns structured timeline data in under 5 minutes.
- **SC-003**: Students can click on a recorded chapter and the video accurately seeks to within 2 seconds of the intended topic start.
- **SC-004**: Admsin can edit the generated chapters/summaries via the dashboard without writing code or formatting manual timestamps.

## Assumptions

- The configured hosting platforms provide accessible source media or audio for the backend to ingest and delegate to the AI service.
- The external AI service supports the necessary context windows required for analyzing long-form lesson videos (up to 2-3 hours).
- The generated textual output will be primarily in Arabic, aligning with the platform's demographic audience.
