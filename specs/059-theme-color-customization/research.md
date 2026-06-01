# Research: Student Theme Color Customization

## Decision 1: Persist theme palette choices in backend-owned student preferences

- **Decision**: Store each student's selected light palette and dark palette in persistent student data managed by the authenticated backend instead of relying only on browser storage.
- **Rationale**: The feature spec requires preferences to survive future sessions. Browser-only storage would not restore the same theme across devices, fresh browsers, or cleared local state. The backend already has authenticated student endpoints and a `StudentProfile` aggregate that can anchor user-specific preferences.
- **Alternatives considered**:
  - Keep palette selection only in `localStorage`: rejected because it fails cross-device persistence and weakens consistency.
  - Store theme choice as generic platform settings: rejected because platform settings are global, while this requirement is per-student.
  - Create an entirely separate standalone preference subsystem: rejected for v1 because the feature scope is narrow and existing student profile ownership is sufficient.

## Decision 2: Separate theme mode from palette identity

- **Decision**: Preserve the existing `light` / `dark` mode toggle behavior and add independent palette selection for each mode.
- **Rationale**: The current frontend already uses a mode toggle and runtime CSS variable injection. Reusing that split avoids rewriting stable behavior and matches the product requirement that students can choose colors in both light and dark experiences.
- **Alternatives considered**:
  - Replace mode toggling with a single combined theme enum: rejected because it would tightly couple palette and mode, making the UX and migration path harder.
  - Allow one palette to drive both modes automatically: rejected because the spec explicitly asks for multiple options in both light and dark.

## Decision 3: Extend the existing CSS variable token pipeline

- **Decision**: Build student palette application on top of the existing CSS custom property strategy already used by `useAdminTheme` and student shell surfaces.
- **Rationale**: The current student experience consumes runtime token variables such as `--admin-*` values. Extending that token pipeline minimizes churn, keeps the UI responsive to instant in-session changes, and reduces the risk of scattered hard-coded colors.
- **Alternatives considered**:
  - Rewrite the student surface to a new styling system: rejected because it is out of scope and would introduce unnecessary regression risk.
  - Apply theme color changes via ad hoc component props: rejected because it would fragment the design system and miss global surfaces.

## Decision 4: Use curated palette identifiers, not arbitrary student-defined colors

- **Decision**: Offer a fixed catalog of approved palette identifiers for light and dark mode rather than freeform color pickers.
- **Rationale**: The spec requires readability and consistent visual quality. Curated palettes make contrast review, brand alignment, and QA tractable while still delivering meaningful student personalization.
- **Alternatives considered**:
  - Freeform hex color customization: rejected because it would create readability failures and a large validation burden.
  - Very small two-choice palette set: rejected because it would weaken the promised value of "multiple colors" for students.

## Decision 5: Add dedicated authenticated student preference endpoints

- **Decision**: Introduce student-scoped read/update endpoints in the existing `StudentController` pattern and consume them through `student-service.ts`.
- **Rationale**: The project already exposes student dashboard and progress data through this controller/service pairing. Theme preferences fit the same ownership and authentication model, and a dedicated contract keeps frontend state bootstrap explicit.
- **Alternatives considered**:
  - Piggyback theme data onto dashboard payloads: rejected because it couples unrelated data and complicates targeted updates.
  - Reuse admin settings endpoints: rejected because the feature is student-specific and must not leak into non-student role flows.

## Decision 6: Add a student settings entry point instead of burying color selection in mode toggle controls

- **Decision**: Keep the existing quick light/dark toggle for mode switching and add a student settings surface or panel for palette selection.
- **Rationale**: Mode switching is a fast frequent action, while palette selection is a lower-frequency personalization task. Separating them keeps the shell uncluttered and aligns with the constitution's UX simplicity principle.
- **Alternatives considered**:
  - Expand the existing theme toggle into a multi-option picker: rejected because it would overload a simple control and reduce clarity.
  - Hide palette controls in a profile page not connected to the shell: rejected because discoverability would be weak for a personalization feature.
