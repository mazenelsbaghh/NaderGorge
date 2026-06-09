# Implementation Plan: Call Center CRM and Student Follow-Up

**Branch**: `094-call-center-crm` | **Date**: 2026-06-09 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `specs/094-call-center-crm/spec.md`

## Summary

Implement a full Call Center CRM system that allows managers (Admins/Supervisors) to assign students to agents, set priority levels, and view performance reports. Agents (Assistants/Staff) can view their personal follow-up queue, normailze phone numbers, open WhatsApp chats, and record detailed call outcome logs with follow-up reminders.

## Technical Context

- **Language/Version**: C# (.NET 9) Backend, TypeScript (Next.js 16.2.1 / React 19) Frontend
- **Primary Dependencies**: MediatR, Entity Framework Core 9.0+, lucide-react, TailwindCSS
- **Storage**: PostgreSQL (new DB tables `CrmStudentStatuses` and `CrmCallLogs`)
- **Testing**: xUnit for C# backend application layer, Jest/Vitest for frontend helpers
- **Target Platform**: Web application (Desktop/Mobile responsive)
- **Project Type**: Web application
- **Performance Goals**: Under 200ms API response time for query lists, sub-second client state updates
- **Constraints**: Agents cannot view student details or logs for records not assigned to them

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Backend Layer Impact**: Creating CrmStudentStatus and CrmCallLog domain entities, MediatR handlers, and `CrmController`.
- **Frontend Layer Impact**: Creating `crm-service.ts`, reusable CRM views, and dashboard integration.
- **Database Layer Impact**: Scaffolding an EF Core migration `AddCrmEntities` to create the tables.
- **Automated Tests**: Creating `CrmSecurityTests.cs` and `CrmCallLogTests.cs` verifying access restrictions and state changes.
- **Docker Gate Command**: Ensure Docker builds and runs migrations correctly via `make up`.

## Project Structure

### Documentation

```text
specs/094-call-center-crm/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   └── crm-api.md       # Phase 1 API specifications
└── tasks.md             # Phase 2 output (to be created next)
```

### Source Code

```text
backend/
├── src/
│   ├── NaderGorge.Domain/
│   │   ├── Entities/
│   │   │   ├── CrmStudentStatus.cs
│   │   │   └── CrmCallLog.cs
│   │   └── Enums/
│   │       ├── CrmStatus.cs
│   │       ├── CrmPriority.cs
│   │       └── CallOutcome.cs
│   ├── NaderGorge.Application/
│   │   ├── Features/
│   │   │   └── CRM/
│   │   │       ├── Commands/
│   │   │       │   ├── AssignStudentToAgentCommand.cs
│   │   │       │   └── LogCrmCallCommand.cs
│   │   │       └── Queries/
│   │   │           ├── GetCrmStudentsQuery.cs
│   │   │           ├── GetCrmStudentHistoryQuery.cs
│   │   │           └── GetCrmPerformanceReportQuery.cs
│   └── NaderGorge.API/
│       └── Controllers/
│           └── CrmController.cs
└── tests/
    └── NaderGorge.Application.Tests/
        └── CRM/
            ├── CrmSecurityTests.cs
            └── CrmCallLogTests.cs

frontend/
├── src/
│   ├── services/
│   │   └── crm-service.ts
│   ├── app/
│   │   ├── admin/
│   │   │   └── crm/
│   │   │       └── page.tsx
│   │   └── assistant/
│   │       └── crm/
│   │           └── page.tsx
│   └── components/
│       └── crm/
│           ├── CrmDashboard.tsx
│           ├── CrmStudentQueue.tsx
│           ├── CrmCallLogModal.tsx
│           ├── CrmCallHistoryTimeline.tsx
│           └── CrmReportsPanel.tsx
```

**Structure Decision**: Option 2: Web Application (Backend & Frontend projects partitioned under `backend/` and `frontend/` folders).

## Phase Closure & Verification Plan

**Automated Tests Required**:
- Run `dotnet test backend/tests/NaderGorge.Application.Tests/ --filter "FullyQualifiedName~CRM"` to execute C# unit and integration tests.

**Docker Gate Required**:
- Run `dotnet ef migrations add AddCrmEntities --project src/NaderGorge.Infrastructure --startup-project src/NaderGorge.API` and verify migration file generates correctly.

**Manual QA Required**:
- **Admin Flow**: Access `/admin/crm`, see the list of all students, select a student, choose an agent in the dropdown, set priority to High, and save.
- **Agent Flow**: Access `/assistant/crm` as the assigned agent, verify the student appears in the queue, click WhatsApp action to open standard redirection link, click Log Call to add outcome "NoAnswer" and schedule next follow-up. Verify history updates.
- **Negative Check**: Access `/admin/crm` or `/assistant/crm` as a student role. Verify the system redirects to `/login` or shows an unauthorized card.
