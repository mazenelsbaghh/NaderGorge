# Implementation Plan: AI Agent Chaptering Workflow

**Branch**: `039-ai-agent-chaptering` | **Date**: 2026-03-31 | **Spec**: [specs/039-ai-agent-chaptering/spec.md](spec.md)
**Input**: Feature specification from `specs/039-ai-agent-chaptering/spec.md`

## Summary

Convert the monolithic AI video analysis routine into a stateful, resilient multi-stage job capable of caching intermediate artifacts (like heavy audio files) to ensure rapid recovery upon retries, all while communicating granular progress stages to the dashboard.

## Technical Context

**Language/Version**: Node.js (v20+), React (Next.js), C# (.NET 9)  
**Primary Dependencies**: BullMQ, @google/genai, tailwindcss  
**Storage**: Redis (BullMQ UI State), Local Storage (`.tmp`) for Audio/SRT  
**Testing**: Manual Dashboard Checks  
**Target Platform**: Browser (Admin Panel), Linux Server (Worker)
**Project Type**: Background Job Orchestration, React UI update
**Performance Goals**: Support 1-hour videos gracefully. Zero-data-loss retries.
**Constraints**: Keep it natively integrated with BullMQ mechanisms. Avoid heavy frameworks like LangGraph since the pipeline is strictly linear.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Modular Clean Architecture**: Yes. All AI execution is confined securely within the Node Worker module.
- **Provider Abstraction First**: Yes (uses existing Gemini service).
- **Security & Access Control by Default**: Yes. Admin dashboard endpoints are already secured.
- **Phased Delivery with MVP Discipline**: Yes. Building a state machine on top of existing BullMQ jobs is minimalist and highly robust.
- **Academic Content Integrity**: N/A
- **Single-Flow Registration & UX Simplicity**: Progress tracking explicitly requested to resolve UX anxiety during 7-minute waits.
- **Observability & Operational Readiness**: Yes. This feature inherently boosts observability by persisting detailed sub-stages.
- **Premium Editorial Design System**: Yes. The frontend will adapt cleanly to new progress formats.

## Project Structure

### Documentation (this feature)

```text
specs/039-ai-agent-chaptering/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── contracts/           
│   └── progress-payload.md
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code

```text
worker/
├── src/
│   ├── jobs/
│   │   └── analyzeVideoChapters.ts  # Add state machine logic and stage artifacts parsing
│   └── index.ts                     # Modify status API to parse JSON progress objects
frontend/
└── src/
    └── components/
        └── admin/
            └── LessonVideoList.tsx  # Update progress payload consumption (progress details)
```

**Structure Decision**: The Node backend worker orchestrates the pipeline, saving stage info, while the React Admin dashboard displays the dynamic progress structure correctly.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Overloading Progress Payload | Sub-stages need UI feedback | Storing separate redis keys scatters the state tracking away from BullMQ's native polling mechanism. |
