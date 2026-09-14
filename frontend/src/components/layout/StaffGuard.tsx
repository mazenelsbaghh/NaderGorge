"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { useAuthStore } from "@/stores/auth-store";
import { evaluateStaffAccess } from "@/hooks/useHasPermission";

export function StaffGuard({ children }: { children: React.ReactNode }) {
  const router = useRouter();
  const { user, isAuthenticated, isLoading } = useAuthStore();
  const hasAccess = evaluateStaffAccess(user);

  useEffect(() => {
    if (isLoading) return;

    if (!isAuthenticated) {
      router.replace("/login");
      return;
    }

    if (!hasAccess) {
      router.replace("/login");
    }
  }, [isAuthenticated, isLoading, router, hasAccess, user?.authorizationVersion]);

  if (isLoading || !isAuthenticated || !hasAccess) {
    return (
      <div
        dir="rtl"
        className="flex min-h-dvh items-center justify-center bg-[var(--admin-bg)] px-6 text-[var(--admin-text)]"
      >
        <div className="relative overflow-hidden rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-6 py-5 text-center shadow-sm">
          <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_80%_10%,var(--admin-primary-15),transparent_42%)]" />
          <p className="relative text-sm font-bold text-[var(--admin-muted)]">
            جارٍ التحقق من صلاحيات الموظف...
          </p>
        </div>
      </div>
    );
  }

  return <>{children}</>;
}
