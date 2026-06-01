# Data Model: Student Theme Color Customization

## Entity: Student Theme Preference

- **Purpose**: Stores the persisted visual preferences that belong to one authenticated student.
- **Ownership**: One preference record per student account, either embedded in the existing student profile aggregate or represented as a tightly linked child record.

### Fields

- `studentId`
  - Unique identifier for the student owner.
  - Required.
- `lightPaletteId`
  - Identifier of the approved palette selected for light mode.
  - Required after first save; defaults to system light palette before student selection.
- `darkPaletteId`
  - Identifier of the approved palette selected for dark mode.
  - Required after first save; defaults to system dark palette before student selection.
- `updatedAt`
  - Timestamp of the latest preference change.
  - Required for auditing and support visibility.
- `updatedBy`
  - Identifier of the actor making the change.
  - Expected to be the same authenticated student for self-service updates.

### Validation Rules

- `studentId` must map to an authenticated student account.
- `lightPaletteId` must reference a palette approved for light mode.
- `darkPaletteId` must reference a palette approved for dark mode.
- Deprecated or unknown palette identifiers must not be accepted on update.

### Relationships

- One-to-one with `StudentProfile` or one-to-one with `User` in the student role, depending on the final persistence location chosen during implementation.

### State Transitions

- `Uninitialized` -> `Defaulted`
  - Trigger: Student has no saved preference and the platform serves system defaults.
- `Defaulted` -> `Customized`
  - Trigger: Student saves at least one palette choice.
- `Customized` -> `Customized`
  - Trigger: Student changes one or both palette selections.
- `Customized` -> `Defaulted`
  - Trigger: A saved palette becomes unavailable and the system falls back to approved defaults until the student picks a replacement.

## Entity: Theme Palette

- **Purpose**: Represents one curated visual option that can be safely applied in a given display mode.
- **Ownership**: Managed by the platform as an approved catalog, not authored by students.

### Fields

- `paletteId`
  - Stable unique identifier used by the frontend and backend contract.
- `displayName`
  - Human-readable label shown in the student picker.
- `mode`
  - Supported display mode: light or dark.
- `previewAccent`
  - Representative color or preview token used to visually distinguish the palette in selection UI.
- `status`
  - Availability state such as active or deprecated.
- `tokenSet`
  - Resolved design token values needed by the frontend to render the palette.

### Validation Rules

- `paletteId` must be unique.
- `mode` must be exactly one supported display mode.
- `status` must not allow deprecated palettes to be newly selected.
- `tokenSet` must satisfy platform readability and contrast review before release.

### Relationships

- One `Theme Palette` can be referenced by many `Student Theme Preference` records.

## Entity: Theme Preference View Model

- **Purpose**: Shapes the data returned to the student UI for bootstrap and settings interactions.

### Fields

- `currentMode`
- `selectedLightPaletteId`
- `selectedDarkPaletteId`
- `availableLightPalettes`
- `availableDarkPalettes`
- `defaultLightPaletteId`
- `defaultDarkPaletteId`

### Validation Rules

- Available palette collections must contain only approved active options.
- Default palette identifiers must always resolve to one of the available options for each mode.
- Selected palette identifiers in the response must either be active approved choices or the corresponding defaults after fallback resolution.
