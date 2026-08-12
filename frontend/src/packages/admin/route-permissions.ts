import { hrAdminRoutePermissions } from '@/lib/hr-permissions';

import { adminAllNavigationRoutePermissions } from './navigation';

export type AdminPolicyUser = {
  roles?: string[];
  permissions?: string[];
  allowedNavbarItems?: string[];
  allowedDomains?: string[];
} | null | undefined;

export type AdminRouteRule = {
  pattern: string;
  permissions?: readonly string[];
  adminOnly?: boolean;
  match?: 'exact' | 'prefix';
};

const navigationRules: AdminRouteRule[] =
  adminAllNavigationRoutePermissions
    .map((item) => ({
      pattern: item.pattern,
      permissions: item.permission ? [item.permission] : [],
      adminOnly: 'adminOnly' in item ? item.adminOnly : undefined,
      match: 'prefix' as const,
    }))
    .sort((left, right) => right.pattern.length - left.pattern.length);

/**
 * One policy inventory owns both menu visibility and route guards. Put more
 * specific routes before their parent prefix.
 */
export const adminRouteRules: readonly AdminRouteRule[] = [
  {
    pattern: '/admin/content/video-types',
    permissions: [],
    adminOnly: true,
    match: 'prefix',
  },
  {
    pattern: '/admin/codes/templates',
    permissions: ['sales.templates.manage'],
    match: 'prefix',
  },
  {
    pattern: '/admin/content',
    permissions: ['content.manage', 'comments.manage'],
    match: 'prefix',
  },
  {
    pattern: '/admin/forms',
    permissions: ['content.manage'],
    match: 'prefix',
  },
  {
    pattern: '/admin/questions',
    permissions: ['exams.manage'],
    match: 'prefix',
  },
  {
    pattern: '/admin/watch-requests',
    permissions: ['watch_requests.manage', 'users.manage'],
    match: 'prefix',
  },
  {
    pattern: '/admin/hr',
    permissions: hrAdminRoutePermissions['/admin/hr'],
    match: 'prefix',
  },
  {
    pattern: '/admin/finance',
    permissions: hrAdminRoutePermissions['/admin/finance'],
    match: 'prefix',
  },
  {
    pattern: '/admin/teacher-finance',
    permissions: ['finance.manage'],
    match: 'prefix',
  },
  {
    pattern: '/admin/operations',
    permissions: ['users.manage'],
    match: 'prefix',
  },
  {
    pattern: '/admin/media',
    permissions: ['content.manage'],
    match: 'prefix',
  },
  {
    pattern: '/admin/settings',
    permissions: ['settings.manage'],
    match: 'prefix',
  },
  {
    pattern: '/admin/popup',
    permissions: ['settings.manage'],
    match: 'prefix',
  },
  {
    pattern: '/admin/users/:id',
    permissions: ['users.manage'],
  },
  {
    pattern: '/admin/teachers/:id',
    permissions: ['users.manage'],
  },
  {
    pattern: '/admin/assistants/:id',
    permissions: ['users.manage'],
  },
  {
    pattern: '/admin/codes/:groupId',
    permissions: ['codes.manage'],
  },
  {
    pattern: '/admin/gifts/:id',
    permissions: ['gifts.manage'],
  },
  {
    pattern: '/admin/public-exams/:id',
    permissions: ['public_exams.manage'],
  },
  ...navigationRules,
  { pattern: '/admin/unauthorized', permissions: [] },
  { pattern: '/admin', permissions: [] },
];

function normalizedRoles(user: AdminPolicyUser) {
  return new Set(user?.roles?.map((role) => role.toLowerCase()) ?? []);
}

export function isFullAdmin(user: AdminPolicyUser) {
  // Supervisors remain permission-scoped. Only the built-in Admin role bypasses
  // the policy matrix.
  return normalizedRoles(user).has('admin');
}

export function hasAdminSurface(user: AdminPolicyUser) {
  if (!user?.roles?.length) return false;
  if (!user.allowedDomains?.length) return true;
  return (
    user.allowedDomains.includes('all') ||
    user.allowedDomains.includes('admin')
  );
}

function splitPath(value: string) {
  return value.split('?')[0].split('/').filter(Boolean);
}

function matchExact(pattern: string, pathname: string) {
  const patternParts = splitPath(pattern);
  const pathParts = splitPath(pathname);
  if (patternParts.length !== pathParts.length) return false;
  return patternParts.every(
    (part, index) => part.startsWith(':') || part === pathParts[index]
  );
}

function matchPrefix(pattern: string, pathname: string) {
  return (
    pathname === pattern ||
    pathname.startsWith(pattern.endsWith('/') ? pattern : `${pattern}/`)
  );
}

export function matchAdminRoute(rule: AdminRouteRule, pathname: string) {
  return rule.match === 'prefix'
    ? matchPrefix(rule.pattern, pathname)
    : matchExact(rule.pattern, pathname);
}

function isNavbarAllowed(pathname: string, user: AdminPolicyUser) {
  const allowed = user?.allowedNavbarItems;
  if (!allowed?.length) return true;
  return allowed.some(
    (item) =>
      pathname === item ||
      pathname.startsWith(item.endsWith('/') ? item : `${item}/`)
  );
}

export function canAccessAdminRoute(
  pathname: string,
  user: AdminPolicyUser
) {
  if (!hasAdminSurface(user)) return false;
  if (pathname === '/admin/unauthorized') return true;
  if (isFullAdmin(user)) return true;
  if (!isNavbarAllowed(pathname, user)) return false;

  const rule = adminRouteRules.find((item) =>
    matchAdminRoute(item, pathname)
  );
  if (!rule || rule.adminOnly) return false;
  if (!rule.permissions?.length) return true;

  const permissions = new Set(user?.permissions ?? []);
  return rule.permissions.some((permission) => permissions.has(permission));
}
