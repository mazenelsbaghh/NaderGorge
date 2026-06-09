"use client";

import { useEffect } from "react";
import { usePathname, useRouter } from "next/navigation";

import { AdminGuard } from "@/components/layout/AdminGuard";
import { Breadcrumbs } from "@/components/layout/Breadcrumbs";
import { Sidebar } from "@/components/layout/Sidebar";
import { adminMenuItems } from "@/packages/admin";
import { useHasPermission } from "@/hooks/useHasPermission";
import { useAuthStore } from "@/stores/auth-store";

const ROUTE_PERMISSIONS = [
  { pattern: /^\/admin\/users(\/|$)/, permission: 'users.manage' },
  { pattern: /^\/admin\/teachers(\/|$)/, permission: 'users.manage' },
  { pattern: /^\/admin\/overrides(\/|$)/, permission: 'users.manage' },
  { pattern: /^\/admin\/content(\/|$)/, permission: 'content.manage' },
  { pattern: /^\/admin\/subjects(\/|$)/, permission: 'content.manage' },
  { pattern: /^\/admin\/forms(\/|$)/, permission: 'content.manage' },
  { pattern: /^\/admin\/community(\/|$)/, permission: 'community.manage' },
  { pattern: /^\/admin\/ai-monitor(\/|$)/, permission: 'reports.manage' },
  { pattern: /^\/admin\/codes(\/|$)/, permission: 'codes.manage' },
  { pattern: /^\/admin\/questions(\/|$)/, permission: 'exams.manage' },
  { pattern: /^\/admin\/watch-requests(\/|$)/, permission: 'watch_requests.manage' },
  { pattern: /^\/admin\/settings(\/|$)/, permission: 'settings.manage' },
];

function PermissionGuard({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const router = useRouter();
  const { hasPermission } = useHasPermission();
  const { isLoading, isAuthenticated } = useAuthStore();

  useEffect(() => {
    if (isLoading || !isAuthenticated) return;

    const isBypassed = pathname === "/admin" || pathname === "/admin/unauthorized";
    if (isBypassed) return;

    const matchedRoute = ROUTE_PERMISSIONS.find((route) =>
      route.pattern.test(pathname)
    );

    if (matchedRoute && !hasPermission(matchedRoute.permission)) {
      router.replace("/admin/unauthorized");
    }
  }, [pathname, isLoading, isAuthenticated, hasPermission, router]);

  if (isLoading || !isAuthenticated) {
    return null;
  }

  const isBypassed = pathname === "/admin" || pathname === "/admin/unauthorized";
  const matchedRoute = !isBypassed
    ? ROUTE_PERMISSIONS.find((route) => route.pattern.test(pathname))
    : null;

  if (matchedRoute && !hasPermission(matchedRoute.permission)) {
    return null;
  }

  return <>{children}</>;
}

export default function AdminLayout({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const { hasPermission } = useHasPermission();
  const usesStandaloneShell =
    pathname === "/admin" || pathname.startsWith("/admin/");

  useEffect(() => {
    document.documentElement.classList.add("admin-route-active");

    return () => {
      document.documentElement.classList.remove("admin-route-active");
    };
  }, []);

  const filteredMenuItems = adminMenuItems.filter((item) =>
    hasPermission(item.permission)
  );

  return (
    <AdminGuard>
      <PermissionGuard>
        {usesStandaloneShell ? (
          <main>{children}</main>
        ) : (
          <div className="flex">
            <Sidebar items={filteredMenuItems} title="لوحة الإدارة" />
            <div className="flex-1">
              <Breadcrumbs />
              <main className="p-6">{children}</main>
            </div>
          </div>
        )}
      </PermissionGuard>
    </AdminGuard>
  );
}
