# Implementation Plan: Cancel AI Analysis and Provider Handling

**Branch**: `038-cancel-ai-analysis` | **Date**: 2026-03-31 | **Spec**: [link](spec.md)
**Input**: Feature specification from `/specs/038-cancel-ai-analysis/spec.md`

## Summary

Add a reliable mechanism for administrators to cancel ongoing AI video analysis jobs directly from the UI to prevent stuck tasks or wasted API quota. Ensure that provided URLs (like Youtube IDs or Telegram embeds) are gracefully normalized before execution using `yt-dlp`.

## Technical Context

**Language/Version**: C# 13, TypeScript, Node.js  
**Primary Dependencies**: BullMQ, Express, `yt-dlp`, .NET API  
**Storage**: Redis (BullMQ queues), PostgreSQL (state)  
**Testing**: Manual E2E Validation via `make dev`  
**Target Platform**: Web Dashboard & Node Worker  

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] **Modular Clean Architecture**: Handled via clean separation between worker logic and internal API callbacks.
- [x] **Provider Abstraction First**: Node worker dynamically handles different provider formats, keeping the core AI command agnostic.
- [x] **Observability**: Exposes accurate job states rather than masking failures.

## Project Structure

### Documentation (this feature)

```text
specs/038-cancel-ai-analysis/
├── plan.md
├── research.md          
├── data-model.md        
├── quickstart.md        
├── contracts/           
│   └── bullmq-api.md
└── tasks.md             
```

### Source Code

```text
backend/src/NaderGorge.API/
└── Controllers/Admin/InternalController.cs
backend/src/NaderGorge.Application/
└── Features/Admin/Commands/CancelAnalyzeVideoAICommand.cs

worker/src/
├── index.ts
└── utils/audioExtractor.ts

frontend/src/
├── components/admin/LessonVideoList.tsx
└── services/admin-service.ts
```

**Structure Decision**: Single Project architecture distributed across independent modules (.NET, Worker, Frontend).
