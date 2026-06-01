# Quickstart: Exam and Question UI Enhancements

## Context
This feature enhances the exam and question system by adding audio support, written hints, AI-assisted essay grading, and a new "Find the Mistake" interactive question type.

## Dependencies needed
- `@google/genai` (already in use for AI Chapters) in the Node.js Worker.
- Standard audio recording/playback logic within the React frontend.
- C# Entity Framework migrations for the new `Question` and `EssaySubmission` attributes.

## How to begin development
1. **Database Update**: Create EF Core migrations in the .NET backend to add `audioUrl`, `writtenCorrection`, `hintText` to `Questions`, and create the `EssaySubmissions` table.
2. **Backend API**: Add endpoints for audio upload, question creation/updating (with new fields), and teacher grading. Configure the webhooks/BullMQ integration for sending essay texts to the Node Worker.
3. **Worker**: Implement a new job handler in the Node Worker to receive an essay submission, prompt Gemini for a grade, and update the backend via webhook.
4. **Frontend UI**: Update the Question Builder (Admin/Teacher view) to allow audio uploads and adding hints. Create the custom interactive component for "Find the Mistake" highlight interactions.
5. **Exam Player**: Update the student-facing exam player to render hints explicitly without cost, and display audio/written corrections upon exam review.
