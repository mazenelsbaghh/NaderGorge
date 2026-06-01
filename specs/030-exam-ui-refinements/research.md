# Research & Technical Decisions: Exam UI Refinements

## DaisyUI Countdown CSS implementation
**Decision**: Implement a custom `CountdownTimer` React component using the provided DaisyUI CSS approach combined with Framer Motion, rather than installing the full DaisyUI library.
**Rationale**: The user wants a specific CSS-variable based ticking animation. DaisyUI achieves this through a specific CSS mask and transition trick using `--value`. Installing the entire DaisyUI library would interfere with our highly customized Tailwind design system and introduce bloat. Instead, we can extract just the required CSS rules (`.countdown`, `::before`) and encapsulate them in a scoped React component with a `useEffect` interval mapping to `--value`.
**Alternatives considered**: Traditional React-rendered text updates (janky without CSS transitions), Framer Motion AnimatePresence sliding numbers (heavier DOM manipulation but more customizable). The CSS variable approach is explicitly requested and highly performant.

## Unlocking "Blocking Assessment Name" in LessonDetailDto
**Decision**: Modify `GetLessonDetailQueryHandler` to query the name of the unpassed Homework or Exam when `isLocked` evaluates to true, instead of returning a generic message.
**Rationale**: The current implementation simply checks existence of a passed attempt. To get the name, we need to join or fetch the underlying `Homework` or `Exam` entity title before throwing the `LockedReason`. This adds a minor query cost but drastically improves UX.
**Alternatives considered**: Sending all prerequisite items to the client and evaluating locks there. Rejected because server-side truth is a strict Constitution requirement.

## Free Test Content Seeding
**Decision**: Implement a hidden or admin-only API Endpoint `POST /api/admin/system/seed-test-content` that programmatically inserts a Term -> Section -> Package -> Lesson -> Exam/Homework hierarchy with Price = 0.
**Rationale**: Hardcoding migrations with seeded data can pollute production. An admin endpoint allows controlled generation on-demand, fetching existing valid IDs for relations (like SubjectId or GradeId) dynamically without hardcoded GUIDs.
**Alternatives considered**: Entity Framework `HasData` seeding. Rejected because `HasData` makes the records immutable through normal application flows, making testing deletion or updates impossible.
