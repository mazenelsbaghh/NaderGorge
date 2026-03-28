# Phase 0: Research & technical Decisions

## 1. File Uploads vs URL Linking
- **Issue**: Need to add Files/Documents to lessons, but the backend lacks a pre-existing `IFileStorageService` or S3 integration.
- **Decision**: For now, we will add an interface that accepts a `FileUrl` input (e.g., Google Drive link, external PDF link) rather than building out complex multipart file uploading.
- **Rationale**: Keeps implementation lean and focused on the core structure rather than infrastructural setup.

## 2. Linking Exams
- **Issue**: Lessons need exams. The `Lesson` entity already has a nullable `ExamId`.
- **Decision**: Expose an endpoint and UI to set or clear the `ExamId` on a lesson. In the UI, populate a dropdown of available exams for the user to select.

## 3. Homework Entity
- **Issue**: We need to attach homework to lessons. The `Homework` entity already exists with `LessonId`.
- **Decision**: Add endpoints to create/update homework for a specific lesson, and a UI layer to list and manage them.

## 4. UI Cockpit 
- **Issue**: The current admin UI has `app/admin/content/packages/[id]` and `app/admin/content/sections/[id]` but lacks a `app/admin/content/lessons/[id]` detail page.
- **Decision**: Create a `app/admin/content/lessons/[id]/page.tsx` page representing the "Lesson Cockpit" to manage Videos, Resources, Homework, and Exams as requested by the user.
