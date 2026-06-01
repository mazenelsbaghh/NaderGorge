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
// Dashboard & Layout
export * from './AdminShellChrome';
export * from './useAdminTheme';
export * from './AdminStatCard';
export * from './AdminTabBar';
export * from './AdminPageSkeleton';
export * from './AdminBackButton';
export * from './AdminBreadcrumbs';
export * from './AdminSearchToolbar';
export * from './AdminModal';
export * from './AdminDataTable';
export * from './TermListManager';
export * from './AddTermForm';
export * from './PackageDetailsForm';
export * from './PackageCodeProfileForm';
export * from './LessonVideoList';
export * from './AddVideoForm';
export * from './LessonResourceList';
export * from './AddResourceForm';
export * from './LessonHomeworkList';
export * from './LessonCommentsModerationTab';
export * from './CommunityPostsModerationTable';
export * from './CommunityCommentsModerationTable';
export * from './UnifiedAssessmentBuilder';
export * from './LinkExamForm';
export * from './SectionListManager';
export * from './AddSectionForm';
export * from './LessonListManager';
export * from './AddLessonForm';
export * from '../ui/confirm-dialog';
export * from './AdminTeacherPhotoUpload';
export * from './ContentHierarchyPanel';
export * from './EntityOverviewDashboard';
export * from './AttachedExamViewer';
