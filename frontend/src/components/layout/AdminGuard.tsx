"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";

import { useAuthStore } from "@/stores/auth-store";

function hasAdminAccess(roles: string[] | undefined) {
  return !!roles?.some((role) =>
    ["Admin", "Teacher", "Assistant"].includes(role),
  );
}

export function AdminGuard({ children }: { children: React.ReactNode }) {
  const router = useRouter();
  const { user, isAuthenticated, isLoading, loadFromStorage } = useAuthStore();

  useEffect(() => {
    loadFromStorage();
  }, [loadFromStorage]);

  useEffect(() => {
    if (isLoading) return;

    if (!isAuthenticated) {
      router.replace("/login");
      return;
    }

    if (!hasAdminAccess(user?.roles)) {
      router.replace("/login");
    }
  }, [isAuthenticated, isLoading, router, user?.roles]);

  if (isLoading || !isAuthenticated || !hasAdminAccess(user?.roles)) {
    return (
      <div className="flex min-h-[40vh] items-center justify-center px-6">
        <div className="landing-panel rounded-[28px] px-6 py-5 text-center">
          <p className="text-sm font-bold text-[var(--landing-muted)]">
            جارٍ التحقق من صلاحية الدخول...
          </p>
        </div>
      </div>
    );
  }

  return <>{children}</>;
}
