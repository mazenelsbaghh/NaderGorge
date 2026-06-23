# Implementation Plan: AI Live Support Actions & Verification

**Branch**: `145-ai-live-support-actions` | **Date**: 2026-06-23 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `specs/145-ai-live-support-actions/spec.md`

## Summary

This plan details the implementation of confirmed actions, guest verification, secure registration, and confirmed human handoff for the AI Live Support Agent. We will build dynamic student context querying and action instruction injection on the backend, update the Node.js worker to support the full JSON schema of 6 decision types, and develop the interactive cards and secure forms in the student widget UI.

## Technical Context

- **Language/Version**: C# 13 (.NET 9), TypeScript (Next.js 16.2.1 / React 19), Node.js v20
- **Primary Dependencies**: Entity Framework Core 9.0.6, BullMQ, Axios, Tailwind CSS, SignalR
- **Storage**: PostgreSQL (existing tables: `LiveSupportAIPendingActions`, `LiveSupportAIVerificationSessions`, `LiveSupportMessages`, etc.)
- **Testing**: xUnit (`dotnet test`), Node built-in test runner
- **Target Platform**: Linux Docker / Local development
- **Performance Goals**: Action execution response < 1.5s, UI updates < 500ms
- **Constraints**: Secure fields (passwords) must never be sent to the AI worker or saved in message history.

## Constitution Check

- **Layer impact**:
  - Backend: `LiveSupportService.cs`, `LiveSupportParticipantController.cs`, DTOs, SignalR Hub.
  - Worker: `geminiService.ts`, decision schema validator.
  - Frontend: `live-support-service.ts`, `ParticipantConversation.tsx`, new card components.
- **Automated tests**:
  - Unit tests in `NaderGorge.Application.Tests` to verify action proposals, expiry, and execution.
  - Integration tests in `NaderGorge.Integration.Tests` to assert concurrency safety.
- **Manual QA flows**:
  - Admin tethers allowed actions → student requests lesson unlock → confirms card → lesson unlocks.
  - Student asks for human → confirms handoff card → transitions to human.
  - Guest requests account creation → fills secure registration form → account is created and linked.
- **Docker gate commands**:
  - `docker compose config -q`, `make up`.

## Phase 0: Research Decisions

- We conducted detailed research of the catalogs and schema designs in [research.md](./research.md).
- We resolved all technical questions including action schema layouts, worker validation, context packaging, and security constraints.

## Phase 1: Design & Contracts

- The database layout and entities are mapped in [data-model.md](./data-model.md).
- The REST API contracts are documented in [contracts/api.md](./contracts/api.md).
- Verification and local setup guides are in [quickstart.md](./quickstart.md).

## Project Structure

### Documentation

```text
specs/145-ai-live-support-actions/
├── plan.md              # This file
├── research.md          # Technical analysis
├── data-model.md        # Database entities and enums mapping
├── quickstart.md        # Verification guide
└── contracts/
    └── api.md           # API endpoints contracts
```

### Source Code

```text
backend/src/
├── NaderGorge.API/Controllers/LiveSupportParticipantController.cs
├── NaderGorge.Application/Features/LiveSupport/
│   ├── Dtos/LiveSupportDtos.cs
│   └── Interfaces/ILiveSupportService.cs
└── NaderGorge.Infrastructure/Services/LiveSupportService.cs

worker/src/
└── services/geminiService.ts

frontend/src/
├── components/live-support/participant/
│   ├── AIPendingActionCard.tsx     # Card for confirming actions
│   ├── AIHandoffConfirmation.tsx   # Card for confirming human handoffs
│   ├── AISecureRegistrationForm.tsx # Form for guest registration
│   └── AIGuestVerification.tsx     # Inputs for verification challenges
├── services/live-support-service.ts
└── components/live-support/ParticipantConversation.tsx
```

**Structure Decision**: Standard multi-project structure. All API endpoints and business logic are added to C# backend. Gemini schemas and prompts are updated in the Node worker. New UI cards are created in the student live support widget.

## Phase Closure & Verification Plan

**Automated Tests Required**:
- `dotnet test backend/tests/NaderGorge.Application.Tests/LiveSupport/ParticipantSessionTests.cs`
- Run custom tests verifying action confirmation, expiry, and handoff cancels.

**Docker Gate Required**:
- `docker compose config -q && make up && docker compose ps`

**Manual QA Required**:
- **Admin**: Configure allowed actions in `/admin/live-support/ai`.
- **Student**: Ask AI to unlock lesson. Confirm proposal. Verify lesson is open.
- **Guest**: Ask AI to register account. Fill secure registration form. Verify account is created.
