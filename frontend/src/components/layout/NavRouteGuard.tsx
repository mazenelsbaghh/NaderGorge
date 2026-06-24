'use client';

import { useAuthStore } from '@/stores/auth-store';
import { useHasPermission } from '@/hooks/useHasPermission';
import { notFound } from 'next/navigation';
import { useEffect, useState } from 'react';

/**
 * NavRouteGuard — route-level guard that returns 404 when the current path
 * is not in the user's `allowedNavbarItems` list.
 *
 * Usage: wrap each assistant (or staff) page client component:
 *   <NavRouteGuard routePath="/assistant/tasks" permission="tasks.manage">
 *     <ActualPageContent />
 *   </NavRouteGuard>
 *
 * Rules:
 * - If `allowedNavbarItems` is empty/undefined → all routes are allowed (backwards compat).
 * - Admins bypass all checks.
 * - If a `permission` string is provided, it is also checked via `useHasPermission`.
 */
export function NavRouteGuard({
  routePath,
  permission,
  children,
}: {
  routePath: string;
  permission?: string;
  children: React.ReactNode;
}) {
  const user = useAuthStore((s) => s.user);
  const isLoading = useAuthStore((s) => s.isLoading);
  const { hasPermission } = useHasPermission();
  const [checked, setChecked] = useState(false);

  const roles = user?.roles || [];
  const isAdmin = roles.some(
    (r: string) => r.toLowerCase() === 'admin' || r.toLowerCase() === 'supervisor'
  );

  // Determine if user is allowed to view this route
  const allowedNavbarItems = user?.allowedNavbarItems;
  const hasNavAccess =
    isAdmin ||
    !allowedNavbarItems ||
    allowedNavbarItems.length === 0 ||
    allowedNavbarItems.some(
      (allowed: string) =>
        allowed === routePath || routePath.startsWith(allowed + '/')
    );

  const hasPermAccess = !permission || hasPermission(permission);
  const isAllowed = hasNavAccess && hasPermAccess;

  useEffect(() => {
    if (!isLoading) {
      setChecked(true);
    }
  }, [isLoading]);

  // While auth is loading, show nothing (the AssistantGuard already shows a loader)
  if (!checked || isLoading) {
    return null;
  }

  if (!isAllowed) {
    notFound();
  }

  return <>{children}</>;
}
