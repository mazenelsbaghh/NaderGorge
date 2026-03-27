/**
 * Admin Design System
 *
 * Components: AdminShellChrome, AdminStatCard, AdminTabBar, AdminSearchToolbar, AdminModal, AdminDataTable
 * Theme Hook: useAdminTheme (CSS variables for --admin-* tokens)
 *
 * CSS Utilities (defined in globals.css — use as className):
 *   .admin-input       — Styled input/select/textarea
 *   .admin-btn-primary — Gold pill button (primary CTA)
 *   .admin-btn-ghost   — Outlined secondary button
 *   .admin-btn-icon    — Icon-only hover button
 *   .admin-panel       — Section card with backdrop blur
 *   .admin-badge       — Icon badge with primary-15 tint
 *   .admin-badge--pill — Pill-shaped badge variant
 */
// Shared Layout
export * from './AdminShellChrome';
export * from './useAdminTheme';

// Utils
export * from './admin-utils';

export * from './AdminStatCard';
export * from './AdminTabBar';
export * from './AdminSearchToolbar';
export * from './AdminModal';
export * from './AdminDataTable';
