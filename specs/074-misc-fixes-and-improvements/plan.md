# Implementation Plan: Miscellaneous Fixes and Improvements

**Branch**: `074-misc-fixes-and-improvements` | **Date**: 2026-06-04 | **Spec**: [spec.md](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/specs/074-misc-fixes-and-improvements/spec.md)
**Input**: Feature specification from `/specs/074-misc-fixes-and-improvements/spec.md`

## Summary

Implement 7 requested fixes and feature improvements:
1. **QR Code Base URL**: Support `NEXT_PUBLIC_APP_URL` in `QrDisplay.tsx` to generate correct QR URLs for scanning on mobile devices.
2. **Student Package Cancellation**: Create a command `CancelPackageGrantCommand` to allow admins to cancel package access grants and optionally refund the package price to the student's balance. Modify the student profile page in the admin view to show a cancel button with warnings and a refund modal.
3. **Disabling Reason**: Store `SuspensionReason` on the `User` entity, record it when toggling student status, and return a clean translated Arabic error message during login containing the reason and support number.
4. **Sidebar Hover Labels**: Add smooth CSS transitions to expand the desktop sidebar on hover and show item labels next to the icons.
5. **Logged-In Users Login Redirect**: Add check inside `/login` to redirect authenticated users to their dashboard automatically.
6. **Balance Edit Button Layout**: Place the edit balance button as a child of `AdminStatCard` in the financials tab to align the edit icon and text horizontally.
7. **Rate Limit Increase**: Increase limits inside `RateLimitingConfig.cs` to double the previous thresholds and prevent standard page actions from hitting 429 errors.

## Technical Context

**Language/Version**: C# 13 / .NET 9, TypeScript 5.x / Next.js 16.2.1 / React 19  
**Primary Dependencies**: EF Core 9.0, MediatR 12.4.1, Tailwind CSS 4, Axios, Zustand  
**Storage**: PostgreSQL (Users, StudentAccessGrants, StudentBalances, BalanceTransactions)  
**Testing**: Build and compile validation, manual verification of UI flows  
**Target Platform**: Docker Stack deployment  
**Project Type**: Multi-project Web Application  

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- All backend changes follow the strict 4-project modular layer architecture: Entities defined in `Domain`, Query/Command logic in `Application` (via MediatR handlers), API route handlers in `API`, database access in `Infrastructure`.
- Frontend service layer isolation is respected: API calls are added to `admin-service.ts` rather than directly in components.
- Audit logging is integrated for the new `CancelPackageGrantCommand` action.
- Arabic-first RTL design guidelines are strictly followed for the new UI elements.

## Project Structure

### Documentation (this feature)

```text
specs/074-misc-fixes-and-improvements/
├── spec.md              # Feature specification
├── plan.md              # This file
├── research.md          # Technical research notes
├── data-model.md        # Database schema modifications
├── quickstart.md        # Quickstart and verification steps
├── checklists/
│   └── requirements.md  # Requirements completeness checklist
└── contracts/           # API and data contracts
```

### Source Code Modifications

```text
backend/
├── src/
│   ├── NaderGorge.Domain/
│   │   └── Entities/
│   │       └── User.cs [MODIFY]
│   ├── NaderGorge.Application/
│   │   └── Features/
│   │       ├── Admin/
│   │       │   ├── Commands/
│   │       │   │   ├── ToggleStudentSystemAccessCommand.cs [MODIFY]
│   │       │   │   └── CancelPackageGrantCommand.cs [NEW]
│   │       │   └── Queries/
│   │       │       ├── StudentProfileExtendedDto.cs [MODIFY]
│   │       │       └── GetStudentProfileDetailQuery.cs [MODIFY]
│   │       └── Auth/
│   │           └── Commands/
│   │               └── LoginCommand.cs [MODIFY]
│   ├── NaderGorge.API/
│   │   └── Controllers/
│   │       └── AdminController.cs [MODIFY]
│   │   └── Configuration/
│   │       └── RateLimitingConfig.cs [MODIFY]
│   └── NaderGorge.Infrastructure/
│       └── Migrations/
│           └── [TIMESTAMP]_AddSuspensionReasonToUser.cs [NEW]

frontend/
├── src/
│   ├── app/
│   │   ├── (public)/
│   │   │   └── login/
│   │   │       └── page.tsx [MODIFY]
│   │   └── admin/
│   │       └── users/
│   │           └── [id]/
│   │               └── page.tsx [MODIFY]
│   ├── components/
│   │   ├── admin/
│   │   │   └── AdminShellChrome.tsx [MODIFY]
│   │   ├── layout/
│   │   │   └── StudentShellChrome.tsx [MODIFY]
│   │   └── codes/
│   │       └── QrDisplay.tsx [MODIFY]
│   └── services/
│       └── admin-service.ts [MODIFY]
```

## Structure Decision

Using the standard dual-project directory structure (`backend/` and `frontend/`).
- Backend: MediatR commands/handlers, Domain entities, and REST API controllers.
- Frontend: Next.js pages, layouts, Tailwind CSS styling, and client service integrations.
