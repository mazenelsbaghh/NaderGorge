"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { useAuthStore } from "@/stores/auth-store";
import { evaluateStaffAccess } from "@/hooks/useHasPermission";

function hasAssistantRole(roles: string[] | undefined) {
  return !!roles?.length && roles.some(r =>
    r.toLowerCase().includes("assistant") ||
    r.toLowerCase().includes("staff") ||
    r.toLowerCase().includes("admin") ||
    r.toLowerCase().includes("supervisor")
  );
}

export function AssistantGuard({ children }: { children: React.ReactNode }) {
  const router = useRouter();
  const { user, isAuthenticated, isLoading } = useAuthStore();

  const isAuthorized = user?.allowedDomains?.length
    ? user.allowedDomains.includes("all") || user.allowedDomains.includes("assistant")
    : hasAssistantRole(user?.roles) || evaluateStaffAccess(user);




  useEffect(() => {
    if (isLoading) return;

    if (!isAuthenticated) {
      router.replace("/login");
      return;
    }

    if (!isAuthorized) {
      router.replace("/login");
    }
  }, [isAuthenticated, isLoading, router, isAuthorized]);

  if (isLoading || !isAuthenticated || !isAuthorized) {
    return (
      <div
        dir="rtl"
        className="flex min-h-dvh items-center justify-center bg-[var(--admin-bg)] px-6 text-[var(--admin-text)]"
      >
        <div className="relative overflow-hidden rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-6 py-5 text-center shadow-sm">
          <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_80%_10%,var(--admin-primary-15),transparent_42%)]" />
          <p className="relative text-sm font-bold text-[var(--admin-muted)]">
            جارٍ التحقق من صلاحيات المساعد...
          </p>
        </div>
      </div>
    );
  }

  return <>{children}</>;
}
