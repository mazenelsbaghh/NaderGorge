# Implementation Plan: [FEATURE]

**Branch**: `[###-feature-name]` | **Date**: [DATE] | **Spec**: [link]
**Input**: Feature specification from `/specs/[###-feature-name]/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

Implement idempotent partial updates for `LessonVideo` and `VideoChapter` via `ExecuteUpdateAsync` and `ExecuteDeleteAsync` in `AiAnalysisCompletedCommandHandler` to prevent `DbUpdateConcurrencyException` when background worker retries arrive, thus ensuring reliable processing.

## Technical Context

<!--
  ACTION REQUIRED: Replace the content in this section with the technical details
  for the project. The structure here is presented in advisory capacity to guide
  the iteration process.
-->

**Language/Version**: C# 13, .NET 9
**Primary Dependencies**: Entity Framework Core 9.0+, MediatR
**Storage**: PostgreSQL
**Testing**: xUnit / Integration tests for MediatR handlers
**Target Platform**: Linux server Docker / Localhost
**Project Type**: Backend Web API Service
**Performance Goals**: N/A (Webhooks from Node.js background process)
**Constraints**: Must execute safely and idempotently without crashing the webhook endpoint or retrying unnecessarily through BullMQ.
**Scale/Scope**: Background processing. Limited payload size per job.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Modular Clean Architecture**: Yes. Command Handler abstracts database work natively and doesn't pollute API controllers.
- **Provider Abstraction**: N/A (AI API is abstracted out in Node.js queue, this is just DB writeback).
- **Security & Access Control**: Webhook endpoint is meant for internal validation by the background worker. Assume authorization is handled correctly.
- **Phased Delivery**: Fixing bug introduced in Phase 4 integration.
- **Observability**: Handlers log exceptions automatically, but handling concurrency avoids polluting logs.

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

### Source Code (repository root)
<!--
  ACTION REQUIRED: Replace the placeholder tree below with the concrete layout
  for this feature. Delete unused options and expand the chosen structure with
  real paths (e.g., apps/admin, packages/something). The delivered plan must
  not include Option labels.
-->

```text
backend/
├── src/
│   └── NaderGorge.Application/Features/Internal/Commands/
│       └── AiAnalysisCompletedCommand.cs
```

**Structure Decision**: The logic is confined strictly to the backend application features layer for handling webhooks.
directories captured above]

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| [e.g., 4th project] | [current need] | [why 3 projects insufficient] |
| [e.g., Repository pattern] | [specific problem] | [why direct DB access insufficient] |
