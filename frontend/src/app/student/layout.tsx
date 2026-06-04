"use client";

/**
 * Student Layout — wraps all /student/* routes.
 *
 * Uses StudentShellChrome which mirrors AdminShellChrome exactly:
 * same dot-grid background, icon sidebar, bottom nav, theme toggle,
 * and --admin-* CSS variable tokens.
 */

import { StudentShellChrome } from "@/components/layout/StudentShellChrome";
import { StudentGuard } from "@/components/layout/StudentGuard";
import { StudentThemeProvider } from "@/hooks/useStudentTheme";

export default function StudentLayout({ children }: { children: React.ReactNode }) {
  return (
    <StudentThemeProvider>
      <StudentGuard>
        <StudentShellChrome>{children}</StudentShellChrome>
      </StudentGuard>
    </StudentThemeProvider>
  );
}
