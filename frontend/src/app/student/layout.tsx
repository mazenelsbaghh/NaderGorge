"use client";

/**
 * Student Layout — wraps all /student/* routes.
 *
 * Uses StudentShellChrome which mirrors AdminShellChrome exactly:
 * same dot-grid background, icon sidebar, bottom nav, theme toggle,
 * and --admin-* CSS variable tokens.
 */

import { useEffect } from "react";
import { StudentShellChrome } from "@/components/layout/StudentShellChrome";
import { StudentGuard } from "@/components/layout/StudentGuard";
import { StudentThemeProvider } from "@/hooks/useStudentTheme";
import { MaintenanceGuard } from "@/components/layout/MaintenanceGuard";

export default function StudentLayout({ children }: { children: React.ReactNode }) {
  useEffect(() => {
    document.documentElement.classList.add("admin-route-active");

    return () => {
      document.documentElement.classList.remove("admin-route-active");
    };
  }, []);

  return (
    <StudentThemeProvider>
      <StudentGuard>
        <MaintenanceGuard>
          <StudentShellChrome>{children}</StudentShellChrome>
        </MaintenanceGuard>
      </StudentGuard>
    </StudentThemeProvider>
  );
}
