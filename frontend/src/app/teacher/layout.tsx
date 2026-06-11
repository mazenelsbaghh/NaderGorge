"use client";

import { useEffect } from "react";
import { TeacherGuard } from "@/components/layout/TeacherGuard";
import { StaffRealtimeBoundary } from "@/components/layout/StaffRealtimeBoundary";

export default function TeacherLayout({ children }: { children: React.ReactNode }) {
  useEffect(() => {
    document.documentElement.classList.add("admin-route-active");

    return () => {
      document.documentElement.classList.remove("admin-route-active");
    };
  }, []);

  return (
    <TeacherGuard>
      <StaffRealtimeBoundary>{children}</StaffRealtimeBoundary>
    </TeacherGuard>
  );
}
