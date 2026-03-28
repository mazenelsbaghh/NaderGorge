# Implementation Plan: Exam Dashboard & Timers

**Branch**: `023-exam-dashboard-timers` | **Date**: 2026-03-28 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/023-exam-dashboard-timers/spec.md`

## Summary

Implement an Admin Exam Dashboard showing an overview of exam statistics and a complete list of student submissions, alongside strict server-side timers for both the overall exam and individual questions, ensuring resilience against page refreshes and preventing cheating.

## Technical Context

**Language/Version**: C# (.NET 8), TypeScript (Next.js 14)
**Primary Dependencies**: EF Core, React Query, Zustand
**Storage**: PostgreSQL
**Testing**: Jest, xUnit (Backend)
**Target Platform**: Web browsers (Mobile & Desktop)
**Project Type**: Full-stack Web Application
**Performance Goals**: < 500ms p95 for dashboard queries
**Constraints**: Absolute server-side start time records for timers to prevent local client manipulation
**Scale/Scope**: System must handle thousands of concurrent students taking exams and enforce correct timestamps.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] Modular Architecture: Exam feature is tightly contained within Content/Assessment modules.
- [x] Security: Relying strictly on server-side `StartedAt` rather than trusting client-side JS clocks.
- [x] UX Simplicity: The admin dashboard consolidates data cleanly instead of spreading it across multiple pages.

## Project Structure

### Documentation (this feature)

```text
specs/023-exam-dashboard-timers/
├── plan.md              
├── research.md          
├── data-model.md        
├── quickstart.md        
└── contracts/           
```

### Source Code (repository root)

```text
backend/
├── src/
│   ├── NaderGorge.Domain/
│   ├── NaderGorge.Application/
│   └── NaderGorge.API/

frontend/
├── src/
│   ├── components/admin/
│   ├── app/admin/
│   └── services/
```

**Structure Decision**: Standard full-stack web application structure separating .NET backend from Next.js frontend frontend.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| N/A | N/A | N/A |
