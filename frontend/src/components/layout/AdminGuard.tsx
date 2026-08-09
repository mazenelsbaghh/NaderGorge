"use client";

import { useEffect } from "react";
import { usePathname, useRouter } from "next/navigation";

import { canAccessAdminRoute, hasAdminSurface } from "@/packages/admin/route-permissions";
import { useAuthStore } from "@/stores/auth-store";

export function AdminGuard({ children }: { children: React.ReactNode }) {
  const router = useRouter();
  const pathname = usePathname();
  const { user, isAuthenticated, isLoading } = useAuthStore();

  const isWrongSurface = isAuthenticated && !hasAdminSurface(user);
  const isAuthorized = canAccessAdminRoute(pathname, user);

  useEffect(() => {
    if (isLoading) return;

    if (!isAuthenticated) {
      router.replace("/login");
      return;
    }

    if (isWrongSurface) {
      return;
    }

    if (!isAuthorized) {
      router.replace("/admin/unauthorized");
    }
  }, [isAuthenticated, isLoading, router, isAuthorized, isWrongSurface]);

  if (!isLoading && isWrongSurface) {
    return (
      <div
        dir="rtl"
        className="flex min-h-dvh items-center justify-center bg-[var(--admin-bg)] px-6 text-[var(--admin-text)]"
      >
        <div className="relative max-w-md overflow-hidden rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-6 py-5 text-center shadow-sm">
          <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_80%_10%,var(--admin-primary-15),transparent_42%)]" />
          <p className="relative text-base font-black text-[var(--admin-text)]">
            الصفحة غير موجودة أو لا تخص هذا الحساب
          </p>
        </div>
      </div>
    );
  }

  if (isLoading || !isAuthenticated || !isAuthorized) {
    return (
      <div
        dir="rtl"
        className="flex min-h-dvh items-center justify-center bg-[var(--admin-bg)] px-6 text-[var(--admin-text)]"
      >
        <div className="relative overflow-hidden rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-6 py-5 text-center shadow-sm">
          <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_80%_10%,var(--admin-primary-15),transparent_42%)]" />
          <p className="relative text-sm font-bold text-[var(--admin-muted)]">
            جارٍ التحقق من صلاحية الدخول...
          </p>
        </div>
      </div>
    );
  }

  return <>{children}</>;
}
