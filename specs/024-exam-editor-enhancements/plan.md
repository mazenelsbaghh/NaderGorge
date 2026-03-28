# Implementation Plan: [FEATURE]

**Branch**: `[###-feature-name]` | **Date**: [DATE] | **Spec**: [link]
**Input**: Feature specification from `/specs/[###-feature-name]/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

## Summary

Move the "Time per question" limit from individual `QuestionbankItem` settings to a global `Exam` setting to optimize the Admin workflow. Additionally, swap the basic textarea in the `QuestionEditor` with a Rich Text Editor (`react-quill`) so question text can support colors and font formatting.

## Technical Context

**Language/Version**: C# (.NET 8.0) Backend, TypeScript (Next.js 14) Frontend
**Primary Dependencies**: `react-quill`, Entity Framework Core
**Storage**: PostgreSQL
**Testing**: EF Core migration tests, UI manual checks
**Target Platform**: Admin Web Interface
**Project Type**: Next.js Fullstack Monorepo Application
**Performance Goals**: N/A
**Constraints**: Minimal overhead for rendering rich text; secure sanitization of output
**Scale/Scope**: Impacts all future exams created. Existing questions rely on rendering HTML if supported.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **IX. Assessment & Time Integrity**: Check successful. Storing timestamps at the Exam level reinforces server-side truth processing. The backend commands must process the new `TimePerQuestionSeconds` attribute.
- **VIII. Premium Editorial Design System**: The Rich text editor must be styled appropriately to match the `AdminShellChrome` standards without appearing disjointed or overly aggressive. 
- **IV. Phased Delivery**: Modifying the EF Core schema incrementally via structured migrations ensures stable delivery without breaking existing records.

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
```text
# Web application 
backend/
├── src/
│   ├── NaderGorge.Domain/
│   └── NaderGorge.Application/
└── tests/

frontend/
├── src/
│   ├── components/admin/
│   └── app/admin/
└── tests/
```

**Structure Decision**: Selected the Monorepo Web Application structure containing `.NET` Web API backend and `Next.js` frontend. All changes are concentrated in the admin components and EF Core domain entities.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| [e.g., 4th project] | [current need] | [why 3 projects insufficient] |
| [e.g., Repository pattern] | [specific problem] | [why direct DB access insufficient] |
