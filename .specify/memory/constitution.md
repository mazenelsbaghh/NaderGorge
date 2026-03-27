<!--
  Sync Impact Report
  ───────────────────
  Version change: 1.1.0 → 2.0.0 (Registration overhaul + hierarchy + code expansion)

  Modified principles:
    - V. Academic Content Integrity — Updated content hierarchy to Package → Year → Term → Section → Lesson
    - VI. Two-Step Registration & UX Simplicity → VI. Single-Flow Registration & UX Simplicity
      (BREAKING: registration is now single-flow with expanded student data fields)

  Added sections: N/A
  Removed sections: N/A

  Templates requiring updates:
    - .specify/templates/plan-template.md       ✅ reviewed — Constitution Check section aligns
    - .specify/templates/spec-template.md        ✅ reviewed — scope/requirements align
    - .specify/templates/tasks-template.md       ✅ reviewed — task categorization aligns

  Follow-up TODOs: None
-->

# Nader George Educational Platform Constitution

## Core Principles

### I. Modular Clean Architecture (NON-NEGOTIABLE)

The system MUST follow a layered, modular architecture at all times:

- **Backend**: API Layer → Application Layer → Domain Layer → Infrastructure Layer.
- **Frontend**: Pages → Components → Services → Hooks → Utils.
- Every backend module (Authentication, Users, Packages, Exams, Codes, Homework, Gamification, AI, Tracking, Notifications, Audit) MUST be self-contained with clear boundaries.
- Cross-module communication MUST happen through well-defined interfaces, never through direct internal access.
- No circular dependencies between modules.
- Database access MUST be confined to the Infrastructure Layer; Domain and Application layers MUST NOT reference ORM-specific types directly.

**Rationale**: The platform spans 8 phases (0 through 7) and 14+ core modules. Without strict modularity, the codebase will become unmaintainable as features scale.

### II. Provider Abstraction First

Every external integration MUST be built behind an abstraction layer from day one:

- **Video providers**: YouTube initially, with a `VideoProviderAbstraction` supporting provider type, external video ID, title, duration, order, watch limits, replay limits, and provider metadata.
- **Notification channels**: In-platform, SMS, future WhatsApp/Zoho — all behind a unified `NotificationProvider` interface.
- **AI services**: All AI features MUST go through an `AIServiceProvider` abstraction, never directly coupled to a specific LLM vendor.
- **Payment/code systems**: Code redemption and access grant logic MUST be separated from specific code type implementations. The system supports 7 code types (year, term, month, lesson, video, exam, balance/credit) and MUST handle each through a unified code engine interface.

**Rationale**: The plan explicitly warns against hard-coding YouTube behavior (Section 2.6) and mandates future migration paths. Abstraction from day one prevents costly rewrites.

### III. Security & Access Control by Default

Security is a foundational concern, not a bolt-on feature:

- **Authentication**: JWT-based with refresh token flow. Phone-based registration with OTP verification.
- **Authorization**: Role-based access control (Student, Admin, Teacher, Assistant) MUST be enforced at the API layer for every endpoint.
- **Audit logging**: Every state-changing operation (code generation, code redemption, content edits, permission changes, student state updates) MUST produce an audit log entry.
- **Session control**: Device tracking, IP tracking, and optional forced logout MUST be supported from Phase 1.
- **Rate limiting**: MUST be applied to code redemption and authentication flows.
- **Data validation**: All user input MUST be validated at both frontend and backend layers. Never trust client-side validation alone.

**Rationale**: The platform handles student data, financial transactions (code-based access), and academic records. A security breach would be catastrophic for trust and legal compliance.

### IV. Phased Delivery with MVP Discipline

Development MUST follow a strict phased approach:

- **Phase 0**: Discovery, planning, and product blueprint — no code production.
- **Phase 1**: Foundation and MVP launch — working auth, content, codes, exams, admin, student dashboard.
- **Phase 2**: Structured learning and academic operations (homework, parent layer, gamification, notifications).
- **Phase 2.5**: Video security and content protection (server-side embed, Shadow DOM, DOM shields).
- **Phase 3**: Registration, code system, and content hierarchy overhaul.
- **Phase 4–7**: Incremental feature delivery (question bank, AI, watch control, analytics).
- Each phase MUST deliver independently usable, testable functionality.
- Features from later phases MUST NOT leak into earlier phase implementations.
- Schema fields for future features MAY be added early (e.g., watch limit fields in Phase 1), but business logic MUST NOT be implemented ahead of schedule.
- Risk avoidance: Do NOT build too much too early (Section 9.1 of the plan).

**Rationale**: The plan explicitly identifies "building too much too early" as a major risk. MVP discipline ensures a launchable product before expanding scope.

### V. Academic Content Integrity

All academic features MUST respect the teacher's content authority:

- **AI boundaries**: AI MUST stay tied to approved academic content (Section 9.6). No open-web generic assistant behavior. The controlled study assistant MUST answer only from approved platform content, teacher-uploaded material, and bounded lesson context.
- **Content hierarchy**: Package → Year → Term → Content Section (Month) → Lesson → Video/Exam/Homework. This hierarchy is non-negotiable and MUST be preserved in all data models and UIs.
- **Question bank integrity**: Questions MUST be classified by grade, unit, lesson, difficulty, question type, exam year, idea repetition, academic tags, and error pattern tags.
- **Progression rules**: When implemented, lesson unlock MUST respect code type, package structure, completion rules, exam score, and homework status.
- **Gamification ethics**: Gamification MUST be motivating, not toxic (Section 4.7 of the plan).

**Rationale**: This is an educational platform for a specific teacher's brand. Academic quality and content control are the core value proposition.

### VI. Single-Flow Registration & UX Simplicity

The platform experience MUST be simple, organized, motivating, and clear in its study path:

- **Registration**: MUST collect all required data in a single registration flow:
  - Personal data: full name (four-part), student phone (Dostab), student code (Dostab), date of birth, gender, governorate, address, parent phone, parent status (father alive/not, mother alive/not).
  - Academic data with conditional logic: education stage (Secondary/Baccalaureate) → grade → track/branch (only for Second Secondary and Second Baccalaureate).
  - Second Secondary tracks: Arts or Science.
  - Second Baccalaureate tracks: Medicine and Life Sciences, Engineering and Computer Science, Business, Arts and Humanities.
- **Student dashboard**: MUST surface available packages, latest lesson, upcoming exams, progress percentage, used codes, notifications, and quick resume-study access.
- **Navigation**: Students MUST always know where they are in their study path and what comes next.
- **Control balance**: The platform MUST guide and control students, not suffocate them (Section 9.5).
- **Responsive design**: The platform MUST work well on both desktop and mobile browsers.

**Rationale**: All student data is collected upfront to avoid incomplete profiles and enable proper academic tracking from the start. Conditional field logic keeps the form clean.

### VII. Observability & Operational Readiness

The system MUST be observable and operationally ready from Phase 1:

- **Structured logging**: All backend services MUST produce structured logs with correlation IDs for request tracing.
- **Error handling**: All API endpoints MUST return consistent error response formats. Unhandled exceptions MUST be caught and logged, never exposed to clients.
- **Health checks**: Each deployable service (frontend, backend API, Node worker) MUST expose a health check endpoint.
- **Environment separation**: Development, Staging, and Production environments MUST be maintained separately. Configuration MUST NOT be hard-coded.
- **Database migrations**: Schema changes MUST use a versioned migration system (Entity Framework Core migrations). Direct schema modifications in production are FORBIDDEN.
- **Background job monitoring**: BullMQ worker jobs MUST be trackable with status, retry count, and failure logs.

**Rationale**: A platform serving students during exam periods cannot afford unexplained downtime. Observability is the foundation of reliable operations.

### VIII. Premium Editorial Design System (The "Curated Archive")

The platform MUST adhere to the "Editorial Scholar" design system to maintain a high-end experience:

- **No-Line Rule**: Borders (e.g., 1px solid defined lines) are strictly prohibited for defining layout sections. Instead, boundaries MUST be established using background tonal shifts (e.g., `surface-container-low` vs. `surface`) and negative space.
- **Glass & Gradient**: Use glassmorphism (`surface_variant` at 60% opacity, 24px blur) for floating navigation/modals, and use signature gradients (e.g., `primary` to `primary_container` at 135°) for main CTAs to add "soul" and depth to the interface.
- **Typography**: Complete reliance on **Manrope** for geometric clarity and modern humanist feel. Use specific semantic typography tiers (Display, Headline, Title, Body, Label) to convey hierarchy naturally.
- **Elevation & Depth**: Rely on layering (stacking nested `surface-container` tiers from lowest to highest) rather than hard drop-shadows. Shadows should be warm and ambient (e.g., tinted with umber, never pure black). If you must use a border fall-back, use `outline_variant` at 15% opacity ("Ghost Border").
- **Tonal Palette**: Employ heritage tones of rich creams (#fcf9ef), deep umbers (#1c1c16), and golds (#775a19). Never use pure black (#000000) for text or pure gray for borders.

**Rationale**: The platform's visual identity rejects sterile SaaS aesthetics in favor of a curated, academic journal feel. This fosters a sense of prestige, focus, and trust among students.

## Technology Stack & Constraints

### Mandatory Stack

| Layer              | Technology                          | Constraint                                                       |
|--------------------|-------------------------------------|------------------------------------------------------------------|
| Frontend Framework | Next.js (React)                     | TypeScript MUST be used. No plain JavaScript files.              |
| Frontend Styling   | Tailwind CSS                        | Custom component system or Shadcn/UI.                            |
| State Management   | React Query (TanStack Query)        | Zustand for local app state where needed.                        |
| Animations         | Framer Motion                       | Use only for lightweight, purposeful animations.                 |
| Backend Framework  | .NET Web API (C#)                   | Clean Architecture. CQRS where useful, not blindly everywhere.   |
| ORM                | Entity Framework Core               | Code-first migrations. No raw SQL unless for performance-critical queries. |
| Primary Database   | PostgreSQL                          | All relational data. Structured and semi-structured.             |
| Cache/Speed Layer  | Redis                               | Caching, rate limiting, OTP storage, leaderboard, session state. |
| Background Jobs    | BullMQ (Node.js worker service)     | .NET writes jobs → Redis broker → Node worker processes.         |
| Video (Phase 1)    | YouTube embedded                    | Behind VideoProviderAbstraction. Never hard-code YouTube URLs.   |

### Deployment Constraints

- Services: Frontend (Next.js), Backend API (.NET), Node Worker (BullMQ), PostgreSQL, Redis, Reverse Proxy/Gateway.
- Environments: Development, Staging, Production — minimum three.
- Secrets MUST be managed through environment variables or a secrets manager, never committed to source control.

### Performance Expectations

- API response time: < 500ms p95 for standard CRUD operations.
- Video page load: < 3 seconds to first content render.
- Code redemption: < 2 seconds end-to-end including confirmation.
- Concurrent users: System MUST handle the expected student base per academic year without degradation.

## Development Workflow & Quality Gates

### Code Quality

- **TypeScript strict mode**: Frontend code MUST compile with strict TypeScript configuration.
- **C# nullable reference types**: Backend code MUST enable nullable reference types.
- **Linting**: ESLint for frontend, .NET analyzers for backend. All warnings MUST be resolved before merge.
- **Code review**: All changes MUST be reviewed before merging to main/production branches.

### Testing Strategy

- **Backend unit tests**: All service-layer logic MUST have unit test coverage.
- **API contract tests**: All public API endpoints MUST have contract tests validating request/response shapes.
- **Frontend component tests**: Critical user flows (registration, code redemption, exam submission) MUST have integration tests.
- **Database migration tests**: Migrations MUST be tested against a clean database before deployment.

### Branch & Release Strategy

- **Feature branches**: All work MUST be done on feature branches, never directly on main.
- **Semantic versioning**: Releases MUST follow MAJOR.MINOR.PATCH versioning.
- **Changelog**: Every release MUST include a changelog entry describing user-facing changes.

### Definition of Done

A feature is considered "done" when:

1. Code is written, reviewed, and merged.
2. All existing tests pass.
3. New tests cover the feature's critical paths.
4. API documentation is updated (if applicable).
5. Database migrations are included (if applicable).
6. The feature works in the Staging environment.

## Governance

- This constitution supersedes all ad-hoc decisions and informal agreements regarding architecture, technology choices, and development practices.
- **Amendments**: Any change to this constitution MUST be documented with a version bump, rationale, and impact assessment. MAJOR changes (principle removal or redefinition) require explicit stakeholder approval.
- **Compliance review**: At the start of each new phase (Phase 1, 2, 3, etc.), the constitution MUST be reviewed to ensure principles still align with evolving requirements.
- **Versioning policy**: Constitution versions follow semantic versioning — MAJOR for backward-incompatible governance changes, MINOR for new principles or expanded guidance, PATCH for clarifications and typo fixes.
- **Conflict resolution**: When a technical decision conflicts with a constitution principle, the principle takes precedence unless an explicit exception is documented in the Complexity Tracking section of the relevant plan.md.
- Use `.specify/memory/constitution.md` as the single source of truth for governance.

**Version**: 2.0.0 | **Ratified**: 2026-03-19 | **Last Amended**: 2026-03-27
