# Feature Specification: Audio Upload Restrictions & Review Display

**Feature Branch**: `136-audio-upload-restrictions`  
**Created**: 2026-06-16  
**Status**: Draft  
**Input**: User description: "عايز وانا بفع الملف الصوتي حاجتين اول حاجه ماينفعش ارفع غير ملفات صوت و تاني حاجه الفويس يظهر ف المراجعه بتاعت الامتحان او الواجب بظهر"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Restrict Voice Note Uploads to Audio Files (Priority: P1)

As a student submitting an essay (homework) or a voice answer in an exam,
I want the upload input to only accept audio files and restrict selection to audio formats,
So that I cannot accidentally or intentionally upload non-audio files (like PDFs, images, executables, etc.).

**Why this priority**: Crucial for data integrity, preventing invalid uploads, and reducing server resource consumption/security risks.

**Independent Test**: Can be fully tested by attempting to select or drop a non-audio file (e.g. PDF/TXT) in the file selector dialog and ensuring it is rejected, while verifying that standard audio formats (MP3, WAV, M4A, WEBM) are accepted and successfully uploaded.

**Acceptance Scenarios**:

1. **Given** a student is on the homework submission page or an exam page that allows voice notes, **When** they click "Upload Audio File", **Then** the native file picker MUST filter for and only allow selection of audio files (e.g., `accept="audio/*"`).
2. **Given** a student attempts to bypass frontend controls and upload a non-audio file (e.g. file with modified extension like `malicious.mp3` but containing HTML/text), **When** the backend receives the upload, **Then** the backend MUST validate the file content/mime type and return a 400 Bad Request indicating only audio files are allowed.

---

### User Story 2 - Audio Player in Homework Review (Priority: P1)

As a student reviewing my submitted homework (or an admin reviewing/grading a student's homework),
I want to see and play the uploaded voice note directly within the homework review panel,
So that I can listen to the spoken content alongside the written solution.

**Why this priority**: Crucial for completing the feedback loop for homework that includes oral/audio components.

**Independent Test**: Can be tested by submitting homework with a voice note, opening the homework review page, and confirming that the HTML5 audio player displays the submitted audio and can play it back.

**Acceptance Scenarios**:

1. **Given** a student has submitted a homework attempt that includes an audio upload, **When** they open the homework review panel, **Then** the audio player MUST be rendered and source the uploaded audio file.
2. **Given** a student has submitted a homework attempt WITHOUT an audio upload, **When** they open the homework review panel, **Then** no audio player should be rendered.

---

### User Story 3 - Audio Player in Exam Review (Priority: P1)

As a student reviewing my completed exam (or an admin grading a student's exam attempt),
I want to see and play the uploaded voice note for the relevant question directly in the exam review panel,
So that I can verify my submitted spoken answers.

**Why this priority**: Necessary for reviewing oral questions in examinations.

**Independent Test**: Can be tested by completing an exam containing an essay question with audio answer, submitting the exam, opening the exam review, and playing back the audio.

**Acceptance Scenarios**:

1. **Given** a student has submitted an exam with an audio answer for a question, **When** they open the exam review panel, **Then** the audio player MUST be rendered below the question and play the submitted audio.
2. **Given** an admin is grading/reviewing the student's exam attempt, **When** they view the student's submission, **Then** the audio player MUST be visible and functional.

---

### Edge Cases

- **File Spoofing / Extension Renaming**: A user renames `image.png` to `image.mp3`. The backend mime-type detector MUST check the actual signature or content type and reject it if it is not an audio mime type.
- **Empty Audio Files**: An audio file size is 0 bytes. The system must reject empty files.
- **Missing Audio Data**: When a submission has no audio, the frontend review screen must gracefully handle null `audioUrl` without crashing or showing a broken audio player.

### Manual QA & Docker Acceptance *(mandatory)*

- **Manual QA Student Upload Flow**: 
  - Login as student.
  - Navigate to a lesson homework submission or an exam question with audio support.
  - Click upload. Verify the file picker options are restricted to Audio files.
  - Choose a PDF/TXT file (if possible by forcing "all files" selector). Verify the page/system throws a validation error and blocks the upload.
  - Choose a valid `.mp3` or `.wav` file. Verify upload succeeds.
- **Manual QA Student Review Flow**:
  - After submitting homework/exam with audio, navigate to the review page.
  - Verify that the audio player is present and plays the uploaded voice note correctly.
- **Docker Acceptance**:
  - Ensure the backend build and frontend build complete without warnings or errors.
  - Run the tests and check the application container logs for any exceptions.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The student homework submission component and exam voice answer component MUST restrict file uploads to audio files. The file picker input element MUST have `accept="audio/*"`.
- **FR-002**: The file upload endpoint used by students (e.g. `/api/uploads/audio` or similar) MUST validate the uploaded file's mime type. It MUST reject files whose content type is not prefix-matched by `audio/`.
- **FR-003**: The homework review panel (`HomeworkResultPanel` or similar) MUST render a native HTML5 `<audio>` player element when the homework submission has a non-empty `audioUrl`.
- **FR-004**: The exam review panel (where students review their graded/submitted questions) MUST render an HTML5 `<audio>` player when a question has an associated `audioUrl` or `AudioUrl` in the answer submission.
- **FR-005**: The admin grading page for exams and homework MUST also show the audio player for any questions/homework containing a student audio submission.

### Key Entities *(include if feature involves data)*

- **EssaySubmission**: Represents a student's homework submission. Has attributes including `AudioUrl` (string).
- **AnswerSubmissionDto / StudentExamAttempt / Question**: Represents the answer submitted by a student to an exam question, which can include `AudioUrl` (string).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of non-audio files are blocked at the client and API level.
- **SC-002**: File validation response time on the backend is under 2 seconds.
- **SC-003**: Review page audio player loads in under 500 ms.
- **SC-004**: 100% responsiveness for the audio player on mobile and desktop layout views.

## Assumptions

- We assume there is an existing API endpoint for uploading files, or specifically audio files, that needs validation logic updated.
- We assume that the backend already persists `AudioUrl` (or `audioUrl`) fields for homework submissions and exam submissions, and that these fields are returned in the review API payloads.
- Custom styling for the audio player should match the theme of the application (Nader Gorge custom theme).
