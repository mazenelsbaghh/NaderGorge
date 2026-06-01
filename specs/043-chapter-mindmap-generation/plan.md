# Implementation Plan: Chapter Mindmap Generation

**Branch**: `043-chapter-mindmap-generation` | **Date**: 2026-04-01 | **Spec**: [spec.md](../spec.md)
**Input**: Feature specification from `/specs/043-chapter-mindmap-generation/spec.md`

## Summary

The system will automatically generate educational mind maps for video chapters using the `gemini-3.1-flash-image-preview` model. The feature leverages prompt engineering to create Arabic mind maps featuring a caricature of the teacher based on admin-uploaded reference photos. The generated image will be natively displayed within and beneath the student's video player experience.

## Technical Context

**Language/Version**: C# 13 (.NET 9) Backend, TypeScript 5.x Frontend, Node.js v20+ Worker
**Primary Dependencies**: @google/genai (Node.js SDK), node:fs, EF Core 9.0 (C#), framer-motion (React)
**Storage**: PostgreSQL (Data Store) and Local Storage for images (no S3)
**Testing**: xUnit (C#), Jest (TS)
**Target Platform**: Web (Responsive Desktop & Mobile)
**Project Type**: Monolith + External Background Worker
**Performance Goals**: Image served in < 2 seconds
**Constraints**: Avoid `next/image` in motion containers. Ensure resilient Arabic text generation via prompt engineering constraints.
**Scale/Scope**: Impacts existing `LessonChapter` schemas. Demands integration with Gemini Image Generation capabilities via REST API or GenAI SDK.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Modular Clean Architecture**: Handled. The `TeacherPhoto` entity stays in backend Domain; generative tasks remain isolated inside the BullMQ `Services` in the Node Worker.
- **Provider Abstraction First**: True. Image generation should ideally sit behind an abstraction layer; S3 image uploads must use existing file storage managers.
- **Frontend Reliability**: True. Framer-motion overlays will use native `<img>` tags, circumventing layout shift issues often associated with `next/image` lazy loading inside modals as requested in constitution.

## Project Structure

### Documentation (this feature)

```text
specs/043-chapter-mindmap-generation/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
backend/
├── src/
│   ├── NaderGorge.Domain/
│   │   ├── Entities/TeacherPhoto.cs
│   │   └── Entities/LessonChapter.cs (modified)
│   ├── NaderGorge.Application/
│   │   ├── Features/Internal/Commands/AiAnalysisCompletedCommand.cs
│   │   ├── Features/Teacher/Commands/UploadTeacherPhotoCommand.cs
│   └── NaderGorge.Infrastructure/
│       └── Persistence/Migrations/

frontend/
├── src/
│   ├── app/admin/lessons/
│   ├── components/video/SecureVideoPlayer.tsx
│   ├── components/video/InteractiveTimeline.tsx
│   └── components/admin/AdminTeacherPhotoUpload.tsx

worker/
├── src/
│   └── services/geminiService.ts (modified for image generation)
```

**Structure Decision**: Monolith structure spanning `.NET Backend`, `Next.js Frontend`, and `Node.js Worker`.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Directly referencing Gemini via `geminiService.ts` | The API surface for Imagen 3 prompt engineering constraint matching is hyperspecific to Google Gen AI. | A generic "ImageGenerationProvider" fails to properly encode native Few-Shot examples required by this exact spec. |
