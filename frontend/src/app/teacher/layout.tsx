"use client";

import { useEffect } from "react";
import { BookOpen, KeyRound, LayoutDashboard, Shield, MessageSquare, Coins, Users, GraduationCap, User } from "lucide-react";

import { TeacherGuard } from "@/components/layout/TeacherGuard";
import { Sidebar } from "@/components/layout/Sidebar";
import { Breadcrumbs } from "@/components/layout/Breadcrumbs";

const teacherMenuItems = [
  {
    label: "الرئيسية",
    href: "/teacher",
    icon: <LayoutDashboard className="h-4 w-4" />,
  },
  {
    label: "المحتوى الدراسي",
    href: "/teacher/packages",
    icon: <BookOpen className="h-4 w-4" />,
  },
  {
    label: "أكواد الوصول",
    href: "/teacher/codes",
    icon: <KeyRound className="h-4 w-4" />,
  },
  {
    label: "الاختبارات والأسئلة",
    href: "/teacher/exams",
    icon: <Shield className="h-4 w-4" />,
  },
  {
    label: "تصحيح المقالي",
    href: "/teacher/essays",
    icon: <GraduationCap className="h-4 w-4" />,
  },
  {
    label: "قائمة الطلاب",
    href: "/teacher/students",
    icon: <Users className="h-4 w-4" />,
  },
  {
    label: "المالية والأرباح",
    href: "/teacher/finance",
    icon: <Coins className="h-4 w-4" />,
  },
  {
    label: "الملف الشخصي",
    href: "/teacher/profile",
    icon: <User className="h-4 w-4" />,
  },
  {
    label: "التواصل الداخلي",
    href: "/teacher/chat",
    icon: <MessageSquare className="h-4 w-4" />,
  },
];

export default function TeacherLayout({ children }: { children: React.ReactNode }) {
  useEffect(() => {
    document.documentElement.classList.add("admin-route-active");

    return () => {
      document.documentElement.classList.remove("admin-route-active");
    };
  }, []);

  return (
    <TeacherGuard>
      <div className="flex" dir="rtl">
        <Sidebar items={teacherMenuItems} title="لوحة المعلم" />
        <div className="flex-1 min-h-screen bg-[var(--admin-bg)] text-[var(--admin-text)]">
          <Breadcrumbs />
          <main className="p-6">{children}</main>
        </div>
      </div>
    </TeacherGuard>
  );
}
