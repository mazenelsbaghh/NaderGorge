"use client";

import type { ReactNode } from "react";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { GraduationCap } from "lucide-react";

import { cn } from "@/lib/utils";
import { GamificationWidget } from "@/components/student-dashboard/GamificationWidget";

interface SidebarItem {
  label: string;
  href: string;
  icon: ReactNode;
}

interface SidebarProps {
  items: SidebarItem[];
  title?: string;
}

export function Sidebar({ items, title }: SidebarProps) {
  const pathname = usePathname();
  const isTeacher = pathname.startsWith("/teacher");
  const isAdmin = pathname.startsWith("/admin");
  const areaLabel = isAdmin ? "ADMIN AREA" : isTeacher ? "TEACHER AREA" : "STUDENT AREA";

  return (
    <aside className="sticky top-0 hidden h-screen w-72 shrink-0 flex-col border-e border-[var(--admin-border)] bg-[var(--admin-sidebar)] px-4 py-5 backdrop-blur md:flex">
      {title && (
        <div className="mb-4 rounded-[28px] border border-[var(--admin-border)] bg-[var(--admin-card)] p-4 shadow-sm">
          <div className="flex items-center gap-3">
            <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-[var(--admin-primary-15)] text-[var(--admin-primary)]">
              <GraduationCap className="h-6 w-6" />
            </div>
            <div>
              <p className="text-xs font-black tracking-[0.26em] text-[var(--admin-primary)]">
                {areaLabel}
              </p>
              <h2 className="mt-1 text-lg font-black text-[var(--admin-text)]">{title}</h2>
            </div>
          </div>
        </div>
      )}

      <nav className="flex-1 space-y-2 rounded-[30px] border border-[var(--admin-border)] bg-[var(--admin-card)] p-3 shadow-sm">
        {items.map((item) => (
          <Link
            key={item.href}
            href={item.href}
            aria-current={pathname === item.href ? "page" : undefined}
            className={cn(
              "flex items-center gap-3 rounded-2xl px-4 py-3.5 text-sm font-bold transition-all",
              pathname === item.href
                ? "bg-gradient-to-r from-[var(--admin-primary)] to-[var(--admin-primary-strong)] text-[var(--admin-primary-contrast)] shadow-[0_4px_12px_var(--admin-shadow)]"
                : "text-[var(--admin-muted)] hover:bg-[var(--admin-hover)] hover:text-[var(--admin-text)]"
            )}
          >
            <span className={cn(
              "flex h-9 w-9 items-center justify-center rounded-xl transition-colors",
              pathname === item.href
                ? "bg-white/10 text-[var(--admin-primary-contrast)]"
                : "bg-[var(--admin-hover)] text-[var(--admin-muted)]"
            )}>
              {item.icon}
            </span>
            <span>{item.label}</span>
          </Link>
        ))}
      </nav>

      <GamificationWidget />
    </aside>
  );
}
