"use client";

import { useEffect } from "react";
import { usePathname } from "next/navigation";
import { TeacherGuard } from "@/components/layout/TeacherGuard";
import { StaffRealtimeBoundary } from "@/components/layout/StaffRealtimeBoundary";
import {
  getTeacherShellDefaults,
  TeacherShellChrome,
} from "@/components/teacher/TeacherShellChrome";

export default function TeacherLayout({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const shell = getTeacherShellDefaults(pathname);

  useEffect(() => {
    document.documentElement.classList.add("admin-route-active");

    return () => {
      document.documentElement.classList.remove("admin-route-active");
    };
  }, []);

  return (
    <TeacherGuard>
      <TeacherShellChrome {...shell} persistentRoot>
        <StaffRealtimeBoundary>{children}</StaffRealtimeBoundary>
      </TeacherShellChrome>
    </TeacherGuard>
  );
}
