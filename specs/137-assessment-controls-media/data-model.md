# Data Model: Assessment Controls And Question Media

## Exam

- `IsMandatory`: existing boolean. Defaults to true. Controls whether lesson/video exam blocks progression.
- Relationships: Lesson-level via `Lesson.ExamId`; video-level via `LessonVideo.ExamId` or `Exam.LessonVideoId`.

## Homework

- `IsMandatory`: existing boolean. Defaults to true. Controls whether homework blocks progression.
- Relationships: belongs to one Lesson.

## QuestionBankItem

- Existing exam question source.
- Add `ImageUrl?: string`.
- Validation: nullable; when present must be a relative or trusted media URL produced by the upload endpoint.

## HomeworkQuestion

- Existing homework question source.
- Add `ImageUrl?: string`.
- Validation: nullable; when present must be a relative or trusted media URL produced by the upload endpoint.

## Student Question DTOs

- Add `ImageUrl?: string` to exam attempt questions, exam review questions, homework attempt questions, and homework review questions.
- Existing text fields remain available; frontend display must prevent raw tags from showing.
