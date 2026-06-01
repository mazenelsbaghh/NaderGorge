# Implementation Plan: [FEATURE]

**Branch**: `[###-feature-name]` | **Date**: [DATE] | **Spec**: [link]
**Input**: Feature specification from `/specs/[###-feature-name]/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

Implement an AI-powered automated video processing pipeline using FFmpeg and Gemini File API embedded in a Node.js worker. It extracts audio, generates SRTs and structured Video Chapters, and returns them to the .NET Backend to be displayed on an interactive Next.js timeline element without risking HTTP timeouts.

## Technical Context

<!--
  ACTION REQUIRED: Replace the content in this section with the technical details
  for the project. The structure here is presented in advisory capacity to guide
  the iteration process.
-->

**Language/Version**: C# 13 (.NET 9) Backend, Node.js Worker, TypeScript Frontend
**Primary Dependencies**: EF Core, `@google/genai` (Node), `fluent-ffmpeg` (Node), BullMQ (Node), StackExchange.Redis (.NET)
**Storage**: PostgreSQL (Data), Local/S3 Bucket (SRT Files), Redis (Job Queue)
**Testing**: xUnit (.NET), Jest/Playwright (Frontend)
**Target Platform**: Backend API + Next.js App Router
**Project Type**: Full-Stack Educational Platform Feature
**Performance Goals**: AI Background processing strictly decoupled from HTTP request timeouts.
**Constraints**: Must strictly use Gemini File API with FFmpeg to satisfy the "Free and Fast" constraint. Total backend response time to enqueue job < 200ms.
**Scale/Scope**: Dozens of 1-3 hour videos. Background processing can take several minutes per video.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] **Modular Clean Architecture**: Chapters and AI background jobs will live natively in the specific Domains (e.g. `LessonVideo` extension).
- [x] **Provider Abstraction First**: Gemini usage will be grouped under an AI module in the Node worker, isolated from business logic.
- [x] **Security & Access Control**: Webhook endpoint from Node to .NET must be secured. Admin trigger endpoint requires Admin Role.
- [x] **Phased Delivery**: Delivered as an independent set of endpoints and DB migrations.
- [x] **Academic Content Integrity**: Chapter marks directly map to the approved lesson video context.
- [x] **Observability**: Background operations rely on BullMQ, observable status exposed to UI.
- [x] **Design System**: Chapter list uses standard surface containers without rigid lines.

## Project Structure

### Documentation (this feature)

```text
specs/[###-feature]/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

```text
backend/
├── src/
│   ├── NaderGorge.Domain/
│   │   └── Entities/Content/VideoChapter.cs
│   ├── NaderGorge.Application/
│   │   └── Features/Admin/Commands/AnalyzeVideoAICommand.cs
│   ├── NaderGorge.Infrastructure/
│   │   └── Data/Migrations/
│   └── NaderGorge.API/
│       └── Controllers/AdminController.cs
worker/
├── src/
│   ├── jobs/analyzeVideoChapters.ts
│   └── services/geminiService.ts
frontend/
├── src/
│   ├── app/admin/content/lessons/[id]/videos/client-component.tsx
│   └── components/video/InteractiveTimeline.tsx
```

**Structure Decision**: The logic spans the backend APIs, the new Node.js worker environment (for BullMQ + FFmpeg), and the frontend `embed` or `client` components.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| [e.g., 4th project] | [current need] | [why 3 projects insufficient] |
| [e.g., Repository pattern] | [specific problem] | [why direct DB access insufficient] |
