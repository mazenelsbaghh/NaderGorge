# Implementation Plan: fix-forms-id-mismatch

**Branch**: `087-fix-forms-id-mismatch` | **Date**: 2026-06-07 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/087-fix-forms-id-mismatch/spec.md`

## Summary
The goal is to resolve the `400 Bad Request` returned when trying to update custom forms or toggle their status. We will fix this by extending the Axios PUT requests inside the frontend `forms-service.ts` file to pass `id` and `submissionId` respectively in the payload bodies to match the backend's validation check.

## Technical Context

- **Language/Version**: Next.js 16.2.1, TypeScript 5.x, .NET 9, C# 13
- **Primary Dependencies**: Axios (API client), MediatR, FluentValidation
- **Storage**: PostgreSQL (CustomForms and FormSubmissions tables)
- **Testing**: Manual verification + compiler validation
- **Target Platform**: Docker-compose production environment / Local dev
- **Project Type**: Web Application (Frontend + Backend)
- **Performance Goals**: Immediate API response
- **Constraints**: Secure API validation must pass (path parameter must equal body parameter)
- **Scale/Scope**: Trivial frontend payload correction

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Modular Clean Architecture**: We are using the centralized service layer file `forms-service.ts` to perform all API requests. Components do not run raw Axios commands directly. **(PASSED)**
- **Security & Access Control**: The backend requires path ID to match body ID to prevent parameter tampering. The frontend will now correctly supply this information. **(PASSED)**
- **Frontend Reliability**: Full TypeScript verification of the modified service layer. **(PASSED)**

## Project Structure

### Documentation (this feature)

```text
specs/087-fix-forms-id-mismatch/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output (empty/reference only)
├── quickstart.md        # Verification details
├── contracts/
│   └── api.md           # API request contracts
└── tasks.md             # Phase 2 output (to be generated)
```

### Source Code (repository root)

```text
frontend/
└── src/
    └── services/
        └── forms-service.ts  # Target file for modification
```

**Structure Decision**: Option 2 (Web application structure). The only file needing modification is `frontend/src/services/forms-service.ts`.
