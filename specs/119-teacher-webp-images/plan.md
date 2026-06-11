# Implementation Plan: Teacher Image WebP Conversion

**Branch**: `119-teacher-webp-images` | **Date**: 2026-06-11 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/119-teacher-webp-images/spec.md`

## Summary

Optimize and enforce WebP format for all teacher uploaded images (avatar profiles and AI analysis photos).
We will perform client-side Canvas-based resizing and WebP compression in the browser (reducing payload size and CPU usage on the server), rename filenames to `.webp` on upload, and update backend handlers to detect the MIME type and save the files with `.webp` extensions on disk.

## Technical Context

- **Language/Version**: C# 13 (.NET 9) Backend, TypeScript 5.x (Next.js 16.2.1) Frontend
- **Primary Dependencies**: Next.js App Router, Axios service layer, MediatR, EF Core 9
- **Storage**: Local filesystem (`wwwroot/uploads/teacher/`), PostgreSQL (`TeacherProfile` and `TeacherPhoto` tables)
- **Testing**: Frontend typecheck and build verification, Backend compilation and MediatR handler validation
- **Target Platform**: Docker-compose deployment (Linux Alpine/Slim container runtime)
- **Project Type**: Web application + Web service
- **Performance Goals**: Reduce uploaded image sizes by >85% (average size <100KB), maintain page load speeds (TTFB <300ms, LCP optimization)
- **Constraints**: No third-party backend C# image libraries required (all compression and format conversion is offloaded client-side to save server CPU/Memory).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Layer Impact**:
  - **Backend**: Modify `UploadTeacherProfileImageCommand` and `UploadTeacherPhotoCommand` handlers to dynamically override file extensions based on base64 headers.
  - **Frontend**: Modify `image-compressor.ts`, `AdminTeachersPageClient.tsx`, and `TeacherProfilePageClient.tsx` to handle `.webp` renaming.
  - **Database**: No schema changes.
  - **Docker**: No build changes, but the built frontend standalone and backend net9.0 must start and serve static uploads.
- **Automated Tests**:
  - Verify that the frontend builds without type errors (`npm run build`).
  - Verify backend project compiles without errors.
- **Manual QA**:
  - Test profile image upload from Admin dashboard and Teacher profile.
  - Test AI photo upload from Admin dashboard and Teacher profile.
  - Verify files on disk have `.webp` extension and are valid WebP formats.

## Project Structure

### Documentation (this feature)

```text
specs/119-teacher-webp-images/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
└── tasks.md             # Phase 2 output
```

### Source Code (repository root)

```text
backend/
├── src/
│   ├── NaderGorge.API/
│   ├── NaderGorge.Application/
│   │   └── Features/Admin/Commands/TeacherPhotoOps/
│   │       ├── UploadTeacherProfileImageCommand.cs
│   │       └── UploadTeacherPhotoCommand.cs
│   ├── NaderGorge.Domain/
│   └── NaderGorge.Infrastructure/
 
frontend/
├── src/
│   ├── app/
│   │   ├── admin/teachers/AdminTeachersPageClient.tsx
│   │   └── teacher/profile/TeacherProfilePageClient.tsx
│   ├── services/
│   └── utils/
│       └── image-compressor.ts
```

**Structure Decision**: Standard Web Application structure. Modify files in-place according to C# and Next.js layers.

## Phase Closure & Verification Plan

**Automated Tests Required**:
- Build and type-check the frontend:
  ```bash
  npm run build
  ```
- Build the backend:
  ```bash
  dotnet build
  ```

**Docker Gate Required**:
- Verify docker compose starts up and service containers are healthy.

**Manual QA Required**:
- Log in to the Admin Dashboard.
- Edit/create a teacher.
- Upload a PNG/JPG profile image.
- Verify image saves as `.webp`.
- Repeat for Teacher Profile.

**End-of-Phase Report Format**:
- Summary of files modified
- Build checks confirmation
- Manual QA results with file sizes before/after compression
