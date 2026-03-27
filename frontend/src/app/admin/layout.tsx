"use client";

import { Shield, Users, BookOpen, KeyRound, Wrench } from "lucide-react";
import { usePathname } from "next/navigation";

import { AdminGuard } from "@/components/layout/AdminGuard";
import { Breadcrumbs } from "@/components/layout/Breadcrumbs";
import { Sidebar } from "@/components/layout/Sidebar";

const adminMenuItems = [
  { label: "المستخدمين", href: "/admin/users", icon: <Users className="h-4 w-4" /> },
  { label: "المحتوى", href: "/admin/content", icon: <BookOpen className="h-4 w-4" /> },
  { label: "أكواد الوصول", href: "/admin/codes", icon: <KeyRound className="h-4 w-4" /> },
  { label: "بنك الأسئلة", href: "/admin/questions", icon: <Shield className="h-4 w-4" /> },
  { label: "التعديلات", href: "/admin/overrides", icon: <Wrench className="h-4 w-4" /> },
];

export default function AdminLayout({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const usesStandaloneShell =
    pathname === "/admin" || pathname.startsWith("/admin/");

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
