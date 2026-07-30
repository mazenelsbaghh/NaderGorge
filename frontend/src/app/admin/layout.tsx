"use client";

import { useEffect } from "react";
import { usePathname, useRouter } from "next/navigation";

import {
  AdminShellChrome,
  getAdminShellDefaults,
} from "@/components/admin/AdminShellChrome";
import { AdminGuard } from "@/components/layout/AdminGuard";
import { useAuthStore } from "@/stores/auth-store";
import { StaffRealtimeBoundary } from "@/components/layout/StaffRealtimeBoundary";
import { canAccessAdminRoute } from "@/packages/admin/route-permissions";

function PermissionGuard({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const router = useRouter();
  const { isLoading, isAuthenticated, user } = useAuthStore();
  const isAllowed = canAccessAdminRoute(pathname, user);

  useEffect(() => {
    if (isLoading || !isAuthenticated) return;

    if (!isAllowed) {
      router.replace("/admin/unauthorized");
    }
  }, [isAllowed, isAuthenticated, isLoading, router]);

  if (isLoading || !isAuthenticated) {
    return null;
  }

  if (!isAllowed) return null;

  return <>{children}</>;
}

export default function AdminLayout({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const shellDefaults = getAdminShellDefaults(pathname);

  useEffect(() => {
    document.documentElement.classList.add("admin-route-active");

    return () => {
      document.documentElement.classList.remove("admin-route-active");
    };
  }, []);

  return (
    <AdminGuard>
      <PermissionGuard>
        <AdminShellChrome {...shellDefaults} persistentRoot>
          <StaffRealtimeBoundary>{children}</StaffRealtimeBoundary>
        </AdminShellChrome>
      </PermissionGuard>
    </AdminGuard>
  );
}
