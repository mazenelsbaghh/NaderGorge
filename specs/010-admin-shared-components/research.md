# Research: Admin Shared Components Library

**Feature**: `010-admin-shared-components`
**Date**: 2026-03-26

## Research Summary

This feature is a frontend-only refactoring effort. No external technologies, APIs, or architectural decisions required research. All unknowns were resolved through direct codebase analysis.

---

## R-001: Existing Component Inventory

**Decision**: Extend `AdminShellChrome` as the foundation; do not replace.

**Rationale**: The component at `frontend/src/components/admin/AdminShellChrome.tsx` (184 lines) already implements:
- Sidebar with navigation links
- Breadcrumb header
- Footer ornament
- Theme integration via `useAdminTheme`
- Props: `activePath`, `sectionLabel`, `pageTitle`, `subtitle`, `action`, `headerAccessory`, `subNav`, `children`, `floatingAction`

The `codes` page already uses it. The `users` and `content` pages duplicate its markup inline. Extending > replacing.

**Alternatives considered**: Creating a `layout.tsx` Next.js layout component at `app/admin/layout.tsx` — rejected because the shell requires per-page props (pageTitle, sectionLabel, action) that a layout can't receive from child pages in Next.js App Router without additional context providers.

---

## R-002: Current Duplication Audit

**Decision**: Target 5 specific duplication zones for extraction.

**Findings** (lines per page estimated):

| Page | Total Lines | Shell Lines (sidebar/header/footer) | Table Lines | Modal Lines | Stats Lines |
|------|-------------|--------------------------------------|-------------|-------------|-------------|
| users/page.tsx | ~626 | ~180 | ~120 | ~70 | ~80 |
| content/page.tsx | ~1215 | ~180 | ~400 | ~110 | ~80 |
| codes/page.tsx | ~422 | 0 (uses Shell) | ~80 | ~70 | ~30 |
| questions/page.tsx | TBD | ~180 | ~100 | ~60 | ~30 |
| overrides/page.tsx | TBD | ~180 | ~100 | ~40 | ~30 |

**Total duplication to eliminate**: ~1,200+ lines across shell, ~800+ across tables/pagination.

---

## R-003: Table Component Design Pattern

**Decision**: Use TypeScript generics with a column-definition pattern for type-safe tables.

**Rationale**: The pattern `AdminDataTable<T>` with `columns: AdminColumn<T>[]` allows each page to define its own row type while reusing all rendering logic. This is the industry-standard approach used by libraries like TanStack Table, Ant Design, and Material UI.

**Interface Shape**:
```typescript
type AdminColumn<T> = {
  key: string;
  label: string;
  render: (row: T) => ReactNode;
  align?: 'right' | 'left' | 'center';
};

type AdminDataTableProps<T> = {
  columns: AdminColumn<T>[];
  data: T[];
  loading?: boolean;
  pageSize?: number;
  emptyMessage?: string;
  rowKey: (row: T) => string;
};
```

**Alternatives considered**:
1. Non-generic component with `any` data — rejected for type safety.
2. Render-prop-only component — rejected for more complex page code.

---

## R-004: Stat Card Variant System

**Decision**: Three named variants: `light`, `accent`, `muted`.

**Rationale**: Analysis of all admin pages shows exactly three visual variants used:
1. **Light**: Bordered card, `--admin-card-soft` bg, icon in tinted circle, dark text
2. **Accent**: Solid `#775a19` gold bg, white text, no border
3. **Muted**: Bordered card, `--admin-card-strong` bg, secondary text colors

Each page uses a row of 3 cards, typically: light + accent + muted.

---

## R-005: Modal Animation Consistency

**Decision**: Standard animation config: `initial={{ opacity: 0, scale: 0.96, y: 14 }}`, `animate={{ opacity: 1, scale: 1, y: 0 }}`.

**Rationale**: All existing modals across pages use identical Framer Motion animation values. Extracting these into a single component ensures zero animation drift.

---

## R-006: Theme Variable Coverage

**Decision**: All shared components must use `--admin-*` CSS custom properties exclusively — zero hard-coded colors.

**Rationale**: The `useAdminTheme` hook provides 15 CSS custom properties that cover all admin UI states. Some existing pages still have hard-coded light-theme colors (e.g., `#1c1c16`, `#7f7667`) that break in dark mode. The refactoring will fix these by replacing them with `var(--admin-text)` and `var(--admin-muted)` respectively.

**Full variable mapping**:
| Purpose | Light Value | Dark Value | Variable |
|---------|-------------|------------|----------|
| Background | `#fcf9ef` | `#0c0c0c` | `--admin-bg` |
| Text | `#1c1c16` | `#f4f1e7` | `--admin-text` |
| Muted text | `#7f7667` | `#d1c5b4` | `--admin-muted` |
| Primary | `#5d4300` | `#c5a059` | `--admin-primary` |
| Hover | `#e5e2d9` | `#2c261f` | `--admin-hover` |
| Card bg | `rgba(241,238,228,0.65)` | `rgba(30,30,30,0.45)` | `--admin-card` |
| Border | `rgba(255,255,255,0.3)` | `rgba(119,90,25,0.2)` | `--admin-border` |
| Shadow | `rgba(78,70,57,0.1)` | `rgba(0,0,0,0.5)` | `--admin-shadow` |
