# Implementation Plan: Audio Upload Restrictions & Review Display

**Branch**: `136-audio-upload-restrictions` | **Date**: 2026-06-16 | **Spec**: [/specs/136-audio-upload-restrictions-and-reviews/spec.md](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/specs/136-audio-upload-restrictions-and-reviews/spec.md)
**Input**: Feature specification from `/specs/136-audio-upload-restrictions-and-reviews/spec.md`

## Summary

This feature restricts student and admin audio file uploads to audio formats only. It also implements the rendering of student-submitted voice note answers in both exam reviews and homework reviews.

### Technical Approach
1. **Validation**: Validate that uploads are audio files in both client-side React code (via `accept="audio/*"` and checking the `File` type) and backend API controllers (`AdminController.cs` and `StudentController.cs` by verifying file `ContentType` starts with `audio/` and checking file extensions against a whitelist).
2. **Student Upload API**: Create `POST /api/Student/upload-audio` to allow authenticated students to upload voice notes for Essay questions.
3. **Homework Review**: Display an `<audio>` player in `HomeworkResultPanel.tsx` if `providedAnswer` is an audio URL.
4. **Exam Review**: Extend DTOs (`ExamQuestionReviewDto`, `QuestionReviewSnapshot`) to map and return the student's `essay.AudioUrl` as `StudentAudioUrl`, then render an `<audio>` player in `ExamViewer.tsx` when present.

## Technical Context

**Language/Version**: C# 13 (.NET 9) / TypeScript 5.x  
**Primary Dependencies**: Next.js 16.2.1, React 19, Entity Framework Core 9  
**Storage**: PostgreSQL (LessonVideo, EssaySubmissions, HomeworkAnswers)  
**Testing**: Playwright E2E tests  
**Target Platform**: Linux Server / Web Browser  
**Project Type**: Web service + SPA frontend  
**Performance Goals**: Standard loading times  
**Constraints**: Only allow audio uploads (P0)  
**Scale/Scope**: Homework results panel, exam result view  

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- Layer impact across backend (API Controller, commands/queries), frontend (Viewer and ResultPanel components, Services).
- Automated tests required: Playwright E2E test verifying audio restrictions and review player rendering.
- Docker gate commands: `docker compose config -q` and `docker compose ps` to verify container health.
- Automated tests must run and pass before final verification.

## Project Structure

### Documentation (this feature)

```text
specs/136-audio-upload-restrictions-and-reviews/
├── spec.md              # Feature specification
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
└── contracts/           
    └── endpoints.md     # API Contracts
```

### Source Code (repository root)

```text
backend/
├── src/
│   ├── NaderGorge.API/
│   │   └── Controllers/
│   │       ├── AdminController.cs      # Question audio upload validation
│   │       └── StudentController.cs    # New student audio upload endpoint
│   ├── NaderGorge.Application/
│   │   └── Features/
│   │       ├── Admin/Commands/
│   │       │   └── AdminQuestionCommands.cs # Write base64 audio to disk
│   │       ├── Exams/
│   │       │   ├── ExamResultBuilder.cs     # Map StudentAudioUrl
│   │       │   ├── Commands/SubmitExamCommand.cs # Pass essay.AudioUrl to snapshot
│   │       │   └── Queries/GetLatestPassedExamResultQuery.cs # Pass essay.AudioUrl to snapshot
│   │       └── Homework/Queries/
│   │           └── GetHomeworkResultQuery.cs
```

```text
frontend/
├── src/
│   ├── components/
│   │   ├── exams/
│   │   │   └── ExamViewer.tsx           # Audio upload input + student audio player in review
│   │   └── homework/
│   │       ├── HomeworkViewer.tsx       # Audio upload input in essay question solving
│   │       └── HomeworkResultPanel.tsx  # Student audio player in homework review
│   └── services/
│       ├── student-service.ts           # uploadAudio client endpoint
│       └── exam-service.ts              # update ExamQuestionReviewDto type
```

**Structure Decision**: Option 2: Web application (Separate backend & frontend projects).

## Phase Closure & Verification Plan

**Automated Tests Required**:
Run the newly created E2E tests:
```bash
npx playwright test frontend/tests/e2e/audio-upload-restrictions.spec.ts
```

**Docker Gate Required**:
Ensure docker containers are active:
```bash
docker compose ps
```

**Manual QA Required**:
1. Login as student, navigate to Homework and try to upload a `.txt` file for an essay question. Verify it gets rejected.
2. Upload a `.mp3` file, submit, and verify that the homework review panel displays the audio player and plays it correctly.
3. Take an exam, upload a `.wav` file for the essay question, submit, and verify that the exam review shows the voice note in the student's answer block.

**End-of-Phase Report Format**:
- Checklist status
- Test command output evidence
- QA verification screenshots or logs
