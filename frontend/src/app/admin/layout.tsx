"use client";

import { useEffect } from "react";
import { usePathname } from "next/navigation";

import { AdminGuard } from "@/components/layout/AdminGuard";
import { Breadcrumbs } from "@/components/layout/Breadcrumbs";
import { Sidebar } from "@/components/layout/Sidebar";
import { adminMenuItems } from "@/packages/admin";

export default function AdminLayout({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const usesStandaloneShell =
    pathname === "/admin" || pathname.startsWith("/admin/");

  useEffect(() => {
    document.documentElement.classList.add("admin-route-active");

    return () => {
      document.documentElement.classList.remove("admin-route-active");
    };
  }, []);

  return (
    <AdminGuard>
      {usesStandaloneShell ? (
        <main>{children}</main>
      ) : (
      <div className="flex">
        <Sidebar items={adminMenuItems} title="لوحة الإدارة" />
        <div className="flex-1">
          <Breadcrumbs />
          <main className="p-6">{children}</main>
        </div>
      </div>
      )}
    </AdminGuard>
  );
}
