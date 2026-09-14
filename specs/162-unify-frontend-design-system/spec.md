# Feature Specification: Frontend Design System Unification

**Feature Branch**: `162-unify-frontend-design-system`  
**Created**: 2026-07-14  
**Status**: Draft  
**Input**: User-approved Arabic feature brief for a full-platform frontend-only visual-system unification.

## Clarifications

### Session 2026-07-14

- Q: ما معيار WCAG AA الملزم للتحقق من التباين في جميع الثيمات؟ → A: WCAG 2.2 AA: نسبة 4.5:1 للنص العادي، و3:1 للنص الكبير ومكوّنات الواجهة ومؤشرات الحالة.
- Q: ما الذي يعد ضمن جرد المسارات لإثبات التغطية الكاملة؟ → A: كل صفحة واجهة متاحة في الإنتاج لكل دور، بما فيها المسارات الديناميكية وحالات الصلاحيات، مع استثناء API والصفحات الداخلية غير المعروضة.
- Q: هل يجب استبدال كل تكرار حالي للأنماط الأساسية بمكونات مشتركة؟ → A: المكونات المشتركة إلزامية لكل الأنماط الأساسية الجديدة أو المعدلة، والاستثناءات الموثقة فقط تبقى محلية.
- Q: هل يشمل منع الألوان الخام كل الصيغ ودرجات Tailwind والشفافية؟ → A: كل صيغ الألوان ودرجات Tailwind والشفافية، ولا تمر الاستثناءات إلا عبر allowlist موثق.
- Q: هل يسمح بتحسين النص للوصول؟ → A: تحفظ النصوص المرئية؛ يسمح فقط بإضافة نص وصول غير مرئي أو وصف حالة مساعد لا يغير المعنى.

## User Scenarios & Testing

### User Story 1 - Consistent readable application surfaces (Priority: P1)

As any platform user, I can use every public, student, teacher, assistant, administrator, and live-support surface in light or dark mode without unreadable text, borders, controls, or status states.

**Why this priority**: Broken contrast blocks normal use and damages trust on every role surface.

**Independent Test**: Toggle the existing theme on representative routes for every role and inspect normal, hover, focus, active, disabled, loading, empty, and error states.

**Acceptance Scenarios**:

1. **Given** an authenticated or public user opens any inventoried route, **When** the existing theme mode changes, **Then** every application surface uses the corresponding semantic theme values and remains readable.
2. **Given** a status, action, input, table, dialog, or navigation item is displayed, **When** its state changes, **Then** its state remains distinguishable without relying only on color.

---

### User Story 2 - Consistent controls and state containers (Priority: P1)

As a user completing an existing workflow, I see the same visual vocabulary for controls, cards, tables, dialogs, loading, empty, error, and disabled states while the screen order, copy, permissions, and behavior remain unchanged.

**Why this priority**: Repeated local implementations currently drift in contrast, focus treatment, spacing, and dark-mode behavior.

**Independent Test**: Exercise representative public, student, staff, and live-support flows before and after migration and compare visible copy, navigation, submitted requests, and outcomes.

**Acceptance Scenarios**:

1. **Given** an existing form, table, dialog, or action, **When** a user completes its current flow, **Then** the same request, permission result, copy, and destination are preserved.
2. **Given** a loading, empty, failure, or disabled state, **When** its existing trigger occurs, **Then** the existing message and recovery behavior remain while its container follows the shared visual pattern.

---

### User Story 3 - Maintainable visual governance (Priority: P2)

As a maintainer, I can add UI without introducing an undocumented raw color or a duplicate core component pattern.

**Why this priority**: A single release will regress unless future changes are checked against the same system.

**Independent Test**: Run the frontend visual-governance check against application source and introduce a known disallowed raw color to verify failure.

**Acceptance Scenarios**:

1. **Given** application UI source is checked, **When** it contains an unapproved raw color, **Then** the check fails with the file and reason.
2. **Given** a documented media or brand-asset exception, **When** it is checked, **Then** it passes without masking unrelated violations.

### Edge Cases

- Long Arabic RTL labels, table overflow, empty lists, loading skeletons, validation errors, disabled controls, and permission-denied views retain readable contrast in both themes.
- Third-party media, logos, images, video overlays, and necessary generated SVG colors are documented exceptions rather than silently rewritten.
- Live-support reconnect, queue, conversation, and AI-status behavior remains unchanged while visual containers migrate.

### Manual QA & Docker Acceptance

- **Manual QA**: Test public, student, teacher, assistant, administrator, and live-support representative flows in both themes and narrow/wide viewports.
- **Manual QA Negative Check**: Confirm permission-denied and disabled actions remain denied and non-interactive.
- **Docker Acceptance**: Run the existing frontend build and role-surface smoke checks against the configured application environment.
- **External Dependencies**: Live-support visual smoke requires its existing backend, SignalR, and test user setup; unavailable environment dependencies must be recorded explicitly.

## Requirements

### Functional Requirements

- **FR-001**: The system MUST provide one semantic visual-token vocabulary for application canvas, surfaces, text, borders, focus, interaction, and information, success, warning, and danger states in both themes.
- **FR-002**: Every inventoried application route and shared UI component MUST use the semantic vocabulary for normal and interactive states.
- **FR-003**: The system MUST provide shared patterns for buttons, icon buttons, fields, selects, surfaces, status badges, status alerts, tables, dialogs, empty states, and skeletons; every new or modified core pattern MUST use them unless a documented local exception applies.
- **FR-004**: The migration MUST preserve existing screen order, visible copy, user flows, business rules, permissions, data requests, and navigation behavior.
- **FR-005**: Existing loading, empty, error, cancellation, and disabled triggers and messages MUST remain intact while their visual containers are standardized.
- **FR-006**: Text/background combinations MUST meet WCAG 2.2 AA at 4.5:1 for normal text; large text, UI components, and meaningful state indicators MUST meet 3:1 in both themes.
- **FR-007**: Application UI source MUST not use any raw color form, palette utility, or color transparency outside a documented allowlist for assets, media, and required third-party rendering.
- **FR-008**: Live-support participant, staff, admin, and AI surfaces MUST migrate without changing realtime connection, queue, conversation, or recovery behavior.
- **FR-009**: The implementation MUST include an auditable inventory of every production-accessible UI route for all roles, including dynamic and permission-state routes, while excluding API and non-user-facing internal routes.
- **FR-010**: The implementation MUST provide automated verification for visual-token governance and theme coverage of shared primitives.
- **FR-011**: Visible copy MUST remain unchanged; accessibility-only non-visible labels or supplementary state descriptions are permitted when they do not change meaning.

### Key Entities

- **Semantic visual token**: A named visual role with theme-specific values.
- **Shared UI primitive**: A reusable interface contract for a common control or state container.
- **Route and component inventory**: The authoritative list used to prove full-platform migration coverage.
- **Raw-color allowlist**: A documented exception record with location and rationale.

## Success Criteria

### Measurable Outcomes

- **SC-001**: 100% of inventoried production-accessible public, student, teacher, assistant, administrator, and live-support routes, including dynamic and permission-state routes, have recorded light and dark verification evidence.
- **SC-002**: 100% of shared primitives support documented normal, hover, focus, active, disabled, loading, empty, and error behavior where relevant.
- **SC-003**: The raw-color governance check reports zero unapproved application UI color violations.
- **SC-004**: Representative existing workflows for all six surface groups preserve their current visible copy, permissions, requests, navigation result, and error/empty behavior.
- **SC-005**: All changed frontend production code passes lint, type checking, build, and targeted behavior checks.
- **SC-006**: A complete route and primitive verification run finishes with zero unapproved color findings and all required role surfaces checked in one release cycle.
- **SC-007**: Each role-surface smoke run completes within 10 minutes with no unreadable state or workflow regression.

## Assumptions

- The existing theme-selection policy remains unchanged; this feature only makes its rendered output consistent.
- Backend, APIs, data storage, workers, authorization, and business rules are out of scope.
- Existing brand assets retain their approved colors and may be allowlisted when tokenization would alter the asset.
