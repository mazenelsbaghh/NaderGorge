# Research: Student Community

## Decision 1: Model Community as a standalone feed, not as lesson-bound discussion

- **Decision**: Create a dedicated community feed that exists separately from lesson pages and lesson comments.
- **Rationale**: The feature request describes a broad student community where students publish posts and interact socially. Tying the feature to lessons would narrow the scope to lesson discussion instead of a cross-platform community surface.
- **Alternatives considered**:
  - Reuse lesson comments only: rejected because the user asked for posts, likes, and comments as a community experience rather than per-lesson discussion.
  - Reuse assistant/chat infrastructure: rejected because the feature is user-generated content with moderation, not guided assistant interaction.

## Decision 2: Moderate only top-level posts in v1

- **Decision**: Every new community post starts as `Pending`, becomes public only when `Approved`, and stays hidden when `Rejected`. Likes and comments are allowed only on already approved posts and do not introduce a second moderation workflow in v1.
- **Rationale**: This matches the explicit requirement that the admin must approve posts while keeping the first release narrow enough to ship. It also follows the same moderated-content pattern already used for lesson comments in the repository.
- **Alternatives considered**:
  - Moderate both posts and comments: rejected because it adds a second queue, more states, and broader UX complexity without being explicitly requested.
  - Publish immediately and moderate later: rejected because it violates the required pre-approval workflow.

## Decision 3: Use explicit state enums and moderation metadata instead of a simple visibility flag

- **Decision**: Represent community post moderation with clear states such as `Pending`, `Approved`, and `Rejected`, plus reviewer and review timestamp metadata.
- **Rationale**: Explicit states are easier to audit, query, test, and display in the admin queue than a single boolean. This also stays consistent with the moderation approach already present in the codebase for lesson comments.
- **Alternatives considered**:
  - Use `IsVisible` only: rejected because it cannot represent unresolved pending work cleanly.
  - Hard-delete rejected posts: rejected because losing moderation history reduces operational traceability.

## Decision 4: Keep engagement flat in v1

- **Decision**: Post comments are flat, chronological replies on approved posts with no nested replies, mentions, or reactions on comments.
- **Rationale**: The user asked for "like and comment and so on" but the minimum version that delivers value is flat discussion. This avoids premature complexity in data modeling, rendering, and moderation.
- **Alternatives considered**:
  - Threaded replies: rejected as scope creep for the initial release.
  - Multiple reaction types: rejected because a single like is enough for first-pass engagement.

## Decision 5: Provide dedicated student and admin endpoints

- **Decision**: Introduce focused community endpoints for feed listing, student post creation, comment creation, like toggling, and admin moderation operations rather than overloading unrelated controllers.
- **Rationale**: Student feed reads, student engagement writes, and admin moderation each require different filters, permissions, and response shapes. Separate contracts reduce leakage risk and make testing clearer.
- **Alternatives considered**:
  - Fold community into existing `ContentController`: rejected because community is not lesson content and deserves its own bounded context.
  - Build one all-purpose endpoint with role-based branching: rejected because it increases ambiguity and weakens contract clarity.

## Decision 6: Reuse current service and UI patterns already present in the project

- **Decision**: Mirror the established repository pattern used by moderated lesson comments: EF-backed entities, MediatR commands/queries, explicit controllers, frontend service wrappers, and shared admin shell components.
- **Rationale**: The codebase already contains a successful moderated-content reference implementation. Reusing the same structure lowers design risk and aligns with the constitution's modularity and admin dashboard consistency rules.
- **Alternatives considered**:
  - Introduce a new architectural pattern for community only: rejected because it would fragment the codebase.
  - Push all feed composition to the frontend through multiple calls: rejected because public visibility and counts are safer and simpler when shaped by dedicated backend endpoints.

## Decision 7: Start with one global community feed

- **Decision**: The first release uses one shared student community feed rather than segmenting by course, grade, or package.
- **Rationale**: The requirement did not ask for segmentation, and a single moderated feed is the simplest version to validate product value quickly.
- **Alternatives considered**:
  - Separate feeds by course or grade: rejected because it multiplies moderation and visibility rules without a clear product requirement.
  - Private groups or circles: rejected as out of scope for v1.
