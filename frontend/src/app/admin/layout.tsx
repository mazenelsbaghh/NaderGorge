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
  { pattern: /^\/admin\/users(\/|$)/, permissions: ['users.manage'] },
  { pattern: /^\/admin\/teachers(\/|$)/, permissions: ['users.manage'] },
  { pattern: /^\/admin\/overrides(\/|$)/, permissions: ['users.manage'] },
  { pattern: /^\/admin\/content(\/|$)/, permissions: ['content.manage', 'comments.manage'] },
  { pattern: /^\/admin\/subjects(\/|$)/, permissions: ['content.manage'] },
  { pattern: /^\/admin\/forms(\/|$)/, permissions: ['content.manage'] },
  { pattern: /^\/admin\/community(\/|$)/, permissions: ['community.manage'] },
  { pattern: /^\/admin\/ai-monitor(\/|$)/, permissions: ['reports.manage'] },
  { pattern: /^\/admin\/codes(\/|$)/, permissions: ['codes.manage'] },
  { pattern: /^\/admin\/questions(\/|$)/, permissions: ['exams.manage'] },
  { pattern: /^\/admin\/watch-requests(\/|$)/, permissions: ['watch_requests.manage'] },
  { pattern: /^\/admin\/settings(\/|$)/, permissions: ['settings.manage'] },
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

    if (matchedRoute) {
      const hasAny = matchedRoute.permissions.some((p) => hasPermission(p));
      if (!hasAny) {
        router.replace("/admin/unauthorized");
      }
    }
  }, [pathname, isLoading, isAuthenticated, hasPermission, router]);

  if (isLoading || !isAuthenticated) {
    return null;
  }

  const isBypassed = pathname === "/admin" || pathname === "/admin/unauthorized";
  const matchedRoute = !isBypassed
    ? ROUTE_PERMISSIONS.find((route) => route.pattern.test(pathname))
    : null;

  if (matchedRoute) {
    const hasAny = matchedRoute.permissions.some((p) => hasPermission(p));
    if (!hasAny) {
      return null;
    }
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

  const filteredMenuItems = adminMenuItems.filter((item) => {
    if (item.href === '/admin/content') {
      return hasPermission('content.manage') || hasPermission('comments.manage');
    }
    return hasPermission(item.permission);
  });

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
