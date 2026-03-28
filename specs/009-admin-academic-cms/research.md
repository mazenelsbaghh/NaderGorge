# Research: Phase 2.5: Admin CMS for Homework and Assistants

## Overview
This document contains the researched decisions and technical direction for the feature: **Phase 2.5: Admin CMS for Homework and Assistants**. 

## Technical Decisions

### Decision 1: Admin User Management Role Modifications
- **Decision**: Update the existing `AdminController` user endpoints or add an Admin update role endpoint. The frontend User Management page (`/admin/users`) will have an inline dropdown or action menu button to edit user roles.
- **Rationale**: Phase 1 already created a base User Management table. We want to avoid creating an entirely new entity simply for roles, since Identity Framework handles it via `UserManager<ApplicationUser>`.
- **Alternatives considered**: Creating a separate "Assistant Registration" page. Rejected because a unified view reduces admin cognitive load.

### Decision 2: Admin Homework Add/Edit UI location
- **Decision**: Embed the Homework management forms within the Lesson Detail page (`/admin/content/packages/[packageId]/sections/[sectionId]/lessons/[lessonId]`), utilizing a tabbed interface (Video / Short Questions / Homework / Quizzes).
- **Rationale**: Keeps related content strongly grouped. The `Homework` entity logically belongs to the `Lesson`, and giving it a separate abstract list detaches it from the curriculum flow.
- **Alternatives considered**: A global "Homework Bank" page where an admin defines a homework, then attaches it to a lesson. Rejected for now to keep the MVP simple.

### Decision 3: Parent Report Link Generation
- **Decision**: A simple "Copy" icon button appended to each row in the student User Management table. The frontend uses `navigator.clipboard.writeText` to construct `https://domain.com/parent-report/{userId}`.
- **Rationale**: Zero-backend effort, highly functional feature that achieves exactly what the user needs (quickly copying a shareable link).
- **Alternatives considered**: Sending the link via automated SMS directly from the admin dashboard. Rejected to keep the scope bounded to CMS link generation.

## Dependencies & Integrations

- **ASP.NET Core Identity**: Leveraging user claims/roles to escalate a `Student` to an `Assistant` (or creating the assistant directly).
- **Shadcn/UI components**: Using existing `Tabs`, `Dialog` (for creating users/homework), `DropdownMenu` for actions.
- **Clipboard API**: Standard Web API for copying text securely without packages like `react-copy-to-clipboard`.
