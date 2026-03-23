# Feature Specification: Phase 0 — Discovery, Planning, and Product Blueprint

**Feature Branch**: `001-phase0-discovery-blueprint`
**Created**: 2026-03-19
**Status**: Draft
**Input**: User description: "Phase 0 — Discovery, Planning, and Product Blueprint"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Define Product Identity and Academic Structure (Priority: P1)

As the **project owner and teacher (Nader George)**, I want the entire platform scope, academic structure, and brand identity clearly documented before any code is written, so that all stakeholders share one unified understanding of what the platform is and what it delivers.

This includes:
- Defining the platform as a full academic control system (not just a content site)
- Documenting the target audience segments: First Secondary, Second Secondary, First Baccalaureate, Second Baccalaureate, parents, assistants, and admin
- Defining the teacher's brand style: youthful, simple, energetic, story-based explanation, maps/tables, motivating
- Documenting the full content hierarchy: Package → Content Section → Lesson → Video/Summary/Quiz/Homework/Resources/MindMap/Revision

**Why this priority**: Without a locked product definition, the entire project risks scope creep, misalignment, and wasted development effort. Everything else depends on this.

**Independent Test**: Can be validated by reviewing the Product Requirements Document and confirming all audience segments, content types, and brand guidelines are explicitly documented with no ambiguity.

**Acceptance Scenarios**:

1. **Given** the project has started, **When** a new team member reads the Product Requirements Document, **Then** they can clearly explain what the platform does, who it serves, and what makes it different from a generic course-selling website.
2. **Given** the academic structure is documented, **When** compared against the teacher's actual teaching flow (Exam → Lesson → Homework), **Then** every element of that flow is accounted for in the content hierarchy.
3. **Given** the target audience is defined, **When** listing all user types, **Then** each user type (student by grade, parent, assistant, admin, teacher) has a clear description of their role and expected platform interactions.

---

### User Story 2 - Define Access Model and Code System Blueprint (Priority: P1)

As the **project owner**, I want the complete code-based access model documented — including code types, activation rules, stacking behavior, expiration logic, and confirmation flows — so that the monetization and content-gating strategy is locked before development.

This includes documenting:
- Code types: lesson code, package code, term code, promotional code (later), referral code (later)
- Code behaviors: single-use, code groups/batches, content-based and/or duration-based logic, activation confirmation, selected activation date, pre-usage expiration, type-based stacking rules
- All student data to be collected: full name, phone, parent phone, grade, study track, governorate, city/district, school, engagement data, package history, code history

**Why this priority**: The code system is the primary revenue engine. If designed poorly, the entire monetization layer becomes unreliable (explicitly called out as a major risk in the plan).

**Independent Test**: Can be validated by walking through 5 representative code activation scenarios (lesson code, package code, term code, stacked codes, expired code) and confirming each scenario has a defined outcome.

**Acceptance Scenarios**:

1. **Given** a code type matrix is documented, **When** reviewing each code type, **Then** it has clearly defined: scope (what it unlocks), duration (how long access lasts), stacking rules (what it can combine with), and expiration rules.
2. **Given** the data blueprint is documented, **When** listing all required student fields, **Then** all fields from the plan (name, phone, parent phone, grade, track, governorate, city/district, school, engagement, package history, code history) are present.
3. **Given** the activation flow is documented, **When** a student activates a code, **Then** the expected flow includes: code entry → validation → confirmation screen → activation with selected date → access grant.

---

### User Story 3 - Define Technical Architecture and System Blueprint (Priority: P2)

As the **technical lead**, I want the full technical architecture document finalized — including frontend stack, backend stack, database design, caching strategy, background jobs architecture, and deployment structure — so that development can begin with clear technical constraints.

This includes:
- Frontend: Next.js, TypeScript, Tailwind CSS, React Query, Zustand, Shadcn/UI, Framer Motion
- Backend: .NET Web API, C#, Clean Architecture, CQRS (where useful), Entity Framework Core, PostgreSQL
- Cache/speed: Redis for caching, rate limiting, OTP storage, leaderboard, sessions, notification buffering
- Background jobs: BullMQ (Node.js worker) + Redis broker + .NET API writes jobs
- Video: YouTube embedded initially, behind a Video Provider Abstraction Layer
- Deployment: Frontend app, Backend API, Node worker, PostgreSQL, Redis, reverse proxy, monitoring
- Environments: Development, Staging, Production

**Why this priority**: The architecture must be locked before any coding begins to prevent ad-hoc technology decisions. However, this depends on the product definition (US1) being complete first.

**Independent Test**: Can be validated by having a developer review the Technical Architecture Document and confirming they can set up the repository structure, install dependencies, and understand the service boundaries without additional questions.

**Acceptance Scenarios**:

1. **Given** the architecture document is complete, **When** listing all services, **Then** each service (frontend, backend API, Node worker, database, cache, proxy) has a defined technology, purpose, and communication pattern.
2. **Given** the database domain is documented, **When** reviewing the data model draft, **Then** all major entity groups are present: users/roles, students, parent contacts, programs/packages, lessons/videos, exams/questions, homework, code groups/activations, tracking events, notifications, gamification, AI outputs, audit logs.
3. **Given** the hybrid job architecture is documented, **When** tracing a background job flow (e.g., send SMS notification), **Then** the flow clearly shows: .NET API writes event → Redis stores job → Node worker processes via BullMQ.

---

### User Story 4 - Define User Roles and Permissions Matrix (Priority: P2)

As the **project owner**, I want all user roles, their permissions, and their expected platform interactions documented, so that the authorization system can be designed correctly from Phase 1.

Roles to document:
- **Student**: dashboard, lesson access, exam solving, homework, progress tracking, ranking, notifications
- **Parent**: student progress visibility, warning summaries, grade summaries, homework tracking
- **Teacher (Nader George)**: content oversight, student analytics, performance review, package planning, exam visibility
- **Assistant** (sub-roles): academic assistant, homework reviewer, follow-up assistant, support assistant — each with different permission scopes
- **Admin**: full system management (users, packages, lessons, codes, questions/exams, analytics, settings, logs, assistant permissions)

Role assignment model: **multi-role** — a single user can hold more than one role simultaneously (e.g., Nader George holds both Teacher and Admin). Roles remain separate in the permission model to support future team scaling.

**Why this priority**: Roles directly impact the authorization layer and UI routing. Defining them clearly prevents permission gaps and security issues.

**Independent Test**: Can be validated by taking each role and listing every action they should be able to perform, confirming no action is undefined and no role has contradictory permissions.

**Acceptance Scenarios**:

1. **Given** the User Roles Matrix is complete, **When** checking any platform feature (e.g., "review homework"), **Then** exactly which roles can perform it is clearly stated.
2. **Given** the assistant sub-roles are documented, **When** comparing two assistant types (e.g., homework reviewer vs. follow-up assistant), **Then** their permissions do not overlap in ways that create confusion.
3. **Given** the teacher role is documented, **When** listing teacher capabilities, **Then** the teacher has oversight and analytics access but the admin handles operational management (creating codes, editing content structure).

---

### User Story 5 - Define UX Direction and Initial Wireframe Strategy (Priority: P3)

As the **project owner**, I want the platform's UX philosophy documented — including visual direction, navigation principles, and initial wireframe approach — so that the UI development has clear guidance from the start.

UX principles to document:
- Simple, organized, motivating, clear study path
- Controlled but not oppressive
- Student always knows where they are and what comes next
- Two-step registration to reduce drop-off
- Dashboard-first experience after login

**Why this priority**: UX direction matters, but wireframes can evolve during Phase 1. The core principles and navigation philosophy are more critical than pixel-perfect designs at this stage.

**Independent Test**: Can be validated by presenting the UX direction document to a non-technical stakeholder (e.g., the teacher) and confirming they agree it represents the intended student experience.

**Acceptance Scenarios**:

1. **Given** the UX direction is documented, **When** reviewing registration flow, **Then** it explicitly shows the two-step approach: Step 1 (name, phone, grade, track) → Step 2 (parent phone, governorate, city, school).
2. **Given** the sitemap is created, **When** tracing a student's journey from login to completing a lesson, **Then** the path is linear, clear, and requires no more than 3 clicks from dashboard to lesson content.
3. **Given** the UX philosophy is documented, **When** a designer reads it, **Then** they can identify at least 5 concrete design principles to follow (e.g., "no overwhelming options on a single screen", "always show progress", "motivating language, not punitive").

---

### User Story 6 - Define AI Scope Boundaries (Priority: P3)

As the **project owner**, I want the AI features scope clearly defined and bounded, so that AI development in later phases stays within approved academic content and does not become an unbounded generic assistant.

AI scope for initial wave:
- Question generation from lesson content and teacher notes
- Basic weak-point analysis (weak lessons, repeated mistakes, low-performing areas)
- Essay correction support (model answer comparison, suggested score, feedback)
- Controlled lesson assistant (answers only from approved platform content)

Explicit boundaries:
- No open-web generic assistant behavior
- AI MUST stay tied to teacher-approved academic content
- All AI outputs are suggestions, not final decisions

**Why this priority**: AI is a Phase 4 feature, but its boundaries must be documented now to prevent scope creep during earlier phases. A clear boundary prevents the team from making AI-friendly but over-engineered data models too early.

**Independent Test**: Can be validated by listing each planned AI feature and confirming it has: a defined input source (teacher content only), a defined output format, and an explicit boundary statement.

**Acceptance Scenarios**:

1. **Given** the AI scope is documented, **When** a developer proposes an AI feature, **Then** they can check it against the boundary document and determine if it's in-scope or out-of-scope within 1 minute.
2. **Given** the controlled assistant boundary is documented, **When** describing what the assistant can NOT do, **Then** the document explicitly states: no open-web browsing, no answers from non-platform content, no direct academic grading (only suggestions).

---

### Edge Cases

- What happens if stakeholders disagree on the content hierarchy structure (e.g., whether "months" are content groups or time-based)?
  → The plan explicitly defines "months" as content bundles, not calendar months. This must be documented unambiguously.
- What happens if the teacher wants to change the brand identity mid-development?
  → The Product Requirements Document should include a versioning strategy for brand guidelines, with a change approval process.
- What happens if a new user role is identified during Phase 1 development?
  → The User Roles Matrix must include a "role extension" procedure that requires updating the matrix before implementing the new role.
- What happens if the code system rules conflict with each other (e.g., a term code and a lesson code unlocking the same content)?
  → The Business Rules Document must include a conflict resolution matrix for code type interactions.

## Clarifications

### Session 2026-03-21

- Q: What format should the 8 Phase 0 deliverables be produced in? → A: All deliverables as Markdown files in the repository (version-controlled, PR-reviewable) under `specs/001-phase0-discovery-blueprint/`.
- Q: What is the canonical term for the grouping layer between Package and Lesson? → A: "Content Section" is the canonical term for all contexts (API, UI, data models, documentation). The legacy term "months" may appear only as an internal alias in documentation with the note "(internally referred to as 'months')" — it MUST NOT appear in API names, UI labels, or data model identifiers.
- Q: Should Teacher and Admin be separate roles or merged, given Nader George is both? → A: Separate roles with multi-role assignment. A single user (e.g., Nader George) can hold both Teacher and Admin roles simultaneously. This keeps the permission model clean for future team scaling.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The discovery process MUST produce a Product Requirements Document (as a Markdown file in the repository) that defines the platform as a full academic control system, not a content-selling website.
- **FR-002**: The Product Requirements Document MUST enumerate all target audience segments: First Secondary, Second Secondary, First Baccalaureate, Second Baccalaureate, parents, assistants, admin.
- **FR-003**: The Content Blueprint MUST define the complete content hierarchy: Package → Content Section → Lesson → Video/Summary/Quiz/Homework/Resources/MindMap/Revision. The term "Content Section" is canonical; the internal alias "months" MUST NOT appear in API, UI, or data model identifiers.
- **FR-004**: The Access Blueprint MUST document all code types (lesson, package, term, promotional, referral) with their activation rules, stacking behavior, expiration logic, and confirmation flow.
- **FR-005**: The Data Blueprint MUST list all required student data fields: full name, phone number, parent number, grade, study track, governorate, city/district, school, engagement data, package history, code history.
- **FR-006**: The Technical Architecture Document MUST specify the complete technology stack: Next.js frontend, .NET backend, PostgreSQL, Redis, BullMQ worker, and all supporting services.
- **FR-007**: The Technical Architecture Document MUST define the Video Provider Abstraction Layer supporting: provider type, external video ID, title, duration, order, watch limits, replay limits, and provider metadata.
- **FR-008**: The User Roles Matrix MUST define permissions for all roles: Student, Parent, Teacher, Assistant (with sub-roles: academic, homework reviewer, follow-up, support), and Admin. The authorization model MUST support multi-role assignment (a single user can hold multiple roles simultaneously).
- **FR-009**: The System Blueprint MUST define the deployment structure: frontend app, backend API, Node worker service, PostgreSQL, Redis, reverse proxy/gateway, and monitoring.
- **FR-010**: The System Blueprint MUST require at least three environments: Development, Staging, and Production.
- **FR-011**: The Sitemap MUST cover all major platform sections: public website, student portal, parent layer, teacher panel, assistant panel, and admin panel.
- **FR-012**: The UX Direction document MUST define the two-step registration flow with specific fields per step.
- **FR-013**: The Business Rules Document MUST include watch control parameters: max watch minutes, max replay count, allowed playback speeds, partial skip policy, and completion threshold.
- **FR-014**: The AI Scope Definition MUST explicitly bound AI features to approved academic content and teacher-uploaded material only.
- **FR-015**: The Data Model Draft MUST include all recommended system entities from the plan across all domains: User/Identity, Student, Content, Assessment, Access, Tracking, and AI.

### Key Entities

- **Product Requirements Document (PRD)**: The master document defining what the platform is, who it serves, what business model it uses, and what makes it unique. Contains audience segments, content hierarchy, and value proposition.
- **System Blueprint**: The technical architecture overview showing all services, their technologies, communication patterns, and deployment structure.
- **User Roles Matrix**: A cross-reference table mapping every role to every platform feature, indicating permission level (full access, read-only, no access).
- **Content Blueprint**: The hierarchical definition of how academic content is organized, from package level down to individual video segments and resources.
- **Access Blueprint (Code System)**: The complete definition of the code-based access model including types, rules, stacking, expiration, and activation flows.
- **Data Blueprint**: The definition of all data to be collected about students, including personal info, academic context, and engagement metrics.
- **Business Rules Document**: The collection of all business logic rules including watch control, exam logic, homework requirements, gamification principles, and student behavior classification.
- **Sitemap**: The visual representation of all platform pages and their navigation relationships across all user roles.
- **Data Model Draft**: The initial entity-relationship design covering all data domains without implementation-specific details.
- **Initial UI Wireframe Direction**: Low-fidelity wireframes or style guides establishing the visual language, layout patterns, and interaction principles.
- **Technical Architecture Document**: Detailed per-service breakdown with technology choices, integration patterns, and infrastructure requirements.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All 8 deliverables (PRD, System Blueprint, User Roles Matrix, Sitemap, Data Model Draft, Business Rules Document, Initial UI Wireframe Direction, Technical Architecture Document) are produced as Markdown files in the repository and approved by the project owner.
- **SC-002**: A new team member can read the Phase 0 deliverables and explain the platform's purpose, audience, content structure, and technical architecture within 30 minutes, without needing verbal clarification.
- **SC-003**: The Business Rules Document covers 100% of the business rules mentioned in the plan (code system, watch control, exam logic, homework logic, student behavior, gamification).
- **SC-004**: The User Roles Matrix has zero undefined permission entries — every role-feature combination has an explicit access level.
- **SC-005**: The Data Model Draft covers all 7 data domains (User/Identity, Student, Content, Assessment, Access, Tracking, AI) with all recommended entities from the plan represented.
- **SC-006**: The Access Blueprint can be used to walk through at least 5 distinct code activation scenarios (lesson code, package code, term code, stacked codes, expired code) with a deterministic outcome for each.
- **SC-007**: The Technical Architecture Document is detailed enough that a developer can set up the repository structure and install all required dependencies without additional guidance.
- **SC-008**: The AI Scope Definition has explicit "in-scope" and "out-of-scope" lists, with zero ambiguity about what AI features are planned for Phase 4 vs. deferred.

### Assumptions

- The teacher (Nader George) is available for review and approval of all Phase 0 deliverables.
- The plan.md (already provided) serves as the primary source of truth for all business, academic, and technical decisions.
- No code will be written during Phase 0; this phase is purely documentation and planning.
- The teacher's brand identity (youthful, simple, energetic, story-based) is stable and will not undergo major changes during development.
- The academic structure (First Secondary, Second Secondary, First Baccalaureate, Second Baccalaureate) represents the complete set of target grades at launch.
