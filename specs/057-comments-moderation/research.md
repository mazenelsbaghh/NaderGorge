# Research: Lesson Comments Moderation

## Decision 1: Anchor comments to the lesson, not to an individual video

- **Decision**: Store comments against `LessonId` and render the comments block beneath the lesson's video area in the lesson page.
- **Rationale**: The current lesson page already treats the lesson as the main content unit, with videos, resources, homework, and exam grouped in `LessonViewer`. Anchoring comments to the lesson keeps discussion tied to the academic unit even when a lesson contains multiple videos.
- **Alternatives considered**:
  - Attach comments to `LessonVideoId`: rejected because the request asks for a section under the lesson video area, while the lesson page can contain multiple videos and already centers the lesson as the main container.
  - Create course-level comments only: rejected because moderation and visibility need to stay specific to the lesson context.

## Decision 2: Use a dedicated moderation workflow with `Pending`, `Approved`, and `Rejected`

- **Decision**: New comments are created as `Pending`, become visible only when `Approved`, and stay hidden when `Rejected`.
- **Rationale**: This maps exactly to the requested teacher review flow and gives clear public visibility rules with minimal ambiguity.
- **Alternatives considered**:
  - Publish immediately and allow teacher removal later: rejected because it violates the requirement that the teacher decides whether to accept the comment.
  - Soft-hide with an `isVisible` boolean only: rejected because explicit moderation states are clearer for teacher workflows and auditability.

## Decision 3: Expose dedicated student and moderation endpoints instead of overloading existing lesson detail queries

- **Decision**: Introduce focused endpoints for listing approved lesson comments, creating a lesson comment, listing/moderating lesson comments for teachers, and keep `GetLessonDetail` / `GetLessonCockpit` responsibilities narrow.
- **Rationale**: Student public reads, student private submission feedback, and teacher moderation queues each require different filters and permissions. Separate contracts reduce accidental data leakage and simplify testing.
- **Alternatives considered**:
  - Add all comment data directly into `LessonDetailDto`: rejected because it mixes public and private moderation concerns and risks exposing pending comments.
  - Add moderation data into `LessonCockpitDto` only: rejected because student submission and public read still need independent contracts.

## Decision 4: Reuse the existing lesson management page as the teacher moderation surface

- **Decision**: Add a dedicated comments tab or comments section to the existing lesson management page at `/admin/content/lessons/[id]` instead of creating a new standalone moderation dashboard.
- **Rationale**: The current lesson cockpit already provides the contextual teacher workspace for videos, resources, homework, and exams. Keeping comments there matches the user's request to review from the course/lesson page and aligns with the constitution's UI consistency rules.
- **Alternatives considered**:
  - Build a global `/admin/comments` page: rejected because it adds navigation overhead and breaks the request's lesson-context moderation expectation.
  - Moderate from the public lesson page: rejected because moderation belongs in staff-only tooling, not student-facing surfaces.

## Decision 5: Allow moderation through existing staff authorization patterns

- **Decision**: Student comment endpoints require authenticated student lesson access. Moderation endpoints should be authorized for `Admin` and `Teacher`, matching existing staff patterns where needed.
- **Rationale**: The codebase already allows both `Admin` and `Teacher` in selected staff operations, and `AccessCheckService` already recognizes teacher/admin lesson access. This keeps the feature aligned with the stated teacher moderation requirement.
- **Alternatives considered**:
  - Keep moderation `Admin`-only because `AdminController` defaults to `Admin`: rejected because it conflicts with the product requirement that the teacher can accept or reject comments.
  - Allow assistants to moderate: rejected because the requirement names the teacher, and expanding moderation actors increases scope.

## Decision 6: Record moderation as a state-changing audited action

- **Decision**: Approval and rejection actions should create audit entries alongside persisted moderation metadata.
- **Rationale**: The constitution requires audit logging for state-changing operations. Comment moderation affects publicly visible academic content and should therefore be traceable.
- **Alternatives considered**:
  - Store only `ReviewedAt` and `ReviewedBy`: rejected as insufficient because platform governance expects audit logging of state changes.

## Decision 7: Keep v1 intentionally narrow

- **Decision**: The first implementation covers flat lesson comments only: create, list approved, list pending/history for staff, approve, and reject.
- **Rationale**: This satisfies the user request without introducing replies, editing, deletion, pinning, notifications, or abuse reporting workflows.
- **Alternatives considered**:
  - Add replies and threaded discussions: rejected as scope creep.
  - Add rejection reasons visible to students: rejected because it is not required and adds extra UX/state complexity.
