import { adminNavigationRoutePermissions } from './navigation';

type AdminUser = {
  roles?: string[];
  permissions?: string[];
  allowedNavbarItems?: string[];
  allowedDomains?: string[];
} | null | undefined;

type AdminRouteRule = {
  pattern: string;
  permission?: string;
  adminOnly?: boolean;
};

export const adminRouteRules: AdminRouteRule[] = [
  { pattern: '/admin' },
  ...adminNavigationRoutePermissions,
  { pattern: '/admin/teachers/:id', permission: 'users.manage' },
  { pattern: '/admin/users', permission: 'users.manage' },
  { pattern: '/admin/users/:id', permission: 'users.manage' },
  { pattern: '/admin/assistants/:id', permission: 'users.manage' },
  { pattern: '/admin/codes/:groupId', permission: 'codes.manage' },
  { pattern: '/admin/codes/templates', permission: 'codes.manage' },
  { pattern: '/admin/gifts/new', permission: 'gifts.manage' },
  { pattern: '/admin/gifts/:id', permission: 'gifts.manage' },
  { pattern: '/admin/public-exams/:id', permission: 'public_exams.manage' },
  { pattern: '/admin/reports', permission: 'reports.manage' },
  { pattern: '/admin/hr', permission: 'users.manage' },
  { pattern: '/admin/hr/my-attendance', permission: 'users.manage' },
  { pattern: '/admin/operations', permission: 'users.manage' },
  { pattern: '/admin/media', permission: 'content.manage' },
  { pattern: '/admin/forms', permission: 'users.manage' },
  { pattern: '/admin/forms/new', permission: 'users.manage' },
  { pattern: '/admin/settings', adminOnly: true },
  { pattern: '/admin/watch-requests', permission: 'users.manage' },
  { pattern: '/admin/unauthorized' },
];

function isFullAdmin(user: AdminUser) {
  // Supervisors are deliberately permission-scoped administrators.  Only the
  // built-in Admin role may bypass the route matrix.
  return user?.roles?.some((role) => role.toLowerCase() === 'admin') ?? false;
}

export function hasAdminSurface(user: AdminUser) {
  if (!user?.roles?.length) return false;
  if (!user.allowedDomains?.length) return true;
  return user.allowedDomains.includes('all') || user.allowedDomains.includes('admin');
}

function matchRoute(pattern: string, pathname: string) {
  const patternParts = pattern.split('/').filter(Boolean);
  const pathParts = pathname.split('?')[0].split('/').filter(Boolean);
  if (patternParts.length !== pathParts.length) return false;

  return patternParts.every((part, index) => part.startsWith(':') || part === pathParts[index]);
}

export function canAccessAdminRoute(pathname: string, user: AdminUser) {
  if (!hasAdminSurface(user)) return false;
  if (isFullAdmin(user)) return true;

  const rule = adminRouteRules.find((item) => matchRoute(item.pattern, pathname));
  if (!rule) return false;
  if (rule.adminOnly) return false;
  if (!rule.permission) return true;

  return user?.permissions?.includes(rule.permission) ||
    user?.allowedNavbarItems?.some((item) => matchRoute(item, pathname)) ||
    false;
}
