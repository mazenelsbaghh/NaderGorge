"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { useAuthStore } from "@/stores/auth-store";

function hasTeacherAccess(roles: string[] | undefined) {
  return !!roles?.length && (roles.includes("Teacher") || roles.includes("Admin"));
}

export function TeacherGuard({ children }: { children: React.ReactNode }) {
  const router = useRouter();
  const { user, isAuthenticated, isLoading } = useAuthStore();

  useEffect(() => {
    if (isLoading) return;

    if (!isAuthenticated) {
      router.replace("/login");
      return;
    }

    if (!hasTeacherAccess(user?.roles)) {
      router.replace("/login");
    }
  }, [isAuthenticated, isLoading, router, user?.roles]);

  if (isLoading || !isAuthenticated || !hasTeacherAccess(user?.roles)) {
    return (
      <div
        dir="rtl"
        className="flex min-h-dvh items-center justify-center bg-[var(--admin-bg)] px-6 text-[var(--admin-text)]"
      >
        <div className="relative overflow-hidden rounded-[24px] border border-[var(--admin-border)] bg-[var(--admin-card)] px-6 py-5 text-center shadow-[0_18px_48px_var(--admin-shadow)]">
          <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_80%_10%,var(--admin-primary-15),transparent_42%)]" />
          <p className="relative text-sm font-bold text-[var(--admin-muted)]">
            جارٍ التحقق من صلاحيات المدرس...
          </p>
        </div>
      </div>
    );
  }

  return <>{children}</>;
}
