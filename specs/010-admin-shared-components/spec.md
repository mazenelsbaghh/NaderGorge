# Feature Specification: Admin Shared Components Library

**Feature Branch**: `010-admin-shared-components`
**Created**: 2026-03-26
**Status**: Draft
**Input**: User description: "توحيد مكونات صفحات الإدارة في كومبوننتات مشتركة قابلة لإعادة الاستخدام (Sidebar, Header, Footer, Table, Pagination, Modal, Stats Cards)"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Admin pages use a single shared layout shell (Priority: P1)

An administrator navigates between admin pages (Users, Content, Codes, Questions, Overrides). Every page renders an identical sidebar navigation, breadcrumb header, footer ornament, and background pattern. When the theme is toggled on any page, all pages reflect the same theme. The layout shell is authored once and reused by every admin page—no page duplicates sidebar or header markup.

**Why this priority**: Eliminating layout duplication is the foundation. Without it, every subsequent shared component still lives inside bloated page files. This is the highest-leverage change.

**Independent Test**: Can be tested by navigating across all admin pages and verifying the sidebar, header, breadcrumb, and footer are visually identical and rendered from the same component.

**Acceptance Scenarios**:

1. **Given** the admin content page and admin users page, **When** a developer views the source of both, **Then** neither page contains inline sidebar, header, breadcrumb, or footer markup—all use the shared layout shell.
2. **Given** the admin shell component, **When** the theme toggle button is clicked, **Then** all admin pages use the updated theme on subsequent navigation.
3. **Given** the admin shell, **When** a navigation item is active, **Then** the correct item is highlighted based on the current route.

---

### User Story 2 - Shared data table with built-in pagination (Priority: P1)

An administrator views tabular data (users, packages, sections, lessons, videos, codes). All tables share the same column header style, row hover effect, loading skeleton, empty state message, and pagination controls. The table component accepts column definitions and row data as inputs, and internally handles page state, display of "showing X-Y of Z" text, and previous/next buttons.

**Why this priority**: Tables are the most repeated UI pattern across all admin pages. Extracting them eliminates hundreds of duplicated lines per page.

**Independent Test**: Can be tested by replacing the inline table in any single admin page with the shared component and verifying identical visual output and pagination behavior.

**Acceptance Scenarios**:

1. **Given** a shared table component, **When** provided with column definitions and row data, **Then** it renders the correct header labels, row cells, loading skeleton, and empty-state message.
2. **Given** pagination is enabled, **When** there are more rows than a single page, **Then** the component shows "showing X-Y of Z" and enables previous/next navigation.
3. **Given** the table is loading, **When** `loading=true`, **Then** animated skeleton rows are rendered instead of data rows.
4. **Given** no rows match a filter, **When** the row list is empty, **Then** the component shows a configurable "no results" message.

---

### User Story 3 - Shared statistics cards (Priority: P2)

An administrator sees key metrics at the top of each admin page (total users, active packages, total codes, etc.). Each metric card follows the same three-variant design: (1) light card with icon and label, (2) gold/primary accent card, (3) muted card. The card receives an icon, label, value, and variant as inputs.

**Why this priority**: Stat cards are duplicated on every admin page with minor text changes. Extracting them reduces per-page boilerplate significantly while ensuring consistent styling.

**Independent Test**: Can be tested by rendering the three card variants in isolation with sample data and verifying correct styling, icon placement, and value formatting.

**Acceptance Scenarios**:

1. **Given** a stat card with variant "light", **When** rendered, **Then** it shows a bordered card with the `admin-card-soft` background, icon in a tinted circle, label, and formatted value.
2. **Given** a stat card with variant "accent", **When** rendered, **Then** it shows a solid gold/primary background card with white text.
3. **Given** a stat card with variant "muted", **When** rendered, **Then** it shows a card with `admin-card-strong` background.

---

### User Story 4 - Shared modal wrapper (Priority: P2)

An administrator opens a form modal (create package, generate codes, view device details) on any admin page. All modals share the same overlay backdrop, animation pattern, close button, and card container styling. The modal component wraps any children content and manages open/close state via a callback.

**Why this priority**: Modals are a repeated pattern that involves animation boilerplate (AnimatePresence + motion.div). Extracting this reduces code and ensures consistent animation timing.

**Independent Test**: Can be tested by opening a modal from a button, verifying the backdrop blur appears, the card animates in, and clicking "close" dismisses it.

**Acceptance Scenarios**:

1. **Given** the modal is triggered, **When** it opens, **Then** a blurred backdrop appears and the content card scales in with a smooth animation.
2. **Given** the modal is open, **When** the "close" button or backdrop is clicked, **Then** the modal animates out and the onClose callback fires.
3. **Given** any form content inside the modal, **When** rendered, **Then** it maintains the standard admin modal container styling (rounded corners, shadow, max width).

---

### User Story 5 - Shared search and filter toolbar (Priority: P3)

An administrator uses a search bar and filter/action buttons above a table. The toolbar component provides a styled search input (with icon), a filter button slot, and an optional action button slot. All pages share the same input style, icon placement, and button grouping.

**Why this priority**: The search+filter bar is repeated on every list page. Extracting it simplifies each page while keeping a consistent design language.

**Independent Test**: Can be tested by rendering the toolbar with a search handler and verifying the search input, filter button, and action buttons render correctly.

**Acceptance Scenarios**:

1. **Given** the toolbar, **When** rendered, **Then** it shows a rounded search input with a search icon on the right and placeholder text.
2. **Given** a search term is typed, **When** the input changes, **Then** the `onSearch` callback fires with the current value.
3. **Given** optional action buttons (filter, export), **When** provided, **Then** they render alongside the search bar with consistent admin styling.

---

### User Story 6 - Shared sub-navigation (tab bar) (Priority: P3)

An administrator switches between sub-views on a page (e.g., Packages / Sections / Lessons / Videos tabs on the Content page, or All / Admins / Assistants / Students on the Users page). The tab bar component renders a horizontal list of rounded pill buttons, highlighting the active tab.

**Why this priority**: Tab filtering is used on at least two admin pages with identical styling. Extracting it ensures tabs are always visually consistent.

**Independent Test**: Can be tested by rendering the tab bar with sample tabs and verifying the active tab is highlighted and tab click fires the onSelect callback.

**Acceptance Scenarios**:

1. **Given** tab definitions with labels, **When** rendered, **Then** the active tab shows the primary gold background and inactive tabs show a soft background.
2. **Given** a tab is clicked, **When** the user selects it, **Then** the `onSelect` callback fires with the tab key.

---

### Edge Cases

- What happens when a page has no stat cards at all? The layout should render cleanly without the metrics section.
- What happens when a table column definition is empty? The table should render a single "no columns" notice.
- What happens when the modal receives no children? It should render the close button and an empty card body.
- What happens when the search toolbar is used without the filter button? Only the search input should render.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST provide a shared layout shell component that renders the admin sidebar, breadcrumb header, footer, and themed background for every admin page.
- **FR-002**: System MUST provide a shared admin data table component that accepts column definitions, row data, loading state, and empty-state message—and renders headers, rows, loading skeletons, and pagination.
- **FR-003**: System MUST provide a shared stat card component accepting icon, label, value, and variant (light/accent/muted).
- **FR-004**: System MUST provide a shared modal component with backdrop blur, entry/exit animation, and a close callback.
- **FR-005**: System MUST provide a shared search/filter toolbar component with search input, optional filter button, and optional action buttons.
- **FR-006**: System MUST provide a shared tab bar (sub-navigation) component accepting tab definitions and firing an onSelect callback.
- **FR-007**: All existing admin pages (Users, Content, Codes, Questions, Overrides) MUST be refactored to use the shared components, eliminating all duplicated layout, table, pagination, and modal markup.
- **FR-008**: The shared components MUST integrate with the existing `useAdminTheme` hook for light/dark mode support.
- **FR-009**: After refactoring, each admin page file MUST contain only page-specific business logic and data, not layout or UI chrome.

### Key Entities

- **AdminShellChrome**: Wraps every admin page with sidebar, breadcrumb, header, footer, and background theming. Already partially exists.
- **AdminDataTable**: Generic, typed table component with pagination, loading, and empty state.
- **AdminStatCard**: Metric display card with three visual variants.
- **AdminModal**: Animated overlay dialog container.
- **AdminSearchToolbar**: Search input + action button group.
- **AdminTabBar**: Sub-navigation pill buttons.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Every admin page (Users, Content, Codes, Questions, Overrides) uses the shared layout shell — zero pages contain inline sidebar, header, or footer markup.
- **SC-002**: The total lines of code across all admin page files is reduced by at least 40% compared to the pre-refactor state.
- **SC-003**: Adding a new admin page requires fewer than 50 lines of boilerplate (shell import + configuration props).
- **SC-004**: All admin pages render identically before and after the refactoring (visual regression test).
- **SC-005**: Theme toggle works consistently across all admin pages via the shared shell.

## Assumptions

- The existing `AdminShellChrome` component and `useAdminTheme` hook are the correct foundation. The refactoring extends them, not replaces them.
- The shared table component will use TypeScript generics for type-safe column/row definitions.
- Page-specific content (form fields, custom cells, domain logic) remains in individual page files.
- No backend API changes are required. This is a frontend-only refactoring effort.
- The `framer-motion` library will continue to be used for modal animations.
