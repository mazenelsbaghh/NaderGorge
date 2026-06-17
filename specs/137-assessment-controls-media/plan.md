# Implementation Plan: Assessment Controls And Question Media

**Branch**: `137-assessment-controls-media` | **Date**: 2026-06-17 | **Spec**: [spec.md](./spec.md)  
**Input**: Feature specification from `/specs/137-assessment-controls-media/spec.md`

## Summary

Make assessment mandatory behavior explicit and reliable across homework, lesson exams, and video part exams; add optional question image uploads to the shared admin question editor; and normalize rich-text question display so raw paragraph tags do not appear to students.

## Technical Context

**Language/Version**: C# 13 / .NET 9 backend, TypeScript 5.x / Next.js 16.2.7 / React 19 frontend  
**Primary Dependencies**: EF Core, MediatR, Next.js App Router, Axios service layer, Tailwind CSS, lucide-react, existing assets-domain upload handling  
**Storage**: PostgreSQL for question image URL fields, existing `IContentImageStorage`/assets-domain upload pipeline for image files  
**Testing**: `dotnet test`, `npm run lint`, focused UI/manual checks  
**Target Platform**: Web, RTL Arabic-first admin and student surfaces  
**Project Type**: Full-stack web application  
**Performance Goals**: No extra blocking request on student attempt render beyond image asset fetch; upload validates client-side before network call  
**Constraints**: Preserve current content hierarchy and existing mandatory defaults; no unsafe HTML rendering; do not introduce new media service unless existing upload path is insufficient  
**Scale/Scope**: Existing assessment creation and display surfaces only

## Constitution Check

- **Modular Clean Architecture**: Pass. Backend changes stay in Domain/Application/API/Infrastructure boundaries.
- **Security & Access Control by Default**: Pass. Upload endpoint uses existing authenticated admin controller and validates image files.
- **Academic Content Integrity**: Pass. Images are tied to teacher-created assessment questions only.
- **UX Simplicity**: Pass. Mandatory controls are explicit toggles with clear copy; image upload is optional and previewable.
- **Phase Verification & Docker Gates**: Pass with local lint/test and documented manual Docker acceptance.

## Design Guidance Applied

- Register: product UI for Arabic educational admin and student workflows.
- Use Massar design tokens: navy for authority, teal for interaction/progress, warm gold only for achievement.
- Use lucide icons for upload/remove actions.
- Keep question image upload as a clear inline field inside the existing editor, not a modal.
- Ensure controls are at least 44px high, keyboard focusable, and responsive.
- Use `next/image` for rendering question images in frontend surfaces where practical.

## Phase 0: Research

See [research.md](./research.md).

## Phase 1: Design & Contracts

See [data-model.md](./data-model.md) and [contracts/assessment-question-media.md](./contracts/assessment-question-media.md).

## Implementation Approach

1. Extend question data with `ImageUrl` in exam and homework entities and DTOs.
2. Add a backend migration for optional image URL columns.
3. Add admin question image upload endpoint reusing `IContentImageStorage` so returned relative URLs resolve to `https://assets.massar-academy.net` in production.
4. Extend admin service and shared question editor with image upload preview/remove.
5. Propagate `imageUrl` through exam/homework create, add, update, attempt, and result payloads.
6. Add shared frontend helper for question text display and update exam/homework renderers.
7. Verify mandatory toggles are sent for homework, lesson exams, and video exams.

## Post-Design Constitution Check

No violations. The plan remains scoped to assessment content and follows existing service/controller patterns.
