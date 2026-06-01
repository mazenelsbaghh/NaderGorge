# Implementation Plan: Two-Phase AI Mindmap Generation

**Branch**: `044-two-phase-mindmaps` | **Date**: 2026-04-01 | **Spec**: [spec.md](../spec.md)
**Input**: Feature specification from `/specs/044-two-phase-mindmaps/spec.md`

## Summary

The objective is to decouple the video audio analysis block from the mind map image generation process entirely. First, sections and subtitles are generated, ensuring no waste on images if chapters need edits. Then, a dedicated action from the UI allows users to safely generate expressive chapter mind maps alongside an uploaded teacher character visually referencing who presents the lesson.

## Technical Context

**Language/Version**: TypeScript 5.x (Frontend, Node.js Worker), C# 13 / .NET 9 (Backend API)  
**Primary Dependencies**: React (Next.js App Router API Handlers), Entity Framework Core (C#), BullMQ, `@google/genai`  
**Storage**: PostgreSQL (Data Store, DB migrations), Redis (BullMQ queue broker and Job Queue backend locking)  
**Testing**: Jest (Node) / xUnit (Backend) - where applicable  
**Target Platform**: Linux server  
**Project Type**: Web Application & Background Job Worker Pipeline  
**Performance Goals**: Decoupled AI generation avoids locking UI indefinitely. Asynchronous event triggers should dispatch within 1 second.  
**Constraints**: BullMQ worker implementation must not block or crash other ongoing AI processing (concurrency handled properly).  
**Scale/Scope**: Scales horizontally along worker processors via Redis.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] Backend architecture correctly separated via MediatR Commands (`GenerateChapterMindmapsCommand`).
- [x] BullMQ jobs are independent interfaces.
- [x] Frontend adheres to strict "Editorial Scholar" guidelines using framer motion and tonal boundaries for the Admin Button.

## Project Structure

### Documentation (this feature)

```text
specs/044-two-phase-mindmaps/
├── plan.md              # This file
├── research.md          # Architecture decisions for job splitting
├── data-model.md        # Database columns and job payloads
├── quickstart.md        # Implementation quickstart guide
└── tasks.md             # Task decomposition (next step)
```

### Source Code (repository root)

```text
# Web application
backend/
├── src/
│   ├── NaderGorge.Domain/
│   ├── NaderGorge.Application/Features/Admin/Commands/MindmapOps/
│   └── NaderGorge.API/Controllers/AdminController.cs
│   └── NaderGorge.Infrastructure/Data/Migrations/

frontend/
├── src/
│   ├── components/admin/AdminLessonVideoList.tsx
│   ├── components/admin/AdminTeacherPhotoUpload.tsx

worker/
├── src/
│   ├── index.ts
│   ├── services/geminiService.ts
│   └── services/apiService.ts
```

**Structure Decision**: Web application option adopted. C# handles REST backend API commands which submit background jobs to Node.js (BullMQ queue), which invokes `geminiService`. React Next.js app powers the frontend interface invoking the C# REST endpoints.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| None | N/A | N/A |
