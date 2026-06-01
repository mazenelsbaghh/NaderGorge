# Implementation Plan: [FEATURE]

**Branch**: `[###-feature-name]` | **Date**: [DATE] | **Spec**: [link]
**Input**: Feature specification from `/specs/[###-feature-name]/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

Implements enhancements to the exam and question interfaces by adding audio voice collections and hint features to individual questions. Extends grading capabilities by offloading essay submissions to AI via the Node.js BullMQ worker, with a teacher review step. Introduces a new "Find the Mistake" interactive question type built with React for high accessibility and strict UI consistency.

## Technical Context

**Language/Version**: TypeScript 5.x (Frontend, Node.js Worker), C# 13 / .NET 9 (Backend API)  
**Primary Dependencies**: Next.js App Router, Entity Framework Core 9.0, BullMQ, `@google/genai` (Node.js SDK)  
**Storage**: PostgreSQL (Data Store) and Local/S3 storage for audio  
**Testing**: xUnit, Jest (or standard framework matching ecosystem constraints)  
**Target Platform**: Web Browsers (Mobile and Desktop)
**Project Type**: web-service + frontend application  
**Performance Goals**: Fast audio playback loading (<0.5s), AI grading loop <10s  
**Constraints**: Standard `.NET` deployment constraints, Gemini generic API limits  
**Scale/Scope**: ~10k concurrent users processing text interaction/audio playback

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Modular Clean Architecture**: Enhancements properly placed in `Questions` and `Exam` domain boundaries without bleeding logic.
- **Academic Content Integrity**: "Find the Mistake" and Essay Grading maintain the structured assessment parameters. Teacher final approval on AI essay grades guarantees content authority.
- **Frontend Reliability & Rendering Strictness**: The new interactive tokens component for "Find the Mistake" avoids fragile DOM manipulation by mapping correctly to state limits.
- **Assessment & Time Integrity**: AI execution is offloaded and doesn't interfere with real-time test ticking on the backend.

## Project Structure

### Documentation (this feature)

```text
specs/055-exam-qs-enhancements/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
frontend/
├── src/
│   ├── app/admin/       # Question builder components with audio/hints support
│   └── components/
│       ├── exam/        # FindTheMistake UI component and hint rendering
│       └── video/       # Reusing standard audio player interactions
backend/
├── src/NaderGorge.Application/Features/Exams/
├── src/NaderGorge.Domain/Entities/
└── src/NaderGorge.API/Controllers/

worker/
└── src/                 # Node.js BullMQ process for AI Essay Grading
```

**Structure Decision**: Selected the Web Application structure with C# Backend, React Frontend, and Node Worker per the existing architectural standard for AI-assisted features.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| [e.g., 4th project] | [current need] | [why 3 projects insufficient] |
| [e.g., Repository pattern] | [specific problem] | [why direct DB access insufficient] |
