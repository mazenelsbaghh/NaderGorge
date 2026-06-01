# Implementation Plan: Google Drive Video Provider

**Branch**: `046-google-drive-provider` | **Date**: 2026-04-02 | **Spec**: [spec.md](../spec.md)
**Input**: Feature specification from `/specs/046-google-drive-provider/spec.md`

## Summary

This feature adds Google Drive as a supported video provider. Admins can upload videos to Google Drive, share them ("Anyone with the link - Viewer"), and paste the Drive URL or File ID into the lesson admin panel. The system will store the `ProviderVideoId` securely. Students will access the video via a Shadow DOM iframe in our player, displaying the Google Drive preview player, preventing direct extraction of the Google Drive underlying ID.

## Technical Context

**Language/Version**: C# 13, .NET 9 (Backend) | TypeScript, Next.js 16.2.1, React 19 (Frontend)
**Primary Dependencies**: EF Core 9.0, Next.js App Router API
**Storage**: PostgreSQL (LessonVideo entity)
**Testing**: Jest (Frontend), xUnit (Backend) - N/A for this simple proxy feature
**Target Platform**: Web Browsers (Chrome, Safari, Firefox)
**Project Type**: Web Application
**Performance Goals**: Embed loads under 5 seconds
**Constraints**: Student browser MUST NOT see the raw Google Drive File ID.
**Scale/Scope**: Scales with Google Drive bandwidth.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] **Modular Clean Architecture**: Provider logic encapsulated behind `VideoProviderAbstraction` (equivalent) or at least separated branching per provider.
- [x] **Provider Abstraction First**: Already established provider abstraction (youtube, telegram). We are just adding `google_drive`.
- [x] **Security & Access Control by Default**: Reuses AES-GCM encrypted tokens for session control. File ID hidden from client.
- [x] **Frontend Reliability & Rendering Strictness**: Validating Google Drive URLs correctly in frontend.

## Project Structure

### Documentation (this feature)

```text
specs/046-google-drive-provider/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
# Web application 
backend/
├── src/
│   ├── NaderGorge.Application/
│   └── NaderGorge.Domain/

frontend/
├── src/
│   ├── app/admin/
│   ├── app/api/video/embed/
│   └── components/
```

**Structure Decision**: Web application structure with updates to both Frontend (Admin UI, Embed Route) and Backend API (DTO validation).

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| N/A       | N/A        | N/A |
